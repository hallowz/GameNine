using UnityEditor;
using UnityEngine;

namespace Voidborne.Editor
{
    /// <summary>
    /// Voidborne > Clean Item Database
    /// Removes null / missing entries from the ItemDatabase items list.
    /// Run after deleting old ScriptableObject assets.
    /// </summary>
    public static class CleanItemDatabase
    {
        [MenuItem("Voidborne/Clean Item Database")]
        public static void Clean()
        {
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(
                "Assets/Resources/ItemDatabase.asset");

            if (db == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                    db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (db == null)
            {
                Debug.LogError("[CleanItemDatabase] ItemDatabase not found.");
                return;
            }

            int before = db.items.Count;
            db.items.RemoveAll(item => item == null);
            int removed = before - db.items.Count;

            if (removed > 0)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CleanItemDatabase] Removed {removed} null entries. {db.items.Count} items remain.");
            }
            else
            {
                Debug.Log("[CleanItemDatabase] No null entries found. Database is clean.");
            }
        }
    }
}
