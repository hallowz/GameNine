#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Core;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Data.Schema;
using Voidborne.Enemies.V2;
using Voidborne.Fauna;
using Voidborne.NPCs;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.5 — orchestrates the six V2.2–V2.4 SO generators behind a single
    /// menu item. Pre-step scans <c>Assets/ScriptableObjects/Generated/</c> for
    /// asset files that have no corresponding entry in the current JSON
    /// extracts and logs a warning (the individual generators handle their own
    /// orphan deletion; this scan is purely a heads-up so the user knows a
    /// destructive prune is about to happen).
    /// </summary>
    /// <remarks>
    /// The "⟳" character in the menu name is a Unicode arrow (U+27F3), not an
    /// emoji — verbatim from the V2.5 spec.
    /// Generation order follows the dependency chain documented in the master
    /// prompt: Items → Recipes → Machines → Fauna → Enemies → NPCs.
    /// </remarks>
    public static class RegenerateAllMenu
    {
        private const string GeneratedRoot = "Assets/ScriptableObjects/Generated";
        private const string ItemsFolder = GeneratedRoot + "/Items";
        private const string RecipesFolder = GeneratedRoot + "/Recipes";
        private const string MachinesFolder = GeneratedRoot + "/Machines";
        private const string FaunaFolder = GeneratedRoot + "/Fauna";
        private const string EnemiesFolder = GeneratedRoot + "/Enemies";
        private const string NpcsFolder = GeneratedRoot + "/NPCs";

        [MenuItem("Voidborne/Generate/⟳ Regenerate All Generated SOs")]
        private static void OnRegenerateAllMenu() => Run();

        /// <summary>
        /// Synchronous entry point — runs all six generators in dependency
        /// order and logs a summary. Exposed publicly so EditMode tests can
        /// invoke the orchestrator without going through the menu system.
        /// Always finishes by saving + refreshing the AssetDatabase even on
        /// partial failure so the editor state is consistent.
        /// </summary>
        public static void Run()
        {
            Debug.Log("=== Voidborne Regenerate All ===");

            // -----------------------------------------------------------------
            // Pre-step: orphan warning. Non-destructive — generators themselves
            // delete orphans during their pass; this is just a heads-up so the
            // user is not surprised by the deletes the next log lines will
            // announce.
            // -----------------------------------------------------------------
            try
            {
                WarnAboutOrphans();
            }
            catch (Exception ex)
            {
                // Pre-step failure must NOT block the regenerate run — log and
                // continue so the user can still recover state.
                Debug.LogWarning($"[RegenerateAll] Orphan pre-scan failed (non-fatal): {ex.Message}");
            }

            // Force-clear the JSON loader cache once up front. The individual
            // generators also call ClearCache, but clearing once here means a
            // single re-extract + regenerate sequence always sees fresh data.
            GameDesignJsonLoader.ClearCache();

            string failedStage = null;
            Exception failure = null;

            try
            {
                RunStage("Items",    ItemSoGenerator.Generate,    ref failedStage, ref failure);
                if (failure != null) return;
                AssetDatabase.SaveAssets();

                RunStage("Recipes",  RecipeSoGenerator.Generate,  ref failedStage, ref failure);
                if (failure != null) return;
                AssetDatabase.SaveAssets();

                RunStage("Machines", MachineSoGenerator.Generate, ref failedStage, ref failure);
                if (failure != null) return;
                AssetDatabase.SaveAssets();

                RunStage("Fauna",    FaunaSoGenerator.Generate,   ref failedStage, ref failure);
                if (failure != null) return;
                AssetDatabase.SaveAssets();

                RunStage("Enemies",  EnemySoGenerator.Generate,   ref failedStage, ref failure);
                if (failure != null) return;
                AssetDatabase.SaveAssets();

                RunStage("NPCs",     NpcSoGenerator.Generate,     ref failedStage, ref failure);
                if (failure != null) return;
            }
            finally
            {
                // Always flush and refresh — even on partial failure — so the
                // editor sees whatever was generated before the abort.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (failure != null)
                {
                    Debug.LogError($"[RegenerateAll] ABORTED at stage '{failedStage}': {failure}");
                }
                else
                {
                    LogSummary();
                }
            }
        }

        // ---------------------------------------------------------------------
        // Stage runner — captures generator exceptions and tags them with the
        // stage name so a failure report is unambiguous.
        // ---------------------------------------------------------------------

        private static void RunStage(
            string stageName,
            Action generator,
            ref string failedStage,
            ref Exception failure)
        {
            try
            {
                Debug.Log($"[RegenerateAll] Stage: {stageName}");
                generator();
            }
            catch (Exception ex)
            {
                failedStage = stageName;
                failure = ex;
            }
        }

        // ---------------------------------------------------------------------
        // Orphan pre-scan: list .asset files in each Generated/<kind> folder
        // and compare against the IDs known to the current JSON extracts.
        // Warns only — does not delete (the per-generator passes handle that).
        // ---------------------------------------------------------------------

        private static void WarnAboutOrphans()
        {
            // Snapshot the JSON content once so we don't re-read it six times.
            Dictionary<string, ItemJson> items;
            List<NpcJson> npcs;
            try
            {
                items = GameDesignJsonLoader.LoadItems();
                npcs = GameDesignJsonLoader.LoadNpcs();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[RegenerateAll] Pre-scan could not load JSON ({ex.Message}); " +
                    $"skipping orphan warning. Generators will still report orphans during their own pass.");
                return;
            }

            // Items: every key in items.json is a valid Item asset id.
            var itemIds = new HashSet<string>(items?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            WarnFolderOrphans(ItemsFolder, fileName => itemIds.Contains(StripAssetExt(fileName)));

            // Recipes: <outputId>__r<N>.asset where outputId is a key in items.json
            // and N is in range of that item's recipes[] list.
            WarnFolderOrphans(RecipesFolder, fileName => IsLiveRecipeFile(fileName, items));

            // Machines: items.json entries with kind == "machine".
            var machineIds = new HashSet<string>(
                (items ?? new Dictionary<string, ItemJson>())
                    .Where(kvp => kvp.Value != null && string.Equals(kvp.Value.kind, "machine", StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key),
                StringComparer.Ordinal);
            WarnFolderOrphans(MachinesFolder, fileName => machineIds.Contains(StripAssetExt(fileName)));

            // Fauna / Enemy / NPC asset ids are slugified names from npcs.json.
            var faunaIds = NpcSlugsByCategory(npcs, "wildlife");
            WarnFolderOrphans(FaunaFolder, fileName => faunaIds.Contains(StripAssetExt(fileName)));

            var enemyIds = NpcSlugsByCategory(npcs, "boss", "finale", "fodder");
            WarnFolderOrphans(EnemiesFolder, fileName => enemyIds.Contains(StripAssetExt(fileName)));

            var npcIds = NpcSlugsByCategory(npcs, "named", "trader");
            WarnFolderOrphans(NpcsFolder, fileName => npcIds.Contains(StripAssetExt(fileName)));
        }

        private static bool IsLiveRecipeFile(string fileName, Dictionary<string, ItemJson> items)
        {
            if (items == null) return false;
            // Recipe asset name format: <outputId>__r<index>.asset
            string stem = StripAssetExt(fileName);
            int sep = stem.LastIndexOf("__r", StringComparison.Ordinal);
            if (sep < 0) return false;
            string outputId = stem.Substring(0, sep);
            string idxStr = stem.Substring(sep + 3);
            if (!int.TryParse(idxStr, out int idx) || idx < 0) return false;
            if (!items.TryGetValue(outputId, out var item) || item?.recipes == null) return false;
            return idx < item.recipes.Count;
        }

        private static HashSet<string> NpcSlugsByCategory(List<NpcJson> npcs, params string[] categories)
        {
            var slugs = new HashSet<string>(StringComparer.Ordinal);
            if (npcs == null) return slugs;
            var wanted = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
            foreach (var n in npcs)
            {
                if (n == null || string.IsNullOrEmpty(n.cat) || string.IsNullOrEmpty(n.name)) continue;
                if (!wanted.Contains(n.cat)) continue;
                slugs.Add(ParsingHelpers.SlugifyName(n.name));
            }
            return slugs;
        }

        private static void WarnFolderOrphans(string assetFolder, Func<string, bool> isLive)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;

            string absFolder = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(absFolder)) return;

            var orphans = new List<string>();
            foreach (string filePath in Directory.GetFiles(absFolder, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (!isLive(fileName)) orphans.Add(fileName);
            }

            if (orphans.Count == 0) return;

            // Cap the per-folder enumeration in the log so a catastrophic mismatch
            // doesn't spew thousands of lines.
            const int MaxNamesShown = 10;
            string sample = string.Join(", ", orphans.Take(MaxNamesShown));
            string more = orphans.Count > MaxNamesShown ? $" (+{orphans.Count - MaxNamesShown} more)" : string.Empty;
            Debug.LogWarning(
                $"[RegenerateAll] {assetFolder} contains {orphans.Count} asset(s) not in current JSON — " +
                $"these will be pruned by the matching generator: {sample}{more}");
        }

        private static string StripAssetExt(string fileName)
        {
            const string ext = ".asset";
            if (fileName != null && fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - ext.Length);
            return fileName ?? string.Empty;
        }

        // ---------------------------------------------------------------------
        // Final summary log.
        // ---------------------------------------------------------------------

        private static void LogSummary()
        {
            int itemCount = SafeCount(() => Registries.Items?.AllItems?.Count ?? 0);
            int recipeCount = SafeCount(() => Registries.Recipes?.AllRecipes?.Count ?? 0);
            int machineCount = SafeCount(() => Registries.Machines?.AllMachines?.Count ?? 0);
            int faunaCount = SafeCount(() => Registries.Fauna?.AllFauna?.Count ?? 0);
            int enemyCount = SafeCount(() => Registries.Enemies?.AllEnemies?.Count ?? 0);
            int npcCount = SafeCount(() => Registries.Npcs?.AllNpcs?.Count ?? 0);

            Debug.Log(
                "Regenerate All complete:\n" +
                $"  Items:    {itemCount}\n" +
                $"  Recipes:  {recipeCount}\n" +
                $"  Machines: {machineCount}\n" +
                $"  Fauna:    {faunaCount}\n" +
                $"  Enemies:  {enemyCount}\n" +
                $"  NPCs:     {npcCount}");
        }

        private static int SafeCount(Func<int> reader)
        {
            try { return reader(); }
            catch { return -1; }
        }
    }
}
#endif
