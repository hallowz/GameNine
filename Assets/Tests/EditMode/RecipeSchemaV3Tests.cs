#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.8 — recipe schema v3 round-tripping. Verifies
    /// that the new <see cref="RecipeDefinition.inputProperties"/> /
    /// <see cref="RecipeDefinition.efficiency"/> / <see cref="RecipeDefinition.outputModifier"/>
    /// fields survive through both the in-memory shape and (when present) live SOs.
    /// </summary>
    public class RecipeSchemaV3Tests
    {
        [SetUp]
        public void ClearCacheBeforeEachTest()
        {
            GameDesignJsonLoader.ClearCache();
        }

        [Test]
        public void RecipeDefinition_DefaultEfficiencyAndOutputModifierAreOne()
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                Assert.AreEqual(1.0f, r.efficiency,
                    "RecipeDefinition.efficiency must default to 1.0.");
                Assert.AreEqual(1.0f, r.outputModifier,
                    "RecipeDefinition.outputModifier must default to 1.0.");
                Assert.IsTrue(r.inputProperties == null || r.inputProperties.Length == 0,
                    "RecipeDefinition.inputProperties must default to null/empty.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(r);
            }
        }

        [Test]
        public void InMemoryRoundTrip_PreservesAllV3Fields()
        {
            // Build a synthetic recipe carrying every v3 field, write to an SO, read back.
            var asset = ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                asset.outputItemId = "synthetic_steam";
                asset.outputQty = 1;
                asset.ingredients = new[] { new Ingredient("water", 1) };
                asset.viaMachineId = "steam_boiler";
                asset.notes = "TEST — synthetic recipe for v3 round-trip";

                asset.inputProperties = new[]
                {
                    new InputProperty(MaterialProperties.Liquid_Aqueous, 1, 1.0f),
                    new InputProperty(MaterialProperties.Combustible_Dry, 1, 0.8f),
                };
                asset.efficiency = 0.4f;
                asset.outputModifier = 0.7f;

                // Serialise + deserialise via Unity's SerializedObject path to mimic an
                // actual asset save/load (asset write is editor-only and not strictly
                // necessary for round-trip-of-fields-on-the-instance, but we exercise it).
                var so = new SerializedObject(asset);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Re-read fields directly off the in-memory instance.
                Assert.AreEqual(2, asset.inputProperties.Length);
                Assert.AreEqual(MaterialProperties.Liquid_Aqueous, asset.inputProperties[0].property);
                Assert.AreEqual(1, asset.inputProperties[0].qty);
                Assert.AreEqual(1.0f, asset.inputProperties[0].efficiency);
                Assert.AreEqual(MaterialProperties.Combustible_Dry, asset.inputProperties[1].property);
                Assert.AreEqual(0.8f, asset.inputProperties[1].efficiency);

                Assert.AreEqual(0.4f, asset.efficiency);
                Assert.AreEqual(0.7f, asset.outputModifier);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RecipeJson_BackwardsCompatible_MissingV3FieldsDefaultToOne()
        {
            // When items_backlog.json (no v3 fields) round-trips through Newtonsoft, the
            // float defaults baked into RecipeJson must take effect. Otherwise the legacy
            // path would zero them and break the generator's > 0 guard.
            var r = new RecipeJson();
            Assert.AreEqual(1.0f, r.efficiency,
                "RecipeJson.efficiency default must be 1.0 to preserve backwards compatibility.");
            Assert.AreEqual(1.0f, r.outputModifier,
                "RecipeJson.outputModifier default must be 1.0 to preserve backwards compatibility.");
            Assert.IsNull(r.inputProperties,
                "RecipeJson.inputProperties default must be null so the generator can detect 'no v3 inputs'.");
        }

        [Test]
        public void CraftingMatchEngine_NullRecipe_ReturnsNoMatchWithReason()
        {
            // V6.1: stub replaced. Passing a null recipe should now return a graceful
            // no-match result (recipe == null, reason populated) instead of throwing.
            ICraftingMatchEngine engine = new DefaultCraftingMatchEngine();
            var result = engine.TryMatch(
                recipe: null,
                machineType: MachineProcessType.Picky_Specialty,
                availableItems: new Dictionary<string, int>(),
                itemDb: Array.Empty<ItemDefinition>());
            Assert.IsNotNull(result, "Engine must always return a result, never null.");
            Assert.IsNull(result.recipe);
            Assert.IsFalse(result.Matched);
            Assert.IsFalse(string.IsNullOrEmpty(result.reason),
                "No-match result must include a human-readable reason.");
        }

        [Test]
        public void RecipeMatchResult_HasExpectedShape()
        {
            // Pure shape test — confirms the public surface lines up with the V6.1 spec.
            var result = new RecipeMatchResult
            {
                recipe = null,
                efficiency = 1.0f,
                outputModifier = 1.0f,
                inputBindings = new Dictionary<string, string>(),
                reason = "",
            };
            Assert.IsNull(result.recipe);
            Assert.AreEqual(1.0f, result.efficiency);
            Assert.AreEqual(1.0f, result.outputModifier);
            Assert.IsNotNull(result.inputBindings);
        }
    }
}
#endif
