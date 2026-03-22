using System.Collections;
using UnityEngine;
using Voidborne.Diagnostics;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Procedural animator for enemies. No Animator component needed.
    /// Drives walk bob + forward tilt, attack lunge, and hit-pulse on a child body transform.
    ///
    /// Usage:
    ///   • Add to the same GameObject as EnemyEntity.
    ///   • Assign _bodyRoot to the child mesh/model transform; if left empty the first
    ///     MeshRenderer child is used automatically.
    ///   • EnemyEntity calls SetMoving() each frame and PlayAttack()/PlayHit() on events.
    /// </summary>
    [AddComponentMenu("Voidborne/Enemies/Enemy Animator")]
    public class EnemyAnimator : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Tooltip("Child transform containing the visible mesh. Auto-detected from first MeshRenderer child if empty.")]
        [SerializeField] private Transform _bodyRoot;

        [Header("Walk Bob")]
        [SerializeField] private float _bobFrequency = 2.2f;   // cycles per second
        [SerializeField] private float _bobAmplitude  = 0.05f;  // metres
        [SerializeField] private float _tiltDegrees   = 7f;     // forward lean at full speed

        [Header("Attack Lunge")]
        [SerializeField] private float _lungeDistance = 0.32f;  // metres forward
        [SerializeField] private float _lungePitch    = 28f;    // forward-pitch degrees

        [Header("Smoothing")]
        [SerializeField] private float _smoothSpeed = 10f;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private Vector3    _baseLocalPos;
        private Quaternion _baseLocalRot;
        private Vector3    _baseLocalScale;

        private float      _bobPhase;
        private Vector3    _posOffset;
        private Quaternion _rotOffset = Quaternion.identity;

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("EnemyAnimator.Update");

        private bool  _isMoving;
        private float _speedFraction;   // 0-1
        private bool  _lunging;
        private bool  _culled;          // true when tick rate ≥ 4 (distant enemy)

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (_bodyRoot == null)
            {
                MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
                if (mr != null) _bodyRoot = mr.transform;
            }

            if (_bodyRoot != null)
            {
                _baseLocalPos   = _bodyRoot.localPosition;
                _baseLocalRot   = _bodyRoot.localRotation;
                _baseLocalScale = _bodyRoot.localScale;
            }
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            if (_bodyRoot == null || _lunging || _culled) { RuntimeProfiler.End(s_prof); return; }

            if (_isMoving)
            {
                // Advance bob phase
                _bobPhase += Time.deltaTime * _bobFrequency * Mathf.PI * 2f;

                float bob = Mathf.Sin(_bobPhase) * _bobAmplitude * _speedFraction;
                Vector3    wantPos = new Vector3(0f, bob, 0f);
                Quaternion wantRot = Quaternion.Euler(-_tiltDegrees * _speedFraction, 0f, 0f);

                _posOffset = Vector3.Lerp(_posOffset, wantPos, Time.deltaTime * _smoothSpeed);
                _rotOffset = Quaternion.Slerp(_rotOffset, wantRot, Time.deltaTime * _smoothSpeed);
            }
            else
            {
                // Return to rest
                _posOffset = Vector3.Lerp(_posOffset, Vector3.zero, Time.deltaTime * _smoothSpeed);
                _rotOffset = Quaternion.Slerp(_rotOffset, Quaternion.identity, Time.deltaTime * _smoothSpeed);
                _bobPhase  = Mathf.Lerp(_bobPhase, 0f, Time.deltaTime * _smoothSpeed);
            }

            _bodyRoot.localPosition = _baseLocalPos + _posOffset;
            _bodyRoot.localRotation = _baseLocalRot * _rotOffset;
            RuntimeProfiler.End(s_prof);
        }

        // -----------------------------------------------------------------------
        // Public API — called by EnemyEntity
        // -----------------------------------------------------------------------

        /// <summary>
        /// Call each frame from EnemyEntity to drive walk animation.
        /// speedFraction: 0 = stopped, 1 = full speed.
        /// tickRate: EnemyEntity tick rate (1/2/4). At 4 (>60m) animation is culled.
        /// </summary>
        public void SetMoving(bool moving, int tickRate = 1, float speedFraction = 1f)
        {
            _isMoving      = moving;
            _speedFraction = Mathf.Clamp01(speedFraction);
            _culled        = tickRate >= 4;

            // Snap to rest when culled so there's no frozen mid-bob pose
            if (_culled && _bodyRoot != null)
            {
                _posOffset = Vector3.zero;
                _rotOffset = Quaternion.identity;
                _bodyRoot.localPosition = _baseLocalPos;
                _bodyRoot.localRotation = _baseLocalRot;
            }
        }

        /// <summary>
        /// Trigger a forward-lunge attack animation that lines up with the attack windup.
        /// </summary>
        public void PlayAttack(float windupDuration)
        {
            if (_bodyRoot == null) return;
            StopAllCoroutines();
            StartCoroutine(AttackRoutine(windupDuration));
        }

        /// <summary>
        /// Trigger a quick scale-pulse hit reaction.
        /// </summary>
        public void PlayHit()
        {
            if (_bodyRoot == null) return;
            StartCoroutine(HitRoutine());
        }

        // -----------------------------------------------------------------------
        // Coroutines
        // -----------------------------------------------------------------------

        private IEnumerator AttackRoutine(float windupDuration)
        {
            _lunging = true;

            // ── Phase 1: lunge forward over the first half of the windup ──
            float rampDur = windupDuration * 0.55f;
            float t = 0f;
            while (t < rampDur)
            {
                t += Time.deltaTime;
                float frac = Mathf.SmoothStep(0f, 1f, t / rampDur);
                ApplyLunge(frac);
                yield return null;
            }

            ApplyLunge(1f);

            // ── Phase 2: brief hold at full lunge (impact moment) ──
            yield return new WaitForSeconds(0.06f);

            // ── Phase 3: retract ──
            float retractDur = windupDuration * 0.7f;
            t = 0f;
            while (t < retractDur)
            {
                t += Time.deltaTime;
                float frac = Mathf.SmoothStep(0f, 1f, t / retractDur);
                ApplyLunge(1f - frac);
                yield return null;
            }

            _posOffset = Vector3.zero;
            _rotOffset = Quaternion.identity;
            _bodyRoot.localPosition = _baseLocalPos;
            _bodyRoot.localRotation = _baseLocalRot;
            _lunging = false;
        }

        private void ApplyLunge(float frac)
        {
            _posOffset = new Vector3(0f, 0f, _lungeDistance * frac);
            _rotOffset = Quaternion.Euler(-_lungePitch * frac, 0f, 0f);
            _bodyRoot.localPosition = _baseLocalPos + _posOffset;
            _bodyRoot.localRotation = _baseLocalRot * _rotOffset;
        }

        private IEnumerator HitRoutine()
        {
            // Brief scale pulse: grow slightly then snap back
            float dur = 0.14f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float pulse = 1f + Mathf.Sin((t / dur) * Mathf.PI) * 0.18f;
                _bodyRoot.localScale = _baseLocalScale * pulse;
                yield return null;
            }
            _bodyRoot.localScale = _baseLocalScale;
        }
    }
}
