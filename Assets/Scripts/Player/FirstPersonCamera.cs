using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Combat;
using Voidborne.Combat.Melee;
using Voidborne.Diagnostics;

namespace Voidborne.Player
{
    /// <summary>
    /// First-person camera controller. Should be placed on a Camera that is a child of the player object.
    /// Handles mouse look with configurable sensitivity and vertical clamping.
    /// Horizontal rotation rotates the player body (parent). Vertical rotation rotates this transform.
    /// Also integrates weapon recoil from RecoilSystem each frame.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float gamepadSensitivity = 2f;

        [Header("Vertical Clamp")]
        [SerializeField] private float verticalClampMin = -90f;
        [SerializeField] private float verticalClampMax = 90f;

        [Header("Smoothing")]
        [SerializeField] private float smoothing = 1f;

        [Header("Recoil")]
        [Tooltip("RecoilSystem that drives camera kick. Assigned by CombatSetup.")]
        [SerializeField] private RecoilSystem recoilSystem;

        [Header("Stagger Shake")]
        [Tooltip("MeleeStagger on the player. Provides screen shake when staggered. Auto-resolved if blank.")]
        [SerializeField] private MeleeStagger meleeStagger;

        /// <summary>
        /// The player body transform (parent). Horizontal rotation is applied here.
        /// </summary>
        public Transform PlayerBody { get; set; }

        private float xRotation; // Vertical (pitch) — excludes recoil (recoil is applied on top as delta)
        private Vector2 lookInput;
        private Vector2 smoothedLookInput;

        // Track the last recoil offset so we can apply only the delta each frame.
        private Vector2 _lastRecoilOffset;

        // Track the last stagger shake offset for the same delta-based integration.
        private Vector2 _lastShakeOffset;

        private InputSystem_Actions inputActions;

        private void Awake()
        {
            if (PlayerBody == null && transform.parent != null)
                PlayerBody = transform.parent;

            if (meleeStagger == null && transform.parent != null)
                meleeStagger = transform.parent.GetComponent<MeleeStagger>();
        }

        private void OnEnable()
        {
            // Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();

            inputActions.Player.Look.performed += OnLook;
            inputActions.Player.Look.canceled += OnLook;
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Look.performed -= OnLook;
                inputActions.Player.Look.canceled -= OnLook;

                inputActions.Player.Disable();
                inputActions.Dispose();
                inputActions = null;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("FPCamera.LateUpdate");

        private void LateUpdate()
        {
            RuntimeProfiler.Begin(s_prof);
            if (PlayerBody == null) { RuntimeProfiler.End(s_prof); return; }

            // Apply smoothing
            smoothedLookInput = Vector2.Lerp(smoothedLookInput, lookInput,
                1f / Mathf.Max(smoothing, 0.01f));

            float mouseX = smoothedLookInput.x * mouseSensitivity;
            float mouseY = smoothedLookInput.y * mouseSensitivity;

            // Vertical rotation (pitch) from mouse
            xRotation -= mouseY;

            // Integrate recoil delta into pitch and yaw.
            // We track the previous offset so only the change is applied — this means
            // recoil recovery (offset shrinking) naturally un-does the kick.
            if (recoilSystem != null)
            {
                Vector2 recoilOffset = recoilSystem.CurrentOffset;
                Vector2 recoilDelta  = recoilOffset - _lastRecoilOffset;
                _lastRecoilOffset    = recoilOffset;

                // Positive recoil.y = kick up = decrease xRotation (looking up)
                xRotation -= recoilDelta.y;

                // Positive recoil.x = kick right = rotate player body right
                PlayerBody.Rotate(Vector3.up * recoilDelta.x);
            }

            // Integrate stagger shake delta (same pattern as recoil).
            if (meleeStagger != null)
            {
                Vector2 shakeOffset = meleeStagger.ShakeOffset;
                Vector2 shakeDelta  = shakeOffset - _lastShakeOffset;
                _lastShakeOffset    = shakeOffset;

                xRotation -= shakeDelta.y;
                PlayerBody.Rotate(Vector3.up * shakeDelta.x);
            }

            xRotation = Mathf.Clamp(xRotation, verticalClampMin, verticalClampMax);

            // Apply vertical rotation to camera
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            // Apply horizontal rotation to player body
            PlayerBody.Rotate(Vector3.up * mouseX);
            RuntimeProfiler.End(s_prof);
        }

        private void OnLook(InputAction.CallbackContext ctx)
        {
            lookInput = ctx.ReadValue<Vector2>();
        }
    }
}
