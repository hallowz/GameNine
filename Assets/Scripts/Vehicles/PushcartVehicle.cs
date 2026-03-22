using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Pushcart — player-pushed vehicle with no engine.
    /// Moves at ≤ 60% player walk speed, completely silent.
    ///
    /// Auto-installs <see cref="defaultCargoBed"/> (typically CargoBed_Large) before
    /// VehicleBase.Awake reads the utility slot, so the cargo inventory is created
    /// with 4× normal capacity without needing the VehicleWorkbench.
    /// </summary>
    public class PushcartVehicle : VehicleBase
    {
        private const float MaxCartSpeed = 3f;  // ~60% of 5 m/s player walk speed

        [Header("Pushcart")]
        [SerializeField] private float pushForce  = 200f;
        [SerializeField] private float brakeForce = 600f;
        [SerializeField] private float turnForce  = 80f;

        [Header("Default Cargo (auto-installed)")]
        [Tooltip("Assign CargoBed_Large here so the pushcart spawns with 4× inventory by default.")]
        [SerializeField] private UtilityComponent defaultCargoBed;

        // ─── Lifecycle ───────────────────────────────────────────────────
        protected override void Awake()
        {
            // Install CargoBed before VehicleBase.Awake() reads utility components
            var assembled = GetComponent<AssembledVehicle>();
            if (assembled != null && defaultCargoBed != null
                && !assembled.HasComponent(AttachmentType.Utility))
            {
                assembled.InstallComponent(defaultCargoBed);
            }

            base.Awake();
        }

        // ─── VehicleBase abstract implementations ───────────────────────

        /// <summary>W/S: push the cart forward or reverse.</summary>
        public override void ApplyMotorForce(float throttle)
        {
            if (Mathf.Abs(throttle) < 0.01f) return;

            float speed = Vector3.Dot(Rb.linearVelocity, transform.forward);
            if (throttle > 0f && speed >= MaxCartSpeed)  return;
            if (throttle < 0f && speed <= -MaxCartSpeed) return;

            Rb.AddForce(transform.forward * (throttle * pushForce), ForceMode.Force);
        }

        /// <summary>A/D: gentle yaw via force at the cart front.</summary>
        public override void ApplySteeringForce(float steer)
        {
            if (Mathf.Abs(steer) < 0.01f) return;
            Rb.AddForceAtPosition(
                transform.right * (steer * turnForce),
                transform.position + transform.forward * 0.8f,
                ForceMode.Force);
        }

        /// <summary>Space: friction braking.</summary>
        public override void ApplyBrakeForce(float brake)
        {
            if (brake < 0.01f) return;
            Rb.AddForce(-Rb.linearVelocity.normalized * (brake * brakeForce), ForceMode.Force);
        }
    }
}
