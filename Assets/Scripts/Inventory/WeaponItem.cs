using UnityEngine;
using Voidborne.Combat;
using Voidborne.Combat.Melee;
using Voidborne.Combat.Projectiles;

/// <summary>
/// An ItemDefinition that represents a weapon in the player's inventory.
/// Assign exactly one of: gunDefinition, meleeDefinition, bowDefinition, or throwableDefinition.
/// Set itemType = Weapon so PlayerInventory.AddStarterItems() picks it up automatically.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Weapon Item")]
public class WeaponItem : ItemDefinition
{
    [Tooltip("The GunDefinition that this inventory item represents. Assign for firearms; leave null otherwise.")]
    public GunDefinition gunDefinition;

    [Tooltip("The MeleeDefinition that this inventory item represents. Assign for melee weapons; leave null otherwise.")]
    public MeleeDefinition meleeDefinition;

    [Tooltip("The BowDefinition for bows and crossbows. Assign for ranged drawn weapons; leave null otherwise.")]
    public BowDefinition bowDefinition;

    [Tooltip("The ThrowableDefinition for throwing spears, axes, etc. Assign for throwable weapons; leave null otherwise.")]
    public ThrowableDefinition throwableDefinition;
}
