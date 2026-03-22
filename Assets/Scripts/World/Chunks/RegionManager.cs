using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Manages chunk regions — groups of LOD1+ chunks whose meshes are combined
    /// into single draw calls. Reduces draw calls from ~2000 to ~50-100 for
    /// distant terrain.
    ///
    /// LOD0 chunks are NOT batched — they need individual colliders, deformation
    /// support, and per-face backface culling.
    /// </summary>
    public class RegionManager
    {
        private readonly Dictionary<Vector3Int, ChunkRegion> regions = new Dictionary<Vector3Int, ChunkRegion>();
        private readonly List<Vector3Int> dirtyRegions = new List<Vector3Int>();
        private Transform regionParent;
        private Material material;

        /// <summary>Max region rebuilds per frame to prevent spikes.</summary>
        private const int MAX_REBUILDS_PER_FRAME = 2;

        public void Initialize(Transform parent, Material mat)
        {
            regionParent = parent;
            material = mat;
        }

        /// <summary>
        /// Called when a LOD1+ chunk becomes active. Adds it to its region.
        /// </summary>
        public void OnChunkActivated(ChunkData chunk)
        {
            if (chunk.lodLevel == 0) return; // LOD0 not batched

            Vector3Int key = ChunkRegion.ChunkToRegionKey(chunk.chunkPosition);
            if (!regions.TryGetValue(key, out ChunkRegion region))
            {
                region = new ChunkRegion { regionKey = key };
                regions[key] = region;
            }

            if (!region.chunks.Contains(chunk))
                region.chunks.Add(chunk);

            MarkDirty(key);
        }

        /// <summary>
        /// Called when a chunk is deactivated or unloaded.
        /// </summary>
        public void OnChunkDeactivated(ChunkData chunk)
        {
            Vector3Int key = ChunkRegion.ChunkToRegionKey(chunk.chunkPosition);
            if (!regions.TryGetValue(key, out ChunkRegion region)) return;

            region.chunks.Remove(chunk);
            if (region.chunks.Count == 0)
            {
                region.Destroy();
                regions.Remove(key);
            }
            else
            {
                MarkDirty(key);
            }
        }

        /// <summary>
        /// Called per frame to rebuild dirty regions (throttled).
        /// </summary>
        public void Update()
        {
            int rebuilt = 0;
            for (int i = dirtyRegions.Count - 1; i >= 0 && rebuilt < MAX_REBUILDS_PER_FRAME; i--)
            {
                Vector3Int key = dirtyRegions[i];
                if (regions.TryGetValue(key, out ChunkRegion region))
                {
                    region.Rebuild(material, regionParent);
                    rebuilt++;
                }
                dirtyRegions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Toggles individual chunk MeshRenderers for LOD1+ chunks that are batched.
        /// When a chunk is part of a region, its individual renderer should be disabled
        /// to avoid double rendering.
        /// </summary>
        public bool IsChunkBatched(Vector3Int chunkPos)
        {
            Vector3Int key = ChunkRegion.ChunkToRegionKey(chunkPos);
            if (!regions.TryGetValue(key, out ChunkRegion region)) return false;
            return region.regionObject != null && region.regionObject.activeSelf;
        }

        /// <summary>
        /// Cleans up all regions.
        /// </summary>
        public void Dispose()
        {
            foreach (var region in regions.Values)
                region.Destroy();
            regions.Clear();
            dirtyRegions.Clear();
        }

        private void MarkDirty(Vector3Int key)
        {
            if (!dirtyRegions.Contains(key))
                dirtyRegions.Add(key);

            if (regions.TryGetValue(key, out ChunkRegion region))
                region.isDirty = true;
        }

        public int RegionCount => regions.Count;
    }
}
