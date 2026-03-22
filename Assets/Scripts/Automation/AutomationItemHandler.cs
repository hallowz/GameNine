using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Sits on the Player. Watches the active hotbar slot each frame and routes to
    /// the correct automation placement mode:
    ///   • NetworkCableItem → NetworkCableController (wire-style click-to-click for data cables)
    ///   • SegmentItem      → SegmentPlacementController (wire-style click-to-click for belts/tubes)
    ///   • AutomationItem   → AutomationPlacementController (ghost preview + place)
    ///   • anything else    → deactivate all
    /// </summary>
    public class AutomationItemHandler : MonoBehaviour
    {
        [SerializeField] private PlayerInventory               playerInventory;
        [SerializeField] private AutomationPlacementController placementController;
        [SerializeField] private SegmentPlacementController    segmentController;
        [SerializeField] private NetworkCableController        networkCableController;

        private ItemDefinition _lastActive;

        private void Start()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            if (placementController == null)
                placementController = FindObjectOfType<AutomationPlacementController>();

            if (segmentController == null)
                segmentController = FindObjectOfType<SegmentPlacementController>();

            if (networkCableController == null)
                networkCableController = FindObjectOfType<NetworkCableController>();

            DeactivateAll();
        }

        private void Update()
        {
            if (playerInventory == null) return;

            // Don't switch modes while any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            {
                DeactivateAll();
                _lastActive = null;
                return;
            }

            ItemDefinition active = playerInventory.ActiveHotbarItem.item;
            if (active == _lastActive) return;
            _lastActive = active;

            DeactivateAll();

            // NetworkCableItem must be checked before AutomationItem.
            if (active is NetworkCableItem nci)
            {
                if (networkCableController != null)
                {
                    networkCableController.SetMaxLength(nci.maxLength);
                    networkCableController.enabled = true;
                }
            }
            // SegmentItem check must come before AutomationItem (SegmentItem extends AutomationItem).
            else if (active is SegmentItem si)
            {
                if (segmentController != null) segmentController.Activate(si);
            }
            else if (active is AutomationItem ai)
            {
                if (placementController != null) placementController.Activate(ai);
            }
        }

        private void DeactivateAll()
        {
            placementController?.Deactivate();
            segmentController?.Deactivate();
            if (networkCableController != null) networkCableController.enabled = false;
        }
    }
}
