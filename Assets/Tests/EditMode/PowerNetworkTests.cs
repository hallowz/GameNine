#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voidborne.Automation;
using Voidborne.Building;
using Voidborne.Core;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Power;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// V8.1 / V8.2 / V8.3 — Power system EditMode coverage.
    ///
    /// <para>Tests drive the <see cref="PowerNetwork"/> deterministically
    /// via <see cref="PowerNetwork.TickOnce"/> (no Unity FixedUpdate loop).
    /// Every fixture spins a fresh PowerNetwork via <see cref="PowerNetwork.ResetInstance"/>
    /// to avoid cross-test contamination from auto-registered nodes.</para>
    /// </summary>
    public class PowerNetworkTests
    {
        // ---------------------------------------------------------------
        //  Fixture state
        // ---------------------------------------------------------------

        private readonly List<GameObject> _created = new List<GameObject>();

        private GameObject NewHost(string name, System.Action<GameObject> setup = null)
        {
            var go = new GameObject(name);
            setup?.Invoke(go);
            _created.Add(go);
            return go;
        }

        private CableSegment MakeCable(PowerNode a, PowerNode b, PowerCableTier tier = PowerCableTier.T1)
        {
            // CableSegment.Init() registers with PowerNetwork explicitly so
            // we don't need to depend on Unity's EditMode-suppressed Awake.
            var go = new GameObject($"cable_{tier}_{a.name}_{b.name}");
            var seg = go.AddComponent<CableSegment>();
            seg.Init(a, b, tier);
            _created.Add(go);
            return seg;
        }

        // ---------------------------------------------------------------
        //  Component spin-up helper. Unity does NOT fire Awake on
        //  AddComponent in EditMode tests; we mimic the PlayMode lifecycle
        //  by calling the public ForceRegister seam right after
        //  AddComponent.
        // ---------------------------------------------------------------
        private T AddPowerComponent<T>(GameObject host) where T : PowerNode
        {
            var c = host.AddComponent<T>();
            c.ForceRegister();
            return c;
        }

        private PowerNode MakeGenericGenerator(string name, int watts)
        {
            // Unity does NOT fire Awake on AddComponent in EditMode tests,
            // so ConfigureNode() also force-registers with the network
            // (see PowerNode.ConfigureNode).
            var go = NewHost(name);
            var node = go.AddComponent<PowerNode>();
            node.ConfigureNode(generator: true, consumer: false, storage: false, maxOutput: watts);
            return node;
        }

        private PowerNode MakeGenericConsumer(string name, int watts, int priority = 0)
        {
            var go = NewHost(name);
            var node = go.AddComponent<PowerNode>();
            node.ConfigureNode(generator: false, consumer: true, storage: false, required: watts, priority: priority);
            return node;
        }

        [SetUp]
        public void SetUp()
        {
            PowerNetwork.ResetInstance();
            BlockRegistry.ResetInstance();
            // Touch the lazy getter so the singleton spins up cleanly.
            var _ = PowerNetwork.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
            foreach (var obj in _soCreated)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _soCreated.Clear();
            PowerNetwork.ResetInstance();
            BlockRegistry.ResetInstance();
        }

        // ===============================================================
        //  V8.1 — PowerNetwork graph
        // ===============================================================

        [Test]
        public void PowerNetwork_NodesAutoRegister()
        {
            // A freshly-instantiated PowerNode lands in the network's node list
            // without any explicit RegisterNode() call from the caller.
            var node = MakeGenericGenerator("gen_50w", 50);

            Assert.Contains(node, (System.Collections.ICollection)PowerNetwork.Instance.Nodes,
                "PowerNode must auto-register with PowerNetwork in OnEnable.");
        }

        [Test]
        public void PowerNetwork_ConnectedComponentsFormCorrectly()
        {
            // gen + gen + consumer wired in a chain land in the same component.
            var g1 = MakeGenericGenerator("gen1", 50);
            var g2 = MakeGenericGenerator("gen2", 50);
            var c1 = MakeGenericConsumer("c1", 30);
            MakeCable(g1, g2);
            MakeCable(g2, c1);

            var info = PowerNetwork.Instance.GetComponentFor(c1);
            Assert.AreEqual(3, info.nodeCount, "g1+g2+c1 should be in one component (3 nodes).");
            Assert.AreEqual(100, info.totalGenerationWatts, "Total generation must sum gen1+gen2 (100W).");
            Assert.AreEqual(30, info.totalDemandWatts, "Total demand must sum the consumer's required watts (30W).");
        }

        [Test]
        public void PowerNetwork_DistributesSupplyToConsumers()
        {
            // 1 gen producing 50W + 1 consumer needing 30W; after a tick, the
            // consumer's CurrentWatts >= 30 (it's fully supplied).
            var gen = MakeGenericGenerator("gen", 50);
            var consumer = MakeGenericConsumer("consumer", 30);
            MakeCable(gen, consumer);

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.GreaterOrEqual(consumer.CurrentWatts, 30,
                "Consumer with sufficient supply must receive its full RequiredWatts.");
            Assert.AreEqual(50, gen.CurrentWatts,
                "Generator's CurrentWatts must equal its peak output when ungated.");
        }

        [Test]
        public void PowerNetwork_DeficitMarksConsumersUnderpowered()
        {
            // 1 gen producing 20W + 1 consumer needing 50W; consumer ends with
            // CurrentWatts < RequiredWatts.
            var gen = MakeGenericGenerator("gen", 20);
            var consumer = MakeGenericConsumer("consumer", 50);
            MakeCable(gen, consumer);

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.Less(consumer.CurrentWatts, consumer.RequiredWatts,
                "Deficient consumer must have CurrentWatts < RequiredWatts.");
            Assert.AreEqual(20, consumer.CurrentWatts,
                "Consumer should still receive every watt the generator produced (20W).");
        }

        [Test]
        public void JunctionBox_BridgesComponentsTransparently()
        {
            // gen -- junction -- consumer: the junction box is a pure
            // pass-through. The gen and consumer must land in the same
            // connected component, and the consumer must receive power.
            var gen = MakeGenericGenerator("gen_50w", 50);
            var consumer = MakeGenericConsumer("consumer_30w", 30);

            var junctionGo = NewHost("junction");
            var junction = AddPowerComponent<JunctionBox>(junctionGo);

            MakeCable(gen, junction);
            MakeCable(junction, consumer);

            var info = PowerNetwork.Instance.GetComponentFor(consumer);
            Assert.AreEqual(3, info.nodeCount, "gen + junction + consumer must share one component.");
            Assert.IsFalse(junction.IsGenerator, "JunctionBox must not be a generator.");
            Assert.IsFalse(junction.IsConsumer, "JunctionBox must not be a consumer.");
            Assert.IsFalse(junction.IsStorage, "JunctionBox must not be storage.");

            PowerNetwork.Instance.TickOnce(0.1f);
            Assert.AreEqual(consumer.RequiredWatts, consumer.CurrentWatts,
                "Consumer beyond a junction box must still be fully powered.");
        }

        [Test]
        public void PowerCable_BurnoutOnOvercurrent()
        {
            // 5000W generator into a T1 cable (200W ceiling) -> the cable
            // disconnects on the next tick.
            var gen = MakeGenericGenerator("gen_5kw", 5000);
            var consumer = MakeGenericConsumer("consumer_5kw", 5000);
            var cable = MakeCable(gen, consumer, PowerCableTier.T1);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Cable burnout"));

            PowerNetwork.Instance.TickOnce(0.1f);

            // After burnout the cable is removed + destroyed.
            Assert.IsFalse(PowerNetwork.Instance.Cables.Contains(cable),
                "Burned-out cable must be removed from PowerNetwork.Cables.");
        }

        // ===============================================================
        //  V8.2 — Generators
        // ===============================================================

        [Test]
        public void SteamGenerator_OutputsWhenBoilerActive()
        {
            // Synthetic steam generator with BoilerOverride=true reports
            // CurrentWatts > 0 after a tick.
            var genGo = NewHost("steam_gen");
            var gen = AddPowerComponent<SteamGenerator>(genGo);
            gen.BoilerOverride = true;

            Assert.AreEqual(GameConstants.Power.SteamGeneratorOutputWatts, gen.MaxOutputWatts,
                "SteamGenerator.MaxOutputWatts must equal GameConstants.Power.SteamGeneratorOutputWatts (100W).");

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.AreEqual(gen.MaxOutputWatts, gen.CurrentWatts,
                "Steam generator with an active boiler must output its peak (100W).");
        }

        [Test]
        public void SteamGenerator_ZeroOutputWhenNoBoiler()
        {
            var genGo = NewHost("steam_gen");
            var gen = AddPowerComponent<SteamGenerator>(genGo);
            gen.BoilerOverride = false;

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.AreEqual(0, gen.CurrentWatts,
                "Steam generator with no boiler must output 0W.");
        }

        [Test]
        public void HandCrankGenerator_OutputsWhilePlayerCranking()
        {
            var genGo = NewHost("hand_crank");
            var gen = AddPowerComponent<HandCrankGenerator>(genGo);
            gen.IsBeingCranked = true;

            Assert.AreEqual(GameConstants.Power.HandCrankGeneratorOutputWatts, gen.MaxOutputWatts,
                "HandCrankGenerator.MaxOutputWatts must equal GameConstants.Power.HandCrankGeneratorOutputWatts (30W).");

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.Greater(gen.CurrentWatts, 0,
                "Hand crank generator with IsBeingCranked=true must output > 0W.");
            Assert.AreEqual(gen.MaxOutputWatts, gen.CurrentWatts,
                "Hand crank generator must output its full 30W while being cranked.");
        }

        // ===============================================================
        //  V8.3 — Battery / Sink / MachineCraftingStation integration
        // ===============================================================

        [Test]
        public void Battery_ChargesOnSurplus()
        {
            // gen 50W + consumer 30W = 20W surplus; after several ticks the
            // battery's StoredWs has increased.
            var gen = MakeGenericGenerator("gen_50w", 50);
            var consumer = MakeGenericConsumer("consumer_30w", 30);

            var batteryGo = NewHost("battery");
            var battery = AddPowerComponent<Battery>(batteryGo);
            battery.ConfigureBattery(GameConstants.Power.BatteryBasicCapacityWs, 0);

            MakeCable(gen, consumer);
            MakeCable(consumer, battery);

            int before = battery.StoredWs;
            for (int i = 0; i < 5; i++)
            {
                PowerNetwork.Instance.TickOnce(0.1f);
            }

            Assert.Greater(battery.StoredWs, before,
                "Battery must charge when the network has surplus generation.");
        }

        [Test]
        public void Battery_DischargesOnDeficit()
        {
            // gen 0W (no generator) + consumer 30W; battery starts at 1000Ws.
            // After ticks, StoredWs decreases.
            var consumer = MakeGenericConsumer("consumer_30w", 30);

            var batteryGo = NewHost("battery");
            var battery = AddPowerComponent<Battery>(batteryGo);
            battery.ConfigureBattery(GameConstants.Power.BatteryBasicCapacityWs, 1000);

            MakeCable(consumer, battery);

            int before = battery.StoredWs;
            for (int i = 0; i < 5; i++)
            {
                PowerNetwork.Instance.TickOnce(0.1f);
            }

            Assert.Less(battery.StoredWs, before,
                "Battery must discharge to cover the network deficit.");
        }

        [Test]
        public void PowerSink_AbsorbsSurplus()
        {
            // gen 500W + consumer 100W + sink: cable T2 carries 500W (allowed)
            // and after a tick the sink absorbs the leftover 400W. No burnout.
            var gen = MakeGenericGenerator("gen_500w", 500);
            var consumer = MakeGenericConsumer("consumer_100w", 100);

            var sinkGo = NewHost("sink");
            var sink = AddPowerComponent<PowerSink>(sinkGo);
            sink.ConfigureSink(500);

            // Use T2 cables (1000W ceiling) so the 500W flow doesn't burnout
            // for unrelated reasons.
            MakeCable(gen, consumer, PowerCableTier.T2);
            MakeCable(consumer, sink, PowerCableTier.T2);

            PowerNetwork.Instance.TickOnce(0.1f);

            Assert.Greater(sink.CurrentWatts, 0,
                "PowerSink must absorb leftover watts when surplus exists.");
            // No cable burnout occurred.
            Assert.AreEqual(2, PowerNetwork.Instance.Cables.Count,
                "No cables should burn out when a sink is on the network.");
        }

        [Test]
        public void MachineStation_ScalesProgressByPowerSatisfaction()
        {
            // Two powered stations: one fully supplied, one at half-power.
            // After the same wall-time advance, the half-power station's
            // progress must be ~half the fully-powered station's.
            //
            // The simplest deterministic shape: drive AdvanceTick directly
            // with a fixed dt, and compare elapsed progress for two
            // PowerSatisfaction levels.

            var ironOre = MakeFixtureItem("iron_ore", MaterialProperties.Solid_Metal);
            var ironIngot = MakeFixtureItem("iron_ingot", MaterialProperties.Solid_Metal);
            var crusher = MakeFixtureMachine("crusher", MachineProcessType.Forgiving_Mechanical_Crush, needsPower: true, powerDrawWatts: 60);
            var recipe = MakeFixtureRecipe("iron_ingot", 1, new[] { new Ingredient("iron_ore", 1) }, "crusher");

            InstallItemDb(new[] { ironOre, ironIngot });

            // Fully-powered station.
            var fullStation = MakeStation(crusher, requiredWatts: 60, currentWatts: 60);
            fullStation.SetInput(0, new ItemStack(ironOre, 1));
            fullStation.TryStartRecipe(recipe);
            Assert.IsTrue(fullStation.IsRunning, "Full-power station should start the recipe.");

            // Half-powered station.
            var halfStation = MakeStation(crusher, requiredWatts: 60, currentWatts: 30);
            halfStation.SetInput(0, new ItemStack(ironOre, 1));
            halfStation.TryStartRecipe(recipe);
            Assert.IsTrue(halfStation.IsRunning, "Half-power station should still start the recipe (TryStartRecipe is gating-only).");

            // Advance both by the same wall-time.
            fullStation.AdvanceTick(0.5f);
            halfStation.AdvanceTick(0.5f);

            // Half-power station progress must be ~half the full-power one's.
            float fullProgress = fullStation.Progress;
            float halfProgress = halfStation.Progress;
            Assert.Greater(fullProgress, 0f, "Full-power station progress must advance.");
            Assert.Greater(halfProgress, 0f, "Half-power station progress must also advance (PowerSatisfaction>0).");
            Assert.Less(halfProgress, fullProgress,
                "Half-power station must advance more slowly than the full-power one.");
            // Allow some slack (the durations might not be exact halves due
            // to clamp / rounding), but the ratio must be near 0.5.
            float ratio = halfProgress / fullProgress;
            Assert.That(ratio, Is.InRange(0.4f, 0.6f),
                $"Half-power station progress should be ~50% of full-power; observed ratio {ratio:F3}.");
        }

        // ---------------------------------------------------------------
        //  MachineCraftingStation fixture helpers
        // ---------------------------------------------------------------

        private readonly List<Object> _soCreated = new List<Object>();

        private ItemDefinition MakeFixtureItem(string id, params MaterialProperties[] props)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.maxStackSize = 64;
            item.properties = props ?? System.Array.Empty<MaterialProperties>();
            _soCreated.Add(item);
            return item;
        }

        private RecipeDefinition MakeFixtureRecipe(string output, int outputQty, Ingredient[] ingredients, string viaMachine)
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.outputItemId = output;
            r.outputQty = outputQty;
            r.ingredients = ingredients ?? System.Array.Empty<Ingredient>();
            r.efficiency = 1.0f;
            r.outputModifier = 1.0f;
            r.viaMachineId = viaMachine;
            _soCreated.Add(r);
            return r;
        }

        private MachineDefinition MakeFixtureMachine(string id, MachineProcessType processType,
            bool needsPower = false, int powerDrawWatts = 0)
        {
            var m = ScriptableObject.CreateInstance<MachineDefinition>();
            m.itemId = id;
            m.displayName = id;
            m.gridWidth = 3;
            m.gridHeight = 3;
            m.processType = processType;
            m.needsPower = needsPower;
            m.powerDrawWatts = powerDrawWatts;
            _soCreated.Add(m);
            return m;
        }

        private ItemDatabase _itemDbFixture;
        private void InstallItemDb(IEnumerable<ItemDefinition> items)
        {
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition>(items);
            _itemDbFixture.Reindex();
            _soCreated.Add(_itemDbFixture);
        }

        private MachineCraftingStation MakeStation(MachineDefinition def, int requiredWatts, int currentWatts)
        {
            // Init() auto-attaches PowerConsumerNode when def.needsPower is
            // true; we just need to override the configured watts + push the
            // CurrentWatts via the public test seam on PowerNode.
            var go = NewHost($"station_{def.itemId}_{currentWatts}w");
            var station = go.AddComponent<MachineCraftingStation>();
            station.Init(def);
            var consumer = go.GetComponent<PowerConsumerNode>();
            Assert.IsNotNull(consumer, "Init must attach a PowerConsumerNode when def.needsPower is true.");
            consumer.Configure(requiredWatts);
            consumer.SetCurrentWatts(currentWatts);
            return station;
        }

    }
}
#endif
