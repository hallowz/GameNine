using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Diagnostics;
using Voidborne.World.Chunks;

namespace Voidborne.Player
{
    /// <summary>
    /// First-person character controller using Unity's CharacterController.
    /// Handles WASD movement, sprinting, crouching, jumping, and gravity.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;

        [Header("Movement Feel")]
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float terminalVelocity = 80f;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private float groundCheckOffset = 0.05f;
        [SerializeField] private LayerMask groundLayers = ~0; // Everything by default

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaDrainRate = 15f;
        [SerializeField] private float staminaRegenRate = 10f;
        [SerializeField] private float staminaRegenDelay = 1f;

        // Public read-only state
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public float CurrentStamina { get; private set; }
        public float MaxStamina => maxStamina;
        public Vector3 Velocity => velocity;

        /// <summary>Exposed for Cortex Interval ability — read-only access to base walk speed.</summary>
        public float WalkSpeed => walkSpeed;

        /// <summary>
        /// External speed multiplier applied by CortexAbilities during The Interval.
        /// 1f = normal; set to 1/timeScale to compensate for slow-world.
        /// </summary>
        public float ExternalSpeedMult { get; set; } = 1f;

        private CharacterController controller;
        private Vector3 velocity;
        private Vector2 moveInput;
        private bool sprintInput;
        private bool crouchInput;
        private bool jumpInput;

        private float targetHeight;
        private float staminaRegenTimer;

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("FPController.Update");

        // Input actions
        private InputSystem_Actions inputActions;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            targetHeight = standingHeight;

            CurrentStamina = maxStamina;
        }

        private void OnEnable()
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();

            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;
            inputActions.Player.Jump.performed += OnJump;
            inputActions.Player.Sprint.performed += OnSprint;
            inputActions.Player.Sprint.canceled += OnSprint;
            inputActions.Player.Crouch.performed += OnCrouch;
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Move.performed -= OnMove;
                inputActions.Player.Move.canceled -= OnMove;
                inputActions.Player.Jump.performed -= OnJump;
                inputActions.Player.Sprint.performed -= OnSprint;
                inputActions.Player.Sprint.canceled -= OnSprint;
                inputActions.Player.Crouch.performed -= OnCrouch;

                inputActions.Player.Disable();
                inputActions.Dispose();
                inputActions = null;
            }
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            // Freeze the player if their chunk isn't loaded yet — prevents falling through the world.
            if (ChunkManager.Instance != null)
            {
                Vector3Int playerChunk = ChunkCoordUtility.WorldToChunkPos(transform.position);
                ChunkData chunk = ChunkManager.Instance.GetChunk(playerChunk);
                if (chunk == null || chunk.state != ChunkState.Active)
                {
                    if (Time.frameCount % 60 == 0)
                        Debug.Log($"[FPController] FROZEN: pos={transform.position} chunk={playerChunk} " +
                                  $"data={(chunk == null ? "NULL" : chunk.state.ToString())} " +
                                  $"activeChunks={ChunkManager.Instance.ActiveChunkObjectCount}");
                    velocity = Vector3.zero;
                    RuntimeProfiler.End(s_prof);
                    return;
                }
            }

            UpdateGroundCheck();
            UpdateMovement();
            UpdateGravityAndJump();
            UpdateCrouch();
            UpdateStamina();

            controller.Move(velocity * Time.deltaTime);
            RuntimeProfiler.End(s_prof);
        }

        private void UpdateGroundCheck()
        {
            IsGrounded = controller.isGrounded;
        }

        private void UpdateMovement()
        {
            // Determine target speed
            float targetSpeed;
            if (IsCrouching)
            {
                targetSpeed = crouchSpeed;
            }
            else if (sprintInput && CurrentStamina > 0f && moveInput.sqrMagnitude > 0.01f)
            {
                targetSpeed = sprintSpeed;
                IsSprinting = true;
            }
            else
            {
                targetSpeed = walkSpeed;
                IsSprinting = false;
            }

            if (IsCrouching) IsSprinting = false;

            // Build desired horizontal velocity relative to the player's facing direction
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 desiredVelocity = (forward * moveInput.y + right * moveInput.x) * (targetSpeed * ExternalSpeedMult);

            // Smooth acceleration / deceleration (horizontal only)
            float smoothRate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
            float vx = Mathf.MoveTowards(velocity.x, desiredVelocity.x, smoothRate * Time.deltaTime);
            float vz = Mathf.MoveTowards(velocity.z, desiredVelocity.z, smoothRate * Time.deltaTime);

            velocity.x = vx;
            velocity.z = vz;
        }

        private void UpdateGravityAndJump()
        {
            if (IsGrounded && velocity.y < 0f)
            {
                // Small downward velocity to keep grounded
                velocity.y = -2f;
            }

            if (jumpInput && IsGrounded && !IsCrouching)
            {
                // v = sqrt(2 * |gravity| * jumpHeight) simplified:
                // We use jumpForce directly as an impulse velocity
                velocity.y = jumpForce;
            }

            jumpInput = false;

            velocity.y += gravity * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -terminalVelocity);
        }

        private void UpdateCrouch()
        {
            targetHeight = IsCrouching ? crouchHeight : standingHeight;

            float currentHeight = controller.height;
            if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
            {
                float newHeight = Mathf.MoveTowards(currentHeight, targetHeight,
                    crouchTransitionSpeed * Time.deltaTime);

                controller.height = newHeight;
                // Keep the local top of the capsule fixed at standingHeight so the
                // camera (child transform, unchanged localPosition) moves in world
                // space as the player sinks/rises.
                controller.center = new Vector3(0f, standingHeight - newHeight * 0.5f, 0f);

                // Sink or raise the transform to keep the capsule bottom on the ground.
                float heightDelta = newHeight - currentHeight; // negative = crouching
                if (IsGrounded)
                    controller.Move(Vector3.up * heightDelta);
            }
        }

        private void UpdateStamina()
        {
            if (IsSprinting)
            {
                CurrentStamina -= staminaDrainRate * Time.deltaTime;
                CurrentStamina = Mathf.Max(CurrentStamina, 0f);
                staminaRegenTimer = staminaRegenDelay;
            }
            else
            {
                staminaRegenTimer -= Time.deltaTime;
                if (staminaRegenTimer <= 0f)
                {
                    CurrentStamina += staminaRegenRate * Time.deltaTime;
                    CurrentStamina = Mathf.Min(CurrentStamina, maxStamina);
                }
            }
        }

        // --- Public API ---

        /// <summary>
        /// Instantly remove stamina (called by melee, abilities, etc.).
        /// Resets the regen delay timer so stamina won't regenerate immediately after.
        /// </summary>
        public void ConsumeStamina(float amount)
        {
            CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
            staminaRegenTimer = staminaRegenDelay;
        }

        // --- Input Callbacks ---

        private void OnMove(InputAction.CallbackContext ctx)
        {
            moveInput = ctx.ReadValue<Vector2>();
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            jumpInput = true;
        }

        private void OnSprint(InputAction.CallbackContext ctx)
        {
            sprintInput = ctx.performed;
        }

        private void OnCrouch(InputAction.CallbackContext ctx)
        {
            // Toggle crouch
            if (IsCrouching)
            {
                // Check if there's room to stand up — cast from current capsule top upward
                Vector3 castOrigin = transform.position + Vector3.up * standingHeight;
                float castDistance = standingHeight - crouchHeight;
                if (!Physics.Raycast(castOrigin, Vector3.up, castDistance, groundLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    IsCrouching = false;
                }
            }
            else
            {
                IsCrouching = true;
            }
        }
    }
}
