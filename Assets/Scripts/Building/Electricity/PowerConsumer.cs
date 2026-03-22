namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Concrete PowerNode subclass for devices that only consume power (no generation).
    /// Add this component to machines, automation devices, etc.
    /// Set powerDraw in the Inspector (or at runtime) to configure watt consumption.
    /// </summary>
    public class PowerConsumer : PowerNode
    {
        // No additional logic needed — PowerNode.GetCurrentDraw() returns powerDraw,
        // and IsPowered is managed by the network.
    }
}
