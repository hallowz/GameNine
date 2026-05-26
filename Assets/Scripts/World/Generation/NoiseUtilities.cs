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

        // =================================================================
        //  Worley / Voronoi Noise — cell edges form connected road networks
        // =================================================================

        /// <summary>
        /// Hash a 2D cell coordinate to a deterministic float2 in [0,1)².
        /// Used to jitter Voronoi cell centers within their grid cell.
        /// </summary>
        private static float2 HashCell2D(float2 cell, float seedOffset)
        {
            // Two independent hashes for X and Y jitter
            float2 p = cell + new float2(seedOffset, seedOffset * 0.7123f);
            return math.frac(math.sin(new float2(
                math.dot(p, new float2(127.1f, 311.7f)),
                math.dot(p, new float2(269.5f, 183.3f))
            )) * 43758.5453f);
        }

        /// <summary>
        /// 2D Worley (cellular) noise. Returns:
        ///   F1 = distance to nearest cell center
        ///   F2 = distance to second-nearest cell center
        ///   F1Cell = integer coordinates of the nearest cell
        ///
        /// Cell size controls the scale — larger cells = wider road spacing.
        /// </summary>
        public static void Worley2D(float2 worldPos, float cellSize, float seedOffset,
            out float F1, out float F2, out float2 F1Cell, out float2 F1Center)
        {
            float2 pos = worldPos / cellSize;
            float2 cellFloor = math.floor(pos);

            F1 = float.MaxValue;
            F2 = float.MaxValue;
            F1Cell = float2.zero;
            F1Center = float2.zero;

            // Search 3x3 neighborhood
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                float2 neighbor = cellFloor + new float2(dx, dz);
                float2 jitter = HashCell2D(neighbor, seedOffset);
                float2 center = neighbor + jitter;
                float dist = math.length(pos - center);

                if (dist < F1)
                {
                    F2 = F1;
                    F1 = dist;
                    F1Cell = neighbor;
                    F1Center = center * cellSize; // back to world space
                }
                else if (dist < F2)
                {
                    F2 = dist;
                }
            }

            // Convert distances back to world space
            F1 *= cellSize;
            F2 *= cellSize;
        }

        /// <summary>
        /// Returns the distance to the nearest Voronoi cell edge in world units.
        /// Small values = near a cell edge = road center.
        /// This is the key function for road generation.
        /// </summary>
        public static float WorleyEdgeDist(float2 worldPos, float cellSize, float seedOffset)
        {
            Worley2D(worldPos, cellSize, seedOffset, out float F1, out float F2, out _, out _);
            return F2 - F1;
        }

        /// <summary>
        /// Returns road influence at a world position [0,1].
        /// 1 = road center, 0 = no road. Smoothly blended.
        /// </summary>
        public static float RoadInfluence(float2 worldPos, float cellSize, float roadHalfWidth, float seedOffset)
        {
            float edgeDist = WorleyEdgeDist(worldPos, cellSize, seedOffset);
            return 1f - math.smoothstep(0f, roadHalfWidth, edgeDist);
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
