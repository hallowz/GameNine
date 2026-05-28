#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Data;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 6.1 — the full forgiving/picky/hybrid match engine.
    /// Each test builds in-memory synthetic <see cref="RecipeDefinition"/> and
    /// <see cref="ItemDefinition"/> fixtures so the engine is exercised in isolation
    /// from items_core.json content. The synergy-sandbox anchor test
    /// (milk-in-boiler) is included.
    /// </summary>
    public class CraftingMatchEngineTests
    {
        // ---------------------------------------------------------------
        // Fixture helpers
        // ---------------------------------------------------------------

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private ItemDefinition MakeItem(string id, params MaterialProperties[] props)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.properties = props ?? Array.Empty<MaterialProperties>();
            _created.Add(item);
            return item;
        }

        private RecipeDefinition MakeRecipe(
            string output,
            Ingredient[] ingredients,
            InputProperty[] inputProperties = null,
            float efficiency = 1.0f,
            float outputModifier = 1.0f,
            string viaMachine = null)
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.outputItemId = output;
            r.outputQty = 1;
            r.ingredients = ingredients ?? Array.Empty<Ingredient>();
            r.inputProperties = inputProperties;
            r.efficiency = efficiency;
            r.outputModifier = outputModifier;
            r.viaMachineId = viaMachine;
            _created.Add(r);
            return r;
        }

        // ---------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------

        [Test]
        public void Picky_RefusesPropertyFallback()
        {
            // Recipe wants iron_ingot; available has only copper_ingot (also Solid_Metal).
            var ironIngot = MakeItem("iron_ingot", MaterialProperties.Solid_Metal);
            var copperIngot = MakeItem("copper_ingot", MaterialProperties.Solid_Metal);
            var recipe = MakeRecipe(
                output: "iron_plate",
                ingredients: new[] { new Ingredient("iron_ingot", 1) },
                inputProperties: new[] { new InputProperty(MaterialProperties.Solid_Metal, 1, 0.8f) },
                viaMachine: "assembler");

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int> { { "copper_ingot", 4 } };
            var itemDb = new List<ItemDefinition> { ironIngot, copperIngot };

            var result = engine.TryMatch(recipe, MachineProcessType.Picky_Assembly, available, itemDb);

            Assert.IsFalse(result.Matched,
                "Picky machine must refuse property fallback even when a property-compatible item is available.");
            Assert.IsNull(result.recipe);
            Assert.IsFalse(string.IsNullOrEmpty(result.reason));
            StringAssert.Contains("Picky", result.reason);
        }

        [Test]
        public void Forgiving_MatchesSpecificFirst()
        {
            // Furnace + exact iron_ore inputs -> efficiency=1.0, outputModifier=1.0, no substitution.
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var recipe = MakeRecipe(
                output: "iron_ingot",
                ingredients: new[]
                {
                    new Ingredient("iron_ore", 1),
                    new Ingredient("coal", 1),
                },
                viaMachine: "furnace",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int>
            {
                { "iron_ore", 3 },
                { "coal", 3 },
            };
            var itemDb = new List<ItemDefinition> { ironOre, coal };

            var result = engine.TryMatch(recipe, MachineProcessType.Forgiving_Thermal_DryBurn, available, itemDb);

            Assert.IsTrue(result.Matched);
            Assert.AreEqual(1.0f, result.efficiency, 0.0001f, "Specific match must yield recipe's nominal efficiency.");
            Assert.AreEqual(1.0f, result.outputModifier, 0.0001f, "Specific match must yield recipe's nominal output.");
            Assert.AreEqual(2, result.inputBindings.Count);
            Assert.AreEqual("iron_ore", result.inputBindings["iron_ore"]);
            Assert.AreEqual("coal", result.inputBindings["coal"]);
            StringAssert.Contains("Specific", result.reason);
        }

        [Test]
        public void Forgiving_FallsThroughToPropertyMatch()
        {
            // Steam Boiler recipe: inputs[] expects `water`, inputProperties[] expects Liquid_Aqueous + Combustible_*.
            // Available: milk + wood (NOT water + coal). Forgiving fallback should match at reduced efficiency.
            var water = MakeItem("water", MaterialProperties.Liquid_Aqueous);
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous, MaterialProperties.Organic_Fresh);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var wood = MakeItem("wood", MaterialProperties.Combustible_Dry, MaterialProperties.Solid_Fiber);

            var recipe = MakeRecipe(
                output: "steam",
                ingredients: new[]
                {
                    new Ingredient("water", 1),
                    new Ingredient("coal", 1),
                },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 0.4f), // milk's a weak aqueous
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 1.0f),
                },
                viaMachine: "steam_boiler",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int>
            {
                { "milk", 2 },
                { "wood", 2 },
            };
            var itemDb = new List<ItemDefinition> { water, milk, coal, wood };

            var result = engine.TryMatch(recipe, MachineProcessType.Forgiving_Thermal_Boil, available, itemDb);

            Assert.IsTrue(result.Matched, "Forgiving fallback should match milk+wood as Liquid_Aqueous+Combustible_Dry.");
            // efficiency = recipe.efficiency (1.0) * 0.4 (milk slot) * 1.0 (wood slot) = 0.4
            Assert.AreEqual(0.4f, result.efficiency, 0.0001f);
            // forgiving: outputModifier unmodified (1.0).
            Assert.AreEqual(1.0f, result.outputModifier, 0.0001f);
            Assert.AreEqual("milk", result.inputBindings["Liquid_Aqueous"]);
            Assert.AreEqual("wood", result.inputBindings["Combustible_Dry"]);
            StringAssert.Contains("Forgiving", result.reason);
        }

        [Test]
        public void Hybrid_PropertyFallbackCappedAt07()
        {
            // Workbench (Hybrid_Crafting) recipe that declares outputModifier=1.0; when it
            // matches via property-fallback the result must be clamped to 0.7.
            var stick = MakeItem("stick", MaterialProperties.Solid_Fiber);
            var bone = MakeItem("bone", MaterialProperties.Solid_Fiber); // improvised
            var recipe = MakeRecipe(
                output: "rough_handle",
                ingredients: new[] { new Ingredient("stick", 2) },
                inputProperties: new[] { new InputProperty(MaterialProperties.Solid_Fiber, 2, 1.0f) },
                viaMachine: "workbench",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int> { { "bone", 4 } };
            var itemDb = new List<ItemDefinition> { stick, bone };

            var result = engine.TryMatch(recipe, MachineProcessType.Hybrid_Crafting, available, itemDb);

            Assert.IsTrue(result.Matched);
            Assert.LessOrEqual(result.outputModifier, DefaultCraftingMatchEngine.HybridPropertyOutputCap + 0.0001f,
                "Hybrid property fallback must clamp output to 0.7x.");
            Assert.AreEqual(DefaultCraftingMatchEngine.HybridPropertyOutputCap, result.outputModifier, 0.0001f);
            StringAssert.Contains("Hybrid", result.reason);
        }

        [Test]
        public void Engine_PureFunction_NoMutation()
        {
            // Run a property-fallback match and verify the available-items dictionary is unchanged.
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous);
            var wood = MakeItem("wood", MaterialProperties.Combustible_Dry);
            var recipe = MakeRecipe(
                output: "steam",
                ingredients: new[] { new Ingredient("water", 1) },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 0.4f),
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 1.0f),
                });

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int>
            {
                { "milk", 7 },
                { "wood", 9 },
            };
            int milkBefore = available["milk"];
            int woodBefore = available["wood"];
            int countBefore = available.Count;
            var itemDb = new List<ItemDefinition> { milk, wood };

            engine.TryMatch(recipe, MachineProcessType.Forgiving_Thermal_Boil, available, itemDb);

            Assert.AreEqual(countBefore, available.Count, "Engine must not add/remove entries.");
            Assert.AreEqual(milkBefore, available["milk"], "Engine must not consume from inputs.");
            Assert.AreEqual(woodBefore, available["wood"], "Engine must not consume from inputs.");
        }

        [Test]
        public void Engine_ReturnsHumanReadableReason()
        {
            // Both success and failure paths must populate a non-empty `reason`.
            var iron = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            var recipe = MakeRecipe(
                output: "iron_ingot",
                ingredients: new[] { new Ingredient("iron_ore", 1) },
                viaMachine: "furnace");

            var engine = new DefaultCraftingMatchEngine();
            var itemDb = new List<ItemDefinition> { iron };

            // Success path:
            var success = engine.TryMatch(
                recipe,
                MachineProcessType.Forgiving_Thermal_DryBurn,
                new Dictionary<string, int> { { "iron_ore", 4 } },
                itemDb);
            Assert.IsTrue(success.Matched);
            Assert.IsFalse(string.IsNullOrEmpty(success.reason), "Success result must have non-empty reason.");

            // Failure path:
            var failure = engine.TryMatch(
                recipe,
                MachineProcessType.Forgiving_Thermal_DryBurn,
                new Dictionary<string, int>(),
                itemDb);
            Assert.IsFalse(failure.Matched);
            Assert.IsFalse(string.IsNullOrEmpty(failure.reason), "Failure result must have non-empty reason.");
        }

        [Test]
        public void MilkInBoiler_MatchesAtReducedEfficiency()
        {
            // *** The canonical M2 synergy proof. ***
            // Steam boiler recipe: inputs[] = water + coal; inputProperties[] = Liquid_Aqueous + Combustible_*.
            // Available items contain milk (Liquid_Aqueous, Organic_Fresh) and coal (Combustible_Dry).
            // Forgiving_Thermal_Boil should fall through to property match and accept milk for water.
            var water = MakeItem("water", MaterialProperties.Liquid_Aqueous);
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous, MaterialProperties.Organic_Fresh);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);

            var recipe = MakeRecipe(
                output: "steam",
                ingredients: new[]
                {
                    new Ingredient("water", 1),
                    new Ingredient("coal", 1),
                },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 0.4f),
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 1.0f),
                },
                viaMachine: "steam_boiler",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int>
            {
                { "milk", 1 },
                { "coal", 1 },
            };
            var itemDb = new List<ItemDefinition> { water, milk, coal };

            var result = engine.TryMatch(recipe, MachineProcessType.Forgiving_Thermal_Boil, available, itemDb);

            Assert.IsTrue(result.Matched, "Steam Boiler must accept milk via forgiving property fallback.");
            Assert.Less(result.efficiency, 1.0f,
                "Milk is a weaker aqueous than water — efficiency must be reduced.");
            Assert.AreEqual(0.4f, result.efficiency, 0.0001f,
                "Expected efficiency = 1.0 (recipe) * 0.4 (milk slot) * 1.0 (coal slot).");
            Assert.AreEqual("milk", result.inputBindings["Liquid_Aqueous"]);
            Assert.AreEqual("coal", result.inputBindings["Combustible_Dry"]);
        }

        [Test]
        public void Picky_StillMatchesExactInputs()
        {
            // Sanity: picky machines DO match when the specific ingredients are present.
            var crudeOil = MakeItem("crude_oil", MaterialProperties.Liquid_Oil);
            var recipe = MakeRecipe(
                output: "refined_oil",
                ingredients: new[] { new Ingredient("crude_oil", 2) },
                viaMachine: "refinery",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int> { { "crude_oil", 3 } };
            var itemDb = new List<ItemDefinition> { crudeOil };

            var result = engine.TryMatch(recipe, MachineProcessType.Picky_Refinement, available, itemDb);

            Assert.IsTrue(result.Matched, "Picky machines still match when the exact inputs are present.");
            Assert.AreEqual(1.0f, result.efficiency, 0.0001f);
            Assert.AreEqual(1.0f, result.outputModifier, 0.0001f);
        }

        [Test]
        public void Forgiving_PropertyMatch_RespectsRequiredQuantity()
        {
            // The property requirement asks for qty=3 of Solid_Metal, but only qty=1 of a
            // qualifying item is in inputs. Engine must refuse the property match.
            var copperIngot = MakeItem("copper_ingot", MaterialProperties.Solid_Metal);
            var recipe = MakeRecipe(
                output: "metal_plate",
                ingredients: new[] { new Ingredient("iron_ingot", 3) },
                inputProperties: new[] { new InputProperty(MaterialProperties.Solid_Metal, 3, 0.7f) },
                viaMachine: "press",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int> { { "copper_ingot", 1 } };
            var itemDb = new List<ItemDefinition> { copperIngot };

            var result = engine.TryMatch(recipe, MachineProcessType.Forgiving_Pressure, available, itemDb);

            Assert.IsFalse(result.Matched,
                "Property fallback must respect the InputProperty.qty (1 copper can't cover 3 Solid_Metal).");
            Assert.IsFalse(string.IsNullOrEmpty(result.reason));
        }

        [Test]
        public void Hybrid_MatchesSpecificWithFullOutput()
        {
            // Hybrid_Crafting: when the specific ingredients ARE present, the 0.7 cap does NOT apply —
            // it only kicks in on the property-fallback path. Confirm specific outputs are honoured.
            var stick = MakeItem("stick", MaterialProperties.Solid_Fiber);
            var recipe = MakeRecipe(
                output: "rough_handle",
                ingredients: new[] { new Ingredient("stick", 2) },
                inputProperties: new[] { new InputProperty(MaterialProperties.Solid_Fiber, 2, 1.0f) },
                viaMachine: "workbench",
                efficiency: 1.0f,
                outputModifier: 1.0f);

            var engine = new DefaultCraftingMatchEngine();
            var available = new Dictionary<string, int> { { "stick", 4 } };
            var itemDb = new List<ItemDefinition> { stick };

            var result = engine.TryMatch(recipe, MachineProcessType.Hybrid_Crafting, available, itemDb);

            Assert.IsTrue(result.Matched);
            Assert.AreEqual(1.0f, result.outputModifier, 0.0001f,
                "Hybrid specific-match must NOT be capped — the 0.7 cap is for property-fallback only.");
        }
    }
}
#endif
