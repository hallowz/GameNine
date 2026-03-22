using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Gyrocopter — lifted by rotor thrust, no WheelColliders.
    ///
    /// Controls:
    ///   W       → increase thrust above hover (ascend / accelerate)
    ///   S       → reduce thrust below hover (descend), minimum 0
    ///   A/D     → roll left / right
    ///   Mouse X → yaw left / right
    ///   Mouse Y → pitch (mouse down = nose down, mouse up = nose up)
    ///
    /// At rest (no W/S), thrust equals gravity — the copter hovers in place.
    /// </summary>
    public class RotorVehicle : VehicleBase
    {
        // ─── Serialized ─────────────────────────────────────────────────
        [Header("Flight Feel")]
        [SerializeField] private float pitchSensitivity = 1.5f;
        [SerializeField] private float yawSensitivity   = 1.8f;
        [SerializeField] private float rollTorque        = 80f;
        [SerializeField] private float pitchTorque        = 600f;
        [SerializeField] private float yawTorque          = 500f;
        [SerializeField] private float angularDrag        = 4f;

        [Header("Thrust")]
        [Tooltip("How much extra thrust W adds above hover (multiplier of gravity)")]
        [SerializeField] private float thrustBoostFactor  = 0.8f;
        [Tooltip("How much thrust S removes below hover (multiplier of gravity)")]
        [SerializeField] private float thrustReduceFactor = 0.7f;


        // ─── Runtime state ───────────────────────────────────────────────
        private float _cachedThrottle; // W/S: +1 = up, -1 = down
        private float _cachedRoll;     // A/D: -1 = left, +1 = right

        private readonly List<RotorComponent> _rotors = new List<RotorComponent>();
        private readonly List<Transform> _rotorVisuals = new List<Transform>();
        private float _totalLiftForce; // sum of rotor lift at full throttle

        // ─── Lifecycle ───────────────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();

            foreach (var comp in Assembly.GetInstalledComponents(AttachmentType.Rotor))
                if (comp is RotorComponent rc) _rotors.Add(rc);

            Rb.angularDamping = angularDrag;
            Rb.linearDamping = 0.2f; // light air drag

            _totalLiftForce = 0f;
            foreach (var r in _rotors)
                _totalLiftForce += r.liftForce;

            SpawnRotorVisuals();
        }

        private void SpawnRotorVisuals()
        {
            if (Assembly.Frame == null) return;
            int slot = 0;
            foreach (var ap in Assembly.Frame.attachmentPoints)
            {
                if (ap.attachmentType != AttachmentType.Rotor) continue;
                if (slot >= _rotors.Count) break;

                var rotor = _rotors[slot];
                Transform vis;
                if (rotor.rotorVisualPrefab != null)
                {
                    var go = Instantiate(rotor.rotorVisualPrefab, transform);
                    go.transform.localPosition = ap.localPosition;
                    vis = go.transform;
                }
                else
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "RotorDisc_" + slot;
                    go.transform.SetParent(transform);
                    go.transform.localPosition = ap.localPosition + Vector3.up * 0.05f;
                    go.transform.localScale = new Vector3(1.4f, 0.04f, 1.4f);
                    foreach (var c in go.GetComponents<Collider>())
                        Destroy(c);
                    vis = go.transform;
                }
                _rotorVisuals.Add(vis);
                slot++;
            }
        }

        // ─── FixedUpdate ─────────────────────────────────────────────────
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!IsBeingDriven) return;
            if (DamageSystem.GetZone(DamageZone.Chassis).IsDestroyed) return;

            // Mouse input for pitch and yaw
            if (Mouse.current != null)
            {
                float mouseX = Mouse.current.delta.x.ReadValue();
                float mouseY = Mouse.current.delta.y.ReadValue();

                // Yaw from mouse X
                if (Mathf.Abs(mouseX) > 0.1f)
                    Rb.AddRelativeTorque(Vector3.up * (mouseX * yawSensitivity * yawTorque * Time.fixedDeltaTime), ForceMode.Force);

                // Pitch from mouse Y (mouse down = nose down = positive pitch torque)
                if (Mathf.Abs(mouseY) > 0.1f)
                    Rb.AddRelativeTorque(Vector3.right * (-mouseY * pitchSensitivity * pitchTorque * Time.fixedDeltaTime), ForceMode.Force);
            }

            // Roll from A/D
            if (Mathf.Abs(_cachedRoll) > 0.01f)
                Rb.AddRelativeTorque(Vector3.forward * (-_cachedRoll * rollTorque), ForceMode.Force);

            ApplyThrust();
            SpinRotorVisuals();
        }

        private void ApplyThrust()
        {
            // Base hover thrust = exactly counteract gravity
            float hoverForce = Rb.mass * Physics.gravity.magnitude;

            // W adds thrust above hover, S reduces below hover (min 0)
            float thrustMul;
            if (_cachedThrottle > 0.01f)
                thrustMul = 1f + _cachedThrottle * thrustBoostFactor;
            else if (_cachedThrottle < -0.01f)
                thrustMul = Mathf.Max(0f, 1f + _cachedThrottle * thrustReduceFactor);
            else
                thrustMul = 1f; // hover

            // Apply rotor availability (stall / altitude ceiling)
            float rotorFactor = GetRotorFactor();
            float finalThrust = hoverForce * thrustMul * rotorFactor;

            // Thrust is applied along the vehicle's local up (tilting = movement)
            Rb.AddForce(transform.up * finalThrust, ForceMode.Force);
        }

        private float GetRotorFactor()
        {
            if (_rotors.Count == 0) return 0f;
            return 1f;
        }

        private void SpinRotorVisuals()
        {
            // Spin faster when thrusting up, slower when reducing
            float spinMul = _cachedThrottle > 0 ? 1f + _cachedThrottle * 0.5f : 1f + _cachedThrottle * 0.3f;
            float spinDeg = spinMul * 720f * Time.fixedDeltaTime;
            foreach (var vis in _rotorVisuals)
                if (vis != null) vis.Rotate(Vector3.up, spinDeg, Space.Self);
        }

        // ─── VehicleBase abstract implementations ───────────────────────

        /// <param name="throttle">W/S: +1 = thrust above hover, -1 = reduce below hover.</param>
        public override void ApplyMotorForce(float throttle) => _cachedThrottle = throttle;

        /// <param name="steer">A/D: roll left/right.</param>
        public override void ApplySteeringForce(float steer) => _cachedRoll = steer;

        /// <param name="brake">Unused for rotor vehicle.</param>
        public override void ApplyBrakeForce(float brake) { }

        /// <summary>Current thrust level for HUD. 1 = hover, >1 = ascending, <1 = descending.</summary>
        public float ThrustLevel => _cachedThrottle > 0
            ? 1f + _cachedThrottle * thrustBoostFactor
            : Mathf.Max(0f, 1f + _cachedThrottle * thrustReduceFactor);
    }
}
