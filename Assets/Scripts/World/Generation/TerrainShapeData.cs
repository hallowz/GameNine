namespace Voidborne.World.Generation
{
    /// <summary>
    /// Blittable struct holding terrain shape parameters derived from climate
    /// via TerrainSplines. Replaces the terrain-shape portion of the old BiomeData.
    /// These parameters are fed to the GPU density shader and CPU density function.
    /// </summary>
    [System.Serializable]
    public struct TerrainShapeData
    {
        public float heightScale;       // Amplitude of terrain height
        public float heightFrequency;   // Frequency of height noise
        public float caveScale;         // How pronounced caves are
        public float caveDensity;       // Threshold for cave carving
        public int   noiseOctaves;      // Number of noise octaves
        public float persistence;       // Octave amplitude falloff
        public float lacunarity;        // Octave frequency multiplier

        public static TerrainShapeData Default => new TerrainShapeData
        {
            heightScale     = 35f,
            heightFrequency = 0.005f,
            caveScale       = 0.6f,
            caveDensity     = 0.26f,
            noiseOctaves    = 3,
            persistence     = 0.5f,
            lacunarity      = 2f
        };
    }
}
