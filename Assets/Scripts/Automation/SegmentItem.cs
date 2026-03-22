using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// An AutomationItem placed via wire-style click-to-click placement (like Rust's Wire Tool).
    /// Used for conveyor belts, pneumatic tubes, and other segment-based transport.
    ///
    /// Placement:
    ///   1. Left-click an output AutomationConnector → start point
    ///   2. Preview line stretches from start to cursor
    ///   3. Left-click an input AutomationConnector → end point
    ///   4. Segment prefab spawns between the two ports, scaled to fit
    ///   5. Right-click cancels mid-placement
    ///   6. X key cuts/removes an existing segment
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Automation/Segment Item",
                     fileName = "NewSegmentItem")]
    public class SegmentItem : AutomationItem
    {
        [Header("Segment")]
        [Tooltip("Maximum distance between the two endpoints (meters).")]
        public float maxLength = 10f;
    }
}
