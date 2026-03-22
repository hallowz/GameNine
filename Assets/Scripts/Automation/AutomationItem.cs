using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// An ItemDefinition that represents a placeable automation device
    /// (conveyor belt, pneumatic tube, hopper, sorter, overflow valve, etc.).
    ///
    /// When selected on the hotbar, AutomationItemHandler activates
    /// AutomationPlacementController with this item.
    /// Placement is FREE (no grid snap) — left-click to place, right-click to rotate 90°.
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Automation/Automation Item",
                     fileName = "NewAutomationItem")]
    public class AutomationItem : ItemDefinition
    {
        [Header("Device")]
        [Tooltip("Prefab to spawn when placed. Should contain an IAutomationNode component.")]
        public GameObject devicePrefab;

        [Header("Automation Stats (for tooltip)")]
        [Tooltip("Seconds per automation tick (lower = faster). 0 = uses manager default.")]
        public float tickInterval = 0f;

        [Tooltip("Watts drawn from the power grid when active. 0 = unpowered device.")]
        public float powerDrawWatts = 0f;

        [Tooltip("Maximum items this device can hold in its internal buffer.")]
        public int   capacity = 0;

        [Tooltip("Short label for tooltip speed tier, e.g. 'Basic', 'Fast', 'Ultra-fast'.")]
        public string speedLabel = "";
    }
}
