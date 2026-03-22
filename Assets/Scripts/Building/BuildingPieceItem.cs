using UnityEngine;
using Voidborne.Building;

/// <summary>
/// An ItemDefinition that represents a building piece in the player's inventory.
/// When selected on the hotbar, BuildingItemHandler activates build mode.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Building/Building Piece Item", fileName = "NewBuildingPieceItem")]
public class BuildingPieceItem : ItemDefinition
{
    [Tooltip("The BuildingPieceData ScriptableObject this item places in the world.")]
    public BuildingPieceData pieceData;

    [Tooltip("Material tier for this item variant.")]
    public MaterialTier materialTier = MaterialTier.Wood;
}
