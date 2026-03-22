using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Burst-compiled jobs for enemy steering, separation, and movement computation.
    /// Scheduled by EnemyManager each frame.
    /// </summary>
    public static class EnemySteeringJobs
    {
        // ─── Separation + cohesion forces ────────────────────────────────

        /// <summary>
        /// Computes separation forces for all enemies using the spatial grid.
        /// Runs as IJobParallelFor — each enemy reads the grid to find neighbors.
        /// </summary>
        [BurstCompile]
        public struct SeparationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<bool> active;
            [ReadOnly] public NativeArray<int> tickRates;
            [ReadOnly] public NativeArray<int> tickCounters;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialGrid;

            [WriteOnly] public NativeArray<float3> separationForces;
            [WriteOnly] public NativeArray<int> neighborCounts;

            public float separationRadius;
            public float cellSize;

            public void Execute(int index)
            {
                if (!active[index])
                {
                    separationForces[index] = float3.zero;
                    neighborCounts[index] = 0;
                    return;
                }

                // Only compute on tick
                if (tickCounters[index] % tickRates[index] != 0)
                {
                    // Keep previous values (written as zero for safety — EnemyManager caches)
                    return;
                }

                float3 pos = positions[index];
                float3 push = float3.zero;
                int neighbors = 0;

                float inv = 1f / cellSize;
                int cx = (int)math.floor(pos.x * inv);
                int cy = (int)math.floor(pos.y * inv);
                int cz = (int)math.floor(pos.z * inv);

                float sepRadSq = separationRadius * separationRadius;

                // Check neighboring cells
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int key = EnemySpatialGrid.HashCell(cx + dx, cy + dy, cz + dz);
                    if (spatialGrid.TryGetFirstValue(key, out int other, out var it))
                    {
                        do
                        {
                            if (other == index) continue;
                            if (!active[other]) continue;

                            float3 away = pos - positions[other];
                            float distSq = math.lengthsq(away);

                            if (distSq < sepRadSq && distSq > 0.0001f)
                            {
                                float dist = math.sqrt(distSq);
                                push += away * ((separationRadius - dist) / dist);
                            }

                            // Count neighbors within a wider radius for ally counting
                            if (distSq < EnemyConstants.GroupAlertRadius * EnemyConstants.GroupAlertRadius)
                                neighbors++;
                        }
                        while (spatialGrid.TryGetNextValue(out other, ref it));
                    }
                }

                separationForces[index] = push;
                neighborCounts[index] = neighbors;
            }
        }

        // ─── Final steering computation ──────────────────────────────────

        /// <summary>
        /// Combines desired direction + separation forces + smoothing into final velocity.
        /// </summary>
        [BurstCompile]
        public struct ComputeVelocityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<bool> active;
            [ReadOnly] public NativeArray<float3> desiredDirections;
            [ReadOnly] public NativeArray<float3> separationForces;
            [ReadOnly] public NativeArray<float> speeds;
            [ReadOnly] public NativeArray<float> separationWeights; // per-enemy spread mult
            [ReadOnly] public NativeArray<float3> previousSmoothed;

            public NativeArray<float3> velocities;
            public NativeArray<float3> smoothedDirections;

            public float deltaTime;
            public float smoothingSpeed;
            public float separationForceScale;

            public void Execute(int index)
            {
                if (!active[index])
                {
                    velocities[index] = float3.zero;
                    smoothedDirections[index] = float3.zero;
                    return;
                }

                float3 desired = desiredDirections[index];
                float speed = speeds[index];

                // Apply separation
                float3 sep = separationForces[index];
                if (math.lengthsq(sep) > 0.01f)
                {
                    sep = math.normalize(sep) * separationForceScale * separationWeights[index];
                    desired += sep * deltaTime;
                }

                // Smooth direction to prevent jank
                float3 prev = previousSmoothed[index];
                float3 smoothed;
                if (math.lengthsq(desired) > 0.001f)
                {
                    desired = math.normalize(desired);
                    smoothed = math.lerp(prev, desired, math.saturate(smoothingSpeed * deltaTime));
                    if (math.lengthsq(smoothed) > 0.001f)
                        smoothed = math.normalize(smoothed);
                }
                else
                {
                    smoothed = math.lerp(prev, float3.zero, math.saturate(smoothingSpeed * deltaTime));
                }

                smoothedDirections[index] = smoothed;
                velocities[index] = smoothed * speed;
            }
        }
    }
}
