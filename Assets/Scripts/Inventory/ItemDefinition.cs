using UnityEngine;

public enum ItemType
{
    Resource,
    Tool,
    Weapon,
    Armor,
    Consumable,
    Block,
    Machine,
    Backpack,
    VehiclePart
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Voidborne/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;
    [TextArea(2, 5)]
    public string description;

    [Header("Visuals")]
    public Sprite icon;
    public GameObject modelPrefab;

    [Header("Properties")]
    public ItemType itemType;
    public int maxStackSize = 64;
    public float weight = 1f;
}
