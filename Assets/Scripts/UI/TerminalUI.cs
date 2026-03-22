using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Automation;
using Voidborne.Building.Electricity;

namespace Voidborne.UI
{
    /// <summary>
    /// Full Terminal interface with four tabs:
    ///   Storage  — live grid of all items across all drives; searchable; click to pull
    ///   Machines — list of all networked machines with status, current recipe, throughput
    ///   Power    — mirrors PowerNetworkUI: generation, consumption, battery
    ///   Scripts  — automation scripting; input field + compile; error display + alerts
    ///
    /// Opened by UIManager.OpenTerminal(). Closed by Escape / UIManager.CloseTerminal().
    /// </summary>
    public class TerminalUI : MonoBehaviour
    {
        // ── Runtime ────────────────────────────────────────────────────────
        private ComputerTerminal _terminal;
        private GameObject       _panel;
        private bool             _isOpen;
        private bool             _directiveFlavourShown;

        // Tab buttons
        private Button _tabStorage;
        private Button _tabMachines;
        private Button _tabPower;
        private Button _tabScripts;

        // Tab panels
        private GameObject _panelStorage;
        private GameObject _panelMachines;
        private GameObject _panelPower;
        private GameObject _panelScripts;

        // Storage tab
        private InputField  _searchField;
        private ScrollRect  _storageScroll;
        private Transform   _storageContent;
        private Text        _storageStatus;

        // Machines tab
        private ScrollRect _machinesScroll;
        private Transform  _machinesContent;

        // Power tab
        private Text _powerGenText;
        private Text _powerDrawText;
        private Text _powerBattText;
        private Text _powerStatusText;
        private Text _powerVordText;

        // Scripts tab
        private InputField _scriptInput;
        private Text       _scriptOutput;
        private Button     _compileButton;
        private Text       _flavorText;

        private float _refreshTimer;
        private const float RefreshInterval = 1f;

        private readonly List<GameObject> _storageRows  = new List<GameObject>();
        private readonly List<GameObject> _machineRows  = new List<GameObject>();

        // ── Open / Close ───────────────────────────────────────────────────

        public bool IsOpen => _isOpen;

        public void Open(ComputerTerminal terminal)
        {
            _terminal = terminal;
            if (_panel == null) BuildUI();
            _panel.SetActive(true);
            _isOpen = true;
            ShowTab(0);
            RefreshAll();
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
            _isOpen = false;
            _terminal = null;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Update()
        {
            if (!_isOpen || _terminal == null) return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RefreshAll();
            }
        }

        // ── UI Construction ────────────────────────────────────────────────

        private void BuildUI()
        {
            _panel = new GameObject("TerminalPanel");
            _panel.transform.SetParent(transform, false);

            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(780, 560);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.10f, 0.97f);

            // Title bar
            var titleGO = CreateLabel(_panel.transform, "TERMINAL v1.0", 16,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, -20), new Vector2(0, 0));
            titleGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            titleGO.GetComponent<Text>().color = new Color(0.2f, 0.9f, 1f);

            // Tab strip
            var tabStrip = CreatePanel(_panel.transform, new Rect(0, -40, 780, 36),
                new Color(0.1f, 0.12f, 0.14f));
            _tabStorage  = CreateTabButton(tabStrip.transform, "STORAGE",  0);
            _tabMachines = CreateTabButton(tabStrip.transform, "MACHINES", 1);
            _tabPower    = CreateTabButton(tabStrip.transform, "POWER",    2);
            _tabScripts  = CreateTabButton(tabStrip.transform, "SCRIPTS",  3);

            // Content area
            var contentArea = CreatePanel(_panel.transform, new Rect(8, -82, 764, 460),
                new Color(0.04f, 0.05f, 0.07f));

            _panelStorage  = BuildStorageTab(contentArea.transform);
            _panelMachines = BuildMachinesTab(contentArea.transform);
            _panelPower    = BuildPowerTab(contentArea.transform);
            _panelScripts  = BuildScriptsTab(contentArea.transform);

            // Close button
            var closeBtn = CreateButton(_panel.transform, "✕ CLOSE",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-50, 16), new Vector2(80, 28));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIManager.Instance != null) UIManager.Instance.CloseTerminal();
            });
        }

        private GameObject BuildStorageTab(Transform parent)
        {
            var tab = CreatePanel(parent, new Rect(0, 0, 764, 460), Color.clear);

            // Search field
            _searchField = CreateInputField(tab.transform, "Search items...",
                new Vector2(0, 1), new Vector2(0.7f, 1),
                new Vector2(4, -4), new Vector2(-4, -30));
            _searchField.onValueChanged.AddListener(_ => RefreshStorageTab());

            _storageStatus = CreateText(tab.transform, "",
                new Vector2(0.7f, 1), new Vector2(1, 1),
                new Vector2(4, -4), new Vector2(-4, -30), 11, Color.grey);

            // Pull button row header
            CreateText(tab.transform, "Item                          Stored   [Pull]",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(4, -34), new Vector2(-4, -52), 11, new Color(0.5f, 0.7f, 0.9f));

            _storageScroll = CreateScrollRect(tab.transform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(4, 4), new Vector2(-4, -56));
            _storageContent = _storageScroll.content;

            return tab;
        }

        private GameObject BuildMachinesTab(Transform parent)
        {
            var tab = CreatePanel(parent, new Rect(0, 0, 764, 460), Color.clear);
            CreateText(tab.transform, "ID                   Type              Status",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(4, -4), new Vector2(-4, -22), 11, new Color(0.5f, 0.7f, 0.9f));
            _machinesScroll = CreateScrollRect(tab.transform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(4, 4), new Vector2(-4, -26));
            _machinesContent = _machinesScroll.content;
            return tab;
        }

        private GameObject BuildPowerTab(Transform parent)
        {
            var tab = CreatePanel(parent, new Rect(0, 0, 764, 460), Color.clear);
            _powerGenText    = CreateText(tab.transform, "Generation: —",
                new Vector2(0,1), new Vector2(1,1), new Vector2(8,-8),  new Vector2(-8,-28), 14, Color.white);
            _powerDrawText   = CreateText(tab.transform, "Consumption: —",
                new Vector2(0,1), new Vector2(1,1), new Vector2(8,-34), new Vector2(-8,-54), 14, Color.white);
            _powerBattText   = CreateText(tab.transform, "Battery: —",
                new Vector2(0,1), new Vector2(1,1), new Vector2(8,-60), new Vector2(-8,-80), 14, Color.white);
            _powerStatusText = CreateText(tab.transform, "",
                new Vector2(0,1), new Vector2(1,1), new Vector2(8,-86), new Vector2(-8,-106), 14, Color.white);
            _powerVordText   = CreateText(tab.transform, "",
                new Vector2(0,1), new Vector2(1,1), new Vector2(8,-112),new Vector2(-8,-132), 14, Color.white);
            return tab;
        }

        private GameObject BuildScriptsTab(Transform parent)
        {
            var tab = CreatePanel(parent, new Rect(0, 0, 764, 460), Color.clear);

            // Flavor text (shown once)
            _flavorText = CreateText(tab.transform,
                "<i>Directive syntax recognized. This language has been used before.</i>",
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(8,-4), new Vector2(-8,-22), 11, new Color(0.6f, 0.4f, 0.8f));
            _flavorText.gameObject.SetActive(false);

            // Help text
            CreateText(tab.transform,
                "IF [item] > [n] THEN PAUSE [id]  |  IF [item] < [n] THEN ACTIVATE [id]  |  ALERT WHEN [item] < [n]",
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(8,-26), new Vector2(-8,-42), 10, Color.grey);

            // Script input
            _scriptInput = CreateInputField(tab.transform, "// Enter automation rules here...",
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(8,-46), new Vector2(-8,-246));
            if (_scriptInput.textComponent != null)
                _scriptInput.textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Compile button
            _compileButton = CreateButton(tab.transform, "▶ COMPILE & RUN",
                new Vector2(0,1), new Vector2(0.4f,1),
                new Vector2(8,-250), new Vector2(-8,-272));
            _compileButton.onClick.AddListener(OnCompileClicked);

            // Output
            CreateText(tab.transform, "Output:",
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(8,-276), new Vector2(-8,-292), 11, new Color(0.5f,0.7f,0.9f));
            _scriptOutput = CreateText(tab.transform, "",
                new Vector2(0,1), new Vector2(1,1),
                new Vector2(8,-296), new Vector2(-8,-446), 11, Color.white);

            return tab;
        }

        // ── Tab switching ──────────────────────────────────────────────────

        private Button CreateTabButton(Transform parent, string label, int idx)
        {
            float w = 780f / 4f;
            var btn = CreateButton(parent, label,
                new Vector2(idx * w / 780f, 0), new Vector2((idx + 1) * w / 780f, 1),
                Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().color = new Color(0.7f, 0.85f, 1f);
            btn.onClick.AddListener(() => ShowTab(idx));
            return btn;
        }

        private void ShowTab(int idx)
        {
            _panelStorage .SetActive(idx == 0);
            _panelMachines.SetActive(idx == 1);
            _panelPower   .SetActive(idx == 2);
            _panelScripts .SetActive(idx == 3);

            if (idx == 3 && !_directiveFlavourShown)
            {
                _directiveFlavourShown = true;
                if (_flavorText != null) _flavorText.gameObject.SetActive(true);
            }

            RefreshAll();
        }

        // ── Refresh ────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_terminal == null) return;
            if (_panelStorage  != null && _panelStorage.activeSelf)  RefreshStorageTab();
            if (_panelMachines != null && _panelMachines.activeSelf) RefreshMachinesTab();
            if (_panelPower    != null && _panelPower.activeSelf)    RefreshPowerTab();
            if (_panelScripts  != null && _panelScripts.activeSelf)  RefreshScriptsAlerts();
        }

        private void RefreshStorageTab()
        {
            foreach (var r in _storageRows) Destroy(r);
            _storageRows.Clear();

            var contents = _terminal.GetAllStorageContents();
            string filter = _searchField != null ? _searchField.text.ToLowerInvariant() : "";

            int totalItems = 0;
            float rowHeight = 24f;
            float y = 0f;

            foreach (var kv in contents)
            {
                if (kv.Value <= 0) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    !kv.Key.displayName.ToLowerInvariant().Contains(filter)) continue;

                totalItems += kv.Value;

                var row = new GameObject($"Row_{kv.Key.itemId}");
                row.transform.SetParent(_storageContent, false);
                var rowRT = row.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0,1);
                rowRT.anchorMax = new Vector2(1,1);
                rowRT.pivot     = new Vector2(0,1);
                rowRT.offsetMin = new Vector2(2, -y - rowHeight);
                rowRT.offsetMax = new Vector2(-2, -y);

                var label = CreateChildText(row.transform, $"{kv.Key.displayName,-28} {kv.Value,8}",
                    11, Color.white);
                label.alignment = TextAnchor.MiddleLeft;

                // Pull button
                var pullBtn = new GameObject("PullBtn");
                pullBtn.transform.SetParent(row.transform, false);
                var pullRT = pullBtn.AddComponent<RectTransform>();
                pullRT.anchorMin = new Vector2(1,0);
                pullRT.anchorMax = new Vector2(1,1);
                pullRT.pivot     = new Vector2(1,0.5f);
                pullRT.sizeDelta = new Vector2(60, 0);
                pullRT.anchoredPosition = new Vector2(-2, 0);
                var pullBg   = pullBtn.AddComponent<Image>();
                pullBg.color = new Color(0.15f, 0.4f, 0.2f);
                var pullBtnComp = pullBtn.AddComponent<Button>();
                var pullLabel = CreateChildText(pullBtn.transform, "PULL", 10, Color.white);
                pullLabel.alignment = TextAnchor.MiddleCenter;
                var capturedItem = kv.Key;
                pullBtnComp.onClick.AddListener(() => PullItemToInventory(capturedItem, 1));

                _storageRows.Add(row);
                y += rowHeight;
            }

            // Resize content
            if (_storageContent != null)
            {
                var ct = _storageContent.GetComponent<RectTransform>();
                if (ct != null) ct.sizeDelta = new Vector2(0, y);
            }

            if (_storageStatus != null)
                _storageStatus.text = $"Total: {totalItems}";
        }

        private void RefreshMachinesTab()
        {
            foreach (var r in _machineRows) Destroy(r);
            _machineRows.Clear();

            float rowHeight = 26f;
            float y = 0f;

            foreach (var node in _terminal.NetworkNodes)
            {
                var row = new GameObject($"MachineRow_{node.NetworkId}");
                row.transform.SetParent(_machinesContent, false);
                var rowRT = row.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0,1);
                rowRT.anchorMax = new Vector2(1,1);
                rowRT.pivot     = new Vector2(0,1);
                rowRT.offsetMin = new Vector2(2, -y - rowHeight);
                rowRT.offsetMax = new Vector2(-2, -y);

                string paused = node.IsPausedByScript ? " [PAUSED]" : "";
                var label = CreateChildText(row.transform,
                    $"{node.NetworkId,-20} {node.MachineType,-17} {node.StatusLine}{paused}",
                    10, node.IsPausedByScript ? Color.yellow : Color.white);
                label.alignment = TextAnchor.MiddleLeft;

                _machineRows.Add(row);
                y += rowHeight;
            }

            if (_machinesContent != null)
            {
                var ct = _machinesContent.GetComponent<RectTransform>();
                if (ct != null) ct.sizeDelta = new Vector2(0, y);
            }
        }

        private void RefreshPowerTab()
        {
            // Find the nearest power network via a PowerNode on this terminal's GO or nearby
            var powerNode = _terminal.GetComponentInChildren<PowerNode>();
            if (powerNode == null)
                powerNode = FindClosestPowerNode();

            if (powerNode == null || powerNode.Network == null)
            {
                if (_powerGenText != null)    _powerGenText.text    = "No power network detected.";
                if (_powerDrawText != null)   _powerDrawText.text   = "";
                if (_powerBattText != null)   _powerBattText.text   = "";
                if (_powerStatusText != null) _powerStatusText.text = "";
                if (_powerVordText != null)   _powerVordText.text   = "";
                return;
            }

            var s = powerNode.Network.GetNetworkSummary();
            if (_powerGenText  != null) _powerGenText.text  = $"Generation:   {s.totalGeneration:F0} W";
            if (_powerDrawText != null) _powerDrawText.text = $"Consumption:  {s.totalConsumption:F0} W";
            float pct = s.batteryCapacity > 0 ? s.batteryLevel / s.batteryCapacity * 100f : 0f;
            if (_powerBattText != null) _powerBattText.text = $"Battery:      {pct:F0}%";
            if (_powerStatusText != null)
            {
                float net = s.totalGeneration - s.totalConsumption;
                _powerStatusText.text = net >= 0f
                    ? "<color=green>SURPLUS</color>"
                    : "<color=red>OVERDRAW</color>";
            }
            if (_powerVordText != null)
                _powerVordText.text = s.voidFilamentActive
                    ? "<color=#CC44FF>⚠ VORD ALERT — Void Filament active</color>"
                    : "";
        }

        private void RefreshScriptsAlerts()
        {
            if (_scriptOutput == null || _terminal == null) return;
            var sb = new System.Text.StringBuilder();

            // Compile errors
            foreach (var kv in _terminal.ScriptErrors)
                sb.AppendLine($"<color=red>ERROR Line {kv.Key}: {kv.Value}</color>");

            // Runtime alerts
            foreach (var alert in _terminal.ScriptAlerts)
                sb.AppendLine($"<color=yellow>{alert}</color>");

            if (sb.Length == 0 && !string.IsNullOrEmpty(_terminal.CurrentScript))
                sb.AppendLine("<color=green>Script running — no errors.</color>");

            _scriptOutput.text = sb.ToString();
        }

        // ── Actions ────────────────────────────────────────────────────────

        private void PullItemToInventory(ItemDefinition item, int quantity)
        {
            if (_terminal == null) return;
            int available = _terminal.QueryStoredAmount(item);
            int toExtract = Mathf.Min(quantity, available);
            if (toExtract <= 0) return;

            // Find DriveRack and extract
            foreach (var rack in _terminal.DriveRacks)
            {
                int got = rack.Extract(item, toExtract);
                if (got > 0)
                {
                    var playerInv = FindFirstObjectByType<PlayerInventory>();
                    playerInv?.AddItem(new ItemStack(item, got));
                    break;
                }
            }
            RefreshStorageTab();
        }

        private void OnCompileClicked()
        {
            if (_terminal == null || _scriptInput == null) return;
            bool ok = _terminal.SetScript(_scriptInput.text);
            RefreshScriptsAlerts();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private PowerNode FindClosestPowerNode()
        {
            var nodes = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);
            PowerNode closest = null;
            float minDist = float.MaxValue;
            foreach (var n in nodes)
            {
                float d = Vector3.Distance(n.transform.position, _terminal.transform.position);
                if (d < minDist) { minDist = d; closest = n; }
            }
            return (minDist < 20f) ? closest : null;
        }

        // ── UI Factory helpers ─────────────────────────────────────────────

        private GameObject CreatePanel(Transform parent, Rect rect, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.offsetMin = new Vector2(rect.x, -rect.y - rect.height);
            rt.offsetMax = new Vector2(rect.x + rect.width, -rect.y);
            if (color != Color.clear)
            {
                var img = go.AddComponent<Image>();
                img.color = color;
            }
            return go;
        }

        private Text CreateText(Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.color     = color;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = true;
            return t;
        }

        private GameObject CreateLabel(Transform parent, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var t = go.AddComponent<Text>();
            t.text     = text;
            t.fontSize = fontSize;
            t.color    = Color.white;
            t.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        private Text CreateChildText(Transform parent, string text, int fontSize, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4, 0); rt.offsetMax = new Vector2(-60, 0);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = fontSize; t.color = color;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = true;
            return t;
        }

        private Button CreateButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.22f, 0.32f);
            var btn = go.AddComponent<Button>();
            var t = CreateChildText(go.transform, label, 11, Color.white);
            t.alignment = TextAnchor.MiddleCenter;
            var rt2 = t.GetComponent<RectTransform>();
            rt2.anchorMin = Vector2.zero; rt2.anchorMax = Vector2.one;
            rt2.offsetMin = rt2.offsetMax = Vector2.zero;
            return btn;
        }

        private InputField CreateInputField(Transform parent, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("InputField");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.15f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(4,2); textRT.offsetMax = new Vector2(-4,-2);
            var textComp = textGO.AddComponent<Text>();
            textComp.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize  = 11;
            textComp.color     = Color.white;
            textComp.supportRichText = false;

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phRT = phGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(4,2); phRT.offsetMax = new Vector2(-4,-2);
            var phComp = phGO.AddComponent<Text>();
            phComp.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phComp.fontSize  = 11;
            phComp.color     = new Color(0.4f, 0.4f, 0.4f);
            phComp.text      = placeholder;
            phComp.fontStyle = FontStyle.Italic;

            var field = go.AddComponent<InputField>();
            field.textComponent   = textComp;
            field.placeholder     = phComp;
            field.lineType        = InputField.LineType.MultiLineNewline;
            return field;
        }

        private ScrollRect CreateScrollRect(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("ScrollRect");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.05f, 0.5f);

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(go.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0,1);
            contentRT.anchorMax = new Vector2(1,1);
            contentRT.pivot     = new Vector2(0,1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.content    = contentRT;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.scrollSensitivity = 20f;
            return scroll;
        }
    }
}
