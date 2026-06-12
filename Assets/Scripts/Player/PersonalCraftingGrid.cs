using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Crafting;

namespace Voidborne.Player
{
    /// <summary>
    /// MonoBehaviour on the Player.
    /// Wraps a 2x2 CraftingGrid available at all times (no workbench needed).
    ///
    /// V6.2: re-wired to consume the V6.1 <see cref="ICraftingMatchEngine"/>.
    /// The personal grid is the player's bare-hands crafting surface. It runs
    /// through the engine with <c>machineType = MachineProcessType.Hybrid_Crafting</c>
    /// so that improvised property-fallback recipes incur the 0.7x output cap,
    /// while specific recipes (the canonical bootstrap path) pay full quality.
    /// </summary>
    /// <remarks>
    /// Coop note: client-local; in V21 server-authoritative crafting will
    /// re-validate via the same deterministic engine, so a desync would imply
    /// a divergent inventory bag, not divergent match logic.
    /// </remarks>
    public class PersonalCraftingGrid : MonoBehaviour
    {
        /// <summary>The underlying 2x2 crafting grid (4 input slots).</summary>
        public CraftingGrid Grid { get; private set; }

        /// <summary>
        /// Current V6.1 match result against <see cref="RecipeRegistry"/> personal-grid
        /// recipes (viaMachineId == null). Null when no recipe matches the current bag.
        /// </summary>
        public RecipeMatchResult CurrentMatch { get; private set; }

        /// <summary>
        /// Output ItemStack that would be produced by the current grid contents.
        /// <c>quantity</c> is <c>floor(recipe.outputQty * CurrentMatch.outputModifier)</c>,
        /// minimum 1 if a match exists. Empty if no recipe matches.
        /// </summary>
        public ItemStack CurrentResult { get; private set; }

        /// <summary>Fired whenever a slot in the grid changes (including after taking a result).</summary>
        public event Action OnGridChanged;

        /// <summary>
        /// Fired whenever <see cref="CurrentMatch"/> transitions (no-match -> match,
        /// match -> different recipe, or match -> no-match). UI subscribes to refresh
        /// the output slot and any process-type badge.
        /// </summary>
        public event Action<RecipeMatchResult> OnMatchChanged;

        private readonly DefaultCraftingMatchEngine _engine = new DefaultCraftingMatchEngine();

        private void Awake()
        {
            Grid = new CraftingGrid(2, 2);
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Sets a slot in the personal crafting grid and updates the cached
        /// match + result via the V6.1 engine.
        /// </summary>
        public void SetSlot(int x, int y, ItemStack stack)
        {
            Grid.SetSlot(x, y, stack);
            UpdateResult();
            OnGridChanged?.Invoke();
        }

        /// <summary>
        /// Re-runs the V6.1 engine over every personal-grid recipe in
        /// <see cref="RecipeRegistry"/> and caches the best match.
        /// Call this after manually mutating Grid.slots directly.
        /// </summary>
        public void UpdateResult()
        {
            RecipeMatchResult previous = CurrentMatch;
            RecipeMatchResult next = ComputeBestMatch();

            CurrentMatch = next;
            CurrentResult = BuildResultStack(next);

            if (!ReferenceEquals(previous, next))
            {
                // Anchor on the recipe identity for the change-detection so we
                // don't spam OnMatchChanged when efficiency/output haven't moved.
                bool prevHasRecipe = previous != null && previous.recipe != null;
                bool nextHasRecipe = next != null && next.recipe != null;

                bool changed = prevHasRecipe != nextHasRecipe;
                if (!changed && nextHasRecipe)
                {
                    changed = previous.recipe != next.recipe
                        || !Mathf.Approximately(previous.efficiency, next.efficiency)
                        || !Mathf.Approximately(previous.outputModifier, next.outputModifier);
                }

                if (changed) OnMatchChanged?.Invoke(next);
            }
        }

        /// <summary>
        /// If a result is available, consumes the matched ingredients from the
        /// grid (per <c>CurrentMatch.inputBindings</c>) and returns the result
        /// stack. Returns an empty ItemStack if no match is current.
        /// </summary>
        public ItemStack TakeResult()
        {
            // Re-validate against the live grid so a stale CurrentMatch can't
            // produce free items if SetSlot wasn't called before TakeResult
            // (e.g. test code mutating Grid.slots directly).
            UpdateResult();
            if (CurrentMatch == null || CurrentMatch.recipe == null) return default;
            if (CurrentResult.IsEmpty) return default;

            ItemStack taken = CurrentResult;
            ConsumeMatchedIngredients(CurrentMatch);

            // Recompute after consumption so the next click sees the right state.
            UpdateResult();
            OnGridChanged?.Invoke();

            return taken;
        }

        // ---------------------------------------------------------------
        //  Engine integration
        // ---------------------------------------------------------------

        /// <summary>
        /// Walks <see cref="RecipeRegistry.ByMachine"/> for the personal-grid
        /// scope (viaMachineId == null/""), invokes <see cref="ICraftingMatchEngine"/>
        /// per candidate with machineType = Hybrid_Crafting, and returns the
        /// best result. Specific matches beat property-fallback; among ties the
        /// recipe with the highest <c>outputQty</c> wins.
        /// </summary>
        private RecipeMatchResult ComputeBestMatch()
        {
            var registry = RecipeRegistry.Instance;
            if (registry == null) return null;

            // Skip if the grid is empty -- no point churning the engine over a
            // bag of nothing. Saves cost on every empty SetSlot pass.
            Dictionary<string, int> bag = BuildAvailableBag();
            if (bag.Count == 0) return null;

            ItemDatabase itemDb = ItemDatabase.GetOrLoad();
            IReadOnlyList<ItemDefinition> dbItems = itemDb != null
                ? itemDb.AllItems
                : Array.Empty<ItemDefinition>();

            RecipeMatchResult best = null;
            bool bestIsSpecific = false;
            int bestOutputQty = 0;

            foreach (RecipeDefinition recipe in registry.ByMachine(RecipeRegistry.PersonalGridKey))
            {
                if (recipe == null) continue;
                // 2x2 capacity guard -- personal grid only fits 4 distinct ingredient slots.
                if (recipe.ingredients != null && recipe.ingredients.Length > 4) continue;

                RecipeMatchResult attempt = _engine.TryMatch(
                    recipe,
                    MachineProcessType.Hybrid_Crafting,
                    bag,
                    dbItems);

                if (attempt == null || attempt.recipe == null) continue;

                // Heuristic: a "specific" match is one where the bindings map
                // every key to itself (the V6.1 specific path emits id -> id).
                // The property-fallback path keys bindings by property tag
                // ToString(), not by item id, so the lookup-then-equality test
                // here cleanly distinguishes the two paths without needing a
                // new flag on RecipeMatchResult.
                bool isSpecific = IsSpecificBindings(attempt);

                if (best == null)
                {
                    best = attempt;
                    bestIsSpecific = isSpecific;
                    bestOutputQty = recipe.outputQty;
                    continue;
                }

                // Prefer specific over property fallback.
                if (isSpecific && !bestIsSpecific)
                {
                    best = attempt;
                    bestIsSpecific = true;
                    bestOutputQty = recipe.outputQty;
                    continue;
                }
                if (!isSpecific && bestIsSpecific) continue;

                // Among same-tier matches, prefer the recipe with the highest
                // declared output qty (the design favours abundance recipes).
                if (recipe.outputQty > bestOutputQty)
                {
                    best = attempt;
                    bestOutputQty = recipe.outputQty;
                }
            }

            return best;
        }

        private static bool IsSpecificBindings(RecipeMatchResult result)
        {
            if (result == null || result.recipe == null) return false;
            var bindings = result.inputBindings;
            if (bindings == null || bindings.Count == 0) return false;
            foreach (var kv in bindings)
            {
                if (kv.Key != kv.Value) return false;
            }
            return true;
        }

        private Dictionary<string, int> BuildAvailableBag()
        {
            var bag = new Dictionary<string, int>(4);
            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    ItemStack slot = Grid.GetSlot(x, y);
                    if (slot.IsEmpty || slot.item == null) continue;
                    string id = slot.item.itemId;
                    if (string.IsNullOrEmpty(id)) continue;
                    bag.TryGetValue(id, out int have);
                    bag[id] = have + slot.quantity;
                }
            }
            return bag;
        }

        private ItemStack BuildResultStack(RecipeMatchResult result)
        {
            if (result == null || result.recipe == null) return default;
            if (string.IsNullOrEmpty(result.recipe.outputItemId)) return default;

            ItemDatabase itemDb = ItemDatabase.GetOrLoad();
            ItemDefinition outDef = itemDb != null
                ? itemDb.GetItem(result.recipe.outputItemId)
                : null;
            if (outDef == null) return default;

            int baseQty = result.recipe.outputQty > 0 ? result.recipe.outputQty : 1;
            int finalQty = Mathf.FloorToInt(baseQty * result.outputModifier);
            if (finalQty < 1) finalQty = 1;
            return new ItemStack(outDef, finalQty);
        }

        /// <summary>
        /// Decrements consumed ingredients from the grid. Uses the matched
        /// bindings: for a specific recipe, walk <c>recipe.ingredients[]</c>;
        /// for a property fallback, walk <c>result.inputBindings.Values</c>
        /// (the engine already resolved which item id covered each property
        /// slot, so we trust its choice rather than re-resolving).
        /// </summary>
        private void ConsumeMatchedIngredients(RecipeMatchResult result)
        {
            if (result == null || result.recipe == null) return;

            bool specific = IsSpecificBindings(result);
            if (specific)
            {
                var ingredients = result.recipe.ingredients;
                if (ingredients == null) return;
                for (int i = 0; i < ingredients.Length; i++)
                {
                    int qty = ingredients[i].qty > 0 ? ingredients[i].qty : 1;
                    DecrementFromGrid(ingredients[i].itemId, qty);
                }
                return;
            }

            // Property fallback: walk the resolved binding values + the
            // recipe's inputProperty[i].qty for the per-binding quantity.
            var props = result.recipe.inputProperties;
            if (props == null || result.inputBindings == null) return;
            for (int i = 0; i < props.Length; i++)
            {
                int qty = props[i].qty > 0 ? props[i].qty : 1;
                string key = props[i].property.ToString();
                if (!result.inputBindings.TryGetValue(key, out string itemId)) continue;
                DecrementFromGrid(itemId, qty);
            }
        }

        private void DecrementFromGrid(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return;
            int remaining = qty;
            for (int y = 0; y < Grid.Height && remaining > 0; y++)
            {
                for (int x = 0; x < Grid.Width && remaining > 0; x++)
                {
                    ItemStack slot = Grid.GetSlot(x, y);
                    if (slot.IsEmpty || slot.item == null) continue;
                    if (slot.item.itemId != itemId) continue;

                    int take = Mathf.Min(remaining, slot.quantity);
                    int newQty = slot.quantity - take;
                    Grid.SetSlot(x, y, newQty > 0 ? new ItemStack(slot.item, newQty) : default);
                    remaining -= take;
                }
            }
        }
    }
}
