using UnityEngine;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Furnace (Forgiving_Thermal_DryBurn). Same shape as Campfire but
    /// contained -- no external heat aura. Recipes (iron_ore -> iron_ingot,
    /// copper_ore -> copper_ingot, etc) come from RecipeRegistry filtered by
    /// viaMachineId == "furnace". The V6.3 station owns the fuel slot +
    /// burns it per recipe.
    /// </summary>
    public class FurnaceRuntime : MachineRuntime
    {
    }
}
