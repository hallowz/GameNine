#if UNITY_EDITOR
using NUnit.Framework;
using Voidborne.Editor.Data;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Unit tests for the shared <see cref="ParsingHelpers"/> static class.
    /// Covers tier parsing, marker search, slugify, and the behaviour
    /// classification helpers used by the V2.4 generators.
    /// </summary>
    public class ParsingHelpersTests
    {
        // ---- ParseTier ----------------------------------------------------

        [Test]
        public void ParseTier_LeadingT1_Returns1()
        {
            Assert.AreEqual(1, ParsingHelpers.ParseTier("T1 — 3x3 crafting (BOOTSTRAP)"));
        }

        [Test]
        public void ParseTier_T3_Returns3()
        {
            Assert.AreEqual(3, ParsingHelpers.ParseTier("T3 — proper smelting"));
        }

        [Test]
        public void ParseTier_T7_Returns7()
        {
            Assert.AreEqual(7, ParsingHelpers.ParseTier("T7 — automated workbench"));
        }

        [Test]
        public void ParseTier_NoTier_Returns0()
        {
            Assert.AreEqual(0, ParsingHelpers.ParseTier("primitive cooking"));
        }

        [Test]
        public void ParseTier_Null_Returns0()
        {
            Assert.AreEqual(0, ParsingHelpers.ParseTier(null));
        }

        [Test]
        public void ParseTier_Empty_Returns0()
        {
            Assert.AreEqual(0, ParsingHelpers.ParseTier(string.Empty));
        }

        [Test]
        public void ParseTier_IgnoresEmbeddedTokens()
        {
            // 'ART3' must not be treated as tier 3 — letters cannot precede the marker.
            Assert.AreEqual(0, ParsingHelpers.ParseTier("ART3 something"));
        }

        [Test]
        public void ParseTier_PicksFirst_WhenMultiple()
        {
            Assert.AreEqual(2, ParsingHelpers.ParseTier("T2 then T5 later"));
        }

        // ---- ContainsMarker -----------------------------------------------

        [Test]
        public void ContainsMarker_BasicMatch_IsCaseInsensitive()
        {
            Assert.IsTrue(ParsingHelpers.ContainsMarker("contains BOOTSTRAP word", "bootstrap"));
            Assert.IsTrue(ParsingHelpers.ContainsMarker("contains BootStrap mixed", "BOOTSTRAP"));
        }

        [Test]
        public void ContainsMarker_NoMatch_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.ContainsMarker("nothing here", "BOOTSTRAP"));
        }

        [Test]
        public void ContainsMarker_NullSafe()
        {
            Assert.IsFalse(ParsingHelpers.ContainsMarker(null, "x"));
            Assert.IsFalse(ParsingHelpers.ContainsMarker("x", null));
            Assert.IsFalse(ParsingHelpers.ContainsMarker(string.Empty, "x"));
            Assert.IsFalse(ParsingHelpers.ContainsMarker("x", string.Empty));
        }

        // ---- SlugifyName --------------------------------------------------

        [Test]
        public void SlugifyName_SimpleSpaces_ToUnderscore()
        {
            Assert.AreEqual("mist_hunter", ParsingHelpers.SlugifyName("Mist Hunter"));
        }

        [Test]
        public void SlugifyName_Apostrophes_Stripped()
        {
            // "Vesh the Smith" -> "vesh_the_smith"; apostrophes collapse to underscore then trim.
            Assert.AreEqual("vesh_the_smith", ParsingHelpers.SlugifyName("Vesh the Smith"));
        }

        [Test]
        public void SlugifyName_SpecialChars_Collapsed()
        {
            // "Vord-Brood Queen!" -> "vord_brood_queen"
            Assert.AreEqual("vord_brood_queen", ParsingHelpers.SlugifyName("Vord-Brood Queen!"));
        }

        [Test]
        public void SlugifyName_MultipleSpaces_CollapsedToSingleUnderscore()
        {
            Assert.AreEqual("the_wanderer", ParsingHelpers.SlugifyName("  The  Wanderer  "));
        }

        [Test]
        public void SlugifyName_TheChoiceVariants_Handled()
        {
            // "The Choice (Variants)" -> "the_choice_variants"
            Assert.AreEqual("the_choice_variants", ParsingHelpers.SlugifyName("The Choice (Variants)"));
        }

        [Test]
        public void SlugifyName_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ParsingHelpers.SlugifyName(string.Empty));
            Assert.AreEqual(string.Empty, ParsingHelpers.SlugifyName(null));
        }

        // ---- IsAggressive -------------------------------------------------

        [Test]
        public void IsAggressive_PredatorMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsAggressive(new[] { "Apex predator at night." }));
        }

        [Test]
        public void IsAggressive_StalkerMarker_Returns_True()
        {
            // The marker is "stalker" (substring match), not "stalk". "stalker"
            // appears verbatim in many predator descriptions.
            Assert.IsTrue(ParsingHelpers.IsAggressive(new[] { "Cavestalker behaviour — solitary stalker pattern." }));
        }

        [Test]
        public void IsAggressive_AmbushMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsAggressive(new[] { "Ambush attacker from above." }));
        }

        [Test]
        public void IsAggressive_HunterMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsAggressive(new[] { "A pack hunter at night." }));
        }

        [Test]
        public void IsAggressive_NoMarker_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.IsAggressive(new[] { "Grazes peacefully." }));
        }

        [Test]
        public void IsAggressive_Null_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.IsAggressive(null));
            Assert.IsFalse(ParsingHelpers.IsAggressive(new string[0]));
        }

        // ---- IsPassive ----------------------------------------------------

        [Test]
        public void IsPassive_GrazeMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsPassive(new[] { "Wanders in herds, grazes on grass." }));
        }

        [Test]
        public void IsPassive_FleeMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsPassive(new[] { "Flees from any threat." }));
        }

        [Test]
        public void IsPassive_NoMarker_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.IsPassive(new[] { "Pursues prey relentlessly." }));
        }

        // ---- IsTameable ---------------------------------------------------

        [Test]
        public void IsTameable_BehaviorMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsTameable(
                new[] { "Can be tamed with Calming Pheromone." }, "Lowlands grazer."));
        }

        [Test]
        public void IsTameable_DescMarker_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsTameable(
                new[] { "Hostile in caves." }, "A domesticated companion when reared from a pup."));
        }

        [Test]
        public void IsTameable_NameTag_Returns_True()
        {
            Assert.IsTrue(ParsingHelpers.IsTameable(
                new[] { "Wanders." }, "No taming markers in desc.", "Cluck (tame)"));
        }

        [Test]
        public void IsTameable_NoSignal_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.IsTameable(
                new[] { "Untamable apex." }, "Hunts the player without exception."));
        }

        [Test]
        public void IsTameable_Null_Returns_False()
        {
            Assert.IsFalse(ParsingHelpers.IsTameable(null, null));
        }
    }
}
#endif
