using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Feints, Morphs, and Chambers — advanced melee deception mechanics.
    ///
    /// Feint  — During Windup, right-click cancels the attack (costs 10 stamina).
    ///           Fires OnFeintExecuted. Opponent saw the windup commit but it never lands.
    ///
    /// Morph  — During Windup, significant mouse movement in a new direction causes the
    ///           attack to silently redirect. Costs 5 stamina. Fires OnDirectionMorphed.
    ///
    /// Chamber — Attacking whose direction mirrors an incoming attack (same mirror rule as
    ///           parry) during the opponent's Release phase deflects that attack and lets
    ///           the player's strike continue. Staggered attacker, no extra stamina cost.
    ///           Window: first chamberWindowFraction of player Windup + entire Release phase.
    ///
    /// Attach to the Player alongside MeleeController, MeleeStagger, and FirstPersonController.
    /// </summary>
    public class FeintSystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Stamina Costs")]
        [SerializeField] private float feintStaminaCost = 10f;
        [SerializeField] private float morphStaminaCost = 5f;

        [Header("Morph")]
        [Tooltip("Minimum mouse travel (pixels) to trigger a direction morph.")]
        [SerializeField] private float morphThresholdPixels = 25f;

        [Header("Chamber")]
        [Tooltip("Fraction of the windup phase during which a chamber can be initiated (0–1).")]
        [SerializeField] private float chamberWindowFraction = 0.4f;

        [Tooltip("Stagger duration applied to the attacker on a successful chamber.")]
        [SerializeField] private float attackerStaggerDuration = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip feintClip;
        [SerializeField] private AudioClip chamberClip;

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------

        /// <summary>
        /// True when the player can accept a chamber challenge:
        /// during the early Windup window, or during the entire Release phase.
        /// </summary>
        public bool IsChamberWindowOpen { get; private set; }

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when a feint is successfully executed.</summary>
        public event System.Action OnFeintExecuted;

        /// <summary>Fired when an attack direction is morphed mid-windup.</summary>
        public event System.Action<AttackDirection> OnDirectionMorphed;

        /// <summary>Fired when an incoming attack is successfully chambered.</summary>
        public event System.Action OnChamberSuccess;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private MeleeController _meleeController;
        private MeleeStagger    _meleeStagger;
        private FirstPersonController _fpc;

        // Accumulated mouse delta tracked independently for morph detection.
        private Vector2 _morphDelta;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _fpc             = GetComponent<FirstPersonController>();
            _meleeController = GetComponent<MeleeController>();
            _meleeStagger    = GetComponent<MeleeStagger>();
        }

        private void Update()
        {
            UpdateChamberWindow();
            HandleFeintInput();
            HandleMorph();
        }

        // -------------------------------------------------------------------------
        // Chamber window
        // -------------------------------------------------------------------------

        private void UpdateChamberWindow()
        {
            if (_meleeController == null) { IsChamberWindowOpen = false; return; }

            AttackPhase phase    = _meleeController.Phase;
            float       progress = _meleeController.PhaseProgress;

            IsChamberWindowOpen =
                (phase == AttackPhase.Windup  && progress <= chamberWindowFraction) ||
                (phase == AttackPhase.Release);
        }

        // -------------------------------------------------------------------------
        // Feint
        // -------------------------------------------------------------------------

        private void HandleFeintInput()
        {
            if (Mouse.current == null) return;
            if (_meleeController == null) return;
            if (_meleeController.Phase != AttackPhase.Windup) return;
            if (!Mouse.current.rightButton.wasPressedThisFrame) return;

            // Stamina check.
            if (_fpc != null && _fpc.CurrentStamina < feintStaminaCost)
            {
                Debug.Log("[FeintSystem] Not enough stamina to feint.");
                return;
            }

            if (_fpc != null) _fpc.ConsumeStamina(feintStaminaCost);

            _meleeController.ForceIdle();
            _morphDelta = Vector2.zero;

            PlayClip(feintClip);
            OnFeintExecuted?.Invoke();
            Debug.Log("[FeintSystem] Feint executed.");
        }

        // -------------------------------------------------------------------------
        // Morph
        // -------------------------------------------------------------------------

        private void HandleMorph()
        {
            if (Mouse.current == null) return;
            if (_meleeController == null) return;

            // Reset stale delta whenever we are not in Windup so it does not
            // carry over and cause a spurious morph at the start of the next attack.
            if (_meleeController.Phase != AttackPhase.Windup)
            {
                _morphDelta = Vector2.zero;
                return;
            }

            // Accumulate mouse movement during the windup.
            Vector2 delta = Mouse.current.delta.ReadValue();
            if (delta.sqrMagnitude > 0.01f)
                _morphDelta += delta;

            float threshold = morphThresholdPixels * morphThresholdPixels;
            if (_morphDelta.sqrMagnitude < threshold) return;

            // Enough movement — check if the direction differs from the current one.
            AttackDirection newDir = AttackDirectionUtility.FromMouseDelta(_morphDelta);
            if (newDir == _meleeController.CurrentDirection)
            {
                // Same direction: keep accumulating, don't reset yet.
                return;
            }

            // Stamina check.
            if (_fpc != null && _fpc.CurrentStamina < morphStaminaCost)
            {
                Debug.Log("[FeintSystem] Not enough stamina to morph.");
                _morphDelta = Vector2.zero;
                return;
            }

            if (_fpc != null) _fpc.ConsumeStamina(morphStaminaCost);

            _meleeController.MorphDirection(newDir);
            _morphDelta = Vector2.zero;

            OnDirectionMorphed?.Invoke(newDir);
            Debug.Log($"[FeintSystem] Direction morphed to {newDir}.");
        }

        // -------------------------------------------------------------------------
        // Chamber (called by attacker's hit detection, same pattern as ParrySystem.TryDeflect)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Call this when an attack would land on this character.
        /// Returns <c>true</c> if the attack was chambered — damage should NOT be applied.
        /// Returns <c>false</c> if the chamber window is not open or direction does not mirror.
        /// On success, the <paramref name="attacker"/> receives a stagger.
        /// </summary>
        public bool TryChamber(AttackDirection incomingDir, GameObject attacker)
        {
            if (!IsChamberWindowOpen)
                return false;

            if (_meleeController == null)
                return false;

            if (!ChamberDirectionMatches(_meleeController.CurrentDirection, incomingDir))
                return false;

            // Stagger the attacker.
            if (attacker != null)
            {
                MeleeStagger attackerStagger = attacker.GetComponentInParent<MeleeStagger>()
                                           ?? attacker.GetComponent<MeleeStagger>();
                attackerStagger?.ApplyStagger(attackerStaggerDuration);
            }

            PlayClip(chamberClip);
            OnChamberSuccess?.Invoke();
            Debug.Log($"[FeintSystem] Chamber SUCCESS — {incomingDir} deflected by {_meleeController.CurrentDirection}.");
            return true;
        }

        // -------------------------------------------------------------------------
        // Direction matching (mirrors ParrySystem logic, inlined per spec)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true if the player's attacking direction chambers the incoming attack.
        /// Rule: same mirror as parry — Right incoming → Left chamber required, etc.
        /// Stabs are always chamberable.
        /// </summary>
        private static bool ChamberDirectionMatches(AttackDirection playerDir, AttackDirection incomingDir)
        {
            if (incomingDir == AttackDirection.Stab) return true;

            AttackDirection required = GetRequiredChamberDirection(incomingDir);
            return playerDir == required;
        }

        /// <summary>
        /// Returns the player attack direction that chambers a given incoming direction.
        /// Mirrors the attack: Right incoming → Left chamber.
        /// </summary>
        private static AttackDirection GetRequiredChamberDirection(AttackDirection incomingDir)
        {
            return incomingDir switch
            {
                AttackDirection.Left     => AttackDirection.Right,
                AttackDirection.Right    => AttackDirection.Left,
                AttackDirection.Overhead => AttackDirection.Overhead,
                AttackDirection.Stab     => AttackDirection.Stab,
                _                        => incomingDir
            };
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
