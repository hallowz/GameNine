using UnityEngine;
using Voidborne.UI;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Sits on the Player. Watches the active hotbar slot each frame and routes to
    /// the correct electricity tool mode:
    ///   • ElectricityItem  → ElectricityPlacementController (ghost preview + place)
    ///   • WireItem         → WireController (click-to-wire two nodes)
    ///   • PowerProbeItem   → PowerProbeUI (live network overview panel)
    ///   • anything else    → deactivate all three
    /// </summary>
    public class ElectricityItemHandler : MonoBehaviour
    {
        [SerializeField] private PlayerInventory             playerInventory;
        [SerializeField] private ElectricityPlacementController placementController;
        [SerializeField] private WireController              wireController;
        [SerializeField] private PowerProbeUI                powerProbeUI;

        private ItemDefinition _lastActive;

        private void Start()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            if (placementController == null)
                placementController = FindObjectOfType<ElectricityPlacementController>();

            if (wireController == null)
                wireController = FindObjectOfType<WireController>();

            if (powerProbeUI == null)
                powerProbeUI = FindObjectOfType<PowerProbeUI>();
            if (powerProbeUI == null)
            {
                var go = new GameObject("PowerProbeUI");
                powerProbeUI = go.AddComponent<PowerProbeUI>();
            }

            DeactivateAll();
        }

        private void Update()
        {
            if (playerInventory == null) return;

            // Don't switch modes while UI is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            {
                DeactivateAll();
                _lastActive = null;
                return;
            }

            ItemDefinition active = playerInventory.ActiveHotbarItem.item;

            if (active == _lastActive) return;
            _lastActive = active;

            DeactivateAll();

            if (active is ElectricityItem ei)
            {
                if (placementController != null) placementController.Activate(ei);
            }
            else if (active is WireItem wi)
            {
                if (wireController != null)
                {
                    wireController.SetWireTier(wi);
                    wireController.enabled = true;
                }
            }
            else if (active is PowerProbeItem pp)
            {
                if (powerProbeUI != null) powerProbeUI.ActivateProbe(pp.scanRange);
            }
        }

        private void DeactivateAll()
        {
            placementController?.Deactivate();
            if (wireController != null)    wireController.enabled    = false;
            if (powerProbeUI   != null)    powerProbeUI.DeactivateProbe();
        }
    }
}
