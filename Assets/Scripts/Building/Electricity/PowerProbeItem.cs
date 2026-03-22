using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Handheld Power Probe — when held on the hotbar, opens the PowerProbeUI
    /// showing a full live overview of the network connected to the device
    /// the player is looking at.
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Electricity/Power Probe Item",
                     fileName = "PowerProbeItem")]
    public class PowerProbeItem : ItemDefinition
    {
        [Header("Probe")]
        public float scanRange = 10f;
    }
}
