using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// ItemDefinition subclass for vehicle body parts. Each instance is a unique part
    /// with its own condition — never stacks.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVehiclePart", menuName = "Voidborne/Vehicles/Vehicle Part Item")]
    public class VehiclePartItem : ItemDefinition
    {
        [Header("Vehicle Part")]
        public AttachmentType partType;
        public BodySection fitsSection;
        public string slotId;
        public float maxCondition = 100f;

        [Header("Part Visuals")]
        [Tooltip("Model shown when installed on vehicle")]
        public GameObject attachedModelPrefab;
        [Tooltip("Model shown when dropped as WorldItem")]
        public GameObject worldModelPrefab;

        [Header("Legacy Compat")]
        [Tooltip("Optional wrapped component for backward compat with old system")]
        public VehicleComponent wrappedComponent;

        void OnEnable()
        {
            itemType = ItemType.VehiclePart;
            maxStackSize = 1;
        }
    }
}
