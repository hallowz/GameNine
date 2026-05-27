// LEGACY — do not use. Asset wipe + replacement happens in Volume 2/5.
using UnityEngine;

/// <summary>
/// Legacy v1 ItemDefinition preserved for reference only.
/// Renamed from <c>ItemDefinition</c> to <c>LegacyItemDefinition</c> so the new
/// V2.2 class (under <c>Assets/Scripts/Inventory/</c>) owns the
/// <c>ItemDefinition</c> identifier project-wide. The <see cref="ItemType"/>
/// enum that used to live here now lives in the new ItemDefinition.cs so all
/// existing call sites continue to compile.
/// </summary>
public class LegacyItemDefinition : ScriptableObject
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
