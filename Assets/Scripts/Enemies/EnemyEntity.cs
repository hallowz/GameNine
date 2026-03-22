using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.Combat;
using Voidborne.Player;
using Voidborne.UI;
using Random = UnityEngine.Random;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Thin MonoBehaviour wrapper on each enemy GameObject.
    /// Handles IDamageable, transform sync, audio, VFX, and loot drops.
    /// All AI logic lives in EnemyManager / EnemyBrain.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyEntity : MonoBehaviour, IDamageable
    {
        // ─── Inspector ──────────────────────────────────────────────────

        [SerializeField] private EnemyDefinition _definition;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hitSound;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip _alertSound;
        [SerializeField] private AudioClip _attackSound;

        // ─── Runtime ────────────────────────────────────────────────────

        private int _managerIndex = -1;
        private CharacterController _cc;
        private EnemyAnimator _animator;
        private EnemyHealthBar _healthBar;

        // Cached hit-effect material
        private static Material _fallbackHitMaterial;

        // ─── Properties ─────────────────────────────────────────────────

        public EnemyDefinition Definition => _definition;
        public CharacterController CharController => _cc;
        public int ManagerIndex => _managerIndex;

        /// <summary>Current health, read from EnemyManager data.</summary>
        public float CurrentHealth
        {
            get
            {
                if (_managerIndex < 0 || EnemyManager.Instance == null) return 0f;
                return EnemyManager.Instance.GetData(_managerIndex).health;
            }
        }

        /// <summary>Max health from definition.</summary>
        public float MaxHealth => _definition != null ? _definition.maxHealth : 1f;

        /// <summary>Is this enemy dead?</summary>
        public bool IsDead
        {
            get
            {
                if (_managerIndex < 0 || EnemyManager.Instance == null) return true;
                return EnemyManager.Instance.GetData(_managerIndex).state == EnemyState.Dead;
            }
        }

        /// <summary>Current AI state.</summary>
        public EnemyState CurrentState
        {
            get
            {
                if (_managerIndex < 0 || EnemyManager.Instance == null) return EnemyState.Dead;
                return EnemyManager.Instance.GetData(_managerIndex).state;
            }
        }

        public bool IsStaggered
        {
            get
            {
                if (_managerIndex < 0 || EnemyManager.Instance == null) return false;
                return Time.time < EnemyManager.Instance.GetData(_managerIndex).staggerEndTime;
            }
        }

        public bool IsMechanicallyDisabled
        {
            get
            {
                if (_managerIndex < 0 || EnemyManager.Instance == null) return false;
                return Time.time < EnemyManager.Instance.GetData(_managerIndex).mechanicalDisableEndTime;
            }
        }

        // ─── Unity Messages ─────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _animator = GetComponent<EnemyAnimator>();

            if (GetComponent<EnemyHealthBar>() == null)
                _healthBar = gameObject.AddComponent<EnemyHealthBar>();
            else
                _healthBar = GetComponent<EnemyHealthBar>();
        }

        private void Start()
        {
            if (_definition == null)
            {
                Debug.LogError($"[EnemyEntity] {name} has no EnemyDefinition assigned.", this);
                enabled = false;
                return;
            }

            // Ensure DamageNumbers manager exists
            if (DamageNumbers.Instance == null)
                new GameObject("DamageNumbers").AddComponent<DamageNumbers>();

            // Register with EnemyManager
            if (EnemyManager.Instance != null)
            {
                _managerIndex = EnemyManager.Instance.Register(this, _definition, transform.position);
            }
            else
            {
                Debug.LogError("[EnemyEntity] No EnemyManager in scene.", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_managerIndex >= 0 && EnemyManager.Instance != null)
                EnemyManager.Instance.Unregister(_managerIndex);
        }

        // ─── IDamageable ────────────────────────────────────────────────

        public void TakeDamage(DamageInfo info)
        {
            if (_managerIndex < 0 || EnemyManager.Instance == null) return;

            EnemyManager.Instance.QueueDamage(new DamageEvent
            {
                enemyId   = GetInstanceID(),
                amount    = info.Amount,
                hitPoint  = new float3(info.HitPoint.x, info.HitPoint.y, info.HitPoint.z),
                hitNormal = new float3(info.HitNormal.x, info.HitNormal.y, info.HitNormal.z),
                type      = info.Type,
            });
        }

        /// <summary>
        /// Stagger this enemy (Cortex Pulse ability).
        /// </summary>
        public void Stagger(float duration)
        {
            if (_managerIndex >= 0 && EnemyManager.Instance != null)
                EnemyManager.Instance.ApplyStagger(_managerIndex, duration);
        }

        /// <summary>
        /// Disable mechanical enemy (Cortex Conductor ability, Optimized only).
        /// </summary>
        public void DisableMechanical(float duration)
        {
            if (_managerIndex >= 0 && EnemyManager.Instance != null)
                EnemyManager.Instance.ApplyMechanicalDisable(_managerIndex, duration);
        }

        /// <summary>
        /// Heal this enemy. Used by DirectedCrafterBehavior.
        /// </summary>
        public void Heal(float amount)
        {
            if (_managerIndex >= 0 && EnemyManager.Instance != null)
                EnemyManager.Instance.HealEnemy(_managerIndex, amount);
        }

        // ─── Manager index management ───────────────────────────────────

        public void SetManagerIndex(int newIndex)
        {
            _managerIndex = newIndex;
        }

        // ─── Transform sync (called by EnemyManager) ────────────────────

        /// <summary>
        /// Sync the GameObject transform from EnemyRuntimeData.
        /// Called by EnemyManager each frame.
        /// </summary>
        public void SyncFromData(ref EnemyRuntimeData data, float dt)
        {
            // Position is set by CharacterController.Move — no need to write back
            // Just handle rotation + animation

            // Smooth rotation toward desired direction
            float3 dir = data.smoothedDirection;
            if (math.lengthsq(dir) > 0.01f)
            {
                Vector3 lookDir = new Vector3(dir.x, 0f, dir.z);
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, target, EnemyConstants.TurnSpeed * dt);
                }
            }

            // Update forward in data
            data.forward = new float3(transform.forward.x, transform.forward.y, transform.forward.z);

            // Drive animation
            if (_animator != null)
            {
                bool moving = data.state == EnemyState.Advance
                           || data.state == EnemyState.Flank
                           || data.state == EnemyState.Charge
                           || data.state == EnemyState.Patrol
                           || data.state == EnemyState.Flee
                           || data.state == EnemyState.TakeCover
                           || data.state == EnemyState.Regroup;

                float speedFrac = data.currentSpeed / math.max(data.stats.moveSpeed, 0.1f);
                _animator.SetMoving(moving, data.tickRate, speedFrac);
            }
        }

        // ─── Callbacks from EnemyManager ────────────────────────────────

        /// <summary>
        /// Called when damage is applied (after armor reduction). Handles VFX/audio.
        /// </summary>
        public void OnDamageApplied(float amount, float3 hitPoint, float3 hitNormal)
        {
            if (amount > 0f)
            {
                Vector3 hp = new Vector3(hitPoint.x, hitPoint.y, hitPoint.z);
                DamageNumbers.Instance?.Spawn(amount, hp);
            }

            PlaySound(_hitSound);

            Vector3 hn = new Vector3(hitNormal.x, hitNormal.y, hitNormal.z);
            SpawnHitEffect(new Vector3(hitPoint.x, hitPoint.y, hitPoint.z), hn);

            _animator?.PlayHit();
        }

        /// <summary>
        /// Called each sunlight tick when the enemy is burning in daylight.
        /// Spawns smoke particles from the body.
        /// </summary>
        public void OnSunlightBurn(float damage)
        {
            // Throttle smoke spawning — reuse existing system if available
            if (_sunSmokeFX == null)
            {
                _sunSmokeFX = new GameObject("SunBurnSmoke");
                _sunSmokeFX.transform.SetParent(transform, false);
                _sunSmokeFX.transform.localPosition = Vector3.up * 1f;

                var ps = _sunSmokeFX.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var psr = _sunSmokeFX.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");
                    if (sh != null)
                    {
                        var mat = new Material(sh);
                        Color smokeColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
                        mat.SetColor("_BaseColor", smokeColor);
                        mat.color = smokeColor;
                        psr.material = mat;
                    }
                }

                var main = ps.main;
                main.startColor    = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                main.startSpeed    = 1.5f;
                main.startSize     = 0.3f;
                main.duration      = 0f;
                main.loop          = true;
                main.startLifetime = 1.0f;

                var emission = ps.emission;
                emission.rateOverTime = 15f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;

                ps.Play();
            }
        }

        private GameObject _sunSmokeFX;

        private void CleanupSunSmoke()
        {
            if (_sunSmokeFX != null)
            {
                Destroy(_sunSmokeFX);
                _sunSmokeFX = null;
            }
        }

        /// <summary>
        /// Called when the enemy dies.
        /// </summary>
        public void OnDeath()
        {
            CleanupSunSmoke();
            PlaySound(_deathSound);
            DropLoot();
            Destroy(gameObject, 0.15f);
        }

        /// <summary>
        /// Play attack animation and schedule melee hit confirmation.
        /// </summary>
        public void PlayAttackAnimation(float windupDuration)
        {
            PlaySound(_attackSound);
            _animator?.PlayAttack(windupDuration);
            StartCoroutine(MeleeHitRoutine(windupDuration));
        }

        private IEnumerator MeleeHitRoutine(float windupDuration)
        {
            yield return new WaitForSeconds(windupDuration);
            if (_managerIndex >= 0 && EnemyManager.Instance != null)
                EnemyManager.Instance.ConfirmMeleeHit(_managerIndex);
        }

        // ─── Loot ───────────────────────────────────────────────────────

        private void DropLoot()
        {
            if (_definition == null || _definition.lootTable == null) return;

            foreach (LootEntry entry in _definition.lootTable)
            {
                if (entry.item == null) continue;
                if (Random.value > entry.dropChance) continue;

                int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                if (qty <= 0) continue;

                Vector3 pos = transform.position + Vector3.up * 0.5f
                    + Random.insideUnitSphere * 0.4f;
                WorldItemSpawner.SpawnItem(new ItemStack(entry.item, qty), pos);
            }
        }

        // ─── Audio/VFX ─────────────────────────────────────────────────

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void SpawnHitEffect(Vector3 point, Vector3 normal)
        {
            if (HitEffects.Instance != null)
            {
                HitEffects.Instance.SpawnHitEffect(point, normal, SurfaceType.Enemy);
                return;
            }

            // Fallback procedural burst
            Color hitColor = new Color(0.8f, 0.1f, 0.1f);
            GameObject fx = new GameObject("EnemyHitFX");
            fx.transform.position = point;
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                if (_fallbackHitMaterial == null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");
                    if (sh != null)
                    {
                        _fallbackHitMaterial = new Material(sh);
                        _fallbackHitMaterial.SetColor("_BaseColor", hitColor);
                        _fallbackHitMaterial.color = hitColor;
                    }
                }
                if (_fallbackHitMaterial != null)
                    psr.material = _fallbackHitMaterial;
            }

            var main = ps.main;
            main.startColor    = hitColor;
            main.startSpeed    = 3f;
            main.startSize     = 0.05f;
            main.duration      = 0.2f;
            main.loop          = false;
            main.startLifetime = 0.3f;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
            ps.Play();
            Destroy(fx, 1f);
        }

        // ─── Gizmos ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_definition == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _definition.detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _definition.attackRange);
        }
#endif
    }
}
