using UnityEngine;

/// <summary>
/// Sits on the Player. Watches the active hotbar slot and activates / deactivates
/// PlaceablePlacementController whenever a PlaceableItem is selected or deselected.
/// </summary>
public class PlaceableItemHandler : MonoBehaviour
{
    [SerializeField] private PlayerInventory              playerInventory;
    [SerializeField] private PlaceablePlacementController placementController;

    private PlaceableItem _lastActive;

    private void Start()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        if (placementController == null)
            placementController = FindFirstObjectByType<PlaceablePlacementController>();
    }

    private void Update()
    {
        if (playerInventory == null || placementController == null) return;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

        ItemStack active = playerInventory.ActiveHotbarItem;
        PlaceableItem pi = active.IsEmpty ? null : active.item as PlaceableItem;

        if (pi == _lastActive) return;
        _lastActive = pi;

        if (pi != null && pi.prefabToPlace != null)
            placementController.Activate(pi);
        else
            placementController.Deactivate();
    }
}
