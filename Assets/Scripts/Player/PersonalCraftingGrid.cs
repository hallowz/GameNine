using UnityEngine;
using Voidborne.Crafting;

namespace Voidborne.Player
{
    /// <summary>
    /// MonoBehaviour on the Player.
    /// Wraps a 2×2 CraftingGrid available at all times (no workbench needed).
    /// Automatically updates the crafting result whenever the grid changes.
    /// </summary>
    public class PersonalCraftingGrid : MonoBehaviour
    {
        /// <summary>The underlying 2×2 crafting grid.</summary>
        public CraftingGrid Grid { get; private set; }

        /// <summary>The item stack that would be produced by the current grid contents. Empty if no recipe matches.</summary>
        public ItemStack CurrentResult { get; private set; }

        /// <summary>Fired whenever a slot in the grid changes (including after taking a result).</summary>
        public event System.Action OnGridChanged;

        private void Awake()
        {
            Grid = new CraftingGrid(2, 2);
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Sets a slot in the personal crafting grid and updates the cached result.
        /// </summary>
        public void SetSlot(int x, int y, ItemStack stack)
        {
            Grid.SetSlot(x, y, stack);
            UpdateResult();
            OnGridChanged?.Invoke();
        }

        /// <summary>
        /// Queries CraftingManager for a matching recipe and caches the result.
        /// Call this after manually modifying Grid.slots directly.
        /// </summary>
        public void UpdateResult()
        {
            if (CraftingManager.Instance == null)
            {
                CurrentResult = default;
                return;
            }

            CraftingRecipe match = CraftingManager.Instance.FindMatch(Grid);
            CurrentResult = (match != null) ? match.result : default;
        }

        /// <summary>
        /// If a result is available, consumes one set of ingredients from the grid,
        /// clears used slots, and returns the result stack.
        /// Returns an empty ItemStack if no result is available.
        /// </summary>
        public ItemStack TakeResult()
        {
            if (CurrentResult.IsEmpty) return default;

            ItemStack taken = CurrentResult;

            // Consume exactly the ingredients that the matching recipe requires.
            CraftingRecipe match = CraftingManager.Instance?.FindMatch(Grid);
            if (match == null)
            {
                CurrentResult = default;
                return default;
            }

            ConsumeIngredients(match);
            UpdateResult();
            OnGridChanged?.Invoke();

            return taken;
        }

        // ---------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Removes one of each ingredient the recipe requires from the grid.
        /// Uses the same bounding-box approach as the matching logic so that
        /// off-centre placements are handled correctly.
        /// </summary>
        private void ConsumeIngredients(CraftingRecipe recipe)
        {
            // Find grid bounding box.
            int gMinX = int.MaxValue, gMinY = int.MaxValue;
            int gMaxX = int.MinValue, gMaxY = int.MinValue;

            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    if (!Grid.GetSlot(x, y).IsEmpty)
                    {
                        if (x < gMinX) gMinX = x;
                        if (x > gMaxX) gMaxX = x;
                        if (y < gMinY) gMinY = y;
                        if (y > gMaxY) gMaxY = y;
                    }
                }
            }

            if (gMaxX < gMinX) return; // nothing to consume

            int bbW = gMaxX - gMinX + 1;
            int bbH = gMaxY - gMinY + 1;

            // Find recipe bounding box.
            int rMinX = int.MaxValue, rMinY = int.MaxValue;
            int rMaxX = int.MinValue, rMaxY = int.MinValue;

            for (int y = 0; y < recipe.gridHeight; y++)
            {
                for (int x = 0; x < recipe.gridWidth; x++)
                {
                    if (recipe.GetIngredient(x, y) != null)
                    {
                        if (x < rMinX) rMinX = x;
                        if (x > rMaxX) rMaxX = x;
                        if (y < rMinY) rMinY = y;
                        if (y > rMaxY) rMaxY = y;
                    }
                }
            }

            if (rMaxX < rMinX) return;

            // Determine if the grid is flipped relative to the recipe.
            bool flipped = IsFlippedMatch(recipe, rMinX, rMinY, bbW, bbH, gMinX, gMinY);

            // Decrement quantities for each ingredient slot.
            for (int dy = 0; dy < bbH; dy++)
            {
                for (int dx = 0; dx < bbW; dx++)
                {
                    int gx = flipped ? (gMinX + bbW - 1 - dx) : (gMinX + dx);
                    int gy = gMinY + dy;

                    ItemStack slot = Grid.GetSlot(gx, gy);
                    if (slot.IsEmpty) continue;

                    int newQty = slot.quantity - 1;
                    Grid.SetSlot(gx, gy, newQty > 0 ? new ItemStack(slot.item, newQty) : default);
                }
            }
        }

        private bool IsFlippedMatch(CraftingRecipe recipe,
                                    int rMinX, int rMinY,
                                    int bbW, int bbH,
                                    int gMinX, int gMinY)
        {
            // Check normal first.
            for (int dy = 0; dy < bbH; dy++)
            {
                for (int dx = 0; dx < bbW; dx++)
                {
                    ItemDefinition expected = recipe.GetIngredient(rMinX + dx, rMinY + dy);
                    ItemStack slot = Grid.GetSlot(gMinX + dx, gMinY + dy);

                    bool slotEmpty  = slot.IsEmpty;
                    bool needsEmpty = expected == null;
                    if (slotEmpty != needsEmpty) goto checkFlipped;
                    if (!slotEmpty && slot.item != expected) goto checkFlipped;
                }
            }
            return false; // normal matches

            checkFlipped:
            return true; // assume flipped (the recipe already matched, so one of them must be right)
        }
    }
}
