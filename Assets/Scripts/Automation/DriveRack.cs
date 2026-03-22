using System.Collections.Generic;
using UnityEngine;
using Voidborne;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Holds up to 4 StorageDrives and connects them to the Terminal network as a single storage pool.
    /// When a ComputerTerminal queries the rack, it aggregates item counts across all installed drives.
    /// Player can interact to eject or reinsert drives.
    /// Requires 50W power — drives are inaccessible when unpowered.
    /// </summary>
    public class DriveRack : MonoBehaviour, INetworkNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Rack Settings")]
        [SerializeField] private string networkId = "DriveRack_01";
        [SerializeField] private int    maxDrives = 4;

        [Header("Drive Slots (assign in Inspector or let Terminal detect)")]
        [SerializeField] private StorageDrive[] installedDrives = new StorageDrive[4];

        [Header("Power")]
        [Tooltip("PowerConsumer component on this GO. Rack requires 50W to operate.")]
        [SerializeField] private PowerConsumer powerConsumer;

        // ── INetworkNode ───────────────────────────────────────────────────
        public string NetworkId        => networkId;
        public string MachineType      => "Drive Rack";
        public string StatusLine
        {
            get
            {
                int filled   = 0;
                int total    = 0;
                int capacity = 0;
                foreach (var d in installedDrives)
                {
                    if (d == null) continue;
                    filled++;
                    total    += d.Total;
                    capacity += d.Capacity;
                }
                return $"{filled}/{maxDrives} drives  |  {total}/{capacity} items";
            }
        }
        public bool IsPausedByScript { get; set; }
        public bool TrySetRecipe(string recipeName) => false;

        /// <summary>True if the rack has a power consumer and it's unpowered.</summary>
        public bool IsUnpowered => powerConsumer != null && !powerConsumer.IsPowered;

        private void Awake()
        {
            if (powerConsumer == null)
                powerConsumer = GetComponent<PowerConsumer>();
        }

        // ── IInteractable ──────────────────────────────────────────────────
        public string InteractPrompt => IsUnpowered ? "Drive Rack (No Power)" : "Press E to manage Drive Rack";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= 3f;

        public void Interact(GameObject interactor)
        {
            // Terminal UI handles drive rack interaction through the Terminal.
            // For standalone use, log the contents as a fallback.
            Debug.Log($"[DriveRack] {networkId}: {StatusLine}");
        }

        // ── Public storage API (called by ComputerTerminal) ─────────────────

        /// <summary>Insert items into the best matching drive. Returns remainder. Blocked when unpowered.</summary>
        public int Insert(ItemDefinition item, int quantity)
        {
            if (IsUnpowered) return quantity;
            if (item == null || quantity <= 0) return quantity;
            int remaining = quantity;
            foreach (var drive in installedDrives)
            {
                if (drive == null || drive.isEjected || drive.isCorrupted) continue;
                remaining = drive.Insert(item, remaining);
                if (remaining <= 0) return 0;
            }
            return remaining;
        }

        /// <summary>Extract items from drives. Returns actual count extracted. Blocked when unpowered.</summary>
        public int Extract(ItemDefinition item, int quantity)
        {
            if (IsUnpowered) return 0;
            if (item == null || quantity <= 0) return 0;
            int extracted = 0;
            foreach (var drive in installedDrives)
            {
                if (drive == null || drive.isEjected || drive.isCorrupted) continue;
                int got = drive.Extract(item, quantity - extracted);
                extracted += got;
                if (extracted >= quantity) break;
            }
            return extracted;
        }

        /// <summary>Total count of item across all installed drives. Returns 0 when unpowered.</summary>
        public int Query(ItemDefinition item)
        {
            if (IsUnpowered) return 0;
            int total = 0;
            foreach (var drive in installedDrives)
            {
                if (drive == null || drive.isEjected || drive.isCorrupted) continue;
                total += drive.Query(item);
            }
            return total;
        }

        /// <summary>All item types and counts pooled from all drives.</summary>
        public Dictionary<ItemDefinition, int> GetAggregatedContents()
        {
            var result = new Dictionary<ItemDefinition, int>();
            foreach (var drive in installedDrives)
            {
                if (drive == null || drive.isEjected || drive.isCorrupted) continue;
                foreach (var kv in drive.Contents)
                {
                    result.TryGetValue(kv.Key, out int existing);
                    result[kv.Key] = existing + kv.Value;
                }
            }
            return result;
        }

        public StorageDrive[] InstalledDrives => installedDrives;

        /// <summary>Install a drive into the first empty slot. Returns true on success.</summary>
        public bool InstallDrive(StorageDrive drive)
        {
            for (int i = 0; i < installedDrives.Length; i++)
            {
                if (installedDrives[i] == null)
                {
                    installedDrives[i] = drive;
                    drive.Reinsert();
                    return true;
                }
            }
            return false;
        }
    }
}
