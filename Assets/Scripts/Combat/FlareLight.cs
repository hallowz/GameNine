using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// A self-contained flare light that burns brightly then fades out and destroys itself.
    /// Spawned at the point where a flare projectile impacts a surface.
    /// </summary>
    public class FlareLight : MonoBehaviour
    {
        [Header("Light")]
        [SerializeField] private float lightIntensity = 250f;
        [SerializeField] private float lightRange     = 35f;
        [SerializeField] private Color lightColor     = new Color(1.0f, 0.35f, 0.1f);

        [Header("Lifetime")]
        [SerializeField] private float burnDuration  = 25f;
        [SerializeField] private float fadeStartRatio = 0.7f;

        [Header("Flicker")]
        [SerializeField] private float flickerSpeed    = 10f;
        [SerializeField] private float flickerStrength = 0.15f;

        // ── Runtime ────────────────────────────────────────────────────
        private Light  _light;
        private float  _age;
        private float  _flickerTimer;

        private GameObject _outerGlow;
        private GameObject _midGlow;
        private GameObject _innerGlow;

        // ── Public factory ─────────────────────────────────────────────

        /// <summary>
        /// Creates a new FlareLight at the given world position and returns it.
        /// </summary>
        public static FlareLight Spawn(Vector3 position, Vector3 surfaceNormal)
        {
            var go = new GameObject("FlareLight");
            // Offset slightly off the surface so light doesn't clip into geometry
            go.transform.position = position + surfaceNormal * 0.05f;

            var flare = go.AddComponent<FlareLight>();
            flare.Build();
            return flare;
        }

        // ── Lifecycle ──────────────────────────────────────────────────

        private void Update()
        {
            _age += Time.deltaTime;

            if (_age >= burnDuration)
            {
                Destroy(gameObject);
                return;
            }

            // Flicker
            _flickerTimer += Time.deltaTime * flickerSpeed;
            float noise = Mathf.PerlinNoise(_flickerTimer, _flickerTimer * 0.6f);
            float flicker = 1f - flickerStrength + noise * flickerStrength * 2f;

            // Fade out during the final portion of the burn
            float fade = 1f;
            float fadeStart = burnDuration * fadeStartRatio;
            if (_age > fadeStart)
                fade = 1f - Mathf.Clamp01((_age - fadeStart) / (burnDuration - fadeStart));

            float intensity = lightIntensity * Mathf.Clamp(flicker, 0.6f, 1.4f) * fade;
            if (_light != null)
            {
                _light.intensity = intensity;
                _light.range = lightRange * Mathf.Lerp(0.5f, 1f, fade);
            }

            // Fade glow emission
            float emissionFade = fade * Mathf.Clamp(flicker, 0.7f, 1.3f);
            UpdateGlowEmission(_outerGlow, new Color(1.0f, 0.35f, 0.08f) * 4f * emissionFade);
            UpdateGlowEmission(_midGlow,   new Color(1.0f, 0.55f, 0.15f) * 5f * emissionFade);
            UpdateGlowEmission(_innerGlow, new Color(1.0f, 0.85f, 0.4f)  * 6f * emissionFade);
        }

        // ── Build ──────────────────────────────────────────────────────

        private void Build()
        {
            // Point light
            var lightGO = new GameObject("FlarePointLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = Vector3.zero;

            _light = lightGO.AddComponent<Light>();
            _light.type            = LightType.Point;
            _light.color           = lightColor;
            _light.intensity       = lightIntensity;
            _light.range           = lightRange;
            _light.shadows         = LightShadows.Soft;
            _light.shadowStrength  = 0.5f;

            // Emissive glow spheres (like the torch but larger)
            _outerGlow = MakeGlow(new Vector3(0f, 0.02f, 0f),
                new Vector3(0.12f, 0.08f, 0.12f),
                new Color(1.0f, 0.35f, 0.08f),
                new Color(1.0f, 0.35f, 0.08f) * 4f);

            _midGlow = MakeGlow(new Vector3(0f, 0.04f, 0f),
                new Vector3(0.07f, 0.06f, 0.07f),
                new Color(1.0f, 0.55f, 0.15f),
                new Color(1.0f, 0.55f, 0.15f) * 5f);

            _innerGlow = MakeGlow(new Vector3(0f, 0.05f, 0f),
                new Vector3(0.035f, 0.04f, 0.035f),
                new Color(1.0f, 0.85f, 0.4f),
                new Color(1.0f, 0.85f, 0.4f) * 6f);
        }

        private GameObject MakeGlow(Vector3 localPos, Vector3 localScale, Color baseColor, Color emissionColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Glow";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(rend.sharedMaterial) { color = baseColor };
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", emissionColor);
            }

            return go;
        }

        private void UpdateGlowEmission(GameObject glow, Color emissionColor)
        {
            if (glow == null) return;
            var rend = glow.GetComponent<Renderer>();
            if (rend != null)
                rend.material.SetColor("_EmissionColor", emissionColor);
        }
    }
}
