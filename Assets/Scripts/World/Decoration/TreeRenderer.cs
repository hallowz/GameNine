using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Voidborne.Diagnostics;
using Voidborne.World.Biomes;
using Voidborne.World.Chunks;
using Voidborne.World.Generation;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// GPU-instanced tree renderer with compute-shader frustum + distance culling.
    /// Mirrors the GrassRenderer pattern — all per-tree culling runs on the GPU.
    ///
    /// Architecture:
    ///   CPU: generates per-tree instance data on chunk load, sub-allocated in persistent per-variant GPU buffers
    ///   GPU (compute): frustum + distance culling, sorts trees into near + far append buffers
    ///   GPU (vertex/fragment): instanced tree mesh rendering + billboard LOD
    ///
    /// Buffer management: incremental sub-allocation (same as GrassRenderer).
    ///   Chunk activate  → partial SetData (only new chunk's trees per variant)
    ///   Chunk deactivate → invalidate slot (zero scale), periodic compaction
    ///   No full-buffer rebuild on every chunk change.
    ///
    /// Each tree variant gets its own compute dispatch and draw calls.
    /// Total: up to 18 draw calls (6 variants × 3: trunk + foliage + billboard).
    ///
    /// Meshes are extracted from prefabs at startup by combining child meshes
    /// (CombineMeshes) into one trunk mesh and one foliage mesh per variant.
    /// </summary>
    public class TreeRenderer : MonoBehaviour
    {
        public static TreeRenderer Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Tree Prefabs (0=Broadleaf, 1=Pine, 2=Scrub, 3=Windswept, 4=Shimmerleaf, 5=SkyCedar)")]
        [SerializeField] private GameObject[] treePrefabs = new GameObject[6];

        [Header("Source Materials (properties are copied to instanced materials)")]
        [SerializeField] private Material sourceTrunkMaterial;
        [SerializeField] private Material sourceFoliageMaterial;

        [Header("Instanced Shader & Compute")]
        [SerializeField] private Shader treeInstanceShader;
        [SerializeField] private ComputeShader cullingShader;

        [Header("Draw Distance")]
        [SerializeField] private float maxDrawDistance = 80f;
        [Tooltip("Beyond maxDrawDistance, trees render as camera-facing billboards. " +
                 "Distance is auto-set from ChunkManager's max horizontal render distance.")]
        [SerializeField] private float maxBillboardDistance = 180f;

        [Header("Shadow Distance")]
        [Tooltip("Trees beyond this distance skip shadow casting. Lower = better FPS.")]
        [SerializeField] private float maxShadowDistance = 40f;

        [Header("Billboard LOD")]
        [SerializeField] private Shader billboardShader;
        [Tooltip("Per-variant billboard textures (alpha-cutout). If empty, a default circle shape is generated.")]
        [SerializeField] private Texture2D[] billboardTextures = new Texture2D[6];
        [Tooltip("Extra padding multiplier on auto-computed billboard dimensions.")]
        [SerializeField] private float billboardPadding = 1.15f;

        // ── Constants ──────────────────────────────────────────────────────
        private const int VariantCount = 6;
        private const int ScanGridSize = 16;

        // WorldSeed channel for tree placement randomness
        private const int ChannelTreePlacement = 25;

        // Resource paths for auto-loading billboard textures (relative to Resources/)
        private static readonly string[] BillboardResourcePaths = {
            "Trees/BillboardBroadleaf",
            "Trees/BillboardPine",
            "Trees/BillboardScrub",
            "Trees/BillboardWindswept",
            "Trees/BillboardShimmerleaf",
            "Trees/BillboardSkyCedar"
        };

        // ── Per-biome tree configs ──────────────────────────────────────────
        private struct TreeWeight
        {
            public int variantIndex;
            public float weight;
            public TreeWeight(int v, float w) { variantIndex = v; weight = w; }
        }

        private static readonly Dictionary<int, (float density, TreeWeight[] weights)> BiomeTreeConfig
            = new Dictionary<int, (float, TreeWeight[])>
        {
            { 4,  (0.035f, new[] { new TreeWeight(0, 0.70f), new TreeWeight(2, 0.30f) }) },
            { 5,  (0.055f, new[] { new TreeWeight(0, 0.55f), new TreeWeight(1, 0.30f), new TreeWeight(2, 0.15f) }) },
            { 6,  (0.040f, new[] { new TreeWeight(0, 0.60f), new TreeWeight(2, 0.40f) }) },
            { 7,  (0.030f, new[] { new TreeWeight(1, 0.60f), new TreeWeight(0, 0.25f), new TreeWeight(2, 0.15f) }) },
            { 8,  (0.012f, new[] { new TreeWeight(2, 0.80f), new TreeWeight(1, 0.20f) }) },
            { 9,  (0.022f, new[] { new TreeWeight(1, 0.50f), new TreeWeight(2, 0.35f), new TreeWeight(0, 0.15f) }) },
            { 10, (0.018f, new[] { new TreeWeight(2, 0.55f), new TreeWeight(0, 0.45f) }) },
            { 11, (0.018f, new[] { new TreeWeight(1, 0.55f), new TreeWeight(2, 0.45f) }) },
            // New surface biomes
            { 12, (0.015f, new[] { new TreeWeight(1, 0.70f), new TreeWeight(2, 0.30f) }) },  // Alpine Peaks: sparse pine + scrub
            { 13, (0.045f, new[] { new TreeWeight(0, 0.50f), new TreeWeight(1, 0.30f), new TreeWeight(2, 0.20f) }) }, // Grand Hills
            { 14, (0.020f, new[] { new TreeWeight(1, 0.55f), new TreeWeight(2, 0.30f), new TreeWeight(0, 0.15f) }) }, // Towering Bluffs
        };

        // Sky tree config: used when surface point Y > SkyStartY, overriding biome config.
        // Mixes all three sky variants: Windswept (3), Shimmerleaf (4), Sky Cedar (5).
        private const float SkyStartY = 100f;
        private static readonly (float density, TreeWeight[] weights) SkyTreeConfig
            = (0.035f, new[] {
                new TreeWeight(3, 0.35f), // Windswept — hardy, wind-bent
                new TreeWeight(4, 0.35f), // Shimmerleaf — ethereal, drooping
                new TreeWeight(5, 0.30f), // Sky Cedar — massive, ancient
            });

        // ── Instance data ──────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct TreeInstanceData
        {
            public Vector3 position;       // 12 bytes
            public Quaternion rotation;    // 16 bytes
            public float scale;            // 4  bytes
        }                                  // 32 bytes total
        private static readonly int Stride = 32;

        // ── Per-chunk storage ──────────────────────────────────────────────
        private class TreeChunkData
        {
            public Vector3Int chunkPos;
            public List<TreeInstanceData>[] perVariant;

            public TreeChunkData(Vector3Int pos)
            {
                chunkPos = pos;
                perVariant = new List<TreeInstanceData>[VariantCount];
                for (int i = 0; i < VariantCount; i++)
                    perVariant[i] = new List<TreeInstanceData>();
            }
        }

        private readonly Dictionary<Vector3Int, TreeChunkData> _treeChunks
            = new Dictionary<Vector3Int, TreeChunkData>();

        // ── Tree removal tracking ──────────────────────────────────────────
        private readonly HashSet<Vector2Int> _removedTrees = new HashSet<Vector2Int>();

        // ── Meshes extracted from prefabs ──────────────────────────────────
        private Mesh[] _trunkMeshes;
        private Mesh[] _foliageMeshes;

        // ── Runtime materials ──────────────────────────────────────────────
        private Material _trunkMat;
        private Material _foliageMat;

        // ── Sub-allocated GPU buffers (per-variant) ─────────────────────────
        // Each variant has a persistent buffer. Chunk activate → partial SetData.
        // Chunk deactivate → invalidate slot (scale=0). Periodic compaction.
        private ComputeBuffer[] _allTreesBuffers;
        private int[] _bufferCapacities;       // allocated size per variant
        private int[] _bufferHighWaters;       // end of used region per variant
        private int[] _liveTreeCounts;         // trees that are alive per variant
        private int[] _deadTreeCounts;         // invalidated trees per variant

        // Per-variant slot maps: chunkPos → (offset, count) in the variant's buffer
        private Dictionary<Vector3Int, (int offset, int count)>[] _slotMaps;

        // Reusable zero array for invalidating freed slots
        private TreeInstanceData[] _zeroArray;
        private const int ZERO_ARRAY_SIZE = 2048;

        // Compaction threshold: rebuild variant when >30% of buffer is dead trees
        private const float CompactionThreshold = 0.30f;

        // CPU mirror for buffer resize (only used during resize/compaction)
        private TreeInstanceData[][] _cpuMirrors;

        // Append + args buffers (per-variant)
        private ComputeBuffer[] _visibleBuffers;
        private ComputeBuffer[] _trunkArgs;
        private ComputeBuffer[] _foliageArgs;
        private int[] _appendCapacities;

        // Far LOD buffers (billboard band)
        private ComputeBuffer[] _visibleFarBuffers;
        private ComputeBuffer[] _billboardArgs;
        private int[] _farAppendCapacities;

        // Billboard resources
        private Mesh _billboardMesh;
        private Material _billboardMat;
        private Texture2D _defaultBillboardTex;
        private float[] _billboardWidths;
        private float[] _billboardHeights;

        private MaterialPropertyBlock[] _trunkMPBs;
        private MaterialPropertyBlock[] _foliageMPBs;
        private MaterialPropertyBlock[] _billboardMPBs;

        private int _csCombinedKernel;

        // Cached camera reference — avoids Camera.main property lookup each frame
        private Camera _cachedCam;

        private readonly uint[] _argsTemplate = new uint[5];
        private Vector4[] _planeVec4 = new Vector4[6];
        private readonly Plane[] _frustumPlanesArr = new Plane[6];

        // ── Shader property IDs ────────────────────────────────────────────
        private static readonly int PropAllTrees      = Shader.PropertyToID("_AllTrees");
        private static readonly int PropNearOut       = Shader.PropertyToID("_NearOut");
        private static readonly int PropFarOut        = Shader.PropertyToID("_FarOut");
        private static readonly int PropCamPos        = Shader.PropertyToID("_CamPos");
        private static readonly int PropNearDistSq    = Shader.PropertyToID("_NearDistSq");
        private static readonly int PropFarDistSq     = Shader.PropertyToID("_FarDistSq");
        private static readonly int PropFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int PropTreeCount     = Shader.PropertyToID("_TreeCount");
        private static readonly int PropTreeBuffer    = Shader.PropertyToID("_TreeBuffer");
        private static readonly int PropBillboardWidth  = Shader.PropertyToID("_BillboardWidth");
        private static readonly int PropBillboardHeight = Shader.PropertyToID("_BillboardHeight");
        private static readonly int PropMainTex         = Shader.PropertyToID("_MainTex");

        // Crossfade band for LOD transition (prevents flickering)
        private static readonly int PropCrossfadeBandSq   = Shader.PropertyToID("_CrossfadeBandSq");
        private static readonly int PropGlobalNearDistSq  = Shader.PropertyToID("_TreeNearDistSq");
        private static readonly int PropGlobalCrossfadeSq  = Shader.PropertyToID("_TreeCrossfadeBandSq");
        private static readonly int PropGlobalFarDistSq    = Shader.PropertyToID("_TreeFarDistSq");
        private static readonly int PropGlobalFarFadeStartSq = Shader.PropertyToID("_TreeFarFadeStartSq");
        private const float CrossfadeBand = 8f; // metres of overlap between near and far
        private const float FarFadeBand   = 25f; // metres of fade-out at billboard far edge

        // Shadow distance globals (set on materials, read by ShadowCaster pass)
        private static readonly int PropShadowCamPos    = Shader.PropertyToID("_ShadowCamPos");
        private static readonly int PropShadowMaxDistSq = Shader.PropertyToID("_ShadowMaxDistSq");

        // ── Tree hit result for raycasting ─────────────────────────────────

        public struct TreeHitResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public int variantIndex;
            public float scale;
            public Vector3Int chunkPos;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Allocate per-variant arrays
            _trunkMeshes        = new Mesh[VariantCount];
            _foliageMeshes      = new Mesh[VariantCount];
            _allTreesBuffers    = new ComputeBuffer[VariantCount];
            _bufferCapacities   = new int[VariantCount];
            _bufferHighWaters   = new int[VariantCount];
            _liveTreeCounts     = new int[VariantCount];
            _deadTreeCounts     = new int[VariantCount];
            _slotMaps           = new Dictionary<Vector3Int, (int, int)>[VariantCount];
            _cpuMirrors         = new TreeInstanceData[VariantCount][];
            _visibleBuffers     = new ComputeBuffer[VariantCount];
            _trunkArgs          = new ComputeBuffer[VariantCount];
            _foliageArgs        = new ComputeBuffer[VariantCount];
            _appendCapacities   = new int[VariantCount];
            _visibleFarBuffers  = new ComputeBuffer[VariantCount];
            _billboardArgs      = new ComputeBuffer[VariantCount];
            _farAppendCapacities = new int[VariantCount];
            _billboardWidths    = new float[VariantCount];
            _billboardHeights   = new float[VariantCount];
            _trunkMPBs          = new MaterialPropertyBlock[VariantCount];
            _foliageMPBs        = new MaterialPropertyBlock[VariantCount];
            _billboardMPBs      = new MaterialPropertyBlock[VariantCount];

            for (int i = 0; i < VariantCount; i++)
                _slotMaps[i] = new Dictionary<Vector3Int, (int, int)>();

            _zeroArray = new TreeInstanceData[ZERO_ARRAY_SIZE];

            SyncBillboardDistanceToChunks();
            LoadBillboardTextures();
            GenerateSkyBillboardTextures();
            ExtractMeshesFromPrefabs();
            GenerateProceduralSkyTreeMeshes();
            CreateBillboardMesh();
            CreateMaterials();

            for (int i = 0; i < VariantCount; i++)
            {
                _trunkMPBs[i]     = new MaterialPropertyBlock();
                _foliageMPBs[i]   = new MaterialPropertyBlock();
                _billboardMPBs[i] = new MaterialPropertyBlock();
            }

            if (cullingShader != null)
            {
                _csCombinedKernel = cullingShader.FindKernel("CSCombined");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseAllGPUBuffers();
            if (_trunkMat != null) Destroy(_trunkMat);
            if (_foliageMat != null) Destroy(_foliageMat);
            if (_billboardMat != null) Destroy(_billboardMat);
            if (_billboardMesh != null) Destroy(_billboardMesh);
            if (_defaultBillboardTex != null) Destroy(_defaultBillboardTex);
        }

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("TreeRenderer.Update");

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            if (_trunkMat == null || cullingShader == null) { RuntimeProfiler.End(s_prof); return; }

            // Re-resolve camera if lost (scene reload, etc.)
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) { RuntimeProfiler.End(s_prof); return; }

            CullAndDraw();
            RuntimeProfiler.End(s_prof);
        }

        // ── Public API — called by WorldDecorationManager ─────────────────

        public void OnChunkActivated(Vector3Int chunkPos, SurfacePoint[] points)
        {
            if (_treeChunks.ContainsKey(chunkPos))
                OnChunkDeactivating(chunkPos);

            var chunkData = new TreeChunkData(chunkPos);
            GenerateTrees(chunkData, points);

            bool hasAny = false;
            for (int v = 0; v < VariantCount; v++)
            {
                if (chunkData.perVariant[v].Count > 0) { hasAny = true; break; }
            }

            if (!hasAny) return;

            _treeChunks[chunkPos] = chunkData;

            // Sub-allocate: append each variant's trees to its GPU buffer
            for (int v = 0; v < VariantCount; v++)
            {
                var list = chunkData.perVariant[v];
                if (list.Count == 0) continue;
                AppendToVariantBuffer(v, chunkPos, list);
            }
        }

        /// <summary>
        /// Async version: generates tree instance data on a background thread,
        /// then uploads to GPU buffers on the main thread via MainThreadDispatcher.
        /// Prevents 1-3ms main-thread stalls per chunk during fast movement.
        /// </summary>
        public void OnChunkActivatedAsync(Vector3Int chunkPos, SurfacePoint[] points)
        {
            if (_treeChunks.ContainsKey(chunkPos))
                OnChunkDeactivating(chunkPos);

            // Snapshot removed trees set for thread safety (rarely modified)
            var removedSnapshot = new HashSet<Vector2Int>(_removedTrees);
            var capturedPos = chunkPos;
            var capturedPoints = points;

            Task.Run(() =>
            {
                var chunkData = new TreeChunkData(capturedPos);
                GenerateTreesThreadSafe(chunkData, capturedPoints, removedSnapshot);

                bool hasAny = false;
                for (int v = 0; v < VariantCount; v++)
                {
                    if (chunkData.perVariant[v].Count > 0) { hasAny = true; break; }
                }
                if (!hasAny) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    // Guard: chunk may have been unloaded while generating
                    if (_treeChunks.ContainsKey(capturedPos))
                        OnChunkDeactivating(capturedPos);

                    _treeChunks[capturedPos] = chunkData;
                    for (int v = 0; v < VariantCount; v++)
                    {
                        var list = chunkData.perVariant[v];
                        if (list.Count == 0) continue;
                        AppendToVariantBuffer(v, capturedPos, list);
                    }
                });
            });
        }

        public void OnChunkDeactivating(Vector3Int chunkPos)
        {
            if (!_treeChunks.Remove(chunkPos)) return;

            // Invalidate each variant's slot for this chunk
            for (int v = 0; v < VariantCount; v++)
                InvalidateVariantSlot(v, chunkPos);
        }

        public void OnChunksRebuilt(Vector3Int chunkPos, SurfacePoint[] newPoints)
        {
            OnChunkDeactivating(chunkPos);
            OnChunkActivated(chunkPos, newPoints);
        }

        /// <summary>Returns total tree count across all variants and chunks.</summary>
        public int GetTotalTreeCount()
        {
            int total = 0;
            for (int v = 0; v < VariantCount; v++)
                total += _liveTreeCounts[v];
            return total;
        }

        // ── Tree removal API (for player chopping) ─────────────────────────

        /// <summary>
        /// Removes the tree at the given position from rendering.
        /// Returns true if a tree was found and removed.
        /// </summary>
        public bool RemoveTree(Vector3 position)
        {
            var key = TreeKey(position);
            if (!_removedTrees.Add(key)) return false;

            foreach (var kv in _treeChunks)
            {
                for (int v = 0; v < VariantCount; v++)
                {
                    var list = kv.Value.perVariant[v];
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (TreeKey(list[i].position) == key)
                        {
                            list.RemoveAt(i);
                            // Re-upload this chunk's variant slot with the tree removed
                            ReuploadVariantSlot(v, kv.Key, list);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // ── Tree raycast API (for player chopping) ─────────────────────────

        /// <summary>
        /// CPU-side ray-cylinder test against all nearby tree trunks.
        /// Returns true if the ray hits a tree trunk within maxDist.
        /// </summary>
        public bool TryRaycastTree(Ray ray, float maxDist, out TreeHitResult hit)
        {
            hit = default;
            float bestDistSq = maxDist * maxDist;
            bool found = false;

            Vector3 origin = ray.origin;
            Vector3 dir = ray.direction;

            Vector3Int centerChunk = new Vector3Int(
                Mathf.FloorToInt(origin.x / ChunkData.SIZE),
                Mathf.FloorToInt(origin.y / ChunkData.SIZE),
                Mathf.FloorToInt(origin.z / ChunkData.SIZE)
            );

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var chunkKey = centerChunk + new Vector3Int(dx, dy, dz);
                if (!_treeChunks.TryGetValue(chunkKey, out var chunk)) continue;

                for (int v = 0; v < VariantCount; v++)
                {
                    var list = chunk.perVariant[v];
                    for (int i = 0; i < list.Count; i++)
                    {
                        var tree = list[i];
                        float trunkRadius = 0.35f * tree.scale;
                        float trunkHeight = 3.5f * tree.scale;

                        // Closest point on ray to tree axis (XZ projection)
                        Vector3 toTree = tree.position - origin;
                        float tClosest = Vector3.Dot(toTree, dir);
                        if (tClosest < 0f || tClosest > maxDist) continue;

                        Vector3 closest = origin + dir * tClosest;
                        float hx = closest.x - tree.position.x;
                        float hz = closest.z - tree.position.z;
                        float horizDistSq = hx * hx + hz * hz;

                        if (horizDistSq > trunkRadius * trunkRadius) continue;

                        // Check Y range (trunk)
                        float yRel = closest.y - tree.position.y;
                        if (yRel < 0f || yRel > trunkHeight) continue;

                        float distSq = (closest - origin).sqrMagnitude;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            hit = new TreeHitResult
                            {
                                position = tree.position,
                                rotation = tree.rotation,
                                variantIndex = v,
                                scale = tree.scale,
                                chunkPos = chunkKey
                            };
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        // ── Nearby tree query (for collision proxy) ────────────────────────

        /// <summary>
        /// Fills results with trees within radius of center. Returns count found.
        /// </summary>
        public int GetNearbyTrees(Vector3 center, float radius, TreeInstanceData[] results)
        {
            float radiusSq = radius * radius;
            int count = 0;
            int max = results.Length;

            Vector3Int centerChunk = new Vector3Int(
                Mathf.FloorToInt(center.x / ChunkData.SIZE),
                Mathf.FloorToInt(center.y / ChunkData.SIZE),
                Mathf.FloorToInt(center.z / ChunkData.SIZE)
            );

            for (int dx = -1; dx <= 1 && count < max; dx++)
            for (int dy = -1; dy <= 1 && count < max; dy++)
            for (int dz = -1; dz <= 1 && count < max; dz++)
            {
                var key = centerChunk + new Vector3Int(dx, dy, dz);
                if (!_treeChunks.TryGetValue(key, out var chunk)) continue;

                for (int v = 0; v < VariantCount && count < max; v++)
                {
                    var list = chunk.perVariant[v];
                    for (int i = 0; i < list.Count && count < max; i++)
                    {
                        if ((list[i].position - center).sqrMagnitude < radiusSq)
                            results[count++] = list[i];
                    }
                }
            }

            return count;
        }

        // ── Mesh extraction from prefabs ──────────────────────────────────

        /// <summary>
        /// Sets maxBillboardDistance to match ChunkManager's furthest LOD
        /// so billboards extend to the edge of the visible terrain.
        /// </summary>
        private void SyncBillboardDistanceToChunks()
        {
            if (ChunkManager.Instance != null)
                maxBillboardDistance = ChunkManager.Instance.MaxHorizontalDistance * ChunkData.SIZE;
        }

        /// Auto-loads billboard textures from Resources, falling back to any
        /// inspector-assigned textures. Removes the need for manual assignment
        /// after re-baking.
        /// </summary>
        private void LoadBillboardTextures()
        {
            var loaded = new Texture2D[VariantCount];

            for (int i = 0; i < VariantCount; i++)
            {
                if (i < BillboardResourcePaths.Length)
                    loaded[i] = Resources.Load<Texture2D>(BillboardResourcePaths[i]);

                // Fall back to inspector-assigned texture
                if (loaded[i] == null && billboardTextures != null
                    && i < billboardTextures.Length)
                    loaded[i] = billboardTextures[i];
            }

            billboardTextures = loaded;
        }

        private void ExtractMeshesFromPrefabs()
        {
            for (int v = 0; v < VariantCount; v++)
            {
                if (v >= treePrefabs.Length || treePrefabs[v] == null) continue;

                var prefab = treePrefabs[v];
                var trunkCombines   = new List<CombineInstance>();
                var foliageCombines = new List<CombineInstance>();

                foreach (Transform child in prefab.transform)
                {
                    var mf = child.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    // Local-to-root transform (prefab root is at identity)
                    Matrix4x4 localMatrix = Matrix4x4.TRS(
                        child.localPosition, child.localRotation, child.localScale);

                    var ci = new CombineInstance
                    {
                        mesh      = mf.sharedMesh,
                        transform = localMatrix
                    };

                    if (child.name.Contains("Trunk"))
                        trunkCombines.Add(ci);
                    else
                        foliageCombines.Add(ci);
                }

                if (trunkCombines.Count > 0)
                {
                    _trunkMeshes[v] = new Mesh { name = $"TreeTrunk_V{v}" };
                    _trunkMeshes[v].CombineMeshes(trunkCombines.ToArray(), true, true);
                    _trunkMeshes[v].RecalculateBounds();
                }

                if (foliageCombines.Count > 0)
                {
                    _foliageMeshes[v] = new Mesh { name = $"TreeFoliage_V{v}" };
                    _foliageMeshes[v].CombineMeshes(foliageCombines.ToArray(), true, true);
                    _foliageMeshes[v].RecalculateBounds();
                }

                // Compute billboard dimensions from combined trunk + foliage bounds
                Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
                bool hasBounds = false;
                if (_trunkMeshes[v] != null)
                {
                    combined = _trunkMeshes[v].bounds;
                    hasBounds = true;
                }
                if (_foliageMeshes[v] != null)
                {
                    if (hasBounds) combined.Encapsulate(_foliageMeshes[v].bounds);
                    else { combined = _foliageMeshes[v].bounds; hasBounds = true; }
                }
                if (hasBounds)
                {
                    // The billboard baker renders into a square texture where the
                    // larger of width/height fills the frame with 1.15× padding.
                    // The quad must use the same square framing so the billboard
                    // matches the full-detail model's proportions exactly.
                    float rawWidth  = Mathf.Max(combined.size.x, combined.size.z);
                    float rawHeight = combined.max.y;
                    float maxDim    = Mathf.Max(rawWidth, rawHeight) * billboardPadding;
                    _billboardWidths[v]  = maxDim;
                    _billboardHeights[v] = maxDim;
                }
            }
        }

        /// <summary>
        /// Generates simple procedural tree meshes for sky variants (3-5) when no
        /// prefab is assigned. These are placeholder meshes that work out-of-the-box.
        /// </summary>
        private void GenerateProceduralSkyTreeMeshes()
        {
            // Variant 3: Windswept — bent trunk, swept foliage
            if (_trunkMeshes[3] == null)
            {
                _trunkMeshes[3] = CreateProceduralTrunk(0.3f, 3.0f, 0.15f, 8);
                _trunkMeshes[3].name = "TreeTrunk_Windswept";
            }
            if (_foliageMeshes[3] == null)
            {
                _foliageMeshes[3] = CreateProceduralFoliage(1.8f, 1.2f, new Vector3(0.5f, 3.0f, 0f));
                _foliageMeshes[3].name = "TreeFoliage_Windswept";
            }

            // Variant 4: Shimmerleaf — slender trunk, drooping canopy
            if (_trunkMeshes[4] == null)
            {
                _trunkMeshes[4] = CreateProceduralTrunk(0.2f, 4.0f, 0.12f, 8);
                _trunkMeshes[4].name = "TreeTrunk_Shimmerleaf";
            }
            if (_foliageMeshes[4] == null)
            {
                _foliageMeshes[4] = CreateProceduralFoliage(2.2f, 1.8f, new Vector3(0f, 3.5f, 0f));
                _foliageMeshes[4].name = "TreeFoliage_Shimmerleaf";
            }

            // Variant 5: Sky Cedar — thick trunk, tall conical canopy
            if (_trunkMeshes[5] == null)
            {
                _trunkMeshes[5] = CreateProceduralTrunk(0.45f, 4.5f, 0.25f, 8);
                _trunkMeshes[5].name = "TreeTrunk_SkyCedar";
            }
            if (_foliageMeshes[5] == null)
            {
                _foliageMeshes[5] = CreateProceduralFoliage(1.5f, 3.0f, new Vector3(0f, 4.0f, 0f));
                _foliageMeshes[5].name = "TreeFoliage_SkyCedar";
            }

            // Compute billboard dimensions for procedural meshes
            for (int v = 3; v < VariantCount; v++)
            {
                if (_billboardWidths[v] > 0f) continue; // already computed from prefab

                Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
                bool hasBounds = false;
                if (_trunkMeshes[v] != null)
                {
                    combined = _trunkMeshes[v].bounds;
                    hasBounds = true;
                }
                if (_foliageMeshes[v] != null)
                {
                    if (hasBounds) combined.Encapsulate(_foliageMeshes[v].bounds);
                    else { combined = _foliageMeshes[v].bounds; hasBounds = true; }
                }
                if (hasBounds)
                {
                    float rawWidth  = Mathf.Max(combined.size.x, combined.size.z);
                    float rawHeight = combined.max.y;
                    float maxDim    = Mathf.Max(rawWidth, rawHeight) * billboardPadding;
                    _billboardWidths[v]  = maxDim;
                    _billboardHeights[v] = maxDim;
                }
            }
        }

        private static Mesh CreateProceduralTrunk(float baseRadius, float height, float topRadius, int segments)
        {
            var mesh = new Mesh();
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Bottom ring
                verts[i] = new Vector3(cos * baseRadius, 0f, sin * baseRadius);
                norms[i] = new Vector3(cos, 0f, sin);

                // Top ring
                verts[i + segments + 1] = new Vector3(cos * topRadius, height, sin * topRadius);
                norms[i + segments + 1] = new Vector3(cos, 0f, sin);
            }

            for (int i = 0; i < segments; i++)
            {
                int t = i * 6;
                tris[t]     = i;
                tris[t + 1] = i + segments + 1;
                tris[t + 2] = i + 1;
                tris[t + 3] = i + 1;
                tris[t + 4] = i + segments + 1;
                tris[t + 5] = i + segments + 2;
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateProceduralFoliage(float radius, float height, Vector3 center)
        {
            // Simple icosphere-like shape (octahedron with mid-ring)
            var mesh = new Mesh();
            var verts = new Vector3[]
            {
                center + new Vector3(0f, height * 0.5f, 0f),       // top
                center + new Vector3(radius, 0f, 0f),               // mid ring
                center + new Vector3(0f, 0f, radius),
                center + new Vector3(-radius, 0f, 0f),
                center + new Vector3(0f, 0f, -radius),
                center + new Vector3(0f, -height * 0.3f, 0f),      // bottom
            };
            var norms = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                norms[i] = (verts[i] - center).normalized;

            var tris = new int[]
            {
                0,1,2, 0,2,3, 0,3,4, 0,4,1, // top cap
                5,2,1, 5,3,2, 5,4,3, 5,1,4, // bottom cap
            };

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── Billboard mesh creation ───────────────────────────────────────

        private void CreateBillboardMesh()
        {
            _billboardMesh = new Mesh { name = "TreeBillboardQuad" };
            _billboardMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3( 0.5f, 0f, 0f),
                new Vector3( 0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f)
            };
            _billboardMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _billboardMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _billboardMesh.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };
            _billboardMesh.RecalculateBounds();
        }

        /// <summary>
        /// Generates a default billboard texture (ellipse with organic noise) when
        /// no artist-authored billboard is assigned for a variant.
        /// </summary>
        private Texture2D GenerateDefaultBillboardTexture()
        {
            return GenerateBillboardTexture(0.65f, 0.85f, 0.18f,
                new Color(0.22f, 0.40f, 0.15f), "DefaultTreeBillboard");
        }

        /// <summary>
        /// Generates per-variant procedural billboard textures for sky tree types.
        /// Called during Awake to fill any missing billboard slots.
        /// </summary>
        private void GenerateSkyBillboardTextures()
        {
            // Variant 3: Windswept — asymmetric, swept shape, darker olive-green
            if (billboardTextures[3] == null)
                billboardTextures[3] = GenerateSkyBillboardTexture(0.55f, 0.80f, 0.22f,
                    new Color(0.25f, 0.40f, 0.18f), new Color(0.35f, 0.50f, 0.22f),
                    0.3f, "BillboardWindswept");

            // Variant 4: Shimmerleaf — drooping wide canopy, teal-green
            if (billboardTextures[4] == null)
                billboardTextures[4] = GenerateSkyBillboardTexture(0.70f, 0.90f, 0.15f,
                    new Color(0.18f, 0.42f, 0.32f), new Color(0.28f, 0.55f, 0.40f),
                    0f, "BillboardShimmerleaf");

            // Variant 5: Sky Cedar — tall, conical, deep green
            if (billboardTextures[5] == null)
                billboardTextures[5] = GenerateSkyBillboardTexture(0.45f, 1.2f, 0.20f,
                    new Color(0.15f, 0.35f, 0.18f), new Color(0.22f, 0.45f, 0.20f),
                    0f, "BillboardSkyCedar");
        }

        private static Texture2D GenerateBillboardTexture(float centerOffset, float yStretch,
            float noiseScale, Color tint, string texName)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half * centerOffset) / (half * yStretch);
                    float dist = dx * dx + dy * dy;
                    float alpha = 1f - Mathf.Clamp01((dist - 0.6f) / 0.4f);
                    float noise = Mathf.PerlinNoise(x * noiseScale + 100f, y * noiseScale + 100f);
                    alpha *= Mathf.Clamp01(noise * 1.4f + 0.2f);
                    tex.SetPixel(x, y, new Color(tint.r, tint.g, tint.b, alpha));
                }
            }
            tex.Apply();
            tex.name = texName;
            return tex;
        }

        /// <summary>
        /// Generates higher-quality procedural billboard textures for sky tree variants.
        /// Uses dual-tone coloring (darker core, lighter edges), higher resolution (128x128),
        /// and an asymmetry offset for wind-swept variants.
        /// </summary>
        private static Texture2D GenerateSkyBillboardTexture(float centerOffset, float yStretch,
            float noiseScale, Color coreColor, Color edgeColor, float xShift, string texName)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half - half * xShift) / half;
                    float dy = (y - half * centerOffset) / (half * yStretch);
                    float dist = dx * dx + dy * dy;

                    // Sharper falloff for more tree-like silhouette
                    float alpha = 1f - Mathf.Clamp01((dist - 0.4f) / 0.5f);

                    // Multi-scale noise for organic edge breakup
                    float noise1 = Mathf.PerlinNoise(x * noiseScale + 100f, y * noiseScale + 100f);
                    float noise2 = Mathf.PerlinNoise(x * noiseScale * 2.3f + 50f, y * noiseScale * 2.3f + 50f);
                    float noise = noise1 * 0.7f + noise2 * 0.3f;
                    alpha *= Mathf.Clamp01(noise * 1.6f + 0.1f);

                    // Dual-tone: darker in the center, lighter at edges for depth
                    float coreness = Mathf.Clamp01(1f - dist * 1.2f);
                    Color col = Color.Lerp(edgeColor, coreColor, coreness);

                    // Subtle brightness variation from noise
                    float brightness = 0.85f + noise * 0.3f;
                    col.r *= brightness;
                    col.g *= brightness;
                    col.b *= brightness;

                    tex.SetPixel(x, y, new Color(col.r, col.g, col.b, alpha));
                }
            }
            tex.Apply(true, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.name = texName;
            return tex;
        }

        // ── Material creation ──────────────────────────────────────────────

        private void CreateMaterials()
        {
            if (treeInstanceShader == null) return;

            _trunkMat = new Material(treeInstanceShader) { name = "TreeTrunk_Instanced" };
            if (sourceTrunkMaterial != null)
            {
                _trunkMat.mainTexture = sourceTrunkMaterial.mainTexture;
                if (sourceTrunkMaterial.HasProperty("_Color"))
                    _trunkMat.color = sourceTrunkMaterial.color;
            }

            _foliageMat = new Material(treeInstanceShader) { name = "TreeFoliage_Instanced" };
            if (sourceFoliageMaterial != null)
            {
                _foliageMat.mainTexture = sourceFoliageMaterial.mainTexture;
                if (sourceFoliageMaterial.HasProperty("_Color"))
                    _foliageMat.color = sourceFoliageMaterial.color;
            }

            // Billboard material — tint is white since baked textures already
            // contain the correct colors from the tree materials.
            if (billboardShader != null)
            {
                _billboardMat = new Material(billboardShader) { name = "TreeBillboard_Instanced" };
                _billboardMat.color = Color.white;
            }

            // Default billboard texture fallback
            _defaultBillboardTex = GenerateDefaultBillboardTexture();
        }

        // ── Tree generation (replicates WorldDecorationManager logic) ─────

        private void GenerateTrees(TreeChunkData chunkData, SurfacePoint[] points)
        {
            GenerateTreesThreadSafe(chunkData, points, _removedTrees);
        }

        /// <summary>
        /// Core tree generation logic — pure math, thread-safe.
        /// Takes an explicit removed-trees set so it can be called from background threads
        /// with a snapshot of the main-thread set.
        /// </summary>
        private static void GenerateTreesThreadSafe(TreeChunkData chunkData, SurfacePoint[] points,
            HashSet<Vector2Int> removedTrees)
        {
            float chunkArea = (ChunkData.SIZE / ScanGridSize) * (ChunkData.SIZE / ScanGridSize);
            float seedTree  = WorldSeed.SeedOffset(ChannelTreePlacement);

            foreach (var pt in points)
            {
                if (pt.biome == null)        continue;
                if (pt.normal.y < 0.6f)      continue;
                if (pt.roadInfluence > 0.3f) continue;
                if (pt.underwater)           continue;
                if (!OreGenerator.IsGrass(pt.oreType)) continue;

                // Sky islands use dedicated sky tree config instead of biome-based config
                (float density, TreeWeight[] weights) config;
                if (pt.worldPos.y > SkyStartY)
                {
                    config = SkyTreeConfig;
                }
                else
                {
                    int biomeId = pt.biome.biomeId;
                    if (!BiomeTreeConfig.TryGetValue(biomeId, out config)) continue;
                }

                int hash = HashPair((int)(pt.worldPos.x * 31f), (int)(pt.worldPos.z * 17f));
                hash ^= (int)seedTree;
                if ((hash & 0xFF) / 255f > config.density * chunkArea) continue;

                // Pick variant from weighted distribution
                float roll = ((hash >> 8) & 0xFF) / 255f;
                float cumulative = 0f;
                int variantIndex = config.weights[0].variantIndex;
                for (int i = 0; i < config.weights.Length; i++)
                {
                    cumulative += config.weights[i].weight;
                    if (roll <= cumulative)
                    {
                        variantIndex = config.weights[i].variantIndex;
                        break;
                    }
                }

                if (variantIndex >= VariantCount) continue;

                // Skip trees that the player has removed
                if (removedTrees.Contains(TreeKey(pt.worldPos))) continue;

                // Compute transform (matches WorldTree.Place exactly)
                float baseRand = ((hash >> 16) & 0xFF) / 255f;
                float scale = 0.55f + baseRand * 0.90f;

                Vector3 blendedUp = Vector3.Lerp(Vector3.up, pt.normal, 0.1f).normalized;
                Quaternion toNormal = Quaternion.FromToRotation(Vector3.up, blendedUp);
                float yaw = Mathf.Abs(pt.worldPos.x * 13.7f + pt.worldPos.z * 7.3f) % 360f;
                Quaternion rotation = toNormal * Quaternion.Euler(0f, yaw, 0f);

                chunkData.perVariant[variantIndex].Add(new TreeInstanceData
                {
                    position = pt.worldPos,
                    rotation = rotation,
                    scale    = scale
                });
            }
        }

        // ── Sub-allocated buffer management (per-variant) ─────────────────

        private void EnsureVariantBufferCapacity(int variant, int requiredTotal)
        {
            if (_allTreesBuffers[variant] != null && _bufferCapacities[variant] >= requiredTotal) return;

            int newCap = Mathf.Max(requiredTotal, 512);
            // Over-allocate by 50% to reduce future resizes
            newCap = Mathf.Max(newCap, (int)(_bufferCapacities[variant] * 1.5f));

            var newBuffer = new ComputeBuffer(newCap, Stride);

            // Copy existing data from old buffer if any
            if (_allTreesBuffers[variant] != null && _bufferHighWaters[variant] > 0)
            {
                if (_cpuMirrors[variant] == null || _cpuMirrors[variant].Length < _bufferHighWaters[variant])
                    _cpuMirrors[variant] = new TreeInstanceData[newCap];
                _allTreesBuffers[variant].GetData(_cpuMirrors[variant], 0, 0, _bufferHighWaters[variant]);
                newBuffer.SetData(_cpuMirrors[variant], 0, 0, _bufferHighWaters[variant]);
            }

            _allTreesBuffers[variant]?.Release();
            _allTreesBuffers[variant] = newBuffer;
            _bufferCapacities[variant] = newCap;

            EnsureAppendBuffers(variant, newCap);
        }

        private void AppendToVariantBuffer(int variant, Vector3Int chunkPos, List<TreeInstanceData> trees)
        {
            int count = trees.Count;
            if (count == 0) return;

            int needed = _bufferHighWaters[variant] + count;
            EnsureVariantBufferCapacity(variant, needed);

            // Build temp array from list for SetData
            if (_cpuMirrors[variant] == null || _cpuMirrors[variant].Length < count)
                _cpuMirrors[variant] = new TreeInstanceData[Mathf.Max(count, 512)];
            trees.CopyTo(_cpuMirrors[variant], 0);

            // Upload just this chunk's trees to the GPU buffer
            _allTreesBuffers[variant].SetData(_cpuMirrors[variant], 0, _bufferHighWaters[variant], count);
            _slotMaps[variant][chunkPos] = (_bufferHighWaters[variant], count);
            _bufferHighWaters[variant] += count;
            _liveTreeCounts[variant] += count;
        }

        private void InvalidateVariantSlot(int variant, Vector3Int chunkPos)
        {
            if (!_slotMaps[variant].TryGetValue(chunkPos, out var slot)) return;
            _slotMaps[variant].Remove(chunkPos);

            int offset = slot.offset;
            int count = slot.count;

            // Upload zeroed trees (scale=0) so the compute shader skips them
            int remaining = count;
            int writePos = offset;
            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, ZERO_ARRAY_SIZE);
                _allTreesBuffers[variant].SetData(_zeroArray, 0, writePos, batch);
                writePos += batch;
                remaining -= batch;
            }

            _liveTreeCounts[variant] -= count;
            _deadTreeCounts[variant] += count;

            // If the freed slot was at the very end, shrink the high water mark
            if (offset + count == _bufferHighWaters[variant])
            {
                _bufferHighWaters[variant] = offset;
                _deadTreeCounts[variant] -= count; // they're gone, not dead
            }

            // Compact if too fragmented
            if (_deadTreeCounts[variant] > 0 && _bufferHighWaters[variant] > 0
                && (float)_deadTreeCounts[variant] / _bufferHighWaters[variant] > CompactionThreshold)
            {
                CompactVariantBuffer(variant);
            }
        }

        /// <summary>
        /// Re-uploads a single chunk's variant slot after a tree was removed.
        /// Invalidates the old slot and appends the updated list.
        /// </summary>
        private void ReuploadVariantSlot(int variant, Vector3Int chunkPos, List<TreeInstanceData> trees)
        {
            InvalidateVariantSlot(variant, chunkPos);
            if (trees.Count > 0)
                AppendToVariantBuffer(variant, chunkPos, trees);
        }

        private void CompactVariantBuffer(int variant)
        {
            int total = 0;
            foreach (var kv in _treeChunks)
                total += kv.Value.perVariant[variant].Count;

            if (total == 0)
            {
                _bufferHighWaters[variant] = 0;
                _liveTreeCounts[variant] = 0;
                _deadTreeCounts[variant] = 0;
                _slotMaps[variant].Clear();
                return;
            }

            EnsureVariantBufferCapacity(variant, total);

            if (_cpuMirrors[variant] == null || _cpuMirrors[variant].Length < total)
                _cpuMirrors[variant] = new TreeInstanceData[Mathf.Max(total, 512)];

            _slotMaps[variant].Clear();
            int writeOffset = 0;
            foreach (var kv in _treeChunks)
            {
                var list = kv.Value.perVariant[variant];
                if (list.Count == 0) continue;
                list.CopyTo(_cpuMirrors[variant], writeOffset);
                _slotMaps[variant][kv.Key] = (writeOffset, list.Count);
                writeOffset += list.Count;
            }

            _allTreesBuffers[variant].SetData(_cpuMirrors[variant], 0, 0, total);
            _bufferHighWaters[variant] = total;
            _liveTreeCounts[variant] = total;
            _deadTreeCounts[variant] = 0;
        }

        private void EnsureAppendBuffers(int variant, int capacity)
        {
            // Near append buffers
            if (_appendCapacities[variant] < capacity)
            {
                _visibleBuffers[variant]?.Release();
                _trunkArgs[variant]?.Release();
                _foliageArgs[variant]?.Release();

                _appendCapacities[variant] = capacity;
                _visibleBuffers[variant] = new ComputeBuffer(capacity, Stride, ComputeBufferType.Append);
                _trunkArgs[variant]   = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                _foliageArgs[variant] = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            }

            // Far append buffers
            if (_farAppendCapacities[variant] < capacity)
            {
                _visibleFarBuffers[variant]?.Release();
                _billboardArgs[variant]?.Release();

                _farAppendCapacities[variant] = capacity;
                _visibleFarBuffers[variant] = new ComputeBuffer(capacity, Stride, ComputeBufferType.Append);
                _billboardArgs[variant]    = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            }
        }

        // ── GPU culling + draw ────────────────────────────────────────────

        private void CullAndDraw()
        {
            Camera cam = _cachedCam;
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            float nearDistSq      = maxDrawDistance * maxDrawDistance;
            float crossfadeBandSq = (maxDrawDistance + CrossfadeBand) * (maxDrawDistance + CrossfadeBand);
            float farDistSq       = maxBillboardDistance * maxBillboardDistance;
            float farFadeStartSq  = (maxBillboardDistance - FarFadeBand) * (maxBillboardDistance - FarFadeBand);
            float shadowDistSq    = maxShadowDistance * maxShadowDistance;

            // Extract frustum planes (reuse array to avoid per-frame allocation)
            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanesArr);
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = _frustumPlanesArr[i].normal;
                _planeVec4[i] = new Vector4(n.x, n.y, n.z, _frustumPlanesArr[i].distance);
            }

            // Set shadow distance globals (read by ShadowCaster pass in the shader)
            Shader.SetGlobalVector(PropShadowCamPos, camPos);
            Shader.SetGlobalFloat(PropShadowMaxDistSq, shadowDistSq);

            // Set crossfade distance globals (read by tree + billboard shaders for dithered fade)
            Shader.SetGlobalFloat(PropGlobalNearDistSq, nearDistSq);
            Shader.SetGlobalFloat(PropGlobalCrossfadeSq, crossfadeBandSq);
            Shader.SetGlobalFloat(PropGlobalFarDistSq, farDistSq);
            Shader.SetGlobalFloat(PropGlobalFarFadeStartSq, farFadeStartSq);

            Bounds nearBounds = new Bounds(camPos, Vector3.one * (maxDrawDistance * 2f));
            Bounds farBounds  = new Bounds(camPos, Vector3.one * (maxBillboardDistance * 2f));

            for (int v = 0; v < VariantCount; v++)
            {
                if (_bufferHighWaters[v] == 0 || _allTreesBuffers[v] == null) continue;

                int groups = Mathf.CeilToInt(_bufferHighWaters[v] / 64f);

                // Reset both append buffers
                _visibleBuffers[v].SetCounterValue(0);
                if (_visibleFarBuffers[v] != null)
                    _visibleFarBuffers[v].SetCounterValue(0);

                // Single combined dispatch — writes near to _NearOut, far to _FarOut
                cullingShader.SetVector(PropCamPos, camPos);
                cullingShader.SetVectorArray(PropFrustumPlanes, _planeVec4);
                cullingShader.SetInt(PropTreeCount, _bufferHighWaters[v]);
                cullingShader.SetFloat(PropNearDistSq, nearDistSq);
                cullingShader.SetFloat(PropFarDistSq, farDistSq);
                cullingShader.SetFloat(PropCrossfadeBandSq, crossfadeBandSq);
                cullingShader.SetBuffer(_csCombinedKernel, PropAllTrees, _allTreesBuffers[v]);
                cullingShader.SetBuffer(_csCombinedKernel, PropNearOut, _visibleBuffers[v]);
                if (_visibleFarBuffers[v] != null)
                    cullingShader.SetBuffer(_csCombinedKernel, PropFarOut, _visibleFarBuffers[v]);
                cullingShader.Dispatch(_csCombinedKernel, groups, 1, 1);

                // ── Draw near trees (trunk + foliage) ──
                DrawVariantMesh(_trunkMeshes[v], _trunkMat, _trunkArgs[v],
                                _trunkMPBs[v], _visibleBuffers[v], nearBounds);
                DrawVariantMesh(_foliageMeshes[v], _foliageMat, _foliageArgs[v],
                                _foliageMPBs[v], _visibleBuffers[v], nearBounds);

                // ── Draw far billboards ──
                if (_visibleFarBuffers[v] == null || _billboardMesh == null || _billboardMat == null)
                    continue;

                // Set billboard params (per-variant sizes from mesh bounds)
                _billboardMPBs[v].SetFloat(PropBillboardWidth, _billboardWidths[v]);
                _billboardMPBs[v].SetFloat(PropBillboardHeight, _billboardHeights[v]);

                // Per-variant billboard texture
                Texture2D bbTex = (billboardTextures != null
                                   && v < billboardTextures.Length
                                   && billboardTextures[v] != null)
                    ? billboardTextures[v]
                    : _defaultBillboardTex;
                if (bbTex != null)
                    _billboardMPBs[v].SetTexture(PropMainTex, bbTex);

                DrawVariantMesh(_billboardMesh, _billboardMat, _billboardArgs[v],
                                _billboardMPBs[v], _visibleFarBuffers[v], farBounds);
            }
        }

        private void DrawVariantMesh(
            Mesh mesh, Material mat, ComputeBuffer argsBuf,
            MaterialPropertyBlock mpb, ComputeBuffer visibleBuf, Bounds bounds)
        {
            if (mesh == null || mat == null) return;

            _argsTemplate[0] = (uint)mesh.GetIndexCount(0);
            _argsTemplate[1] = 0; // overwritten by CopyCount
            _argsTemplate[2] = (uint)mesh.GetIndexStart(0);
            _argsTemplate[3] = (uint)mesh.GetBaseVertex(0);
            _argsTemplate[4] = 0;
            argsBuf.SetData(_argsTemplate);

            ComputeBuffer.CopyCount(visibleBuf, argsBuf, sizeof(uint));

            mpb.SetBuffer(PropTreeBuffer, visibleBuf);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuf, 0, mpb);
        }

        // ── GPU buffer cleanup ────────────────────────────────────────────

        private void ReleaseAllGPUBuffers()
        {
            for (int v = 0; v < VariantCount; v++)
            {
                _allTreesBuffers[v]?.Release();    _allTreesBuffers[v]    = null;
                _visibleBuffers[v]?.Release();     _visibleBuffers[v]     = null;
                _trunkArgs[v]?.Release();          _trunkArgs[v]          = null;
                _foliageArgs[v]?.Release();         _foliageArgs[v]        = null;
                _visibleFarBuffers[v]?.Release();  _visibleFarBuffers[v]  = null;
                _billboardArgs[v]?.Release();     _billboardArgs[v]     = null;
                _bufferCapacities[v]      = 0;
                _bufferHighWaters[v]      = 0;
                _liveTreeCounts[v]        = 0;
                _deadTreeCounts[v]        = 0;
                _appendCapacities[v]      = 0;
                _farAppendCapacities[v]   = 0;
                _slotMaps[v]?.Clear();
            }
        }

        // ── Utilities ──────────────────────────────────────────────────────

        private static int HashPair(int a, int b)
        {
            int n = a * 1664525 + b * 1013904223;
            n = (n << 13) ^ n;
            return n * (n * n * 15731 + 789221) + 1376312589;
        }

        /// <summary>
        /// Quantized position key for tree identity. Trees are deterministic
        /// and spaced far enough apart that 0.1m precision is unique.
        /// </summary>
        private static Vector2Int TreeKey(Vector3 pos)
        {
            return new Vector2Int(
                Mathf.RoundToInt(pos.x * 10f),
                Mathf.RoundToInt(pos.z * 10f)
            );
        }
    }
}
