using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Combat
{
    /// <summary>
    /// Aim-Down-Sights controller.
    /// Smoothly adjusts the player camera's field-of-view when the right mouse button
    /// is held, and reports a spread multiplier so GunController can tighten accuracy.
    /// </summary>
    public class ADSController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------

        [Tooltip("The first-person camera whose FOV will be adjusted.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Camera FOV when not aiming down sights.")]
        [SerializeField] private float normalFOV = 90f;

        [Tooltip("Higher values = faster FOV transition.")]
        [SerializeField] private float adsSmoothSpeed = 10f;

        [Tooltip("Mouse-look sensitivity is multiplied by this value while in ADS.")]
        [SerializeField] private float adsSensitivityMultiplier = 0.7f;

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        /// <summary>True while the right mouse button is held.</summary>
        public bool IsADS { get; private set; }

        /// <summary>Current FOV target (updated each frame).</summary>
        public float ADS_SensitivityMultiplier => IsADS ? adsSensitivityMultiplier : 1f;

        private float _targetFOV;
        private GunDefinition _currentGun;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _targetFOV = normalFOV;
        }

        private void Update()
        {
            // Read right mouse button via the Unity Input System.
            IsADS = Mouse.current != null && Mouse.current.rightButton.isPressed;

            float zoomFOV = normalFOV;
            if (_currentGun != null && _currentGun.adsZoomMultiplier > 0f)
                zoomFOV = normalFOV / _currentGun.adsZoomMultiplier;

            _targetFOV = IsADS ? zoomFOV : normalFOV;

            // Smoothly interpolate the camera FOV toward the target.
            if (playerCamera != null)
                playerCamera.fieldOfView = Mathf.Lerp(
                    playerCamera.fieldOfView,
                    _targetFOV,
                    Time.deltaTime * adsSmoothSpeed);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Notifies the ADSController which gun is currently equipped so it can
        /// read the correct zoom multiplier.
        /// </summary>
        public void SetGun(GunDefinition gun)
        {
            _currentGun = gun;
        }

        /// <summary>
        /// Returns a spread multiplier: 0.5 while ADS (tighter), 1.0 otherwise.
        /// Multiply the base spread value by this before firing.
        /// </summary>
        public float GetSpreadMultiplier()
        {
            return IsADS ? 0.5f : 1f;
        }
    }
}
