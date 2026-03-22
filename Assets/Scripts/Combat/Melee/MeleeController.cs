using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Mordhau-style directional melee attack system.
    ///
    /// Input flow:
    ///   1. Player holds left mouse button — Windup phase begins.
    ///   2. While holding, player moves mouse to select attack direction (with deadzone).
    ///      The direction indicator updates live. Default direction while mouse is still = Stab.
    ///   3. Player releases left mouse button → direction is locked and Release begins immediately.
    ///      If the windup timer expires while still held, Release also begins automatically.
    ///   4. During Release a SphereCast arc checks for IDamageable targets.
    ///
    /// Riposte:
    ///   Attacking within the riposte window auto-aims the opposite direction of the
    ///   blocked attack (e.g. opponent came from Right → your riposte goes Left).
    ///   Ripostes also get a 30% faster windup.
    ///
    /// Attach to the Player GameObject alongside FirstPersonController.
    /// </summary>
    public class MeleeController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Weapon")]
        [SerializeField] private MeleeDefinition equippedWeapon;

        [Header("References")]
        [Tooltip("Camera transform used as raycast origin and forward direction.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Point in front of the camera where the weapon arc originates.")]
        [SerializeField] private Transform weaponOrigin;

        [Header("Hit Detection")]
        [Tooltip("Layers that melee swings can hit.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Feedback")]
        [Tooltip("Optional AudioSource for swing/hit sounds.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Swing whoosh clip.")]
        [SerializeField] private AudioClip swingClip;

        [Tooltip("Hit impact clip.")]
        [SerializeField] private AudioClip hitClip;

        // -------------------------------------------------------------------------
        // Public state (read by UI, ParrySystem, etc.)
        // -------------------------------------------------------------------------

        /// <summary>Current phase of the attack state machine.</summary>
        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;

        /// <summary>Direction of the in-progress or last attack.</summary>
        public AttackDirection CurrentDirection { get; private set; }

        /// <summary>0–1 progress within the current phase.</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>How many consecutive hits in the current combo chain.</summary>
        public int ComboCount { get; private set; }

        /// <summary>True while the attack is in the active Release phase.</summary>
        public bool IsInReleaseWindow => Phase == AttackPhase.Release;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when a hit is confirmed. Passes the hit target.</summary>
        public event System.Action<GameObject, AttackDirection> OnHitConfirmed;

        /// <summary>Fired when an attack is initiated (windup starts).</summary>
        public event System.Action<AttackDirection> OnAttackStarted;

        /// <summary>Fired when the attack fully completes (recovery ends).</summary>
        public event System.Action OnAttackEnded;

        /// <summary>Fired when the attack direction is morphed mid-windup by FeintSystem.</summary>
        public event System.Action<AttackDirection> OnDirectionMorphed;

        [Header("Parry & Stagger (auto-resolved if blank)")]
        [Tooltip("ParrySystem on this character. Used to detect riposte window.")]
        [SerializeField] private ParrySystem parrySystem;

        [Tooltip("MeleeStagger on this character. Blocks attacks while staggered.")]
        [SerializeField] private MeleeStagger meleeStagger;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        // True when the current attack was initiated as a riposte (auto-aimed direction).
        private bool _isRiposte;

        private float _phaseTimer;
        private bool  _hitRegisteredThisSwing;

        // Direction selection while holding M1.
        private bool    _m1Held;
        private Vector2 _windupDelta;

        // Targets hit during a single Release phase (to avoid multi-hitting same collider).
        private readonly System.Collections.Generic.HashSet<Collider> _hitThisSwing
            = new System.Collections.Generic.HashSet<Collider>();

        private FirstPersonController _fpc;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _fpc = GetComponent<FirstPersonController>();

            // Auto-resolve camera if not assigned
            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }

            if (parrySystem  == null) parrySystem  = GetComponent<ParrySystem>();
            if (meleeStagger == null) meleeStagger = GetComponent<MeleeStagger>();
        }

        private void Update()
        {
            HandleInput();
            TickStateMachine();
        }

        // -------------------------------------------------------------------------
        // Input
        // -------------------------------------------------------------------------

        private void HandleInput()
        {
            if (Mouse.current == null) return;

            // No melee weapon equipped — do nothing.
            if (equippedWeapon == null) return;

            // Block melee input when any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            // Cannot attack while staggered.
            if (meleeStagger != null && meleeStagger.IsStaggered) return;

            // Cannot start a new attack while actively blocking.
            if (parrySystem != null && parrySystem.IsParrying) return;

            bool canAttack = Phase == AttackPhase.Idle ||
                             (Phase == AttackPhase.Recovery && _phaseTimer > 0f);

            // M1 pressed → begin windup; reset windup delta.
            if (canAttack && Mouse.current.leftButton.wasPressedThisFrame)
            {
                _windupDelta = Vector2.zero;
                _m1Held      = true;
                TryBeginWindup();
            }

            // While M1 held during Windup → accumulate delta and update direction live.
            // (Riposte attacks have their direction auto-aimed — don't override.)
            if (_m1Held && Phase == AttackPhase.Windup && !_isRiposte)
            {
                _windupDelta     += Mouse.current.delta.ReadValue();
                CurrentDirection  = AttackDirectionUtility.FromMouseDelta(_windupDelta);
            }

            // M1 released during Windup → commit direction and launch the swing.
            if (_m1Held && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _m1Held = false;
                if (Phase == AttackPhase.Windup)
                    BeginPhase(AttackPhase.Release);
            }
        }

        // -------------------------------------------------------------------------
        // Attack initiation
        // -------------------------------------------------------------------------

        private void TryBeginWindup()
        {
            if (equippedWeapon == null)
            {
                Debug.LogWarning("[MeleeController] No weapon equipped.");
                _m1Held = false;
                return;
            }

            if (_fpc != null && _fpc.CurrentStamina < equippedWeapon.staminaCost)
            {
                Debug.Log("[MeleeController] Not enough stamina to attack.");
                _m1Held = false;
                return;
            }

            // Combo bookkeeping
            if (Phase == AttackPhase.Recovery)
                ComboCount = Mathf.Min(ComboCount + 1, equippedWeapon.maxComboChain);
            else
                ComboCount = 1;

            // Riposte: auto-aim the opposite direction of the blocked attack.
            _isRiposte = parrySystem != null && parrySystem.InRiposteWindow;
            if (_isRiposte)
            {
                CurrentDirection = parrySystem.RiposteDirection;
                parrySystem.ConsumeRiposte();
                Debug.Log($"[MeleeController] Riposte — auto-direction: {CurrentDirection}.");
            }
            else
            {
                // Default to Stab; updated live while player holds and moves mouse.
                CurrentDirection = AttackDirection.Stab;
            }

            ConsumeStamina(equippedWeapon.staminaCost);
            BeginPhase(AttackPhase.Windup);
            OnAttackStarted?.Invoke(CurrentDirection);

            if (audioSource != null && swingClip != null)
                audioSource.PlayOneShot(swingClip);

            Debug.Log($"[MeleeController] Windup started (combo #{ComboCount})");
        }

        // -------------------------------------------------------------------------
        // State machine
        // -------------------------------------------------------------------------

        private void TickStateMachine()
        {
            // Windup has no timer — the player is exposed and holds until they commit by releasing M1.
            if (Phase == AttackPhase.Idle || Phase == AttackPhase.Windup) return;

            _phaseTimer -= Time.deltaTime;

            float phaseDuration = GetPhaseDuration(Phase);
            PhaseProgress = phaseDuration > 0f
                ? 1f - Mathf.Clamp01(_phaseTimer / phaseDuration)
                : 1f;

            // Perform hit detection during the Release phase, once per position sample
            if (Phase == AttackPhase.Release)
                PerformHitDetection();

            if (_phaseTimer <= 0f)
                AdvancePhase();
        }

        private void BeginPhase(AttackPhase phase)
        {
            Phase       = phase;
            _phaseTimer = GetPhaseDuration(phase);
            PhaseProgress = 0f;

            if (phase == AttackPhase.Release)
            {
                _hitRegisteredThisSwing = false;
                _hitThisSwing.Clear();
            }
        }

        private void AdvancePhase()
        {
            switch (Phase)
            {
                case AttackPhase.Release:
                    float recoveryDuration = equippedWeapon.ScaledRecoveryTime;
                    if (ComboCount >= equippedWeapon.maxComboChain)
                        recoveryDuration += equippedWeapon.comboExhaustionPenalty;
                    Phase       = AttackPhase.Recovery;
                    _phaseTimer = recoveryDuration;
                    PhaseProgress = 0f;
                    break;

                case AttackPhase.Recovery:
                    Phase     = AttackPhase.Idle;
                    PhaseProgress = 0f;
                    ComboCount = 0;
                    _isRiposte = false;
                    OnAttackEnded?.Invoke();
                    break;
            }
        }

        private float GetPhaseDuration(AttackPhase phase)
        {
            if (equippedWeapon == null) return 0.2f;

            return phase switch
            {
                AttackPhase.Windup   => 0f, // no timer — held until M1 released
                AttackPhase.Release  => equippedWeapon.ScaledReleaseTime,
                AttackPhase.Recovery => equippedWeapon.ScaledRecoveryTime,
                _                    => 0f
            };
        }

        // -------------------------------------------------------------------------
        // Hit detection
        // -------------------------------------------------------------------------

        private void PerformHitDetection()
        {
            if (equippedWeapon == null || _hitRegisteredThisSwing) return;

            Transform origin = weaponOrigin != null ? weaponOrigin : cameraTransform;
            if (origin == null) return;

            Vector3 start     = origin.position;
            Vector3 direction = GetSwingDirection(origin);

            int hitCount = Physics.SphereCastNonAlloc(
                start,
                equippedWeapon.hitRadius,
                direction,
                _sphereCastResults,
                equippedWeapon.range,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _sphereCastResults[i];

                if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;
                if (!_hitThisSwing.Add(hit.collider)) continue;

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    var info = new DamageInfo
                    {
                        Amount    = equippedWeapon.damage,
                        HitPoint  = hit.point,
                        HitNormal = hit.normal,
                        Type      = equippedWeapon.damageType,
                        Attacker  = gameObject
                    };
                    damageable.TakeDamage(info);

                    if (audioSource != null && hitClip != null)
                        audioSource.PlayOneShot(hitClip);

                    if (equippedWeapon.hitStaggerDuration > 0f)
                    {
                        var targetStagger = hit.collider.GetComponentInParent<MeleeStagger>();
                        targetStagger?.ApplyStagger(equippedWeapon.hitStaggerDuration);
                    }

                    OnHitConfirmed?.Invoke(hit.collider.gameObject, CurrentDirection);
                    _hitRegisteredThisSwing = true;

                    Debug.Log($"[MeleeController] Hit {hit.collider.gameObject.name} for {equippedWeapon.damage} dmg ({CurrentDirection})");
                }
            }
        }

        private readonly RaycastHit[] _sphereCastResults = new RaycastHit[8];

        private Vector3 GetSwingDirection(Transform origin)
        {
            Vector3 forward = origin.forward;
            Vector3 right   = origin.right;

            return CurrentDirection switch
            {
                AttackDirection.Right    => (forward + right  * -0.5f).normalized, // starts right, sweeps left
                AttackDirection.Left     => (forward + right  *  0.5f).normalized, // starts left, sweeps right
                AttackDirection.Overhead => (forward + Vector3.down * 0.3f).normalized,
                AttackDirection.Stab     => forward,
                _                        => forward
            };
        }

        // -------------------------------------------------------------------------
        // Stamina integration
        // -------------------------------------------------------------------------

        private void ConsumeStamina(float amount)
        {
            if (_fpc != null)
                _fpc.ConsumeStamina(amount);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Equip a new melee weapon at runtime.</summary>
        public void Equip(MeleeDefinition weapon)
        {
            equippedWeapon = weapon;
            Debug.Log($"[MeleeController] Equipped: {weapon.weaponName}");
        }

        /// <summary>Unequip the current melee weapon (e.g. when a gun is selected).</summary>
        public void Unequip()
        {
            equippedWeapon = null;
            ForceIdle();
            Debug.Log("[MeleeController] Weapon unequipped.");
        }

        /// <summary>Force the controller into Idle (used by stagger and feint).</summary>
        public void ForceIdle()
        {
            Phase         = AttackPhase.Idle;
            _phaseTimer   = 0f;
            PhaseProgress = 0f;
            _m1Held       = false;
        }

        /// <summary>
        /// Redirects the current attack to a new direction mid-windup.
        /// Only effective during the Windup phase. Called by FeintSystem for morphs.
        /// Seeds _windupDelta to the morphed direction so it "sticks" without mouse movement.
        /// </summary>
        public void MorphDirection(AttackDirection newDir)
        {
            if (Phase != AttackPhase.Windup) return;

            CurrentDirection = newDir;
            _windupDelta     = AttackDirectionUtility.ToSeedDelta(newDir);

            OnDirectionMorphed?.Invoke(newDir);
            Debug.Log($"[MeleeController] Direction morphed to {newDir}.");
        }

        /// <summary>Read-only reference to the equipped definition (may be null).</summary>
        public MeleeDefinition EquippedWeapon => equippedWeapon;
    }

    /// <summary>Phases of a melee attack.</summary>
    public enum AttackPhase
    {
        Idle,
        Windup,
        Release,
        Recovery
    }
}
