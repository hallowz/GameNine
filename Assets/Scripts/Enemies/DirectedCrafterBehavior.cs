using UnityEngine;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Additional behavior layer for the DirectedCrafter enemy type.
    /// Scans for damaged Optimized allies and repairs them.
    /// High-priority kill target — eliminating it stops field repairs.
    /// </summary>
    [RequireComponent(typeof(EnemyEntity))]
    [AddComponentMenu("Voidborne/Enemies/Directed Crafter Behavior")]
    public class DirectedCrafterBehavior : MonoBehaviour
    {
        [SerializeField] private float _scanRadius = 10f;
        [SerializeField] private float _repairProximity = 1.8f;
        [SerializeField] private float _repairAmount = 20f;
        [SerializeField] private float _repairInterval = 1.5f;

        private EnemyEntity _entity;
        private int         _repairTargetIndex = -1;
        private float       _repairTimer;
        private bool        _isRepairing;

        public bool IsActivelyRepairing => _isRepairing;

        private void Awake()
        {
            _entity = GetComponent<EnemyEntity>();
        }

        private void Update()
        {
            if (_entity.IsDead) return;
            if (EnemyManager.Instance == null) return;
            if (_entity.ManagerIndex < 0) return;

            var state = _entity.CurrentState;
            if (state == EnemyState.Attack || state == EnemyState.Flee || state == EnemyState.Dead)
            {
                _isRepairing = false;
                _repairTargetIndex = -1;
                return;
            }

            _repairTimer -= Time.deltaTime;

            // Validate current target
            if (!IsRepairTargetValid())
            {
                _repairTargetIndex = EnemyManager.Instance.FindMostDamagedOptimized(
                    transform.position, _scanRadius, _entity.ManagerIndex);
            }

            if (_repairTargetIndex < 0)
            {
                _isRepairing = false;
                return;
            }

            ref var target = ref EnemyManager.Instance.GetData(_repairTargetIndex);
            float dist = Vector3.Distance(transform.position,
                new Vector3(target.position.x, target.position.y, target.position.z));

            if (dist > _repairProximity)
            {
                _isRepairing = false;
                // Override brain steering to move toward repair target
                if (_entity.ManagerIndex >= 0)
                {
                    ref var self = ref EnemyManager.Instance.GetData(_entity.ManagerIndex);
                    var toTarget = target.position - self.position;
                    if (Unity.Mathematics.math.lengthsq(toTarget) > 0.01f)
                    {
                        self.desiredDirection = Unity.Mathematics.math.normalize(toTarget);
                        self.currentSpeed = self.stats.moveSpeed * self.stats.weights.speedMult;
                    }
                }
            }
            else
            {
                _isRepairing = true;
                // Stop moving
                if (_entity.ManagerIndex >= 0)
                {
                    ref var self = ref EnemyManager.Instance.GetData(_entity.ManagerIndex);
                    self.desiredDirection = Unity.Mathematics.float3.zero;
                    self.currentSpeed = 0f;
                }

                if (_repairTimer <= 0f)
                {
                    _repairTimer = _repairInterval;
                    EnemyManager.Instance.HealEnemy(_repairTargetIndex, _repairAmount);
                    SpawnRepairEffect(new Vector3(target.position.x, target.position.y, target.position.z));
                }
            }
        }

        private bool IsRepairTargetValid()
        {
            if (_repairTargetIndex < 0 || EnemyManager.Instance == null) return false;
            if (_repairTargetIndex >= EnemyManager.Instance.ActiveCount) return false;

            ref var target = ref EnemyManager.Instance.GetData(_repairTargetIndex);
            return target.isActive
                && target.state != EnemyState.Dead
                && target.health < target.stats.maxHealth;
        }

        private void SpawnRepairEffect(Vector3 position)
        {
            Color repairColor = new Color(0.2f, 1f, 0.4f);

            GameObject fx = new GameObject("RepairFX");
            fx.transform.position = position + Vector3.up * 1f;
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
                    mat.SetColor("_BaseColor", repairColor);
                    mat.color = repairColor;
                    psr.material = mat;
                }
            }

            var main = ps.main;
            main.startColor    = repairColor;
            main.startSpeed    = 1.5f;
            main.startSize     = 0.05f;
            main.duration      = 0.5f;
            main.loop          = false;
            main.startLifetime = 0.5f;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            ps.Play();

            Destroy(fx, 1f);
        }
    }
}
