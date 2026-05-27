using System;
using System.Collections.Generic;
using Voidborne.Automation;

namespace Voidborne.Crafting
{
    /// <summary>
    /// Result of a <see cref="ICraftingMatchEngine.TryMatch"/> call.
    /// </summary>
    /// <remarks>
    /// Returned by reference rather than by struct so the consumer can check for
    /// <c>null</c> to signify "no match". Coop note: pure data.
    /// </remarks>
    public class RecipeMatchResult
    {
        /// <summary>The matched recipe. Null if no match was found.</summary>
        public RecipeDefinition recipe;

        /// <summary>
        /// Effective efficiency multiplier for this match, computed as the product of:
        /// <list type="bullet">
        /// <item><description>The recipe's <see cref="RecipeDefinition.efficiency"/>.</description></item>
        /// <item><description>The minimum per-input <c>InputProperty.efficiency</c> across all property-fallback substitutions used.</description></item>
        /// </list>
        /// 1.0 means full speed; values &lt; 1.0 indicate degraded (e.g. milk in a boiler).
        /// </summary>
        public float efficiency;

        /// <summary>
        /// Map of property tag name to the item id that satisfied that requirement.
        /// Empty when the match was via specific <see cref="RecipeDefinition.ingredients"/>;
        /// populated when matched via the <see cref="RecipeDefinition.inputProperties"/> fallback.
        /// Useful for downstream UI ("you used milk in place of water").
        /// </summary>
        public Dictionary<string, string> inputBindings;
    }

    /// <summary>
    /// Volume 2.8 — interface for the crafting match engine. The full forgiving/picky/hybrid
    /// matching logic lands in Volume 6.1; this interface lets M1 compile against it so
    /// other systems (Machine UI tooltip, recipe list panel) can be wired up early.
    /// </summary>
    /// <remarks>
    /// Coop note: implementations must be stateless. Per-call inputs are passed in by the
    /// caller; the engine never mutates the recipe or item-db arguments.
    /// </remarks>
    public interface ICraftingMatchEngine
    {
        /// <summary>
        /// Attempt to match a single recipe against the player's currently-available items.
        /// </summary>
        /// <param name="recipe">The candidate recipe.</param>
        /// <param name="machineType">The process type of the machine the recipe is being attempted on.</param>
        /// <param name="availableItems">Map of item id to count currently available to the machine.</param>
        /// <param name="itemDb">Item database used to look up property tags on candidate substitution items.</param>
        /// <returns>
        /// A <see cref="RecipeMatchResult"/> if the recipe matches given the process type's
        /// matching rules, or <c>null</c> if no match (e.g. picky machine with a property-only
        /// recipe, or hybrid machine where the property fallback would drop output below
        /// the quality floor).
        /// </returns>
        RecipeMatchResult TryMatch(
            RecipeDefinition recipe,
            MachineProcessType machineType,
            IReadOnlyDictionary<string, int> availableItems,
            IReadOnlyList<ItemDefinition> itemDb);
    }

    /// <summary>
    /// Volume 2.8 — stub implementation. Throws <see cref="NotImplementedException"/>.
    /// The real implementation lands in Volume 6.1; this stub exists so the M1 build
    /// has a concrete type to bind against (e.g. for inspector serialization or for
    /// tests that don't actually invoke matching).
    /// </summary>
    public sealed class DefaultCraftingMatchEngine : ICraftingMatchEngine
    {
        /// <inheritdoc/>
        public RecipeMatchResult TryMatch(
            RecipeDefinition recipe,
            MachineProcessType machineType,
            IReadOnlyDictionary<string, int> availableItems,
            IReadOnlyList<ItemDefinition> itemDb)
        {
            throw new NotImplementedException("Full match logic in Volume 6.1");
        }
    }
}
