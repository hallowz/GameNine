using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;
using Voidborne.World.Generation;

namespace Voidborne.Automation
{
    /// <summary>
    /// Placed on the terrain above an ore vein. Scans the density field below it for
    /// ore within 'scanRadius'. When powered (50 W) and active, extracts 1 ore per
    /// basic automation tick into its internal buffer (8 slots). A Hopper connected
    /// to the buffer output port will drain items onto belts/tubes.
    ///
    /// Depends on OreGenerator.CountOreInRadius / DepleteOre to track depletion.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class AutoMiner : MonoBehaviour, IAutomationNode, ITickable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Mining")]
        [Tooltip("Radius (world units) to scan for ore below the miner.")]
        [SerializeField] private float scanRadius = 4f;

        [Tooltip("Ore type index to mine (matches OreGenerator ore index). -1 = any ore found.")]
        [SerializeField] private int targetOreType = -1;

        [Header("Buffer")]
        [Tooltip("Maximum items held in the internal output buffer.")]
        [SerializeField] private int bufferCapacity = 8;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer  _power;
        private DrillHead      _drillHead;

        // Internal buffer — acts as the IAutomationNode hoppers connect to.
        private readonly List<ItemStack> _buffer = new List<ItemStack>();

        // Track last known ore count and type from OreGenerator.
        private int            _oreRemaining = -1;  // -1 = unchecked
        private byte           _detectedOreType;
        private ItemDefinition _oreItemDef;

        // ── ITickable ──────────────────────────────────────────────────────

        // Priority 40 = machine tier (after belts/hoppers have moved items out).
        public int TickPriority => 40;

        public void AutomationTick()
        {
            RefreshOreInfo();
            UpdateStatus();

            if (!_power.IsPowered)
            {
                _drillHead?.SetActive(false);
                return;
            }

            bool bufferFull = BufferCount() >= bufferCapacity;
            bool veinEmpty  = _oreRemaining == 0;

            if (bufferFull || veinEmpty || _oreItemDef == null)
            {
                _drillHead?.SetActive(false);
                return;
            }

            // Mine one ore: deplete the vein and add to buffer.
            Vector3 belowPos = transform.position + Vector3.down * 2f;
            bool    depleted = OreGenerator.DepleteOre(belowPos);

            if (depleted)
            {
                AddToBuffer(new ItemStack(_oreItemDef, 1));
                _oreRemaining = Mathf.Max(0, _oreRemaining - 1);
                _drillHead?.SetActive(true);
            }
            else
            {
                _drillHead?.SetActive(false);
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _power     = GetComponent<PowerConsumer>();
            _drillHead = GetComponentInChildren<DrillHead>();
        }

        private void OnEnable()
        {
            AutomationTickManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
            _drillHead?.SetActive(false);
        }

        // ── IAutomationNode — the buffer is exposed as an automation node ──

        public bool CanAccept(ItemDefinition item) => false; // miners don't accept input

        public bool TryInsert(ItemStack stack) => false;

        public bool HasItem(ItemDefinition filter)
        {
            foreach (var s in _buffer)
                if (filter == null || s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (filter == null || _buffer[i].item == filter)
                {
                    var src = _buffer[i];
                    var result = new ItemStack(src.item, 1);
                    _buffer[i] = new ItemStack(src.item, src.quantity - 1);
                    if (_buffer[i].quantity <= 0)
                        _buffer.RemoveAt(i);
                    return result;
                }
            }
            return new ItemStack(null, 0);
        }

        // ── Ore scan ───────────────────────────────────────────────────────

        private void RefreshOreInfo()
        {
            // Pick the ore type to target.
            byte oreType = targetOreType < 0 ? _detectedOreType : (byte)targetOreType;

            if (targetOreType < 0 && _oreItemDef == null)
            {
                // Auto-detect: find first ore type present below.
                for (byte t = 1; t < 16; t++)
                {
                    int cnt = OreGenerator.CountOreInRadius(
                        transform.position + Vector3.down * 2f, scanRadius, t);
                    if (cnt > 0)
                    {
                        oreType          = t;
                        _detectedOreType = t;
                        break;
                    }
                }
            }

            _oreRemaining = OreGenerator.CountOreInRadius(
                transform.position + Vector3.down * 2f, scanRadius, oreType);

            // Resolve ItemDefinition for this ore type.
            if (_oreItemDef == null && _oreRemaining > 0)
                _oreItemDef = OreTypeToItemDef(oreType);
        }

        private static ItemDefinition OreTypeToItemDef(byte oreType)
        {
            var registry = Voidborne.World.Chunks.ChunkManager.Instance?.OreRegistry;
            if (registry == null || registry.oreDefinitions == null) return null;
            foreach (var def in registry.oreDefinitions)
                if (def != null && def.oreTypeId == oreType)
                    return def.associatedItem;
            return null;
        }

        // ── Buffer helpers ─────────────────────────────────────────────────

        private int BufferCount()
        {
            int total = 0;
            foreach (var s in _buffer) total += s.quantity;
            return total;
        }

        private void AddToBuffer(ItemStack stack)
        {
            // Try to stack with existing entry.
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i].item == stack.item)
                {
                    _buffer[i] = new ItemStack(_buffer[i].item, _buffer[i].quantity + stack.quantity);
                    return;
                }
            }
            _buffer.Add(stack);
        }

        // ── Status display ─────────────────────────────────────────────────

        private void UpdateStatus()
        {
            if (statusText == null) return;
            string ore  = _oreItemDef != null ? _oreItemDef.displayName : "None";
            string vein = _oreRemaining < 0 ? "?" : _oreRemaining.ToString();
            statusText.text =
                $"Ore: {ore}\nVein: {vein}\nBuffer: {BufferCount()}/{bufferCapacity}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}";
        }

        // ── Public accessors (for UI / status reads) ───────────────────────

        public int  OreRemaining  => _oreRemaining;
        public int  BufferFill    => BufferCount();
        public int  BufferMax     => bufferCapacity;
        public bool IsMining      => _power.IsPowered && _oreRemaining > 0 && BufferCount() < bufferCapacity;
        public string OreTypeName => _oreItemDef != null ? _oreItemDef.displayName : "None";
    }
}
