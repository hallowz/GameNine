using NUnit.Framework;
using Unity.Mathematics;
using Voidborne.World.Generation;

namespace Voidborne.Tests.EditMode
{
    [TestFixture]
    public class DensityFunctionTests
    {
        private TerrainShapeData _defaultShape;
        private BiomeData _defaultBiome;

        [SetUp]
        public void SetUp()
        {
            _defaultShape = TerrainShapeData.Default;
            _defaultBiome = BiomeData.Default;
            WorldSeed.Seed = 42;
        }

        // ─── Density sanity checks ───

        [Test]
        public void Density_IsPositive_DeepUnderground()
        {
            // At y = -500, density should generally be positive (solid)
            float density = DensityFunction.GetDensity(new float3(100f, -500f, 100f), _defaultShape, _defaultBiome);
            Assert.IsTrue(density > 0f,
                $"Expected positive density deep underground (y=-500), got {density}");
        }

        [Test]
        public void Density_IsNegative_HighInAir()
        {
            // At y = 500, density should be negative (air) in the low sky zone
            // Test multiple positions to be robust against floating island pockets
            int airCount = 0;
            int totalSamples = 10;
            for (int i = 0; i < totalSamples; i++)
            {
                float x = i * 137f;
                float z = i * 251f;
                float density = DensityFunction.GetDensity(new float3(x, 500f, z), _defaultShape, _defaultBiome);
                if (density < 0f) airCount++;
            }

            Assert.IsTrue(airCount > totalSamples / 2,
                $"Expected majority of samples at y=500 to be air, but only {airCount}/{totalSamples} were negative");
        }

        [Test]
        public void Density_SurfaceExistsInExpectedRange()
        {
            // The surface (density crossing zero) should occur somewhere between y=-50 and y=200
            // for standard terrain with default parameters
            float x = 50f;
            float z = 50f;

            float densityLow = DensityFunction.GetDensity(new float3(x, -200f, z), _defaultShape, _defaultBiome);
            float densityHigh = DensityFunction.GetDensity(new float3(x, 300f, z), _defaultShape, _defaultBiome);

            // Deep should be positive, high should be negative
            Assert.IsTrue(densityLow > 0f,
                $"Expected positive density at y=-200, got {densityLow}");
            Assert.IsTrue(densityHigh < 0f,
                $"Expected negative density at y=300, got {densityHigh}");

            // Find approximate surface by binary search
            float lo = -200f;
            float hi = 300f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float d = DensityFunction.GetDensity(new float3(x, mid, z), _defaultShape, _defaultBiome);
                if (d > 0f) lo = mid;
                else hi = mid;
            }

            float surfaceY = (lo + hi) * 0.5f;
            Assert.IsTrue(surfaceY > -150f && surfaceY < 250f,
                $"Expected surface between y=-150 and y=250, found at y={surfaceY}");
        }

        [Test]
        public void Density_CavesCreateNegativePockets()
        {
            // Sample many underground points; some should be carved out (negative)
            // if cave parameters are active
            TerrainShapeData caveyShape = TerrainShapeData.Default;
            caveyShape.caveScale = 1.5f;
            caveyShape.caveDensity = 0.2f; // Lower threshold = more caves

            int negativeCount = 0;
            int totalSamples = 200;

            for (int i = 0; i < totalSamples; i++)
            {
                float x = (i * 73) % 500;
                float z = (i * 131) % 500;
                float y = -30f - (i % 50); // y from -30 to -80

                float density = DensityFunction.GetDensity(new float3(x, y, z), caveyShape, _defaultBiome);
                if (density < 0f) negativeCount++;
            }

            Assert.IsTrue(negativeCount > 0,
                $"Expected some cave pockets (negative density) underground, found {negativeCount}/{totalSamples}");
        }

        // ─── NoiseUtilities consistency ───

        [Test]
        public void NoiseUtilities_Noise2D_IsConsistent()
        {
            float2 pos = new float2(123.456f, 789.012f);
            float a = NoiseUtilities.Noise2D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            float b = NoiseUtilities.Noise2D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            Assert.AreEqual(a, b, "Noise2D should return identical values for identical inputs");
        }

        [Test]
        public void NoiseUtilities_Noise3D_IsConsistent()
        {
            float3 pos = new float3(123.456f, 45.6f, 789.012f);
            float a = NoiseUtilities.Noise3D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            float b = NoiseUtilities.Noise3D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            Assert.AreEqual(a, b, "Noise3D should return identical values for identical inputs");
        }

        [Test]
        public void NoiseUtilities_DifferentSeedOffsets_ProduceDifferentResults()
        {
            float2 pos = new float2(100f, 200f);
            float a = NoiseUtilities.Noise2D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            float b = NoiseUtilities.Noise2D(pos, 0.01f, 4, 0.5f, 2f, 999f);
            Assert.AreNotEqual(a, b, "Different seed offsets should produce different noise values");
        }

        [Test]
        public void NoiseUtilities_RidgedNoise2D_ReturnsPositiveValues()
        {
            float2 pos = new float2(50f, 75f);
            float val = NoiseUtilities.RidgedNoise2D(pos, 0.01f, 4, 0.5f, 2f, 0f);
            Assert.IsTrue(val >= 0f, $"Ridged noise should be non-negative, got {val}");
        }

        [Test]
        public void NoiseUtilities_Noise2D_InExpectedRange()
        {
            // Sample many points and verify noise stays in [-1, 1]
            for (int i = 0; i < 100; i++)
            {
                float2 pos = new float2(i * 17.3f, i * 31.7f);
                float val = NoiseUtilities.Noise2D(pos, 0.01f, 6, 0.5f, 2f, 0f);
                Assert.IsTrue(val >= -1f && val <= 1f,
                    $"Noise2D at {pos} returned {val}, expected [-1, 1]");
            }
        }

        // ─── WorldSeed determinism ───

        [Test]
        public void WorldSeed_SeedOffset_IsDeterministic()
        {
            WorldSeed.Seed = 12345;
            float a = WorldSeed.SeedOffset(0);
            float b = WorldSeed.SeedOffset(0);
            Assert.AreEqual(a, b, "SeedOffset should be deterministic");
        }

        [Test]
        public void WorldSeed_DifferentChannels_ProduceDifferentOffsets()
        {
            WorldSeed.Seed = 42;
            float a = WorldSeed.SeedOffset(0);
            float b = WorldSeed.SeedOffset(1);
            Assert.AreNotEqual(a, b, "Different channels should produce different offsets");
        }

        [Test]
        public void WorldSeed_DifferentSeeds_ProduceDifferentOffsets()
        {
            WorldSeed.Seed = 42;
            float a = WorldSeed.SeedOffset(0);

            WorldSeed.Seed = 999;
            float b = WorldSeed.SeedOffset(0);

            Assert.AreNotEqual(a, b, "Different seeds should produce different offsets");
        }

        [Test]
        public void WorldSeed_SeedOffset2D_IsDeterministic()
        {
            WorldSeed.Seed = 42;
            float2 a = WorldSeed.SeedOffset2D(3);
            float2 b = WorldSeed.SeedOffset2D(3);
            Assert.AreEqual(a.x, b.x);
            Assert.AreEqual(a.y, b.y);
        }

        [Test]
        public void WorldSeed_SeedOffset3D_IsDeterministic()
        {
            WorldSeed.Seed = 42;
            float3 a = WorldSeed.SeedOffset3D(5);
            float3 b = WorldSeed.SeedOffset3D(5);
            Assert.AreEqual(a.x, b.x);
            Assert.AreEqual(a.y, b.y);
            Assert.AreEqual(a.z, b.z);
        }

        // ─── Full pipeline determinism ───

        [Test]
        public void Density_IsDeterministic_WithSameSeed()
        {
            WorldSeed.Seed = 42;
            float3 pos = new float3(100f, 10f, 200f);

            float a = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);
            float b = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);

            Assert.AreEqual(a, b, "Density should be deterministic for the same seed and position");
        }

        [Test]
        public void Density_ChangesWith_DifferentSeed()
        {
            float3 pos = new float3(100f, 10f, 200f);

            WorldSeed.Seed = 42;
            float a = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);

            WorldSeed.Seed = 9999;
            float b = DensityFunction.GetDensity(pos, _defaultShape, _defaultBiome);

            Assert.AreNotEqual(a, b, "Different seeds should produce different density values");
        }
    }
}
