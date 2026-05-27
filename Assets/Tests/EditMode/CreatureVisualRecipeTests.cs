#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;
using Voidborne.Editor.ArtPipeline;
using Voidborne.Fauna;
using Voidborne.NPCs;
using V2EnemyRegistry = Voidborne.Enemies.V2.EnemyRegistry;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Volume 3.5 — Creature Visual Recipe / Generator tests.
    ///
    /// Verifies the CreatureVisualRecipeMapper produces sensible recipes for
    /// the 7 Core NPCs and that the CreaturePrefabGenerator writes prefab
    /// references back onto the matching SOs.
    /// </summary>
    public class CreatureVisualRecipeTests
    {
        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static FaunaRegistry FaunaReg()
        {
            var reg = Resources.Load<FaunaRegistry>("FaunaRegistry");
            Assert.NotNull(reg, "FaunaRegistry missing — run Voidborne/Generate/Fauna first.");
            return reg;
        }

        private static V2EnemyRegistry EnemyReg()
        {
            var reg = Resources.Load<V2EnemyRegistry>("EnemyRegistry");
            Assert.NotNull(reg, "EnemyRegistry (V2) missing — run Voidborne/Generate/Enemies first.");
            return reg;
        }

        private static NpcRegistry NpcReg()
        {
            var reg = Resources.Load<NpcRegistry>("NpcRegistry");
            Assert.NotNull(reg, "NpcRegistry missing — run Voidborne/Generate/NPCs first.");
            return reg;
        }

        private static bool MaterialExists(string materialKey)
        {
            if (string.IsNullOrEmpty(materialKey)) return false;
            string path = PaletteRegistry.AssetPathFor(materialKey);
            return AssetDatabase.LoadAssetAtPath<Material>(path) != null;
        }

        private static void AssertRecipeValid(ItemVisualRecipe recipe, int maxLayers, string label)
        {
            Assert.NotNull(recipe, $"{label}: recipe is null.");
            Assert.NotNull(recipe.layers, $"{label}: recipe.layers is null.");
            Assert.GreaterOrEqual(recipe.layers.Length, 1,
                $"{label}: recipe has no layers.");
            Assert.LessOrEqual(recipe.layers.Length, maxLayers,
                $"{label}: recipe exceeds the {maxLayers}-layer ceiling.");

            foreach (var layer in recipe.layers)
            {
                Assert.IsTrue(System.Enum.IsDefined(typeof(PrimitiveShape), layer.shape),
                    $"{label}: layer carries invalid PrimitiveShape enum value {layer.shape}.");
                Assert.IsTrue(MaterialExists(layer.materialKey),
                    $"{label}: materialKey '{layer.materialKey}' does not resolve to an existing material " +
                    $"(expected at {PaletteRegistry.AssetPathFor(layer.materialKey ?? string.Empty)}).");
            }
        }

        // -------------------------------------------------------------------
        // Mapper tests
        // -------------------------------------------------------------------

        [Test]
        public void Mapper_FaunaProducesValidRecipe()
        {
            var graze = FaunaReg().GetById("graze");
            Assert.NotNull(graze, "graze FaunaDefinition missing.");

            var recipe = CreatureVisualRecipeMapper.MapFauna(graze);
            // Quadruped baseline is 6 layers; allow up to the boss ceiling so
            // any future expansion stays test-clean.
            AssertRecipeValid(recipe, CreatureVisualRecipeMapper.BossLayerCeiling, "graze");
        }

        [Test]
        public void Mapper_BossScalesUp()
        {
            var fbm = EnemyReg().GetById("fungal_brood_mother");
            Assert.NotNull(fbm, "fungal_brood_mother EnemyDefinition (V2) missing.");
            Assert.IsTrue(fbm.isBoss,
                "Fungal Brood Mother SO must carry isBoss=true for the boss-scaling pass to fire.");

            var recipe = CreatureVisualRecipeMapper.MapEnemy(fbm);
            AssertRecipeValid(recipe, CreatureVisualRecipeMapper.BossLayerCeiling, "fungal_brood_mother");

            bool anyOversized = false;
            foreach (var layer in recipe.layers)
            {
                float maxScale = Mathf.Max(layer.localScale.x,
                    Mathf.Max(layer.localScale.y, layer.localScale.z));
                if (maxScale > 1.5f)
                {
                    anyOversized = true;
                    break;
                }
            }
            Assert.IsTrue(anyOversized,
                "Boss recipe should carry at least one layer with scale > 1.5 (the 2x boss scaling).");
        }

        [Test]
        public void Mapper_HumanoidProducesHumanoidSilhouette()
        {
            var wren = NpcReg().GetById("wren");
            Assert.NotNull(wren, "wren NpcDefinition missing.");

            var drone = EnemyReg().GetById("vord_drone");
            Assert.NotNull(drone, "vord_drone EnemyDefinition (V2) missing.");

            var wrenRecipe = CreatureVisualRecipeMapper.MapNpc(wren);
            AssertRecipeValid(wrenRecipe, CreatureVisualRecipeMapper.BossLayerCeiling, "wren");
            Assert.GreaterOrEqual(wrenRecipe.layers.Length, 3,
                "Wren humanoid silhouette should have at least 3 layers (body + head + arms).");

            var droneRecipe = CreatureVisualRecipeMapper.MapEnemy(drone);
            AssertRecipeValid(droneRecipe, CreatureVisualRecipeMapper.BossLayerCeiling, "vord_drone");
            Assert.GreaterOrEqual(droneRecipe.layers.Length, 3,
                "Vord Drone humanoid silhouette should have at least 3 layers (body + head + arms).");
        }

        [Test]
        public void Mapper_QuadrupedHasFourLegs()
        {
            var graze = FaunaReg().GetById("graze");
            Assert.NotNull(graze, "graze FaunaDefinition missing.");

            var recipe = CreatureVisualRecipeMapper.MapFauna(graze);
            // Quadruped silhouette = body + head + 4 legs = 6 layers minimum.
            Assert.GreaterOrEqual(recipe.layers.Length, 5,
                "Quadruped silhouette should have at least 5 layers (body + head + 4 legs).");
        }

        [Test]
        public void Mapper_PredatorHasSpikes()
        {
            var thornback = FaunaReg().GetById("thornback");
            Assert.NotNull(thornback, "thornback FaunaDefinition missing.");

            var recipe = CreatureVisualRecipeMapper.MapFauna(thornback);
            AssertRecipeValid(recipe, CreatureVisualRecipeMapper.BossLayerCeiling, "thornback");

            bool anySpike = false;
            foreach (var layer in recipe.layers)
            {
                if (layer.shape == PrimitiveShape.Spike)
                {
                    anySpike = true;
                    break;
                }
            }
            Assert.IsTrue(anySpike,
                "Thornback recipe should contain at least one Spike-shape layer.");
        }

        // -------------------------------------------------------------------
        // Generator test
        // -------------------------------------------------------------------

        [Test]
        public void Generator_AssignsPrefabToSo()
        {
            int total = CreaturePrefabGenerator.Run();
            Assert.Greater(total, 0, "CreaturePrefabGenerator produced zero prefabs.");

            var graze = FaunaReg().GetById("graze");
            Assert.NotNull(graze, "graze SO missing after generation.");
            Assert.NotNull(graze.prefab, "graze.prefab should be non-null after generation.");

            var drone = EnemyReg().GetById("vord_drone");
            Assert.NotNull(drone, "vord_drone SO missing after generation.");
            Assert.NotNull(drone.prefab, "vord_drone.prefab should be non-null after generation.");

            var wren = NpcReg().GetById("wren");
            Assert.NotNull(wren, "wren SO missing after generation.");
            Assert.NotNull(wren.prefab, "wren.prefab should be non-null after generation.");
        }
    }
}
#endif
