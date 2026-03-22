using UnityEngine;

namespace Voidborne.Combat.Projectiles
{
    [CreateAssetMenu(menuName = "Voidborne/Combat/Throwable Definition", fileName = "NewThrowable")]
    public class ThrowableDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName = "Throwable";

        [Header("Projectile")]
        [Tooltip("Projectile definition used while the weapon is in flight.")]
        public ProjectileDefinition projectileDefinition;

        [Header("Throw")]
        [Tooltip("Hold time (seconds) before the throw becomes ready to release.")]
        public float windupTime = 0.35f;

        [Tooltip("Launch speed in m/s.")]
        public float throwVelocity = 38f;

        [Header("Spin")]
        [Tooltip("If true the weapon tumbles end-over-end in flight (axe). " +
                 "If false it flies stable nose-forward (spear).")]
        public bool tumbles = false;

        [Tooltip("Rotation speed in degrees per second when tumbling.")]
        public float tumbleSpeed = 360f;

        [Header("Blade Impact")]
        [Tooltip("Damage multiplier when the blade end hits. Only relevant when tumbles = true.")]
        public float bladeHitMultiplier = 1.0f;

        [Tooltip("Damage multiplier when the handle end hits. Only relevant when tumbles = true.")]
        public float handleHitMultiplier = 0.4f;
    }
}
