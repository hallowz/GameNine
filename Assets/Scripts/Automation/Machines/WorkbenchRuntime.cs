using UnityEngine;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Workbench (Hybrid_Crafting). The base T1 crafting station.
    /// No special behaviour beyond the base <see cref="MachineRuntime"/>:
    /// the player puts ingredients in, picks a recipe in the Machine UI,
    /// and the recipe runs.
    ///
    /// Also serves as the safe default subclass when
    /// <see cref="Voidborne.Building.BlockPlacer.AttachMachineStation"/>
    /// can't find a specialised runtime for an unknown machine id.
    /// </summary>
    public class WorkbenchRuntime : MachineRuntime
    {
    }
}
