using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;

namespace Voidborne.Editor
{
    /// <summary>
    /// Voidborne > Setup Chest (Dev)
    /// Places a ChestBlock in the active scene loaded with one of every item in the
    /// ItemDatabase, plus generous stacks of every building piece for testing builds.
    /// </summary>
    public static class ChestSetup
    {
        // Item IDs for building pieces — must match BuildingItemSetup definitions.
        private static readonly string[] BuildingPieceIds =
        {
            "build_wood_foundation",
            "build_wood_trifoundation",
            "build_wood_wall",
            "build_wood_doorway",
            "build_wood_window",
            "build_wood_floor",
            "build_wood_trifloor",
            "build_wood_stairs",
            "build_wood_pillar",
            "build_wood_halfwall",
        };

        private const int BuildingSupplyQty = 99;  // near-full stacks for each piece type

        [MenuItem("Voidborne/Setup Chest (Dev)")]
        public static void Setup()
        {
            // Remove any existing dev chest to avoid duplicates
            var existing = Object.FindObjectOfType<ChestBlock>();
            if (existing != null)
            {
                Debug.Log("[ChestSetup] Existing ChestBlock found — replacing it.");
                Object.DestroyImmediate(existing.gameObject);
            }

            // Load the ItemDatabase
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
                Debug.LogError("[ChestSetup] ItemDatabase not found.");
                return;
            }

            // Build a quick lookup by itemId
            var lookup = new Dictionary<string, ItemDefinition>();
            foreach (var def in db.items)
                if (def != null && !string.IsNullOrEmpty(def.itemId))
                    lookup[def.itemId] = def;

            // Create chest GameObject
            GameObject chestGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestGO.name = "DevChest";
            chestGO.transform.position = new Vector3(5f, 1f, 0f);
            chestGO.transform.localScale = new Vector3(1f, 1f, 0.6f);

            // Tint it brown
            Renderer rend = chestGO.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.45f, 0.28f, 0.10f);
                rend.sharedMaterial = mat;
            }

            ChestBlock chest = chestGO.AddComponent<ChestBlock>();

            var items = new List<ItemStack>();

            // ── Building pieces first (generous stacks for testing builds) ───────
            int buildingAdded = 0;
            foreach (string id in BuildingPieceIds)
            {
                if (lookup.TryGetValue(id, out ItemDefinition def))
                {
                    items.Add(new ItemStack(def, BuildingSupplyQty));
                    buildingAdded++;
                }
                else
                {
                    Debug.LogWarning($"[ChestSetup] Building piece '{id}' not in ItemDatabase. " +
                                     "Run 'Setup Building Items' first.");
                }
            }

            // ── Everything else (1 weapon/tool/machine/block, 10 resources) ─────
            var buildingIdSet = new HashSet<string>(BuildingPieceIds);
            foreach (ItemDefinition def in db.items)
            {
                if (def == null) continue;
                if (buildingIdSet.Contains(def.itemId)) continue; // already added above
                if (def is AutomationItem) continue;              // automation items not in dev chest

                int qty = (def.itemType == ItemType.Tool
                        || def.itemType == ItemType.Weapon
                        || def.itemType == ItemType.Machine
                        || def.itemType == ItemType.Block
                        || def.itemType == ItemType.Backpack) ? 1 : 10;
                items.Add(new ItemStack(def, qty));
            }

            chest.starterItems = items;

            Debug.Log($"[ChestSetup] DevChest placed at (5,1,0) with {items.Count} item types. " +
                      $"Building pieces: {buildingAdded}×{BuildingSupplyQty} each.");
        }
    }
}
