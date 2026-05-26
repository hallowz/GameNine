using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Voidborne.Player;
using Voidborne.Combat;
using Voidborne.Combat.Melee;
using Voidborne.Enemies;
using Voidborne.World.Chunks;

namespace Voidborne.Diagnostics
{
    /// <summary>
    /// Programmatic player controller for automated gameplay testing.
    /// Uses reflection to inject input values into FirstPersonController's private fields.
    /// Reuses DensityFieldNavigator (same as enemy AI) for terrain-aware pathfinding.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerAutopilot : MonoBehaviour
    {
        // Cached component references
        private FirstPersonController _fpc;
        private CharacterController _cc;
        private Transform _cameraTransform;
        private PlayerInventory _inventory;
        private GunController _gunController;
        private MeleeController _meleeController;
        private WeaponSwitcher _weaponSwitcher;

        // Navigation
        private DensityFieldNavigator _densityNav;

        // Reflection handles (cached once)
        private FieldInfo _moveInputField;
        private FieldInfo _sprintInputField;
        private FieldInfo _jumpInputField;
        private FieldInfo _m1HeldField;
        private MethodInfo _tryFireMethod;
        private MethodInfo _tryBeginWindupMethod;

        // Input state (set by test code, applied each frame)
        private Vector2 _desiredMoveInput;
        private bool _desiredSprint;
        private bool _desiredJump; // single-frame, auto-clears

        private bool _initialized;

        public bool IsInitialized => _initialized;
        public FirstPersonController FPC => _fpc;
        public CharacterController CC => _cc;
        public PlayerInventory Inventory => _inventory;
        public GunController GunCtrl => _gunController;
        public MeleeController MeleeCtrl => _meleeController;
        public WeaponSwitcher Switcher => _weaponSwitcher;

        /// <summary>
        /// Find or create PlayerAutopilot on the player GameObject.
        /// </summary>
        public static PlayerAutopilot GetOrCreate()
        {
            var fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc == null)
            {
                Debug.LogError("[GAMEPLAY] No FirstPersonController found in scene");
                return null;
            }

            var autopilot = fpc.GetComponent<PlayerAutopilot>();
            if (autopilot == null)
                autopilot = fpc.gameObject.AddComponent<PlayerAutopilot>();

            if (!autopilot._initialized)
                autopilot.Init();

            return autopilot;
        }

        public void Init()
        {
            if (_initialized) return;

            const BindingFlags privInst = BindingFlags.NonPublic | BindingFlags.Instance;

            // Resolve components
            _fpc = GetComponent<FirstPersonController>();
            _cc = GetComponent<CharacterController>();
            _cameraTransform = GetComponentInChildren<Camera>()?.transform;
            _inventory = GetComponent<PlayerInventory>();
            _gunController = GetComponent<GunController>();
            _meleeController = GetComponent<MeleeController>();
            _weaponSwitcher = GetComponent<WeaponSwitcher>();

            // Cache reflection handles -- FirstPersonController
            _moveInputField = typeof(FirstPersonController).GetField("moveInput", privInst);
            _sprintInputField = typeof(FirstPersonController).GetField("sprintInput", privInst);
            _jumpInputField = typeof(FirstPersonController).GetField("jumpInput", privInst);

            Debug.Assert(_moveInputField != null, "[GAMEPLAY] FPC.moveInput field not found");
            Debug.Assert(_sprintInputField != null, "[GAMEPLAY] FPC.sprintInput field not found");
            Debug.Assert(_jumpInputField != null, "[GAMEPLAY] FPC.jumpInput field not found");

            // Cache reflection handles -- GunController
            _tryFireMethod = typeof(GunController).GetMethod("TryFire", privInst);
            Debug.Assert(_tryFireMethod != null, "[GAMEPLAY] GunController.TryFire method not found");

            // Cache reflection handles -- MeleeController
            _m1HeldField = typeof(MeleeController).GetField("_m1Held", privInst);
            _tryBeginWindupMethod = typeof(MeleeController).GetMethod("TryBeginWindup", privInst);
            Debug.Assert(_m1HeldField != null, "[GAMEPLAY] MeleeController._m1Held field not found");
            Debug.Assert(_tryBeginWindupMethod != null, "[GAMEPLAY] MeleeController.TryBeginWindup method not found");

            // Create DensityFieldNavigator (plain C# class, needs ChunkManager)
            _densityNav = new DensityFieldNavigator();

            _initialized = true;
            Debug.Log("[GAMEPLAY] PlayerAutopilot initialized successfully");
        }

        private void Update()
        {
            if (!_initialized || _fpc == null) return;

            // Inject input values into FPC's private fields before FPC reads them
            _moveInputField.SetValue(_fpc, _desiredMoveInput);
            _sprintInputField.SetValue(_fpc, _desiredSprint);

            if (_desiredJump)
            {
                _jumpInputField.SetValue(_fpc, true);
                _desiredJump = false; // single-frame trigger
            }
            else
            {
                _jumpInputField.SetValue(_fpc, false);
            }
        }

        // =====================================================================
        // Input Control Methods
        // =====================================================================

        /// <summary>Set movement direction. (0,1)=forward, (1,0)=right, etc.</summary>
        public void SetMovement(Vector2 input) => _desiredMoveInput = input;

        /// <summary>Move forward at full speed.</summary>
        public void MoveForward() => _desiredMoveInput = Vector2.up;

        /// <summary>Move backward.</summary>
        public void MoveBackward() => _desiredMoveInput = Vector2.down;

        /// <summary>Stop all movement.</summary>
        public void Stop()
        {
            _desiredMoveInput = Vector2.zero;
            _desiredSprint = false;
        }

        /// <summary>Enable/disable sprint.</summary>
        public void SetSprint(bool sprint) => _desiredSprint = sprint;

        /// <summary>Trigger a jump (single frame).</summary>
        public void Jump() => _desiredJump = true;

        /// <summary>Toggle crouch state.</summary>
        public void SetCrouch(bool crouch)
        {
            // IsCrouching is a public property with private set
            // The OnCrouch callback toggles it. We need to set the backing field.
            var prop = typeof(FirstPersonController).GetProperty("IsCrouching");
            var backingField = typeof(FirstPersonController).GetField("<IsCrouching>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
                backingField.SetValue(_fpc, crouch);
        }

        // =====================================================================
        // Camera / Look Control
        // =====================================================================

        /// <summary>Rotate player body and camera to face a world position.</summary>
        public void LookAt(Vector3 worldTarget)
        {
            Vector3 toTarget = worldTarget - transform.position;

            // Yaw (body rotation)
            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Pitch (camera rotation)
            if (_cameraTransform != null)
            {
                float horizontalDist = new Vector2(toTarget.x, toTarget.z).magnitude;
                float pitch = -Mathf.Atan2(toTarget.y, horizontalDist) * Mathf.Rad2Deg;
                _cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
        }

        // =====================================================================
        // Weapon Actions
        // =====================================================================

        /// <summary>Fire the currently equipped gun once.</summary>
        public void FireGun()
        {
            if (_gunController == null || _gunController.CurrentGun == null)
            {
                Debug.LogWarning("[GAMEPLAY] Cannot fire - no gun equipped");
                return;
            }
            _tryFireMethod.Invoke(_gunController, null);
        }

        /// <summary>Begin a melee swing. Sets m1Held and triggers windup.</summary>
        public void SwingMelee()
        {
            if (_meleeController == null)
            {
                Debug.LogWarning("[GAMEPLAY] Cannot swing - no MeleeController");
                return;
            }
            _m1HeldField.SetValue(_meleeController, true);
            _tryBeginWindupMethod.Invoke(_meleeController, null);
        }

        /// <summary>Release melee button (allows windup to transition to release).</summary>
        public void ReleaseMelee()
        {
            if (_meleeController != null)
                _m1HeldField.SetValue(_meleeController, false);
        }

        /// <summary>Full melee attack sequence: press, wait, release.</summary>
        public IEnumerator PerformMeleeAttack(float holdTime = 0.3f)
        {
            SwingMelee();
            yield return new WaitForSeconds(holdTime);
            ReleaseMelee();
        }

        // =====================================================================
        // Navigation
        // =====================================================================

        /// <summary>
        /// Wait until terrain chunks are loaded around the player.
        /// Checks player's chunk and 6 face-adjacent neighbors.
        /// </summary>
        public IEnumerator WaitForTerrain(float timeout = 30f)
        {
            Debug.Log("[GAMEPLAY] Waiting for terrain generation...");
            float startTime = Time.time;

            while (Time.time - startTime < timeout)
            {
                if (ChunkManager.Instance == null)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                Vector3Int playerChunk = ChunkCoordUtility.WorldToChunkPos(transform.position);
                var chunk = ChunkManager.Instance.GetChunk(playerChunk);

                if (chunk != null && chunk.state == ChunkState.Active)
                {
                    // Check at least a few neighbors
                    int activeNeighbors = 0;
                    Vector3Int[] offsets = new Vector3Int[]
                    {
                        Vector3Int.right, Vector3Int.left,
                        Vector3Int.forward, Vector3Int.back,
                        Vector3Int.up, Vector3Int.down
                    };

                    foreach (var offset in offsets)
                    {
                        var neighbor = ChunkManager.Instance.GetChunk(playerChunk + offset);
                        if (neighbor != null && neighbor.state == ChunkState.Active)
                            activeNeighbors++;
                    }

                    if (activeNeighbors >= 4) // At least 4 of 6 neighbors active
                    {
                        float elapsed = Time.time - startTime;
                        Debug.Log($"[GAMEPLAY] Terrain ready in {elapsed:F1}s ({activeNeighbors}/6 neighbors active)");
                        yield break;
                    }
                }

                yield return new WaitForSeconds(0.25f);
            }

            Debug.LogWarning($"[GAMEPLAY] Terrain wait timed out after {timeout}s");
        }

        /// <summary>
        /// Navigate to a target position using DensityFieldNavigator for obstacle avoidance.
        /// Same pathfinding approach used by enemy AI.
        /// </summary>
        public IEnumerator MoveTo(Vector3 target, float arrivalDistance = 1.5f, float timeout = 30f)
        {
            Debug.Log($"[GAMEPLAY] Navigating to {target} (arrival dist: {arrivalDistance}m)");
            float startTime = Time.time;

            while (Time.time - startTime < timeout)
            {
                Vector3 currentPos = transform.position;
                Vector3 toTarget = target - currentPos;
                toTarget.y = 0f; // XZ plane only
                float distance = toTarget.magnitude;

                if (distance < arrivalDistance)
                {
                    Stop();
                    Debug.Log($"[GAMEPLAY] Arrived at target (distance: {distance:F1}m)");
                    yield break;
                }

                Vector3 desiredDir = toTarget.normalized;
                Vector3 moveDir = desiredDir;

                // Check for obstruction and find alternative direction
                if (_densityNav != null)
                {
                    Vector3 chestPos = currentPos + Vector3.up * 1.0f;
                    if (_densityNav.IsObstructed(chestPos, desiredDir, 2.0f))
                    {
                        // Find best open direction closest to desired
                        var openDirs = _densityNav.GetOpenDirections(currentPos, 2.0f);
                        if (openDirs != null && openDirs.Count > 0)
                        {
                            float bestDot = -2f;
                            Vector3 bestDir = desiredDir;
                            foreach (var dir in openDirs)
                            {
                                float dot = Vector3.Dot(dir.normalized, desiredDir);
                                if (dot > bestDot)
                                {
                                    bestDot = dot;
                                    bestDir = dir.normalized;
                                }
                            }
                            moveDir = bestDir;
                        }
                    }
                }

                // Convert world direction to local input space
                Vector3 localDir = transform.InverseTransformDirection(moveDir);
                _desiredMoveInput = new Vector2(localDir.x, localDir.z).normalized;

                // Face the movement direction
                LookAt(currentPos + moveDir * 10f);

                yield return null;
            }

            Stop();
            Debug.LogWarning($"[GAMEPLAY] Navigation timed out after {timeout}s");
        }

        /// <summary>
        /// Get the terrain surface height at an XZ position.
        /// </summary>
        public float GetSurfaceHeight(Vector3 worldPos)
        {
            if (_densityNav != null)
                return _densityNav.GetSurfaceHeightAt(worldPos);
            return worldPos.y;
        }

        private void OnDestroy()
        {
            _initialized = false;
        }
    }
}
