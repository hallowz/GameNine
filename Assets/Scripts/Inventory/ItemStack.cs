using System;
using UnityEngine;

[Serializable]
public struct ItemStack
{
    public ItemDefinition item;
    public int quantity;

    public ItemStack(ItemDefinition item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public bool IsEmpty => item == null || quantity <= 0;

    /// <summary>Returns true if both stacks hold the same item type and neither is empty.</summary>
    public bool CanStackWith(ItemStack other)
    {
        if (IsEmpty || other.IsEmpty) return false;
        if (item != other.item) return false;
        return quantity < item.maxStackSize;
    }

    /// <summary>
    /// Splits off <paramref name="amount"/> from this stack and returns it as a new stack.
    /// Modifies this stack's quantity in place (use the ref pattern or reassign).
    /// </summary>
    public ItemStack Split(int amount)
    {
        if (amount <= 0) return new ItemStack(null, 0);
        if (amount >= quantity)
        {
            ItemStack full = new ItemStack(item, quantity);
            return full;
        }
        return new ItemStack(item, amount);
    }

    public override string ToString()
    {
        if (IsEmpty) return "Empty";
        return $"{item.displayName} x{quantity}";
    }
}
