using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines a quest: metadata, prerequisites, objectives, and rewards.
/// Create via Assets > Create > Voidborne > Quest > Quest Definition.
/// </summary>
[CreateAssetMenu(menuName = "Voidborne/Quest/Quest Definition", fileName = "NewQuest")]
public class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    public string questId;
    public string questName;
    [TextArea(2, 5)]
    public string description;
    public string questGiverNpcId;

    [Header("Prerequisites")]
    [Tooltip("This quest cannot be accepted until all listed quest IDs are completed.")]
    public List<string> prerequisiteQuestIds = new();

    [Header("Objectives")]
    public List<QuestObjective> objectives = new();

    [Header("Rewards")]
    public List<ItemStack> rewards = new();

    [Header("Flags")]
    public bool isMainStory;
    public bool isRepeatable;
}
