using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.1 — Cable tier discriminator. Each tier caps the watts that can
    /// flow through a single <see cref="CableSegment"/> before burnout.
    ///
    /// M2 ships T1 only (the Core 60 includes <c>copper_cable_t1</c>); T2 / T3
    /// values are defined now so M7 can drop in their cable items without a
    /// schema change.
    /// </summary>
    /// <remarks>
    /// Ceilings live in <see cref="GameConstants.Power"/> -- a balance tweak
    /// is a one-file edit there. <see cref="MaxWatts"/> below is the canonical
    /// resolver any consumer should call.
    /// </remarks>
    public enum PowerCableTier
    {
        /// <summary>200W ceiling. Copper cable (M2 starter tier).</summary>
        T1 = 1,

        /// <summary>1000W ceiling. Insulated cable. M7.</summary>
        T2 = 2,

        /// <summary>5000W ceiling. Heavy-duty cable. M7.</summary>
        T3 = 3,
    }

    /// <summary>Extension helpers for <see cref="PowerCableTier"/>.</summary>
    public static class PowerCableTierExtensions
    {
        /// <summary>Returns the burnout-watt ceiling for this tier.</summary>
        public static int MaxWatts(this PowerCableTier tier)
        {
            switch (tier)
            {
                case PowerCableTier.T1: return GameConstants.Power.CableT1MaxWatts;
                case PowerCableTier.T2: return GameConstants.Power.CableT2MaxWatts;
                case PowerCableTier.T3: return GameConstants.Power.CableT3MaxWatts;
                default: return GameConstants.Power.CableT1MaxWatts;
            }
        }
    }
}
