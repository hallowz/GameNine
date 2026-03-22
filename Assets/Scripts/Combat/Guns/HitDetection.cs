using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Result data returned by HitDetection.ProcessHit().
    /// </summary>
    public struct HitResult
    {
        /// <summary>The raycast hit a collidable surface (enemy or world geometry).</summary>
        public bool HitSomething;

        /// <summary>The hit object carries an IDamageable component (enemy).</summary>
        public bool HitEnemy;

        /// <summary>Reserved for future use when enemy health tracking is added (4.5+).</summary>
        public bool WasKill;

        /// <summary>The hit landed on a collider tagged "Head".</summary>
        public bool WasHeadshot;

        /// <summary>Actual damage value dealt (after multipliers).</summary>
        public float DamageDone;

        /// <summary>The surface type that was hit.</summary>
        public SurfaceType Surface;
    }

    /// <summary>
    /// Static utility that processes a hitscan RaycastHit:
    ///   - Classifies the surface type.
    ///   - Applies headshot / limb damage multipliers against enemies.
    ///   - Calls IDamageable.TakeDamage on the hit object.
    ///   - Delegates hit-effect spawning to HitEffects.Instance.
    /// </summary>
    public static class HitDetection
    {
        /// <summary>Damage multiplier applied when a collider tagged "Head" is hit.</summary>
        public const float HeadshotMultiplier = 2.0f;

        /// <summary>Damage multiplier applied when a collider tagged "Limb" is hit.</summary>
        public const float LimbMultiplier = 0.75f;

        /// <summary>
        /// Processes a single hitscan hit and returns a populated HitResult.
        /// </summary>
        /// <param name="hit">The RaycastHit produced by Physics.Raycast.</param>
        /// <param name="baseDamage">The unmodified damage value from the gun definition.</param>
        /// <param name="attacker">The GameObject that fired the shot (used in DamageInfo).</param>
        public static HitResult ProcessHit(RaycastHit hit, float baseDamage, GameObject attacker)
        {
            HitResult result = default;
            result.HitSomething = true;

            // Classify the surface that was struck.
            SurfaceType surface = HitEffects.GetSurfaceType(hit.collider);
            result.Surface = surface;

            if (surface == SurfaceType.Enemy)
            {
                result.HitEnemy = true;

                // Determine zone multiplier from the specific collider's tag.
                float finalDamage = baseDamage;
                bool isHeadshot = hit.collider.CompareTag("Head");
                bool isLimb     = hit.collider.CompareTag("Limb");

                if (isHeadshot)
                {
                    finalDamage *= HeadshotMultiplier;
                    result.WasHeadshot = true;
                }
                else if (isLimb)
                {
                    finalDamage *= LimbMultiplier;
                }

                result.DamageDone = finalDamage;

                // Walk up the hierarchy to find an IDamageable (e.g., on the root enemy GO).
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo info = new DamageInfo
                    {
                        Amount    = finalDamage,
                        HitPoint  = hit.point,
                        HitNormal = hit.normal,
                        Type      = DamageType.Bullet,
                        Attacker  = attacker
                    };
                    damageable.TakeDamage(info);
                }

                // Blood effect on the enemy.
                HitEffects.Instance?.SpawnHitEffect(hit.point, hit.normal, SurfaceType.Enemy);

                // WasKill: deferred to Volume 4.5 when enemy health is tracked.
                result.WasKill = false;
            }
            else
            {
                // World-geometry hit — spawn surface-appropriate effect, no damage.
                HitEffects.Instance?.SpawnHitEffect(hit.point, hit.normal, surface);
            }

            return result;
        }
    }
}
