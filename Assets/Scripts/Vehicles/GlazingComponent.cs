using UnityEngine;

namespace Voidborne.Vehicles
{
    [CreateAssetMenu(fileName = "NewGlazing", menuName = "Voidborne/Vehicles/Components/Glazing")]
    public class GlazingComponent : VehicleComponent
    {
        [Header("Glazing")]
        public float shatterThreshold = 30f;
        /// <summary>Mesh guards don't shatter — they stay on the vehicle even when the zone is Destroyed.</summary>
        public bool isMesh = false;

        private void Awake() { attachmentType = AttachmentType.Glazing; }
    }
}
