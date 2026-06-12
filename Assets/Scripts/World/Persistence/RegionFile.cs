using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Voidborne.World.Persistence
{
    /// <summary>
    /// On-disk region file: one file per 16×16×16-chunk region, holding the edit
    /// blobs of every modified chunk inside it. Pristine chunks are never written,
    /// so an unmodified world has zero region files and region files stay small —
    /// which is why a full rewrite-on-save (atomic temp + rename) is fine and we
    /// avoid Minecraft-style offset tables / fragmentation entirely.
    ///
    /// File layout (version 1):
    ///   int magic "VBRG", byte version, int chunkCount
    ///   per chunk: int x, int y, int z, int blobLength, byte[] blob
    /// </summary>
    public static class RegionFile
    {
        /// <summary>Chunks per region axis (16³ = 4096 chunks per region file).</summary>
        public const int REGION_SIZE = 16;

        private const int Magic = 0x47524256; // "VBRG" little-endian
        private const byte Version = 1;

        /// <summary>Converts a chunk position to its region key (floor division).</summary>
        public static Vector3Int RegionKey(Vector3Int chunkPos)
        {
            return new Vector3Int(
                FloorDiv(chunkPos.x, REGION_SIZE),
                FloorDiv(chunkPos.y, REGION_SIZE),
                FloorDiv(chunkPos.z, REGION_SIZE));
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        public static string GetPath(string regionDirectory, Vector3Int regionKey)
        {
            return Path.Combine(regionDirectory, $"r.{regionKey.x}.{regionKey.y}.{regionKey.z}.vbr");
        }

        /// <summary>
        /// Loads a region file into a chunkPos → blob dictionary.
        /// Missing or corrupt files return an empty dictionary (corrupt logs a warning —
        /// the world still loads, those edits are lost rather than crashing the game).
        /// </summary>
        public static Dictionary<Vector3Int, byte[]> Load(string path)
        {
            var blobs = new Dictionary<Vector3Int, byte[]>();
            if (!File.Exists(path)) return blobs;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadInt32() != Magic || r.ReadByte() != Version)
                    {
                        Debug.LogWarning($"[RegionFile] Unrecognized header in {path} — ignoring file.");
                        return blobs;
                    }

                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var pos = new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
                        int length = r.ReadInt32();
                        blobs[pos] = r.ReadBytes(length);
                    }
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[RegionFile] Failed to read {path}: {e.Message} — ignoring file.");
                blobs.Clear();
            }

            return blobs;
        }

        /// <summary>
        /// Writes a region file atomically (temp file, then move into place).
        /// An empty dictionary deletes the file — the region is pristine again.
        /// Safe to call from a background thread.
        /// </summary>
        public static void Save(string path, Dictionary<Vector3Int, byte[]> blobs)
        {
            if (blobs == null || blobs.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(blobs.Count);
                foreach (var kv in blobs)
                {
                    w.Write(kv.Key.x);
                    w.Write(kv.Key.y);
                    w.Write(kv.Key.z);
                    w.Write(kv.Value.Length);
                    w.Write(kv.Value);
                }
                w.Flush();
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }
    }
}
