#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Data;
using Voidborne.Data.Schema;
using Voidborne.Fauna;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.4 — generates one <see cref="FaunaDefinition"/> per entry
    /// in <c>npcs.json</c> with <c>cat == "wildlife"</c> (18 entries) and
    /// writes the singleton <see cref="FaunaRegistry"/> asset at
    /// <c>Assets/Resources/FaunaRegistry.asset</c>.
    /// </summary>
    public static class FaunaSoGenerator
    {
        private const string GeneratedFolder = "Assets/ScriptableObjects/Generated/Fauna";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RegistryAssetPath = "Assets/Resources/FaunaRegistry.asset";

        [MenuItem("Voidborne/Generate/Fauna")]
        public static void Generate()
        {
            GameDesignJsonLoader.ClearCache();

            List<NpcJson> npcs;
            try
            {
                npcs = GameDesignJsonLoader.LoadNpcs();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FaunaSoGenerator] Failed to load npcs.json: {ex.Message}");
                return;
            }

            if (npcs == null || npcs.Count == 0)
            {
                Debug.LogError("[FaunaSoGenerator] npcs.json is empty — aborting.");
                return;
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(ResourcesFolder);

            // Resolve ID slugs and filter wildlife.
            var wildlife = npcs
                .Where(n => n != null && string.Equals(n.cat, "wildlife", StringComparison.OrdinalIgnoreCase))
                .Select(n => new { Npc = n, Id = ParsingHelpers.SlugifyName(n.name) })
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .OrderBy(x => x.Id, StringComparer.Ordinal)
                .ToList();

            // Pre-clean orphans.
            var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in wildlife) expectedFiles.Add($"{w.Id}.asset");
            int deletedOrphans = DeleteOrphans(GeneratedFolder, expectedFiles);

            int created = 0;
            int updated = 0;
            var generated = new List<FaunaDefinition>(wildlife.Count);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var w in wildlife)
                {
                    string assetPath = $"{GeneratedFolder}/{w.Id}.asset";
                    bool isNew = false;

                    var asset = AssetDatabase.LoadAssetAtPath<FaunaDefinition>(assetPath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<FaunaDefinition>();
                        AssetDatabase.CreateAsset(asset, assetPath);
                        isNew = true;
                    }

                    Populate(asset, w.Id, w.Npc);
                    EditorUtility.SetDirty(asset);

                    if (isNew) created++; else updated++;
                    generated.Add(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            FaunaRegistry registry = LoadOrCreateRegistry();
            registry.allFauna = generated
                .OrderBy(f => f.id, StringComparer.Ordinal)
                .ToList();
            registry.Reindex();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[FaunaSoGenerator] Processed {generated.Count} fauna " +
                $"({created} created, {updated} updated, {deletedOrphans} orphans deleted). " +
                $"Registry at {RegistryAssetPath}.");
        }

        // -------------------------------------------------------------------
        // Field population
        // -------------------------------------------------------------------

        private static void Populate(FaunaDefinition asset, string id, NpcJson npc)
        {
            asset.id = id;
            asset.displayName = npc.name ?? id;
            asset.description = npc.desc ?? string.Empty;

            asset.behaviors = npc.behaviors != null ? npc.behaviors.ToArray() : Array.Empty<string>();
            asset.dropItemIds = npc.drops != null ? npc.drops.ToArray() : Array.Empty<string>();

            asset.habitat = npc.section ?? string.Empty;

            asset.isAggressive = ParsingHelpers.IsAggressive(asset.behaviors);
            asset.isPassive = ParsingHelpers.IsPassive(asset.behaviors);
            asset.isTameable = ParsingHelpers.IsTameable(asset.behaviors, asset.description, asset.displayName);

            // Default stats; balance in V14.
            if (asset.baseHealth <= 0f) asset.baseHealth = 50f;
            if (asset.baseSpeed <= 0f) asset.baseSpeed = 3f;
        }

        // -------------------------------------------------------------------
        // Plumbing
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
                    Debug.Log($"[FaunaSoGenerator] Deleted orphan fauna asset: {relPath}");
                    deleted++;
                }
            }
            return deleted;
        }

        private static FaunaRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<FaunaRegistry>(RegistryAssetPath);
            if (reg != null) return reg;

            if (AssetDatabase.LoadMainAssetAtPath(RegistryAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RegistryAssetPath);
            }

            reg = ScriptableObject.CreateInstance<FaunaRegistry>();
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
