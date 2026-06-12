using UnityEngine;
using Voidborne.Automation;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.3 — sibling component attached to any placed machine whose
    /// <see cref="MachineDefinition.needsPower"/> is true. Carries the
    /// machine's <see cref="MachineDefinition.powerDrawWatts"/> into the
    /// power graph as a <see cref="PowerNode"/> consumer.
    ///
    /// <para>The companion <see cref="MachineCraftingStation"/> reads the
    /// per-tick <see cref="PowerNode.CurrentWatts"/> via
    /// <see cref="PowerSatisfaction"/> and scales its recipe progress
    /// accordingly -- underpowered machines run slower; fully unpowered
    /// machines stall.</para>
    /// </summary>
    /// <remarks>
    /// V9.1 (MachineRuntime) will own the full power-aware recipe-time
    /// pipeline. V8.3 ships the consumer-node + the
    /// <see cref="PowerSatisfaction"/> getter so MachineCraftingStation
    /// can drop its stubbed always-0 power state and start scaling
    /// progress today.
    /// </remarks>
    [RequireComponent(typeof(MachineCraftingStation))]
    public class PowerConsumerNode : PowerNode
    {
        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private MachineCraftingStation _station;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected override void InitializeRole()
        {
            if (_station == null) _station = GetComponent<MachineCraftingStation>();
            isConsumer = true;
            // Machines are priority 0 (highest). Gadgets are priority 1,
            // sinks priority 2.
            Priority = 0;
            // If the station already has a machineDef when we wake up,
            // pull the watts from it.
            if (_station != null && _station.Machine != null && requiredWatts == 0)
            {
                requiredWatts = Mathf.Max(0, _station.Machine.powerDrawWatts);
            }
            base.InitializeRole();
        }

        /// <summary>
        /// Bind this consumer to a machine's wattage. Called by the V7
        /// BlockPlacer right after it attaches the
        /// <see cref="MachineCraftingStation"/> (V8.3 hook), and re-callable
        /// by tests.
        /// </summary>
        public void Configure(int draw)
        {
            requiredWatts = Mathf.Max(0, draw);
        }

        /// <summary>
        /// Fraction of <see cref="PowerNode.RequiredWatts"/> currently
        /// supplied. <c>1.0</c> when fully powered; less when the network
        /// is deficient. Clamped to [0,1]. Returns 1 when
        /// <see cref="PowerNode.RequiredWatts"/> is 0 (a machine that
        /// doesn't actually draw power should not be throttled).
        /// </summary>
        public float PowerSatisfaction
        {
            get
            {
                int need = RequiredWatts;
                if (need <= 0) return 1f;
                int have = Mathf.Max(0, CurrentWatts);
                return Mathf.Clamp01((float)have / need);
            }
        }
    }
}
