using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Rotor component that slots into a RotorFrame's Rotor attachment point.
    /// Provides lift, stall threshold, and altitude soft ceiling.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRotor", menuName = "Voidborne/Vehicles/Components/Rotor")]
    public class RotorComponent : VehicleComponent
    {
        [Header("Rotor")]
        [Tooltip("Maximum upward force contributed at full throttle, sea level.")]
        public float liftForce = 800f;

        [Tooltip("Minimum airspeed (m/s) before this rotor stalls and loses lift.")]
        public float minAirspeed = 3f;

        [Tooltip("Altitude (world Y) above which lift degrades linearly to zero at maxAltitude + 50.")]
        public float maxAltitude = 200f;

        [Header("Rotor Visual")]
        public GameObject rotorVisualPrefab;

        private void Awake() { attachmentType = AttachmentType.Rotor; }
    }
}
