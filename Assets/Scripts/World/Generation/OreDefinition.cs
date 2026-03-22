using UnityEngine;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// ScriptableObject that defines a single ore type — its identity, Y spawn range,
    /// noise parameters, and optional biome restriction.
    /// </summary>
    [CreateAssetMenu(fileName = "NewOreDefinition", menuName = "Voidborne/Ore Definition")]
    public class OreDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this ore. 0 = no ore (reserved). 1–255 = valid ore types.")]
        public byte oreTypeId;

        public string oreName;

        [Tooltip("The item dropped when this ore is mined.")]
        public ItemDefinition associatedItem;

        [Header("Visuals")]
        [Tooltip("Color tint used by the terrain shader ore overlay.")]
        public Color colorTint = Color.white;

        [Header("Spawn Range")]
        [Tooltip("Minimum world Y (inclusive) where this ore can spawn.")]
        public float minY = -200f;

        [Tooltip("Maximum world Y (inclusive) where this ore can spawn.")]
        public float maxY = 50f;

        [Header("Noise Parameters")]
        [Tooltip("Noise frequency for vein shape. Higher = smaller, tighter veins.")]
        public float noiseFrequency = 0.08f;

        [Tooltip("Noise threshold for rarity. Higher = rarer. Typical range 0.6–0.85.")]
        [Range(0f, 1f)]
        public float noiseThreshold = 0.65f;

        [Header("Biome Restriction")]
        [Tooltip("Required biome ID (biomeIdByte) for this ore. 0 = spawns in any biome.")]
        public byte requiredBiomeId;
    }
}
