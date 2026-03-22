using UnityEngine;

/// <summary>
/// An ItemDefinition for backpack items. When equipped in the player's
/// backpack slot, grants an additional inventory grid of size
/// extraColumns × extraRows.
/// </summary>
[CreateAssetMenu(fileName = "NewBackpack", menuName = "Voidborne/Items/Backpack Item")]
public class BackpackItem : ItemDefinition
{
    [Header("Backpack Storage")]
    [Tooltip("Number of extra inventory rows this backpack provides.")]
    public int extraRows = 3;

    [Tooltip("Number of extra inventory columns this backpack provides.")]
    public int extraColumns = 9;
}
