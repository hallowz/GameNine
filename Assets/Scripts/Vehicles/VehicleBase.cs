using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Abstract base for all driveable vehicles. Subclasses (WheeledVehicle, RotorVehicle, PushcartVehicle)
    /// implement ApplyMotorForce / ApplySteeringForce / ApplyBrakeForce.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AssembledVehicle))]
    [RequireComponent(typeof(VehicleDamageSystem))]
    [RequireComponent(typeof(VehicleFuel))]
    public abstract class VehicleBase : MonoBehaviour
    {
        // ─── Serialized ─────────────────────────────────────────────────
        [Header("Performance")]
        [SerializeField] protected float baseMaxSpeed = 15f;
        [SerializeField] protected float boostMultiplier = 1.5f;

        [Header("Camera")]
        [SerializeField] private Vector3 driverCameraOffset = new Vector3(0f, 1.2f, -0.4f);

        [Header("Seats")]
        [SerializeField] private Transform[] seatTransforms;

        // ─── Components ─────────────────────────────────────────────────
        protected Rigidbody Rb { get; private set; }
        protected AssembledVehicle Assembly { get; private set; }
        protected VehicleDamageSystem DamageSystem { get; private set; }
        protected VehicleFuel Fuel { get; private set; }
        protected Inventory CargoInventory { get; private set; }

        // ─── Engine state ────────────────────────────────────────────────
        protected EngineComponent InstalledEngine { get; private set; }
        protected float MaxPower => InstalledEngine != null ? InstalledEngine.maxPower : 0f;

        public bool IsBoosting { get; set; }
        public bool IsBraking { get; set; }

        // ─── Passenger tracking ──────────────────────────────────────────
        private readonly List<GameObject> _passengers = new List<GameObject>();
        public int PassengerCount => _passengers.Count;

        public bool IsBeingDriven => _passengers.Count > 0;

        // ─── Body system ───────────────────────────────────────────────────
        protected VehicleBody Body { get; private set; }

        // ─── Overheat ────────────────────────────────────────────────────
        private float _sustainTime;

        // ─── Lifecycle ───────────────────────────────────────────────────
        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Assembly = GetComponent<AssembledVehicle>();
            DamageSystem = GetComponent<VehicleDamageSystem>();
            Fuel = GetComponent<VehicleFuel>();

            // Ensure AssembledVehicle has initialized before we query components
            Assembly.EnsureInitialized();

            // Cache engine
            InstalledEngine = Assembly.GetFirstInstalledComponent<EngineComponent>(AttachmentType.Engine);

            // Build cargo inventory if CargoBed utility is installed
            var utility = Assembly.GetFirstInstalledComponent<UtilityComponent>(AttachmentType.Utility);
            if (utility != null && utility.utilityType == UtilityType.CargoBed)
                CargoInventory = new Inventory(utility.cargoSlots, 1);

            // Apply fuel tank extension
            foreach (var comp in Assembly.GetInstalledComponents(AttachmentType.Utility))
            {
                if (comp is UtilityComponent u && u.utilityType == UtilityType.FuelTankExtension)
                    Fuel.ExtendTank(u.fuelExtension);
            }

            // Body system reference
            Body = GetComponent<VehicleBody>();

            // Mass = frame + components; keep the Rigidbody default if nothing is installed
            float totalWeight = Assembly.TotalWeight;
            if (totalWeight > 0f)
                Rb.mass = totalWeight;

            RecalculateCenterOfMass();
        }

        /// <summary>
        /// Recalculate center of mass based on installed body parts.
        /// Call on Awake and whenever a part is installed/removed.
        /// </summary>
        public void RecalculateCenterOfMass()
        {
            if (Body == null)
            {
                Rb.ResetCenterOfMass();
                return;
            }

            float totalWeight = Assembly.Frame != null ? Assembly.Frame.baseWeight : 100f;
            Vector3 weightedPos = Vector3.zero; // frame center at origin

            foreach (var slot in Body.AllSlots)
            {
                if (slot.IsEmpty) continue;
                float partWeight = slot.InstalledPart.weight;
                weightedPos += slot.transform.localPosition * partWeight;
                totalWeight += partWeight;
            }

            if (totalWeight > 0f)
                Rb.centerOfMass = weightedPos / totalWeight;
            else
                Rb.ResetCenterOfMass();
        }

        protected virtual void FixedUpdate()
        {
            if (!IsBeingDriven) return;
            if (DamageSystem.GetZone(DamageZone.Chassis).IsDestroyed) return;

            // Fuel drain only when there's an engine
            if (InstalledEngine != null)
                Fuel.DrainLeak(Time.fixedDeltaTime);

            // Overheat check
            if (InstalledEngine != null && InstalledEngine.overheatsOnSustain)
            {
                if (Rb.linearVelocity.magnitude > 0.5f)
                    _sustainTime += Time.fixedDeltaTime;
                else
                    _sustainTime = 0f;

                if (_sustainTime >= InstalledEngine.overheatTime)
                    DamageSystem.ApplyDamageToZone(DamageZone.EngineBay, 10f);
            }
        }

        // ─── Abstract driving interface ──────────────────────────────────

        /// <summary>Apply forward/reverse motor force (throttle in range [-1, 1]).</summary>
        public abstract void ApplyMotorForce(float throttle);

        /// <summary>Apply left/right steering input (steer in range [-1, 1]).</summary>
        public abstract void ApplySteeringForce(float steer);

        /// <summary>Apply braking force.</summary>
        public abstract void ApplyBrakeForce(float brake);

        // ─── Seat management ────────────────────────────────────────────

        public bool TryEnter(GameObject passenger)
        {
            int maxSeats = Assembly.Frame != null ? Assembly.Frame.maxSeatCount : 1;
            if (_passengers.Count >= maxSeats) return false;
            _passengers.Add(passenger);
            return true;
        }

        public void Exit(GameObject passenger)
        {
            _passengers.Remove(passenger);
        }

        public Vector3 DriverCameraOffset => driverCameraOffset;

        public Transform GetDriverSeat()
            => seatTransforms != null && seatTransforms.Length > 0 ? seatTransforms[0] : transform;

        public Transform GetExitPoint()
            => seatTransforms != null && seatTransforms.Length > 0 ? seatTransforms[0] : transform;

        // ─── Cargo ──────────────────────────────────────────────────────
        public bool HasCargo => CargoInventory != null;

        public Inventory GetCargoInventory() => CargoInventory;

        // ─── Detection radius (for VORD AI and SignalDampener) ──────────
        public float GetNoiseRadius()
        {
            float noise = InstalledEngine != null ? InstalledEngine.noiseLevel : 0f;
            var dampener = Assembly.GetFirstInstalledComponent<UtilityComponent>(AttachmentType.Utility);
            if (dampener != null && dampener.utilityType == UtilityType.SignalDampener)
                noise *= (1f - dampener.detectionRangeReduction);
            return noise;
        }
    }
}
