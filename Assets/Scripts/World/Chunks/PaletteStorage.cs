using Unity.Collections;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Memory-efficient storage for per-voxel byte data (biome IDs, ore types).
    /// Uses palette compression to reduce memory from 32KB to as low as 1 byte
    /// for homogeneous chunks. Three modes:
    ///   Single:    All voxels share one value (1 byte total).
    ///   Palette:   Up to 16 unique values, 4 bits per voxel (16KB + palette).
    ///   Direct:    Fallback byte array (32KB) for >16 unique values.
    /// </summary>
    public class PaletteStorage
    {
        private const int VOLUME = ChunkData.VOLUME; // 32768

        private enum Mode : byte { Single, Palette, Direct }

        private Mode mode;
        private byte singleValue;

        // Palette mode: up to 16 entries, 4 bits per voxel packed into byte[]
        private byte[] palette;     // palette[i] = actual value
        private int paletteCount;
        private byte[] packedData;  // 4 bits per voxel = VOLUME/2 = 16384 bytes

        // Direct mode: flat byte array
        private byte[] directData;

        public PaletteStorage()
        {
            mode = Mode.Single;
            singleValue = 0;
        }

        /// <summary>
        /// Get the value at the given flat index.
        /// </summary>
        public byte Get(int index)
        {
            switch (mode)
            {
                case Mode.Single:
                    return singleValue;

                case Mode.Palette:
                {
                    int byteIdx = index >> 1;
                    int nibble = (index & 1) == 0
                        ? packedData[byteIdx] & 0x0F
                        : (packedData[byteIdx] >> 4) & 0x0F;
                    return palette[nibble];
                }

                default: // Direct
                    return directData[index];
            }
        }

        /// <summary>
        /// Set the value at the given flat index.
        /// May promote from Single → Palette → Direct as needed.
        /// </summary>
        public void Set(int index, byte value)
        {
            switch (mode)
            {
                case Mode.Single:
                    if (value == singleValue) return;
                    // Promote to Palette with 2 entries
                    PromoteSingleToPalette(value, index);
                    break;

                case Mode.Palette:
                {
                    int paletteIdx = FindOrAddPalette(value);
                    if (paletteIdx < 0)
                    {
                        // Palette full, promote to Direct
                        PromotePaletteToDirect();
                        directData[index] = value;
                        return;
                    }
                    int byteIdx = index >> 1;
                    if ((index & 1) == 0)
                        packedData[byteIdx] = (byte)((packedData[byteIdx] & 0xF0) | (paletteIdx & 0x0F));
                    else
                        packedData[byteIdx] = (byte)((packedData[byteIdx] & 0x0F) | ((paletteIdx & 0x0F) << 4));
                    break;
                }

                default: // Direct
                    directData[index] = value;
                    break;
            }
        }

        /// <summary>
        /// Bulk import from a NativeArray (typically from a completed Burst job).
        /// Analyzes content and picks the most compact mode.
        /// </summary>
        public void CopyFrom(NativeArray<byte> source)
        {
            // Count unique values (fast — stop at 17 to know if palette is viable)
            // Use a small fixed-size lookup since values are bytes
            int uniqueCount = 0;
            var seen = new bool[256];
            byte firstVal = source[0];
            bool allSame = true;

            for (int i = 0; i < VOLUME; i++)
            {
                byte v = source[i];
                if (v != firstVal) allSame = false;
                if (!seen[v])
                {
                    seen[v] = true;
                    uniqueCount++;
                    if (uniqueCount > 16 && !allSame)
                        break; // no need to continue scanning
                }
            }

            if (allSame)
            {
                mode = Mode.Single;
                singleValue = firstVal;
                palette = null;
                packedData = null;
                directData = null;
                return;
            }

            if (uniqueCount <= 16)
            {
                // Build palette
                mode = Mode.Palette;
                palette = new byte[16];
                paletteCount = 0;
                var reverseMap = new byte[256]; // value → palette index

                for (int v = 0; v < 256; v++)
                {
                    if (seen[v])
                    {
                        reverseMap[v] = (byte)paletteCount;
                        palette[paletteCount] = (byte)v;
                        paletteCount++;
                    }
                }

                packedData = new byte[VOLUME / 2];
                for (int i = 0; i < VOLUME; i++)
                {
                    byte pi = reverseMap[source[i]];
                    int byteIdx = i >> 1;
                    if ((i & 1) == 0)
                        packedData[byteIdx] |= (byte)(pi & 0x0F);
                    else
                        packedData[byteIdx] |= (byte)((pi & 0x0F) << 4);
                }

                directData = null;
                return;
            }

            // Direct mode
            mode = Mode.Direct;
            directData = source.ToArray();
            palette = null;
            packedData = null;
        }

        /// <summary>
        /// Bulk import from a managed byte array.
        /// </summary>
        public void CopyFrom(byte[] source)
        {
            int uniqueCount = 0;
            var seen = new bool[256];
            byte firstVal = source[0];
            bool allSame = true;

            for (int i = 0; i < VOLUME; i++)
            {
                byte v = source[i];
                if (v != firstVal) allSame = false;
                if (!seen[v])
                {
                    seen[v] = true;
                    uniqueCount++;
                    if (uniqueCount > 16 && !allSame)
                        break;
                }
            }

            if (allSame)
            {
                mode = Mode.Single;
                singleValue = firstVal;
                palette = null;
                packedData = null;
                directData = null;
                return;
            }

            if (uniqueCount <= 16)
            {
                mode = Mode.Palette;
                palette = new byte[16];
                paletteCount = 0;
                var reverseMap = new byte[256];

                for (int v = 0; v < 256; v++)
                {
                    if (seen[v])
                    {
                        reverseMap[v] = (byte)paletteCount;
                        palette[paletteCount] = (byte)v;
                        paletteCount++;
                    }
                }

                packedData = new byte[VOLUME / 2];
                for (int i = 0; i < VOLUME; i++)
                {
                    byte pi = reverseMap[source[i]];
                    int byteIdx = i >> 1;
                    if ((i & 1) == 0)
                        packedData[byteIdx] |= (byte)(pi & 0x0F);
                    else
                        packedData[byteIdx] |= (byte)((pi & 0x0F) << 4);
                }

                directData = null;
                return;
            }

            mode = Mode.Direct;
            directData = new byte[VOLUME];
            System.Array.Copy(source, directData, VOLUME);
            palette = null;
            packedData = null;
        }

        /// <summary>
        /// Export to a flat byte array. Used for NativeArray interop with Burst jobs.
        /// </summary>
        public byte[] ToArray()
        {
            switch (mode)
            {
                case Mode.Single:
                {
                    var arr = new byte[VOLUME];
                    for (int i = 0; i < VOLUME; i++)
                        arr[i] = singleValue;
                    return arr;
                }

                case Mode.Palette:
                {
                    var arr = new byte[VOLUME];
                    for (int i = 0; i < VOLUME; i++)
                    {
                        int byteIdx = i >> 1;
                        int nibble = (i & 1) == 0
                            ? packedData[byteIdx] & 0x0F
                            : (packedData[byteIdx] >> 4) & 0x0F;
                        arr[i] = palette[nibble];
                    }
                    return arr;
                }

                default: // Direct
                {
                    var arr = new byte[VOLUME];
                    System.Array.Copy(directData, arr, VOLUME);
                    return arr;
                }
            }
        }

        /// <summary>
        /// Fill all voxels with the given value (resets to Single mode).
        /// </summary>
        public void Fill(byte value)
        {
            mode = Mode.Single;
            singleValue = value;
            palette = null;
            packedData = null;
            directData = null;
        }

        /// <summary>
        /// Current storage mode for diagnostics.
        /// </summary>
        public string CurrentMode => mode.ToString();

        /// <summary>
        /// Approximate memory usage in bytes.
        /// </summary>
        public int MemoryUsage
        {
            get
            {
                switch (mode)
                {
                    case Mode.Single: return 1;
                    case Mode.Palette: return 16 + (packedData?.Length ?? 0);
                    default: return directData?.Length ?? 0;
                }
            }
        }

        private void PromoteSingleToPalette(byte newValue, int index)
        {
            mode = Mode.Palette;
            palette = new byte[16];
            palette[0] = singleValue;
            palette[1] = newValue;
            paletteCount = 2;

            // All voxels are palette index 0 (the old single value)
            // which is all zeros in the packed array
            packedData = new byte[VOLUME / 2];

            // Set the new value at the specified index
            byte pi = 1;
            int byteIdx = index >> 1;
            if ((index & 1) == 0)
                packedData[byteIdx] = (byte)(packedData[byteIdx] | (pi & 0x0F));
            else
                packedData[byteIdx] = (byte)(packedData[byteIdx] | ((pi & 0x0F) << 4));
        }

        private void PromotePaletteToDirect()
        {
            directData = ToArray();
            mode = Mode.Direct;
            palette = null;
            packedData = null;
        }

        private int FindOrAddPalette(byte value)
        {
            for (int i = 0; i < paletteCount; i++)
            {
                if (palette[i] == value)
                    return i;
            }

            if (paletteCount >= 16)
                return -1; // palette full

            palette[paletteCount] = value;
            return paletteCount++;
        }
    }
}
