using System.Collections.Generic;
using UnityEngine;
using Voidborne.Combat;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// MonoBehaviour on a vehicle in the world. Holds the frame reference and all installed components.
    /// Routes damage to VehicleDamageSystem and exposes component queries to other vehicle systems.
    /// </summary>
    public class AssembledVehicle : MonoBehaviour, IDamageable
    {
        [Header("Frame")]
        [SerializeField] private VehicleFrame _frame;

        [Header("Default Components (auto-installed on spawn)")]
        [SerializeField] private List<VehicleComponent> _defaultComponents = new List<VehicleComponent>();

        // Installed components keyed by attachment type (multiple per type is valid, e.g. 4 suspension wheels)
        private readonly Dictionary<AttachmentType, List<VehicleComponent>> _installed
            = new Dictionary<AttachmentType, List<VehicleComponent>>();

        private static readonly List<VehicleComponent> EmptyComponentList = new List<VehicleComponent>();

        private VehicleDamageSystem _damageSystem;
        private VehicleBase _vehicleBase;
        private VehicleBody _body;
        private bool _isDead;

        public VehicleFrame Frame => _frame;
        public VehicleBody Body => _body;

        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Ensures default components are installed. Safe to call multiple times.
        /// Called from Awake and from VehicleBase.Awake to handle component ordering.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _damageSystem = GetComponent<VehicleDamageSystem>();
            _vehicleBase = GetComponent<VehicleBase>();
            _body = GetComponent<VehicleBody>();

            // Auto-install default components from serialized list
            foreach (var comp in _defaultComponents)
                if (comp != null) InstallComponent(comp);

            if (_body != null && _frame != null)
                _body.Initialize(_frame);
        }

        // ─── Component API ──────────────────────────────────────────────

        public void SetFrame(VehicleFrame frame) => _frame = frame;

        public void InstallComponent(VehicleComponent comp)
        {
            if (!_installed.TryGetValue(comp.attachmentType, out var list))
            {
                list = new List<VehicleComponent>();
                _installed[comp.attachmentType] = list;
            }
            list.Add(comp);
        }

        public bool HasComponent(AttachmentType type)
            => _installed.ContainsKey(type) && _installed[type].Count > 0;

        public T GetFirstInstalledComponent<T>(AttachmentType type) where T : VehicleComponent
        {
            if (!_installed.TryGetValue(type, out var list)) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is T t) return t;
            return null;
        }

        public List<VehicleComponent> GetInstalledComponents(AttachmentType type)
        {
            if (_installed.TryGetValue(type, out var list)) return list;
            return EmptyComponentList;
        }

        /// <summary>Total weight: frame base + all components.</summary>
        public float TotalWeight
        {
            get
            {
                float w = _frame != null ? _frame.baseWeight : 0f;
                foreach (var kvp in _installed)
                    foreach (var c in kvp.Value)
                        w += c.TotalWeight;
                return w;
            }
        }

        // ─── IDamageable ────────────────────────────────────────────────

        public void TakeDamage(DamageInfo info)
        {
            if (_isDead || _damageSystem == null) return;

            float amount = info.Amount;

            // Armor reduces incoming damage
            if (HasComponent(AttachmentType.Armor))
            {
                var armor = GetFirstInstalledComponent<ArmorComponent>(AttachmentType.Armor);
                if (armor != null)
                    amount = Mathf.Max(0f, amount - armor.damageResistancePerHit);
            }

            // Front hits check for glazing
            float frontDot = Vector3.Dot(transform.forward, -info.HitNormal);
            if (frontDot > 0.5f && HasComponent(AttachmentType.Glazing))
            {
                var glazing = GetFirstInstalledComponent<GlazingComponent>(AttachmentType.Glazing);
                if (glazing != null && !glazing.isMesh)
                {
                    _damageSystem.ApplyDamageToZone(DamageZone.FrontGlazing, amount);
                    return;
                }
            }

            _damageSystem.ReceiveDirectionalDamage(amount, info.HitNormal);

            // Check total destruction
            bool allDestroyed = true;
            foreach (var z in _damageSystem.AllZones)
                if (!z.IsDestroyed) { allDestroyed = false; break; }
            if (allDestroyed) SpawnSalvageAndDestroy();
        }

        // ─── Destruction ────────────────────────────────────────────────

        private void SpawnSalvageAndDestroy()
        {
            if (_isDead) return;
            _isDead = true;

            // New body system: detach all parts as proper WorldItems
            if (_body != null)
            {
                _body.DetachAll();
            }
            else
            {
                // Legacy fallback: spawn marker cubes
                foreach (var kvp in _installed)
                {
                    foreach (var comp in kvp.Value)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.transform.position = transform.position + Random.insideUnitSphere * 1.5f;
                        go.transform.localScale = Vector3.one * 0.25f;
                        go.name = "Salvage_" + comp.componentName;
                        var rb = go.AddComponent<Rigidbody>();
                        rb.AddForce(Random.insideUnitSphere * 4f, ForceMode.Impulse);
                    }
                }
            }

            Destroy(gameObject, 0.1f);
        }
    }
}
