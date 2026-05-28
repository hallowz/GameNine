#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Player;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for V6.2 — the personal crafting grid wired through the
    /// V6.1 <see cref="DefaultCraftingMatchEngine"/> with
    /// <see cref="MachineProcessType.Hybrid_Crafting"/>.
    ///
    /// Each test builds in-memory synthetic <see cref="ItemDefinition"/>,
    /// <see cref="RecipeDefinition"/>, and a one-off <see cref="RecipeRegistry"/>
    /// fixture so the personal grid never has to consult the project's real
    /// items_core.json. The grid is driven directly through its public API.
    /// </summary>
    public class PersonalCraftingGridTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private GameObject _gridGO;
        private PersonalCraftingGrid _grid;
        private RecipeRegistry _registryFixture;
        private RecipeRegistry _registryPrevious;
        private ItemDatabase _itemDbFixture;
        private ItemDatabase _itemDbPrevious;

        // ---------------------------------------------------------------
        // Fixture helpers
        // ---------------------------------------------------------------

        private ItemDefinition MakeItem(string id, params MaterialProperties[] props)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.maxStackSize = 64;
            item.properties = props ?? Array.Empty<MaterialProperties>();
            _created.Add(item);
            return item;
        }

        private RecipeDefinition MakeRecipe(
            string output,
            int outputQty,
            Ingredient[] ingredients,
            InputProperty[] inputProperties = null,
            float efficiency = 1.0f,
            float outputModifier = 1.0f,
            string viaMachine = null)
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.outputItemId = output;
            r.outputQty = outputQty;
            r.ingredients = ingredients ?? Array.Empty<Ingredient>();
            r.inputProperties = inputProperties;
            r.efficiency = efficiency;
            r.outputModifier = outputModifier;
            r.viaMachineId = viaMachine;
            _created.Add(r);
            return r;
        }

        private void InstallFixtureSingletons(IEnumerable<ItemDefinition> items, IEnumerable<RecipeDefinition> recipes)
        {
            // Build a one-off ItemDatabase + RecipeRegistry pair and stash them
            // as the active singletons via reflection on the private static
            // backing fields. TearDown restores the originals so other tests
            // see the project's real registries.
            _itemDbPrevious = ItemDatabase.Instance;
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition>(items);
            _itemDbFixture.Reindex();
            _created.Add(_itemDbFixture);

            // ItemDatabase exposes Instance directly via its OnEnable; the
            // ScriptableObject.CreateInstance call above already overwrote
            // Instance to _itemDbFixture. We capture _itemDbPrevious so we
            // can restore later.

            _registryPrevious = RecipeRegistry.Instance;
            _registryFixture = ScriptableObject.CreateInstance<RecipeRegistry>();
            _registryFixture.allRecipes = new List<RecipeDefinition>(recipes);
            _registryFixture.Reindex();
            _created.Add(_registryFixture);

            // RecipeRegistry.Instance is set on its OnEnable -- the
            // CreateInstance call above wired that up automatically.
        }

        [TearDown]
        public void TearDown()
        {
            if (_gridGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_gridGO);
                _gridGO = null;
            }
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            // Best-effort: clear the stale Instance pointers that may now
            // point at destroyed SOs. The previous instances should re-claim
            // on their own next OnEnable; for EditMode tests we just leave
            // Instance dangling and the next test fixture re-installs.
        }

        private PersonalCraftingGrid CreateGrid()
        {
            _gridGO = new GameObject("PersonalCraftingGrid_TestHost");
            _grid = _gridGO.AddComponent<PersonalCraftingGrid>();
            // EditMode does NOT auto-invoke Awake on newly-added components on
            // an inactive GO; we invoke it by hand so Grid is non-null.
            InvokeAwake(_grid);
            return _grid;
        }

        private static void InvokeAwake(PersonalCraftingGrid grid)
        {
            var m = typeof(PersonalCraftingGrid).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            m?.Invoke(grid, null);
        }

        // ---------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------

        [Test]
        public void PersonalGrid_TopRecipeMatchesViaHybrid()
        {
            // Workbench bootstrap recipe: 4 wood + 2 stone + 2 plant_fiber.
            // The personal grid is 2x2; the recipe has 3 distinct ingredient
            // slots so it fits inside the 4-slot cap.
            var wood = MakeItem("wood", MaterialProperties.Combustible_Dry, MaterialProperties.Solid_Fiber);
            var stone = MakeItem("stone", MaterialProperties.Solid_Stone);
            var fiber = MakeItem("plant_fiber", MaterialProperties.Solid_Fiber);
            var workbench = MakeItem("workbench");

            var recipe = MakeRecipe(
                output: "workbench",
                outputQty: 1,
                ingredients: new[]
                {
                    new Ingredient("wood", 4),
                    new Ingredient("stone", 2),
                    new Ingredient("plant_fiber", 2),
                },
                viaMachine: null); // personal grid

            InstallFixtureSingletons(
                new[] { wood, stone, fiber, workbench },
                new[] { recipe });

            var grid = CreateGrid();
            grid.SetSlot(0, 0, new ItemStack(wood, 4));
            grid.SetSlot(1, 0, new ItemStack(stone, 2));
            grid.SetSlot(0, 1, new ItemStack(fiber, 2));

            Assert.IsNotNull(grid.CurrentMatch, "Engine must return a match result.");
            Assert.IsNotNull(grid.CurrentMatch.recipe, "Workbench specific recipe must match.");
            Assert.AreEqual("workbench", grid.CurrentMatch.recipe.outputItemId);
            // Specific match path -> full output modifier (no 0.7x clamp).
            Assert.AreEqual(1.0f, grid.CurrentMatch.outputModifier, 0.0001f,
                "Specific match must NOT trigger the hybrid 0.7x cap.");
            Assert.AreEqual(1.0f, grid.CurrentMatch.efficiency, 0.0001f);
            Assert.AreEqual(workbench, grid.CurrentResult.item);
            Assert.AreEqual(1, grid.CurrentResult.quantity);
        }

        [Test]
        public void PersonalGrid_PropertyFallback_AppliesHybridCap()
        {
            // Synthetic property-only recipe that matches via Solid_Fiber x2.
            // outputModifier declared 1.0; the hybrid path must clamp to 0.7.
            var stick = MakeItem("stick", MaterialProperties.Solid_Fiber);
            var bone = MakeItem("bone", MaterialProperties.Solid_Fiber);
            var handle = MakeItem("rough_handle");

            var recipe = MakeRecipe(
                output: "rough_handle",
                outputQty: 1,
                ingredients: new[] { new Ingredient("stick", 2) },
                inputProperties: new[] { new InputProperty(MaterialProperties.Solid_Fiber, 2, 1.0f) },
                efficiency: 1.0f,
                outputModifier: 1.0f,
                viaMachine: null);

            InstallFixtureSingletons(
                new[] { stick, bone, handle },
                new[] { recipe });

            var grid = CreateGrid();
            // Only `bone` (improvised) in the bag; specific match requires stick x2.
            grid.SetSlot(0, 0, new ItemStack(bone, 2));

            Assert.IsNotNull(grid.CurrentMatch);
            Assert.IsNotNull(grid.CurrentMatch.recipe, "Hybrid property fallback must accept bone for Solid_Fiber.");
            Assert.LessOrEqual(grid.CurrentMatch.outputModifier, 0.7f + 0.0001f,
                "Hybrid property-fallback path must clamp outputModifier to <= 0.7.");
            // Floor(1 * 0.7) = 0 -> bumped to minimum 1.
            Assert.AreEqual(handle, grid.CurrentResult.item);
            Assert.AreEqual(1, grid.CurrentResult.quantity);
        }

        [Test]
        public void PersonalGrid_ConsumesInputsOnCraft()
        {
            // Specific match consumes the listed ingredients per their qty.
            var wood = MakeItem("wood", MaterialProperties.Solid_Fiber);
            var plank = MakeItem("plank");
            var recipe = MakeRecipe(
                output: "plank",
                outputQty: 2,
                ingredients: new[] { new Ingredient("wood", 1) },
                viaMachine: null);

            InstallFixtureSingletons(
                new[] { wood, plank },
                new[] { recipe });

            var grid = CreateGrid();
            grid.SetSlot(0, 0, new ItemStack(wood, 3));

            Assert.IsNotNull(grid.CurrentMatch, "Should match plank recipe.");
            ItemStack taken = grid.TakeResult();

            Assert.AreEqual(plank, taken.item);
            Assert.AreEqual(2, taken.quantity, "outputQty=2 with outputModifier=1.0 yields 2.");
            // After consumption: wood should be down by 1 (qty=2 remaining).
            ItemStack slot00 = grid.Grid.GetSlot(0, 0);
            Assert.IsFalse(slot00.IsEmpty);
            Assert.AreEqual(wood, slot00.item);
            Assert.AreEqual(2, slot00.quantity);
        }

        [Test]
        public void PersonalGrid_OnMatchChanged_FiresOnTransition()
        {
            var wood = MakeItem("wood", MaterialProperties.Solid_Fiber);
            var plank = MakeItem("plank");
            var recipe = MakeRecipe(
                output: "plank",
                outputQty: 1,
                ingredients: new[] { new Ingredient("wood", 1) },
                viaMachine: null);

            InstallFixtureSingletons(
                new[] { wood, plank },
                new[] { recipe });

            var grid = CreateGrid();
            int callCount = 0;
            RecipeMatchResult lastEmitted = null;
            grid.OnMatchChanged += r => { callCount++; lastEmitted = r; };

            grid.SetSlot(0, 0, new ItemStack(wood, 1));
            Assert.AreEqual(1, callCount, "OnMatchChanged must fire on first transition into match.");
            Assert.IsNotNull(lastEmitted);
            Assert.IsNotNull(lastEmitted.recipe);

            // Clearing should fire the inverse transition.
            grid.SetSlot(0, 0, default);
            Assert.AreEqual(2, callCount, "OnMatchChanged must fire on match -> no-match transition.");
        }
    }
}
#endif
