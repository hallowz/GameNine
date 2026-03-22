using System.Collections.Generic;
using UnityEngine;
using Voidborne.World.Chunks;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Zero-raycast terrain awareness by sampling the voxel density field directly.
    /// Replaces EnvironmentSensor's 26-ray hemisphere LIDAR with pure array lookups.
    ///
    /// All terrain queries (surface height, slope normal, obstruction, cover) read from
    /// ChunkData.densityField. Only player-placed buildings (which exist as colliders but
    /// not in the density field) need a physics query — handled externally.
    ///
    /// Cost: ~20-40 array reads per frame per enemy (trivial, all in L1 cache).
    /// </summary>
    public sealed class DensityFieldNavigator
    {
        private const int SIZE = ChunkData.SIZE; // 32
        private const float SAMPLE_STEP = 0.5f;  // half-voxel march step

        // Cached open directions result to avoid per-call allocation
        private readonly List<Vector3> _openDirsCache = new List<Vector3>(12);

        // 12 compass directions for cover/open-direction scans (horizontal + 45° up/down)
        private static readonly Vector3[] s_ScanDirs = BuildScanDirections();

        private static Vector3[] BuildScanDirections()
        {
            var dirs = new Vector3[12];
            // 8 horizontal compass directions
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                dirs[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            // 4 diagonal up/down for caves and overhangs
            dirs[8]  = new Vector3( 1f, 0.5f, 0f).normalized;
            dirs[9]  = new Vector3(-1f, 0.5f, 0f).normalized;
            dirs[10] = new Vector3(0f, 0.5f,  1f).normalized;
            dirs[11] = new Vector3(0f, 0.5f, -1f).normalized;
            return dirs;
        }

        // ─── Core: density sampling ─────────────────────────────────────────

        /// <summary>
        /// Samples the density field at a world position.
        /// Returns >0 for solid, <=0 for air. Returns 0 if chunk is unloaded.
        /// Uses nearest-voxel sampling (fast, sufficient for navigation).
        /// </summary>
        public float SampleDensity(Vector3 worldPos)
        {
            if (ChunkManager.Instance == null) return 0f;

            Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(worldPos);
            ChunkData chunk = ChunkManager.Instance.GetChunk(chunkPos);
            if (chunk == null || chunk.state == ChunkState.Unloaded) return 0f;

            Vector3 origin = chunk.WorldPosition;
            int lx = Mathf.Clamp(Mathf.FloorToInt(worldPos.x - origin.x), 0, SIZE - 1);
            int ly = Mathf.Clamp(Mathf.FloorToInt(worldPos.y - origin.y), 0, SIZE - 1);
            int lz = Mathf.Clamp(Mathf.FloorToInt(worldPos.z - origin.z), 0, SIZE - 1);

            return chunk.densityField[lx + ly * SIZE + lz * SIZE * SIZE];
        }

        // ─── Surface detection ──────────────────────────────────────────────

        /// <summary>
        /// Finds the terrain surface Y at a given XZ position by scanning downward
        /// from startY. Returns the interpolated Y where density crosses zero
        /// (air-to-solid transition). Returns startY - searchDepth if no surface found.
        /// </summary>
        public float GetSurfaceHeightAt(Vector3 worldPos, float searchDepth = 8f)
        {
            if (ChunkManager.Instance == null) return worldPos.y;

            float startY = worldPos.y + 2f; // start slightly above
            float endY = worldPos.y - searchDepth;

            float prevDensity = SampleDensityFast(worldPos.x, startY, worldPos.z);

            for (float y = startY - 0.5f; y >= endY; y -= 0.5f)
            {
                float density = SampleDensityFast(worldPos.x, y, worldPos.z);

                // Found the air-to-solid transition
                if (prevDensity <= 0f && density > 0f)
                {
                    // Linear interpolation for sub-voxel accuracy
                    float t = prevDensity / (prevDensity - density);
                    return y + 0.5f - t * 0.5f;
                }
                prevDensity = density;
            }

            return endY;
        }

        /// <summary>
        /// Computes the surface normal at a world position using central differences
        /// on the density field gradient. The normal points away from solid (into air).
        /// </summary>
        public Vector3 GetSurfaceNormalAt(Vector3 worldPos)
        {
            const float d = 0.5f;
            float nx = SampleDensityFast(worldPos.x + d, worldPos.y, worldPos.z)
                     - SampleDensityFast(worldPos.x - d, worldPos.y, worldPos.z);
            float ny = SampleDensityFast(worldPos.x, worldPos.y + d, worldPos.z)
                     - SampleDensityFast(worldPos.x, worldPos.y - d, worldPos.z);
            float nz = SampleDensityFast(worldPos.x, worldPos.y, worldPos.z + d)
                     - SampleDensityFast(worldPos.x, worldPos.y, worldPos.z - d);

            // Gradient points into solid; normal points away (into air)
            Vector3 normal = new Vector3(-nx, -ny, -nz);
            float sqrMag = normal.sqrMagnitude;
            if (sqrMag < 0.0001f) return Vector3.up;
            return normal / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// Returns true if the surface at the given position is too steep to walk on.
        /// </summary>
        public bool IsWalkable(Vector3 worldPos, float maxSlopeDegrees = 50f)
        {
            Vector3 normal = GetSurfaceNormalAt(worldPos);
            return normal.y >= Mathf.Cos(maxSlopeDegrees * Mathf.Deg2Rad);
        }

        // ─── Obstruction detection ──────────────────────────────────────────

        /// <summary>
        /// Marches through the density field along a direction. Returns true if any
        /// solid voxel is encountered within the given distance.
        /// Cost: ~distance/0.5 array reads.
        /// </summary>
        public bool IsObstructed(Vector3 from, Vector3 dir, float distance)
        {
            int steps = Mathf.CeilToInt(distance / SAMPLE_STEP);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 sample = from + dir * (i * SAMPLE_STEP);
                if (SampleDensityFast(sample.x, sample.y, sample.z) > 0f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the distance to the nearest solid voxel along a direction.
        /// Returns maxDist if unobstructed.
        /// </summary>
        public float DistanceToObstacle(Vector3 from, Vector3 dir, float maxDist)
        {
            int steps = Mathf.CeilToInt(maxDist / SAMPLE_STEP);
            for (int i = 1; i <= steps; i++)
            {
                float dist = i * SAMPLE_STEP;
                Vector3 sample = from + dir * dist;
                if (SampleDensityFast(sample.x, sample.y, sample.z) > 0f)
                    return dist;
            }
            return maxDist;
        }

        // ─── Spatial queries ────────────────────────────────────────────────

        /// <summary>
        /// Returns world-space directions that have no terrain obstacle within minClearance.
        /// Uses the 12 pre-built scan directions. Results are cached to avoid allocation.
        /// </summary>
        public List<Vector3> GetOpenDirections(Vector3 pos, float minClearance = 3f)
        {
            _openDirsCache.Clear();
            Vector3 origin = pos + Vector3.up * 0.8f; // chest height

            for (int i = 0; i < s_ScanDirs.Length; i++)
            {
                float dist = DistanceToObstacle(origin, s_ScanDirs[i], minClearance);
                if (dist >= minClearance)
                    _openDirsCache.Add(s_ScanDirs[i]);
            }
            return _openDirsCache;
        }

        /// <summary>
        /// Finds a position behind terrain cover that breaks line-of-sight from dangerPos.
        /// Scans outward in 12 directions looking for solid terrain between the candidate
        /// point and the danger source. Returns null if no cover found.
        /// </summary>
        public Vector3? FindCoverPoint(Vector3 pos, Vector3 dangerPos, float searchRadius = 10f)
        {
            Vector3 dangerDir = (dangerPos - pos).normalized;
            Vector3 origin = pos + Vector3.up * 0.8f;
            float bestScore = float.MaxValue;
            Vector3? bestPoint = null;

            for (int i = 0; i < s_ScanDirs.Length; i++)
            {
                Vector3 dir = s_ScanDirs[i];

                // Skip directions toward danger — we want to move away or sideways
                if (Vector3.Dot(dir, dangerDir) > 0.3f) continue;

                // March outward to find a position with solid terrain between us and danger
                for (float d = 2f; d <= searchRadius; d += 1.5f)
                {
                    Vector3 candidate = pos + dir * d;

                    // Must have ground beneath
                    float surfaceY = GetSurfaceHeightAt(candidate, 4f);
                    if (surfaceY < candidate.y - 4f) continue;

                    candidate.y = surfaceY;

                    // Must have air at standing height (enemy can fit)
                    Vector3 standPos = candidate + Vector3.up * 1f;
                    if (SampleDensityFast(standPos.x, standPos.y, standPos.z) > 0f) continue;

                    // Must have solid terrain between candidate and danger (the "cover")
                    Vector3 toDanger = (dangerPos - candidate).normalized;
                    float dangerDist = Vector3.Distance(candidate, dangerPos);
                    if (!IsObstructed(candidate + Vector3.up * 0.8f, toDanger, Mathf.Min(dangerDist, 8f)))
                        continue;

                    // Score: prefer closer cover points
                    float score = Vector3.Distance(pos, candidate);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPoint = candidate;
                    }

                    break; // found cover in this direction, try next direction
                }
            }

            return bestPoint;
        }

        // ─── Fast density sampling (inlined for hot paths) ──────────────────

        /// <summary>
        /// Component-wise density sample avoiding Vector3 construction overhead.
        /// </summary>
        private float SampleDensityFast(float wx, float wy, float wz)
        {
            if (ChunkManager.Instance == null) return 0f;

            int cx = FloorDiv(wx, SIZE);
            int cy = FloorDiv(wy, SIZE);
            int cz = FloorDiv(wz, SIZE);

            ChunkData chunk = ChunkManager.Instance.GetChunk(new Vector3Int(cx, cy, cz));
            if (chunk == null || chunk.state == ChunkState.Unloaded) return 0f;

            int lx = Mathf.Clamp((int)(wx - cx * SIZE), 0, SIZE - 1);
            int ly = Mathf.Clamp((int)(wy - cy * SIZE), 0, SIZE - 1);
            int lz = Mathf.Clamp((int)(wz - cz * SIZE), 0, SIZE - 1);

            return chunk.densityField[lx + ly * SIZE + lz * SIZE * SIZE];
        }

        private static int FloorDiv(float value, int divisor)
        {
            int i = (int)value;
            if (value < 0f && value != i) i--;
            if (i < 0 && i % divisor != 0)
                return (i / divisor) - 1;
            return i / divisor;
        }
    }
}
