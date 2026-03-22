using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// BFS-based occlusion culler using per-chunk visibility graphs.
    /// Starting from the camera chunk, traverses outward through connected air volumes.
    /// Chunks that are unreachable through air connectivity are hidden (MeshRenderer disabled).
    ///
    /// Safety rules to prevent visual holes:
    ///   - Camera chunk + all 26 neighbors are ALWAYS visible (no close-range culling)
    ///   - Empty chunks (no mesh) are treated as fully connected (air passthrough)
    ///   - Chunks with visibilityGraph == 0xFFFF are fully connected (default/deformed)
    ///   - Only chunks fully enclosed in solid terrain get culled
    ///   - BFS only traverses loaded Active chunks (no flooding through empty space)
    ///   - Hard limit on BFS visits prevents runaway traversal
    /// </summary>
    public class ChunkOcclusionCuller
    {
        private readonly HashSet<Vector3Int> visibleChunks = new HashSet<Vector3Int>();
        private Vector3Int lastCameraChunk;
        private bool hasLastCamera;

        private static readonly Vector3Int[] FaceDirections =
        {
            new Vector3Int( 1,  0,  0),
            new Vector3Int(-1,  0,  0),
            new Vector3Int( 0,  1,  0),
            new Vector3Int( 0, -1,  0),
            new Vector3Int( 0,  0,  1),
            new Vector3Int( 0,  0, -1),
        };

        private struct BfsEntry
        {
            public Vector3Int pos;
            public int entryFace;
        }

        private readonly Queue<BfsEntry> bfsQueue = new Queue<BfsEntry>(256);

        /// <summary>
        /// Recomputes visibility from the given camera chunk position.
        /// Only call when the camera crosses a chunk boundary.
        /// </summary>
        public void UpdateVisibility(Vector3Int cameraChunk, Dictionary<Vector3Int, ChunkData> chunks,
            DirectionalDistance maxDistance)
        {
            if (hasLastCamera && cameraChunk == lastCameraChunk) return;
            lastCameraChunk = cameraChunk;
            hasLastCamera = true;

            visibleChunks.Clear();
            bfsQueue.Clear();

            // Safety zone: camera chunk + all 26 neighbors are ALWAYS visible.
            // This prevents any close-range culling artifacts when turning the camera.
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                Vector3Int safePos = new Vector3Int(
                    cameraChunk.x + dx, cameraChunk.y + dy, cameraChunk.z + dz);
                visibleChunks.Add(safePos);
                bfsQueue.Enqueue(new BfsEntry { pos = safePos, entryFace = -1 });
            }

            const int MAX_VISITS = 4096;

            while (bfsQueue.Count > 0 && visibleChunks.Count < MAX_VISITS)
            {
                BfsEntry entry = bfsQueue.Dequeue();

                // Get this chunk's visibility graph
                ushort graph;
                if (!chunks.TryGetValue(entry.pos, out ChunkData data) || data == null)
                {
                    // Not loaded — already marked visible, don't propagate
                    continue;
                }

                if (data.state != ChunkState.Active)
                    continue;

                // Empty chunks (no mesh) = fully open air, always pass through
                if (data.mesh == null || data.mesh.vertexCount == 0)
                    graph = 0x7FFF; // all faces connected
                else
                    graph = data.visibilityGraph;

                // For each of the 6 neighbor directions
                for (int face = 0; face < 6; face++)
                {
                    // Check face connectivity (skip for safety zone entries)
                    if (entry.entryFace >= 0)
                    {
                        int a = entry.entryFace < face ? entry.entryFace : face;
                        int b = entry.entryFace < face ? face : entry.entryFace;
                        if (a == b) continue;
                        int pairIdx = ChunkVisibilityJob.PairIndex(a, b);
                        if ((graph & (1 << pairIdx)) == 0)
                            continue;
                    }

                    Vector3Int neighborPos = entry.pos + FaceDirections[face];

                    if (visibleChunks.Contains(neighborPos)) continue;

                    // Distance bounds check
                    int ndx = neighborPos.x - cameraChunk.x;
                    int ndy = neighborPos.y - cameraChunk.y;
                    int ndz = neighborPos.z - cameraChunk.z;
                    if (!maxDistance.Contains(ndx, ndy, ndz)) continue;

                    // Only propagate through loaded chunks
                    if (!chunks.ContainsKey(neighborPos)) continue;

                    visibleChunks.Add(neighborPos);

                    int neighborEntryFace = ChunkVisibilityJob.OppositeFace(face);
                    bfsQueue.Enqueue(new BfsEntry { pos = neighborPos, entryFace = neighborEntryFace });
                }
            }
        }

        /// <summary>
        /// Returns whether a chunk should be visible.
        /// Chunks NOT in the visible set are occluded (fully enclosed in solid terrain).
        /// </summary>
        public bool IsVisible(Vector3Int chunkPos)
        {
            if (!hasLastCamera) return true;
            return visibleChunks.Contains(chunkPos);
        }

        public void Invalidate()
        {
            hasLastCamera = false;
        }

        public int VisibleCount => visibleChunks.Count;
    }
}
