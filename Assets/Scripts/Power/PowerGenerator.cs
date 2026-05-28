using UnityEngine;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.2 — base class for every generator. Sets <see cref="PowerNode.IsGenerator"/>
    /// to true on Awake and exposes the per-tick output hook
    /// <see cref="GetTickOutputWatts"/> that <see cref="PowerNetwork"/> calls
    /// during the gen-sum step.
    ///
    /// <para>Subclasses override <see cref="GetTickOutputWatts"/> for any
    /// gated generator (boiler-active, player-cranking, wind-speed-based,
    /// etc). The default returns <see cref="PowerNode.MaxOutputWatts"/> --
    /// i.e. always-on.</para>
    /// </summary>
    /// <remarks>
    /// M2 ships two concrete generators (<see cref="SteamGenerator"/> and
    /// <see cref="HandCrankGenerator"/>). The 54-zoo from the master prompt
    /// is M7 work; each new generator type slots in by overriding
    /// <see cref="GetTickOutputWatts"/> alone -- no PowerNetwork changes
    /// needed.
    /// </remarks>
    public class PowerGenerator : PowerNode
    {
        protected override void InitializeRole()
        {
            // Set the role flag before registering; the network reads
            // IsGenerator during its initial component flood.
            isGenerator = true;
            base.InitializeRole();
        }

        /// <summary>
        /// Watts produced this tick. Override in subclass to gate on a
        /// runtime condition. Default: full <see cref="PowerNode.MaxOutputWatts"/>.
        /// </summary>
        public virtual int GetTickOutputWatts()
        {
            return Mathf.Max(0, MaxOutputWatts);
        }
    }
}
