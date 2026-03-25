using System;
using System.Collections.Concurrent;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Thread-safe, frame-budgeted callback queue that replaces unbounded
    /// SynchronizationContext.Post for chunk pipeline operations.
    ///
    /// Background threads (mesh generation, collider baking) enqueue callbacks
    /// via Enqueue/EnqueueHigh. The main thread processes them in Update via
    /// ProcessBudgeted(), which respects ChunkFrameBudget to prevent spikes.
    ///
    /// Two priority tiers:
    ///   High   — collider assignments (max 2 per frame, always processed first)
    ///   Normal — mesh generation completions (budget-limited)
    /// </summary>
    public static class ChunkCallbackQueue
    {
        private static readonly ConcurrentQueue<Action> highQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> normalQueue = new ConcurrentQueue<Action>();

        /// <summary>Maximum high-priority callbacks per frame.</summary>
        private const int MaxHighPerFrame = 3;

        /// <summary>Enqueue a normal-priority callback (mesh completion, etc).</summary>
        public static void Enqueue(Action callback)
        {
            if (callback != null)
                normalQueue.Enqueue(callback);
        }

        /// <summary>Enqueue a high-priority callback (collider assignment, etc).</summary>
        public static void EnqueueHigh(Action callback)
        {
            if (callback != null)
                highQueue.Enqueue(callback);
        }

        /// <summary>
        /// Process queued callbacks within the frame budget.
        /// Call from ChunkManager.Update after BeginFrame.
        /// Returns the number of callbacks processed.
        /// </summary>
        public static int ProcessBudgeted()
        {
            int processed = 0;

            // High-priority first (always, up to cap)
            int highProcessed = 0;
            while (highProcessed < MaxHighPerFrame && highQueue.TryDequeue(out Action highCb))
            {
                highCb();
                highProcessed++;
                processed++;
            }

            // Normal priority: respect budget
            while (ChunkFrameBudget.HasBudget && normalQueue.TryDequeue(out Action cb))
            {
                cb();
                processed++;
            }

            return processed;
        }

        /// <summary>Number of pending normal-priority callbacks.</summary>
        public static int NormalCount => normalQueue.Count;

        /// <summary>Number of pending high-priority callbacks.</summary>
        public static int HighCount => highQueue.Count;

        /// <summary>Total pending callbacks across both queues.</summary>
        public static int TotalCount => normalQueue.Count + highQueue.Count;

        /// <summary>Clear all queued callbacks (used during cleanup/destroy).</summary>
        public static void Clear()
        {
            while (highQueue.TryDequeue(out _)) { }
            while (normalQueue.TryDequeue(out _)) { }
        }
    }
}
