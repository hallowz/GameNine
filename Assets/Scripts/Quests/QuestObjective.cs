using System;
using UnityEngine;
using Voidborne.Enemies;

public enum ObjectiveType
{
    KillEnemy,
    CollectItem,
    DeliverItem,
    ReachLocation,
    CraftItem,
    PlaceBuilding,
    InteractWith,
    InstallCortexModule
}

/// <summary>
/// Data container for a single quest objective.
/// Stored in QuestDefinition. Runtime state (currentCount, isCompleted) is
/// tracked in QuestInstance (deep-copied at quest accept time) and marked [NonSerialized]
/// so the SO stays clean across sessions.
/// </summary>
[Serializable]
public class QuestObjective
{
    public ObjectiveType type;

    [TextArea(1, 3)]
    public string description;

    public int targetCount = 1;

    // ---------------------------------------------------------------
    //  Filter fields — leave null/empty to match anything of that type
    // ---------------------------------------------------------------

    /// <summary>KillEnemy: which definition to require. Null = any enemy.</summary>
    public EnemyDefinition targetEnemy;

    /// <summary>CollectItem / DeliverItem / CraftItem: required item type.</summary>
    public ItemDefinition targetItem;

    /// <summary>ReachLocation: matches the locationId set on a QuestLocationTrigger.</summary>
    public string locationId;

    /// <summary>InteractWith: matches the interactableId on the triggering object.</summary>
    public string interactableId;

    /// <summary>InstallCortexModule: module index (0–5). -1 = any module.</summary>
    public int cortexModuleIndex = -1;

    // ---------------------------------------------------------------
    //  Runtime state — not persisted on the ScriptableObject
    // ---------------------------------------------------------------

    [NonSerialized] public int  currentCount;
    [NonSerialized] public bool isCompleted;

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    public void Increment(int amount = 1)
    {
        if (isCompleted) return;
        currentCount = Mathf.Min(currentCount + amount, targetCount);
        if (currentCount >= targetCount)
            isCompleted = true;
    }

    /// <summary>Progress string suitable for the tracker HUD.</summary>
    public string GetProgressText() =>
        targetCount > 1 ? $"{description} ({currentCount}/{targetCount})" : description;
}
