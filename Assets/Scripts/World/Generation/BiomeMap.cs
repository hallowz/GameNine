using Unity.Mathematics;
using UnityEngine;
using Voidborne.World.Biomes;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Determines biome and terrain shape at world positions using a 4-channel climate
    /// noise system (temperature, moisture, continentalness, erosion). Terrain shape
    /// is derived from continentalness/erosion via TerrainSplines, decoupled from biome
    /// identity. Biome selection uses nearest-neighbor lookup in climate parameter space.
    /// </summary>
    public static class BiomeMap
    {
        // Noise channels
        private const int ChannelTemperature     = 10;
        private const int ChannelMoisture        = 11;
        private const int ChannelContinentalness = 13;
        private const int ChannelErosion         = 14;

        // Climate noise parameters — large scale for broad biome regions
        private const float ClimateFrequency   = 0.0008f;
        private const int   ClimateOctaves     = 4;
        private const float ClimatePersistence = 0.5f;
        private const float ClimateLacunarity  = 2f;

        // Continentalness/erosion use slightly different frequencies for variety
        private const float ContinentFrequency = 0.0006f;
        private const float ErosionFrequency   = 0.0007f;

        // Cached biome definitions and lookup structures
        private static BiomeDefinition[] _biomes;
        private static BiomeTree _biomeTree;
        private static BiomeLookupTable _lookupTable;
        private static System.Collections.Generic.Dictionary<byte, BiomeDefinition> _byteIdMap;

        /// <summary>
        /// Initializes the BiomeMap with an array of biome definitions.
        /// Builds the BiomeTree and BiomeLookupTable. If not called, falls back to BiomeRegistry defaults.
        /// </summary>
        public static void Initialize(BiomeDefinition[] biomes)
        {
            _biomes = biomes;
            BuildLookupStructures();
        }

        /// <summary>
        /// Returns the array of biome definitions currently in use.
        /// </summary>
        public static BiomeDefinition[] GetBiomes()
        {
            EnsureBiomes();
            return _biomes;
        }

        /// <summary>
        /// Sample the temperature value at a world XZ position. Range [0,1].
        /// </summary>
        public static float GetTemperature(float2 worldPosXZ)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelTemperature);
            float raw = NoiseUtilities.Noise2D(worldPosXZ, ClimateFrequency,
                ClimateOctaves, ClimatePersistence, ClimateLacunarity, seedOff);
            return math.saturate(raw * 0.5f + 0.5f);
        }

        /// <summary>
        /// Sample the moisture value at a world XZ position. Range [0,1].
        /// </summary>
        public static float GetMoisture(float2 worldPosXZ)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelMoisture);
            float raw = NoiseUtilities.Noise2D(worldPosXZ, ClimateFrequency * 1.3f,
                ClimateOctaves, ClimatePersistence, ClimateLacunarity, seedOff);
            return math.saturate(raw * 0.5f + 0.5f);
        }

        /// <summary>
        /// Sample the continentalness value at a world XZ position. Range [0,1].
        /// Low = coastal/lowland, High = inland/mountainous.
        /// </summary>
        public static float GetContinentalness(float2 worldPosXZ)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelContinentalness);
            float raw = NoiseUtilities.Noise2D(worldPosXZ, ContinentFrequency,
                ClimateOctaves, ClimatePersistence, ClimateLacunarity, seedOff);
            return math.saturate(raw * 0.5f + 0.5f);
        }

        /// <summary>
        /// Sample the erosion value at a world XZ position. Range [0,1].
        /// Low = rough/dramatic terrain, High = smooth/flat.
        /// </summary>
        public static float GetErosion(float2 worldPosXZ)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelErosion);
            float raw = NoiseUtilities.Noise2D(worldPosXZ, ErosionFrequency,
                ClimateOctaves, ClimatePersistence, ClimateLacunarity, seedOff);
            return math.saturate(raw * 0.5f + 0.5f);
        }

        /// <summary>
        /// Sample all 4 climate noise channels at a world XZ position.
        /// Depth is set to 0 (surface query). Caller can set depth for underground queries.
        /// </summary>
        public static ClimateParameters SampleClimate(float2 worldPosXZ)
        {
            return new ClimateParameters
            {
                temperature     = GetTemperature(worldPosXZ),
                moisture        = GetMoisture(worldPosXZ),
                continentalness = GetContinentalness(worldPosXZ),
                erosion         = GetErosion(worldPosXZ),
                depth           = 0f
            };
        }

        /// <summary>
        /// Get the terrain shape data at a world XZ position, derived from
        /// continentalness and erosion via TerrainSplines.
        /// </summary>
        public static TerrainShapeData GetTerrainShape(float2 worldPosXZ)
        {
            float c = GetContinentalness(worldPosXZ);
            float e = GetErosion(worldPosXZ);
            return TerrainSplines.GetTerrainShape(c, e);
        }

        /// <summary>
        /// Sample all climate channels AND compute terrain shape in one call.
        /// Avoids redundant continentalness/erosion noise sampling that occurs
        /// when calling SampleClimate() + GetTerrainShape() separately.
        /// Use this in hot paths that need both (e.g., GPU biome precomputation).
        /// </summary>
        public static void SampleClimateAndShape(float2 worldPosXZ,
            out ClimateParameters climate, out TerrainShapeData shape)
        {
            float temp = GetTemperature(worldPosXZ);
            float moist = GetMoisture(worldPosXZ);
            float cont = GetContinentalness(worldPosXZ);
            float ero = GetErosion(worldPosXZ);

            climate = new ClimateParameters
            {
                temperature     = temp,
                moisture        = moist,
                continentalness = cont,
                erosion         = ero,
                depth           = 0f
            };

            shape = TerrainSplines.GetTerrainShape(cont, ero);
        }

        /// <summary>
        /// Returns the BiomeTree instance. Build is lazy on first access.
        /// </summary>
        public static BiomeTree GetBiomeTree()
        {
            EnsureBiomes();
            return _biomeTree;
        }

        /// <summary>
        /// Returns the BiomeLookupTable instance. Build is lazy on first access.
        /// </summary>
        public static BiomeLookupTable GetLookupTable()
        {
            EnsureBiomes();
            return _lookupTable;
        }

        /// <summary>
        /// Returns the single best-matching BiomeDefinition at the given world XZ position.
        /// Uses the BiomeTree for nearest-neighbor lookup in 5D climate space.
        /// For underground positions, pass depth > 0 to enable cave biome selection.
        /// </summary>
        public static BiomeDefinition GetBiome(float2 worldPosXZ, float depth = 0f)
        {
            EnsureBiomes();

            ClimateParameters climate = SampleClimate(worldPosXZ);
            climate.depth = depth;

            byte biomeId = _biomeTree.FindClosest(climate);
            return _byteIdMap.TryGetValue(biomeId, out var def) ? def : _biomes[0];
        }

        /// <summary>
        /// Returns blended BiomeData (overhang parameters) for the density function.
        /// Uses nearest-neighbor distance to find top 2 biomes and blend overhang params.
        /// </summary>
        public static BiomeData GetBlendedBiomeData(float2 worldPosXZ)
        {
            EnsureBiomes();

            ClimateParameters climate = SampleClimate(worldPosXZ);

            // Find closest biome directly (no allocation)
            byte bestId = _biomeTree.FindClosest(climate);
            BiomeLookupTable.Entry e0 = _lookupTable.Get(bestId);

            return new BiomeData
            {
                overhangStrength  = e0.overhangStrength,
                overhangFrequency = e0.overhangFrequency,
                overhangMinY      = e0.overhangMinY
            };
        }

        private static void EnsureBiomes()
        {
            if (_biomes == null || _biomes.Length == 0)
            {
                _biomes = BiomeRegistry.GetDefaults();
                BuildLookupStructures();
            }
            else if (_biomeTree == null)
            {
                BuildLookupStructures();
            }
        }

        private static void BuildLookupStructures()
        {
            _biomeTree = new BiomeTree();
            _biomeTree.Build(_biomes);

            _lookupTable = new BiomeLookupTable();
            _lookupTable.Build(_biomes);

            _byteIdMap = new System.Collections.Generic.Dictionary<byte, BiomeDefinition>();
            foreach (var b in _biomes)
            {
                if (b != null)
                    _byteIdMap[b.biomeIdByte] = b;
            }
        }
    }
}
