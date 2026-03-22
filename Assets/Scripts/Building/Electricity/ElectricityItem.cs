using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// An ItemDefinition that represents a placeable electricity device
    /// (generator, battery, light, door, junction box, etc.).
    /// When selected on the hotbar ElectricityItemHandler activates
    /// ElectricityPlacementController with this item.
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Electricity/Electricity Item",
                     fileName = "NewElectricityItem")]
    public class ElectricityItem : ItemDefinition
    {
        [Header("Device")]
        [Tooltip("Prefab to spawn when placed. Must have a PowerNode component.")]
        public GameObject devicePrefab;

        [Header("Power Stats (for tooltip)")]
        public float outputWatts;
        public float drawWatts;

        [Tooltip("Grid snap size override. 0 = use BuildingManager.GridSize.")]
        public float snapSize = 0f;

        public float EffectiveSnapSize =>
            snapSize > 0f ? snapSize : Voidborne.Building.BuildingManager.GridSize;
    }
}
