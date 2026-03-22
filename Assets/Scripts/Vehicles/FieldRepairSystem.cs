using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Player MonoBehaviour. Hold E near a damaged vehicle to repair.
    /// Raycasts toward the vehicle — if a specific body part slot is hit, repairs that part.
    /// Otherwise falls back to repairing the worst-damaged zone.
    /// Consumes one Repair Kit charge per repair action.
    /// </summary>
    public class FieldRepairSystem : MonoBehaviour
    {
        [Header("Repair")]
        [SerializeField] private float repairRange = 3f;
        [SerializeField] private float holdDuration = 1.5f;
        [SerializeField] private float partRepairAmount = 25f;
        [SerializeField] private ItemDefinition repairKitItem;
        [SerializeField] private LayerMask vehicleLayers = ~0;

        private float _holdTimer;
        private VehicleDamageSystem _targetVehicle;
        private DamageZone _targetZone;
        private VehicleBodyPartSlot _targetSlot;
        private bool _isRepairing;

        public string RepairPrompt { get; private set; }

        private readonly Collider[] _buffer = new Collider[8];
        private PlayerInventory _playerInventory;
        private Camera _cam;

        private void Awake()
        {
            _playerInventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            bool eHeld = Keyboard.current.eKey.isPressed;

            if (!eHeld)
            {
                _holdTimer = 0f;
                _isRepairing = false;
                _targetSlot = null;
                RepairPrompt = null;
                return;
            }

            // Try raycast-to-part first
            if (_cam == null) _cam = Camera.main;
            var (hitSlot, hitVehicle, hitZone) = RaycastForTarget();

            if (hitSlot != null)
            {
                // Part-targeted repair
                _targetSlot = hitSlot;
                _targetVehicle = hitVehicle;
                _targetZone = hitZone;

                if (hitSlot.Condition.IsDestroyed)
                {
                    RepairPrompt = $"{hitSlot.slotId}: Destroyed — needs workshop";
                    _holdTimer = 0f;
                    return;
                }

                if (hitSlot.Condition.stage == DamageStage.Healthy)
                {
                    RepairPrompt = $"{hitSlot.slotId}: Healthy";
                    _holdTimer = 0f;
                    return;
                }

                if (!HasRepairKit())
                {
                    RepairPrompt = "No Repair Kit";
                    _holdTimer = 0f;
                    return;
                }

                float fraction = _holdTimer / holdDuration;
                RepairPrompt = $"Repairing {hitSlot.slotId} [{(int)(fraction * 100)}%]";

                _holdTimer += Time.deltaTime;
                _isRepairing = true;

                if (_holdTimer >= holdDuration)
                {
                    _holdTimer = 0f;
                    ConsumeKit();
                    hitSlot.RepairDamage(partRepairAmount);
                }
                return;
            }

            // Fallback: zone-based repair
            _targetSlot = null;
            var (vehicle, zone) = FindNearestDamagedZone();

            if (vehicle == null)
            {
                _holdTimer = 0f;
                RepairPrompt = null;
                return;
            }

            _targetVehicle = vehicle;
            _targetZone = zone;

            var zoneState = vehicle.GetZone(zone);

            if (zoneState.IsDestroyed)
            {
                RepairPrompt = $"{zone}: Destroyed — needs workshop repair";
                _holdTimer = 0f;
                return;
            }

            if (!HasRepairKit())
            {
                RepairPrompt = "No Repair Kit";
                _holdTimer = 0f;
                return;
            }

            float frac = _holdTimer / holdDuration;
            RepairPrompt = $"Repairing {zone} [{(int)(frac * 100)}%]";

            _holdTimer += Time.deltaTime;
            _isRepairing = true;

            if (_holdTimer >= holdDuration)
            {
                _holdTimer = 0f;
                ConsumeKit();
                vehicle.RepairZone(zone);
            }
        }

        private (VehicleBodyPartSlot, VehicleDamageSystem, DamageZone) RaycastForTarget()
        {
            if (_cam == null)
                return (null, null, DamageZone.Chassis);

            Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, repairRange, vehicleLayers))
                return (null, null, DamageZone.Chassis);

            // Check if we hit a body part slot directly
            var slot = hit.collider.GetComponentInParent<VehicleBodyPartSlot>();
            if (slot != null && !slot.IsEmpty)
            {
                var ds = slot.GetComponentInParent<VehicleDamageSystem>();
                return (slot, ds, DamageZone.Chassis);
            }

            return (null, null, DamageZone.Chassis);
        }

        private bool HasRepairKit()
        {
            if (repairKitItem == null || _playerInventory == null) return true;
            return _playerInventory.Hotbar.CountItem(repairKitItem.itemId) > 0
                || _playerInventory.Main.CountItem(repairKitItem.itemId) > 0;
        }

        private void ConsumeKit()
        {
            if (repairKitItem == null || _playerInventory == null) return;
            if (_playerInventory.Hotbar.CountItem(repairKitItem.itemId) > 0)
                _playerInventory.Hotbar.RemoveItem(repairKitItem.itemId, 1);
            else if (_playerInventory.Main.CountItem(repairKitItem.itemId) > 0)
                _playerInventory.Main.RemoveItem(repairKitItem.itemId, 1);
        }

        private (VehicleDamageSystem, DamageZone) FindNearestDamagedZone()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, repairRange, _buffer, vehicleLayers);
            VehicleDamageSystem best = null;
            DamageZone bestZone = DamageZone.Chassis;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var ds = _buffer[i].GetComponentInParent<VehicleDamageSystem>();
                if (ds == null) continue;
                float d = Vector3.Distance(transform.position, ds.transform.position);
                if (d >= bestDist) continue;

                foreach (var z in ds.AllZones)
                {
                    if (z.stage == DamageStage.Healthy) continue;
                    bestDist = d;
                    best = ds;
                    bestZone = z.zone;
                    break;
                }
            }
            return (best, bestZone);
        }
    }
}
