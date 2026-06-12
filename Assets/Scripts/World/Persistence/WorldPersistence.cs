using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Voidborne.World.Chunks;

namespace Voidborne.World.Persistence
{
    /// <summary>
    /// Runtime manager for chunk edit persistence (terrain overhaul P2.1).
    ///
    /// Flow:
    ///  - Chunk generation completes → ApplySavedEdits() re-applies persisted density
    ///    edits to the fresh field and seeds the chunk's in-memory edit dictionaries
    ///    (so a later save still carries old edits). Ore edits are re-applied
    ///    separately after voxel classification (which would overwrite them).
    ///  - Chunk unloads / autosave / quit → StashChunk() serializes the chunk's edits
    ///    into the in-memory region cache; Flush() writes dirty regions to disk.
    ///
    /// Threading: the region cache is touched ONLY on the main thread. Flushes
    /// snapshot a region's blob dictionary (blobs are immutable byte[]) and hand the
    /// snapshot to a background task; a per-call lock serializes file writes.
    /// Region loads are synchronous on first touch — files hold only edited chunks,
    /// so this is a sub-millisecond read once per region per session.
    /// </summary>
    public static class WorldPersistence
    {
        private class Region
        {
            public Dictionary<Vector3Int, byte[]> blobs;
            public bool dirty;
        }

        private static readonly Dictionary<Vector3Int, Region> regionCache =
            new Dictionary<Vector3Int, Region>();

        private static readonly object writeLock = new object();
        private static readonly List<Task> inflightWrites = new List<Task>();

        private static string regionDirectory;
        private static bool initialized;

        /// <summary>Initializes persistence for a world derived from the seed.</summary>
        public static void Initialize(int worldSeed)
        {
            InitializeAt(Path.Combine(Application.persistentDataPath,
                "Saves", $"world_{worldSeed}", "region"));
        }

        /// <summary>Initializes persistence rooted at an explicit directory (tests).</summary>
        public static void InitializeAt(string directory)
        {
            regionDirectory = directory;
            regionCache.Clear();
            inflightWrites.Clear();
            initialized = true;
        }

        public static bool IsInitialized => initialized;

        /// <summary>
        /// Re-applies persisted DENSITY edits to a freshly generated chunk and seeds
        /// the chunk's edit dictionaries from the saved blob. Returns true if the
        /// chunk had any saved edits (caller queues a remesh — the GPU meshed the
        /// pristine field). Ore edits are seeded here but applied by the caller
        /// after voxel classification via ApplyOreEdits().
        /// </summary>
        public static bool ApplySavedEdits(ChunkData chunk)
        {
            if (!initialized || chunk == null) return false;

            // LOD transitions regenerate the density field on the SAME ChunkData —
            // stash any unsaved in-memory edits first so the blob we deserialize
            // below is current (otherwise a stale blob would clobber them).
            // This also fixes the pre-LOD-change loss of deformation edits: every
            // regeneration now re-applies the recorded edits over the fresh field.
            if (chunk.hasUnsavedEdits)
                StashChunk(chunk);

            Region region = GetRegion(RegionFile.RegionKey(chunk.chunkPosition));
            if (!region.blobs.TryGetValue(chunk.chunkPosition, out byte[] blob))
                return false;

            chunk.densityEdits ??= new Dictionary<int, float>();
            chunk.oreEdits ??= new Dictionary<int, byte>();
            if (!ChunkSerializer.Deserialize(blob, chunk.densityEdits, chunk.oreEdits))
                return false;

            foreach (var kv in chunk.densityEdits)
                chunk.densityField[kv.Key] = kv.Value;

            return chunk.densityEdits.Count > 0 || chunk.oreEdits.Count > 0;
        }

        /// <summary>
        /// Re-applies persisted ore edits (mined/depleted voxels). Call after
        /// VoxelClassificationJob results are copied into the chunk's OreField.
        /// </summary>
        public static void ApplyOreEdits(ChunkData chunk)
        {
            if (chunk?.oreEdits == null || chunk.oreEdits.Count == 0) return;
            foreach (var kv in chunk.oreEdits)
                chunk.OreField.Set(kv.Key, kv.Value);
        }

        /// <summary>
        /// Serializes a chunk's edits into the region cache (no disk IO).
        /// Call on unload and before flushes for loaded edited chunks.
        /// </summary>
        public static void StashChunk(ChunkData chunk)
        {
            if (!initialized || chunk == null || !chunk.HasEdits) return;
            if (!chunk.hasUnsavedEdits) return; // cache already holds this exact state

            Region region = GetRegion(RegionFile.RegionKey(chunk.chunkPosition));
            region.blobs[chunk.chunkPosition] = ChunkSerializer.Serialize(chunk.densityEdits, chunk.oreEdits);
            region.dirty = true;
            chunk.hasUnsavedEdits = false;
        }

        /// <summary>
        /// Writes all dirty regions to disk on background tasks. Main-thread cost is
        /// a dictionary snapshot per dirty region.
        /// </summary>
        public static void FlushAsync()
        {
            if (!initialized) return;
            inflightWrites.RemoveAll(t => t.IsCompleted);

            foreach (var kv in regionCache)
            {
                Region region = kv.Value;
                if (!region.dirty) continue;
                region.dirty = false;

                string path = RegionFile.GetPath(regionDirectory, kv.Key);
                var snapshot = new Dictionary<Vector3Int, byte[]>(region.blobs);
                inflightWrites.Add(Task.Run(() =>
                {
                    lock (writeLock) RegionFile.Save(path, snapshot);
                }));
            }
        }

        /// <summary>
        /// Synchronous flush for shutdown: waits for in-flight writes, then writes
        /// all dirty regions on the calling thread.
        /// </summary>
        public static void FlushSync()
        {
            if (!initialized) return;

            foreach (Task t in inflightWrites)
            {
                try { t.Wait(2000); }
                catch (System.AggregateException e)
                {
                    Debug.LogWarning($"[WorldPersistence] Background save failed: {e.InnerException?.Message}");
                }
            }
            inflightWrites.Clear();

            foreach (var kv in regionCache)
            {
                Region region = kv.Value;
                if (!region.dirty) continue;
                region.dirty = false;
                lock (writeLock) RegionFile.Save(RegionFile.GetPath(regionDirectory, kv.Key), region.blobs);
            }
        }

        private static Region GetRegion(Vector3Int regionKey)
        {
            if (!regionCache.TryGetValue(regionKey, out Region region))
            {
                region = new Region
                {
                    blobs = RegionFile.Load(RegionFile.GetPath(regionDirectory, regionKey)),
                    dirty = false
                };
                regionCache[regionKey] = region;
            }
            return region;
        }
    }
}
