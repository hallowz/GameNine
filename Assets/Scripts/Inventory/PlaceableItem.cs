using UnityEngine;

/// <summary>
/// An ItemDefinition that represents something the player can place freely in the world
/// (vehicles, workbenches, etc.). When selected on the hotbar, PlaceableItemHandler
/// activates PlaceablePlacementController with this item.
/// Set itemType = Machine for proper routing and tooltip colour.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Items/Placeable Item", fileName = "NewPlaceableItem")]
public class PlaceableItem : ItemDefinition
{
    [Header("Placement")]
    [Tooltip("Prefab spawned in the world when placed.")]
    public GameObject prefabToPlace;

    [Tooltip("Short category label shown in the tooltip (e.g. 'Vehicle', 'Station').")]
    public string categoryLabel = "Placeable";
}
