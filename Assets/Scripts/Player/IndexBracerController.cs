using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Voidborne.Player
{
    /// <summary>
    /// Builds and controls the physical Index bracer model on the player's left forearm.
    /// Manages fold-out panel animation for inventory/crafting UIs and hosts
    /// world-space canvases for diegetic HUD elements.
    ///
    /// Hierarchy created at runtime:
    ///   PlayerCamera
    ///     └─ BracerRoot (this transform)
    ///         ├─ BracerBody (procedural mesh — the forearm bracer)
    ///         ├─ BracerScreen (world-space canvas — hotbar, status, messages)
    ///         └─ FoldoutPivotA (hinge at bracer screen far edge)
    ///             ├─ FoldoutPanelA (world-space canvas — main inventory)
    ///             ├─ PanelA_Backing (thin mesh)
    ///             └─ FoldoutPivotB (hinge at Panel A far edge — chained)
    ///                 ├─ FoldoutPanelB (world-space canvas — crafting)
    ///                 └─ PanelB_Backing (thin mesh)
    /// </summary>
    public class IndexBracerController : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Constants — bracer positioning relative to camera
        // ---------------------------------------------------------------

        // Resting position (bracer visible at bottom-left of view)
        private static readonly Vector3 RestPosition  = new(-0.22f, -0.18f, 0.38f);
        private static readonly Vector3 RestRotation   = new(15f, 10f, -25f);

        // Raised position (when inventory/foldout is open — bracer lifts into lower-center of view)
        private static readonly Vector3 RaisedPosition = new(-0.10f, -0.18f, 0.42f);
        private static readonly Vector3 RaisedRotation  = new(35f, 10f, -8f);

        // Bracer body dimensions (metres)
        private const float BracerLength = 0.18f;   // along forearm (Z)
        private const float BracerWidth  = 0.06f;    // across wrist (X)
        private const float BracerHeight = 0.025f;   // thickness (Y)

        // Screen canvas (on bracer face — always visible)
        private const float ScreenWidth       = 0.14f;
        private const float ScreenHeight      = 0.045f;
        private const float ScreenCanvasWidth  = 400f;
        private const float ScreenCanvasHeight = 130f;

        // Panel A (inventory) dimensions
        private const float PanelAWidth       = 0.14f;
        private const float PanelAHeight      = 0.11f;
        private const float PanelACanvasW     = 502f;
        private const float PanelACanvasH     = 378f;

        // Panel B (crafting) — chained child of Panel A
        private const float PanelBWidth       = 0.08f;
        private const float PanelBHeight      = 0.08f;
        private const float PanelBCanvasW     = 280f;
        private const float PanelBCanvasH     = 300f;

        // Animation
        private const float LerpSpeed         = 8f;
        private const float FoldAngleOpen     = 120f;  // upward from bracer face toward player
        private const float FoldAngleClosed   = 0f;    // flat on bracer

        // ---------------------------------------------------------------
        //  Colors
        // ---------------------------------------------------------------

        private static readonly Color BracerColor       = new(0.12f, 0.12f, 0.14f, 1f);
        private static readonly Color ScreenColor       = new(0.04f, 0.08f, 0.10f, 1f);
        private static readonly Color ScreenBorderColor = new(0.20f, 0.35f, 0.40f, 1f);
        private static readonly Color PanelColor        = new(0.06f, 0.08f, 0.10f, 0.96f);

        // ---------------------------------------------------------------
        //  Public references (set after build, used by UI systems)
        // ---------------------------------------------------------------

        public Canvas BracerCanvas { get; private set; }
        public RectTransform BracerScreenRect { get; private set; }
        public Canvas FoldoutCanvasA { get; private set; }
        public RectTransform FoldoutRectA { get; private set; }
        public Canvas FoldoutCanvasB { get; private set; }
        public RectTransform FoldoutRectB { get; private set; }
        public bool IsOpen { get; private set; }

        public static IndexBracerController Instance { get; private set; }

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        public static event Action OnBracerOpened;
        public static event Action OnBracerClosed;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private Transform _bracerRoot;
        private Transform _foldoutPivotA;
        private Transform _foldoutPivotB;

        // Start folded (angle 0 = flat on bracer)
        private float _foldoutAngleA = FoldAngleClosed;
        private float _foldoutAngleB = FoldAngleClosed;

        private Camera _playerCamera;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
            while (PlayerManager.Instance == null)
                yield return null;

            _playerCamera = PlayerManager.Instance.CameraController.GetComponent<Camera>();
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (_bracerRoot == null) return;

            // Lerp bracer position/rotation
            Vector3 targetPos   = IsOpen ? RaisedPosition : RestPosition;
            Vector3 targetEuler = IsOpen ? RaisedRotation : RestRotation;

            _bracerRoot.localPosition = Vector3.Lerp(
                _bracerRoot.localPosition, targetPos, Time.unscaledDeltaTime * LerpSpeed);
            _bracerRoot.localRotation = Quaternion.Slerp(
                _bracerRoot.localRotation, Quaternion.Euler(targetEuler), Time.unscaledDeltaTime * LerpSpeed);

            // Fold angles: 0 = closed (flat on bracer), FoldAngleOpen = open (flipped forward)
            float targetA = IsOpen ? FoldAngleOpen : FoldAngleClosed;
            float targetB = IsOpen ? FoldAngleOpen : FoldAngleClosed;

            // Panel A unfolds at full speed
            _foldoutAngleA = Mathf.Lerp(_foldoutAngleA, targetA, Time.unscaledDeltaTime * LerpSpeed);

            // Panel B cascades — waits until Panel A passes 90° before accelerating
            float bSpeed = _foldoutAngleA > 90f ? LerpSpeed : LerpSpeed * 0.2f;
            _foldoutAngleB = Mathf.Lerp(_foldoutAngleB, targetB, Time.unscaledDeltaTime * bSpeed);

            // Positive X rotation folds panels UPWARD/OUTWARD from the bracer face
            if (_foldoutPivotA != null)
                _foldoutPivotA.localRotation = Quaternion.Euler(_foldoutAngleA, 0f, 0f);
            if (_foldoutPivotB != null)
                _foldoutPivotB.localRotation = Quaternion.Euler(_foldoutAngleB, 0f, 0f);
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        public void OpenFoldout()
        {
            if (IsOpen) return;
            IsOpen = true;
            SetFoldoutCanvasesActive(true);
            OnBracerOpened?.Invoke();
        }

        public void CloseFoldout()
        {
            if (!IsOpen) return;
            IsOpen = false;
            StartCoroutine(DelayedFoldoutHide());
            OnBracerClosed?.Invoke();
        }

        public void ToggleFoldout()
        {
            if (IsOpen) CloseFoldout(); else OpenFoldout();
        }

        // ---------------------------------------------------------------
        //  Build hierarchy
        // ---------------------------------------------------------------

        private void Build()
        {
            Transform cameraTransform = _playerCamera.transform;

            // Root object
            var rootGO = new GameObject("IndexBracer");
            rootGO.transform.SetParent(cameraTransform, false);
            rootGO.transform.localPosition = RestPosition;
            rootGO.transform.localRotation = Quaternion.Euler(RestRotation);
            _bracerRoot = rootGO.transform;

            BuildBracerBody(_bracerRoot);
            BuildBracerScreen(_bracerRoot);

            // --- Chained pivot hierarchy ---
            // PivotA: hinge at the far edge of the bracer screen (negative Z edge)
            Vector3 pivotAPos = new(0f, BracerHeight * 0.5f + 0.001f, -BracerLength * 0.5f);
            BuildFoldoutPanel(_bracerRoot, "FoldoutA", pivotAPos,
                PanelAWidth, PanelAHeight, PanelACanvasW, PanelACanvasH, 210, 0.001f,
                out _foldoutPivotA, out var canvasA, out var rectA);
            FoldoutCanvasA = canvasA;
            FoldoutRectA   = rectA;

            // PivotB: child of PivotA, hinge at the far edge of Panel A
            Vector3 pivotBPos = new(0f, 0f, -PanelAHeight);
            BuildFoldoutPanel(_foldoutPivotA, "FoldoutB", pivotBPos,
                PanelBWidth, PanelBHeight, PanelBCanvasW, PanelBCanvasH, 220, 0.002f,
                out _foldoutPivotB, out var canvasB, out var rectB);
            FoldoutCanvasB = canvasB;
            FoldoutRectB   = rectB;

            // Start folded flat
            SetFoldoutCanvasesActive(false);
            if (_foldoutPivotA != null)
                _foldoutPivotA.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (_foldoutPivotB != null)
                _foldoutPivotB.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // ---------------------------------------------------------------
        //  Bracer body — procedural box mesh
        // ---------------------------------------------------------------

        private void BuildBracerBody(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BracerBody";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(BracerWidth, BracerHeight, BracerLength);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = CreateURPMat(BracerColor, 0.6f, 0.4f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            BuildAccentStrip(go.transform, new Vector3(0f, 0.51f, 0f), new Vector3(1.02f, 0.02f, 1.02f));
            BuildAccentStrip(go.transform, new Vector3(0.51f, 0f, 0f), new Vector3(0.02f, 1.02f, 1.02f));
            BuildAccentStrip(go.transform, new Vector3(-0.51f, 0f, 0f), new Vector3(0.02f, 1.02f, 1.02f));
        }

        private void BuildAccentStrip(Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Accent";
            strip.transform.SetParent(parent, false);
            strip.transform.localPosition = localPos;
            strip.transform.localScale = localScale;

            var col = strip.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = strip.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = CreateURPMat(ScreenBorderColor, 0.7f, 0.5f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------
        //  Bracer screen — world-space canvas on the bracer face
        // ---------------------------------------------------------------

        private void BuildBracerScreen(Transform parent)
        {
            var canvasGO = new GameObject("BracerScreen");
            canvasGO.transform.SetParent(parent, false);
            canvasGO.transform.localPosition = new Vector3(0f, BracerHeight * 0.5f + 0.001f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            BracerCanvas = canvasGO.AddComponent<Canvas>();
            BracerCanvas.renderMode = RenderMode.WorldSpace;
            BracerCanvas.sortingOrder = 200;
            BracerCanvas.worldCamera = _playerCamera;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 200f;

            canvasGO.AddComponent<GraphicRaycaster>();

            BracerScreenRect = canvasGO.GetComponent<RectTransform>();
            BracerScreenRect.sizeDelta = new Vector2(ScreenCanvasWidth, ScreenCanvasHeight);
            BracerScreenRect.localScale = Vector3.one * (ScreenWidth / ScreenCanvasWidth);

            // Background panel
            var bg = new GameObject("ScreenBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = ScreenColor;
        }

        // ---------------------------------------------------------------
        //  Fold-out panels — chained hinged world-space canvases
        // ---------------------------------------------------------------

        private void BuildFoldoutPanel(Transform parent, string name, Vector3 pivotLocalPos,
            float physWidth, float physHeight, float canvasW, float canvasH,
            int sortOrder, float backingYOffset,
            out Transform pivotOut, out Canvas canvasOut, out RectTransform rectOut)
        {
            // Pivot (hinge)
            var pivotGO = new GameObject($"{name}_Pivot");
            pivotGO.transform.SetParent(parent, false);
            pivotGO.transform.localPosition = pivotLocalPos;
            pivotGO.transform.localRotation = Quaternion.identity;
            pivotOut = pivotGO.transform;

            // Panel canvas — offset from pivot so center is at half-height forward.
            // Rotation -90° so canvas faces downward (toward player when panel folds upward).
            var panelGO = new GameObject($"{name}_Panel");
            panelGO.transform.SetParent(pivotGO.transform, false);
            panelGO.transform.localPosition = new Vector3(0f, 0f, -physHeight * 0.5f);
            panelGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            canvasOut = panelGO.AddComponent<Canvas>();
            canvasOut.renderMode = RenderMode.WorldSpace;
            canvasOut.sortingOrder = sortOrder;
            canvasOut.worldCamera = _playerCamera;

            var scaler = panelGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 200f;

            panelGO.AddComponent<GraphicRaycaster>();

            rectOut = panelGO.GetComponent<RectTransform>();
            float scale = physWidth / canvasW;
            rectOut.sizeDelta = new Vector2(canvasW, canvasH);
            rectOut.localScale = Vector3.one * scale;

            // Background
            var bg = new GameObject("PanelBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(panelGO.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(2f, 2f);
            bgRect.offsetMax = new Vector2(-2f, -2f);
            bg.GetComponent<Image>().color = PanelColor;

            // Border frame
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(panelGO.transform, false);
            border.transform.SetAsFirstSibling();
            var borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            border.GetComponent<Image>().color = ScreenBorderColor;

            // Backing mesh
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "PanelBacking";
            backing.transform.SetParent(pivotGO.transform, false);
            backing.transform.localPosition = new Vector3(0f, -backingYOffset, -physHeight * 0.5f);
            backing.transform.localScale = new Vector3(physWidth, 0.003f, physHeight);
            var backCol = backing.GetComponent<Collider>();
            if (backCol != null) Destroy(backCol);
            var backR = backing.GetComponent<Renderer>();
            if (backR != null)
            {
                backR.material = CreateURPMat(BracerColor, 0.5f, 0.3f);
                backR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                backR.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private void SetFoldoutCanvasesActive(bool active)
        {
            if (FoldoutCanvasA != null) FoldoutCanvasA.gameObject.SetActive(active);
            if (FoldoutCanvasB != null) FoldoutCanvasB.gameObject.SetActive(active);
        }

        private IEnumerator DelayedFoldoutHide()
        {
            // Wait until panels are mostly folded back
            while (_foldoutAngleA > 5f)
                yield return null;
            if (!IsOpen) SetFoldoutCanvasesActive(false);
        }

        private static Material CreateURPMat(Color color, float metallic, float smoothness)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
