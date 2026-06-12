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
    /// EditMode tests for V6.3 — <see cref="MachineCraftingStation"/> wired
    /// through the V6.1 <see cref="DefaultCraftingMatchEngine"/> against each
    /// process type from <see cref="MachineProcessType"/>.
    ///
    /// Tests instantiate the station programmatically (V9.1 will wire it onto
    /// placed prefabs) and drive its tick via the <c>AdvanceTick</c> /
    /// <c>ForceCompleteActiveRecipe</c> seams so EditMode runs deterministically
    /// without a Unity update loop.
    /// </summary>
    public class MachineCraftingStationTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private GameObject _stationGO;
        private ItemDatabase _itemDbFixture;

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

        private MachineDefinition MakeMachine(string id, MachineProcessType processType,
                                              int gridWidth = 3, int gridHeight = 3,
                                              bool needsPower = false)
        {
            var m = ScriptableObject.CreateInstance<MachineDefinition>();
            m.itemId = id;
            m.displayName = id;
            m.gridWidth = gridWidth;
            m.gridHeight = gridHeight;
            m.processType = processType;
            m.needsPower = needsPower;
            _created.Add(m);
            return m;
        }

        private void InstallItemDb(IEnumerable<ItemDefinition> items)
        {
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition>(items);
            _itemDbFixture.Reindex();
            _created.Add(_itemDbFixture);
            // OnEnable assigns Instance.
        }

        private MachineCraftingStation CreateStation(MachineDefinition def)
        {
            _stationGO = new GameObject("MachineCraftingStation_TestHost");
            var station = _stationGO.AddComponent<MachineCraftingStation>();
            station.Init(def);
            return station;
        }

        [TearDown]
        public void TearDown()
        {
            if (_stationGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_stationGO);
                _stationGO = null;
            }
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------

        [Test]
        public void MachineStation_StartsRecipeOnTryStart()
        {
            // Furnace + exact iron_ore -> iron_ingot specific match.
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            var ironIngot = MakeItem("iron_ingot", MaterialProperties.Solid_Metal);
            var furnace = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var recipe = MakeRecipe(
                output: "iron_ingot",
                outputQty: 1,
                ingredients: new[] { new Ingredient("iron_ore", 1) },
                viaMachine: "furnace");

            InstallItemDb(new[] { ironOre, ironIngot });

            var station = CreateStation(furnace);
            station.SetInput(0, new ItemStack(ironOre, 2));

            station.TryStartRecipe(recipe);

            Assert.IsTrue(station.IsRunning, "Station should be running after TryStartRecipe with valid inputs.");
            // The tick coroutine drives Progress in PlayMode; advance manually
            // here so EditMode shows the non-zero state.
            station.AdvanceTick(0.5f);
            Assert.Greater(station.Progress, 0f, "Progress should be > 0 after a tick.");
        }

        [Test]
        public void MachineStation_PickyRefusesPropertySubstitution()
        {
            // Steam generator: Picky_Specialty. Recipe declares inputProperties[]
            // but the engine + station must refuse the substitution.
            var ironIngot = MakeItem("iron_ingot", MaterialProperties.Solid_Metal);
            var copperIngot = MakeItem("copper_ingot", MaterialProperties.Solid_Metal, MaterialProperties.Conducts_Electric);
            var steamGen = MakeMachine("steam_generator", MachineProcessType.Picky_Specialty);
            var recipe = MakeRecipe(
                output: "steam_generator",
                outputQty: 1,
                ingredients: new[]
                {
                    new Ingredient("iron_ingot", 1),
                },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Solid_Metal, 1, 0.8f),
                },
                viaMachine: "steam_generator");

            InstallItemDb(new[] { ironIngot, copperIngot });

            var station = CreateStation(steamGen);
            // Feed only copper -- a property-compatible substitute.
            station.SetInput(0, new ItemStack(copperIngot, 4));

            station.TryStartRecipe(recipe);

            Assert.IsFalse(station.IsRunning,
                "Picky_Specialty machine must refuse a property substitution; no recipe should be active.");
            // Inputs untouched.
            Assert.AreEqual(4, station.Inputs[0].quantity,
                "Refused recipe must not consume any inputs.");
        }

        [Test]
        public void MachineStation_ForgivingThermalAcceptsMilkInBoiler()
        {
            // The canonical M2 acceptance proof at the station wiring level:
            // a Forgiving_Thermal_Boil machine accepts milk in place of water
            // via the V6.1 property-fallback path, at reduced efficiency.
            var water = MakeItem("water", MaterialProperties.Liquid_Aqueous);
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous, MaterialProperties.Organic_Fresh);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var steam = MakeItem("steam");
            var boiler = MakeMachine("steam_boiler", MachineProcessType.Forgiving_Thermal_Boil);
            var recipe = MakeRecipe(
                output: "steam",
                outputQty: 1,
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

            InstallItemDb(new[] { water, milk, coal, steam });

            var station = CreateStation(boiler);
            // Feed milk + coal (not water).
            station.SetInput(0, new ItemStack(milk, 1));
            station.SetInput(1, new ItemStack(coal, 1));

            station.TryStartRecipe(recipe);

            Assert.IsTrue(station.IsRunning,
                "Forgiving_Thermal_Boil must accept milk as Liquid_Aqueous via property fallback.");
            Assert.IsNotNull(station.ActiveMatch);
            Assert.AreEqual(0.4f, station.ActiveMatch.efficiency, 0.0001f,
                "Milk-as-aqueous efficiency must be 0.4 (per inputProperty.efficiency).");
            // The NeedsFuel flag should be true for Forgiving_Thermal_Boil per spec.
            Assert.IsTrue(station.NeedsFuel, "Steam boiler is a thermal machine; NeedsFuel must be true.");
        }

        [Test]
        public void MachineStation_ConsumesInputsOnRecipeStart()
        {
            // After TryStartRecipe succeeds, the input bag should be drained
            // per the recipe's ingredient quantities.
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            var ironIngot = MakeItem("iron_ingot", MaterialProperties.Solid_Metal);
            var furnace = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var recipe = MakeRecipe(
                output: "iron_ingot",
                outputQty: 1,
                ingredients: new[] { new Ingredient("iron_ore", 2) },
                viaMachine: "furnace");

            InstallItemDb(new[] { ironOre, ironIngot });

            var station = CreateStation(furnace);
            station.SetInput(0, new ItemStack(ironOre, 3));

            station.TryStartRecipe(recipe);

            Assert.IsTrue(station.IsRunning);
            // The first slot held qty=3, recipe needed 2 -> 1 left.
            Assert.AreEqual(1, station.Inputs[0].quantity, "Recipe must consume the matched ingredient quantities.");
        }

        [Test]
        public void MachineStation_ProducesOutputOnRecipeComplete()
        {
            // Advance the recipe to completion -- output slot must hold the
            // produced item with floor(outputQty * outputModifier) qty.
            var wood = MakeItem("wood", MaterialProperties.Combustible_Dry);
            var charcoal = MakeItem("charcoal", MaterialProperties.Combustible_Dry);
            var furnace = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var recipe = MakeRecipe(
                output: "charcoal",
                outputQty: 1,
                ingredients: new[] { new Ingredient("wood", 1) },
                viaMachine: "furnace");

            InstallItemDb(new[] { wood, charcoal });

            var station = CreateStation(furnace);
            station.SetInput(0, new ItemStack(wood, 1));

            station.TryStartRecipe(recipe);
            Assert.IsTrue(station.IsRunning);

            station.ForceCompleteActiveRecipe();

            Assert.IsFalse(station.IsRunning, "Recipe must clear after completion.");
            Assert.AreEqual(charcoal, station.Outputs[0].item, "Output slot must hold the recipe's output item.");
            Assert.AreEqual(1, station.Outputs[0].quantity);
        }

        [Test]
        public void MachineStation_CancelRefundsConsumedInputs()
        {
            // Canceling a running recipe refunds the consumed inputs back
            // into the grid (matches the V6.3 design note "drop on the floor"
            // for overflow but for a fresh refund there's room).
            var wood = MakeItem("wood", MaterialProperties.Combustible_Dry);
            var charcoal = MakeItem("charcoal");
            var furnace = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var recipe = MakeRecipe(
                output: "charcoal",
                outputQty: 1,
                ingredients: new[] { new Ingredient("wood", 2) },
                viaMachine: "furnace");

            InstallItemDb(new[] { wood, charcoal });

            var station = CreateStation(furnace);
            station.SetInput(0, new ItemStack(wood, 5));

            station.TryStartRecipe(recipe);
            Assert.IsTrue(station.IsRunning);
            // qty was 5, recipe needed 2 -> 3 left.
            Assert.AreEqual(3, station.Inputs[0].quantity);

            station.CancelRecipe();

            Assert.IsFalse(station.IsRunning);
            // Refund returns the 2 consumed back into the same slot.
            Assert.AreEqual(5, station.Inputs[0].quantity, "Cancel must refund the 2 wood consumed by the recipe.");
        }

        [Test]
        public void MachineStation_NeedsPowerReflectsMachineDef()
        {
            var crusher = MakeMachine("crusher", MachineProcessType.Forgiving_Mechanical_Crush, needsPower: true);
            InstallItemDb(Array.Empty<ItemDefinition>());
            var station = CreateStation(crusher);
            Assert.IsTrue(station.NeedsPower, "NeedsPower must mirror machineDef.needsPower.");
        }
    }
}
#endif
