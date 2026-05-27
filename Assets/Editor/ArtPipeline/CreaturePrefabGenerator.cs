#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;
using Voidborne.Fauna;
using Voidborne.NPCs;
using V2EnemyDef = Voidborne.Enemies.V2.EnemyDefinition;
using V2EnemyRegistry = Voidborne.Enemies.V2.EnemyRegistry;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.5 — Creature Prefab Generator.
    ///
    /// <c>[MenuItem("Voidborne/Generate/Creature Prefabs")]</c> entry point that
    /// composes a placeholder prefab + icon for every creature SO in the three
    /// registries (Fauna, V2 Enemy, NPC). Mirrors V3.3's
    /// <c>BulkPrefabGenerator</c> in shape but consumes the V3.5
    /// <see cref="CreatureVisualRecipeMapper"/> and writes into
    /// Assets/Prefabs/{Fauna,Enemies,NPCs}/ rather than Assets/Prefabs/Items/.
    ///
    /// AI MonoBehaviours (FaunaAi, VordFodderAi, NpcBase) belong to V14/V15/V16
    /// and don't exist yet — the generator probes the loaded assemblies and
    /// attaches them when they happen to be present, otherwise just adds a
    /// <see cref="CharacterController"/> to the root. Same reflective pattern
    /// V3.3 used for MachineRuntime / PlacedBlock.
    ///
    /// Two-phase to play nicely with <c>AssetDatabase.StartAssetEditing</c>:
    ///  1. Build prefabs + render PNG icons inside the asset-editing batch.
    ///  2. After StopAssetEditing closes the batch, reload each Sprite and
    ///     assign it onto the matching SO's "icon" field (when present).
    ///
    /// Idempotent: re-running overwrites the prefabs at their canonical paths
    /// and the SerializedObject writes only when the reference changes.
    /// </summary>
    public static class CreaturePrefabGenerator
    {
        public const string FaunaPrefabFolder = "Assets/Prefabs/Fauna";
        public const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
        public const string NpcPrefabFolder = "Assets/Prefabs/NPCs";
        public const string IconFolder = "Assets/Textures/Icons";

        // Reflection cache for the V14/V15/V16 AI stub types — see ProbeAiStubTypes.
        private static Type _faunaAiType;
        private static Type _vordFodderAiType;
        private static Type _npcBaseType;
        private static bool _stubsProbed;

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        [MenuItem("Voidborne/Generate/Creature Prefabs")]
        public static void RunMenu() => Run();

        /// <summary>
        /// Test-callable entry point. Iterates the three registries, composes a
        /// placeholder prefab for each definition, renders a PNG icon, and
        /// writes prefab + icon references back onto the SO via
        /// SerializedObject. Returns the number of creature prefabs generated.
        /// </summary>
        public static int Run()
        {
            var faunaReg = Resources.Load<FaunaRegistry>("FaunaRegistry");
            var enemyReg = Resources.Load<V2EnemyRegistry>("EnemyRegistry");
            var npcReg = Resources.Load<NpcRegistry>("NpcRegistry");

            if (faunaReg == null) Debug.LogWarning("[CreaturePrefabGenerator] No FaunaRegistry in Resources.");
            if (enemyReg == null) Debug.LogWarning("[CreaturePrefabGenerator] No EnemyRegistry (V2) in Resources.");
            if (npcReg == null) Debug.LogWarning("[CreaturePrefabGenerator] No NpcRegistry in Resources.");

            ProbeAiStubTypes();
            EnsureFolder(FaunaPrefabFolder);
            EnsureFolder(EnemyPrefabFolder);
            EnsureFolder(NpcPrefabFolder);
            EnsureFolder(IconFolder);

            var pendingIcons = new List<PendingIcon>(16);
            int faunaCount = 0;
            int enemyCount = 0;
            int npcCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                if (faunaReg != null)
                {
                    foreach (var def in faunaReg.AllFauna)
                    {
                        if (def == null || string.IsNullOrEmpty(def.id)) continue;
                        var recipe = CreatureVisualRecipeMapper.MapFauna(def);
                        var prefab = BuildAndSavePrefab(def.id, recipe, FaunaPrefabFolder,
                            CreatureKind.Fauna);
                        if (prefab != null)
                        {
                            AssignPrefabField(def, "prefab", prefab);
                            faunaCount++;
                            RenderIconForPrefab(prefab, def.id, pendingIcons, def);
                        }
                    }
                }

                if (enemyReg != null)
                {
                    foreach (var def in enemyReg.AllEnemies)
                    {
                        if (def == null || string.IsNullOrEmpty(def.id)) continue;
                        var recipe = CreatureVisualRecipeMapper.MapEnemy(def);
                        var prefab = BuildAndSavePrefab(def.id, recipe, EnemyPrefabFolder,
                            CreatureKind.Enemy);
                        if (prefab != null)
                        {
                            AssignPrefabField(def, "prefab", prefab);
                            enemyCount++;
                            RenderIconForPrefab(prefab, def.id, pendingIcons, def);
                        }
                    }
                }

                if (npcReg != null)
                {
                    foreach (var def in npcReg.AllNpcs)
                    {
                        if (def == null || string.IsNullOrEmpty(def.id)) continue;
                        var recipe = CreatureVisualRecipeMapper.MapNpc(def);
                        var prefab = BuildAndSavePrefab(def.id, recipe, NpcPrefabFolder,
                            CreatureKind.Npc);
                        if (prefab != null)
                        {
                            AssignPrefabField(def, "prefab", prefab);
                            npcCount++;
                            RenderIconForPrefab(prefab, def.id, pendingIcons, def);
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Phase 2 — the icon imports are now finalised. Reload each Sprite
            // and write it onto the SO's "icon" field IF that field exists
            // (FaunaDefinition currently has no icon field; only Enemy/NPC may).
            AssetDatabase.Refresh();

            int iconsAssigned = 0;
            int iconsRendered = pendingIcons.Count;
            foreach (var pending in pendingIcons)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pending.assetPath);
                if (sprite == null) continue;
                if (TryAssignIconField(pending.so, sprite)) iconsAssigned++;
            }

            AssetDatabase.SaveAssets();

            int total = faunaCount + enemyCount + npcCount;
            Debug.Log(
                $"[CreaturePrefabGenerator] Generated {total} creature prefabs " +
                $"({faunaCount} fauna + {enemyCount} enemies + {npcCount} NPCs) + " +
                $"{iconsRendered} icons (assigned {iconsAssigned} to SOs).");

            return total;
        }

        // -------------------------------------------------------------------
        // Internals
        // -------------------------------------------------------------------

        private enum CreatureKind { Fauna, Enemy, Npc }

        private struct PendingIcon
        {
            public string assetPath;
            public UnityEngine.Object so;
        }

        /// <summary>
        /// Compose a recipe into a GameObject hierarchy and save it as a prefab
        /// at <paramref name="folder"/>/<paramref name="id"/>.prefab. Adds a
        /// CharacterController to the root and probes for the V14/V15/V16 AI
        /// stubs reflectively.
        /// </summary>
        private static GameObject BuildAndSavePrefab(string id, ItemVisualRecipe recipe,
            string folder, CreatureKind kind)
        {
            if (string.IsNullOrEmpty(id) || recipe == null) return null;

            // Use ItemModelComposer's layer composition convention so the
            // material magenta canary fires identically. We can't call
            // ItemModelComposer.Build because that expects an ItemDefinition;
            // duplicate the layer-instantiate loop here.
            var root = new GameObject(id);
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
                    child.transform.localRotation = (layer.localRot.x == 0f &&
                                                     layer.localRot.y == 0f &&
                                                     layer.localRot.z == 0f &&
                                                     layer.localRot.w == 0f)
                        ? Quaternion.identity
                        : layer.localRot;

                    var mf = child.AddComponent<MeshFilter>();
                    mf.sharedMesh = PrimitiveMeshFactory.GetOrCreate(layer.shape);

                    var mr = child.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = ItemModelComposer.LoadMaterialOrFallback(layer.materialKey);
                }
            }

            // CharacterController — frame the controller around the combined
            // mesh bounds so the prefab can move under physics out of the box.
            AttachCharacterController(root, recipe, kind);

            // Reflective AI stub probe — attach if the V14/V15/V16 types
            // happen to exist already; otherwise just skip.
            AttachAiStubIfAvailable(root, kind);

            GameObject prefab = null;
            try
            {
                string path = $"{folder}/{id}.prefab";
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok)
                {
                    Debug.LogError($"[CreaturePrefabGenerator] Failed to save prefab at {path}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            return prefab;
        }

        private static void AttachCharacterController(GameObject root, ItemVisualRecipe recipe,
            CreatureKind kind)
        {
            var cc = root.AddComponent<CharacterController>();

            // Default humanoid controller. Quadruped / boss overrides below
            // widen the radius and shorten the height to fit a four-legged
            // silhouette better.
            cc.center = new Vector3(0f, 1f, 0f);
            cc.radius = 0.3f;
            cc.height = 1.8f;

            if (recipe == null || recipe.layers == null || recipe.layers.Length == 0) return;

            // Compute a rough AABB from the recipe layers so the controller
            // tracks the actual silhouette extents (matters for bosses where
            // the 2x scale would otherwise leave the controller too small).
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var layer in recipe.layers)
            {
                Vector3 half = 0.5f * new Vector3(
                    Mathf.Abs(layer.localScale.x),
                    Mathf.Abs(layer.localScale.y),
                    Mathf.Abs(layer.localScale.z));
                Vector3 lo = layer.localPos - half;
                Vector3 hi = layer.localPos + half;
                min = Vector3.Min(min, lo);
                max = Vector3.Max(max, hi);
            }
            Vector3 size = max - min;
            Vector3 center = (max + min) * 0.5f;

            float height = Mathf.Max(0.2f, size.y);
            float radius = Mathf.Max(0.1f, 0.5f * Mathf.Max(size.x, size.z));
            cc.center = new Vector3(center.x, Mathf.Max(center.y, height * 0.5f), center.z);
            cc.radius = radius;
            cc.height = height;
        }

        // -------------------------------------------------------------------
        // Icon rendering
        // -------------------------------------------------------------------

        private static void RenderIconForPrefab(GameObject prefab, string creatureId,
            List<PendingIcon> pending, UnityEngine.Object so)
        {
            if (prefab == null || string.IsNullOrEmpty(creatureId)) return;
            string iconId = $"creature_{creatureId}";

            try
            {
                IconRenderer.RenderIcon(prefab, iconId, 256);
                pending.Add(new PendingIcon
                {
                    assetPath = $"{IconFolder}/{iconId}.png",
                    so = so
                });
            }
            catch (Exception ex)
            {
                // IconRenderer cleans up its own instances; we just want to log
                // and continue with the rest of the bulk pass.
                Debug.LogError(
                    $"[CreaturePrefabGenerator] Icon render failed for '{creatureId}': {ex.Message}");
            }
        }

        // -------------------------------------------------------------------
        // SerializedObject helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Write <paramref name="prefab"/> into the named GameObject field on
        /// <paramref name="so"/> via SerializedObject. No-op if the existing
        /// reference already matches.
        /// </summary>
        private static void AssignPrefabField(UnityEngine.Object so, string fieldName, GameObject prefab)
        {
            if (so == null || prefab == null) return;
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError(
                    $"[CreaturePrefabGenerator] '{so.name}' has no serialized field '{fieldName}'.",
                    so);
                return;
            }
            if (prop.objectReferenceValue == prefab) return;
            prop.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Best-effort write of <paramref name="sprite"/> into the SO's "icon"
        /// field, IF the SO has such a field. Returns true on success, false
        /// when the field is absent (no error logged — FaunaDefinition
        /// intentionally lacks one as of V3.5).
        /// </summary>
        private static bool TryAssignIconField(UnityEngine.Object so, Sprite sprite)
        {
            if (so == null || sprite == null) return false;
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("icon");
            if (prop == null) return false;
            if (prop.objectReferenceValue == sprite) return true;
            prop.objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // -------------------------------------------------------------------
        // Reflective AI stub probe
        // -------------------------------------------------------------------

        private static void AttachAiStubIfAvailable(GameObject go, CreatureKind kind)
        {
            ProbeAiStubTypes();

            Type t = null;
            switch (kind)
            {
                case CreatureKind.Fauna: t = _faunaAiType; break;
                case CreatureKind.Enemy: t = _vordFodderAiType; break;
                case CreatureKind.Npc: t = _npcBaseType; break;
            }
            if (t != null) go.AddComponent(t);
        }

        private static void ProbeAiStubTypes()
        {
            if (_stubsProbed) return;
            _stubsProbed = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_faunaAiType != null && _vordFodderAiType != null && _npcBaseType != null) break;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                    // Match the spec's fully-qualified names so we don't pick
                    // up a same-name MonoBehaviour from an unrelated namespace.
                    string fn = t.FullName;
                    if (_faunaAiType == null && fn == "Voidborne.Fauna.FaunaAi") _faunaAiType = t;
                    else if (_vordFodderAiType == null && fn == "Voidborne.Enemies.V2.VordFodderAi") _vordFodderAiType = t;
                    else if (_npcBaseType == null && fn == "Voidborne.NPCs.NpcBase") _npcBaseType = t;
                }
            }
        }

        // -------------------------------------------------------------------
        // Folder helper
        // -------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            string acc = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{acc}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(acc, parts[i]);
                }
                acc = next;
            }

            // Also ensure the on-disk directory exists in case the Asset
            // Database is still catching up (Icons folder is created via
            // File.WriteAllBytes by IconRenderer; this guards against the
            // initial folder-not-yet-imported case).
            string abs = Path.GetFullPath(assetPath);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }
    }
}
#endif
