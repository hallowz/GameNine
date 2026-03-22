namespace Voidborne.World.Generation
{
    /// <summary>
    /// Blittable struct holding the 5 climate parameters used for biome selection.
    /// Temperature and moisture are classification-only (don't affect terrain shape).
    /// Continentalness and erosion drive terrain shape via TerrainSplines.
    /// Depth is derived from surface distance and enables underground biomes.
    /// </summary>
    public struct ClimateParameters
    {
        public float temperature;      // 0-1, hot/cold classification
        public float moisture;         // 0-1, wet/dry classification
        public float continentalness;  // 0-1, low = lowlands, high = inland mountains
        public float erosion;          // 0-1, high = flat/smooth, low = rough/dramatic
        public float depth;            // 0+ normalized depth below surface (0 = at surface)
    }
}
