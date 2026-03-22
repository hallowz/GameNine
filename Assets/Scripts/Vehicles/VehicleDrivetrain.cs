using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Realistic drivetrain simulation: RPM, torque curve, gear ratios, auto-shifting.
    /// Attach to any vehicle with an EngineComponent that has drivetrain data.
    /// </summary>
    public class VehicleDrivetrain : MonoBehaviour
    {
        // ─── Runtime state ──────────────────────────────────────────────
        public float CurrentRPM { get; private set; }
        public int CurrentGear { get; private set; }
        public float RPMNormalized { get; private set; }
        public bool IsReversing { get; private set; }

        private EngineComponent _engine;
        private Rigidbody _rb;
        private WheelCollider[] _driveWheels;
        private float _shiftCooldown;

        private const float ShiftDelay = 0.2f;
        private const float UpshiftPoint = 0.85f;
        private const float DownshiftPoint = 0.35f;

        public void Initialize(EngineComponent engine, Rigidbody rb, WheelCollider[] driveWheels)
        {
            _engine = engine;
            _rb = rb;
            _driveWheels = driveWheels;
            CurrentGear = 0;
            CurrentRPM = engine.idleRPM;
        }

        public bool IsActive => _engine != null && _engine.HasDrivetrain;

        /// <summary>
        /// Call from FixedUpdate. Returns the torque to apply to each drive wheel.
        /// Handles RPM tracking, gear shifting, engine braking, and rev limiting.
        /// </summary>
        public float GetWheelTorque(float throttle, float boostMultiplier = 1f)
        {
            if (_engine == null || !_engine.HasDrivetrain) return 0f;

            IsReversing = throttle < -0.01f;

            // Get average drive wheel angular velocity
            float avgWheelRPM = GetAverageDriveWheelRPM();

            // Calculate engine RPM from wheel speed
            float gearRatio = _engine.gearRatios[CurrentGear];
            float driveRatio = gearRatio * _engine.finalDriveRatio;
            float engineRPM = Mathf.Abs(avgWheelRPM) * driveRatio;
            engineRPM = Mathf.Clamp(engineRPM, _engine.idleRPM, _engine.redlineRPM);

            // Smooth RPM changes for visual/audio feel
            CurrentRPM = Mathf.Lerp(CurrentRPM, engineRPM, Time.fixedDeltaTime * 8f);
            RPMNormalized = Mathf.InverseLerp(0f, _engine.redlineRPM, CurrentRPM);

            // Auto-shift
            _shiftCooldown -= Time.fixedDeltaTime;
            if (_shiftCooldown <= 0f)
                AutoShift();

            // Rev limiter: cut torque at redline
            if (CurrentRPM >= _engine.redlineRPM * 0.98f && throttle > 0f)
                return 0f;

            float absThrottle = Mathf.Abs(throttle);

            // Engine braking when off-throttle
            if (absThrottle < 0.05f)
            {
                float brakeTorque = -Mathf.Sign(avgWheelRPM)
                    * CurrentRPM * _engine.engineBrakingFactor * 0.001f
                    * driveRatio;
                return brakeTorque;
            }

            // Evaluate torque from curve
            float engineTorque = EvaluateTorque(CurrentRPM) * absThrottle * boostMultiplier;
            float wheelTorque = engineTorque * driveRatio;

            // Reverse: flip direction, limit to 1st gear equivalent
            if (IsReversing)
                wheelTorque = -wheelTorque * 0.6f;

            return wheelTorque;
        }

        /// <summary>Parabolic torque curve peaking at peakTorqueRPM.</summary>
        float EvaluateTorque(float rpm)
        {
            float peak = _engine.peakTorqueNm;
            float peakRPM = _engine.peakTorqueRPM;
            float diff = (rpm - peakRPM) / peakRPM;
            float torque = peak * (1f - diff * diff);
            return Mathf.Max(0f, torque);
        }

        void AutoShift()
        {
            float rpmFraction = CurrentRPM / _engine.redlineRPM;
            int maxGear = _engine.gearRatios.Length - 1;

            if (rpmFraction > UpshiftPoint && CurrentGear < maxGear)
            {
                CurrentGear++;
                _shiftCooldown = ShiftDelay;
            }
            else if (rpmFraction < DownshiftPoint && CurrentGear > 0)
            {
                CurrentGear--;
                _shiftCooldown = ShiftDelay;
            }
        }

        float GetAverageDriveWheelRPM()
        {
            if (_driveWheels == null || _driveWheels.Length == 0) return 0f;

            float total = 0f;
            int count = 0;
            foreach (var wc in _driveWheels)
            {
                if (wc == null || !wc.enabled) continue;
                total += wc.rpm;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        // ─── Display helpers ────────────────────────────────────────────

        /// <summary>Current horsepower output (display only).</summary>
        public float CurrentHP
        {
            get
            {
                float torque = EvaluateTorque(CurrentRPM);
                return torque * CurrentRPM / 5252f;
            }
        }

        /// <summary>Display string for current gear.</summary>
        public string GearDisplay
        {
            get
            {
                if (IsReversing) return "R";
                if (CurrentRPM <= _engine.idleRPM * 1.1f && _rb.linearVelocity.magnitude < 0.5f) return "N";
                return (CurrentGear + 1).ToString();
            }
        }
    }
}
