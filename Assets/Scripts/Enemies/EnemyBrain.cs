using Unity.Mathematics;
using UnityEngine;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Utility AI decision engine. Evaluates action scores for each enemy
    /// and outputs state transitions + target positions.
    ///
    /// Called by EnemyManager on each enemy's brain tick (every 0.25s by default).
    /// Runs on main thread because it needs DensityFieldNavigator (managed references).
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly DensityFieldNavigator _densityNav = new();

        // Wander parameters
        private const float WanderRadius     = 25f;  // how far from spawn enemies roam
        private const float WanderMinDist    = 8f;   // minimum distance for next wander target
        private const float WanderMaxDist    = 20f;  // maximum distance for next wander target
        private const float PatrolSpreadDist = 5f;   // minimum spread between patrol targets in a squad
        private const float IdlePauseMin     = 1.5f;
        private const float IdlePauseMax     = 4f;

        // Cover search — physics raycast for buildings/props alongside density field
        private const float CoverRaycastRange = 12f;
        private static readonly LayerMask CoverBlockerMask = ~0; // all layers

        /// <summary>
        /// Evaluate what an enemy should do and update its state + target.
        /// </summary>
        public void Evaluate(ref EnemyRuntimeData e, float3 playerPos, bool playerArmed, float time)
        {
            if (!e.isActive || e.state == EnemyState.Dead) return;

            // Index ability freeze
            if (time < e.staggerEndTime || time < e.mechanicalDisableEndTime)
            {
                e.desiredDirection = float3.zero;
                e.currentSpeed = 0f;
                return;
            }

            float playerDist = math.sqrt(e.playerDistSq);
            float healthRatio = e.health / math.max(e.stats.maxHealth, 1f);
            var w = e.stats.weights;

            // ── Detection ──────────────────────────────────────────────
            if (!e.playerDetected)
            {
                if (e.playerDistSq <= e.stats.detectionRange * e.stats.detectionRange)
                    e.playerDetected = true;
            }

            // If player not detected, stay in passive states
            if (!e.playerDetected)
            {
                HandlePassive(ref e, time);
                return;
            }

            // ── Leash check — return to patrol if too far ──────────────
            float leashRange = e.stats.detectionRange * 2.5f;
            if (e.playerDistSq > leashRange * leashRange)
            {
                e.playerDetected = false;
                TransitionTo(ref e, EnemyState.Patrol, time);
                return;
            }

            // ── Directed: Converse check ───────────────────────────────
            if (e.stats.category == EnemyCategory.Directed && !e.hasSpoken && !playerArmed)
            {
                float converseRange = e.stats.detectionRange * 0.5f;
                if (e.playerDistSq <= converseRange * converseRange)
                {
                    TransitionTo(ref e, EnemyState.Converse, time);
                    return;
                }
            }

            // ── Currently attacking — stay until done ──────────────────
            if (e.state == EnemyState.Attack)
            {
                HandleAttack(ref e, playerPos);
                return;
            }

            // ── Alert pause (just detected) ────────────────────────────
            if (e.state == EnemyState.Alert)
            {
                if (e.stateTimer >= EnemyConstants.AlertDuration)
                    TransitionTo(ref e, EnemyState.Advance, time);
                return;
            }

            // ── Ambush — handled by behavior modules ───────────────────
            if (e.state == EnemyState.Ambush)
                return;

            // ── Converse — waiting for dialogue ────────────────────────
            if (e.state == EnemyState.Converse)
            {
                if (playerArmed || e.stateTimer > 3f)
                {
                    e.hasSpoken = true;
                    TransitionTo(ref e, EnemyState.Advance, time);
                }
                return;
            }

            // ── Utility AI scoring ─────────────────────────────────────
            float distRatio = math.saturate(playerDist / (e.stats.detectionRange * 2f));
            float allyRatio = math.saturate(e.nearbyAllyCount / 4f);

            // In attack range? Always attack if ready.
            if (e.playerDistSq <= e.stats.attackRange * e.stats.attackRange && e.attackTimer <= 0f)
            {
                TransitionTo(ref e, EnemyState.Attack, time);
                return;
            }

            // Score each action
            float scoreAdvance  = healthRatio * w.aggression * (1f - distRatio);
            float scoreFlank    = w.tactical * (playerDist > e.stats.attackRange * 3f ? 1f : 0.3f) * (1f - distRatio);
            float scoreCharge   = 0f;
            if (playerDist >= EnemyConstants.ChargeMinDistance && playerDist <= EnemyConstants.ChargeMaxDistance)
                scoreCharge = w.aggression * healthRatio * 1.5f;

            float scoreCover = (1f - healthRatio) * w.coverTendency;
            if (e.hasCoverPoint) scoreCover *= 1.3f;

            float scoreRegroup = 0f;
            if (e.nearbyAllyCount < 2 && e.squadId >= 0)
                scoreRegroup = (1f - allyRatio) * w.social * 0.7f;

            float scoreFlee = (1f - healthRatio) * w.cowardice * (1f - allyRatio);
            if (healthRatio < EnemyConstants.FleeHealthThreshold)
                scoreFlee *= 2f;

            // Find best action
            EnemyState bestState = EnemyState.Advance;
            float bestScore = scoreAdvance;

            if (scoreFlank > bestScore)   { bestScore = scoreFlank;   bestState = EnemyState.Flank; }
            if (scoreCharge > bestScore)  { bestScore = scoreCharge;  bestState = EnemyState.Charge; }
            if (scoreCover > bestScore)   { bestScore = scoreCover;   bestState = EnemyState.TakeCover; }
            if (scoreRegroup > bestScore) { bestScore = scoreRegroup; bestState = EnemyState.Regroup; }
            if (scoreFlee > bestScore)    { bestScore = scoreFlee;    bestState = EnemyState.Flee; }

            // Apply state transition
            if (bestState != e.state || e.state == EnemyState.Idle || e.state == EnemyState.Patrol)
            {
                TransitionTo(ref e, bestState, time);
            }

            // Compute target + desired direction based on chosen state
            ComputeSteering(ref e, playerPos, playerDist);
        }

        /// <summary>
        /// Alert check — used by squads to propagate detection.
        /// </summary>
        public void ForceAlert(ref EnemyRuntimeData e)
        {
            if (e.state == EnemyState.Idle || e.state == EnemyState.Patrol)
            {
                e.playerDetected = true;
                TransitionTo(ref e, EnemyState.Alert, 0f);
            }
        }

        // ─── Passive behavior (no player detected) ──────────────────────

        private void HandlePassive(ref EnemyRuntimeData e, float time)
        {
            switch (e.state)
            {
                case EnemyState.Idle:
                    e.desiredDirection = float3.zero;
                    e.currentSpeed = 0f;
                    // Variable idle pause before next wander
                    float pauseDuration = IdlePauseMin + (math.abs(math.frac(time * 0.1f + e.id * 0.37f)) * (IdlePauseMax - IdlePauseMin));
                    if (e.stateTimer > pauseDuration)
                        TransitionTo(ref e, EnemyState.Patrol, time);
                    break;

                case EnemyState.Patrol:
                    float3 toPatrol = e.patrolTarget - e.position;
                    toPatrol.y = 0f;
                    if (math.lengthsq(toPatrol) < 1f) // reached wander target
                    {
                        TransitionTo(ref e, EnemyState.Idle, time);
                    }
                    else
                    {
                        e.desiredDirection = math.normalize(toPatrol);
                        e.currentSpeed = e.stats.moveSpeed * e.stats.weights.speedMult * 0.5f;
                    }
                    break;

                default:
                    TransitionTo(ref e, EnemyState.Idle, time);
                    break;
            }
        }

        // ─── Attack state ───────────────────────────────────────────────

        private void HandleAttack(ref EnemyRuntimeData e, float3 playerPos)
        {
            float3 toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            if (math.lengthsq(toPlayer) > 0.01f)
                e.desiredDirection = math.normalize(toPlayer);

            e.currentSpeed = 0f;

            float attackLeash = e.stats.attackRange * 1.4f;
            if (e.playerDistSq > attackLeash * attackLeash)
                TransitionTo(ref e, EnemyState.Advance, 0f);
        }

        // ─── Compute steering direction for active states ───────────────

        private void ComputeSteering(ref EnemyRuntimeData e, float3 playerPos, float playerDist)
        {
            float speed = e.stats.moveSpeed * e.stats.weights.speedMult;
            Vector3 pos3 = new Vector3(e.position.x, e.position.y, e.position.z);

            switch (e.state)
            {
                case EnemyState.Advance:
                {
                    float3 toPlayer = playerPos - e.position;
                    toPlayer.y = 0f;
                    if (math.lengthsq(toPlayer) > 0.01f)
                        e.desiredDirection = math.normalize(toPlayer);
                    e.currentSpeed = speed;
                    e.targetPosition = playerPos;
                    break;
                }

                case EnemyState.Flank:
                {
                    float3 toPlayer = math.normalize(playerPos - e.position);
                    float3 right = math.normalize(math.cross(new float3(0, 1, 0), toPlayer));
                    float3 flankDir = (e.id % 2 == 0) ? right : -right;
                    e.desiredDirection = math.normalize(flankDir * 0.7f + toPlayer * 0.3f);
                    e.currentSpeed = speed;
                    e.targetPosition = e.position + e.desiredDirection * 4f;
                    break;
                }

                case EnemyState.Charge:
                {
                    float3 toPlayer = playerPos - e.position;
                    toPlayer.y = 0f;
                    if (math.lengthsq(toPlayer) > 0.01f)
                        e.desiredDirection = math.normalize(toPlayer);
                    e.currentSpeed = speed * EnemyConstants.ChargeSpeedMult;
                    e.targetPosition = playerPos;
                    break;
                }

                case EnemyState.TakeCover:
                {
                    if (!e.hasCoverPoint || e.coverHoldTimer <= 0f)
                    {
                        Vector3 playerV3 = new Vector3(playerPos.x, playerPos.y, playerPos.z);

                        // Try density field cover first (terrain)
                        Vector3? cover = _densityNav.FindCoverPoint(pos3, playerV3);

                        // Fallback: physics raycast for buildings/props
                        if (!cover.HasValue)
                            cover = FindPhysicsCoverPoint(pos3, playerV3);

                        if (cover.HasValue)
                        {
                            e.coverPoint = new float3(cover.Value.x, cover.Value.y, cover.Value.z);
                            e.hasCoverPoint = true;
                            e.coverHoldTimer = EnemyConstants.CoverHoldTime * e.stats.weights.coverTendency;
                        }
                        else
                        {
                            TransitionTo(ref e, EnemyState.Advance, 0f);
                            e.desiredDirection = math.normalize(playerPos - e.position);
                            e.currentSpeed = speed;
                            return;
                        }
                    }

                    float3 toCover = e.coverPoint - e.position;
                    toCover.y = 0f;
                    if (math.lengthsq(toCover) < 1.0f)
                    {
                        TransitionTo(ref e, EnemyState.InCover, 0f);
                        e.desiredDirection = float3.zero;
                        e.currentSpeed = 0f;
                    }
                    else
                    {
                        e.desiredDirection = math.normalize(toCover);
                        e.currentSpeed = speed;
                    }
                    e.targetPosition = e.coverPoint;
                    break;
                }

                case EnemyState.InCover:
                {
                    float3 toPlayer = playerPos - e.position;
                    toPlayer.y = 0f;
                    if (math.lengthsq(toPlayer) > 0.01f)
                        e.desiredDirection = math.normalize(toPlayer);
                    e.currentSpeed = 0f;
                    e.coverHoldTimer -= EnemyConstants.BrainTickInterval;
                    if (e.coverHoldTimer <= 0f)
                    {
                        e.hasCoverPoint = false;
                    }
                    break;
                }

                case EnemyState.Regroup:
                {
                    float3 toTarget = e.targetPosition - e.position;
                    toTarget.y = 0f;
                    if (math.lengthsq(toTarget) < 4f)
                        TransitionTo(ref e, EnemyState.Advance, 0f);
                    else
                    {
                        e.desiredDirection = math.normalize(toTarget);
                        e.currentSpeed = speed;
                    }
                    break;
                }

                case EnemyState.Flee:
                {
                    float3 away = e.position - playerPos;
                    away.y = 0f;
                    if (math.lengthsq(away) < 0.01f)
                        away = new float3(1, 0, 0);
                    e.desiredDirection = math.normalize(away);
                    e.currentSpeed = speed * 1.1f;

                    float fleeRange = e.stats.detectionRange * 1.5f;
                    if (e.playerDistSq > fleeRange * fleeRange)
                    {
                        e.playerDetected = false;
                        TransitionTo(ref e, EnemyState.Idle, 0f);
                    }
                    break;
                }
            }

            // Erratic jitter for Directed/Wild
            if (e.stats.weights.erraticChance > 0f)
            {
                uint hash = math.hash(new int2(e.id, (int)(e.stateTimer * 100f)));
                float roll = (hash % 1000) / 1000f;
                if (roll < e.stats.weights.erraticChance)
                {
                    float angle = ((hash >> 10) % 360) - 180f;
                    float rad = math.radians(angle * 0.4f);
                    float c = math.cos(rad);
                    float s = math.sin(rad);
                    float3 d = e.desiredDirection;
                    e.desiredDirection = new float3(d.x * c - d.z * s, 0f, d.x * s + d.z * c);
                }
            }
        }

        // ─── Cover via physics raycast (buildings, props) ───────────────

        /// <summary>
        /// Finds a cover point behind a physics collider (building, prop).
        /// Scans 8 horizontal directions for something that blocks LOS to danger.
        /// </summary>
        private Vector3? FindPhysicsCoverPoint(Vector3 pos, Vector3 dangerPos)
        {
            Vector3 dangerDir = (dangerPos - pos).normalized;
            Vector3 origin = pos + Vector3.up * 0.8f;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;

                // Skip directions toward danger
                if (Vector3.Dot(dir, dangerDir) > 0.3f) continue;

                // Cast outward to find an obstacle
                if (Physics.Raycast(origin, dir, out RaycastHit hit, CoverRaycastRange,
                        CoverBlockerMask, QueryTriggerInteraction.Ignore))
                {
                    // Candidate is just behind the obstacle (on our side)
                    Vector3 candidate = hit.point - dir * 0.8f;

                    // Verify LOS from candidate to danger is blocked
                    Vector3 toDanger = (dangerPos - candidate).normalized;
                    float dangerDist = Vector3.Distance(candidate, dangerPos);
                    if (Physics.Raycast(candidate + Vector3.up * 0.8f, toDanger,
                            Mathf.Min(dangerDist, 10f), CoverBlockerMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        // ─── State transition ───────────────────────────────────────────

        private void TransitionTo(ref EnemyRuntimeData e, EnemyState next, float time)
        {
            e.previousState = e.state;
            e.state = next;
            e.stateTimer = 0f;

            switch (next)
            {
                case EnemyState.Idle:
                case EnemyState.Alert:
                case EnemyState.Attack:
                case EnemyState.Converse:
                case EnemyState.InCover:
                    e.desiredDirection = float3.zero;
                    e.currentSpeed = 0f;
                    break;

                case EnemyState.Patrol:
                    PickWanderTarget(ref e, time);
                    break;
            }
        }

        /// <summary>
        /// Pick a random wander target within WanderRadius of spawn, at least
        /// WanderMinDist away from current position. Ensures enemies spread out
        /// instead of pacing back and forth.
        /// </summary>
        private void PickWanderTarget(ref EnemyRuntimeData e, float time)
        {
            // Generate a pseudo-random direction based on enemy id + time
            uint seed = math.hash(new int2(e.id, (int)(time * 7.3f)));
            float angle = (seed % 3600) * 0.1f; // 0-360 degrees
            float dist = WanderMinDist + (((seed >> 12) % 1000) / 1000f) * (WanderMaxDist - WanderMinDist);

            float rad = math.radians(angle);
            float3 offset = new float3(math.cos(rad) * dist, 0f, math.sin(rad) * dist);

            float3 candidate = e.position + offset;

            // Clamp within wander radius of spawn point
            float3 fromSpawn = candidate - e.spawnPoint;
            fromSpawn.y = 0f;
            if (math.length(fromSpawn) > WanderRadius)
            {
                candidate = e.spawnPoint + math.normalize(fromSpawn) * WanderRadius;
            }

            // Snap to terrain surface
            Vector3 candidateV3 = new Vector3(candidate.x, candidate.y, candidate.z);
            float surfaceY = _densityNav.GetSurfaceHeightAt(candidateV3, 12f);
            candidate.y = surfaceY;

            // Verify walkable
            if (_densityNav.IsWalkable(candidateV3))
                e.patrolTarget = candidate;
            else
                e.patrolTarget = e.spawnPoint; // fallback
        }
    }
}
