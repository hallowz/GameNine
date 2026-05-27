#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.3 — Bulk Item Prefab Generator.
    ///
    /// <c>[MenuItem("Voidborne/Generate/Item Prefabs")]</c> entry point that
    /// composes every Core 60 item into a placeholder prefab and writes the
    /// references back onto the matching <see cref="ItemDefinition"/> via
    /// <see cref="SerializedObject"/> — that's the only way to persist into
    /// the private serialized <c>modelPrefab</c> / <c>placedPrefab</c> fields.
    ///
    /// Idempotent: existing prefabs at the canonical paths are overwritten;
    /// SerializedObject only writes when the values changed.
    /// </summary>
    public static class BulkPrefabGenerator
    {
        [MenuItem("Voidborne/Generate/Item Prefabs")]
        public static void Generate()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            if (db == null)
            {
                Debug.LogError(
                    "[BulkPrefabGenerator] No ItemDatabase found in Resources. " +
                    "Run Voidborne/Generate/Items first.");
                return;
            }

            int worldCount = 0;
            int placedCount = 0;
            int skippedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in db.AllItems)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemId))
                    {
                        skippedCount++;
                        continue;
                    }

                    var (worldPrefab, placedPrefab) = ItemModelComposer.BuildAndSaveAll(item);

                    if (worldPrefab != null)
                    {
                        AssignPrefabField(item, "modelPrefab", worldPrefab);
                        worldCount++;
                    }

                    if (placedPrefab != null)
                    {
                        AssignPrefabField(item, "placedPrefab", placedPrefab);
                        placedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[BulkPrefabGenerator] Generated {worldCount} item prefabs (Core 60). " +
                $"Machines/blocks also got placed variants ({placedCount}). " +
                (skippedCount > 0 ? $"Skipped {skippedCount} malformed entries." : string.Empty));
        }

        /// <summary>
        /// Write <paramref name="prefab"/> into the named GameObject field on
        /// <paramref name="item"/> via SerializedObject so the value persists
        /// even though the field is private/inspector-only. No-op when the
        /// existing reference already matches.
        /// </summary>
        private static void AssignPrefabField(ItemDefinition item, string fieldName, GameObject prefab)
        {
            var so = new SerializedObject(item);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError(
                    $"[BulkPrefabGenerator] ItemDefinition has no serialized field '{fieldName}'.",
                    item);
                return;
            }
            if (prop.objectReferenceValue == prefab) return;
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
