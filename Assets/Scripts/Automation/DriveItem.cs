using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// ItemDefinition subclass representing an ejected StorageDrive as a portable inventory item.
    /// Shows stored contents in the item tooltip.
    /// Can be reinserted into a DriveRack.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDriveItem", menuName = "Voidborne/Automation/Drive Item")]
    public class DriveItem : ItemDefinition
    {
        [Header("Drive Item")]
        [Tooltip("Category label shown in tooltip.")]
        public DriveCategory category = DriveCategory.Misc;

        // Runtime reference — set when the physical drive is ejected.
        // Not serialized; only valid in play mode.
        [System.NonSerialized]
        private StorageDrive _runtimeDrive;

        public StorageDrive RuntimeDrive => _runtimeDrive;

        public void SetRuntimeDrive(StorageDrive drive)
        {
            _runtimeDrive = drive;
        }

        /// <summary>Build a tooltip string listing stored contents.</summary>
        public string BuildContentsTooltip()
        {
            if (_runtimeDrive == null) return "[No Drive Data]";
            if (_runtimeDrive.isCorrupted) return "[DRIVE CORRUPTED — repair required]";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Drive: {category}  {_runtimeDrive.Total}/{_runtimeDrive.Capacity}");
            int shown = 0;
            foreach (var kv in _runtimeDrive.Contents)
            {
                if (shown >= 8) { sb.AppendLine("  ..."); break; }
                sb.AppendLine($"  {kv.Key.displayName}: {kv.Value}");
                shown++;
            }
            if (shown == 0) sb.AppendLine("  (empty)");
            return sb.ToString().TrimEnd();
        }
    }
}
