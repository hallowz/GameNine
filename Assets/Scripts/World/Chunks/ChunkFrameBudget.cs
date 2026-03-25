using System.Diagnostics;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Central frame-time budget for all chunk processing on the main thread.
    /// Call BeginFrame() at the start of ChunkManager.Update, then check HasBudget
    /// before processing each item in any loop. This guarantees that chunk work
    /// never exceeds the configured millisecond budget per frame, regardless of
    /// how many chunks are pending.
    /// </summary>
    public static class ChunkFrameBudget
    {
        private static readonly Stopwatch sw = new Stopwatch();

        /// <summary>Maximum milliseconds of chunk work allowed per frame.</summary>
        private static float budgetMs = 4f;

        /// <summary>Smoothed recent frame time for adaptive budgeting.</summary>
        private static float smoothedFrameMs = 16f;

        /// <summary>Whether adaptive budgeting is enabled.</summary>
        public static bool AdaptiveEnabled = true;

        /// <summary>Start the per-frame budget timer. Call once at the top of Update.</summary>
        public static void BeginFrame()
        {
            sw.Restart();
        }

        /// <summary>True if there is time remaining in the current frame's budget.</summary>
        public static bool HasBudget => sw.Elapsed.TotalMilliseconds < budgetMs;

        /// <summary>Elapsed milliseconds since BeginFrame was called.</summary>
        public static double ElapsedMs => sw.Elapsed.TotalMilliseconds;

        /// <summary>Current budget in milliseconds.</summary>
        public static float BudgetMs => budgetMs;

        /// <summary>
        /// Set the base budget in milliseconds.
        /// </summary>
        public static void SetBudget(float ms)
        {
            budgetMs = ms;
        }

        /// <summary>
        /// Call at the end of each frame to adapt the budget based on recent frame times.
        /// When frames are fast (under target), we allow more chunk work.
        /// When frames are slow, we reduce chunk work to maintain framerate.
        /// </summary>
        public static void AdaptBudget(float frameTimeMs, float targetFrameMs = 16.67f)
        {
            if (!AdaptiveEnabled) return;

            smoothedFrameMs = smoothedFrameMs * 0.9f + frameTimeMs * 0.1f;

            // How much headroom do we have under the target?
            float headroom = targetFrameMs - smoothedFrameMs;

            if (headroom > 4f)
            {
                // Lots of headroom — allow more work (up to 6ms)
                budgetMs = System.Math.Min(budgetMs + 0.1f, 6f);
            }
            else if (headroom < 1f)
            {
                // Tight or over budget — reduce (minimum 2ms to avoid starvation)
                budgetMs = System.Math.Max(budgetMs - 0.2f, 2f);
            }
        }
    }
}
