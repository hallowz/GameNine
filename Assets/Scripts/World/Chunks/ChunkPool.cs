using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Object pool for chunk GameObjects. Reuses deactivated chunks instead of
    /// Instantiate/Destroy to reduce GC pressure and improve performance.
    /// </summary>
    public class ChunkPool
    {
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly Transform poolParent;
        private readonly Material defaultMaterial;

        public ChunkPool(Transform parent, Material defaultMaterial)
        {
            poolParent = parent;
            this.defaultMaterial = defaultMaterial;
        }

        /// <summary>
        /// Get a chunk GameObject from the pool, or create a new one if the pool is empty.
        /// The returned GameObject is active and ready to be initialized.
        /// </summary>
        public GameObject Get()
        {
            GameObject go;

            if (pool.Count > 0)
            {
                go = pool.Pop();
                go.SetActive(true);
            }
            else
            {
                go = CreateChunkGameObject();
            }

            return go;
        }

        /// <summary>
        /// Return a chunk GameObject to the pool for reuse.
        /// Clears its mesh and deactivates it.
        /// </summary>
        public void Return(GameObject go)
        {
            if (go == null) return;

            // Clear mesh data
            var renderer = go.GetComponent<ChunkRenderer>();
            if (renderer != null)
            {
                renderer.ApplyMesh(null);
            }

            go.SetActive(false);
            go.transform.SetParent(poolParent);
            pool.Push(go);
        }

        /// <summary>
        /// Destroy all pooled GameObjects. Call on shutdown.
        /// </summary>
        public void Clear()
        {
            while (pool.Count > 0)
            {
                var go = pool.Pop();
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
        }

        public int PooledCount => pool.Count;

        private GameObject CreateChunkGameObject()
        {
            var go = new GameObject("Chunk (pooled)");
            go.transform.SetParent(poolParent);
            go.layer = LayerMask.NameToLayer("Terrain");

            // ChunkRenderer has RequireComponent for MeshFilter, MeshRenderer, MeshCollider
            var chunkRenderer = go.AddComponent<ChunkRenderer>();

            if (defaultMaterial != null)
                chunkRenderer.SetMaterial(defaultMaterial);

            return go;
        }
    }
}
