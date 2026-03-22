using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Marks a connection port on an automation device (belt, tube, hopper, etc.).
    ///
    /// Attach as a child GameObject of the device. The setup script creates
    /// a small indicator sphere on the same child so the port is visible in-game.
    ///
    /// When two ports Connect():
    ///   • ConnectedTo is set on both sides.
    ///   • OnConnectionChanged fires on both, telling the parent device to
    ///     refresh its IAutomationNode references.
    ///
    /// portId naming convention (used by devices to locate their specific ports):
    ///   "input"           — single incoming port
    ///   "output"          — single outgoing port (or primary output for Split)
    ///   "secondary_output"— second outgoing port (Split belts)
    ///   "left_output"     — sorter left output
    ///   "right_output"    — sorter right output
    ///   "default_output"  — sorter default/fallback output
    ///   "belt_port"       — hopper port facing the belt/tube
    /// </summary>
    public class AutomationConnector : MonoBehaviour
    {
        public enum PortType { Output, Input, Bidirectional }

        // ── Inspector / public fields (set by editor setup script) ────────

        public PortType portType = PortType.Output;
        public string   portId   = "output";

        /// <summary>Optional visual indicator sphere set by AutomationSetup.</summary>
        public Renderer indicator;

        // ── Runtime state ──────────────────────────────────────────────────

        public AutomationConnector ConnectedTo { get; private set; }
        public bool IsConnected => ConnectedTo != null;

        /// <summary>Fires on both connectors whenever the connection changes.</summary>
        public event System.Action OnConnectionChanged;

        // ── Indicator colours ──────────────────────────────────────────────

        private static readonly Color FreeColor      = new Color(1.00f, 0.55f, 0.00f); // orange
        private static readonly Color ConnectedColor = new Color(0.15f, 0.85f, 0.20f); // green
        public  static readonly Color SnapColor      = Color.white;

        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            SetIndicatorColor(FreeColor);
        }

        private void OnDestroy()
        {
            // Clean up the other side when a device is destroyed.
            Disconnect();
        }

        // ── Connection API ─────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this port is compatible with <paramref name="other"/>
        /// and neither is already connected.
        /// </summary>
        public bool CanConnectTo(AutomationConnector other)
        {
            if (other == null || other == this)   return false;
            if (IsConnected || other.IsConnected) return false;

            // Reject ports on the same device.
            if (GetDeviceNode() == other.GetDeviceNode() && GetDeviceNode() != null)
                return false;

            return portType == PortType.Bidirectional
                || other.portType == PortType.Bidirectional
                || (portType == PortType.Output && other.portType == PortType.Input)
                || (portType == PortType.Input  && other.portType == PortType.Output);
        }

        /// <summary>Links this port to <paramref name="other"/> and notifies both devices.</summary>
        public void Connect(AutomationConnector other)
        {
            if (!CanConnectTo(other)) return;

            ConnectedTo       = other;
            other.ConnectedTo = this;

            SetIndicatorColor(ConnectedColor);
            other.SetIndicatorColor(ConnectedColor);

            OnConnectionChanged?.Invoke();
            other.OnConnectionChanged?.Invoke();
        }

        /// <summary>Severs the connection and notifies both devices.</summary>
        public void Disconnect()
        {
            var was = ConnectedTo;
            ConnectedTo = null;
            SetIndicatorColor(FreeColor);
            OnConnectionChanged?.Invoke();

            if (was != null && was.ConnectedTo == this)
            {
                was.ConnectedTo = null;
                was.SetIndicatorColor(FreeColor);
                was.OnConnectionChanged?.Invoke();
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>Returns the IAutomationNode of the connected device, or null.</summary>
        public IAutomationNode GetConnectedNode()
            => ConnectedTo != null
               ? ConnectedTo.GetComponentInParent<IAutomationNode>()
               : null;

        /// <summary>The IAutomationNode this connector belongs to.</summary>
        public IAutomationNode GetDeviceNode()
            => GetComponentInParent<IAutomationNode>();

        // ── Snap highlight (used by placement controller) ──────────────────

        public void ShowSnapHighlight() => SetIndicatorColor(SnapColor);
        public void HideSnapHighlight() => SetIndicatorColor(IsConnected ? ConnectedColor : FreeColor);

        // ── Indicator visual ───────────────────────────────────────────────

        private void SetIndicatorColor(Color c)
        {
            if (indicator == null || _mpb == null) return;
            indicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId,     c);
            indicator.SetPropertyBlock(_mpb);
        }
    }
}
