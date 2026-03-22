using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Crafting
{
    /// <summary>
    /// A 2D grid of ItemStack slots used for crafting.
    /// Not a MonoBehaviour — plain C# class.
    /// Supports bounding-box pattern matching and horizontal mirroring.
    /// </summary>
    public class CraftingGrid
    {
        public int Width  { get; private set; }
        public int Height { get; private set; }

        public ItemStack[] slots;

        public CraftingGrid(int width, int height)
        {
            Width  = width;
            Height = height;
            slots  = new ItemStack[width * height];
        }

        // ---------------------------------------------------------------
        //  Slot accessors
        // ---------------------------------------------------------------

        public ItemStack GetSlot(int x, int y)
        {
            return slots[y * Width + x];
        }

        public void SetSlot(int x, int y, ItemStack stack)
        {
            slots[y * Width + x] = stack;
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i] = default;
        }

        // ---------------------------------------------------------------
        //  Recipe matching
        // ---------------------------------------------------------------

        /// <summary>
        /// Checks if the current grid contents match any recipe in <paramref name="recipes"/>.
        /// Matching is bounding-box based (position-independent within the grid).
        /// Also checks a horizontally flipped layout.
        /// Returns the first matching recipe, or null.
        /// </summary>
        public CraftingRecipe CheckRecipes(List<CraftingRecipe> recipes)
        {
            if (recipes == null) return null;

            // Get the bounding box of non-empty slots in this grid.
            if (!GetBoundingBox(out int gMinX, out int gMinY, out int gMaxX, out int gMaxY))
                return null; // grid is entirely empty

            int gBBW = gMaxX - gMinX + 1;
            int gBBH = gMaxY - gMinY + 1;

            foreach (CraftingRecipe recipe in recipes)
            {
                if (recipe == null) continue;
                if (recipe.ingredients == null) continue;

                // Get bounding box of the recipe pattern.
                if (!GetRecipeBoundingBox(recipe, out int rMinX, out int rMinY, out int rMaxX, out int rMaxY))
                    continue; // empty recipe

                int rBBW = rMaxX - rMinX + 1;
                int rBBH = rMaxY - rMinY + 1;

                // Bounding boxes must match in size.
                if (rBBW != gBBW || rBBH != gBBH) continue;

                // Try normal orientation.
                if (PatternMatches(recipe, rMinX, rMinY, rBBW, rBBH,
                                   gMinX, gMinY, false))
                    return recipe;

                // Try horizontally flipped.
                if (PatternMatches(recipe, rMinX, rMinY, rBBW, rBBH,
                                   gMinX, gMinY, true))
                    return recipe;
            }

            return null;
        }

        // ---------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Finds the bounding box of non-empty slots in this grid.
        /// Returns false if the grid is completely empty.
        /// </summary>
        private bool GetBoundingBox(out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!GetSlot(x, y).IsEmpty)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            return maxX >= minX; // false if nothing found
        }

        /// <summary>
        /// Finds the bounding box of non-null ingredient slots in a recipe.
        /// Returns false if the recipe has no ingredients.
        /// </summary>
        private bool GetRecipeBoundingBox(CraftingRecipe recipe,
                                          out int minX, out int minY,
                                          out int maxX, out int maxY)
        {
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;

            for (int y = 0; y < recipe.gridHeight; y++)
            {
                for (int x = 0; x < recipe.gridWidth; x++)
                {
                    if (recipe.GetIngredient(x, y) != null)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            return maxX >= minX;
        }

        /// <summary>
        /// Compares the bounding-box region of the recipe against the bounding-box
        /// region of the current grid. Optionally flips the grid region horizontally.
        /// </summary>
        private bool PatternMatches(CraftingRecipe recipe,
                                    int rMinX, int rMinY, int bbW, int bbH,
                                    int gMinX, int gMinY,
                                    bool flipGrid)
        {
            for (int dy = 0; dy < bbH; dy++)
            {
                for (int dx = 0; dx < bbW; dx++)
                {
                    ItemDefinition expected = recipe.GetIngredient(rMinX + dx, rMinY + dy);

                    int gx = flipGrid ? (gMinX + bbW - 1 - dx) : (gMinX + dx);
                    int gy = gMinY + dy;

                    ItemStack slot = GetSlot(gx, gy);

                    // Both must be empty, or both must contain the same item.
                    bool slotEmpty  = slot.IsEmpty;
                    bool needsEmpty = expected == null;

                    if (slotEmpty != needsEmpty) return false;
                    if (!slotEmpty && slot.item != expected) return false;
                }
            }
            return true;
        }
    }
}
