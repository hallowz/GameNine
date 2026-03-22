using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Item that activates NetworkCableController when held on the hotbar.
    /// Click a network device (DriveRack, Terminal, etc.), then another to connect.
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Automation/Network Cable Item", fileName = "NetworkCableItem")]
    public class NetworkCableItem : ItemDefinition
    {
        [Header("Cable")]
        public float maxLength = 30f;
    }
}
