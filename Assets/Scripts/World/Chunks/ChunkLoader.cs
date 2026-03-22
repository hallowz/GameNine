using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.Diagnostics;
using Voidborne.Player;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// MonoBehaviour that manages loading and unloading chunks around a target transform.
    /// Uses staged LOD loading: LOD0 (closest) loads first, then LOD1, then LOD2.
    /// Each LOD ring has its own queue so distant chunks never block nearby ones.
    /// Applies velocity-based priority weighting so chunks ahead of a fast-moving
    /// player load before chunks behind them.
    /// </summary>
    public class ChunkLoader : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Timing")]
        [Tooltip("How often (in frames) to check for chunks to load/unload.")]
        [SerializeField] private int checkInterval = 2;

        [Header("Distance Buffer")]
        [Tooltip("Extra chunk distance beyond render distance before unloading. Prevents thrashing.")]
        [SerializeField] private int unloadBuffer = 2;

        [Header("Predictive Loading")]
        [Tooltip("How strongly player velocity biases chunk priority. 0 = pure distance, 1 = heavily favor direction of travel.")]
        [Range(0f, 1f)]
        [SerializeField] private float velocityBias = 0.6f;


        [Header("Slot Allocation")]
        [Tooltip("Minimum GPU slots reserved for LOD0 when LOD0 queue is non-empty. " +
                 "Remaining slots are shared with LOD1/LOD2 for concurrent generation.")]
        [SerializeField] private int minLod0Slots = 8;

        [Tooltip("Maximum GPU slots LOD1 and LOD2 can use combined. " +
                 "Prevents distant terrain from starving LOD0 during loading.")]
        [SerializeField] private int maxLodLowerSlots = 4;

        private ChunkManager chunkManager;

        // Separate queue per LOD level — LOD0 is always processed first.
        private ChunkGenerationQueue lod0Queue;
        private ChunkGenerationQueue lod1Queue;
        private ChunkGenerationQueue lod2Queue;
        private ChunkGenerationQueue lod3Queue;
        private ChunkGenerationQueue lod4Queue;

        // Tracks whether each LOD ring has been queued since the last player chunk change.
        // LOD1 is only queued after LOD0 drains, LOD2 after LOD1 drains.
        private bool lod0Queued;
        private bool lod1Queued;
        private bool lod2Queued;
        private bool lod3Queued;
        private bool lod4Queued;

        // Tracks the player chunk position at the time each LOD ring was last built.
        // LOD1/2/3 queues are only invalidated when the player moves >N chunks from
        // the position they were built at, preventing repeated rebuilds at high speed.
        private Vector3Int lod1BuiltAtChunk;
        private Vector3Int lod2BuiltAtChunk;
        private Vector3Int lod3BuiltAtChunk;
        private Vector3Int lod4BuiltAtChunk;

        // Clipmap ring delta: tracks the center + distances used for each LOD ring
        // so we can skip positions that were already in the previous ring on rebuild.
        private Vector3Int[] lastRingCenter = new Vector3Int[5];
        private DirectionalDistance[] lastRingOuter = new DirectionalDistance[5];
        private DirectionalDistance[] lastRingInner = new DirectionalDistance[5];
        private bool[] hasLastRing = new bool[5];

        // Tracks the effective LOD0 distance when LOD1 queue was last built.
        // LOD1 inner boundary depends on this, so the queue must be rebuilt when it changes.
        private DirectionalDistance lod1BuiltAtEffective;

        [Header("Empty Zone Promotion")]
        [Tooltip("When all LOD0 chunks are empty (no terrain), promote the closest chunks " +
                 "with terrain from LOD1/2 to LOD0. Max number to promote per check.")]
        [SerializeField] private int maxEmptyZonePromotions = 8;

        private Vector3Int lastPlayerChunkPos;
        private Vector3 lastPlayerWorldPos;
        private bool hasLastPos;
        private int frameCounter;
        private int unloadFrameCounter;

        // Reusable list for empty-zone promotion candidates
        private readonly List<(Vector3Int pos, float distSq)> promotionCandidates =
            new List<(Vector3Int, float)>();
        // Reusable buffer for LOD distance levels in PromoteClosestTerrainToLod0
        private readonly List<DirectionalDistance> lodDistBuffer = new List<DirectionalDistance>(4);

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("ChunkLoader.Update");

        // Velocity tracking for predictive loading
        private Vector3 playerVelocity;
        private Vector3 prevPlayerPos;

        private void Start()
        {
            chunkManager = ChunkManager.Instance;
            lod0Queue = new ChunkGenerationQueue();
            lod1Queue = new ChunkGenerationQueue();
            lod2Queue = new ChunkGenerationQueue();
            lod3Queue = new ChunkGenerationQueue();
            lod4Queue = new ChunkGenerationQueue();

            if (chunkManager == null)
            {
                Debug.LogError("[ChunkLoader] ChunkManager.Instance is null. Make sure ChunkManager exists in the scene.");
                enabled = false;
                return;
            }

            // If no target is assigned, try to find the player via PlayerManager
            if (target == null)
            {
                if (PlayerManager.Instance != null)
                {
                    target = PlayerManager.Instance.PlayerTransform;
                    Debug.Log("[ChunkLoader] No target assigned — using PlayerManager.");
                }
                else
                {
                    var mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        target = mainCam.transform;
                        Debug.Log("[ChunkLoader] No target assigned — using Camera.main.");
                    }
                    else
                    {
                        Debug.LogError("[ChunkLoader] No target assigned, no PlayerManager, and no Camera.main found.");
                        enabled = false;
                        return;
                    }
                }
            }

            // Force an immediate check on start
            hasLastPos = false;
            frameCounter = checkInterval;
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            // Track velocity for predictive loading
            if (target != null)
            {
                Vector3 currentPos = target.position;
                if (hasLastPos)
                {
                    float dt = Time.deltaTime;
                    if (dt > 0f)
                        playerVelocity = Vector3.Lerp(playerVelocity, (currentPos - prevPlayerPos) / dt, 0.3f);
                }
                prevPlayerPos = currentPos;
            }

            // Check for new chunks to load every N frames — runs BEFORE ProcessLoadQueue
            // so LOD0 always has its queue built before LOD1/LOD2 can grab GPU slots.
            frameCounter++;
            if (frameCounter >= checkInterval)
            {
                frameCounter = 0;
                CheckChunks();
            }

            // Process queued chunks every frame (rate-limited by GPU slots)
            ProcessLoadQueue();

            // Unload distant chunks on a separate timer — runs even when the player
            // hasn't crossed a chunk boundary, ensuring stale chunks are always cleaned up.
            unloadFrameCounter++;
            if (unloadFrameCounter >= checkInterval && target != null && chunkManager != null)
            {
                unloadFrameCounter = 0;
                chunkManager.UnloadDistantChunks(target.position);
            }
            RuntimeProfiler.End(s_prof);
        }

        /// <summary>
        /// Check which chunks need to be loaded or unloaded based on the target's position.
        /// </summary>
        private void CheckChunks()
        {
            if (target == null || chunkManager == null) return;

            Vector3 playerPos = target.position;
            Vector3Int currentChunkPos = ChunkCoordUtility.WorldToChunkPos(playerPos);

            // Skip if the player hasn't moved to a new chunk since last check
            if (hasLastPos && currentChunkPos == lastPlayerChunkPos)
                return;

            lastPlayerChunkPos = currentChunkPos;
            lastPlayerWorldPos = playerPos;
            hasLastPos = true;

            // Update sky -Y boost before anything reads LOD distances this frame.
            chunkManager.UpdateSkyBoost(playerPos.y);

            // Cancel stale in-flight generations in a single dictionary scan:
            // chunks beyond max LOD range are cancelled outright; LOD0 gens that
            // dropped out of LOD0 range are cancelled to free GPU slots for closer chunks.
            DirectionalDistance cancelDist = chunkManager.MaxDistance;
            cancelDist.posX += unloadBuffer; cancelDist.negX += unloadBuffer;
            cancelDist.posY += unloadBuffer; cancelDist.negY += unloadBuffer;
            cancelDist.posZ += unloadBuffer; cancelDist.negZ += unloadBuffer;
            chunkManager.CancelStaleGenerations(currentChunkPos, cancelDist,
                chunkManager.EffectiveLod0Distance);

            // Always rebuild LOD0 immediately (small: ~245 positions).
            // LOD1/LOD2 are only invalidated when the player moves far enough from
            // where they were built, preventing repeated queue rebuilds at high speed
            // that never get a chance to drain.
            QueueLodRing(0);

            if (lod1Queued)
            {
                int dx = Mathf.Abs(currentChunkPos.x - lod1BuiltAtChunk.x);
                int dz = Mathf.Abs(currentChunkPos.z - lod1BuiltAtChunk.z);
                if (Mathf.Max(dx, dz) > 1)
                {
                    lod1Queued = false;
                    hasLastRing[1] = false; // force full rebuild on next queue
                }
            }

            if (lod2Queued)
            {
                int dx = Mathf.Abs(currentChunkPos.x - lod2BuiltAtChunk.x);
                int dz = Mathf.Abs(currentChunkPos.z - lod2BuiltAtChunk.z);
                if (Mathf.Max(dx, dz) > 2)
                {
                    lod2Queued = false;
                    hasLastRing[2] = false;
                }
            }

            if (lod3Queued)
            {
                int dx = Mathf.Abs(currentChunkPos.x - lod3BuiltAtChunk.x);
                int dz = Mathf.Abs(currentChunkPos.z - lod3BuiltAtChunk.z);
                if (Mathf.Max(dx, dz) > 3)
                {
                    lod3Queued = false;
                    hasLastRing[3] = false;
                }
            }

            if (lod4Queued)
            {
                int dx = Mathf.Abs(currentChunkPos.x - lod4BuiltAtChunk.x);
                int dz = Mathf.Abs(currentChunkPos.z - lod4BuiltAtChunk.z);
                if (Mathf.Max(dx, dz) > 4)
                {
                    lod4Queued = false;
                    hasLastRing[4] = false;
                }
            }
        }

        /// <summary>
        /// Queues all unloaded chunk positions within a specific LOD ring.
        /// The ring is the set difference between the outer directional box and the
        /// inner directional box (the previous LOD's effective boundary).
        /// </summary>
        private void QueueLodRing(int lodLevel)
        {
            DirectionalDistance outer, inner;
            ChunkGenerationQueue queue;

            switch (lodLevel)
            {
                case 0:
                    outer = chunkManager.Lod0Distance;
                    inner = default; // no inner exclusion for LOD0
                    queue = lod0Queue;
                    lod0Queued = true;
                    break;
                case 1:
                    outer = chunkManager.Lod1Distance;
                    inner = chunkManager.EffectiveLod0Distance;
                    queue = lod1Queue;
                    lod1Queued = true;
                    lod1BuiltAtChunk = lastPlayerChunkPos;
                    lod1BuiltAtEffective = chunkManager.EffectiveLod0Distance;
                    if (outer.IsDisabled) return; // LOD1 disabled
                    break;
                case 2:
                    outer = chunkManager.Lod2Distance;
                    inner = chunkManager.Lod1Distance;
                    queue = lod2Queue;
                    lod2Queued = true;
                    lod2BuiltAtChunk = lastPlayerChunkPos;
                    if (outer.IsDisabled) return; // LOD2 disabled
                    break;
                case 3:
                    outer = chunkManager.Lod3Distance;
                    inner = chunkManager.Lod2Distance;
                    queue = lod3Queue;
                    lod3Queued = true;
                    lod3BuiltAtChunk = lastPlayerChunkPos;
                    if (outer.IsDisabled) return; // LOD3 disabled
                    break;
                case 4:
                    outer = chunkManager.Lod4Distance;
                    inner = chunkManager.Lod3Distance;
                    queue = lod4Queue;
                    lod4Queued = true;
                    lod4BuiltAtChunk = lastPlayerChunkPos;
                    if (outer.IsDisabled) return; // LOD4 disabled
                    break;
                default:
                    return;
            }

            queue.Clear();
            Vector3Int center = lastPlayerChunkPos;
            Vector3 playerPos = lastPlayerWorldPos;
            bool hasInner = lodLevel > 0;

            // Clipmap delta: for LOD1+, skip positions that were already in the
            // previous ring (same outer/inner box offset by old center). This avoids
            // re-iterating thousands of already-loaded chunk positions.
            bool useDelta = lodLevel > 0 && hasLastRing[lodLevel];
            Vector3Int deltaOffset = default;
            DirectionalDistance prevOuter = default, prevInner = default;
            if (useDelta)
            {
                deltaOffset = lastRingCenter[lodLevel] - center; // old center relative to new
                prevOuter = lastRingOuter[lodLevel];
                prevInner = lastRingInner[lodLevel];
            }

            // Save current ring state for next delta comparison
            lastRingCenter[lodLevel] = center;
            lastRingOuter[lodLevel] = outer;
            lastRingInner[lodLevel] = inner;
            hasLastRing[lodLevel] = true;

            // Velocity-based priority: chunks in the player's travel direction
            // get lower priority values (loaded first).
            Vector3 velDir = Vector3.zero;
            float speed = playerVelocity.magnitude;
            float bias = 0f;
            if (speed > 2f && velocityBias > 0f)
            {
                velDir = playerVelocity / speed;
                bias = velocityBias;
            }

            for (int x = -outer.negX; x <= outer.posX; x++)
            for (int z = -outer.negZ; z <= outer.posZ; z++)
            for (int y = -outer.negY; y <= outer.posY; y++)
            {
                // Skip positions within the inner LOD region
                if (hasInner && inner.Contains(x, y, z)) continue;

                // Clipmap delta: skip positions that were in the previous ring.
                // Transform this offset to the old center's frame and check if it
                // was inside the old (outer - inner) region.
                if (useDelta)
                {
                    int ox = x + deltaOffset.x;
                    int oy = y + deltaOffset.y;
                    int oz = z + deltaOffset.z;
                    if (prevOuter.Contains(ox, oy, oz) && !(hasInner && prevInner.Contains(ox, oy, oz)))
                    {
                        // This position was already queued in the previous ring — skip
                        // unless the chunk needs a LOD change.
                        Vector3Int chunkPos2 = new Vector3Int(center.x + x, center.y + y, center.z + z);
                        ChunkData ex = chunkManager.GetChunk(chunkPos2);
                        if (ex != null && ex.state == ChunkState.Active && ex.lodLevel == lodLevel)
                            continue;
                        if (ex != null && ex.state != ChunkState.Unloaded
                                       && ex.state != ChunkState.MarkedForUnload
                                       && !(ex.state == ChunkState.Active && ex.lodLevel != lodLevel))
                            continue;
                    }
                }

                Vector3Int chunkPos = new Vector3Int(
                    center.x + x,
                    center.y + y,
                    center.z + z
                );

                ChunkData existing = chunkManager.GetChunk(chunkPos);
                if (existing != null && existing.state != ChunkState.Unloaded
                                     && existing.state != ChunkState.MarkedForUnload)
                {
                    if (existing.state == ChunkState.Active && existing.lodLevel != lodLevel)
                    {
                        // LOD mismatch — fall through to enqueue for regeneration.
                    }
                    else
                    {
                        continue;
                    }
                }

                Vector3 chunkWorldCenter = ChunkCoordUtility.ChunkToWorldPos(chunkPos)
                                           + Vector3.one * (ChunkData.SIZE * 0.5f);
                Vector3 toChunk = chunkWorldCenter - playerPos;
                float distSq = toChunk.sqrMagnitude;

                if (bias > 0f && distSq > 0.01f)
                {
                    float rawDot = toChunk.x * velDir.x + toChunk.y * velDir.y + toChunk.z * velDir.z;
                    float invDist = math.rsqrt(distSq);
                    distSq *= (1f - bias * rawDot * invDist);
                }

                queue.Enqueue(chunkPos, lodLevel, distSq);
            }

            queue.Sort();
        }

        /// <summary>
        /// Each frame, process all LOD queues concurrently. LOD0 has priority on slots
        /// (guaranteed minLod0Slots), but LOD1/LOD2 can use any remaining free slots
        /// without waiting for LOD0 to fully drain. This allows distant terrain to
        /// generate in parallel with nearby terrain.
        /// </summary>
        private void ProcessLoadQueue()
        {
            if (chunkManager == null) return;

            // Build LOD1/LOD2/LOD3 queues one per frame to avoid spikes from
            // multiple QueueLodRing calls in the same frame (each iterates hundreds
            // to thousands of chunk positions with dictionary lookups + distance calcs).
            if (lod0Queued)
            {
                if (!lod1Queued)
                    QueueLodRing(1);
                else if (!lod2Queued)
                    QueueLodRing(2);
                else if (!lod3Queued)
                    QueueLodRing(3);
                else if (!lod4Queued)
                    QueueLodRing(4);
            }

            // Phase 1: LOD0 gets first pick of slots.
            DrainQueue(lod0Queue);

            // Promote closest terrain to LOD0 — empty chunks don't count toward
            // render distance, so terrain beyond the normal LOD0 range gets upgraded.
            if (lod0Queue.Count == 0)
            {
                DirectionalDistance prevEffective = chunkManager.EffectiveLod0Distance;
                PromoteClosestTerrainToLod0();

                // If effective LOD0 distance changed, LOD ring boundaries shifted.
                // Invalidate all lower-LOD queues so they're rebuilt with the new inner box.
                if (chunkManager.EffectiveLod0Distance != prevEffective)
                {
                    if (lod1Queued) { lod1Queued = false; hasLastRing[1] = false; }
                    if (lod2Queued) { lod2Queued = false; hasLastRing[2] = false; }
                    if (lod3Queued) { lod3Queued = false; hasLastRing[3] = false; }
                    if (lod4Queued) { lod4Queued = false; hasLastRing[4] = false; }
                }
            }

            // Phase 2+: each LOD level only starts when the previous level's queue is
            // fully drained. This prevents distant terrain (which generates faster due
            // to lower density) from appearing before closer terrain, creating holes.
            if (lod0Queue.Count == 0)
            {
                int freeSlots = chunkManager.FreeGenerationSlots;
                int slotsForLower = Mathf.Min(freeSlots, maxLodLowerSlots);

                if (slotsForLower > 0 && lod1Queue.Count > 0)
                    DrainQueue(lod1Queue, slotsForLower);

                // LOD2 only after LOD1 queue is fully drained
                if (lod1Queue.Count == 0)
                {
                    freeSlots = chunkManager.FreeGenerationSlots;
                    slotsForLower = Mathf.Min(freeSlots, maxLodLowerSlots);

                    if (slotsForLower > 0 && lod2Queue.Count > 0)
                        DrainQueue(lod2Queue, slotsForLower);

                    // LOD3 only after LOD2 queue is fully drained
                    if (lod2Queue.Count == 0)
                    {
                        freeSlots = chunkManager.FreeGenerationSlots;
                        slotsForLower = Mathf.Min(freeSlots, maxLodLowerSlots);

                        if (slotsForLower > 0 && lod3Queue.Count > 0)
                            DrainQueue(lod3Queue, slotsForLower);

                        // LOD4 only after LOD3 queue is fully drained
                        if (lod3Queue.Count == 0)
                        {
                            freeSlots = chunkManager.FreeGenerationSlots;
                            slotsForLower = Mathf.Min(freeSlots, maxLodLowerSlots);

                            if (slotsForLower > 0 && lod4Queue.Count > 0)
                                DrainQueue(lod4Queue, slotsForLower);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ensures the closest terrain chunks are always at LOD0, regardless of how many
        /// empty (air) chunks sit between the player and the terrain. Empty chunks do not
        /// count toward render distance — the LOD0 budget is spent only on chunks with
        /// actual geometry.
        ///
        /// Manages EffectiveLod0Distance on ChunkManager: when the player is surrounded
        /// by air, effective LOD0 range expands progressively through LOD1/2/3 until
        /// terrain is found (up to MaxDistance) so promoted chunks aren't immediately
        /// downgraded (which caused oscillation). When the player has terrain within the
        /// static LOD0 range, effective resets to static.
        /// </summary>
        private void PromoteClosestTerrainToLod0()
        {
            if (!hasLastPos) return;

            Vector3Int center = lastPlayerChunkPos;
            Vector3 playerPos = lastPlayerWorldPos;

            DirectionalDistance staticDist = chunkManager.Lod0Distance;

            // Collect all enabled LOD distances to expand through progressively.
            var lodDists = lodDistBuffer;
            lodDists.Clear();
            lodDists.Add(staticDist);
            if (!chunkManager.Lod1Distance.IsDisabled) lodDists.Add(chunkManager.Lod1Distance);
            if (!chunkManager.Lod2Distance.IsDisabled) lodDists.Add(chunkManager.Lod2Distance);
            if (!chunkManager.Lod3Distance.IsDisabled) lodDists.Add(chunkManager.Lod3Distance);

            if (lodDists.Count < 2) return; // nothing to expand into

            // --- Phase 1: Per-direction terrain search ---
            // For each direction, progressively expand through LOD rings until
            // terrain is found or we run out of LOD levels. The effective LOD0
            // distance for that direction is set to the ring where terrain was found,
            // or MaxDistance if no terrain exists anywhere.
            // foundPosX[i] etc. tracks which LOD level first found terrain per direction.
            // -1 means not yet found.
            int foundPosX = -1, foundNegX = -1;
            int foundPosY = -1, foundNegY = -1;
            int foundPosZ = -1, foundNegZ = -1;

            for (int li = 0; li < lodDists.Count; li++)
            {
                // Skip directions already resolved
                if (foundPosX >= 0 && foundNegX >= 0 &&
                    foundPosY >= 0 && foundNegY >= 0 &&
                    foundPosZ >= 0 && foundNegZ >= 0)
                    break;

                DirectionalDistance cur = lodDists[li];
                // Previous ring boundary (to avoid re-scanning inner region)
                DirectionalDistance prev = li > 0 ? lodDists[li - 1] : default;

                for (int x = -cur.negX; x <= cur.posX; x++)
                for (int z = -cur.negZ; z <= cur.posZ; z++)
                for (int y = -cur.negY; y <= cur.posY; y++)
                {
                    // Skip inner region already scanned
                    if (li > 0 && prev.Contains(x, y, z)) continue;

                    Vector3Int pos = new Vector3Int(center.x + x, center.y + y, center.z + z);
                    ChunkData data = chunkManager.GetChunk(pos);
                    if (data == null || data.state != ChunkState.Active) continue;
                    if (data.mesh == null || data.mesh.vertexCount == 0) continue;

                    if (x > 0 && foundPosX < 0) foundPosX = li;
                    if (x < 0 && foundNegX < 0) foundNegX = li;
                    if (y > 0 && foundPosY < 0) foundPosY = li;
                    if (y < 0 && foundNegY < 0) foundNegY = li;
                    if (z > 0 && foundPosZ < 0) foundPosZ = li;
                    if (z < 0 && foundNegZ < 0) foundNegZ = li;
                }
            }

            // --- Phase 2: Build effective distance per-direction ---
            // For each direction: use the static LOD0 distance if terrain was found
            // at LOD0, otherwise use the distance of the LOD level where terrain was
            // found. If no terrain was found at all, expand to MaxDistance.
            DirectionalDistance maxDist = chunkManager.MaxDistance;

            DirectionalDistance effective = new DirectionalDistance
            {
                posX = foundPosX == 0 ? staticDist.posX : foundPosX > 0 ? lodDists[foundPosX].posX : maxDist.posX,
                negX = foundNegX == 0 ? staticDist.negX : foundNegX > 0 ? lodDists[foundNegX].negX : maxDist.negX,
                posY = foundPosY == 0 ? staticDist.posY : foundPosY > 0 ? lodDists[foundPosY].posY : maxDist.posY,
                negY = foundNegY == 0 ? staticDist.negY : foundNegY > 0 ? lodDists[foundNegY].negY : maxDist.negY,
                posZ = foundPosZ == 0 ? staticDist.posZ : foundPosZ > 0 ? lodDists[foundPosZ].posZ : maxDist.posZ,
                negZ = foundNegZ == 0 ? staticDist.negZ : foundNegZ > 0 ? lodDists[foundNegZ].negZ : maxDist.negZ,
            };

            DirectionalDistance oldEffective = chunkManager.EffectiveLod0Distance;
            chunkManager.EffectiveLod0Distance = effective;

            // --- Phase 3: Promote LOD1+ chunks in newly expanded regions ---
            // Only needed if effective actually grew in any direction.
            if (effective == oldEffective) return;

            promotionCandidates.Clear();
            for (int x = -effective.negX; x <= effective.posX; x++)
            for (int z = -effective.negZ; z <= effective.posZ; z++)
            for (int y = -effective.negY; y <= effective.posY; y++)
            {
                // Skip chunks that were already in the old effective range
                if (oldEffective.Contains(x, y, z)) continue;

                Vector3Int pos = new Vector3Int(center.x + x, center.y + y, center.z + z);
                ChunkData data = chunkManager.GetChunk(pos);
                if (data == null || data.state != ChunkState.Active) continue;
                if (data.lodLevel == 0) continue; // already LOD0
                if (data.mesh == null || data.mesh.vertexCount == 0) continue;

                Vector3 chunkWorldCenter = ChunkCoordUtility.ChunkToWorldPos(pos)
                                           + Vector3.one * (ChunkData.SIZE * 0.5f);
                float distSq = (chunkWorldCenter - playerPos).sqrMagnitude;
                promotionCandidates.Add((pos, distSq));
            }

            if (promotionCandidates.Count > 0)
            {
                promotionCandidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                int count = Mathf.Min(promotionCandidates.Count, maxEmptyZonePromotions);
                for (int i = 0; i < count; i++)
                    lod0Queue.Enqueue(promotionCandidates[i].pos, 0, promotionCandidates[i].distSq);
                if (count > 0)
                    lod0Queue.Sort();
            }
        }

        /// <summary>
        /// Processes entries from a single queue until empty or GPU slots are full.
        /// Returns true if the queue is fully drained, false if slots are busy.
        /// </summary>
        private bool DrainQueue(ChunkGenerationQueue queue, int maxSubmissions = int.MaxValue)
        {
            if (queue == null) return true;

            int submitted = 0;
            while (queue.Count > 0 && submitted < maxSubmissions)
            {
                ChunkLoadRequest request = queue.Peek();

                // Skip if already generating, or active at the correct LOD.
                // Allow active chunks through when they need a LOD change (upgrade or downgrade).
                ChunkData existing = chunkManager.GetChunk(request.position);
                if (existing != null)
                {
                    if (existing.state == ChunkState.Generating)
                    {
                        queue.Dequeue();
                        continue;
                    }
                    if (existing.state == ChunkState.Active && existing.lodLevel == request.lodLevel)
                    {
                        queue.Dequeue();
                        continue;
                    }
                }

                // Returns false when all GPU generation slots are busy.
                if (!chunkManager.LoadChunk(request.position, request.lodLevel))
                    return false;

                queue.Dequeue();
                submitted++;
            }

            return queue.Count == 0;
        }
    }
}
