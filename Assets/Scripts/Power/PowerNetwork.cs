using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.1 — singleton power-graph solver. The single source of truth for
    /// "is this consumer powered, and at what fraction of its requirement?".
    ///
    /// <para>Graph shape:</para>
    /// <list type="bullet">
    /// <item><description>Nodes: every <see cref="PowerNode"/> that has
    /// auto-registered (generator / consumer / battery / sink / junction).</description></item>
    /// <item><description>Edges: every <see cref="CableSegment"/> that has
    /// connected via <see cref="ConnectCable"/>.</description></item>
    /// </list>
    ///
    /// <para>Tick (every <see cref="GameConstants.Power.NetworkTickHz"/> Hz):</para>
    /// <list type="number">
    /// <item><description>Walk connected components via BFS.</description></item>
    /// <item><description>Per component, sum generator output + consumer demand.</description></item>
    /// <item><description>Distribute supply to consumers in priority order
    /// (machines first, then gadgets, then sinks).</description></item>
    /// <item><description>Leftover supply -> batteries (charge).</description></item>
    /// <item><description>Deficit -> drain batteries first, then mark
    /// remaining consumers as underpowered (<c>CurrentWatts &lt; RequiredWatts</c>).</description></item>
    /// <item><description>Final surplus -> sinks (no cable burnout, no overflow).</description></item>
    /// <item><description>Burnout check: any cable whose flow exceeds its
    /// tier ceiling is disconnected with a warning (M2 minimum-viable; M7
    /// adds visual burnout).</description></item>
    /// </list>
    ///
    /// <para>Public API:</para>
    /// <list type="bullet">
    /// <item><description><see cref="RegisterNode"/> / <see cref="UnregisterNode"/>
    /// (auto-called by <see cref="PowerNode"/>).</description></item>
    /// <item><description><see cref="ConnectCable"/> / <see cref="DisconnectCable"/>
    /// (auto-called by <see cref="CableSegment"/>).</description></item>
    /// <item><description><see cref="GetComponentFor"/> -- diagnostic peek
    /// at a node's component aggregate.</description></item>
    /// <item><description><see cref="TickOnce"/> -- EditMode test seam that
    /// drives one tick deterministically without waiting for FixedUpdate.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Coop note: in V21 the tick runs server-authoritative. The graph
    /// topology is deterministic given the same Register / Connect events,
    /// so the algorithm above will produce identical CurrentWatts on every
    /// peer when fed the same inputs. Only generator gating conditions
    /// (boiler-active, player-cranking) need network sync.
    /// </remarks>
    [DisallowMultipleComponent]
    public class PowerNetwork : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Singleton
        // ---------------------------------------------------------------

        private static PowerNetwork _instance;

        /// <summary>
        /// Lazily-created singleton. PowerNode.OnEnable hits this on the
        /// first placed node; this getter spins up the host GO so the first
        /// register call always succeeds.
        /// </summary>
        public static PowerNetwork Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = FindFirstObjectByType<PowerNetwork>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }
                var go = new GameObject("PowerNetwork");
                _instance = go.AddComponent<PowerNetwork>();
                return _instance;
            }
        }

        /// <summary>
        /// EditMode test seam -- reset the singleton between fixtures so
        /// the per-test PowerNetwork starts empty.
        /// </summary>
        public static void ResetInstance()
        {
            if (_instance != null)
            {
                if (Application.isPlaying) Destroy(_instance.gameObject);
                else DestroyImmediate(_instance.gameObject);
            }
            _instance = null;
        }

        // ---------------------------------------------------------------
        //  Graph state
        // ---------------------------------------------------------------

        private readonly List<PowerNode> _nodes = new List<PowerNode>();
        private readonly List<CableSegment> _cables = new List<CableSegment>();

        /// <summary>All registered nodes (debug/diagnostic).</summary>
        public IReadOnlyList<PowerNode> Nodes => _nodes;

        /// <summary>All registered cables (debug/diagnostic).</summary>
        public IReadOnlyList<CableSegment> Cables => _cables;

        // ---------------------------------------------------------------
        //  Tick state
        // ---------------------------------------------------------------

        private float _tickAccumulator;

        /// <summary>
        /// Seconds between auto-ticks. Default 1/<see cref="GameConstants.Power.NetworkTickHz"/>
        /// (i.e. 10 Hz). Tests can override this via the inspector or
        /// (better) just call <see cref="TickOnce"/> directly.
        /// </summary>
        public float TickInterval = 1f / GameConstants.Power.NetworkTickHz;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            _instance = this;
        }

        private void FixedUpdate()
        {
            _tickAccumulator += Time.fixedDeltaTime;
            if (_tickAccumulator < TickInterval) return;

            float dt = _tickAccumulator;
            _tickAccumulator = 0f;
            TickOnce(dt);
        }

        // ---------------------------------------------------------------
        //  Node registration
        // ---------------------------------------------------------------

        /// <summary>Register a node. Idempotent (no double-add).</summary>
        public void RegisterNode(PowerNode node)
        {
            if (node == null) return;
            if (_nodes.Contains(node)) return;
            _nodes.Add(node);
        }

        /// <summary>
        /// Deregister a node + drop any cables that touched it. Idempotent.
        /// </summary>
        public void UnregisterNode(PowerNode node)
        {
            if (node == null) return;
            _nodes.Remove(node);
            // Drop dangling cables.
            for (int i = _cables.Count - 1; i >= 0; i--)
            {
                var c = _cables[i];
                if (c == null || c.EndpointA == node || c.EndpointB == node)
                {
                    _cables.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------
        //  Cable registration
        // ---------------------------------------------------------------

        /// <summary>
        /// Register a cable connecting two nodes. The <paramref name="segment"/>
        /// owns the tier + visual; this graph just stores the edge.
        /// </summary>
        public void ConnectCable(CableSegment segment)
        {
            if (segment == null) return;
            if (segment.EndpointA == null || segment.EndpointB == null) return;
            if (_cables.Contains(segment)) return;
            _cables.Add(segment);
        }

        /// <summary>Deregister a cable. Idempotent.</summary>
        public void DisconnectCable(CableSegment segment)
        {
            if (segment == null) return;
            _cables.Remove(segment);
        }

        // ---------------------------------------------------------------
        //  Diagnostics
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns the aggregate component info for the connected component
        /// containing <paramref name="node"/>. Result is computed on demand
        /// from the current graph state (cheap; M2 graphs are small).
        /// </summary>
        public PowerComponentInfo GetComponentFor(PowerNode node)
        {
            if (node == null) return default;
            var component = FloodFrom(node);
            return BuildComponentInfo(component);
        }

        // ---------------------------------------------------------------
        //  Tick
        // ---------------------------------------------------------------

        /// <summary>
        /// Run one solver pass over the current graph. <paramref name="dt"/>
        /// is the seconds elapsed since the previous tick (used by batteries
        /// to convert watts <-> watt-seconds).
        ///
        /// EditMode tests call this directly so they don't depend on a
        /// Unity update loop running.
        /// </summary>
        public void TickOnce(float dt)
        {
            if (dt <= 0f) dt = TickInterval;

            // 1. Walk components.
            var visited = new HashSet<PowerNode>();
            var sortedConsumers = new List<PowerNode>();
            // Reused per-component scratch buffers to keep allocations bounded.
            var componentNodes = new List<PowerNode>();
            var batteriesInComp = new List<Battery>();
            var sinksInComp = new List<PowerSink>();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var seed = _nodes[i];
                if (seed == null) continue;
                if (visited.Contains(seed)) continue;

                // BFS this component into componentNodes (cleared first).
                componentNodes.Clear();
                batteriesInComp.Clear();
                sinksInComp.Clear();
                FloodInto(seed, visited, componentNodes);

                // 2. Sum generation + demand.
                int totalGen = 0;
                int totalDemand = 0;
                sortedConsumers.Clear();

                for (int n = 0; n < componentNodes.Count; n++)
                {
                    var node = componentNodes[n];
                    if (node.IsGenerator)
                    {
                        int gen = ComputeGeneratorOutput(node);
                        totalGen += gen;
                        // Stash the actual gen on the node itself so the
                        // distribution pass below can attribute it correctly.
                        node.SetCurrentWatts(gen);
                    }
                    if (node.IsConsumer && node.RequiredWatts > 0)
                    {
                        // We'll allocate watts to this consumer in step 3.
                        // Set CurrentWatts=0 up front so deficient consumers
                        // don't carry stale state from last tick.
                        if (!node.IsGenerator) node.SetCurrentWatts(0);
                        totalDemand += node.RequiredWatts;
                        // Sinks have IsConsumer=true but the lowest priority --
                        // they only absorb surplus AFTER real loads are served.
                        if (node is PowerSink sink)
                        {
                            sinksInComp.Add(sink);
                        }
                        else
                        {
                            sortedConsumers.Add(node);
                        }
                    }
                    if (node.IsStorage && node is Battery battery)
                    {
                        batteriesInComp.Add(battery);
                    }
                }

                // 3. Distribute to real consumers (machines first, then gadgets).
                sortedConsumers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                int available = totalGen;
                for (int c = 0; c < sortedConsumers.Count; c++)
                {
                    var consumer = sortedConsumers[c];
                    int need = consumer.RequiredWatts;
                    int give = Mathf.Min(need, Mathf.Max(0, available));
                    consumer.SetCurrentWatts(give);
                    available -= give;
                }

                // 4. Surplus -> batteries (charge).
                int realConsumerDeficit = 0;
                for (int c = 0; c < sortedConsumers.Count; c++)
                {
                    var consumer = sortedConsumers[c];
                    int shortfall = consumer.RequiredWatts - consumer.CurrentWatts;
                    if (shortfall > 0) realConsumerDeficit += shortfall;
                }

                if (available > 0 && batteriesInComp.Count > 0)
                {
                    int per = available / batteriesInComp.Count;
                    int leftover = available - per * batteriesInComp.Count;
                    for (int b = 0; b < batteriesInComp.Count; b++)
                    {
                        int give = per + (b == 0 ? leftover : 0);
                        int absorbed = batteriesInComp[b].ChargeWatts(give, dt);
                        batteriesInComp[b].SetCurrentWatts(absorbed);
                        available -= absorbed;
                    }
                }
                else if (realConsumerDeficit > 0 && batteriesInComp.Count > 0)
                {
                    // 5. Deficit -> drain batteries to top up shortfall.
                    int remainingDeficit = realConsumerDeficit;
                    for (int b = 0; b < batteriesInComp.Count && remainingDeficit > 0; b++)
                    {
                        int drawn = batteriesInComp[b].DischargeWatts(remainingDeficit, dt);
                        batteriesInComp[b].SetCurrentWatts(-drawn);
                        remainingDeficit -= drawn;
                    }
                    // Re-distribute the discharge to consumers (priority order).
                    int recovered = realConsumerDeficit - remainingDeficit;
                    for (int c = 0; c < sortedConsumers.Count && recovered > 0; c++)
                    {
                        var consumer = sortedConsumers[c];
                        int shortfall = consumer.RequiredWatts - consumer.CurrentWatts;
                        if (shortfall <= 0) continue;
                        int give = Mathf.Min(shortfall, recovered);
                        consumer.SetCurrentWatts(consumer.CurrentWatts + give);
                        recovered -= give;
                    }
                }
                else
                {
                    // No surplus, no deficit -- make sure non-charging batteries
                    // report 0 so OnPowerChanged doesn't fire spuriously.
                    for (int b = 0; b < batteriesInComp.Count; b++)
                    {
                        batteriesInComp[b].SetCurrentWatts(0);
                    }
                }

                // 6. Final surplus -> sinks (cable protection).
                if (available > 0 && sinksInComp.Count > 0)
                {
                    int per = available / sinksInComp.Count;
                    int leftover = available - per * sinksInComp.Count;
                    for (int s = 0; s < sinksInComp.Count; s++)
                    {
                        var sink = sinksInComp[s];
                        int give = Mathf.Min(sink.RequiredWatts, per + (s == 0 ? leftover : 0));
                        sink.SetCurrentWatts(give);
                        available -= give;
                    }
                }
                else
                {
                    for (int s = 0; s < sinksInComp.Count; s++)
                    {
                        sinksInComp[s].SetCurrentWatts(0);
                    }
                }
            }

            // 7. Burnout check. M2: log + disconnect; M7 polish adds VFX.
            CheckCableBurnout();
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        /// <summary>BFS from <paramref name="seed"/> into <paramref name="output"/>, recording every visited node in <paramref name="visited"/>.</summary>
        private void FloodInto(PowerNode seed, HashSet<PowerNode> visited, List<PowerNode> output)
        {
            var queue = new Queue<PowerNode>();
            queue.Enqueue(seed);
            visited.Add(seed);
            output.Add(seed);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int e = 0; e < _cables.Count; e++)
                {
                    var cable = _cables[e];
                    if (cable == null) continue;
                    PowerNode other = null;
                    if (cable.EndpointA == cur) other = cable.EndpointB;
                    else if (cable.EndpointB == cur) other = cable.EndpointA;
                    if (other == null) continue;
                    if (visited.Add(other))
                    {
                        output.Add(other);
                        queue.Enqueue(other);
                    }
                }
            }
        }

        /// <summary>Compact BFS that returns the visited set (diagnostic).</summary>
        private List<PowerNode> FloodFrom(PowerNode seed)
        {
            var visited = new HashSet<PowerNode>();
            var result = new List<PowerNode>();
            FloodInto(seed, visited, result);
            return result;
        }

        /// <summary>
        /// Resolve a generator's per-tick output. Defers to the subclass
        /// for gated generators (SteamGenerator checks for a boiler; the
        /// HandCrankGenerator checks player-input).
        /// </summary>
        private int ComputeGeneratorOutput(PowerNode node)
        {
            if (!node.IsGenerator) return 0;
            if (node is PowerGenerator gen)
            {
                return gen.GetTickOutputWatts();
            }
            // Generic generator -- assume always-on at MaxOutputWatts.
            return Mathf.Max(0, node.MaxOutputWatts);
        }

        private PowerComponentInfo BuildComponentInfo(List<PowerNode> nodes)
        {
            int gen = 0;
            int demand = 0;
            int batteryStored = 0;
            int batteryCapacity = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.IsGenerator) gen += ComputeGeneratorOutput(n);
                if (n.IsConsumer) demand += n.RequiredWatts;
                if (n is Battery b)
                {
                    batteryStored += b.StoredWs;
                    batteryCapacity += b.CapacityWs;
                }
            }
            return new PowerComponentInfo
            {
                nodeCount = nodes.Count,
                totalGenerationWatts = gen,
                totalDemandWatts = demand,
                batteryStoredWs = batteryStored,
                batteryCapacityWs = batteryCapacity,
            };
        }

        /// <summary>
        /// Walk every cable; if it's carrying more than its tier allows,
        /// disconnect it. Flow approximation for M2: a cable carries the
        /// lesser of the two endpoints' CurrentWatts -- good enough for the
        /// burnout test (one 5kW gen wired through a 200W T1 cable to a
        /// consumer absolutely exceeds the ceiling).
        /// </summary>
        private void CheckCableBurnout()
        {
            for (int i = _cables.Count - 1; i >= 0; i--)
            {
                var cable = _cables[i];
                if (cable == null) { _cables.RemoveAt(i); continue; }
                int flow = ApproximateCableFlow(cable);
                cable.SetCurrentWatts(flow);
                int ceiling = cable.Tier.MaxWatts();
                if (flow > ceiling)
                {
                    Debug.LogWarning(
                        $"[PowerNetwork] Cable burnout: {cable.name} carrying {flow}W exceeds {cable.Tier} ceiling of {ceiling}W. Disconnecting.",
                        cable);
                    _cables.RemoveAt(i);
                    // Destroy the cable GO last so the list invariant is
                    // restored before any OnDisable hooks fire.
                    cable.NotifyBurnedOut();
                }
            }
        }

        private static int ApproximateCableFlow(CableSegment cable)
        {
            // Take the maximum of the two endpoints' absolute current watts.
            // For a series link (one gen -> one consumer through this cable),
            // both sides see the same flow magnitude. For a junction box
            // (pass-through), the higher of either side is the conservative
            // upper bound for burnout detection.
            int a = cable.EndpointA != null ? Mathf.Abs(cable.EndpointA.CurrentWatts) : 0;
            int b = cable.EndpointB != null ? Mathf.Abs(cable.EndpointB.CurrentWatts) : 0;
            return Mathf.Max(a, b);
        }
    }

    /// <summary>
    /// Aggregate diagnostic view of a connected component. Read-only.
    /// Returned by <see cref="PowerNetwork.GetComponentFor"/>.
    /// </summary>
    [Serializable]
    public struct PowerComponentInfo
    {
        public int nodeCount;
        public int totalGenerationWatts;
        public int totalDemandWatts;
        public int batteryStoredWs;
        public int batteryCapacityWs;
    }
}
