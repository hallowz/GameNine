using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Automation
{
    /// <summary>
    /// Wire-style click-to-click placement for segment items (conveyors, tubes).
    /// Mirrors WireController's UX but targets AutomationConnectors instead of PowerNodes.
    ///
    /// Flow:
    ///   1. Left-click on an output-compatible AutomationConnector → first endpoint
    ///   2. Preview line from first port to cursor (green = valid, red = too far)
    ///   3. Left-click on an input-compatible AutomationConnector → second endpoint
    ///   4. Segment prefab spawns between the two ports, stretched to fit
    ///   5. Right-click or Escape cancels mid-placement
    ///   6. X key aims at an existing segment and removes it
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SegmentPlacementController : MonoBehaviour
    {
        [Header("Raycast")]
        public Camera    playerCamera;
        public LayerMask portLayer = ~0;
        public LayerMask segmentLayer;
        public float     maxRayDistance = 20f;

        [Header("Visuals")]
        [Tooltip("Width of the preview line.")]
        public float lineWidth = 0.06f;

        // ── Runtime ──────────────────────────────────────────────────────

        private SegmentItem         _activeItem;
        private LineRenderer        _previewLine;
        private AutomationConnector _firstPort;
        private bool                _isActive;
        private PlayerInventory     _inventory;

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            _previewLine = GetComponent<LineRenderer>();
            _previewLine.positionCount = 0;
            _previewLine.startWidth = lineWidth;
            _previewLine.endWidth   = lineWidth;

            _inventory = GetComponentInParent<PlayerInventory>()
                         ?? FindFirstObjectByType<PlayerInventory>();
        }

        private void OnDisable()
        {
            CancelPlacement();
        }

        private void Update()
        {
            if (!_isActive || _activeItem == null || playerCamera == null) return;

            var mouse    = Mouse.current;
            var keyboard = Keyboard.current;

            // Cancel
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            // Cut existing segment
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                TryCutSegment();
                return;
            }

            if (_firstPort != null)
            {
                UpdatePreviewLine();

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    TryConnectSecond();
            }
            else
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    TrySelectFirst();
            }
        }

        // ── Public API ───────────────────────────────────────────────────

        public void Activate(SegmentItem item)
        {
            _activeItem = item;
            _isActive   = true;
            CancelPlacement();
        }

        public void Deactivate()
        {
            _isActive   = false;
            _activeItem = null;
            CancelPlacement();
        }

        public bool IsPlacing => _firstPort != null;

        // ── First click: select output port ──────────────────────────────

        private void TrySelectFirst()
        {
            var port = RaycastPort();
            if (port == null) return;

            // First click should be an output or bidirectional port.
            if (port.portType == AutomationConnector.PortType.Input) return;
            if (port.IsConnected) return;

            _firstPort = port;
            _firstPort.ShowSnapHighlight();

            _previewLine.positionCount = 2;
            _previewLine.SetPosition(0, _firstPort.transform.position);
        }

        // ── Second click: select input port and spawn segment ────────────

        private void TryConnectSecond()
        {
            var port = RaycastPort();
            if (port == null) return;

            // Second click should be an input or bidirectional port.
            if (port.portType == AutomationConnector.PortType.Output) return;
            if (port.IsConnected) return;

            // Can't connect to the same device.
            if (port.GetDeviceNode() != null && port.GetDeviceNode() == _firstPort.GetDeviceNode())
                return;

            float dist = Vector3.Distance(_firstPort.transform.position, port.transform.position);
            if (dist > _activeItem.maxLength)
            {
                Debug.Log($"[SegmentPlacement] Too far ({dist:F1}m, max {_activeItem.maxLength}m).");
                return;
            }

            if (!HasItemInInventory()) return;

            SpawnSegment(_firstPort, port, dist);
            ConsumeItem();

            _firstPort.HideSnapHighlight();
            CancelPlacement();
        }

        // ── Spawn the segment between two ports ─────────────────────────

        private void SpawnSegment(AutomationConnector startPort, AutomationConnector endPort, float length)
        {
            if (_activeItem.devicePrefab == null) return;

            Vector3 startPos = startPort.transform.position;
            Vector3 endPos   = endPort.transform.position;
            Vector3 midpoint = (startPos + endPos) * 0.5f;
            Vector3 dir      = (endPos - startPos).normalized;

            Quaternion rotation = dir != Vector3.zero
                ? Quaternion.LookRotation(dir, Vector3.up)
                : Quaternion.identity;

            var segment = Object.Instantiate(_activeItem.devicePrefab, midpoint, rotation);

            // Scale the segment's local Z to match the distance.
            // The prefab is assumed to be 1 unit long along local Z.
            Vector3 scale = segment.transform.localScale;
            scale.z *= length;
            segment.transform.localScale = scale;

            // Set segment length on the transport component for proper item spacing.
            var belt = segment.GetComponent<ConveyorBelt>();
            if (belt != null)
                belt.SetSegmentLength(length);

            var tube = segment.GetComponent<PneumaticTube>();
            if (tube != null)
                tube.SetSegmentLength(length);

            // Add a SegmentHandle for cutting/removal.
            var handle = segment.AddComponent<SegmentHandle>();
            handle.startPort = startPort;
            handle.endPort   = endPort;

            // Set the segment layer for cut-targeting.
            if (segmentLayer.value != 0)
                segment.layer = (int)Mathf.Log(segmentLayer.value, 2);

            // Connect the automation ports.
            // Find matching connectors on the spawned segment.
            var segmentConnectors = segment.GetComponentsInChildren<AutomationConnector>();

            AutomationConnector segInput  = null;
            AutomationConnector segOutput = null;
            foreach (var c in segmentConnectors)
            {
                if (c.portId == "input" && segInput == null)    segInput  = c;
                if (c.portId == "output" && segOutput == null)  segOutput = c;
            }

            // Connect: world output → segment input, segment output → world input
            if (segInput != null)
                startPort.Connect(segInput);
            if (segOutput != null)
                segOutput.Connect(endPort);
        }

        // ── Preview line ─────────────────────────────────────────────────

        private void UpdatePreviewLine()
        {
            if (_firstPort == null || playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            Vector3 endPos = ray.origin + ray.direction * 10f;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
                endPos = hit.point;

            _previewLine.SetPosition(0, _firstPort.transform.position);
            _previewLine.SetPosition(1, endPos);

            float dist  = Vector3.Distance(_firstPort.transform.position, endPos);
            bool  valid = dist <= _activeItem.maxLength && HasItemInInventory();
            Color color = valid ? Color.green : Color.red;
            _previewLine.startColor = color;
            _previewLine.endColor   = color;
        }

        // ── Cut existing segment ─────────────────────────────────────────

        private void TryCutSegment()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, segmentLayer))
            {
                var handle = hit.collider.GetComponentInParent<SegmentHandle>();
                if (handle != null)
                {
                    // Disconnect ports before destroying.
                    if (handle.startPort != null) handle.startPort.Disconnect();
                    if (handle.endPort   != null) handle.endPort.Disconnect();
                    Destroy(handle.gameObject);
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private AutomationConnector RaycastPort()
        {
            if (playerCamera == null) return null;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, portLayer))
                return null;

            return hit.collider.GetComponentInParent<AutomationConnector>();
        }

        private void CancelPlacement()
        {
            if (_firstPort != null)
            {
                _firstPort.HideSnapHighlight();
                _firstPort = null;
            }
            if (_previewLine != null)
                _previewLine.positionCount = 0;
        }

        private bool HasItemInInventory()
        {
            if (_inventory == null || _activeItem == null) return true;
            return _inventory.CountAllItem(_activeItem.itemId) > 0;
        }

        private void ConsumeItem()
        {
            _inventory?.RemoveItem(_activeItem.itemId, 1);
        }
    }

    /// <summary>
    /// Attached to spawned segments so they can be targeted for cutting/removal.
    /// Tracks the two world ports this segment connects.
    /// </summary>
    public class SegmentHandle : MonoBehaviour
    {
        public AutomationConnector startPort;
        public AutomationConnector endPort;
    }
}
