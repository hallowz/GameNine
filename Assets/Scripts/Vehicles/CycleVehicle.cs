using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Two-wheel motorbike. Leans into turns via rigidbody torque.
    /// Fast and high top speed, but twitchy at low speed and stable at high speed.
    /// Either destroyed drivetrain zone = unrideable (one wheel gone = tips over).
    /// </summary>
    public class CycleVehicle : WheeledVehicle
    {
        [Header("Cycle Lean")]
        [SerializeField] private float maxLeanAngle     = 25f;   // degrees of max lean at speed
        [SerializeField] private float leanTorque       = 800f;  // corrective torque strength
        [SerializeField] private float leanSmoothing    = 5f;    // how quickly lean target is reached
        [SerializeField] private float stabilizeSpeed   = 8f;    // m/s at which bike is fully stable
        [SerializeField] private float lowSpeedWobble   = 150f;  // random wobble force below 2 m/s

        private float _currentSteer;
        private float _currentLean;

        public override void ApplySteeringForce(float steer)
        {
            _currentSteer = steer;
            base.ApplySteeringForce(steer);
        }

        /// <summary>Either drivetrain destroyed = no drive, bike loses balance and falls.</summary>
        public override void ApplyMotorForce(float throttle)
        {
            if (DamageSystem.GetZone(DamageZone.LeftDrivetrain).IsDestroyed ||
                DamageSystem.GetZone(DamageZone.RightDrivetrain).IsDestroyed)
                return;

            base.ApplyMotorForce(throttle);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (IsBeingDriven)
                ApplyLeanPhysics();
        }

        private void ApplyLeanPhysics()
        {
            float speed = Rb.linearVelocity.magnitude;

            // Stability: 0 when stopped, 1 at stabilizeSpeed and beyond
            float stab = Mathf.Clamp01(speed / stabilizeSpeed);

            // Target roll: lean into the turn, deeper at speed
            float targetLean = -_currentSteer * maxLeanAngle * stab;
            _currentLean = Mathf.Lerp(_currentLean, targetLean, Time.fixedDeltaTime * leanSmoothing);

            // Corrective torque toward target lean
            float rollError = _currentLean - GetLocalRollDegrees();
            Rb.AddRelativeTorque(Vector3.forward * rollError * leanTorque * Time.fixedDeltaTime, ForceMode.Force);

            // Low-speed twitchiness — destabilising wobble when nearly stopped
            if (speed < 2f)
            {
                float wobbleFactor = 1f - (speed / 2f);
                Rb.AddRelativeTorque(
                    Vector3.forward * Random.Range(-lowSpeedWobble, lowSpeedWobble) * wobbleFactor * Time.fixedDeltaTime,
                    ForceMode.Force);
            }
        }

        /// <summary>Returns the vehicle's current roll around its local forward axis, in degrees.</summary>
        private float GetLocalRollDegrees()
        {
            Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
            return Mathf.Atan2(localUp.x, localUp.y) * Mathf.Rad2Deg;
        }
    }
}
