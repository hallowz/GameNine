using UnityEngine;
using Voidborne.Building;

/// <summary>
/// Sits on the Player. Watches the active hotbar slot and activates / deactivates
/// BuildingManager whenever a BuildingPieceItem is selected or deselected.
/// </summary>
public class BuildingItemHandler : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;

    private BuildingPieceItem _lastActivePiece;

    private void Start()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (playerInventory == null || BuildingManager.Instance == null) return;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

        ItemStack active = playerInventory.ActiveHotbarItem;
        BuildingPieceItem bpi = active.IsEmpty ? null : active.item as BuildingPieceItem;

        if (bpi == _lastActivePiece) return;

        _lastActivePiece = bpi;

        if (bpi != null && bpi.pieceData != null)
            BuildingManager.Instance.EnterBuildMode(bpi.pieceData, bpi.materialTier);
        else
            BuildingManager.Instance.ExitBuildMode();
    }
}
