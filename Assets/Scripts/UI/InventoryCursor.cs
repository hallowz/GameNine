using Voidborne.Crafting;

namespace Voidborne.UI
{
    /// <summary>
    /// Tracks the item stack currently held on the cursor (Minecraft-style pick-up/place).
    /// Owned by UIManager. Not a MonoBehaviour — plain C# class.
    /// </summary>
    public class InventoryCursor
    {
        // ---------------------------------------------------------------
        //  Held stack
        // ---------------------------------------------------------------

        /// <summary>The stack currently held by the cursor. Empty = nothing held.</summary>
        public ItemStack HeldStack { get; set; }

        // ---------------------------------------------------------------
        //  Source tracking (where we picked up from)
        // ---------------------------------------------------------------

        /// <summary>The inventory the item was picked up from. Null if picked up from a crafting grid.</summary>
        public Inventory SourceInventory { get; private set; }

        /// <summary>Slot index in SourceInventory. -1 if from crafting grid.</summary>
        public int SourceSlotIndex { get; private set; }

        /// <summary>The crafting grid the item was picked up from. Null if from inventory.</summary>
        public CraftingGrid SourceCraftingGrid { get; private set; }

        /// <summary>X coordinate in SourceCraftingGrid. -1 if from inventory.</summary>
        public int SourceCraftingX { get; private set; }

        /// <summary>Y coordinate in SourceCraftingGrid. -1 if from inventory.</summary>
        public int SourceCraftingY { get; private set; }

        // ---------------------------------------------------------------
        //  Query
        // ---------------------------------------------------------------

        /// <summary>True when the cursor is holding a non-empty stack.</summary>
        public bool IsHolding => !HeldStack.IsEmpty;

        // ---------------------------------------------------------------
        //  Pick-up from inventory slot
        // ---------------------------------------------------------------

        public void PickUp(ItemStack stack, Inventory inventory, int slotIndex)
        {
            HeldStack           = stack;
            SourceInventory     = inventory;
            SourceSlotIndex     = slotIndex;
            SourceCraftingGrid  = null;
            SourceCraftingX     = -1;
            SourceCraftingY     = -1;
        }

        // ---------------------------------------------------------------
        //  Pick-up from a furnace slot (no inventory / grid source to return to)
        // ---------------------------------------------------------------

        /// <summary>
        /// Picks up an item from a furnace slot. The furnace manages its own slot data,
        /// so no source reference is stored — CancelAndReturn will simply discard the stack
        /// back to the furnace via FurnaceUI.Close().
        /// </summary>
        public void PickUpFurnace(ItemStack stack)
        {
            HeldStack           = stack;
            SourceInventory     = null;
            SourceSlotIndex     = -1;
            SourceCraftingGrid  = null;
            SourceCraftingX     = -1;
            SourceCraftingY     = -1;
        }

        // ---------------------------------------------------------------
        //  Pick-up from crafting grid slot
        // ---------------------------------------------------------------

        public void PickUp(ItemStack stack, CraftingGrid grid, int x, int y)
        {
            HeldStack           = stack;
            SourceInventory     = null;
            SourceSlotIndex     = -1;
            SourceCraftingGrid  = grid;
            SourceCraftingX     = x;
            SourceCraftingY     = y;
        }

        // ---------------------------------------------------------------
        //  Clear (drop / after place)
        // ---------------------------------------------------------------

        public void Clear()
        {
            HeldStack           = default;
            SourceInventory     = null;
            SourceSlotIndex     = -1;
            SourceCraftingGrid  = null;
            SourceCraftingX     = -1;
            SourceCraftingY     = -1;
        }

        // ---------------------------------------------------------------
        //  Cancel: return held item to its original source
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns the held stack to where it was picked up from, then clears the cursor.
        /// Call this when UI closes while cursor is holding something.
        /// </summary>
        public void CancelAndReturn()
        {
            if (!IsHolding)
            {
                Clear();
                return;
            }

            if (SourceInventory != null)
            {
                // Try to put it back in the exact slot first
                if (SourceSlotIndex >= 0 && SourceSlotIndex < SourceInventory.SlotCount)
                {
                    ItemStack existing = SourceInventory.GetSlot(SourceSlotIndex);
                    if (existing.IsEmpty)
                    {
                        SourceInventory.SetSlot(SourceSlotIndex, HeldStack);
                        Clear();
                        return;
                    }
                    // Slot is now occupied (e.g., we swapped something in), try to merge or find space
                    if (!existing.IsEmpty && existing.item == HeldStack.item)
                    {
                        int space = existing.item.maxStackSize - existing.quantity;
                        int move  = UnityEngine.Mathf.Min(space, HeldStack.quantity);
                        if (move > 0)
                        {
                            SourceInventory.SetSlot(SourceSlotIndex,
                                new ItemStack(existing.item, existing.quantity + move));
                            int leftover = HeldStack.quantity - move;
                            if (leftover > 0)
                                HeldStack = new ItemStack(HeldStack.item, leftover);
                            else
                            {
                                Clear();
                                return;
                            }
                        }
                    }
                }
                // Fall through: add whatever is left to the inventory generally
                SourceInventory.AddItem(HeldStack);
            }
            else if (SourceCraftingGrid != null)
            {
                if (SourceCraftingX >= 0 && SourceCraftingX < SourceCraftingGrid.Width &&
                    SourceCraftingY >= 0 && SourceCraftingY < SourceCraftingGrid.Height)
                {
                    ItemStack existing = SourceCraftingGrid.GetSlot(SourceCraftingX, SourceCraftingY);
                    if (existing.IsEmpty)
                    {
                        SourceCraftingGrid.SetSlot(SourceCraftingX, SourceCraftingY, HeldStack);
                        Clear();
                        return;
                    }
                }
            }

            Clear();
        }
    }
}
