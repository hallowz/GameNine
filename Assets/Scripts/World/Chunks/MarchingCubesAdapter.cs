using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Voidborne.World.Biomes;
using Voidborne.World.Generation;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Wraps the external marching-cubes compute shader.
    ///
    /// ASYNC PATH (new chunk generation):
    ///   GenerateAsync() submits DensityGeneration.compute + MarchingCubes.compute to the
    ///   GPU command queue, then uses AsyncGPUReadback for density + triangle data.
    ///   Zero main-thread stalls. Up to SLOT_COUNT chunks in-flight simultaneously.
    ///
    ///   GPU command-queue ordering per chunk (FIFO — safe to share density buffer/texture):
    ///     [density dispatch] → [density readback] → [MC dispatch] → [triCount readback] → [triangle readback]
    ///   The density readback is inserted BEFORE the next chunk's density dispatch, so each
    ///   callback receives that chunk's density, not a later chunk's.
    ///
    /// SYNC PATH (terrain deformation):
    ///   GenerateMesh(float[]) uploads existing density, runs MC synchronously.
    ///   Uses separate buffers so it never conflicts with in-flight async operations.
    /// </summary>
    public class MarchingCubesAdapter
    {
        private const int VOXELS_PER_AXIS = ChunkData.SIZE;                                      // 32
        private const int POINTS_PER_AXIS = VOXELS_PER_AXIS + 1;                                 // 33
        private const int POINTS_VOLUME   = POINTS_PER_AXIS * POINTS_PER_AXIS * POINTS_PER_AXIS; // 35937
        private const int MAX_TRIANGLES   = VOXELS_PER_AXIS * VOXELS_PER_AXIS * VOXELS_PER_AXIS * 5;

        private const int   DENSITY_DISPATCH     = 9;  // 9 × 4 threads = 36 ≥ 33 per axis
        private const int   MC_DISPATCH          = VOXELS_PER_AXIS / 8; // 4 groups × 8 = 32 voxels
        private const float SCALE                = 1f;
        private const float SURFACE_LEVEL        = 0f;
        private const float DOMAIN_WARP_STRENGTH = 12f;
        private const int   SLOT_COUNT           = 12;
        private const int   TRIANGLE_STRIDE      = sizeof(float) * 18; // 6 × float3 = 72 bytes

        // Triangle layout matches GPU: struct Triangle { float3 nC, vC, nB, vB, nA, vA; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Triangle
        {
            public Vector3 normalC, vertexC, normalB, vertexB, normalA, vertexA;
            public Vector3 this[int i] => i switch { 0 => vertexC, 1 => vertexB, _ => vertexA };
            public Vector3 GetNormal(int i) => i switch { 0 => normalC, 1 => normalB, _ => normalA };
        }

        // -------------------------------------------------------------------------
        //  Per-slot state for concurrent async generation
        // -------------------------------------------------------------------------
        private class GenerationSlot
        {
            public readonly ComputeBuffer triangleBuffer;
            public readonly ComputeBuffer triCountBuffer;

            // Per-slot GPU resources — each slot has its own density pipeline so
            // multiple chunks can generate truly in parallel on the GPU.
            public readonly RenderTexture densityTexture;
            public readonly ComputeBuffer densityBuffer;
            public readonly ComputeBuffer biomeBuffer;
            public readonly ComputeBuffer surfaceHeightBuffer;
            // Readback results
            public float[]    densityData;
            public float[]    surfaceHeightData;
            public Triangle[] triangleData;

            // Completion flags (set independently as each readback finishes)
            public bool densityReady;
            public bool surfaceHeightsReady;
            public bool trianglesReady;

            // Request context
            public bool                           inFlight;
            public bool                           cancelled;
            public int                            generationId; // incremented on each new use; stale callbacks check this
            public ChunkData                      pendingChunk;
            public Action<Mesh, float[], float[]> onComplete;  // (mesh, density33, surfaceHeights33)

            public bool IsComplete => densityReady && surfaceHeightsReady && trianglesReady;

            public GenerationSlot()
            {
                triangleBuffer = new ComputeBuffer(MAX_TRIANGLES, TRIANGLE_STRIDE, ComputeBufferType.Append);
                triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

                densityTexture = new RenderTexture(POINTS_PER_AXIS, POINTS_PER_AXIS, 0)
                {
                    format            = RenderTextureFormat.RFloat,
                    dimension         = TextureDimension.Tex3D,
                    volumeDepth       = POINTS_PER_AXIS,
                    enableRandomWrite = true,
                    filterMode        = FilterMode.Point,
                    wrapMode          = TextureWrapMode.Clamp,
                };
                densityTexture.Create();

                densityBuffer       = new ComputeBuffer(POINTS_VOLUME, sizeof(float));
                biomeBuffer         = new ComputeBuffer(POINTS_PER_AXIS * POINTS_PER_AXIS,
                    Marshal.SizeOf<GpuBiomeParams>());
                surfaceHeightBuffer = new ComputeBuffer(POINTS_PER_AXIS * POINTS_PER_AXIS, sizeof(float));
            }

            public void Reset()
            {
                inFlight            = false;
                cancelled           = false;
                // generationId is NOT reset — it only increments so stale callbacks can detect reuse
                densityData         = null;
                surfaceHeightData   = null;
                triangleData        = null;
                densityReady        = false;
                surfaceHeightsReady = false;
                trianglesReady      = false;
                pendingChunk        = null;
                onComplete          = null;
            }

            public void Dispose()
            {
                triangleBuffer?.Release();
                triCountBuffer?.Release();
                densityTexture?.Release();
                densityBuffer?.Release();
                biomeBuffer?.Release();
                surfaceHeightBuffer?.Release();
            }
        }

        // -------------------------------------------------------------------------
        //  Compute shaders and GPU resources
        // -------------------------------------------------------------------------
        private ComputeShader marchCompute;
        private ComputeShader densityCompute;

        // GPU-side biome parameter struct (must match HLSL BiomeParams)
        [StructLayout(LayoutKind.Sequential)]
        private struct GpuBiomeParams
        {
            public float heightScale;
            public float heightFrequency;
            public float caveScale;
            public float caveDensity;
            public float noiseOctaves; // int on CPU, float on GPU for buffer alignment
            public float persistence;
            public float lacunarity;
            public float moisture;
        }

        // Async generation slots
        private readonly GenerationSlot[] asyncSlots = new GenerationSlot[SLOT_COUNT];

        // -------------------------------------------------------------------------
        //  Per-slot state for concurrent async rebuild (terrain deformation)
        // -------------------------------------------------------------------------
        private class RebuildSlot
        {
            public readonly ComputeBuffer triangleBuffer;
            public readonly ComputeBuffer triCountBuffer;
            public readonly ComputeBuffer densityBuffer;
            public readonly RenderTexture densityTexture;
            public bool inFlight;

            public RebuildSlot()
            {
                triangleBuffer = new ComputeBuffer(MAX_TRIANGLES, TRIANGLE_STRIDE, ComputeBufferType.Append);
                triCountBuffer  = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                densityBuffer   = new ComputeBuffer(POINTS_VOLUME, sizeof(float));
                densityTexture  = new RenderTexture(POINTS_PER_AXIS, POINTS_PER_AXIS, 0)
                {
                    format            = RenderTextureFormat.RFloat,
                    dimension         = TextureDimension.Tex3D,
                    volumeDepth       = POINTS_PER_AXIS,
                    enableRandomWrite = true,
                    filterMode        = FilterMode.Point,
                    wrapMode          = TextureWrapMode.Clamp,
                };
                densityTexture.Create();
            }

            public void Dispose()
            {
                triangleBuffer?.Release();
                triCountBuffer?.Release();
                densityBuffer?.Release();
                densityTexture?.Release();
            }
        }

        private const int REBUILD_SLOT_COUNT = 4;
        private readonly RebuildSlot[] rebuildSlots = new RebuildSlot[REBUILD_SLOT_COUNT];

        private bool initialized;

        // Cached kernel indices — avoid per-dispatch string lookups
        private int _densityKernel = -1;
        private int _copyKernel    = -1;

        // -------------------------------------------------------------------------
        //  Initialisation
        // -------------------------------------------------------------------------
        public MarchingCubesAdapter()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            marchCompute = Resources.Load<ComputeShader>("MarchingCubes");
            if (marchCompute == null)
            {
                Debug.LogError("[MarchingCubesAdapter] Failed to load MarchingCubes compute shader.");
                return;
            }

            densityCompute = Resources.Load<ComputeShader>("DensityGeneration");
            if (densityCompute == null)
                Debug.LogWarning("[MarchingCubesAdapter] DensityGeneration not found — GPU async path unavailable.");

            // Cache kernel indices once at init
            if (densityCompute != null)
            {
                _densityKernel = densityCompute.FindKernel("GenerateDensity");
                _copyKernel    = densityCompute.FindKernel("CopyBufferToTexture");
            }

            // Async generation slots — each has its own density texture/buffers
            // so multiple chunks can generate truly in parallel on the GPU.
            for (int i = 0; i < SLOT_COUNT; i++)
                asyncSlots[i] = new GenerationSlot();

            // Rebuild slots — each has its own dedicated buffers so multiple rebuilds can
            // run in parallel without conflicting with each other or the async new-chunk path.
            for (int i = 0; i < REBUILD_SLOT_COUNT; i++)
                rebuildSlots[i] = new RebuildSlot();

            // Static MC shader params
            marchCompute.SetInt  ("pointsPerAxis", POINTS_PER_AXIS);
            marchCompute.SetFloat("scale",         SCALE);
            marchCompute.SetFloat("surfaceLevel",  SURFACE_LEVEL);
            marchCompute.SetBool ("smooth",        true);
            marchCompute.SetBool ("invert",        false);
            marchCompute.SetInt  ("lodModifier",   1);

            initialized = true;
        }

        public bool HasGPUDensity     => densityCompute != null && initialized;
        public bool IsRebuildInFlight { get { foreach (var s in rebuildSlots) if (s.inFlight) return true; return false; } }

        /// <summary>Total number of async generation slots.</summary>
        public int TotalSlotCount => SLOT_COUNT;

        /// <summary>Number of async generation slots not currently in-flight.</summary>
        public int FreeSlotCount
        {
            get { int n = 0; foreach (var s in asyncSlots) if (!s.inFlight) n++; return n; }
        }

        /// <summary>
        /// Cancel in-flight generation for a specific chunk. The GPU work can't be
        /// stopped, but when the readback completes TryFinalize will skip the expensive
        /// mesh building and free the slot immediately.
        /// Returns true if a matching in-flight slot was found and cancelled.
        /// </summary>
        public bool CancelGeneration(ChunkData chunk)
        {
            if (chunk == null) return false;
            foreach (var slot in asyncSlots)
            {
                if (slot.inFlight && !slot.cancelled && slot.pendingChunk == chunk)
                {
                    // Reset immediately — frees the slot for reuse. Any in-flight
                    // readback callbacks will see a generationId mismatch and bail out.
                    slot.Reset();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Number of rebuild slots not currently in-flight.</summary>
        public int FreeRebuildSlotCount
        {
            get { int n = 0; foreach (var s in rebuildSlots) if (!s.inFlight) n++; return n; }
        }

        // =========================================================================
        //  ASYNC PATH — new chunk generation (zero main-thread stalls)
        // =========================================================================

        /// <summary>
        /// Submits a full chunk generation pipeline to the GPU asynchronously.
        /// Returns false immediately if all slots are busy (caller should retry next frame).
        ///
        /// <paramref name="onComplete"/> fires on the main thread (via AsyncGPUReadback)
        /// with (mesh, density33, surfaceHeights33).
        /// </summary>
        /// <summary>
        /// Maps LOD level to marching cubes step size.
        /// LOD0 = full detail (1), LOD1 = half (2), LOD2/LOD3 = quarter (4).
        /// Step 4 is the maximum safe value for a 33³ density texture.
        /// </summary>
        public static int LodToModifier(int lodLevel)
        {
            return lodLevel switch { 1 => 2, >= 2 => 4, _ => 1 };
        }

        public bool GenerateAsync(ChunkData chunk, Vector3 worldPos,
                                  Action<Mesh, float[], float[]> onComplete)
        {
            if (!initialized || densityCompute == null) return false;

            GenerationSlot slot = FindFreeSlot();
            if (slot == null) return false;

            slot.inFlight     = true;
            slot.generationId++;
            slot.pendingChunk = chunk;
            slot.onComplete   = onComplete;

            // Biome precomputation runs on a background thread (1089 noise lookups).
            // The slot is reserved (inFlight=true) so it won't be grabbed by another call.
            // GPU dispatch happens on main thread via ctx.Post after biome data is ready.
            var capturedWorldPos = worldPos;
            int capturedLod      = chunk.lodLevel;
            int capturedGenId    = slot.generationId;
            var ctx              = SynchronizationContext.Current;

            Task.Run(() =>
            {
                var biomeParams = new GpuBiomeParams[POINTS_PER_AXIS * POINTS_PER_AXIS];
                for (int z = 0; z < POINTS_PER_AXIS; z++)
                for (int x = 0; x < POINTS_PER_AXIS; x++)
                {
                    float2 worldXZ = new float2(capturedWorldPos.x + x, capturedWorldPos.z + z);
                    // Single call samples all 4 noise channels + derives terrain shape
                    BiomeMap.SampleClimateAndShape(worldXZ, out ClimateParameters climate, out TerrainShapeData ts);
                    biomeParams[x + z * POINTS_PER_AXIS] = new GpuBiomeParams
                    {
                        heightScale     = ts.heightScale,
                        heightFrequency = ts.heightFrequency,
                        caveScale       = ts.caveScale,
                        caveDensity     = ts.caveDensity,
                        noiseOctaves    = ts.noiseOctaves,
                        persistence     = ts.persistence,
                        lacunarity      = ts.lacunarity,
                        moisture        = climate.moisture
                    };
                }

                ctx.Post(_ => DispatchGenerationGPU(slot, biomeParams, capturedWorldPos, capturedLod, capturedGenId), null);
            });

            return true;
        }

        /// <summary>
        /// Main-thread continuation of GenerateAsync after biome precomputation.
        /// Uploads biome data, dispatches density + MC shaders, and queues readbacks.
        /// </summary>
        private void DispatchGenerationGPU(GenerationSlot slot, GpuBiomeParams[] biomeParams,
            Vector3 worldPos, int lodLevel, int genId)
        {
            // Guard: adapter may have been disposed, chunk unloaded, or generation cancelled
            // while biome was computing on the background thread.
            // If cancelled before GPU dispatch, no readbacks are queued — safe to Reset().
            if (!initialized || !slot.inFlight || slot.generationId != genId)
            {
                if (slot.generationId == genId) slot.Reset();
                return;
            }
            if (slot.cancelled)
            {
                slot.Reset();
                return;
            }

            slot.biomeBuffer.SetData(biomeParams);

            // ---- 1. Dispatch DensityGeneration (per-slot buffers — true GPU parallelism) ----
            densityCompute.SetVector("chunkWorldOffset",   worldPos);
            densityCompute.SetFloat ("domainWarpStrength", DOMAIN_WARP_STRENGTH);
            densityCompute.SetInt   ("worldSeed",          WorldSeed.Seed);
            densityCompute.SetBuffer (_densityKernel, "biomeDataBuffer",      slot.biomeBuffer);
            densityCompute.SetBuffer (_densityKernel, "densityBuffer",         slot.densityBuffer);
            densityCompute.SetBuffer (_densityKernel, "surfaceHeightBuffer",   slot.surfaceHeightBuffer);
            densityCompute.SetTexture(_densityKernel, "densityTextureOut",     slot.densityTexture);
            densityCompute.Dispatch(_densityKernel, DENSITY_DISPATCH, DENSITY_DISPATCH, DENSITY_DISPATCH);

            // ---- 2. Async readbacks ----
            // All LOD levels read back density (needed for ore generation / terrain coloring).
            // LOD chunks skip surface height readbacks (only needed for terrain coloring).
            // Each callback captures genId to detect stale readbacks from cancelled/reused slots.
            bool isLod = lodLevel > 0;

            AsyncGPUReadback.Request(slot.densityBuffer, req =>
            {
                if (slot.generationId != genId) return; // stale — slot was reused
                if (!req.hasError)
                    slot.densityData = req.GetData<float>().ToArray();
                slot.densityReady = true;
                TryFinalize(slot);
            });

            if (isLod)
            {
                slot.surfaceHeightsReady = true;
            }
            else
            {
                AsyncGPUReadback.Request(slot.surfaceHeightBuffer, req =>
                {
                    if (slot.generationId != genId) return;
                    if (!req.hasError)
                        slot.surfaceHeightData = req.GetData<float>().ToArray();
                    slot.surfaceHeightsReady = true;
                    TryFinalize(slot);
                });
            }

            // ---- 3. Dispatch MarchingCubes (per-slot density texture) ----
            int lodMod   = LodToModifier(lodLevel);
            int mcGroups = Mathf.Max(MC_DISPATCH / lodMod, 1);
            slot.triangleBuffer.SetCounterValue(0);
            marchCompute.SetInt   ("lodModifier", lodMod);
            marchCompute.SetTexture(0, "densityData", slot.densityTexture);
            marchCompute.SetBuffer (0, "triangles",   slot.triangleBuffer);
            marchCompute.Dispatch(0, mcGroups, mcGroups, mcGroups);

            // ---- 4. Copy triangle count, then async readback ----
            ComputeBuffer.CopyCount(slot.triangleBuffer, slot.triCountBuffer, 0);

            AsyncGPUReadback.Request(slot.triCountBuffer, countReq =>
            {
                if (slot.generationId != genId) return; // stale
                if (countReq.hasError)
                {
                    slot.trianglesReady = true;
                    TryFinalize(slot);
                    return;
                }

                int count = (int)countReq.GetData<uint>()[0];

                if (count <= 0)
                {
                    slot.triangleData   = Array.Empty<Triangle>();
                    slot.trianglesReady = true;
                    TryFinalize(slot);
                    return;
                }

                AsyncGPUReadback.Request(slot.triangleBuffer, count * TRIANGLE_STRIDE, 0, triReq =>
                {
                    if (slot.generationId != genId) return; // stale
                    if (!triReq.hasError)
                        slot.triangleData = triReq.GetData<Triangle>().ToArray();
                    slot.trianglesReady = true;
                    TryFinalize(slot);
                });
            });
        }

        private void TryFinalize(GenerationSlot slot)
        {
            if (!slot.IsComplete) return;

            // If cancelled, free the slot immediately — skip expensive mesh building.
            if (slot.cancelled)
            {
                slot.Reset();
                return;
            }

            var triangleData      = slot.triangleData;
            var densityData       = slot.densityData;
            var surfaceHeightData = slot.surfaceHeightData;
            var cb                = slot.onComplete;
            slot.Reset();

            // Capture the main-thread sync context before handing off to a Task.
            // BuildMeshArrays is pure CPU work and can run on a thread-pool thread.
            // Only Mesh construction (Unity API) needs main thread.
            var ctx = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var (verts, indices, normals) = BuildMeshArrays(triangleData);
                ctx.Post(_ =>
                {
                    Mesh mesh = null;
                    if (verts != null)
                    {
                        mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                        mesh.SetVertices(verts);
                        mesh.SetTriangles(indices, 0);
                        if (normals != null) mesh.SetNormals(normals);
                        else mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                    }
                    cb?.Invoke(mesh, densityData, surfaceHeightData);
                }, null);
            });
        }

        /// <summary>
        /// Pure CPU work — safe to run on any thread.
        /// Assembles vertex/index/normal arrays from GPU triangle data.
        /// Normals are computed on the GPU (gradient-based) and included in the Triangle struct.
        /// </summary>
        private static (Vector3[] verts, int[] indices, Vector3[] normals) BuildMeshArrays(
            Triangle[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
                return (null, null, null);

            int numTris  = triangles.Length;
            var vertices = new Vector3[numTris * 3];
            var indices  = new int[numTris * 3];
            var normals  = new Vector3[numTris * 3];

            for (int i = 0; i < numTris; i++)
                for (int j = 0; j < 3; j++)
                {
                    int idx       = i * 3 + j;
                    indices[idx]  = idx;
                    vertices[idx] = triangles[i][j];
                    normals[idx]  = triangles[i].GetNormal(j);
                }

            return (vertices, indices, normals);
        }

        // Kept for the sync fallback path only
        private Mesh BuildMesh(Triangle[] triangles)
        {
            var (verts, indices, normals) = BuildMeshArrays(triangles);
            if (verts == null) return null;

            Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);
            if (normals != null) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // =========================================================================
        //  ASYNC REBUILD PATH — terrain deformation mesh rebuilds (zero main-thread stalls)
        // =========================================================================

        /// <summary>
        /// Asynchronously generates a mesh from an existing 33³ CPU density array.
        /// Used by RebuildChunkMesh (terrain deformation). Each rebuild uses an independent
        /// slot with its own GPU buffers, so up to REBUILD_SLOT_COUNT rebuilds can run in
        /// parallel without conflicting with each other or the new-chunk async path.
        ///
        /// Returns false if all rebuild slots are busy (caller retries next frame).
        /// <paramref name="onComplete"/> fires on the main thread (via AsyncGPUReadback).
        /// </summary>
        public bool GenerateMeshAsync(float[] densityField33, Action<Mesh> onComplete)
        {
            if (!initialized) return false;

            RebuildSlot slot = FindFreeRebuildSlot();
            if (slot == null) return false;

            slot.inFlight = true;

            // Upload density into the slot's buffer → GPU-copy into the slot's dedicated texture.
            // Each slot has its own buffers so concurrent rebuilds never overwrite each other.
            slot.densityBuffer.SetData(densityField33);
            densityCompute.SetBuffer (_copyKernel, "densityCopySource", slot.densityBuffer);
            densityCompute.SetTexture(_copyKernel, "densityTextureOut",  slot.densityTexture);
            densityCompute.Dispatch(_copyKernel, DENSITY_DISPATCH, DENSITY_DISPATCH, DENSITY_DISPATCH);

            // Run MC — reads slot.densityTexture, writes slot.triangleBuffer
            // Rebuilds always use full detail (lodModifier=1) — deformed terrain is always LOD0.
            slot.triangleBuffer.SetCounterValue(0);
            marchCompute.SetInt   ("lodModifier", 1);
            marchCompute.SetTexture(0, "densityData", slot.densityTexture);
            marchCompute.SetBuffer (0, "triangles",   slot.triangleBuffer);
            marchCompute.Dispatch(0, MC_DISPATCH, MC_DISPATCH, MC_DISPATCH);

            // Async triangle-count readback
            ComputeBuffer.CopyCount(slot.triangleBuffer, slot.triCountBuffer, 0);
            AsyncGPUReadback.Request(slot.triCountBuffer, countReq =>
            {
                if (countReq.hasError)
                {
                    slot.inFlight = false;
                    onComplete?.Invoke(null);
                    return;
                }

                int count = (int)countReq.GetData<uint>()[0];
                if (count <= 0)
                {
                    slot.inFlight = false;
                    onComplete?.Invoke(null);
                    return;
                }

                // Async triangle readback — exactly `count` triangles
                AsyncGPUReadback.Request(slot.triangleBuffer, count * TRIANGLE_STRIDE, 0, triReq =>
                {
                    slot.inFlight = false;
                    if (triReq.hasError)
                    {
                        onComplete?.Invoke(null);
                        return;
                    }

                    var trianglesCopy = triReq.GetData<Triangle>().ToArray();
                    var ctx           = SynchronizationContext.Current;
                    Task.Run(() =>
                    {
                        var (verts, indices, normals) = BuildMeshArrays(trianglesCopy);
                        ctx.Post(_ =>
                        {
                            Mesh mesh = null;
                            if (verts != null)
                            {
                                mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                                mesh.SetVertices(verts);
                                mesh.SetTriangles(indices, 0);
                                if (normals != null) mesh.SetNormals(normals);
                                else mesh.RecalculateNormals();
                                mesh.RecalculateBounds();
                            }
                            onComplete?.Invoke(mesh);
                        }, null);
                    });
                });
            });

            return true;
        }

        // =========================================================================
        //  SYNC PATH — kept as fallback (not used by normal gameplay)
        // =========================================================================

        /// <summary>
        /// Synchronously generates a mesh from an existing 33³ density array.
        /// Prefer GenerateMeshAsync for runtime use. This stalls the main thread on GetData.
        /// </summary>
        public Mesh GenerateMesh(float[] densityField33)
        {
            if (!initialized) return null;

            // Sync path uses rebuildSlots[0]. Safe because sync calls block the main thread
            // so no other rebuild can start while this is executing.
            RebuildSlot slot = rebuildSlots[0];

            slot.densityBuffer.SetData(densityField33);
            densityCompute.SetBuffer (_copyKernel, "densityCopySource", slot.densityBuffer);
            densityCompute.SetTexture(_copyKernel, "densityTextureOut",  slot.densityTexture);
            densityCompute.Dispatch(_copyKernel, DENSITY_DISPATCH, DENSITY_DISPATCH, DENSITY_DISPATCH);

            // Sync path always uses full detail.
            slot.triangleBuffer.SetCounterValue(0);
            marchCompute.SetInt   ("lodModifier", 1);
            marchCompute.SetTexture(0, "densityData", slot.densityTexture);
            marchCompute.SetBuffer (0, "triangles",   slot.triangleBuffer);
            marchCompute.Dispatch(0, MC_DISPATCH, MC_DISPATCH, MC_DISPATCH);

            ComputeBuffer.CopyCount(slot.triangleBuffer, slot.triCountBuffer, 0);
            int[] countArr = { 0 };
            slot.triCountBuffer.GetData(countArr);
            int numTris = countArr[0];
            if (numTris == 0) return null;

            Triangle[] triangles = new Triangle[numTris];
            slot.triangleBuffer.GetData(triangles, 0, 0, numTris);

            return BuildMesh(triangles);
        }

        // =========================================================================
        //  HELPERS
        // =========================================================================

        private GenerationSlot FindFreeSlot()
        {
            foreach (var s in asyncSlots)
                if (!s.inFlight) return s;
            return null;
        }

        private RebuildSlot FindFreeRebuildSlot()
        {
            foreach (var s in rebuildSlots)
                if (!s.inFlight) return s;
            return null;
        }

        /// <summary>Release all GPU resources. Call when the adapter is no longer needed.</summary>
        public void Dispose()
        {
            foreach (var s in asyncSlots)
                s?.Dispose();
            foreach (var s in rebuildSlots)
                s?.Dispose();
            initialized = false;
        }
    }
}
