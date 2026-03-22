using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;
using Voidborne.UI;
using Voidborne.World.Chunks;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Player-side MonoBehaviour that handles entering and exiting vehicles.
    /// Disables FirstPersonController while driving; re-enables it on exit.
    /// </summary>
    public class VehicleInteraction : MonoBehaviour
    {
        [SerializeField] private float enterRange = 3f;
        [SerializeField] private LayerMask vehicleLayers = ~0;

        private FirstPersonController _fpsController;
        private FirstPersonCamera _fpsCamera;
        private VehicleBase _currentVehicle;
        private VehicleInput _vehicleInput;
        private VehicleHUD _vehicleHUD;

        private Transform _originalCameraParent;
        private Vector3 _originalCameraLocalPos;
        private Quaternion _originalCameraLocalRot;
        private Transform _originalPlayerBody;
        private Transform _originalPlayerParent;
        private CharacterController _charController;
        private Rigidbody _playerRb;

        private readonly Collider[] _buffer = new Collider[8];
        private int _enterFrame; // frame we entered, to ignore E press on same frame

        // Alt-look for rotor vehicles
        private bool _isRotorVehicle;
        private bool _altLooking;

        // Chunk loading pause
        private bool _waitingForChunks;
        private Vector3 _savedVelocity;
        private Vector3 _savedAngularVelocity;

        private void Awake()
        {
            _fpsController = GetComponent<FirstPersonController>();
            _fpsCamera = GetComponentInChildren<FirstPersonCamera>();
            _charController = GetComponent<CharacterController>();
            _playerRb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            _vehicleHUD = FindFirstObjectByType<VehicleHUD>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            bool ePressed = Keyboard.current.eKey.wasPressedThisFrame;
            if (!ePressed) return;

            // Skip if any other UI is open
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            if (_currentVehicle == null)
                TryEnterVehicle();
            else
                ExitVehicle();
        }

        private void TryEnterVehicle()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, enterRange, _buffer, vehicleLayers);
            VehicleBase closest = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var vb = _buffer[i].GetComponentInParent<VehicleBase>();
                if (vb == null) continue;
                float d = Vector3.Distance(transform.position, vb.transform.position);
                if (d < bestDist) { bestDist = d; closest = vb; }
            }

            if (closest == null) return;

            if (!closest.TryEnter(gameObject)) return;

            _currentVehicle = closest;
            _enterFrame = Time.frameCount;
            _vehicleInput = closest.GetComponent<VehicleInput>();
            if (_vehicleInput == null)
                _vehicleInput = closest.gameObject.AddComponent<VehicleInput>();

            // Disable FPS movement but keep camera look enabled
            if (_fpsController != null) _fpsController.enabled = false;

            // Reparent camera to vehicle and enable mouse look
            if (_fpsCamera != null)
            {
                var camTransform = _fpsCamera.transform;
                _originalCameraParent = camTransform.parent;
                _originalCameraLocalPos = camTransform.localPosition;
                _originalCameraLocalRot = camTransform.localRotation;
                _originalPlayerBody = _fpsCamera.PlayerBody;

                // Create a camera pivot on the vehicle for free look
                var seat = closest.GetDriverSeat();
                var pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(seat, false);
                // Eye position from vehicle's per-type camera offset
                pivot.transform.localPosition = closest.DriverCameraOffset;
                pivot.transform.localRotation = Quaternion.identity;

                camTransform.SetParent(pivot.transform, false);
                camTransform.localPosition = Vector3.zero;
                camTransform.localRotation = Quaternion.identity;

                _isRotorVehicle = closest is RotorVehicle;
                if (_isRotorVehicle)
                {
                    // Disable camera mouse look — mouse controls pitch/yaw on the vehicle
                    // Alt key re-enables free look temporarily
                    _fpsCamera.enabled = false;
                    // Re-lock cursor (FirstPersonCamera.OnDisable unlocks it)
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    _fpsCamera.PlayerBody = pivot.transform;
                }
            }

            // Parent player to vehicle so world position follows (chunk loading tracks player)
            _originalPlayerParent = transform.parent;
            if (_charController != null) _charController.enabled = false;
            if (_playerRb != null) _playerRb.isKinematic = true;
            transform.SetParent(closest.transform, true);

            // Show vehicle HUD
            if (_vehicleHUD != null)
                _vehicleHUD.AttachToVehicle(closest);
        }

        private void ExitVehicle()
        {
            if (_currentVehicle == null) return;

            _currentVehicle.Exit(gameObject);

            // Restore camera
            if (_fpsCamera != null)
            {
                var camTransform = _fpsCamera.transform;

                // Destroy the camera pivot we created
                var pivot = camTransform.parent;
                camTransform.SetParent(_originalCameraParent, false);
                if (pivot != null && pivot.name == "CameraPivot")
                    Destroy(pivot.gameObject);

                camTransform.localPosition = _originalCameraLocalPos;
                camTransform.localRotation = _originalCameraLocalRot;

                // Restore player body and re-enable camera look
                _fpsCamera.PlayerBody = _originalPlayerBody;
                _fpsCamera.enabled = true;
            }

            // Unparent player from vehicle
            transform.SetParent(_originalPlayerParent, true);

            // Always reset player to upright orientation
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            // Place player at safe exit position (use world-space right, not vehicle right which may be flipped)
            Vector3 exitRight = Vector3.Cross(Vector3.up, _currentVehicle.transform.forward).normalized;
            if (exitRight.sqrMagnitude < 0.01f) exitRight = Vector3.right;
            transform.position = _currentVehicle.transform.position + exitRight * 2f + Vector3.up * 1f;

            // Re-enable player physics
            if (_charController != null) _charController.enabled = true;
            if (_playerRb != null) _playerRb.isKinematic = false;

            // Re-enable FPS
            if (_fpsController != null) _fpsController.enabled = true;

            // Hide HUD
            if (_vehicleHUD != null)
                _vehicleHUD.Detach();

            // Clear chunk waiting state
            if (_waitingForChunks)
            {
                _waitingForChunks = false;
                var rb = _currentVehicle.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }

            _isRotorVehicle = false;
            _altLooking = false;
            _vehicleInput = null;
            _currentVehicle = null;
        }

        private void LateUpdate()
        {
            if (_currentVehicle == null || _vehicleInput == null) return;

            // Alt-look for rotor vehicles
            if (_isRotorVehicle && _fpsCamera != null)
                HandleAltLook();

            // Check if vehicle is in an unloaded chunk — pause physics if so
            CheckChunkReadiness();
            if (_waitingForChunks) return; // player can still look around, but no driving

            // Forward input to vehicle
            _currentVehicle.ApplyMotorForce(_vehicleInput.Throttle);
            _currentVehicle.ApplySteeringForce(_vehicleInput.Steer);
            _currentVehicle.ApplyBrakeForce(_vehicleInput.Brake);
            _currentVehicle.IsBoosting = _vehicleInput.Boost;

            // Tab opens cargo
            if (_vehicleInput.InventoryPressed && _currentVehicle.HasCargo)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.OpenVehicleCargo(_currentVehicle.GetCargoInventory());
            }

            // Exit — skip the frame we entered to avoid same-frame enter+exit
            if (_vehicleInput.ExitPressed && Time.frameCount > _enterFrame)
                ExitVehicle();
        }

        private void HandleAltLook()
        {
            bool altHeld = Keyboard.current != null && Keyboard.current.leftAltKey.isPressed;

            if (altHeld && !_altLooking)
            {
                // Start alt-look: enable camera free look
                _altLooking = true;
                _fpsCamera.enabled = true;
                // Find the pivot and set it as PlayerBody
                var pivot = _fpsCamera.transform.parent;
                if (pivot != null) _fpsCamera.PlayerBody = pivot;
            }
            else if (!altHeld && _altLooking)
            {
                // Stop alt-look: disable camera, it will lerp back
                _altLooking = false;
                _fpsCamera.enabled = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // When not alt-looking, lerp the camera pivot back to forward
            if (!_altLooking)
            {
                var pivot = _fpsCamera.transform.parent;
                if (pivot != null)
                    pivot.localRotation = Quaternion.Slerp(pivot.localRotation, Quaternion.identity, Time.deltaTime * 5f);
                _fpsCamera.transform.localRotation = Quaternion.Slerp(
                    _fpsCamera.transform.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            }
        }

        private void CheckChunkReadiness()
        {
            var cm = ChunkManager.Instance;
            if (cm == null) return;

            Vector3 vehiclePos = _currentVehicle.transform.position;
            Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(vehiclePos);

            // Check the current chunk and immediate forward neighbor
            bool currentReady = IsChunkReady(cm, chunkPos);
            Vector3 fwd = _currentVehicle.transform.forward;
            Vector3Int aheadPos = ChunkCoordUtility.WorldToChunkPos(vehiclePos + fwd * ChunkData.SIZE);
            bool aheadReady = IsChunkReady(cm, aheadPos);

            if (!currentReady || !aheadReady)
            {
                if (!_waitingForChunks)
                {
                    // Freeze vehicle — save velocity for restoration
                    _waitingForChunks = true;
                    var rb = _currentVehicle.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        _savedVelocity = rb.linearVelocity;
                        _savedAngularVelocity = rb.angularVelocity;
                        rb.isKinematic = true;
                    }
                }
            }
            else if (_waitingForChunks)
            {
                // Chunks loaded — restore velocity
                _waitingForChunks = false;
                var rb = _currentVehicle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = _savedVelocity;
                    rb.angularVelocity = _savedAngularVelocity;
                }
            }
        }

        private static bool IsChunkReady(ChunkManager cm, Vector3Int pos)
        {
            var chunk = cm.GetChunk(pos);
            return chunk != null && chunk.state == ChunkState.Active;
        }

        public bool IsInVehicle => _currentVehicle != null;
    }
}
