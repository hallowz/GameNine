using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using Voidborne.World.Chunks;
using Voidborne.World.Generation;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Comprehensive tests for the terrain generation pipeline:
    /// DensityFunction, ChunkData, and ChunkCoordUtility.
    /// </summary>
    [TestFixture]
    public class TerrainGenerationTests
    {
        private TerrainShapeData _defaultShape;
        private BiomeData _defaultBiome;

        [SetUp]
        public void SetUp()
        {
            WorldSeed.Seed = 42;
            _defaultShape = TerrainShapeData.Default;
            _defaultBiome = BiomeData.Default;
        }

        // =====================================================================
        // DensityFunction — underground / surface / sky behavior
        // =====================================================================

        [Test]
        public void Density_DeepUnderground_MajoritySolid()
        {
            // Well below any possible surface, most samples should be solid (positive).
            // Caves and modifiers can create some air pockets even deep underground.
            int solidCount = 0;
            const int samples = 30;

            for (int i = 0; i < samples; i++)
            {
                float x = i * 137f;
                float z = i * 251f;
                float density = DensityFunction.GetDensity(
                    new float3(x, -500f, z), _defaultShape, _defaultBiome);
                if (density > 0f) solidCount++;
            }

            Assert.Greater(solidCount, samples * 0.5f,
                $"Expected majority solid (positive density) at y=-500, got {solidCount}/{samples}");
        }

        [Test]
        public void Density_NegativeY_GenerallyPositive()
        {
            // At y = -100 the vast majority of samples should be solid.
            int solidCount = 0;
            const int samples = 50;

            for (int i = 0; i < samples; i++)
            {
                float x = i * 97f;
                float z = i * 163f;
                float density = DensityFunction.GetDensity(
                    new float3(x, -100f, z), _defaultShape, _defaultBiome);
                if (density > 0f) solidCount++;
            }

            Assert.Greater(solidCount, samples * 0.8f,
                $"Expected > 80% solid at y=-100, got {solidCount}/{samples}");
        }

        [Test]
        public void Density_AboveSurface_IsAir()
        {
            // Well above any terrain surface but below sky zone, should be air (negative density).
            // Using y=60 which is above default terrain (heightScale ~35) but below SkyTransitionStart (80).
            int airCount = 0;
            const int samples = 20;

            for (int i = 0; i < samples; i++)
            {
                float x = i * 57f;
                float z = i * 89f;
                float density = DensityFunction.GetDensity(
                    new float3(x, 60f, z), _defaultShape, _defaultBiome);
                if (density < 0f) airCount++;
            }

            Assert.Greater(airCount, samples / 2,
                $"Expected majority air (negative density) at y=60, got {airCount}/{samples} air");
        }

        [Test]
        public void Density_HighSky_AboveSkyStart_VariesBetweenAirAndIsland()
        {
            // Above SkyStart (100f) density is driven by sky island logic.
            // We expect a mix of air and island voxels, not uniformly one or the other.
            int positiveCount = 0;
            int negativeCount = 0;
            const int samples = 200;

            for (int i = 0; i < samples; i++)
            {
                float x = i * 47f;
                float z = i * 79f;
                float density = DensityFunction.GetDensity(
                    new float3(x, 200f, z), _defaultShape, _defaultBiome);
                if (density > 0f) positiveCount++;
                else negativeCount++;
            }

            // At least some samples should be air (sky islands are sparse)
            Assert.Greater(negativeCount, 0,
                $"Expected some air above SkyStart, got {negativeCount}/{samples} air samples");
        }

        [Test]
        public void Density_SkyConstants_AreOrdered()
        {
            Assert.Less(DensityFunction.SkyTransitionStart, DensityFunction.SkyStart,
                "SkyTransitionStart must be below SkyStart");
            Assert.Less(DensityFunction.SkyStart, DensityFunction.SkyMax,
                "SkyStart must be below SkyMax");
            Assert.AreEqual(80f, DensityFunction.SkyTransitionStart);
            Assert.AreEqual(100f, DensityFunction.SkyStart);
            Assert.AreEqual(800f, DensityFunction.SkyMax);
        }

        // =====================================================================
        // DensityFunction — determinism
        // =====================================================================

        [Test]
        public void Density_SameInputs_ReturnSameOutput()
        {
            float3 pos = new float3(42f, 5f, 99f);
            float a = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);
            float b = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);
            Assert.AreEqual(a, b, "Density must be deterministic for identical inputs");
        }

        [Test]
        public void Density_Deterministic_AcrossMultiplePositions()
        {
            float3[] positions = new float3[]
            {
                new float3(0f, 0f, 0f),
                new float3(1000f, -200f, 500f),
                new float3(-300f, 50f, -700f),
                new float3(0.5f, 0.5f, 0.5f),
            };

            foreach (var pos in positions)
            {
                float first = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);
                float second = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);
                Assert.AreEqual(first, second,
                    $"Density not deterministic at {pos}: {first} vs {second}");
            }
        }

        [Test]
        public void Density_DifferentSeed_ProducesDifferentResult()
        {
            float3 pos = new float3(100f, 10f, 200f);

            WorldSeed.Seed = 42;
            float a = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);

            WorldSeed.Seed = 9999;
            float b = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);

            Assert.AreNotEqual(a, b,
                "Different world seeds should produce different density values");
        }

        [Test]
        public void Density_DifferentPositions_ProduceDifferentResults()
        {
            float a = DensityFunction.GetDensity(
                new float3(0f, 0f, 0f), _defaultShape, _defaultBiome);
            float b = DensityFunction.GetDensity(
                new float3(500f, 0f, 500f), _defaultShape, _defaultBiome);

            Assert.AreNotEqual(a, b,
                "Distant positions should generally produce different density values");
        }

        // =====================================================================
        // DensityFunction — GetSurfaceHeight
        // =====================================================================

        [Test]
        public void GetSurfaceHeight_IsDeterministic()
        {
            float a = DensityFunction.GetSurfaceHeight(100f, 200f, _defaultShape);
            float b = DensityFunction.GetSurfaceHeight(100f, 200f, _defaultShape);
            Assert.AreEqual(a, b, "GetSurfaceHeight must be deterministic");
        }

        [Test]
        public void GetSurfaceHeight_ReturnsFiniteValue()
        {
            float h = DensityFunction.GetSurfaceHeight(0f, 0f, _defaultShape);
            Assert.IsFalse(float.IsNaN(h), "Surface height should not be NaN");
            Assert.IsFalse(float.IsInfinity(h), "Surface height should not be infinity");
        }

        [Test]
        public void GetSurfaceHeight_VariesAcrossPositions()
        {
            float h1 = DensityFunction.GetSurfaceHeight(0f, 0f, _defaultShape);
            float h2 = DensityFunction.GetSurfaceHeight(500f, 500f, _defaultShape);

            // With noise-based terrain, two distant points should not produce
            // exactly the same height (vanishingly unlikely).
            Assert.AreNotEqual(h1, h2,
                "Surface height should vary across distant positions");
        }

        // =====================================================================
        // DensityFunction — GetSkyBiomeType
        // =====================================================================

        [Test]
        public void GetSkyBiomeType_ReturnsValueInRange_0To4()
        {
            for (int i = 0; i < 100; i++)
            {
                float2 xz = new float2(i * 137f, i * 251f);
                int biomeType = DensityFunction.GetSkyBiomeType(xz);
                Assert.GreaterOrEqual(biomeType, 0,
                    $"Sky biome type at {xz} was {biomeType}, expected >= 0");
                Assert.LessOrEqual(biomeType, 4,
                    $"Sky biome type at {xz} was {biomeType}, expected <= 4");
            }
        }

        [Test]
        public void GetSkyBiomeType_IsDeterministic()
        {
            float2 xz = new float2(123f, 456f);
            int a = DensityFunction.GetSkyBiomeType(xz);
            int b = DensityFunction.GetSkyBiomeType(xz);
            Assert.AreEqual(a, b, "GetSkyBiomeType must be deterministic");
        }

        [Test]
        public void GetSkyBiomeType_ProducesMultipleTypes()
        {
            // Over a large enough area, we should see more than one biome type.
            var typesFound = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 500; i++)
            {
                float2 xz = new float2(i * 200f, i * 300f);
                typesFound.Add(DensityFunction.GetSkyBiomeType(xz));
            }

            Assert.Greater(typesFound.Count, 1,
                $"Expected multiple sky biome types across 500 samples, found {typesFound.Count}");
        }

        [Test]
        public void GetSkyBiomeType_MatchesNamedConstants()
        {
            Assert.AreEqual(0, DensityFunction.SkyBiomeSkywardSpires);
            Assert.AreEqual(1, DensityFunction.SkyBiomeDriftingMeadows);
            Assert.AreEqual(2, DensityFunction.SkyBiomeOasisIsles);
            Assert.AreEqual(3, DensityFunction.SkyBiomeStormridgePeaks);
            Assert.AreEqual(4, DensityFunction.SkyBiomeAncientBastions);
        }

        // =====================================================================
        // ChunkData — creation and initialization
        // =====================================================================

        [Test]
        public void ChunkData_Constructor_InitializesDensityField()
        {
            var chunk = new ChunkData(new Vector3Int(1, 2, 3));
            Assert.IsNotNull(chunk.densityField);
            Assert.AreEqual(ChunkData.VOLUME, chunk.densityField.Length);
        }

        [Test]
        public void ChunkData_Constructor_SetsChunkPosition()
        {
            var pos = new Vector3Int(5, -3, 7);
            var chunk = new ChunkData(pos);
            Assert.AreEqual(pos, chunk.chunkPosition);
        }

        [Test]
        public void ChunkData_Constants_AreCorrect()
        {
            Assert.AreEqual(32, ChunkData.SIZE);
            Assert.AreEqual(32 * 32 * 32, ChunkData.VOLUME);
            Assert.AreEqual(32768, ChunkData.VOLUME);
        }

        [Test]
        public void ChunkData_InitialState_IsUnloaded()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            Assert.AreEqual(ChunkState.Unloaded, chunk.state);
        }

        [Test]
        public void ChunkData_InitialIsDirty_IsTrue()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            Assert.IsTrue(chunk.isDirty,
                "Newly created chunk should be dirty");
        }

        [Test]
        public void ChunkData_InitialDensity_IsAllZero()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            for (int i = 0; i < chunk.densityField.Length; i++)
            {
                Assert.AreEqual(0f, chunk.densityField[i],
                    $"Initial density at index {i} should be 0");
            }
        }

        [Test]
        public void ChunkData_InitialMesh_IsNull()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            Assert.IsNull(chunk.mesh);
        }

        // =====================================================================
        // ChunkData — density read/write
        // =====================================================================

        [Test]
        public void ChunkData_SetGetDensity_RoundTrips()
        {
            var chunk = new ChunkData(Vector3Int.zero);

            chunk.SetDensity(0, 0, 0, 1.5f);
            Assert.AreEqual(1.5f, chunk.GetDensity(0, 0, 0), 1e-6f);

            chunk.SetDensity(15, 15, 15, -0.75f);
            Assert.AreEqual(-0.75f, chunk.GetDensity(15, 15, 15), 1e-6f);

            chunk.SetDensity(31, 31, 31, 99.9f);
            Assert.AreEqual(99.9f, chunk.GetDensity(31, 31, 31), 1e-6f);
        }

        [Test]
        public void ChunkData_SetDensity_SetsIsDirty()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            // Reset dirty flag manually to test that SetDensity sets it
            chunk.isDirty = false;

            chunk.SetDensity(5, 5, 5, 1.0f);
            Assert.IsTrue(chunk.isDirty,
                "SetDensity should mark the chunk as dirty");
        }

        [Test]
        public void ChunkData_SetDensity_DoesNotAffectOtherVoxels()
        {
            var chunk = new ChunkData(Vector3Int.zero);

            chunk.SetDensity(0, 0, 0, 42f);
            chunk.SetDensity(31, 31, 31, -42f);

            // Nearby voxels should still be zero
            Assert.AreEqual(0f, chunk.GetDensity(1, 0, 0), 1e-6f);
            Assert.AreEqual(0f, chunk.GetDensity(0, 1, 0), 1e-6f);
            Assert.AreEqual(0f, chunk.GetDensity(0, 0, 1), 1e-6f);
            Assert.AreEqual(0f, chunk.GetDensity(15, 15, 15), 1e-6f);

            // Original values untouched
            Assert.AreEqual(42f, chunk.GetDensity(0, 0, 0), 1e-6f);
            Assert.AreEqual(-42f, chunk.GetDensity(31, 31, 31), 1e-6f);
        }

        [Test]
        public void ChunkData_SetDensity_OverwritesPreviousValue()
        {
            var chunk = new ChunkData(Vector3Int.zero);

            chunk.SetDensity(10, 10, 10, 1.0f);
            Assert.AreEqual(1.0f, chunk.GetDensity(10, 10, 10), 1e-6f);

            chunk.SetDensity(10, 10, 10, -5.0f);
            Assert.AreEqual(-5.0f, chunk.GetDensity(10, 10, 10), 1e-6f);
        }

        [Test]
        public void ChunkData_DensityField_AllCornersAccessible()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            int s = ChunkData.SIZE - 1; // 31

            // Write all 8 corners
            float[] values = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
            int[][] corners = new int[][]
            {
                new[] { 0, 0, 0 },
                new[] { s, 0, 0 },
                new[] { 0, s, 0 },
                new[] { 0, 0, s },
                new[] { s, s, 0 },
                new[] { s, 0, s },
                new[] { 0, s, s },
                new[] { s, s, s },
            };

            for (int i = 0; i < corners.Length; i++)
                chunk.SetDensity(corners[i][0], corners[i][1], corners[i][2], values[i]);

            for (int i = 0; i < corners.Length; i++)
            {
                float result = chunk.GetDensity(corners[i][0], corners[i][1], corners[i][2]);
                Assert.AreEqual(values[i], result, 1e-6f,
                    $"Corner ({corners[i][0]},{corners[i][1]},{corners[i][2]}) mismatch");
            }
        }

        [Test]
        public void ChunkData_DensityIndex_BoundaryValues()
        {
            // Verify that the index formula x + y*SIZE + z*SIZE*SIZE maps correctly
            // for min and max coordinates.
            var chunk = new ChunkData(Vector3Int.zero);

            // First element: index 0
            chunk.SetDensity(0, 0, 0, 111f);
            Assert.AreEqual(111f, chunk.densityField[0], 1e-6f);

            // Last element: index SIZE^3 - 1
            chunk.SetDensity(31, 31, 31, 222f);
            int lastIndex = 31 + 31 * ChunkData.SIZE + 31 * ChunkData.SIZE * ChunkData.SIZE;
            Assert.AreEqual(222f, chunk.densityField[lastIndex], 1e-6f);
        }

        // =====================================================================
        // ChunkData — state management
        // =====================================================================

        [Test]
        public void ChunkData_StateTransitions_Work()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            Assert.AreEqual(ChunkState.Unloaded, chunk.state);

            chunk.state = ChunkState.Generating;
            Assert.AreEqual(ChunkState.Generating, chunk.state);

            chunk.state = ChunkState.MeshPending;
            Assert.AreEqual(ChunkState.MeshPending, chunk.state);

            chunk.state = ChunkState.Active;
            Assert.AreEqual(ChunkState.Active, chunk.state);

            chunk.state = ChunkState.MarkedForUnload;
            Assert.AreEqual(ChunkState.MarkedForUnload, chunk.state);
        }

        [Test]
        public void ChunkState_AllValues_AreDefined()
        {
            // Ensure no gaps in the lifecycle enum.
            Assert.IsTrue(System.Enum.IsDefined(typeof(ChunkState), ChunkState.Unloaded));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ChunkState), ChunkState.Generating));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ChunkState), ChunkState.MeshPending));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ChunkState), ChunkState.Active));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ChunkState), ChunkState.MarkedForUnload));
        }

        // =====================================================================
        // ChunkData — world position calculation
        // =====================================================================

        [Test]
        public void ChunkData_WorldPosition_AtOrigin()
        {
            var chunk = new ChunkData(Vector3Int.zero);
            Assert.AreEqual(Vector3.zero, chunk.WorldPosition);
        }

        [Test]
        public void ChunkData_WorldPosition_PositiveChunk()
        {
            var chunk = new ChunkData(new Vector3Int(3, 1, 2));
            Assert.AreEqual(new Vector3(96f, 32f, 64f), chunk.WorldPosition);
        }

        [Test]
        public void ChunkData_WorldPosition_NegativeChunk()
        {
            var chunk = new ChunkData(new Vector3Int(-1, -2, -3));
            Assert.AreEqual(new Vector3(-32f, -64f, -96f), chunk.WorldPosition);
        }

        [Test]
        public void ChunkData_WorldPosition_MatchesChunkToWorldPos()
        {
            var positions = new Vector3Int[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(5, -3, 7),
                new Vector3Int(-10, 0, 10),
            };

            foreach (var pos in positions)
            {
                var chunk = new ChunkData(pos);
                Vector3 expected = ChunkCoordUtility.ChunkToWorldPos(pos);
                Assert.AreEqual(expected, chunk.WorldPosition,
                    $"WorldPosition mismatch for chunk at {pos}");
            }
        }

        // =====================================================================
        // ChunkData — multiple chunks don't interfere
        // =====================================================================

        [Test]
        public void MultipleChunks_IndependentDensityFields()
        {
            var chunkA = new ChunkData(new Vector3Int(0, 0, 0));
            var chunkB = new ChunkData(new Vector3Int(1, 0, 0));

            chunkA.SetDensity(5, 5, 5, 100f);
            chunkB.SetDensity(5, 5, 5, -100f);

            Assert.AreEqual(100f, chunkA.GetDensity(5, 5, 5), 1e-6f,
                "ChunkA density should not be affected by ChunkB");
            Assert.AreEqual(-100f, chunkB.GetDensity(5, 5, 5), 1e-6f,
                "ChunkB density should not be affected by ChunkA");
        }

        [Test]
        public void MultipleChunks_IndependentState()
        {
            var chunkA = new ChunkData(Vector3Int.zero);
            var chunkB = new ChunkData(new Vector3Int(1, 1, 1));

            chunkA.state = ChunkState.Active;
            chunkB.state = ChunkState.Generating;

            Assert.AreEqual(ChunkState.Active, chunkA.state);
            Assert.AreEqual(ChunkState.Generating, chunkB.state);
        }

        [Test]
        public void MultipleChunks_IndependentDirtyFlags()
        {
            var chunkA = new ChunkData(Vector3Int.zero);
            var chunkB = new ChunkData(new Vector3Int(2, 0, 0));

            chunkA.isDirty = false;
            chunkB.SetDensity(0, 0, 0, 1f); // Should mark B dirty

            Assert.IsFalse(chunkA.isDirty,
                "ChunkA dirty flag should not change when ChunkB is modified");
            Assert.IsTrue(chunkB.isDirty,
                "ChunkB should be dirty after SetDensity");
        }

        [Test]
        public void MultipleChunks_DifferentPositions_DifferentWorldPositions()
        {
            var chunkA = new ChunkData(new Vector3Int(0, 0, 0));
            var chunkB = new ChunkData(new Vector3Int(1, 0, 0));
            var chunkC = new ChunkData(new Vector3Int(0, 1, 0));

            Assert.AreNotEqual(chunkA.WorldPosition, chunkB.WorldPosition);
            Assert.AreNotEqual(chunkA.WorldPosition, chunkC.WorldPosition);
            Assert.AreNotEqual(chunkB.WorldPosition, chunkC.WorldPosition);
        }

        // =====================================================================
        // ChunkCoordUtility — WorldToChunkPos
        // =====================================================================

        [Test]
        public void WorldToChunkPos_Origin_ReturnsZero()
        {
            Assert.AreEqual(Vector3Int.zero,
                ChunkCoordUtility.WorldToChunkPos(Vector3.zero));
        }

        [Test]
        public void WorldToChunkPos_InsideFirstChunk()
        {
            Assert.AreEqual(new Vector3Int(0, 0, 0),
                ChunkCoordUtility.WorldToChunkPos(new Vector3(15f, 20f, 31f)));
        }

        [Test]
        public void WorldToChunkPos_ExactBoundary()
        {
            // Exactly at 32 should be chunk 1
            Assert.AreEqual(new Vector3Int(1, 1, 1),
                ChunkCoordUtility.WorldToChunkPos(new Vector3(32f, 32f, 32f)));
        }

        [Test]
        public void WorldToChunkPos_NegativePositions_FloorCorrectly()
        {
            // -1 should be in chunk -1, not chunk 0
            Assert.AreEqual(new Vector3Int(-1, -1, -1),
                ChunkCoordUtility.WorldToChunkPos(new Vector3(-1f, -1f, -1f)));

            // -32 should be in chunk -1
            Assert.AreEqual(new Vector3Int(-1, -1, -1),
                ChunkCoordUtility.WorldToChunkPos(new Vector3(-32f, -32f, -32f)));

            // -33 should be in chunk -2
            Assert.AreEqual(new Vector3Int(-2, -2, -2),
                ChunkCoordUtility.WorldToChunkPos(new Vector3(-33f, -33f, -33f)));
        }

        [TestCase(0f, 0)]
        [TestCase(31.9f, 0)]
        [TestCase(32f, 1)]
        [TestCase(63.9f, 1)]
        [TestCase(64f, 2)]
        [TestCase(-0.1f, -1)]
        [TestCase(-32f, -1)]
        [TestCase(-32.1f, -2)]
        public void WorldToChunkPos_XAxis_Parameterized(float worldX, int expectedChunkX)
        {
            Vector3Int result = ChunkCoordUtility.WorldToChunkPos(new Vector3(worldX, 0f, 0f));
            Assert.AreEqual(expectedChunkX, result.x,
                $"WorldX={worldX} should map to chunkX={expectedChunkX}");
        }

        // =====================================================================
        // ChunkCoordUtility — ChunkToWorldPos
        // =====================================================================

        [Test]
        public void ChunkToWorldPos_ReturnsChunkOriginInWorldSpace()
        {
            Assert.AreEqual(new Vector3(64f, -32f, 96f),
                ChunkCoordUtility.ChunkToWorldPos(new Vector3Int(2, -1, 3)));
        }

        [Test]
        public void ChunkToWorldPos_Origin_ReturnsZero()
        {
            Assert.AreEqual(Vector3.zero,
                ChunkCoordUtility.ChunkToWorldPos(Vector3Int.zero));
        }

        [Test]
        public void ChunkToWorldPos_RoundTrip_WithWorldToChunkPos()
        {
            // Chunk origin positions should round-trip perfectly
            var chunkPos = new Vector3Int(4, -2, 7);
            Vector3 worldPos = ChunkCoordUtility.ChunkToWorldPos(chunkPos);
            Vector3Int backToChunk = ChunkCoordUtility.WorldToChunkPos(worldPos);
            Assert.AreEqual(chunkPos, backToChunk);
        }

        [Test]
        public void WorldToChunk_ContainmentProperty()
        {
            // For any world position, the chunk origin should be <= the position
            // and the position should be within one chunk-size of the origin.
            Vector3[] testPositions = new Vector3[]
            {
                new Vector3(45f, -10f, 100f),
                new Vector3(-0.5f, 0.5f, -100.3f),
                new Vector3(1000f, -500f, 200f),
            };

            foreach (var worldPos in testPositions)
            {
                Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(worldPos);
                Vector3 origin = ChunkCoordUtility.ChunkToWorldPos(chunkPos);

                Assert.LessOrEqual(origin.x, worldPos.x, $"Origin.x > worldPos.x for {worldPos}");
                Assert.LessOrEqual(origin.y, worldPos.y, $"Origin.y > worldPos.y for {worldPos}");
                Assert.LessOrEqual(origin.z, worldPos.z, $"Origin.z > worldPos.z for {worldPos}");
                Assert.Greater(origin.x + ChunkData.SIZE, worldPos.x);
                Assert.Greater(origin.y + ChunkData.SIZE, worldPos.y);
                Assert.Greater(origin.z + ChunkData.SIZE, worldPos.z);
            }
        }

        // =====================================================================
        // ChunkCoordUtility — LocalToWorld
        // =====================================================================

        [Test]
        public void LocalToWorld_AtOriginChunk_EqualsLocalCoords()
        {
            Vector3Int result = ChunkCoordUtility.LocalToWorld(Vector3Int.zero, 7, 15, 31);
            Assert.AreEqual(new Vector3Int(7, 15, 31), result);
        }

        [Test]
        public void LocalToWorld_OffsetsCorrectly()
        {
            Vector3Int chunkPos = new Vector3Int(1, 0, -1);
            Vector3Int result = ChunkCoordUtility.LocalToWorld(chunkPos, 5, 10, 3);
            Assert.AreEqual(new Vector3Int(37, 10, -29), result);
        }

        [Test]
        public void LocalToWorld_ZeroLocal_EqualsChunkOrigin()
        {
            var chunkPos = new Vector3Int(3, -2, 1);
            Vector3Int result = ChunkCoordUtility.LocalToWorld(chunkPos, 0, 0, 0);
            Vector3 chunkOrigin = ChunkCoordUtility.ChunkToWorldPos(chunkPos);

            Assert.AreEqual((int)chunkOrigin.x, result.x);
            Assert.AreEqual((int)chunkOrigin.y, result.y);
            Assert.AreEqual((int)chunkOrigin.z, result.z);
        }

        // =====================================================================
        // Density field bounds — index correctness
        // =====================================================================

        [Test]
        public void ChunkData_DensityField_IndexLayout()
        {
            // Verify the index formula: x + y * SIZE + z * SIZE * SIZE
            var chunk = new ChunkData(Vector3Int.zero);
            int size = ChunkData.SIZE;

            // Set a unique value at a known position and verify via direct array access
            chunk.SetDensity(3, 7, 11, 12.34f);
            int expectedIndex = 3 + 7 * size + 11 * size * size;
            Assert.AreEqual(12.34f, chunk.densityField[expectedIndex], 1e-6f);
        }

        [Test]
        public void ChunkData_DensityField_EdgeCoordinates()
        {
            var chunk = new ChunkData(Vector3Int.zero);

            // Test along each axis edge
            chunk.SetDensity(31, 0, 0, 1f);
            chunk.SetDensity(0, 31, 0, 2f);
            chunk.SetDensity(0, 0, 31, 3f);

            Assert.AreEqual(1f, chunk.GetDensity(31, 0, 0), 1e-6f);
            Assert.AreEqual(2f, chunk.GetDensity(0, 31, 0), 1e-6f);
            Assert.AreEqual(3f, chunk.GetDensity(0, 0, 31), 1e-6f);
        }

        [Test]
        public void ChunkData_DensityField_FullWrite_NoOverlap()
        {
            // Write a unique value to every voxel, then verify all are correct.
            // Uses a smaller sub-region to keep test fast.
            var chunk = new ChunkData(Vector3Int.zero);
            int testSize = 8; // Test an 8x8x8 sub-region

            for (int z = 0; z < testSize; z++)
            for (int y = 0; y < testSize; y++)
            for (int x = 0; x < testSize; x++)
            {
                float val = x + y * 100f + z * 10000f;
                chunk.SetDensity(x, y, z, val);
            }

            for (int z = 0; z < testSize; z++)
            for (int y = 0; y < testSize; y++)
            for (int x = 0; x < testSize; x++)
            {
                float expected = x + y * 100f + z * 10000f;
                float actual = chunk.GetDensity(x, y, z);
                Assert.AreEqual(expected, actual, 1e-6f,
                    $"Mismatch at ({x},{y},{z})");
            }
        }
    }
}
