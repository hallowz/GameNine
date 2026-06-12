using Voidborne.Crafting;

namespace Voidborne.Automation
{
    /// <summary>
    /// V6.3 — central lookup for how long a recipe takes on a given machine,
    /// in seconds, before any per-match efficiency multiplier is applied.
    ///
    /// M2 keeps this trivially simple: every recipe is 3 seconds. Tier-scaled
    /// timing, per-recipe overrides, and balance polish are M8 work; centralising
    /// the lookup here so callers don't need to know the constant lets that
    /// later pass land without touching MachineCraftingStation.
    /// </summary>
    /// <remarks>
    /// Coop note: stateless pure function; both sides of the wire will agree
    /// on duration given identical inputs.
    /// </remarks>
    public static class MachineRecipeTimeProvider
    {
        /// <summary>Default recipe duration in seconds (M2 placeholder).</summary>
        public const float DefaultRecipeSeconds = 3.0f;

        /// <summary>
        /// Returns the base recipe duration in seconds. Efficiency multipliers
        /// from <see cref="RecipeMatchResult.efficiency"/> are applied by the
        /// caller (faster effective duration = baseSeconds / efficiency).
        /// </summary>
        public static float GetBaseSeconds(MachineDefinition machineDef, RecipeDefinition recipe)
        {
            // Both args are reserved for future per-machine / per-recipe scaling;
            // M2 ignores them and returns the constant. Callers should still
            // pass the real refs so M8 balance can wire in without churn.
            return DefaultRecipeSeconds;
        }
    }
}
