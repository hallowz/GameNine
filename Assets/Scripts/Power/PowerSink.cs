using UnityEngine;
using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.3 — Power Sink. Consumer with the lowest priority that absorbs
    /// any surplus watts left over after real loads and batteries are
    /// served. Acts as a cable-protection device: when over-generation
    /// would otherwise burn a T1 cable, a sink on the line absorbs the
    /// excess and keeps the network safe.
    ///
    /// <para>Sinks have no gameplay effect beyond surplus absorption -- the
    /// watts they consume are discarded.</para>
    /// </summary>
    /// <remarks>
    /// The Core 60 ships <c>power_sink</c> as a Component item; placing it
    /// auto-attaches this MonoBehaviour via the V7 BlockPlacer + V8.3
    /// AttachPowerNodeIfPowerItem hook.
    /// </remarks>
    public class PowerSink : PowerNode
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Header("Power Sink")]
        [Tooltip("Maximum watts this sink can absorb per tick. Default 500W.")]
        [SerializeField] private int maxAbsorbWatts = GameConstants.Power.PowerSinkMaxAbsorbWatts;

        /// <summary>Maximum watts this sink absorbs per tick.</summary>
        public int MaxAbsorbWatts => maxAbsorbWatts;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected override void InitializeRole()
        {
            isConsumer = true;
            // Mirror MaxAbsorbWatts into PowerNode.RequiredWatts so the
            // network distributor sees a consumer with the right capacity.
            // Sinks live in their own bucket in PowerNetwork.TickOnce, so
            // they're served only AFTER real consumers + batteries.
            requiredWatts = maxAbsorbWatts;
            // Lowest priority (highest number) so the sort puts real
            // consumers ahead. The PowerNetwork peeled sinks out into a
            // dedicated bucket; this priority is mostly defensive.
            Priority = 2;
            base.InitializeRole();
        }

        /// <summary>Test seam: rebind the absorb ceiling.</summary>
        public void ConfigureSink(int absorbWatts)
        {
            maxAbsorbWatts = Mathf.Max(0, absorbWatts);
            requiredWatts = maxAbsorbWatts;
        }
    }
}
