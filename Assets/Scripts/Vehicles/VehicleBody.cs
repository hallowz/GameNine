using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Section manager for the modular body system. Creates child GOs with VehicleBodyPartSlot
    /// for each AttachmentPoint in the VehicleFrame, grouped by BodySection.
    /// </summary>
    public class VehicleBody : MonoBehaviour
    {
        private readonly Dictionary<string, VehicleBodyPartSlot> _slotsByIdMap
            = new Dictionary<string, VehicleBodyPartSlot>();
        private readonly Dictionary<BodySection, List<VehicleBodyPartSlot>> _sectionSlots
            = new Dictionary<BodySection, List<VehicleBodyPartSlot>>();
        private readonly List<VehicleBodyPartSlot> _allSlots = new List<VehicleBodyPartSlot>();

        private AssembledVehicle _assembly;

        public IReadOnlyList<VehicleBodyPartSlot> AllSlots => _allSlots;

        /// <summary>Call once after the vehicle is assembled to build slot objects from frame data.</summary>
        public void Initialize(VehicleFrame frame)
        {
            _assembly = GetComponent<AssembledVehicle>();

            foreach (var point in frame.attachmentPoints)
            {
                // Skip points without a slotId — these are legacy-only attachment points
                if (string.IsNullOrEmpty(point.slotId)) continue;

                var slotGO = new GameObject("Slot_" + point.slotId);
                slotGO.transform.SetParent(transform, false);
                slotGO.transform.localPosition = point.localPosition;
                slotGO.transform.localRotation = Quaternion.Euler(point.localRotation);

                var slot = slotGO.AddComponent<VehicleBodyPartSlot>();
                slot.Init(point, this);

                _slotsByIdMap[point.slotId] = slot;
                _allSlots.Add(slot);

                if (!_sectionSlots.ContainsKey(point.section))
                    _sectionSlots[point.section] = new List<VehicleBodyPartSlot>();
                _sectionSlots[point.section].Add(slot);
            }
        }

        // ─── Public API ──────────────────────────────────────────────────

        public VehicleBodyPartSlot GetSlot(string slotId)
        {
            _slotsByIdMap.TryGetValue(slotId, out var slot);
            return slot;
        }

        public List<VehicleBodyPartSlot> GetSlotsInSection(BodySection section)
        {
            if (_sectionSlots.TryGetValue(section, out var list)) return list;
            return new List<VehicleBodyPartSlot>();
        }

        public bool InstallPart(string slotId, VehiclePartItem part, VehiclePartCondition condition)
        {
            var slot = GetSlot(slotId);
            if (slot == null) return false;
            if (!slot.Install(part, condition)) return false;

            // If part wraps a legacy component, register it in AssembledVehicle for backward compat
            if (part.wrappedComponent != null && _assembly != null)
                _assembly.InstallComponent(part.wrappedComponent);

            return true;
        }

        public (VehiclePartItem part, VehiclePartCondition condition)? RemovePart(string slotId)
        {
            var slot = GetSlot(slotId);
            if (slot == null) return null;
            return slot.Uninstall();
        }

        /// <summary>Detach all parts as WorldItems (used on vehicle destruction).</summary>
        public void DetachAll()
        {
            foreach (var slot in _allSlots)
            {
                if (!slot.IsEmpty)
                    slot.Detach();
            }
        }

        /// <summary>Called by VehicleBodyPartSlot when a part is ejected.</summary>
        public void OnSlotEmptied(VehicleBodyPartSlot slot)
        {
            // Future: notify HUD, trigger effects, check if vehicle is still driveable
        }

        // ─── Damage routing ─────────────────────────────────────────────

        /// <summary>Route damage to a specific slot by ID.</summary>
        public void ReceivePartDamage(string slotId, float amount)
        {
            var slot = GetSlot(slotId);
            if (slot != null && !slot.IsEmpty)
                slot.TakeDamage(amount);
        }

        /// <summary>Route damage to all parts in a section.</summary>
        public void ReceiveSectionDamage(BodySection section, float amount)
        {
            var slots = GetSlotsInSection(section);
            if (slots.Count == 0) return;

            // Spread damage evenly across occupied slots in the section
            float perSlot = amount;
            int occupied = 0;
            foreach (var s in slots)
                if (!s.IsEmpty) occupied++;
            if (occupied == 0) return;

            perSlot = amount / occupied;
            foreach (var s in slots)
            {
                if (!s.IsEmpty)
                    s.TakeDamage(perSlot);
            }
        }
    }
}
