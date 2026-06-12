using UnityEngine;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Campfire (Forgiving_Thermal_DryBurn). Adds a passive heat aura
    /// flag readable by any nearby player-temperature consumer (M7's player
    /// thermal sim). For M2 this is purely advisory -- the field is
    /// inspectable + queryable but doesn't drive any gameplay yet (no player
    /// temperature). The aura is "on" while the station is actively running a
    /// recipe (burning fuel).
    /// </summary>
    public class CampfireRuntime : MachineRuntime
    {
        [Tooltip("Heat aura radius in meters. Read by future player-temperature sim (M7).")]
        [SerializeField] private float heatRadius = 3f;

        /// <summary>True while the campfire is actively burning (recipe running).</summary>
        public bool IsHeatActive => Station != null && Station.IsRunning;

        /// <summary>Heat aura radius. Read-only; future M7 sim consumes this.</summary>
        public float HeatRadius => heatRadius;
    }
}
