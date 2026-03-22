using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// MonoBehaviour placed as a child GO at each attachment point on a vehicle.
    /// Holds an installed VehiclePartItem with runtime condition, manages visuals,
    /// and handles detachment as WorldItem.
    /// </summary>
    public class VehicleBodyPartSlot : MonoBehaviour
    {
        [Header("Slot Config")]
        public string slotId;
        public BodySection section;
        public AttachmentType attachmentType;
        public bool isRequired;

        [Header("Runtime State")]
        [SerializeField] private VehiclePartItem _installedPart;
        [SerializeField] private VehiclePartCondition _condition;

        private GameObject _visualInstance;
        private VehicleBody _body;
        private Rigidbody _vehicleRb;

        // Spontaneous detachment tracking
        private const float DetachChancePerSec = 0.05f;
        private const float DetachSpeedThreshold = 10f;

        public VehiclePartItem InstalledPart => _installedPart;
        public VehiclePartCondition Condition => _condition;
        public bool IsEmpty => _installedPart == null;
        public bool IsOccupied => _installedPart != null;

        public void Init(AttachmentPoint point, VehicleBody body)
        {
            slotId = point.slotId;
            section = point.section;
            attachmentType = point.attachmentType;
            isRequired = point.isRequired;
            _body = body;
            _vehicleRb = body.GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_installedPart == null) return;
            if (_condition.stage != DamageStage.Critical) return;
            if (_vehicleRb == null) return;

            // At Critical + vehicle speed > threshold, chance of spontaneous detachment
            if (_vehicleRb.linearVelocity.magnitude > DetachSpeedThreshold)
            {
                if (Random.value < DetachChancePerSec * Time.deltaTime)
                    Detach();
            }
        }

        /// <summary>Install a part with the given condition. Returns false if slot is occupied.</summary>
        public bool Install(VehiclePartItem part, VehiclePartCondition condition)
        {
            if (_installedPart != null) return false;

            _installedPart = part;
            _condition = condition;
            SpawnVisual();
            return true;
        }

        /// <summary>Remove the part, returning it and its condition. Returns null if empty.</summary>
        public (VehiclePartItem part, VehiclePartCondition condition)? Uninstall()
        {
            if (_installedPart == null) return null;

            var result = (_installedPart, _condition);
            _installedPart = null;
            _condition = default;
            DestroyVisual();
            return result;
        }

        /// <summary>Repair this part by the given HP amount.</summary>
        public void RepairDamage(float amount)
        {
            if (_installedPart == null) return;
            _condition.Repair(amount);
        }

        /// <summary>Apply damage to this part. Auto-detaches at Destroyed stage.</summary>
        public void TakeDamage(float amount)
        {
            if (_installedPart == null) return;

            bool stageChanged = _condition.TakeDamage(amount);
            if (_condition.IsDestroyed)
                Detach();
            else if (stageChanged)
                UpdateVisualDamage();
        }

        /// <summary>Eject the part as a WorldItem into the world.</summary>
        public void Detach()
        {
            if (_installedPart == null) return;

            var part = _installedPart;
            var condition = _condition;

            _installedPart = null;
            _condition = default;
            DestroyVisual();

            // Create WorldItem at slot position
            SpawnDetachedWorldItem(part, condition);

            // Notify body that slot is now empty
            if (_body != null)
                _body.OnSlotEmptied(this);
        }

        private void SpawnDetachedWorldItem(VehiclePartItem part, VehiclePartCondition condition)
        {
            var go = new GameObject("Detached_" + part.displayName);
            go.transform.position = transform.position;
            go.transform.rotation = transform.rotation;

            // Add rigidbody for physics
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = part.weight;

            // Ejection force: outward from vehicle center + up
            if (_vehicleRb != null)
            {
                Vector3 outward = (transform.position - _vehicleRb.transform.position).normalized;
                Vector3 ejection = (outward + Vector3.up * 0.5f).normalized * 6f;
                // Add vehicle velocity so parts fly convincingly
                rb.linearVelocity = _vehicleRb.linearVelocity;
                rb.AddForce(ejection, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
            }

            // Add WorldItem component
            var worldItem = go.AddComponent<WorldItem>();
            worldItem.InitAsVehiclePart(part, condition);

            // Use world model prefab if available, otherwise a placeholder cube
            GameObject model;
            if (part.worldModelPrefab != null)
            {
                model = Instantiate(part.worldModelPrefab, go.transform);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(go.transform, false);
                model.transform.localScale = Vector3.one * 0.3f;
                // Remove default collider from primitive — WorldItem manages its own
                var col = model.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            // Add a collider to root for pickup interaction
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.4f;

            // Auto-destroy after a long time if not picked up
            Destroy(go, 300f);
        }

        private void SpawnVisual()
        {
            DestroyVisual();

            GameObject prefab = _installedPart.attachedModelPrefab ?? _installedPart.modelPrefab;
            if (prefab != null)
            {
                _visualInstance = Instantiate(prefab, transform);
                _visualInstance.transform.localPosition = Vector3.zero;
                _visualInstance.transform.localRotation = Quaternion.identity;
            }
        }

        private void DestroyVisual()
        {
            if (_visualInstance != null)
            {
                Destroy(_visualInstance);
                _visualInstance = null;
            }
        }

        private void UpdateVisualDamage()
        {
            // Future: swap materials or enable damage overlays based on stage
        }
    }
}
