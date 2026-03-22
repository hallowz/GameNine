using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Building.Electricity;

namespace Voidborne.UI
{
    /// <summary>
    /// Full network overview panel shown while the player holds the Power Probe.
    /// Raycasts from camera centre; if a PowerNode is within scan range, it displays
    /// the complete network: generation, consumption, battery, all nodes by type.
    /// </summary>
    public class PowerProbeUI : MonoBehaviour
    {
        // ── References (built in code if null) ────────────────────────────
        private GameObject    _panel;
        private Text          _headerText;
        private Text          _statsText;
        private Text          _nodeListText;
        private Text          _hintText;

        private Camera        _cam;
        private float         _scanRange;
        private bool          _probeActive;

        private const float RefreshInterval = 0.25f;
        private float _refreshTimer;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main;
            // BuildPanel is deferred to first ActivateProbe so UIManager's Canvas exists.
        }

        private void Update()
        {
            if (!_probeActive) return;

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = RefreshInterval;

            Refresh();
        }

        // ── Public ─────────────────────────────────────────────────────────

        public void ActivateProbe(float scanRange)
        {
            if (_panel == null) BuildPanel();
            _scanRange    = scanRange;
            _probeActive  = true;
            _panel.SetActive(true);
            _refreshTimer = 0f;
        }

        public void DeactivateProbe()
        {
            _probeActive = false;
            if (_panel != null) _panel.SetActive(false);
        }

        // ── Refresh ────────────────────────────────────────────────────────

        private void Refresh()
        {
            PowerNode node = GetLookedAtNode();

            if (node == null || node.Network == null)
            {
                _headerText.text   = "POWER PROBE";
                _statsText.text    = "No network in range.";
                _nodeListText.text = "";
                _hintText.text     = "Aim at a power device to scan.";
                return;
            }

            var net = node.Network;
            var s   = net.GetNetworkSummary();

            float battPct = s.batteryCapacity > 0f
                ? s.batteryLevel / s.batteryCapacity * 100f
                : 0f;
            float efficiency = s.totalGeneration > 0f
                ? Mathf.Clamp01(s.totalConsumption / s.totalGeneration) * 100f
                : 0f;
            float surplus = s.totalGeneration - s.totalConsumption;
            string statusColor = surplus >= 0f ? "#00FF88" : "#FF4444";
            string statusLabel = surplus >= 0f
                ? $"<color={statusColor}>SURPLUS  +{surplus:F0} W</color>"
                : $"<color={statusColor}>OVERDRAW  {surplus:F0} W</color>";

            _headerText.text = $"NET #{s.networkId}  —  {s.nodeCount} devices";

            _statsText.text =
                $"Gen:   {s.totalGeneration:F0} W\n" +
                $"Draw:  {s.totalConsumption:F0} W\n" +
                $"{statusLabel}\n" +
                $"Batt:  {battPct:F0}%  ({s.batteryLevel:F0}/{s.batteryCapacity:F0} Ws)\n" +
                $"Wires: {net.Wires.Count}  |  Efficiency: {efficiency:F0}%";

            _nodeListText.text = BuildNodeList(net);
            _hintText.text     = "Right-click: cancel  X: cut wire";
        }

        private string BuildNodeList(PowerNetwork net)
        {
            var lines = new System.Text.StringBuilder();
            var nodes = new List<PowerNode>(net.Nodes);
            // Sort: generators first, then by name
            nodes.Sort((a, b) =>
            {
                int ga = a.powerOutput > 0 ? 0 : 1;
                int gb = b.powerOutput > 0 ? 0 : 1;
                if (ga != gb) return ga.CompareTo(gb);
                return a.gameObject.name.CompareTo(b.gameObject.name);
            });

            foreach (var n in nodes)
            {
                string powered = n.IsPowered ? "<color=#00FF88>●</color>" : "<color=#FF4444>●</color>";
                string role    = n.powerOutput > 0f
                    ? $"+{n.GetCurrentOutput():F0}W"
                    : (n is BatteryBank bb
                        ? $"BATT {bb.ChargePercent * 100f:F0}%"
                        : $"-{n.GetCurrentDraw():F0}W");
                lines.AppendLine($"{powered} {n.gameObject.name}  {role}");
            }
            return lines.ToString();
        }

        private PowerNode GetLookedAtNode()
        {
            if (_cam == null) return null;
            Ray ray = _cam.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(ray, out RaycastHit hit, _scanRange))
                return hit.collider.GetComponentInParent<PowerNode>();
            return null;
        }

        // ── Build panel in code ────────────────────────────────────────────

        private void BuildPanel()
        {
            // Find or create a Screen Space – Overlay Canvas.
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("UICanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            Transform canvasT = canvas.transform;

            _panel = new GameObject("PowerProbePanel");
            _panel.transform.SetParent(canvasT, false);

            // Anchor to right side of screen.
            var panelRT = _panel.AddComponent<RectTransform>();
            panelRT.anchorMin        = new Vector2(1f, 0.5f);
            panelRT.anchorMax        = new Vector2(1f, 0.5f);
            panelRT.pivot            = new Vector2(1f, 0.5f);
            panelRT.anchoredPosition = new Vector2(-10f, 0f);
            panelRT.sizeDelta        = new Vector2(280f, 420f);

            // Background.
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.10f, 0.92f);

            // Header.
            _headerText = AddTextChild(_panel, "Header",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -8f), new Vector2(-10f, -8f - 28f),
                16, FontStyle.Bold, Color.cyan);

            // Divider.
            AddDivider(_panel, -38f);

            // Stats block.
            _statsText = AddTextChild(_panel, "Stats",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -44f), new Vector2(-10f, -44f - 120f),
                12, FontStyle.Normal, new Color(0.85f, 0.85f, 0.85f));

            // Divider.
            AddDivider(_panel, -168f);

            // Node list.
            _nodeListText = AddTextChild(_panel, "NodeList",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -174f), new Vector2(-10f, -174f - 210f),
                11, FontStyle.Normal, new Color(0.70f, 0.70f, 0.70f));

            // Divider.
            AddDivider(_panel, -388f);

            // Hint footer.
            _hintText = AddTextChild(_panel, "Hint",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -392f), new Vector2(-10f, -392f - 22f),
                10, FontStyle.Italic, new Color(0.5f, 0.5f, 0.5f));
        }

        private static Text AddTextChild(
            GameObject parent,
            string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var t = go.GetComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = color;
            t.text      = "";
            return t;
        }

        private static void AddDivider(GameObject parent, float offsetY)
        {
            var go = new GameObject("Div", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(0f, offsetY);
            rt.sizeDelta        = new Vector2(-20f, 1f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
        }
    }
}
