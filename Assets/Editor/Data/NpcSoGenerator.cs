#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Data;
using Voidborne.Data.Schema;
using Voidborne.NPCs;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.4 — generates one <see cref="NpcDefinition"/> per entry in
    /// <c>npcs.json</c> with <c>cat in {named, trader}</c> (6 + 3 = 9
    /// entries) and writes the singleton <see cref="NpcRegistry"/> asset at
    /// <c>Assets/Resources/NpcRegistry.asset</c>.
    /// </summary>
    public static class NpcSoGenerator
    {
        private const string GeneratedFolder = "Assets/ScriptableObjects/Generated/NPCs";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RegistryAssetPath = "Assets/Resources/NpcRegistry.asset";

        private static readonly HashSet<string> IncludedCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "named", "trader"
        };

        [MenuItem("Voidborne/Generate/NPCs")]
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
                Debug.LogError($"[NpcSoGenerator] Failed to load npcs.json: {ex.Message}");
                return;
            }

            if (npcs == null || npcs.Count == 0)
            {
                Debug.LogError("[NpcSoGenerator] npcs.json is empty — aborting.");
                return;
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(ResourcesFolder);

            var npcEntries = npcs
                .Where(n => n != null && n.cat != null && IncludedCats.Contains(n.cat))
                .Select(n => new { Npc = n, Id = ParsingHelpers.SlugifyName(n.name) })
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .OrderBy(x => x.Id, StringComparer.Ordinal)
                .ToList();

            var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in npcEntries) expectedFiles.Add($"{e.Id}.asset");
            int deletedOrphans = DeleteOrphans(GeneratedFolder, expectedFiles);

            int created = 0;
            int updated = 0;
            var generated = new List<NpcDefinition>(npcEntries.Count);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var e in npcEntries)
                {
                    string assetPath = $"{GeneratedFolder}/{e.Id}.asset";
                    bool isNew = false;

                    var asset = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<NpcDefinition>();
                        AssetDatabase.CreateAsset(asset, assetPath);
                        isNew = true;
                    }

                    Populate(asset, e.Id, e.Npc);
                    EditorUtility.SetDirty(asset);

                    if (isNew) created++; else updated++;
                    generated.Add(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            NpcRegistry registry = LoadOrCreateRegistry();
            registry.allNpcs = generated
                .OrderBy(n => n.id, StringComparer.Ordinal)
                .ToList();
            registry.Reindex();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[NpcSoGenerator] Processed {generated.Count} NPCs " +
                $"({created} created, {updated} updated, {deletedOrphans} orphans deleted). " +
                $"Registry at {RegistryAssetPath}.");
        }

        // -------------------------------------------------------------------
        // Field population
        // -------------------------------------------------------------------

        private static void Populate(NpcDefinition asset, string id, NpcJson npc)
        {
            asset.id = id;
            asset.displayName = npc.name ?? id;
            asset.description = npc.desc ?? string.Empty;
            asset.section = npc.section ?? string.Empty;

            asset.behaviors = npc.behaviors != null ? npc.behaviors.ToArray() : Array.Empty<string>();
            asset.abilities = npc.abilities != null ? npc.abilities.ToArray() : Array.Empty<string>();
            asset.dropOrTradeItems = npc.drops != null ? npc.drops.ToArray() : Array.Empty<string>();

            // Named Kin + traders are non-killable.
            asset.isKillable = false;

            // Quest givers = cat == "named" per spec.
            asset.isQuestGiver = string.Equals(npc.cat, "named", StringComparison.OrdinalIgnoreCase);
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
                    Debug.Log($"[NpcSoGenerator] Deleted orphan NPC asset: {relPath}");
                    deleted++;
                }
            }
            return deleted;
        }

        private static NpcRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<NpcRegistry>(RegistryAssetPath);
            if (reg != null) return reg;

            if (AssetDatabase.LoadMainAssetAtPath(RegistryAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RegistryAssetPath);
            }

            reg = ScriptableObject.CreateInstance<NpcRegistry>();
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
