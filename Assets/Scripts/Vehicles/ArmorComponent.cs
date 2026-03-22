using UnityEngine;

namespace Voidborne.Vehicles
{
    [CreateAssetMenu(fileName = "NewArmor", menuName = "Voidborne/Vehicles/Components/Armor")]
    public class ArmorComponent : VehicleComponent
    {
        [Header("Armor")]
        public float damageResistancePerHit = 5f;
        public float additionalWeight = 30f;

        public override float TotalWeight => weight + additionalWeight;

        private void Awake() { attachmentType = AttachmentType.Armor; }
    }
}
