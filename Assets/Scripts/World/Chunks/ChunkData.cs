using UnityEngine;
using Voidborne.World.Biomes;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// A ground-surface sample point for decoration placement.
    /// Generated during terrain mesh building (background thread, pure math).
    /// </summary>
    public struct SurfacePoint
    {
        public Vector3         worldPos;
        public Vector3         normal;
        public BiomeDefinition biome;
        public float           roadInfluence;
        public bool            underwater;
        public byte            oreType;
    }

    /// <summary>
    /// Plain C# data container for a single chunk's voxel data and state.
    /// Not a MonoBehaviour — this is purely a data holder.
    /// </summary>
    public class ChunkData
    {
        public const int SIZE = 32;
        public const int VOLUME = SIZE * SIZE * SIZE;

        public Vector3Int chunkPosition;
        public float[] densityField;

        /// <summary>
        /// Palette-compressed per-voxel ore/terrain type IDs.
        /// Use OreField.Get(index) / OreField.Set(index, value) for access.
        /// Use OreFieldRaw for bulk NativeArray interop (returns flat byte[]).
        /// </summary>
        public PaletteStorage OreField;

        /// <summary>
        /// Palette-compressed per-voxel biome IDs (biomeIdByte from BiomeDefinition).
        /// Pre-computed alongside ore/terrain types by VoxelClassificationJob so the
        /// mesh builder can look up colors without re-sampling noise.
        /// </summary>
        public PaletteStorage BiomeField;

        /// <summary>
        /// GPU-computed surface heights (32×32) matching the terrain the GPU actually generated.
        /// Null when the CPU fallback path is used.
        /// </summary>
        public float[] gpuSurfaceHeights;
        public ChunkState state;
        public Mesh mesh;
        public bool isDirty;

        /// <summary>
        /// 15-bit visibility graph for occlusion culling.
        /// Each bit indicates whether two faces of this chunk are connected through air.
        /// 0xFFFF = all faces connected (fully open or deformed chunk).
        /// Computed by ChunkVisibilityJob after density readback.
        /// </summary>
        public ushort visibilityGraph = 0xFFFF; // default: assume fully connected (safe)

        /// <summary>
        /// LOD level of this chunk. 0 = full detail, 1+ = reduced detail.
        /// LOD1+ skip colliders and decorations.
        /// </summary>
        public int lodLevel;

        /// <summary>
        /// Pre-computed surface points for decoration placement (trees, rocks, grass).
        /// Generated during terrain mesh building on a background thread.
        /// Only populated for chunks that have upward-facing terrain surfaces.
        /// WorldDecorationManager reads this directly — no runtime raycasting needed.
        /// </summary>
        public SurfacePoint[] surfacePoints;

        /// <summary>
        /// Per-vertex sky exposure (0 = underground, 1 = open sky).
        /// Computed during mesh building from surface heights.
        /// Used by CompactMesh to write the skyExposure vertex byte.
        /// </summary>
        public float[] skyExposure;

        /// <summary>
        /// Pre-classified triangle face buckets for per-face backface culling.
        /// LOD0 only. Null for LOD1+.
        /// </summary>
        public DirectionalSubmeshBuilder.FaceBuckets faceBuckets;

        /// <summary>
        /// World-space position of this chunk's origin corner.
        /// </summary>
        public Vector3 WorldPosition => new Vector3(
            chunkPosition.x * SIZE,
            chunkPosition.y * SIZE,
            chunkPosition.z * SIZE
        );

        public ChunkData(Vector3Int chunkPosition)
        {
            this.chunkPosition = chunkPosition;
            densityField = new float[VOLUME];
            OreField = new PaletteStorage();
            BiomeField = new PaletteStorage();
            state = ChunkState.Unloaded;
            mesh = null;
            isDirty = true;
        }

        /// <summary>
        /// Gets the density value at the given local coordinates.
        /// Index layout: x + y * SIZE + z * SIZE * SIZE
        /// </summary>
        public float GetDensity(int x, int y, int z)
        {
            return densityField[x + y * SIZE + z * SIZE * SIZE];
        }

        /// <summary>
        /// Sets the density value at the given local coordinates.
        /// </summary>
        public void SetDensity(int x, int y, int z, float value)
        {
            densityField[x + y * SIZE + z * SIZE * SIZE] = value;
            isDirty = true;
        }

    }
}
