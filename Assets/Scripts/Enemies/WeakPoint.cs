using UnityEngine;
using Voidborne.Combat;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Attach to a child collider placed on the BACK of an enemy (e.g. OptimizedHeavy).
    /// Intercepts IDamageable calls on that collider, applies a bonus multiplier when the
    /// attacker is behind the enemy, then forwards the modified hit to the root EnemyEntity.
    ///
    /// Tag the child GameObject "Enemy" so HitEffects classifies it correctly.
    ///
    /// The Shard Index module (future) will highlight this component visually.
    /// </summary>
    [AddComponentMenu("Voidborne/Enemies/Weak Point")]
    public class WeakPoint : MonoBehaviour, IDamageable
    {
        [Tooltip("Damage multiplier when hit from behind the enemy.")]
        [SerializeField] private float _multiplier = 2.5f;

        [Tooltip("Dot-product threshold that defines 'from behind'. -0.2 ≈ 100° rear cone.")]
        [SerializeField] private float _backDotThreshold = -0.2f;

        private EnemyEntity _root;

        private void Awake()
        {
            _root = GetComponentInParent<EnemyEntity>();
            if (_root == null)
                Debug.LogError($"[WeakPoint] {name}: no EnemyEntity found in parent hierarchy.", this);
        }

        /// <summary>
        /// IDamageable implementation — intercepts hits on this collider.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_root == null || _root.IsDead) return;

            // Determine whether the hit came from behind
            Transform root = transform.root;
            Vector3 toHit = (info.HitPoint - root.position).normalized;
            float dot = Vector3.Dot(root.forward, toHit);

            if (dot < _backDotThreshold)
            {
                info.Amount *= _multiplier;
            }

            _root.TakeDamage(info);
        }
    }
}
