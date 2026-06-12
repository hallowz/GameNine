using Voidborne.World.Chunks;

namespace Voidborne.World.Liquid
{
    /// <summary>
    /// Per-chunk liquid state: one fill level byte per voxel (0 = none, 255 = full)
    /// plus a single liquid type for the whole chunk.
    ///
    /// Sparse by design (P3 design rule 3): chunks with no liquid hold no
    /// LiquidField at all — callers allocate on first liquid entry via
    /// EnsureLevels(). The flat array exists only while the chunk holds liquid.
    /// </summary>
    public class LiquidField
    {
        public const int SIZE = ChunkData.SIZE;
        public const int VOLUME = ChunkData.VOLUME;

        /// <summary>Per-voxel fill levels. Null until liquid first enters the chunk.</summary>
        public byte[] levels;

        /// <summary>The liquid type occupying this chunk (one type per chunk).</summary>
        public LiquidType type = LiquidType.None;

        public bool HasLevels => levels != null;

        public void EnsureLevels()
        {
            levels ??= new byte[VOLUME];
        }

        public static int Index(int x, int y, int z) => x + y * SIZE + z * SIZE * SIZE;

        public byte Get(int x, int y, int z) =>
            levels == null ? (byte)0 : levels[Index(x, y, z)];

        /// <summary>Sets a cell level, allocating storage on first use.</summary>
        public void Set(int x, int y, int z, byte level, LiquidType liquidType)
        {
            EnsureLevels();
            levels[Index(x, y, z)] = level;
            if (level > 0 && type == LiquidType.None)
                type = liquidType;
        }

        /// <summary>Total liquid volume in the chunk (sum of all cell levels).</summary>
        public long TotalVolume()
        {
            if (levels == null) return 0;
            long sum = 0;
            for (int i = 0; i < levels.Length; i++) sum += levels[i];
            return sum;
        }

        /// <summary>
        /// Releases storage when the chunk has fully drained (called by the
        /// simulator when a settled chunk holds zero liquid).
        /// </summary>
        public void Collapse()
        {
            levels = null;
            type = LiquidType.None;
        }
    }
}
