using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Per-face backface culling via index buffer rebuild.
    /// Classifies triangles into 6 directional buckets by dominant normal, then
    /// rebuilds the mesh's index buffer with only front-facing triangles when
    /// the camera direction changes. Single submesh, single draw call — no pink
    /// textures from null materials.
    ///
    /// Usage:
    ///   1. After mesh generation, call ClassifyTriangles() to build the bucket data.
    ///   2. Store the FaceBuckets on ChunkData.
    ///   3. When camera direction changes, call RebuildIndices() to update the mesh.
    /// </summary>
    public static class DirectionalSubmeshBuilder
    {
        /// <summary>
        /// Pre-classified triangle data for a chunk. Stored on ChunkData so we
        /// don't re-classify every time the camera rotates.
        /// </summary>
        public class FaceBuckets
        {
            // indices[face][triIndex] — triangles sorted into 6 face buckets
            public int[][] bucketIndices; // 6 arrays of triangle indices
            public int totalIndexCount;   // sum of all bucket lengths

            // Cached last applied mask to avoid redundant rebuilds
            public int lastAppliedMask = 0x3F; // all faces visible initially
        }

        // Face indices: +X=0, -X=1, +Y=2, -Y=3, +Z=4, -Z=5
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down,
            Vector3.forward, Vector3.back
        };

        /// <summary>
        /// Classify all triangles in a mesh into 6 directional buckets.
        /// Call once after mesh generation.
        /// </summary>
        public static FaceBuckets ClassifyTriangles(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0) return null;

            var normals = mesh.normals;
            if (normals == null || normals.Length == 0) return null;

            var indices = mesh.GetTriangles(0);
            int triCount = indices.Length / 3;
            if (triCount == 0) return null;

            var buckets = new List<int>[6];
            for (int i = 0; i < 6; i++)
                buckets[i] = new List<int>(triCount / 3);

            for (int t = 0; t < triCount; t++)
            {
                int i0 = indices[t * 3];
                int i1 = indices[t * 3 + 1];
                int i2 = indices[t * 3 + 2];

                Vector3 avgN = normals[i0] + normals[i1] + normals[i2];
                float ax = Mathf.Abs(avgN.x), ay = Mathf.Abs(avgN.y), az = Mathf.Abs(avgN.z);

                int bucket;
                if (ax >= ay && ax >= az)
                    bucket = avgN.x >= 0 ? 0 : 1;
                else if (ay >= ax && ay >= az)
                    bucket = avgN.y >= 0 ? 2 : 3;
                else
                    bucket = avgN.z >= 0 ? 4 : 5;

                buckets[bucket].Add(i0);
                buckets[bucket].Add(i1);
                buckets[bucket].Add(i2);
            }

            var result = new FaceBuckets
            {
                bucketIndices = new int[6][],
                totalIndexCount = indices.Length
            };
            for (int i = 0; i < 6; i++)
                result.bucketIndices[i] = buckets[i].ToArray();

            return result;
        }

        /// <summary>
        /// Rebuild the mesh's index buffer with only triangles from visible face buckets.
        /// Only rebuilds if the mask changed since last call.
        /// </summary>
        public static void RebuildIndices(Mesh mesh, FaceBuckets buckets, int visibleFaceMask)
        {
            if (mesh == null || buckets == null) return;
            if (visibleFaceMask == buckets.lastAppliedMask) return;
            buckets.lastAppliedMask = visibleFaceMask;

            // Count total visible indices
            int totalVisible = 0;
            for (int i = 0; i < 6; i++)
            {
                if ((visibleFaceMask & (1 << i)) != 0)
                    totalVisible += buckets.bucketIndices[i].Length;
            }

            // Build combined index array
            var combined = new int[totalVisible];
            int offset = 0;
            for (int i = 0; i < 6; i++)
            {
                if ((visibleFaceMask & (1 << i)) == 0) continue;
                var bucket = buckets.bucketIndices[i];
                System.Array.Copy(bucket, 0, combined, offset, bucket.Length);
                offset += bucket.Length;
            }

            // Use low-level API to avoid bounds recalculation (compact meshes use Float16)
            mesh.SetIndexBufferParams(combined.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(combined, 0, 0, combined.Length,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, combined.Length, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        /// <summary>
        /// Determines which of the 6 face directions should be rendered given the camera forward.
        /// Returns a 6-bit mask where bit i is set if face i should be rendered.
        /// </summary>
        public static int GetVisibleFaceMask(Vector3 cameraForward)
        {
            int mask = 0;
            Vector3 viewDir = -cameraForward;

            for (int i = 0; i < 6; i++)
            {
                // Only cull faces that FULLY face away from the camera.
                // Threshold of -0.7 means a face must be >135° from the view direction
                // to be culled. This is very conservative — avoids holes at screen edges
                // even with wide FOV. In practice culls ~1 face (the one directly behind).
                if (Vector3.Dot(FaceNormals[i], viewDir) > -0.7f)
                    mask |= (1 << i);
            }

            return mask;
        }
    }
}
