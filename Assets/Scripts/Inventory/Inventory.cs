using System;
using System.Collections.Generic;

/// <summary>
/// A grid of ItemStack slots. Not a MonoBehaviour — plain C# class.
/// Construct with width × height. Index 0 = top-left, row-major.
/// </summary>
public class Inventory
{
    public int Width  { get; private set; }
    public int Height { get; private set; }
    public int SlotCount => Width * Height;

    /// <summary>
    /// Multiplier applied to every item's maxStackSize in this inventory.
    /// 1 = normal, 4 = compression chest (64→256).
    /// </summary>
    public int StackMultiplier { get; private set; } = 1;

    /// <summary>
    /// When true, RemoveItem never actually depletes stacks (used by DevBackpack).
    /// </summary>
    public bool IsInfinite { get; set; }

    private ItemStack[] _slots;

    /// <summary>Fired whenever any slot changes.</summary>
    public event Action OnInventoryChanged;

    public Inventory(int width, int height, int stackMultiplier = 1)
    {
        Width  = width;
        Height = height;
        StackMultiplier = Math.Max(1, stackMultiplier);
        _slots = new ItemStack[SlotCount];
        // All slots start as default (empty) ItemStack values.
    }

    /// <summary>Returns the effective max stack size for an item in this inventory.</summary>
    public int GetMaxStackSize(ItemDefinition item)
    {
        if (item == null) return 1;
        return item.maxStackSize * StackMultiplier;
    }

    // ---------------------------------------------------------------
    //  Slot accessors
    // ---------------------------------------------------------------

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            throw new IndexOutOfRangeException($"[Inventory] Slot index {index} out of range (0–{SlotCount - 1}).");
        return _slots[index];
    }

    public void SetSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= SlotCount)
            throw new IndexOutOfRangeException($"[Inventory] Slot index {index} out of range (0–{SlotCount - 1}).");
        _slots[index] = stack;
        OnInventoryChanged?.Invoke();
    }

    // ---------------------------------------------------------------
    //  Add / Remove
    // ---------------------------------------------------------------

    /// <summary>
    /// Tries to add <paramref name="stack"/> to the inventory.
    /// First fills existing stacks of the same item, then finds empty slots.
    /// Returns true if ALL items were placed; false if the inventory is full
    /// (in which case the stack's quantity reflects what was NOT placed).
    /// </summary>
    public bool AddItem(ItemStack stack)
    {
        if (stack.IsEmpty) return true;

        int remaining = stack.quantity;

        int effectiveMax = GetMaxStackSize(stack.item);

        // Pass 1: top up existing, non-full stacks of the same item.
        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            ItemStack slot = _slots[i];
            if (slot.IsEmpty) continue;
            if (slot.item != stack.item) continue;

            int space = effectiveMax - slot.quantity;
            if (space <= 0) continue;

            int transfer = Math.Min(space, remaining);
            _slots[i] = new ItemStack(slot.item, slot.quantity + transfer);
            remaining -= transfer;
        }

        // Pass 2: fill empty slots.
        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            int take = Math.Min(effectiveMax, remaining);
            _slots[i] = new ItemStack(stack.item, take);
            remaining -= take;
        }

        OnInventoryChanged?.Invoke();
        return remaining == 0;
    }

    /// <summary>
    /// Removes <paramref name="quantity"/> units of the item identified by
    /// <paramref name="itemId"/>. Returns true if the full quantity was removed.
    /// </summary>
    public bool RemoveItem(string itemId, int quantity)
    {
        if (quantity <= 0) return true;
        if (CountItem(itemId) < quantity) return false;

        // Infinite inventories report success but never deplete.
        if (IsInfinite) return true;

        int remaining = quantity;
        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            ItemStack slot = _slots[i];
            if (slot.IsEmpty) continue;
            if (slot.item.itemId != itemId) continue;

            int take = Math.Min(slot.quantity, remaining);
            int newQty = slot.quantity - take;
            _slots[i] = newQty > 0 ? new ItemStack(slot.item, newQty) : new ItemStack(null, 0);
            remaining -= take;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Returns the total number of units of the given item in this inventory.</summary>
    public int CountItem(string itemId)
    {
        int count = 0;
        foreach (ItemStack slot in _slots)
        {
            if (!slot.IsEmpty && slot.item.itemId == itemId)
                count += slot.quantity;
        }
        return count;
    }

    // ---------------------------------------------------------------
    //  Utility
    // ---------------------------------------------------------------

    /// <summary>Returns true if there is room to accept at least one unit of <paramref name="stack"/>.</summary>
    public bool HasRoomFor(ItemStack stack)
    {
        if (stack.IsEmpty) return true;
        int space = 0;
        foreach (ItemStack slot in _slots)
        {
            if (slot.IsEmpty)
            {
                space += stack.item.maxStackSize;
            }
            else if (slot.item == stack.item)
            {
                space += slot.item.maxStackSize - slot.quantity;
            }
            if (space >= stack.quantity) return true;
        }
        return false;
    }
}
