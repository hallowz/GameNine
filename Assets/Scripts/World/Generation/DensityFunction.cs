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
        private const int ChannelNoodle = 8;            // noodle cave noise (thin connectors)
        private const int ChannelBiomeOverhang = 12;    // W1.1 — biome-driven overhangs
        private const int ChannelRoad = 22;             // surface road Voronoi
        private const int ChannelRoadShallow = 23;      // shallow underground road Voronoi
        private const int ChannelRoadDeep = 24;         // deep underground road Voronoi
        private const int ChannelRoadElevation = 25;    // road elevation smoothing noise
        private const int ChannelSkyBiomeType = 30;     // sky biome type selection noise
        private const int ChannelSkyIsland = 31;        // sky island mask/shape noise
        private const int ChannelStalactite = 32;       // stalactite drip detail noise

        // Road generation constants
        public const float RoadCellSize = 400f;         // Voronoi cell size for surface roads
        public const float RoadHalfWidth = 10f;         // road half-width in voxels (wider for driving)
        private const float RoadMaxGrade = 0.15f;       // 15% max road slope
        private const float ShallowTunnelCellSize = 300f;
        private const float DeepTunnelCellSize = 500f;
        private const float TunnelHalfWidth = 5f;
        private const float TunnelHeight = 6f;          // tunnel vertical clearance
        private const float TunnelWidth = 10f;          // tunnel horizontal width

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
        //  ROAD HELPERS
        // =========================================================================

        /// <summary>
        /// Computes a smoothed road target elevation using low-frequency noise.
        /// This gives roads a gently undulating profile independent of sharp terrain features.
        /// The road elevation is much flatter than the terrain — uses only 15% of heightScale.
        /// </summary>
        private static float GetRoadElevation(float2 xz, TerrainShapeData shape)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelRoadElevation);
            // Very low frequency → slow, gentle elevation changes along roads
            float roadHeight = NoiseUtilities.Noise2D(xz, 0.0005f, 2, 0.5f, 2f, seedOff);
            return roadHeight * shape.heightScale * 0.15f;
        }

        /// <summary>
        /// Carves road density: flattens terrain along Voronoi edges.
        /// Returns modified density. roadInfluence is [0,1] where 1 = road center.
        /// Uses a sharp road profile: solid below, air above, with slight shoulder ramp.
        /// </summary>
        private static float ApplyRoadCarving(float baseDensity, float worldY,
            float roadElevation, float roadInfluence)
        {
            if (roadInfluence <= 0f) return baseDensity;

            float distAboveRoad = worldY - roadElevation;

            // Sharp road profile:
            //   > 1 voxel above road surface → carved to air
            //   0-1 voxel above → transition
            //   below road → solid (fill valleys)
            float roadDensity;
            if (distAboveRoad > 1f)
                roadDensity = -distAboveRoad * 8f; // strong air above road
            else if (distAboveRoad > 0f)
                roadDensity = math.lerp(2f, -8f, distAboveRoad); // transition at surface
            else
                roadDensity = math.min(baseDensity, 2f - distAboveRoad * 4f); // solid below, fills valleys

            // Blend: road influence squared for sharper edges
            float blendFactor = roadInfluence * roadInfluence;
            return math.lerp(baseDensity, roadDensity, blendFactor);
        }

        /// <summary>
        /// Evaluates road influence at an XZ position for the surface road network.
        /// Public so VoxelClassificationJob and ChunkMeshBuilder can use it.
        /// </summary>
        public static float GetRoadInfluence(float2 worldXZ)
        {
            return NoiseUtilities.RoadInfluence(worldXZ, RoadCellSize, RoadHalfWidth,
                WorldSeed.SeedOffset(ChannelRoad));
        }

        /// <summary>
        /// Compute a smoothed surface height for road flattening.
        /// Uses only 2 octaves (vs the full set) so high-frequency bumps are removed
        /// but the general terrain shape is preserved. Roads follow the landscape
        /// but are gentler — no sharp hills or dips.
        /// </summary>
        private static float GetSmoothedSurfaceHeight(float x, float z, TerrainShapeData shape)
        {
            float2 pos = new float2(x, z);
            float seedOff = WorldSeed.SeedOffset(ChannelSurfaceHeight);

            // 2 octaves at same frequency, no domain warp, 70% amplitude
            // Removes fine detail bumps while keeping the broad terrain shape
            float heightNoise = NoiseUtilities.Noise2D(pos, shape.heightFrequency,
                2, shape.persistence, shape.lacunarity, seedOff);

            return heightNoise * shape.heightScale * 0.7f;
        }

        // =========================================================================
        //  CAVE HELPERS — Minecraft-inspired cave types
        // =========================================================================

        /// <summary>
        /// Noodle caves: thin connecting passages (3-5 voxels wide).
        /// Higher frequency than spaghetti, adds fine-grain connectivity.
        /// </summary>
        private static float NoodleCaveDensity(float3 worldPos, float caveScale, float caveDensity)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelNoodle);
            float freq = 0.03f * caveScale;
            float n1 = NoiseUtilities.Noise3D(worldPos, freq, 2, 0.5f, 2f, seedOff);
            float n2 = NoiseUtilities.Noise3D(worldPos, freq, 2, 0.5f, 2f, seedOff + 91.7f);
            float dist2 = n1 * n1 + n2 * n2;
            float radius = 0.04f + caveDensity * 0.06f; // thin tunnels
            if (dist2 < radius * radius)
            {
                float t = 1f - math.sqrt(dist2) / radius;
                return -t * 15f;
            }
            return 0f; // no carving
        }

        /// <summary>
        /// Noise pillars inside cheese caves: adds density back where a secondary noise is high.
        /// Prevents "boring empty room" by creating navigable columns/obstacles.
        /// </summary>
        private static float CheesePillarDensity(float3 worldPos, float caveScale)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCavern) + 300f;
            float pillarNoise = NoiseUtilities.Noise3D(
                new float3(worldPos.x, worldPos.y * 0.3f, worldPos.z),
                0.02f * caveScale, 2, 0.5f, 2f, seedOff);
            // Pillars where noise > 0.3
            if (pillarNoise > 0.3f)
            {
                float strength = (pillarNoise - 0.3f) / 0.7f;
                return strength * 40f; // add solid density back
            }
            return 0f;
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
        /// Y -128 to 128: Surface terrain with roads, caves, ravines, and cave entrances.
        /// Cave system reworked: fewer but wider/longer spaghetti caves (driveable),
        /// noodle caves for connectivity, cave entrances that breach surface.
        /// Roads carved as Voronoi edge flattening.
        /// </summary>
        private static float SurfaceBandDensity(float3 worldPos, TerrainShapeData shape, BiomeData biome, float baseDensity, float surfaceHeight)
        {
            float2 xz = new float2(worldPos.x, worldPos.z);
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);
            float density = baseDensity;

            // Cave frequency and radius scale with shape.caveScale
            float caveFreq = 0.02f * shape.caveScale;
            float caveNoise = NoiseUtilities.Noise3D(worldPos, caveFreq, 2, 0.5f, 2f, seedOff);

            // Smaller overhang contribution at surface level
            float overhang = 0f;
            if (worldPos.y >= 0f)
            {
                overhang = caveNoise * shape.heightScale * 0.1f;
            }

            float depthBelowSurface = surfaceHeight - worldPos.y;

            // =================================================================
            //  ROADS — gentle terrain flattening along Voronoi edges
            //  Blends toward a smoothed (low-octave) surface height so roads
            //  follow the landscape but with less steep hills/dips.
            // =================================================================
            float roadInf = GetRoadInfluence(xz);
            if (roadInf > 0f)
            {
                float smoothHeight = GetSmoothedSurfaceHeight(worldPos.x, worldPos.z, shape);
                float smoothDensity = -worldPos.y + smoothHeight;
                // Squared influence for sharp road edges, 70% blend — noticeable but not jarring
                float blend = roadInf * roadInf * 0.7f;
                density = math.lerp(density, smoothDensity, blend);
            }

            // =================================================================
            //  RAVINES — narrow deep gashes, scarce
            // =================================================================
            float ravineMaxDepth = 60f + shape.caveScale * 40f;
            float skyProximityFade = math.saturate((SkyTransitionStart - worldPos.y) / 32f);
            if (depthBelowSurface > 0f && depthBelowSurface < ravineMaxDepth && skyProximityFade > 0f)
            {
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
                        density = math.min(density, -falloff * strength * skyProximityFade * gateFade);
                    }
                }
            }

            // =================================================================
            //  SPAGHETTI CAVES — more horizontal for driving, gated to be less frequent
            //  Surface guard at 8 voxels depth; caves that naturally approach the
            //  surface create entrances via a gentle fade (not forced widening).
            //  Y-stretched 2x in surface band for more horizontal passages.
            // =================================================================
            if (depthBelowSurface > 8f)
            {
                // Horizontal stretch: Y*2 makes tunnels run mostly horizontal
                float3 horzPos = new float3(worldPos.x, worldPos.y * 2f, worldPos.z);
                float spaghettiFreq = caveFreq * 0.8f; // slightly lower freq = longer passages
                float spag1 = NoiseUtilities.Noise3D(horzPos, spaghettiFreq, 2, 0.5f, 2f, seedOff);
                float spag2 = NoiseUtilities.Noise3D(horzPos, spaghettiFreq, 2, 0.5f, 2f, seedOff + 77.3f);
                float spagDist2 = spag1 * spag1 + spag2 * spag2;

                // Driveable radius but not overwhelming — close to original but wider
                float spagRadius = 0.07f + shape.caveDensity * 0.10f;

                // Fade out carving strength near surface (smooth entrance, not abrupt cutoff)
                float surfaceFade = math.smoothstep(8f, 20f, depthBelowSurface);

                if (spagDist2 < spagRadius * spagRadius)
                {
                    float t = 1f - math.sqrt(spagDist2) / spagRadius;
                    float carveStr = (25f + shape.caveScale * 20f) * surfaceFade;
                    density = math.min(density, -t * carveStr);
                }
            }

            // =================================================================
            //  NOODLE CAVES — thin connecting passages (deeper only)
            // =================================================================
            if (depthBelowSurface > 15f)
            {
                float noodleDen = NoodleCaveDensity(worldPos, shape.caveScale, shape.caveDensity);
                if (noodleDen < 0f)
                    density = math.min(density, noodleDen);
            }

            // =================================================================
            //  W1.1 — biome-driven overhangs
            // =================================================================
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

            return density + overhang;
        }

        // =========================================================================
        //  UNDERGROUND ZONES — Mostly horizontal caves, big caverns at depth
        // =========================================================================

        /// <summary>
        /// Y -128 to -256: Cheese caves with pillars, wide spaghetti tunnels (driveable),
        /// noodle connectors, road tunnels, and surface-to-underground ramp connections.
        /// </summary>
        private static float ShallowUndergroundDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);
            float2 xz = new float2(worldPos.x, worldPos.z);
            float density = baseDensity;

            // =================================================================
            //  CHEESE CAVES — large chambers with noise pillars (raised threshold = fewer)
            // =================================================================
            float3 cavernPos = new float3(worldPos.x, worldPos.y * 3f, worldPos.z);
            float cavernNoise = NoiseUtilities.Noise3D(cavernPos, 0.005f * shape.caveScale, 2, 0.5f, 2f,
                WorldSeed.SeedOffset(ChannelCavern));
            float cavernThreshold = 0.45f - shape.caveDensity * 0.35f; // raised from 0.35 → fewer chambers
            if (cavernNoise > cavernThreshold)
            {
                float intensity = (cavernNoise - cavernThreshold) / (1f - cavernThreshold);
                float caveDen = -intensity * (70f + shape.caveScale * 40f);
                // Add pillars inside cheese caves
                caveDen += CheesePillarDensity(worldPos, shape.caveScale);
                density = math.min(density, caveDen);
            }

            // =================================================================
            //  SPAGHETTI CAVES — wider, more horizontal (driveable)
            //  Y-stretched 2x, wider radius, lower frequency for longer passages
            // =================================================================
            float3 horzPos = new float3(worldPos.x, worldPos.y * 2f, worldPos.z);
            float spagFreq = 0.014f * shape.caveScale; // lower freq = longer passages
            float spag1 = NoiseUtilities.Noise3D(horzPos, spagFreq, 2, 0.5f, 2f, seedOff + 150f);
            float spag2 = NoiseUtilities.Noise3D(horzPos, spagFreq, 2, 0.5f, 2f, seedOff + 227f);
            float spagDist2 = spag1 * spag1 + spag2 * spag2;
            float spagRadius = 0.12f + shape.caveDensity * 0.14f; // wider for driving
            if (spagDist2 < spagRadius * spagRadius)
            {
                float t = 1f - math.sqrt(spagDist2) / spagRadius;
                density = math.min(density, -t * 60f);
            }

            // =================================================================
            //  NOODLE CAVES — thin connecting passages
            // =================================================================
            float noodleDen = NoodleCaveDensity(worldPos, shape.caveScale, shape.caveDensity);
            if (noodleDen < 0f)
                density = math.min(density, noodleDen);

            // =================================================================
            //  ROAD TUNNELS — Voronoi-based connected tunnel network
            //  Flat-bottomed rectangular tunnels along Voronoi edges
            // =================================================================
            float tunnelSeed = WorldSeed.SeedOffset(ChannelRoadShallow);
            NoiseUtilities.Worley2D(xz, ShallowTunnelCellSize, tunnelSeed,
                out float tF1, out float tF2, out float2 _, out float2 tunnelCenter);

            float tunnelEdgeDist = tF2 - tF1;
            if (tunnelEdgeDist < TunnelWidth)
            {
                // Tunnel target Y: gently undulating within the layer
                float tunnelTargetY = math.lerp(-192f, -160f,
                    NoiseUtilities.Noise2D(xz, 0.002f, 2, 0.5f, 2f, tunnelSeed + 50f) * 0.5f + 0.5f);

                float yDistFromFloor = worldPos.y - tunnelTargetY;
                float xInfluence = 1f - math.smoothstep(0f, TunnelWidth, tunnelEdgeDist);

                // Flat-bottomed tunnel: carve if within height range
                if (yDistFromFloor >= 0f && yDistFromFloor < TunnelHeight)
                {
                    // Taper at ceiling and edges
                    float ceilFade = math.smoothstep(TunnelHeight, TunnelHeight - 1.5f, yDistFromFloor);
                    density = math.min(density, -xInfluence * ceilFade * 50f);
                }
                // Flatten floor: fill below tunnel floor
                else if (yDistFromFloor < 0f && yDistFromFloor > -3f)
                {
                    float fillStr = xInfluence * math.smoothstep(-3f, 0f, yDistFromFloor);
                    density = math.max(density, fillStr * 30f);
                }
            }

            // =================================================================
            //  SURFACE-TO-UNDERGROUND RAMP CONNECTIONS
            //  Where surface road F1 center is near shallow tunnel F1 center
            // =================================================================
            float surfRoadSeed = WorldSeed.SeedOffset(ChannelRoad);
            NoiseUtilities.Worley2D(xz, RoadCellSize, surfRoadSeed,
                out float srF1, out float srF2, out float2 srCell, out float2 surfRoadCenter);

            float rampProximity = math.length(new float2(surfRoadCenter.x - tunnelCenter.x,
                                                          surfRoadCenter.y - tunnelCenter.y));
            if (rampProximity < 120f)
            {
                // Ramp zone: carve a sloped passage from surface down to tunnel layer
                float rampInfluence = 1f - math.smoothstep(0f, 120f, rampProximity);
                float rampCenterX = (surfRoadCenter.x + tunnelCenter.x) * 0.5f;
                float rampCenterZ = (surfRoadCenter.y + tunnelCenter.y) * 0.5f;
                float distFromRampCenter = math.length(xz - new float2(rampCenterX, rampCenterZ));

                if (distFromRampCenter < TunnelWidth * 1.5f)
                {
                    float rampXInf = 1f - math.smoothstep(0f, TunnelWidth * 1.5f, distFromRampCenter);
                    // Ramp: linear slope from surface (Y ~0) down to tunnel (Y ~ -170)
                    float surfY = GetRoadElevation(xz, shape);
                    float tunnelY = math.lerp(-192f, -160f,
                        NoiseUtilities.Noise2D(xz, 0.002f, 2, 0.5f, 2f, tunnelSeed + 50f) * 0.5f + 0.5f);
                    float rampY = math.lerp(surfY, tunnelY, rampInfluence);

                    float yAboveRamp = worldPos.y - rampY;
                    if (yAboveRamp >= 0f && yAboveRamp < TunnelHeight * 1.2f)
                    {
                        float ceilFade = math.smoothstep(TunnelHeight * 1.2f, TunnelHeight * 0.8f, yAboveRamp);
                        density = math.min(density, -rampXInf * ceilFade * rampInfluence * 45f);
                    }
                }
            }

            // =================================================================
            //  SPARSE VERTICAL SHAFTS — occasional up/down connections
            // =================================================================
            float shaftGate = NoiseUtilities.Noise2D(xz, 0.003f, 1, 0.5f, 2f, seedOff + 300f);
            if (shaftGate > 0.65f) // ~17% of area
            {
                float shaftN1 = NoiseUtilities.Noise2D(xz, 0.01f, 2, 0.5f, 2f, seedOff + 400f);
                float shaftN2 = NoiseUtilities.Noise2D(xz, 0.01f, 2, 0.5f, 2f, seedOff + 477f);
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
        /// Y -256 to -512: Mega cheese caves with pillars, wide spaghetti highways,
        /// noodle connectors, deep road tunnels. Fewer but bigger.
        /// </summary>
        private static float DeepUndergroundDensity(float3 worldPos, TerrainShapeData shape, float baseDensity, float y)
        {
            float seedOff = WorldSeed.SeedOffset(ChannelCave3D);
            float2 xz = new float2(worldPos.x, worldPos.z);
            float density = baseDensity;

            float depthFactor = math.saturate((math.abs(y) - 256f) / 256f);

            // =================================================================
            //  MEGA CHEESE CAVES — raised threshold, with pillars
            // =================================================================
            float3 megaCavPos = new float3(worldPos.x, worldPos.y * 4f, worldPos.z);
            float megaCavNoise = NoiseUtilities.Noise3D(megaCavPos,
                math.lerp(0.004f, 0.003f, depthFactor) * shape.caveScale,
                3, 0.55f, 2f, WorldSeed.SeedOffset(ChannelCavern) + 500f);
            float megaThreshold = math.lerp(0.40f, 0.32f, depthFactor) - shape.caveDensity * 0.30f; // raised
            if (megaCavNoise > megaThreshold)
            {
                float intensity = (megaCavNoise - megaThreshold) / (1f - megaThreshold);
                float caveDen = -intensity * (90f + shape.caveScale * 50f + depthFactor * 30f);
                caveDen += CheesePillarDensity(worldPos, shape.caveScale);
                density = math.min(density, caveDen);
            }

            // =================================================================
            //  SPAGHETTI HIGHWAYS — wider than shallow, driveable throughout
            // =================================================================
            float deepSpagFreq = math.lerp(0.010f, 0.007f, depthFactor) * shape.caveScale;
            float3 horzPos = new float3(worldPos.x, worldPos.y * 2.5f, worldPos.z);
            float spag1 = NoiseUtilities.Noise3D(horzPos, deepSpagFreq, 3, 0.6f, 2f, seedOff);
            float spag2 = NoiseUtilities.Noise3D(horzPos, deepSpagFreq, 2, 0.5f, 2f, seedOff + 77.3f);
            float spagDist2 = spag1 * spag1 + spag2 * spag2;
            float spagRadius = math.lerp(0.12f, 0.16f, depthFactor) + shape.caveDensity * 0.18f;
            if (spagDist2 < spagRadius * spagRadius)
            {
                float t = 1f - math.sqrt(spagDist2) / spagRadius;
                density = math.min(density, -t * 80f);
            }

            // =================================================================
            //  NOODLE CAVES — thin connectors
            // =================================================================
            float noodleDen = NoodleCaveDensity(worldPos, shape.caveScale, shape.caveDensity);
            if (noodleDen < 0f)
                density = math.min(density, noodleDen);

            // =================================================================
            //  DEEP ROAD TUNNELS — sparser, wider than shallow
            // =================================================================
            float deepTunnelSeed = WorldSeed.SeedOffset(ChannelRoadDeep);
            float deepEdgeDist = NoiseUtilities.WorleyEdgeDist(xz, DeepTunnelCellSize, deepTunnelSeed);

            float deepTunnelWidth = TunnelWidth * 1.3f;
            if (deepEdgeDist < deepTunnelWidth)
            {
                float tunnelTargetY = math.lerp(-384f, -320f,
                    NoiseUtilities.Noise2D(xz, 0.0015f, 2, 0.5f, 2f, deepTunnelSeed + 50f) * 0.5f + 0.5f);

                float yDistFromFloor = worldPos.y - tunnelTargetY;
                float xInfluence = 1f - math.smoothstep(0f, deepTunnelWidth, deepEdgeDist);
                float deepTunnelH = TunnelHeight * 1.3f;

                if (yDistFromFloor >= 0f && yDistFromFloor < deepTunnelH)
                {
                    float ceilFade = math.smoothstep(deepTunnelH, deepTunnelH - 2f, yDistFromFloor);
                    density = math.min(density, -xInfluence * ceilFade * 50f);
                }
                else if (yDistFromFloor < 0f && yDistFromFloor > -3f)
                {
                    float fillStr = xInfluence * math.smoothstep(-3f, 0f, yDistFromFloor);
                    density = math.max(density, fillStr * 30f);
                }
            }

            // =================================================================
            //  SCATTERED VERTICAL SHAFTS
            // =================================================================
            float shaftGate = NoiseUtilities.Noise2D(xz, 0.002f, 1, 0.5f, 2f, seedOff + 350f);
            if (shaftGate > 0.7f)
            {
                float shaftN1 = NoiseUtilities.Noise2D(xz, 0.006f, 2, 0.5f, 2f, seedOff + 400f);
                float shaftN2 = NoiseUtilities.Noise2D(xz, 0.006f, 2, 0.5f, 2f, seedOff + 477f);
                float shaftD2 = shaftN1 * shaftN1 + shaftN2 * shaftN2;
                float shaftR = 0.03f + shape.caveDensity * 0.06f;
                if (shaftD2 < shaftR * shaftR)
                {
                    float t = 1f - math.sqrt(shaftD2) / shaftR;
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
