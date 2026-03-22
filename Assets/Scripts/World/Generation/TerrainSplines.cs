using Unity.Mathematics;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Converts continentalness and erosion climate parameters into terrain shape
    /// parameters. This decouples terrain geometry from biome identity — biomes
    /// "paint over" terrain rather than controlling its shape.
    ///
    /// Uses bilinear interpolation between control points. The control points are
    /// tuned to reproduce the height diversity of the original per-biome system
    /// (Plains ~10, Alpine Peaks ~100, etc.) but driven by continuous noise fields.
    /// </summary>
    public static class TerrainSplines
    {
        /// <summary>
        /// Compute full terrain shape data from climate parameters.
        /// </summary>
        public static TerrainShapeData GetTerrainShape(float continentalness, float erosion)
        {
            return new TerrainShapeData
            {
                heightScale     = GetHeightScale(continentalness, erosion),
                heightFrequency = GetHeightFrequency(continentalness, erosion),
                caveScale       = GetCaveScale(erosion),
                caveDensity     = GetCaveDensity(erosion),
                noiseOctaves    = GetNoiseOctaves(continentalness),
                persistence     = GetPersistence(continentalness),
                lacunarity      = GetLacunarity(continentalness, erosion)
            };
        }

        /// <summary>
        /// Height scale: low continentalness = flat lowlands (~10),
        /// high continentalness + low erosion = towering peaks (~100),
        /// high continentalness + high erosion = moderate hills (~40).
        /// </summary>
        public static float GetHeightScale(float continentalness, float erosion)
        {
            // Four corners: (continent, erosion) → heightScale
            // (0,0) low-continent rough = 15  (small rough terrain)
            // (0,1) low-continent smooth = 10 (flat plains)
            // (1,0) high-continent rough = 100 (alpine peaks)
            // (1,1) high-continent smooth = 40 (rolling highlands)
            float c = math.saturate(continentalness);
            float e = math.saturate(erosion);

            float lowC  = math.lerp(15f, 10f, e);   // low continentalness
            float highC = math.lerp(100f, 40f, e);   // high continentalness

            return math.lerp(lowC, highC, c);
        }

        /// <summary>
        /// Height frequency: higher continentalness = more detailed terrain,
        /// higher erosion = smoother (lower frequency).
        /// </summary>
        public static float GetHeightFrequency(float continentalness, float erosion)
        {
            float c = math.saturate(continentalness);
            float e = math.saturate(erosion);

            // (0,0) = 0.004, (0,1) = 0.002, (1,0) = 0.008, (1,1) = 0.004
            float lowC  = math.lerp(0.004f, 0.002f, e);
            float highC = math.lerp(0.008f, 0.004f, e);

            return math.lerp(lowC, highC, c);
        }

        /// <summary>
        /// Cave scale: erosion controls cave size. High erosion = fewer/smaller caves.
        /// </summary>
        public static float GetCaveScale(float erosion)
        {
            // Low erosion (rough terrain) = more caves (0.6), high erosion = fewer (0.35)
            return math.lerp(0.6f, 0.35f, math.saturate(erosion));
        }

        /// <summary>
        /// Cave density: erosion controls threshold. High erosion = sparser caves.
        /// </summary>
        public static float GetCaveDensity(float erosion)
        {
            // Low erosion = denser caves (0.28), high erosion = sparser (0.24)
            return math.lerp(0.28f, 0.24f, math.saturate(erosion));
        }

        /// <summary>
        /// Noise octaves: more continental terrain gets more octaves for detail.
        /// </summary>
        public static int GetNoiseOctaves(float continentalness)
        {
            // Range: 3 (flat lowlands) to 6 (mountain peaks)
            return (int)math.round(math.lerp(3f, 6f, math.saturate(continentalness)));
        }

        /// <summary>
        /// Persistence: slightly higher for continental terrain.
        /// </summary>
        public static float GetPersistence(float continentalness)
        {
            return math.lerp(0.45f, 0.55f, math.saturate(continentalness));
        }

        /// <summary>
        /// Lacunarity: slightly higher for rough, continental terrain.
        /// </summary>
        public static float GetLacunarity(float continentalness, float erosion)
        {
            float c = math.saturate(continentalness);
            float e = math.saturate(erosion);

            // Range: 1.9 (smooth lowlands) to 2.3 (rough mountains)
            float base_ = math.lerp(2.0f, 2.3f, c);
            return math.lerp(base_, base_ - 0.1f, e);
        }
    }
}
