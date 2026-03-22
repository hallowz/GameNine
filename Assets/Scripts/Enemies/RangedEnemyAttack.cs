using UnityEngine;
using Voidborne.Combat.Projectiles;
using Voidborne.Player;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Projectile-based ranged attack for enemies.
    /// Fires during Attack and InCover states. Aiming intelligence tied to EnemyCategory.
    /// </summary>
    [RequireComponent(typeof(EnemyEntity))]
    [AddComponentMenu("Voidborne/Enemies/Ranged Enemy Attack")]
    public class RangedEnemyAttack : MonoBehaviour
    {
        [SerializeField] private ProjectileDefinition _projectileDef;
        [SerializeField] private float _projectileSpeed = 10f;
        [SerializeField] private float _attackRange = 22f;
        [SerializeField] private float _fireCooldownOverride = 2f;
        [SerializeField] private float _eyeHeight = 1.4f;
        [SerializeField] private LayerMask _losBlockers = ~0;
        [SerializeField] private Transform _muzzlePoint;

        [Header("Aim — Optimized")]
        [Range(0f, 1f)]
        [SerializeField] private float _optimizedLeadFraction = 0.95f;
        [SerializeField] private float _optimizedSpread = 3f;

        [Header("Aim — Directed")]
        [Range(0f, 1f)]
        [SerializeField] private float _directedLeadFraction = 0.45f;
        [SerializeField] private float _directedSpread = 10f;

        [Header("Aim — Wild Creature")]
        [Range(0f, 1f)]
        [SerializeField] private float _wildLeadFraction = 0f;
        [SerializeField] private float _wildSpread = 22f;

        [Header("Audio")]
        [SerializeField] private AudioClip _fireSound;

        private EnemyEntity _entity;
        private AudioSource _audio;
        private float       _timer;
        private float       _cooldown;
        private Transform   _playerTransform;

        private Vector3 _playerPrevPos;
        private Vector3 _playerVelocity;

        private void Awake()
        {
            _entity = GetComponent<EnemyEntity>();
            _audio  = GetComponent<AudioSource>();
        }

        private void Start()
        {
            _cooldown = _fireCooldownOverride > 0f
                ? _fireCooldownOverride
                : (_entity.Definition != null ? _entity.Definition.attackCooldown : 2f);

            if (PlayerManager.Instance != null)
            {
                _playerTransform = PlayerManager.Instance.PlayerTransform;
                _playerPrevPos   = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            }

            _timer = Random.Range(0f, _cooldown);
        }

        private void Update()
        {
            if (_entity.IsDead || _playerTransform == null) return;

            // Track player velocity
            Vector3 delta = _playerTransform.position - _playerPrevPos;
            _playerVelocity = Vector3.Lerp(_playerVelocity, delta / Mathf.Max(Time.deltaTime, 0.001f), 8f * Time.deltaTime);
            _playerPrevPos  = _playerTransform.position;

            var state = _entity.CurrentState;
            if (state != EnemyState.Advance && state != EnemyState.Attack
                && state != EnemyState.InCover && state != EnemyState.Flank
                && state != EnemyState.Charge)
                return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > _attackRange) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _cooldown;

            TryFire();
        }

        private void TryFire()
        {
            if (_projectileDef == null || ProjectilePool.Instance == null) return;

            Vector3 muzzlePos  = MuzzlePosition();
            Vector3 playerEyes = _playerTransform.position + Vector3.up * 0.8f;
            float   dist       = Vector3.Distance(muzzlePos, playerEyes);

            if (Physics.Raycast(muzzlePos, (playerEyes - muzzlePos).normalized,
                                out _, dist * 0.9f, _losBlockers,
                                QueryTriggerInteraction.Ignore))
                return;

            Vector3 aimDir = BuildAimDirection(muzzlePos, playerEyes, dist);
            float speed = _projectileSpeed > 0f ? _projectileSpeed : _projectileDef.launchVelocity;
            ProjectilePool.Instance.Spawn(_projectileDef, muzzlePos, aimDir, gameObject, speed);

            if (_audio != null && _fireSound != null)
                _audio.PlayOneShot(_fireSound);

            PlayMuzzleFlash(muzzlePos);
        }

        private Vector3 BuildAimDirection(Vector3 muzzlePos, Vector3 playerEyes, float dist)
        {
            EnemyCategory category = _entity.Definition != null
                ? _entity.Definition.category
                : EnemyCategory.WildCreature;

            float speed = _projectileSpeed > 0f ? _projectileSpeed : _projectileDef.launchVelocity;

            float leadFraction;
            float spreadDeg;

            switch (category)
            {
                case EnemyCategory.Optimized:
                    leadFraction = _optimizedLeadFraction;
                    spreadDeg    = _optimizedSpread;
                    break;
                case EnemyCategory.Directed:
                    leadFraction = _directedLeadFraction;
                    spreadDeg    = _directedSpread;
                    break;
                default:
                    leadFraction = _wildLeadFraction;
                    spreadDeg    = _wildSpread;
                    break;
            }

            float   tof             = dist / Mathf.Max(speed, 0.1f);
            Vector3 predictedTarget = playerEyes + _playerVelocity * tof * leadFraction;
            Vector3 baseDir = (predictedTarget - muzzlePos).normalized;

            if (spreadDeg > 0f)
            {
                float   half  = spreadDeg * 0.5f;
                Vector3 right = Vector3.Cross(baseDir, Vector3.up);
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                right.Normalize();
                Vector3 up2 = Vector3.Cross(right, baseDir).normalized;

                baseDir = Quaternion.AngleAxis(Random.Range(-half, half), up2)
                        * Quaternion.AngleAxis(Random.Range(-half, half), right)
                        * baseDir;
            }

            return baseDir.normalized;
        }

        private Vector3 MuzzlePosition()
        {
            if (_muzzlePoint != null) return _muzzlePoint.position;
            return transform.position + Vector3.up * _eyeHeight;
        }

        private void PlayMuzzleFlash(Vector3 position)
        {
            Color flashColor = new Color(0.3f, 0.8f, 1f);

            GameObject fx = new GameObject("MuzzleFX");
            fx.transform.position = position;
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    mat.SetColor("_BaseColor", flashColor);
                    mat.color = flashColor;
                    psr.material = mat;
                }
            }

            var main = ps.main;
            main.startColor    = flashColor;
            main.startSpeed    = 4f;
            main.startSize     = 0.04f;
            main.duration      = 0.12f;
            main.loop          = false;
            main.startLifetime = 0.2f;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
            ps.Play();

            Destroy(fx, 0.5f);
        }
    }
}
