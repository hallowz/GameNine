#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Voidborne.Automation;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.7 — machine process type vocabulary on
    /// machines in <c>items_core.json</c>. Anchors specific Core 60 machine
    /// expectations (workbench, furnace, steam_boiler, steam_generator) and
    /// validates that every Core 60 machine declares a non-empty
    /// <c>howItWorks</c> prose description for the Machine UI tooltip.
    /// </summary>
    public class MachineProcessTypeTests
    {
        [SetUp]
        public void ClearCacheBeforeEachTest()
        {
            GameDesignJsonLoader.ClearCache();
        }

        [Test]
        public void Workbench_IsHybridCrafting()
        {
            ItemJson m = LoadMachine("workbench");
            Assert.AreEqual("Hybrid_Crafting", m.processType,
                "Workbench is the canonical hybrid - specific recipes + improvised property matching.");
        }

        [Test]
        public void Furnace_IsForgivingThermalDryBurn()
        {
            ItemJson m = LoadMachine("furnace");
            Assert.AreEqual("Forgiving_Thermal_DryBurn", m.processType);
        }

        [Test]
        public void SteamBoiler_IsForgivingThermalBoil()
        {
            // The synergy-sandbox anchor: forgiving boiler is what lets milk burn for power.
            ItemJson m = LoadMachine("steam_boiler");
            Assert.AreEqual("Forgiving_Thermal_Boil", m.processType,
                "Steam Boiler must be forgiving thermal boil for the milk-in-boiler synergy.");
        }

        [Test]
        public void SteamGenerator_IsPickySpecialty()
        {
            ItemJson m = LoadMachine("steam_generator");
            Assert.AreEqual("Picky_Specialty", m.processType,
                "Steam Generator only accepts steam - no property fallback.");
        }

        [Test]
        public void EveryMachine_HasNonEmptyHowItWorks()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            var missing = new List<string>();

            foreach (var kvp in items)
            {
                string id = kvp.Key;
                ItemJson item = kvp.Value;
                if (item == null) continue;
                if (!string.Equals(item.kind, "machine", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.IsNullOrWhiteSpace(item.howItWorks))
                {
                    missing.Add(id);
                }
            }

            Assert.IsEmpty(missing,
                $"Machines missing howItWorks (required for Volume 4.4 Machine UI tooltip): {string.Join(", ", missing)}");
        }

        [Test]
        public void EveryMachineProcessType_ParsesToKnownEnum()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            var unknown = new List<string>();

            foreach (var kvp in items)
            {
                ItemJson item = kvp.Value;
                if (item == null) continue;
                if (!string.Equals(item.kind, "machine", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(item.processType)) continue;

                if (!Enum.TryParse<MachineProcessType>(item.processType, ignoreCase: false, out _))
                {
                    unknown.Add($"{kvp.Key}:{item.processType}");
                }
            }

            Assert.IsEmpty(unknown,
                $"Unknown machine processType values: {string.Join(", ", unknown)}");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static ItemJson LoadMachine(string id)
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey(id), $"items_core.json missing machine '{id}'.");
            ItemJson m = items[id];
            Assert.AreEqual("machine", m.kind, $"'{id}' should be a machine.");
            return m;
        }
    }
}
#endif
