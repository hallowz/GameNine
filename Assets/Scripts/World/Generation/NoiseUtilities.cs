using Unity.Mathematics;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Static noise utility methods wrapping Unity.Mathematics simplex noise.
    /// All methods support an optional seed offset for deterministic generation.
    /// </summary>
    public static class NoiseUtilities
    {
        /// <summary>
        /// Layered 2D simplex noise (fractional Brownian motion).
        /// Returns a value roughly in [-1, 1] range.
        /// </summary>
        public static float Noise2D(float2 pos, float frequency, int octaves,
            float persistence, float lacunarity, float seedOffset = 0f)
        {
            float amplitude = 1f;
            float freq = frequency;
            float sum = 0f;
            float maxAmplitude = 0f;

            float2 offset = new float2(seedOffset, seedOffset * 0.7123f);

            for (int i = 0; i < octaves; i++)
            {
                sum += noise.snoise((pos + offset) * freq) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            return sum / maxAmplitude;
        }

        /// <summary>
        /// Layered 3D simplex noise (fractional Brownian motion).
        /// Returns a value roughly in [-1, 1] range.
        /// </summary>
        public static float Noise3D(float3 pos, float frequency, int octaves,
            float persistence, float lacunarity, float seedOffset = 0f)
        {
            float amplitude = 1f;
            float freq = frequency;
            float sum = 0f;
            float maxAmplitude = 0f;

            float3 offset = new float3(seedOffset, seedOffset * 0.7123f, seedOffset * 0.3917f);

            for (int i = 0; i < octaves; i++)
            {
                sum += noise.snoise((pos + offset) * freq) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            return sum / maxAmplitude;
        }

        /// <summary>
        /// Ridged 2D simplex noise. Takes the absolute value and inverts it
        /// to create sharp ridges. Returns a value roughly in [0, 1] range.
        /// </summary>
        public static float RidgedNoise2D(float2 pos, float frequency, int octaves,
            float persistence, float lacunarity, float seedOffset = 0f)
        {
            float amplitude = 1f;
            float freq = frequency;
            float sum = 0f;
            float maxAmplitude = 0f;

            float2 offset = new float2(seedOffset, seedOffset * 0.7123f);

            for (int i = 0; i < octaves; i++)
            {
                float n = noise.snoise((pos + offset) * freq);
                // Ridged: invert absolute value so ridges are at 1.0
                n = 1f - math.abs(n);
                // Square it to sharpen ridges
                n *= n;
                sum += n * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            return sum / maxAmplitude;
        }

        /// <summary>
        /// Domain warping using 2D simplex noise. Distorts the input position
        /// before sampling, creating organic-looking displacement.
        /// Returns the warped position.
        /// </summary>
        public static float2 DomainWarp2D(float2 pos, float warpStrength, float frequency,
            float seedOffset = 0f)
        {
            float2 offset = new float2(seedOffset, seedOffset * 0.7123f);

            float warpX = noise.snoise((pos + offset) * frequency);
            float warpY = noise.snoise((pos + offset + new float2(5.2f, 1.3f)) * frequency);

            return pos + new float2(warpX, warpY) * warpStrength;
        }
    }
}
