using UnityEngine;

namespace Voidborne.Combat.Projectiles
{
    [CreateAssetMenu(menuName = "Voidborne/Combat/Projectile Definition", fileName = "NewProjectile")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string projectileName = "Arrow";

        [Header("Damage")]
        [Tooltip("Base damage at maximum launch velocity.")]
        public float baseDamage = 30f;

        [Header("Physics")]
        [Tooltip("Initial speed in m/s imparted by the launcher.")]
        public float launchVelocity = 40f;

        [Tooltip("Multiplier on Physics gravity (9.81). 0 = no drop, 1 = real gravity.")]
        public float gravityMultiplier = 1f;

        [Tooltip("Linear drag coefficient. Velocity reduced by drag * velocity * dt each step.")]
        public float drag = 0.05f;

        [Tooltip("Seconds until the projectile is returned to the pool if it hasn't hit anything.")]
        public float lifetime = 8f;

        [Header("Impact")]
        [Tooltip("How deeply the projectile embeds into a surface (world units). 0 = no embed, just vanish.")]
        public float penetrationDepth = 0.15f;

        [Tooltip("If true the projectile bounces off non-enemy surfaces. If false it embeds.")]
        public bool bounceOnSurface = false;

        [Tooltip("Energy retained per bounce (0–1).")]
        [Range(0f, 1f)]
        public float bounciness = 0.6f;

        [Tooltip("Can the player walk up and retrieve this projectile from the world?")]
        public bool isRetrievable = true;

        [Header("Visuals")]
        [Tooltip("Optional prefab to instantiate as the projectile's visible model (parented to the Projectile transform).")]
        public GameObject modelPrefab;

        [Tooltip("Optional trail effect prefab spawned as a child on launch.")]
        public GameObject trailEffectPrefab;

        [Tooltip("Optional impact effect prefab spawned at hit point. Falls back to HitEffects if null.")]
        public GameObject impactEffectPrefab;

        [Header("Spin / Blade")]
        [Tooltip("If true the projectile tumbles in flight instead of orienting to velocity.")]
        public bool tumbles = false;

        [Tooltip("Degrees per second of tumble rotation (around the projectile's right axis).")]
        public float tumbleSpeed = 360f;

        [Tooltip("If true, damage is halved when the handle hits instead of the blade.")]
        public bool checkBladeOrientation = false;

        [Tooltip("Damage multiplier applied when the handle/butt hits (not the blade/tip). 0–1.")]
        [Range(0f, 1f)]
        public float handleDamageMultiplier = 0.4f;
    }
}
