using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Automation
{
    /// <summary>
    /// Wire-style placement for network cables.
    /// Click an INetworkNode device, then another to connect them with a NetworkCable.
    /// Similar to WireController but for data network connections.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkCableController : MonoBehaviour
    {
        [Header("Cable Settings")]
        public float maxCableLength = 30f;
        public LayerMask networkNodeLayer = ~0;
        public LayerMask cableLayer;

        [Header("Cable Prefab")]
        [Tooltip("Prefab with NetworkCable + LineRenderer components.")]
        public GameObject cablePrefab;

        private LineRenderer _previewLine;
        private MonoBehaviour _firstNode;
        private Camera _cam;

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

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                TryCutCable();
                return;
            }

            if (_firstNode != null)
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

        private void TrySelectFirst()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 30f, networkNodeLayer))
            {
                var node = hit.collider.GetComponentInParent<INetworkNode>();
                if (node != null && node is MonoBehaviour mb)
                {
                    _firstNode = mb;
                    _previewLine.positionCount = 2;
                    _previewLine.SetPosition(0, mb.transform.position);
                }
            }
        }

        private void TryConnectSecond()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 30f, networkNodeLayer)) return;

            var secondNode = hit.collider.GetComponentInParent<INetworkNode>();
            if (secondNode == null || secondNode is not MonoBehaviour secondMb) return;
            if (secondMb == _firstNode) return;

            float dist = Vector3.Distance(_firstNode.transform.position, secondMb.transform.position);
            if (dist > maxCableLength)
            {
                Debug.Log($"[NetworkCableController] Too far ({dist:F1}m, max {maxCableLength}m).");
                return;
            }

            // Spawn cable
            Vector3 midpoint = (_firstNode.transform.position + secondMb.transform.position) * 0.5f;
            GameObject cableGO;

            if (cablePrefab != null)
            {
                cableGO = Instantiate(cablePrefab, midpoint, Quaternion.identity);
            }
            else
            {
                cableGO = new GameObject("NetworkCable");
                cableGO.transform.position = midpoint;
                var lr = cableGO.AddComponent<LineRenderer>();
                lr.material = new Material(
                    Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                cableGO.AddComponent<NetworkCable>();
            }

            var cable = cableGO.GetComponent<NetworkCable>();
            if (cable != null)
            {
                cable.nodeA = _firstNode;
                cable.nodeB = secondMb;
            }

            // Add collider for cable cutting
            var col = cableGO.AddComponent<CapsuleCollider>();
            col.radius = 0.08f;
            col.height = dist;
            col.direction = 0;
            cableGO.transform.position = midpoint;
            cableGO.transform.LookAt(secondMb.transform.position);
            if (cableLayer.value != 0)
                cableGO.layer = (int)Mathf.Log(cableLayer.value, 2);

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
            Color c = dist <= maxCableLength ? new Color(0.2f, 0.8f, 1f) : Color.red;
            _previewLine.startColor = c;
            _previewLine.endColor = c;
        }

        private void TryCutCable()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 20f, cableLayer))
            {
                var cable = hit.collider.GetComponent<NetworkCable>();
                if (cable != null)
                    Destroy(hit.collider.gameObject);
            }
        }

        private void CancelPlacement()
        {
            _firstNode = null;
            if (_previewLine != null)
                _previewLine.positionCount = 0;
        }

        public void SetMaxLength(float length) => maxCableLength = length;
        public bool IsPlacing => _firstNode != null;
    }
}
