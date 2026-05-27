#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.4 — generates one <see cref="MachineDefinition"/> per entry
    /// in <c>items.json</c> with <c>kind == "machine"</c> (43 entries) and
    /// writes the singleton <see cref="MachineRegistry"/> asset at
    /// <c>Assets/Resources/MachineRegistry.asset</c>.
    ///
    /// Idempotent: re-running updates fields on existing assets in place;
    /// new entries are created; orphan asset files whose machine no longer
    /// exists in the JSON are deleted before the generation pass.
    /// </summary>
    public static class MachineSoGenerator
    {
        private const string GeneratedFolder = "Assets/ScriptableObjects/Generated/Machines";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RegistryAssetPath = "Assets/Resources/MachineRegistry.asset";

        [MenuItem("Voidborne/Generate/Machines")]
        public static void Generate()
        {
            GameDesignJsonLoader.ClearCache();

            Dictionary<string, ItemJson> items;
            try
            {
                items = GameDesignJsonLoader.LoadItems();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MachineSoGenerator] Failed to load items.json: {ex.Message}");
                return;
            }

            if (items == null || items.Count == 0)
            {
                Debug.LogError("[MachineSoGenerator] items.json is empty — aborting.");
                return;
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(ResourcesFolder);

            // Filter machines.
            var machineEntries = items
                .Where(kvp => kvp.Value != null
                              && string.Equals(kvp.Value.kind, "machine", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToList();

            // Pre-clean orphans.
            var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in machineEntries) expectedFiles.Add($"{kvp.Key}.asset");
            int deletedOrphans = DeleteOrphans(GeneratedFolder, expectedFiles);

            int created = 0;
            int updated = 0;
            var generated = new List<MachineDefinition>(machineEntries.Count);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kvp in machineEntries)
                {
                    string id = kvp.Key;
                    ItemJson json = kvp.Value;

                    string assetPath = $"{GeneratedFolder}/{id}.asset";
                    bool isNew = false;

                    var asset = AssetDatabase.LoadAssetAtPath<MachineDefinition>(assetPath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<MachineDefinition>();
                        AssetDatabase.CreateAsset(asset, assetPath);
                        isNew = true;
                    }

                    Populate(asset, id, json);
                    EditorUtility.SetDirty(asset);

                    if (isNew) created++; else updated++;
                    generated.Add(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Registry.
            MachineRegistry registry = LoadOrCreateRegistry();
            registry.allMachines = generated
                .OrderBy(m => m.itemId, StringComparer.Ordinal)
                .ToList();
            registry.Reindex();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[MachineSoGenerator] Processed {generated.Count} machines " +
                $"({created} created, {updated} updated, {deletedOrphans} orphans deleted). " +
                $"Registry at {RegistryAssetPath}.");
        }

        // -------------------------------------------------------------------
        // Field population
        // -------------------------------------------------------------------

        private static void Populate(MachineDefinition asset, string id, ItemJson json)
        {
            asset.itemId = id;
            asset.displayName = string.IsNullOrEmpty(json.name) ? id : json.name;
            asset.tier = ParsingHelpers.ParseTier(json.role);

            // Grid dimensions (per V2.4 spec heuristics).
            (int w, int h) = ResolveGridSize(id);
            asset.gridWidth = w;
            asset.gridHeight = h;

            // Power profile.
            string role = json.role ?? string.Empty;
            bool startsWithAuto = id.StartsWith("auto_", StringComparison.OrdinalIgnoreCase);
            bool startsWithElectric = id.StartsWith("electric", StringComparison.OrdinalIgnoreCase);

            bool needsPower = asset.tier >= 4
                              || ParsingHelpers.ContainsMarker(role, "powered")
                              || startsWithAuto
                              || startsWithElectric;
            asset.needsPower = needsPower;
            asset.powerDrawWatts = needsPower ? ComputePowerDraw(asset.tier) : 0;

            // isAutomatable per spec ("id starts with auto_").
            asset.isAutomatable = startsWithAuto;

            // Category.
            asset.category = ResolveCategory(id);
        }

        private static int ComputePowerDraw(int tier)
        {
            // Per spec: 50 * tier as a tunable default for T4..T7.
            // Tiers 0-3 should never hit this path (needsPower = false), but
            // fall through safely if they do.
            if (tier <= 0) return 100;
            return 50 * tier;
        }

        private static (int w, int h) ResolveGridSize(string id)
        {
            if (string.IsNullOrEmpty(id)) return (3, 3);
            string lower = id.ToLowerInvariant();

            if (lower == "workbench") return (3, 3);
            if (lower.Contains("assembler")) return (5, 5);
            if (lower.Contains("press")) return (1, 2);
            if (lower.Contains("apiary")) return (1, 1);
            if (lower.Contains("campfire")) return (1, 1);
            if (lower.Contains("growth_plot")) return (1, 1);
            if (lower.Contains("fish_trap")) return (1, 1);
            return (3, 3);
        }

        private static MachineCategory ResolveCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return MachineCategory.Other;
            string lower = id.ToLowerInvariant();

            if (lower.StartsWith("auto_")) return MachineCategory.Auto;
            if (lower.Contains("workbench")) return MachineCategory.Workbench;
            if (lower.Contains("assembler")) return MachineCategory.Assembler;
            if (lower.Contains("furnace")) return MachineCategory.Furnace;
            if (lower.Contains("forge")) return MachineCategory.Forge;
            if (lower.Contains("apiary")) return MachineCategory.Apiary;
            if (lower.Contains("press")) return MachineCategory.Press;
            return MachineCategory.Other;
        }

        // -------------------------------------------------------------------
        // Orphan cleanup / folder / registry plumbing
        // -------------------------------------------------------------------

        private static int DeleteOrphans(string assetFolder, HashSet<string> expected)
        {
            int deleted = 0;
            if (!AssetDatabase.IsValidFolder(assetFolder)) return 0;

            string absFolder = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(absFolder)) return 0;

            foreach (string filePath in Directory.GetFiles(absFolder, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (expected.Contains(fileName)) continue;

                string relPath = $"{assetFolder}/{fileName}";
                if (AssetDatabase.DeleteAsset(relPath))
                {
                    Debug.Log($"[MachineSoGenerator] Deleted orphan machine asset: {relPath}");
                    deleted++;
                }
            }
            return deleted;
        }

        private static MachineRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<MachineRegistry>(RegistryAssetPath);
            if (reg != null) return reg;

            if (AssetDatabase.LoadMainAssetAtPath(RegistryAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RegistryAssetPath);
            }

            reg = ScriptableObject.CreateInstance<MachineRegistry>();
            AssetDatabase.CreateAsset(reg, RegistryAssetPath);
            return reg;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            string current = parts[0];
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
