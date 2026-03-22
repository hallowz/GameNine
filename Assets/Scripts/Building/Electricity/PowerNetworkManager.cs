using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Singleton. Manages all PowerNetworks.
    /// Ticks them once per second. Handles network creation, merge, and split
    /// when wires are added or removed.
    /// </summary>
    public class PowerNetworkManager : MonoBehaviour
    {
        public static PowerNetworkManager Instance { get; private set; }

        private readonly List<PowerNetwork> _networks = new List<PowerNetwork>();
        private readonly List<WireConnection> _allWires = new List<WireConnection>();

        [Tooltip("Seconds between power ticks.")]
        public float tickInterval = 1f;

        public IReadOnlyList<PowerNetwork> Networks => _networks;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() => StartCoroutine(TickLoop());

        private IEnumerator TickLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickInterval);
                foreach (var network in _networks)
                    network.Tick(tickInterval);
            }
        }

        // ── Wire creation/destruction ──────────────────────────────────────

        /// <summary>
        /// Connect two nodes with a wire. Merges or creates networks as needed.
        /// Returns the new WireConnection.
        /// </summary>
        public WireConnection Connect(PowerNode a, PowerNode b, int segments = 1, float maxThroughput = 0f)
        {
            var wire = new WireConnection(a, b, segments, maxThroughput);
            _allWires.Add(wire);

            if (a.Network == null && b.Network == null)
            {
                var net = new PowerNetwork();
                _networks.Add(net);
                net.AddNode(a);
                net.AddNode(b);
                net.AddWire(wire);
            }
            else if (a.Network == null)
            {
                b.Network.AddNode(a);
                b.Network.AddWire(wire);
            }
            else if (b.Network == null)
            {
                a.Network.AddNode(b);
                a.Network.AddWire(wire);
            }
            else if (a.Network != b.Network)
            {
                // Merge smaller into larger.
                var keep   = a.Network.Nodes.Count >= b.Network.Nodes.Count ? a.Network : b.Network;
                var merge  = keep == a.Network ? b.Network : a.Network;
                keep.MergeFrom(merge);
                _networks.Remove(merge);
                keep.AddWire(wire);
            }
            else
            {
                // Same network — just add the wire.
                a.Network.AddWire(wire);
            }

            return wire;
        }

        /// <summary>
        /// Remove a wire. May split a network into two separate networks.
        /// </summary>
        public void Disconnect(WireConnection wire)
        {
            if (wire == null) return;
            _allWires.Remove(wire);

            var network = wire.NodeA?.Network ?? wire.NodeB?.Network;
            if (network == null) return;

            network.RemoveWire(wire);

            // Check if the network split by re-flooding from NodeA.
            RebuildNetworkFromScratch(network);
        }

        // ── Node registration (standalone, no wire) ────────────────────────

        public void RegisterNode(PowerNode node)
        {
            if (node.Network != null) return;
            var net = new PowerNetwork();
            _networks.Add(net);
            net.AddNode(node);
        }

        public void UnregisterNode(PowerNode node)
        {
            if (node.Network == null) return;
            var network = node.Network;

            // Remove all wires connected to this node.
            var toRemove = new List<WireConnection>();
            foreach (var w in _allWires)
                if (w.NodeA == node || w.NodeB == node)
                    toRemove.Add(w);

            foreach (var w in toRemove)
            {
                _allWires.Remove(w);
                network.RemoveWire(w);
            }

            network.RemoveNode(node);

            if (network.Nodes.Count == 0)
                _networks.Remove(network);
            else
                RebuildNetworkFromScratch(network);
        }

        // ── Network topology rebuild ───────────────────────────────────────

        /// <summary>
        /// After removing a wire, flood-fill from scratch to detect splits.
        /// All nodes in the network are temporarily detached and reassigned.
        /// Uses an adjacency list for O(N+W) BFS instead of O(N*W).
        /// </summary>
        private void RebuildNetworkFromScratch(PowerNetwork oldNetwork)
        {
            var allNodes  = new List<PowerNode>(oldNetwork.Nodes);
            var allWires  = new List<WireConnection>(oldNetwork.Wires);

            // Detach all nodes from the old network without triggering full unregister.
            foreach (var n in allNodes)
                n.LeaveNetwork();

            _networks.Remove(oldNetwork);

            // Build adjacency list for O(N+W) BFS instead of O(N*W) per-node wire scan
            var adj = new Dictionary<PowerNode, List<(PowerNode neighbor, WireConnection wire)>>(allNodes.Count);
            foreach (var n in allNodes)
                adj[n] = new List<(PowerNode, WireConnection)>();
            foreach (var w in allWires)
            {
                if (w.NodeA != null && w.NodeB != null)
                {
                    if (adj.ContainsKey(w.NodeA)) adj[w.NodeA].Add((w.NodeB, w));
                    if (adj.ContainsKey(w.NodeB)) adj[w.NodeB].Add((w.NodeA, w));
                }
            }

            // Flood-fill to build connected components.
            var unvisited = new HashSet<PowerNode>(allNodes);

            while (unvisited.Count > 0)
            {
                // Pick a seed.
                PowerNode seed = null;
                foreach (var n in unvisited) { seed = n; break; }

                var component = new HashSet<PowerNode>();
                var componentWires = new HashSet<WireConnection>();
                FloodFill(seed, unvisited, adj, component, componentWires);

                var net = new PowerNetwork();
                _networks.Add(net);
                foreach (var n in component)
                    net.AddNode(n);
                foreach (var w in componentWires)
                    net.AddWire(w);
            }
        }

        private void FloodFill(
            PowerNode start,
            HashSet<PowerNode> unvisited,
            Dictionary<PowerNode, List<(PowerNode neighbor, WireConnection wire)>> adj,
            HashSet<PowerNode> component,
            HashSet<WireConnection> componentWires)
        {
            var queue = new Queue<PowerNode>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                if (!adj.TryGetValue(current, out var edges)) continue;
                foreach (var (neighbor, wire) in edges)
                {
                    componentWires.Add(wire);
                    if (neighbor != null && unvisited.Contains(neighbor))
                    {
                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // ── Utility ────────────────────────────────────────────────────────

        public PowerNetwork GetNetworkForNode(PowerNode node) => node?.Network;
    }
}
