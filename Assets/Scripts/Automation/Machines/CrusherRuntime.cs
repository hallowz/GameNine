using UnityEngine;
using Voidborne.Power;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Crusher (Forgiving_Mechanical_Crush). Powered T4 machine -- the
    /// V6.3 station auto-attaches a <see cref="PowerConsumerNode"/> in Init
    /// because the machine def's <c>needsPower=true</c>, so the base
    /// MachineRuntime's PowerConsumer resolver picks it up automatically.
    ///
    /// <para>This subclass exists for two reasons: (1) a marker so the
    /// BlockPlacer's "what runtime should I attach?" switch can pick a
    /// strongly-typed component, and (2) a sanity-check getter
    /// (<see cref="HasPowerNode"/>) tests can assert against.</para>
    /// </summary>
    public class CrusherRuntime : MachineRuntime
    {
        /// <summary>True if a sibling PowerConsumerNode is attached (Crusher needs power).</summary>
        public bool HasPowerNode => PowerConsumer != null;
    }
}
