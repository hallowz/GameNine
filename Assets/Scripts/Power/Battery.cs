using UnityEngine;
using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.3 — Battery storage node. Stores power in watt-seconds; charges
    /// from network surplus and discharges to cover deficit. Default
    /// capacity for the Core 60 <c>battery_basic</c> item is
    /// <see cref="GameConstants.Power.BatteryBasicCapacityWs"/> (10 000 Ws,
    /// roughly "100W for 100 seconds").
    /// </summary>
    /// <remarks>
    /// <see cref="PowerNetwork"/> calls <see cref="ChargeWatts"/> on the
    /// surplus branch and <see cref="DischargeWatts"/> on the deficit
    /// branch. Both convert watts <-> watt-seconds via <c>dt</c> -- so a
    /// 100W charge for 0.1s adds 10Ws.
    ///
    /// Coop note: storage state is server-authoritative in V21. M2 runs
    /// locally; the deterministic charge/discharge math means a client
    /// that observed the same supply/demand will land on the same
    /// <see cref="StoredWs"/>.
    /// </remarks>
    public class Battery : PowerNode
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Header("Battery")]
        [Tooltip("Total capacity in watt-seconds. Default 10 000 (100W * 100s).")]
        [SerializeField] private int capacityWs = GameConstants.Power.BatteryBasicCapacityWs;

        [Tooltip("Currently stored watt-seconds. Clamped to [0, capacityWs].")]
        [SerializeField] private int storedWs = 0;

        /// <summary>Total capacity in watt-seconds.</summary>
        public int CapacityWs => capacityWs;

        /// <summary>Currently stored watt-seconds. Read-only externally.</summary>
        public int StoredWs => storedWs;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected override void InitializeRole()
        {
            isStorage = true;
            base.InitializeRole();
        }

        /// <summary>
        /// Test seam: rebind capacity + stored Ws. Useful for fixtures
        /// that need a starting charge (e.g. discharge test starts at 1000Ws).
        /// </summary>
        public void ConfigureBattery(int capacity, int stored)
        {
            capacityWs = Mathf.Max(0, capacity);
            storedWs = Mathf.Clamp(stored, 0, capacityWs);
        }

        // ---------------------------------------------------------------
        //  Charge / discharge
        // ---------------------------------------------------------------

        /// <summary>
        /// Try to absorb <paramref name="watts"/> over <paramref name="dt"/>
        /// seconds. Returns the watts actually absorbed (might be less if
        /// the battery is nearly full).
        /// </summary>
        public int ChargeWatts(int watts, float dt)
        {
            if (watts <= 0 || dt <= 0f) return 0;
            int wsAvailableCapacity = capacityWs - storedWs;
            if (wsAvailableCapacity <= 0) return 0;
            int wsRequest = Mathf.FloorToInt(watts * dt);
            if (wsRequest <= 0) return 0;
            int wsAbsorbed = Mathf.Min(wsRequest, wsAvailableCapacity);
            storedWs += wsAbsorbed;
            // Convert absorbed watt-seconds back to watts for this tick.
            return Mathf.RoundToInt(wsAbsorbed / dt);
        }

        /// <summary>
        /// Try to discharge <paramref name="watts"/> over <paramref name="dt"/>
        /// seconds. Returns the watts actually delivered (might be less if
        /// the battery is nearly empty).
        /// </summary>
        public int DischargeWatts(int watts, float dt)
        {
            if (watts <= 0 || dt <= 0f) return 0;
            if (storedWs <= 0) return 0;
            int wsRequest = Mathf.FloorToInt(watts * dt);
            if (wsRequest <= 0) return 0;
            int wsDrawn = Mathf.Min(wsRequest, storedWs);
            storedWs -= wsDrawn;
            return Mathf.RoundToInt(wsDrawn / dt);
        }
    }
}
