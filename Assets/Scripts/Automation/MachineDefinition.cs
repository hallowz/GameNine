using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// High-level grouping of a machine by its in-game role. Drives the
    /// generator's heuristic categorisation and downstream UI hints.
    /// </summary>
    public enum MachineCategory
    {
        Workbench,
        Furnace,
        Forge,
        Apiary,
        Press,
        Assembler,
        Auto,
        Other
    }

    /// <summary>
    /// Volume 2.4 machine definition — content data for every item whose
    /// underlying <c>kind == Machine</c> entry in <c>items.json</c>. Generated
    /// en masse by <c>Assets/Editor/Data/MachineSoGenerator.cs</c>.
    ///
    /// One MachineDefinition exists per machine ID (e.g. <c>workbench</c>,
    /// <c>furnace</c>, <c>assembler</c>). The machine's recipes live on the
    /// existing <see cref="Voidborne.Crafting.RecipeRegistry"/> indexed by
    /// <c>viaMachineId</c>; this SO carries only the machine's grid
    /// dimensions, tier, power profile, and category — i.e. data that
    /// belongs to the machine itself, not to a recipe.
    /// </summary>
    /// <remarks>
    /// The ID is shared with the corresponding ItemDefinition (Volume 2.2);
    /// look the item up via <c>ItemDatabase.GetItem(itemId)</c>.
    ///
    /// Coop note: stateless content data. Per-instance state (input slots,
    /// current recipe, progress) lives on a MonoBehaviour wrapper added in
    /// Volume 9, never on this SO.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewMachine", menuName = "Voidborne/Automation/Machine Definition")]
    public class MachineDefinition : ScriptableObject
    {
        // ----- Identity -----
        [Header("Identity")]
        [Tooltip("Snake_case machine ID, matches the JSON key in items.json and the linked ItemDefinition.itemId.")]
        public string itemId;

        public string displayName;

        // ----- Tier -----
        [Header("Tier")]
        [Tooltip("T1..T7 parsed from the item's 'role' string. 0 when no tier marker is present.")]
        public int tier;

        // ----- Crafting grid -----
        [Header("Crafting Grid")]
        [Tooltip("Width of the input grid for this machine's recipes. Default 3.")]
        public int gridWidth = 3;

        [Tooltip("Height of the input grid for this machine's recipes. Default 3.")]
        public int gridHeight = 3;

        // ----- Power -----
        [Header("Power")]
        [Tooltip("True if the machine requires power to operate (T4+ generally, or 'powered' in role).")]
        public bool needsPower;

        [Tooltip("Steady-state power draw in watts when running. 0 if !needsPower.")]
        public int powerDrawWatts;

        // ----- Automation -----
        [Header("Automation")]
        [Tooltip("True if this is an auto-* variant (pulls inputs / pushes outputs automatically).")]
        public bool isAutomatable;

        // ----- Category -----
        [Header("Category")]
        public MachineCategory category = MachineCategory.Other;
    }
}
