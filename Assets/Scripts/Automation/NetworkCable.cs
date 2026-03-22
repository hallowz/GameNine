using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Building piece for connecting ComputerTerminal to machines and StorageDrives (via DriveRack).
    /// Distinct from PowerCable — thinner, different color, zero data loss over any distance.
    /// Placed like power cable segments: nodeA and nodeB are any MonoBehaviour on the connected objects.
    ///
    /// The ComputerTerminal crawls connected NetworkCables to discover all INetworkNode members.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkCable : MonoBehaviour
    {
        [Header("Endpoints")]
        [Tooltip("The MonoBehaviour on the first connected object.")]
        public MonoBehaviour nodeA;

        [Tooltip("The MonoBehaviour on the second connected object.")]
        public MonoBehaviour nodeB;

        [Header("Visual")]
        [Tooltip("Color for the network cable (distinct from yellow power cable).")]
        [SerializeField] private Color cableColor = new Color(0.2f, 0.8f, 1f); // cyan

        [SerializeField] private float cableWidth = 0.02f;

        private LineRenderer _line;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLine();
        }

        private void Start() => UpdateLineRenderer();

        private void ConfigureLine()
        {
            if (_line == null) return;
            _line.startWidth  = cableWidth;
            _line.endWidth    = cableWidth;
            _line.material    = new Material(Shader.Find("Sprites/Default"));
            _line.startColor  = cableColor;
            _line.endColor    = cableColor;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows    = false;
        }

        private void UpdateLineRenderer()
        {
            if (_line == null || nodeA == null || nodeB == null) return;
            _line.positionCount = 2;
            _line.SetPosition(0, nodeA.transform.position);
            _line.SetPosition(1, nodeB.transform.position);
        }

        /// <summary>Returns the INetworkNode implemented by nodeA's GameObject (if any).</summary>
        public INetworkNode GetNodeA() =>
            nodeA != null ? nodeA.GetComponentInParent<INetworkNode>() : null;

        /// <summary>Returns the INetworkNode implemented by nodeB's GameObject (if any).</summary>
        public INetworkNode GetNodeB() =>
            nodeB != null ? nodeB.GetComponentInParent<INetworkNode>() : null;
    }
}
