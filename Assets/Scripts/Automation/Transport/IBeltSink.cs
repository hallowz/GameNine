namespace Voidborne.Automation.Transport
{
    /// <summary>
    /// V9.5 — Anything that can receive an item from a
    /// <see cref="ConveyorBelt"/> or be the target of an
    /// <see cref="Inserter"/>. Implementations:
    /// <list type="bullet">
    /// <item><description><see cref="ConveyorBelt"/> -- forwards items along
    /// a belt chain.</description></item>
    /// <item><description><c>StorageChestRuntime</c> -- wraps deposit /
    /// withdraw against the chest's <see cref="Inventory"/>.</description></item>
    /// <item><description>Machine input adapters that route the item into a
    /// <see cref="MachineCraftingStation"/>'s input grid.</description></item>
    /// </list>
    /// </summary>
    public interface IBeltSink
    {
        /// <summary>True if this sink can accept an item right now.</summary>
        bool CanInsert { get; }

        /// <summary>
        /// Try to push <paramref name="stack"/> into this sink. Returns true
        /// iff the stack was accepted (sinks accept all-or-nothing for M2).
        /// </summary>
        bool TryInsert(ItemStack stack);
    }
}
