using Unity.Collections;
using UnityEngine;
using Voidborne.World.Biomes;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// Flat lookup table indexed by biomeIdByte (0-255) for fast access to
    /// biome visual and surface properties. Built once at startup from
    /// BiomeDefinition assets. Used by mesh builder and voxel classification jobs.
    /// </summary>
    public class BiomeLookupTable
    {
        public const int MAX_BIOMES = 256;

        /// <summary>
        /// Per-biome entry with visual and surface layer properties.
        /// Blittable for Burst job compatibility.
        /// </summary>
        public struct Entry
        {
            public float colorR, colorG, colorB;
            public int   textureGroup;
            public int   surfaceMaterialIndex;
            public byte  surfaceTopId;
            public byte  surfaceSubId;
            public int   surfaceTopDepth;
            public int   surfaceSubDepth;
            public float overhangStrength;
            public float overhangFrequency;
            public float overhangMinY;
        }

        private Entry[] _entries;

        /// <summary>
        /// Build the lookup table from biome definitions.
        /// </summary>
        public void Build(BiomeDefinition[] biomes)
        {
            _entries = new Entry[MAX_BIOMES];

            // Set defaults for unclaimed IDs
            for (int i = 0; i < MAX_BIOMES; i++)
            {
                _entries[i] = new Entry
                {
                    colorR = 0.5f, colorG = 0.5f, colorB = 0.5f,
                    textureGroup = 0,
                    surfaceTopId = OreGenerator.GrassOreId,
                    surfaceSubId = OreGenerator.DirtOreId,
                    surfaceTopDepth = 2,
                    surfaceSubDepth = 4
                };
            }

            foreach (var b in biomes)
            {
                if (b == null) continue;
                int id = b.biomeIdByte;
                _entries[id] = new Entry
                {
                    colorR = b.colorTint.r,
                    colorG = b.colorTint.g,
                    colorB = b.colorTint.b,
                    textureGroup = b.textureGroup,
                    surfaceMaterialIndex = b.surfaceMaterialIndex,
                    surfaceTopId = b.surfaceTopId,
                    surfaceSubId = b.surfaceSubId,
                    surfaceTopDepth = b.surfaceTopDepth,
                    surfaceSubDepth = b.surfaceSubDepth,
                    overhangStrength = b.hasOverhangs ? b.overhangStrength : 0f,
                    overhangFrequency = b.overhangFrequency,
                    overhangMinY = b.overhangMinY
                };
            }
        }

        /// <summary>
        /// Get the entry for a given biome ID.
        /// </summary>
        public Entry Get(byte biomeId)
        {
            return _entries[biomeId];
        }

        /// <summary>
        /// Get the managed entries array (for bulk operations).
        /// </summary>
        public Entry[] GetEntries() => _entries;

        /// <summary>
        /// Create a NativeArray copy for use in Burst jobs.
        /// Caller is responsible for disposing.
        /// </summary>
        public NativeArray<Entry> ToNative()
        {
            var native = new NativeArray<Entry>(MAX_BIOMES, Allocator.Persistent);
            for (int i = 0; i < MAX_BIOMES; i++)
                native[i] = _entries[i];
            return native;
        }
    }
}
