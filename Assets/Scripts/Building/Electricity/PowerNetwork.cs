using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// A connected graph of PowerNodes linked by wires/cables.
    /// Ticked by PowerNetworkManager. Distributes power in priority order.
    /// Handles 2% transmission loss per cable segment between source and consumer.
    /// </summary>
    public class PowerNetwork
    {
        public int NetworkId { get; private set; }

        private readonly List<PowerNode> _nodes = new List<PowerNode>();
        private readonly List<WireConnection> _wires = new List<WireConnection>();

        // Batteries are tracked separately for charge/discharge logic.
        private readonly List<BatteryBank> _batteries = new List<BatteryBank>();

        // Reusable sorted list to avoid per-tick allocation.
        private readonly List<PowerNode> _sortedNodes = new List<PowerNode>();
        private bool _sortDirty = true;

        // Cached last-tick stats.
        public float TotalGeneration { get; private set; }
        public float TotalConsumption { get; private set; }
        public float BatteryLevel { get; private set; }  // Aggregate watt-seconds stored
        public float BatteryCapacity { get; private set; }

        public bool IsSatisfied => TotalGeneration + _dischargeAvailable >= TotalConsumption;

        private float _dischargeAvailable;

        private static int _nextId = 1;

        public PowerNetwork()
        {
            NetworkId = _nextId++;
        }

        // ── Node management ────────────────────────────────────────────────

        public void AddNode(PowerNode node)
        {
            if (_nodes.Contains(node)) return;
            _nodes.Add(node);
            node.JoinNetwork(this);
            _sortDirty = true;
            if (node is BatteryBank battery)
                _batteries.Add(battery);
        }

        public void RemoveNode(PowerNode node)
        {
            _nodes.Remove(node);
            node.LeaveNetwork();
            _sortDirty = true;
            if (node is BatteryBank battery)
                _batteries.Remove(battery);
        }

        public IReadOnlyList<PowerNode> Nodes => _nodes;

        // ── Wire management ────────────────────────────────────────────────

        public void AddWire(WireConnection wire)
        {
            if (!_wires.Contains(wire))
                _wires.Add(wire);
        }

        public void RemoveWire(WireConnection wire)
        {
            _wires.Remove(wire);
        }

        public IReadOnlyList<WireConnection> Wires => _wires;

        // ── Tick ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called every second by PowerNetworkManager.
        /// dt = seconds since last tick (normally 1.0).
        /// </summary>
        public void Tick(float dt)
        {
            // 1–2. Single pass: sum generation, total consumption, and split battery vs non-battery draw.
            float rawGeneration = 0f;
            TotalConsumption = 0f;
            float nonBatteryConsumption = 0f;
            float batteryDraw = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                rawGeneration += node.GetCurrentOutput();
                float draw = node.GetCurrentDraw();
                TotalConsumption += draw;
                if (node is BatteryBank)
                    batteryDraw += draw;
                else
                    nonBatteryConsumption += draw;
            }

            // Apply 2% loss per cable segment for wired connections.
            float lossFraction = CalculateTransmissionLoss();
            TotalGeneration = rawGeneration * (1f - lossFraction);

            // 3. Battery logic.
            float netBeforeBattery = TotalGeneration - nonBatteryConsumption;

            // Update battery capacity totals.
            BatteryCapacity = 0f;
            BatteryLevel = 0f;
            foreach (var b in _batteries)
            {
                BatteryCapacity += b.maxCapacity;
                BatteryLevel += b.StoredEnergy;
            }

            _dischargeAvailable = BatteryLevel;

            if (netBeforeBattery > 0f)
            {
                // Surplus — charge batteries.
                float surplus = netBeforeBattery * dt;
                foreach (var b in _batteries)
                {
                    float absorbed = b.Charge(surplus);
                    surplus -= absorbed;
                    if (surplus <= 0f) break;
                }
            }
            else if (netBeforeBattery < 0f)
            {
                // Deficit — discharge batteries.
                float deficit = Mathf.Abs(netBeforeBattery) * dt;
                foreach (var b in _batteries)
                {
                    float discharged = b.Discharge(deficit);
                    deficit -= discharged;
                    if (deficit <= 0f) break;
                }
            }

            // Refresh stored energy after charge/discharge.
            BatteryLevel = 0f;
            foreach (var b in _batteries)
                BatteryLevel += b.StoredEnergy;

            // 4. Determine which nodes are powered (priority order).
            float availableWatts = TotalGeneration + (BatteryLevel / Mathf.Max(dt, 0.001f));

            // Sort nodes by priority (lower = more important, powered first).
            // Batteries are always considered powered for this step.
            if (_sortDirty)
            {
                _sortedNodes.Clear();
                _sortedNodes.AddRange(_nodes);
                _sortedNodes.Sort((a, b2) => a.priority.CompareTo(b2.priority));
                _sortDirty = false;
            }

            for (int si = 0; si < _sortedNodes.Count; si++)
            {
                var node = _sortedNodes[si];
                if (node is BatteryBank)
                {
                    node.SetPowered(true);
                    continue;
                }

                float draw = node.GetCurrentDraw();
                if (draw <= 0f)
                {
                    // Generator — powered if it can produce (fuel check is internal).
                    node.SetPowered(node.GetCurrentOutput() > 0f || draw == 0f);
                }
                else if (availableWatts >= draw)
                {
                    node.SetPowered(true);
                    availableWatts -= draw;
                }
                else
                {
                    node.SetPowered(false);
                }
            }

            // 5. Enforce wire throughput limits (hard cutoff).
            EnforceThroughputLimits();

            // Refresh TotalConsumption to reflect actually-powered nodes.
            TotalConsumption = 0f;
            foreach (var node in _nodes)
                if (node.IsPowered && !(node is BatteryBank))
                    TotalConsumption += node.GetCurrentDraw();
        }

        // ── Throughput enforcement ────────────────────────────────────────

        // Per-wire utilization for probe display. Populated during EnforceThroughputLimits.
        private readonly Dictionary<WireConnection, float> _wireFlow = new Dictionary<WireConnection, float>();

        /// <summary>Returns the current watts flowing through a wire (0 if not tracked).</summary>
        public float GetWireFlow(WireConnection wire) => _wireFlow.TryGetValue(wire, out float f) ? f : 0f;

        /// <summary>Returns utilization 0–1 for a wire (flow / maxThroughput). 0 if unlimited.</summary>
        public float GetWireUtilization(WireConnection wire)
        {
            if (wire == null || !wire.HasThroughputLimit) return 0f;
            float flow = GetWireFlow(wire);
            return Mathf.Clamp01(flow / wire.MaxThroughputWatts);
        }

        /// <summary>
        /// After main power distribution, check each throughput-limited wire.
        /// For each: split the graph at that wire, find the deficit side,
        /// and unpower lowest-priority nodes until flow fits within the limit.
        /// </summary>
        private void EnforceThroughputLimits()
        {
            _wireFlow.Clear();

            // Build adjacency for the current network.
            var adj = new Dictionary<PowerNode, List<(PowerNode neighbor, WireConnection wire)>>(_nodes.Count);
            foreach (var n in _nodes)
                adj[n] = new List<(PowerNode, WireConnection)>();
            foreach (var w in _wires)
            {
                if (w.NodeA != null && w.NodeB != null)
                {
                    adj[w.NodeA].Add((w.NodeB, w));
                    adj[w.NodeB].Add((w.NodeA, w));
                }
            }

            foreach (var wire in _wires)
            {
                if (!wire.HasThroughputLimit) continue;
                if (wire.NodeA == null || wire.NodeB == null) continue;

                // BFS from NodeA without crossing this wire → component A.
                var sideA = FloodWithout(wire.NodeA, wire, adj);
                // Everything not in sideA is sideB (if in the same network).
                var sideB = new HashSet<PowerNode>();
                foreach (var n in _nodes)
                    if (!sideA.Contains(n)) sideB.Add(n);

                if (sideB.Count == 0) continue; // wire is redundant (cycle)

                // Calculate generation and draw for each side (only powered nodes).
                float genA = 0f, drawA = 0f, genB = 0f, drawB = 0f;
                foreach (var n in sideA)
                {
                    if (n.IsPowered) genA += n.GetCurrentOutput();
                    if (n.IsPowered && !(n is BatteryBank)) drawA += n.GetCurrentDraw();
                }
                foreach (var n in sideB)
                {
                    if (n.IsPowered) genB += n.GetCurrentOutput();
                    if (n.IsPowered && !(n is BatteryBank)) drawB += n.GetCurrentDraw();
                }

                // Net flow: positive = A exports to B, negative = B exports to A.
                float netA = genA - drawA; // surplus on A side
                float flow = Mathf.Abs(netA);

                _wireFlow[wire] = flow;

                if (flow <= wire.MaxThroughputWatts) continue;

                // Determine which side has the deficit (needs imported power).
                HashSet<PowerNode> deficitSide = netA > 0 ? sideB : sideA;
                float excess = flow - wire.MaxThroughputWatts;

                // Collect powered consumers on the deficit side, sorted by priority descending
                // (lowest priority = most expendable = unpowered first).
                var consumers = new List<PowerNode>();
                foreach (var n in deficitSide)
                {
                    if (n.IsPowered && !(n is BatteryBank) && n.GetCurrentDraw() > 0f)
                        consumers.Add(n);
                }
                consumers.Sort((a, b) => b.priority.CompareTo(a.priority));

                foreach (var node in consumers)
                {
                    if (excess <= 0f) break;
                    float draw = node.GetCurrentDraw();
                    node.SetPowered(false);
                    excess -= draw;
                }
            }
        }

        /// <summary>BFS from start, skipping the given wire. Returns the reachable set.</summary>
        private HashSet<PowerNode> FloodWithout(
            PowerNode start, WireConnection skip,
            Dictionary<PowerNode, List<(PowerNode neighbor, WireConnection wire)>> adj)
        {
            var visited = new HashSet<PowerNode>();
            var queue = new Queue<PowerNode>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adj.TryGetValue(current, out var edges)) continue;
                foreach (var (neighbor, wire) in edges)
                {
                    if (wire == skip) continue;
                    if (neighbor != null && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return visited;
        }

        // ── Transmission loss ──────────────────────────────────────────────

        /// <summary>Returns the fractional loss (0–1) for this network's wiring.</summary>
        private float CalculateTransmissionLoss()
        {
            // Each wire segment applies 2% loss. We average the hop-count across the network.
            // For a simple network (small base) this is near zero.
            if (_wires.Count == 0) return 0f;
            float totalSegments = 0f;
            foreach (var w in _wires)
                totalSegments += w.SegmentCount;
            float avgSegments = totalSegments / _wires.Count;
            // Cap at 90% loss so networks with many relay boxes still function.
            return Mathf.Min(0.9f, avgSegments * 0.02f);
        }

        // ── Network merge/split ────────────────────────────────────────────

        /// <summary>Merges all nodes and wires from other into this network.</summary>
        public void MergeFrom(PowerNetwork other)
        {
            foreach (var node in new List<PowerNode>(other._nodes))
            {
                other._nodes.Remove(node);
                AddNode(node);
            }
            foreach (var wire in new List<WireConnection>(other._wires))
            {
                other._wires.Remove(wire);
                AddWire(wire);
            }
        }

        // ── Summary ────────────────────────────────────────────────────────

        public NetworkSummary GetNetworkSummary()
        {
            bool hasVoidFilament = false;
            foreach (var n in _nodes)
            {
                if (n is VoidFilamentGenerator vfg && vfg.IsPowered)
                {
                    hasVoidFilament = true;
                    break;
                }
            }

            return new NetworkSummary
            {
                networkId          = NetworkId,
                totalGeneration    = TotalGeneration,
                totalConsumption   = TotalConsumption,
                batteryLevel       = BatteryLevel,
                batteryCapacity    = BatteryCapacity,
                nodeCount          = _nodes.Count,
                isSatisfied        = IsSatisfied,
                voidFilamentActive = hasVoidFilament
            };
        }

        /// <summary>
        /// Returns up to <paramref name="maxCount"/> nodes sorted by current draw (descending).
        /// Excludes generators (draw == 0) and batteries.
        /// </summary>
        public List<PowerNode> GetTopConsumers(int maxCount = 5)
        {
            var result = new List<PowerNode>(_nodes);
            result.Sort((a, b) => b.GetCurrentDraw().CompareTo(a.GetCurrentDraw()));

            var filtered = new List<PowerNode>();
            foreach (var n in result)
            {
                if (n is BatteryBank) continue;
                if (n.GetCurrentDraw() <= 0f) continue;
                filtered.Add(n);
                if (filtered.Count >= maxCount) break;
            }
            return filtered;
        }
    }

    [System.Serializable]
    public struct NetworkSummary
    {
        public int   networkId;
        public float totalGeneration;
        public float totalConsumption;
        public float batteryLevel;
        public float batteryCapacity;
        public int   nodeCount;
        public bool  isSatisfied;
        /// <summary>True if at least one active VoidFilamentGenerator is on this network.</summary>
        public bool  voidFilamentActive;
    }

    /// <summary>Represents a wire or cable connection between two nodes, with segment count for loss calculation.</summary>
    public class WireConnection
    {
        public PowerNode NodeA;
        public PowerNode NodeB;
        public int SegmentCount = 1;  // 1 for short wire, >1 for cable runs

        /// <summary>Maximum watts this wire can carry. 0 = unlimited (legacy wires).</summary>
        public float MaxThroughputWatts;

        public WireConnection(PowerNode a, PowerNode b, int segments = 1, float maxThroughput = 0f)
        {
            NodeA = a;
            NodeB = b;
            SegmentCount = segments;
            MaxThroughputWatts = maxThroughput;
        }

        public PowerNode Other(PowerNode from) => from == NodeA ? NodeB : NodeA;

        /// <summary>True if this wire has a throughput limit.</summary>
        public bool HasThroughputLimit => MaxThroughputWatts > 0f;
    }
}
