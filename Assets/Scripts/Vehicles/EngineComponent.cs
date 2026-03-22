using UnityEngine;

namespace Voidborne.Vehicles
{
    public enum FuelType { Combustion, Electric }

    [CreateAssetMenu(fileName = "NewEngine", menuName = "Voidborne/Vehicles/Components/Engine")]
    public class EngineComponent : VehicleComponent
    {
        [Header("Engine")]
        public FuelType fuelType = FuelType.Combustion;
        public float maxPower = 100f;
        public float noiseLevel = 50f;
        public bool overheatsOnSustain = false;
        public float overheatTime = 30f;

        [Header("Drivetrain")]
        [Tooltip("Peak torque in Newton-metres")]
        public float peakTorqueNm = 180f;
        [Tooltip("RPM at which peak torque occurs")]
        public float peakTorqueRPM = 3500f;
        [Tooltip("Maximum RPM before rev limiter")]
        public float redlineRPM = 6500f;
        [Tooltip("Idle RPM when not throttling")]
        public float idleRPM = 800f;
        [Tooltip("Per-gear ratios (1st is highest). Leave empty for legacy flat-power model.")]
        public float[] gearRatios = { 3.5f, 2.1f, 1.4f, 1.0f, 0.8f };
        [Tooltip("Differential / final drive ratio")]
        public float finalDriveRatio = 3.7f;
        [Tooltip("Engine braking strength when off-throttle (0-1)")]
        public float engineBrakingFactor = 0.3f;

        /// <summary>True if this engine has drivetrain data configured.</summary>
        public bool HasDrivetrain => gearRatios != null && gearRatios.Length > 0;

        private void Awake() { attachmentType = AttachmentType.Engine; }
    }
}
