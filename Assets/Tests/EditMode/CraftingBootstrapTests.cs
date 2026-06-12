#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Crafting;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// V6.4 — Bootstrap Path Validator.
    ///
    /// Walks the recipe graph backwards from the Source items (items with
    /// <c>kind == Source</c>, i.e. mineable / harvestable from the world) to
    /// verify every Core 60 item is reachable from empty hands.
    ///
    /// Algorithm:
    /// <list type="number">
    /// <item><description>Load the project's <see cref="ItemDatabase"/> +
    /// <see cref="RecipeRegistry"/> from Resources/.</description></item>
    /// <item><description>Mark every <c>kind == Source</c> item as reachable
    /// (free from the world).</description></item>
    /// <item><description>BFS: for every not-yet-reachable item, check if ANY
    /// of its recipes has every <c>ingredients[]</c> entry already reachable.
    /// If so, mark it reachable.</description></item>
    /// <item><description>Iterate until no new items get added; assert every
    /// Core 60 item is now reachable.</description></item>
    /// </list>
    ///
    /// Per the V6.1 review note: this walks <c>inputs[]</c> (specific
    /// ingredients) ONLY. The <c>inputProperties[]</c> array is a runtime
    /// fallback for forgiving / hybrid machines, not a progression path -- a
    /// recipe with only inputProperties[] would not be authored as a
    /// bootstrap step, so we ignore it here.
    /// </summary>
    public class CraftingBootstrapTests
    {
        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static ItemDatabase LoadItemDb()
        {
            var db = ItemDatabase.GetOrLoad();
            Assert.IsNotNull(db, "ItemDatabase must be loadable from Resources/ItemDatabase.asset.");
            return db;
        }

        private static RecipeRegistry LoadRecipeRegistry()
        {
            var reg = RecipeRegistry.Instance;
            Assert.IsNotNull(reg, "RecipeRegistry must be loadable from Resources/RecipeRegistry.asset.");
            return reg;
        }

        // ---------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------

        [Test]
        public void BootstrapValidator_EveryCore60IsReachable()
        {
            ItemDatabase db = LoadItemDb();
            RecipeRegistry reg = LoadRecipeRegistry();

            // Pull all items from the database. The Core 60 cap is enforced
            // by the items_core.json source data + the V2.9 loader; we don't
            // re-validate count here, only reachability.
            IReadOnlyList<ItemDefinition> allItems = db.AllItems;
            Assert.Greater(allItems.Count, 0, "ItemDatabase must contain Core 60 items.");

            // Reachable set seeded with Source items (free from the world).
            HashSet<string> reachable = new HashSet<string>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.itemId)) continue;
                if (item.kind == ItemKind.Source) reachable.Add(item.itemId);
            }

            // BFS: each pass, attempt to promote items into reachable[] by
            // walking their recipes. Stop when a full pass adds nothing.
            bool changed = true;
            int passes = 0;
            while (changed && passes < 32)
            {
                changed = false;
                passes++;
                foreach (var item in allItems)
                {
                    if (item == null) continue;
                    if (string.IsNullOrEmpty(item.itemId)) continue;
                    if (reachable.Contains(item.itemId)) continue;

                    // Check every recipe for this output. If ANY recipe has
                    // all inputs[] already reachable, promote this item.
                    foreach (RecipeDefinition recipe in reg.ByOutput(item.itemId))
                    {
                        if (recipe == null) continue;
                        if (RecipeIsReachable(recipe, reachable))
                        {
                            reachable.Add(item.itemId);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            // Build the unreachable list + their failing dependency chains.
            List<ItemDefinition> unreachable = new List<ItemDefinition>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.itemId)) continue;
                if (!reachable.Contains(item.itemId)) unreachable.Add(item);
            }

            if (unreachable.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{unreachable.Count} Core 60 items are not reachable from empty hands:");
                foreach (var item in unreachable)
                {
                    sb.Append("  - ").Append(item.itemId);
                    var recipes = reg.ByOutput(item.itemId).ToList();
                    if (recipes.Count == 0)
                    {
                        sb.AppendLine(" (NO RECIPES; not a Source item either)");
                        continue;
                    }
                    sb.AppendLine();
                    for (int r = 0; r < recipes.Count; r++)
                    {
                        var recipe = recipes[r];
                        sb.Append("      via=").Append(recipe.viaMachineId ?? "(personal-grid)").Append(": ");
                        if (recipe.ingredients == null || recipe.ingredients.Length == 0)
                        {
                            sb.AppendLine("(empty ingredients[])");
                            continue;
                        }
                        bool first = true;
                        foreach (var ing in recipe.ingredients)
                        {
                            if (!first) sb.Append(", ");
                            first = false;
                            string state = reachable.Contains(ing.itemId) ? "OK" : "BLOCKED";
                            sb.Append(ing.itemId).Append("x").Append(ing.qty).Append(" [").Append(state).Append("]");
                        }
                        sb.AppendLine();
                    }
                }
                Assert.Fail(sb.ToString());
            }
        }

        [Test]
        public void BootstrapValidator_PersonalGridRecipesFitIn2x2()
        {
            // Any recipe with viaMachineId == null (personal-grid scope)
            // must have <= 4 ingredient slots so it fits the 2x2 grid.
            //
            // Reports every violation; soft-warns for non-critical items,
            // hard-fails when a critical bootstrap item (workbench in
            // particular -- without it the whole machine tier is locked)
            // cannot fit.
            RecipeRegistry reg = LoadRecipeRegistry();

            // Critical bootstrap items whose personal-grid recipe MUST fit
            // in the 2x2. Any miss here is a hard fail; everything else is
            // a logged warning that the team can triage.
            HashSet<string> criticalBootstrap = new HashSet<string>(new[]
            {
                "workbench",
            });

            List<string> warnings = new List<string>();
            List<string> failures = new List<string>();

            foreach (RecipeDefinition recipe in reg.AllRecipes)
            {
                if (recipe == null) continue;
                if (!string.IsNullOrEmpty(recipe.viaMachineId)) continue;
                int ingredientCount = recipe.ingredients != null ? recipe.ingredients.Length : 0;
                if (ingredientCount <= 4) continue;

                string output = recipe.outputItemId ?? "(unknown)";
                string line = $"{output}: {ingredientCount} ingredients (> 4) [scope=personal-grid]";
                if (criticalBootstrap.Contains(output)) failures.Add(line);
                else warnings.Add(line);
            }

            if (warnings.Count > 0)
            {
                // Surface as a single warning so the report stays compact.
                Debug.LogWarning("[V6.4] " + warnings.Count + " personal-grid recipes have > 4 ingredients (cannot fit 2x2 grid):\n  " +
                    string.Join("\n  ", warnings));
            }

            if (failures.Count > 0)
            {
                Assert.Fail("[V6.4] Critical bootstrap items cannot fit in the 2x2 personal grid:\n  " +
                    string.Join("\n  ", failures));
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// True when every <c>ingredients[]</c> entry of <paramref name="recipe"/>
        /// has its itemId already in <paramref name="reachable"/>. Empty
        /// ingredients[] is treated as unreachable -- a recipe with no
        /// specific inputs is not a progression path (property fallback
        /// is runtime-only per the V6.1 design note).
        /// </summary>
        private static bool RecipeIsReachable(RecipeDefinition recipe, HashSet<string> reachable)
        {
            if (recipe.ingredients == null || recipe.ingredients.Length == 0) return false;
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                string id = recipe.ingredients[i].itemId;
                if (string.IsNullOrEmpty(id)) return false;
                if (!reachable.Contains(id)) return false;
            }
            return true;
        }
    }
}
#endif
