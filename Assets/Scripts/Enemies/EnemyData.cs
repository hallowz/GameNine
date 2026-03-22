using Unity.Mathematics;

namespace Voidborne.Enemies
{
    // ─── States ──────────────────────────────────────────────────────────

    public enum EnemyState : byte
    {
        Idle,
        Patrol,
        Alert,
        Advance,     // closing distance directly
        Flank,       // moving perpendicular to approach
        Charge,      // committed sprint at player
        Attack,      // melee / ranged execution
        TakeCover,   // moving to a cover point
        InCover,     // holding behind cover
        Regroup,     // moving toward nearest allies
        Flee,        // retreating from player
        Converse,    // Directed only — dialogue before hostility
        Ambush,      // waiting (ceiling drop, etc.)
        Dead
    }

    // ─── Behavior weights (per enemy‑type tuning) ────────────────────────

    /// <summary>
    /// Utility AI weights that determine how an enemy type prioritises actions.
    /// Stored on EnemyDefinition; copied into runtime data at spawn.
    /// </summary>
    [System.Serializable]
    public struct BehaviorWeights
    {
        /// <summary>Tendency to close distance and attack.</summary>
        public float aggression;
        /// <summary>Tendency to flank / use cover instead of direct approach.</summary>
        public float tactical;
        /// <summary>Likelihood of seeking cover when damaged.</summary>
        public float coverTendency;
        /// <summary>Desire to stay near allies.</summary>
        public float social;
        /// <summary>Tendency to flee when health is low.</summary>
        public float cowardice;
        /// <summary>Speed multiplier applied to base move speed.</summary>
        public float speedMult;
        /// <summary>Chance per decision tick of adding erratic jitter.</summary>
        public float erraticChance;

        // ── Presets ──────────────────────────────────────────────

        public static readonly BehaviorWeights Optimized = new()
        {
            aggression    = 0.8f,
            tactical      = 0.9f,
            coverTendency = 0.7f,
            social        = 0.8f,
            cowardice     = 0.1f,
            speedMult     = 1.05f,
            erraticChance = 0.0f,
        };

        public static readonly BehaviorWeights Directed = new()
        {
            aggression    = 0.5f,
            tactical      = 0.3f,
            coverTendency = 0.4f,
            social        = 0.3f,
            cowardice     = 0.6f,
            speedMult     = 0.9f,
            erraticChance = 0.04f,
        };

        public static readonly BehaviorWeights WildPredator = new()
        {
            aggression    = 1.0f,
            tactical      = 0.0f,
            coverTendency = 0.0f,
            social        = 0.5f,
            cowardice     = 0.3f,
            speedMult     = 1.2f,
            erraticChance = 0.08f,
        };

        public static readonly BehaviorWeights WildPrey = new()
        {
            aggression    = 0.0f,
            tactical      = 0.0f,
            coverTendency = 0.0f,
            social        = 0.7f,
            cowardice     = 1.0f,
            speedMult     = 1.1f,
            erraticChance = 0.05f,
        };

        public static BehaviorWeights ForCategory(EnemyCategory category)
        {
            return category switch
            {
                EnemyCategory.Optimized    => Optimized,
                EnemyCategory.Directed     => Directed,
                EnemyCategory.WildCreature => WildPredator,
                _                          => Optimized,
            };
        }
    }

    // ─── Blittable stat snapshot (copied from EnemyDefinition at spawn) ──

    public struct EnemyStats
    {
        public float maxHealth;
        public float moveSpeed;
        public float attackDamage;
        public float attackRange;
        public float detectionRange;
        public float armor;
        public float attackCooldown;
        public float attackWindup;
        public EnemyCategory category;
        public BehaviorWeights weights;
    }

    // ─── Per-enemy runtime data ──────────────────────────────────────────

    public struct EnemyRuntimeData
    {
        // Identity
        public int id;                     // unique id, matches EnemyEntity
        public int squadId;                // group coordination (-1 = solo)
        public EnemyStats stats;

        // Transform
        public float3 position;
        public float3 forward;
        public float  yVelocity;           // gravity accumulator

        // AI state
        public EnemyState state;
        public EnemyState previousState;
        public float stateTimer;           // time in current state
        public float decisionTimer;        // time until next brain tick
        public float attackTimer;          // cooldown remaining

        // Sensing
        public float3 targetPosition;      // current movement target
        public float  playerDistSq;        // cached squared dist to player
        public int    nearbyAllyCount;     // from spatial grid
        public bool   hasLineOfSight;      // to player
        public bool   playerDetected;

        // Steering
        public float3 desiredDirection;
        public float3 separationForce;
        public float3 velocity;            // final move velocity this frame
        public float  currentSpeed;

        // Health
        public float health;

        // Status effects
        public float staggerEndTime;
        public float mechanicalDisableEndTime;

        // Cover
        public float3 coverPoint;
        public bool   hasCoverPoint;
        public float  coverHoldTimer;

        // Movement smoothing
        public float3 smoothedDirection;   // lerped steering for jank prevention
        public float3 smoothedSlopeNormal;

        // LOD
        public int  tickRate;              // 1, 2, or 4
        public int  tickCounter;
        public bool isActive;              // false = pooled / despawned

        // Flags
        public bool isRanged;              // has RangedEnemyAttack
        public bool hasSpoken;             // Directed converse tracking
        public bool isAmbushing;           // CaveStalker etc.

        // Patrol
        public float3 spawnPoint;
        public float3 patrolTarget;
        public int    patrolIndex;
    }

    // ─── Damage event (queued for batch processing) ─────────────────────

    public struct DamageEvent
    {
        public int   enemyId;
        public float amount;
        public float3 hitPoint;
        public float3 hitNormal;
        public Combat.DamageType type;
        public bool  isWeakPointHit;
    }

    // ─── Constants ──────────────────────────────────────────────────────

    public static class EnemyConstants
    {
        // Distance-based LOD thresholds (squared)
        public const float TickDist2Sq = 30f * 30f;   // beyond 30m → half rate
        public const float TickDist4Sq = 60f * 60f;   // beyond 60m → quarter rate

        // Brain tick interval (seconds between utility AI evaluations)
        public const float BrainTickInterval = 0.25f;

        // Steering
        public const float TurnSpeed         = 280f;   // degrees/sec
        public const float Gravity           = 18f;
        public const float SteeringSmoothing = 10f;    // lerp speed for direction smoothing
        public const float SeparationRadius  = 1.6f;
        public const float SeparationForce   = 4f;

        // Spatial grid
        public const float GridCellSize = 8f;

        // Alert
        public const float AlertDuration    = 0.8f;
        public const float GroupAlertRadius  = 12f;

        // Cover
        public const float CoverSearchRadius = 10f;
        public const float CoverHoldTime     = 6f;

        // Stuck recovery
        public const float StuckThreshold = 0.25f;
        public const float StuckTimeout   = 1.2f;

        // Obstacle avoidance
        public const float FanRange = 2.2f;
        public const float FanAngle = 40f;

        // Charge
        public const float ChargeSpeedMult    = 1.6f;
        public const float ChargeMinDistance   = 5f;
        public const float ChargeMaxDistance   = 15f;

        // Flee
        public const float FleeHealthThreshold = 0.3f;

        // Max active enemies
        public const int MaxEnemies = 256;
    }
}
