using UnityEngine;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Steam Boiler (Forgiving_Thermal_Boil).
    ///
    /// <para>Accepts a liquid (water OR milk via property fallback) + a fuel,
    /// runs a recipe that outputs a synthetic "steam" item. The actual
    /// upstream interaction with the V8.2 <see cref="Voidborne.Power.SteamGenerator"/>
    /// is the boiler-adjacency probe: SteamGenerator looks for a placed
    /// steam_boiler whose <see cref="MachineCraftingStation.IsRunning"/> is
    /// true. So "produces steam" == "station is running"; no separate fluid
    /// pipe sim in M2.</para>
    ///
    /// <para>The runtime exposes <see cref="IsProducingSteam"/> so tests +
    /// HUD probes can read the boiler's gate state without poking into the
    /// station directly. It matches what SteamGenerator's IsBoilerActive
    /// adjacency probe sees.</para>
    /// </summary>
    public class SteamBoilerRuntime : MachineRuntime
    {
        /// <summary>
        /// True while the boiler is running a recipe and is therefore producing
        /// steam from the V8.2 SteamGenerator's point of view.
        /// </summary>
        public bool IsProducingSteam => Station != null && Station.IsRunning;
    }
}
