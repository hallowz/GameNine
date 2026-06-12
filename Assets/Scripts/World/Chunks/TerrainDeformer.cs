using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Static utility class for modifying terrain density fields at runtime.
    /// Handles sphere-based deformation that can cross chunk boundaries,
    /// marks affected chunks as dirty, and triggers mesh regeneration.
    /// </summary>
    public static class TerrainDeformer
    {
        // Reusable set to track which chunks were modified during a deformation pass
        private static readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();

        /// <summary>
        /// Modifies terrain density in a sphere around a world-space center point.
        /// Positive intensity adds material (builds), negative intensity removes material (digs).
        /// Handles deformation across chunk boundaries seamlessly.
        /// </summary>
        /// <param name="center">World-space center of the deformation sphere.</param>
        /// <param name="radius">Radius of the deformation sphere in world units.</param>
        /// <param name="intensity">Strength of deformation. Positive = build, negative = dig.</param>
        public static void DeformSphere(Vector3 center, float radius, float intensity)
        {
            ChunkManager chunkManager = ChunkManager.Instance;
            if (chunkManager == null)
            {
                Debug.LogWarning("[TerrainDeformer] ChunkManager.Instance is null. Cannot deform terrain.");
                return;
            }

            dirtyChunks.Clear();

            // Calculate the axis-aligned bounding box of the sphere in world space
            Vector3 min = center - Vector3.one * radius;
            Vector3 max = center + Vector3.one * radius;

            // Convert bounds to voxel coordinates (integer world positions)
            int minX = Mathf.FloorToInt(min.x);
            int minY = Mathf.FloorToInt(min.y);
            int minZ = Mathf.FloorToInt(min.z);
            int maxX = Mathf.CeilToInt(max.x);
            int maxY = Mathf.CeilToInt(max.y);
            int maxZ = Mathf.CeilToInt(max.z);

            float radiusSqr = radius * radius;

            // Iterate over every voxel in the bounding box
            for (int wz = minZ; wz <= maxZ; wz++)
            {
                for (int wy = minY; wy <= maxY; wy++)
                {
                    for (int wx = minX; wx <= maxX; wx++)
                    {
                        // Distance from this voxel to the sphere center
                        float dx = wx - center.x;
                        float dy = wy - center.y;
                        float dz = wz - center.z;
                        float distSqr = dx * dx + dy * dy + dz * dz;

                        if (distSqr > radiusSqr)
                            continue;

                        // Smooth falloff: 1 at center, 0 at edge
                        float t = 1f - (distSqr / radiusSqr);
                        float falloff = t * t * (3f - 2f * t); // smoothstep

                        // Determine which chunk this voxel belongs to
                        Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(new Vector3(wx, wy, wz));

                        // Local coordinates within the chunk
                        int localX = wx - chunkPos.x * ChunkData.SIZE;
                        int localY = wy - chunkPos.y * ChunkData.SIZE;
                        int localZ = wz - chunkPos.z * ChunkData.SIZE;

                        // Clamp to valid range (safety)
                        if (localX < 0 || localX >= ChunkData.SIZE ||
                            localY < 0 || localY >= ChunkData.SIZE ||
                            localZ < 0 || localZ >= ChunkData.SIZE)
                            continue;

                        // Get or skip the chunk (only modify loaded chunks)
                        ChunkData chunk = chunkManager.GetChunk(chunkPos);
                        if (chunk == null)
                            continue;

                        // Modify density — RecordDensityEdit (not SetDensity) so the
                        // edit persists across save/load (P2.1 sparse edit overlay)
                        float oldDensity = chunk.GetDensity(localX, localY, localZ);
                        float newDensity = oldDensity + intensity * falloff;
                        chunk.RecordDensityEdit(localX, localY, localZ, newDensity);

                        dirtyChunks.Add(chunkPos);

                        // Marching cubes for chunk C builds a 33³ grid where index 32 on each
                        // axis is borrowed from the +1 neighbor's voxel 0.  Therefore, if we
                        // modify voxel 0 of chunk C, the chunk at C−1 (whose 33rd sample IS
                        // voxel 0 of C) also needs remeshing.  We only need the three "low-face"
                        // neighbors; the +X/Y/Z neighbors borrow from further chunks, not this one.
                        if (localX == 0) { var nb = chunkManager.GetChunk(chunkPos + new Vector3Int(-1, 0, 0)); if (nb != null) dirtyChunks.Add(chunkPos + new Vector3Int(-1, 0, 0)); }
                        if (localY == 0) { var nb = chunkManager.GetChunk(chunkPos + new Vector3Int(0, -1, 0)); if (nb != null) dirtyChunks.Add(chunkPos + new Vector3Int(0, -1, 0)); }
                        if (localZ == 0) { var nb = chunkManager.GetChunk(chunkPos + new Vector3Int(0, 0, -1)); if (nb != null) dirtyChunks.Add(chunkPos + new Vector3Int(0, 0, -1)); }
                    }
                }
            }

            // Mark deformed chunks' visibility as fully connected (safe conservative value)
            foreach (var chunkPos in dirtyChunks)
            {
                ChunkData data = chunkManager.GetChunk(chunkPos);
                if (data != null)
                    data.visibilityGraph = 0xFFFF; // all faces connected — recalculated on next ore job
            }

            // Trigger mesh regeneration for all dirty chunks
            chunkManager.RegenerateDirtyChunks(dirtyChunks);

            // Force occlusion culling recompute since terrain changed
            chunkManager.InvalidateOcclusionCulling();
        }

    }
}
