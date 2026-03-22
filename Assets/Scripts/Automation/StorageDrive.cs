using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne;
using Voidborne.Combat;

namespace Voidborne.Automation
{
    /// <summary>
    /// Placeable unit that stores up to 50,000 items digitally (matter compression).
    /// Connected to the ComputerTerminal network via DriveRack or direct NetworkCable.
    ///
    /// Categories: Raw Materials / Components / Fuel / Misc — one drive per category
    /// is recommended but not enforced.
    ///
    /// isCorrupted: set when the drive takes a direct hit — a random portion of contents
    /// is lost and the drive must be repaired with a Drive Repair Kit.
    ///
    /// isEjected: drives can be physically removed and carried (they become DriveItem in inventory).
    /// Reinserting into a DriveRack makes the contents available again.
    /// </summary>
    public class StorageDrive : MonoBehaviour, INetworkNode, IDamageable
    {
        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Drive Settings")]
        [SerializeField] private string networkId = "Drive_01";
        [SerializeField] private DriveCategory category = DriveCategory.RawMaterials;
        [Tooltip("Maximum total item count across all stored types.")]
        [SerializeField] private int maxCapacity = 50000;

        [Header("Drive Item (for ejection)")]
        [Tooltip("The DriveItem ScriptableObject that represents this drive as a portable inventory item.")]
        [SerializeField] private DriveItem driveItemDefinition;

        [Header("Visual")]
        [SerializeField] private Renderer[] statusLights;
        [SerializeField] private Color normalColor   = Color.green;
        [SerializeField] private Color corruptColor  = Color.red;
        [SerializeField] private Color ejectedColor  = Color.grey;

        // ── Runtime ────────────────────────────────────────────────────────
        private readonly Dictionary<ItemDefinition, int> _contents = new Dictionary<ItemDefinition, int>();
        private int _totalStored;

        public bool isCorrupted { get; private set; }
        public bool isEjected   { get; private set; }

        public event Action OnContentsChanged;

        // ── INetworkNode ───────────────────────────────────────────────────
        public string NetworkId         => networkId;
        public string MachineType       => $"StorageDrive ({category})";
        public string StatusLine        => isCorrupted ? "[CORRUPTED]" :
                                          isEjected   ? "[EJECTED]"   :
                                          $"{_totalStored}/{maxCapacity} items";
        public bool   IsPausedByScript  { get; set; }
        public bool   TrySetRecipe(string recipeName) => false; // drives don't have recipes

        // ── IDamageable ────────────────────────────────────────────────────

        public void TakeDamage(DamageInfo info)
        {
            if (isCorrupted || isEjected) return;
            // Any direct hit corrupts the drive and randomly destroys a portion of contents.
            Corrupt();
        }

        // ── Unity lifecycle ────────────────────────────────────────────────
        private void Awake() => RefreshLights();

        // ── Public API ─────────────────────────────────────────────────────

        public DriveCategory Category  => category;
        public int            Total    => _totalStored;
        public int            Capacity => maxCapacity;

        public IReadOnlyDictionary<ItemDefinition, int> Contents => _contents;

        /// <summary>Insert items into the drive. Returns remainder that didn't fit.</summary>
        public int Insert(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0 || isCorrupted || isEjected) return quantity;
            int available = maxCapacity - _totalStored;
            int accepted  = Mathf.Min(quantity, available);
            if (accepted <= 0) return quantity;

            _contents.TryGetValue(item, out int existing);
            _contents[item]  = existing + accepted;
            _totalStored    += accepted;
            OnContentsChanged?.Invoke();
            RefreshLights();
            return quantity - accepted;
        }

        /// <summary>Extract up to <paramref name="quantity"/> of <paramref name="item"/>. Returns actual extracted count.</summary>
        public int Extract(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0 || isCorrupted || isEjected) return 0;
            if (!_contents.TryGetValue(item, out int held)) return 0;

            int extracted = Mathf.Min(quantity, held);
            _contents[item] = held - extracted;
            if (_contents[item] <= 0) _contents.Remove(item);
            _totalStored -= extracted;
            OnContentsChanged?.Invoke();
            return extracted;
        }

        /// <summary>Query how many of an item are stored.</summary>
        public int Query(ItemDefinition item)
        {
            if (item == null || isCorrupted || isEjected) return 0;
            _contents.TryGetValue(item, out int count);
            return count;
        }

        /// <summary>Corrupt the drive: lose a random portion of stored items.</summary>
        public void Corrupt()
        {
            if (isCorrupted) return;
            isCorrupted = true;

            // Lose 20–80% of each item type at random.
            var keys = new List<ItemDefinition>(_contents.Keys);
            foreach (var key in keys)
            {
                float lossFraction = UnityEngine.Random.Range(0.2f, 0.8f);
                int   lost         = Mathf.RoundToInt(_contents[key] * lossFraction);
                _contents[key] -= lost;
                _totalStored   -= lost;
                if (_contents[key] <= 0) _contents.Remove(key);
            }
            _totalStored = Mathf.Max(0, _totalStored);
            OnContentsChanged?.Invoke();
            RefreshLights();
            Debug.Log($"[StorageDrive] {networkId} corrupted — some items lost.");
        }

        /// <summary>Repair the drive with a Drive Repair Kit.</summary>
        public void Repair()
        {
            isCorrupted = false;
            RefreshLights();
            OnContentsChanged?.Invoke();
        }

        /// <summary>Eject the drive — it becomes a portable item. Clears it from the rack network.</summary>
        public ItemStack Eject()
        {
            if (isEjected) return new ItemStack(null, 0);
            isEjected = true;
            RefreshLights();
            OnContentsChanged?.Invoke();
            // Return a DriveItem stack (if defined) for the player to pick up.
            if (driveItemDefinition != null)
            {
                driveItemDefinition.SetRuntimeDrive(this);
                return new ItemStack(driveItemDefinition, 1);
            }
            return new ItemStack(null, 0);
        }

        /// <summary>Re-insert the drive into the rack — makes contents live again.</summary>
        public void Reinsert()
        {
            isEjected = false;
            RefreshLights();
            OnContentsChanged?.Invoke();
        }

        private void RefreshLights()
        {
            if (statusLights == null) return;
            Color c = isCorrupted ? corruptColor :
                      isEjected   ? ejectedColor  : normalColor;
            foreach (var r in statusLights)
                if (r != null)
                    r.material.color = c;
        }
    }

    public enum DriveCategory { RawMaterials, Components, Fuel, Misc }
}
