using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Directional block, deflect, and riposte system.
    ///
    /// How it works:
    ///   1. Player holds right mouse button — enters a blocking stance.
    ///   2. While holding, player moves mouse to select the block direction (same deadzone
    ///      as attacks). Direction updates live. Default while mouse is still = Stab.
    ///   3. If an incoming attack calls TryDeflect() while blocking AND the direction
    ///      matches (same-side: opponent attacks from your right → block right), the
    ///      attack is deflected:
    ///        • Attacker receives a stagger via their MeleeStagger component.
    ///        • A riposte window opens. RiposteDirection is auto-set to the OPPOSITE of
    ///          the blocked attack (e.g. opponent attacked Right → riposte goes Left).
    ///   4. Player releases right mouse button — block stance drops (no attack launched).
    ///
    /// Same-side direction rule:
    ///   Opponent attacks from YOUR right → you block right.
    ///   Opponent attacks from YOUR left  → you block left.
    ///   Overhead                         → block overhead.
    ///   Stab                             → any direction deflects it.
    ///
    /// Attach to the Player alongside MeleeController and MeleeStagger.
    /// </summary>
    public class ParrySystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Timing (seconds)")]
        [Tooltip("How long the riposte window stays open after a successful block.")]
        [SerializeField] private float riposteWindowDuration = 0.5f;

        [Header("Cost")]
        [Tooltip("Stamina consumed when a block stance is entered.")]
        [SerializeField] private float parryStaminaCost = 8f;

        [Header("Attacker Stagger")]
        [Tooltip("Stagger duration applied to the attacker on a successful deflect.")]
        [SerializeField] private float attackerStaggerDuration = 0.5f;

        [Header("Direction Forgiveness")]
        [Tooltip("If true, adjacent directions (e.g. Left/Overhead) also count as valid blocks.")]
        [SerializeField] private bool allowAdjacentDirections = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip parrySuccessClip;
        [SerializeField] private AudioClip parryFailClip;

        [Header("References (auto-resolved if blank)")]
        [SerializeField] private MeleeController meleeController;
        [SerializeField] private MeleeStagger stagger;

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------

        /// <summary>True while the player is holding right mouse button in a block stance.</summary>
        public bool IsParrying { get; private set; }

        /// <summary>True after a successful deflect until the riposte window expires.</summary>
        public bool InRiposteWindow { get; private set; }

        /// <summary>Direction this block is defending (updated live while M2 held).</summary>
        public AttackDirection ParryDirection { get; private set; }

        /// <summary>
        /// Auto-aimed riposte direction: opposite of the last successfully deflected attack.
        /// Read by MeleeController when initiating a riposte attack.
        /// </summary>
        public AttackDirection RiposteDirection { get; private set; }

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when the block stance is entered.</summary>
        public event System.Action OnParryStarted;

        /// <summary>Fired when an incoming attack is successfully deflected.</summary>
        public event System.Action OnParrySuccess;

        /// <summary>Fired when a block fails (wrong direction).</summary>
        public event System.Action OnParryFailed;

        /// <summary>Fired when the riposte window opens.</summary>
        public event System.Action OnRiposteWindowOpen;

        /// <summary>Fired when the riposte window closes without a riposte being landed.</summary>
        public event System.Action OnRiposteWindowClose;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private float   _riposteTimer;
        private Vector2 _blockDelta;
        private FirstPersonController _fpc;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _fpc = GetComponent<FirstPersonController>();
            if (meleeController == null) meleeController = GetComponent<MeleeController>();
            if (stagger         == null) stagger         = GetComponent<MeleeStagger>();
        }

        private void Update()
        {
            HandleInput();
            TickTimers();
        }

        // -------------------------------------------------------------------------
        // Input
        // -------------------------------------------------------------------------

        private void HandleInput()
        {
            if (Mouse.current == null) return;

            // Block parry input when any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            bool staggered    = stagger         != null && stagger.IsStaggered;
            bool meleeActive  = meleeController != null &&
                                (meleeController.Phase == AttackPhase.Windup ||
                                 meleeController.Phase == AttackPhase.Release);

            // M2 pressed → enter block stance (not while staggered or mid-swing).
            if (!IsParrying && !staggered && !meleeActive &&
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                TryBeginBlock();
            }

            // While M2 held → accumulate delta and update block direction live.
            if (IsParrying && Mouse.current.rightButton.isPressed)
            {
                _blockDelta   += Mouse.current.delta.ReadValue();
                ParryDirection = AttackDirectionUtility.FromMouseDelta(_blockDelta);
            }

            // M2 released → drop block stance (no attack launched).
            if (IsParrying && Mouse.current.rightButton.wasReleasedThisFrame)
                EndBlock();
        }

        // -------------------------------------------------------------------------
        // Block stance
        // -------------------------------------------------------------------------

        private void TryBeginBlock()
        {
            if (meleeController != null && meleeController.EquippedWeapon == null)
                return;

            if (_fpc != null && _fpc.CurrentStamina < parryStaminaCost)
            {
                Debug.Log("[ParrySystem] Not enough stamina to block.");
                return;
            }

            _blockDelta    = Vector2.zero;
            ParryDirection = AttackDirection.Stab; // default; updates as mouse moves
            IsParrying     = true;

            if (_fpc != null) _fpc.ConsumeStamina(parryStaminaCost);

            OnParryStarted?.Invoke();
            Debug.Log("[ParrySystem] Block stance entered.");
        }

        private void EndBlock()
        {
            IsParrying = false;
            Debug.Log("[ParrySystem] Block released.");
        }

        // -------------------------------------------------------------------------
        // Deflection (called by attacking entity's hit detection)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Call this when an attack would land on this character.
        /// Returns <c>true</c> if the attack was deflected — damage should NOT be applied.
        /// On success, opens the riposte window and staggers the attacker.
        /// The block stance remains active after a successful deflect (M2 still held).
        /// </summary>
        public bool TryDeflect(AttackDirection incomingDirection, GameObject attacker)
        {
            if (!IsParrying) return false;

            if (!DirectionMatches(ParryDirection, incomingDirection))
            {
                PlayClip(parryFailClip);
                OnParryFailed?.Invoke();
                Debug.Log($"[ParrySystem] Block FAILED — blocking {ParryDirection} vs incoming {incomingDirection}.");
                return false;
            }

            // ----------------------------------------------------------------
            // Successful deflect — keep block stance open, open riposte window.
            // ----------------------------------------------------------------
            RiposteDirection = GetOppositeDirection(incomingDirection);
            InRiposteWindow  = true;
            _riposteTimer    = riposteWindowDuration;

            if (attacker != null)
            {
                MeleeStagger attackerStagger = attacker.GetComponentInParent<MeleeStagger>()
                                           ?? attacker.GetComponent<MeleeStagger>();
                attackerStagger?.ApplyStagger(attackerStaggerDuration);
            }

            PlayClip(parrySuccessClip);
            OnParrySuccess?.Invoke();
            OnRiposteWindowOpen?.Invoke();
            Debug.Log($"[ParrySystem] Block SUCCESS — {incomingDirection} deflected. Riposte: {RiposteDirection}.");
            return true;
        }

        /// <summary>
        /// Forcibly ends the active block stance without a successful deflect.
        /// Called by unblockable attacks (e.g. KickAbility).
        /// </summary>
        public void ForceBreak()
        {
            if (!IsParrying) return;
            IsParrying = false;
            PlayClip(parryFailClip);
            OnParryFailed?.Invoke();
            Debug.Log("[ParrySystem] Block forcibly broken by an unblockable attack.");
        }

        /// <summary>
        /// Called by MeleeController after a riposte attack is initiated,
        /// so the riposte window doesn't stay open for its full duration.
        /// </summary>
        public void ConsumeRiposte()
        {
            if (!InRiposteWindow) return;
            InRiposteWindow = false;
            _riposteTimer   = 0f;
            OnRiposteWindowClose?.Invoke();
        }

        // -------------------------------------------------------------------------
        // Timers
        // -------------------------------------------------------------------------

        private void TickTimers()
        {
            if (InRiposteWindow)
            {
                _riposteTimer -= Time.deltaTime;
                if (_riposteTimer <= 0f)
                {
                    InRiposteWindow = false;
                    OnRiposteWindowClose?.Invoke();
                    Debug.Log("[ParrySystem] Riposte window closed.");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Direction matching
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true if <paramref name="blockDir"/> successfully deflects
        /// an attack coming in at <paramref name="incomingDir"/>.
        ///
        /// Same-side rule: opponent attacks from your right → block right.
        /// Stabs can be blocked from any direction.
        /// Adjacent directions accepted when allowAdjacentDirections is enabled.
        /// </summary>
        private bool DirectionMatches(AttackDirection blockDir, AttackDirection incomingDir)
        {
            if (incomingDir == AttackDirection.Stab) return true;
            if (blockDir    == incomingDir)           return true;
            if (allowAdjacentDirections && AreAdjacent(blockDir, incomingDir)) return true;
            return false;
        }

        /// <summary>
        /// Returns the direction opposite to the given attack — used to auto-aim ripostes.
        /// Left ↔ Right, Overhead ↔ Stab.
        /// </summary>
        private static AttackDirection GetOppositeDirection(AttackDirection dir) => dir switch
        {
            AttackDirection.Left     => AttackDirection.Right,
            AttackDirection.Right    => AttackDirection.Left,
            AttackDirection.Overhead => AttackDirection.Stab,
            AttackDirection.Stab     => AttackDirection.Overhead,
            _                        => dir
        };

        /// <summary>
        /// Two directions are adjacent if they are one step apart on the
        /// Left – Overhead – Right axis. Stab is never adjacent.
        /// </summary>
        private static bool AreAdjacent(AttackDirection a, AttackDirection b)
        {
            return (a == AttackDirection.Left     && b == AttackDirection.Overhead)
                || (a == AttackDirection.Right    && b == AttackDirection.Overhead)
                || (a == AttackDirection.Overhead && (b == AttackDirection.Left || b == AttackDirection.Right));
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
