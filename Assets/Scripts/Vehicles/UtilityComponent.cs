using UnityEngine;

namespace Voidborne.Vehicles
{
    public enum UtilityType
    {
        CargoBed,
        FuelTankExtension,
        WeaponMount,
        RepairKitRack,
        TerrainLampArray,
        SignalDampener
    }

    [CreateAssetMenu(fileName = "NewUtility", menuName = "Voidborne/Vehicles/Components/Utility")]
    public class UtilityComponent : VehicleComponent
    {
        [Header("Utility")]
        public UtilityType utilityType;

        [Header("Cargo Bed")]
        public int cargoSlots = 9;

        [Header("Fuel Tank Extension")]
        public float fuelExtension = 50f;

        [Header("Signal Dampener")]
        public float detectionRangeReduction = 0.4f;

        [Header("Lamp Array")]
        public float lampRadius = 20f;

        private void Awake() { attachmentType = AttachmentType.Utility; }
    }
}
