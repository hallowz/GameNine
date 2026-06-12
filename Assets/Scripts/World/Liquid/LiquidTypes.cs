namespace Voidborne.World.Liquid
{
    /// <summary>
    /// Liquid type identifiers. One liquid type per chunk (cross-contamination
    /// resolves to the heavier type — see master_prompt_terrain.md P3 design).
    /// </summary>
    public enum LiquidType : byte
    {
        None = 0,
        Water = 1,
        Acid = 2,
        Petroleum = 3,
        Milk = 4,
    }

    /// <summary>
    /// Per-type simulation constants. Indexed by (byte)LiquidType.
    /// </summary>
    public static class LiquidConstants
    {
        /// <summary>Cell fill level is 0–255; 255 = full voxel of liquid.</summary>
        public const byte MAX_LEVEL = 255;

        /// <summary>
        /// Levels below this render as nothing and stagnant dribbles below it
        /// evaporate (kills infinite shimmer from 1-level films).
        /// </summary>
        public const byte MIN_VISIBLE = 4;

        /// <summary>
        /// Max liquid moved to EACH lateral neighbor per tick — the viscosity knob.
        /// Indexed by (byte)LiquidType. Falling is not viscosity-capped.
        /// </summary>
        public static readonly byte[] LateralFlowRate =
        {
            0,   // None
            64,  // Water
            64,  // Acid
            16,  // Petroleum (thick)
            48,  // Milk
        };

        /// <summary>
        /// Heavier types win when liquids meet at a chunk boundary
        /// (higher = heavier).
        /// </summary>
        public static readonly byte[] Density =
        {
            0,   // None
            2,   // Water
            3,   // Acid
            1,   // Petroleum (floats on water)
            2,   // Milk
        };
    }
}
