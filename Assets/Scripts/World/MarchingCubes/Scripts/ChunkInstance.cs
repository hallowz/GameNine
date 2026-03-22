/*
    Author: Gus Tahara-Edmonds (original)
    Modified for Unity 6.3 compatibility
    Purpose: Used to communicate and hold some data for one specific chunk.
*/

using UnityEngine;

namespace Voidborne.World.MarchingCubes
{
    public class ChunkInstance : MonoBehaviour
    {
        public Mesh mesh;
        public MeshRenderer rend;
        public Vector3 coord;
        public int lodIndex;
        public new MeshCollider collider;

        // When this chunk is created, function is called to make sure chunk is setup properly
        public void Setup(Vector3 worldPos, int lodModifier, bool useCollisions)
        {
            coord = worldPos;
            this.lodIndex = lodModifier;

            rend = gameObject.AddComponent<MeshRenderer>();
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();

            mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            filter.mesh = mesh;

            if (useCollisions)
            {
                collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        // Sets the material this chunk uses to render
        public void SetMat(Material mat)
        {
            rend.material = mat;
        }
    }
}
