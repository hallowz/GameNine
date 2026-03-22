using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Player;

namespace Voidborne.Combat
{
    /// <summary>
    /// Procedural first-person torch viewmodel with flickering fire light.
    ///
    /// Attach to the PlayerCamera GameObject (same object as FirstPersonCamera).
    /// When <see cref="Equip"/> is called the torch appears in the player's hand
    /// with idle sway, movement bob, and a warm point light that flickers.
    /// <see cref="Unequip"/> hides and destroys the model.
    /// </summary>
    public class TorchView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController fpc;

        [Header("Idle Pose")]
        [SerializeField] private Vector3 idlePosition = new Vector3(0.28f, -0.22f, 0.40f);

        [Header("Sway")]
        [SerializeField] private float swayAmount    = 0.02f;
        [SerializeField] private float swaySmoothing = 8f;

        [Header("Bob")]
        [SerializeField] private float bobAmount = 0.010f;
        [SerializeField] private float bobSpeed  = 9f;

        [Header("Fire Light")]
        [Tooltip("Base intensity of the torch point light.")]
        [SerializeField] private float lightIntensity = 150f;
        [Tooltip("Range of the torch point light (metres).")]
        [SerializeField] private float lightRange = 18f;
        [Tooltip("How strongly the light flickers (0 = steady, 1 = wild).")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerStrength = 0.25f;
        [Tooltip("Speed of the flicker animation.")]
        [SerializeField] private float flickerSpeed = 12f;
        [SerializeField] private Color lightColor = new Color(1.0f, 0.65f, 0.25f);

        // ── Runtime ──────────────────────────────────────────────────────────

        private Transform  _holder;
        private GameObject _modelRoot;
        private Light      _fireLight;
        private bool       _equipped;

        private Vector3 _currentSwayOffset;
        private float   _bobTimer;
        private float   _flickerTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (fpc == null)
                fpc = GetComponentInParent<FirstPersonController>();

            var holderGO = new GameObject("TorchHolder");
            _holder = holderGO.transform;
            _holder.SetParent(transform, false);
            _holder.localPosition = idlePosition;
            _holder.localRotation = Quaternion.identity;
            _holder.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_equipped || _holder == null) return;

            // Sway + bob
            Vector3 targetPos = idlePosition + ComputeSway();
            targetPos.y += ComputeBob();

            _holder.localPosition = Vector3.Lerp(
                _holder.localPosition, targetPos, Time.deltaTime * 8f);

            // Fire light flicker
            if (_fireLight != null)
            {
                _flickerTimer += Time.deltaTime * flickerSpeed;
                float noise = Mathf.PerlinNoise(_flickerTimer, _flickerTimer * 0.7f);
                float flicker = 1f - flickerStrength + noise * flickerStrength * 2f;
                _fireLight.intensity = lightIntensity * Mathf.Clamp(flicker, 0.5f, 1.5f);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        public bool IsEquipped => _equipped;

        public void Equip()
        {
            if (_equipped) return;
            _equipped = true;

            BuildModel();
            _holder.gameObject.SetActive(true);
        }

        public void Unequip()
        {
            if (!_equipped) return;
            _equipped = false;

            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }
            _fireLight = null;
            _holder.gameObject.SetActive(false);
        }

        // ── Procedural model ─────────────────────────────────────────────────

        private void BuildModel()
        {
            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }

            _modelRoot = new GameObject("TorchModel");
            _modelRoot.transform.SetParent(_holder, false);
            _modelRoot.transform.localPosition = Vector3.zero;
            _modelRoot.transform.localRotation = Quaternion.Euler(-10f, 0f, 15f);

            // ── Handle — wooden stick ────────────────────────────────────────
            MakePart(PrimitiveType.Cylinder, _modelRoot.transform,
                new Vector3(0f, -0.06f, 0f),
                new Vector3(0.028f, 0.14f, 0.028f),
                new Color(0.52f, 0.35f, 0.18f));

            // Wrapping / cloth band near the top
            MakePart(PrimitiveType.Cylinder, _modelRoot.transform,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(0.036f, 0.025f, 0.036f),
                new Color(0.35f, 0.25f, 0.12f));

            // ── Ember core — dark red/orange mass at the top ─────────────────
            MakePart(PrimitiveType.Sphere, _modelRoot.transform,
                new Vector3(0f, 0.12f, 0f),
                new Vector3(0.065f, 0.055f, 0.065f),
                new Color(0.6f, 0.18f, 0.02f));

            // ── Flame layers — three overlapping emissive spheres ────────────
            // Outer flame (large, orange)
            var outerFlame = MakePart(PrimitiveType.Sphere, _modelRoot.transform,
                new Vector3(0f, 0.16f, 0f),
                new Vector3(0.08f, 0.10f, 0.08f),
                new Color(1.0f, 0.45f, 0.05f));
            SetEmissive(outerFlame, new Color(1.0f, 0.45f, 0.05f) * 3f);

            // Middle flame (medium, bright orange-yellow)
            var midFlame = MakePart(PrimitiveType.Sphere, _modelRoot.transform,
                new Vector3(0f, 0.18f, 0f),
                new Vector3(0.05f, 0.08f, 0.05f),
                new Color(1.0f, 0.7f, 0.1f));
            SetEmissive(midFlame, new Color(1.0f, 0.7f, 0.1f) * 4f);

            // Inner flame (small, bright yellow-white)
            var innerFlame = MakePart(PrimitiveType.Sphere, _modelRoot.transform,
                new Vector3(0f, 0.19f, 0f),
                new Vector3(0.025f, 0.05f, 0.025f),
                new Color(1.0f, 0.92f, 0.5f));
            SetEmissive(innerFlame, new Color(1.0f, 0.92f, 0.5f) * 5f);

            // ── Point light — attached to the flame ──────────────────────────
            var lightGO = new GameObject("TorchLight");
            lightGO.transform.SetParent(_modelRoot.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.16f, 0f);

            _fireLight = lightGO.AddComponent<Light>();
            _fireLight.type = LightType.Point;
            _fireLight.color = lightColor;
            _fireLight.intensity = lightIntensity;
            _fireLight.range = lightRange;
            _fireLight.shadows = LightShadows.Soft;
            _fireLight.shadowStrength = 0.6f;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private GameObject MakePart(PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material = new Material(rend.sharedMaterial) { color = color };

            return go;
        }

        private void SetEmissive(GameObject go, Color emissionColor)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", emissionColor);
        }

        private Vector3 ComputeSway()
        {
            if (Mouse.current == null) return Vector3.zero;

            Vector2 delta = Mouse.current.delta.ReadValue();
            float targetX = -delta.x * swayAmount * 0.008f;
            float targetY = -delta.y * swayAmount * 0.008f;

            _currentSwayOffset = Vector3.Lerp(
                _currentSwayOffset,
                new Vector3(targetX, targetY, 0f),
                Time.deltaTime * swaySmoothing);

            return _currentSwayOffset;
        }

        private float ComputeBob()
        {
            if (fpc == null) return 0f;

            float speed = fpc.Velocity.magnitude;
            if (speed < 0.2f)
            {
                _bobTimer = 0f;
                return 0f;
            }

            _bobTimer += Time.deltaTime * bobSpeed * Mathf.Clamp01(speed / 5f);
            return Mathf.Sin(_bobTimer) * bobAmount;
        }
    }
}
