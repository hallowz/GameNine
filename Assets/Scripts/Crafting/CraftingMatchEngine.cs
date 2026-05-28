using System;
using System.Collections.Generic;
using Voidborne.Automation;
using Voidborne.Data;

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
        /// Effective output quality modifier for this match. 1.0 = full output;
        /// <c>Hybrid_Crafting</c> property-fallback matches are capped at 0.7.
        /// Specific-input matches use the recipe's declared <see cref="RecipeDefinition.outputModifier"/> as-is.
        /// </summary>
        public float outputModifier;

        /// <summary>
        /// Map of property tag name to the item id that satisfied that requirement.
        /// Empty when the match was via specific <see cref="RecipeDefinition.ingredients"/>;
        /// populated when matched via the <see cref="RecipeDefinition.inputProperties"/> fallback.
        /// Useful for downstream UI ("you used milk in place of water").
        /// </summary>
        public Dictionary<string, string> inputBindings;

        /// <summary>
        /// Human-readable explanation of the match (or refusal). Non-empty for both
        /// success and failure paths. UI surfaces this verbatim in the Machine tooltip /
        /// failure toast.
        /// </summary>
        public string reason;

        /// <summary>True when the match succeeded (recipe != null).</summary>
        public bool Matched => recipe != null;
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
        /// matching rules, or a result with <c>recipe == null</c> and a <c>reason</c> explaining
        /// the refusal (picky machine with a property-only recipe, missing inputs, etc).
        /// </returns>
        RecipeMatchResult TryMatch(
            RecipeDefinition recipe,
            MachineProcessType machineType,
            IReadOnlyDictionary<string, int> availableItems,
            IReadOnlyList<ItemDefinition> itemDb);
    }

    /// <summary>
    /// Volume 6.1 — full forgiving/picky/hybrid match engine.
    /// </summary>
    /// <remarks>
    /// <para>Algorithm:</para>
    /// <list type="number">
    /// <item><description>Try specific <see cref="RecipeDefinition.ingredients"/> match (bag-of-items, position-independent).</description></item>
    /// <item><description>If specific match succeeded: return success with the recipe's declared efficiency/outputModifier.</description></item>
    /// <item><description>If specific match failed AND machine is <c>Forgiving_*</c>: try property fallback via
    ///   <see cref="RecipeDefinition.inputProperties"/>. Efficiency = recipe.efficiency × product(perInputEfficiency).
    ///   outputModifier = recipe.outputModifier (no cap on forgiving).</description></item>
    /// <item><description>If specific match failed AND machine is <c>Hybrid_Crafting</c>: same property fallback,
    ///   but outputModifier is clamped to <c>min(recipe.outputModifier, 0.7)</c>.</description></item>
    /// <item><description>If specific match failed AND machine is <c>Picky_*</c>: refuse. Return null-recipe result with reason.</description></item>
    /// </list>
    /// <para>Coop note: pure function. No mutation of inputs, no caching, no static state. Two callers
    /// invoking <see cref="TryMatch"/> with identical inputs always get equal outputs.</para>
    /// </remarks>
    public sealed class DefaultCraftingMatchEngine : ICraftingMatchEngine
    {
        /// <summary>Hybrid-machine output-quality cap for property-fallback matches (per design philosophy).</summary>
        public const float HybridPropertyOutputCap = 0.7f;

        /// <inheritdoc/>
        public RecipeMatchResult TryMatch(
            RecipeDefinition recipe,
            MachineProcessType machineType,
            IReadOnlyDictionary<string, int> availableItems,
            IReadOnlyList<ItemDefinition> itemDb)
        {
            if (recipe == null)
            {
                return new RecipeMatchResult
                {
                    recipe = null,
                    efficiency = 0f,
                    outputModifier = 0f,
                    inputBindings = new Dictionary<string, string>(),
                    reason = "No recipe supplied.",
                };
            }

            var available = availableItems ?? EmptyAvailable;

            // -----------------------------------------------------------------
            // Step 1: try specific ingredient match (the picky/default path).
            // -----------------------------------------------------------------
            if (TrySpecificMatch(recipe, available, out var specificBindings, out string specificMiss))
            {
                return new RecipeMatchResult
                {
                    recipe = recipe,
                    efficiency = recipe.efficiency,
                    outputModifier = recipe.outputModifier,
                    inputBindings = specificBindings,
                    reason = "Specific recipe matched.",
                };
            }

            // -----------------------------------------------------------------
            // Step 2: gate on process type. Picky machines stop here.
            // -----------------------------------------------------------------
            bool isForgiving = IsForgiving(machineType);
            bool isHybrid = machineType == MachineProcessType.Hybrid_Crafting;

            if (!isForgiving && !isHybrid)
            {
                return new RecipeMatchResult
                {
                    recipe = null,
                    efficiency = 0f,
                    outputModifier = 0f,
                    inputBindings = new Dictionary<string, string>(),
                    reason = "Picky machine refuses property fallback. " + specificMiss,
                };
            }

            // -----------------------------------------------------------------
            // Step 3: property fallback for Forgiving_* / Hybrid_Crafting.
            // -----------------------------------------------------------------
            if (recipe.inputProperties == null || recipe.inputProperties.Length == 0)
            {
                return new RecipeMatchResult
                {
                    recipe = null,
                    efficiency = 0f,
                    outputModifier = 0f,
                    inputBindings = new Dictionary<string, string>(),
                    reason = "Specific inputs missing and recipe declares no inputProperties[] fallback. " + specificMiss,
                };
            }

            if (!TryPropertyMatch(recipe.inputProperties, available, itemDb,
                    out var propertyBindings, out float efficiencyProduct, out string propertyMiss))
            {
                return new RecipeMatchResult
                {
                    recipe = null,
                    efficiency = 0f,
                    outputModifier = 0f,
                    inputBindings = new Dictionary<string, string>(),
                    reason = "Property fallback failed: " + propertyMiss,
                };
            }

            float effectiveEfficiency = recipe.efficiency * efficiencyProduct;
            float effectiveOutput = recipe.outputModifier;
            string note;

            if (isHybrid && effectiveOutput > HybridPropertyOutputCap)
            {
                effectiveOutput = HybridPropertyOutputCap;
                note = "Hybrid property fallback (output capped at " + HybridPropertyOutputCap.ToString("0.##") + "). ";
            }
            else if (isHybrid)
            {
                note = "Hybrid property fallback. ";
            }
            else
            {
                note = "Forgiving property fallback. ";
            }

            return new RecipeMatchResult
            {
                recipe = recipe,
                efficiency = effectiveEfficiency,
                outputModifier = effectiveOutput,
                inputBindings = propertyBindings,
                reason = note + BuildSubstitutionSummary(recipe.inputProperties, propertyBindings, efficiencyProduct),
            };
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static readonly Dictionary<string, int> EmptyAvailable = new Dictionary<string, int>(0);

        /// <summary>
        /// Returns true when every <see cref="Ingredient"/> in <paramref name="recipe"/> is
        /// satisfied (count >= qty) by <paramref name="available"/>. Pure function; does NOT
        /// consume from <paramref name="available"/>. On success, <paramref name="bindings"/>
        /// maps each ingredient id to itself.
        /// </summary>
        /// <param name="missReason">When false is returned, contains a short reason
        /// (e.g. "Need iron_ore x3 (have 1)") for the caller to surface.</param>
        public bool TrySpecificMatch(
            RecipeDefinition recipe,
            IReadOnlyDictionary<string, int> available,
            out Dictionary<string, string> bindings,
            out string missReason)
        {
            bindings = new Dictionary<string, string>();
            missReason = string.Empty;

            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Length == 0)
            {
                missReason = "Recipe has no specific ingredients[].";
                return false;
            }

            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                var ing = recipe.ingredients[i];
                if (string.IsNullOrEmpty(ing.itemId))
                {
                    missReason = "Ingredient[" + i + "] has empty itemId.";
                    return false;
                }
                int requiredQty = ing.qty > 0 ? ing.qty : 1;
                if (!available.TryGetValue(ing.itemId, out int have) || have < requiredQty)
                {
                    missReason = "Need " + ing.itemId + " x" + requiredQty + " (have " + have + ").";
                    bindings = new Dictionary<string, string>();
                    return false;
                }
                bindings[ing.itemId] = ing.itemId;
            }
            return true;
        }

        /// <summary>
        /// Greedy resolver: for each <see cref="InputProperty"/> requirement, scan the item
        /// db for an item in <paramref name="available"/> whose <c>properties[]</c> contains
        /// the required tag AND whose count satisfies the qty. Items are consumed at most
        /// once across requirements (within this call), so the same item id can't double-bill.
        /// </summary>
        /// <param name="bindings">On success: maps "<paramref name="property"/>" name to the resolved item id.</param>
        /// <param name="efficiencyProduct">Product of per-requirement <see cref="InputProperty.efficiency"/>.</param>
        /// <param name="missReason">When false: explains which requirement could not be satisfied.</param>
        public bool TryPropertyMatch(
            InputProperty[] requirements,
            IReadOnlyDictionary<string, int> available,
            IReadOnlyList<ItemDefinition> itemDb,
            out Dictionary<string, string> bindings,
            out float efficiencyProduct,
            out string missReason)
        {
            bindings = new Dictionary<string, string>();
            efficiencyProduct = 1f;
            missReason = string.Empty;

            if (requirements == null || requirements.Length == 0)
            {
                missReason = "No property requirements.";
                return false;
            }

            // Track per-item consumption so a single item can't satisfy two requirements
            // beyond what its available count supports. Build a working copy locally; we
            // never mutate the caller's `available` map (coop-safety).
            var remaining = new Dictionary<string, int>(available?.Count ?? 0);
            if (available != null)
            {
                foreach (var kv in available) remaining[kv.Key] = kv.Value;
            }

            for (int i = 0; i < requirements.Length; i++)
            {
                var req = requirements[i];
                int needQty = req.qty > 0 ? req.qty : 1;

                string resolved = FindItemSatisfying(req.property, needQty, remaining, itemDb);
                if (resolved == null)
                {
                    missReason = "No item carrying " + req.property + " (qty " + needQty + ") in inputs.";
                    bindings = new Dictionary<string, string>();
                    efficiencyProduct = 0f;
                    return false;
                }

                remaining[resolved] = remaining[resolved] - needQty;
                bindings[req.property.ToString()] = resolved;

                float perEff = req.efficiency > 0f ? req.efficiency : 1f;
                efficiencyProduct *= perEff;
            }

            return true;
        }

        private static string FindItemSatisfying(
            MaterialProperties property,
            int neededQty,
            IReadOnlyDictionary<string, int> remaining,
            IReadOnlyList<ItemDefinition> itemDb)
        {
            if (itemDb == null) return null;
            for (int j = 0; j < itemDb.Count; j++)
            {
                var def = itemDb[j];
                if (def == null) continue;
                if (!def.HasProperty(property)) continue;
                string id = def.itemId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!remaining.TryGetValue(id, out int have)) continue;
                if (have < neededQty) continue;
                return id;
            }
            return null;
        }

        private static bool IsForgiving(MachineProcessType type)
        {
            switch (type)
            {
                case MachineProcessType.Forgiving_Thermal_DryBurn:
                case MachineProcessType.Forgiving_Thermal_Boil:
                case MachineProcessType.Forgiving_Organic_Decay:
                case MachineProcessType.Forgiving_Organic_Dry:
                case MachineProcessType.Forgiving_Mechanical_Crush:
                case MachineProcessType.Forgiving_Mechanical_Separate:
                case MachineProcessType.Forgiving_Pressure:
                    return true;
                default:
                    return false;
            }
        }

        private static string BuildSubstitutionSummary(
            InputProperty[] requirements,
            Dictionary<string, string> bindings,
            float efficiencyProduct)
        {
            if (requirements == null || requirements.Length == 0 || bindings == null || bindings.Count == 0)
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(64);
            sb.Append("Substituted ");
            for (int i = 0; i < requirements.Length; i++)
            {
                var req = requirements[i];
                if (i > 0) sb.Append(", ");
                string propName = req.property.ToString();
                if (bindings.TryGetValue(propName, out string itemId))
                {
                    sb.Append(itemId).Append(" -> ").Append(propName);
                }
                else
                {
                    sb.Append("?? -> ").Append(propName);
                }
            }
            sb.Append(" (efficiency x").Append(efficiencyProduct.ToString("0.##")).Append(").");
            return sb.ToString();
        }
    }
}
