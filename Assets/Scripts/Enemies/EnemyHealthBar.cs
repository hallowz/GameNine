using UnityEngine;
using UnityEngine.Rendering;
using Voidborne.Diagnostics;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Renders a floating health bar above an enemy using two Quad meshes (no Canvas).
    /// Always faces the camera. Auto-added by EnemyEntity.Awake.
    ///
    /// Performance: skips all work when the enemy is at full health or beyond
    /// the visibility threshold (40 m). Uses MaterialPropertyBlock to avoid
    /// per-frame material instance writes.
    /// </summary>
    [AddComponentMenu("Voidborne/Enemies/Enemy Health Bar")]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private float heightOffset = 3.0f;

        private static readonly float BarW = 1.0f;
        private static readonly float BarH = 0.12f;

        private const float MaxVisibleDistSq = 40f * 40f;
        private const int   CullCheckInterval = 8; // re-evaluate visibility every N frames

        private EnemyEntity _enemy;
        private Transform  _cameraTransform;
        private Transform  _barRoot;
        private Transform  _fillTf;
        private Material   _fillMat;

        private MeshRenderer        _bgRenderer;
        private MeshRenderer        _fillRenderer;
        private MaterialPropertyBlock _propBlock;
        private static readonly int  BaseColorID = Shader.PropertyToID("_BaseColor");

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("EnemyHealthBar.Late");

        private bool  _barVisible;
        private int   _cullFrame;
        private float _lastRatio = -1f; // force first update

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _propBlock = new MaterialPropertyBlock();
            BuildBar();
        }

        private void Start()
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null) _cameraTransform = cam.transform;
        }

        private void LateUpdate()
        {
            RuntimeProfiler.Begin(s_prof);
            if (_barRoot == null || _enemy == null) { RuntimeProfiler.End(s_prof); return; }

            // ── Visibility gate ──────────────────────────────────────────
            if (++_cullFrame >= CullCheckInterval)
            {
                _cullFrame = 0;
                bool shouldShow = ShouldShowBar();
                if (shouldShow != _barVisible)
                {
                    _barVisible = shouldShow;
                    _barRoot.gameObject.SetActive(_barVisible);
                    if (!_barVisible) { RuntimeProfiler.End(s_prof); return; }
                    _lastRatio = -1f; // force colour refresh on re-show
                }
            }

            if (!_barVisible) { RuntimeProfiler.End(s_prof); return; }

            // ── Position + billboard ─────────────────────────────────────
            _barRoot.position = transform.position + Vector3.up * heightOffset;

            if (_cameraTransform != null)
            {
                Vector3 toCamera = _cameraTransform.position - _barRoot.position;
                if (toCamera.sqrMagnitude > 0.001f)
                    _barRoot.rotation = Quaternion.LookRotation(toCamera, _cameraTransform.up);
            }

            // ── Fill update (only when ratio actually changes) ───────────
            float ratio = _enemy.MaxHealth > 0f
                ? Mathf.Clamp01(_enemy.CurrentHealth / _enemy.MaxHealth)
                : 0f;

            // Quantize to ~1% steps so we don't touch the renderer every frame
            float quantized = Mathf.Round(ratio * 100f) * 0.01f;
            if (Mathf.Approximately(quantized, _lastRatio)) { RuntimeProfiler.End(s_prof); return; }
            _lastRatio = quantized;

            _fillTf.localScale    = new Vector3(BarW * ratio, BarH * 0.75f, 1f);
            _fillTf.localPosition = new Vector3((1f - ratio) * BarW * 0.5f, 0f, 0.002f);

            Color col = Color.Lerp(Color.red, Color.green, ratio);
            _propBlock.SetColor(BaseColorID, col);
            _fillRenderer.SetPropertyBlock(_propBlock);
            RuntimeProfiler.End(s_prof);
        }

        private void OnDestroy()
        {
            if (_barRoot != null) Destroy(_barRoot.gameObject);
            if (_fillMat  != null) Destroy(_fillMat);
        }

        // -----------------------------------------------------------------------
        // Visibility
        // -----------------------------------------------------------------------

        private bool ShouldShowBar()
        {
            // Hide when at full health
            if (_enemy.CurrentHealth >= _enemy.MaxHealth) return false;

            // Hide when too far from camera
            if (_cameraTransform != null)
            {
                float distSq = (_cameraTransform.position - transform.position).sqrMagnitude;
                if (distSq > MaxVisibleDistSq) return false;
            }

            return true;
        }

        // -----------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------

        private void BuildBar()
        {
            _barRoot = new GameObject($"HealthBar_{name}").transform;

            // Start hidden — bar appears only when enemy takes damage
            _barRoot.gameObject.SetActive(false);
            _barVisible = false;

            // Dark background panel
            Material bgMat = MakeUnlitMat(new Color(0.1f, 0.1f, 0.1f));
            var bgGO = MakeQuad("BG", _barRoot, Vector3.zero, new Vector3(BarW, BarH, 1f), bgMat);
            _bgRenderer = bgGO.GetComponent<MeshRenderer>();

            // Green fill (slightly closer to camera via +Z offset)
            _fillMat = MakeUnlitMat(Color.green);
            var fillGO = MakeQuad("Fill", _barRoot,
                new Vector3(0f, 0f, 0.002f), new Vector3(BarW, BarH * 0.75f, 1f), _fillMat);
            _fillTf = fillGO.transform;
            _fillRenderer = fillGO.GetComponent<MeshRenderer>();
        }

        private static GameObject MakeQuad(string objName, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = objName;
            // Remove the auto-added MeshCollider
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            var mr = go.GetComponent<MeshRenderer>();
            mr.material          = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            return go;
        }

        private static Material MakeUnlitMat(Color color)
        {
            // Prefer URP Unlit; fall back to legacy Unlit/Color
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

            var mat = new Material(sh);
            mat.color = color;
            mat.SetColor("_BaseColor", color);
            // Disable backface culling so the bar is visible from either side
            mat.SetFloat("_Cull", (float)CullMode.Off);
            return mat;
        }
    }
}
