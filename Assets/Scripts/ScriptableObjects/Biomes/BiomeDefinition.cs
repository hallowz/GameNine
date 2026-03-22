using UnityEngine;
using Voidborne.World.Generation;

namespace Voidborne.World.Biomes
{
    /// <summary>
    /// ScriptableObject defining a single biome's climate position, surface properties,
    /// and visual settings. Terrain shape is now driven by TerrainSplines (via
    /// continentalness/erosion), not by per-biome parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBiome", menuName = "Voidborne/Biome Definition")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string biomeName = "Unnamed Biome";
        public int biomeId;

        [Tooltip("Unique byte ID for per-voxel storage in BiomeField (0-255). Must be unique across all biomes.")]
        public byte biomeIdByte;

        [Header("Climate Position (ideal point in 5D parameter space)")]
        [Range(0f, 1f)] public float idealTemperature = 0.5f;
        [Range(0f, 1f)] public float idealMoisture = 0.5f;
        [Range(0f, 1f)] public float idealContinentalness = 0.5f;
        [Range(0f, 1f)] public float idealErosion = 0.5f;

        [Header("Depth Range (for underground biomes)")]
        [Tooltip("Minimum normalized depth below surface. 0 = surface biome.")]
        public float depthMin;
        [Tooltip("Maximum normalized depth below surface. 0 = surface biome.")]
        public float depthMax;

        [Header("Overhangs (W1.1)")]
        public bool hasOverhangs;
        [Range(0f, 1f)] public float overhangStrength;
        public float overhangFrequency = 0.018f;
        public float overhangMinY = 20f;

        [Header("Surface Layers")]
        [Tooltip("Ore/terrain type ID for the top surface layer (e.g., grass=9, snow, sand).")]
        public byte surfaceTopId = 9;  // GrassOreId by default
        [Tooltip("Ore/terrain type ID for the sub-surface layer (e.g., dirt=8).")]
        public byte surfaceSubId = 8;  // DirtOreId by default
        [Tooltip("Depth in voxels for the top surface layer.")]
        public int surfaceTopDepth = 2;
        [Tooltip("Depth in voxels for the sub-surface layer.")]
        public int surfaceSubDepth = 4;

        [Header("Visual")]
        public Color colorTint = Color.green;
        [Tooltip("Which of the 4 shader texture slots (0-3) this biome uses for its surface. " +
                 "Multiple biomes can share a texture group while having distinct tint colors.")]
        [Range(0, 3)] public int textureGroup;
        [Tooltip("Index into the terrain texture array for this biome's surface.")]
        public int surfaceMaterialIndex;

        /// <summary>
        /// Converts this definition into a BiomeData struct for use in the density function
        /// (overhang parameters only).
        /// </summary>
        public BiomeData ToBiomeData()
        {
            return new BiomeData
            {
                overhangStrength  = hasOverhangs ? overhangStrength : 0f,
                overhangFrequency = overhangFrequency,
                overhangMinY      = overhangMinY
            };
        }
    }
}
