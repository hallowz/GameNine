using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Vehicles
{
    public enum FrameType { Pushcart, Cycle, Buggy, Hauler, DrillFrame, RotorFrame }

    public enum BodySection { Front, Middle, Back, Undercarriage }

    public enum AttachmentType
    {
        Engine, Suspension, Armor, Glazing, Utility, Rotor, Drill,
        Hood, Bumper, CockpitShell, CockpitDoorLeft, CockpitDoorRight, TrunkDoor, Wheel
    }

    [System.Serializable]
    public class AttachmentPoint
    {
        public string pointName;
        public Vector3 localPosition;
        public AttachmentType attachmentType;
        public BodySection section;
        public string slotId;
        public Vector3 localRotation;
        public bool isRequired;
    }

    [CreateAssetMenu(fileName = "NewVehicleFrame", menuName = "Voidborne/Vehicles/Vehicle Frame")]
    public class VehicleFrame : ScriptableObject
    {
        [Header("Identity")]
        public string frameName;
        public FrameType frameType;
        public int maxSeatCount = 1;

        [Header("Attachment Points")]
        public List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

        [Header("Physics")]
        public float baseWeight = 200f;

        [Header("Visuals")]
        public GameObject modelPrefab;
    }
}
