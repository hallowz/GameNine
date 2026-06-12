using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.World.Chunks;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Blittable struct describing a single ore type's noise parameters.
    /// Contains no managed references — safe for use in Burst-compiled Jobs.
    /// </summary>
    public struct OreParams
    {
        public byte   oreTypeId;
        public float  minY;
        public float  maxY;
        public float  noiseFrequency;
        public float  noiseThreshold;
        public float3 seedOffset;
        public byte   requiredBiomeId; // 0 = any biome
    }

    /// <summary>
    /// Burst-compiled parallel job that evaluates ore noise for every voxel in a chunk.
    /// Each Execute(index) handles one flat voxel index (x + y*32 + z*32*32).
    ///
    /// Writing to OreField is safe here because each voxel index is processed by exactly
    /// one worker thread — IJobParallelFor guarantees non-overlapping index ranges, so
    /// we disable the parallel restriction check that Unity would otherwise enforce on a
    /// NativeArray written inside a parallel job.
    /// </summary>
    [BurstCompile]
    public struct OreGenerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float>     DensityField;
        [ReadOnly] public NativeArray<OreParams> OreParamsArray;
        public            int3                   ChunkWorldOrigin;

        [NativeDisableParallelForRestriction]
        public NativeArray<byte> OreField;

        public void Execute(int index)
        {
            // Reconstruct local voxel coordinates from flat index.
            int x = index % 32;
            int y = (index / 32) % 32;
            int z = index / (32 * 32);

            // Only place ore in solid voxels (positive density).
            if (DensityField[index] <= 0f) return;

            float worldX = ChunkWorldOrigin.x + x;
            float worldY = ChunkWorldOrigin.y + y;
            float worldZ = ChunkWorldOrigin.z + z;

            for (int i = 0; i < OreParamsArray.Length; i++)
            {
                // Lower-ID ores have priority — once claimed, skip.
                if (OreField[index] != 0) break;

                OreParams ore = OreParamsArray[i];

                if (worldY < ore.minY || worldY > ore.maxY) continue;

                float3 samplePos = new float3(worldX, worldY, worldZ) * ore.noiseFrequency + ore.seedOffset;
                float  noiseValue = noise.snoise(samplePos);

                // snoise returns [-1, 1]; remap to [0, 1] for threshold comparison.
                float remapped = (noiseValue + 1f) * 0.5f;

                if (remapped > ore.noiseThreshold)
                    OreField[index] = ore.oreTypeId;
            }
        }
    }

    /// <summary>
    /// Static class responsible for populating ChunkData.OreField with ore type IDs.
    /// Called once per chunk after density generation.
    ///
    /// Algorithm:
    ///   For every voxel that is solid (densityField > 0) and within the ore's Y range,
    ///   sample 3D Simplex noise seeded per ore type. If the noise value exceeds the
    ///   ore's noiseThreshold, mark that voxel with the ore's oreTypeId.
    ///   Lower oreTypeId values have priority (they are checked first and can be
    ///   overwritten by higher IDs only if the oreField byte is still 0 when checked).
    ///   Caller is responsible for sorting oreDefinitions by oreTypeId ascending if
    ///   strict priority is desired.
    /// </summary>
    public static class OreGenerator
    {
        private const int SIZE = ChunkData.SIZE; // 32

        // =========================================================================
        //  JOB SYSTEM PATH — preferred (off main thread)
        // =========================================================================

        /// <summary>
        /// Schedules a Burst-compiled parallel ore generation job. Returns a JobHandle the
        /// caller must eventually Complete(). The three NativeArrays passed via out parameters
        /// must be Disposed after the job completes (call after job.handle.Complete()).
        ///
        /// Returns JobHandle.default if oreDefinitions is null/empty.
        /// </summary>
        public static JobHandle ScheduleAsync(
            ChunkData        chunk,
            OreDefinition[]  oreDefinitions,
            out NativeArray<byte>      resultOreField,
            out NativeArray<float>     densityNative,
            out NativeArray<OreParams> oreParamsNative)
        {
            int volume = SIZE * SIZE * SIZE;

            densityNative   = new NativeArray<float>(chunk.densityField, Allocator.TempJob);
            resultOreField  = new NativeArray<byte>(volume, Allocator.TempJob);
            oreParamsNative = new NativeArray<OreParams>(oreDefinitions.Length, Allocator.TempJob);

            for (int i = 0; i < oreDefinitions.Length; i++)
            {
                OreDefinition ore = oreDefinitions[i];
                if (ore == null) continue;

                oreParamsNative[i] = new OreParams
                {
                    oreTypeId       = ore.oreTypeId,
                    minY            = ore.minY,
                    maxY            = ore.maxY,
                    noiseFrequency  = ore.noiseFrequency,
                    noiseThreshold  = ore.noiseThreshold,
                    seedOffset      = WorldSeed.SeedOffset3D(ore.oreTypeId),
                    requiredBiomeId = ore.requiredBiomeId
                };
            }

            var job = new OreGenerationJob
            {
                DensityField     = densityNative,
                OreField         = resultOreField,
                OreParamsArray   = oreParamsNative,
                ChunkWorldOrigin = new int3(
                    chunk.chunkPosition.x * SIZE,
                    chunk.chunkPosition.y * SIZE,
                    chunk.chunkPosition.z * SIZE)
            };

            // innerloopBatchCount of 64 is a good balance between job overhead and parallelism.
            return job.Schedule(volume, 64);
        }

        // =========================================================================
        //  SYNCHRONOUS FALLBACK — kept for non-runtime / editor use
        // =========================================================================

        /// <summary>
        /// Iterates all voxels in <paramref name="chunk"/> and writes ore type IDs into
        /// <c>chunk.OreField</c> based on each <see cref="OreDefinition"/>'s noise parameters.
        /// </summary>
        /// <param name="chunk">The chunk whose OreField will be populated.</param>
        /// <param name="oreDefinitions">Array of ore definitions to evaluate. Should be
        /// ordered by oreTypeId ascending so lower IDs have higher placement priority.</param>
        public static void GenerateOres(ChunkData chunk, OreDefinition[] oreDefinitions)
        {
            if (oreDefinitions == null || oreDefinitions.Length == 0)
                return;

            // Clear the ore field before writing
            chunk.OreField.Fill(0);

            float worldOriginX = chunk.chunkPosition.x * SIZE;
            float worldOriginY = chunk.chunkPosition.y * SIZE;
            float worldOriginZ = chunk.chunkPosition.z * SIZE;

            for (int z = 0; z < SIZE; z++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    float worldY = worldOriginY + y;

                    for (int x = 0; x < SIZE; x++)
                    {
                        int voxelIndex = VoxelIndex(x, y, z);

                        // Only place ore in solid voxels (positive density)
                        if (chunk.densityField[voxelIndex] <= 0f)
                            continue;

                        float worldX = worldOriginX + x;
                        float worldZ = worldOriginZ + z;

                        // Evaluate each ore definition. Lower oreTypeId has priority:
                        // once a voxel is claimed, higher-ID ores cannot overwrite it.
                        for (int i = 0; i < oreDefinitions.Length; i++)
                        {
                            OreDefinition ore = oreDefinitions[i];
                            if (ore == null) continue;

                            // Skip if voxel already claimed by a lower-ID ore
                            if (chunk.OreField.Get(voxelIndex) != 0) break;

                            // Y range check
                            if (worldY < ore.minY || worldY > ore.maxY) continue;

                            // Seeded noise sample per ore type
                            float3 seedOffset = WorldSeed.SeedOffset3D(ore.oreTypeId);
                            float3 samplePos = new float3(worldX, worldY, worldZ) * ore.noiseFrequency + seedOffset;

                            float noiseValue = noise.snoise(samplePos);

                            // snoise returns [-1, 1]; remap to [0, 1] for threshold comparison
                            float remapped = (noiseValue + 1f) * 0.5f;

                            if (remapped > ore.noiseThreshold)
                            {
                                chunk.OreField.Set(voxelIndex, ore.oreTypeId);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the flat index into a 32³ array for the given local voxel coordinates.
        /// Layout: x + y*32 + z*32*32
        /// </summary>
        public static int VoxelIndex(int x, int y, int z)
        {
            return x + y * SIZE + z * SIZE * SIZE;
        }

        public const byte SandOreId = 7;

        public const byte DirtOreId = 8;

        // =========================================================================
        //  TERRAIN TYPES — grass, dirt, rock layering based on surface distance
        // =========================================================================

        /// <summary>Default grass — used as fallback when no biome-specific grass is assigned.</summary>
        public const byte GrassOreId = 9;
        public const byte RockOreId  = 10;

        // Per-biome grass variants — each biome gets a unique ore ID with its own color.
        // This eliminates the dependency on biome vertex tinting for terrain coloring.
        public const byte GrassPlainsId   = 14; // Plains — light yellow-green (0.50, 0.75, 0.25)
        public const byte GrassForestId   = 15; // Dense Forest — deep green (0.10, 0.50, 0.10)
        public const byte GrassValleyId   = 16; // River Valley — medium green (0.30, 0.60, 0.30)
        public const byte GrassHighlandId = 17; // Highlands — muted olive (0.50, 0.55, 0.40)
        public const byte GrassCliffId    = 18; // Overhang Cliffs — stone grey (0.55, 0.50, 0.45)
        public const byte GrassTundraId   = 19; // Tundra — pale icy (0.85, 0.90, 0.95)
        public const byte GrassHillId     = 20; // Grand Hills — rich green (0.40, 0.65, 0.30)
        public const byte GrassCavernId   = 21; // Lush Caverns — cave moss (0.20, 0.60, 0.30)

        /// <summary>Maximum ore type ID in use. Used for array sizing.</summary>
        public const int MaxOreTypeId = 21;

        /// <summary>Returns true if the ore type ID is any grass variant (generic or per-biome).</summary>
        public static bool IsGrass(byte oreTypeId)
        {
            return oreTypeId == GrassOreId
                || (oreTypeId >= GrassPlainsId && oreTypeId <= GrassCavernId);
        }

        private const int GRASS_DEPTH = 2;  // top 2 solid voxels from surface
        private const int DIRT_LAYER  = 4;  // next 4 solid voxels below grass

        /// <summary>
        /// Fills all solid voxels with oreId==0 with terrain types:
        ///   - Grass (9) on the surface (top GRASS_DEPTH solid voxels)
        ///   - Dirt (8) just below grass (next DIRT_LAYER solid voxels)
        ///   - Rock (10) for everything else underground
        ///
        /// For sky island chunks (where gpuSurfaceHeights contains a sentinel value
        /// because the ground-level surface is far below), the method scans each column
        /// top-down to find the local surface from the density field directly.
        ///
        /// Call AFTER the noise-based ore generation job has completed and been
        /// copied back, but BEFORE PlaceRiverbedSand / PlaceRoadDirt (which may
        /// override grass/dirt near roads).
        /// </summary>
        public static void PlaceTerrainTypes(ChunkData chunk)
        {
            if (chunk.gpuSurfaceHeights == null)
                return;

            int originY = chunk.chunkPosition.y * SIZE;
            float[] surfaceHeights = chunk.gpuSurfaceHeights;

            for (int z = 0; z < SIZE; z++)
            for (int x = 0; x < SIZE; x++)
            {
                float surfH = surfaceHeights[x + z * SIZE];

                // Surface Y in local chunk coords
                int localSurf;

                // Sky/cave chunks: GPU wrote sentinel (-1e9) because the 2D ground surface
                // is far away. Scan the column to find the actual local surface instead.
                if (surfH < -1e8f || Mathf.Abs(surfH - originY) > SIZE * 4)
                {
                    localSurf = FindLocalSurface(chunk.densityField, x, z);
                }
                else
                {
                    localSurf = Mathf.FloorToInt(surfH - originY);
                }

                // Scan the column top-down, counting solid voxels from the surface
                for (int y = SIZE - 1; y >= 0; y--)
                {
                    int idx = VoxelIndex(x, y, z);

                    // Skip air voxels
                    if (chunk.densityField[idx] <= 0f)
                        continue;

                    // Skip voxels already claimed by actual ores
                    if (chunk.OreField.Get(idx) != 0)
                        continue;

                    // Distance below the surface (in voxels)
                    int depthBelowSurface = localSurf - y;

                    if (depthBelowSurface < GRASS_DEPTH)
                        chunk.OreField.Set(idx, GrassOreId);
                    else if (depthBelowSurface < GRASS_DEPTH + DIRT_LAYER)
                        chunk.OreField.Set(idx, DirtOreId);
                    else
                        chunk.OreField.Set(idx, RockOreId);
                }
            }
        }

        /// <summary>
        /// Scans a column top-down to find the highest solid voxel with air above it.
        /// Used for sky island chunks where the 2D ground surface is irrelevant.
        /// Returns the local Y of the surface, or -1 if the column is empty.
        /// </summary>
        private static int FindLocalSurface(float[] densityField, int x, int z)
        {
            bool foundAir = false;
            for (int y = SIZE - 1; y >= 0; y--)
            {
                int idx = VoxelIndex(x, y, z);
                if (densityField[idx] <= 0f)
                {
                    foundAir = true;
                }
                else if (foundAir)
                {
                    // First solid voxel below air = surface
                    return y;
                }
            }
            // Entire column is solid (no air gap found) — treat top as surface
            return SIZE - 1;
        }

        // =========================================================================
        //  DEPLETION — runtime ore extraction tracking
        // =========================================================================

        // Sparse set of voxels that have been depleted by auto-miners.
        // Key: packed world-voxel position (x<<20 | y<<10 | z, 10-bit each, offset +512 for negative coords).
        private static readonly HashSet<long> _depleted = new HashSet<long>();

        private static long PackVoxel(int wx, int wy, int wz)
            => ((long)(wx + 512) << 40) | ((long)(wy + 512) << 20) | (long)(wz + 512);

        /// <summary>
        /// Counts the number of non-depleted ore voxels of <paramref name="oreType"/> within
        /// <paramref name="radius"/> world units of <paramref name="worldPos"/>.
        /// Returns 0 if ChunkManager is not available.
        /// </summary>
        public static int CountOreInRadius(Vector3 worldPos, float radius, byte oreType)
        {
            if (ChunkManager.Instance == null) return 0;

            int r     = Mathf.CeilToInt(radius);
            int count = 0;
            float r2  = radius * radius;

            int wxMin = Mathf.FloorToInt(worldPos.x) - r;
            int wxMax = Mathf.CeilToInt(worldPos.x)  + r;
            int wyMin = Mathf.FloorToInt(worldPos.y) - r;
            int wyMax = Mathf.CeilToInt(worldPos.y)  + r;
            int wzMin = Mathf.FloorToInt(worldPos.z) - r;
            int wzMax = Mathf.CeilToInt(worldPos.z)  + r;

            for (int wx = wxMin; wx <= wxMax; wx++)
            for (int wy = wyMin; wy <= wyMax; wy++)
            for (int wz = wzMin; wz <= wzMax; wz++)
            {
                float dx = wx - worldPos.x, dy = wy - worldPos.y, dz = wz - worldPos.z;
                if (dx*dx + dy*dy + dz*dz > r2) continue;
                if (_depleted.Contains(PackVoxel(wx, wy, wz))) continue;

                // Look up the chunk.
                int cx = Mathf.FloorToInt(wx / (float)SIZE);
                int cy = Mathf.FloorToInt(wy / (float)SIZE);
                int cz = Mathf.FloorToInt(wz / (float)SIZE);
                ChunkData chunk = ChunkManager.Instance.GetChunk(new Vector3Int(cx, cy, cz));
                if (chunk == null) continue;

                int lx = wx - cx * SIZE;
                int ly = wy - cy * SIZE;
                int lz = wz - cz * SIZE;
                if (lx < 0 || ly < 0 || lz < 0 || lx >= SIZE || ly >= SIZE || lz >= SIZE) continue;

                int idx = VoxelIndex(lx, ly, lz);
                if (chunk.OreField.Get(idx) == oreType) count++;
            }
            return count;
        }

        /// <summary>
        /// Marks the ore voxel nearest to <paramref name="worldPos"/> as depleted and
        /// clears it in the chunk's OreField (sets to 0). Returns true if a voxel was
        /// found and depleted, false if no ore was present at that position.
        /// </summary>
        public static bool DepleteOre(Vector3 worldPos)
        {
            if (ChunkManager.Instance == null) return false;

            int wx = Mathf.RoundToInt(worldPos.x);
            int wy = Mathf.RoundToInt(worldPos.y);
            int wz = Mathf.RoundToInt(worldPos.z);

            int cx = Mathf.FloorToInt(wx / (float)SIZE);
            int cy = Mathf.FloorToInt(wy / (float)SIZE);
            int cz = Mathf.FloorToInt(wz / (float)SIZE);
            ChunkData chunk = ChunkManager.Instance.GetChunk(new Vector3Int(cx, cy, cz));
            if (chunk == null) return false;

            int lx = wx - cx * SIZE;
            int ly = wy - cy * SIZE;
            int lz = wz - cz * SIZE;
            if (lx < 0 || ly < 0 || lz < 0 || lx >= SIZE || ly >= SIZE || lz >= SIZE) return false;

            int idx = VoxelIndex(lx, ly, lz);
            if (chunk.OreField.Get(idx) == 0) return false;

            // RecordOreEdit so auto-miner depletion persists across save/load (P2.1)
            chunk.RecordOreEdit(idx, 0);
            _depleted.Add(PackVoxel(wx, wy, wz));
            return true;
        }
    }
}
