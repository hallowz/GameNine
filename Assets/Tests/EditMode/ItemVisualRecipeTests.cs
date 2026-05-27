#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;
using Voidborne.Editor.ArtPipeline;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Volume 3.3 — Item Visual Recipe / Composer tests.
    ///
    /// Verifies the mapper produces well-formed recipes for every Core 60
    /// item and that the composer can instantiate at least one of them.
    /// </summary>
    public class ItemVisualRecipeTests
    {
        // ----- Mapper coverage -----

        [Test]
        public void Mapper_AllCore60Items_ProduceValidRecipe()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.NotNull(db, "ItemDatabase missing — run Voidborne/Generate/Items first.");
            Assert.GreaterOrEqual(db.AllItems.Count, 50,
                "Expected Core 60 ItemDatabase to carry ~60 items.");

            foreach (var item in db.AllItems)
            {
                Assert.NotNull(item, "Null entry in ItemDatabase.items.");
                var recipe = ItemVisualRecipeMapper.MapItem(item);
                Assert.NotNull(recipe, $"Recipe was null for '{item.itemId}'.");
                Assert.NotNull(recipe.layers, $"Recipe.layers was null for '{item.itemId}'.");
                Assert.GreaterOrEqual(recipe.layers.Length, 1,
                    $"Recipe for '{item.itemId}' had zero layers.");
                Assert.LessOrEqual(recipe.layers.Length, 4,
                    $"Recipe for '{item.itemId}' exceeded the 4-layer ceiling.");

                foreach (var layer in recipe.layers)
                {
                    // shape is an enum — System.Enum.IsDefined handles out-of-range.
                    Assert.IsTrue(System.Enum.IsDefined(typeof(PrimitiveShape), layer.shape),
                        $"Recipe for '{item.itemId}' had invalid PrimitiveShape {layer.shape}.");
                    Assert.IsFalse(string.IsNullOrEmpty(layer.materialKey),
                        $"Recipe for '{item.itemId}' had empty materialKey.");

                    string matPath = PaletteRegistry.AssetPathFor(layer.materialKey);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    Assert.NotNull(mat,
                        $"Recipe for '{item.itemId}' references missing material '{layer.materialKey}' at {matPath}.");
                }
            }
        }

        // ----- Per-rule spot checks -----

        [Test]
        public void Mapper_OreItem_ReturnsSinglePrimitive()
        {
            var item = LoadItem("iron_ore");
            var recipe = ItemVisualRecipeMapper.MapItem(item);
            Assert.AreEqual(1, recipe.layers.Length, "iron_ore should be a single layer.");
            Assert.AreEqual(PrimitiveShape.Sphere, recipe.layers[0].shape);
            Assert.AreEqual("ore", recipe.layers[0].materialKey);
        }

        [Test]
        public void Mapper_Flora_ReturnsStemPlusFoliage()
        {
            var item = LoadItem("wood");
            var recipe = ItemVisualRecipeMapper.MapItem(item);
            Assert.GreaterOrEqual(recipe.layers.Length, 2,
                "Flora source should have at least a stem + foliage.");
            bool hasCylinder = false, hasSphere = false;
            foreach (var l in recipe.layers)
            {
                if (l.shape == PrimitiveShape.Cylinder) hasCylinder = true;
                if (l.shape == PrimitiveShape.Sphere) hasSphere = true;
            }
            Assert.IsTrue(hasCylinder, "wood recipe missing stem (Cylinder).");
            Assert.IsTrue(hasSphere, "wood recipe missing foliage (Sphere).");
        }

        [Test]
        public void Mapper_Furnace_HasMultipleLayers()
        {
            var item = LoadItem("furnace");
            var recipe = ItemVisualRecipeMapper.MapItem(item);
            Assert.GreaterOrEqual(recipe.layers.Length, 2,
                "Furnace should have body + chimney + glow (>=2 layers).");
        }

        [Test]
        public void Mapper_BuildBlock_MatchesForm()
        {
            var cube = ItemVisualRecipeMapper.MapItem(LoadItem("stone_cube"));
            Assert.AreEqual(1, cube.layers.Length);
            Assert.AreEqual(PrimitiveShape.Cube, cube.layers[0].shape);

            var slab = ItemVisualRecipeMapper.MapItem(LoadItem("wood_slab"));
            Assert.AreEqual(1, slab.layers.Length);
            Assert.AreEqual(PrimitiveShape.Slab, slab.layers[0].shape);

            var door = ItemVisualRecipeMapper.MapItem(LoadItem("wood_door"));
            Assert.AreEqual(1, door.layers.Length);
            Assert.AreEqual(PrimitiveShape.Door, door.layers[0].shape);
        }

        // ----- Composer -----

        [Test]
        public void Composer_BuildProducesNonNullPrefab()
        {
            var item = LoadItem("workbench");
            var recipe = ItemVisualRecipeMapper.MapItem(item);
            GameObject go = null;
            try
            {
                go = ItemModelComposer.Build(item, recipe, asPlacedPrefab: false);
                Assert.NotNull(go);
                Assert.AreEqual(item.itemId, go.name);
                Assert.GreaterOrEqual(go.transform.childCount, 1,
                    "Composer should produce at least one layer child.");
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i);
                    Assert.NotNull(child.GetComponent<MeshFilter>(),
                        $"Layer child {child.name} missing MeshFilter.");
                    Assert.NotNull(child.GetComponent<MeshRenderer>(),
                        $"Layer child {child.name} missing MeshRenderer.");
                }
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // ----- Helpers -----

        private static ItemDefinition LoadItem(string id)
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.NotNull(db, "ItemDatabase missing — run Voidborne/Generate/Items first.");
            var item = db.GetItem(id);
            Assert.NotNull(item, $"ItemDatabase has no entry '{id}'.");
            return item;
        }
    }
}
#endif
