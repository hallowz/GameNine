using UnityEngine;
using Voidborne.Diagnostics;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Manages the procedural skybox material and picks random sky variations
    /// each day and night. Works alongside DayNightCycle which drives time.
    /// </summary>
    [RequireComponent(typeof(DayNightCycle))]
    public class SkyVariationManager : MonoBehaviour
    {
        [Header("Skybox Material")]
        [Tooltip("Material using Voidborne/ProceduralSkybox shader. Created at runtime if null.")]
        public Material skyboxMaterial;

        [Header("Day Sky")]
        public Color dayZenith = new Color(0.15f, 0.45f, 0.85f);
        public Color dayHorizon = new Color(0.55f, 0.78f, 0.95f);

        [Header("Night Sky")]
        public Color nightZenith = new Color(0.005f, 0.008f, 0.025f);
        public Color nightHorizon = new Color(0.015f, 0.015f, 0.04f);

        [Header("Stars")]
        public float starBrightness = 1.0f;
        public float starDensity = 22f;
        public float starTwinkleSpeed = 1.5f;

        [Header("Moon")]
        public float moonBrightness = 0.8f;
        public float moonDiscSize = 0.003f;

        // ── Variation definitions ────────────────────────────────────────────

        private static readonly SunriseVariation[] SunriseVariations = new[]
        {
            new SunriseVariation // Classic warm orange
            {
                glowColor = new Color(1.0f, 0.45f, 0.12f),
                horizonTint = new Color(0.95f, 0.6f, 0.35f),
                sunColor = new Color(1.0f, 0.7f, 0.3f),
                glowIntensity = 1.1f,
                zenithTint = new Color(0.25f, 0.35f, 0.65f)
            },
            new SunriseVariation // Soft pink and lavender
            {
                glowColor = new Color(0.95f, 0.45f, 0.55f),
                horizonTint = new Color(0.9f, 0.55f, 0.65f),
                sunColor = new Color(1.0f, 0.65f, 0.5f),
                glowIntensity = 0.9f,
                zenithTint = new Color(0.35f, 0.3f, 0.6f)
            },
            new SunriseVariation // Deep red and gold
            {
                glowColor = new Color(0.9f, 0.25f, 0.08f),
                horizonTint = new Color(0.85f, 0.4f, 0.15f),
                sunColor = new Color(1.0f, 0.55f, 0.15f),
                glowIntensity = 1.3f,
                zenithTint = new Color(0.2f, 0.2f, 0.5f)
            },
            new SunriseVariation // Pale peach and blue
            {
                glowColor = new Color(1.0f, 0.7f, 0.45f),
                horizonTint = new Color(0.95f, 0.75f, 0.6f),
                sunColor = new Color(1.0f, 0.85f, 0.55f),
                glowIntensity = 0.7f,
                zenithTint = new Color(0.3f, 0.45f, 0.75f)
            },
            new SunriseVariation // Fiery crimson
            {
                glowColor = new Color(0.95f, 0.2f, 0.1f),
                horizonTint = new Color(0.9f, 0.35f, 0.2f),
                sunColor = new Color(1.0f, 0.5f, 0.2f),
                glowIntensity = 1.4f,
                zenithTint = new Color(0.3f, 0.15f, 0.45f)
            }
        };

        private static readonly SunsetVariation[] SunsetVariations = new[]
        {
            new SunsetVariation // Classic amber sunset
            {
                glowColor = new Color(1.0f, 0.4f, 0.1f),
                horizonTint = new Color(0.9f, 0.5f, 0.2f),
                sunColor = new Color(1.0f, 0.6f, 0.2f),
                glowIntensity = 1.2f,
                zenithTint = new Color(0.15f, 0.2f, 0.45f)
            },
            new SunsetVariation // Purple and magenta
            {
                glowColor = new Color(0.85f, 0.3f, 0.5f),
                horizonTint = new Color(0.75f, 0.35f, 0.55f),
                sunColor = new Color(1.0f, 0.55f, 0.4f),
                glowIntensity = 1.0f,
                zenithTint = new Color(0.2f, 0.12f, 0.4f)
            },
            new SunsetVariation // Golden hour
            {
                glowColor = new Color(1.0f, 0.65f, 0.2f),
                horizonTint = new Color(0.95f, 0.7f, 0.35f),
                sunColor = new Color(1.0f, 0.8f, 0.4f),
                glowIntensity = 0.9f,
                zenithTint = new Color(0.25f, 0.35f, 0.6f)
            },
            new SunsetVariation // Blood red
            {
                glowColor = new Color(0.85f, 0.15f, 0.05f),
                horizonTint = new Color(0.8f, 0.25f, 0.1f),
                sunColor = new Color(1.0f, 0.4f, 0.1f),
                glowIntensity = 1.5f,
                zenithTint = new Color(0.15f, 0.08f, 0.35f)
            },
            new SunsetVariation // Soft rose
            {
                glowColor = new Color(0.95f, 0.5f, 0.45f),
                horizonTint = new Color(0.9f, 0.6f, 0.55f),
                sunColor = new Color(1.0f, 0.7f, 0.5f),
                glowIntensity = 0.8f,
                zenithTint = new Color(0.3f, 0.25f, 0.55f)
            }
        };

        private static readonly NightVariation[] NightVariations = new[]
        {
            new NightVariation // Clear starry night
            {
                zenithColor = new Color(0.005f, 0.008f, 0.03f),
                horizonColor = new Color(0.015f, 0.018f, 0.04f),
                starBrightnessMultiplier = 1.2f,
                moonPhase = 0.35f,
                moonBrightnessMultiplier = 1.0f
            },
            new NightVariation // Deep blue night
            {
                zenithColor = new Color(0.008f, 0.012f, 0.04f),
                horizonColor = new Color(0.02f, 0.025f, 0.05f),
                starBrightnessMultiplier = 0.9f,
                moonPhase = -0.2f,
                moonBrightnessMultiplier = 1.1f
            },
            new NightVariation // Near-black, vivid stars
            {
                zenithColor = new Color(0.002f, 0.003f, 0.012f),
                horizonColor = new Color(0.008f, 0.008f, 0.02f),
                starBrightnessMultiplier = 1.5f,
                moonPhase = 0.7f,
                moonBrightnessMultiplier = 0.4f
            },
            new NightVariation // Faint indigo wash
            {
                zenithColor = new Color(0.012f, 0.008f, 0.035f),
                horizonColor = new Color(0.025f, 0.018f, 0.045f),
                starBrightnessMultiplier = 1.0f,
                moonPhase = -0.5f,
                moonBrightnessMultiplier = 0.9f
            },
            new NightVariation // Full moon bright
            {
                zenithColor = new Color(0.01f, 0.015f, 0.04f),
                horizonColor = new Color(0.025f, 0.03f, 0.055f),
                starBrightnessMultiplier = 0.6f,
                moonPhase = 0.05f,
                moonBrightnessMultiplier = 1.4f
            }
        };

        // ── Runtime state ────────────────────────────────────────────────────

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("SkyVariationMgr.Late");

        private DayNightCycle _cycle;
        private int _currentDay = -1;
        private bool _wasNight;

        private SunriseVariation _todaySunrise;
        private SunsetVariation _todaySunset;
        private NightVariation _tonightNight;

        // Shader property IDs
        private static readonly int ZenithColorID = Shader.PropertyToID("_ZenithColor");
        private static readonly int HorizonColorID = Shader.PropertyToID("_HorizonColor");
        private static readonly int GroundColorID = Shader.PropertyToID("_GroundColor");
        private static readonly int SunDirID = Shader.PropertyToID("_SunDir");
        private static readonly int SunColorID = Shader.PropertyToID("_SunColor");
        private static readonly int SunSizeID = Shader.PropertyToID("_SunSize");
        private static readonly int SunGlowSizeID = Shader.PropertyToID("_SunGlowSize");
        private static readonly int SunGlowIntensityID = Shader.PropertyToID("_SunGlowIntensity");
        private static readonly int MoonDirID = Shader.PropertyToID("_MoonDir");
        private static readonly int MoonColorID = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonSizeID = Shader.PropertyToID("_MoonSize");
        private static readonly int MoonBrightnessID = Shader.PropertyToID("_MoonBrightness");
        private static readonly int MoonPhaseID = Shader.PropertyToID("_MoonPhase");
        private static readonly int StarBrightnessID = Shader.PropertyToID("_StarBrightness");
        private static readonly int StarDensityID = Shader.PropertyToID("_StarDensity");
        private static readonly int StarTwinkleSpeedID = Shader.PropertyToID("_StarTwinkleSpeed");
        private static readonly int NightFactorID = Shader.PropertyToID("_NightFactor");
        private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowWidthID = Shader.PropertyToID("_GlowWidth");
        private static readonly int UndergroundFactorID = Shader.PropertyToID("_UndergroundFactor");

        private void Awake()
        {
            _cycle = GetComponent<DayNightCycle>();

            if (skyboxMaterial == null)
            {
                var shader = Shader.Find("Voidborne/ProceduralSkybox");
                if (shader != null)
                {
                    skyboxMaterial = new Material(shader);
                    skyboxMaterial.name = "ProceduralSkybox (Runtime)";
                }
                else
                {
                    Debug.LogError("SkyVariationManager: Voidborne/ProceduralSkybox shader not found!");
                    enabled = false;
                    return;
                }
            }

            RenderSettings.skybox = skyboxMaterial;

            // Pick initial variations
            PickDayVariations();
            PickNightVariation();
        }

        private void LateUpdate()
        {
            RuntimeProfiler.Begin(s_prof);
            if (skyboxMaterial == null || _cycle == null) { RuntimeProfiler.End(s_prof); return; }

            float time = _cycle.TimeOfDay;

            // Sun actually crosses the horizon at 0.25 (rise) and 0.75 (set)
            // based on the rotation formula: sunAngle = (time - 0.25) * 360
            const float sunHorizonRise = 0.25f;
            const float sunHorizonSet = 0.75f;

            // Track day changes - pick new variations when sun crosses horizon rising
            int gameDay = Mathf.FloorToInt(Time.time / _cycle.dayLengthSeconds);
            if (gameDay != _currentDay && time >= sunHorizonRise && time < sunHorizonRise + 0.02f)
            {
                _currentDay = gameDay;
                PickDayVariations();
            }

            // Pick new night variation when sun drops below horizon
            bool isSunDown = time < sunHorizonRise || time >= sunHorizonSet;
            if (isSunDown && !_wasNight)
            {
                PickNightVariation();
            }
            _wasNight = isSunDown;

            UpdateSkyboxMaterial(time);
            RuntimeProfiler.End(s_prof);
        }

        private void PickDayVariations()
        {
            _todaySunrise = SunriseVariations[Random.Range(0, SunriseVariations.Length)];
            _todaySunset = SunsetVariations[Random.Range(0, SunsetVariations.Length)];
        }

        private void PickNightVariation()
        {
            _tonightNight = NightVariations[Random.Range(0, NightVariations.Length)];
        }

        private void UpdateSkyboxMaterial(float time)
        {
            var mat = skyboxMaterial;

            // ── Derive sky brightness from the sun's actual elevation ────────
            // Sun direction Y > 0 means above horizon; use it directly for a
            // tight, physically-grounded day/night transition.
            Vector3 sunDir = Vector3.up;
            if (_cycle.SunLight != null)
                sunDir = -_cycle.SunLight.transform.forward;

            // sunElevation: 1 at zenith, 0 at horizon, negative below
            float sunElev = sunDir.y;

            // Sky brightness ramps quickly around the horizon.
            // Fully bright once sun is ~15 degrees up, fully dark once ~5 degrees below.
            float skyBrightness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.25f, sunElev));
            float nightFactor = 1f - skyBrightness;

            // ── Horizon glow peaks when sun is near the horizon ──────────────
            // Strongest when sunElev is in [-0.05, 0.15] range
            float horizonEventStrength = 1f - Mathf.Clamp01(Mathf.Abs(sunElev - 0.05f) / 0.15f);
            horizonEventStrength *= horizonEventStrength; // sharpen

            // Determine if this is a sunrise or sunset based on which half of the day
            bool isMorning = time < 0.5f;

            Color glowColor, horizonTint, sunEventColor, zenithTint;
            float glowIntensity;
            if (isMorning)
            {
                glowColor = _todaySunrise.glowColor;
                horizonTint = _todaySunrise.horizonTint;
                sunEventColor = _todaySunrise.sunColor;
                glowIntensity = _todaySunrise.glowIntensity;
                zenithTint = _todaySunrise.zenithTint;
            }
            else
            {
                glowColor = _todaySunset.glowColor;
                horizonTint = _todaySunset.horizonTint;
                sunEventColor = _todaySunset.sunColor;
                glowIntensity = _todaySunset.glowIntensity;
                zenithTint = _todaySunset.zenithTint;
            }

            // Sky colours - blend between day/night with horizon event tinting
            Color zenith = Color.Lerp(_tonightNight.zenithColor, dayZenith, skyBrightness);
            Color horizon = Color.Lerp(_tonightNight.horizonColor, dayHorizon, skyBrightness);

            // During sunrise/sunset, tint the sky
            zenith = Color.Lerp(zenith, zenithTint, horizonEventStrength * 0.5f);
            horizon = Color.Lerp(horizon, horizonTint, horizonEventStrength * 0.7f);

            float ugFactor = _cycle.UndergroundFactor;

            mat.SetColor(ZenithColorID, zenith);
            mat.SetColor(HorizonColorID, horizon);
            mat.SetColor(GroundColorID, horizon * 0.5f);

            // Underground biomes: darken skybox to hide terrain holes
            mat.SetFloat(UndergroundFactorID, ugFactor);

            // Sync fog colour with the skybox horizon so fog blends seamlessly
            // into the sky at distance. DayNightCycle still controls fog distances.
            Color fogColor = horizon;
            fogColor = Color.Lerp(fogColor, glowColor * 0.7f, horizonEventStrength * 0.4f);
            if (ugFactor > 0.01f)
                fogColor = Color.Lerp(fogColor, new Color(0.003f, 0.003f, 0.005f), ugFactor);
            RenderSettings.fogColor = fogColor;

            // Sun direction and appearance
            mat.SetVector(SunDirID, sunDir);
            Color sunCol = Color.Lerp(_cycle.SunColorNoon, sunEventColor, horizonEventStrength);
            mat.SetColor(SunColorID, sunCol);
            mat.SetFloat(SunSizeID, 0.004f);
            mat.SetFloat(SunGlowSizeID, Mathf.Lerp(0.1f, 0.2f, horizonEventStrength));
            // Sun glow visible whenever sun is above horizon
            float sunGlowVis = Mathf.Clamp01(sunElev * 10f); // fades out right at horizon
            mat.SetFloat(SunGlowIntensityID, Mathf.Lerp(0.4f, 0.8f, horizonEventStrength) * sunGlowVis);

            // Moon direction and appearance
            if (_cycle.MoonLight != null)
            {
                Vector3 moonFwd = -_cycle.MoonLight.transform.forward;
                mat.SetVector(MoonDirID, moonFwd);
                mat.SetColor(MoonColorID, _cycle.MoonColor);
                mat.SetFloat(MoonSizeID, moonDiscSize);
                mat.SetFloat(MoonBrightnessID, moonBrightness * _tonightNight.moonBrightnessMultiplier);
                mat.SetFloat(MoonPhaseID, _tonightNight.moonPhase);
            }

            // Stars
            mat.SetFloat(StarBrightnessID, starBrightness * _tonightNight.starBrightnessMultiplier);
            mat.SetFloat(StarDensityID, starDensity);
            mat.SetFloat(StarTwinkleSpeedID, starTwinkleSpeed);
            mat.SetFloat(NightFactorID, nightFactor);

            // Horizon glow band
            mat.SetColor(GlowColorID, glowColor);
            mat.SetFloat(GlowIntensityID, glowIntensity * horizonEventStrength);
            mat.SetFloat(GlowWidthID, 0.15f);
        }

        // ── Variation structs ────────────────────────────────────────────────

        private struct SunriseVariation
        {
            public Color glowColor;
            public Color horizonTint;
            public Color sunColor;
            public float glowIntensity;
            public Color zenithTint;
        }

        private struct SunsetVariation
        {
            public Color glowColor;
            public Color horizonTint;
            public Color sunColor;
            public float glowIntensity;
            public Color zenithTint;
        }

        private struct NightVariation
        {
            public Color zenithColor;
            public Color horizonColor;
            public float starBrightnessMultiplier;
            public float moonPhase;
            public float moonBrightnessMultiplier;
        }
    }
}
