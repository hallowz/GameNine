using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Generates "skirt" geometry at LOD boundaries to hide cracks between
    /// marching cubes meshes of different LOD levels. For each boundary vertex
    /// on the higher-LOD side, a duplicate vertex is created offset inward
    /// (into solid terrain), and connecting triangles form a skirt.
    ///
    /// This is the simple approach — full Transvoxel transition cells can be
    /// added later if visual quality demands it.
    /// </summary>
    public static class LodSeamStitcher
    {
        /// <summary>
        /// Skirt offset distance — how far boundary vertices are pushed into solid terrain.
        /// Larger values hide bigger cracks but may cause visible skirts at close range.
        /// </summary>
        private const float SKIRT_DEPTH = 1.0f;

        /// <summary>
        /// Adds skirt geometry to a mesh at LOD boundary faces.
        /// Must be called after the mesh is fully built but before CompactMesh.
        ///
        /// boundaryMask: 6-bit mask indicating which faces border a lower-LOD neighbor.
        /// Bit 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z.
        /// </summary>
        public static void AddSkirts(Mesh mesh, int boundaryMask)
        {
            if (mesh == null || mesh.vertexCount == 0 || boundaryMask == 0) return;

            var vertices = new List<Vector3>(mesh.vertices);
            var normals = new List<Vector3>(mesh.normals);
            var indices = new List<int>(mesh.GetTriangles(0));

            // Also preserve per-vertex data
            var colors = mesh.colors;
            var colorList = new List<Color>(colors);
            var uv2 = mesh.uv2;
            var uv2List = new List<Vector2>(uv2 ?? new Vector2[0]);
            var uv3List = new List<Vector4>();
            mesh.GetUVs(3, uv3List);

            int originalVertCount = vertices.Count;
            float chunkSize = ChunkData.SIZE;

            // For each face with a lower-LOD neighbor, find boundary edges and add skirts
            for (int face = 0; face < 6; face++)
            {
                if ((boundaryMask & (1 << face)) == 0) continue;

                // Determine which axis and bound this face corresponds to
                int axis; // 0=X, 1=Y, 2=Z
                float bound; // the coordinate value at this face
                float tolerance = 0.5f; // how close to the edge counts as "boundary"

                switch (face)
                {
                    case 0: axis = 0; bound = chunkSize; break;  // +X
                    case 1: axis = 0; bound = 0f;        break;  // -X
                    case 2: axis = 1; bound = chunkSize; break;  // +Y
                    case 3: axis = 1; bound = 0f;        break;  // -Y
                    case 4: axis = 2; bound = chunkSize; break;  // +Z
                    default:axis = 2; bound = 0f;        break;  // -Z
                }

                // Find boundary edges: edges where at least one vertex is on the face boundary
                var boundaryEdges = new Dictionary<long, (int v0, int v1)>();

                for (int t = 0; t < indices.Count; t += 3)
                {
                    int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                    if (i0 >= originalVertCount || i1 >= originalVertCount || i2 >= originalVertCount)
                        continue;

                    bool b0 = IsOnBoundary(vertices[i0], axis, bound, tolerance);
                    bool b1 = IsOnBoundary(vertices[i1], axis, bound, tolerance);
                    bool b2 = IsOnBoundary(vertices[i2], axis, bound, tolerance);

                    // An edge is a boundary edge if both its vertices are on the boundary
                    // and the triangle has at least one interior vertex
                    if (b0 && b1 && !b2) AddEdge(boundaryEdges, i0, i1);
                    if (b1 && b2 && !b0) AddEdge(boundaryEdges, i1, i2);
                    if (b2 && b0 && !b1) AddEdge(boundaryEdges, i2, i0);
                }

                // For each boundary edge, create a skirt quad (2 triangles)
                Vector3 skirtDir = Vector3.zero;
                skirtDir[axis] = (face % 2 == 0) ? SKIRT_DEPTH : -SKIRT_DEPTH;
                // Also push slightly downward for visual blending
                skirtDir.y -= SKIRT_DEPTH * 0.5f;

                foreach (var edge in boundaryEdges.Values)
                {
                    int v0 = edge.v0, v1 = edge.v1;

                    // Create offset vertices
                    int sv0 = vertices.Count;
                    vertices.Add(vertices[v0] + skirtDir);
                    normals.Add(normals[v0]);
                    if (v0 < colorList.Count) colorList.Add(colorList[v0]); else colorList.Add(Color.white);
                    if (v0 < uv2List.Count) uv2List.Add(uv2List[v0]); else uv2List.Add(Vector2.zero);
                    if (v0 < uv3List.Count) uv3List.Add(uv3List[v0]); else uv3List.Add(Vector4.zero);

                    int sv1 = vertices.Count;
                    vertices.Add(vertices[v1] + skirtDir);
                    normals.Add(normals[v1]);
                    if (v1 < colorList.Count) colorList.Add(colorList[v1]); else colorList.Add(Color.white);
                    if (v1 < uv2List.Count) uv2List.Add(uv2List[v1]); else uv2List.Add(Vector2.zero);
                    if (v1 < uv3List.Count) uv3List.Add(uv3List[v1]); else uv3List.Add(Vector4.zero);

                    // Create quad (two triangles)
                    indices.Add(v0); indices.Add(v1); indices.Add(sv1);
                    indices.Add(v0); indices.Add(sv1); indices.Add(sv0);
                }
            }

            if (vertices.Count == originalVertCount) return; // no skirts added

            // Apply modified mesh data
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            if (colorList.Count == vertices.Count) mesh.SetColors(colorList);
            if (uv2List.Count == vertices.Count) mesh.uv2 = uv2List.ToArray();
            if (uv3List.Count == vertices.Count) mesh.SetUVs(3, uv3List);
            mesh.RecalculateBounds();
        }

        private static bool IsOnBoundary(Vector3 pos, int axis, float bound, float tolerance)
        {
            float val = axis == 0 ? pos.x : (axis == 1 ? pos.y : pos.z);
            return Mathf.Abs(val - bound) < tolerance;
        }

        private static void AddEdge(Dictionary<long, (int, int)> edges, int v0, int v1)
        {
            int lo = Mathf.Min(v0, v1);
            int hi = Mathf.Max(v0, v1);
            long key = ((long)lo << 32) | (uint)hi;
            edges[key] = (lo, hi); // deduplicates shared edges
        }

        /// <summary>
        /// Computes the LOD boundary mask for a chunk by checking each neighbor's LOD.
        /// Returns a 6-bit mask: bit i is set if face i borders a lower-LOD neighbor.
        /// </summary>
        public static int ComputeBoundaryMask(ChunkData chunk, ChunkManager manager)
        {
            if (manager == null) return 0;

            int mask = 0;
            var offsets = new Vector3Int[]
            {
                new Vector3Int( 1, 0, 0),  // +X
                new Vector3Int(-1, 0, 0),  // -X
                new Vector3Int( 0, 1, 0),  // +Y
                new Vector3Int( 0,-1, 0),  // -Y
                new Vector3Int( 0, 0, 1),  // +Z
                new Vector3Int( 0, 0,-1),  // -Z
            };

            for (int i = 0; i < 6; i++)
            {
                ChunkData neighbor = manager.GetChunk(chunk.chunkPosition + offsets[i]);
                if (neighbor != null && neighbor.state == ChunkState.Active
                    && neighbor.lodLevel > chunk.lodLevel)
                {
                    mask |= (1 << i);
                }
            }

            return mask;
        }
    }
}
