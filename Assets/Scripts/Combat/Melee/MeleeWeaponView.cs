using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Procedural first-person weapon viewmodel for melee weapons.
    ///
    /// Attach to the PlayerCamera GameObject (same object as FirstPersonCamera).
    ///
    /// At runtime:
    ///   • Creates a WeaponHolder child under the camera.
    ///   • Detects when MeleeController.EquippedWeapon changes and spawns/destroys
    ///     a procedural Unity-primitive mesh for that weapon type.
    ///   • Each LateUpdate, drives the WeaponHolder position + rotation to produce
    ///     directional swing animations, idle sway from mouse input, and movement bob.
    ///
    /// No Animator or AnimationClip assets are required — everything is code-driven.
    /// </summary>
    public class MeleeWeaponView : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("References (auto-resolved from parent hierarchy)")]
        [SerializeField] private MeleeController meleeController;
        [SerializeField] private FirstPersonController fpc;

        [Header("Idle Pose")]
        [Tooltip("Weapon holder position in camera-local space when idle.")]
        [SerializeField] private Vector3 idlePosition = new Vector3(0.25f, -0.25f, 0.45f);

        [Header("Sway")]
        [Tooltip("How much the weapon drifts opposite to mouse movement.")]
        [SerializeField] private float swayAmount    = 0.025f;
        [Tooltip("How quickly the sway offset returns to zero.")]
        [SerializeField] private float swaySmoothing = 8f;

        [Header("Bob")]
        [Tooltip("Amplitude of the movement-driven bob.")]
        [SerializeField] private float bobAmount = 0.012f;
        [Tooltip("Speed of the bob cycle. Scales with player velocity.")]
        [SerializeField] private float bobSpeed  = 9f;

        [Header("Animation Speed (lerp rate)")]
        [SerializeField] private float windupLerpSpeed   = 12f;
        [SerializeField] private float releaseLerpSpeed  = 30f;
        [SerializeField] private float recoveryLerpSpeed =  7f;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private Transform        _weaponHolder;   // child of this (camera) transform
        private GameObject       _modelRoot;       // current weapon mesh hierarchy
        private MeleeDefinition  _lastDef;         // tracks equipped weapon changes

        private Vector3 _currentSwayOffset;
        private float   _bobTimer;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            // Auto-resolve up the hierarchy (MeleeWeaponView is on PlayerCamera,
            // MeleeController and FirstPersonController are on the Player parent).
            if (meleeController == null)
                meleeController = GetComponentInParent<MeleeController>();
            if (fpc == null)
                fpc = GetComponentInParent<FirstPersonController>();

            // Create the weapon holder as a camera-local child.
            var holderGO = new GameObject("MeleeWeaponHolder");
            _weaponHolder = holderGO.transform;
            _weaponHolder.SetParent(transform, false);
            _weaponHolder.localPosition = idlePosition;
            _weaponHolder.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            // Poll for weapon changes — rebuild model when definition changes.
            MeleeDefinition current = meleeController != null ? meleeController.EquippedWeapon : null;
            if (current != _lastDef)
            {
                _lastDef = current;
                RebuildModel(current);
            }

            // Hide the holder entirely when no melee weapon is equipped.
            if (_weaponHolder != null)
                _weaponHolder.gameObject.SetActive(_lastDef != null);
        }

        private void LateUpdate()
        {
            if (_weaponHolder == null || _lastDef == null) return;

            // Read attack state.
            AttackPhase     phase    = meleeController != null ? meleeController.Phase    : AttackPhase.Idle;
            AttackDirection dir      = meleeController != null ? meleeController.CurrentDirection : AttackDirection.Stab;

            // Resolve target pose from attack phase + direction.
            GetTargetPose(phase, dir, out Vector3 targetPos, out Vector3 targetEuler);

            // Layer sway and bob on top of the attack pose (only at Idle/Recovery).
            if (phase == AttackPhase.Idle || phase == AttackPhase.Recovery)
            {
                targetPos += ComputeSway();
                targetPos.y += ComputeBob();
            }

            // Lerp speed varies by phase for punch and settle feel.
            float speed = phase switch
            {
                AttackPhase.Windup   => windupLerpSpeed,
                AttackPhase.Release  => releaseLerpSpeed,
                _                    => recoveryLerpSpeed
            };

            _weaponHolder.localPosition = Vector3.Lerp(
                _weaponHolder.localPosition, targetPos, Time.deltaTime * speed);
            _weaponHolder.localRotation = Quaternion.Slerp(
                _weaponHolder.localRotation, Quaternion.Euler(targetEuler), Time.deltaTime * speed);
        }

        // -------------------------------------------------------------------------
        // Pose definitions
        // -------------------------------------------------------------------------

        private void GetTargetPose(AttackPhase phase, AttackDirection dir,
            out Vector3 pos, out Vector3 euler)
        {
            switch (phase)
            {
                case AttackPhase.Windup:
                    GetWindupPose(dir, out pos, out euler);
                    break;
                case AttackPhase.Release:
                    GetReleasePose(dir, out pos, out euler);
                    break;
                default: // Idle and Recovery both target neutral
                    pos   = idlePosition;
                    euler = Vector3.zero;
                    break;
            }
        }

        /// <summary>
        /// Windup — weapon cocks in the OPPOSITE direction of the swing so the
        /// release feels like a real follow-through.
        /// </summary>
        private void GetWindupPose(AttackDirection dir, out Vector3 pos, out Vector3 euler)
        {
            switch (dir)
            {
                case AttackDirection.Right:     // cocked to right, ready to sweep left
                    pos   = idlePosition + new Vector3( 0.12f,  0.06f, -0.05f);
                    euler = new Vector3(-8f,  38f, -22f);
                    break;
                case AttackDirection.Left:      // cocked to left, ready to sweep right
                    pos   = idlePosition + new Vector3(-0.08f,  0.06f, -0.05f);
                    euler = new Vector3(-8f, -38f,  22f);
                    break;
                case AttackDirection.Overhead:  // raised, ready to chop down
                    pos   = idlePosition + new Vector3( 0.00f,  0.20f, -0.08f);
                    euler = new Vector3(-58f,  0f,  0f);
                    break;
                case AttackDirection.Stab:      // pulled back, ready to thrust
                    pos   = idlePosition + new Vector3( 0.00f,  0.00f, -0.22f);
                    euler = Vector3.zero;
                    break;
                default:
                    pos   = idlePosition;
                    euler = Vector3.zero;
                    break;
            }
        }

        /// <summary>
        /// Release — weapon swings through fast in the attack direction.
        /// </summary>
        private void GetReleasePose(AttackDirection dir, out Vector3 pos, out Vector3 euler)
        {
            switch (dir)
            {
                case AttackDirection.Right:     // follows through to left
                    pos   = idlePosition + new Vector3(-0.24f, -0.10f,  0.06f);
                    euler = new Vector3(14f, -68f,  32f);
                    break;
                case AttackDirection.Left:      // follows through to right
                    pos   = idlePosition + new Vector3( 0.30f, -0.10f,  0.06f);
                    euler = new Vector3(14f,  68f, -32f);
                    break;
                case AttackDirection.Overhead:
                    pos   = idlePosition + new Vector3( 0.00f, -0.24f,  0.10f);
                    euler = new Vector3(52f,  0f,  0f);
                    break;
                case AttackDirection.Stab:
                    pos   = idlePosition + new Vector3( 0.00f,  0.00f,  0.30f);
                    euler = Vector3.zero;
                    break;
                default:
                    pos   = idlePosition;
                    euler = Vector3.zero;
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Sway & Bob
        // -------------------------------------------------------------------------

        private Vector3 ComputeSway()
        {
            if (Mouse.current == null) return Vector3.zero;

            Vector2 delta = Mouse.current.delta.ReadValue();

            // Target sway drifts opposite to mouse direction, then decays to zero.
            float targetX = -delta.x * swayAmount * 0.008f;
            float targetY = -delta.y * swayAmount * 0.008f;

            _currentSwayOffset = Vector3.Lerp(
                _currentSwayOffset,
                new Vector3(targetX, targetY, 0f),
                Time.deltaTime * swaySmoothing);

            return _currentSwayOffset;
        }

        private float ComputeBob()
        {
            if (fpc == null) return 0f;

            float speed = fpc.Velocity.magnitude;
            if (speed < 0.2f)
            {
                _bobTimer = 0f;
                return 0f;
            }

            _bobTimer += Time.deltaTime * bobSpeed * Mathf.Clamp01(speed / 5f);
            return Mathf.Sin(_bobTimer) * bobAmount;
        }

        // -------------------------------------------------------------------------
        // Procedural model construction
        // -------------------------------------------------------------------------

        private void RebuildModel(MeleeDefinition def)
        {
            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }
            if (def == null) return;

            _modelRoot = new GameObject("MeleeModel_" + def.weaponName);
            _modelRoot.transform.SetParent(_weaponHolder, false);
            _modelRoot.transform.localPosition = Vector3.zero;
            _modelRoot.transform.localRotation = Quaternion.identity;

            // Select shape by weapon name substring.
            string n = def.weaponName.ToLowerInvariant();
            if      (n.Contains("club"))                      BuildClub(_modelRoot.transform);
            else if (n.Contains("dagger"))                    BuildDagger(_modelRoot.transform);
            else if (n.Contains("longsword") || n.Contains("sword")) BuildLongsword(_modelRoot.transform);
            else if (n.Contains("spear"))                     BuildSpear(_modelRoot.transform);
            else if (n.Contains("hammer"))                    BuildWarhammer(_modelRoot.transform);
            else                                              BuildLongsword(_modelRoot.transform); // generic blade fallback
        }

        // ---- Weapon shapes -------------------------------------------------------

        /// <summary>
        /// Wooden Club — thick brown cylinder handle + oversized sphere head at top.
        /// </summary>
        private void BuildClub(Transform root)
        {
            // Handle
            MakePart(PrimitiveType.Capsule, root,
                new Vector3(0f, -0.12f, 0f), new Vector3(0.040f, 0.13f, 0.040f),
                new Color(0.62f, 0.40f, 0.18f));

            // Club head (large sphere)
            MakePart(PrimitiveType.Sphere, root,
                new Vector3(0f,  0.10f, 0f), new Vector3(0.110f, 0.110f, 0.110f),
                new Color(0.38f, 0.22f, 0.08f));
        }

        /// <summary>
        /// Iron Dagger — short silver blade + narrow crossguard + leather grip.
        /// </summary>
        private void BuildDagger(Transform root)
        {
            // Blade
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f,  0.11f, 0f), new Vector3(0.012f, 0.18f, 0.005f),
                new Color(0.82f, 0.84f, 0.88f));

            // Crossguard
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f,  0.01f, 0f), new Vector3(0.072f, 0.013f, 0.013f),
                new Color(0.60f, 0.62f, 0.66f));

            // Grip
            MakePart(PrimitiveType.Capsule, root,
                new Vector3(0f, -0.09f, 0f), new Vector3(0.022f, 0.072f, 0.022f),
                new Color(0.28f, 0.18f, 0.08f));
        }

        /// <summary>
        /// Iron Longsword — long silver blade + wide crossguard + wrapped grip + pommel sphere.
        /// </summary>
        private void BuildLongsword(Transform root)
        {
            // Blade
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f,  0.18f, 0f), new Vector3(0.016f, 0.30f, 0.005f),
                new Color(0.78f, 0.80f, 0.84f));

            // Crossguard
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f,  0.02f, 0f), new Vector3(0.110f, 0.013f, 0.016f),
                new Color(0.62f, 0.62f, 0.66f));

            // Grip
            MakePart(PrimitiveType.Capsule, root,
                new Vector3(0f, -0.10f, 0f), new Vector3(0.022f, 0.090f, 0.022f),
                new Color(0.35f, 0.22f, 0.10f));

            // Pommel
            MakePart(PrimitiveType.Sphere, root,
                new Vector3(0f, -0.22f, 0f), new Vector3(0.036f, 0.036f, 0.036f),
                new Color(0.62f, 0.62f, 0.66f));
        }

        /// <summary>
        /// Titanium Spear — long thin cylinder shaft + elongated spearhead cube at top.
        /// </summary>
        private void BuildSpear(Transform root)
        {
            // Shaft (long cylinder along Y)
            MakePart(PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.05f, 0f), new Vector3(0.022f, 0.38f, 0.022f),
                new Color(0.58f, 0.64f, 0.72f));

            // Spearhead — flat elongated cube pointing upward
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, 0.46f, 0f), new Vector3(0.018f, 0.090f, 0.007f),
                new Color(0.88f, 0.92f, 0.96f));
        }

        /// <summary>
        /// Void Warhammer — dark shaft + massive cube head with a glowing void core.
        /// </summary>
        private void BuildWarhammer(Transform root)
        {
            // Handle
            MakePart(PrimitiveType.Cylinder, root,
                new Vector3(0f, -0.08f, 0f), new Vector3(0.030f, 0.24f, 0.030f),
                new Color(0.28f, 0.22f, 0.35f));

            // Hammer head
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f,  0.20f, 0f), new Vector3(0.18f, 0.12f, 0.10f),
                new Color(0.20f, 0.12f, 0.32f));

            // Void glow core — emissive purple sphere on the face
            var glowGO = MakePart(PrimitiveType.Sphere, root,
                new Vector3(0f, 0.20f, -0.05f), new Vector3(0.048f, 0.048f, 0.048f),
                new Color(0.65f, 0.28f, 1.00f));

            var rend = glowGO.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", new Color(0.6f, 0.1f, 1.0f) * 2.5f);
            }
        }

        // -------------------------------------------------------------------------
        // Primitive helper
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates a primitive child, removes its Collider (so it doesn't affect
        /// physics), and tints it with a unique Material instance.
        /// Returns the created GameObject.
        /// </summary>
        private GameObject MakePart(PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            // Remove collider — weapon model parts must not interfere with physics.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Tint via a new material instance (avoids modifying the shared material).
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material = new Material(rend.sharedMaterial) { color = color };

            return go;
        }
    }
}
