using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// A chunk position + LOD level pair for the generation queue.
    /// </summary>
    public struct ChunkLoadRequest
    {
        public Vector3Int position;
        public int lodLevel;
    }

    /// <summary>
    /// Priority queue for chunk generation. Chunks closest to the player (lowest priority value)
    /// are dequeued first. Uses batch add + sort (O(n log n)) instead of sorted insertion (O(n²))
    /// to handle large LOD rings without freezing.
    /// </summary>
    public class ChunkGenerationQueue
    {
        private struct Entry
        {
            public Vector3Int chunkPos;
            public int lodLevel;
            public float priority;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<Vector3Int> contained = new HashSet<Vector3Int>();
        private bool needsSort;

        // Cursor into sorted entries — avoids O(n) RemoveAt(0) on every dequeue.
        // Entries before cursor are consumed. Clear() resets both list and cursor.
        private int cursor;

        public int Count => entries.Count - cursor;

        /// <summary>
        /// Add a chunk position with the given priority (distance to player).
        /// Does nothing if the position is already in the queue.
        /// Call Sort() after batch-adding entries.
        /// </summary>
        public void Enqueue(Vector3Int pos, int lodLevel, float priority)
        {
            if (contained.Contains(pos))
                return;

            contained.Add(pos);
            entries.Add(new Entry { chunkPos = pos, lodLevel = lodLevel, priority = priority });
            needsSort = true;
        }

        /// <summary>
        /// Sorts entries by priority (ascending). Called once after a batch of Enqueue calls.
        /// O(n log n) — much faster than sorted insertion O(n²) for large queues.
        /// </summary>
        public void Sort()
        {
            if (!needsSort) return;
            entries.Sort(cursor, entries.Count - cursor, EntryComparer.Instance);
            needsSort = false;
        }

        /// <summary>
        /// Remove and return the chunk load request with the lowest priority (closest to player).
        /// O(1) via cursor advancement instead of O(n) RemoveAt(0).
        /// </summary>
        public ChunkLoadRequest Dequeue()
        {
            if (needsSort) Sort();
            var entry = entries[cursor];
            cursor++;
            contained.Remove(entry.chunkPos);
            return new ChunkLoadRequest { position = entry.chunkPos, lodLevel = entry.lodLevel };
        }

        /// <summary>
        /// Return the chunk load request with the lowest priority without removing it.
        /// </summary>
        public ChunkLoadRequest Peek()
        {
            if (needsSort) Sort();
            var entry = entries[cursor];
            return new ChunkLoadRequest { position = entry.chunkPos, lodLevel = entry.lodLevel };
        }

        /// <summary>
        /// Check if the given chunk position is already in the queue.
        /// </summary>
        public bool Contains(Vector3Int pos)
        {
            return contained.Contains(pos);
        }

        /// <summary>
        /// Remove all entries from the queue.
        /// </summary>
        public void Clear()
        {
            entries.Clear();
            contained.Clear();
            needsSort = false;
            cursor = 0;
        }

        /// <summary>
        /// Reusable comparer to avoid delegate allocation on every Sort call.
        /// </summary>
        private class EntryComparer : IComparer<Entry>
        {
            public static readonly EntryComparer Instance = new EntryComparer();
            public int Compare(Entry a, Entry b) => a.priority.CompareTo(b.priority);
        }
    }
}
