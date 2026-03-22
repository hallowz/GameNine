using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Surface material categories used by hit-effect and damage systems.
    /// </summary>
    public enum SurfaceType
    {
        Default,
        Terrain,
        Metal,
        Wood,
        Enemy
    }

    /// <summary>
    /// Singleton MonoBehaviour that spawns impact VFX at hit points.
    /// Assign prefabs in the Inspector; any null prefab falls back to a
    /// procedurally-generated particle burst so effects always play.
    /// </summary>
    public class HitEffects : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static HitEffects Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------

        [Tooltip("Particle effect spawned when hitting terrain or default surfaces.")]
        [SerializeField] private GameObject dustImpactPrefab;

        [Tooltip("Particle effect spawned when hitting metal surfaces.")]
        [SerializeField] private GameObject metalSparkPrefab;

        [Tooltip("Particle effect spawned when hitting enemies.")]
        [SerializeField] private GameObject bloodParticlePrefab;

        [Tooltip("Decal projected onto terrain/wall surfaces.")]
        [SerializeField] private GameObject bulletHoleDecalPrefab;

        [Tooltip("Seconds before spawned effect objects are destroyed.")]
        [SerializeField] private float effectLifetime = 2f;

        // Fallback particle colors per surface
        private static readonly Color FallbackDustColor   = new Color(0.80f, 0.72f, 0.55f);
        private static readonly Color FallbackSparkColor  = new Color(1.00f, 0.85f, 0.20f);
        private static readonly Color FallbackBloodColor  = new Color(0.75f, 0.05f, 0.05f);

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Spawns the appropriate impact VFX at the given world-space position,
        /// oriented so the effect faces outward along the surface normal.
        /// </summary>
        public void SpawnHitEffect(Vector3 position, Vector3 normal, SurfaceType surface)
        {
            Quaternion rotation = normal != Vector3.zero
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            switch (surface)
            {
                case SurfaceType.Enemy:
                    SpawnAndDestroy(bloodParticlePrefab, position, rotation, FallbackBloodColor, 12);
                    break;

                case SurfaceType.Metal:
                    SpawnAndDestroy(metalSparkPrefab, position, rotation, FallbackSparkColor, 15, speed: 6f, size: 0.04f);
                    break;

                case SurfaceType.Wood:
                    SpawnAndDestroy(dustImpactPrefab, position, rotation, FallbackDustColor, 10);
                    break;

                case SurfaceType.Terrain:
                case SurfaceType.Default:
                default:
                    SpawnAndDestroy(dustImpactPrefab, position, rotation, FallbackDustColor, 12);
                    // Only spawn a decal if a prefab is provided (no procedural decal fallback).
                    if (bulletHoleDecalPrefab != null)
                        Instantiate(bulletHoleDecalPrefab, position, rotation);
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Static helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Determines the surface type of a collider from its GameObject's tag.
        /// </summary>
        public static SurfaceType GetSurfaceType(Collider col)
        {
            if (col == null)
                return SurfaceType.Default;

            string tag = col.tag;

            if (tag == "Enemy")   return SurfaceType.Enemy;
            if (tag == "Head")    return SurfaceType.Enemy;
            if (tag == "Limb")    return SurfaceType.Enemy;
            if (tag == "Metal")   return SurfaceType.Metal;
            if (tag == "Wood")    return SurfaceType.Wood;
            if (tag == "Terrain") return SurfaceType.Terrain;

            return SurfaceType.Default;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void SpawnAndDestroy(
            GameObject prefab, Vector3 position, Quaternion rotation,
            Color fallbackColor, int fallbackCount,
            float speed = 4f, float size = 0.06f)
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, position, rotation);
                Destroy(instance, effectLifetime);
                return;
            }

            // No prefab assigned — spawn a procedural burst so hits always feel responsive.
            SpawnProceduralBurst(position, rotation, fallbackColor, fallbackCount, speed, size);
        }

        /// <summary>
        /// Creates a short-lived ParticleSystem burst at the hit point.
        /// Called as a fallback when no prefab is assigned.
        /// </summary>
        private void SpawnProceduralBurst(
            Vector3 position, Quaternion rotation,
            Color color, int count, float speed, float size)
        {
            GameObject go = new GameObject("HitEffect_Proc");
            go.transform.SetPositionAndRotation(position, rotation);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            // Assign a URP-compatible particle material so effects aren't pink.
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Sprites/Default");
                if (particleShader != null)
                {
                    var mat = new Material(particleShader);
                    mat.SetColor("_BaseColor", color);
                    mat.color = color;
                    psr.material = mat;
                }
            }

            // ParticleSystem starts playing immediately on AddComponent because
            // playOnAwake defaults to true. Stop and clear it before configuring
            // so we can set duration and other properties without Unity warnings.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration         = 0.05f;
            main.loop             = false;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed       = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize        = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.5f);
            main.startColor       = new ParticleSystem.MinMaxGradient(color * 0.7f, color);
            main.maxParticles     = count + 5;
            main.gravityModifier  = 0.5f;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, (short)count)
            });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius    = 0.02f;

            ps.Play();
            Destroy(go, effectLifetime);
        }
    }
}
