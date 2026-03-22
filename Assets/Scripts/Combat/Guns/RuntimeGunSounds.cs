using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Generates simple procedural gunshot AudioClips at runtime.
    /// Used as a fallback when GunDefinition.fireSound is null so
    /// shooting always feels responsive without needing imported audio.
    /// Clips are cached per gun name so they are only generated once.
    /// </summary>
    public static class RuntimeGunSounds
    {
        private const int SampleRate = 22050;

        private static readonly Dictionary<string, AudioClip> _cache =
            new Dictionary<string, AudioClip>();

        /// <summary>
        /// Returns a cached procedural gunshot clip for the given gun.
        /// If gun.fireSound is set, returns that instead.
        /// </summary>
        public static AudioClip GetShot(GunDefinition gun)
        {
            if (gun.fireSound != null)
                return gun.fireSound;

            string key = gun.gunName ?? "default";
            if (_cache.TryGetValue(key, out AudioClip cached))
                return cached;

            AudioClip clip = GenerateShot(key, GetProfile(gun));
            _cache[key] = clip;
            return clip;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private struct ShotProfile
        {
            public float Pitch;       // Fundamental tone frequency multiplier
            public float Duration;    // Clip length in seconds
            public float Decay;       // Envelope decay speed (higher = shorter pop)
            public float NoiseMix;    // 0 = pure tone, 1 = pure noise (crack)
            public float Volume;      // Overall clip volume (0-1)
        }

        private static ShotProfile GetProfile(GunDefinition gun)
        {
            // Heavy hitter: sniper
            if (gun.damage >= 80f)
                return new ShotProfile { Pitch = 0.55f, Duration = 0.22f, Decay = 12f, NoiseMix = 0.55f, Volume = 0.9f };

            // Shotgun
            if (gun.pelletCount > 1)
                return new ShotProfile { Pitch = 0.70f, Duration = 0.20f, Decay = 10f, NoiseMix = 0.70f, Volume = 0.85f };

            // SMG — fast, light crack
            if (gun.fireRate >= 700f)
                return new ShotProfile { Pitch = 1.50f, Duration = 0.08f, Decay = 28f, NoiseMix = 0.65f, Volume = 0.65f };

            // AR — medium crack
            if (gun.fireRate >= 400f)
                return new ShotProfile { Pitch = 1.10f, Duration = 0.12f, Decay = 20f, NoiseMix = 0.60f, Volume = 0.75f };

            // Pistol/Revolver default
            return new ShotProfile { Pitch = 1.00f, Duration = 0.14f, Decay = 18f, NoiseMix = 0.55f, Volume = 0.80f };
        }

        private static AudioClip GenerateShot(string name, ShotProfile p)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * p.Duration);
            float[] data    = new float[sampleCount];

            // Use a deterministic seed based on the gun name so each gun
            // sounds slightly different even with the same profile.
            System.Random rng = new System.Random(name.GetHashCode());

            // Low-frequency tone simulating the pressure wave / body of the shot.
            float toneHz = 80f * p.Pitch;

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / SampleRate;          // Time in seconds
                float tNorm    = (float)i / sampleCount;          // 0..1 progress

                // Attack: very short ramp up (< 1 ms) then exponential decay
                float attack   = Mathf.Min(1f, (float)i / (SampleRate * 0.001f));
                float envelope = attack * Mathf.Exp(-tNorm * sampleCount / (SampleRate * p.Duration / p.Decay));

                float noise    = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone     = Mathf.Sin(2f * Mathf.PI * toneHz * t);

                // Add subtle sub-bass thump at the very start
                float thump    = Mathf.Sin(2f * Mathf.PI * 40f * p.Pitch * t)
                                 * Mathf.Exp(-tNorm * sampleCount / (SampleRate * 0.04f));

                data[i] = (noise * p.NoiseMix + tone * (1f - p.NoiseMix) + thump * 0.3f)
                          * envelope * p.Volume;
            }

            AudioClip clip = AudioClip.Create(name + "_proc", sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
