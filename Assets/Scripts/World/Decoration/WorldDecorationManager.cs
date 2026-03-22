using System.Collections.Generic;
using UnityEngine;
using Voidborne.Diagnostics;
using Voidborne.World.Biomes;
using Voidborne.World.Chunks;
using Voidborne.World.Generation;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// Singleton MonoBehaviour that decorates terrain chunks with trees, rocks, and grass.
    ///
    /// Surface points are pre-computed by ChunkMeshBuilder during terrain generation
    /// (background thread, pure math — no raycasting). This manager reads chunk.surfacePoints
    /// and delegates to specialized renderers.
    ///
    /// Decoration pipeline per chunk:
    ///   1. Read pre-computed surface points from ChunkData
    ///   2. Trees  → TreeRenderer  (GPU instanced, compute-shader culled)
    ///   3. Rocks  → pooled prefabs (future: GPU instanced)
    ///   4. Grass  → GrassRenderer  (GPU instanced, compute-shader culled)
    /// </summary>
    public class WorldDecorationManager : MonoBehaviour
    {
        public static WorldDecorationManager Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────
        [SerializeField] private LayerMask terrainLayerMask = ~0;

        [Header("Rock Prefabs (0–2 Small A/B/C, 3–5 Medium A/B/C)")]
        [SerializeField] private GameObject[] rockPrefabs = new GameObject[6];

        [Header("Systems")]
        [SerializeField] private TreeRenderer treeRenderer;
        [SerializeField] private GrassRenderer grassRenderer;

        // ── Constants ──────────────────────────────────────────────────────
        private const int   ScanGridSize   = 16;

        // Rock distance culling — hide rock GameObjects beyond this distance
        [Header("Rock Culling")]
        [SerializeField] private float rockCullDistance = 80f;
        [SerializeField] private float rockCullCheckInterval = 0.5f;
        private float _rockCullTimer;
        private readonly HashSet<Vector3Int> _rockCulledChunks = new HashSet<Vector3Int>();

        // Cached camera reference — avoids Camera.main lookup every cull check
        private Camera _cachedCam;

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("WorldDecorMgr.Update");

        // Rock density
        private const float RockDensity = 0.08f;       // /m²   × 4m² = 32% per point

        // Max pool sizes
        private const int MaxRocksPerVariant = 1200;

        // WorldSeed channels for decoration randomness (≥20 is safe; grass uses 30)
        private const int ChannelRockPlacement = 26;

        // ── Per-chunk decoration state ─────────────────────────────────────

        private class ChunkDecorationState
        {
            public Vector3Int      chunkPos;
            public List<WorldRock> rocks = new List<WorldRock>(64);
        }

        private readonly Dictionary<Vector3Int, ChunkDecorationState> _decoratedChunks
            = new Dictionary<Vector3Int, ChunkDecorationState>();

        private readonly Dictionary<Vector3Int, List<WorldRock>> _rocksByChunk
            = new Dictionary<Vector3Int, List<WorldRock>>();

        // ── Simple object pools ────────────────────────────────────────────

        private class ComponentPool<T> where T : Component
        {
            private readonly Stack<T> _stack = new Stack<T>();
            private readonly GameObject _prefab;
            private readonly Transform  _parent;
            private readonly int        _maxSize;

            public ComponentPool(GameObject prefab, Transform parent, int maxSize)
            {
                _prefab  = prefab;
                _parent  = parent;
                _maxSize = maxSize;
            }

            public T Get()
            {
                if (_stack.Count > 0)
                {
                    T item = _stack.Pop();
                    item.gameObject.SetActive(true);
                    return item;
                }
                if (_prefab == null) return null;
                GameObject go = Instantiate(_prefab, _parent);
                return go.GetComponent<T>();
            }

            public void Return(T item)
            {
                if (item == null) return;
                item.gameObject.SetActive(false);
                item.transform.SetParent(_parent);
                if (_stack.Count < _maxSize)
                    _stack.Push(item);
                else
                    Destroy(item.gameObject);
            }
        }

        private ComponentPool<WorldRock>[] _rockPools;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            InitPools();

            if (treeRenderer == null)
                treeRenderer = TreeRenderer.Instance
                               ?? FindObjectOfType<TreeRenderer>();

            if (grassRenderer == null)
                grassRenderer = GrassRenderer.Instance
                                ?? FindObjectOfType<GrassRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            _rockCullTimer -= Time.deltaTime;
            if (_rockCullTimer > 0f) { RuntimeProfiler.End(s_prof); return; }
            _rockCullTimer = rockCullCheckInterval;
            if (_cachedCam == null) _cachedCam = Camera.main;
            UpdateRockCulling();
            RuntimeProfiler.End(s_prof);
        }

        private void UpdateRockCulling()
        {
            if (_cachedCam == null) return;

            Vector3 camPos = _cachedCam.transform.position;
            float cullDistSq = rockCullDistance * rockCullDistance;

            foreach (var kv in _decoratedChunks)
            {
                Vector3 chunkWorld = new Vector3(
                    kv.Key.x * ChunkData.SIZE + ChunkData.SIZE * 0.5f,
                    kv.Key.y * ChunkData.SIZE + ChunkData.SIZE * 0.5f,
                    kv.Key.z * ChunkData.SIZE + ChunkData.SIZE * 0.5f);
                float distSq = (chunkWorld - camPos).sqrMagnitude;
                bool shouldShow = distSq <= cullDistSq;

                if (!shouldShow && !_rockCulledChunks.Contains(kv.Key))
                {
                    // Cull — deactivate rocks in this chunk
                    _rockCulledChunks.Add(kv.Key);
                    foreach (WorldRock r in kv.Value.rocks)
                    {
                        if (r != null && r.gameObject.activeSelf)
                            r.gameObject.SetActive(false);
                    }
                }
                else if (shouldShow && _rockCulledChunks.Contains(kv.Key))
                {
                    // Un-cull — reactivate rocks in this chunk
                    _rockCulledChunks.Remove(kv.Key);
                    foreach (WorldRock r in kv.Value.rocks)
                    {
                        if (r != null && !r.gameObject.activeSelf)
                            r.gameObject.SetActive(true);
                    }
                }
            }
        }

        private void InitPools()
        {
            _rockPools = new ComponentPool<WorldRock>[rockPrefabs.Length];
            for (int i = 0; i < rockPrefabs.Length; i++)
            {
                if (rockPrefabs[i] != null)
                    _rockPools[i] = new ComponentPool<WorldRock>(rockPrefabs[i], transform, MaxRocksPerVariant);
            }
        }

        // ── Public API — called by ChunkManager ────────────────────────────

        /// <summary>
        /// Called by ChunkManager when a chunk becomes Active.
        /// Reads pre-computed surfacePoints from ChunkData — no raycasting needed.
        /// </summary>
        public void OnChunkActivated(ChunkData chunk, Mesh mesh)
        {
            if (chunk == null) return;

            Vector3Int chunkPos = chunk.chunkPosition;

            // Skip chunks with no surface data (underground, all-air, etc.)
            SurfacePoint[] points = chunk.surfacePoints;
            if (points == null || points.Length == 0) return;

            // Remove previous state if re-decorating
            if (_decoratedChunks.TryGetValue(chunkPos, out ChunkDecorationState existing))
            {
                RemoveChunkDecoration(existing);
                _decoratedChunks.Remove(chunkPos);
            }

            var state = new ChunkDecorationState { chunkPos = chunkPos };
            _decoratedChunks[chunkPos] = state;

            treeRenderer?.OnChunkActivated(chunkPos, points);
            PlaceRocks(state, points);
            GenerateGrass(state, chunkPos, points);

        }

        /// <summary>
        /// Called by ChunkManager for LOD1–LOD2 chunks — only generates billboard trees (no rocks/grass).
        /// </summary>
        public void OnBillboardChunkActivated(ChunkData chunk)
        {
            if (chunk == null) return;

            SurfacePoint[] points = chunk.surfacePoints;
            if (points == null || points.Length == 0) return;

            treeRenderer?.OnChunkActivated(chunk.chunkPosition, points);
        }

        /// <summary>Called by ChunkManager before a chunk is unloaded.</summary>
        public void OnChunkDeactivating(Vector3Int chunkPos)
        {
            if (_decoratedChunks.TryGetValue(chunkPos, out ChunkDecorationState state))
            {
                RemoveChunkDecoration(state);
                _decoratedChunks.Remove(chunkPos);
            }

            treeRenderer?.OnChunkDeactivating(chunkPos);
        }

        /// <summary>Called by ChunkManager after terrain deformation rebuilds chunks.</summary>
        public void OnChunksRebuilt(HashSet<Vector3Int> affectedChunks)
        {
            foreach (Vector3Int pos in affectedChunks)
                NotifyDeformationToDecorations(pos);
        }

        // ── Placement ─────────────────────────────────────────────────────

        private void PlaceRocks(ChunkDecorationState state, SurfacePoint[] points)
        {
            return; // Rocks disabled
            if (_rockPools == null) return;

            float chunkArea = (ChunkData.SIZE / ScanGridSize) * (ChunkData.SIZE / ScanGridSize);
            float seedRock  = WorldSeed.SeedOffset(ChannelRockPlacement);

            foreach (var pt in points)
            {
                if (pt.normal.y < 0.4f)      continue;
                if (pt.roadInfluence > 0.5f) continue;
                if (pt.underwater)           continue;

                int hash = HashPair((int)(pt.worldPos.x * 53f), (int)(pt.worldPos.z * 29f));
                hash ^= (int)seedRock + 50;
                if ((hash & 0xFF) / 255f > RockDensity * chunkArea) continue;

                int sizeBias   = ((hash >> 8) & 0xFF);
                int sizeClass  = sizeBias < 200 ? 0 : 1;
                int subVariant = ((hash >> 16) & 0xFF) % 3;
                int variantIdx = sizeClass * 3 + subVariant;

                if (_rockPools.Length <= variantIdx || _rockPools[variantIdx] == null) continue;

                WorldRock rock = _rockPools[variantIdx].Get();
                if (rock == null) continue;

                rock.variantIndex = variantIdx;
                float scale = sizeClass == 0
                    ? 0.25f + (((hash >> 24) & 0xFF) / 255f) * 0.15f
                    : 0.45f + (((hash >> 24) & 0xFF) / 255f) * 0.25f;

                float yaw = ((hash >> 12) & 0xFF) / 255f * 360f;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, pt.normal)
                               * Quaternion.Euler(0f, yaw, 0f);

                rock.Place(pt.worldPos, rot, scale, state.chunkPos, terrainLayerMask);
                state.rocks.Add(rock);
            }

            _rocksByChunk[state.chunkPos] = state.rocks;
        }

        private void GenerateGrass(ChunkDecorationState state, Vector3Int chunkPos, SurfacePoint[] points)
        {
            if (grassRenderer == null)
                grassRenderer = GrassRenderer.Instance;

            if (grassRenderer != null)
                grassRenderer.OnChunkActivated(chunkPos, points);
        }

        // ── Deformation response ────────────────────────────────────────────

        private void NotifyDeformationToDecorations(Vector3Int chunkPos)
        {
            // Trees — regenerate from updated surface points (GPU instanced, no raycasting)
            if (treeRenderer != null)
            {
                ChunkData chunk = ChunkManager.Instance?.GetChunk(chunkPos);
                if (chunk?.surfacePoints != null)
                    treeRenderer.OnChunksRebuilt(chunkPos, chunk.surfacePoints);
            }

            if (_rocksByChunk.TryGetValue(chunkPos, out List<WorldRock> rocks))
            {
                foreach (WorldRock r in rocks)
                {
                    if (r == null) continue;
                    r.OnTerrainDeformed();
                }
            }

            // Grass regeneration on deformation — use chunk's updated surfacePoints
            if (grassRenderer != null)
            {
                ChunkData chunk = ChunkManager.Instance?.GetChunk(chunkPos);
                if (chunk?.surfacePoints != null)
                    grassRenderer.OnChunksRebuilt(chunkPos, chunk.surfacePoints);
            }
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        private void RemoveChunkDecoration(ChunkDecorationState state)
        {
            // Clear culling state for this chunk
            bool wasCulled = _rockCulledChunks.Remove(state.chunkPos);

            if (_rockPools != null)
            {
                foreach (WorldRock r in state.rocks)
                {
                    if (r == null) continue;
                    // Re-activate distance-culled rocks so pool return works
                    if (wasCulled && !r.gameObject.activeSelf)
                        r.gameObject.SetActive(true);

                    if (r.gameObject.activeSelf)
                    {
                        var rb = r.GetComponent<Rigidbody>();
                        if (rb != null && rb.isKinematic)
                        {
                            if (r.variantIndex < _rockPools.Length && _rockPools[r.variantIndex] != null)
                                _rockPools[r.variantIndex].Return(r);
                        }
                    }
                }
            }

            if (grassRenderer != null)
                grassRenderer.OnChunkDeactivating(state.chunkPos);

            _rocksByChunk.Remove(state.chunkPos);
            state.rocks.Clear();
        }

        // ── Utilities ──────────────────────────────────────────────────────

        private static int HashPair(int a, int b)
        {
            int n = a * 1664525 + b * 1013904223;
            n = (n << 13) ^ n;
            return n * (n * n * 15731 + 789221) + 1376312589;
        }
    }
}
