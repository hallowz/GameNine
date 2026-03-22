using NUnit.Framework;
using UnityEngine;
using Voidborne.World.Chunks;

namespace Voidborne.Tests.EditMode
{
    public class ChunkCoordTests
    {
        // --- WorldToChunkPos ---

        [Test]
        public void WorldToChunkPos_PositivePosition_ReturnsCorrectChunk()
        {
            Vector3 worldPos = new Vector3(50f, 10f, 70f);
            Vector3Int result = ChunkCoordUtility.WorldToChunkPos(worldPos);
            // 50 / 32 = 1, 10 / 32 = 0, 70 / 32 = 2
            Assert.AreEqual(new Vector3Int(1, 0, 2), result);
        }

        [Test]
        public void WorldToChunkPos_NegativePosition_FloorsCorrectly()
        {
            Vector3 worldPos = new Vector3(-1f, -33f, -0.5f);
            Vector3Int result = ChunkCoordUtility.WorldToChunkPos(worldPos);
            // -1 / 32 = -1 (floor), -33 / 32 = -2 (floor), -0.5 / 32 = -1 (floor)
            Assert.AreEqual(new Vector3Int(-1, -2, -1), result);
        }

        [Test]
        public void WorldToChunkPos_OnBoundary_ReturnsBoundaryChunk()
        {
            Vector3 worldPos = new Vector3(32f, 64f, 0f);
            Vector3Int result = ChunkCoordUtility.WorldToChunkPos(worldPos);
            Assert.AreEqual(new Vector3Int(1, 2, 0), result);
        }

        [Test]
        public void WorldToChunkPos_Origin_ReturnsZero()
        {
            Vector3 worldPos = Vector3.zero;
            Vector3Int result = ChunkCoordUtility.WorldToChunkPos(worldPos);
            Assert.AreEqual(Vector3Int.zero, result);
        }

        // --- ChunkToWorldPos ---

        [Test]
        public void ChunkToWorldPos_ReturnsChunkOrigin()
        {
            Vector3Int chunkPos = new Vector3Int(2, -1, 3);
            Vector3 result = ChunkCoordUtility.ChunkToWorldPos(chunkPos);
            Assert.AreEqual(new Vector3(64f, -32f, 96f), result);
        }

        [Test]
        public void ChunkToWorldPos_IsInverseOfWorldToChunkPos()
        {
            // A position exactly at a chunk origin should round-trip cleanly.
            Vector3Int chunkPos = new Vector3Int(3, -2, 1);
            Vector3 worldPos = ChunkCoordUtility.ChunkToWorldPos(chunkPos);
            Vector3Int backToChunk = ChunkCoordUtility.WorldToChunkPos(worldPos);
            Assert.AreEqual(chunkPos, backToChunk);
        }

        // --- Round-trip ---

        [Test]
        public void RoundTrip_ChunkToWorldToChunk_GivesChunkOrigin()
        {
            // For an arbitrary world position, converting to chunk pos and back
            // should give the origin of the chunk that contains it.
            Vector3 worldPos = new Vector3(45f, -10f, 100f);
            Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(worldPos);
            Vector3 chunkOrigin = ChunkCoordUtility.ChunkToWorldPos(chunkPos);

            // The origin should be <= the world pos and within one chunk size
            Assert.LessOrEqual(chunkOrigin.x, worldPos.x);
            Assert.LessOrEqual(chunkOrigin.y, worldPos.y);
            Assert.LessOrEqual(chunkOrigin.z, worldPos.z);
            Assert.Greater(chunkOrigin.x + ChunkData.SIZE, worldPos.x);
            Assert.Greater(chunkOrigin.y + ChunkData.SIZE, worldPos.y);
            Assert.Greater(chunkOrigin.z + ChunkData.SIZE, worldPos.z);
        }

        // --- LocalToWorld ---

        [Test]
        public void LocalToWorld_ConvertsCorrectly()
        {
            Vector3Int chunkPos = new Vector3Int(1, 0, -1);
            Vector3Int result = ChunkCoordUtility.LocalToWorld(chunkPos, 5, 10, 3);
            // (1*32+5, 0*32+10, -1*32+3) = (37, 10, -29)
            Assert.AreEqual(new Vector3Int(37, 10, -29), result);
        }

        [Test]
        public void LocalToWorld_AtOriginChunk_EqualsLocalCoords()
        {
            Vector3Int result = ChunkCoordUtility.LocalToWorld(Vector3Int.zero, 7, 15, 31);
            Assert.AreEqual(new Vector3Int(7, 15, 31), result);
        }

        // --- ChunkData density ---

        [Test]
        public void ChunkData_DensityArray_IsCorrectSize()
        {
            ChunkData data = new ChunkData(Vector3Int.zero);
            Assert.AreEqual(ChunkData.VOLUME, data.densityField.Length);
            Assert.AreEqual(32768, data.densityField.Length);
        }

        [Test]
        public void ChunkData_SetGetDensity_RoundTrip()
        {
            ChunkData data = new ChunkData(Vector3Int.zero);
            data.SetDensity(5, 10, 20, 0.75f);
            float result = data.GetDensity(5, 10, 20);
            Assert.AreEqual(0.75f, result, 0.0001f);
        }

        [Test]
        public void ChunkData_SetDensity_DoesNotAffectOtherCells()
        {
            ChunkData data = new ChunkData(Vector3Int.zero);
            data.SetDensity(0, 0, 0, 1.0f);
            data.SetDensity(31, 31, 31, -1.0f);

            Assert.AreEqual(1.0f, data.GetDensity(0, 0, 0), 0.0001f);
            Assert.AreEqual(-1.0f, data.GetDensity(31, 31, 31), 0.0001f);
            Assert.AreEqual(0.0f, data.GetDensity(15, 15, 15), 0.0001f);
        }

        [Test]
        public void ChunkData_WorldPosition_ReturnsCorrectValue()
        {
            ChunkData data = new ChunkData(new Vector3Int(2, -1, 3));
            Assert.AreEqual(new Vector3(64f, -32f, 96f), data.WorldPosition);
        }

        [Test]
        public void ChunkData_InitialState_IsUnloaded()
        {
            ChunkData data = new ChunkData(Vector3Int.zero);
            Assert.AreEqual(ChunkState.Unloaded, data.state);
        }

        [Test]
        public void ChunkData_InitialIsDirty_IsTrue()
        {
            ChunkData data = new ChunkData(Vector3Int.zero);
            Assert.IsTrue(data.isDirty);
        }
    }
}
