using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Voidborne.World.Biomes;
using Voidborne.World.Generation;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// MonoBehaviour attached to each chunk GameObject.
    /// Holds references to the rendering and collision components.
    /// Receives a mesh from ChunkMeshBuilder and applies it.
    /// Supports per-chunk biome material properties via MaterialPropertyBlock.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        /// <summary>
        /// The ChunkData this renderer is displaying.
        /// </summary>
        public ChunkData ChunkData { get; private set; }

        /// <summary>
        /// The mesh currently assigned to the MeshFilter (may differ from ChunkData.mesh
        /// when a new mesh has been generated but not yet applied).
        /// </summary>
        public Mesh CurrentMesh => meshFilter != null ? meshFilter.sharedMesh : null;

        // Visibility is the AND of two independent systems: occlusion culling
        // (SetVisible) and region batching (SetBatched). Tracking both here keeps
        // them from fighting over MeshRenderer.enabled.
        private bool occlusionVisible = true;
        private bool isBatched;

        /// <summary>True while this chunk's mesh is rendered by a combined region mesh.</summary>
        public bool IsBatched => isBatched;

        private void UpdateRendererEnabled()
        {
            if (meshRenderer == null || meshFilter == null) return;
            meshRenderer.enabled = occlusionVisible && !isBatched && meshFilter.sharedMesh != null;
        }

        /// <summary>
        /// Marks this chunk as rendered (or not) by a combined region mesh.
        /// While batched, the individual MeshRenderer stays disabled regardless
        /// of occlusion culling, preventing double rendering / z-fighting.
        /// </summary>
        public void SetBatched(bool batched)
        {
            isBatched = batched;
            UpdateRendererEnabled();
        }

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
        }

        /// <summary>
        /// Initialize this renderer with a ChunkData reference.
        /// Sets the GameObject position to match the chunk's world position.
        /// </summary>
        public void Initialize(ChunkData chunkData)
        {
            ChunkData = chunkData;
            transform.position = chunkData.WorldPosition;
            gameObject.name = $"Chunk ({chunkData.chunkPosition.x}, {chunkData.chunkPosition.y}, {chunkData.chunkPosition.z})";
        }

        /// <summary>
        /// Apply a mesh to the MeshFilter and MeshCollider.
        /// Also sets up per-chunk biome material properties.
        /// Transitions the chunk state to Active.
        /// LOD chunks (lodLevel > 0) skip the collider for performance.
        /// </summary>
        public void ApplyMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                // Empty chunk (all air or all solid) — hide renderer, clear collider.
                // Also resets visibility flags: this path runs on pool return, and a
                // recycled GameObject must not inherit a previous chunk's batched state.
                meshFilter.sharedMesh = null;
                meshCollider.sharedMesh = null;
                occlusionVisible = true;
                isBatched = false;
                meshRenderer.enabled = false;

                if (ChunkData != null)
                {
                    ChunkData.state = ChunkState.Active;
                }

                return;
            }

            meshFilter.sharedMesh = mesh;
            occlusionVisible = true;
            // isBatched is intentionally preserved: if this chunk is in a combined
            // region, the region rebuild (already queued by the caller) decides
            // whether it renders individually again. Enabling it here would
            // z-fight with the region's copy for a few frames.
            UpdateRendererEnabled();

            // LOD chunks skip the collider entirely — huge perf saving
            bool useCollider = ChunkData == null || ChunkData.lodLevel == 0;
            if (useCollider)
            {
                // Bake collision data on a background thread (saves 5-50ms main thread stall).
                // Physics.BakeMesh is thread-safe in Unity 2022.2+/Unity 6.
                meshCollider.sharedMesh = null;
                int meshId = mesh.GetInstanceID();
                Mesh capturedMesh = mesh;
                MeshCollider capturedCollider = meshCollider;
                Task.Run(() =>
                {
                    Physics.BakeMesh(meshId, false);
                    // Use ChunkCallbackQueue with high priority instead of unbounded ctx.Post.
                    // This ensures collider assignments are budgeted and don't compete
                    // with mesh generation callbacks for main-thread time.
                    ChunkCallbackQueue.EnqueueHigh(() =>
                    {
                        // Guard: chunk may have been recycled/destroyed during bake
                        if (capturedCollider != null && capturedMesh != null)
                            capturedCollider.sharedMesh = capturedMesh;
                    });
                });
            }
            else
            {
                meshCollider.sharedMesh = null;
            }

            // Biome color tints are set once on the shared material (in ChunkManager)
            // rather than per-chunk via MaterialPropertyBlock, which would break SRP Batching.

            if (ChunkData != null)
            {
                ChunkData.state = ChunkState.Active;
            }
        }

        /// <summary>
        /// Set the material used by the terrain MeshRenderer.
        /// </summary>
        public void SetMaterial(Material material)
        {
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Biome tints are now computed per-vertex and passed via UV3 — no material
        /// properties needed. This method is kept as a no-op for backward compatibility.
        /// </summary>
        public static void ApplyBiomeColorsToMaterial(Material material) { }

        /// <summary>
        /// Toggle MeshRenderer visibility for occlusion culling.
        /// Does not affect chunk state — the chunk remains Active and loaded.
        /// </summary>
        public void SetVisible(bool visible)
        {
            occlusionVisible = visible;
            UpdateRendererEnabled();
        }

        // Face culling is now handled via index buffer rebuild in ChunkManager.UpdateFaceCulling
        // using DirectionalSubmeshBuilder.RebuildIndices — no per-chunk renderer method needed.
    }
}
