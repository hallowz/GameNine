using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Voidborne.World.Biomes;
using Voidborne.World.Generation;
using Buffer = System.Buffer;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Orchestrates chunk mesh generation through MarchingCubesAdapter.
    ///
    /// Terrain mesh is generated from solid density (GPU density shader → MC).
    /// Surface decoration points are computed on a background thread alongside biome colors.
    ///
    /// ASYNC PATH (GenerateChunkMeshAsync):
    ///   Submits GPU work without stalling the main thread.
    ///   Per-column biome data is precomputed on CPU and uploaded to the GPU.
    ///
    /// CPU FALLBACK (GenerateChunkMesh):
    ///   Used when GPU density path is unavailable.
    ///
    /// REBUILD PATH (RebuildChunkMesh):
    ///   Regenerates mesh from existing chunk.densityField after terrain deformation.
    /// </summary>
    public class ChunkMeshBuilder
    {
        private const int SIZE   = ChunkData.SIZE;  // 32
        private const int POINTS = SIZE + 1;         // 33
        private const int POINTS_VOLUME = POINTS * POINTS * POINTS; // 35937

        private const int TEXTURE_GROUPS = 4;

        private readonly MarchingCubesAdapter adapter;

        public ChunkMeshBuilder(MarchingCubesAdapter adapter)
        {
            this.adapter = adapter;
        }

        // =========================================================================
        //  ASYNC PATH — primary path for new chunk generation
        // =========================================================================

        public bool GenerateChunkMeshAsync(ChunkData chunk, Action onComplete)
        {
            if (!adapter.HasGPUDensity)
            {
                GenerateChunkMesh(chunk);
                onComplete?.Invoke();
                return true;
            }

            Vector3 worldOrigin = chunk.WorldPosition;

            chunk.state = ChunkState.Generating;

            bool started = adapter.GenerateAsync(chunk, worldOrigin, (mesh, density33, surfaceHeights33) =>
            {
                // 1. Populate chunk.densityField (32³) from the inner slice of the 33³ array.
                //    All LOD levels need density for ore generation (terrain coloring).
                if (density33 != null)
                {
                    for (int z = 0; z < SIZE; z++)
                        for (int y = 0; y < SIZE; y++)
                            Buffer.BlockCopy(density33,
                                (y * POINTS + z * POINTS * POINTS) * sizeof(float),
                                chunk.densityField,
                                (y * SIZE + z * SIZE * SIZE) * sizeof(float),
                                SIZE * sizeof(float));
                }

                // 2. Store GPU-computed surface heights — needed for terrain type
                //    coloring via VoxelClassificationJob (all LODs).
                if (surfaceHeights33 != null)
                {
                    // Reuse existing array if already allocated (LOD transitions)
                    float[] gpuHeights = chunk.gpuSurfaceHeights;
                    if (gpuHeights == null || gpuHeights.Length != SIZE * SIZE)
                        gpuHeights = new float[SIZE * SIZE];
                    for (int z = 0; z < SIZE; z++)
                        for (int x = 0; x < SIZE; x++)
                            gpuHeights[x + z * SIZE] = surfaceHeights33[x + z * POINTS];
                    chunk.gpuSurfaceHeights = gpuHeights;
                }

                // Return pooled readback arrays — data has been copied into chunk fields above.
                // These arrays were rented from ArrayPool in MarchingCubesAdapter.TryFinalize.
                if (density33 != null)       ArrayPool<float>.Shared.Return(density33);
                if (surfaceHeights33 != null) ArrayPool<float>.Shared.Return(surfaceHeights33);

                if (mesh == null)
                {
                    chunk.mesh         = null;
                    chunk.surfacePoints = null;
                    chunk.state        = ChunkState.MeshPending;
                    chunk.isDirty      = false;
                    onComplete?.Invoke();
                    return;
                }

                // 2. Schedule Burst ore UV2 job on main thread (non-blocking),
                //    then biome colors + surface points on thread pool.
                //    LOD chunks skip ore UV2 and surface points (no decorations/ore at distance).
                var capturedVerts      = mesh.vertices;
                chunk.cachedVertices   = capturedVerts; // cache for FinalizeOreChunk (avoids 2nd mesh.vertices copy)
                var capturedWorldOrigin = worldOrigin;
                var capturedOreField   = chunk.OreField?.ToArray();
                var capturedDensity    = chunk.densityField;
                var capturedLod        = chunk.lodLevel;
                var capturedHeights    = chunk.gpuSurfaceHeights;

                // Schedule Burst job on main thread — runs on worker threads in parallel
                OreUV2JobData? oreJob = (capturedLod == 0 && capturedOreField != null)
                    ? ScheduleOreUV2Job(capturedVerts, capturedOreField)
                    : (OreUV2JobData?)null;

                var capturedBiomeField = chunk.BiomeField?.ToArray();

                Task.Run(() =>
                {
                    ComputeBiomeData(capturedVerts, capturedWorldOrigin, capturedBiomeField, out Color[] texWeights, out Vector4[] tints);
                    float[] skyExp = ComputeSkyExposure(capturedVerts, capturedHeights, capturedDensity, capturedWorldOrigin);
                    SurfacePoint[] surfPts = capturedLod <= 3 ? ComputeSurfacePoints(capturedHeights, capturedWorldOrigin, capturedDensity, capturedOreField) : null;

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        if (mesh == null)
                        {
                            oreJob?.Complete(); // dispose NativeArrays even if mesh was destroyed
                            ArrayPool<Color>.Shared.Return(texWeights);
                            ArrayPool<Vector4>.Shared.Return(tints);
                            return;
                        }
                        int vc = mesh.vertexCount;
                        mesh.SetColors(texWeights, 0, vc);
                        mesh.SetUVs(3, tints, 0, vc);
                        ArrayPool<Color>.Shared.Return(texWeights);
                        ArrayPool<Vector4>.Shared.Return(tints);
                        if (oreJob.HasValue)
                        {
                            var oreUV2 = oreJob.Value.Complete();
                            mesh.SetUVs(1, oreUV2, 0, oreJob.Value.ResultCount);
                            ArrayPool<Vector2>.Shared.Return(oreUV2);
                        }
                        chunk.skyExposure   = skyExp;
                        chunk.mesh          = mesh;
                        chunk.surfacePoints = surfPts;
                        chunk.state         = ChunkState.MeshPending;
                        chunk.isDirty       = false;
                        onComplete?.Invoke();
                    });
                });
            });

            if (!started)
                chunk.state = ChunkState.Unloaded;

            return started;
        }

        // =========================================================================
        //  CPU FALLBACK
        // =========================================================================

        public void GenerateChunkMesh(ChunkData chunk)
        {
            FillDensityField(chunk);
            float[] density33 = BuildExpandedDensityField(chunk);

            chunk.state = ChunkState.Generating;
            Mesh mesh = adapter.GenerateMesh(density33);

            if (mesh != null)
            {
                byte[] biomeArr = chunk.BiomeField?.ToArray();
                byte[] oreArr   = chunk.OreField?.ToArray();
                var verts = mesh.vertices;
                ComputeBiomeData(verts, chunk.WorldPosition, biomeArr, out Color[] texWeights, out Vector4[] tints);
                Vector2[] uv2    = ComputeOreUV2(verts, oreArr);
                int vc = mesh.vertexCount;
                mesh.SetColors(texWeights, 0, vc);
                mesh.SetUVs(3, tints, 0, vc);
                ArrayPool<Color>.Shared.Return(texWeights);
                ArrayPool<Vector4>.Shared.Return(tints);
                mesh.uv2    = uv2;
                // CPU fallback: compute surface heights from density field if GPU heights aren't available
                float[] heights = chunk.gpuSurfaceHeights ?? ComputeSurfaceHeightsFromDensity(chunk.densityField, chunk.WorldPosition.y);
                chunk.skyExposure   = ComputeSkyExposure(verts, heights, chunk.densityField, chunk.WorldPosition);
                chunk.surfacePoints = ComputeSurfacePoints(heights, chunk.WorldPosition, chunk.densityField, oreArr);
            }

            chunk.mesh    = mesh;
            chunk.state   = ChunkState.MeshPending;
            chunk.isDirty = false;
        }

        // =========================================================================
        //  REBUILD PATH — after terrain deformation
        // =========================================================================

        public bool RebuildChunkMeshAsync(ChunkData chunk, Action onComplete)
        {
            float[] density33 = BuildExpandedDensityField(chunk);

            chunk.state = ChunkState.Generating;
            Mesh oldMesh = chunk.mesh;

            bool started = adapter.GenerateMeshAsync(density33, mesh =>
            {
                if (mesh == null)
                {
                    if (oldMesh != null) UnityEngine.Object.Destroy(oldMesh);
                    chunk.mesh         = null;
                    chunk.surfacePoints = null;
                    chunk.state        = ChunkState.MeshPending;
                    chunk.isDirty      = false;
                    onComplete?.Invoke();
                    return;
                }

                var capturedVerts    = mesh.vertices;
                var capturedWorldPos = chunk.WorldPosition;
                var capturedOreField = chunk.OreField?.ToArray();
                var capturedDensity  = chunk.densityField;
                // Rebuild path: density changed from deformation, recompute heights from current density
                var capturedHeights  = ComputeSurfaceHeightsFromDensity(capturedDensity, capturedWorldPos.y);

                // Schedule Burst job on main thread before going to thread pool
                OreUV2JobData? oreJob = capturedOreField != null
                    ? ScheduleOreUV2Job(capturedVerts, capturedOreField)
                    : (OreUV2JobData?)null;

                var capturedBiomeField = chunk.BiomeField?.ToArray();

                Task.Run(() =>
                {
                    ComputeBiomeData(capturedVerts, capturedWorldPos, capturedBiomeField, out Color[] texWeights, out Vector4[] tints);
                    float[] skyExp = ComputeSkyExposure(capturedVerts, capturedHeights, capturedDensity, capturedWorldPos);
                    SurfacePoint[] surfPts = ComputeSurfacePoints(capturedHeights, capturedWorldPos, capturedDensity, capturedOreField);

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        int vc = mesh.vertexCount;
                        mesh.SetColors(texWeights, 0, vc);
                        mesh.SetUVs(3, tints, 0, vc);
                        ArrayPool<Color>.Shared.Return(texWeights);
                        ArrayPool<Vector4>.Shared.Return(tints);
                        if (oreJob.HasValue)
                        {
                            var oreUV2 = oreJob.Value.Complete();
                            mesh.SetUVs(1, oreUV2, 0, oreJob.Value.ResultCount);
                            ArrayPool<Vector2>.Shared.Return(oreUV2);
                        }
                        chunk.skyExposure   = skyExp;
                        chunk.mesh          = mesh;
                        chunk.surfacePoints = surfPts;
                        chunk.state         = ChunkState.MeshPending;
                        chunk.isDirty       = false;
                        onComplete?.Invoke();
                        if (oldMesh != null) UnityEngine.Object.Destroy(oldMesh);
                    });
                });
            });

            if (!started)
                chunk.state = ChunkState.Active;

            return started;
        }

        public void RebuildChunkMesh(ChunkData chunk)
        {
            float[] density33 = BuildExpandedDensityField(chunk);

            chunk.state = ChunkState.Generating;

            if (chunk.mesh != null)
                UnityEngine.Object.Destroy(chunk.mesh);

            Mesh mesh = adapter.GenerateMesh(density33);

            if (mesh != null)
            {
                byte[] biomeArrR = chunk.BiomeField?.ToArray();
                byte[] oreArrR   = chunk.OreField?.ToArray();
                var vertsR = mesh.vertices;
                ComputeBiomeData(vertsR, chunk.WorldPosition, biomeArrR, out Color[] texWeights, out Vector4[] tints);
                Vector2[] uv2    = ComputeOreUV2(vertsR, oreArrR);
                mesh.colors = texWeights;
                mesh.SetUVs(3, tints);
                mesh.uv2    = uv2;
                // Rebuild: density changed from deformation, recompute heights from current density
                float[] heights = ComputeSurfaceHeightsFromDensity(chunk.densityField, chunk.WorldPosition.y);
                chunk.skyExposure   = ComputeSkyExposure(vertsR, heights, chunk.densityField, chunk.WorldPosition);
                chunk.surfacePoints = ComputeSurfacePoints(heights, chunk.WorldPosition, chunk.densityField, oreArrR);
            }

            chunk.mesh    = mesh;
            chunk.state   = ChunkState.MeshPending;
            chunk.isDirty = false;
        }

        // =========================================================================
        //  SURFACE POINT GENERATION (for decoration placement)
        // =========================================================================

        /// <summary>
        /// Scans terrain mesh vertices to find upward-facing surface points for
        /// decoration placement (trees, rocks, grass). Samples biome and road influence
        /// at each point.
        ///
        /// Uses a 16×16 grid: picks the highest upward-facing vertex in each cell.
        /// Thread-safe — pure noise functions, no Unity API calls.
        /// </summary>
        /// <summary>
        /// Computes surface points from gpuSurfaceHeights (density-derived, LOD-independent).
        /// Positions are deterministic: a fixed 16×16 grid sampling the 32×32 height map,
        /// so LOD0 and LOD1 produce identical tree/decoration positions.
        /// </summary>
        private static SurfacePoint[] ComputeSurfacePoints(
            float[] surfaceHeights, Vector3 chunkWorldPos,
            float[] densityField = null, byte[] oreField = null)
        {
            if (surfaceHeights == null || surfaceHeights.Length == 0)
                return Array.Empty<SurfacePoint>();

            const int GRID = 16;
            const float STEP = SIZE / (float)GRID;
            float originY = chunkWorldPos.y;

            var points = new List<SurfacePoint>(GRID * GRID);
            for (int gz = 0; gz < GRID; gz++)
            for (int gx = 0; gx < GRID; gx++)
            {
                // Grid cell center in local XZ
                float localX = gx * STEP + STEP * 0.5f;
                float localZ = gz * STEP + STEP * 0.5f;

                // Bilinearly interpolate height from the 32×32 height map at the cell center
                float fx = Mathf.Clamp(localX, 0f, SIZE - 1.001f);
                float fz = Mathf.Clamp(localZ, 0f, SIZE - 1.001f);
                int ix = (int)fx;
                int iz = (int)fz;
                int ix1 = Mathf.Min(ix + 1, SIZE - 1);
                int iz1 = Mathf.Min(iz + 1, SIZE - 1);
                float tx = fx - ix;
                float tz = fz - iz;

                float h00 = surfaceHeights[ix  + iz  * SIZE];
                float h10 = surfaceHeights[ix1 + iz  * SIZE];
                float h01 = surfaceHeights[ix  + iz1 * SIZE];
                float h11 = surfaceHeights[ix1 + iz1 * SIZE];

                // Determine local surface Y from GPU heights or density fallback
                float localY;
                if (h00 < -1e8f || h10 < -1e8f || h01 < -1e8f || h11 < -1e8f)
                {
                    // Sky/underground chunk: GPU heights are sentinel — scan density column
                    if (densityField == null) continue;
                    int cx = Mathf.Clamp(Mathf.RoundToInt(localX), 0, SIZE - 1);
                    int cz = Mathf.Clamp(Mathf.RoundToInt(localZ), 0, SIZE - 1);
                    localY = FindLocalSurfaceY(densityField, cx, cz);
                    if (localY < 0f) continue;
                }
                else
                {
                    float interpH = h00 * (1f - tx) * (1f - tz)
                                  + h10 * tx        * (1f - tz)
                                  + h01 * (1f - tx) * tz
                                  + h11 * tx        * tz;
                    localY = interpH - originY;
                    if (localY < 0f || localY >= SIZE) continue;
                }
                Vector3 localPos = new Vector3(localX, localY, localZ);
                Vector3 worldPos = chunkWorldPos + localPos;

                // Skip points with solid terrain above (caves, ravines, overhangs)
                if (densityField != null && HasSolidAbove(localPos, densityField))
                    continue;

                // Compute normal from density gradient at this surface point
                Vector3 normal = ComputeSurfaceNormal(localPos, densityField);
                if (normal.y < 0.5f) continue;

                // Sample ore type at this surface voxel
                byte ore = 0;
                if (oreField != null)
                {
                    int ox = Mathf.Clamp(Mathf.FloorToInt(localPos.x), 0, SIZE - 1);
                    int oy = Mathf.Clamp(Mathf.FloorToInt(localPos.y), 0, SIZE - 1);
                    int oz = Mathf.Clamp(Mathf.FloorToInt(localPos.z), 0, SIZE - 1);
                    ore = oreField[ox + oy * SIZE + oz * SIZE * SIZE];
                }

                float2 worldXZ = new float2(worldPos.x, worldPos.z);
                points.Add(new SurfacePoint
                {
                    worldPos      = worldPos,
                    normal        = normal,
                    biome         = BiomeMap.GetBiome(worldXZ),
                    roadInfluence = 0f,
                    underwater    = false,
                    oreType       = ore
                });
            }

            return points.ToArray();
        }

        /// <summary>
        /// CPU fallback: computes surface heights from the density field when GPU heights
        /// aren't available. Scans each column top-down to find the first solid→air transition.
        /// Returns heights in world Y, matching gpuSurfaceHeights format.
        /// </summary>
        private static float[] ComputeSurfaceHeightsFromDensity(float[] densityField, float originY)
        {
            if (densityField == null) return null;

            float[] heights = new float[SIZE * SIZE];
            for (int i = 0; i < heights.Length; i++) heights[i] = float.MinValue;

            for (int z = 0; z < SIZE; z++)
            for (int x = 0; x < SIZE; x++)
            {
                for (int y = SIZE - 1; y >= 1; y--)
                {
                    float dBelow = densityField[x + (y - 1) * SIZE + z * SIZE * SIZE];
                    float dHere  = densityField[x + y * SIZE + z * SIZE * SIZE];

                    // Find solid→air transition: density goes from positive (below) to <= 0 (here)
                    if (dBelow > 0f && dHere <= 0f)
                    {
                        // Interpolate the zero-crossing for sub-voxel precision
                        float t = dBelow / Mathf.Max(dBelow - dHere, 0.001f);
                        heights[x + z * SIZE] = originY + (y - 1) + t;
                        break;
                    }

                    // Top-of-chunk solid: no air above within this chunk
                    if (y == SIZE - 1 && dHere > 0f)
                    {
                        heights[x + z * SIZE] = originY + y;
                        break;
                    }
                }
            }

            return heights;
        }

        /// <summary>
        /// Computes an approximate surface normal from the density gradient at a local position.
        /// Uses central differences on the density field.
        /// </summary>
        private static Vector3 ComputeSurfaceNormal(Vector3 localPos, float[] densityField)
        {
            if (densityField == null)
                return Vector3.up;

            int x = Mathf.Clamp(Mathf.FloorToInt(localPos.x), 1, SIZE - 2);
            int y = Mathf.Clamp(Mathf.FloorToInt(localPos.y), 1, SIZE - 2);
            int z = Mathf.Clamp(Mathf.FloorToInt(localPos.z), 1, SIZE - 2);

            // Central differences — gradient of density field points toward solid
            float dx = densityField[(x + 1) + y * SIZE + z * SIZE * SIZE]
                     - densityField[(x - 1) + y * SIZE + z * SIZE * SIZE];
            float dy = densityField[x + (y + 1) * SIZE + z * SIZE * SIZE]
                     - densityField[x + (y - 1) * SIZE + z * SIZE * SIZE];
            float dz = densityField[x + y * SIZE + (z + 1) * SIZE * SIZE]
                     - densityField[x + y * SIZE + (z - 1) * SIZE * SIZE];

            // Normal points away from solid (negative gradient)
            Vector3 n = new Vector3(-dx, -dy, -dz);
            float mag = n.magnitude;
            return mag > 0.001f ? n / mag : Vector3.up;
        }

        /// <summary>
        /// Returns true if any solid voxel exists above the given local position
        /// within the same chunk. Used to reject cave/ravine surfaces.
        /// </summary>
        private static bool HasSolidAbove(Vector3 localPos, float[] densityField)
        {
            int lx = Mathf.Clamp(Mathf.FloorToInt(localPos.x), 0, SIZE - 1);
            int lz = Mathf.Clamp(Mathf.FloorToInt(localPos.z), 0, SIZE - 1);
            int startY = Mathf.Clamp(Mathf.CeilToInt(localPos.y) + 1, 0, SIZE - 1);

            for (int y = startY; y < SIZE; y++)
            {
                if (densityField[lx + y * SIZE + lz * SIZE * SIZE] > 0f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Scans a density column top-down to find the highest solid voxel with air above it.
        /// Returns the local Y (+ 0.5 for cell center) or -1 if no surface found.
        /// Used for sky/underground chunks where GPU surface heights are sentinel.
        /// </summary>
        private static float FindLocalSurfaceY(float[] densityField, int x, int z)
        {
            bool foundAir = false;
            for (int y = SIZE - 1; y >= 0; y--)
            {
                float d = densityField[x + y * SIZE + z * SIZE * SIZE];
                if (d <= 0f) { foundAir = true; continue; }
                if (foundAir) return y + 0.5f;
            }
            return -1f;
        }

        // =========================================================================
        //  SKY EXPOSURE (per-vertex)
        // =========================================================================

        /// <summary>
        /// Computes per-vertex sky exposure: 1 = open sky, 0 = underground.
        /// Uses gpuSurfaceHeights to determine whether each vertex is above or below
        /// the terrain surface.  A 3×3 column kernel provides smooth transitions at
        /// cave mouths.  For sky-zone chunks (sentinels) the density field is scanned
        /// for solid voxels above each vertex.
        /// Thread-safe — no Unity API calls.
        /// </summary>
        private static float[] ComputeSkyExposure(
            Vector3[] vertices, float[] surfaceHeights, float[] densityField,
            Vector3 chunkWorldPos)
        {
            if (vertices == null || vertices.Length == 0)
                return Array.Empty<float>();

            int vertCount = vertices.Length;
            float[] exposure = new float[vertCount];
            float originY = chunkWorldPos.y;

            // Determine default for sentinel heights based on chunk Y position.
            // Chunks in the sky zone default to exposed; underground chunks default to occluded.
            bool isSkyZone = originY >= DensityFunction.SkyStart;

            // Fast path: no surface height data at all (LOD3 or sky island chunks)
            if (surfaceHeights == null || surfaceHeights.Length == 0)
            {
                if (densityField != null)
                {
                    // Check density above each vertex within this chunk.
                    // Surface vertices with open sky above → exposed.
                    // Underground vertices with solid above → occluded.
                    for (int i = 0; i < vertCount; i++)
                    {
                        Vector3 v = vertices[i];
                        exposure[i] = HasSolidAboveLocal(v, densityField) ? 0f : 1f;
                    }
                }
                else
                {
                    // No data at all — assume exposed (safe default for distant LODs)
                    for (int i = 0; i < vertCount; i++)
                        exposure[i] = 1f;
                }
                return exposure;
            }

            // Normal path: compare vertex Y against surface height with 3×3 kernel
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 v = vertices[i];
                float worldY = originY + v.y;

                int cx = Mathf.Clamp(Mathf.FloorToInt(v.x), 0, SIZE - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(v.z), 0, SIZE - 1);

                int aboveCount = 0;
                int totalCount = 0;

                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = cx + dx;
                    int sz = cz + dz;
                    if (sx < 0 || sx >= SIZE || sz < 0 || sz >= SIZE)
                        continue;

                    float h = surfaceHeights[sx + sz * SIZE];
                    if (h < -1e8f)
                    {
                        // Sentinel column — sky-zone chunks count as exposed,
                        // underground chunks count as occluded.
                        totalCount++;
                        if (isSkyZone)
                        {
                            if (densityField == null || !HasSolidAboveLocal(v, densityField))
                                aboveCount++;
                        }
                        continue;
                    }

                    totalCount++;
                    // Small tolerance so vertices exactly on the surface read as exposed
                    if (worldY >= h - 0.5f)
                        aboveCount++;
                }

                exposure[i] = totalCount > 0 ? (float)aboveCount / totalCount : 1f;
            }

            return exposure;
        }

        /// <summary>
        /// Returns true if any solid voxel exists above the given local position
        /// within the same chunk.  Lightweight column scan for sky-exposure fallback.
        /// </summary>
        private static bool HasSolidAboveLocal(Vector3 localPos, float[] densityField)
        {
            int lx = Mathf.Clamp(Mathf.FloorToInt(localPos.x), 0, SIZE - 1);
            int lz = Mathf.Clamp(Mathf.FloorToInt(localPos.z), 0, SIZE - 1);
            int startY = Mathf.Clamp(Mathf.CeilToInt(localPos.y) + 1, 0, SIZE - 1);

            for (int y = startY; y < SIZE; y++)
            {
                if (densityField[lx + y * SIZE + lz * SIZE * SIZE] > 0f)
                    return true;
            }
            return false;
        }

        // =========================================================================
        //  DENSITY HELPERS
        // =========================================================================

        private void FillDensityField(ChunkData chunk)
        {
            Vector3 worldOrigin = chunk.WorldPosition;

            for (int z = 0; z < SIZE; z++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float worldX = worldOrigin.x + x;
                    float worldZ = worldOrigin.z + z;
                    float2 xz = new float2(worldX, worldZ);

                    // Sample climate once per column, derive both shape + biome data
                    TerrainShapeData shape = BiomeMap.GetTerrainShape(xz);
                    BiomeData biome = BiomeMap.GetBlendedBiomeData(xz);

                    for (int y = 0; y < SIZE; y++)
                    {
                        float3 worldPos = new float3(worldX, worldOrigin.y + y, worldZ);
                        chunk.SetDensity(x, y, z, DensityFunction.GetDensity(worldPos, shape, biome));
                    }
                }
            }
        }

        private float[] BuildExpandedDensityField(ChunkData chunk)
        {
            float[]      density33   = new float[POINTS_VOLUME];
            Vector3      worldOrigin = chunk.WorldPosition;
            ChunkManager chunkMgr    = ChunkManager.Instance;

            // Pass 1: Fast inner 32³ copy using BlockCopy (row-by-row, no per-voxel calls).
            for (int z = 0; z < SIZE; z++)
            for (int y = 0; y < SIZE; y++)
            {
                Buffer.BlockCopy(chunk.densityField,
                    (y * SIZE + z * SIZE * SIZE) * sizeof(float),
                    density33,
                    (y * POINTS + z * POINTS * POINTS) * sizeof(float),
                    SIZE * sizeof(float));
            }

            // Pass 2: Boundary shell only — voxels where x=32, y=32, or z=32.
            for (int z = 0; z < POINTS; z++)
            for (int y = 0; y < POINTS; y++)
            for (int x = 0; x < POINTS; x++)
            {
                if (x < SIZE && y < SIZE && z < SIZE) continue; // already copied

                int index = x + y * POINTS + z * POINTS * POINTS;
                density33[index] = GetBoundaryDensity(chunk, chunkMgr, x, y, z, worldOrigin);
            }

            return density33;
        }

        private float GetBoundaryDensity(ChunkData chunk, ChunkManager chunkManager,
                                         int x, int y, int z, Vector3 worldOrigin)
        {
            int chunkOffsetX = x >= SIZE ? 1 : 0;
            int chunkOffsetY = y >= SIZE ? 1 : 0;
            int chunkOffsetZ = z >= SIZE ? 1 : 0;

            int localX = x >= SIZE ? x - SIZE : x;
            int localY = y >= SIZE ? y - SIZE : y;
            int localZ = z >= SIZE ? z - SIZE : z;

            if (chunkManager != null)
            {
                Vector3Int neighborPos = chunk.chunkPosition + new Vector3Int(chunkOffsetX, chunkOffsetY, chunkOffsetZ);
                ChunkData  neighbor    = chunkManager.GetChunk(neighborPos);

                if (neighbor != null && neighbor.state != ChunkState.Unloaded)
                    return neighbor.GetDensity(localX, localY, localZ);
            }

            float3 worldPos = new float3(
                worldOrigin.x + x,
                worldOrigin.y + y,
                worldOrigin.z + z);

            return DensityFunction.GetDensity(worldPos);
        }

        // =========================================================================
        //  BIOME VERTEX COLORS
        // =========================================================================

        /// <summary>
        /// Computes per-vertex biome data for ALL biomes:
        ///   textureWeights (vertex colors): blend weights for the 4 texture groups (RGBA)
        ///   biomeTints (UV3): weighted-average of every biome's colorTint per vertex
        /// </summary>
        /// <summary>
        /// Cached biome result for a specific XZ column — avoids redundant noise
        /// lookups when multiple vertices share the same integer XZ position.
        /// </summary>
        private struct CachedBiomeColumn
        {
            public int   keyX, keyZ;
            public Color texWeights;
            public float tintR, tintG, tintB;
            public bool  valid;
        }

        internal static void ComputeBiomeData(Vector3[] vertices, Vector3 chunkWorldPos,
            byte[] biomeField, out Color[] textureWeights, out Vector4[] biomeTints)
        {
            if (vertices == null || vertices.Length == 0)
            {
                textureWeights = Array.Empty<Color>();
                biomeTints     = Array.Empty<Vector4>();
                return;
            }

            int vertCount = vertices.Length;
            textureWeights = ArrayPool<Color>.Shared.Rent(vertCount);
            biomeTints     = ArrayPool<Vector4>.Shared.Rent(vertCount);

            // Get the lookup table for biome visual properties
            BiomeLookupTable lookupTable = BiomeMap.GetLookupTable();
            BiomeLookupTable.Entry[] entries = lookupTable.GetEntries();

            // Cache results per integer XZ+Y cell to avoid redundant lookups
            const int CACHE_SIZE = 256;
            const int CACHE_MASK = CACHE_SIZE - 1;
            var cache = new CachedBiomeColumn[CACHE_SIZE];
            var neighbors = new byte[6]; // reused across iterations

            for (int i = 0; i < vertCount; i++)
            {
                // Map vertex to voxel coordinates within the 32³ grid
                int vx = Mathf.Clamp((int)Math.Floor(vertices[i].x), 0, SIZE - 1);
                int vy = Mathf.Clamp((int)Math.Floor(vertices[i].y), 0, SIZE - 1);
                int vz = Mathf.Clamp((int)Math.Floor(vertices[i].z), 0, SIZE - 1);

                // Check cache by XZ (biome is mostly 2D)
                int cacheIdx = ((vx * 73856093) ^ (vz * 19349663)) & CACHE_MASK;
                ref CachedBiomeColumn entry = ref cache[cacheIdx];

                if (entry.valid && entry.keyX == vx && entry.keyZ == vz)
                {
                    textureWeights[i] = entry.texWeights;
                    biomeTints[i]     = new Vector4(entry.tintR, entry.tintG, entry.tintB, 1f);
                    continue;
                }

                // Read biome ID from pre-computed BiomeField
                int voxelIdx = vx + vy * SIZE + vz * SIZE * SIZE;
                byte primaryBiome = (biomeField != null && voxelIdx < biomeField.Length)
                    ? biomeField[voxelIdx] : (byte)0;

                // Sample 6 face-adjacent voxels for edge blending
                int nc = 0;
                if (vx > 0)        neighbors[nc++] = biomeField[voxelIdx - 1];
                if (vx < SIZE - 1) neighbors[nc++] = biomeField[voxelIdx + 1];
                if (vy > 0)        neighbors[nc++] = biomeField[voxelIdx - SIZE];
                if (vy < SIZE - 1) neighbors[nc++] = biomeField[voxelIdx + SIZE];
                if (vz > 0)        neighbors[nc++] = biomeField[voxelIdx - SIZE * SIZE];
                if (vz < SIZE - 1) neighbors[nc++] = biomeField[voxelIdx + SIZE * SIZE];

                // Count how many neighbors share the primary biome
                int sameCount = 1; // the primary itself
                byte secondBiome = 0;
                int secondCount = 0;

                for (int n = 0; n < nc; n++)
                {
                    if (neighbors[n] == primaryBiome)
                    {
                        sameCount++;
                    }
                    else if (neighbors[n] == secondBiome)
                    {
                        secondCount++;
                    }
                    else if (secondCount == 0)
                    {
                        secondBiome = neighbors[n];
                        secondCount = 1;
                    }
                }

                // Compute blend weight
                float w0, w1;
                if (secondCount > 0 && secondBiome != 0)
                {
                    float total = sameCount + secondCount;
                    w0 = sameCount / total;
                    w1 = secondCount / total;
                }
                else
                {
                    w0 = 1f;
                    w1 = 0f;
                }

                // Look up visual properties
                BiomeLookupTable.Entry e0 = entries[primaryBiome];
                int grp0 = Mathf.Clamp(e0.textureGroup, 0, TEXTURE_GROUPS - 1);
                float g0 = 0f, g1 = 0f, g2 = 0f, g3 = 0f;
                if      (grp0 == 0) g0 += w0;
                else if (grp0 == 1) g1 += w0;
                else if (grp0 == 2) g2 += w0;
                else                g3 += w0;

                float tR = e0.colorR * w0;
                float tG = e0.colorG * w0;
                float tB = e0.colorB * w0;

                if (w1 > 0f)
                {
                    BiomeLookupTable.Entry e1 = entries[secondBiome];
                    int grp1 = Mathf.Clamp(e1.textureGroup, 0, TEXTURE_GROUPS - 1);
                    if      (grp1 == 0) g0 += w1;
                    else if (grp1 == 1) g1 += w1;
                    else if (grp1 == 2) g2 += w1;
                    else                g3 += w1;
                    tR += e1.colorR * w1;
                    tG += e1.colorG * w1;
                    tB += e1.colorB * w1;
                }

                Color texW = new Color(g0, g1, g2, g3);
                textureWeights[i] = texW;
                biomeTints[i]     = new Vector4(tR, tG, tB, 1f);

                // Store in cache
                entry.keyX       = vx;
                entry.keyZ       = vz;
                entry.texWeights = texW;
                entry.tintR      = tR;
                entry.tintG      = tG;
                entry.tintB      = tB;
                entry.valid      = true;
            }
        }

        // =========================================================================
        //  ORE UV2 DATA
        // =========================================================================

        /// <summary>
        /// Neighbor ore fields for cross-boundary ore UV2 sampling.
        /// Indices: 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z. Null entries are skipped.
        /// </summary>
        public struct NeighborOreFields
        {
            public byte[][] fields; // length 6, entries may be null
        }

        // =====================================================================
        //  Burst-compiled ore UV2 job
        // =====================================================================

        [BurstCompile]
        public struct OreUV2Job : IJobParallelFor
        {
            private const int S   = 32;        // ChunkData.SIZE
            private const int VOL = S * S * S;  // 32768

            [ReadOnly] public NativeArray<float3> Vertices;
            [ReadOnly] public NativeArray<byte>   OreField;       // length VOL
            [ReadOnly] public NativeArray<byte>   NeighborPacked; // length 6*VOL
            public            int                 NeighborMask;   // bit i = neighbor i valid

            [WriteOnly] public NativeArray<float2> UV2;

            byte SampleOre(int cx, int cy, int cz)
            {
                if (cx >= 0 && cx < S && cy >= 0 && cy < S && cz >= 0 && cz < S)
                    return OreField[cx + cy * S + cz * S * S];

                int lx = cx, ly = cy, lz = cz;
                int ni = -1;

                if      (cx >= S) { ni = 0; lx = cx - S; }
                else if (cx < 0)  { ni = 1; lx = cx + S; }

                if      (cy >= S) { ni = 2; ly = cy - S; }
                else if (cy < 0)  { ni = 3; ly = cy + S; }

                if      (cz >= S) { ni = 4; lz = cz - S; }
                else if (cz < 0)  { ni = 5; lz = cz + S; }

                if (ni < 0 || (NeighborMask & (1 << ni)) == 0)
                    return 0;

                if (lx < 0 || lx >= S || ly < 0 || ly >= S || lz < 0 || lz >= S)
                    return 0;

                return NeighborPacked[ni * VOL + lx + ly * S + lz * S * S];
            }

            public void Execute(int i)
            {
                float3 v = Vertices[i];
                int bx = (int)math.floor(v.x);
                int by = (int)math.floor(v.y);
                int bz = (int)math.floor(v.z);

                // Sample 8 corners of the voxel cell surrounding this vertex
                byte s0 = SampleOre(bx,     by,     bz    );
                byte s1 = SampleOre(bx + 1, by,     bz    );
                byte s2 = SampleOre(bx,     by + 1, bz    );
                byte s3 = SampleOre(bx + 1, by + 1, bz    );
                byte s4 = SampleOre(bx,     by,     bz + 1);
                byte s5 = SampleOre(bx + 1, by,     bz + 1);
                byte s6 = SampleOre(bx,     by + 1, bz + 1);
                byte s7 = SampleOre(bx + 1, by + 1, bz + 1);

                // Find dominant (mode) among non-zero samples.
                // Only 8 values — brute force counting is trivially fast in Burst.
                byte dominant      = 0;
                int  dominantCount = 0;
                byte candidate;

                candidate = s0;
                if (candidate != 0)
                {
                    int c = 1;
                    if (s1 == candidate) c++;
                    if (s2 == candidate) c++;
                    if (s3 == candidate) c++;
                    if (s4 == candidate) c++;
                    if (s5 == candidate) c++;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s1;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s2 == candidate) c++;
                    if (s3 == candidate) c++;
                    if (s4 == candidate) c++;
                    if (s5 == candidate) c++;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s2;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s3 == candidate) c++;
                    if (s4 == candidate) c++;
                    if (s5 == candidate) c++;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s3;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s4 == candidate) c++;
                    if (s5 == candidate) c++;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s4;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s5 == candidate) c++;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s5;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s6 == candidate) c++;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s6;
                if (candidate != 0 && candidate != dominant)
                {
                    int c = 1;
                    if (s7 == candidate) c++;
                    if (c > dominantCount) { dominantCount = c; dominant = candidate; }
                }

                candidate = s7;
                if (candidate != 0 && candidate != dominant)
                {
                    if (1 > dominantCount) { dominant = candidate; }
                }

                UV2[i] = dominant != 0
                    ? new float2(dominant / 32.0f, 1.0f)
                    : float2.zero;
            }
        }

        // =====================================================================
        //  Job scheduling helpers
        // =====================================================================

        /// <summary>
        /// Schedules the Burst ore UV2 job. Must be called on the main thread.
        /// Returns the job handle and NativeArrays (caller must Complete + Dispose).
        /// </summary>
        public static OreUV2JobData ScheduleOreUV2Job(List<Vector3> vertices, byte[] oreField,
            NeighborOreFields neighbors = default)
        {
            const int VOL = SIZE * SIZE * SIZE;
            int vertexCount = vertices.Count;

            var nativeVerts = new NativeArray<float3>(vertexCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 v = vertices[i];
                nativeVerts[i] = new float3(v.x, v.y, v.z);
            }

            return ScheduleOreUV2JobCore(nativeVerts, vertexCount, oreField, neighbors);
        }

        public static OreUV2JobData ScheduleOreUV2Job(Vector3[] vertices, byte[] oreField,
            NeighborOreFields neighbors = default)
        {
            int vertexCount = vertices.Length;

            var nativeVerts = new NativeArray<float3>(vertexCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 v = vertices[i];
                nativeVerts[i] = new float3(v.x, v.y, v.z);
            }

            return ScheduleOreUV2JobCore(nativeVerts, vertexCount, oreField, neighbors);
        }

        private static OreUV2JobData ScheduleOreUV2JobCore(NativeArray<float3> nativeVerts,
            int vertexCount, byte[] oreField, NeighborOreFields neighbors)
        {
            const int VOL = SIZE * SIZE * SIZE;

            var nativeOre = new NativeArray<byte>(VOL, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<byte>.Copy(oreField, nativeOre, VOL);

            var neighborPacked = new NativeArray<byte>(6 * VOL, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            int neighborMask = 0;
            if (neighbors.fields != null)
            {
                for (int n = 0; n < 6 && n < neighbors.fields.Length; n++)
                {
                    if (neighbors.fields[n] != null)
                    {
                        neighborMask |= 1 << n;
                        NativeArray<byte>.Copy(neighbors.fields[n], 0,
                            neighborPacked, n * VOL, VOL);
                    }
                }
            }

            var uv2 = new NativeArray<float2>(vertexCount, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);

            var job = new OreUV2Job
            {
                Vertices       = nativeVerts,
                OreField       = nativeOre,
                NeighborPacked = neighborPacked,
                NeighborMask   = neighborMask,
                UV2            = uv2
            };

            return new OreUV2JobData
            {
                Handle         = job.Schedule(vertexCount, 64),
                UV2            = uv2,
                Verts          = nativeVerts,
                Ore            = nativeOre,
                NeighborPacked = neighborPacked
            };
        }

        /// <summary>
        /// Holds all NativeArrays for an in-flight OreUV2Job so they can be
        /// completed and disposed together.
        /// </summary>
        public struct OreUV2JobData
        {
            public JobHandle           Handle;
            public NativeArray<float2> UV2;
            public NativeArray<float3> Verts;
            public NativeArray<byte>   Ore;
            public NativeArray<byte>   NeighborPacked;

            /// <summary>Number of valid elements in the result array (pooled arrays may be larger).</summary>
            public int ResultCount { get; private set; }

            public Vector2[] Complete()
            {
                Handle.Complete();

                int len = UV2.Length;
                ResultCount = len;
                var result = ArrayPool<Vector2>.Shared.Rent(len);
                for (int i = 0; i < len; i++)
                {
                    float2 v = UV2[i];
                    result[i] = new Vector2(v.x, v.y);
                }

                UV2.Dispose();
                Verts.Dispose();
                Ore.Dispose();
                NeighborPacked.Dispose();

                return result;
            }
        }

        /// <summary>
        /// Synchronous convenience: schedules and immediately completes the Burst ore UV2 job.
        /// Must be called on the main thread.
        /// </summary>
        public static Vector2[] ComputeOreUV2(Vector3[] vertices, byte[] oreField,
            NeighborOreFields neighbors = default)
        {
            if (vertices == null || vertices.Length == 0)
                return Array.Empty<Vector2>();

            if (oreField == null)
                return new Vector2[vertices.Length];

            return ScheduleOreUV2Job(vertices, oreField, neighbors).Complete();
        }

        /// <summary>
        /// Overload that accepts a List&lt;Vector3&gt; to avoid the mesh.vertices copy allocation.
        /// </summary>
        public static Vector2[] ComputeOreUV2(List<Vector3> vertices, byte[] oreField,
            NeighborOreFields neighbors = default)
        {
            if (vertices == null || vertices.Count == 0)
                return Array.Empty<Vector2>();

            if (oreField == null)
                return new Vector2[vertices.Count];

            return ScheduleOreUV2Job(vertices, oreField, neighbors).Complete();
        }

        // ComputeBiomeWeight removed — biome colors now read directly from BiomeField
    }
}
