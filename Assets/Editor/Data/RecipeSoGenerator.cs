#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.3 — generates one <see cref="RecipeDefinition"/> ScriptableObject
    /// per entry in every item's <c>recipes[]</c> array in
    /// <c>Design Documents/GameDesign/data/items.json</c>. Also writes the
    /// <see cref="RecipeRegistry"/> singleton consumed at runtime.
    ///
    /// Idempotent: re-running updates fields on existing assets in place; new
    /// entries are created; orphan asset files whose recipe no longer exists in
    /// the JSON are deleted before the generation pass.
    ///
    /// Asset path convention: one file per recipe at
    /// <c>Assets/ScriptableObjects/Generated/Recipes/{outputId}__r{idx}.asset</c>
    /// where <c>idx</c> is the position of the recipe in the item's
    /// <c>recipes[]</c> array (0-based).
    /// </summary>
    public static class RecipeSoGenerator
    {
        private const string GeneratedRecipesFolder = "Assets/ScriptableObjects/Generated/Recipes";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RecipeRegistryAssetPath = "Assets/Resources/RecipeRegistry.asset";

        // Markers parsed from the JSON 'notes' field. Case-insensitive substring match.
        private const string BootstrapMarker = "BOOTSTRAP";
        private const string SynergyMarker = "SYNERGY";

        [MenuItem("Voidborne/Generate/Recipes")]
        public static void Generate()
        {
            // Always read fresh JSON for a generation pass.
            GameDesignJsonLoader.ClearCache();

            Dictionary<string, ItemJson> items;
            try
            {
                items = GameDesignJsonLoader.LoadItems();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecipeSoGenerator] Failed to load items.json: {ex.Message}");
                return;
            }

            if (items == null || items.Count == 0)
            {
                Debug.LogError("[RecipeSoGenerator] items.json is empty — aborting.");
                return;
            }

            EnsureFolder(GeneratedRecipesFolder);
            EnsureFolder(ResourcesFolder);

            // ---------------------------------------------------------------
            // Pre-clean: build expected asset filenames first so we can delete
            // orphans from prior runs (items that lost a recipe / were renamed).
            // ---------------------------------------------------------------
            var expectedFileNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in items)
            {
                string outputId = kvp.Key;
                var item = kvp.Value;
                if (item?.recipes == null) continue;
                for (int i = 0; i < item.recipes.Count; i++)
                {
                    expectedFileNames.Add(BuildAssetFileName(outputId, i));
                }
            }

            int deletedOrphans = DeleteOrphans(expectedFileNames);

            // ---------------------------------------------------------------
            // Generation pass
            // ---------------------------------------------------------------
            int created = 0;
            int updated = 0;
            int processed = 0;
            var generatedRecipes = new List<RecipeDefinition>(expectedFileNames.Count);

            // Sort keys for deterministic asset ordering.
            var sortedKeys = items.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var outputId in sortedKeys)
                {
                    ItemJson item = items[outputId];
                    if (item?.recipes == null || item.recipes.Count == 0) continue;

                    // The parent item's `role` may also carry the BOOTSTRAP
                    // marker (e.g. workbench's role is "T1 — 3×3 crafting
                    // (BOOTSTRAP)"). When set, the via==null recipe(s) for
                    // that item inherit the bootstrap flag — this captures the
                    // canonical hand-built starter recipes whose `notes` field
                    // does not itself repeat the marker.
                    bool itemRoleIsBootstrap = ContainsMarker(item.role, BootstrapMarker);

                    for (int i = 0; i < item.recipes.Count; i++)
                    {
                        RecipeJson r = item.recipes[i];
                        if (r == null) continue;

                        string fileName = BuildAssetFileName(outputId, i);
                        string assetPath = $"{GeneratedRecipesFolder}/{fileName}";

                        bool isNew = false;
                        var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(assetPath);
                        if (asset == null)
                        {
                            asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                            AssetDatabase.CreateAsset(asset, assetPath);
                            isNew = true;
                        }

                        PopulateRecipe(asset, outputId, r, itemRoleIsBootstrap);
                        EditorUtility.SetDirty(asset);

                        if (isNew) created++; else updated++;
                        processed++;

                        generatedRecipes.Add(asset);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // ---------------------------------------------------------------
            // Registry refresh
            // ---------------------------------------------------------------
            RecipeRegistry registry = LoadOrCreateRegistry();

            // Sort by outputItemId, then by recipe index suffix in the asset
            // path so the registry's list is deterministic across runs.
            registry.allRecipes = generatedRecipes
                .OrderBy(rd => rd.outputItemId, StringComparer.Ordinal)
                .ThenBy(rd => AssetDatabase.GetAssetPath(rd), StringComparer.Ordinal)
                .ToList();
            registry.Reindex();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RecipeSoGenerator] Processed {processed} recipes ({created} created, " +
                $"{updated} updated, {deletedOrphans} orphans deleted). " +
                $"Registry at {RecipeRegistryAssetPath} with {registry.allRecipes.Count} entries.");
        }

        // -------------------------------------------------------------------
        // Field population
        // -------------------------------------------------------------------

        private static void PopulateRecipe(
            RecipeDefinition asset,
            string outputId,
            RecipeJson r,
            bool itemRoleIsBootstrap)
        {
            asset.outputItemId = outputId;

            // JSON does not carry an output quantity. Leave the default (1)
            // unless a future schema introduces one.
            if (asset.outputQty <= 0) asset.outputQty = 1;

            // viaMachineId: preserve the null-or-empty distinction consistently
            // by collapsing both to null at write time (registry normalises
            // either back to "" for lookup).
            asset.viaMachineId = string.IsNullOrEmpty(r.via) ? null : r.via;

            asset.notes = r.notes ?? string.Empty;

            // Bootstrap detection has two signals: an explicit marker in the
            // recipe's own notes (covers post-workbench hand-craft fallbacks
            // such as the crude-smelt furnace, hand-pressed circuit, etc.),
            // OR the parent item's role string contains the marker AND this
            // recipe is hand-built (via == null). The latter captures the
            // workbench itself, whose role declares it the BOOTSTRAP machine.
            bool notesBootstrap = ContainsMarker(asset.notes, BootstrapMarker);
            bool inferredBootstrap = itemRoleIsBootstrap && asset.viaMachineId == null;
            asset.isBootstrap = notesBootstrap || inferredBootstrap;

            asset.isSynergy = ContainsMarker(asset.notes, SynergyMarker);

            // Ingredients
            if (r.inputs == null || r.inputs.Count == 0)
            {
                asset.ingredients = Array.Empty<Ingredient>();
            }
            else
            {
                var ingredients = new Ingredient[r.inputs.Count];
                for (int j = 0; j < r.inputs.Count; j++)
                {
                    var input = r.inputs[j];
                    ingredients[j] = new Ingredient(
                        input?.id ?? string.Empty,
                        input?.qty ?? 0);
                }
                asset.ingredients = ingredients;
            }
        }

        // -------------------------------------------------------------------
        // Orphan cleanup
        // -------------------------------------------------------------------

        private static int DeleteOrphans(HashSet<string> expectedFileNames)
        {
            int deleted = 0;

            if (!AssetDatabase.IsValidFolder(GeneratedRecipesFolder)) return 0;

            // Use the raw filesystem path because AssetDatabase.FindAssets does
            // not enumerate the folder contents efficiently for arbitrary file
            // patterns; the .asset files are guaranteed to live directly in the
            // recipes folder.
            string absFolder = Path.GetFullPath(GeneratedRecipesFolder);
            if (!Directory.Exists(absFolder)) return 0;

            foreach (string filePath in Directory.GetFiles(absFolder, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (expectedFileNames.Contains(fileName)) continue;

                // Convert back to a project-relative asset path for AssetDatabase.
                string relPath = $"{GeneratedRecipesFolder}/{fileName}";
                if (AssetDatabase.DeleteAsset(relPath))
                {
                    Debug.Log($"[RecipeSoGenerator] Deleted orphan recipe asset: {relPath}");
                    deleted++;
                }
                else
                {
                    Debug.LogWarning($"[RecipeSoGenerator] Failed to delete orphan recipe asset: {relPath}");
                }
            }

            return deleted;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static string BuildAssetFileName(string outputId, int recipeIndex)
        {
            return $"{outputId}__r{recipeIndex}.asset";
        }

        private static bool ContainsMarker(string text, string marker)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker)) return false;
            return text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static RecipeRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<RecipeRegistry>(RecipeRegistryAssetPath);
            if (reg != null) return reg;

            // Defensive: if an asset exists at that path under a stale class
            // binding (e.g. left over from an earlier iteration), delete and
            // recreate so the new RecipeRegistry script owns it.
            if (AssetDatabase.LoadMainAssetAtPath(RecipeRegistryAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RecipeRegistryAssetPath);
            }

            reg = ScriptableObject.CreateInstance<RecipeRegistry>();
            AssetDatabase.CreateAsset(reg, RecipeRegistryAssetPath);
            return reg;
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
