using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Combat.Projectiles
{
    /// <summary>
    /// Singleton object pool for Projectile instances. Each ProjectileDefinition
    /// has its own sub-pool so different projectile types are managed independently.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static ProjectilePool Instance { get; private set; }

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
        // Inspector
        // -----------------------------------------------------------------------

        [Tooltip("Initial number of Projectile GameObjects pre-allocated per definition.")]
        [SerializeField] private int initialPoolSizePerDef = 10;

        // -----------------------------------------------------------------------
        // Internal pool
        // -----------------------------------------------------------------------

        // Key: ProjectileDefinition instance ID → pool of inactive Projectile objects
        private readonly Dictionary<int, Queue<Projectile>> _pools
            = new Dictionary<int, Queue<Projectile>>();

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Retrieves a pooled Projectile, launches it, and returns the instance.
        /// </summary>
        /// <param name="def">The definition that describes this projectile.</param>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="direction">Normalised launch direction.</param>
        /// <param name="owner">The GameObject firing the shot.</param>
        public Projectile Spawn(ProjectileDefinition def, Vector3 position, Vector3 direction, GameObject owner)
        {
            if (def == null)
            {
                Debug.LogWarning("[ProjectilePool] Spawn called with null ProjectileDefinition.");
                return null;
            }

            Projectile p = GetFromPool(def);
            p.gameObject.SetActive(true);
            p.transform.SetParent(transform, false); // keep parented to pool root when active
            p.Launch(def, position, direction, owner);
            return p;
        }

        /// <summary>
        /// Overload that launches the projectile at a custom speed instead of def.launchVelocity.
        /// </summary>
        public Projectile Spawn(ProjectileDefinition def, Vector3 position, Vector3 direction, GameObject owner, float speedOverride)
        {
            if (def == null)
            {
                Debug.LogWarning("[ProjectilePool] Spawn called with null ProjectileDefinition.");
                return null;
            }

            Projectile p = GetFromPool(def);
            p.gameObject.SetActive(true);
            p.transform.SetParent(transform, false);
            p.Launch(def, position, direction, owner, speedOverride);
            return p;
        }

        /// <summary>
        /// Explicitly returns a projectile to the pool. You can also just call p.ReturnToPool()
        /// which calls this method internally.
        /// </summary>
        public void Return(ProjectileDefinition def, Projectile projectile)
        {
            if (def == null || projectile == null) return;
            projectile.ReturnToPool();
            GetPool(def).Enqueue(projectile);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private Projectile GetFromPool(ProjectileDefinition def)
        {
            Queue<Projectile> pool = GetPool(def);

            // Drain stale (null) entries that may have been Destroy()-ed externally
            while (pool.Count > 0 && pool.Peek() == null)
                pool.Dequeue();

            if (pool.Count > 0)
                return pool.Dequeue();

            return CreateInstance(def);
        }

        private Queue<Projectile> GetPool(ProjectileDefinition def)
        {
            int key = def.GetInstanceID();
            if (!_pools.TryGetValue(key, out Queue<Projectile> pool))
            {
                pool = new Queue<Projectile>(initialPoolSizePerDef);
                _pools[key] = pool;
                // Pre-warm
                for (int i = 0; i < initialPoolSizePerDef; i++)
                {
                    Projectile p = CreateInstance(def);
                    pool.Enqueue(p);
                }
            }
            return pool;
        }

        private Projectile CreateInstance(ProjectileDefinition def)
        {
            GameObject go = new GameObject($"Projectile_{def.projectileName}");
            go.transform.SetParent(transform, false);

            // Give it a tiny sphere collider so Physics queries work, but mark as
            // trigger so it doesn't push rigidbodies — collision is handled by manual Raycast.
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.radius    = 0.04f;
            col.isTrigger = true;

            Projectile proj = go.AddComponent<Projectile>();
            go.SetActive(false);
            return proj;
        }
    }
}
