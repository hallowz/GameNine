using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Base ScriptableObject for all vehicle components that slot into a VehicleFrame attachment point.
    /// </summary>
    public abstract class VehicleComponent : ScriptableObject
    {
        [Header("Identity")]
        public string componentName;
        public AttachmentType attachmentType;

        [Header("Properties")]
        public float weight = 20f;

        [Header("Crafting")]
        public ItemDefinition[] craftingIngredients;
        public int[] craftingAmounts;

        [Header("Visuals")]
        public GameObject modelPrefab;

        /// <summary>Total weight contribution to the vehicle.</summary>
        public virtual float TotalWeight => weight;
    }
}
