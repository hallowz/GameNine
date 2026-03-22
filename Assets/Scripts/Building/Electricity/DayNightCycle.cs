using UnityEngine;
using UnityEngine.Rendering;
using Voidborne.Diagnostics;
using Voidborne.World.Biomes;
using Voidborne.World.Chunks;
using Voidborne.World.Generation;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Day/night cycle singleton with atmospheric lighting.
    ///
    /// Drives time-of-day as a 0-1 normalised value where:
    ///   0.0  = midnight
    ///   0.25 = sunrise
    ///   0.5  = noon
    ///   0.75 = sunset
    ///   1.0  = midnight (wraps)
    ///
    /// Features:
    ///   - Sun colour changes throughout the day (warm sunrise/sunset, white noon)
    ///   - Moon light at night (faint blue-white)
    ///   - Atmospheric distance fog at the max render boundary to hide the
    ///     terrain edge; rendered chunks remain visible at any distance
    ///
    /// Cave darkness is handled per-vertex via sky exposure in the terrain shader,
    /// not by global ambient manipulation.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Time")]
        [Tooltip("Length of one full day in real seconds.")]
        public float dayLengthSeconds = 1440f;

        [Tooltip("Starting normalised time-of-day (0 = midnight, 0.5 = noon).")]
        [Range(0f, 1f)]
        public float startTimeOfDay = 0.3f;

        [Header("Sun")]
        public Light sunLight;
        [Tooltip("Maximum sun intensity at noon.")]
        public float dayIntensity = 1.2f;

        [Header("Moon")]
        [Tooltip("Optional secondary directional light for moonlight. " +
                 "If left empty, one is created automatically.")]
        public Light moonLight;
        [Tooltip("Moon intensity at full night.")]
        public float moonIntensity = 0.08f;
        [Tooltip("Moon light colour.")]
        public Color moonColor = new Color(0.35f, 0.45f, 0.65f);

        [Header("Sun Colour Gradient")]
        [Tooltip("Sun colour at sunrise/sunset.")]
        public Color sunColorHorizon = new Color(1.0f, 0.55f, 0.2f);
        [Tooltip("Sun colour at noon.")]
        public Color sunColorNoon = new Color(1.0f, 0.97f, 0.9f);
        [Tooltip("Sun colour just after sunrise / before sunset (transition).")]
        public Color sunColorGolden = new Color(1.0f, 0.8f, 0.45f);

        [Header("Ambient")]
        public Color dayAmbient   = new Color(0.4f, 0.45f, 0.5f);
        public Color nightAmbient = new Color(0.02f, 0.02f, 0.06f);

        [Header("Thresholds")]
        [Range(0f, 0.5f)]
        public float sunriseTime = 0.22f;
        [Range(0.5f, 1f)]
        public float sunsetTime  = 0.78f;

        [Header("Fog")]
        [Tooltip("Enable distance fog at the edge of render distance.")]
        public bool enableFog = true;
        [Tooltip("Fog fade band width (in world units). Fog starts this many units before the end distance.")]
        [Range(16f, 256f)]
        public float fogFadeBand = 64f;
        [Tooltip("Fog colour during daytime.")]
        public Color dayFogColor = new Color(0.65f, 0.78f, 0.90f);
        [Tooltip("Fog colour at night.")]
        public Color nightFogColor = new Color(0.01f, 0.01f, 0.04f);

        [Header("Sky")]
        [Tooltip("Camera background colour at noon.")]
        public Color daySkyColor = new Color(0.53f, 0.80f, 0.92f);
        [Tooltip("Camera background colour at night.")]
        public Color nightSkyColor = new Color(0.01f, 0.01f, 0.03f);
        [Tooltip("Camera background colour at sunrise/sunset.")]
        public Color horizonSkyColor = new Color(0.85f, 0.55f, 0.3f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("DayNightCycle.Update");

        private float _timeOfDay;
        private float _dayFactor;
        private Camera _mainCamera;

        // Fog distances — based on max render distance
        private float _fogStart;
        private float _fogEnd;

        // Biome-based underground detection — drives skybox/fog darkening only.
        // Terrain ambient is handled per-vertex by sky exposure in the shader.
        private float _undergroundFactor; // 0 = surface biome, 1 = underground biome
        private bool[] _isUndergroundBiome; // indexed by biomeIdByte
        private Vector3Int _chunkLookupKey;

        /// <summary>Normalised time of day in [0, 1).</summary>
        public float TimeOfDay => _timeOfDay;

        /// <summary>True between sunrise and sunset thresholds.</summary>
        public bool IsDaytime => _timeOfDay >= sunriseTime && _timeOfDay < sunsetTime;

        /// <summary>0 = surface biome, 1 = underground biome. Drives skybox/fog darkening only.</summary>
        public float UndergroundFactor => _undergroundFactor;

        /// <summary>Expose sun light for SkyVariationManager.</summary>
        public Light SunLight => sunLight;

        /// <summary>Expose moon light for SkyVariationManager.</summary>
        public Light MoonLight => moonLight;

        /// <summary>Expose sun noon colour for sky blending.</summary>
        public Color SunColorNoon => sunColorNoon;

        /// <summary>Expose moon colour for sky blending.</summary>
        public Color MoonColor => moonColor;

        /// <summary>Expose sunrise threshold.</summary>
        public float SunriseTime => sunriseTime;

        /// <summary>Expose sunset threshold.</summary>
        public float SunsetTime => sunsetTime;

        /// <summary>Smoothed day factor: 0 = full night, 1 = full day (noon).</summary>
        public float DayFactor => _dayFactor;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _timeOfDay = startTimeOfDay;

            // Auto-create moon light if not assigned
            if (moonLight == null)
            {
                var moonGO = new GameObject("MoonLight");
                moonGO.transform.SetParent(transform);
                moonLight = moonGO.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.shadows = LightShadows.Soft;
                moonLight.shadowStrength = 0.3f;
                moonLight.intensity = 0f;
                moonLight.color = moonColor;
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                // Use Skybox clear so the procedural skybox shader renders.
                // SkyVariationManager drives the skybox material; camera backgroundColor
                // is still set as a fallback colour for fog matching.
                _mainCamera.clearFlags = CameraClearFlags.Skybox;
            }

            // Force ambient mode to Flat so RenderSettings.ambientLight is respected.
            // Skybox/Gradient modes ignore this property entirely.
            RenderSettings.ambientMode = AmbientMode.Flat;

            // Build underground biome lookup table — biomes with depthMin > 0 are underground.
            _isUndergroundBiome = new bool[256];
            BiomeDefinition[] biomes = BiomeMap.GetBiomes();
            if (biomes != null)
            {
                foreach (var b in biomes)
                {
                    if (b != null && b.depthMin > 0f)
                        _isUndergroundBiome[b.biomeIdByte] = true;
                }
            }

            // Compute fog distances from max render distance
            UpdateFogDistances();

            if (enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
            }
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            _timeOfDay += Time.deltaTime / dayLengthSeconds;
            if (_timeOfDay >= 1f) _timeOfDay -= 1f;

            if (_mainCamera == null) _mainCamera = Camera.main;

            _dayFactor = ComputeDayFactor();

            UpdateBiomeUndergroundDetection();

            ApplySunRotation();
            ApplySunColorAndIntensity(_dayFactor);
            ApplyMoonLight(_dayFactor);
            ApplyAmbient(_dayFactor);
            ApplyFog(_dayFactor);
            ApplySkyColor(_dayFactor);
            RuntimeProfiler.End(s_prof);
        }

        // ── Fog distance from ChunkManager ───────────────────────────────────

        private void UpdateFogDistances()
        {
            float maxDist;
            if (ChunkManager.Instance != null)
            {
                maxDist = ChunkManager.Instance.MaxHorizontalDistance * ChunkData.SIZE;
            }
            else
            {
                maxDist = 448f;
            }

            // Fog sits at the far edge of max render distance — purely atmospheric.
            // Rendered chunks at any distance remain fully visible; fog only hides
            // the boundary where terrain stops being generated.
            _fogEnd   = maxDist;
            _fogStart = maxDist - fogFadeBand;
        }

        // ── Day factor ───────────────────────────────────────────────────────

        private float ComputeDayFactor()
        {
            float t;
            if (_timeOfDay < sunriseTime)
                t = 0f;
            else if (_timeOfDay < 0.5f)
                t = Mathf.InverseLerp(sunriseTime, 0.5f, _timeOfDay);
            else if (_timeOfDay < sunsetTime)
                t = 1f - Mathf.InverseLerp(0.5f, sunsetTime, _timeOfDay);
            else
                t = 0f;

            return Mathf.SmoothStep(0f, 1f, t);
        }

        private float ComputeHorizonFactor()
        {
            float sunriseDist = Mathf.Abs(_timeOfDay - sunriseTime);
            float sunsetDist = Mathf.Abs(_timeOfDay - sunsetTime);
            float nearestHorizon = Mathf.Min(sunriseDist, sunsetDist);
            const float horizonWidth = 0.08f;
            return Mathf.SmoothStep(1f, 0f, nearestHorizon / horizonWidth);
        }

        // ── Sun ──────────────────────────────────────────────────────────────

        private void ApplySunRotation()
        {
            if (sunLight == null) return;
            float sunAngle = (_timeOfDay - 0.25f) * 360f;
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
        }

        private void ApplySunColorAndIntensity(float dayT)
        {
            if (sunLight == null) return;

            float horizonT = ComputeHorizonFactor();

            Color noonToGolden = Color.Lerp(sunColorNoon, sunColorGolden, horizonT);
            Color sunCol = Color.Lerp(noonToGolden, sunColorHorizon, horizonT * horizonT);
            sunLight.color = sunCol;

            float intensity = dayT * dayIntensity;
            sunLight.intensity = intensity;

            // Only the dominant light casts shadows to prevent two directional
            // lights fighting over the shadow map during transitions.
            // Hysteresis prevents rapid toggling near the threshold.
            bool sunUp = _timeOfDay >= sunriseTime && _timeOfDay < sunsetTime;
            bool sunShadowsOn = sunLight.shadows != LightShadows.None;
            float sunThreshold = sunShadowsOn ? 0.005f : 0.02f;
            sunLight.shadows = sunUp && intensity > sunThreshold
                ? LightShadows.Soft : LightShadows.None;
        }

        // ── Moon ─────────────────────────────────────────────────────────────

        private void ApplyMoonLight(float dayT)
        {
            if (moonLight == null) return;

            float moonAngle = ((_timeOfDay + 0.5f) - 0.25f) * 360f;
            moonLight.transform.rotation = Quaternion.Euler(moonAngle, 150f, 0f);

            float nightT = 1f - dayT;
            float moonT = Mathf.SmoothStep(0f, 1f, nightT);

            moonLight.color = moonColor;
            moonLight.intensity = moonT * moonIntensity;

            bool moonUp = _timeOfDay < sunriseTime || _timeOfDay >= sunsetTime;
            bool moonShadowsOn = moonLight.shadows != LightShadows.None;
            float moonThreshold = moonShadowsOn ? 0.005f : 0.02f;
            moonLight.shadows = moonUp && moonLight.intensity > moonThreshold
                ? LightShadows.Soft : LightShadows.None;
        }

        // ── Ambient ──────────────────────────────────────────────────────────

        private void ApplyAmbient(float dayT)
        {
            // Ensure Flat mode stays active (volumes or other scripts may reset it)
            RenderSettings.ambientMode = AmbientMode.Flat;

            // Cave darkness is handled per-vertex by sky exposure in the terrain shader.
            // The global ambient remains at full sky brightness for non-terrain objects.
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, dayT);
        }

        // ── Fog ──────────────────────────────────────────────────────────────

        private void ApplyFog(float dayT)
        {
            if (!enableFog) return;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            float startDist = _fogStart;
            float endDist   = _fogEnd;

            // Underground biomes: pull fog very close to hide lit surface terrain
            // visible through gaps and prevent sunlight leaking beyond shadow distance.
            if (_undergroundFactor > 0.01f)
            {
                startDist = Mathf.Lerp(_fogStart, 20f, _undergroundFactor);
                endDist   = Mathf.Lerp(_fogEnd,   60f, _undergroundFactor);
            }

            // Fog colour is set by SkyVariationManager (synced with skybox horizon)
            RenderSettings.fogStartDistance  = startDist;
            RenderSettings.fogEndDistance    = endDist;

            // Far clip extends slightly beyond fog so the skybox renders behind it
            if (_mainCamera != null)
                _mainCamera.farClipPlane = endDist + 64f;
        }

        // ── Sky ──────────────────────────────────────────────────────────────

        private void ApplySkyColor(float dayT)
        {
            if (_mainCamera == null) return;

            // Underground biomes: switch to solid black background so terrain
            // holes don't show sky.  Surface: render the procedural skybox.
            if (_undergroundFactor > 0.99f)
            {
                _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                _mainCamera.backgroundColor = Color.black;
            }
            else
            {
                _mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        // ── Biome-based underground detection ──────────────────────────────

        /// <summary>
        /// Lightweight underground check: samples the BiomeField in a small grid
        /// around the camera.  The ratio of underground biome samples drives
        /// skybox and fog darkening.  Terrain ambient is handled separately by
        /// per-vertex sky exposure.
        /// </summary>
        private void UpdateBiomeUndergroundDetection()
        {
            if (_mainCamera == null || _isUndergroundBiome == null) return;

            var chunkMgr = ChunkManager.Instance;
            if (chunkMgr == null) return;

            Vector3 camPos = _mainCamera.transform.position;
            const int SIZE = ChunkData.SIZE;

            // Sample a 3×3×3 grid (spacing 2m) around the camera for stable results.
            int ugCount = 0;
            int totalCount = 0;
            const float SPACING = 2f;

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ix = Mathf.FloorToInt(camPos.x + dx * SPACING);
                int iy = Mathf.FloorToInt(camPos.y + dy * SPACING);
                int iz = Mathf.FloorToInt(camPos.z + dz * SPACING);
                int chunkX = (ix >= 0) ? ix / SIZE : (ix - SIZE + 1) / SIZE;
                int chunkY = (iy >= 0) ? iy / SIZE : (iy - SIZE + 1) / SIZE;
                int chunkZ = (iz >= 0) ? iz / SIZE : (iz - SIZE + 1) / SIZE;

                _chunkLookupKey.x = chunkX;
                _chunkLookupKey.y = chunkY;
                _chunkLookupKey.z = chunkZ;

                ChunkData data = chunkMgr.GetChunk(_chunkLookupKey);
                if (data == null || data.state != ChunkState.Active || data.BiomeField == null)
                    continue;

                int localX = Mathf.Clamp(ix - chunkX * SIZE, 0, SIZE - 1);
                int localY = Mathf.Clamp(iy - chunkY * SIZE, 0, SIZE - 1);
                int localZ = Mathf.Clamp(iz - chunkZ * SIZE, 0, SIZE - 1);

                byte biomeId = data.BiomeField.Get(localX + localY * SIZE + localZ * SIZE * SIZE);
                totalCount++;
                if (_isUndergroundBiome[biomeId])
                    ugCount++;
            }

            float target = totalCount > 0 ? (float)ugCount / totalCount : 0f;
            // Smooth transition — fast into underground, moderate out
            float speed = target > _undergroundFactor ? 6f : 3f;
            _undergroundFactor = Mathf.MoveTowards(_undergroundFactor, target, speed * Time.deltaTime);
        }

        /// <summary>Override time for scripting / testing.</summary>
        public void SetTimeOfDay(float normalised) => _timeOfDay = Mathf.Repeat(normalised, 1f);
    }
}
