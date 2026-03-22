using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Blocks item flow when the downstream container is full; opens when space is available.
    ///
    /// Place an OverflowValve between a belt/tube and an input hopper (or directly between
    /// two belts) to prevent upstream machines from jamming when a downstream buffer fills.
    ///
    /// Behaviour:
    ///   • When open:   acts as a pass-through — items inserted immediately forwarded downstream.
    ///   • When closed: holds up to 1 item internally (the item that just got blocked).
    ///   • Re-checks downstream capacity every tick.
    ///   • Visual: the 'gateRenderer' changes color (green = open, red = closed).
    /// </summary>
    public class OverflowValve : MonoBehaviour, IAutomationNode, ITickable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Connection")]
        [Tooltip("Downstream node (belt, hopper, machine input).")]
        [SerializeField] private MonoBehaviour downstreamMono;

        [Header("Visuals")]
        [SerializeField] private Renderer gateRenderer;
        [SerializeField] private Color    openColor   = Color.green;
        [SerializeField] private Color    closedColor = Color.red;

        // ── Runtime ────────────────────────────────────────────────────────

        private IAutomationNode _downstream;
        private ItemStack       _held; // at most one item held when closed
        private bool            _isOpen = true;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // ── ITickable ──────────────────────────────────────────────────────

        public int TickPriority => 28;

        public void AutomationTick()
        {
            // If holding a blocked item, try to forward it.
            if (!_held.IsEmpty && _downstream != null && _downstream.CanAccept(_held.item))
            {
                _downstream.TryInsert(_held);
                _held   = new ItemStack(null, 0);
                _isOpen = true;
                UpdateGate();
            }
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            if (!_held.IsEmpty) return false; // already holding one blocked item
            // Accept if downstream has space OR we can buffer the incoming item.
            return _downstream == null || _downstream.CanAccept(item) || _held.IsEmpty;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            if (!_held.IsEmpty) return false; // valve is full

            // Try immediate pass-through.
            if (_downstream != null && _downstream.CanAccept(stack.item))
            {
                _downstream.TryInsert(stack);
                _isOpen = true;
                UpdateGate();
                return true;
            }

            // Downstream is full — hold the item internally.
            _held   = stack;
            _isOpen = false;
            UpdateGate();
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            if (_held.IsEmpty) return false;
            return filter == null || _held.item == filter;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            if (_held.IsEmpty) return new ItemStack(null, 0);
            if (filter != null && _held.item != filter) return new ItemStack(null, 0);
            var result = _held;
            _held   = new ItemStack(null, 0);
            _isOpen = true;
            UpdateGate();
            return result;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private AutomationConnector _outputConn;

        private void Awake()
        {
            _downstream = downstreamMono as IAutomationNode;
            _mpb        = new MaterialPropertyBlock();

            foreach (var c in GetComponentsInChildren<AutomationConnector>())
            {
                c.OnConnectionChanged += RefreshConnections;
                if (c.portId == "output") _outputConn = c;
            }
        }

        private void RefreshConnections()
        {
            if (_outputConn != null)
                _downstream = _outputConn.GetConnectedNode() ?? (downstreamMono as IAutomationNode);
        }

        private void OnEnable()
        {
            AutomationTickManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
        }

        // ── Visuals ────────────────────────────────────────────────────────

        private void UpdateGate()
        {
            if (gateRenderer == null) return;
            gateRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, _isOpen ? openColor : closedColor);
            gateRenderer.SetPropertyBlock(_mpb);
        }

        // ── Public ─────────────────────────────────────────────────────────

        public bool IsOpen => _isOpen;
        public void ConnectDownstream(IAutomationNode node) => _downstream = node;
    }
}
