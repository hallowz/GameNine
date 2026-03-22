using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Voidborne.Building;

namespace Voidborne.Editor
{
    /// <summary>
    /// Voidborne > Setup Building Items
    /// Creates BuildingPieceItem assets for all Wood-tier pieces,
    /// then registers them in the ItemDatabase.
    /// </summary>
    public static class BuildingItemSetup
    {
        private const string ItemDir  = "Assets/ScriptableObjects/Items/Building";
        private const string DataDir  = "Assets/ScriptableObjects/Building";
        private const string DbPath   = "Assets/Resources/ItemDatabase.asset";

        private static readonly (string dataFile, string itemId, string displayName, string description)[] Entries =
        {
            ("Foundation_Wood",    "build_wood_foundation",    "Wood Foundation",    "A flat wooden platform. The base of any structure."),
            ("TriFoundation_Wood", "build_wood_trifoundation", "Wood Triangle Foundation", "A triangular wooden platform for angled builds."),
            ("Wall_Wood",          "build_wood_wall",          "Wood Wall",          "A vertical plank wall. Stops bullets at 25% efficiency."),
            ("Doorway_Wood",       "build_wood_doorway",       "Wood Doorway",       "A wall frame with a door opening."),
            ("Window_Wood",        "build_wood_window",        "Wood Window",        "A framed window with glass panes."),
            ("Floor_Wood",         "build_wood_floor",         "Wood Floor",         "Walkable wooden floor panel for multi-story structures."),
            ("TriFloor_Wood",      "build_wood_trifloor",      "Wood Triangle Floor","A triangular floor panel."),
            ("Stairs_Wood",        "build_wood_stairs",        "Wood Stairs",        "Stepped ramp connecting floors. Occupies one wall slot."),
            ("Pillar_Wood",        "build_wood_pillar",        "Wood Pillar",        "A vertical column for corners. Snaps to foundation corners."),
            ("HalfWall_Wood",      "build_wood_halfwall",      "Wood Half Wall",     "A short fence-height wall panel."),
        };

        [MenuItem("Voidborne/Setup Building Items")]
        public static void Setup()
        {
            EnsureDirectory(ItemDir);

            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DbPath);
            if (db == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                    db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (db == null)
            {
                Debug.LogError("[BuildingItemSetup] ItemDatabase not found. Aborting.");
                return;
            }

            var existingIds = new HashSet<string>();
            foreach (var def in db.items)
                if (def != null && !string.IsNullOrEmpty(def.itemId))
                    existingIds.Add(def.itemId);

            int created = 0;
            foreach (var (dataFile, itemId, displayName, description) in Entries)
            {
                string dataPath = $"{DataDir}/{dataFile}.asset";
                BuildingPieceData data = AssetDatabase.LoadAssetAtPath<BuildingPieceData>(dataPath);
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingItemSetup] BuildingPieceData not found at {dataPath} — run 'Setup Building System' first.");
                    continue;
                }

                string itemPath = $"{ItemDir}/{dataFile}_Item.asset";
                BuildingPieceItem item = AssetDatabase.LoadAssetAtPath<BuildingPieceItem>(itemPath);

                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<BuildingPieceItem>();
                    AssetDatabase.CreateAsset(item, itemPath);
                    Debug.Log($"[BuildingItemSetup] Created {itemPath}");
                    created++;
                }

                item.itemId       = itemId;
                item.displayName  = displayName;
                item.description  = description;
                item.itemType     = ItemType.Block;
                item.maxStackSize = 100;
                item.weight       = 0.5f;
                item.pieceData    = data;
                item.materialTier = MaterialTier.Wood;

                // Link inventory item ID back to piece data
                data.inventoryItemId = itemId;
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(item);

                if (!existingIds.Contains(itemId))
                {
                    db.items.Add(item);
                    existingIds.Add(itemId);
                    EditorUtility.SetDirty(db);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildingItemSetup] Done. {created} item assets created; all registered in ItemDatabase.");
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
