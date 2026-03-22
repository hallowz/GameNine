using System.Collections;
using UnityEngine;
using Voidborne.Enemies;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// End-game passive generator. Outputs 2000W with no fuel requirement.
    /// Must be placed in the abyssal zone (Y &lt; maximumPlacementY).
    ///
    /// VORD threat mechanic: after <see cref="vordAlertDelay"/> seconds of
    /// continuous operation, a VORD Optimized patrol is dispatched toward this
    /// generator. If the patrol is killed the timer resets and a new one
    /// arrives after another full delay. Destroying or powering down the
    /// generator cancels the active threat.
    /// </summary>
    public class VoidFilamentGenerator : PowerGenerator
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Void Filament")]
        [Tooltip("Maximum Y position at which placement is valid (abyssal zone).")]
        public float maximumPlacementY = -512f;

        public float outputWatts = 2000f;

        [Tooltip("Seconds of continuous operation before a VORD patrol is sent.")]
        public float vordAlertDelay = 300f; // 5 real minutes

        [Tooltip("Definition of the VORD Optimized patrol to spawn. Assign in Inspector or via setup script.")]
        public EnemyDefinition vordPatrolDefinition;

        [Tooltip("How far from the generator the patrol spawns (behind the player if possible).")]
        public float patrolSpawnRadius = 40f;

        [Header("FX")]
        public Light         filamentGlow;
        public ParticleSystem voidParticles;
        public AudioSource   hum;
        [Tooltip("Color of the filament glow when active.")]
        public Color glowColor = new Color(0.3f, 0f, 1f);

        // ── Runtime state ─────────────────────────────────────────────────────

        private float    _operatingTimer;
        private bool     _vordDispatched;
        private EnemyEntity _activePatrol;

        /// <summary>True when a VORD patrol is actively hunting this generator.</summary>
        public bool IsVordActive => _vordDispatched && _activePatrol != null;

        // ── Init ──────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            powerOutput = outputWatts;
        }

        protected override void Start()
        {
            if (transform.position.y > maximumPlacementY)
            {
                Debug.LogWarning($"[VoidFilamentGenerator] Must be placed below Y = {maximumPlacementY}. " +
                                 $"Current Y = {transform.position.y:F1}. Not generating.");
                _isGenerating = false;
            }
            else
            {
                _isGenerating = true;
            }

            base.Start();
            UpdateFX();

            EnemyManager.OnAnyEnemyDied += HandleEnemyDied;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EnemyManager.OnAnyEnemyDied -= HandleEnemyDied;
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isGenerating) return;

            _operatingTimer += Time.deltaTime;

            // First VORD dispatch.
            if (!_vordDispatched && _operatingTimer >= vordAlertDelay)
                DispatchVordPatrol();
        }

        protected override void OnPowerChanged(bool powered)
        {
            if (!powered)
            {
                _operatingTimer = 0f;
                // If a patrol was on the way but hasn't arrived, cancel alert flag.
                // Active patrol stays alive — it won't pursue a dead target.
                _vordDispatched = false;
                _activePatrol   = null;
            }
            UpdateFX();
        }

        // ── VORD mechanics ────────────────────────────────────────────────────

        private void DispatchVordPatrol()
        {
            if (vordPatrolDefinition == null || vordPatrolDefinition.prefab == null)
            {
                Debug.LogWarning("[VoidFilamentGenerator] vordPatrolDefinition not set — cannot dispatch VORD patrol.");
                return;
            }

            Vector3 spawnPos = ChooseSpawnPosition();

            var go = Instantiate(vordPatrolDefinition.prefab, spawnPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            _activePatrol   = go.GetComponent<EnemyEntity>();
            _vordDispatched = true;

            Debug.Log($"[VoidFilamentGenerator] VORD patrol dispatched from {spawnPos}.");
        }

        private Vector3 ChooseSpawnPosition()
        {
            // Try to spawn behind the player (outside camera frustum).
            Camera cam = Camera.main;
            Vector3 origin = transform.position;

            for (int i = 0; i < 12; i++)
            {
                Vector2 rng = Random.insideUnitCircle.normalized;
                Vector3 candidate = origin + new Vector3(rng.x, 0f, rng.y) * patrolSpawnRadius;

                if (cam != null)
                {
                    Vector3 dir = (candidate - cam.transform.position).normalized;
                    if (Vector3.Dot(cam.transform.forward, dir) > 0.3f) continue;
                }
                return candidate;
            }

            // Fallback: directly behind generator.
            return origin + Vector3.back * patrolSpawnRadius;
        }

        private void HandleEnemyDied(EnemyEntity enemy)
        {
            if (enemy != _activePatrol) return;

            // Patrol killed — reset timer so another arrives after a full delay.
            _activePatrol   = null;
            _vordDispatched = false;
            _operatingTimer = 0f;
            Debug.Log("[VoidFilamentGenerator] VORD patrol neutralised. Timer reset.");
        }

        // ── FX ────────────────────────────────────────────────────────────────

        private void UpdateFX()
        {
            if (filamentGlow != null)
            {
                filamentGlow.enabled = _isGenerating;
                filamentGlow.color   = glowColor;
            }

            if (voidParticles != null)
            {
                if (_isGenerating && !voidParticles.isPlaying) voidParticles.Play();
                if (!_isGenerating && voidParticles.isPlaying) voidParticles.Stop();
            }

            if (hum != null)
            {
                if (_isGenerating && !hum.isPlaying) hum.Play();
                if (!_isGenerating && hum.isPlaying) hum.Stop();
            }
        }

        // ── Public accessors ──────────────────────────────────────────────────

        public float OperatingTimer      => _operatingTimer;
        public float VordAlertProgress   => Mathf.Clamp01(_operatingTimer / vordAlertDelay);
        public bool  IsAtValidDepth      => transform.position.y <= maximumPlacementY;
    }
}
