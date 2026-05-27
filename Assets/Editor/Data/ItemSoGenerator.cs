#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.2 — generates an <see cref="ItemDefinition"/> ScriptableObject per entry in
    /// <c>Design Documents/GameDesign/data/items.json</c>, plus the <see cref="ItemDatabase"/>
    /// registry that the runtime loads from <c>Assets/Resources/ItemDatabase.asset</c>.
    ///
    /// Idempotent: re-running updates fields on existing assets in place; new entries are
    /// created; assets removed from the source JSON are NOT deleted by this generator
    /// (orphan cleanup is Volume 2.5's "Regenerate All" responsibility).
    ///
    /// Visuals (icon, modelPrefab, placedPrefab) are intentionally untouched here — those
    /// are owned by Volume 3 (procedural model + icon pipeline).
    /// </summary>
    public static class ItemSoGenerator
    {
        private const string GeneratedItemsFolder = "Assets/ScriptableObjects/Generated/Items";
        private const string ResourcesFolder = "Assets/Resources";
        private const string ItemDatabaseAssetPath = "Assets/Resources/ItemDatabase.asset";

        [MenuItem("Voidborne/Generate/Items")]
        public static void Generate()
        {
            // Always read fresh JSON for a generation pass — designers may have re-extracted.
            GameDesignJsonLoader.ClearCache();

            Dictionary<string, ItemJson> items;
            Dictionary<string, List<string>> categoriesByName;

            try
            {
                items = GameDesignJsonLoader.LoadItems();
                categoriesByName = GameDesignJsonLoader.LoadCategories();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemSoGenerator] Failed to load source JSON: {ex.Message}");
                return;
            }

            if (items == null || items.Count == 0)
            {
                Debug.LogError("[ItemSoGenerator] items.json is empty — aborting.");
                return;
            }

            Dictionary<string, List<string>> categoriesByItemId = BuildReverseIndex(categoriesByName);

            EnsureFolder(GeneratedItemsFolder);
            EnsureFolder(ResourcesFolder);

            int created = 0;
            int updated = 0;
            int processed = 0;

            // Sort keys for deterministic asset ordering in the registry and diffs.
            var sortedKeys = items.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var generatedDefs = new List<ItemDefinition>(sortedKeys.Count);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var id in sortedKeys)
                {
                    ItemJson json = items[id];
                    if (json == null) continue;

                    string assetPath = $"{GeneratedItemsFolder}/{id}.asset";
                    bool isNew = false;

                    var asset = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<ItemDefinition>();
                        AssetDatabase.CreateAsset(asset, assetPath);
                        isNew = true;
                    }

                    PopulateDefinition(asset, id, json, categoriesByItemId);
                    EditorUtility.SetDirty(asset);

                    if (isNew) created++; else updated++;
                    processed++;

                    generatedDefs.Add(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Build / refresh the registry asset.
            ItemDatabase db = LoadOrCreateRegistry();
            db.items = generatedDefs;
            db.Reindex();
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemSoGenerator] Generated/updated {processed} items ({created} newly created, {updated} updated). Registry at {ItemDatabaseAssetPath}.");
        }

        // ---------------------------------------------------------------------
        // Field population
        // ---------------------------------------------------------------------

        private static void PopulateDefinition(
            ItemDefinition asset,
            string id,
            ItemJson json,
            IReadOnlyDictionary<string, List<string>> categoriesByItemId)
        {
            asset.itemId = id;
            asset.displayName = string.IsNullOrEmpty(json.name) ? id : json.name;
            asset.description = json.role ?? string.Empty;

            asset.kind = ParseKind(json.kind);
            asset.source = ParseSource(json.src);

            asset.categories = categoriesByItemId.TryGetValue(id, out var cats) && cats != null
                ? cats.ToArray()
                : Array.Empty<string>();

            asset.isBuildBlock = json.build == true;
            asset.isDeco = json.deco == true;
            asset.buildColor = json.buildColor ?? string.Empty;

            asset.maxStackSize = ComputeMaxStackSize(asset.kind, asset.categories);

            // Legacy compatibility classification — derived, not stored in JSON.
            asset.itemType = DeriveLegacyItemType(asset.kind, asset.source, asset.categories, asset.isBuildBlock, asset.isDeco);

            // Visuals intentionally NOT touched — Volume 3 owns icon / modelPrefab / placedPrefab.
        }

        // ---------------------------------------------------------------------
        // Stack size policy (per V2.2 spec)
        // ---------------------------------------------------------------------

        private static int ComputeMaxStackSize(ItemKind kind, string[] categories)
        {
            if (kind == ItemKind.Machine) return 1;

            bool hasWeapon = ContainsCategory(categories, "weapon");
            bool hasVehicle = ContainsCategory(categories, "vehicle");
            if (hasWeapon || hasVehicle) return 1;

            if (kind == ItemKind.Source) return 16;

            return 64;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static ItemKind ParseKind(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return ItemKind.Product;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "source": return ItemKind.Source;
                case "machine": return ItemKind.Machine;
                case "component": return ItemKind.Component;
                case "product": return ItemKind.Product;
                default:
                    Debug.LogWarning($"[ItemSoGenerator] Unknown item kind '{raw}' — defaulting to Product.");
                    return ItemKind.Product;
            }
        }

        private static ItemSource ParseSource(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return ItemSource.None;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "fauna": return ItemSource.Fauna;
                case "flora": return ItemSource.Flora;
                case "ore": return ItemSource.Ore;
                case "soil": return ItemSource.Soil;
                case "exotic": return ItemSource.Exotic;
                default:
                    Debug.LogWarning($"[ItemSoGenerator] Unknown item source '{raw}' — defaulting to None.");
                    return ItemSource.None;
            }
        }

        private static ItemType DeriveLegacyItemType(
            ItemKind kind, ItemSource source, string[] categories, bool isBuildBlock, bool isDeco)
        {
            // Best-effort mapping of the new model onto the legacy v1 enum for the handful of
            // call sites (TooltipUI, ItemIconGenerator, WorldItem, ChestSetup) that still
            // switch on itemType. Priority order matches what those call sites expect.
            if (kind == ItemKind.Machine) return ItemType.Machine;
            if (ContainsCategory(categories, "weapon")) return ItemType.Weapon;
            if (ContainsCategory(categories, "armor")) return ItemType.Armor;
            if (ContainsCategory(categories, "vehicle")) return ItemType.VehiclePart;
            if (ContainsCategory(categories, "food")) return ItemType.Consumable;
            if (isBuildBlock || isDeco) return ItemType.Block;
            if (kind == ItemKind.Source) return ItemType.Resource;
            return ItemType.Resource;
        }

        private static bool ContainsCategory(string[] categories, string key)
        {
            if (categories == null) return false;
            for (int i = 0; i < categories.Length; i++)
            {
                if (string.Equals(categories[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static Dictionary<string, List<string>> BuildReverseIndex(
            Dictionary<string, List<string>> categoriesByName)
        {
            var byItem = new Dictionary<string, List<string>>();
            if (categoriesByName == null) return byItem;

            foreach (var kvp in categoriesByName)
            {
                string category = kvp.Key;
                var ids = kvp.Value;
                if (ids == null) continue;
                foreach (var id in ids)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!byItem.TryGetValue(id, out var list))
                    {
                        list = new List<string>(2);
                        byItem[id] = list;
                    }
                    if (!list.Contains(category)) list.Add(category);
                }
            }

            return byItem;
        }

        private static ItemDatabase LoadOrCreateRegistry()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabaseAssetPath);
            if (db != null) return db;

            // An existing asset may be bound to the legacy ItemDatabase script (renamed in V2.2).
            // Delete it so CreateAsset can write a fresh one with the new class binding.
            if (AssetDatabase.LoadMainAssetAtPath(ItemDatabaseAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(ItemDatabaseAssetPath);
            }

            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, ItemDatabaseAssetPath);
            return db;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            var parts = assetFolder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
