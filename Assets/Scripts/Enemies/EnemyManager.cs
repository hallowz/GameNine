using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.Building.Electricity;
using Voidborne.Combat;
using Voidborne.Diagnostics;
using Voidborne.Player;
using Voidborne.World.Generation;
using Random = UnityEngine.Random;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Central singleton that owns all enemy runtime data and drives the per-frame
    /// pipeline: spatial grid → sense → decide → steer (Burst) → move → sync.
    ///
    /// Enemies register via <see cref="Register"/> and unregister via <see cref="Unregister"/>.
    /// </summary>
    public sealed class EnemyManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────────

        public static EnemyManager Instance { get; private set; }

        // ─── Data ───────────────────────────────────────────────────────

        private EnemyRuntimeData[] _enemies;
        private EnemyEntity[]      _entities;        // parallel: GO wrappers
        private int                _count;

        // NativeArrays for Burst jobs (mirrored from _enemies each frame)
        private NativeArray<float3> _positions;
        private NativeArray<bool>   _active;
        private NativeArray<float3> _desiredDirs;
        private NativeArray<float3> _separationForces;
        private NativeArray<float3> _velocities;
        private NativeArray<float3> _smoothedDirs;
        private NativeArray<float3> _prevSmoothed;
        private NativeArray<float>  _speeds;
        private NativeArray<float>  _sepWeights;
        private NativeArray<int>    _tickRates;
        private NativeArray<int>    _tickCounters;
        private NativeArray<int>    _neighborCounts;

        // Systems
        private EnemySpatialGrid _spatialGrid;
        private EnemyBrain       _brain;
        private EnemySquad       _squad;
        private DensityFieldNavigator _densityNav;

        // Damage queue
        private readonly Queue<DamageEvent> _damageQueue = new();

        // Player cache
        private Transform _playerTransform;
        private float3    _playerPos;
        private bool      _playerArmed;

        // Profiling
        private static readonly RuntimeProfiler.Token s_profUpdate = RuntimeProfiler.Register("EnemyManager.Update");
        private static readonly RuntimeProfiler.Token s_profJobs   = RuntimeProfiler.Register("EnemyManager.Jobs");
        private static readonly RuntimeProfiler.Token s_profBrain  = RuntimeProfiler.Register("EnemyManager.Brain");
        private static readonly RuntimeProfiler.Token s_profMove   = RuntimeProfiler.Register("EnemyManager.Move");

        // LOD distance check timer
        private float _lodTimer;
        private const float LodCheckInterval = 1f;

        // Sunlight damage
        private float _sunlightDamageTimer;
        private const float SunlightDamageInterval = 0.5f;  // check every 0.5s
        private const float SunlightDPS = 8f;                // damage per second in direct sunlight
        private const float SunlightCheckHeight = 200f;      // raycast distance up (matches player's undergroundRayDistance)
        private const int TerrainLayerMask = 1 << 8; // Layer 8 = Terrain

        // ─── Events ─────────────────────────────────────────────────────

        /// <summary>Fired when any enemy dies. Used by EnemySpawner for population tracking.</summary>
        public static event System.Action<EnemyEntity> OnAnyEnemyDied;

        // ─── Properties ─────────────────────────────────────────────────

        public int ActiveCount => _count;
        public EnemyRuntimeData[] Enemies => _enemies;
        public EnemySquad Squad => _squad;

        // ─── Unity Messages ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            int max = EnemyConstants.MaxEnemies;
            _enemies  = new EnemyRuntimeData[max];
            _entities = new EnemyEntity[max];
            _count    = 0;

            // Allocate NativeArrays
            _positions        = new NativeArray<float3>(max, Allocator.Persistent);
            _active           = new NativeArray<bool>(max, Allocator.Persistent);
            _desiredDirs      = new NativeArray<float3>(max, Allocator.Persistent);
            _separationForces = new NativeArray<float3>(max, Allocator.Persistent);
            _velocities       = new NativeArray<float3>(max, Allocator.Persistent);
            _smoothedDirs     = new NativeArray<float3>(max, Allocator.Persistent);
            _prevSmoothed     = new NativeArray<float3>(max, Allocator.Persistent);
            _speeds           = new NativeArray<float>(max, Allocator.Persistent);
            _sepWeights       = new NativeArray<float>(max, Allocator.Persistent);
            _tickRates        = new NativeArray<int>(max, Allocator.Persistent);
            _tickCounters     = new NativeArray<int>(max, Allocator.Persistent);
            _neighborCounts   = new NativeArray<int>(max, Allocator.Persistent);

            _spatialGrid = new EnemySpatialGrid(max);
            _brain       = new EnemyBrain();
            _squad       = new EnemySquad();
            _densityNav  = new DensityFieldNavigator();
        }

        private void Start()
        {
            if (PlayerManager.Instance != null)
                _playerTransform = PlayerManager.Instance.PlayerTransform;
        }

        private void OnDestroy()
        {
            DisposeNativeArrays();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_profUpdate);

            if (_count == 0) { RuntimeProfiler.End(s_profUpdate); return; }

            // Resolve player
            if (_playerTransform == null && PlayerManager.Instance != null)
                _playerTransform = PlayerManager.Instance.PlayerTransform;

            if (_playerTransform != null)
            {
                _playerPos = new float3(_playerTransform.position.x,
                                        _playerTransform.position.y,
                                        _playerTransform.position.z);
            }

            // Check if player is armed (for Directed converse)
            _playerArmed = IsPlayerArmed();

            float dt = Time.deltaTime;
            float time = Time.time;

            // 1. Process damage queue
            ProcessDamageQueue(time);

            // 2. Copy data to NativeArrays + update LOD + advance timers
            CopyToNative(dt, time);

            // 3. Rebuild spatial grid + separation (Burst)
            RuntimeProfiler.Begin(s_profJobs);
            var gridHandle = _spatialGrid.Rebuild(_positions, _active, _count);

            // Wait for grid before separation (separation reads the grid)
            gridHandle.Complete();

            var sepJob = new EnemySteeringJobs.SeparationJob
            {
                positions        = _positions,
                active           = _active,
                tickRates        = _tickRates,
                tickCounters     = _tickCounters,
                spatialGrid      = GetSpatialGridMap(),
                separationForces = _separationForces,
                neighborCounts   = _neighborCounts,
                separationRadius = EnemyConstants.SeparationRadius,
                cellSize         = EnemyConstants.GridCellSize,
            };
            var sepHandle = sepJob.Schedule(_count, 16);
            sepHandle.Complete();

            // Copy separation results back
            for (int i = 0; i < _count; i++)
            {
                _enemies[i].separationForce = _separationForces[i];
                _enemies[i].nearbyAllyCount = _neighborCounts[i];
            }
            RuntimeProfiler.End(s_profJobs);

            // 4. Brain tick (main thread — needs DensityFieldNavigator)
            RuntimeProfiler.Begin(s_profBrain);
            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;

                e.decisionTimer -= dt;
                if (e.decisionTimer <= 0f)
                {
                    e.decisionTimer = EnemyConstants.BrainTickInterval;
                    _brain.Evaluate(ref e, _playerPos, _playerArmed, time);
                }

                e.stateTimer += dt;
                e.attackTimer -= dt;
            }

            // Squad coordination
            _squad.PropagateAlert(_enemies, _count, _brain);
            _squad.ComputeRegroupTargets(_enemies, _count);
            _squad.DiversifyFlankDirections(_enemies, _count, _playerPos);

            // Sunlight damage — burn enemies exposed to daytime sun
            ApplySunlightDamage(dt, time);

            RuntimeProfiler.End(s_profBrain);

            // 5. Compute velocity (Burst)
            RuntimeProfiler.Begin(s_profJobs);
            for (int i = 0; i < _count; i++)
            {
                _desiredDirs[i] = _enemies[i].desiredDirection;
                _speeds[i]      = _enemies[i].currentSpeed;
                _prevSmoothed[i] = _enemies[i].smoothedDirection;
            }

            var velJob = new EnemySteeringJobs.ComputeVelocityJob
            {
                active             = _active,
                desiredDirections  = _desiredDirs,
                separationForces   = _separationForces,
                speeds             = _speeds,
                separationWeights  = _sepWeights,
                previousSmoothed   = _prevSmoothed,
                velocities         = _velocities,
                smoothedDirections = _smoothedDirs,
                deltaTime          = dt,
                smoothingSpeed     = EnemyConstants.SteeringSmoothing,
                separationForceScale = EnemyConstants.SeparationForce,
            };
            velJob.Schedule(_count, 32).Complete();

            for (int i = 0; i < _count; i++)
            {
                _enemies[i].velocity = _velocities[i];
                _enemies[i].smoothedDirection = _smoothedDirs[i];
            }
            RuntimeProfiler.End(s_profJobs);

            // 6. Apply movement + obstacle avoidance (main thread)
            RuntimeProfiler.Begin(s_profMove);
            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;

                // Skip movement on off-tick frames for distant enemies
                e.tickCounter++;
                if (e.tickCounter % e.tickRate != 0) continue;

                ApplyMovement(ref e, _entities[i], dt);
            }
            RuntimeProfiler.End(s_profMove);

            // 7. Sync transforms + handle attacks
            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.isActive) continue;

                var entity = _entities[i];
                if (entity == null) continue;

                // Sync transform from data
                entity.SyncFromData(ref e, dt);

                // Handle attack execution
                if (e.state == EnemyState.Attack && e.attackTimer <= 0f && !e.isRanged)
                {
                    ExecuteMeleeAttack(ref e, entity);
                }
            }

            RuntimeProfiler.End(s_profUpdate);
        }

        // ─── Registration ───────────────────────────────────────────────

        /// <summary>
        /// Register a new enemy entity. Returns the assigned index.
        /// </summary>
        public int Register(EnemyEntity entity, EnemyDefinition def, Vector3 spawnPos, int squadId = -1)
        {
            if (_count >= EnemyConstants.MaxEnemies)
            {
                Debug.LogWarning("[EnemyManager] Max enemy count reached.");
                return -1;
            }

            int idx = _count++;
            var stats = new EnemyStats
            {
                maxHealth      = def.maxHealth,
                moveSpeed      = def.moveSpeed,
                attackDamage   = def.attackDamage,
                attackRange    = def.attackRange,
                detectionRange = def.detectionRange,
                armor          = def.armor,
                attackCooldown = def.attackCooldown,
                attackWindup   = def.attackWindup,
                category       = def.category,
                weights        = def.behaviorWeights,
            };

            float3 pos = new float3(spawnPos.x, spawnPos.y, spawnPos.z);
            _enemies[idx] = new EnemyRuntimeData
            {
                id               = entity.GetInstanceID(),
                squadId          = squadId,
                stats            = stats,
                position         = pos,
                forward          = new float3(0, 0, 1),
                health           = stats.maxHealth,
                state            = EnemyState.Idle,
                previousState    = EnemyState.Idle,
                isActive         = true,
                tickRate         = 1,
                spawnPoint       = pos,
                patrolTarget     = pos + new float3(4, 0, 0),
                smoothedDirection = float3.zero,
                smoothedSlopeNormal = new float3(0, 1, 0),
                decisionTimer    = Random.Range(0f, EnemyConstants.BrainTickInterval),
                isRanged         = entity.GetComponent<RangedEnemyAttack>() != null,
            };

            _entities[idx] = entity;

            // Set separation weight from behavior
            float spreadMult = def.category switch
            {
                EnemyCategory.Optimized    => 0.6f,
                EnemyCategory.Directed     => 1.4f,
                EnemyCategory.WildCreature => 1.8f,
                _ => 1f,
            };
            if (idx < _sepWeights.Length)
                _sepWeights[idx] = spreadMult;

            return idx;
        }

        /// <summary>
        /// Unregister an enemy (on death or despawn).
        /// Swaps with the last element to keep the array compact.
        /// </summary>
        public void Unregister(int index)
        {
            if (index < 0 || index >= _count) return;

            int last = _count - 1;
            if (index != last)
            {
                // Swap with last
                _enemies[index]  = _enemies[last];
                _entities[index] = _entities[last];

                // Notify swapped entity of its new index
                if (_entities[index] != null)
                    _entities[index].SetManagerIndex(index);
            }

            _enemies[last]  = default;
            _entities[last] = null;
            _count--;
        }

        // ─── Damage queue ───────────────────────────────────────────────

        /// <summary>
        /// Queue a damage event for batch processing. Thread-safe.
        /// </summary>
        public void QueueDamage(DamageEvent evt)
        {
            lock (_damageQueue)
            {
                _damageQueue.Enqueue(evt);
            }
        }

        private void ProcessDamageQueue(float time)
        {
            while (_damageQueue.Count > 0)
            {
                DamageEvent evt;
                lock (_damageQueue)
                {
                    evt = _damageQueue.Dequeue();
                }

                int idx = FindIndexById(evt.enemyId);
                if (idx < 0) continue;

                ref var e = ref _enemies[idx];
                if (!e.isActive || e.state == EnemyState.Dead) continue;

                float reduced = Mathf.Max(0f, evt.amount - e.stats.armor);
                if (evt.isWeakPointHit) reduced *= 2.5f;

                e.health -= reduced;

                // Notify entity for VFX/audio
                var entity = _entities[idx];
                if (entity != null)
                    entity.OnDamageApplied(reduced, evt.hitPoint, evt.hitNormal);

                // Wake up on hit
                if (e.state == EnemyState.Idle || e.state == EnemyState.Patrol || e.state == EnemyState.Converse)
                {
                    e.playerDetected = true;
                    e.state = EnemyState.Advance;
                    e.stateTimer = 0f;
                }

                if (e.health <= 0f)
                    KillEnemy(idx, time);
            }
        }

        // ─── Death ──────────────────────────────────────────────────────

        private void KillEnemy(int idx, float time)
        {
            ref var e = ref _enemies[idx];
            e.state = EnemyState.Dead;
            e.desiredDirection = float3.zero;
            e.currentSpeed = 0f;

            var entity = _entities[idx];
            if (entity != null)
            {
                entity.OnDeath();
                OnAnyEnemyDied?.Invoke(entity);
            }
        }

        // ─── Sunlight damage ────────────────────────────────────────────

        /// <summary>
        /// Enemies on the surface during daytime take constant burn damage.
        /// Checks every 0.5s via density field (is there a ceiling above?).
        /// </summary>
        private void ApplySunlightDamage(float dt, float time)
        {
            // Only burn during daytime
            if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsDaytime) return;

            _sunlightDamageTimer -= dt;
            if (_sunlightDamageTimer > 0f) return;
            _sunlightDamageTimer = SunlightDamageInterval;

            float damage = SunlightDPS * SunlightDamageInterval;

            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;

                // Check if this enemy is exposed to the sky (no solid above)
                Vector3 pos = new Vector3(e.position.x, e.position.y, e.position.z);
                if (IsExposedToSky(pos))
                {
                    e.health -= damage;

                    // Spawn smoke VFX on the entity
                    var entity = _entities[i];
                    if (entity != null)
                        entity.OnSunlightBurn(damage);

                    if (e.health <= 0f)
                        KillEnemy(i, time);
                }
            }
        }

        /// <summary>
        /// Returns true if a position is exposed to the sky (single upward raycast,
        /// same approach as the player's underground detection in DayNightCycle).
        /// </summary>
        private bool IsExposedToSky(Vector3 pos)
        {
            Vector3 origin = pos + Vector3.up * 1f;
            if (!Physics.Raycast(origin, Vector3.up, out RaycastHit hit, SunlightCheckHeight,
                    TerrainLayerMask, QueryTriggerInteraction.Ignore))
                return true; // nothing above = exposed

            return !IsRelevantCeiling(pos.y, hit.point.y);
        }

        /// <summary>
        /// Determines if a ceiling hit is relevant (actually blocks the sky) based on
        /// where the entity is. Below sky zone: ignore sky island hits. On a sky island:
        /// only count hits within the same altitude band.
        /// </summary>
        private static bool IsRelevantCeiling(float entityY, float hitY)
        {
            // Entity is on the surface (below sky transition) — only surface
            // terrain counts as ceiling, sky islands above don't.
            if (entityY < DensityFunction.SkyTransitionStart)
                return hitY < DensityFunction.SkyTransitionStart;

            // Entity is on a sky island — only count ceiling within the same band.
            // Band layout: each band is IslandBandThickness (112) + gap (64) = 176 total.
            const float bandSize = 176f; // IslandBandThickness + IslandBandGap
            const float bandThickness = 112f;

            float entityRel = entityY - DensityFunction.SkyStart;
            float hitRel = hitY - DensityFunction.SkyStart;

            int entityBand = (int)Mathf.Floor(entityRel / bandSize);
            int hitBand = (int)Mathf.Floor(hitRel / bandSize);

            // Hit is in a different band — it's a separate island above, not our ceiling
            if (hitBand != entityBand) return false;

            // Hit is in the same band — it's terrain within our island (cave/overhang)
            return true;
        }

        // ─── Movement ───────────────────────────────────────────────────

        private void ApplyMovement(ref EnemyRuntimeData e, EnemyEntity entity, float dt)
        {
            if (entity == null) return;
            var cc = entity.CharController;
            if (cc == null || !cc.enabled) return;

            float3 vel = e.velocity;

            // Obstacle avoidance via density field (main thread)
            if (math.lengthsq(vel) > 0.01f)
            {
                Vector3 pos3 = new Vector3(e.position.x, e.position.y, e.position.z);
                Vector3 dir3 = new Vector3(vel.x, 0f, vel.z);
                if (dir3.sqrMagnitude > 0.01f)
                {
                    dir3.Normalize();
                    Vector3 deflected = ObstacleFanDeflect(pos3, dir3);
                    float speed = math.length(vel);
                    vel = new float3(deflected.x, 0f, deflected.z) * speed;
                }

                // Slope projection
                Vector3 normal = _densityNav.GetSurfaceNormalAt(pos3);
                float3 fnormal = new float3(normal.x, normal.y, normal.z);
                e.smoothedSlopeNormal = math.lerp(e.smoothedSlopeNormal, fnormal,
                    math.saturate(12f * dt));

                float3 projected = vel - e.smoothedSlopeNormal * math.dot(vel, e.smoothedSlopeNormal);
                if (math.lengthsq(projected) > 0.001f)
                    vel = math.normalize(projected) * math.length(vel);
            }

            // Gravity
            if (cc.isGrounded)
                e.yVelocity = -2f;
            else
                e.yVelocity -= EnemyConstants.Gravity * dt;

            Vector3 move = new Vector3(vel.x * dt, e.yVelocity * dt, vel.z * dt);
            cc.Move(move);

            // Update position from CC
            Vector3 ccPos = cc.transform.position;
            e.position = new float3(ccPos.x, ccPos.y, ccPos.z);

            // Update player distance
            if (_playerTransform != null)
                e.playerDistSq = math.distancesq(e.position, _playerPos);
        }

        // ─── Obstacle fan deflect (density field) ───────────────────────

        private Vector3 ObstacleFanDeflect(Vector3 pos, Vector3 dir)
        {
            Vector3 origin = pos + Vector3.up * 0.8f;
            float halfAng = EnemyConstants.FanAngle;
            float step = halfAng / 2f;

            float bestClear = -1f;
            Vector3 bestDir = dir;

            for (int i = -2; i <= 2; i++)
            {
                float angle = i * step;
                Vector3 fanned = Quaternion.AngleAxis(angle, Vector3.up) * dir;
                float clearDist = _densityNav.DistanceToObstacle(origin, fanned, EnemyConstants.FanRange);
                float clearance = clearDist / EnemyConstants.FanRange +
                    (1f - Mathf.Abs(angle) / (halfAng + 1f));

                if (clearance > bestClear)
                {
                    bestClear = clearance;
                    bestDir = fanned;
                }
            }

            return bestDir.normalized;
        }

        // ─── Melee attack execution ─────────────────────────────────────

        private void ExecuteMeleeAttack(ref EnemyRuntimeData e, EnemyEntity entity)
        {
            e.attackTimer = e.stats.attackCooldown;
            entity.PlayAttackAnimation(e.stats.attackWindup);

            // Damage check (after windup — entity handles the delay via coroutine)
            // The entity will call back to ConfirmMeleeHit after windup
        }

        /// <summary>
        /// Called by EnemyEntity after attack windup completes.
        /// </summary>
        public void ConfirmMeleeHit(int index)
        {
            if (index < 0 || index >= _count) return;
            ref var e = ref _enemies[index];
            if (!e.isActive || e.state == EnemyState.Dead) return;

            if (_playerTransform == null) return;
            float distSq = math.distancesq(e.position, _playerPos);
            float range = e.stats.attackRange * 1.3f;
            if (distSq <= range * range)
                PlayerManager.Instance?.TakeDamage(e.stats.attackDamage);
        }

        // ─── Ability effects ────────────────────────────────────────────

        /// <summary>
        /// Apply Cortex Pulse stagger to an enemy.
        /// </summary>
        public void ApplyStagger(int index, float duration)
        {
            if (index < 0 || index >= _count) return;
            _enemies[index].staggerEndTime = Time.time + duration;
        }

        /// <summary>
        /// Apply Cortex Conductor mechanical disable (Optimized only).
        /// </summary>
        public void ApplyMechanicalDisable(int index, float duration)
        {
            if (index < 0 || index >= _count) return;
            if (_enemies[index].stats.category != EnemyCategory.Optimized) return;
            _enemies[index].mechanicalDisableEndTime = Time.time + duration;
        }

        /// <summary>
        /// Heal an enemy by amount. Used by DirectedCrafterBehavior.
        /// </summary>
        public void HealEnemy(int index, float amount)
        {
            if (index < 0 || index >= _count) return;
            ref var e = ref _enemies[index];
            if (!e.isActive || e.state == EnemyState.Dead) return;
            e.health = math.min(e.health + amount, e.stats.maxHealth);
        }

        // ─── Queries ────────────────────────────────────────────────────

        public ref EnemyRuntimeData GetData(int index)
        {
            return ref _enemies[index];
        }

        public void SetSquadId(int index, int squadId)
        {
            if (index >= 0 && index < _count)
                _enemies[index].squadId = squadId;
        }

        public EnemyEntity GetEntity(int index)
        {
            if (index < 0 || index >= _count) return null;
            return _entities[index];
        }

        public int FindIndexById(int instanceId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_enemies[i].id == instanceId) return i;
            }
            return -1;
        }

        /// <summary>
        /// Find the nearest enemy to a world position within maxDist.
        /// Returns the index, or -1 if none found.
        /// </summary>
        public int FindNearestEnemy(Vector3 pos, float maxDist, int excludeIndex = -1)
        {
            float3 p = new float3(pos.x, pos.y, pos.z);
            float bestDistSq = maxDist * maxDist;
            int best = -1;

            for (int i = 0; i < _count; i++)
            {
                if (i == excludeIndex) continue;
                if (!_enemies[i].isActive || _enemies[i].state == EnemyState.Dead) continue;

                float distSq = math.distancesq(p, _enemies[i].position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Find the most damaged Optimized ally within radius.
        /// Used by DirectedCrafterBehavior.
        /// </summary>
        public int FindMostDamagedOptimized(Vector3 pos, float radius, int excludeIndex = -1)
        {
            float3 p = new float3(pos.x, pos.y, pos.z);
            float radiusSq = radius * radius;
            float lowestRatio = 1f;
            int best = -1;

            for (int i = 0; i < _count; i++)
            {
                if (i == excludeIndex) continue;
                ref var e = ref _enemies[i];
                if (!e.isActive || e.state == EnemyState.Dead) continue;
                if (e.stats.category != EnemyCategory.Optimized) continue;

                float distSq = math.distancesq(p, e.position);
                if (distSq > radiusSq) continue;

                float ratio = e.health / e.stats.maxHealth;
                if (ratio >= 1f) continue;
                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    best = i;
                }
            }
            return best;
        }

        // ─── Internal helpers ───────────────────────────────────────────

        private void CopyToNative(float dt, float time)
        {
            // LOD update (periodic)
            _lodTimer -= dt;
            bool updateLod = _lodTimer <= 0f;
            if (updateLod) _lodTimer = LodCheckInterval;

            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                _positions[i] = e.position;
                _active[i]    = e.isActive && e.state != EnemyState.Dead;
                _tickRates[i] = e.tickRate;
                _tickCounters[i] = e.tickCounter;

                // LOD update
                if (updateLod && e.isActive)
                {
                    if (_playerTransform == null)
                        e.tickRate = 4;
                    else
                    {
                        e.playerDistSq = math.distancesq(e.position, _playerPos);
                        e.tickRate = e.playerDistSq > EnemyConstants.TickDist4Sq ? 4
                                   : e.playerDistSq > EnemyConstants.TickDist2Sq ? 2
                                   : 1;
                    }
                }
            }
        }

        private NativeParallelMultiHashMap<int, int> GetSpatialGridMap()
        {
            // Access the internal grid map for the separation job
            // This requires exposing it from EnemySpatialGrid
            return _spatialGrid.GetMap();
        }

        private bool IsPlayerArmed()
        {
            if (PlayerManager.Instance == null) return false;
            var ws = PlayerManager.Instance.GetComponent<WeaponSwitcher>();
            return ws != null && ws.HasActiveWeapon;
        }

        private void DisposeNativeArrays()
        {
            if (_positions.IsCreated) _positions.Dispose();
            if (_active.IsCreated) _active.Dispose();
            if (_desiredDirs.IsCreated) _desiredDirs.Dispose();
            if (_separationForces.IsCreated) _separationForces.Dispose();
            if (_velocities.IsCreated) _velocities.Dispose();
            if (_smoothedDirs.IsCreated) _smoothedDirs.Dispose();
            if (_prevSmoothed.IsCreated) _prevSmoothed.Dispose();
            if (_speeds.IsCreated) _speeds.Dispose();
            if (_sepWeights.IsCreated) _sepWeights.Dispose();
            if (_tickRates.IsCreated) _tickRates.Dispose();
            if (_tickCounters.IsCreated) _tickCounters.Dispose();
            if (_neighborCounts.IsCreated) _neighborCounts.Dispose();
            if (_spatialGrid.IsCreated) _spatialGrid.Dispose();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_enemies == null) return;
            for (int i = 0; i < _count; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.isActive) continue;
                Vector3 pos = new Vector3(e.position.x, e.position.y, e.position.z);

                Gizmos.color = e.state switch
                {
                    EnemyState.Advance or EnemyState.Charge => Color.red,
                    EnemyState.Flank => new Color(1f, 0.5f, 0f),
                    EnemyState.TakeCover or EnemyState.InCover => Color.blue,
                    EnemyState.Flee => Color.yellow,
                    EnemyState.Regroup => Color.cyan,
                    EnemyState.Patrol => Color.green,
                    _ => Color.gray,
                };
                Gizmos.DrawWireSphere(pos, 0.5f);

                // Draw desired direction
                if (math.lengthsq(e.desiredDirection) > 0.01f)
                {
                    Vector3 dir = new Vector3(e.desiredDirection.x, 0f, e.desiredDirection.z);
                    Gizmos.DrawRay(pos + Vector3.up, dir * 2f);
                }
            }
        }
#endif
    }
}
