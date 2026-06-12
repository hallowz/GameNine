#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Voidborne.ArtPipeline;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 3.1 — Material Palette.
    ///
    /// Validates PaletteRegistry lookup semantics. The MaterialGenerator's
    /// disk side-effects are intentionally not exercised here — they belong
    /// in a manual editor run, not a deterministic EditMode suite.
    /// </summary>
    public class MaterialGeneratorTests
    {
        [Test]
        public void PaletteRegistry_HasAllKeys()
        {
            string[] required =
            {
                "fauna", "flora", "ore", "soil", "exotic",
                "food", "power", "weapon", "armor", "machine",
                "build_neutral", "deco", "vehicle", "automation", "gadget"
            };

            foreach (var key in required)
            {
                var c = PaletteRegistry.GetByKey(key);
                Assert.AreNotEqual(Color.magenta, c,
                    $"PaletteRegistry is missing palette key '{key}' (returned magenta fallback).");
                Assert.Greater(c.a, 0f,
                    $"PaletteRegistry key '{key}' returned a fully-transparent colour.");
            }
        }

        [Test]
        public void GetByKey_UnknownReturnsMagenta()
        {
            var c = PaletteRegistry.GetByKey("this_key_does_not_exist_xyz");
            Assert.AreEqual(Color.magenta, c, "Unknown keys should fall back to magenta.");
        }

        [Test]
        public void GetByKey_NullOrEmptyReturnsMagenta()
        {
            Assert.AreEqual(Color.magenta, PaletteRegistry.GetByKey(null));
            Assert.AreEqual(Color.magenta, PaletteRegistry.GetByKey(""));
        }

        [Test]
        public void GetForItem_BuildBlock_ResolvesByBuildColor()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.itemId = "stone_cube_test";
                item.kind = ItemKind.Product;
                item.source = ItemSource.None;
                item.isBuildBlock = true;
                item.isDeco = false;
                item.buildColor = "stone";
                item.categories = new[] { "build" };

                Color resolved = PaletteRegistry.GetForItem(item);
                Color expected = PaletteRegistry.BuildMaterialColors["stone"];

                Assert.AreEqual(expected.r, resolved.r, 0.001f, "Stone R mismatch.");
                Assert.AreEqual(expected.g, resolved.g, 0.001f, "Stone G mismatch.");
                Assert.AreEqual(expected.b, resolved.b, 0.001f, "Stone B mismatch.");
                Assert.AreNotEqual(Color.magenta, resolved, "Should not fall back to magenta.");
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void GetForItem_Fauna_ResolvesBySource()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.itemId = "fauna_meat_test";
                item.kind = ItemKind.Source;
                item.source = ItemSource.Fauna;
                item.isBuildBlock = false;
                item.isDeco = false;
                item.categories = new string[0];

                Color resolved = PaletteRegistry.GetForItem(item);
                Color expected = PaletteRegistry.GetByKey("fauna");

                Assert.AreEqual(expected, resolved, "Fauna source should resolve to fauna palette colour.");
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void GetForItem_Machine_ResolvesToMachineColor()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.itemId = "workbench_test";
                item.kind = ItemKind.Machine;
                item.source = ItemSource.None;
                item.categories = new string[0];

                Color resolved = PaletteRegistry.GetForItem(item);
                Color expected = PaletteRegistry.GetByKey("machine");

                Assert.AreEqual(expected, resolved);
                Assert.Greater(resolved.a, 0f);
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void GetForItem_Deco_BeatsBuildBlock()
        {
            // Some deco blocks are also marked isBuildBlock; deco should win.
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.itemId = "antler_chandelier_test";
                item.kind = ItemKind.Product;
                item.isBuildBlock = true;
                item.isDeco = true;
                item.buildColor = "wood";
                item.categories = new[] { "build", "deco" };

                Color resolved = PaletteRegistry.GetForItem(item);
                Color expected = PaletteRegistry.GetByKey("deco");

                Assert.AreEqual(expected, resolved, "Deco priority should outrank buildColor.");
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void AllEntries_IncludesBuildAndCategoryKeys()
        {
            int total = 0;
            bool sawCategory = false;
            bool sawBuild = false;

            foreach (var kv in PaletteRegistry.AllEntries)
            {
                total++;
                if (kv.Key == "machine") sawCategory = true;
                if (kv.Key == "build_stone") sawBuild = true;
            }

            Assert.IsTrue(sawCategory, "AllEntries should include 'machine'.");
            Assert.IsTrue(sawBuild, "AllEntries should include 'build_stone'.");
            Assert.GreaterOrEqual(total, 25, $"Expected at least 25 palette entries; got {total}.");
        }
    }
}
#endif
