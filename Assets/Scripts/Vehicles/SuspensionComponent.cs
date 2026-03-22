using UnityEngine;

namespace Voidborne.Vehicles
{
    [CreateAssetMenu(fileName = "NewSuspension", menuName = "Voidborne/Vehicles/Components/Suspension")]
    public class SuspensionComponent : VehicleComponent
    {
        [Header("Suspension")]
        [Tooltip("No longer used directly — spring is auto-calculated from vehicle mass.")]
        public float springStrength = 35000f;
        [Tooltip("No longer used directly — damper is auto-calculated (critical damping).")]
        public float damper = 4500f;
        public float suspensionDistance = 0.3f;
        public float terrainHandlingBonus = 0f;

        private void Awake() { attachmentType = AttachmentType.Suspension; }
    }
}
