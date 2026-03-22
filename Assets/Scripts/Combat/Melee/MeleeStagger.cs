using UnityEngine;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// Manages the stagger state for a character.
    /// While staggered, the character cannot attack or parry.
    ///
    /// Also produces a decaying ShakeOffset (pitch/yaw degrees) each frame.
    /// FirstPersonCamera reads ShakeOffset and integrates it into the view —
    /// the same delta-based pattern used by RecoilSystem.
    ///
    /// Attach to the Player (or Enemy) alongside MeleeController.
    /// </summary>
    public class MeleeStagger : MonoBehaviour
    {
        [Header("Stagger")]
        [Tooltip("Default duration when ApplyStagger() is called without an explicit value.")]
        [SerializeField] private float defaultStaggerDuration = 0.5f;

        [Header("Screen Shake")]
        [Tooltip("Peak shake amplitude in degrees (applied to camera pitch / yaw).")]
        [SerializeField] private float shakeIntensity = 1.2f;

        [Tooltip("Oscillation speed of the shake in Hz.")]
        [SerializeField] private float shakeFrequency = 18f;

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------

        /// <summary>True while the character is staggered and cannot attack or parry.</summary>
        public bool IsStaggered { get; private set; }

        /// <summary>0–1 progress through the stagger (0 = just started, 1 = finished).</summary>
        public float StaggerProgress { get; private set; }

        /// <summary>
        /// Camera shake offset in degrees (x = yaw, y = pitch).
        /// Read by FirstPersonCamera each LateUpdate and applied as a running delta,
        /// so that it automatically un-does itself as the shake decays toward zero.
        /// </summary>
        public Vector2 ShakeOffset { get; private set; }

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        public event System.Action OnStaggerStart;
        public event System.Action OnStaggerEnd;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private float _staggerDuration;
        private float _staggerTimer;
        private float _shakeTime;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Applies stagger for <paramref name="duration"/> seconds.
        /// If <paramref name="duration"/> is negative the default is used.
        /// Re-calling while already staggered restarts the timer (does not stack).
        /// </summary>
        public void ApplyStagger(float duration = -1f)
        {
            float d = duration > 0f ? duration : defaultStaggerDuration;
            bool wasStaggered = IsStaggered;

            _staggerDuration = d;
            _staggerTimer    = d;
            _shakeTime       = 0f;
            StaggerProgress  = 0f;
            IsStaggered      = true;

            if (!wasStaggered)
                OnStaggerStart?.Invoke();
        }

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Update()
        {
            if (!IsStaggered) return;

            _staggerTimer -= Time.deltaTime;
            _shakeTime    += Time.deltaTime;

            StaggerProgress = 1f - Mathf.Clamp01(_staggerTimer / _staggerDuration);

            // Shake amplitude decays linearly as the stagger nears its end.
            float shakeMag = shakeIntensity * Mathf.Clamp01(_staggerTimer / _staggerDuration);
            float t        = _shakeTime * shakeFrequency * Mathf.PI * 2f;
            ShakeOffset    = new Vector2(Mathf.Sin(t * 1.3f), Mathf.Cos(t)) * shakeMag;

            if (_staggerTimer <= 0f)
            {
                IsStaggered     = false;
                StaggerProgress = 1f;
                ShakeOffset     = Vector2.zero;
                OnStaggerEnd?.Invoke();
            }
        }
    }
}
