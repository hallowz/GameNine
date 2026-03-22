using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Combat;
using Voidborne.Player;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Kick ability — middle mouse button or F key.
    ///
    /// Properties:
    ///   • Short range (1.5 m), fast windup, low damage (15).
    ///   • UNBLOCKABLE — forcibly breaks the target's active parry/block stance.
    ///   • Staggers the target on hit, creating a punish window.
    ///   • Cannot be used while self is staggered.
    ///   • Costs stamina.
    ///
    /// Attach to the Player alongside MeleeController, MeleeStagger, and FirstPersonController.
    /// </summary>
    public class KickAbility : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Kick Stats")]
        [Tooltip("Base damage dealt by the kick.")]
        [SerializeField] private float damage = 15f;

        [Tooltip("Maximum kick reach in metres.")]
        [SerializeField] private float range = 1.5f;

        [Tooltip("SphereCast radius — wider = easier to connect.")]
        [SerializeField] private float kickRadius = 0.35f;

        [Tooltip("Stagger duration applied to the kick target (seconds).")]
        [SerializeField] private float staggerDuration = 0.8f;

        [Tooltip("Stamina consumed per kick.")]
        [SerializeField] private float staminaCost = 12f;

        [Header("Timing (seconds)")]
        [SerializeField] private float windupTime   = 0.10f;
        [SerializeField] private float releaseTime  = 0.12f;
        [SerializeField] private float recoveryTime = 0.40f;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   kickSwingClip;
        [SerializeField] private AudioClip   kickHitClip;

        [Header("References (auto-resolved if blank)")]
        [Tooltip("Camera transform used as raycast origin.")]
        [SerializeField] private Transform cameraTransform;

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------

        public enum KickPhase { Idle, Windup, Release, Recovery }

        /// <summary>Current phase of the kick state machine.</summary>
        public KickPhase Phase { get; private set; } = KickPhase.Idle;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when the kick successfully lands on a target.</summary>
        public event System.Action<GameObject> OnKickLanded;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private float       _phaseTimer;
        private bool        _hitRegistered;

        // Shared non-allocating buffer for the SphereCast.
        private readonly RaycastHit[] _castResults = new RaycastHit[4];

        private MeleeStagger        _selfStagger;
        private FirstPersonController _fpc;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _selfStagger = GetComponent<MeleeStagger>();
            _fpc         = GetComponent<FirstPersonController>();

            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }

        private void Update()
        {
            HandleInput();
            Tick();
        }

        // -------------------------------------------------------------------------
        // Input
        // -------------------------------------------------------------------------

        private void HandleInput()
        {
            if (Phase != KickPhase.Idle) return;

            // Block kick input when any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            // Cannot kick while staggered.
            if (_selfStagger != null && _selfStagger.IsStaggered) return;

            bool pressed =
                (Mouse.current    != null && Mouse.current.middleButton.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);

            if (!pressed) return;

            // Stamina gate.
            if (_fpc != null && _fpc.CurrentStamina < staminaCost)
            {
                Debug.Log("[KickAbility] Not enough stamina to kick.");
                return;
            }

            _fpc?.ConsumeStamina(staminaCost);

            BeginPhase(KickPhase.Windup);
            PlayClip(kickSwingClip);
            Debug.Log("[KickAbility] Kick initiated.");
        }

        // -------------------------------------------------------------------------
        // State machine
        // -------------------------------------------------------------------------

        private void Tick()
        {
            if (Phase == KickPhase.Idle) return;

            _phaseTimer -= Time.deltaTime;

            // Perform hit detection once during the Release phase.
            if (Phase == KickPhase.Release && !_hitRegistered)
                DoHitDetection();

            if (_phaseTimer <= 0f)
                AdvancePhase();
        }

        private void BeginPhase(KickPhase phase)
        {
            Phase       = phase;
            _phaseTimer = phase switch
            {
                KickPhase.Windup   => windupTime,
                KickPhase.Release  => releaseTime,
                KickPhase.Recovery => recoveryTime,
                _                  => 0f
            };

            if (phase == KickPhase.Release)
                _hitRegistered = false;
        }

        private void AdvancePhase()
        {
            switch (Phase)
            {
                case KickPhase.Windup:   BeginPhase(KickPhase.Release);  break;
                case KickPhase.Release:  BeginPhase(KickPhase.Recovery); break;
                case KickPhase.Recovery: Phase = KickPhase.Idle;         break;
            }
        }

        // -------------------------------------------------------------------------
        // Hit detection
        // -------------------------------------------------------------------------

        private void DoHitDetection()
        {
            Transform origin = cameraTransform != null ? cameraTransform : transform;

            int count = Physics.SphereCastNonAlloc(
                origin.position,
                kickRadius,
                origin.forward,
                _castResults,
                range,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _castResults[i];

                // Skip self.
                if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;

                // Kicks are unblockable — forcibly break any active parry.
                ParrySystem targetParry = hit.collider.GetComponentInParent<ParrySystem>();
                targetParry?.ForceBreak();

                // Apply damage.
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(new DamageInfo
                    {
                        Amount    = damage,
                        HitPoint  = hit.point,
                        HitNormal = hit.normal,
                        Type      = DamageType.Melee,
                        Attacker  = gameObject
                    });
                }

                // Stagger the target regardless of whether they were damageable
                // (structural objects, enemies with no IDamageable yet, etc. still get stunned).
                MeleeStagger targetStagger = hit.collider.GetComponentInParent<MeleeStagger>();
                targetStagger?.ApplyStagger(staggerDuration);

                PlayClip(kickHitClip);
                _hitRegistered = true;
                OnKickLanded?.Invoke(hit.collider.gameObject);

                Debug.Log($"[KickAbility] Kick landed on {hit.collider.gameObject.name} " +
                          $"— parry broken, staggered {staggerDuration:F2}s.");

                // One target per kick.
                break;
            }
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
