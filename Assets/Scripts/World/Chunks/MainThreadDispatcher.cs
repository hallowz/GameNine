using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Voidborne.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Time-budgeted main-thread callback dispatcher.
    ///
    /// Background threads (Task.Run, AsyncGPUReadback) enqueue work via Enqueue().
    /// ChunkManager calls ProcessQueue() each frame, which drains the queue within
    /// a configurable time budget (default 4ms). This prevents frame spikes when
    /// many async operations complete simultaneously (e.g. 12 GPU readbacks finishing
    /// in the same frame while the player moves fast).
    ///
    /// Physics-critical callbacks (collider assignments) use EnqueuePriority() and
    /// are processed first with NO budget limit — they are O(μs) pointer swaps that
    /// must not be delayed or the player falls through the world.
    ///
    /// Replaces direct SynchronizationContext.Post usage which dumps ALL pending
    /// callbacks into a single frame with no budget control.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> _priorityQueue = new ConcurrentQueue<Action>();
        private static readonly Stopwatch _sw = new Stopwatch();
        private static float _budgetMs = 4f;

        private static readonly RuntimeProfiler.Token s_prof =
            RuntimeProfiler.Register("MainThreadDispatcher");

        /// <summary>Per-frame time budget in milliseconds for processing queued callbacks.</summary>
        public static float BudgetMs
        {
            get => _budgetMs;
            set => _budgetMs = value;
        }

        /// <summary>Number of callbacks currently waiting in the queue.</summary>
        public static int PendingCount => _queue.Count + _priorityQueue.Count;

        /// <summary>
        /// Enqueue a callback to run on the main thread within the time budget.
        /// Thread-safe — call from any thread.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action != null)
                _queue.Enqueue(action);
        }

        /// <summary>
        /// Enqueue a physics-critical callback that runs BEFORE the budgeted queue
        /// with NO time limit. Use for lightweight operations that must not be
        /// delayed (e.g. MeshCollider assignment after Physics.BakeMesh).
        /// Thread-safe — call from any thread.
        /// </summary>
        public static void EnqueuePriority(Action action)
        {
            if (action != null)
                _priorityQueue.Enqueue(action);
        }

        /// <summary>
        /// Drain BOTH priority and normal queues with an elevated budget (3× normal).
        /// Use when the player is frozen waiting for chunk activation — we want the
        /// chunk Active ASAP but dumping ALL callbacks in one frame causes 50ms+ spikes
        /// that stall rendering. Priority callbacks still drain unbounded (they're μs-cost).
        /// </summary>
        public static void ProcessQueueUnbounded()
        {
            bool hasPriority = !_priorityQueue.IsEmpty;
            bool hasNormal = !_queue.IsEmpty;
            if (!hasPriority && !hasNormal) return;

            RuntimeProfiler.Begin(s_prof);

            // Priority queue: always drain fully (collider assignments, ~0.01ms each)
            while (_priorityQueue.TryDequeue(out Action priority))
            {
                try { priority(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Normal queue: use 3× the normal budget instead of truly unbounded.
            // This prevents 50ms+ spikes from mesh construction bursts while still
            // processing callbacks faster than normal to unfreeze the player quickly.
            if (!_queue.IsEmpty)
            {
                float urgentBudget = _budgetMs * 3f;
                _sw.Restart();
                while (_sw.Elapsed.TotalMilliseconds < urgentBudget && _queue.TryDequeue(out Action action))
                {
                    try { action(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                _sw.Stop();
            }

            RuntimeProfiler.End(s_prof);
        }

        /// <summary>
        /// Process queued callbacks within the time budget. Call once per frame
        /// from a main-thread Update() method (e.g. ChunkManager.Update).
        /// Priority queue is drained fully first (no budget — these are μs-cost).
        /// </summary>
        public static void ProcessQueue()
        {
            bool hasPriority = !_priorityQueue.IsEmpty;
            bool hasNormal = !_queue.IsEmpty;
            if (!hasPriority && !hasNormal) return;

            RuntimeProfiler.Begin(s_prof);

            // Phase 1: drain priority queue with NO budget limit.
            // These are physics-critical callbacks (collider assignments) that are
            // essentially free (~0.01ms each) but must run immediately.
            while (_priorityQueue.TryDequeue(out Action priority))
            {
                try
                {
                    priority();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Phase 2: drain normal queue within the time budget.
            if (!_queue.IsEmpty)
            {
                _sw.Restart();
                while (_sw.Elapsed.TotalMilliseconds < _budgetMs && _queue.TryDequeue(out Action action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                _sw.Stop();
            }

            RuntimeProfiler.End(s_prof);
        }
    }
}
