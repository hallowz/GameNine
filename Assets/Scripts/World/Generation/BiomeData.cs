namespace Voidborne.World.Generation
{
    /// <summary>
    /// Per-biome surface data that supplements terrain shape. Contains only biome-specific
    /// features (overhangs) that can't be derived from the terrain splines alone.
    /// Terrain shape parameters (heightScale, heightFrequency, etc.) are now in TerrainShapeData.
    /// </summary>
    [System.Serializable]
    public struct BiomeData
    {
        // Overhang / arch generation (W1.1)
        // overhangStrength == 0 means no overhangs for this biome
        public float overhangStrength;   // 0-1: how aggressively overhangs form
        public float overhangFrequency;  // 3D noise frequency for overhang blobs
        public float overhangMinY;       // Only add overhangs above this world Y

        public static BiomeData Default => new BiomeData
        {
            overhangStrength  = 0f,
            overhangFrequency = 0.018f,
            overhangMinY      = 20f
        };
    }
}
