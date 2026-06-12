using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Voidborne.World.Liquid
{
    /// <summary>
    /// Chunked liquid simulation driver (P3.2). Pure C# — no MonoBehaviour, no
    /// rendering — so it is fully EditMode-testable; ChunkManager integration
    /// (P3.3+) registers chunks and forwards Wake() calls from deformation.
    ///
    /// The perf core is the sleep/wake activity model: a chunk simulates ONLY
    /// while in the active set. It enters on Wake() (liquid placed, terrain
    /// deformed nearby, inflow from a neighbor, border change next door) and
    /// leaves after SLEEP_AFTER_TICKS ticks with zero movement. A placid lake
    /// costs zero CPU.
    ///
    /// Each tick: per-active-chunk Burst jobs run in parallel (cross-chunk reads
    /// are tick-start border plane copies), then cross-chunk outflows are applied
    /// on the calling thread with refund-on-overfill so liquid is conserved.
    /// Determinism: fixed scan order in the job, sorted chunk order for the
    /// apply phase, no randomness anywhere.
    /// </summary>
    public class LiquidSimulator
    {
        public const int SIZE = LiquidField.SIZE;
        public const int PLANE = SIZE * SIZE;
        public const int SLEEP_AFTER_TICKS = 4;

        /// <summary>Default simulation rate (Hz). Runtime integration accumulates
        /// real time and calls TickOnce at this cadence.</summary>
        public const float TICK_RATE = 8f;

        private class Entry
        {
            public LiquidField field;
            public float[] density;
            public int sleepTicks;
            public bool active;
        }

        private readonly Dictionary<Vector3Int, Entry> chunks = new Dictionary<Vector3Int, Entry>();
        private readonly List<Vector3Int> activeList = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> activeSet = new HashSet<Vector3Int>();

        /// <summary>Fired for every chunk whose liquid changed this tick (drives remesh).</summary>
        public event Action<Vector3Int> OnChunkLiquidChanged;

        /// <summary>Total liquid destroyed by settling/terrain displacement (diagnostics).</summary>
        public long TotalEvaporated { get; private set; }

        public int ActiveChunkCount => activeSet.Count;
        public bool IsActive(Vector3Int pos) => activeSet.Contains(pos);

        // ------------------------------------------------------------------
        //  Registration & waking
        // ------------------------------------------------------------------

        /// <summary>Registers a loaded chunk with the simulation world. The density
        /// array is referenced, not copied — deformation changes are seen live.</summary>
        public void RegisterChunk(Vector3Int pos, LiquidField field, float[] density)
        {
            chunks[pos] = new Entry { field = field, density = density };
        }

        public void UnregisterChunk(Vector3Int pos)
        {
            chunks.Remove(pos);
            if (activeSet.Remove(pos))
                activeList.Remove(pos);
        }

        public LiquidField GetField(Vector3Int pos) =>
            chunks.TryGetValue(pos, out Entry e) ? e.field : null;

        /// <summary>
        /// Adds a chunk to the active set (no-op if unregistered). Call when
        /// liquid is placed, terrain near liquid is deformed, or a seal to a
        /// source volume breaks.
        /// </summary>
        public void Wake(Vector3Int pos)
        {
            if (!chunks.TryGetValue(pos, out Entry e)) return;
            e.sleepTicks = 0;
            if (activeSet.Add(pos))
            {
                e.active = true;
                activeList.Add(pos);
            }
        }

        /// <summary>Places liquid into a cell and wakes the chunk.</summary>
        public void AddLiquid(Vector3Int chunkPos, int x, int y, int z, byte amount, LiquidType type)
        {
            if (!chunks.TryGetValue(chunkPos, out Entry e)) return;
            e.field.EnsureLevels();
            int idx = LiquidField.Index(x, y, z);
            int merged = Math.Min(LiquidConstants.MAX_LEVEL, e.field.levels[idx] + amount);
            e.field.levels[idx] = (byte)merged;
            if (e.field.type == LiquidType.None) e.field.type = type;
            Wake(chunkPos);
        }

        // ------------------------------------------------------------------
        //  Tick
        // ------------------------------------------------------------------

        // Reused per-tick scratch (avoids per-tick GC for the common small case)
        private static readonly Vector3Int[] FaceOffsets =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        private struct JobBuffers
        {
            public NativeArray<byte> levels;
            public NativeArray<float> solid;
            public NativeArray<byte>[] nbLevel;   // Down, XNeg, XPos, ZNeg, ZPos
            public NativeArray<byte>[] nbBlocked;
            public NativeArray<byte>[] outflow;
            public NativeArray<int> stats;
        }

        /// <summary>
        /// Runs one simulation tick synchronously (jobs in parallel internally).
        /// Returns the number of chunks that changed.
        /// </summary>
        public int TickOnce()
        {
            if (activeList.Count == 0) return 0;

            // Deterministic processing order
            activeList.Sort(PosComparison);

            int n = activeList.Count;
            var buffers = new JobBuffers[n];
            var handles = new NativeArray<JobHandle>(n, Allocator.Temp);

            // ---- Phase 1: snapshot inputs, schedule one job per active chunk ----
            for (int c = 0; c < n; c++)
            {
                Entry e = chunks[activeList[c]];
                e.field.EnsureLevels();

                var b = new JobBuffers
                {
                    levels = new NativeArray<byte>(e.field.levels, Allocator.TempJob),
                    solid = new NativeArray<float>(e.density, Allocator.TempJob),
                    nbLevel = new NativeArray<byte>[5],
                    nbBlocked = new NativeArray<byte>[5],
                    outflow = new NativeArray<byte>[5],
                    stats = new NativeArray<int>(3, Allocator.TempJob),
                };
                for (int f = 0; f < 5; f++)
                {
                    b.nbLevel[f] = new NativeArray<byte>(PLANE, Allocator.TempJob);
                    b.nbBlocked[f] = new NativeArray<byte>(PLANE, Allocator.TempJob);
                    b.outflow[f] = new NativeArray<byte>(PLANE, Allocator.TempJob);
                }

                BuildNeighborPlanes(activeList[c], b);
                buffers[c] = b;

                var job = new LiquidSimJob
                {
                    Levels = b.levels,
                    Solid = b.solid,
                    FlowRate = LiquidConstants.LateralFlowRate[(byte)e.field.type],
                    NbLevelDown = b.nbLevel[0], NbBlockedDown = b.nbBlocked[0],
                    NbLevelXNeg = b.nbLevel[1], NbBlockedXNeg = b.nbBlocked[1],
                    NbLevelXPos = b.nbLevel[2], NbBlockedXPos = b.nbBlocked[2],
                    NbLevelZNeg = b.nbLevel[3], NbBlockedZNeg = b.nbBlocked[3],
                    NbLevelZPos = b.nbLevel[4], NbBlockedZPos = b.nbBlocked[4],
                    OutDown = b.outflow[0],
                    OutXNeg = b.outflow[1], OutXPos = b.outflow[2],
                    OutZNeg = b.outflow[3], OutZPos = b.outflow[4],
                    Stats = b.stats,
                };
                handles[c] = job.Schedule();
            }

            JobHandle.CompleteAll(handles);
            handles.Dispose();

            // ---- Phase 2: copy results back, apply cross-chunk outflows ----
            var changedChunks = new HashSet<Vector3Int>();
            var wokeNeighbors = new List<Vector3Int>();

            for (int c = 0; c < n; c++)
            {
                Vector3Int pos = activeList[c];
                Entry e = chunks[pos];
                buffers[c].levels.CopyTo(e.field.levels);

                int changed = buffers[c].stats[0];
                TotalEvaporated += buffers[c].stats[1];
                int borderMask = buffers[c].stats[2];

                if (changed > 0)
                {
                    e.sleepTicks = 0;
                    changedChunks.Add(pos);
                    // Wake registered face neighbors whose shared border state changed
                    for (int f = 0; f < 6; f++)
                        if ((borderMask & (1 << f)) != 0)
                            wokeNeighbors.Add(pos + FaceOffsets[f]);
                }
                else
                {
                    e.sleepTicks++;
                }
            }

            // Outflow application — sorted chunk order keeps this deterministic.
            // Refund-on-overfill: liquid that no longer fits (the target also
            // received from its own tick) returns to the source cell.
            for (int c = 0; c < n; c++)
            {
                Vector3Int pos = activeList[c];
                Entry source = chunks[pos];
                ApplyOutflowPlane(buffers[c].outflow[0], pos, pos + new Vector3Int(0, -1, 0), source, PlaneAxis.Y, changedChunks);
                ApplyOutflowPlane(buffers[c].outflow[1], pos, pos + new Vector3Int(-1, 0, 0), source, PlaneAxis.X, changedChunks);
                ApplyOutflowPlane(buffers[c].outflow[2], pos, pos + new Vector3Int( 1, 0, 0), source, PlaneAxis.X, changedChunks);
                ApplyOutflowPlane(buffers[c].outflow[3], pos, pos + new Vector3Int(0, 0, -1), source, PlaneAxis.Z, changedChunks);
                ApplyOutflowPlane(buffers[c].outflow[4], pos, pos + new Vector3Int(0, 0,  1), source, PlaneAxis.Z, changedChunks);
            }

            // Dispose all native buffers
            for (int c = 0; c < n; c++)
            {
                buffers[c].levels.Dispose();
                buffers[c].solid.Dispose();
                buffers[c].stats.Dispose();
                for (int f = 0; f < 5; f++)
                {
                    buffers[c].nbLevel[f].Dispose();
                    buffers[c].nbBlocked[f].Dispose();
                    buffers[c].outflow[f].Dispose();
                }
            }

            // ---- Phase 3: activity bookkeeping ----
            foreach (Vector3Int nb in wokeNeighbors)
                if (chunks.TryGetValue(nb, out Entry nbEntry) && nbEntry.field.HasLevels)
                    Wake(nb);

            for (int c = activeList.Count - 1; c >= 0; c--)
            {
                Vector3Int pos = activeList[c];
                Entry e = chunks[pos];
                if (e.sleepTicks >= SLEEP_AFTER_TICKS)
                {
                    activeSet.Remove(pos);
                    activeList.RemoveAt(c);
                    e.active = false;
                    // Fully drained chunks release their storage
                    if (e.field.HasLevels && e.field.TotalVolume() == 0)
                        e.field.Collapse();
                }
            }

            if (OnChunkLiquidChanged != null)
                foreach (Vector3Int pos in changedChunks)
                    OnChunkLiquidChanged.Invoke(pos);

            return changedChunks.Count;
        }

        // ------------------------------------------------------------------
        //  Internals
        // ------------------------------------------------------------------

        private enum PlaneAxis { X, Y, Z }

        // Cached delegate — avoids a per-tick allocation from the method-group conversion
        private static readonly Comparison<Vector3Int> PosComparison = ChunkPosCompare;

        private static int ChunkPosCompare(Vector3Int a, Vector3Int b)
        {
            int cmp = a.x.CompareTo(b.x);
            if (cmp != 0) return cmp;
            cmp = a.y.CompareTo(b.y);
            return cmp != 0 ? cmp : a.z.CompareTo(b.z);
        }

        /// <summary>
        /// Fills the 5 neighbor border planes (below + 4 lateral) from tick-start
        /// managed state. Unregistered neighbors are blocked — liquid piles at the
        /// simulation boundary instead of draining into the void.
        /// </summary>
        private void BuildNeighborPlanes(Vector3Int pos, JobBuffers b)
        {
            FillPlane(pos + new Vector3Int(0, -1, 0), b.nbLevel[0], b.nbBlocked[0],
                (x, z) => LiquidField.Index(x, SIZE - 1, z), PlaneAxis.Y);
            FillPlane(pos + new Vector3Int(-1, 0, 0), b.nbLevel[1], b.nbBlocked[1],
                (yy, z) => LiquidField.Index(SIZE - 1, yy, z), PlaneAxis.X);
            FillPlane(pos + new Vector3Int(1, 0, 0), b.nbLevel[2], b.nbBlocked[2],
                (yy, z) => LiquidField.Index(0, yy, z), PlaneAxis.X);
            FillPlane(pos + new Vector3Int(0, 0, -1), b.nbLevel[3], b.nbBlocked[3],
                (x, yy) => LiquidField.Index(x, yy, SIZE - 1), PlaneAxis.Z);
            FillPlane(pos + new Vector3Int(0, 0, 1), b.nbLevel[4], b.nbBlocked[4],
                (x, yy) => LiquidField.Index(x, yy, 0), PlaneAxis.Z);
        }

        private void FillPlane(Vector3Int neighborPos, NativeArray<byte> levelPlane,
                               NativeArray<byte> blockedPlane, Func<int, int, int> cellIndex,
                               PlaneAxis axis)
        {
            if (!chunks.TryGetValue(neighborPos, out Entry nb))
            {
                for (int p = 0; p < PLANE; p++) blockedPlane[p] = 1;
                return;
            }

            byte[] nbLevels = nb.field.HasLevels ? nb.field.levels : null;
            for (int a = 0; a < SIZE; a++)
            for (int bb = 0; bb < SIZE; bb++)
            {
                int p = a + bb * SIZE;
                int idx = cellIndex(a, bb);
                blockedPlane[p] = nb.density[idx] > 0f ? (byte)1 : (byte)0;
                levelPlane[p] = nbLevels != null ? nbLevels[idx] : (byte)0;
            }
        }

        /// <summary>
        /// Applies one face's outflow into the target chunk. Liquid that no longer
        /// fits is refunded to the source cell so the total is conserved. The
        /// target chunk wakes and adopts the source's liquid type if empty.
        /// </summary>
        private void ApplyOutflowPlane(NativeArray<byte> outflow, Vector3Int sourcePos,
                                       Vector3Int targetPos, Entry source, PlaneAxis axis,
                                       HashSet<Vector3Int> changedChunks)
        {
            Entry target = null;
            bool hasTarget = chunks.TryGetValue(targetPos, out target);
            bool anyFlow = false;

            for (int a = 0; a < SIZE; a++)
            for (int bb = 0; bb < SIZE; bb++)
            {
                int p = a + bb * SIZE;
                byte t = outflow[p];
                if (t == 0) continue;

                // Job guarantees it only emits outflow toward unblocked, registered
                // neighbors, but the target may have been unregistered mid-frame.
                if (!hasTarget)
                {
                    RefundToSource(source, sourcePos, targetPos, a, bb, axis, t);
                    continue;
                }

                target.field.EnsureLevels();
                int targetIdx = TargetCellIndex(targetPos, sourcePos, a, bb, axis);
                int current = target.field.levels[targetIdx];
                int accepted = Math.Min(t, LiquidConstants.MAX_LEVEL - current);
                if (accepted > 0)
                {
                    target.field.levels[targetIdx] = (byte)(current + accepted);
                    if (target.field.type == LiquidType.None)
                        target.field.type = source.field.type;
                    anyFlow = true;
                }
                int excess = t - accepted;
                if (excess > 0)
                    RefundToSource(source, sourcePos, targetPos, a, bb, axis, (byte)excess);
            }

            if (anyFlow)
            {
                changedChunks.Add(targetPos);
                changedChunks.Add(sourcePos);
                Wake(targetPos);
            }
        }

        /// <summary>Index of the receiving cell in the target chunk for a plane entry.</summary>
        private static int TargetCellIndex(Vector3Int targetPos, Vector3Int sourcePos,
                                           int a, int b, PlaneAxis axis)
        {
            Vector3Int d = targetPos - sourcePos;
            switch (axis)
            {
                case PlaneAxis.Y: // plane (x,z): falling down → target's top layer
                    return LiquidField.Index(a, SIZE - 1, b);
                case PlaneAxis.X: // plane (y,z): -X → target x=31, +X → target x=0
                    return LiquidField.Index(d.x < 0 ? SIZE - 1 : 0, a, b);
                default:          // plane (x,y): -Z → target z=31, +Z → target z=0
                    return LiquidField.Index(a, b, d.z < 0 ? SIZE - 1 : 0);
            }
        }

        /// <summary>Returns refunded liquid to the source border cell.</summary>
        private static void RefundToSource(Entry source, Vector3Int sourcePos,
                                           Vector3Int targetPos, int a, int b,
                                           PlaneAxis axis, byte amount)
        {
            Vector3Int d = targetPos - sourcePos;
            int idx;
            switch (axis)
            {
                case PlaneAxis.Y: idx = LiquidField.Index(a, 0, b); break;
                case PlaneAxis.X: idx = LiquidField.Index(d.x < 0 ? 0 : SIZE - 1, a, b); break;
                default:          idx = LiquidField.Index(a, b, d.z < 0 ? 0 : SIZE - 1); break;
            }
            int merged = Math.Min(LiquidConstants.MAX_LEVEL, source.field.levels[idx] + amount);
            source.field.levels[idx] = (byte)merged;
        }
    }
}
