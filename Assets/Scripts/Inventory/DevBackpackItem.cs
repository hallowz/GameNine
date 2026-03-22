using UnityEngine;

/// <summary>
/// A special backpack that contains an infinite supply of every item in the game.
/// When equipped or opened, its inventory is automatically filled with every item
/// from the ItemDatabase and never depletes.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Items/Dev Backpack", fileName = "DevBackpack")]
public class DevBackpackItem : BackpackItem
{
    // Marker subclass — BackpackInstance checks for this type
    // and creates an infinite inventory pre-filled with all items.
}
