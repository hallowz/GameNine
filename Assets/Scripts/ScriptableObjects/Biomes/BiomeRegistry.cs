using UnityEngine;
using Voidborne.World.Generation;

namespace Voidborne.World.Biomes
{
    /// <summary>
    /// Provides default biome definitions as code-based fallbacks when
    /// ScriptableObject assets are not available. Each biome is defined by its
    /// ideal position in 4D climate space (temperature, moisture, continentalness, erosion).
    /// </summary>
    public static class BiomeRegistry
    {
        private static BiomeDefinition[] _defaults;

        /// <summary>
        /// Returns the array of default biome definitions.
        /// These are created once and cached for the lifetime of the application.
        /// </summary>
        public static BiomeDefinition[] GetDefaults()
        {
            if (_defaults != null)
                return _defaults;

            // First 4 biomes map to shader color slots _Color0-_Color3,
            // so they need maximum visual variety (green, warm, cold, dark).
            _defaults = new BiomeDefinition[]
            {
                CreatePlains(),          // 0: yellow-green (temperate)
                CreateVolcanicWastes(),  // 1: dark red-brown (hot extreme)
                CreateTundra(),          // 2: pale icy grey (cold extreme)
                CreateSavanna(),         // 3: golden tan (warm dry)
                CreateDenseForest(),     // 4+: blended from above 4 in shader
                CreateRiverValley(),
                CreateHighlands(),
                CreateOverhangCliffs(),
                CreateAlpinePeaks(),
                CreateGrandHills(),
                CreateToweringBluffs(),
                CreateLushCaverns(),
                CreateCrystalCaves()
            };

            return _defaults;
        }

        // =========================================================================
        //  SURFACE BIOMES
        // =========================================================================

        private static BiomeDefinition CreatePlains()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Plains";
            b.biomeId = 4;
            b.biomeIdByte = 1;
            // Climate: temperate, moderate moisture, low continent (flat), high erosion (smooth)
            b.idealTemperature = 0.45f;
            b.idealMoisture = 0.35f;
            b.idealContinentalness = 0.2f;
            b.idealErosion = 0.75f;
            b.colorTint = new Color(0.5f, 0.75f, 0.25f); // light yellow-green
            b.textureGroup = 0; // grassy
            b.surfaceMaterialIndex = 4;
            b.surfaceTopId = OreGenerator.GrassPlainsId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 4;
            return b;
        }

        private static BiomeDefinition CreateDenseForest()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Dense Forest";
            b.biomeId = 5;
            b.biomeIdByte = 2;
            // Climate: warm, wet, low-moderate continent, moderate erosion
            b.idealTemperature = 0.6f;
            b.idealMoisture = 0.72f;
            b.idealContinentalness = 0.3f;
            b.idealErosion = 0.55f;
            b.colorTint = new Color(0.1f, 0.5f, 0.1f); // deep green
            b.textureGroup = 0; // grassy
            b.surfaceMaterialIndex = 5;
            b.surfaceTopId = OreGenerator.GrassForestId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 5;
            return b;
        }

        private static BiomeDefinition CreateRiverValley()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "River Valley";
            b.biomeId = 6;
            b.biomeIdByte = 3;
            // Climate: cool, very wet, low continent, high erosion (carved smooth)
            b.idealTemperature = 0.38f;
            b.idealMoisture = 0.78f;
            b.idealContinentalness = 0.15f;
            b.idealErosion = 0.8f;
            b.colorTint = new Color(0.3f, 0.6f, 0.3f); // medium green
            b.textureGroup = 0; // grassy
            b.surfaceMaterialIndex = 6;
            b.surfaceTopId = OreGenerator.GrassValleyId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 4;
            return b;
        }

        private static BiomeDefinition CreateHighlands()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Highlands";
            b.biomeId = 7;
            b.biomeIdByte = 4;
            // Climate: cool, moderate moisture, moderate-high continent, low erosion (rough)
            b.idealTemperature = 0.25f;
            b.idealMoisture = 0.45f;
            b.idealContinentalness = 0.6f;
            b.idealErosion = 0.35f;
            b.colorTint = new Color(0.5f, 0.55f, 0.4f); // muted olive
            b.textureGroup = 3; // rocky
            b.surfaceMaterialIndex = 7;
            b.surfaceTopId = OreGenerator.GrassHighlandId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 1;
            b.surfaceSubDepth = 3;
            return b;
        }

        private static BiomeDefinition CreateVolcanicWastes()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Volcanic Wastes";
            b.biomeId = 8;
            b.biomeIdByte = 5;
            // Climate: very hot, very dry, high continent, low erosion (jagged)
            b.idealTemperature = 0.9f;
            b.idealMoisture = 0.1f;
            b.idealContinentalness = 0.7f;
            b.idealErosion = 0.2f;
            b.colorTint = new Color(0.25f, 0.1f, 0.05f); // dark red-brown
            b.textureGroup = 1; // volcanic
            b.surfaceMaterialIndex = 8;
            b.surfaceTopId = 10; // Rock at surface for volcanic
            b.surfaceSubId = 10;
            b.surfaceTopDepth = 3;
            b.surfaceSubDepth = 0;
            return b;
        }

        private static BiomeDefinition CreateOverhangCliffs()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Overhang Cliffs";
            b.biomeId = 9;
            b.biomeIdByte = 6;
            // Climate: cool-moderate, moderate moisture, moderate continent, low erosion
            b.idealTemperature = 0.35f;
            b.idealMoisture = 0.48f;
            b.idealContinentalness = 0.55f;
            b.idealErosion = 0.3f;
            b.hasOverhangs = true;
            b.overhangStrength = 0.35f;
            b.overhangFrequency = 0.018f;
            b.overhangMinY = 20f;
            b.colorTint = new Color(0.55f, 0.5f, 0.45f); // stone grey
            b.textureGroup = 1; // volcanic/rocky
            b.surfaceMaterialIndex = 9;
            b.surfaceTopId = OreGenerator.GrassCliffId;
            b.surfaceSubId = OreGenerator.RockOreId; // Rock sub-surface for cliffs
            b.surfaceTopDepth = 1;
            b.surfaceSubDepth = 2;
            return b;
        }

        private static BiomeDefinition CreateSavanna()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Savanna";
            b.biomeId = 10;
            b.biomeIdByte = 7;
            // Climate: hot, dry, low continent, high erosion (flat dry)
            b.idealTemperature = 0.75f;
            b.idealMoisture = 0.22f;
            b.idealContinentalness = 0.25f;
            b.idealErosion = 0.7f;
            b.colorTint = new Color(0.75f, 0.65f, 0.3f); // golden tan
            b.textureGroup = 3; // dry/sandy
            b.surfaceMaterialIndex = 10;
            b.surfaceTopId = 7;  // Sand at surface
            b.surfaceSubId = 8;  // Dirt underneath
            b.surfaceTopDepth = 3;
            b.surfaceSubDepth = 3;
            return b;
        }

        private static BiomeDefinition CreateTundra()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Tundra";
            b.biomeId = 11;
            b.biomeIdByte = 8;
            // Climate: very cold, dry, low continent, high erosion (flat frozen)
            b.idealTemperature = 0.08f;
            b.idealMoisture = 0.2f;
            b.idealContinentalness = 0.2f;
            b.idealErosion = 0.7f;
            b.colorTint = new Color(0.85f, 0.9f, 0.95f); // pale icy grey
            b.textureGroup = 2; // cold/icy
            b.surfaceMaterialIndex = 11;
            b.surfaceTopId = OreGenerator.GrassTundraId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 4;
            return b;
        }

        private static BiomeDefinition CreateAlpinePeaks()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Alpine Peaks";
            b.biomeId = 12;
            b.biomeIdByte = 9;
            // Climate: cold, moderate moisture, very high continent, very low erosion (massive peaks)
            b.idealTemperature = 0.12f;
            b.idealMoisture = 0.35f;
            b.idealContinentalness = 0.95f;
            b.idealErosion = 0.1f;
            b.hasOverhangs = true;
            b.overhangStrength = 0.25f;
            b.overhangFrequency = 0.015f;
            b.overhangMinY = 40f;
            b.colorTint = new Color(0.6f, 0.6f, 0.65f); // cold grey stone
            b.textureGroup = 2; // cold/icy
            b.surfaceMaterialIndex = 12;
            b.surfaceTopId = 10; // Rock at high peaks
            b.surfaceSubId = 10;
            b.surfaceTopDepth = 1;
            b.surfaceSubDepth = 0;
            return b;
        }

        private static BiomeDefinition CreateGrandHills()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Grand Hills";
            b.biomeId = 13;
            b.biomeIdByte = 10;
            // Climate: temperate, moderate moisture, high continent, moderate erosion (rolling)
            b.idealTemperature = 0.5f;
            b.idealMoisture = 0.5f;
            b.idealContinentalness = 0.7f;
            b.idealErosion = 0.5f;
            b.colorTint = new Color(0.4f, 0.65f, 0.3f); // rich green
            b.textureGroup = 0; // grassy
            b.surfaceMaterialIndex = 13;
            b.surfaceTopId = OreGenerator.GrassHillId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 4;
            return b;
        }

        private static BiomeDefinition CreateToweringBluffs()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Towering Bluffs";
            b.biomeId = 14;
            b.biomeIdByte = 11;
            // Climate: cool-moderate, dry, high continent, low erosion (dramatic cliffs)
            b.idealTemperature = 0.4f;
            b.idealMoisture = 0.18f;
            b.idealContinentalness = 0.85f;
            b.idealErosion = 0.15f;
            b.hasOverhangs = true;
            b.overhangStrength = 0.4f;
            b.overhangFrequency = 0.02f;
            b.overhangMinY = 30f;
            b.colorTint = new Color(0.6f, 0.5f, 0.35f); // sandy cliff
            b.textureGroup = 3; // dry/rocky
            b.surfaceMaterialIndex = 14;
            b.surfaceTopId = 10; // Rock surface for bluffs
            b.surfaceSubId = 8;
            b.surfaceTopDepth = 2;
            b.surfaceSubDepth = 3;
            return b;
        }

        // =========================================================================
        //  CAVE BIOMES (underground, depth > 0)
        // =========================================================================

        private static BiomeDefinition CreateLushCaverns()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Lush Caverns";
            b.biomeId = 15;
            b.biomeIdByte = 12;
            // Climate: warm, wet, moderate continent — but underground
            b.idealTemperature = 0.55f;
            b.idealMoisture = 0.7f;
            b.idealContinentalness = 0.4f;
            b.idealErosion = 0.5f;
            b.depthMin = 0.3f;
            b.depthMax = 1.0f;
            b.colorTint = new Color(0.2f, 0.6f, 0.3f); // cave green
            b.textureGroup = 0;
            b.surfaceMaterialIndex = 15;
            b.surfaceTopId = OreGenerator.GrassCavernId;
            b.surfaceSubId = OreGenerator.DirtOreId;
            b.surfaceTopDepth = 1;
            b.surfaceSubDepth = 2;
            return b;
        }

        private static BiomeDefinition CreateCrystalCaves()
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.biomeName = "Crystal Caves";
            b.biomeId = 16;
            b.biomeIdByte = 13;
            // Climate: cold, dry, high continent — deep underground
            b.idealTemperature = 0.2f;
            b.idealMoisture = 0.15f;
            b.idealContinentalness = 0.6f;
            b.idealErosion = 0.3f;
            b.depthMin = 0.7f;
            b.depthMax = 1.5f;
            b.colorTint = new Color(0.5f, 0.5f, 0.8f); // crystal blue-grey
            b.textureGroup = 2;
            b.surfaceMaterialIndex = 16;
            b.surfaceTopId = 10; // Rock
            b.surfaceSubId = 10;
            b.surfaceTopDepth = 1;
            b.surfaceSubDepth = 0;
            return b;
        }
    }
}
