namespace Voidborne.Automation
{
    /// <summary>
    /// Implemented by anything that can be tracked on the Terminal data-network:
    /// StorageDrive, DriveRack, and machines that expose their state to the ComputerTerminal.
    /// Distinct from the power network and the automation (item-routing) network.
    /// </summary>
    public interface INetworkNode
    {
        /// <summary>Unique identifier used in automation scripts (e.g. "Press_01").</summary>
        string NetworkId { get; }

        /// <summary>Human-readable type shown in the Terminal Machines tab.</summary>
        string MachineType { get; }

        /// <summary>One-line status string (e.g. "Running: Iron Plate x2/min").</summary>
        string StatusLine { get; }

        /// <summary>True when the node is paused by an automation script.</summary>
        bool IsPausedByScript { get; set; }

        /// <summary>Called by AutomationScriptEngine to change the active recipe by name. Returns false if unsupported or recipe not found.</summary>
        bool TrySetRecipe(string recipeName);
    }
}
