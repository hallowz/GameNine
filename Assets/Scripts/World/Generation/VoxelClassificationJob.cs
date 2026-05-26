using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Burst-compiled parallel job that assigns biome ID, ore type, and terrain type
    /// for every voxel in a chunk. Replaces the old separate OreGenerationJob +
    /// PlaceTerrainTypes passes with a single unified pass.
    ///
    /// Runs after density generation and surface height readback. Each voxel gets:
    ///   - BiomeField: biome ID from BiomeTree nearest-neighbor lookup
    ///   - OreField: ore type from noise (biome-aware) or terrain type (grass/dirt/rock)
    /// </summary>
    [BurstCompile]
    public struct VoxelClassificationJob : IJobParallelFor
    {
        private const int SIZE = 32;

        [ReadOnly] public NativeArray<float>     DensityField;      // 32^3
        [ReadOnly] public NativeArray<float>     SurfaceHeights;    // 32x32
        [ReadOnly] public NativeArray<ClimateParameters> ClimateColumns; // 32x32
        [ReadOnly] public NativeArray<OreParams> OreParamsArray;

        // BiomeTree flat arrays
        [ReadOnly] public NativeArray<float> TreeSplitValues;
        [ReadOnly] public NativeArray<int>   TreeSplitAxes;
        [ReadOnly] public NativeArray<byte>  TreeBiomeIds;
        [ReadOnly] public NativeArray<int>   TreeLeftChild;
        [ReadOnly] public NativeArray<int>   TreeRightChild;
        [ReadOnly] public NativeArray<float> TreePoints; // flattened 5D
        public int TreeNodeCount;

        // BiomeLookupTable entries (flat)
        [ReadOnly] public NativeArray<BiomeLookupTable.Entry> LookupEntries;

        public int3 ChunkWorldOrigin;

        // Depth normalization scale (voxels of surface distance → normalized depth)
        public float DepthScale;

        // Road parameters for dirt override
        public float RoadCellSize;
        public float RoadHalfWidth;
        public float RoadSeedOffset;

        [NativeDisableParallelForRestriction]
        public NativeArray<byte> BiomeField;  // 32^3 output

        [NativeDisableParallelForRestriction]
        public NativeArray<byte> OreField;    // 32^3 output

        public void Execute(int index)
        {
            int x = index % SIZE;
            int y = (index / SIZE) % SIZE;
            int z = index / (SIZE * SIZE);

            // --- Step 1: Biome assignment ---
            ClimateParameters climate = ClimateColumns[x + z * SIZE];

            float worldY = ChunkWorldOrigin.y + y;
            float surfH = SurfaceHeights[x + z * SIZE];

            // Compute depth below surface
            float rawDepth = surfH - worldY;
            if (rawDepth < 0f) rawDepth = 0f;
            climate.depth = rawDepth / DepthScale;

            byte biomeId = FindClosestBiome(climate);
            BiomeField[index] = biomeId;

            // --- Step 2: Ore / terrain type ---
            if (DensityField[index] <= 0f)
            {
                OreField[index] = 0; // air
                return;
            }

            float worldX = ChunkWorldOrigin.x + x;
            float worldZ = ChunkWorldOrigin.z + z;

            // Road surface override: force dirt for top 3 voxels on roads
            if (RoadCellSize > 0f)
            {
                float depthBelow = surfH - worldY;
                if (depthBelow >= 0f && depthBelow < 3f)
                {
                    float roadInf = NoiseUtilities.RoadInfluence(
                        new float2(worldX, worldZ), RoadCellSize, RoadHalfWidth, RoadSeedOffset);
                    if (roadInf > 0.3f)
                    {
                        OreField[index] = 8; // DirtOreId
                        return;
                    }
                }
            }

            // Try ore placement (same logic as old OreGenerationJob)

            for (int i = 0; i < OreParamsArray.Length; i++)
            {
                if (OreField[index] != 0) break;

                OreParams ore = OreParamsArray[i];

                if (worldY < ore.minY || worldY > ore.maxY) continue;

                // Biome restriction check
                if (ore.requiredBiomeId != 0 && ore.requiredBiomeId != biomeId) continue;

                float3 samplePos = new float3(worldX, worldY, worldZ) * ore.noiseFrequency + ore.seedOffset;
                float noiseValue = noise.snoise(samplePos);
                float remapped = (noiseValue + 1f) * 0.5f;

                if (remapped > ore.noiseThreshold)
                    OreField[index] = ore.oreTypeId;
            }

            // If no ore claimed, assign terrain type based on surface distance
            if (OreField[index] == 0)
            {
                BiomeLookupTable.Entry entry = LookupEntries[biomeId];

                // Find local surface for this column
                int localSurf;
                if (surfH < -1e8f)
                {
                    // Sentinel value: scan column for local surface
                    localSurf = FindLocalSurface(x, z);
                }
                else
                {
                    localSurf = (int)math.floor(surfH - ChunkWorldOrigin.y);
                }

                int depthBelowSurface = localSurf - y;

                if (depthBelowSurface < entry.surfaceTopDepth)
                    OreField[index] = entry.surfaceTopId;
                else if (depthBelowSurface < entry.surfaceTopDepth + entry.surfaceSubDepth)
                    OreField[index] = entry.surfaceSubId;
                else
                    OreField[index] = OreGenerator.RockOreId;
            }
        }

        private int FindLocalSurface(int x, int z)
        {
            bool foundAir = false;
            for (int y = SIZE - 1; y >= 0; y--)
            {
                int idx = x + y * SIZE + z * SIZE * SIZE;
                if (DensityField[idx] <= 0f)
                {
                    foundAir = true;
                }
                else if (foundAir)
                {
                    return y;
                }
            }
            return SIZE - 1;
        }

        private byte FindClosestBiome(ClimateParameters climate)
        {
            if (TreeNodeCount == 0) return 0;

            float qt = climate.temperature;
            float qm = climate.moisture;
            float qc = climate.continentalness;
            float qe = climate.erosion;
            float qd = climate.depth;

            float bestDist = float.MaxValue;
            byte bestId = 0;

            // Iterative stack-based k-d tree traversal
            // Max depth for ~30 biomes is ~5, so stack of 16 is plenty
            int stackTop = 0;
            int s0 = 0, s1 = -1, s2 = -1, s3 = -1, s4 = -1, s5 = -1, s6 = -1, s7 = -1;
            int s8 = -1, s9 = -1, s10 = -1, s11 = -1, s12 = -1, s13 = -1, s14 = -1, s15 = -1;
            stackTop = 1; // s0 = 0 (root)

            while (stackTop > 0)
            {
                stackTop--;
                int idx;
                switch (stackTop)
                {
                    case 0: idx = s0; break; case 1: idx = s1; break;
                    case 2: idx = s2; break; case 3: idx = s3; break;
                    case 4: idx = s4; break; case 5: idx = s5; break;
                    case 6: idx = s6; break; case 7: idx = s7; break;
                    case 8: idx = s8; break; case 9: idx = s9; break;
                    case 10: idx = s10; break; case 11: idx = s11; break;
                    case 12: idx = s12; break; case 13: idx = s13; break;
                    case 14: idx = s14; break; default: idx = s15; break;
                }

                if (idx < 0 || idx >= TreeNodeCount) continue;

                int pBase = idx * 5;
                float d0 = qt - TreePoints[pBase];
                float d1 = qm - TreePoints[pBase + 1];
                float d2 = qc - TreePoints[pBase + 2];
                float d3 = qe - TreePoints[pBase + 3];
                float d4 = qd - TreePoints[pBase + 4];
                float dist = d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = TreeBiomeIds[idx];
                }

                int axis = TreeSplitAxes[idx];
                float queryVal;
                switch (axis)
                {
                    case 0: queryVal = qt; break;
                    case 1: queryVal = qm; break;
                    case 2: queryVal = qc; break;
                    case 3: queryVal = qe; break;
                    default: queryVal = qd; break;
                }

                float diff = queryVal - TreeSplitValues[idx];
                int first  = diff < 0 ? TreeLeftChild[idx] : TreeRightChild[idx];
                int second = diff < 0 ? TreeRightChild[idx] : TreeLeftChild[idx];

                // Push second (farther) branch
                if (second >= 0 && diff * diff < bestDist && stackTop < 16)
                {
                    switch (stackTop)
                    {
                        case 0: s0 = second; break; case 1: s1 = second; break;
                        case 2: s2 = second; break; case 3: s3 = second; break;
                        case 4: s4 = second; break; case 5: s5 = second; break;
                        case 6: s6 = second; break; case 7: s7 = second; break;
                        case 8: s8 = second; break; case 9: s9 = second; break;
                        case 10: s10 = second; break; case 11: s11 = second; break;
                        case 12: s12 = second; break; case 13: s13 = second; break;
                        case 14: s14 = second; break; default: s15 = second; break;
                    }
                    stackTop++;
                }

                // Push first (closer) branch
                if (first >= 0 && stackTop < 16)
                {
                    switch (stackTop)
                    {
                        case 0: s0 = first; break; case 1: s1 = first; break;
                        case 2: s2 = first; break; case 3: s3 = first; break;
                        case 4: s4 = first; break; case 5: s5 = first; break;
                        case 6: s6 = first; break; case 7: s7 = first; break;
                        case 8: s8 = first; break; case 9: s9 = first; break;
                        case 10: s10 = first; break; case 11: s11 = first; break;
                        case 12: s12 = first; break; case 13: s13 = first; break;
                        case 14: s14 = first; break; default: s15 = first; break;
                    }
                    stackTop++;
                }
            }

            return bestId;
        }
    }

    /// <summary>
    /// Burst-compiled job that precomputes climate parameters for each XZ column.
    /// Runs before VoxelClassificationJob to avoid redundant noise sampling.
    /// </summary>
    [BurstCompile]
    public struct ClimateColumnJob : IJobParallelFor
    {
        private const int SIZE = 32;

        public int2  ChunkWorldOriginXZ;

        // Noise seed offsets (precomputed on main thread)
        public float TemperatureSeedOffset;
        public float MoistureSeedOffset;
        public float ContinentalnessSeedOffset;
        public float ErosionSeedOffset;

        [WriteOnly] public NativeArray<ClimateParameters> ClimateColumns; // 32x32

        public void Execute(int index)
        {
            int x = index % SIZE;
            int z = index / SIZE;

            float worldX = ChunkWorldOriginXZ.x + x;
            float worldZ = ChunkWorldOriginXZ.y + z;
            float2 worldXZ = new float2(worldX, worldZ);

            // Climate frequency constants (matching BiomeMap)
            const float climateFreq    = 0.0008f;
            const float continentFreq  = 0.0006f;
            const float erosionFreq    = 0.0007f;
            const int   octaves        = 4;
            const float persistence    = 0.5f;
            const float lacunarity     = 2f;

            float rawTemp = NoiseUtilities.Noise2D(worldXZ, climateFreq,
                octaves, persistence, lacunarity, TemperatureSeedOffset);
            float rawMoist = NoiseUtilities.Noise2D(worldXZ, climateFreq * 1.3f,
                octaves, persistence, lacunarity, MoistureSeedOffset);
            float rawCont = NoiseUtilities.Noise2D(worldXZ, continentFreq,
                octaves, persistence, lacunarity, ContinentalnessSeedOffset);
            float rawEro = NoiseUtilities.Noise2D(worldXZ, erosionFreq,
                octaves, persistence, lacunarity, ErosionSeedOffset);

            ClimateColumns[index] = new ClimateParameters
            {
                temperature     = math.saturate(rawTemp  * 0.5f + 0.5f),
                moisture        = math.saturate(rawMoist * 0.5f + 0.5f),
                continentalness = math.saturate(rawCont  * 0.5f + 0.5f),
                erosion         = math.saturate(rawEro   * 0.5f + 0.5f),
                depth           = 0f // filled per-voxel by VoxelClassificationJob
            };
        }
    }
}
