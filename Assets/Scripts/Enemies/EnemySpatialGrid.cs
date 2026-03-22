using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Grid-based spatial partitioning for fast neighbor queries.
    /// Replaces all OverlapSphere calls with O(1) cell lookups.
    /// Rebuilt each frame via a Burst job.
    /// </summary>
    public struct EnemySpatialGrid : System.IDisposable
    {
        private NativeParallelMultiHashMap<int, int> _grid;
        private float _cellSize;
        private bool _isCreated;

        public bool IsCreated => _isCreated;

        /// <summary>
        /// Expose the internal map for use in Burst jobs (read-only).
        /// </summary>
        public NativeParallelMultiHashMap<int, int> GetMap() => _grid;

        public EnemySpatialGrid(int capacity, float cellSize = EnemyConstants.GridCellSize)
        {
            _grid = new NativeParallelMultiHashMap<int, int>(capacity * 4, Allocator.Persistent);
            _cellSize = cellSize;
            _isCreated = true;
        }

        public void Dispose()
        {
            if (_isCreated)
            {
                _grid.Dispose();
                _isCreated = false;
            }
        }

        /// <summary>
        /// Clear and rebuild the grid from current enemy positions.
        /// Call this once per frame before any queries.
        /// </summary>
        public JobHandle Rebuild(NativeArray<float3> positions, NativeArray<bool> active, int count, JobHandle dependency = default)
        {
            _grid.Clear();

            var job = new BuildGridJob
            {
                positions = positions,
                active    = active,
                grid      = _grid.AsParallelWriter(),
                cellSize  = _cellSize,
            };

            return job.Schedule(count, 32, dependency);
        }

        /// <summary>
        /// Get all enemy indices within radius of a position. Main-thread only.
        /// </summary>
        public void GetNeighbors(float3 pos, float radius, NativeList<int> results)
        {
            results.Clear();
            float invCell = 1f / _cellSize;
            int range = (int)math.ceil(radius * invCell);

            int cx = (int)math.floor(pos.x * invCell);
            int cy = (int)math.floor(pos.y * invCell);
            int cz = (int)math.floor(pos.z * invCell);

            float radiusSq = radius * radius;

            for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            for (int dz = -range; dz <= range; dz++)
            {
                int key = HashCell(cx + dx, cy + dy, cz + dz);
                if (_grid.TryGetFirstValue(key, out int idx, out var it))
                {
                    do
                    {
                        results.Add(idx);
                    }
                    while (_grid.TryGetNextValue(out idx, ref it));
                }
            }
        }

        /// <summary>
        /// Count enemies near a position. Main-thread only.
        /// </summary>
        public int CountNeighbors(float3 pos, float radius, NativeArray<float3> allPositions, int selfIndex = -1)
        {
            float invCell = 1f / _cellSize;
            int range = (int)math.ceil(radius * invCell);

            int cx = (int)math.floor(pos.x * invCell);
            int cy = (int)math.floor(pos.y * invCell);
            int cz = (int)math.floor(pos.z * invCell);

            float radiusSq = radius * radius;
            int count = 0;

            for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            for (int dz = -range; dz <= range; dz++)
            {
                int key = HashCell(cx + dx, cy + dy, cz + dz);
                if (_grid.TryGetFirstValue(key, out int idx, out var it))
                {
                    do
                    {
                        if (idx != selfIndex)
                        {
                            float distSq = math.distancesq(pos, allPositions[idx]);
                            if (distSq <= radiusSq) count++;
                        }
                    }
                    while (_grid.TryGetNextValue(out idx, ref it));
                }
            }

            return count;
        }

        public static int HashCell(int x, int y, int z)
        {
            unchecked
            {
                int hash = x * 73856093;
                hash ^= y * 19349663;
                hash ^= z * 83492791;
                return hash;
            }
        }

        // ─── Burst job ──────────────────────────────────────────────────

        [BurstCompile]
        private struct BuildGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<bool> active;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter grid;
            public float cellSize;

            public void Execute(int index)
            {
                if (!active[index]) return;

                float inv = 1f / cellSize;
                int cx = (int)math.floor(positions[index].x * inv);
                int cy = (int)math.floor(positions[index].y * inv);
                int cz = (int)math.floor(positions[index].z * inv);

                grid.Add(HashCell(cx, cy, cz), index);
            }
        }
    }
}
