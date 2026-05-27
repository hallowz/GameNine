#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Data;
using Voidborne.Data.Schema;
using Voidborne.Enemies.V2;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.4 — generates one V2 <see cref="EnemyDefinition"/> per entry
    /// in <c>npcs.json</c> with <c>cat in {boss, finale, fodder}</c>
    /// (30 + 2 + 11 = 43 entries) and writes the singleton
    /// <see cref="EnemyRegistry"/> asset at
    /// <c>Assets/Resources/EnemyRegistry.asset</c>.
    /// </summary>
    public static class EnemySoGenerator
    {
        private const string GeneratedFolder = "Assets/ScriptableObjects/Generated/Enemies";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RegistryAssetPath = "Assets/Resources/EnemyRegistry.asset";

        // Categories included in this generator pass.
        private static readonly HashSet<string> IncludedCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "boss", "finale", "fodder"
        };

        [MenuItem("Voidborne/Generate/Enemies")]
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
                Debug.LogError($"[EnemySoGenerator] Failed to load npcs.json: {ex.Message}");
                return;
            }

            if (npcs == null || npcs.Count == 0)
            {
                Debug.LogError("[EnemySoGenerator] npcs.json is empty — aborting.");
                return;
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(ResourcesFolder);

            var enemyEntries = npcs
                .Where(n => n != null && n.cat != null && IncludedCats.Contains(n.cat))
                .Select(n => new { Npc = n, Id = ParsingHelpers.SlugifyName(n.name) })
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .OrderBy(x => x.Id, StringComparer.Ordinal)
                .ToList();

            var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in enemyEntries) expectedFiles.Add($"{e.Id}.asset");
            int deletedOrphans = DeleteOrphans(GeneratedFolder, expectedFiles);

            int created = 0;
            int updated = 0;
            var generated = new List<EnemyDefinition>(enemyEntries.Count);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var e in enemyEntries)
                {
                    string assetPath = $"{GeneratedFolder}/{e.Id}.asset";
                    bool isNew = false;

                    var asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(assetPath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<EnemyDefinition>();
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

            EnemyRegistry registry = LoadOrCreateRegistry();
            registry.allEnemies = generated
                .OrderBy(e => e.id, StringComparer.Ordinal)
                .ToList();
            registry.Reindex();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[EnemySoGenerator] Processed {generated.Count} enemies " +
                $"({created} created, {updated} updated, {deletedOrphans} orphans deleted). " +
                $"Registry at {RegistryAssetPath}.");
        }

        // -------------------------------------------------------------------
        // Field population
        // -------------------------------------------------------------------

        private static void Populate(EnemyDefinition asset, string id, NpcJson npc)
        {
            asset.id = id;
            asset.displayName = npc.name ?? id;
            asset.description = npc.desc ?? string.Empty;

            asset.behaviors = npc.behaviors != null ? npc.behaviors.ToArray() : Array.Empty<string>();
            asset.abilities = npc.abilities != null ? npc.abilities.ToArray() : Array.Empty<string>();
            asset.dropItemIds = npc.drops != null ? npc.drops.ToArray() : Array.Empty<string>();

            asset.boss_section = npc.section ?? string.Empty;

            string cat = npc.cat ?? string.Empty;
            asset.isFinale = string.Equals(cat, "finale", StringComparison.OrdinalIgnoreCase);
            asset.isBoss = asset.isFinale
                           || string.Equals(cat, "boss", StringComparison.OrdinalIgnoreCase);

            asset.family = ResolveFamily(cat, asset.boss_section);
            asset.tier = ResolveTier(cat, asset.boss_section, asset.description);

            if (asset.baseHealth <= 0f) asset.baseHealth = 30f;
            if (asset.baseSpeed <= 0f) asset.baseSpeed = 4f;
        }

        private static EnemyArchetype ResolveFamily(string cat, string section)
        {
            if (string.Equals(cat, "fodder", StringComparison.OrdinalIgnoreCase))
                return EnemyArchetype.Fodder;
            if (string.Equals(cat, "finale", StringComparison.OrdinalIgnoreCase))
                return EnemyArchetype.Finale;

            if (string.IsNullOrEmpty(section)) return EnemyArchetype.Other;
            string s = section;

            if (s.IndexOf("Brood", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Brood;
            if (s.IndexOf("Warden", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Warden;
            if (s.IndexOf("Hunter", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Hunter;
            if (s.IndexOf("Channeler", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Channeler;
            if (s.IndexOf("Aberrant", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Aberrant;
            if (s.IndexOf("Finale", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyArchetype.Finale;
            if (s.IndexOf("Fodder", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Patrol", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EnemyArchetype.Fodder;
            }
            return EnemyArchetype.Other;
        }

        private static int ResolveTier(string cat, string section, string desc)
        {
            // Fodder/finale: 0.
            if (!string.Equals(cat, "boss", StringComparison.OrdinalIgnoreCase)) return 0;

            // Try to parse a tier from desc first (e.g. "Tier 1 Warden, ...").
            int tierFromDesc = ParseTierTokenInText(desc);
            if (tierFromDesc > 0) return tierFromDesc;

            // Fallback to section (e.g. "Brood T1") via ParsingHelpers.ParseTier.
            int tierFromSection = ParsingHelpers.ParseTier(section);
            if (tierFromSection > 0) return tierFromSection;

            // Default for unannotated bosses (per spec).
            return 1;
        }

        /// <summary>
        /// Look for a "Tier N" or "Tn" token in free text. Returns 1-3 when
        /// found, 0 otherwise.
        /// </summary>
        private static int ParseTierTokenInText(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // "Tier 1/2/3" phrase first (most common in JSON desc fields).
            int idx = text.IndexOf("Tier ", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx + 5 < text.Length)
            {
                char d = text[idx + 5];
                if (d >= '1' && d <= '7') return d - '0';
            }

            // "T1/T2/T3" via ParsingHelpers — same boundary rules.
            return ParsingHelpers.ParseTier(text);
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
                    Debug.Log($"[EnemySoGenerator] Deleted orphan enemy asset: {relPath}");
                    deleted++;
                }
            }
            return deleted;
        }

        private static EnemyRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<EnemyRegistry>(RegistryAssetPath);
            if (reg != null) return reg;

            if (AssetDatabase.LoadMainAssetAtPath(RegistryAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RegistryAssetPath);
            }

            reg = ScriptableObject.CreateInstance<EnemyRegistry>();
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
