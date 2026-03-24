using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Compact terrain vertex format: 24 bytes vs ~68 bytes standard.
    /// Unity requires each attribute's data size to be a multiple of 4 bytes.
    /// Layout:
    ///   [0-7]   position:     half4 (8 bytes)  — xyz used, w=0 padding
    ///   [8-11]  normal:       SNorm8 x 4 (4 bytes) — xy = octahedral, zw = 0 padding
    ///   [12-15] biomeWeights: UNorm8 x 4 (4 bytes) — texture group blend weights
    ///   [16-19] oreData:      UNorm8 x 4 (4 bytes) — ore type, blend, sky exposure, padding
    ///   [20-23] biomeTint:    UNorm8 x 4 (4 bytes) — per-vertex biome tint RGBA
    /// Total: 24 bytes per vertex
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompactVertex
    {
        public half posX, posY, posZ, posW;          // 8 bytes (w unused)
        public sbyte normalX, normalY, normalZ, normalW; // 4 bytes (zw unused)
        public byte biomeR, biomeG, biomeB, biomeA;  // 4 bytes
        public byte oreType, oreBlend, skyExposure, orePad1; // 4 bytes
        public byte tintR, tintG, tintB, tintA;      // 4 bytes

        /// <summary>
        /// The vertex attribute descriptors for this format.
        /// All attributes are in stream 0, all sizes multiples of 4 bytes.
        /// </summary>
        public static readonly VertexAttributeDescriptor[] Descriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  VertexAttributeFormat.Float16, 4, 0), // 8 bytes
            new VertexAttributeDescriptor(VertexAttribute.Normal,    VertexAttributeFormat.SNorm8,  4, 0), // 4 bytes
            new VertexAttributeDescriptor(VertexAttribute.Color,     VertexAttributeFormat.UNorm8,  4, 0), // 4 bytes
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.UNorm8,  4, 0), // 4 bytes
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.UNorm8,  4, 0), // 4 bytes
        };

        /// <summary>
        /// Encode a unit normal vector to octahedral 2-byte representation.
        /// </summary>
        public static void OctEncode(Vector3 n, out sbyte x, out sbyte y)
        {
            // Project onto octahedron
            float absSum = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);
            if (absSum < 1e-6f) absSum = 1f;
            float ox = n.x / absSum;
            float oy = n.y / absSum;

            // Reflect lower hemisphere
            if (n.z < 0f)
            {
                float tmpX = ox;
                float tmpY = oy;
                ox = (1f - Mathf.Abs(tmpY)) * (tmpX >= 0f ? 1f : -1f);
                oy = (1f - Mathf.Abs(tmpX)) * (tmpY >= 0f ? 1f : -1f);
            }

            // Map [-1, 1] → [-127, 127]
            x = (sbyte)Mathf.Clamp(Mathf.RoundToInt(ox * 127f), -127, 127);
            y = (sbyte)Mathf.Clamp(Mathf.RoundToInt(oy * 127f), -127, 127);
        }

        /// <summary>
        /// Pack a fully-built mesh into compact vertex format.
        /// Replaces the mesh's vertex buffer in-place.
        /// Call this after all vertex data (colors, uv2, uv3) has been set.
        /// </summary>
        public static void CompactMesh(Mesh mesh, float[] skyExposureData = null)
        {
            if (mesh == null || mesh.vertexCount == 0) return;

            int count = mesh.vertexCount;
            var positions = mesh.vertices;
            var normals = mesh.normals;
            var colors = mesh.colors;
            var uv2 = mesh.uv2;

            // UV3 (biome tint) — mesh.GetUVs returns Vector4
            var uv3List = new System.Collections.Generic.List<Vector4>(count);
            mesh.GetUVs(3, uv3List);

            var compact = new CompactVertex[count];

            for (int i = 0; i < count; i++)
            {
                ref CompactVertex v = ref compact[i];

                // Position (half4, w=0)
                Vector3 pos = positions[i];
                v.posX = (half)pos.x;
                v.posY = (half)pos.y;
                v.posZ = (half)pos.z;
                v.posW = (half)0f;

                // Normal (octahedral in xy, zw=0)
                Vector3 n = (normals != null && i < normals.Length) ? normals[i] : Vector3.up;
                OctEncode(n, out v.normalX, out v.normalY);
                v.normalZ = 0;
                v.normalW = 0;

                // Biome weights
                if (colors != null && i < colors.Length)
                {
                    Color c = colors[i];
                    v.biomeR = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
                    v.biomeG = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
                    v.biomeB = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
                    v.biomeA = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
                }

                // Ore data
                if (uv2 != null && i < uv2.Length)
                {
                    v.oreType = (byte)Mathf.Clamp(Mathf.RoundToInt(uv2[i].x * 255f), 0, 255);
                    v.oreBlend = (byte)Mathf.Clamp(Mathf.RoundToInt(uv2[i].y * 255f), 0, 255);
                }

                // Sky exposure (0 = underground, 255 = full sky)
                v.skyExposure = (skyExposureData != null && i < skyExposureData.Length)
                    ? (byte)Mathf.Clamp(Mathf.RoundToInt(skyExposureData[i] * 255f), 0, 255)
                    : (byte)255;

                // Biome tint
                if (i < uv3List.Count)
                {
                    Vector4 tint = uv3List[i];
                    v.tintR = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.x * 255f), 0, 255);
                    v.tintG = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.y * 255f), 0, 255);
                    v.tintB = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.z * 255f), 0, 255);
                    v.tintA = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.w * 255f), 0, 255);
                }
            }

            // Preserve triangle data and submesh structure
            int subMeshCount = mesh.subMeshCount;
            var subMeshDescs = new SubMeshDescriptor[subMeshCount];
            int totalIndices = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                subMeshDescs[s] = mesh.GetSubMesh(s);
                totalIndices += subMeshDescs[s].indexCount;
            }

            // Get all indices across all submeshes
            var allIndices = new int[totalIndices];
            int offset = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                var subTris = mesh.GetTriangles(s);
                System.Array.Copy(subTris, 0, allIndices, offset, subTris.Length);
                offset += subTris.Length;
            }

            var bounds = mesh.bounds;

            // Clear and rebuild with compact layout.
            // Set subMeshCount BEFORE vertex buffer params to avoid bounds recalculation
            // errors (Unity can't recalculate bounds with Float16 positions).
            mesh.Clear();
            mesh.subMeshCount = subMeshCount; // safe here — mesh is empty after Clear()
            mesh.SetVertexBufferParams(count, Descriptors);
            mesh.SetVertexBufferData(compact, 0, 0, count, 0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferParams(allIndices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(allIndices, 0, 0, allIndices.Length,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // Restore submesh structure with DontRecalculateBounds
            int idxOffset = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                int idxCount = subMeshDescs[s].indexCount;
                mesh.SetSubMesh(s, new SubMeshDescriptor(idxOffset, idxCount, MeshTopology.Triangles),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                idxOffset += idxCount;
            }
            mesh.bounds = bounds;
        }

        /// <summary>
        /// Result of async compact encoding, ready to be applied to a mesh on the main thread.
        /// </summary>
        public struct CompactResult
        {
            public CompactVertex[] vertices;
            public int[] indices;
            public SubMeshDescriptor[] subMeshDescs;
            public Bounds bounds;
            public int vertexCount;
        }

        /// <summary>
        /// Asynchronously encodes mesh data into compact vertex format.
        /// Extracts mesh data on the main thread (returns copies), then runs the
        /// encoding loop on a background thread. Calls onComplete with the result
        /// via MainThreadDispatcher so it's time-budgeted.
        ///
        /// Call ApplyCompactResult() on the main thread to write the result to the mesh.
        /// </summary>
        public static void CompactMeshAsync(Mesh mesh, float[] skyExposureData, Action<CompactResult> onComplete)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                onComplete?.Invoke(default);
                return;
            }

            // --- Main thread: extract mesh data (returns managed copies) ---
            int count = mesh.vertexCount;
            var positions = mesh.vertices;
            var normals = mesh.normals;
            var colors = mesh.colors;
            var uv2 = mesh.uv2;

            var uv3List = new List<Vector4>(count);
            mesh.GetUVs(3, uv3List);
            var uv3 = uv3List.ToArray(); // snapshot for background thread

            int subMeshCount = mesh.subMeshCount;
            var subMeshDescs = new SubMeshDescriptor[subMeshCount];
            int totalIndices = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                subMeshDescs[s] = mesh.GetSubMesh(s);
                totalIndices += subMeshDescs[s].indexCount;
            }

            var allIndices = new int[totalIndices];
            int offset = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                var subTris = mesh.GetTriangles(s);
                Array.Copy(subTris, 0, allIndices, offset, subTris.Length);
                offset += subTris.Length;
            }

            var bounds = mesh.bounds;
            var capturedSkyExposure = skyExposureData;

            // --- Background thread: encode vertices (pure math, no Unity API) ---
            Task.Run(() =>
            {
                var compact = new CompactVertex[count];

                for (int i = 0; i < count; i++)
                {
                    ref CompactVertex v = ref compact[i];

                    Vector3 pos = positions[i];
                    v.posX = (half)pos.x;
                    v.posY = (half)pos.y;
                    v.posZ = (half)pos.z;
                    v.posW = (half)0f;

                    Vector3 n = (normals != null && i < normals.Length) ? normals[i] : Vector3.up;
                    OctEncode(n, out v.normalX, out v.normalY);
                    v.normalZ = 0;
                    v.normalW = 0;

                    if (colors != null && i < colors.Length)
                    {
                        Color c = colors[i];
                        v.biomeR = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
                        v.biomeG = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
                        v.biomeB = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
                        v.biomeA = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
                    }

                    if (uv2 != null && i < uv2.Length)
                    {
                        v.oreType = (byte)Mathf.Clamp(Mathf.RoundToInt(uv2[i].x * 255f), 0, 255);
                        v.oreBlend = (byte)Mathf.Clamp(Mathf.RoundToInt(uv2[i].y * 255f), 0, 255);
                    }

                    v.skyExposure = (capturedSkyExposure != null && i < capturedSkyExposure.Length)
                        ? (byte)Mathf.Clamp(Mathf.RoundToInt(capturedSkyExposure[i] * 255f), 0, 255)
                        : (byte)255;

                    if (i < uv3.Length)
                    {
                        Vector4 tint = uv3[i];
                        v.tintR = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.x * 255f), 0, 255);
                        v.tintG = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.y * 255f), 0, 255);
                        v.tintB = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.z * 255f), 0, 255);
                        v.tintA = (byte)Mathf.Clamp(Mathf.RoundToInt(tint.w * 255f), 0, 255);
                    }
                }

                var result = new CompactResult
                {
                    vertices = compact,
                    indices = allIndices,
                    subMeshDescs = subMeshDescs,
                    bounds = bounds,
                    vertexCount = count
                };

                MainThreadDispatcher.Enqueue(() => onComplete?.Invoke(result));
            });
        }

        /// <summary>
        /// Creates a NEW mesh from a pre-computed CompactResult.
        /// Returns the new mesh — caller must swap it on the renderer and destroy the old one.
        /// Using a new mesh avoids a CPU-GPU pipeline stall: calling mesh.Clear() on a mesh
        /// the GPU is actively rendering forces the CPU to wait for the GPU to finish (10-50ms).
        /// </summary>
        public static Mesh CreateCompactMesh(in CompactResult result)
        {
            if (result.vertices == null) return null;

            var mesh = new Mesh();
            mesh.subMeshCount = result.subMeshDescs.Length;
            mesh.SetVertexBufferParams(result.vertexCount, Descriptors);
            mesh.SetVertexBufferData(result.vertices, 0, 0, result.vertexCount, 0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferParams(result.indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(result.indices, 0, 0, result.indices.Length,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            int idxOffset = 0;
            for (int s = 0; s < result.subMeshDescs.Length; s++)
            {
                int idxCount = result.subMeshDescs[s].indexCount;
                mesh.SetSubMesh(s, new SubMeshDescriptor(idxOffset, idxCount, MeshTopology.Triangles),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                idxOffset += idxCount;
            }
            mesh.bounds = result.bounds;
            return mesh;
        }
    }
}
