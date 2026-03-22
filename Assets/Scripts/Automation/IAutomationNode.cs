namespace Voidborne.Automation
{
    /// <summary>
    /// Implemented by anything that can send or receive items on the automation network:
    /// ConveyorBelt, PneumaticTube, Hopper endpoints, machines, chests.
    /// </summary>
    public interface IAutomationNode
    {
        /// <summary>Returns true if this node can accept one more of <paramref name="item"/>.</summary>
        bool CanAccept(ItemDefinition item);

        /// <summary>
        /// Inserts one item into this node. Caller must check CanAccept first.
        /// Returns true if the item was accepted.
        /// </summary>
        bool TryInsert(ItemStack stack);

        /// <summary>Returns true if any item matching <paramref name="filter"/> is available. Null filter = any.</summary>
        bool HasItem(ItemDefinition filter);

        /// <summary>
        /// Removes and returns one item matching <paramref name="filter"/> (null = any).
        /// Returns an empty ItemStack if nothing is available.
        /// </summary>
        ItemStack TryExtract(ItemDefinition filter);
    }
}
