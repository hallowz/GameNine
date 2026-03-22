using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Placeable workstation. Interact to open the Terminal UI.
    ///
    /// Discovers all connected INetworkNode members by crawling NetworkCable connections.
    /// Also functions as the storage aggregator: machines on the network can automatically
    /// pull materials from and push output to connected DriveRacks — no conveyors needed
    /// for the last step once a machine is networked.
    ///
    /// Requires 10W power to operate.
    /// </summary>
    public class ComputerTerminal : MonoBehaviour, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Network")]
        [Tooltip("Automatically finds NetworkCables whose endpoints include this Terminal.")]
        [SerializeField] private float cableSearchRadius = 30f;

        [Header("Power")]
        [Tooltip("PowerConsumer component. Terminal requires 10W to operate.")]
        [SerializeField] private PowerConsumer powerConsumer;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text screenText;

        // ── Runtime ────────────────────────────────────────────────────────

        private readonly List<INetworkNode>   _networkNodes = new List<INetworkNode>();
        private readonly List<DriveRack>      _driveRacks   = new List<DriveRack>();
        private readonly AutomationScriptEngine _scriptEngine = new AutomationScriptEngine();

        // Drive-network storage (aggregated from all racks)
        private string _currentScript = string.Empty;
        private float  _refreshTimer;
        private const float RefreshInterval = 2f;

        public event Action OnNetworkChanged;

        /// <summary>True if the terminal has a power consumer and it's unpowered.</summary>
        public bool IsUnpowered => powerConsumer != null && !powerConsumer.IsPowered;

        // ── IInteractable ──────────────────────────────────────────────────
        public string InteractPrompt => IsUnpowered ? "Terminal (No Power)" : "Press E to open Terminal";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= 3f;

        public void Interact(GameObject interactor)
        {
            if (IsUnpowered)
            {
                Debug.Log("[ComputerTerminal] No power — terminal is offline.");
                return;
            }
            RefreshNetwork();
            if (UIManager.Instance != null)
                UIManager.Instance.OpenTerminal(this);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _scriptEngine.SetTerminal(this);
            if (powerConsumer == null)
                powerConsumer = GetComponent<PowerConsumer>();
        }

        private void Start()
        {
            RefreshNetwork();
            // Register with AutomationTickManager for script execution
            AutomationTickManager.Instance?.Register(new TerminalTickProxy(this));
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                UpdateScreenText();
            }
        }

        // ── Network discovery ──────────────────────────────────────────────

        /// <summary>Re-scan all NetworkCables within range to rebuild the node list.</summary>
        public void RefreshNetwork()
        {
            _networkNodes.Clear();
            _driveRacks.Clear();

            // Find all NetworkCable components within search radius
            var cables = FindObjectsByType<NetworkCable>(FindObjectsSortMode.None);
            var visited = new HashSet<INetworkNode>();

            foreach (var cable in cables)
            {
                if (cable == null) continue;
                // Only include cables that touch this terminal
                bool touchesTerminal = cable.nodeA != null &&
                    cable.nodeA.GetComponentInParent<ComputerTerminal>() == this;
                bool touchesTerminalB = cable.nodeB != null &&
                    cable.nodeB.GetComponentInParent<ComputerTerminal>() == this;

                if (!touchesTerminal && !touchesTerminalB) continue;

                var nodeA = cable.GetNodeA();
                var nodeB = cable.GetNodeB();

                if (nodeA != null && !(nodeA is ComputerTerminal) && !visited.Contains(nodeA))
                {
                    visited.Add(nodeA);
                    _networkNodes.Add(nodeA);
                    if (nodeA is DriveRack rack) _driveRacks.Add(rack);
                }
                if (nodeB != null && !(nodeB is ComputerTerminal) && !visited.Contains(nodeB))
                {
                    visited.Add(nodeB);
                    _networkNodes.Add(nodeB);
                    if (nodeB is DriveRack rack2) _driveRacks.Add(rack2);
                }
            }

            OnNetworkChanged?.Invoke();
        }

        // ── Storage API (used by script engine and TerminalUI) ─────────────

        /// <summary>Query total stored amount of an item by display name across all racks.</summary>
        public int QueryStoredAmount(string itemName)
        {
            foreach (var rack in _driveRacks)
            {
                var contents = rack.GetAggregatedContents();
                foreach (var kv in contents)
                    if (kv.Key.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        return kv.Value;
            }
            return 0;
        }

        /// <summary>Query by ItemDefinition reference.</summary>
        public int QueryStoredAmount(ItemDefinition item)
        {
            int total = 0;
            foreach (var rack in _driveRacks)
                total += rack.Query(item);
            return total;
        }

        /// <summary>Pull items from storage by name. Returns the ItemStack (may be partial or empty).</summary>
        public ItemStack PullFromStorage(string itemName, int quantity)
        {
            foreach (var rack in _driveRacks)
            {
                var contents = rack.GetAggregatedContents();
                foreach (var kv in contents)
                {
                    if (!kv.Key.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    int extracted = rack.Extract(kv.Key, quantity);
                    if (extracted > 0)
                        return new ItemStack(kv.Key, extracted);
                }
            }
            return new ItemStack(null, 0);
        }

        /// <summary>Push items back into storage (used by script engine when machine rejects).</summary>
        public void PushToStorage(ItemStack stack)
        {
            if (stack.IsEmpty || stack.item == null) return;
            int remaining = stack.quantity;
            foreach (var rack in _driveRacks)
            {
                remaining = rack.Insert(stack.item, remaining);
                if (remaining <= 0) return;
            }
            // Items that didn't fit are dropped in the world near the terminal.
            if (remaining > 0)
                Debug.LogWarning($"[ComputerTerminal] Storage full — could not store {remaining}× {stack.item.displayName}");
        }

        /// <summary>Get aggregated storage contents across all racks (for TerminalUI Storage tab).</summary>
        public Dictionary<ItemDefinition, int> GetAllStorageContents()
        {
            var result = new Dictionary<ItemDefinition, int>();
            foreach (var rack in _driveRacks)
            {
                foreach (var kv in rack.GetAggregatedContents())
                {
                    result.TryGetValue(kv.Key, out int existing);
                    result[kv.Key] = existing + kv.Value;
                }
            }
            return result;
        }

        /// <summary>Find an INetworkNode by its NetworkId (case-insensitive).</summary>
        public INetworkNode FindNetworkNode(string id)
        {
            foreach (var node in _networkNodes)
                if (node.NetworkId.Equals(id, System.StringComparison.OrdinalIgnoreCase))
                    return node;
            return null;
        }

        // ── Script API ─────────────────────────────────────────────────────

        public string CurrentScript => _currentScript;

        /// <summary>Compile and store a new automation script.</summary>
        public bool SetScript(string script)
        {
            _currentScript = script;
            return _scriptEngine.Compile(script);
        }

        public Dictionary<int, string> ScriptErrors  => _scriptEngine.CompileErrors;
        public List<string>            ScriptAlerts   => _scriptEngine.RuntimeAlerts;

        /// <summary>Called by TerminalTickProxy each automation tick.</summary>
        internal void OnTick()
        {
            _scriptEngine.Execute();
        }

        // ── Networked machine auto-supply (called by machines on their ticks) ───

        /// <summary>
        /// A networked machine can call this to request materials from connected drives.
        /// Returns how many were successfully extracted.
        /// </summary>
        public int RequestMaterial(ItemDefinition item, int quantity)
        {
            int total = 0;
            foreach (var rack in _driveRacks)
            {
                int got = rack.Extract(item, quantity - total);
                total += got;
                if (total >= quantity) break;
            }
            return total;
        }

        /// <summary>A networked machine deposits its output to connected drives.</summary>
        public void DepositOutput(ItemStack stack)
        {
            if (stack.IsEmpty || stack.item == null) return;
            PushToStorage(stack);
        }

        // ── Public read-only access ────────────────────────────────────────

        public IReadOnlyList<INetworkNode> NetworkNodes => _networkNodes;
        public IReadOnlyList<DriveRack>    DriveRacks   => _driveRacks;

        // ── Screen display ─────────────────────────────────────────────────

        private void UpdateScreenText()
        {
            if (screenText == null) return;

            if (IsUnpowered)
            {
                screenText.text = "TERMINAL OFFLINE\nNo power supply";
                return;
            }

            var contents = GetAllStorageContents();
            int itemTypes = contents.Count;
            int totalItems = 0;
            foreach (var kv in contents) totalItems += kv.Value;
            screenText.text =
                $"TERMINAL ONLINE\n" +
                $"Nodes: {_networkNodes.Count}  Racks: {_driveRacks.Count}\n" +
                $"Storage: {itemTypes} types / {totalItems} items\n" +
                (_scriptEngine.RuntimeAlerts.Count > 0
                    ? $"ALERTS: {_scriptEngine.RuntimeAlerts.Count}"
                    : "No alerts");
        }

        // ── Inner proxy class (lets Terminal participate in tick system) ────

        private class TerminalTickProxy : ITickable
        {
            private readonly ComputerTerminal _terminal;
            public TerminalTickProxy(ComputerTerminal t) => _terminal = t;
            public int TickPriority => 90; // runs late in the tick — after machines deposit
            public void AutomationTick() => _terminal.OnTick();
        }
    }
}
