using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Static utility class for converting between world, chunk, and local voxel coordinates.
    /// </summary>
    public static class ChunkCoordUtility
    {
        /// <summary>
        /// Converts a world-space position to the chunk coordinate that contains it.
        /// Uses floor division so negative positions map correctly.
        /// </summary>
        public static Vector3Int WorldToChunkPos(Vector3 worldPos)
        {
            return new Vector3Int(
                FloorDiv(worldPos.x, ChunkData.SIZE),
                FloorDiv(worldPos.y, ChunkData.SIZE),
                FloorDiv(worldPos.z, ChunkData.SIZE)
            );
        }

        /// <summary>
        /// Converts a chunk coordinate to the world-space origin of that chunk.
        /// </summary>
        public static Vector3 ChunkToWorldPos(Vector3Int chunkPos)
        {
            return new Vector3(
                chunkPos.x * ChunkData.SIZE,
                chunkPos.y * ChunkData.SIZE,
                chunkPos.z * ChunkData.SIZE
            );
        }

        /// <summary>
        /// Converts local voxel coordinates within a chunk to world-space voxel coordinates.
        /// </summary>
        public static Vector3Int LocalToWorld(Vector3Int chunkPos, int x, int y, int z)
        {
            return new Vector3Int(
                chunkPos.x * ChunkData.SIZE + x,
                chunkPos.y * ChunkData.SIZE + y,
                chunkPos.z * ChunkData.SIZE + z
            );
        }

        private static int FloorDiv(float value, int divisor)
        {
            int i = Mathf.FloorToInt(value);
            // FloorToInt already handles negative values correctly for the numerator,
            // but we need integer floor division for the result.
            if (i < 0 && i % divisor != 0)
                return (i / divisor) - 1;
            return i / divisor;
        }
    }
}
