using UnityEngine;
using Voidborne;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Placeable building piece. Interact to open VehicleAssemblyUI.
    /// Handles: assemble new vehicle, full workshop repair, reconfigure loadout.
    /// </summary>
    public class VehicleWorkbench : MonoBehaviour, IInteractable
    {
        [Header("Workbench")]
        [SerializeField] private float interactRange = 3f;

        // The vehicle currently parked at this workbench (optional)
        private AssembledVehicle _parkedVehicle;

        // ─── IInteractable ───────────────────────────────────────────────

        public string InteractPrompt => _parkedVehicle != null
            ? "Vehicle Workbench [Repair / Reconfigure]"
            : "Vehicle Workbench [Assemble]";

        public bool CanInteract(Vector3 fromPosition)
            => Vector3.Distance(fromPosition, transform.position) <= interactRange;

        public void Interact(GameObject interactor)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OpenVehicleWorkbench(this);
        }

        // ─── Workbench operations ────────────────────────────────────────

        /// <summary>Full workshop repair — restores all damage zones and replaces Destroyed components.</summary>
        public void FullRepair(AssembledVehicle vehicle)
        {
            if (vehicle == null) return;
            var ds = vehicle.GetComponent<VehicleDamageSystem>();
            if (ds != null) ds.FullRepair();
        }

        /// <summary>Park a vehicle at this workbench for repair/reconfiguration.</summary>
        public void ParkVehicle(AssembledVehicle vehicle)
        {
            _parkedVehicle = vehicle;
        }

        public AssembledVehicle GetParkedVehicle() => _parkedVehicle;
    }
}
