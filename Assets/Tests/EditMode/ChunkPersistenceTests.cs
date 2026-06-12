using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Voidborne.World.Chunks;
using Voidborne.World.Persistence;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Tests for the chunk edit persistence layer (terrain overhaul P2.1):
    /// ChunkSerializer blob round-trips, RegionFile IO, and the
    /// WorldPersistence stash → flush → reload → apply cycle.
    /// </summary>
    public class ChunkPersistenceTests
    {
        private string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "VoidbornePersistTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        // ------------------------------------------------------------------
        //  ChunkSerializer
        // ------------------------------------------------------------------

        [Test]
        public void Serializer_RoundTripsDensityAndOreEdits()
        {
            var density = new Dictionary<int, float> { { 0, -1.5f }, { 31, 0.25f }, { 32767, 3.75f } };
            var ore = new Dictionary<int, byte> { { 5, 0 }, { 1000, 4 } };

            byte[] blob = ChunkSerializer.Serialize(density, ore);
            var densityOut = new Dictionary<int, float>();
            var oreOut = new Dictionary<int, byte>();
            bool ok = ChunkSerializer.Deserialize(blob, densityOut, oreOut);

            Assert.IsTrue(ok);
            CollectionAssert.AreEquivalent(density, densityOut);
            CollectionAssert.AreEquivalent(ore, oreOut);
        }

        [Test]
        public void Serializer_HandlesNullAndEmptyDictionaries()
        {
            byte[] blob = ChunkSerializer.Serialize(null, null);
            var densityOut = new Dictionary<int, float>();
            var oreOut = new Dictionary<int, byte>();

            Assert.IsTrue(ChunkSerializer.Deserialize(blob, densityOut, oreOut));
            Assert.AreEqual(0, densityOut.Count);
            Assert.AreEqual(0, oreOut.Count);
        }

        [Test]
        public void Serializer_RejectsGarbageWithoutThrowing()
        {
            var densityOut = new Dictionary<int, float> { { 1, 1f } };
            var oreOut = new Dictionary<int, byte> { { 2, 2 } };

            Assert.IsFalse(ChunkSerializer.Deserialize(new byte[] { 99 }, densityOut, oreOut));
            // Failed deserialize must not leave stale data behind
            Assert.AreEqual(0, densityOut.Count);
            Assert.AreEqual(0, oreOut.Count);
            Assert.IsFalse(ChunkSerializer.Deserialize(null, densityOut, oreOut));
        }

        // ------------------------------------------------------------------
        //  RegionFile
        // ------------------------------------------------------------------

        [Test]
        public void RegionKey_FloorDividesNegativeCoordinates()
        {
            Assert.AreEqual(new Vector3Int(0, 0, 0), RegionFile.RegionKey(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), RegionFile.RegionKey(new Vector3Int(15, 15, 15)));
            Assert.AreEqual(new Vector3Int(1, 0, 0), RegionFile.RegionKey(new Vector3Int(16, 0, 0)));
            Assert.AreEqual(new Vector3Int(-1, -1, -1), RegionFile.RegionKey(new Vector3Int(-1, -1, -1)));
            Assert.AreEqual(new Vector3Int(-1, 0, 0), RegionFile.RegionKey(new Vector3Int(-16, 0, 0)));
            Assert.AreEqual(new Vector3Int(-2, 0, 0), RegionFile.RegionKey(new Vector3Int(-17, 0, 0)));
        }

        [Test]
        public void RegionFile_RoundTripsBlobs()
        {
            string path = RegionFile.GetPath(tempDir, new Vector3Int(1, -2, 3));
            var blobs = new Dictionary<Vector3Int, byte[]>
            {
                { new Vector3Int(16, -32, 48), new byte[] { 1, 2, 3 } },
                { new Vector3Int(17, -31, 49), new byte[] { 0xFF } },
            };

            RegionFile.Save(path, blobs);
            var loaded = RegionFile.Load(path);

            Assert.AreEqual(2, loaded.Count);
            CollectionAssert.AreEqual(blobs[new Vector3Int(16, -32, 48)], loaded[new Vector3Int(16, -32, 48)]);
            CollectionAssert.AreEqual(blobs[new Vector3Int(17, -31, 49)], loaded[new Vector3Int(17, -31, 49)]);
        }

        [Test]
        public void RegionFile_MissingFileLoadsEmpty()
        {
            var loaded = RegionFile.Load(Path.Combine(tempDir, "does_not_exist.vbr"));
            Assert.AreEqual(0, loaded.Count);
        }

        [Test]
        public void RegionFile_EmptyDictionaryDeletesFile()
        {
            string path = RegionFile.GetPath(tempDir, Vector3Int.zero);
            RegionFile.Save(path, new Dictionary<Vector3Int, byte[]>
            {
                { Vector3Int.zero, new byte[] { 1 } }
            });
            Assert.IsTrue(File.Exists(path));

            RegionFile.Save(path, new Dictionary<Vector3Int, byte[]>());
            Assert.IsFalse(File.Exists(path));
        }

        // ------------------------------------------------------------------
        //  ChunkData edit recording
        // ------------------------------------------------------------------

        [Test]
        public void RecordDensityEdit_UpdatesFieldAndOverlay()
        {
            var chunk = new ChunkData(new Vector3Int(1, 2, 3));
            chunk.RecordDensityEdit(4, 5, 6, -2.5f);

            int index = 4 + 5 * ChunkData.SIZE + 6 * ChunkData.SIZE * ChunkData.SIZE;
            Assert.AreEqual(-2.5f, chunk.densityField[index]);
            Assert.AreEqual(-2.5f, chunk.densityEdits[index]);
            Assert.IsTrue(chunk.HasEdits);
            Assert.IsTrue(chunk.hasUnsavedEdits);
            Assert.IsTrue(chunk.isDirty);
        }

        [Test]
        public void RecordOreEdit_UpdatesOreFieldAndOverlay()
        {
            var chunk = new ChunkData(new Vector3Int(0, 0, 0));
            chunk.OreField.Fill(7);
            chunk.RecordOreEdit(123, 0);

            Assert.AreEqual(0, chunk.OreField.Get(123));
            Assert.AreEqual((byte)0, chunk.oreEdits[123]);
            Assert.IsTrue(chunk.HasEdits);
        }

        [Test]
        public void PlainSetDensity_DoesNotRecordEdits()
        {
            var chunk = new ChunkData(new Vector3Int(0, 0, 0));
            chunk.SetDensity(1, 1, 1, 5f); // generation fill path

            Assert.IsFalse(chunk.HasEdits);
            Assert.IsFalse(chunk.hasUnsavedEdits);
        }

        // ------------------------------------------------------------------
        //  WorldPersistence end-to-end
        // ------------------------------------------------------------------

        [Test]
        public void Persistence_StashFlushReloadApply_RestoresEdits()
        {
            WorldPersistence.InitializeAt(tempDir);

            var pos = new Vector3Int(5, -3, 12);
            var chunk = new ChunkData(pos);
            chunk.RecordDensityEdit(0, 0, 0, -4f);
            chunk.RecordDensityEdit(10, 11, 12, 2f);
            chunk.OreField.Fill(7);
            chunk.RecordOreEdit(999, 0);

            WorldPersistence.StashChunk(chunk);
            Assert.IsFalse(chunk.hasUnsavedEdits, "Stash should clear the unsaved flag");
            WorldPersistence.FlushSync();

            // Simulate a fresh session: re-init wipes the in-memory cache,
            // forcing a re-read from the region file on disk.
            WorldPersistence.InitializeAt(tempDir);

            var reloaded = new ChunkData(pos); // freshly "generated" (pristine field)
            bool had = WorldPersistence.ApplySavedEdits(reloaded);

            Assert.IsTrue(had);
            int idx0 = 0;
            int idx1 = 10 + 11 * ChunkData.SIZE + 12 * ChunkData.SIZE * ChunkData.SIZE;
            Assert.AreEqual(-4f, reloaded.densityField[idx0]);
            Assert.AreEqual(2f, reloaded.densityField[idx1]);

            // Ore edits are seeded but applied separately after classification
            Assert.AreEqual((byte)0, reloaded.oreEdits[999]);
            reloaded.OreField.Fill(7); // simulate classification output
            WorldPersistence.ApplyOreEdits(reloaded);
            Assert.AreEqual(0, reloaded.OreField.Get(999));
        }

        [Test]
        public void Persistence_ReloadedChunkCarriesOldEditsIntoNextSave()
        {
            WorldPersistence.InitializeAt(tempDir);

            var pos = new Vector3Int(0, 0, 0);
            var chunk = new ChunkData(pos);
            chunk.RecordDensityEdit(1, 1, 1, -1f);
            WorldPersistence.StashChunk(chunk);
            WorldPersistence.FlushSync();

            // New session, chunk reloads and the player makes a SECOND edit
            WorldPersistence.InitializeAt(tempDir);
            var reloaded = new ChunkData(pos);
            WorldPersistence.ApplySavedEdits(reloaded);
            reloaded.RecordDensityEdit(2, 2, 2, -2f);
            WorldPersistence.StashChunk(reloaded);
            WorldPersistence.FlushSync();

            // Third session: BOTH edits must survive
            WorldPersistence.InitializeAt(tempDir);
            var third = new ChunkData(pos);
            Assert.IsTrue(WorldPersistence.ApplySavedEdits(third));
            int idxA = 1 + 1 * ChunkData.SIZE + 1 * ChunkData.SIZE * ChunkData.SIZE;
            int idxB = 2 + 2 * ChunkData.SIZE + 2 * ChunkData.SIZE * ChunkData.SIZE;
            Assert.AreEqual(-1f, third.densityField[idxA]);
            Assert.AreEqual(-2f, third.densityField[idxB]);
        }

        [Test]
        public void Persistence_LodRegeneration_PreservesUnsavedEdits()
        {
            WorldPersistence.InitializeAt(tempDir);

            var pos = new Vector3Int(2, 2, 2);
            var chunk = new ChunkData(pos);
            chunk.RecordDensityEdit(3, 3, 3, -9f); // edit never stashed/flushed

            // Simulate a LOD-change regeneration on the SAME ChunkData: the GPU
            // overwrites the field with pristine data, then the generation callback
            // calls ApplySavedEdits — the unsaved edit must survive.
            int idx = 3 + 3 * ChunkData.SIZE + 3 * ChunkData.SIZE * ChunkData.SIZE;
            chunk.densityField[idx] = 0.5f; // "pristine" regenerated value

            Assert.IsTrue(WorldPersistence.ApplySavedEdits(chunk));
            Assert.AreEqual(-9f, chunk.densityField[idx],
                "Unsaved edits must be stashed before the saved blob is re-applied");
        }

        [Test]
        public void Persistence_PristineChunkWritesNothing()
        {
            WorldPersistence.InitializeAt(tempDir);

            var chunk = new ChunkData(new Vector3Int(7, 7, 7));
            WorldPersistence.StashChunk(chunk); // no edits — must be a no-op
            WorldPersistence.FlushSync();

            Assert.AreEqual(0, Directory.GetFiles(tempDir).Length,
                "An unmodified world must produce zero region files");
        }

        [Test]
        public void Persistence_ApplySavedEditsOnUneditedChunkReturnsFalse()
        {
            WorldPersistence.InitializeAt(tempDir);
            var chunk = new ChunkData(new Vector3Int(9, 9, 9));
            Assert.IsFalse(WorldPersistence.ApplySavedEdits(chunk));
            Assert.IsFalse(chunk.HasEdits);
        }
    }
}
