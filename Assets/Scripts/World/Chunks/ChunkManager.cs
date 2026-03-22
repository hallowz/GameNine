using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.Diagnostics;
using Voidborne.Player;
using Voidborne.World.Biomes;
using Voidborne.Building;
using Voidborne.World.Decoration;
using Voidborne.World.Generation;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Singleton MonoBehaviour that manages the lifecycle of all chunks in the world.
    /// Not to be confused with Voidborne.World.MarchingCubes.ChunkManager (external MC repo).
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("Render Distance (LOD0 — Full Detail)")]
        [SerializeField] private DirectionalDistance lod0Distance = new DirectionalDistance(3, 2, 2);

        [Header("LOD1 (half-detail) — set all to 0 to disable")]
        [SerializeField] private DirectionalDistance lod1Distance = new DirectionalDistance(6, 5, 2);

        [Header("LOD2 (quarter-detail) — set all to 0 to disable")]
        [SerializeField] private DirectionalDistance lod2Distance = new DirectionalDistance(9, 5, 2);

        [Header("LOD3 (minimal — no ore/decorations) — set all to 0 to disable")]
        [SerializeField] private DirectionalDistance lod3Distance = new DirectionalDistance(14, 3, 1);

        [Header("LOD4 (greedy mesh — extreme distance) — set all to 0 to disable")]
        [SerializeField] private DirectionalDistance lod4Distance = new DirectionalDistance(22, 4, 1);

        [Header("Sky Island -Y Boost")]
        [Tooltip("Extra -Y chunks added to LOD1/2/3 when the player is above the sky zone. " +
                 "Lets surface terrain stay visible from sky islands.")]
        [SerializeField] private int skyNegYBoostLod1 = 4;
        [SerializeField] private int skyNegYBoostLod2 = 6;
        [SerializeField] private int skyNegYBoostLod3 = 10;

        [Header("Unload Buffer")]
        [Tooltip("Extra chunk distance beyond render distance before unloading. Prevents thrashing at the boundary.")]
        [SerializeField] private int unloadBuffer = 2;

        [Header("Callback Throttle")]
        [Tooltip("Max ore job completions processed per frame. Prevents spike when many chunks finish simultaneously.")]
        [SerializeField] private int maxOreCompletionsPerFrame = 2;

        [Header("Decoration")]
        [Tooltip("WorldDecorationManager that places trees, rocks, grass on active chunks.")]
        [SerializeField] private WorldDecorationManager decorationManager;

        [Header("Ore Generation")]
        [Tooltip("Registry of all ore definitions. Assign the OreRegistry asset here to enable ore generation.")]
        [SerializeField] private OreRegistry oreRegistry;

        [Header("Rendering")]
        [Tooltip("Default material for chunk meshes. If left empty, a triplanar terrain material is created automatically.")]
        [SerializeField] private Material defaultChunkMaterial;

        [Header("Triplanar Shader Settings")]
        [Tooltip("Texture scale for triplanar mapping. Smaller = larger textures on terrain.")]
        [SerializeField] private float triplanarTexScale = 0.25f;
        [Tooltip("Sharpness of triplanar axis blending. Higher = sharper transitions between projection axes.")]
        [Range(1f, 16f)]
        [SerializeField] private float triplanarBlendSharpness = 4f;

        /// <summary>
        /// The name of the triplanar terrain shader. Used to find the shader at runtime.
        /// </summary>
        private const string TriplanarShaderName = "Voidborne/TriplanarTerrain";

        // Per-LOD directional distance accessors (LOD1-3 include sky -Y boost)
        public DirectionalDistance Lod0Distance => lod0Distance;
        public DirectionalDistance Lod1Distance => ApplySkyBoost(lod1Distance, skyNegYBoostLod1);
        public DirectionalDistance Lod2Distance => ApplySkyBoost(lod2Distance, skyNegYBoostLod2);
        public DirectionalDistance Lod3Distance => ApplySkyBoost(lod3Distance, skyNegYBoostLod3);
        public DirectionalDistance Lod4Distance => lod4Distance;

        /// <summary>Current sky -Y boost in chunks, based on player altitude. 0 on the surface, 1.0 at/above SkyStart.</summary>
        private float _skyBoostFactor;

        /// <summary>
        /// Updates the sky boost factor based on the player's world Y position.
        /// Called by ChunkLoader when the player moves to a new chunk.
        /// </summary>
        public void UpdateSkyBoost(float playerWorldY)
        {
            // Ramp from 0 at SkyTransitionStart (80) to full at SkyStart (100)
            _skyBoostFactor = Mathf.Clamp01(
                (playerWorldY - DensityFunction.SkyTransitionStart) /
                (DensityFunction.SkyStart - DensityFunction.SkyTransitionStart));
        }

        private DirectionalDistance ApplySkyBoost(DirectionalDistance dist, int maxBoost)
        {
            if (_skyBoostFactor <= 0f || maxBoost <= 0) return dist;
            dist.negY += Mathf.RoundToInt(_skyBoostFactor * maxBoost);
            return dist;
        }

        /// <summary>
        /// Dynamic LOD0 distance that expands per-direction when the player is surrounded
        /// by empty (air) chunks. Expands progressively through LOD1/2/3 until terrain is
        /// found, up to MaxDistance. Reset to static Lod0Distance when the player has
        /// terrain within the static range.
        /// </summary>
        private DirectionalDistance _effectiveLod0Distance;
        public DirectionalDistance EffectiveLod0Distance
        {
            get => _effectiveLod0Distance;
            set => _effectiveLod0Distance = value;
        }

        /// <summary>
        /// Maximum directional distance across all LOD levels (used for unload boundary).
        /// </summary>
        public DirectionalDistance MaxDistance =>
            DirectionalDistance.Max(lod0Distance,
                DirectionalDistance.Max(Lod1Distance,
                    DirectionalDistance.Max(Lod2Distance,
                        DirectionalDistance.Max(Lod3Distance, Lod4Distance))));

        /// <summary>Maximum horizontal render distance across all LOD levels.</summary>
        public int MaxHorizontalDistance => MaxDistance.MaxHorizontal;

        /// <summary>
        /// Returns the target LOD level for a chunk at the given signed offset
        /// (dx, dy, dz) from the player chunk.
        /// </summary>
        public int GetTargetLodLevel(int dx, int dy, int dz)
        {
            if (lod0Distance.Contains(dx, dy, dz)) return 0;
            DirectionalDistance l1 = Lod1Distance, l2 = Lod2Distance, l3 = Lod3Distance, l4 = Lod4Distance;
            if (!l1.IsDisabled && l1.Contains(dx, dy, dz)) return 1;
            if (!l2.IsDisabled && l2.Contains(dx, dy, dz)) return 2;
            if (!l3.IsDisabled && l3.Contains(dx, dy, dz)) return 3;
            if (!l4.IsDisabled && l4.Contains(dx, dy, dz)) return 4;
            return -1; // out of range
        }

        // Hysteresis buffer: a chunk must move this many extra chunks beyond
        // its current LOD boundary before being downgraded. Prevents oscillation
        // at LOD boundaries that causes terrain flicker and tree pop-in.
        private const int LodHysteresis = 1;

        /// <summary>
        /// Returns the target LOD level for downgrade decisions, with hysteresis.
        /// A chunk at LOD N won't downgrade until it's LodHysteresis chunks beyond
        /// the LOD N boundary. This prevents oscillation at LOD boundaries.
        /// </summary>
        public int GetTargetLodLevelForDowngrade(int dx, int dy, int dz, int currentLod)
        {
            DirectionalDistance l1 = Lod1Distance, l2 = Lod2Distance;
            switch (currentLod)
            {
                case 0:
                    if (_effectiveLod0Distance.ContainsWithMargin(dx, dy, dz, LodHysteresis))
                        return 0; // stay at LOD0
                    break;
                case 1:
                    if (!l1.IsDisabled
                        && l1.ContainsWithMargin(dx, dy, dz, LodHysteresis))
                        return 1; // stay at LOD1
                    break;
                case 2:
                    if (!l2.IsDisabled
                        && l2.ContainsWithMargin(dx, dy, dz, LodHysteresis))
                        return 2; // stay at LOD2
                    break;
            }

            // Beyond hysteresis zone — use normal LOD determination
            return GetTargetLodLevel(dx, dy, dz);
        }

        /// <summary>
        /// Returns the world-space distance to the nearest chunk position that should
        /// be rendered but is not yet Active (still generating, pending, or unloaded).
        /// Only checks horizontal neighbours at the player's Y level for performance.
        /// Returns float.MaxValue if all expected chunks are loaded.
        /// </summary>
        public float NearestUnrenderedDistance(Vector3 playerPos)
        {
            Vector3Int center = ChunkCoordUtility.WorldToChunkPos(playerPos);
            DirectionalDistance maxDist = MaxDistance;
            float nearestSq = float.MaxValue;

            for (int x = -maxDist.negX; x <= maxDist.posX; x++)
            {
                for (int z = -maxDist.negZ; z <= maxDist.posZ; z++)
                {
                    if (x == 0 && z == 0) continue;

                    // Only check positions that should exist according to LOD config
                    if (GetTargetLodLevel(x, 0, z) == -1) continue;

                    Vector3Int pos = new Vector3Int(center.x + x, center.y, center.z + z);

                    bool isRendered = false;
                    if (chunks.TryGetValue(pos, out ChunkData data) && data.state == ChunkState.Active)
                        isRendered = true;

                    if (!isRendered)
                    {
                        float fdx = x * ChunkData.SIZE;
                        float fdz = z * ChunkData.SIZE;
                        float distSq = fdx * fdx + fdz * fdz;
                        if (distSq < nearestSq)
                            nearestSq = distSq;
                    }
                }
            }

            return nearestSq < float.MaxValue ? Mathf.Sqrt(nearestSq) : float.MaxValue;
        }

        /// <summary>
        /// The material used for all chunk rendering. Exposed so other systems can reference it.
        /// </summary>
        public Material ChunkMaterial => defaultChunkMaterial;

        /// <summary>
        /// The OreRegistry used for ore generation. Exposed so mining systems can look up ore definitions by ID.
        /// </summary>
        public OreRegistry OreRegistry => oreRegistry;

        private Dictionary<Vector3Int, ChunkData> chunks = new Dictionary<Vector3Int, ChunkData>();
        private Dictionary<Vector3Int, GameObject> chunkObjects = new Dictionary<Vector3Int, GameObject>();

        private ChunkMeshBuilder meshBuilder;
        private MarchingCubesAdapter marchingCubesAdapter;
        private ChunkPool chunkPool;
        private Transform chunkParent;

        /// <summary>Material using TriplanarTerrainCompact shader for LOD0 (compact vertex format).</summary>
        private Material compactChunkMaterial;

        // Occlusion culling: BFS visibility traversal using per-chunk visibility graphs
        private readonly ChunkOcclusionCuller occlusionCuller = new ChunkOcclusionCuller();
        private Vector3Int lastOcclusionCameraChunk;
        private bool hasLastOcclusionCamera;

        // Region batching: combines LOD1+ chunks into larger meshes to reduce draw calls
        private readonly RegionManager regionManager = new RegionManager();

        // Reusable list to avoid allocations during unload checks
        private readonly List<Vector3Int> chunksToRemove = new List<Vector3Int>();

        // Cached key lists to avoid Dictionary enumerator allocations in UnloadDistantChunks
        private readonly List<Vector3Int> _chunkDataKeyCache   = new List<Vector3Int>();

        // Pre-allocated sort delegate for ore finalization — avoids lambda allocation every frame
        private Vector3 _oreSortPlayerPos;
        private System.Comparison<ChunkData> _oreFinalizationComparison;

        // Cached arrays for FinalizeOreChunk to avoid per-call allocations
        private static readonly Vector3Int[] CardinalOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };
        private static readonly Vector3Int[] OreNeighborOffsets = {
            new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
            new Vector3Int(0,1,0), new Vector3Int(0,-1,0),
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
        };
        // Cached sort delegate to avoid lambda allocation every unload cycle
        private Vector3Int _sortPlayerChunk;
        private System.Comparison<Vector3Int> _unloadSortComparison;

        // LOD downgrade tracking — chunks at a better LOD than their distance warrants
        private struct ChunkDowngradeEntry
        {
            public Vector3Int position;
            public int targetLod;
            public int distance;
        }

        private readonly List<ChunkDowngradeEntry> chunksToDowngrade = new List<ChunkDowngradeEntry>();

        // Pre-allocated sort delegate for downgrade entries
        private static readonly System.Comparison<ChunkDowngradeEntry> _downgradeComparison =
            (a, b) => b.distance.CompareTo(a.distance);

        // -------------------------------------------------------------------------
        //  Pending ore generation jobs (Job System, off main thread)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Holds the state of a scheduled VoxelClassificationJob so it can be polled
        /// each frame and finalised once complete.
        /// </summary>
        private struct PendingOreJob
        {
            public JobHandle              handle;
            public ChunkData              chunk;
            public NativeArray<byte>      oreField;
            public NativeArray<byte>      biomeField;
            public NativeArray<float>     densityNative;
            public NativeArray<float>     surfaceHeightsNative;
            public NativeArray<ClimateParameters> climateColumnsNative;
            public NativeArray<OreParams> oreParamsNative;
            public NativeArray<BiomeLookupTable.Entry> lookupNative;
            public BiomeTree.NativeBiomeTree nativeTree;
            // Occlusion culling: visibility graph computed alongside ore generation
            public JobHandle              visibilityHandle;
            public NativeArray<ushort>    visibilityResult;
        }

        private readonly List<PendingOreJob> pendingOreJobs = new List<PendingOreJob>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ChunkManager] Duplicate instance detected. Destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // If no material assigned in the Inspector, create a triplanar terrain material
            if (defaultChunkMaterial == null)
            {
                defaultChunkMaterial = CreateTriplanarMaterial();
            }

            // Create compact material for LOD0 (octahedral normals, half-float positions).
            // Copies all properties from the standard material so they look identical.
            Shader compactShader = Shader.Find("Voidborne/TriplanarTerrainCompact");
            if (compactShader != null && defaultChunkMaterial != null)
            {
                compactChunkMaterial = new Material(defaultChunkMaterial);
                compactChunkMaterial.shader = compactShader;
                compactChunkMaterial.name = "TriplanarTerrainCompact (Auto)";
            }

            // Ensure biome definitions are created on the main thread (ScriptableObject.CreateInstance
            // is main-thread-only). ComputeBiomeData runs on Task.Run threads and needs these cached.
            BiomeMap.GetBiomes();

            // Set global ore color array for all terrain shaders.
            InitializeOreColors();

            // Create a parent transform to keep the hierarchy clean
            chunkParent = new GameObject("Chunks").transform;
            chunkParent.SetParent(transform);

            // Initialize the marching cubes pipeline
            marchingCubesAdapter = new MarchingCubesAdapter();
            meshBuilder = new ChunkMeshBuilder(marchingCubesAdapter);

            // Initialize effective LOD0 distance to static value
            _effectiveLod0Distance = lod0Distance;

            // Initialize the chunk pool
            chunkPool = new ChunkPool(chunkParent, defaultChunkMaterial);

            // Initialize region batching for LOD1+ draw call reduction
            var regionParent = new GameObject("ChunkRegions").transform;
            regionParent.SetParent(transform);
            regionManager.Initialize(regionParent, defaultChunkMaterial);

            // Pre-allocate sort delegate to avoid lambda capture allocation each unload cycle
            _unloadSortComparison = (a, b) =>
            {
                int distA = Mathf.Max(Mathf.Abs(a.x - _sortPlayerChunk.x), Mathf.Abs(a.z - _sortPlayerChunk.z));
                int distB = Mathf.Max(Mathf.Abs(b.x - _sortPlayerChunk.x), Mathf.Abs(b.z - _sortPlayerChunk.z));
                return distB.CompareTo(distA);
            };

            // Pre-allocate ore finalization sort delegate
            _oreFinalizationComparison = (a, b) =>
            {
                if (a.lodLevel != b.lodLevel)
                    return a.lodLevel.CompareTo(b.lodLevel);

                float halfSize = ChunkData.SIZE * 0.5f;
                Vector3 ca = ChunkCoordUtility.ChunkToWorldPos(a.chunkPosition);
                ca.x += halfSize; ca.y += halfSize; ca.z += halfSize;
                Vector3 cb = ChunkCoordUtility.ChunkToWorldPos(b.chunkPosition);
                cb.x += halfSize; cb.y += halfSize; cb.z += halfSize;
                Vector3 p = _oreSortPlayerPos;
                float distA = (ca.x - p.x) * (ca.x - p.x) + (ca.y - p.y) * (ca.y - p.y) + (ca.z - p.z) * (ca.z - p.z);
                float distB = (cb.x - p.x) * (cb.x - p.x) + (cb.y - p.y) * (cb.y - p.y) + (cb.z - p.z) * (cb.z - p.z);
                return distA.CompareTo(distB);
            };
        }

        /// <summary>
        /// Builds the global ore color array from OreDefinitions and biome colorTints,
        /// then sets it on all terrain shaders via Shader.SetGlobalVectorArray and
        /// directly on both chunk materials for robustness.
        /// </summary>
        private static readonly int OreColorsPropId = Shader.PropertyToID("_OreColors");
        private Vector4[] _oreColorArray;

        private void InitializeOreColors()
        {
            const int ORE_ARRAY_SIZE = 32;
            var colors = new Vector4[ORE_ARRAY_SIZE];

            // Slot 0 = "no ore / air" — neutral gray fallback so it's never invisible black
            colors[0] = new Vector4(0.5f, 0.5f, 0.5f, 1f);

            // Fill from OreDefinition assets (actual ores + terrain types)
            if (oreRegistry != null && oreRegistry.oreDefinitions != null)
            {
                foreach (var ore in oreRegistry.oreDefinitions)
                {
                    if (ore == null || ore.oreTypeId >= ORE_ARRAY_SIZE) continue;
                    Color c = ore.colorTint;
                    colors[ore.oreTypeId] = new Vector4(c.r, c.g, c.b, c.a);
                }
            }

            // Fill per-biome grass colors from biome definitions.
            // Each biome's colorTint becomes the ore color for its grass variant.
            var biomes = BiomeMap.GetBiomes();
            if (biomes != null)
            {
                foreach (var biome in biomes)
                {
                    if (biome == null) continue;
                    byte grassId = biome.surfaceTopId;
                    // Only fill per-biome grass slots (14-21); don't overwrite shared types
                    if (grassId >= OreGenerator.GrassPlainsId && grassId < ORE_ARRAY_SIZE)
                    {
                        Color c = biome.colorTint;
                        colors[grassId] = new Vector4(c.r, c.g, c.b, c.a);
                    }
                }
            }

            _oreColorArray = colors;

            // Set globally (covers future materials) and directly on both chunk materials
            Shader.SetGlobalVectorArray(OreColorsPropId, colors);
            if (defaultChunkMaterial != null)
                defaultChunkMaterial.SetVectorArray(OreColorsPropId, colors);
            if (compactChunkMaterial != null)
                compactChunkMaterial.SetVectorArray(OreColorsPropId, colors);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Complete and dispose any in-flight ore generation jobs.
                for (int i = 0; i < pendingOreJobs.Count; i++)
                {
                    PendingOreJob job = pendingOreJobs[i];
                    job.handle.Complete();
                    if (job.oreField.IsCreated)              job.oreField.Dispose();
                    if (job.biomeField.IsCreated)            job.biomeField.Dispose();
                    if (job.densityNative.IsCreated)         job.densityNative.Dispose();
                    if (job.surfaceHeightsNative.IsCreated)  job.surfaceHeightsNative.Dispose();
                    if (job.climateColumnsNative.IsCreated)  job.climateColumnsNative.Dispose();
                    if (job.oreParamsNative.IsCreated)       job.oreParamsNative.Dispose();
                    if (job.lookupNative.IsCreated)          job.lookupNative.Dispose();
                    job.nativeTree.Dispose();
                    job.visibilityHandle.Complete();
                    if (job.visibilityResult.IsCreated)      job.visibilityResult.Dispose();
                }
                pendingOreJobs.Clear();

                // Complete and dispose any in-flight UV2 jobs.
                for (int i = 0; i < pendingUV2Jobs.Count; i++)
                {
                    PendingUV2Job uv2Job = pendingUV2Jobs[i];
                    uv2Job.jobData.Complete(); // Complete disposes NativeArrays internally
                }
                pendingUV2Jobs.Clear();

                // Dispose region batching
                regionManager.Dispose();

                // Dispose of GPU resources
                marchingCubesAdapter?.Dispose();

                // Clean up pool
                chunkPool?.Clear();

                // Destroy all active chunk meshes
                foreach (var kvp in chunks)
                {
                    if (kvp.Value.mesh != null)
                    {
                        Destroy(kvp.Value.mesh);
                    }
                }

                chunks.Clear();
                chunkObjects.Clear();

                Instance = null;
            }
        }

        /// <summary>
        /// Returns the ChunkData at the given chunk position, or null if not loaded.
        /// </summary>
        public ChunkData GetChunk(Vector3Int pos)
        {
            chunks.TryGetValue(pos, out ChunkData chunk);
            return chunk;
        }

        /// <summary>
        /// Returns the ChunkData at the given chunk position, creating a new one if it doesn't exist.
        /// </summary>
        public ChunkData GetOrCreateChunk(Vector3Int pos)
        {
            if (!chunks.TryGetValue(pos, out ChunkData chunk))
            {
                chunk = new ChunkData(pos);
                chunks[pos] = chunk;
            }

            return chunk;
        }

        /// <summary>
        /// Submits a chunk for async GPU generation. Returns true if generation was started,
        /// false if all async slots are busy (ChunkLoader will retry on the next frame).
        /// The chunk GameObject is created and the mesh applied in the async callback.
        /// LOD chunks (lodLevel > 0) skip ore generation and decorations.
        /// </summary>
        public bool LoadChunk(Vector3Int chunkPos, int lodLevel = 0)
        {
            ChunkData data = GetOrCreateChunk(chunkPos);

            // Skip if already in-flight
            if (data.state == ChunkState.Generating)
                return true;

            // Skip if already active at the correct LOD level
            if (data.state == ChunkState.Active && chunkObjects.ContainsKey(chunkPos)
                && data.lodLevel == lodLevel)
                return true;

            // LOD downgrade: clean up LOD-specific resources before regenerating.
            // LOD0 has decorations, colliders; LOD1+ have none of these.
            bool isDowngrade = data.state == ChunkState.Active && data.lodLevel < lodLevel;
            if (isDowngrade)
            {
                // Clean up decorations (trees, rocks, grass, billboards)
                decorationManager?.OnChunkDeactivating(chunkPos);
                // Hide buildings when LOD0 is downgraded (buildings are LOD0-only)
                if (data.lodLevel == 0)
                    BuildingManager.Instance?.OnChunkColumnDeactivated(chunkPos);
            }

            // LOD upgrade/downgrade: chunk is Active at a different LOD. Keep the old
            // GameObject visible during generation so there's no flicker gap.
            // Capture the old mesh so we can destroy it once the new one is applied.
            Mesh oldMesh = (data.state == ChunkState.Active) ? data.mesh : null;

            data.lodLevel = lodLevel;

            // LOD4 greedy mesher disabled — DensityFunction is not thread-safe for
            // Task.Run, and the coarse sampling produces visual artifacts.
            // LOD4 chunks fall through to the standard GPU marching cubes path.
            // if (lodLevel >= 4)
            //     return LoadChunkGreedy(data, chunkPos, oldMesh);

            bool started = meshBuilder.GenerateChunkMeshAsync(data, () =>
            {
                // Fires on the main thread when mesh is ready.
                // Guard against the chunk being unloaded while in-flight.
                if (!chunks.ContainsKey(chunkPos) || chunks[chunkPos] != data)
                    return;

                // Empty chunks (all air or all solid) have no vertices to color —
                // skip ore and activate immediately so the player controller
                // doesn't freeze waiting for a chunk that has nothing to render.
                if (data.mesh == null)
                {
                    if (oldMesh != null)
                        Destroy(oldMesh);

                    GameObject chunkGO  = CreateChunkGameObject(data);
                    var        renderer = chunkGO.GetComponent<ChunkRenderer>();
                    renderer.ApplyMesh(null);
                    chunkGO.SetActive(true);
                    return;
                }

                // Schedule voxel classification (biome + ore + terrain type) for all LODs.
                // Biome data must be populated before mesh activation so vertex colors
                // are consistent across every LOD level.
                if (oreRegistry != null && oreRegistry.oreDefinitions != null)
                {
                    ScheduleVoxelClassification(data);
                }
                else
                {
                    // No ore registry — apply mesh and activate immediately.
                    if (oldMesh != null && oldMesh != data.mesh)
                        Destroy(oldMesh);

                    GameObject chunkGO  = CreateChunkGameObject(data);
                    var        renderer = chunkGO.GetComponent<ChunkRenderer>();
                    renderer.ApplyMesh(data.mesh);
                    chunkGO.SetActive(true);

                    data.state = ChunkState.Active;
                    if (lodLevel == 0)
                        decorationManager?.OnChunkActivated(data, data.mesh);
                    else if (lodLevel <= 3)
                        decorationManager?.OnBillboardChunkActivated(data);
                }
            });

            return started;
        }

        /// <summary>
        /// Schedules the unified VoxelClassificationJob for a chunk. This replaces
        /// the old OreGenerator.ScheduleAsync + PlaceTerrainTypes pipeline.
        /// Pre-computes climate columns, then assigns biome ID, ore type, and
        /// terrain type for every voxel in a single Burst job.
        /// </summary>
        private void ScheduleVoxelClassification(ChunkData data)
        {
            const int SIZE = ChunkData.SIZE;
            int volume = SIZE * SIZE * SIZE;
            int columns = SIZE * SIZE;

            // Prepare surface heights
            float[] surfHeights = data.gpuSurfaceHeights;
            if (surfHeights == null)
            {
                // Fallback: fill with sentinel
                surfHeights = new float[columns];
                for (int i = 0; i < columns; i++) surfHeights[i] = -1e9f;
            }

            // Get BiomeTree and LookupTable
            BiomeTree tree = BiomeMap.GetBiomeTree();
            BiomeLookupTable lookupTable = BiomeMap.GetLookupTable();

            // Create native arrays
            var densityNative = new NativeArray<float>(data.densityField, Allocator.TempJob);
            var surfaceHeightsNative = new NativeArray<float>(surfHeights, Allocator.TempJob);
            var biomeFieldNative = new NativeArray<byte>(volume, Allocator.TempJob);
            var oreFieldNative = new NativeArray<byte>(volume, Allocator.TempJob);

            // Precompute climate columns
            var climateColumnsNative = new NativeArray<ClimateParameters>(columns, Allocator.TempJob);
            var climateJob = new ClimateColumnJob
            {
                ChunkWorldOriginXZ = new Unity.Mathematics.int2(
                    data.chunkPosition.x * SIZE,
                    data.chunkPosition.z * SIZE),
                TemperatureSeedOffset     = WorldSeed.SeedOffset(10),
                MoistureSeedOffset        = WorldSeed.SeedOffset(11),
                ContinentalnessSeedOffset = WorldSeed.SeedOffset(13),
                ErosionSeedOffset         = WorldSeed.SeedOffset(14),
                ClimateColumns            = climateColumnsNative
            };
            JobHandle climateHandle = climateJob.Schedule(columns, 64);

            // Prepare ore params
            OreDefinition[] oreDefs = oreRegistry.oreDefinitions;
            var oreParamsNative = new NativeArray<OreParams>(oreDefs.Length, Allocator.TempJob);
            for (int i = 0; i < oreDefs.Length; i++)
            {
                OreDefinition ore = oreDefs[i];
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

            // Build native tree + lookup
            BiomeTree.NativeBiomeTree nativeTree = tree.ToNative();
            NativeArray<BiomeLookupTable.Entry> lookupNative = lookupTable.ToNative();

            // Schedule the main voxel classification job (depends on climate column job)
            var voxelJob = new VoxelClassificationJob
            {
                DensityField      = densityNative,
                SurfaceHeights    = surfaceHeightsNative,
                ClimateColumns    = climateColumnsNative,
                OreParamsArray    = oreParamsNative,
                TreeSplitValues   = nativeTree.SplitValues,
                TreeSplitAxes     = nativeTree.SplitAxes,
                TreeBiomeIds      = nativeTree.BiomeIds,
                TreeLeftChild     = nativeTree.LeftChild,
                TreeRightChild    = nativeTree.RightChild,
                TreePoints        = nativeTree.Points,
                TreeNodeCount     = nativeTree.NodeCount,
                LookupEntries     = lookupNative,
                ChunkWorldOrigin  = new Unity.Mathematics.int3(
                    data.chunkPosition.x * SIZE,
                    data.chunkPosition.y * SIZE,
                    data.chunkPosition.z * SIZE),
                DepthScale        = 300f, // normalize depth: 300 voxels = depth 1.0
                BiomeField        = biomeFieldNative,
                OreField          = oreFieldNative
            };

            JobHandle voxelHandle = voxelJob.Schedule(volume, 64, climateHandle);

            // Schedule visibility graph computation (reads density, independent of ore job)
            var visResult = new NativeArray<ushort>(1, Allocator.TempJob);
            var visJob = new ChunkVisibilityJob
            {
                DensityField = densityNative,
                Result = visResult
            };
            // Depends on nothing (density is already populated), but we share densityNative
            // with the voxel job, so schedule after the density dependency is satisfied.
            // Since densityNative is ReadOnly in both jobs, they can run in parallel.
            JobHandle visHandle = visJob.Schedule();

            pendingOreJobs.Add(new PendingOreJob
            {
                handle                = voxelHandle,
                chunk                 = data,
                oreField              = oreFieldNative,
                biomeField            = biomeFieldNative,
                densityNative         = densityNative,
                surfaceHeightsNative  = surfaceHeightsNative,
                climateColumnsNative  = climateColumnsNative,
                oreParamsNative       = oreParamsNative,
                lookupNative          = lookupNative,
                nativeTree            = nativeTree,
                visibilityHandle      = visHandle,
                visibilityResult      = visResult
            });
        }

        /// <summary>
        /// Loads chunks in a radius around the player's position.
        /// Delegated to ChunkLoader — this is kept for backward compatibility.
        /// </summary>
        public void LoadChunksAroundPlayer(Vector3 playerPos)
        {
            Vector3Int centerChunk = ChunkCoordUtility.WorldToChunkPos(playerPos);

            for (int x = -lod0Distance.negX; x <= lod0Distance.posX; x++)
            {
                for (int z = -lod0Distance.negZ; z <= lod0Distance.posZ; z++)
                {
                    for (int y = -lod0Distance.negY; y <= lod0Distance.posY; y++)
                    {
                        Vector3Int chunkPos = new Vector3Int(
                            centerChunk.x + x,
                            centerChunk.y + y,
                            centerChunk.z + z
                        );

                        if (!chunkObjects.ContainsKey(chunkPos))
                        {
                            LoadChunk(chunkPos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Manages distant chunk lifecycle: unloads chunks beyond all LOD ranges,
        /// and downgrades chunks that are at a better LOD than their distance warrants.
        /// Both operations are processed furthest-first so the most distant chunks
        /// transition/disappear before closer ones. Uses a single dictionary scan
        /// over chunks (superset of chunkObjects) and caps removals per call.
        /// </summary>
        public void UnloadDistantChunks(Vector3 playerPos)
        {
            Vector3Int playerChunk = ChunkCoordUtility.WorldToChunkPos(playerPos);

            chunksToRemove.Clear();
            chunksToDowngrade.Clear();

            // Max distance across all LODs + unloadBuffer = the hard unload boundary.
            DirectionalDistance maxDist = MaxDistance;
            DirectionalDistance unloadDist = new DirectionalDistance
            {
                posX = maxDist.posX + unloadBuffer, negX = maxDist.negX + unloadBuffer,
                posY = maxDist.posY + unloadBuffer, negY = maxDist.negY + unloadBuffer,
                posZ = maxDist.posZ + unloadBuffer, negZ = maxDist.negZ + unloadBuffer,
            };

            // Single snapshot over chunks (superset of chunkObjects) — replaces two
            // separate dictionary scans over chunkObjects.Keys and chunks.Keys.
            _chunkDataKeyCache.Clear();
            foreach (var key in chunks.Keys) _chunkDataKeyCache.Add(key);

            for (int i = 0; i < _chunkDataKeyCache.Count; i++)
            {
                Vector3Int pos = _chunkDataKeyCache[i];
                if (!chunks.TryGetValue(pos, out ChunkData data)) continue;

                int dx = pos.x - playerChunk.x;
                int dy = pos.y - playerChunk.y;
                int dz = pos.z - playerChunk.z;

                // Hard unload: beyond max LOD range + buffer
                if (!unloadDist.Contains(dx, dy, dz))
                {
                    // Skip in-flight generations that have no GO — they'll be caught
                    // by CancelStaleGenerations instead.
                    if (data.state == ChunkState.Generating && !chunkObjects.ContainsKey(pos))
                        continue;
                    chunksToRemove.Add(pos);
                    continue;
                }

                // Only chunks with GameObjects can be downgraded
                if (!chunkObjects.ContainsKey(pos)) continue;

                // Use hysteresis-aware LOD for downgrade decisions to prevent
                // oscillation at boundaries (which causes terrain flicker + tree pop).
                int targetLod = GetTargetLodLevelForDowngrade(dx, dy, dz, data.lodLevel);

                if (targetLod == -1)
                {
                    // Beyond all LOD ranges but within buffer — unload
                    chunksToRemove.Add(pos);
                }
                else if (data.state == ChunkState.Active
                         && data.lodLevel < targetLod)
                {
                    // At a better LOD than distance warrants — downgrade
                    chunksToDowngrade.Add(new ChunkDowngradeEntry
                    {
                        position  = pos,
                        targetLod = targetLod,
                        distance  = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz))
                    });
                }
            }

            // Sort unloads furthest first (uses cached delegate to avoid lambda allocation)
            _sortPlayerChunk = playerChunk;
            chunksToRemove.Sort(_unloadSortComparison);

            for (int i = 0; i < chunksToRemove.Count; i++)
            {
                RemoveChunk(chunksToRemove[i]);
            }

            // Sort downgrades furthest first (uses cached delegate)
            chunksToDowngrade.Sort(_downgradeComparison);

            for (int i = 0; i < chunksToDowngrade.Count; i++)
            {
                // LoadChunk returns false when GPU slots are full — stop and retry next frame
                if (!LoadChunk(chunksToDowngrade[i].position, chunksToDowngrade[i].targetLod))
                    break;
            }
        }

        /// <summary>
        /// Creates a chunk GameObject from the pool, initializes it with ChunkData,
        /// and positions it correctly in the world.
        /// </summary>
        public GameObject CreateChunkGameObject(ChunkData data)
        {
            // If a GO already exists for this chunk, return it
            if (chunkObjects.TryGetValue(data.chunkPosition, out GameObject existingGO))
            {
                return existingGO;
            }

            GameObject go = chunkPool.Get();
            var renderer = go.GetComponent<ChunkRenderer>();
            renderer.Initialize(data);

            if (defaultChunkMaterial != null)
            {
                renderer.SetMaterial(defaultChunkMaterial);
            }

            chunkObjects[data.chunkPosition] = go;
            return go;
        }

        /// <summary>
        /// Destroys the chunk GameObject (returns to pool) and removes the ChunkData from tracking.
        /// </summary>
        public void RemoveChunk(Vector3Int pos)
        {
            // Notify decoration manager before removing so it can pool/destroy decorations
            decorationManager?.OnChunkDeactivating(pos);
            BuildingManager.Instance?.OnChunkColumnDeactivated(pos);

            // Return the GameObject to the pool
            if (chunkObjects.TryGetValue(pos, out GameObject go))
            {
                chunkPool.Return(go);
                chunkObjects.Remove(pos);
            }

            // Clean up ChunkData
            if (chunks.TryGetValue(pos, out ChunkData data))
            {
                // Destroy the mesh to free memory
                if (data.mesh != null)
                {
                    Destroy(data.mesh);
                    data.mesh = null;
                }

                data.state = ChunkState.Unloaded;
                chunks.Remove(pos);
            }
        }

        // Queue of chunks waiting to be regenerated after terrain deformation.
        // regenerationQueueSet mirrors the queue contents for O(1) duplicate checks.
        private readonly Queue<Vector3Int>   regenerationQueue    = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> regenerationQueueSet = new HashSet<Vector3Int>();

        // Number of rebuild jobs currently in-flight. Multiple rebuilds run concurrently —
        // one per free rebuild slot in MarchingCubesAdapter.
        private int activeRebuildCount;

        /// <summary>
        /// Queues dirty chunks for mesh regeneration. Called by TerrainDeformer after
        /// modifying density values. Duplicates are ignored in O(1) via a shadow HashSet.
        /// </summary>
        public void RegenerateDirtyChunks(HashSet<Vector3Int> dirtyPositions)
        {
            foreach (Vector3Int pos in dirtyPositions)
            {
                if (regenerationQueueSet.Add(pos))
                    regenerationQueue.Enqueue(pos);
            }
        }

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("ChunkManager.Update");

        // Cached face mask to avoid recomputing every frame
        private int lastFaceMask = 0x3F; // all visible initially
        private Vector3 lastCameraFwd;

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            ProcessRegenerationQueue();
            ProcessPendingOreJobs();
            ProcessPendingUV2Jobs();
            // Occlusion culling: only run every 10 frames and only when camera moves
            if (Time.frameCount % 10 == 0)
                UpdateOcclusionCulling();
            RuntimeProfiler.End(s_prof);
        }

        /// <summary>
        /// Dispatches queued chunk rebuilds asynchronously, filling all free rebuild slots.
        /// Multiple rebuilds can run concurrently — one per free slot in MarchingCubesAdapter.
        /// Each completed rebuild re-invokes this method to immediately start the next pending chunk.
        /// </summary>
        private void ProcessRegenerationQueue()
        {
            while (regenerationQueue.Count > 0)
            {
                Vector3Int chunkPos = regenerationQueue.Peek();
                ChunkData  data     = GetChunk(chunkPos);

                if (data == null)
                {
                    regenerationQueue.Dequeue();
                    regenerationQueueSet.Remove(chunkPos);
                    continue;
                }

                // Capture locals for the closure — Vector3Int is a value type so the copy is free.
                Vector3Int capturedPos  = chunkPos;
                ChunkData  capturedData = data;

                bool started = meshBuilder.RebuildChunkMeshAsync(capturedData, () =>
                {
                    // Fires on main thread when async GPU readback + biome colors complete.
                    if (chunkObjects.TryGetValue(capturedPos, out GameObject chunkGO))
                    {
                        var rnd = chunkGO.GetComponent<ChunkRenderer>();
                        rnd.ApplyMesh(capturedData.mesh);
                    }

                    // Notify decoration manager that this chunk's terrain changed
                    if (decorationManager != null)
                    {
                        _singleChunkSet.Clear();
                        _singleChunkSet.Add(capturedPos);
                        decorationManager.OnChunksRebuilt(_singleChunkSet);
                    }

                    activeRebuildCount--;
                    ProcessRegenerationQueue(); // fill any slot that just freed up
                });

                if (!started) break; // all rebuild slots busy — try again next frame

                activeRebuildCount++;
                regenerationQueue.Dequeue();
                regenerationQueueSet.Remove(chunkPos);
            }
        }

        // -------------------------------------------------------------------------
        //  Deferred ore finalization — heavy work (terrain types, decorations, UV2)
        //  is spread across frames to prevent spikes when many chunks complete at once.
        // -------------------------------------------------------------------------

        private readonly List<ChunkData> pendingOreFinalization = new List<ChunkData>();

        // Reused across FinalizeOreChunk calls to avoid per-frame allocations.
        private readonly byte[][] _cachedNeighborFields = new byte[6][];
        // Temporary expanded ore array for NativeArray interop (avoid per-frame allocation)
        private byte[] _cachedOreFieldExpanded;
        private readonly List<Vector3> _cachedVertexList = new List<Vector3>(2048);
        private readonly HashSet<Vector3Int> _singleChunkSet = new HashSet<Vector3Int>();

        // Async UV2 pipeline — jobs are scheduled in FinalizeOreChunk and completed
        // on the next Update tick to avoid blocking the main thread.
        private struct PendingUV2Job
        {
            public ChunkData chunk;
            public ChunkMeshBuilder.OreUV2JobData jobData;
        }
        private readonly List<PendingUV2Job> pendingUV2Jobs = new List<PendingUV2Job>();

        /// <summary>
        /// Phase 1: Polls pending ore generation jobs. When a job is complete,
        /// copies OreField data and disposes NativeArrays immediately (prevents
        /// TempJob leak warnings). Queues the chunk for deferred finalization.
        /// </summary>
        private void ProcessPendingOreJobs()
        {
            for (int i = pendingOreJobs.Count - 1; i >= 0; i--)
            {
                PendingOreJob job = pendingOreJobs[i];

                if (!job.handle.IsCompleted) continue;
                if (!job.visibilityHandle.IsCompleted) continue;

                // Complete() must be called even when IsCompleted is true.
                job.handle.Complete();
                job.visibilityHandle.Complete();

                // Copy ore + biome data back if the chunk is still tracked.
                if (chunks.TryGetValue(job.chunk.chunkPosition, out ChunkData live) && live == job.chunk)
                {
                    job.chunk.OreField.CopyFrom(job.oreField);
                    job.chunk.BiomeField.CopyFrom(job.biomeField);
                    job.chunk.visibilityGraph = job.visibilityResult[0];
                    pendingOreFinalization.Add(job.chunk);
                }

                // Always dispose NativeArrays immediately to avoid TempJob leak warnings.
                job.oreField.Dispose();
                job.biomeField.Dispose();
                job.densityNative.Dispose();
                job.surfaceHeightsNative.Dispose();
                job.climateColumnsNative.Dispose();
                job.oreParamsNative.Dispose();
                job.lookupNative.Dispose();
                job.nativeTree.Dispose();
                if (job.visibilityResult.IsCreated) job.visibilityResult.Dispose();

                // Swap-with-last removal — O(1) instead of O(n) RemoveAt shift.
                int lastOre = pendingOreJobs.Count - 1;
                if (i < lastOre) pendingOreJobs[i] = pendingOreJobs[lastOre];
                pendingOreJobs.RemoveAt(lastOre);
            }

            // Phase 2: Process deferred ore finalization with a per-frame budget.
            // This is the heavy work: decorations, UV2, mesh activation.
            // Sort by LOD (LOD0 first) then distance to player (closest first) so nearby
            // terrain always activates before distant terrain — prevents gaps and fall-through.
            if (pendingOreFinalization.Count > 1)
            {
                _oreSortPlayerPos = Vector3.zero;
                if (PlayerManager.Instance != null)
                    _oreSortPlayerPos = PlayerManager.Instance.PlayerTransform.position;
                else if (Camera.main != null)
                    _oreSortPlayerPos = Camera.main.transform.position;

                pendingOreFinalization.Sort(_oreFinalizationComparison);
            }

            int finalized = 0;
            int oreIdx = 0;
            while (oreIdx < pendingOreFinalization.Count)
            {
                if (maxOreCompletionsPerFrame > 0 && finalized >= maxOreCompletionsPerFrame)
                    break; // defer remaining to next frame

                ChunkData chunk = pendingOreFinalization[oreIdx];
                oreIdx++;

                // Guard: chunk may have been unloaded while waiting in the queue
                if (!chunks.TryGetValue(chunk.chunkPosition, out ChunkData live) || live != chunk)
                    continue;

                FinalizeOreChunk(chunk);
                finalized++;
            }
            // Remove processed entries in one bulk operation instead of per-item RemoveAt(0)
            if (oreIdx > 0)
                pendingOreFinalization.RemoveRange(0, oreIdx);
        }

        /// <summary>
        /// Per-chunk finalization phase 1: surface point ore types + UV2 job scheduling.
        /// The UV2 Burst job runs asynchronously; completion and mesh activation happen
        /// in ProcessPendingUV2Jobs on the next frame(s).
        /// </summary>
        private void FinalizeOreChunk(ChunkData chunk)
        {
            // Terrain types (grass/dirt/rock) are now assigned by VoxelClassificationJob
            // alongside biome and ore data — no separate PlaceTerrainTypes call needed.

            Vector3Int cp = chunk.chunkPosition;

            if (chunk.lodLevel <= 1)
                UpdateSurfacePointOreTypes(chunk);

            // Recompute biome vertex colors now that BiomeField is properly populated.
            // The initial ComputeBiomeData in the mesh builder ran before voxel
            // classification, so it used a stale/empty biome field.
            Mesh mesh = chunk.mesh;
            if (mesh != null && chunk.BiomeField != null)
            {
                Vector3[] verts = mesh.vertices;
                byte[] biomeArr = chunk.BiomeField.ToArray();
                ChunkMeshBuilder.ComputeBiomeData(verts, chunk.WorldPosition,
                    biomeArr, out Color[] texWeights, out Vector4[] tints);
                mesh.colors = texWeights;
                mesh.SetUVs(3, tints);
            }

            // Schedule ore UV2 asynchronously. The old LOD mesh stays on screen until
            // the job completes and we swap in the new mesh with UV2 baked — no flicker.
            if (mesh != null && chunk.OreField != null)
            {
                _cachedVertexList.Clear();
                mesh.GetVertices(_cachedVertexList);
                byte[] oreField = chunk.OreField.ToArray();

                for (int n = 0; n < 6; n++)
                {
                    ChunkData neighbor = GetChunk(cp + OreNeighborOffsets[n]);
                    _cachedNeighborFields[n] = neighbor?.OreField?.ToArray();
                }

                var oreNeighbors = new ChunkMeshBuilder.NeighborOreFields
                {
                    fields = _cachedNeighborFields
                };

                var jobData = ChunkMeshBuilder.ScheduleOreUV2Job(_cachedVertexList, oreField, oreNeighbors);
                pendingUV2Jobs.Add(new PendingUV2Job { chunk = chunk, jobData = jobData });

                // Clear references so we don't hold neighbor ore fields longer than needed
                for (int n = 0; n < 6; n++)
                    _cachedNeighborFields[n] = null;
            }
            else
            {
                // No mesh or no ore data — activate immediately without UV2.
                ActivateChunk(chunk);
            }
        }

        /// <summary>
        /// Polls pending UV2 jobs and activates chunks once their Burst job completes.
        /// Budgeted to maxOreCompletionsPerFrame activations to prevent spikes when
        /// many UV2 jobs finish simultaneously.
        /// </summary>
        private void ProcessPendingUV2Jobs()
        {
            int activated = 0;
            for (int i = pendingUV2Jobs.Count - 1; i >= 0; i--)
            {
                PendingUV2Job pending = pendingUV2Jobs[i];

                if (!pending.jobData.Handle.IsCompleted) continue;

                // Complete and apply UV2 data
                Vector2[] uv2 = pending.jobData.Complete();

                // Guard: chunk may have been unloaded while the job was in flight
                if (chunks.TryGetValue(pending.chunk.chunkPosition, out ChunkData live)
                    && live == pending.chunk && pending.chunk.mesh != null)
                {
                    pending.chunk.mesh.uv2 = uv2;
                    ActivateChunk(pending.chunk);
                    activated++;
                }

                // Swap-with-last removal — O(1)
                int last = pendingUV2Jobs.Count - 1;
                if (i < last) pendingUV2Jobs[i] = pendingUV2Jobs[last];
                pendingUV2Jobs.RemoveAt(last);

                // Budget: defer remaining activations to next frame
                if (maxOreCompletionsPerFrame > 0 && activated >= maxOreCompletionsPerFrame)
                    break;
            }
        }

        /// <summary>
        /// Applies the mesh (with UV2 baked) to the chunk GameObject and activates it.
        /// Shared by the async UV2 path and the no-mesh fast path.
        /// </summary>
        private void ActivateChunk(ChunkData chunk)
        {
            // LOD0: compact vertex format using dedicated shader variant
            if (chunk.lodLevel == 0 && chunk.mesh != null)
            {
                try
                {
                    CompactVertex.CompactMesh(chunk.mesh, chunk.skyExposure);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ChunkManager] CompactMesh failed: {e.Message}");
                }
            }
            else if (chunk.mesh != null && chunk.skyExposure != null)
            {
                // LOD1+: inject sky exposure into UV2.z for the standard shader.
                // UV2 is currently Vector2 (ore data); expand to Vector4 to carry sky exposure.
                var uv2 = chunk.mesh.uv2;
                int count = chunk.mesh.vertexCount;
                var uv2Expanded = new Vector4[count];
                float[] skyExp = chunk.skyExposure;
                for (int i = 0; i < count; i++)
                {
                    Vector2 ore = (uv2 != null && i < uv2.Length) ? uv2[i] : Vector2.zero;
                    float sky = (i < skyExp.Length) ? skyExp[i] : 1f;
                    uv2Expanded[i] = new Vector4(ore.x, ore.y, sky, 0f);
                }
                chunk.mesh.SetUVs(2, uv2Expanded);
            }

            GameObject chunkGO  = CreateChunkGameObject(chunk);
            var        renderer = chunkGO.GetComponent<ChunkRenderer>();
            Mesh oldMesh = renderer.CurrentMesh;

            // LOD0 uses compact shader variant, LOD1+ uses standard shader
            if (chunk.lodLevel == 0 && compactChunkMaterial != null)
                renderer.SetMaterial(compactChunkMaterial);
            else
                renderer.SetMaterial(defaultChunkMaterial);

            renderer.ApplyMesh(chunk.mesh);
            chunkGO.SetActive(true);
            if (oldMesh != null && oldMesh != chunk.mesh)
                Destroy(oldMesh);

            chunk.state = ChunkState.Active;

            // Region batching disabled — causes z-fighting double rendering.
            // if (chunk.lodLevel >= 1)
            //     regionManager.OnChunkActivated(chunk);

            if (chunk.lodLevel == 0)
            {
                decorationManager?.OnChunkActivated(chunk, chunk.mesh);
                BuildingManager.Instance?.OnChunkColumnActivated(chunk.chunkPosition);
            }
            else if (chunk.lodLevel <= 3)
                decorationManager?.OnBillboardChunkActivated(chunk);
        }

        /// <summary>
        /// Patches ore types on pre-computed surface points using the finalized OreField.
        /// </summary>
        private static void UpdateSurfacePointOreTypes(ChunkData chunk)
        {
            if (chunk.surfacePoints == null || chunk.OreField == null) return;

            const int SIZE = ChunkData.SIZE;
            Vector3 origin = chunk.WorldPosition;

            for (int i = 0; i < chunk.surfacePoints.Length; i++)
            {
                Vector3 local = chunk.surfacePoints[i].worldPos - origin;
                int ox = Mathf.Clamp(Mathf.FloorToInt(local.x), 0, SIZE - 1);
                int oy = Mathf.Clamp(Mathf.FloorToInt(local.y), 0, SIZE - 1);
                int oz = Mathf.Clamp(Mathf.FloorToInt(local.z), 0, SIZE - 1);

                var pt = chunk.surfacePoints[i];
                pt.oreType = chunk.OreField.Get(ox + oy * SIZE + oz * SIZE * SIZE);
                chunk.surfacePoints[i] = pt;
            }
        }

        /// <summary>
        /// Returns the total number of currently tracked chunks.
        /// </summary>
        public int ChunkCount => chunks.Count;

        /// <summary>
        /// Returns the number of active chunk GameObjects.
        /// </summary>
        public int ActiveChunkObjectCount => chunkObjects.Count;

        /// <summary>Number of free async generation slots on the GPU.</summary>
        public int FreeGenerationSlots => marchingCubesAdapter?.FreeSlotCount ?? 0;

        /// <summary>Total async generation slots.</summary>
        public int TotalGenerationSlots => marchingCubesAdapter?.TotalSlotCount ?? 0;

        /// <summary>
        /// Cancel in-flight GPU generation for a chunk and revert it to Unloaded state.
        /// The GPU readback still completes but the expensive mesh build is skipped and
        /// the slot is freed immediately.
        /// Returns true if a generation was cancelled.
        /// </summary>
        public bool CancelChunkGeneration(Vector3Int chunkPos)
        {
            if (!chunks.TryGetValue(chunkPos, out ChunkData data)) return false;
            if (data.state != ChunkState.Generating) return false;

            bool cancelled = marchingCubesAdapter?.CancelGeneration(data) ?? false;
            if (cancelled)
            {
                data.state = ChunkState.Unloaded;
            }
            return cancelled;
        }

        // Reusable list to avoid allocations during cancel sweep
        private readonly List<Vector3Int> _cancelCandidates = new List<Vector3Int>();

        /// <summary>
        /// Cancels in-flight generations in a single dictionary scan:
        /// - Any generating chunk beyond maxDist is cancelled outright.
        /// - LOD0 generating chunks beyond lod0Dist (but within maxDist) are cancelled
        ///   to free GPU slots for chunks now truly closest to the player.
        /// Returns the number of cancelled generations.
        /// </summary>
        public int CancelStaleGenerations(Vector3Int playerChunk,
            DirectionalDistance maxDist, DirectionalDistance lod0Dist)
        {
            _cancelCandidates.Clear();
            _chunkDataKeyCache.Clear();
            foreach (var key in chunks.Keys) _chunkDataKeyCache.Add(key);

            for (int i = 0; i < _chunkDataKeyCache.Count; i++)
            {
                Vector3Int pos = _chunkDataKeyCache[i];
                if (!chunks.TryGetValue(pos, out ChunkData cd)) continue;
                if (cd.state != ChunkState.Generating) continue;

                int dx = pos.x - playerChunk.x;
                int dy = pos.y - playerChunk.y;
                int dz = pos.z - playerChunk.z;

                // Beyond max LOD range — cancel any LOD level
                if (!maxDist.Contains(dx, dy, dz))
                {
                    _cancelCandidates.Add(pos);
                    continue;
                }

                // LOD0 outside its effective range — cancel to free slots for closer chunks
                if (cd.lodLevel == 0 && !lod0Dist.Contains(dx, dy, dz))
                    _cancelCandidates.Add(pos);
            }

            int cancelled = 0;
            for (int i = 0; i < _cancelCandidates.Count; i++)
            {
                if (CancelChunkGeneration(_cancelCandidates[i]))
                    cancelled++;
            }
            return cancelled;
        }

        /// <summary>
        /// Creates a Material using the Voidborne/TriplanarTerrain shader with
        /// default biome color tints from the BiomeRegistry. If the shader is not
        /// found, falls back to URP/Lit.
        /// </summary>
        private Material CreateTriplanarMaterial()
        {
            Shader shader = Shader.Find(TriplanarShaderName);

            if (shader == null)
            {
                Debug.LogWarning($"[ChunkManager] Could not find shader '{TriplanarShaderName}'. Falling back to URP/Lit.");
                shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    Debug.LogError("[ChunkManager] Could not find fallback URP/Lit shader either. Using error shader.");
                    return new Material(Shader.Find("Hidden/InternalErrorShader"));
                }

                return new Material(shader) { color = Color.gray };
            }

            Material mat = new Material(shader);
            mat.name = "TriplanarTerrain (Auto)";

            // Set texture scale and blend sharpness
            mat.SetFloat("_TexScale", triplanarTexScale);
            mat.SetFloat("_BlendSharpness", triplanarBlendSharpness);

            // Biome tints are now per-vertex (UV3), no material properties needed.

            Debug.Log("[ChunkManager] Created triplanar terrain material with biome colors.");
            return mat;
        }

        // =========================================================================
        //  LOD4 GREEDY MESH PATH — extreme distance, no GPU marching cubes
        // =========================================================================

        private Material distantTerrainMaterial;

        /// <summary>
        /// Generates a LOD4 chunk using the CPU greedy mesher instead of GPU marching cubes.
        /// Uses the density function directly (no GPU density readback needed).
        /// Returns true (always submits successfully since it runs on CPU).
        /// </summary>
        private bool LoadChunkGreedy(ChunkData data, Vector3Int chunkPos, Mesh oldMesh)
        {
            data.state = ChunkState.Generating;

            // Ensure distant material exists
            if (distantTerrainMaterial == null)
            {
                Shader distShader = Shader.Find("Voidborne/DistantTerrain");
                if (distShader != null)
                    distantTerrainMaterial = new Material(distShader) { name = "DistantTerrain (Auto)" };
                else
                    distantTerrainMaterial = defaultChunkMaterial; // fallback
            }

            // Sample density on background thread using the density function
            var capturedData = data;
            var capturedPos = chunkPos;
            var ctx = System.Threading.SynchronizationContext.Current;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                const int SIZE = ChunkData.SIZE;
                const int VOL = SIZE * SIZE * SIZE;
                int step = 8;

                // Fill density field from DensityFunction
                float worldX = capturedData.chunkPosition.x * SIZE;
                float worldY = capturedData.chunkPosition.y * SIZE;
                float worldZ = capturedData.chunkPosition.z * SIZE;

                // Only sample density at coarse grid positions (step=8 → 4³=64 samples
                // instead of 32³=32768). The greedy mesher only reads these positions anyway.
                // Fill non-sampled positions with 0 (air) to avoid stale data.
                System.Array.Clear(capturedData.densityField, 0, capturedData.densityField.Length);
                for (int cz = 0; cz < SIZE; cz += step)
                for (int cy = 0; cy < SIZE; cy += step)
                for (int cx = 0; cx < SIZE; cx += step)
                {
                    // Sample at the center of each coarse voxel
                    int sx = Mathf.Min(cx + step / 2, SIZE - 1);
                    int sy = Mathf.Min(cy + step / 2, SIZE - 1);
                    int sz = Mathf.Min(cz + step / 2, SIZE - 1);
                    var worldPos = new Unity.Mathematics.float3(worldX + sx, worldY + sy, worldZ + sz);
                    capturedData.densityField[sx + sy * SIZE + sz * SIZE * SIZE] =
                        DensityFunction.GetDensity(worldPos);
                }

                // Fill biome field (simple: use center biome for the whole chunk)
                byte centerBiome = 0;
                var biomeTree = BiomeMap.GetBiomeTree();
                if (biomeTree != null)
                {
                    var centerXZ = new Unity.Mathematics.float2(worldX + SIZE / 2f, worldZ + SIZE / 2f);
                    var climate = BiomeMap.SampleClimate(centerXZ);
                    centerBiome = biomeTree.FindClosest(climate);
                }

                // Use center biome for all voxels in LOD4 (fine for distant view)
                capturedData.BiomeField.Fill(centerBiome);

                // Build biome color lookup (plain C# array — safe for background thread)
                var biomeColorLookup = new Color[256];
                var biomesList = BiomeMap.GetBiomes();
                if (biomesList != null)
                {
                    foreach (var b in biomesList)
                    {
                        if (b != null && b.biomeIdByte < 256)
                            biomeColorLookup[b.biomeIdByte] = b.colorTint;
                    }
                }

                // Run greedy mesher (pure C# — no NativeArrays)
                var biomeArr = capturedData.BiomeField.ToArray();
                var result = GreedyMesher.Generate(
                    capturedData.densityField, biomeArr, step, biomeColorLookup);

                // Post to main thread for mesh creation
                ctx.Post(_ =>
                {
                    if (!chunks.ContainsKey(capturedPos) || chunks[capturedPos] != capturedData)
                        return;

                    Mesh mesh = null;
                    if (result.vertices != null && result.vertices.Length > 0)
                    {
                        mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        mesh.SetVertices(result.vertices);
                        mesh.SetTriangles(result.indices, 0);
                        mesh.SetNormals(result.normals);
                        mesh.SetColors(result.colors);
                        mesh.RecalculateBounds();
                    }

                    capturedData.mesh = mesh;

                    if (oldMesh != null && oldMesh != mesh)
                        Destroy(oldMesh);

                    // Activate directly (no ore/UV2 for LOD4)
                    GameObject chunkGO = CreateChunkGameObject(capturedData);
                    var renderer = chunkGO.GetComponent<ChunkRenderer>();
                    renderer.SetMaterial(distantTerrainMaterial);
                    renderer.ApplyMesh(mesh);
                    chunkGO.SetActive(true);
                    capturedData.state = ChunkState.Active;
                }, null);
                }
                catch (System.Exception e)
                {
                    ctx.Post(_ =>
                    {
                        Debug.LogWarning($"[ChunkManager] LOD4 greedy mesh failed: {e.Message}");
                        capturedData.state = ChunkState.Unloaded;
                    }, null);
                }
            });

            return true;
        }

        // =========================================================================
        //  OCCLUSION CULLING — Minecraft-style cave culling via visibility graphs
        // =========================================================================

        /// <summary>
        /// Runs BFS occlusion culling from the camera chunk outward.
        /// Chunks not reachable through connected air volumes have their MeshRenderer disabled.
        /// Only re-runs when the camera crosses a chunk boundary.
        /// </summary>
        private void UpdateOcclusionCulling()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3Int cameraChunk = ChunkCoordUtility.WorldToChunkPos(cam.transform.position);

            // Only update when camera crosses chunk boundary
            if (hasLastOcclusionCamera && cameraChunk == lastOcclusionCameraChunk)
                return;

            lastOcclusionCameraChunk = cameraChunk;
            hasLastOcclusionCamera = true;

            // Run BFS visibility traversal
            occlusionCuller.UpdateVisibility(cameraChunk, chunks, MaxDistance);

            // Apply visibility: only HIDE chunks the BFS proves are fully occluded.
            // Chunks not in the loaded set or not visited by BFS stay visible (safe default).
            foreach (var kvp in chunkObjects)
            {
                ChunkData data = GetChunk(kvp.Key);
                if (data == null) continue;

                // Never cull chunks with no mesh (empty air) — nothing to hide
                if (data.mesh == null || data.mesh.vertexCount == 0) continue;

                // Never cull chunks that haven't had their visibility graph computed yet
                // (default 0xFFFF = all connected = visible)
                if (data.visibilityGraph == 0xFFFF) continue;

                ChunkRenderer rnd = kvp.Value.GetComponent<ChunkRenderer>();
                if (rnd != null)
                    rnd.SetVisible(occlusionCuller.IsVisible(kvp.Key));
            }
        }

        /// <summary>
        /// Updates per-face backface culling for LOD0 chunks.
        /// Only recalculates when camera direction changes significantly.
        /// </summary>
        private void UpdateFaceCulling()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 camFwd = cam.transform.forward;

            // Only update when camera direction changes by >15 degrees
            if (Vector3.Dot(camFwd, lastCameraFwd) > 0.966f) // cos(15°) ≈ 0.966
                return;

            lastCameraFwd = camFwd;
            int faceMask = DirectionalSubmeshBuilder.GetVisibleFaceMask(camFwd);

            if (faceMask == lastFaceMask) return;
            lastFaceMask = faceMask;

            // Rebuild index buffers for LOD0 chunks with face buckets
            foreach (var kvp in chunkObjects)
            {
                ChunkData data = GetChunk(kvp.Key);
                if (data == null || data.lodLevel != 0 || data.faceBuckets == null) continue;
                if (data.mesh == null) continue;

                DirectionalSubmeshBuilder.RebuildIndices(data.mesh, data.faceBuckets, faceMask);
            }
        }

        /// <summary>
        /// Invalidate occlusion culling (e.g. after terrain deformation).
        /// Forces a full BFS recompute on the next frame.
        /// </summary>
        public void InvalidateOcclusionCulling()
        {
            occlusionCuller.Invalidate();
            hasLastOcclusionCamera = false;
        }
    }
}
