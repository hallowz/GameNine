using System;
using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════
//  DialogueTree  —  ScriptableObject that stores all nodes
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// A complete branching dialogue. Authored per NPC conversation.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueTree", menuName = "Voidborne/Quests/DialogueTree")]
public class DialogueTree : ScriptableObject
{
    public string startNodeId = "root";
    public List<DialogueNode> nodes = new();

    public DialogueNode GetNode(string id)
    {
        foreach (var n in nodes)
            if (n.nodeId == id) return n;
        return null;
    }
}

// ═══════════════════════════════════════════════════════════════
//  DialogueNode
// ═══════════════════════════════════════════════════════════════

[Serializable]
public class DialogueNode
{
    public string nodeId;
    public string speakerName;   // overrides NPC name when set
    [TextArea(3, 8)]
    public string text;
    public List<DialogueChoice> choices = new();
}

// ═══════════════════════════════════════════════════════════════
//  DialogueChoice
// ═══════════════════════════════════════════════════════════════

[Serializable]
public class DialogueChoice
{
    [TextArea(1, 2)]
    public string text;

    /// <summary>NodeId to move to. Leave blank to end the dialogue.</summary>
    public string nextNodeId;

    public DialogueCondition condition;
    public DialogueAction    action;
}

// ═══════════════════════════════════════════════════════════════
//  DialogueCondition
// ═══════════════════════════════════════════════════════════════

[Serializable]
public class DialogueCondition
{
    public enum ConditionType
    {
        None,
        QuestComplete,
        QuestActive,
        HasItem,
        IndexModuleInstalled,
        HasIndexDevice,
        HasItem_Any,   // player has any item flagged as "archive fragment"
    }

    public ConditionType   type        = ConditionType.None;
    public string          questId;
    public ItemDefinition  item;
    public int             indexModuleIndex = -1;
    public bool            invert;           // true = NOT this condition

    /// <summary>Evaluate against current game state.</summary>
    public bool Evaluate()
    {
        bool result = EvaluateInner();
        return invert ? !result : result;
    }

    private bool EvaluateInner()
    {
        switch (type)
        {
            case ConditionType.None:
                return true;

            case ConditionType.QuestComplete:
                return QuestManager.Instance != null &&
                       QuestManager.Instance.IsCompleted(questId);

            case ConditionType.QuestActive:
                return QuestManager.Instance != null &&
                       QuestManager.Instance.IsActive(questId);

            case ConditionType.HasItem:
            {
                if (item == null) return false;
                PlayerInventory inv = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                if (inv == null) return false;
                return inv.HasItem(item);
            }

            case ConditionType.IndexModuleInstalled:
            {
                IndexDevice dev = UnityEngine.Object.FindFirstObjectByType<IndexDevice>();
                if (dev == null || indexModuleIndex < 0) return false;
                return dev.IsModuleInstalled(indexModuleIndex);
            }

            case ConditionType.HasIndexDevice:
            {
                IndexDevice dev = UnityEngine.Object.FindFirstObjectByType<IndexDevice>();
                return dev != null && dev.HasDevice;
            }

            default:
                return true;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
//  DialogueAction
// ═══════════════════════════════════════════════════════════════

[Serializable]
public class DialogueAction
{
    public enum ActionType
    {
        None,
        AcceptQuest,
        GiveItem,
        OpenShop,
        TriggerWorldEvent,
        UnlockSchematic,    // adds a recipe / schematic item to player
    }

    public ActionType      type = ActionType.None;
    public QuestDefinition quest;
    public ItemStack       giveItem;
    public string          worldEventId;
    public ItemDefinition  schematicItem;

    /// <summary>Execute the action. Interactor is the player's root GameObject.</summary>
    public void Execute(GameObject interactor)
    {
        switch (type)
        {
            case ActionType.AcceptQuest:
                if (quest != null && QuestManager.Instance != null)
                    QuestManager.Instance.AcceptQuest(quest);
                break;

            case ActionType.GiveItem:
            {
                if (giveItem.IsEmpty) break;
                PlayerInventory inv = interactor?.GetComponentInChildren<PlayerInventory>()
                                   ?? UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                inv?.AddItem(giveItem);
                break;
            }

            case ActionType.OpenShop:
                // Placeholder — ShopUI implemented in future vol
                Debug.Log("[DialogueAction] OpenShop — not yet implemented.");
                break;

            case ActionType.TriggerWorldEvent:
                DialogueEventBus.Raise(worldEventId);
                break;

            case ActionType.UnlockSchematic:
            {
                if (schematicItem == null) break;
                PlayerInventory inv = interactor?.GetComponentInChildren<PlayerInventory>()
                                   ?? UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                inv?.AddItem(new ItemStack(schematicItem, 1));
                break;
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════
//  DialogueEventBus  —  minimal static event channel for world events
// ═══════════════════════════════════════════════════════════════

public static class DialogueEventBus
{
    public static event Action<string> OnWorldEvent;
    public static void Raise(string id) => OnWorldEvent?.Invoke(id);
}
