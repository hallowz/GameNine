namespace Voidborne.Core
{
    /// <summary>
    /// Shared engine-side tuning constants. Lives outside any single
    /// subsystem so multiple volumes can reference the same numbers without
    /// coupling. Add new groups as nested static classes -- keep things grep-
    /// friendly.
    /// </summary>
    /// <remarks>
    /// V8 cable-tier ceilings live here; they are referenced by
    /// <see cref="Voidborne.Power.PowerCableTier"/> and by future
    /// generator-side throughput checks (M7 visual burnout, M7 voltage
    /// regulator clamping). Centralising the numbers means a balance tweak
    /// is a one-file edit instead of a sweep.
    /// </remarks>
    public static class GameConstants
    {
        /// <summary>Power-domain numeric ceilings (V8 minimum-viable scope).</summary>
        public static class Power
        {
            /// <summary>Max watts a T1 (copper) cable can carry before burnout. Per master_prompt V8.1.</summary>
            public const int CableT1MaxWatts = 200;

            /// <summary>Max watts a T2 (insulated) cable can carry. Defined now for M7 use; T2 cable item is not in M2 scope.</summary>
            public const int CableT2MaxWatts = 1000;

            /// <summary>Max watts a T3 (heavy-duty) cable can carry. Defined now for M7 use; T3 cable item is not in M2 scope.</summary>
            public const int CableT3MaxWatts = 5000;

            /// <summary>Default battery_basic capacity in watt-seconds (~100W for 100s). Per master_prompt V8.3.</summary>
            public const int BatteryBasicCapacityWs = 10_000;

            /// <summary>Default sink absorb ceiling in watts. Per master_prompt V8.3.</summary>
            public const int PowerSinkMaxAbsorbWatts = 500;

            /// <summary>Steam generator output watts when its boiler is active. Per master_prompt V8.2.</summary>
            public const int SteamGeneratorOutputWatts = 100;

            /// <summary>Hand crank generator output watts while the player is cranking. Per master_prompt V8.2.</summary>
            public const int HandCrankGeneratorOutputWatts = 30;

            /// <summary>PowerNetwork tick frequency in Hz. 10 Hz keeps per-tick allocations bounded for M2 scope.</summary>
            public const int NetworkTickHz = 10;
        }
    }
}
