#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Automation.Machines;
using Voidborne.Automation.Transport;
using ConveyorBelt = Voidborne.Automation.Transport.ConveyorBelt;
using Inserter = Voidborne.Automation.Transport.Inserter;
using Voidborne.Building;
using Voidborne.Core;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Power;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// V9.1 / V9.2 / V9.5 — EditMode coverage for the M2 machine runtime
    /// stack: MachineRuntime facade, the Core 8 subclasses, the storage
    /// chest container API, and the Conveyor + Inserter transport pair.
    ///
    /// <para>The final test in this file -- <c>M2_MilkInBoilerProducesPowerEndToEnd</c>
    /// -- is the canonical M2 acceptance proof: feed milk + coal into a
    /// boiler, drive ticks, observe the adjacent SteamGenerator producing
    /// power and a battery's StoredWs increasing.</para>
    /// </summary>
    public class MachineRuntimeTests
    {
        // ---------------------------------------------------------------
        //  Fixture state
        // ---------------------------------------------------------------

        private readonly List<GameObject> _hosts = new List<GameObject>();
        private readonly List<UnityEngine.Object> _soCreated = new List<UnityEngine.Object>();
        private ItemDatabase _itemDbFixture;

        // ---------------------------------------------------------------
        //  Fixture helpers
        // ---------------------------------------------------------------

        private ItemDefinition MakeItem(string id, params MaterialProperties[] props)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.maxStackSize = 64;
            item.properties = props ?? Array.Empty<MaterialProperties>();
            _soCreated.Add(item);
            return item;
        }

        private RecipeDefinition MakeRecipe(
            string output, int outputQty, Ingredient[] ingredients,
            InputProperty[] inputProperties = null, string viaMachine = null,
            float efficiency = 1.0f, float outputModifier = 1.0f)
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.outputItemId = output;
            r.outputQty = outputQty;
            r.ingredients = ingredients ?? Array.Empty<Ingredient>();
            r.inputProperties = inputProperties;
            r.viaMachineId = viaMachine;
            r.efficiency = efficiency;
            r.outputModifier = outputModifier;
            _soCreated.Add(r);
            return r;
        }

        private MachineDefinition MakeMachine(string id, MachineProcessType processType,
                                              bool needsPower = false, int powerDrawWatts = 0,
                                              int gridWidth = 3, int gridHeight = 3)
        {
            var m = ScriptableObject.CreateInstance<MachineDefinition>();
            m.itemId = id;
            m.displayName = id;
            m.gridWidth = gridWidth;
            m.gridHeight = gridHeight;
            m.processType = processType;
            m.needsPower = needsPower;
            m.powerDrawWatts = powerDrawWatts;
            _soCreated.Add(m);
            return m;
        }

        private void InstallItemDb(IEnumerable<ItemDefinition> items)
        {
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition>(items);
            _itemDbFixture.Reindex();
            _soCreated.Add(_itemDbFixture);
        }

        private GameObject NewHost(string name)
        {
            var go = new GameObject(name);
            _hosts.Add(go);
            return go;
        }

        private T AddRuntime<T>(GameObject host, MachineDefinition def) where T : MachineRuntime
        {
            // Order matters: MachineCraftingStation must be present before
            // MachineRuntime because MachineRuntime has [RequireComponent].
            var station = host.GetComponent<MachineCraftingStation>();
            if (station == null) station = host.AddComponent<MachineCraftingStation>();
            station.Init(def);
            T runtime = host.GetComponent<T>();
            if (runtime == null) runtime = host.AddComponent<T>();
            runtime.ForceInitialize();
            return runtime;
        }

        [SetUp]
        public void SetUp()
        {
            PowerNetwork.ResetInstance();
            BlockRegistry.ResetInstance();
            var _ = PowerNetwork.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _hosts)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _hosts.Clear();
            foreach (var obj in _soCreated)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _soCreated.Clear();
            PowerNetwork.ResetInstance();
            BlockRegistry.ResetInstance();
            _itemDbFixture = null;
        }

        // ===============================================================
        //  V9.1 — MachineRuntime base
        // ===============================================================

        [Test]
        public void MachineRuntime_BindsMachineDefThroughStation()
        {
            // The base MachineRuntime should expose the same MachineDefinition
            // the underlying station was initialised with.
            var workbench = MakeMachine("workbench", MachineProcessType.Hybrid_Crafting);
            var host = NewHost("workbench_host");
            var rt = AddRuntime<WorkbenchRuntime>(host, workbench);

            Assert.IsNotNull(rt.MachineDef, "MachineRuntime.MachineDef must reflect the underlying station's def.");
            Assert.AreEqual("workbench", rt.MachineDef.itemId,
                "MachineRuntime.MachineDef should be the same SO Init was called with.");
        }

        [Test]
        public void MachineRuntime_InteractPromptUsesDisplayName()
        {
            // The IInteractable contract should advertise the machine's display
            // name in the prompt so the InteractionPromptUI reads correctly.
            var workbench = MakeMachine("workbench", MachineProcessType.Hybrid_Crafting);
            workbench.displayName = "Workbench";
            var host = NewHost("workbench_host");
            var rt = AddRuntime<WorkbenchRuntime>(host, workbench);

            Assert.That(rt.InteractPrompt, Does.Contain("Workbench"),
                "MachineRuntime.InteractPrompt should mention the machine's display name.");
        }

        [Test]
        public void MachineRuntime_CanInteractWithinRange()
        {
            var workbench = MakeMachine("workbench", MachineProcessType.Hybrid_Crafting);
            var host = NewHost("workbench_host");
            host.transform.position = Vector3.zero;
            var rt = AddRuntime<WorkbenchRuntime>(host, workbench);

            Assert.IsTrue(rt.CanInteract(new Vector3(1f, 0f, 0f)),
                "Player 1m away should be able to interact with a 4m-range machine.");
            Assert.IsFalse(rt.CanInteract(new Vector3(50f, 0f, 0f)),
                "Player 50m away should NOT be able to interact.");
        }

        // ===============================================================
        //  V9.2 — Core 8 subclasses
        // ===============================================================

        [Test]
        public void FurnaceRuntime_CraftsIronIngotFromIronOre()
        {
            // End-to-end shape: the V6.3 station + V9.2 FurnaceRuntime can run
            // a furnace recipe to completion and deposit the output.
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            var ironIngot = MakeItem("iron_ingot", MaterialProperties.Solid_Metal);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var furnace = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var recipe = MakeRecipe(
                output: "iron_ingot", outputQty: 1,
                ingredients: new[] { new Ingredient("iron_ore", 1) },
                viaMachine: "furnace");

            InstallItemDb(new[] { ironOre, ironIngot, coal });

            var host = NewHost("furnace_host");
            var rt = AddRuntime<FurnaceRuntime>(host, furnace);
            var station = rt.Station;

            station.SetInput(0, new ItemStack(ironOre, 1));
            station.SetFuel(new ItemStack(coal, 1));
            station.TryStartRecipe(recipe);
            Assert.IsTrue(station.IsRunning, "Furnace recipe must start with valid inputs.");

            station.ForceCompleteActiveRecipe();
            Assert.IsFalse(station.IsRunning, "Furnace must finish the recipe.");
            Assert.IsFalse(station.Outputs[0].IsEmpty, "Furnace output slot should contain iron_ingot.");
            Assert.AreEqual("iron_ingot", station.Outputs[0].item.itemId);
        }

        [Test]
        public void SteamBoilerRuntime_ProducesSteamSignalWhenRunning()
        {
            // The SteamBoilerRuntime exposes IsProducingSteam == station.IsRunning
            // so an adjacent SteamGenerator's IsBoilerActive probe returns true.
            var water = MakeItem("water", MaterialProperties.Liquid_Aqueous);
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous, MaterialProperties.Organic_Fresh);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var steam = MakeItem("steam");
            var boilerDef = MakeMachine("steam_boiler", MachineProcessType.Forgiving_Thermal_Boil);
            var recipe = MakeRecipe(
                output: "steam", outputQty: 1,
                ingredients: new[] { new Ingredient("water", 1), new Ingredient("coal", 1) },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 0.4f),
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 1.0f),
                },
                viaMachine: "steam_boiler");

            InstallItemDb(new[] { water, milk, coal, steam });

            var host = NewHost("boiler_host");
            var rt = AddRuntime<SteamBoilerRuntime>(host, boilerDef);
            var station = rt.Station;

            station.SetInput(0, new ItemStack(milk, 1));
            station.SetInput(1, new ItemStack(coal, 1));
            station.TryStartRecipe(recipe);

            Assert.IsTrue(station.IsRunning, "Boiler should start the milk+coal recipe.");
            Assert.IsTrue(rt.IsProducingSteam,
                "SteamBoilerRuntime.IsProducingSteam should mirror station.IsRunning.");
        }

        [Test]
        public void CrusherRuntime_NeedsPowerAndHasPowerConsumerNode()
        {
            // The Crusher's machine def has needsPower=true; the V6.3 station's
            // Init auto-attaches a PowerConsumerNode sibling. The runtime
            // resolves it via its base MachineRuntime accessor.
            var crusherDef = MakeMachine("crusher", MachineProcessType.Forgiving_Mechanical_Crush,
                needsPower: true, powerDrawWatts: 60);

            var host = NewHost("crusher_host");
            var rt = AddRuntime<CrusherRuntime>(host, crusherDef);

            Assert.IsTrue(rt.Station.NeedsPower, "Crusher station must report NeedsPower=true.");
            Assert.IsTrue(rt.HasPowerNode, "Crusher runtime must report HasPowerNode=true.");
            Assert.IsNotNull(rt.PowerConsumer, "Crusher should have a sibling PowerConsumerNode.");
        }

        [Test]
        public void StorageChestRuntime_DepositAndWithdraw()
        {
            // Core M2 chest behaviour: 10 wood deposited, 5 withdrawn -> 5 remain.
            var wood = MakeItem("wood");
            InstallItemDb(new[] { wood });

            var chestDef = MakeMachine("storage_chest", MachineProcessType.Picky_Specialty);
            var host = NewHost("chest_host");
            var rt = AddRuntime<StorageChestRuntime>(host, chestDef);

            Assert.IsTrue(rt.TryDeposit(new ItemStack(wood, 10)),
                "Empty chest should accept 10 wood in a single deposit.");
            Assert.AreEqual(10, rt.Contents.CountItem("wood"));

            var pulled = rt.TryWithdraw("wood", 5);
            Assert.IsFalse(pulled.IsEmpty);
            Assert.AreEqual(5, pulled.quantity);
            Assert.AreEqual("wood", pulled.item.itemId);
            Assert.AreEqual(5, rt.Contents.CountItem("wood"));
        }

        // ===============================================================
        //  V9.5 — Conveyor + Inserter
        // ===============================================================

        [Test]
        public void Conveyor_MovesItemToNextSegment()
        {
            // Place 2 belts end-to-end; put an item on the first; tick;
            // verify it hops to the second.
            var wood = MakeItem("wood");
            InstallItemDb(new[] { wood });

            var beltAGo = NewHost("beltA");
            beltAGo.transform.position = new Vector3(0f, 0f, 0f);
            var beltA = beltAGo.AddComponent<ConveyorBelt>();
            beltA.Forward = new Vector3Int(1, 0, 0);
            beltA.CellsPerSecond = 1f;
            beltA.SetPowerSatisfactionOverride(1f);

            var beltBGo = NewHost("beltB");
            beltBGo.transform.position = new Vector3(1f, 0f, 0f);
            var beltB = beltBGo.AddComponent<ConveyorBelt>();
            beltB.Forward = new Vector3Int(1, 0, 0);
            beltB.CellsPerSecond = 1f;
            beltB.SetPowerSatisfactionOverride(1f);

            Assert.IsTrue(beltA.TryInsert(new ItemStack(wood, 1)), "BeltA should accept the wood.");

            // Wait one full second so the belt's dwell timer elapses.
            beltA.AdvanceTick(1.1f);

            Assert.IsFalse(beltA.HasItem, "BeltA must release the item after one dwell period.");
            Assert.IsTrue(beltB.HasItem, "BeltB must now be carrying the item that came from BeltA.");
            Assert.AreEqual("wood", beltB.Carried.item.itemId);
        }

        [Test]
        public void Conveyor_StallsWhenNextFull()
        {
            var wood = MakeItem("wood");
            InstallItemDb(new[] { wood });

            var beltAGo = NewHost("beltA");
            beltAGo.transform.position = new Vector3(0f, 0f, 0f);
            var beltA = beltAGo.AddComponent<ConveyorBelt>();
            beltA.Forward = new Vector3Int(1, 0, 0);
            beltA.CellsPerSecond = 1f;
            beltA.SetPowerSatisfactionOverride(1f);

            var beltBGo = NewHost("beltB");
            beltBGo.transform.position = new Vector3(1f, 0f, 0f);
            var beltB = beltBGo.AddComponent<ConveyorBelt>();
            beltB.Forward = new Vector3Int(1, 0, 0);
            beltB.CellsPerSecond = 1f;
            beltB.SetPowerSatisfactionOverride(1f);

            // Both belts loaded; B has nowhere to go (no third belt) and
            // therefore can't accept new items.
            Assert.IsTrue(beltA.TryInsert(new ItemStack(wood, 1)));
            Assert.IsTrue(beltB.TryInsert(new ItemStack(wood, 1)));

            beltA.AdvanceTick(1.1f);

            // A still carries; B still carries -- belt stalled.
            Assert.IsTrue(beltA.HasItem, "BeltA must NOT release its item when BeltB is full.");
            Assert.IsTrue(beltB.HasItem, "BeltB must still be carrying its own item.");
        }

        [Test]
        public void Conveyor_NeedsPower_PerSegment()
        {
            // Each segment owns its own PowerNode consumer (10W) so a long
            // belt aggregates draw.
            var beltGo = NewHost("belt");
            var belt = beltGo.AddComponent<ConveyorBelt>();
            belt.EnsurePowerConsumerNode();

            var node = beltGo.GetComponent<PowerNode>();
            Assert.IsNotNull(node, "Conveyor segment must have its own PowerNode.");
            Assert.IsTrue(node.IsConsumer, "Conveyor's PowerNode must be a consumer.");
            Assert.AreEqual(belt.PowerDrawWatts, node.RequiredWatts,
                "Conveyor's PowerNode RequiredWatts must equal segment's powerDrawWatts.");
        }

        [Test]
        public void Conveyor_StallsWhenPowerSatisfactionZero()
        {
            var wood = MakeItem("wood");
            InstallItemDb(new[] { wood });

            var beltGo = NewHost("belt");
            beltGo.transform.position = Vector3.zero;
            var belt = beltGo.AddComponent<ConveyorBelt>();
            belt.Forward = new Vector3Int(1, 0, 0);
            belt.CellsPerSecond = 1f;
            belt.SetPowerSatisfactionOverride(0f);

            var sinkGo = NewHost("sink");
            sinkGo.transform.position = new Vector3(1f, 0f, 0f);
            var sinkBelt = sinkGo.AddComponent<ConveyorBelt>();
            sinkBelt.SetPowerSatisfactionOverride(1f);

            belt.TryInsert(new ItemStack(wood, 1));

            // Even after a full second, no movement happens.
            belt.AdvanceTick(5f);

            Assert.IsTrue(belt.HasItem, "Unpowered belt should not move its item.");
            Assert.IsFalse(sinkBelt.HasItem, "Sink belt should not have received anything from an unpowered upstream belt.");
        }

        [Test]
        public void Inserter_PullsFromChestToMachine()
        {
            // chest (with iron_ore) -> inserter -> furnace input slot.
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            InstallItemDb(new[] { ironOre });

            var chestDef = MakeMachine("storage_chest", MachineProcessType.Picky_Specialty);
            var chestGo = NewHost("chest");
            var chest = AddRuntime<StorageChestRuntime>(chestGo, chestDef);
            chest.TryDeposit(new ItemStack(ironOre, 5));

            var furnaceDef = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var furnaceGo = NewHost("furnace");
            var furnace = AddRuntime<FurnaceRuntime>(furnaceGo, furnaceDef);

            var inserterDef = MakeMachine("inserter", MachineProcessType.Picky_Specialty, needsPower: true, powerDrawWatts: 20);
            var inserterGo = NewHost("inserter");
            var inserter = AddRuntime<Inserter>(inserterGo, inserterDef);
            inserter.SetPowerSatisfactionOverride(1f);
            inserter.SetSource(chest);
            inserter.SetTarget(furnace.Station);

            // First tick should not move because cooldown starts at 0 and the
            // first AdvanceTick after start picks immediately. Drive a few
            // ticks to be deterministic.
            inserter.AdvanceTick(0.1f);

            Assert.AreEqual(4, chest.Contents.CountItem("iron_ore"),
                "Chest should have one fewer iron_ore after a single pull.");
            int furnaceTotal = 0;
            for (int i = 0; i < furnace.Station.Inputs.Count; i++)
            {
                var s = furnace.Station.Inputs[i];
                if (!s.IsEmpty && s.item != null && s.item.itemId == "iron_ore") furnaceTotal += s.quantity;
            }
            Assert.AreEqual(1, furnaceTotal,
                "Furnace input grid should have received exactly one iron_ore.");
        }

        [Test]
        public void Inserter_StallsWhenUnderpowered()
        {
            var ironOre = MakeItem("iron_ore", MaterialProperties.Solid_Metal);
            InstallItemDb(new[] { ironOre });

            var chestDef = MakeMachine("storage_chest", MachineProcessType.Picky_Specialty);
            var chestGo = NewHost("chest");
            var chest = AddRuntime<StorageChestRuntime>(chestGo, chestDef);
            chest.TryDeposit(new ItemStack(ironOre, 5));

            var furnaceDef = MakeMachine("furnace", MachineProcessType.Forgiving_Thermal_DryBurn);
            var furnaceGo = NewHost("furnace");
            var furnace = AddRuntime<FurnaceRuntime>(furnaceGo, furnaceDef);

            var inserterDef = MakeMachine("inserter", MachineProcessType.Picky_Specialty, needsPower: true, powerDrawWatts: 20);
            var inserterGo = NewHost("inserter");
            var inserter = AddRuntime<Inserter>(inserterGo, inserterDef);
            inserter.SetPowerSatisfactionOverride(0f); // No power.
            inserter.SetSource(chest);
            inserter.SetTarget(furnace.Station);

            inserter.AdvanceTick(5f); // Five seconds, no power.

            Assert.AreEqual(5, chest.Contents.CountItem("iron_ore"),
                "Unpowered inserter must not move any items from the chest.");
        }

        // ===============================================================
        //  M2 ACCEPTANCE PROOF
        // ===============================================================

        /// <summary>
        /// The M2 acceptance test for the entire milestone:
        ///   milk -> steam_boiler (Forgiving_Thermal_Boil) -> steam_generator
        ///   (V8.2 100W) -> battery storage.
        ///
        /// <para>This is the single proof point that the whole power loop
        /// works end-to-end. If this passes, M2 is playable.</para>
        /// </summary>
        [Test]
        public void M2_MilkInBoilerProducesPowerEndToEnd()
        {
            // 1) Items + recipe.
            var water = MakeItem("water", MaterialProperties.Liquid_Aqueous);
            var milk = MakeItem("milk", MaterialProperties.Liquid_Aqueous, MaterialProperties.Organic_Fresh);
            var coal = MakeItem("coal", MaterialProperties.Combustible_Dry);
            var steam = MakeItem("steam");
            InstallItemDb(new[] { water, milk, coal, steam });

            var boilerDef = MakeMachine("steam_boiler", MachineProcessType.Forgiving_Thermal_Boil);
            var recipe = MakeRecipe(
                output: "steam", outputQty: 1,
                ingredients: new[] { new Ingredient("water", 1), new Ingredient("coal", 1) },
                inputProperties: new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 0.4f),
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 1.0f),
                },
                viaMachine: "steam_boiler");

            // 2) Boiler.
            var boilerHost = NewHost("steam_boiler");
            boilerHost.transform.position = Vector3.zero;
            var boilerRt = AddRuntime<SteamBoilerRuntime>(boilerHost, boilerDef);
            // Boiler placed at cell (0,0,0). Register a PlacedBlock so the
            // SteamGenerator's adjacency probe finds it.
            var boilerPlaced = boilerHost.AddComponent<PlacedBlock>();
            boilerPlaced.OnPlaced("steam_boiler", new Vector3Int(0, 0, 0), Quaternion.identity);
            BlockRegistry.Instance.Register(boilerPlaced);

            // 3) Steam generator placed at (1,0,0) so it's adjacent.
            var genHost = NewHost("steam_generator");
            genHost.transform.position = new Vector3(1f, 0f, 0f);
            var gen = genHost.AddComponent<SteamGenerator>();
            gen.ForceRegister();
            var genPlaced = genHost.AddComponent<PlacedBlock>();
            genPlaced.OnPlaced("steam_generator", new Vector3Int(1, 0, 0), Quaternion.identity);
            BlockRegistry.Instance.Register(genPlaced);

            // 4) Battery on the same component as the generator.
            var batteryGo = NewHost("battery");
            var battery = batteryGo.AddComponent<Battery>();
            battery.ForceRegister();
            battery.ConfigureBattery(GameConstants.Power.BatteryBasicCapacityWs, 0);

            // Cable the generator to the battery so they're in the same
            // power-network component.
            var cableGo = NewHost("cable_gen_battery");
            var cable = cableGo.AddComponent<CableSegment>();
            cable.Init(gen, battery, PowerCableTier.T2);

            // 5) Feed milk + coal into the boiler and start.
            var station = boilerRt.Station;
            station.SetInput(0, new ItemStack(milk, 1));
            station.SetInput(1, new ItemStack(coal, 1));
            station.TryStartRecipe(recipe);
            Assert.IsTrue(station.IsRunning,
                "Boiler must start the milk+coal recipe (Forgiving_Thermal_Boil accepts milk as Aqueous).");

            // 6) Advance the station so it's actively running, AND tick the
            //    network so the generator senses the active boiler.
            station.AdvanceTick(0.1f); // boiler in-progress
            Assert.IsTrue(station.IsRunning, "Boiler should still be running mid-recipe.");

            // The generator's IsBoilerActive scans BlockRegistry and finds the
            // boiler whose station.IsRunning == true.
            Assert.IsTrue(gen.IsBoilerActive(),
                "SteamGenerator must see the adjacent running boiler.");

            // 7) Tick the PowerNetwork; battery must charge from the
            //    generator's 100W output.
            int storedBefore = battery.StoredWs;
            for (int i = 0; i < 5; i++)
            {
                PowerNetwork.Instance.TickOnce(0.1f);
            }
            int storedAfter = battery.StoredWs;

            Assert.Greater(storedAfter, storedBefore,
                "M2 acceptance: battery must charge from steam_generator while boiler runs on milk.");
            Assert.AreEqual(GameConstants.Power.SteamGeneratorOutputWatts, gen.CurrentWatts,
                "SteamGenerator must output its full 100W during the M2 acceptance loop.");
        }
    }
}
#endif
