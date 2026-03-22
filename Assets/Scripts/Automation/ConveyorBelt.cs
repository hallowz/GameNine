using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Physical rubber-and-metal belt placed in the world on a grid.
    ///
    /// Items sit visibly on top as small 3D models. Each automation tick, the front item
    /// attempts to transfer to the connected node; remaining items slide forward one step.
    ///
    /// Segment types:
    ///   Straight — standard single-direction segment.
    ///   Corner   — 90° turn; visual only (same tick logic, direction set at placement).
    ///   Slope    — moves items vertically; exitOffset controls the height delta.
    ///   Split    — 1 input → 2 outputs; alternates between primaryNode and secondaryNode,
    ///              or falls back to whichever has space.
    ///
    /// Speed tiers:
    ///   Basic (0.5 s tick) — registered with AutomationTickManager.Register().
    ///   Fast  (0.2 s tick) — registered with AutomationTickManager.RegisterFast().
    ///
    /// Power: basic draws 5W, fast draws 12W. Requires a PowerNode on the same GameObject
    /// (or no power check if PowerNode absent — offline mode for prototype).
    /// </summary>
    public class ConveyorBelt : MonoBehaviour, IAutomationNode, ITickable
    {
        // ── Enums ──────────────────────────────────────────────────────────

        public enum SegmentType { Straight, Corner, Slope, Split }
        public enum BeltSpeed   { Basic, Fast }

        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Belt Configuration")]
        [SerializeField] private SegmentType segmentType = SegmentType.Straight;
        [SerializeField] private BeltSpeed   speed       = BeltSpeed.Basic;

        [Tooltip("Maximum items this belt segment can hold before stalling.")]
        [SerializeField] private int maxItems = 5;

        [Header("Connections")]
        [Tooltip("Primary output — the next belt, hopper, or machine input port.")]
        [SerializeField] private MonoBehaviour primaryNodeMono;

        [Tooltip("Secondary output (Split segments only).")]
        [SerializeField] private MonoBehaviour secondaryNodeMono;

        [Header("Visuals")]
        [Tooltip("Renderer whose material scrolls to animate the belt surface.")]
        [SerializeField] private Renderer beltRenderer;
        [SerializeField] private float    scrollSpeed = 0.5f;

        // ── Runtime ────────────────────────────────────────────────────────

        private IAutomationNode _primary;
        private IAutomationNode _secondary;

        // Queue: index 0 = front (nearest exit), last index = back (entry end).
        private readonly List<ItemStack>   _items        = new List<ItemStack>();
        private readonly List<GameObject>  _visuals      = new List<GameObject>();

        // Split alternation state.
        private bool _splitToggle;

        // MaterialPropertyBlock for per-renderer texture offset (preserves SRP batching).
        private MaterialPropertyBlock _beltMPB;
        private Vector2 _texOffset;
        private static readonly int PropMainTexST = Shader.PropertyToID("_BaseMap_ST");

        // Segment length — 1 unit for legacy block placement, set by SegmentPlacementController
        // for wire-style segments that stretch between two ports.
        private float _segmentLength = 1f;

        // Visual item spacing along the belt's local Z (forward) axis.
        private float BeltLength => _segmentLength;
        private float ItemSpacing => BeltLength / Mathf.Max(maxItems, 1);

        // ── ITickable ──────────────────────────────────────────────────────

        public int TickPriority => 20;

        public void AutomationTick()
        {
            if (_items.Count == 0) return;

            // Try to push the front item to the next node.
            TryAdvanceFront();
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
            => item != null && _items.Count < maxItems;

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty || _items.Count >= maxItems) return false;
            _items.Add(stack); // added to the back
            SpawnVisual(stack, _items.Count - 1);
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            if (_items.Count == 0) return false;
            if (filter == null) return true;
            foreach (var s in _items)
                if (s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (filter == null || _items[i].item == filter)
                {
                    var result = _items[i];
                    _items.RemoveAt(i);
                    DestroyVisual(i);
                    return result;
                }
            }
            return new ItemStack(null, 0);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        // Connector references resolved at Awake from child AutomationConnectors.
        private AutomationConnector _outputConn;
        private AutomationConnector _secondaryConn;

        private void Awake()
        {
            _primary   = primaryNodeMono   as IAutomationNode;
            _secondary = secondaryNodeMono as IAutomationNode;
            _beltMPB   = new MaterialPropertyBlock();

            foreach (var c in GetComponentsInChildren<AutomationConnector>())
            {
                c.OnConnectionChanged += RefreshConnections;
                if (c.portId == "output")            _outputConn    = c;
                if (c.portId == "secondary_output")  _secondaryConn = c;
            }
        }

        private void RefreshConnections()
        {
            if (_outputConn    != null)
                _primary   = _outputConn.GetConnectedNode()    ?? (primaryNodeMono   as IAutomationNode);
            if (_secondaryConn != null)
                _secondary = _secondaryConn.GetConnectedNode() ?? (secondaryNodeMono as IAutomationNode);
        }

        private void OnEnable()
        {
            if (AutomationTickManager.Instance == null) return;
            if (speed == BeltSpeed.Fast)
                AutomationTickManager.Instance.RegisterFast(this);
            else
                AutomationTickManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
        }

        private void Update()
        {
            AnimateBeltSurface();
            UpdateItemVisualPositions();
        }

        // ── Internal logic ─────────────────────────────────────────────────

        private void TryAdvanceFront()
        {
            if (_items.Count == 0) return;
            var front = _items[0];

            if (segmentType == SegmentType.Split)
            {
                // Alternate primary/secondary, fall back if one is full.
                IAutomationNode preferred  = _splitToggle ? _secondary : _primary;
                IAutomationNode fallback   = _splitToggle ? _primary   : _secondary;

                if (preferred != null && preferred.CanAccept(front.item))
                {
                    preferred.TryInsert(front);
                    RemoveFront();
                    _splitToggle = !_splitToggle;
                }
                else if (fallback != null && fallback.CanAccept(front.item))
                {
                    fallback.TryInsert(front);
                    RemoveFront();
                    // Don't toggle — retry the preferred side next tick.
                }
                // else: belt stalls — do nothing this tick.
            }
            else
            {
                if (_primary != null && _primary.CanAccept(front.item))
                {
                    _primary.TryInsert(front);
                    RemoveFront();
                }
                // else: belt stalls.
            }
        }

        private void RemoveFront()
        {
            _items.RemoveAt(0);
            DestroyVisual(0);
            // Shift remaining visual indices
            // (handled by UpdateItemVisualPositions using index directly)
        }

        // ── Visuals ────────────────────────────────────────────────────────

        private void SpawnVisual(ItemStack stack, int index)
        {
            GameObject go;
            if (stack.item != null && stack.item.modelPrefab != null)
            {
                go = Instantiate(stack.item.modelPrefab);
                go.transform.localScale = Vector3.one * 0.25f;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = Vector3.one * 0.15f;
                Destroy(go.GetComponent<Collider>());
            }

            go.transform.SetParent(transform);
            _visuals.Insert(index, go);
        }

        private void DestroyVisual(int index)
        {
            if (index < 0 || index >= _visuals.Count) return;
            if (_visuals[index] != null)
                Destroy(_visuals[index]);
            _visuals.RemoveAt(index);
        }

        private void UpdateItemVisualPositions()
        {
            // Items are distributed from front (local Z = 0) to back (local Z = 1).
            // For slope segments, Y is interpolated too.
            float slopeHeight = (segmentType == SegmentType.Slope) ? 1f : 0f;

            for (int i = 0; i < _items.Count && i < _visuals.Count; i++)
            {
                if (_visuals[i] == null) continue;
                float t = (float)i / Mathf.Max(_items.Count - 1, 1);
                Vector3 localPos = new Vector3(0f, 0.1f + slopeHeight * t, t * BeltLength);
                _visuals[i].transform.localPosition = localPos;
            }
        }

        private void AnimateBeltSurface()
        {
            if (beltRenderer == null) return;
            _texOffset += Vector2.up * (scrollSpeed * Time.deltaTime);
            beltRenderer.GetPropertyBlock(_beltMPB);
            _beltMPB.SetVector(PropMainTexST, new Vector4(1f, 1f, _texOffset.x, _texOffset.y));
            beltRenderer.SetPropertyBlock(_beltMPB);
        }

        // ── Public helpers (for placement system) ──────────────────────────

        /// <summary>Connect this belt's primary output to another automation node.</summary>
        public void ConnectPrimary(IAutomationNode node)
        {
            _primary = node;
        }

        /// <summary>Connect this belt's secondary output (for Split segments).</summary>
        public void ConnectSecondary(IAutomationNode node)
        {
            _secondary = node;
        }

        /// <summary>Item count currently on this belt.</summary>
        public int ItemCount => _items.Count;

        /// <summary>True if the belt is at capacity and will stall incoming items.</summary>
        public bool IsStalled => _items.Count >= maxItems;

        /// <summary>
        /// Set the physical length of this belt segment (meters).
        /// Called by SegmentPlacementController when placing wire-style segments.
        /// </summary>
        public void SetSegmentLength(float length)
        {
            _segmentLength = Mathf.Max(0.1f, length);
        }
    }
}
