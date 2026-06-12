using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Voidborne.World.Liquid
{
    /// <summary>
    /// Burst cellular-automata tick for ONE chunk's liquid field (P3.2).
    ///
    /// Rules per cell, in deterministic bottom-up scan order (y, then z, then x):
    ///   1. FALL    — move as much as fits into the cell below (1 cell per tick).
    ///   2. SPREAD  — if falling is blocked, equalize toward lower lateral
    ///                neighbors, capped by the type's viscosity flow rate.
    ///   3. SETTLE  — stagnant dribbles below MIN_VISIBLE that cannot fall
    ///                evaporate (prevents infinite 1-level shimmer films).
    ///
    /// Cross-chunk flow: the job never writes other chunks. Flow off the six
    /// faces is emitted into per-face outflow buffers using tick-start neighbor
    /// border planes for capacity decisions; LiquidSimulator applies them after
    /// all chunk jobs complete (with refund on overfill, so liquid is conserved).
    ///
    /// In-place single-buffer update: water falls one cell per tick and lateral
    /// spread has a slight +X/+Z direction bias within a tick. Both are
    /// deterministic; tune tick rate / sub-steps before reaching for double
    /// buffering if flow feels slow.
    /// </summary>
    [BurstCompile]
    public struct LiquidSimJob : IJob
    {
        public const int SIZE = 32;
        public const int PLANE = SIZE * SIZE;

        /// <summary>Cell fill levels, mutated in place.</summary>
        public NativeArray<byte> Levels;

        /// <summary>Terrain density; &gt; 0 = solid.</summary>
        [ReadOnly] public NativeArray<float> Solid;

        /// <summary>Viscosity: max transfer per lateral neighbor per tick.</summary>
        public byte FlowRate;

        // Tick-start neighbor border planes. Level = the bordering cell's fill;
        // Blocked = 1 when the bordering cell is solid or the neighbor chunk is
        // not simulated (liquid piles up at the border instead of vanishing).
        // Plane index: Y faces (x,z) → x + z*32; X faces (y,z) → y + z*32;
        // Z faces (x,y) → x + y*32.
        [ReadOnly] public NativeArray<byte> NbLevelDown;
        [ReadOnly] public NativeArray<byte> NbBlockedDown;
        [ReadOnly] public NativeArray<byte> NbLevelXNeg;
        [ReadOnly] public NativeArray<byte> NbBlockedXNeg;
        [ReadOnly] public NativeArray<byte> NbLevelXPos;
        [ReadOnly] public NativeArray<byte> NbBlockedXPos;
        [ReadOnly] public NativeArray<byte> NbLevelZNeg;
        [ReadOnly] public NativeArray<byte> NbBlockedZNeg;
        [ReadOnly] public NativeArray<byte> NbLevelZPos;
        [ReadOnly] public NativeArray<byte> NbBlockedZPos;

        // Per-face outflow amounts (one source cell maps to one plane cell).
        public NativeArray<byte> OutDown;
        public NativeArray<byte> OutXNeg;
        public NativeArray<byte> OutXPos;
        public NativeArray<byte> OutZNeg;
        public NativeArray<byte> OutZPos;

        /// <summary>[0] cells changed, [1] liquid evaporated, [2] border-changed face mask
        /// (bit 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z — matches LodSeamStitcher face order).</summary>
        public NativeArray<int> Stats;

        private const byte MAX = 255;
        private const byte MIN_VISIBLE = 4;

        /// <summary>
        /// Cells at or below this level stop spreading. Without this floor, any
        /// pool with an open edge bleeds to death: an edge cell at level 4 halves
        /// into two sub-MIN_VISIBLE cells, both evaporate, and the erosion walks
        /// inward until the whole pool is gone. With it, pools settle into a
        /// stable shape with a 4–8 level skin at the edges, while genuine films
        /// (&lt; MIN_VISIBLE) still self-clean.
        /// </summary>
        private const byte MIN_SPREAD = MIN_VISIBLE * 2;

        private static int Index(int x, int y, int z) => x + y * SIZE + z * SIZE * SIZE;

        public void Execute()
        {
            int changed = 0;
            int evaporated = 0;
            int borderMask = 0;

            for (int y = 0; y < SIZE; y++)
            for (int z = 0; z < SIZE; z++)
            for (int x = 0; x < SIZE; x++)
            {
                int i = Index(x, y, z);

                // Terrain occupies the cell (e.g. deformation built into liquid):
                // the liquid is destroyed. Counted so conservation tests can assert.
                if (Solid[i] > 0f)
                {
                    if (Levels[i] > 0)
                    {
                        evaporated += Levels[i];
                        Levels[i] = 0;
                        changed++;
                        borderMask |= BorderBits(x, y, z);
                    }
                    continue;
                }

                byte level = Levels[i];
                if (level == 0) continue;
                byte original = level;

                // ---- 1. FALL ----
                bool fallBlocked;
                if (y > 0)
                {
                    int below = Index(x, y - 1, z);
                    if (Solid[below] <= 0f && Levels[below] < MAX)
                    {
                        byte t = (byte)System.Math.Min(level, MAX - Levels[below]);
                        Levels[below] += t;
                        level -= t;
                        fallBlocked = level > 0;
                    }
                    else fallBlocked = true;
                }
                else
                {
                    int p = x + z * SIZE;
                    if (NbBlockedDown[p] == 0 && NbLevelDown[p] < MAX)
                    {
                        // Capacity minus anything this tick already routed there
                        // (only this cell maps to plane cell p, so OutDown[p] is 0 here)
                        byte t = (byte)System.Math.Min(level, MAX - NbLevelDown[p]);
                        OutDown[p] = t;
                        level -= t;
                        fallBlocked = level > 0;
                    }
                    else fallBlocked = true;
                }

                // ---- 2. SPREAD (only liquid that could not fall) ----
                if (fallBlocked && level >= MIN_SPREAD)
                {
                    // -X
                    level = SpreadLateral(level, x > 0 ? Index(x - 1, y, z) : -1,
                        y + z * SIZE, NbLevelXNeg, NbBlockedXNeg, OutXNeg);
                    // +X
                    if (level >= MIN_SPREAD)
                        level = SpreadLateral(level, x < SIZE - 1 ? Index(x + 1, y, z) : -1,
                            y + z * SIZE, NbLevelXPos, NbBlockedXPos, OutXPos);
                    // -Z
                    if (level >= MIN_SPREAD)
                        level = SpreadLateral(level, z > 0 ? Index(x, y, z - 1) : -1,
                            x + y * SIZE, NbLevelZNeg, NbBlockedZNeg, OutZNeg);
                    // +Z
                    if (level >= MIN_SPREAD)
                        level = SpreadLateral(level, z < SIZE - 1 ? Index(x, y, z + 1) : -1,
                            x + y * SIZE, NbLevelZPos, NbBlockedZPos, OutZPos);
                }

                // ---- 3. SETTLE ----
                if (level > 0 && level < MIN_VISIBLE && fallBlocked)
                {
                    evaporated += level;
                    level = 0;
                }

                if (level != original)
                {
                    Levels[i] = level;
                    changed++;
                    borderMask |= BorderBits(x, y, z);
                }
            }

            Stats[0] = changed;
            Stats[1] = evaporated;
            Stats[2] = borderMask;
        }

        /// <summary>
        /// Equalizing transfer toward one lateral neighbor — in-chunk when
        /// inChunkIndex >= 0, otherwise across the border via plane data + outflow.
        /// Returns the remaining level.
        /// </summary>
        private byte SpreadLateral(byte level, int inChunkIndex, int planeIndex,
                                   NativeArray<byte> nbLevel, NativeArray<byte> nbBlocked,
                                   NativeArray<byte> outflow)
        {
            if (inChunkIndex >= 0)
            {
                if (Solid[inChunkIndex] > 0f) return level;
                byte nb = Levels[inChunkIndex];
                if (nb >= level - 1) return level;
                int t = System.Math.Min((level - nb) / 2, (int)FlowRate);
                t = System.Math.Min(t, MAX - nb);
                if (t <= 0) return level;
                Levels[inChunkIndex] = (byte)(nb + t);
                return (byte)(level - t);
            }
            else
            {
                if (nbBlocked[planeIndex] != 0) return level;
                int nb = nbLevel[planeIndex] + outflow[planeIndex];
                if (nb >= level - 1) return level;
                int t = System.Math.Min((level - nb) / 2, (int)FlowRate);
                t = System.Math.Min(t, MAX - nb);
                if (t <= 0) return level;
                outflow[planeIndex] = (byte)(outflow[planeIndex] + t);
                return (byte)(level - t);
            }
        }

        /// <summary>
        /// Face-bit mask for cells at or within one cell of a chunk border (which
        /// neighbors should wake). The 1-cell margin covers RECEIVING cells —
        /// a transfer from (1,y,z) into (0,y,z) changes the border state the -X
        /// neighbor reads, even though the source cell isn't on the border.
        /// </summary>
        private static int BorderBits(int x, int y, int z)
        {
            int mask = 0;
            if (x >= SIZE - 2) mask |= 1 << 0; // +X
            if (x <= 1)        mask |= 1 << 1; // -X
            if (y >= SIZE - 2) mask |= 1 << 2; // +Y
            if (y <= 1)        mask |= 1 << 3; // -Y
            if (z >= SIZE - 2) mask |= 1 << 4; // +Z
            if (z <= 1)        mask |= 1 << 5; // -Z
            return mask;
        }
    }
}
