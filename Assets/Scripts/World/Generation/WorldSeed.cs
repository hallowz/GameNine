using Unity.Mathematics;

namespace Voidborne.World.Generation
{
    public static class WorldSeed
    {
        public static int Seed { get; set; } = 42;

        /// <summary>
        /// Returns a deterministic float offset based on seed and channel index.
        /// Useful for offsetting noise samples to get independent noise layers.
        /// </summary>
        public static float SeedOffset(int channel)
        {
            // Large primes for hash-like distribution
            return (Seed * 73856093 ^ channel * 19349663) % 10000;
        }

        public static float2 SeedOffset2D(int channel)
        {
            return new float2(
                SeedOffset(channel),
                SeedOffset(channel + 7919)
            );
        }

        public static float3 SeedOffset3D(int channel)
        {
            return new float3(
                SeedOffset(channel),
                SeedOffset(channel + 7919),
                SeedOffset(channel + 17389)
            );
        }
    }
}
