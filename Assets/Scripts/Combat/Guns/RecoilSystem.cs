using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Tracks accumulated camera recoil and recovers it smoothly over time.
    /// Does NOT directly rotate anything — FirstPersonCamera reads CurrentOffset
    /// and integrates it into its own rotation each frame.
    /// </summary>
    public class RecoilSystem : MonoBehaviour
    {
        [Tooltip("Degrees per second at which accumulated recoil recovers back toward zero.")]
        [SerializeField] private float recoveryRate = 5f;

        // Accumulated recoil not yet recovered.
        // x = yaw (degrees right), y = pitch (degrees up).
        private Vector2 _recoilOffset;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Current accumulated recoil offset in degrees.
        /// x = yaw right, y = pitch up.
        /// FirstPersonCamera subtracts this from xRotation each frame.
        /// </summary>
        public Vector2 CurrentOffset => _recoilOffset;

        /// <summary>
        /// Adds a recoil impulse. x = yaw right (deg), y = pitch up (deg).
        /// </summary>
        public void ApplyRecoil(Vector2 recoilStep)
        {
            _recoilOffset += recoilStep;
        }

        /// <summary>
        /// Zeroes accumulated recoil instantly WITHOUT moving the camera.
        /// Call on weapon switch.
        /// </summary>
        public void ResetInstant()
        {
            _recoilOffset = Vector2.zero;
        }

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Update()
        {
            if (_recoilOffset == Vector2.zero)
                return;

            float maxRecovery = recoveryRate * Time.deltaTime;
            _recoilOffset = Vector2.MoveTowards(_recoilOffset, Vector2.zero, maxRecovery);
        }
    }
}
