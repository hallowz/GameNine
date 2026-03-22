using UnityEngine;

namespace Voidborne.Combat.Projectiles
{
    /// <summary>
    /// Physical projectile that arcs under gravity, applies velocity-scaled damage on impact,
    /// and either embeds in or bounces off surfaces. Managed by ProjectilePool.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private ProjectileDefinition _def;
        private Vector3              _velocity;
        private GameObject           _owner;
        private float                _timeAlive;
        private bool                 _active;
        private bool                 _embedded;

        // Embed callback — fired once when the projectile embeds in a surface
        // bool arg = true if the target was an IDamageable enemy
        public System.Action<Vector3, bool> OnEmbedded;

        // child objects created on launch (model, trail)
        private GameObject _modelInstance;
        private GameObject _trailInstance;

        // -----------------------------------------------------------------------
        // Launch API (called by ProjectilePool)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Initialises and activates the projectile.
        /// </summary>
        /// <param name="def">Projectile configuration.</param>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="direction">Normalised launch direction.</param>
        /// <param name="owner">The GameObject that fired this projectile.</param>
        public void Launch(ProjectileDefinition def, Vector3 position, Vector3 direction, GameObject owner, float speedOverride = -1f)
        {
            _def      = def;
            float speed = speedOverride > 0f ? speedOverride : def.launchVelocity;
            _velocity = direction.normalized * speed;
            _owner    = owner;
            _timeAlive = 0f;
            _active    = true;
            _embedded  = false;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

            // Spawn visuals
            SpawnVisuals();
        }

        // -----------------------------------------------------------------------
        // Simulation
        // -----------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (!_active || _embedded) return;

            float dt = Time.fixedDeltaTime;

            // Lifetime check
            _timeAlive += dt;
            if (_timeAlive >= _def.lifetime)
            {
                ReturnToPool();
                return;
            }

            // Apply gravity
            _velocity.y -= Physics.gravity.magnitude * _def.gravityMultiplier * dt;

            // Apply drag (linear approximation)
            _velocity -= _velocity * (_def.drag * dt);

            // Determine movement this step
            Vector3 movement = _velocity * dt;
            float   distance = movement.magnitude;

            if (distance < 0.0001f) return;

            // Sweep raycast along the movement vector
            if (Physics.Raycast(transform.position, movement.normalized, out RaycastHit hit, distance + 0.05f,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                HandleImpact(hit);
            }
            else
            {
                transform.position += movement;
                // Orient along velocity
                if (_def.tumbles)
                    transform.Rotate(Vector3.right, _def.tumbleSpeed * dt, Space.Self);
                else if (_velocity.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(_velocity);
            }
        }

        // -----------------------------------------------------------------------
        // Impact
        // -----------------------------------------------------------------------

        private void HandleImpact(RaycastHit hit)
        {
            // Damage scales with current speed as a fraction of launch velocity.
            float speedRatio  = Mathf.Clamp01(_velocity.magnitude / Mathf.Max(_def.launchVelocity, 1f));

            // Blade-vs-handle orientation check for tumbling weapons
            float orientMult = 1f;
            if (_def.checkBladeOrientation && _def.tumbles)
            {
                // If the projectile's forward is against the velocity direction, handle is hitting
                float dot = Vector3.Dot(transform.forward, _velocity.normalized);
                if (dot < 0f) orientMult = _def.handleDamageMultiplier;
            }
            float finalDamage = _def.baseDamage * speedRatio * orientMult;

            // Try to deal damage
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo info = new DamageInfo
                {
                    Amount    = finalDamage,
                    HitPoint  = hit.point,
                    HitNormal = hit.normal,
                    Type      = DamageType.Projectile,
                    Attacker  = _owner
                };
                target.TakeDamage(info);
            }

            // Spawn impact effect
            SpawnImpactEffect(hit);

            // Embed or bounce
            bool hitEnemy = target != null;
            if (!hitEnemy && _def.bounceOnSurface)
            {
                Bounce(hit);
            }
            else
            {
                EmbedAt(hit);
            }
        }

        private void Bounce(RaycastHit hit)
        {
            _velocity = Vector3.Reflect(_velocity, hit.normal) * _def.bounciness;
            transform.position = hit.point + hit.normal * 0.02f;
            if (_velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_velocity);
        }

        private void EmbedAt(RaycastHit hit)
        {
            _embedded = true;
            _active   = false;

            // Position the tip at the surface and push it in by penetrationDepth
            Vector3 embedPos = hit.point - transform.forward * _def.penetrationDepth;
            transform.SetPositionAndRotation(embedPos, Quaternion.LookRotation(-hit.normal));

            // Fire embed callback then detach from pool hierarchy so it stays in the world
            bool hitEnemy2 = hit.collider.GetComponentInParent<IDamageable>() != null;
            OnEmbedded?.Invoke(hit.point, hitEnemy2);
            OnEmbedded = null; // one-shot
            transform.SetParent(null, true);

            // Destroy trail (keep model so the player can see and retrieve it)
            if (_trailInstance != null)
                Destroy(_trailInstance);

            // If not retrievable, or on an enemy, return to pool after a delay
            if (!_def.isRetrievable || hit.collider.GetComponentInParent<IDamageable>() != null)
            {
                Destroy(gameObject, 4f);
            }
            // Otherwise stays in world until the player picks it up — no auto-destroy.
        }

        // -----------------------------------------------------------------------
        // Visuals
        // -----------------------------------------------------------------------

        private void SpawnVisuals()
        {
            // Clean up any previous visuals
            if (_modelInstance != null) Destroy(_modelInstance);
            if (_trailInstance  != null) Destroy(_trailInstance);

            if (_def.modelPrefab != null)
            {
                _modelInstance = Instantiate(_def.modelPrefab, transform);
                _modelInstance.transform.localPosition = Vector3.zero;
                _modelInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Procedural fallback: capsule oriented along the forward axis.
                // Tumbling weapons get a wider/shorter shape; stable ones get a thin rod.
                _modelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _modelInstance.transform.SetParent(transform);
                _modelInstance.transform.localPosition = Vector3.zero;
                _modelInstance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _modelInstance.transform.localScale = _def.tumbles
                    ? new Vector3(0.08f, 0.12f, 0.08f)  // axe: compact tumbling shape
                    : new Vector3(0.03f, 0.25f, 0.03f); // arrow/bolt/spear: thin rod
                Destroy(_modelInstance.GetComponent<Collider>()); // use our own raycast
            }

            if (_def.trailEffectPrefab != null)
            {
                _trailInstance = Instantiate(_def.trailEffectPrefab, transform);
                _trailInstance.transform.localPosition = Vector3.zero;
            }
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            if (_def.impactEffectPrefab != null)
            {
                Quaternion rot = hit.normal != Vector3.zero
                    ? Quaternion.LookRotation(hit.normal)
                    : Quaternion.identity;
                GameObject fx = Instantiate(_def.impactEffectPrefab, hit.point, rot);
                Destroy(fx, 3f);
            }
            else
            {
                // Fall back to the global HitEffects system
                SurfaceType surface = HitEffects.GetSurfaceType(hit.collider);
                HitEffects.Instance?.SpawnHitEffect(hit.point, hit.normal, surface);
            }
        }

        // -----------------------------------------------------------------------
        // Pool recycling
        // -----------------------------------------------------------------------

        /// <summary>Returns this projectile to the pool. Called by ProjectilePool.</summary>
        public void ReturnToPool()
        {
            OnEmbedded = null;
            _active   = false;
            _embedded = false;

            if (_modelInstance != null) Destroy(_modelInstance);
            if (_trailInstance  != null) Destroy(_trailInstance);
            _modelInstance = null;
            _trailInstance = null;

            gameObject.SetActive(false);
        }
    }
}
