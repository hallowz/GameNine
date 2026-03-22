using Unity.Mathematics;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Core density function for world generation.
    /// Positive = solid, Negative = air, Zero = surface.
    /// Terrain shape is driven by TerrainShapeData (from splines), while
    /// biome-specific features (overhangs) come from BiomeData.
    /// </summary>
    public static class DensityFunction
    {
        // Noise channels for deterministic seeding
        private const int ChannelSurfaceHeight = 0;
        private const int ChannelCave3D = 1;
        private const int ChannelDomainWarp = 2;
        private const int ChannelFloatingIslands = 3;
        private const int ChannelPlateau = 4;
        private const int ChannelAbyssal = 5;
        private const int ChannelRavine = 6;            // ravine centerline noise
        private const int ChannelCavern = 7;            // large cavern noise
        private const int ChannelBiomeOverhang = 12; // W1.1 — biome-driven overhangs
        // Channel 22 is reserved for future road generation — do not reuse here.
        private const int ChannelSkyBiomeType = 30;  // sky biome type selection noise
        private const int ChannelSkyIsland = 31;     // sky island mask/shape noise
        private const int ChannelStalactite = 32;    // stalactite drip detail noise

        // Sky zone boundary: 8 chunks above the highest surface terrain (~120).
        // Transition blends from 250 to 350 so surface mountains connect smoothly.
        public const float SkyTransitionStart = 80f;
        public const float SkyStart = 100f;
        public const float SkyMax = 800f;

        // Sky biome types (0-4)
        public const int SkyBiomeSkywardSpires = 0;
        public const int SkyBiomeDriftingMeadows = 1;
        public const int SkyBiomeOasisIsles = 2;
        public const int SkyBiomeStormridgePeaks = 3;
        public const int SkyBiomeAncientBastions = 4;

        /// <summary>
        /// Evaluate the density at a world position, automatically looking up the
        /// terrain shape from splines and biome data from the BiomeMap.
        /// </summary>
        public static float GetDensity(float3 worldPos)
        {
            float2 xz = new float2(worldPos.x, worldPos.z);
            TerrainShapeData shape = BiomeMap.GetTerrainShape(xz);
            BiomeData biome = BiomeMap.GetBlendedBiomeData(xz);
            return GetDensity(worldPos, shape, biome);
        }

        /// <summary>
        /// Evaluate the density at a world position using explicit terrain shape and biome data.
        /// </summary>
        public static float GetDensity(float3 worldPos, TerrainShapeData shape, BiomeData biome)
        {
            float y = worldPos.y;

            // Surface height from 2D noise
            float surfaceHeight = GetSurfaceHeight(worldPos.x, worldPos.z, shape);

            // Base density: positive below surface, negative above
            float density = -y + surfaceHeight;

            // Sky zone: floating islands override base density
            if (y > SkyTransitionStart)
            {
                float skyDensity = SkyIslandDensity(worldPos);
                if (y >= SkyStart)
                {
                    density = skyDensity;
                }
                else
                {
                    // Smooth transition from surface to sky
                    float blend = math.saturate((y - SkyTransitionStart) / (SkyStart - SkyTransitionStart));
                    density = math.lerp(density, skyDensity, blend);
                }
            }
            else if (y > 128f)
            {
                density = UpperSurfaceDensity(worldPos, shape, density, y);
            }
            else if (y >= -128f)
            {
                density = SurfaceBandDensity(worldPos, shape, biome, density, surfaceHeight);
            }
            else if (y >= -256f)
            {
                density = ShallowUndergroundDensity(worldPos, shape, density, y);
            }
            else if (y >= -512f)
            {
                density = DeepUndergroundDensity(worldPos, shape, density, y);
            }
            else
            {
                density = AbyssalDensity(worldPos, shape, density, y);
            }

            return density;
        }

        /// <summary>
        /// Backward-compatible overload that takes the old BiomeData struct.
        /// Converts to TerrainShapeData using defaults and passes through.
        /// Used during migration by systems not yet updated.
        /// </summary>
        public static float GetDensity(float3 worldPos, BiomeData biome)
        {
            // During migration: use spline-derived shape from BiomeMap
            float2 xz = new float2(worldPos.x, worldPos.z);
            TerrainShapeData shape = BiomeMap.GetTerrainShape(xz);
            return GetDensity(worldPos, shape, biome);
        }

        /// <summary>
        /// Compute the surface height at a given XZ position using layered 2D noise.
        /// </summary>
        public static float GetSurfaceHeight(float x, float z, TerrainShapeData shape)
        {
            float2 pos = new float2(x, z);
            float seedOff = WorldSeed.SeedOffset(ChannelSurfaceHeight);

            // Domain warp for more organic shapes (strength tuned to 12 for 32-unit chunks)
            float2 warpedPos = NoiseUtilities.DomainWarp2D(pos, 12f, shape.heightFrequency * 0.5f,
                WorldSeed.SeedOffset(ChannelDomainWarp));

            // Layered simplex noise for surface height
            float heightNoise = NoiseUtilities.Noise2D(warpedPos, shape.heightFrequency,
                shape.noiseOctaves, shape.persistence, shape.lacunarity, seedOff);

            return heightNoise * shape.heightScale;
        }

        // =========================================================================
        //  SKY BIOMES — Floating islands with stalactite bottoms
        // =========================================================================

        /// <summary>
        /// Returns the sky biome type (0-4) at a given XZ position.
        /// Uses a dedicated large-scale noise channel independent of surface biomes.
        /// </summary>
        public static int GetSkyBiomeType(float2 xz)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelSkyBiomeType);
            float raw = NoiseUtilities.Noise2D(xz, 0.0003f, 2, 0.5f, 2f, seedOff);
            // Map [-1,1] to [0,5) and clamp to valid range
            int type = (int)math.floor(math.saturate(raw * 0.5f + 0.5f) * 5f);
            return math.clamp(type, 0, 4);
        }

        /// <summary>
        /// Sky island parameters per biome type.
        /// Returns (maskFrequency, threshold, islandHalfHeight, topHeight, stalactiteDepth).
        /// </summary>
        private static void GetSkyBiomeParams(int skyBiomeType, float altFactor,
            out float maskFreq, out float threshold, out float islandHalfH,
            out float topHeight, out float stalactiteDepth)
        {
            // altFactor: 0 at SkyStart, 1 near SkyMax — islands get bigger higher up
            switch (skyBiomeType)
            {
                case SkyBiomeSkywardSpires: // Small islands — more frequent
                    maskFreq = math.lerp(0.010f, 0.006f, altFactor);
                    threshold = math.lerp(0.25f, 0.15f, altFactor);
                    islandHalfH = math.lerp(20f, 32f, altFactor);
                    topHeight = math.lerp(8f, 14f, altFactor);
                    stalactiteDepth = math.lerp(16f, 28f, altFactor);
                    break;
                case SkyBiomeDriftingMeadows: // Medium flat grassy islands
                    maskFreq = math.lerp(0.005f, 0.003f, altFactor);
                    threshold = math.lerp(0.15f, 0.05f, altFactor);
                    islandHalfH = math.lerp(16f, 24f, altFactor);
                    topHeight = math.lerp(6f, 12f, altFactor);
                    stalactiteDepth = math.lerp(14f, 24f, altFactor);
                    break;
                case SkyBiomeOasisIsles: // Islands with small lake depressions
                    maskFreq = math.lerp(0.004f, 0.0024f, altFactor);
                    threshold = math.lerp(0.12f, 0.02f, altFactor);
                    islandHalfH = math.lerp(18f, 28f, altFactor);
                    topHeight = math.lerp(8f, 14f, altFactor);
                    stalactiteDepth = math.lerp(16f, 28f, altFactor);
                    break;
                case SkyBiomeStormridgePeaks: // Mountainous floating islands
                    maskFreq = math.lerp(0.003f, 0.0016f, altFactor);
                    threshold = math.lerp(0.10f, 0.00f, altFactor);
                    islandHalfH = math.lerp(24f, 40f, altFactor);
                    topHeight = math.lerp(16f, 30f, altFactor);
                    stalactiteDepth = math.lerp(18f, 32f, altFactor);
                    break;
                default: // SkyBiomeAncientBastions: Massive flat-topped mesa islands
                    maskFreq = math.lerp(0.002f, 0.001f, altFactor);
                    threshold = math.lerp(0.05f, -0.05f, altFactor);
                    islandHalfH = math.lerp(20f, 32f, altFactor);
                    topHeight = math.lerp(10f, 20f, altFactor);
                    stalactiteDepth = math.lerp(16f, 30f, altFactor);
                    break;
            }
        }

        // Band system: sky is divided into altitude bands, each with its own 2D noise.
        // Islands within a band can't stack. Bands are separated by air gaps.
        private const float IslandBandThickness = 112f;  // ~3.5 chunks of island zone
        private const float IslandBandGap = 64f;          // 2 chunks of guaranteed air
        private const float IslandBandSize = IslandBandThickness + IslandBandGap; // ~5.5 chunks total

        /// <summary>
        /// Compute floating island density using altitude bands.
        /// Each band uses 2D noise for the island mask (no vertical stacking within a band).
        /// Bands are separated by 3-chunk air gaps. The vertical profile within each band
        /// creates flat-topped islands with stalactite bottoms.
        /// </summary>
        private static float SkyIslandDensity(float3 worldPos)
        {
            float y = worldPos.y;
            float2 xz = new float2(worldPos.x, worldPos.z);

            // Which band are we in?
            float relY = y - SkyStart;
            int band = (int)math.floor(relY / IslandBandSize);
            float localY = relY - band * IslandBandSize; // 0 to IslandBandSize

            // Gap zone: guaranteed air between bands
            if (localY > IslandBandThickness)
                return -1f;

            // Altitude factor for this band (higher bands = bigger islands)
            float bandCenterY = SkyStart + band * IslandBandSize + IslandBandThickness * 0.5f;
            float altFactor = math.saturate((bandCenterY - SkyStart) / (SkyMax - SkyStart));

            // Sky biome type at this XZ
            int skyBiomeType = GetSkyBiomeType(xz);
            GetSkyBiomeParams(skyBiomeType, altFactor,
                out float maskFreq, out float threshold, out float islandHalfH,
                out float topHeight, out float stalactiteDepth);

            // 2D noise mask — each band gets a different seed offset so islands don't align
            float islandSeed = WorldSeed.SeedOffset(ChannelSkyIsland) + band * 73.7f;
            float stalSeed = WorldSeed.SeedOffset(ChannelStalactite);

            float islandNoise = NoiseUtilities.Noise2D(xz, maskFreq, 3, 0.5f, 2f, islandSeed);

            if (islandNoise <= threshold)
                return -1f; // no island at this XZ in this band

            // Island strength: 0 at edge, 1 at core
            float raw = (islandNoise - threshold) / (1f - threshold);
            float strength = math.smoothstep(0f, 1f, raw);

            // XZ distance from island edge — used to slope edges instead of vertical walls
            float xzDist = raw * islandHalfH;

            // Island center is in the middle of the band
            float bandCenter = IslandBandThickness * 0.5f;
            float distFromCenter = localY - bandCenter;

            float halfH = islandHalfH * strength;
            if (halfH < 4f) return -1f;

            // --- STALACTITE ZONE: extends below the main island body ---
            if (distFromCenter < -halfH)
            {
                float below = (-distFromCenter) - halfH;
                if (below > stalactiteDepth) return -1f;

                float3 dripPos = new float3(worldPos.x, worldPos.y * 3f, worldPos.z);
                float dripNoise = NoiseUtilities.Noise3D(dripPos, 0.03f, 2, 0.5f, 2f, stalSeed);

                if (dripNoise < 0.1f) return -1f;

                float stalNorm = below / stalactiteDepth;
                float taper = (1f - stalNorm) * (1f - stalNorm);
                float dripStr = (dripNoise - 0.1f) / 0.9f;
                // Also taper stalactites near XZ edge
                return dripStr * taper * math.min(xzDist, 1f) * 15f;
            }

            // --- TOP TERRAIN: shapes the top surface ---
            float topTerrain = NoiseUtilities.Noise2D(xz, 0.008f, 3, 0.5f, 2f, islandSeed + 500f);

            if (skyBiomeType == SkyBiomeStormridgePeaks)
            {
                float ridged = NoiseUtilities.RidgedNoise2D(xz, 0.006f, 3, 0.5f, 2f, islandSeed + 600f);
                topTerrain = math.lerp(topTerrain, ridged, 0.6f);
            }

            float terrainOffset = topTerrain * topHeight * strength;

            // Oasis Isles: carve pools
            if (skyBiomeType == SkyBiomeOasisIsles && strength > 0.4f)
            {
                float poolNoise = NoiseUtilities.Noise2D(xz, 0.012f, 2, 0.5f, 2f, islandSeed + 700f);
                if (poolNoise > 0.25f)
                    terrainOffset -= (poolNoise - 0.25f) / 0.75f * 8f * strength;
            }

            float topSurface = halfH + terrainOffset;
            float botSurface = -halfH;

            if (distFromCenter > topSurface)
                return -1f;

            // --- ISLAND BODY: SDF combining vertical and XZ distances ---
            // min(vertical dist, XZ dist) gives sloped edges on all sides
            float distToTop = topSurface - distFromCenter;
            float distToBot = distFromCenter - botSurface;
            float vertDist = math.min(distToTop, distToBot);
            float density = math.min(vertDist, xzDist);

            return density;
        }

        // =========================================================================
        //  SURFACE ZONES
        // =========================================================================

        /// <summary>
        /// Y 128-250: Mountain peaks, cliffs, tall terrain features.
        /// </summary>
        private static float UpperSurfaceDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);

            // Add ridged noise for cliff-like features
            float ridged = NoiseUtilities.RidgedNoise2D(
                new float2(worldPos.x, worldPos.z), shape.heightFrequency * 1.5f,
                4, 0.5f, 2f, seedOff + 200f);

            // Add extra height for mountain peaks
            float mountainBoost = ridged * shape.heightScale * 0.4f;

            // 3D noise for overhangs
            float overhang = NoiseUtilities.Noise3D(worldPos, 0.02f, 3, 0.5f, 2f, seedOff);

            return baseDensity + mountainBoost * math.saturate(1f - (y - 128f) / 122f)
                   + overhang * shape.heightScale * 0.15f;
        }

        /// <summary>
        /// Y -128 to 128: Standard surface terrain with caves, ravines.
        /// </summary>
        private static float SurfaceBandDensity(float3 worldPos, TerrainShapeData shape, BiomeData biome, float baseDensity, float surfaceHeight)
        {
            float2 xz = new float2(worldPos.x, worldPos.z);

            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);

            // Cave frequency and radius scale with shape.caveScale (0.3 = sparse, 0.7 = dense)
            float caveFreq = 0.02f * shape.caveScale;
            float caveNoise = NoiseUtilities.Noise3D(worldPos, caveFreq, 2, 0.5f, 2f, seedOff);

            // Smaller overhang contribution at surface level
            float overhang = 0f;
            if (worldPos.y >= 0f)
            {
                overhang = caveNoise * shape.heightScale * 0.1f;
            }

            float depthBelowSurface = surfaceHeight - worldPos.y;

            // --- Ravines: narrow deep gashes, ~400m max length, scarce ---
            // A low-frequency gate noise creates isolated patches (~400m across)
            // where ravines can form. The gate fades smoothly at edges so ravines
            // taper closed rather than cutting off abruptly.
            float ravineMaxDepth = 60f + shape.caveScale * 40f;
            float skyProximityFade = math.saturate((SkyTransitionStart - worldPos.y) / 32f);
            if (depthBelowSurface > 0f && depthBelowSurface < ravineMaxDepth && skyProximityFade > 0f)
            {
                // Gate: 0.005 frequency → ~200m feature size. With threshold 0.15
                // ~40% of the area has ravines. Smoothstep taper (0.15→0.35) ensures
                // ravines close gradually at segment ends.
                float ravineGate = NoiseUtilities.Noise2D(xz, 0.005f, 1, 0.5f, 2f,
                    WorldSeed.SeedOffset(ChannelRavine) + 500f);
                float gateFade = math.smoothstep(0.15f, 0.35f, ravineGate);

                if (gateFade > 0f)
                {
                    float ravineNoise = NoiseUtilities.Noise2D(xz, 0.002f, 2, 0.5f, 2f,
                        WorldSeed.SeedOffset(ChannelRavine));
                    float ravineValue = math.abs(ravineNoise);
                    float ravineWidth = math.lerp(0.02f + shape.caveScale * 0.01f, 0.006f,
                        depthBelowSurface / ravineMaxDepth);
                    if (ravineValue < ravineWidth)
                    {
                        float falloff = 1f - ravineValue / ravineWidth;
                        float strength = math.lerp(45f, 12f, depthBelowSurface / ravineMaxDepth);
                        baseDensity = math.min(baseDensity, -falloff * strength * skyProximityFade * gateFade);
                    }
                }
            }

            // --- Worm caves: tube where both noise fields are near zero ---
            if (depthBelowSurface > 8f)
            {
                float cave2 = NoiseUtilities.Noise3D(worldPos, caveFreq, 2, 0.5f, 2f,
                    seedOff + 77.3f);
                float tubeDist2 = caveNoise * caveNoise + cave2 * cave2;
                float tubeRadius = 0.06f + shape.caveDensity * 0.16f;
                if (tubeDist2 < tubeRadius * tubeRadius)
                {
                    float t = 1f - math.sqrt(tubeDist2) / tubeRadius;
                    float carveStr = 20f + shape.caveScale * 20f;
                    return math.min(baseDensity + overhang, -t * carveStr);
                }
            }

            // W1.1 — biome-driven overhangs
            if (biome.overhangStrength > 0f
                && worldPos.y > biome.overhangMinY
                && worldPos.y > surfaceHeight + 4f)
            {
                float rawOvh = NoiseUtilities.Noise3D(worldPos, biome.overhangFrequency,
                    3, 0.5f, 2f, WorldSeed.SeedOffset(ChannelBiomeOverhang));
                float normOvh = rawOvh * 0.5f + 0.5f;
                float threshold = 1f - biome.overhangStrength;
                if (normOvh > threshold)
                    overhang += (normOvh - threshold) * shape.heightScale;
            }

            return baseDensity + overhang;
        }

        // =========================================================================
        //  UNDERGROUND ZONES — Mostly horizontal caves, big caverns at depth
        // =========================================================================

        /// <summary>
        /// Y -128 to -256: Wide horizontal caverns, gentle worm tunnels, spiral passages.
        /// Caves are predominantly horizontal for easy exploration.
        /// </summary>
        private static float ShallowUndergroundDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);
            float density = baseDensity;

            float caveFreq = 0.02f * shape.caveScale;

            // --- Wide horizontal caverns ---
            // Y is stretched 3x so noise features are 3x flatter → wide, low-ceiling chambers.
            float3 cavernPos = new float3(worldPos.x, worldPos.y * 3f, worldPos.z);
            float cavernNoise = NoiseUtilities.Noise3D(cavernPos, 0.005f * shape.caveScale, 2, 0.5f, 2f,
                WorldSeed.SeedOffset(ChannelCavern));
            float cavernThreshold = 0.35f - shape.caveDensity * 0.4f;
            if (cavernNoise > cavernThreshold)
            {
                float intensity = (cavernNoise - cavernThreshold) / (1f - cavernThreshold);
                density = math.min(density, -intensity * (70f + shape.caveScale * 40f));
            }

            // --- Gentle worm tunnels (mostly horizontal) ---
            // Y-stretched sampling makes tunnels run horizontally with gentle slopes.
            float3 horzPos = new float3(worldPos.x, worldPos.y * 2f, worldPos.z);
            float horzCave1 = NoiseUtilities.Noise3D(horzPos, caveFreq * 0.8f, 2, 0.5f, 2f,
                seedOff + 150f);
            float horzCave2 = NoiseUtilities.Noise3D(horzPos, caveFreq * 0.8f, 2, 0.5f, 2f,
                seedOff + 227f);
            float horzDist2 = horzCave1 * horzCave1 + horzCave2 * horzCave2;
            float horzRadius = 0.08f + shape.caveDensity * 0.14f;
            if (horzDist2 < horzRadius * horzRadius)
            {
                float t = 1f - math.sqrt(horzDist2) / horzRadius;
                density = math.min(density, -t * 55f);
            }

            // --- Connecting worm caves (wider at depth) ---
            float caveNoise = NoiseUtilities.Noise3D(worldPos, caveFreq, 2, 0.5f, 2f, seedOff);
            float cave2 = NoiseUtilities.Noise3D(worldPos, caveFreq, 2, 0.5f, 2f, seedOff + 77.3f);
            float tubeDist2 = caveNoise * caveNoise + cave2 * cave2;
            float shallowTubeRadius = 0.08f + shape.caveDensity * 0.16f;
            if (tubeDist2 < shallowTubeRadius * shallowTubeRadius)
            {
                float t = 1f - math.sqrt(tubeDist2) / shallowTubeRadius;
                density = math.min(density, -t * 60f);
            }

            // --- Sparse vertical connections (scattered, not frequent) ---
            // Only at specific XZ positions determined by low-frequency noise
            float2 shaftXZ = new float2(worldPos.x, worldPos.z);
            float shaftGate = NoiseUtilities.Noise2D(shaftXZ, 0.003f, 1, 0.5f, 2f, seedOff + 300f);
            if (shaftGate > 0.6f) // only 20% of XZ area has vertical connections
            {
                float shaftN1 = NoiseUtilities.Noise2D(shaftXZ, 0.01f, 2, 0.5f, 2f, seedOff + 400f);
                float shaftN2 = NoiseUtilities.Noise2D(shaftXZ, 0.01f, 2, 0.5f, 2f, seedOff + 477f);
                float shaftD2 = shaftN1 * shaftN1 + shaftN2 * shaftN2;
                float shaftR = 0.03f + shape.caveDensity * 0.05f;
                if (shaftD2 < shaftR * shaftR)
                {
                    float t = 1f - math.sqrt(shaftD2) / shaftR;
                    density = math.min(density, -t * 40f);
                }
            }

            return density;
        }

        /// <summary>
        /// Y -256 to -512: Mega-caverns, wide horizontal networks, scattered vertical shafts.
        /// Deeper = bigger caverns. Mostly horizontal for player exploration.
        /// </summary>
        private static float DeepUndergroundDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);
            float density = baseDensity;

            // Depth factor: 0 at -256, 1 at -512 — caverns get bigger deeper
            float depthFactor = math.saturate((math.abs(y) - 256f) / 256f);

            // --- Mega-caverns: enormous horizontal open spaces ---
            // Y stretched 4x for extremely flat, wide cavern shapes.
            float3 megaCavPos = new float3(worldPos.x, worldPos.y * 4f, worldPos.z);
            float megaCavNoise = NoiseUtilities.Noise3D(megaCavPos,
                math.lerp(0.004f, 0.003f, depthFactor) * shape.caveScale,
                3, 0.55f, 2f, WorldSeed.SeedOffset(ChannelCavern) + 500f);
            float megaThreshold = math.lerp(0.32f, 0.25f, depthFactor) - shape.caveDensity * 0.35f;
            if (megaCavNoise > megaThreshold)
            {
                float intensity = (megaCavNoise - megaThreshold) / (1f - megaThreshold);
                density = math.min(density, -intensity * (90f + shape.caveScale * 50f + depthFactor * 30f));
            }

            // --- Wide horizontal tunnel network ---
            float deepCaveFreq = math.lerp(0.012f, 0.008f, depthFactor) * shape.caveScale;
            float3 horzPos = new float3(worldPos.x, worldPos.y * 2.5f, worldPos.z);
            float megaCaveNoise1 = NoiseUtilities.Noise3D(horzPos, deepCaveFreq, 3, 0.6f, 2f, seedOff);
            float megaCave2 = NoiseUtilities.Noise3D(horzPos, deepCaveFreq, 2, 0.5f, 2f, seedOff + 77.3f);
            float megaTubeDist2 = megaCaveNoise1 * megaCaveNoise1 + megaCave2 * megaCave2;
            float megaTubeRadius = math.lerp(0.10f, 0.14f, depthFactor) + shape.caveDensity * 0.2f;
            if (megaTubeDist2 < megaTubeRadius * megaTubeRadius)
            {
                float t = 1f - math.sqrt(megaTubeDist2) / megaTubeRadius;
                density = math.min(density, -t * 80f);
            }

            // --- Scattered vertical shafts connecting cavern layers ---
            // Very sparse — only at specific gated positions for occasional up/down routes.
            float2 shaftXZ = new float2(worldPos.x, worldPos.z);
            float shaftGate = NoiseUtilities.Noise2D(shaftXZ, 0.002f, 1, 0.5f, 2f, seedOff + 350f);
            if (shaftGate > 0.7f) // only ~15% of XZ area has shafts
            {
                float shaftNoise1 = NoiseUtilities.Noise2D(shaftXZ, 0.006f, 2, 0.5f, 2f, seedOff + 400f);
                float shaftNoise2 = NoiseUtilities.Noise2D(shaftXZ, 0.006f, 2, 0.5f, 2f, seedOff + 477f);
                float shaftDist2 = shaftNoise1 * shaftNoise1 + shaftNoise2 * shaftNoise2;
                float shaftRadius = 0.03f + shape.caveDensity * 0.06f;
                if (shaftDist2 < shaftRadius * shaftRadius)
                {
                    float t = 1f - math.sqrt(shaftDist2) / shaftRadius;
                    density = math.min(density, -t * 50f);
                }
            }

            return density;
        }

        /// <summary>
        /// Y -512 to -1000: Inverted terrain, extreme density, alien geometry.
        /// </summary>
        private static float AbyssalDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelAbyssal);

            // Inversion factor increases with depth
            float inversionStrength = math.saturate((math.abs(y) - 512f) / 488f);

            // 3D noise at multiple scales for alien geometry
            float alienNoise = NoiseUtilities.Noise3D(worldPos, 0.015f, 5, 0.55f, 2.1f, seedOff);
            float detail = NoiseUtilities.Noise3D(worldPos, 0.05f, 3, 0.5f, 2f, seedOff + 500f);

            // Blend toward inverted terrain
            float invertedDensity = -baseDensity;
            float blended = math.lerp(baseDensity, invertedDensity, inversionStrength);

            // Add extreme 3D features
            blended += (alienNoise * 1.5f + detail * 0.5f) * shape.heightScale * inversionStrength;

            return blended;
        }
    }
}
