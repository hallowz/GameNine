using System;
using UnityEngine;
using Voidborne.Enemies;

/// <summary>
/// Bridges game-world events to QuestManager.UpdateObjective().
///
/// Hooks wired automatically:
///   • KillEnemy          — EnemyManager.OnAnyEnemyDied
///   • CollectItem        — PlayerInventory.OnItemAdded (static event)
///   • CraftItem          — QuestEventBridge.OnItemCrafted (raised by UIManager)
///   • InstallIndexModule — QuestEventBridge.OnIndexModuleInstalled (raised by IndexDevice, Vol 10.3)
///   • ReachLocation      — QuestLocationTrigger fires QuestManager.UpdateObjective directly
///   • PlaceBuilding      — BuildingManager calls QuestManager.UpdateObjective directly
///   • InteractWith       — Interactable objects call QuestManager.UpdateObjective directly
///   • DeliverItem        — NPC interactions call QuestManager.UpdateObjective directly
/// </summary>
public class QuestEventBridge : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Static events — external systems raise these
    // ---------------------------------------------------------------

    /// <summary>
    /// Raised by UIManager.OnTakeResultFromStation when a crafted item enters the inventory.
    /// </summary>
    public static event Action<ItemDefinition, int> OnItemCrafted;

    /// <summary>
    /// Raised by IndexDevice (Vol 10.3) when a module is installed.
    /// </summary>
    public static event Action<int> OnIndexModuleInstalled;

    // ---------------------------------------------------------------
    //  Static raise helpers (called by external systems)
    // ---------------------------------------------------------------

    public static void RaiseItemCrafted(ItemDefinition item, int qty)
        => OnItemCrafted?.Invoke(item, qty);

    public static void RaiseIndexModuleInstalled(int moduleIndex)
        => OnIndexModuleInstalled?.Invoke(moduleIndex);

    /// <summary>Alias used by IndexDevice.</summary>
    public static void RaiseModuleInstalled(int moduleIndex)
        => RaiseIndexModuleInstalled(moduleIndex);

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void OnEnable()
    {
        EnemyManager.OnAnyEnemyDied             += HandleEnemyDied;
        PlayerInventory.OnItemAdded          += HandleItemAdded;
        OnItemCrafted                        += HandleItemCrafted;
        OnIndexModuleInstalled              += HandleIndexModuleInstalled;
    }

    private void OnDisable()
    {
        EnemyManager.OnAnyEnemyDied             -= HandleEnemyDied;
        PlayerInventory.OnItemAdded          -= HandleItemAdded;
        OnItemCrafted                        -= HandleItemCrafted;
        OnIndexModuleInstalled              -= HandleIndexModuleInstalled;
    }

    // ---------------------------------------------------------------
    //  Handlers
    // ---------------------------------------------------------------

    private static void HandleEnemyDied(EnemyEntity enemy)
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.KillEnemy,
            amount:   1,
            enemyDef: enemy.Definition);
    }

    private static void HandleItemAdded(ItemDefinition item, int qty)
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.CollectItem,
            amount:  qty,
            itemDef: item);
    }

    private static void HandleItemCrafted(ItemDefinition item, int qty)
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.CraftItem,
            amount:  qty,
            itemDef: item);
        // Crafted items also count as collected
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.CollectItem,
            amount:  qty,
            itemDef: item);
    }

    private static void HandleIndexModuleInstalled(int moduleIndex)
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.InstallIndexModule,
            indexModuleIndex: moduleIndex);
    }
}
