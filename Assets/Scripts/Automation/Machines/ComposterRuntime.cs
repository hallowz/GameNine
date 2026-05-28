using UnityEngine;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Composter (Forgiving_Organic_Decay). Slow-tick organic processor.
    /// No special behaviour beyond the base station; recipes are routed via
    /// viaMachineId == "composter" in the V6.1 engine. The "slow tick" is
    /// encoded in each recipe's <c>baseSeconds</c> via
    /// <see cref="MachineRecipeTimeProvider"/>, not at the runtime layer.
    /// </summary>
    public class ComposterRuntime : MachineRuntime
    {
    }
}
