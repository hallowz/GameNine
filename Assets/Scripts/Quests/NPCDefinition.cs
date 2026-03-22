using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject describing a named NPC: their identity, dialogue, associated quests,
/// and optional shop inventory.
/// </summary>
[CreateAssetMenu(fileName = "NewNPC", menuName = "Voidborne/Quests/NPCDefinition")]
public class NPCDefinition : ScriptableObject
{
    [Header("Identity")]
    public string     npcId;
    public string     npcName;
    [TextArea(1, 2)]
    public string     role;

    [Header("Visuals")]
    public GameObject     modelPrefab;
    public AnimationClip  idleAnimation;

    [Header("Dialogue")]
    public DialogueTree   defaultDialogue;

    [Header("World")]
    public Vector3 worldPosition;
    public string  safeholdName;   // "Ashfen", "The Delve", "Spire's Rest"

    [Header("Quests")]
    public List<QuestDefinition> associatedQuests = new();

    [Header("Shop (optional)")]
    public List<ItemStack> shopInventory = new();
}
