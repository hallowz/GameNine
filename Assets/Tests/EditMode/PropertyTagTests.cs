#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.6 — material property tags on items in
    /// <c>items_core.json</c>. Validates the schema-extension acceptance criteria:
    /// every non-machine Core 60 item declares at least one property; every property
    /// string parses to a known <see cref="MaterialProperties"/> enum value.
    /// </summary>
    public class PropertyTagTests
    {
        [SetUp]
        public void ClearCacheBeforeEachTest()
        {
            GameDesignJsonLoader.ClearCache();
        }

        [Test]
        public void EveryNonMachineItem_HasAtLeastOneProperty()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            var missing = new List<string>();

            foreach (var kvp in items)
            {
                string id = kvp.Key;
                ItemJson item = kvp.Value;
                if (item == null) continue;

                bool isMachine = string.Equals(item.kind, "machine", StringComparison.OrdinalIgnoreCase);
                if (isMachine) continue; // Machines may legitimately declare zero properties.

                int count = item.properties != null ? item.properties.Length : 0;
                if (count == 0)
                {
                    missing.Add(id);
                }
            }

            Assert.IsEmpty(missing,
                $"Non-machine items with no properties (expected >= 1 each): {string.Join(", ", missing)}");
        }

        [Test]
        public void EveryPropertyString_ParsesToKnownEnum()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            var unknown = new List<string>();

            foreach (var kvp in items)
            {
                string id = kvp.Key;
                ItemJson item = kvp.Value;
                if (item?.properties == null) continue;

                foreach (string tag in item.properties)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    if (!Enum.TryParse<MaterialProperties>(tag, ignoreCase: false, out _))
                    {
                        unknown.Add($"{id}:{tag}");
                    }
                }
            }

            Assert.IsEmpty(unknown,
                $"Unknown property tags (must resolve to MaterialProperties enum): {string.Join(", ", unknown)}");
        }

        [Test]
        public void Wood_HasCombustibleDryAndSolidFiber()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("wood"), "items_core.json missing 'wood'.");
            string[] props = items["wood"].properties;
            CollectionAssert.Contains(props, "Combustible_Dry");
            CollectionAssert.Contains(props, "Solid_Fiber");
        }

        [Test]
        public void Milk_IsTheSynergyAnchor()
        {
            // The canonical 'weird path' anchor: milk = Liquid_Aqueous + Organic_Fresh.
            // Required for the M2 milk-in-boiler synergy acceptance test.
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("milk"), "items_core.json missing 'milk' (synergy anchor).");
            string[] props = items["milk"].properties;
            CollectionAssert.Contains(props, "Liquid_Aqueous");
            CollectionAssert.Contains(props, "Organic_Fresh");
        }

        [Test]
        public void IronOre_HasSolidMetal()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("iron_ore"));
            CollectionAssert.Contains(items["iron_ore"].properties, "Solid_Metal");
        }

        [Test]
        public void Gunpowder_HasCombustibleVolatile()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("gunpowder"));
            CollectionAssert.Contains(items["gunpowder"].properties, "Combustible_Volatile");
        }
    }
}
#endif
