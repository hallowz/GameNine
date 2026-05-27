#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.4 — Bulk Icon Renderer.
    ///
    /// <c>[MenuItem("Voidborne/Generate/Item Icons")]</c> entry point that
    /// iterates the Core 60 <see cref="ItemDatabase"/>, snapshots each item's
    /// <c>modelPrefab</c> via <see cref="IconRenderer.RenderIcon"/>, and writes
    /// the resulting Sprite back onto <see cref="ItemDefinition.icon"/> via
    /// <see cref="SerializedObject"/> so the (private) serialized field actually
    /// persists — same pattern V3.3 used for modelPrefab/placedPrefab.
    ///
    /// Two-phase to play nicely with <c>AssetDatabase.StartAssetEditing</c>:
    ///   1. Render + save every PNG (and apply importer settings) inside the
    ///      asset-editing batch. Imports defer until StopAssetEditing.
    ///   2. After StopAssetEditing finalises the imports, reload each Sprite
    ///      and wire it onto the matching ItemDefinition.
    ///
    /// Idempotent: PNGs are overwritten in place, and the Sprite reference is
    /// rewritten only when it changes.
    /// </summary>
    public static class IconBulkRenderer
    {
        [MenuItem("Voidborne/Generate/Item Icons")]
        public static void GenerateMenu() => Run();

        /// <summary>
        /// Public, test-callable entry point. Returns the number of icons that
        /// were successfully rendered AND assigned to ItemDefinition.icon.
        /// </summary>
        public static int Run(int resolution = 256)
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            if (db == null)
            {
                Debug.LogError(
                    "[IconBulkRenderer] No ItemDatabase found in Resources. " +
                    "Run Voidborne/Generate/Items first.");
                return 0;
            }

            int skippedMissingPrefab = 0;
            int skippedRenderFailed = 0;

            int total = db.AllItems.Count;

            // Phase 1: render every PNG. Inside StartAssetEditing the importer
            // queues all the texture imports; Sprites won't be loadable until
            // StopAssetEditing fires.
            var pendingPaths = new List<(ItemDefinition item, string assetPath)>(total);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < total; i++)
                {
                    var item = db.AllItems[i];
                    if (item == null || string.IsNullOrEmpty(item.itemId))
                    {
                        continue;
                    }

                    float progress = total > 0 ? (float)i / total : 0f;
                    EditorUtility.DisplayProgressBar(
                        "Rendering icons",
                        item.itemId,
                        progress);

                    if (item.modelPrefab == null)
                    {
                        Debug.LogWarning(
                            $"[IconBulkRenderer] '{item.itemId}' has no modelPrefab. Skipping icon render. " +
                            "(Run Voidborne/Generate/Item Prefabs first.)");
                        skippedMissingPrefab++;
                        continue;
                    }

                    // RenderIcon writes the PNG, applies importer settings, and
                    // returns the Sprite asset. Inside StartAssetEditing the
                    // returned Sprite is typically null (import is queued); we
                    // care about the on-disk PNG either way.
                    IconRenderer.RenderIcon(item.modelPrefab, item.itemId, resolution);
                    pendingPaths.Add((item, $"Assets/Textures/Icons/{item.itemId}.png"));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            // Phase 2: imports finalised. Reload each Sprite and assign it.
            AssetDatabase.Refresh();

            int assigned = 0;
            foreach (var (item, assetPath) in pendingPaths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[IconBulkRenderer] Could not load Sprite at {assetPath} after import.");
                    skippedRenderFailed++;
                    continue;
                }
                AssignIconField(item, sprite);
                assigned++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[IconBulkRenderer] Rendered {assigned} icons (Core 60). " +
                $"Saved to Assets/Textures/Icons/." +
                (skippedMissingPrefab > 0
                    ? $" Skipped {skippedMissingPrefab} with no modelPrefab."
                    : string.Empty) +
                (skippedRenderFailed > 0
                    ? $" {skippedRenderFailed} sprite reload(s) failed."
                    : string.Empty));

            return assigned;
        }

        /// <summary>
        /// Write <paramref name="sprite"/> into <c>ItemDefinition.icon</c> via
        /// SerializedObject so the serialized field actually persists. No-op
        /// when the existing reference already matches.
        /// </summary>
        private static void AssignIconField(ItemDefinition item, Sprite sprite)
        {
            var so = new SerializedObject(item);
            var prop = so.FindProperty("icon");
            if (prop == null)
            {
                Debug.LogError(
                    $"[IconBulkRenderer] ItemDefinition has no serialized field 'icon'.",
                    item);
                return;
            }
            if (prop.objectReferenceValue == sprite) return;
            prop.objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
