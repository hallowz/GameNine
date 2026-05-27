#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.4 — Icon Renderer.
    ///
    /// Off-screen renderer that snapshots an item prefab into a square PNG
    /// (default 256x256), saves it under <c>Assets/Textures/Icons/{id}.png</c>,
    /// imports it as a Sprite, and returns the loaded Sprite asset.
    ///
    /// The renderer creates a hidden Camera + Directional Light, instantiates
    /// the prefab on a dedicated layer (31 by default, the first unused layer
    /// in this project) so the scene's existing geometry never leaks into the
    /// render, frames the camera tight to the combined Renderer bounds, and
    /// writes the resulting pixels to disk.
    ///
    /// Idempotent: re-running overwrites the PNG at the same path. Try/finally
    /// guarantees cleanup of the RenderTexture + temp scene objects even if
    /// rendering throws.
    /// </summary>
    public static class IconRenderer
    {
        // Output folder convention; created on first use if missing.
        private const string IconFolder = "Assets/Textures/Icons";

        // Layer 31 is unused in this project's TagManager — see ProjectSettings.
        // We fall through to a small set of fallbacks if a future change occupies it.
        private static readonly int[] CandidateLayers = { 31, 30, 29, 28 };

        /// <summary>
        /// Render <paramref name="prefab"/> into a square sprite saved as
        /// <c>Assets/Textures/Icons/{itemId}.png</c>, return the imported Sprite.
        /// Returns null when <paramref name="prefab"/> is null or has no
        /// <see cref="Renderer"/> components to frame.
        /// </summary>
        public static Sprite RenderIcon(GameObject prefab, string itemId, int resolution = 256)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[IconRenderer] Null prefab for '{itemId}'. Skipping.");
                return null;
            }
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogError("[IconRenderer] Empty itemId. Skipping.");
                return null;
            }
            if (resolution < 16) resolution = 16;

            EnsureFolder(IconFolder);

            int iconLayer = PickIconLayer();

            GameObject instance = null;
            GameObject camGo = null;
            GameObject lightGo = null;
            RenderTexture rt = null;
            Texture2D readback = null;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                // Spawn the prefab far from origin so any in-scene geometry
                // (which we further mask out via cullingMask) can't pollute
                // the framing. HideFlags keep it out of the Hierarchy.
                Vector3 spawnPos = new Vector3(10000f, 10000f, 10000f);
                instance = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
                instance.hideFlags = HideFlags.HideAndDontSave;
                SetLayerRecursively(instance, iconLayer);

                // Compute combined Renderer bounds in WORLD space.
                if (!TryGetCombinedBounds(instance, out Bounds bounds))
                {
                    Debug.LogWarning(
                        $"[IconRenderer] Prefab '{itemId}' has no Renderers to frame. Skipping.");
                    return null;
                }

                // ---- Camera ---------------------------------------------------
                camGo = new GameObject("IconRendererCamera");
                camGo.hideFlags = HideFlags.HideAndDontSave;
                camGo.layer = iconLayer;
                var cam = camGo.AddComponent<Camera>();

                // URP-friendly setup: disable shadows + post so we get a clean,
                // deterministic readback that doesn't depend on global URP settings.
                var urpCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
                urpCamData.renderShadows = false;
                urpCamData.renderPostProcessing = false;
                urpCamData.antialiasing = AntialiasingMode.None;

                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << iconLayer;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = bounds.size.magnitude * 8f + 10f;
                cam.allowMSAA = true;
                cam.allowHDR = false;
                cam.enabled = false; // manual Render()

                // ---- Light ----------------------------------------------------
                // Per-camera light by setting culling mask to the icon layer.
                lightGo = new GameObject("IconRendererLight");
                lightGo.hideFlags = HideFlags.HideAndDontSave;
                lightGo.layer = iconLayer;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1.0f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << iconLayer;
                lightGo.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
                lightGo.transform.position = bounds.center + new Vector3(0f, 5f, 0f);

                // ---- Frame the camera at a 3/4 angle -------------------------
                // Bounds extents drive the orthographic size: fit the largest
                // axis with a small padding so the silhouette occupies ~80% of
                // the icon (the spec asks for tight fit + small padding).
                float maxExtent = Mathf.Max(
                    bounds.extents.x,
                    Mathf.Max(bounds.extents.y, bounds.extents.z));
                // The diagonal projection at a 3/4 angle stretches X/Z; use the
                // diagonal of the XZ footprint together with Y so the full
                // silhouette never clips.
                float xzDiagonal = Mathf.Sqrt(
                    bounds.size.x * bounds.size.x +
                    bounds.size.z * bounds.size.z) * 0.5f;
                float framedExtent = Mathf.Max(maxExtent, xzDiagonal, bounds.extents.y);
                if (framedExtent <= 0.0001f) framedExtent = 0.5f;
                cam.orthographicSize = framedExtent * 1.25f; // ~80% fill = 1.0/0.8

                Vector3 dir = new Vector3(1f, 1f, -1f).normalized;
                float camDist = bounds.size.magnitude * 2f + 2f;
                cam.transform.position = bounds.center + dir * camDist;
                cam.transform.LookAt(bounds.center, Vector3.up);

                // ---- RenderTexture -------------------------------------------
                var desc = new RenderTextureDescriptor(resolution, resolution,
                    RenderTextureFormat.ARGB32, 24);
                desc.sRGB = true;
                desc.msaaSamples = SystemInfo.supportsMultisampleAutoResolve ? 4 : 1;
                rt = new RenderTexture(desc);
                rt.antiAliasing = desc.msaaSamples;
                rt.Create();
                cam.targetTexture = rt;

                // ---- Render ---------------------------------------------------
                cam.Render();

                // ---- Read pixels ---------------------------------------------
                RenderTexture.active = rt;
                readback = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false);
                readback.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0);
                readback.Apply();

                // ---- Save PNG -------------------------------------------------
                byte[] png = readback.EncodeToPNG();
                string assetPath = $"{IconFolder}/{itemId}.png";
                string absPath = Path.GetFullPath(assetPath);
                File.WriteAllBytes(absPath, png);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ApplySpriteImportSettings(assetPath, resolution);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                // NOTE: when called inside an AssetDatabase.StartAssetEditing
                // batch (e.g. IconBulkRenderer), the import is deferred and
                // LoadAssetAtPath<Sprite> will return null until the batch
                // closes. Callers in that path must reload after StopAssetEditing.
                return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
            finally
            {
                // Cleanup in reverse order. RenderTexture.active first so we
                // never leave a destroyed RT bound.
                RenderTexture.active = prevActive;

                if (readback != null) Object.DestroyImmediate(readback);

                if (camGo != null)
                {
                    var cam = camGo.GetComponent<Camera>();
                    if (cam != null) cam.targetTexture = null;
                }

                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                if (lightGo != null) Object.DestroyImmediate(lightGo);
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static void EnsureFolder(string assetFolder)
        {
            string abs = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(abs))
            {
                Directory.CreateDirectory(abs);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Picks the first candidate layer that's empty in TagManager.asset.
        /// Layer 31 is empty in this project today; fallbacks exist in case a
        /// future change occupies it. If none of the candidates are empty we
        /// still return the first candidate — the cullingMask will just include
        /// whatever else lives there (low risk for editor-only one-shot renders).
        /// </summary>
        private static int PickIconLayer()
        {
            foreach (int layer in CandidateLayers)
            {
                string name = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(name)) return layer;
            }
            return CandidateLayers[0];
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }

        private static bool TryGetCombinedBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: false);
            bool hasBounds = false;
            bounds = new Bounds(instance.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return hasBounds;
        }

        private static void ApplySpriteImportSettings(string assetPath, int resolution)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            // Cap max size at the rendered resolution so the importer doesn't
            // pad up to 2048 and waste memory for tiny icons.
            int maxSize = Mathf.Max(32, Mathf.NextPowerOfTwo(resolution));
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }
    }
}
#endif
