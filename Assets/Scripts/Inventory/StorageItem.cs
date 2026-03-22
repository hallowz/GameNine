using UnityEngine;

/// <summary>
/// ItemDefinition for placeable storage containers (Wood Chest, Iron Chest, Compression Chest).
/// When selected on the hotbar, uses the PlaceableItemHandler to place the chest in the world.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Items/Storage Item", fileName = "NewStorageItem")]
public class StorageItem : PlaceableItem
{
    [Header("Chest Stats")]
    [Tooltip("Number of columns in the chest grid.")]
    public int cols = 9;

    [Tooltip("Number of rows in the chest grid.")]
    public int rows = 3;

    [Tooltip("Stack size multiplier. 1 = normal, 4 = compression (64→256).")]
    public int stackMultiplier = 1;

    [Tooltip("Power draw in watts. 0 = unpowered.")]
    public float powerDrawWatts = 0f;

    /// <summary>Total slot count.</summary>
    public int SlotCount => cols * rows;
}
