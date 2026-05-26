using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour. Central authority for quest state.
///
/// Other systems call UpdateObjective() to advance progress; QuestEventBridge wires
/// most of these calls automatically. Some objectives (PlaceBuilding, InteractWith,
/// CraftItem) may also call QuestManager.Instance.UpdateObjective() directly.
/// </summary>
public class QuestManager : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Singleton
    // ---------------------------------------------------------------

    public static QuestManager Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Events
    // ---------------------------------------------------------------

    /// <summary>Fired when a quest enters the active list.</summary>
    public static event Action<QuestDefinition>       OnQuestAccepted;

    /// <summary>Fired when any objective count changes. Args: definition, objective index.</summary>
    public static event Action<QuestDefinition, int>  OnObjectiveUpdated;

    /// <summary>Fired when all objectives are complete and rewards are distributed.</summary>
    public static event Action<QuestDefinition>       OnQuestCompleted;

    // ---------------------------------------------------------------
    //  Inspector — register all known quests here
    // ---------------------------------------------------------------

    [SerializeField] private List<QuestDefinition> allQuests = new();

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------

    private readonly HashSet<string>                   _completedIds = new();
    private readonly Dictionary<string, QuestInstance> _active       = new();

    // ---------------------------------------------------------------
    //  Properties
    // ---------------------------------------------------------------

    public IReadOnlyDictionary<string, QuestInstance> ActiveQuests => _active;
    public IReadOnlyCollection<string>                CompletedIds => _completedIds;
    public IReadOnlyList<QuestDefinition>             AllQuests    => allQuests;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>Returns true if the player can currently accept this quest.</summary>
    public bool CanAccept(QuestDefinition def)
    {
        if (def == null) return false;
        if (_active.ContainsKey(def.questId)) return false;
        if (_completedIds.Contains(def.questId) && !def.isRepeatable) return false;
        foreach (string pre in def.prerequisiteQuestIds)
            if (!_completedIds.Contains(pre)) return false;
        return true;
    }

    /// <summary>Accept a quest. Returns false if prerequisites are not met or it is already active.</summary>
    public bool AcceptQuest(QuestDefinition def)
    {
        if (!CanAccept(def)) return false;
        var inst = new QuestInstance(def);
        _active[def.questId] = inst;
        OnQuestAccepted?.Invoke(def);
        Debug.Log($"[QuestManager] Accepted: {def.questName}");
        return true;
    }

    /// <summary>
    /// Advance matching objectives across all active quests.
    /// Supply only the filter fields relevant to the objective type;
    /// null/empty fields match any value.
    /// </summary>
    public void UpdateObjective(
        ObjectiveType   type,
        int             amount                  = 1,
        ItemDefinition  itemDef                 = null,
        Voidborne.Enemies.EnemyDefinition enemyDef = null,
        string          locationOrInteractableId = null,
        int             indexModuleIndex        = -1)
    {
        foreach (var kvp in _active)
        {
            var inst = kvp.Value;
            for (int i = 0; i < inst.Objectives.Length; i++)
            {
                var obj = inst.Objectives[i];
                if (obj.isCompleted || obj.type != type) continue;

                bool match = type switch
                {
                    ObjectiveType.KillEnemy          => enemyDef == null || obj.targetEnemy == null || obj.targetEnemy == enemyDef,
                    ObjectiveType.CollectItem        => itemDef != null && obj.targetItem == itemDef,
                    ObjectiveType.DeliverItem        => itemDef != null && obj.targetItem == itemDef,
                    ObjectiveType.CraftItem          => itemDef != null && obj.targetItem == itemDef,
                    ObjectiveType.ReachLocation      => string.IsNullOrEmpty(obj.locationId)       || obj.locationId       == locationOrInteractableId,
                    ObjectiveType.PlaceBuilding      => true,
                    ObjectiveType.InteractWith       => string.IsNullOrEmpty(obj.interactableId)   || obj.interactableId   == locationOrInteractableId,
                    ObjectiveType.InstallIndexModule => indexModuleIndex < 0 || obj.indexModuleIndex < 0 || obj.indexModuleIndex == indexModuleIndex,
                    _                                => false
                };

                if (!match) continue;

                obj.Increment(amount);
                OnObjectiveUpdated?.Invoke(inst.Definition, i);
                Debug.Log($"[QuestManager] Objective [{i}] updated: {obj.GetProgressText()}");

                if (CheckAllObjectivesComplete(inst))
                    CompleteQuest(inst.Definition.questId);
            }
        }
    }

    /// <summary>Forcibly complete a quest (all rewards distributed immediately).</summary>
    public void CompleteQuest(string questId)
    {
        if (!_active.TryGetValue(questId, out var inst)) return;
        _active.Remove(questId);
        if (!inst.Definition.isRepeatable)
            _completedIds.Add(questId);
        DistributeRewards(inst.Definition);
        OnQuestCompleted?.Invoke(inst.Definition);
        Debug.Log($"[QuestManager] Completed: {inst.Definition.questName}");
    }

    public bool IsCompleted(string questId) => _completedIds.Contains(questId);

    public bool IsActive(string questId) => _active.ContainsKey(questId);

    /// <summary>Returns all quests the player could accept right now.</summary>
    public List<QuestDefinition> GetAvailableQuests()
    {
        var result = new List<QuestDefinition>();
        foreach (var def in allQuests)
            if (CanAccept(def)) result.Add(def);
        return result;
    }

    /// <summary>Retrieve the live QuestInstance for a quest, or null if not active.</summary>
    public QuestInstance GetActiveInstance(string questId) =>
        _active.TryGetValue(questId, out var inst) ? inst : null;

    // ---------------------------------------------------------------
    //  Internal helpers
    // ---------------------------------------------------------------

    private static bool CheckAllObjectivesComplete(QuestInstance inst)
    {
        foreach (var obj in inst.Objectives)
            if (!obj.isCompleted) return false;
        return true;
    }

    private static void DistributeRewards(QuestDefinition def)
    {
        PlayerInventory playerInv = FindFirstObjectByType<PlayerInventory>();
        if (playerInv == null) return;

        foreach (var reward in def.rewards)
        {
            if (reward.IsEmpty) continue;
            bool placed = playerInv.AddItem(reward);
            if (!placed)
                Debug.LogWarning($"[QuestManager] Inventory full — reward {reward} dropped.");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  QuestInstance — runtime wrapper that owns cloned, mutable objective state
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Runtime representation of an accepted quest.
/// Objectives are deep-cloned from QuestDefinition so the SO is never mutated.
/// </summary>
public class QuestInstance
{
    public readonly QuestDefinition Definition;
    public readonly QuestObjective[] Objectives;

    public QuestInstance(QuestDefinition def)
    {
        Definition = def;
        Objectives = new QuestObjective[def.objectives.Count];
        for (int i = 0; i < def.objectives.Count; i++)
        {
            var src = def.objectives[i];
            Objectives[i] = new QuestObjective
            {
                type              = src.type,
                description       = src.description,
                targetCount       = src.targetCount,
                targetEnemy       = src.targetEnemy,
                targetItem        = src.targetItem,
                locationId        = src.locationId,
                interactableId    = src.interactableId,
                indexModuleIndex = src.indexModuleIndex,
                // currentCount and isCompleted start at default (0 / false)
            };
        }
    }
}
