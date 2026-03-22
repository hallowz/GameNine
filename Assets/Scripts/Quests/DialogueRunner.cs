using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour.
/// Walks through a DialogueTree, evaluating conditions and firing actions.
/// DialogueUI subscribes to its events and displays the result.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Singleton
    // ---------------------------------------------------------------

    public static DialogueRunner Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Events
    // ---------------------------------------------------------------

    /// <summary>Fired when a dialogue session starts. Args: node, visible-choice indices.</summary>
    public event Action<DialogueNode, List<int>> OnNodePresented;

    /// <summary>Fired when dialogue ends (no more nodes or player exited).</summary>
    public event Action OnDialogueEnded;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------

    private DialogueTree _tree;
    private string       _npcId;
    private GameObject   _interactor;
    private DialogueNode _currentNode;

    public bool IsRunning => _currentNode != null;

    public NPCDefinition CurrentNPC { get; private set; }

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>Begin a dialogue session.</summary>
    public void StartDialogue(NPCDefinition npc, GameObject interactor)
    {
        if (npc == null || npc.defaultDialogue == null)
        {
            Debug.LogWarning("[DialogueRunner] StartDialogue: null tree.");
            return;
        }

        CurrentNPC  = npc;
        _tree       = npc.defaultDialogue;
        _npcId      = npc.npcId;
        _interactor = interactor;

        PresentNode(_tree.startNodeId);
    }

    /// <summary>Player selects a visible choice by its visible list index.</summary>
    public void SelectChoice(int visibleIndex)
    {
        if (_currentNode == null) return;

        List<int> visible = GetVisibleChoiceIndices(_currentNode);
        if (visibleIndex < 0 || visibleIndex >= visible.Count) return;

        int realIndex  = visible[visibleIndex];
        DialogueChoice choice = _currentNode.choices[realIndex];

        // Execute any action attached to this choice
        choice.action?.Execute(_interactor);

        // Navigate to next node or end
        if (string.IsNullOrEmpty(choice.nextNodeId))
            EndDialogue();
        else
            PresentNode(choice.nextNodeId);
    }

    /// <summary>Skip typewriter or exit if text is already fully shown (handled in UI).</summary>
    public void RequestSkip()
    {
        // DialogueUI handles typewriter skip; this is a hook for no-choices nodes
        if (_currentNode != null && _currentNode.choices.Count == 0)
            EndDialogue();
    }

    /// <summary>Force-close the dialogue (ESC).</summary>
    public void EndDialogue()
    {
        _currentNode = null;
        CurrentNPC   = null;
        OnDialogueEnded?.Invoke();
    }

    // ---------------------------------------------------------------
    //  Internal
    // ---------------------------------------------------------------

    private void PresentNode(string nodeId)
    {
        DialogueNode node = _tree.GetNode(nodeId);
        if (node == null)
        {
            Debug.LogWarning($"[DialogueRunner] Node '{nodeId}' not found in tree '{_tree.name}'.");
            EndDialogue();
            return;
        }

        _currentNode = node;
        List<int> visible = GetVisibleChoiceIndices(node);
        OnNodePresented?.Invoke(node, visible);
    }

    /// <summary>Returns indices of choices whose conditions pass.</summary>
    private static List<int> GetVisibleChoiceIndices(DialogueNode node)
    {
        var result = new List<int>();
        for (int i = 0; i < node.choices.Count; i++)
        {
            DialogueChoice c = node.choices[i];
            if (c.condition == null || c.condition.type == DialogueCondition.ConditionType.None
                || c.condition.Evaluate())
            {
                result.Add(i);
            }
        }
        return result;
    }
}
