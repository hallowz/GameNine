using System.Collections.Generic;
using System.IO;

namespace Voidborne.World.Persistence
{
    /// <summary>
    /// Encodes/decodes a single chunk's persisted edits to a compact binary blob.
    ///
    /// Persistence stores sparse per-voxel EDIT OVERLAYS, not full fields: pristine
    /// chunks regenerate from the seed, and edits are re-applied after generation.
    /// Recording edits at the edit sites (TerrainDeformer, mining) sidesteps the
    /// GPU-vs-CPU density baseline mismatch that a "diff against regenerated
    /// baseline" scheme would suffer from.
    ///
    /// Blob layout (version 1):
    ///   byte   version
    ///   int    densityEditCount   then per edit: int voxelIndex, float value
    ///   int    oreEditCount       then per edit: int voxelIndex, byte value
    /// Future sections (liquid field, structure state) append with new versions.
    /// </summary>
    public static class ChunkSerializer
    {
        public const byte Version = 1;

        public static byte[] Serialize(Dictionary<int, float> densityEdits,
                                       Dictionary<int, byte> oreEdits)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);

                int densityCount = densityEdits?.Count ?? 0;
                w.Write(densityCount);
                if (densityCount > 0)
                {
                    foreach (var kv in densityEdits)
                    {
                        w.Write(kv.Key);
                        w.Write(kv.Value);
                    }
                }

                int oreCount = oreEdits?.Count ?? 0;
                w.Write(oreCount);
                if (oreCount > 0)
                {
                    foreach (var kv in oreEdits)
                    {
                        w.Write(kv.Key);
                        w.Write(kv.Value);
                    }
                }

                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Decodes a blob into the supplied dictionaries (cleared first).
        /// Returns false (with empty dictionaries) on unknown version or malformed data.
        /// </summary>
        public static bool Deserialize(byte[] blob,
                                       Dictionary<int, float> densityEdits,
                                       Dictionary<int, byte> oreEdits)
        {
            densityEdits.Clear();
            oreEdits.Clear();
            if (blob == null || blob.Length == 0) return false;

            try
            {
                using (var ms = new MemoryStream(blob))
                using (var r = new BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != Version) return false;

                    int densityCount = r.ReadInt32();
                    for (int i = 0; i < densityCount; i++)
                    {
                        int index = r.ReadInt32();
                        float value = r.ReadSingle();
                        densityEdits[index] = value;
                    }

                    int oreCount = r.ReadInt32();
                    for (int i = 0; i < oreCount; i++)
                    {
                        int index = r.ReadInt32();
                        byte value = r.ReadByte();
                        oreEdits[index] = value;
                    }
                }
                return true;
            }
            catch (IOException)
            {
                densityEdits.Clear();
                oreEdits.Clear();
                return false;
            }
        }
    }
}
