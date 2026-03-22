using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Applies damage to the vehicle and its body parts when colliding with objects at speed.
    /// High-speed crashes can knock off bumpers and doors.
    /// </summary>
    [RequireComponent(typeof(VehicleDamageSystem))]
    public class VehicleCollisionDamage : MonoBehaviour
    {
        [Header("Crash Thresholds")]
        [SerializeField] private float minCrashForce = 5000f;
        [SerializeField] private float damagePerNewton = 0.005f;

        private VehicleDamageSystem _damageSystem;
        private VehicleBody _body;
        private Rigidbody _rb;

        private void Awake()
        {
            _damageSystem = GetComponent<VehicleDamageSystem>();
            _body = GetComponent<VehicleBody>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_damageSystem == null) return;

            float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
            if (impactForce < minCrashForce) return;

            float damage = (impactForce - minCrashForce) * damagePerNewton;

            // Route to zone-based damage system
            Vector3 hitNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.forward;
            _damageSystem.ReceiveDirectionalDamage(damage, hitNormal);

            // Also route to nearest body part slot
            if (_body != null && collision.contacts.Length > 0)
            {
                Vector3 hitPoint = collision.contacts[0].point;
                DamageNearestSlot(hitPoint, damage);
            }
        }

        private void DamageNearestSlot(Vector3 worldPoint, float damage)
        {
            VehicleBodyPartSlot nearest = null;
            float bestDist = float.MaxValue;

            foreach (var slot in _body.AllSlots)
            {
                if (slot.IsEmpty) continue;
                float d = Vector3.SqrMagnitude(slot.transform.position - worldPoint);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = slot;
                }
            }

            if (nearest != null)
                nearest.TakeDamage(damage);
        }
    }
}
