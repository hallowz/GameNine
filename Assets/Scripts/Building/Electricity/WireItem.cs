using UnityEngine;

namespace Voidborne.Building.Electricity
{
    public enum WireTier
    {
        Copper,     // 100W, 20m
        Insulated,  // 500W, 30m
        HeavyDuty   // 2000W, 15m
    }

    /// <summary>
    /// Item that activates the WireController when held on the hotbar.
    /// Different tiers have different throughput limits and max lengths.
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Electricity/Wire Item", fileName = "WireItem")]
    public class WireItem : ItemDefinition
    {
        [Header("Wire")]
        public float maxLength = 20f;

        [Header("Tier")]
        public WireTier wireTier = WireTier.Copper;

        [Tooltip("Maximum watts this wire can carry. Exceeding this causes a hard cutoff — nothing downstream gets power.")]
        public float maxThroughputWatts = 100f;

        [Header("Visuals")]
        [Tooltip("Color tint for the wire LineRenderer.")]
        public Color wireColor = Color.yellow;

        [Tooltip("Line width for the wire.")]
        public float wireWidth = 0.05f;
    }
}
