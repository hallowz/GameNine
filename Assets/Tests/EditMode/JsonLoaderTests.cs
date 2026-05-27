#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.1 — <see cref="GameDesignJsonLoader"/>.
    /// Confirms the JSON design data parses cleanly into the schema POCOs.
    /// </summary>
    public class JsonLoaderTests
    {
        [SetUp]
        public void ClearCacheBeforeEachTest()
        {
            // Ensure each test reads fresh from disk so a corrupted cache from a prior
            // run doesn't mask a regression.
            GameDesignJsonLoader.ClearCache();
        }

        [Test]
        public void LoadItems_ReturnsAtLeast1000Entries()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsNotNull(items, "LoadItems() returned null.");
            Assert.GreaterOrEqual(items.Count, 1000,
                $"Expected >= 1000 items in items.json, got {items.Count}.");
        }

        [Test]
        public void LoadItems_WorkbenchIsMachine()
        {
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("workbench"), "items.json missing 'workbench' entry.");

            ItemJson workbench = items["workbench"];
            Assert.AreEqual("machine", workbench.kind,
                "Expected workbench.kind == 'machine'.");
            Assert.AreEqual("Workbench", workbench.name);
            Assert.IsNotNull(workbench.recipes, "Workbench should have a bootstrap recipe.");
            Assert.GreaterOrEqual(workbench.recipes.Count, 1);
        }

        [Test]
        public void LoadItems_WorkbenchBootstrapRecipeHasNullVia()
        {
            // BOOTSTRAP recipes use via=null to indicate "personal grid / hand crafted".
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            RecipeJson bootstrap = items["workbench"].recipes[0];
            Assert.IsNull(bootstrap.via, "Workbench's first recipe should be bootstrap (via=null).");
            Assert.IsNotNull(bootstrap.inputs);
            Assert.Greater(bootstrap.inputs.Count, 0);
        }

        [Test]
        public void LoadItems_HasBuildBlockWithFlag()
        {
            // stone_block (and friends) are tagged with _build=true.
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            bool anyBuild = false;
            foreach (ItemJson item in items.Values)
            {
                if (item.build == true) { anyBuild = true; break; }
            }
            Assert.IsTrue(anyBuild, "Expected at least one item with _build=true.");
        }

        [Test]
        public void LoadNpcs_ReturnsExactly70Entries()
        {
            List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();
            Assert.IsNotNull(npcs);
            Assert.AreEqual(70, npcs.Count,
                $"Expected exactly 70 NPCs in npcs.json, got {npcs.Count}.");
        }

        [Test]
        public void LoadNpcs_FirstBossHasExpectedShape()
        {
            List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();
            NpcJson first = npcs[0];
            Assert.IsFalse(string.IsNullOrEmpty(first.cat), "NPC.cat should be set.");
            Assert.IsFalse(string.IsNullOrEmpty(first.name), "NPC.name should be set.");
            Assert.IsNotNull(first.behaviors, "NPC.behaviors should be non-null.");
            Assert.IsNotNull(first.abilities, "NPC.abilities should be non-null.");
            Assert.IsNotNull(first.drops, "NPC.drops should be non-null.");
        }

        [Test]
        public void LoadCategories_HasCoreCategories()
        {
            Dictionary<string, List<string>> cats = GameDesignJsonLoader.LoadCategories();
            Assert.IsNotNull(cats);
            // Spot-check a few of the 17 expected categories.
            Assert.IsTrue(cats.ContainsKey("food"), "categories.json missing 'food'.");
            Assert.IsTrue(cats.ContainsKey("weapon"), "categories.json missing 'weapon'.");
            Assert.IsTrue(cats.ContainsKey("build"), "categories.json missing 'build'.");
            Assert.IsTrue(cats.ContainsKey("deco"), "categories.json missing 'deco'.");
            Assert.Greater(cats["food"].Count, 0, "'food' category should not be empty.");
        }

        [Test]
        public void LoadCategoriesWrapped_GetReturnsItemIds()
        {
            CategoriesJson wrapped = GameDesignJsonLoader.LoadCategoriesWrapped();
            Assert.IsNotNull(wrapped);
            var foodIds = wrapped.Get("food");
            Assert.IsNotNull(foodIds);
            Assert.Greater(foodIds.Count, 0);
            // Unknown category returns empty, not null.
            var unknown = wrapped.Get("__nonexistent__");
            Assert.IsNotNull(unknown);
            Assert.AreEqual(0, unknown.Count);
        }

        [Test]
        public void LoadBuildMaterials_HasStoneAndWood()
        {
            List<BuildMaterialJson> mats = GameDesignJsonLoader.LoadBuildMaterials();
            Assert.IsNotNull(mats);
            Assert.Greater(mats.Count, 0, "build_materials.json should not be empty.");
            bool hasStone = false, hasWood = false;
            foreach (BuildMaterialJson m in mats)
            {
                if (m.id == "stone") hasStone = true;
                if (m.id == "wood") hasWood = true;
            }
            Assert.IsTrue(hasStone, "build_materials.json missing 'stone' entry.");
            Assert.IsTrue(hasWood, "build_materials.json missing 'wood' entry.");
        }
    }
}
#endif
