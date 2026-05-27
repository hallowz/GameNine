#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.3 — Item Model Composer.
    ///
    /// Editor-only tool that turns an <see cref="ItemVisualRecipe"/> into a
    /// GameObject hierarchy and writes it to disk as a prefab under
    /// <c>Assets/Prefabs/Items/</c>. The MonoBehaviour stubs the spec calls
    /// for (<c>MachineRuntime</c>, <c>PlacedBlock</c>) do not exist in the
    /// codebase yet — the composer guards their attachment with reflection
    /// lookups so the prefab still lands when those types are missing.
    ///
    /// Idempotent: re-running overwrites the prefab at the canonical path.
    /// Magenta material fallback surfaces missing palette assets loudly.
    /// </summary>
    public static class ItemModelComposer
    {
        public const string PrefabsFolder = "Assets/Prefabs/Items";

        private const string FallbackShader = "Universal Render Pipeline/Lit";

        // Reflection cache for the optional gameplay stub types — see
        // BuildAndSaveAll for the rationale.
        private static Type _machineRuntimeType;
        private static Type _placedBlockType;
        private static bool _stubTypesProbed;

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Instantiate a GameObject hierarchy implementing
        /// <paramref name="recipe"/>: a root named after <paramref name="item"/>'s
        /// id plus one MeshRenderer child per layer. The returned object lives
        /// in the active scene — caller is responsible for saving as prefab
        /// or destroying it.
        /// </summary>
        public static GameObject Build(ItemDefinition item, ItemVisualRecipe recipe, bool asPlacedPrefab)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            string rootName = string.IsNullOrEmpty(item.itemId) ? "UnnamedItem" : item.itemId;
            if (asPlacedPrefab) rootName += "_placed";

            var root = new GameObject(rootName);

            // One child per layer. Layers carry localPos/localScale/localRot
            // relative to the root.
            if (recipe.layers != null)
            {
                for (int i = 0; i < recipe.layers.Length; i++)
                {
                    var layer = recipe.layers[i];
                    var child = new GameObject($"L{i}_{layer.shape}");
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = layer.localPos;
                    child.transform.localScale = layer.localScale == Vector3.zero
                        ? Vector3.one
                        : layer.localScale;
                    // Quaternion default (0,0,0,0) is invalid for transforms —
                    // sub in identity when the layer was authored without a
                    // rotation.
                    child.transform.localRotation = (layer.localRot.x == 0f &&
                                                     layer.localRot.y == 0f &&
                                                     layer.localRot.z == 0f &&
                                                     layer.localRot.w == 0f)
                        ? Quaternion.identity
                        : layer.localRot;

                    var mf = child.AddComponent<MeshFilter>();
                    mf.sharedMesh = PrimitiveMeshFactory.GetOrCreate(layer.shape);

                    var mr = child.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = LoadMaterialOrFallback(layer.materialKey);
                }
            }

            return root;
        }

        /// <summary>
        /// Save <paramref name="go"/> as a prefab at the canonical path for
        /// <paramref name="itemId"/>. Overwrites the existing prefab if any.
        /// Returns the on-disk prefab reference.
        /// </summary>
        public static GameObject SaveAsPrefab(GameObject go, string itemId, bool isPlaced)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("itemId required", nameof(itemId));

            EnsureFolder(PrefabsFolder);
            string suffix = isPlaced ? "_placed" : string.Empty;
            string path = $"{PrefabsFolder}/{itemId}{suffix}.prefab";

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            if (!success)
            {
                Debug.LogError($"[ItemModelComposer] Failed to save prefab at {path}.");
            }
            return prefab;
        }

        /// <summary>
        /// Build the world-drop prefab (and, for machines/blocks, the placed
        /// prefab too). Cleans up the scene-side instances after saving so
        /// the active scene stays clean. Returns
        /// (worldDropPrefab, placedPrefab) — placed is null for items that
        /// don't get a placed variant.
        /// </summary>
        public static (GameObject world, GameObject placed) BuildAndSaveAll(ItemDefinition item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var recipe = ItemVisualRecipeMapper.MapItem(item);

            // World drop prefab.
            var worldGo = Build(item, recipe, asPlacedPrefab: false);
            GameObject worldPrefab = null;
            try
            {
                worldPrefab = SaveAsPrefab(worldGo, item.itemId, isPlaced: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldGo);
            }

            // Placed variant — only for machines and build blocks.
            GameObject placedPrefab = null;
            bool needsPlaced = item.kind == ItemKind.Machine ||
                               (item.kind == ItemKind.Product && item.isBuildBlock);
            if (needsPlaced)
            {
                var placedGo = Build(item, recipe, asPlacedPrefab: true);
                try
                {
                    // BoxCollider so blocks/machines have physical presence
                    // on placement — the gameplay code (later milestones)
                    // expects this.
                    placedGo.AddComponent<BoxCollider>();
                    AttachGameplayStubIfAvailable(placedGo, item);
                    placedPrefab = SaveAsPrefab(placedGo, item.itemId, isPlaced: true);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(placedGo);
                }
            }

            return (worldPrefab, placedPrefab);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Load <c>Mat_{materialKey}.mat</c> via the canonical PaletteRegistry
        /// path. Falls back to an in-memory magenta material so missing
        /// assets are visually obvious.
        /// </summary>
        public static Material LoadMaterialOrFallback(string materialKey)
        {
            if (!string.IsNullOrEmpty(materialKey))
            {
                string path = PaletteRegistry.AssetPathFor(materialKey);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
            }

            Debug.LogWarning(
                $"[ItemModelComposer] Material '{materialKey}' missing at " +
                $"{PaletteRegistry.AssetPathFor(materialKey ?? string.Empty)} — using magenta fallback.");

            var shader = Shader.Find(FallbackShader);
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            var fallback = new Material(shader) { color = Color.magenta };
            fallback.name = "Mat_Fallback_Magenta";
            return fallback;
        }

        /// <summary>
        /// Attach the appropriate gameplay stub component (<c>MachineRuntime</c>
        /// for machines, <c>PlacedBlock</c> for blocks) to the placed-variant
        /// root — but only if the type exists in the loaded assemblies. The
        /// spec deliberately allows skipping these when the type isn't there
        /// yet (M2 only needs the prefab; gameplay wiring lands later).
        /// </summary>
        private static void AttachGameplayStubIfAvailable(GameObject go, ItemDefinition item)
        {
            ProbeStubTypes();

            if (item.kind == ItemKind.Machine && _machineRuntimeType != null)
            {
                go.AddComponent(_machineRuntimeType);
                return;
            }

            if (item.kind == ItemKind.Product && item.isBuildBlock && _placedBlockType != null)
            {
                go.AddComponent(_placedBlockType);
            }
        }

        private static void ProbeStubTypes()
        {
            if (_stubTypesProbed) return;
            _stubTypesProbed = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_machineRuntimeType != null && _placedBlockType != null) break;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

                    if (_machineRuntimeType == null && t.Name == "MachineRuntime")
                    {
                        _machineRuntimeType = t;
                    }
                    else if (_placedBlockType == null && t.Name == "PlacedBlock")
                    {
                        _placedBlockType = t;
                    }
                }
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            string acc = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{acc}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(acc, parts[i]);
                }
                acc = next;
            }
        }
    }
}
#endif
