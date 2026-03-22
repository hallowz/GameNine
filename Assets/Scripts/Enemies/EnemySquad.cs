using Unity.Mathematics;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Group coordination logic for enemy squads.
    /// Handles shared alert propagation, regroup target computation,
    /// and coordinated flanking direction assignment.
    ///
    /// Called by EnemyManager after brain ticks to layer group behavior
    /// on top of individual decisions.
    /// </summary>
    public sealed class EnemySquad
    {
        private int _nextSquadId;

        /// <summary>
        /// Assigns a squad ID to a newly spawned enemy. Enemies spawned in the
        /// same group get the same squad ID.
        /// </summary>
        public int AllocateSquadId() => _nextSquadId++;

        /// <summary>
        /// Propagate alert state through a squad. When one member detects the player,
        /// all squad members within alert radius also become alert.
        /// </summary>
        public void PropagateAlert(EnemyRuntimeData[] enemies, int count, EnemyBrain brain)
        {
            for (int i = 0; i < count; i++)
            {
                ref var e = ref enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;
                if (!e.playerDetected) continue;
                if (e.squadId < 0) continue;

                // Alert squad members
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    ref var ally = ref enemies[j];
                    if (!ally.isActive || ally.state == EnemyState.Dead) continue;
                    if (ally.squadId != e.squadId) continue;
                    if (ally.playerDetected) continue;

                    float distSq = math.distancesq(e.position, ally.position);
                    if (distSq <= EnemyConstants.GroupAlertRadius * EnemyConstants.GroupAlertRadius)
                    {
                        brain.ForceAlert(ref ally);
                    }
                }
            }
        }

        /// <summary>
        /// Compute regroup targets for squad members in Regroup state.
        /// Target = centroid of active, non-fleeing squad members.
        /// </summary>
        public void ComputeRegroupTargets(EnemyRuntimeData[] enemies, int count)
        {
            // First pass: compute centroids per squad
            // Use a simple approach — max 64 squads tracked at once
            const int maxSquads = 64;
            var centroids = new float3[maxSquads];
            var squadCounts = new int[maxSquads];

            for (int i = 0; i < count; i++)
            {
                ref var e = ref enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;
                if (e.squadId < 0 || e.squadId >= maxSquads) continue;
                if (e.state == EnemyState.Flee) continue;

                centroids[e.squadId] += e.position;
                squadCounts[e.squadId]++;
            }

            for (int s = 0; s < maxSquads; s++)
            {
                if (squadCounts[s] > 0)
                    centroids[s] /= squadCounts[s];
            }

            // Second pass: assign regroup targets
            for (int i = 0; i < count; i++)
            {
                ref var e = ref enemies[i];
                if (!e.isActive || e.state != EnemyState.Regroup) continue;
                if (e.squadId < 0 || e.squadId >= maxSquads) continue;

                if (squadCounts[e.squadId] > 0)
                    e.targetPosition = centroids[e.squadId];
                else
                    e.targetPosition = e.spawnPoint; // fallback
            }
        }

        /// <summary>
        /// Diversify flanking directions within a squad so they don't all go the same way.
        /// Alternates left/right assignment among flanking squad members.
        /// </summary>
        public void DiversifyFlankDirections(EnemyRuntimeData[] enemies, int count, float3 playerPos)
        {
            const int maxSquads = 64;
            var flankToggle = new int[maxSquads]; // alternates per squad

            for (int i = 0; i < count; i++)
            {
                ref var e = ref enemies[i];
                if (!e.isActive || e.state != EnemyState.Flank) continue;
                if (e.squadId < 0 || e.squadId >= maxSquads) continue;

                float3 toPlayer = math.normalize(playerPos - e.position);
                float3 right = math.normalize(math.cross(new float3(0, 1, 0), toPlayer));

                // Alternate left/right within squad
                bool goRight = (flankToggle[e.squadId]++ % 2) == 0;
                float3 flankDir = goRight ? right : -right;

                e.desiredDirection = math.normalize(flankDir * 0.7f + toPlayer * 0.3f);
            }
        }
    }
}
