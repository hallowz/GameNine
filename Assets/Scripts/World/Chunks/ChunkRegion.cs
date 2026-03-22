using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Groups multiple LOD1+ chunks into a single combined mesh to reduce draw calls.
    /// Each region covers a 4×4×4 chunk group (128×128×128 world units).
    /// LOD0 chunks are excluded — they need individual colliders and deformation support.
    /// </summary>
    public class ChunkRegion
    {
        public const int REGION_SIZE = 4;

        public Vector3Int regionKey;
        public readonly List<ChunkData> chunks = new List<ChunkData>(64);
        public Mesh combinedMesh;
        public GameObject regionObject;
        public bool isDirty;

        /// <summary>
        /// Converts a chunk position to a region key.
        /// </summary>
        public static Vector3Int ChunkToRegionKey(Vector3Int chunkPos)
        {
            // Integer division that handles negatives correctly
            return new Vector3Int(
                FloorDiv(chunkPos.x, REGION_SIZE),
                FloorDiv(chunkPos.y, REGION_SIZE),
                FloorDiv(chunkPos.z, REGION_SIZE));
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        /// <summary>
        /// Rebuilds the combined mesh from all chunks in this region.
        /// </summary>
        public void Rebuild(Material material, Transform parent)
        {
            isDirty = false;

            // Collect valid meshes — only standard-format meshes (skip compacted ones)
            var combines = new List<CombineInstance>();
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                ChunkData data = chunks[i];
                if (data.state != ChunkState.Active || data.mesh == null || data.mesh.vertexCount == 0)
                    continue;
                // Only combine meshes with a single submesh and standard vertex format.
                // Multi-submesh = directional split (LOD0). Compact format (Float16/SNorm8)
                // is unsupported by CombineMeshes.
                if (data.mesh.subMeshCount != 1)
                    continue;
                // Check that position format is Float32 (CombineMeshes can't handle Float16)
                if (data.mesh.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32)
                    continue;

                combines.Add(new CombineInstance
                {
                    mesh = data.mesh,
                    transform = Matrix4x4.Translate(data.WorldPosition)
                });
            }

            if (combines.Count == 0)
            {
                // No meshes — hide region
                if (regionObject != null)
                    regionObject.SetActive(false);
                if (combinedMesh != null)
                {
                    Object.Destroy(combinedMesh);
                    combinedMesh = null;
                }
                return;
            }

            // Create or update the combined mesh
            if (combinedMesh == null)
                combinedMesh = new Mesh { name = $"Region_{regionKey}", indexFormat = IndexFormat.UInt32 };
            else
                combinedMesh.Clear();

            try
            {
                combinedMesh.CombineMeshes(combines.ToArray(), true, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ChunkRegion] CombineMeshes failed for region {regionKey}: {e.Message}");
                return;
            }

            // Create or update the region GameObject
            if (regionObject == null)
            {
                regionObject = new GameObject($"Region ({regionKey.x},{regionKey.y},{regionKey.z})");
                regionObject.transform.SetParent(parent);
                regionObject.transform.position = Vector3.zero;
                var mf = regionObject.AddComponent<MeshFilter>();
                var mr = regionObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mf.sharedMesh = combinedMesh;
            }
            else
            {
                regionObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
                regionObject.SetActive(true);
            }
        }

        /// <summary>
        /// Cleans up the region's resources.
        /// </summary>
        public void Destroy()
        {
            if (combinedMesh != null) Object.Destroy(combinedMesh);
            if (regionObject != null) Object.Destroy(regionObject);
        }
    }
}
