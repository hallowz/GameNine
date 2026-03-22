using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Automation
{
    /// <summary>
    /// Activated by AutomationItemHandler when the player holds an AutomationItem.
    ///
    /// • Free placement — no grid snap.
    /// • Connector snap — if the ghost's connector is within snapRange of a compatible
    ///   world connector, the ghost slides to align the two ports exactly.
    /// • On place, automatically calls Connect() on matching port pairs within
    ///   autoConnectRange so devices wire up without manual assignment.
    /// • Ghost appearance matches the placed device (same colors, semi-transparent)
    ///   with a green/red tint overlay to indicate valid/invalid placement.
    ///
    /// Controls:
    ///   Left-click  — place device
    ///   Right-click — rotate 90° around Y
    /// </summary>
    public class AutomationPlacementController : MonoBehaviour
    {
        [Header("Raycast")]
        public Camera    playerCamera;
        public LayerMask placementMask = ~0;
        public float     maxPlacementDistance = 10f;

        [Header("Snapping")]
        [Tooltip("Distance within which a ghost connector snaps to a world connector.")]
        public float snapRange = 0.8f;
        [Tooltip("Distance within which ports auto-connect after placement.")]
        public float autoConnectRange = 0.25f;

        // ── Runtime ────────────────────────────────────────────────────────

        private AutomationItem    _activeItem;
        private GameObject        _ghost;
        private Renderer[]        _ghostRenderers;
        private AutomationConnector[] _ghostConnectors;
        private int               _rotStep;
        private bool              _isActive;
        private PlayerInventory   _inventory;

        // Per-renderer ghost materials preserving original device colors.
        private struct GhostRendererEntry
        {
            public Renderer  renderer;
            public Material  validMat;
            public Material  invalidMat;
        }
        private List<GhostRendererEntry> _ghostEntries;

        // Currently highlighted world port (for visual feedback).
        private AutomationConnector _snapTarget;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            _inventory = GetComponentInParent<PlayerInventory>()
                         ?? FindObjectOfType<PlayerInventory>();
        }

        private void Update()
        {
            if (!_isActive || _activeItem == null) return;
            UpdateGhost();
            HandleInput();
        }

        private void OnDisable() => DestroyGhost();

        // ── Public API ─────────────────────────────────────────────────────

        public void Activate(AutomationItem item)
        {
            _activeItem = item;
            _rotStep    = 0;
            _isActive   = true;
            SpawnGhost(item);
        }

        public void Deactivate()
        {
            ClearSnapHighlight();
            _isActive   = false;
            _activeItem = null;
            DestroyGhost();
        }

        // ── Ghost ──────────────────────────────────────────────────────────

        private void SpawnGhost(AutomationItem item)
        {
            DestroyGhost();

            _ghost = item.devicePrefab != null
                ? Instantiate(item.devicePrefab)
                : CreateFallbackGhost(item.displayName);

            // Remove colliders so raycasts pass through.
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                Destroy(col);

            // Remove automation behaviour — keep AutomationConnectors for snap detection.
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb is ITickable || mb is IAutomationNode)
                    Destroy(mb);
            }

            _ghostConnectors = _ghost.GetComponentsInChildren<AutomationConnector>();
            _ghostRenderers  = _ghost.GetComponentsInChildren<Renderer>();

            // Build per-renderer transparent material pairs that preserve
            // the device's original colors with a green/red validity tint.
            _ghostEntries = new List<GhostRendererEntry>();
            foreach (var r in _ghostRenderers)
            {
                // Keep connector indicator spheres at their original material.
                if (r.GetComponentInParent<AutomationConnector>() != null) continue;

                var entry = new GhostRendererEntry
                {
                    renderer   = r,
                    validMat   = MakeTintedGhost(r.sharedMaterial, new Color(0.10f, 0.80f, 0.20f, 0.40f)),
                    invalidMat = MakeTintedGhost(r.sharedMaterial, new Color(0.80f, 0.10f, 0.10f, 0.40f)),
                };
                _ghostEntries.Add(entry);
            }

            ApplyGhostMaterial(true);
        }

        private static GameObject CreateFallbackGhost(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name + "_Ghost";
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void DestroyGhost()
        {
            ClearSnapHighlight();
            if (_ghost != null) Destroy(_ghost);

            if (_ghostEntries != null)
            {
                foreach (var e in _ghostEntries)
                {
                    if (e.validMat   != null) Destroy(e.validMat);
                    if (e.invalidMat != null) Destroy(e.invalidMat);
                }
                _ghostEntries = null;
            }

            _ghost           = null;
            _ghostRenderers  = null;
            _ghostConnectors = null;
        }

        /// <summary>
        /// Applies tinted transparent materials to every non-connector renderer.
        /// Valid = green tint, invalid = red tint, while preserving device colors.
        /// </summary>
        private void ApplyGhostMaterial(bool valid)
        {
            if (_ghostEntries == null) return;
            foreach (var entry in _ghostEntries)
            {
                if (entry.renderer == null) continue;
                entry.renderer.sharedMaterial = valid ? entry.validMat : entry.invalidMat;
            }
        }

        /// <summary>
        /// Creates a transparent copy of <paramref name="src"/> blended with
        /// <paramref name="tint"/> (RGBA, alpha = blend strength).
        /// The result preserves the source color and adds a validity tint.
        /// </summary>
        private static Material MakeTintedGhost(Material src, Color tint)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            var mat = new Material(sh ?? Shader.Find("Diffuse"));

            // Base color: blend original color with tint.
            Color baseColor = Color.gray;
            if (src != null)
            {
                if (src.HasProperty("_BaseColor")) baseColor = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) baseColor = src.GetColor("_Color");
            }
            Color blended = Color.Lerp(baseColor, new Color(tint.r, tint.g, tint.b), 0.35f);
            blended.a = tint.a;

            // Configure transparency for URP.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", blended);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     blended);

            return mat;
        }

        // ── Update ─────────────────────────────────────────────────────────

        private void UpdateGhost()
        {
            if (_ghost == null || playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask))
            {
                _ghost.SetActive(false);
                ClearSnapHighlight();
                return;
            }

            _ghost.SetActive(true);

            // Base position (free placement on hit surface).
            Vector3    basePos = hit.point;
            Quaternion rot     = Quaternion.Euler(0f, _rotStep * 90f, 0f);
            _ghost.transform.SetPositionAndRotation(basePos, rot);

            // Connector snap — find nearest compatible port pair.
            ClearSnapHighlight();
            if (_ghostConnectors != null && _ghostConnectors.Length > 0)
            {
                FindSnapTarget(out var ghostPort, out var worldPort);
                if (ghostPort != null && worldPort != null)
                {
                    // Offset ghost so ghostPort.position == worldPort.position.
                    Vector3 offset = worldPort.transform.position - ghostPort.transform.position;
                    _ghost.transform.position += offset;

                    worldPort.ShowSnapHighlight();
                    _snapTarget = worldPort;
                }
            }

            bool canPlace = HasItemInInventory() && !IsOccupied(_ghost.transform.position);
            ApplyGhostMaterial(canPlace);
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame)  TryPlace();
            if (mouse.rightButton.wasPressedThisFrame) { _rotStep = (_rotStep + 1) % 4; }
        }

        private void TryPlace()
        {
            if (_activeItem == null || playerCamera == null || !HasItemInInventory()) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask)) return;

            // Use the ghost's current position (includes snap offset).
            Vector3    pos = _ghost != null ? _ghost.transform.position : hit.point;
            Quaternion rot = _ghost != null ? _ghost.transform.rotation : Quaternion.Euler(0f, _rotStep * 90f, 0f);

            if (IsOccupied(pos)) return;
            if (_activeItem.devicePrefab == null) return;

            // Place the real device.
            var placed = Object.Instantiate(_activeItem.devicePrefab, pos, rot);

            // Auto-connect ports that are close enough.
            AutoConnectPorts(placed);

            // Consume one item.
            _inventory?.RemoveItem(_activeItem.itemId, 1);
        }

        // ── Connector snap ─────────────────────────────────────────────────

        private void FindSnapTarget(out AutomationConnector ghostPort, out AutomationConnector worldPort)
        {
            ghostPort = null;
            worldPort = null;

            if (_ghostConnectors == null || _ghostConnectors.Length == 0) return;

            float bestSqDist = snapRange * snapRange;
            var   allWorld   = FindObjectsOfType<AutomationConnector>();

            foreach (var gc in _ghostConnectors)
            {
                foreach (var wc in allWorld)
                {
                    // Skip if already connected, or incompatible type.
                    if (wc.IsConnected) continue;
                    if (!PortTypesCompatible(gc.portType, wc.portType)) continue;

                    float sqd = (gc.transform.position - wc.transform.position).sqrMagnitude;
                    if (sqd < bestSqDist)
                    {
                        bestSqDist = sqd;
                        ghostPort  = gc;
                        worldPort  = wc;
                    }
                }
            }
        }

        private static bool PortTypesCompatible(AutomationConnector.PortType a, AutomationConnector.PortType b)
        {
            if (a == AutomationConnector.PortType.Bidirectional ||
                b == AutomationConnector.PortType.Bidirectional) return true;
            return (a == AutomationConnector.PortType.Output && b == AutomationConnector.PortType.Input)
                || (a == AutomationConnector.PortType.Input  && b == AutomationConnector.PortType.Output);
        }

        private void AutoConnectPorts(GameObject placed)
        {
            var placedConnectors = placed.GetComponentsInChildren<AutomationConnector>();
            var worldConnectors  = FindObjectsOfType<AutomationConnector>();

            float rangeSq = autoConnectRange * autoConnectRange;

            foreach (var pc in placedConnectors)
            {
                foreach (var wc in worldConnectors)
                {
                    if (wc.transform.IsChildOf(placed.transform)) continue; // skip self
                    if (!pc.CanConnectTo(wc)) continue;

                    float sqd = (pc.transform.position - wc.transform.position).sqrMagnitude;
                    if (sqd <= rangeSq)
                    {
                        pc.Connect(wc);
                        break; // one connection per port
                    }
                }
            }
        }

        private void ClearSnapHighlight()
        {
            if (_snapTarget != null)
            {
                _snapTarget.HideSnapHighlight();
                _snapTarget = null;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private bool HasItemInInventory()
        {
            if (_inventory == null || _activeItem == null) return true;
            return _inventory.CountAllItem(_activeItem.itemId) > 0;
        }

        private static bool IsOccupied(Vector3 pos)
        {
            var cols = Physics.OverlapSphere(pos, 0.30f);
            foreach (var c in cols)
            {
                if (c.GetComponentInParent<IAutomationNode>() != null) return true;
                if (c.GetComponentInParent<ITickable>()       != null) return true;
            }
            return false;
        }
    }
}
