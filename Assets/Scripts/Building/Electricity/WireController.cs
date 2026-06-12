using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Player-held wire tool. Click a PowerNode, then click another to connect them.
    /// Wire rendered as a LineRenderer. Max length 20 units. Right-click to cancel.
    /// To cut a wire: aim at wire GO and press [X].
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class WireController : MonoBehaviour
    {
        [Header("Wire Settings")]
        public float maxWireLength = 20f;
        public LayerMask powerNodeLayer;
        public LayerMask wireLayer;

        private LineRenderer _previewLine;
        private PowerNode    _firstNode;
        private Camera       _cam;

        // Current wire tier info (set by ElectricityItemHandler from the active WireItem).
        private float _maxThroughput;
        private Color _wireColor = Color.yellow;
        private float _wireWidth = 0.05f;

        private void Awake()
        {
            _previewLine = GetComponent<LineRenderer>();
            _previewLine.positionCount = 0;
            // LineRenderer has no default material under URP — without one the
            // preview renders magenta/invisible. Sprites/Default is unlit and
            // respects the start/end vertex colors set in UpdatePreviewLine.
            if (_previewLine.sharedMaterial == null)
                _previewLine.material = new Material(
                    Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        }

        private void Start()
        {
            _cam = Camera.main;
        }

        private void OnEnable()
        {
            // Reset state when tool is selected.
            _firstNode = null;
            if (_previewLine != null) _previewLine.positionCount = 0;
        }

        private void OnDisable()
        {
            CancelPlacement();
        }

        private void Update()
        {
            if (_cam == null) return;

            var mouse    = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                TryCutWire();
                return;
            }

            if (_firstNode != null)
            {
                // Preview line from first node to crosshair/cursor.
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

        private void TrySelectFirst()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 30f, powerNodeLayer))
            {
                var node = hit.collider.GetComponentInParent<PowerNode>();
                if (node != null)
                {
                    _firstNode = node;
                    _previewLine.positionCount = 2;
                    _previewLine.SetPosition(0, node.transform.position);
                }
            }
        }

        private void TryConnectSecond()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 30f, powerNodeLayer)) return;

            var secondNode = hit.collider.GetComponentInParent<PowerNode>();
            if (secondNode == null || secondNode == _firstNode) return;

            float dist = Vector3.Distance(_firstNode.transform.position, secondNode.transform.position);
            if (dist > maxWireLength)
            {
                Debug.Log($"[WireController] Too far ({dist:F1}m, max {maxWireLength}m). Use PowerCable for long runs.");
                return;
            }

            // Create a wire GO with LineRenderer.
            var wireGO = new GameObject("Wire");
            wireGO.transform.position = _firstNode.transform.position;

            var lr = wireGO.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, _firstNode.transform.position);
            lr.SetPosition(1, secondNode.transform.position);
            lr.startWidth  = _wireWidth;
            lr.endWidth    = _wireWidth;
            // Sprites/Default first: URP/Unlit ignores vertex colors, so the
            // tier tint (startColor/endColor) only shows with a vertex-color shader.
            lr.material    = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            lr.startColor  = _wireColor;
            lr.endColor    = _wireColor;

            var wh = wireGO.AddComponent<WireHandle>();
            wh.nodeA = _firstNode;
            wh.nodeB = secondNode;
            wh.connection = PowerNetworkManager.Instance?.Connect(_firstNode, secondNode, 1, _maxThroughput);

            // Add a thin capsule collider so the wire can be targeted for cutting.
            var col = wireGO.AddComponent<CapsuleCollider>();
            col.radius    = 0.1f;
            col.height    = dist;
            col.direction = 0; // X axis; will be rotated
            Vector3 midpoint = (_firstNode.transform.position + secondNode.transform.position) * 0.5f;
            wireGO.transform.position = midpoint;
            wireGO.transform.LookAt(secondNode.transform.position);
            wireGO.layer = (int)Mathf.Log(wireLayer.value, 2);

            CancelPlacement();
        }

        private void UpdatePreviewLine()
        {
            if (_firstNode == null) return;
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3 endPos = ray.origin + ray.direction * 10f;
            if (Physics.Raycast(ray, out RaycastHit hit))
                endPos = hit.point;
            _previewLine.SetPosition(0, _firstNode.transform.position);
            _previewLine.SetPosition(1, endPos);

            float dist = Vector3.Distance(_firstNode.transform.position, endPos);
            _previewLine.startColor = dist <= maxWireLength ? Color.green : Color.red;
            _previewLine.endColor   = _previewLine.startColor;
        }

        private void TryCutWire()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 20f, wireLayer))
            {
                var wh = hit.collider.GetComponent<WireHandle>();
                if (wh != null)
                {
                    PowerNetworkManager.Instance?.Disconnect(wh.connection);
                    Destroy(hit.collider.gameObject);
                }
            }
        }

        private void CancelPlacement()
        {
            _firstNode = null;
            _previewLine.positionCount = 0;
        }

        public bool IsPlacing => _firstNode != null;

        /// <summary>
        /// Called by ElectricityItemHandler when the active WireItem changes.
        /// Sets tier-specific properties for the next wire placement.
        /// </summary>
        public void SetWireTier(WireItem wireItem)
        {
            if (wireItem == null) return;
            maxWireLength   = wireItem.maxLength;
            _maxThroughput  = wireItem.maxThroughputWatts;
            _wireColor      = wireItem.wireColor;
            _wireWidth      = wireItem.wireWidth;
        }
    }

    /// <summary>
    /// Component placed on wire GameObjects to track their connection for cutting.
    /// </summary>
    public class WireHandle : MonoBehaviour
    {
        public PowerNode     nodeA;
        public PowerNode     nodeB;
        public WireConnection connection;
    }
}
