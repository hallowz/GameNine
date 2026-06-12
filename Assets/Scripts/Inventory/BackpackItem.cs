using UnityEngine;

// V5.3 DEPRECATION NOTE — kept in place because PlayerInventory,
// BackpackInstance, BackpackUI, UIManager, TooltipUI, WorldItem and
// DevBackpackItem still consume `BackpackItem` directly. The V11 plan
// is to fold these fields into ItemDefinition (or an ItemKind-driven
// extension) so a single ItemDefinition shape covers backpacks too;
// the asset SOs at Assets/ScriptableObjects/Backpacks/* were archived
// to _Archived/Legacy/Backpacks/ in V5.1, but the runtime class stays
// until V11 refactors the consumers.

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
