using Unity.Mathematics;
using UnityEngine;
using Voidborne.World.Generation;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Wheeled vehicle using Unity WheelColliders.
    /// Handles suspension config from SuspensionComponent, motor/steer/brake,
    /// anti-roll stabilization, asymmetric drivetrain damage, and biome surface friction.
    /// </summary>
    public class WheeledVehicle : VehicleBase
    {
        [Header("Wheels")]
        [SerializeField] protected WheelCollider[] wheelColliders;
        [SerializeField] protected Transform[] wheelMeshes;
        /// <summary>Which wheel indices receive motor torque.</summary>
        [SerializeField] protected bool[] isDriveWheel;
        /// <summary>Which wheel indices apply steering angle.</summary>
        [SerializeField] protected bool[] isSteerWheel;
        [SerializeField] protected float maxSteerAngle = 30f;

        [Header("Anti-Roll Bar")]
        [SerializeField] private float rollResistance = 2500f;
        [SerializeField] private float rollThreshold = 8f; // lateral G-force threshold (m/s^2)

        [Header("Drift")]
        [SerializeField] private float driftThreshold = 5f;
        [SerializeField] private float driftFrictionMultiplier = 0.75f;
        [SerializeField] private float brakeLockSpeedThreshold = 8f;

        [Header("Rolling Resistance")]
        [SerializeField] private float rollingResistanceTorque = 80f;

        [Header("Surface Friction (biome-dependent)")]
        [SerializeField] private float dirtFriction = 0.8f;
        [SerializeField] private float rockFriction = 1.1f;
        [SerializeField] private float sandFriction = 0.6f;

        // Drivetrain damage torque factors (1 = full, 0 = destroyed)
        protected float LeftTorqueFactor  = 1f;
        protected float RightTorqueFactor = 1f;

        private SuspensionComponent _suspension;
        private float _frictionTimer;
        private const float FrictionInterval = 0.5f;

        // Cached drive wheel count to avoid recomputing every FixedUpdate
        private int _cachedDriveCount;

        // Rollover tracking
        private float _flippedTime;

        // Realistic drivetrain (optional — falls back to flat power if absent)
        protected VehicleDrivetrain Drivetrain { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _suspension = Assembly.GetFirstInstalledComponent<SuspensionComponent>(AttachmentType.Suspension);
            ApplySuspensionToWheels();
            DamageSystem.OnZoneChanged += OnDamageZoneChanged;

            // Initialize drivetrain if engine has gear data
            if (InstalledEngine != null && InstalledEngine.HasDrivetrain)
            {
                Drivetrain = gameObject.AddComponent<VehicleDrivetrain>();
                var driveWheelList = new System.Collections.Generic.List<WheelCollider>();
                if (wheelColliders != null && isDriveWheel != null)
                {
                    for (int i = 0; i < wheelColliders.Length && i < isDriveWheel.Length; i++)
                        if (isDriveWheel[i] && wheelColliders[i] != null)
                            driveWheelList.Add(wheelColliders[i]);
                }
                Drivetrain.Initialize(InstalledEngine, Rb, driveWheelList.ToArray());
            }

            // Cache drive wheel count
            _cachedDriveCount = 0;
            if (wheelColliders != null && isDriveWheel != null)
            {
                for (int i = 0; i < wheelColliders.Length && i < isDriveWheel.Length; i++)
                    if (isDriveWheel[i] && wheelColliders[i] != null) _cachedDriveCount++;
            }
        }

        private void OnDestroy()
        {
            if (DamageSystem != null)
                DamageSystem.OnZoneChanged -= OnDamageZoneChanged;
        }

        private void ApplySuspensionToWheels()
        {
            if (wheelColliders == null) return;

            int wheelCount = 0;
            foreach (var wc in wheelColliders)
                if (wc != null) wheelCount++;
            if (wheelCount == 0) return;

            float dist = _suspension != null ? _suspension.suspensionDistance : 0.3f;

            // Calculate spring so rest position sits at 50% of suspension travel (targetPosition = 0.5).
            // F_spring = mass * g  =>  spring * dist * 0.5 = (mass * g) / wheelCount
            float massPerWheel = Rb.mass / wheelCount;
            float idealSpring  = (massPerWheel * 9.81f) / (dist * 0.5f);

            // Critical damping: 2 * sqrt(k * m) — prevents oscillation on landing
            float idealDamper = 2f * Mathf.Sqrt(idealSpring * massPerWheel);

            foreach (var wc in wheelColliders)
            {
                if (wc == null) continue;
                var spring = wc.suspensionSpring;
                spring.spring = idealSpring;
                spring.damper = idealDamper;
                wc.suspensionSpring   = spring;
                wc.suspensionDistance  = dist;
            }
        }

        protected virtual void OnDamageZoneChanged(DamageZone zone, DamageStage stage)
        {
            if (zone == DamageZone.LeftDrivetrain)
            {
                LeftTorqueFactor = TorqueForStage(stage);
                if (stage == DamageStage.Destroyed) EjectWheels(left: true);
            }
            else if (zone == DamageZone.RightDrivetrain)
            {
                RightTorqueFactor = TorqueForStage(stage);
                if (stage == DamageStage.Destroyed) EjectWheels(left: false);
            }
        }

        private static float TorqueForStage(DamageStage s) => s switch
        {
            DamageStage.Healthy   => 1.0f,
            DamageStage.Damaged   => 0.6f,
            DamageStage.Critical  => 0.2f,
            DamageStage.Destroyed => 0.0f,
            _                     => 1.0f
        };

        /// <summary>Hides wheel meshes on the destroyed side and detaches wheel parts or spawns debris.</summary>
        protected virtual void EjectWheels(bool left)
        {
            if (wheelColliders == null || wheelMeshes == null) return;

            // Try to detach via VehicleBody for proper WorldItem ejection
            if (Body != null)
            {
                string[] wheelSlots = left
                    ? new[] { "wheel_fl", "wheel_rl", "wheel_ml" }
                    : new[] { "wheel_fr", "wheel_rr", "wheel_mr" };

                foreach (string slotId in wheelSlots)
                {
                    var slot = Body.GetSlot(slotId);
                    if (slot != null && !slot.IsEmpty)
                        slot.Detach();
                }
            }

            // Disable WheelColliders and meshes on the destroyed side
            int n = Mathf.Min(wheelColliders.Length, wheelMeshes.Length);
            for (int i = 0; i < n; i++)
            {
                if (wheelColliders[i] == null || wheelMeshes[i] == null) continue;
                bool isLeft = wheelColliders[i].transform.localPosition.x < 0f;
                if (isLeft != left) continue;

                wheelColliders[i].enabled = false;
                wheelMeshes[i].gameObject.SetActive(false);

                // Legacy fallback: fly-off debris sphere when no VehicleBody
                if (Body == null)
                {
                    var fly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fly.transform.SetPositionAndRotation(wheelMeshes[i].position, wheelMeshes[i].rotation);
                    fly.transform.localScale = Vector3.one * (wheelColliders[i].radius * 2f);
                    var rb = fly.GetComponent<Rigidbody>() ?? fly.AddComponent<Rigidbody>();
                    Vector3 eject = (left ? -transform.right : transform.right) + Vector3.up * 0.5f;
                    rb.AddForce(eject.normalized * 6f, ForceMode.Impulse);
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * 12f, ForceMode.Impulse);
                    Destroy(fly, 8f);
                }
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            // Parking brake: when no driver, lock all wheels
            if (!IsBeingDriven)
            {
                ApplyParkingBrake();
                SyncWheelVisuals();
                return;
            }

            SyncWheelVisuals();
            ApplyAntiRoll();
            ApplyDriftPhysics();
            ApplyRollingResistance();
            CheckRollover();

            _frictionTimer -= Time.fixedDeltaTime;
            if (_frictionTimer <= 0f)
            {
                _frictionTimer = FrictionInterval;
                UpdateSurfaceFriction();
            }
        }

        private void ApplyRollingResistance()
        {
            if (wheelColliders == null || IsBraking) return;
            // When no throttle is applied, add light braking to simulate rolling resistance + engine drag
            // The drivetrain also applies engine braking, but this ensures non-drivetrain wheels slow down too
            foreach (var wc in wheelColliders)
            {
                if (wc == null || !wc.enabled) continue;
                if (Mathf.Abs(wc.motorTorque) < 1f) // no throttle on this wheel
                    wc.brakeTorque = Mathf.Max(wc.brakeTorque, rollingResistanceTorque);
            }
        }

        private void ApplyParkingBrake()
        {
            if (wheelColliders == null) return;
            foreach (var wc in wheelColliders)
            {
                if (wc == null) continue;
                wc.motorTorque = 0f;
                wc.brakeTorque = 5000f;
            }
        }

        private void SyncWheelVisuals()
        {
            if (wheelColliders == null || wheelMeshes == null) return;
            int n = Mathf.Min(wheelColliders.Length, wheelMeshes.Length);
            for (int i = 0; i < n; i++)
            {
                if (wheelColliders[i] == null || wheelMeshes[i] == null) continue;
                wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheelMeshes[i].SetPositionAndRotation(pos, rot);
            }
        }

        private void ApplyAntiRoll()
        {
            if (wheelColliders == null) return;
            // Wheels laid out in pairs: (0,1)=front axle, (2,3)=rear, (4,5)=mid (Hauler)
            for (int i = 0; i + 1 < wheelColliders.Length; i += 2)
                ApplyAntiRollPair(i, i + 1);
        }

        private void ApplyAntiRollPair(int a, int b)
        {
            var wcA = wheelColliders[a];
            var wcB = wheelColliders[b];
            if (wcA == null || wcB == null) return;

            bool gA = wcA.GetGroundHit(out WheelHit hA);
            bool gB = wcB.GetGroundHit(out WheelHit hB);

            float tA = gA
                ? Mathf.Clamp01((-wcA.transform.InverseTransformPoint(hA.point).y - wcA.radius) / wcA.suspensionDistance)
                : 1f;
            float tB = gB
                ? Mathf.Clamp01((-wcB.transform.InverseTransformPoint(hB.point).y - wcB.radius) / wcB.suspensionDistance)
                : 1f;

            // Calculate lateral G-force to allow rollover at extremes
            float lateralG = Mathf.Abs(Vector3.Dot(Rb.linearVelocity, transform.right));
            float lateralAccel = lateralG > 0.1f ? lateralG * lateralG / 5f : 0f; // rough G estimate
            float rollMul = lateralAccel > rollThreshold ? 0.2f : 1f;

            float torque = (tA - tB) * rollResistance * rollMul;
            if (gA) Rb.AddForceAtPosition(wcA.transform.up * -torque, wcA.transform.position);
            if (gB) Rb.AddForceAtPosition(wcB.transform.up *  torque, wcB.transform.position);
        }

        private void ApplyDriftPhysics()
        {
            if (wheelColliders == null) return;

            float lateralSpeed = Mathf.Abs(Vector3.Dot(Rb.linearVelocity, transform.right));
            float forwardSpeed = Rb.linearVelocity.magnitude;

            // Always apply lateral correction force to resist casual sliding
            if (lateralSpeed > 0.5f)
            {
                float correctionStrength = lateralSpeed > driftThreshold ? 0.3f : 0.6f;
                Vector3 counterForce = -Vector3.Project(Rb.linearVelocity, transform.right) * correctionStrength;
                Rb.AddForce(counterForce, ForceMode.Acceleration);
            }

            foreach (var wc in wheelColliders)
            {
                if (wc == null) continue;

                var sw = wc.sidewaysFriction;
                var fw = wc.forwardFriction;

                // Boost baseline sideways stiffness for grip
                sw.stiffness = Mathf.Max(sw.stiffness, 1.2f);

                // Drift: only reduce sideways friction when sliding hard laterally
                if (lateralSpeed > driftThreshold)
                    sw.stiffness *= driftFrictionMultiplier;

                // Brake lock: reduce forward friction when braking at speed
                if (IsBraking && forwardSpeed > brakeLockSpeedThreshold)
                    fw.stiffness *= 0.5f;

                wc.sidewaysFriction = sw;
                wc.forwardFriction = fw;
            }
        }

        private void CheckRollover()
        {
            float upDot = Vector3.Dot(transform.up, Vector3.up);

            if (upDot < -0.3f)
            {
                _flippedTime += Time.fixedDeltaTime;
                if (_flippedTime >= 2f)
                {
                    // Force eject all passengers
                    var vb = GetComponent<VehicleBase>();
                    // VehicleBase handles passengers — we trigger it via the public API
                    // Passengers will be ejected by the interaction system detecting the flip
                    _flippedTime = 0f;
                }
            }
            else
            {
                _flippedTime = 0f;
            }
        }

        private void UpdateSurfaceFriction()
        {
            if (wheelColliders == null) return;
            var p = transform.position;
            var biome = BiomeMap.GetBiome(new float2(p.x, p.z));
            float friction = ClassifyBiomeFriction(biome != null ? biome.biomeName : string.Empty);
            foreach (var wc in wheelColliders)
            {
                if (wc == null) continue;
                var fw = wc.forwardFriction;  fw.stiffness = friction; wc.forwardFriction  = fw;
                var sw = wc.sidewaysFriction; sw.stiffness = friction; wc.sidewaysFriction = sw;
            }
        }

        private float ClassifyBiomeFriction(string name)
        {
            if (string.IsNullOrEmpty(name)) return dirtFriction;
            // Use OrdinalIgnoreCase to avoid string allocation from ToLowerInvariant
            if (name.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("cryst", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return rockFriction;
            if (name.IndexOf("sand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("desert", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ash", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return sandFriction;
            return dirtFriction;
        }

        // ─── Helpers ────────────────────────────────────────────────────

        /// <summary>Sum of all installed engine powers (supports dual-engine frames like Hauler).</summary>
        protected float GetTotalEnginePower()
        {
            float total = 0f;
            foreach (var comp in Assembly.GetInstalledComponents(AttachmentType.Engine))
                if (comp is EngineComponent eng) total += eng.maxPower;
            return total;
        }

        protected bool IsGrounded()
        {
            if (wheelColliders == null) return false;
            foreach (var wc in wheelColliders)
                if (wc != null && wc.isGrounded) return true;
            return false;
        }

        // ─── VehicleBase abstract implementations ───────────────────────

        public override void ApplyMotorForce(float throttle)
        {
            if (wheelColliders == null || isDriveWheel == null) return;
            if (Fuel.IsEmpty) { ZeroAllMotorTorque(); return; }

            if (Mathf.Abs(throttle) > 0.01f)
                Fuel.Drain(Mathf.Abs(throttle), Time.fixedDeltaTime);

            float torque;

            if (Drivetrain != null && Drivetrain.IsActive)
            {
                // Realistic drivetrain: RPM-based torque curve + gear ratios
                float boost = IsBoosting ? boostMultiplier : 1f;
                torque = Drivetrain.GetWheelTorque(throttle, boost);
            }
            else
            {
                // Legacy flat-power fallback
                float power    = GetTotalEnginePower();
                float speed    = Rb.linearVelocity.magnitude;
                float maxSpeed = baseMaxSpeed * (IsBoosting ? boostMultiplier : 1f);
                float speedLimitFactor = maxSpeed > 0f ? Mathf.Clamp01(1f - speed / maxSpeed) : 0f;
                float effectiveThrottle = throttle * (IsBoosting ? boostMultiplier : 1f);
                torque = effectiveThrottle * power * speedLimitFactor;
            }

            float perWheel = _cachedDriveCount > 0 ? torque / _cachedDriveCount : torque;

            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] == null) continue;
                if (i >= isDriveWheel.Length || !isDriveWheel[i])
                {
                    wheelColliders[i].motorTorque = 0f;
                    continue;
                }
                bool isLeft = wheelColliders[i].transform.localPosition.x < 0f;
                float side  = isLeft ? LeftTorqueFactor : RightTorqueFactor;
                wheelColliders[i].motorTorque = perWheel * side;
            }
        }

        private void ZeroAllMotorTorque()
        {
            if (wheelColliders == null) return;
            foreach (var wc in wheelColliders)
                if (wc != null) wc.motorTorque = 0f;
        }

        public override void ApplySteeringForce(float steer)
        {
            if (wheelColliders == null || isSteerWheel == null) return;

            float speed = Rb.linearVelocity.magnitude;
            float maxSpeed = baseMaxSpeed * (IsBoosting ? boostMultiplier : 1f);

            // Speed-dependent steer reduction: at >70% max speed, steer angle drops to 60%
            float speedFactor = maxSpeed > 0f
                ? Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((speed / maxSpeed - 0.7f) / 0.3f))
                : 1f;

            // Mass factor: heavier = slower turn
            float massSteerFactor = Mathf.Clamp(600f / Rb.mass, 0.5f, 1f);

            float angle = steer * maxSteerAngle * speedFactor * massSteerFactor;
            for (int i = 0; i < wheelColliders.Length && i < isSteerWheel.Length; i++)
            {
                if (wheelColliders[i] == null || !isSteerWheel[i]) continue;
                wheelColliders[i].steerAngle = angle;
            }
        }

        public override void ApplyBrakeForce(float brake)
        {
            if (wheelColliders == null) return;
            float brakeTorque = brake * 3000f;
            foreach (var wc in wheelColliders)
                if (wc != null) wc.brakeTorque = brakeTorque;
        }
    }
}
