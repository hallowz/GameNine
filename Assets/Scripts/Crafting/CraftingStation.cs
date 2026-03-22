using UnityEngine;
using Voidborne.Crafting;

namespace Voidborne
{
    /// <summary>
    /// A world-placed crafting station (e.g., a Workbench).
    /// Has its own CraftingGrid and implements IInteractable so the player
    /// can open it by pressing E when nearby.
    /// </summary>
    public class CraftingStation : MonoBehaviour, IInteractable
    {
        // ---------------------------------------------------------------
        //  Inspector fields
        // ---------------------------------------------------------------
        [SerializeField] private int gridWidth  = 3;
        [SerializeField] private int gridHeight = 3;
        [SerializeField] private float interactRange = 3f;

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------
        public CraftingGrid Grid           { get; private set; }
        public ItemStack    CurrentResult  { get; private set; }

        public event System.Action OnGridChanged;

        // ---------------------------------------------------------------
        //  IInteractable
        // ---------------------------------------------------------------
        public string InteractPrompt =>
            $"Press E to craft ({gridWidth}×{gridHeight})";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= interactRange;

        public void Interact(GameObject interactor)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OpenCraftingStation(this);
        }

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------
        private void Awake()
        {
            Grid = new CraftingGrid(gridWidth, gridHeight);
        }

        // ---------------------------------------------------------------
        //  Grid helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Sets a slot in the crafting grid and updates the cached result.
        /// </summary>
        public void SetSlot(int x, int y, ItemStack stack)
        {
            Grid.SetSlot(x, y, stack);
            UpdateResult();
            OnGridChanged?.Invoke();
        }

        /// <summary>
        /// Queries CraftingManager for a matching recipe and caches the result.
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
        /// Consumes one set of ingredients from the grid and returns the crafted result.
        /// Returns an empty ItemStack if no recipe matches.
        /// </summary>
        public ItemStack TakeResult()
        {
            if (CurrentResult.IsEmpty) return default;

            CraftingRecipe match = CraftingManager.Instance?.FindMatch(Grid);
            if (match == null)
            {
                CurrentResult = default;
                return default;
            }

            ItemStack taken = CurrentResult;
            ConsumeIngredients(match);
            UpdateResult();
            OnGridChanged?.Invoke();

            return taken;
        }

        /// <summary>
        /// Returns true if the player at <paramref name="playerPos"/> is within interact range.
        /// </summary>
        public bool IsPlayerInRange(Vector3 playerPos) =>
            Vector3.Distance(playerPos, transform.position) <= interactRange;

        // ---------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------

        private void ConsumeIngredients(CraftingRecipe recipe)
        {
            // Compute the bounding box of occupied grid slots.
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

            if (gMaxX < gMinX) return;

            int bbW = gMaxX - gMinX + 1;
            int bbH = gMaxY - gMinY + 1;

            // Compute bounding box of recipe ingredients.
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

            bool flipped = IsFlippedMatch(recipe, rMinX, rMinY, bbW, bbH, gMinX, gMinY);

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
            return false;

            checkFlipped:
            return true;
        }
    }
}
