using UnityEngine;

/// <summary>
/// Tool tier determines which ores the tool can mine.
/// Each tier unlocks the ability to mine ores up to that tier level.
/// </summary>
public enum ToolTier
{
    Wood     = 0, // Can mine stone/dirt only — no ores
    Stone    = 1, // Can mine iron ore (tier 1) and below
    Iron     = 2, // Can mine gold ore (tier 2) and below
    Titanium = 3, // Can mine titanium ore (tier 3) and below
    Void     = 4  // Can mine any ore
}

/// <summary>
/// Type of tool — determines which actions the tool applies to.
/// </summary>
public enum ToolType
{
    Pickaxe,
    Axe,
    Shovel
}

/// <summary>
/// ScriptableObject defining a mining tool.
/// Extends ItemDefinition with tool-specific fields.
/// Set itemType = Tool on the base item fields.
/// </summary>
[CreateAssetMenu(fileName = "NewTool", menuName = "Voidborne/Items/Tool Definition")]
public class ToolDefinition : ItemDefinition
{
    [Header("Tool Properties")]
    [Tooltip("The category of tool — Pickaxe mines terrain, Axe chops trees, Shovel digs soft terrain.")]
    public ToolType toolType = ToolType.Pickaxe;

    [Tooltip("The tier of this tool. Higher tiers can mine harder ores and mine faster.")]
    public ToolTier toolTier = ToolTier.Wood;

    [Tooltip("Multiplier applied to the base mining speed. 1.0 = normal speed.")]
    public float miningSpeedMultiplier = 1f;

    [Tooltip("Maximum number of uses before this tool breaks.")]
    public int maxDurability = 60;

    /// <summary>
    /// Returns the integer tier requirement of this tool.
    /// Corresponds to ToolTier enum values (Wood=0, Stone=1, Iron=2, Titanium=3, Void=4).
    /// An ore can be mined if its required tier <= this tool's tier value.
    /// </summary>
    public int GetMiningTierRequirement()
    {
        return (int)toolTier;
    }
}
