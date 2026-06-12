using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Data;

// -----------------------------------------------------------------------------
// V2.2 ItemDefinition
// -----------------------------------------------------------------------------
// This file REPLACES the legacy ItemDefinition that lived here before
// 2026-05-26. The legacy class is preserved under Assets/Scripts/_Legacy/Inventory/
// as LegacyItemDefinition for reference; existing project code (BackpackItem,
// PlayerInventory, WorldItem, etc.) continues to bind against THIS class via
// compatibility shim fields/properties (itemId, modelPrefab, itemType, weight).
//
// Coop note: ItemDefinitions are stateless content data. Runtime state lives on
// MonoBehaviours / NetworkBehaviours, never on the SO.
// -----------------------------------------------------------------------------

/// <summary>
/// Classification of an item per the V3 design (Voidborne flowchart).
/// Mirrors <c>kind</c> in <c>Design Documents/GameDesign/data/items.json</c>.
/// </summary>
public enum ItemKind
{
    Source,
    Machine,
    Component,
    Product
}

/// <summary>
/// Source archetype for <see cref="ItemKind.Source"/> items.
/// Mirrors <c>src</c> in items.json. Non-source items use <see cref="None"/>.
/// </summary>
public enum ItemSource
{
    None,
    Fauna,
    Flora,
    Ore,
    Soil,
    Exotic
}

/// <summary>
/// Legacy v1 classification preserved for compatibility with existing code
/// that switches on <c>ItemDefinition.itemType</c> (TooltipUI, ItemIconGenerator,
/// WorldItem, ChestSetup, …). New code should use <see cref="ItemKind"/> and
/// <see cref="ItemDefinition.categories"/> instead.
/// </summary>
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

/// <summary>
/// V2.2 item definition — content data for every item in the game.
/// Generated en masse by <c>Assets/Editor/Data/ItemSoGenerator.cs</c> from
/// <c>Design Documents/GameDesign/data/items.json</c> + <c>categories.json</c>.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Voidborne/Items/Item Definition (v2)")]
public class ItemDefinition : ScriptableObject
{
    // ----- Identity -----
    [Header("Identity")]
    [Tooltip("Snake_case item ID, matches the JSON key in items.json.")]
    public string itemId;

    public string displayName;

    [TextArea(2, 5)]
    [Tooltip("Free-form description; usually sourced from JSON 'role'.")]
    public string description;

    // ----- Classification -----
    [Header("Classification")]
    public ItemKind kind;
    public ItemSource source;

    [Tooltip("Categories this item belongs to (from categories.json). Examples: food, power, weapon, build.")]
    public string[] categories;

    [Tooltip("Legacy v1 classification. Derived by the generator from kind+categories; some scripts (TooltipUI, ItemIconGenerator, WorldItem) still switch on it.")]
    public ItemType itemType;

    // ----- Build / Deco flags -----
    [Header("Build/Deco flags")]
    public bool isBuildBlock;
    public bool isDeco;
    [Tooltip("Build material color key from JSON (e.g. 'build', 'stone'). Empty if not a build block.")]
    public string buildColor;

    // ----- Inventory -----
    [Header("Inventory")]
    [Tooltip("Auto-assigned by the generator: 1 for machines/tools/weapons/vehicles, 16 for sources, 64 for components/products.")]
    public int maxStackSize = 64;

    [Tooltip("Legacy compatibility field. Default 1f; kept so existing tooltip/UI code continues to compile.")]
    public float weight = 1f;

    // ----- Visuals (Volume 3 owns these) -----
    [Header("Visuals (assigned by Volume 3 pipeline)")]
    public Sprite icon;

    [Tooltip("World-drop prefab spawned when this item is dropped on the ground. Assigned by Volume 3.")]
    public GameObject modelPrefab;

    [Tooltip("Placed prefab (for blocks/machines) — used by the building/automation systems. Assigned by Volume 3.")]
    public GameObject placedPrefab;

    // ----- V3 schema additions (Volume 2.6) -----
    [Header("Material Properties (V2.6)")]
    [Tooltip("Material property tags driving the forgiving/picky crafting match engine (Volume 6.1). Machines typically have an empty array; their behaviour is governed by MachineDefinition.processType instead.")]
    public MaterialProperties[] properties;

    // -------------------------------------------------------------------------
    // Compatibility shims — read-only aliases used by callers written against
    // the V2.2 spec field names. Underlying serialized fields keep the legacy
    // names so existing setup scripts (BuildingItemSetup, AutomationSetupNN,
    // ChestSetup, …) that ASSIGN to itemId / modelPrefab keep compiling.
    // -------------------------------------------------------------------------

    /// <summary>V2.2 alias for <see cref="itemId"/>.</summary>
    public string id => itemId;

    /// <summary>V2.2 alias for <see cref="modelPrefab"/>.</summary>
    public GameObject worldDropPrefab => modelPrefab;

    /// <summary>Returns true if <see cref="categories"/> contains the given key.</summary>
    public bool HasCategory(string cat)
    {
        if (categories == null || string.IsNullOrEmpty(cat)) return false;
        return Array.IndexOf(categories, cat) >= 0;
    }

    /// <summary>
    /// Read-only view over the item's material-property tags (V2.6). Never null;
    /// returns an empty list when no properties are assigned.
    /// </summary>
    public IReadOnlyList<MaterialProperties> Properties
        => properties != null ? (IReadOnlyList<MaterialProperties>)properties : Array.Empty<MaterialProperties>();

    /// <summary>Returns true if <see cref="properties"/> contains the given tag.</summary>
    public bool HasProperty(MaterialProperties p)
    {
        if (properties == null) return false;
        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i] == p) return true;
        }
        return false;
    }
}
