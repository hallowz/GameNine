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
        public void LoadItems_ReturnsCore60Roster()
        {
            // V2.9 — loader switched from items.json (1031 entries) to items_core.json (60).
            // Allow a small tolerance (>=60, <=80) so designers can add a few hand-authored
            // items during M2 iteration without immediately breaking the test.
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsNotNull(items, "LoadItems() returned null.");
            Assert.GreaterOrEqual(items.Count, 60,
                $"Expected >= 60 items in items_core.json, got {items.Count}.");
            Assert.LessOrEqual(items.Count, 80,
                $"Expected <= 80 items in items_core.json (Core 60 with small iteration headroom), got {items.Count}.");
        }

        [Test]
        public void LoadItems_StripsUnderscoreMetadataKeys()
        {
            // items_core.json carries _schema_version / _schema_notes metadata keys that
            // would otherwise round-trip as malformed ItemJson values.
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            foreach (var key in items.Keys)
            {
                Assert.IsFalse(key.StartsWith("_"),
                    $"items_core.json metadata key '{key}' should have been filtered out by the loader.");
            }
        }

        [Test]
        public void LoadItems_HasSynergyAnchors()
        {
            // milk + steam_boiler are the M2 synergy-sandbox anchors per master_prompt.md.
            Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
            Assert.IsTrue(items.ContainsKey("milk"),
                "items_core.json missing 'milk' (synergy anchor).");
            Assert.IsTrue(items.ContainsKey("steam_boiler"),
                "items_core.json missing 'steam_boiler' (synergy anchor).");
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
        public void LoadNpcs_ReturnsExactly7Entries()
        {
            // V2.9 — loader switched from npcs.json (70 entries) to npcs_core.json (7).
            // The leading _schema_notes placeholder entry is filtered out by the loader.
            List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();
            Assert.IsNotNull(npcs);
            Assert.AreEqual(7, npcs.Count,
                $"Expected exactly 7 NPCs in npcs_core.json (Wren + 1 boss + 2 fodder + 3 wildlife), got {npcs.Count}.");
        }

        [Test]
        public void LoadNpcs_HasWrenAndBroodMother()
        {
            List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();
            bool hasWren = false, hasBoss = false;
            foreach (var n in npcs)
            {
                if (n.name == "Wren") hasWren = true;
                if (n.name == "Fungal Brood Mother") hasBoss = true;
            }
            Assert.IsTrue(hasWren, "npcs_core.json missing 'Wren' (M6 rescuable Kin).");
            Assert.IsTrue(hasBoss, "npcs_core.json missing 'Fungal Brood Mother' (M6 boss).");
        }

        [Test]
        public void LoadNpcs_FirstEntryHasExpectedShape()
        {
            List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();
            Assert.Greater(npcs.Count, 0, "npcs_core.json should contain at least one NPC after filtering.");
            NpcJson first = npcs[0];
            Assert.IsFalse(string.IsNullOrEmpty(first.cat), "NPC.cat should be set.");
            Assert.IsFalse(string.IsNullOrEmpty(first.name), "NPC.name should be set.");
            Assert.IsNotNull(first.behaviors, "NPC.behaviors should be non-null.");
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
