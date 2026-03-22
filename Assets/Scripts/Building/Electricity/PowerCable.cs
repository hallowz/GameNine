using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Building-piece variant of a wire for long-distance power runs.
    /// Placed grid-aligned (horizontal or vertical).
    /// 2% loss per segment. Connects any two PowerNodes at either end.
    /// Throughput limited by wire tier.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PowerCable : MonoBehaviour
    {
        [Header("Cable")]
        public PowerNode nodeA;
        public PowerNode nodeB;

        [Tooltip("Each PowerCable = 1 segment for loss calculation.")]
        public int segmentCount = 1;

        [Header("Throughput")]
        [Tooltip("Maximum watts this cable can carry. 0 = unlimited (legacy).")]
        public float maxThroughputWatts = 0f;

        [Tooltip("Wire tier for visual/tooltip purposes.")]
        public WireTier wireTier = WireTier.Copper;

        private LineRenderer _line;
        private WireConnection _connection;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            if (nodeA != null && nodeB != null)
                Connect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect()
        {
            if (nodeA == null || nodeB == null) return;
            _connection = PowerNetworkManager.Instance?.Connect(nodeA, nodeB, segmentCount, maxThroughputWatts);
            UpdateLineRenderer();
        }

        public void Disconnect()
        {
            if (_connection != null)
            {
                PowerNetworkManager.Instance?.Disconnect(_connection);
                _connection = null;
            }
        }

        private void UpdateLineRenderer()
        {
            if (_line == null || nodeA == null || nodeB == null) return;
            _line.positionCount = 2;
            _line.SetPosition(0, nodeA.transform.position);
            _line.SetPosition(1, nodeB.transform.position);
        }

        public string GetLossReadout()
        {
            float loss = segmentCount * 0.02f * 100f;
            return $"Cable segment loss: {loss:F0}%";
        }
    }
}
