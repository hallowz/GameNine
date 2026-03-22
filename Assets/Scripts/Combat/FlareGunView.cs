using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Combat.Projectiles;
using Voidborne.Player;

namespace Voidborne.Combat
{
    /// <summary>
    /// Procedural first-person flare gun viewmodel.
    ///
    /// Attach to the PlayerCamera GameObject (same as TorchView).
    /// When equipped, builds a held flare gun model. Left-click fires a flare
    /// projectile that emits bright light on impact for ~25 seconds.
    /// </summary>
    public class FlareGunView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController fpc;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private ProjectileDefinition flareProjectileDef;

        [Header("Idle Pose")]
        [SerializeField] private Vector3 idlePosition = new Vector3(0.30f, -0.20f, 0.42f);

        [Header("Sway")]
        [SerializeField] private float swayAmount    = 0.015f;
        [SerializeField] private float swaySmoothing = 8f;

        [Header("Bob")]
        [SerializeField] private float bobAmount = 0.008f;
        [SerializeField] private float bobSpeed  = 9f;

        [Header("Firing")]
        [SerializeField] private float fireCooldown = 3f;
        [SerializeField] private float muzzleFlashDuration = 0.08f;

        [Header("Recoil Kick")]
        [SerializeField] private float recoilKickBack  = 0.04f;
        [SerializeField] private float recoilKickUp    = 0.02f;
        [SerializeField] private float recoilRecovery  = 6f;

        // ── Runtime ────────────────────────────────────────────────────

        private Transform  _holder;
        private GameObject _modelRoot;
        private bool       _equipped;

        private Vector3 _currentSwayOffset;
        private float   _bobTimer;
        private float   _lastFireTime = -999f;
        private Vector3 _recoilOffset;

        private GameObject _muzzleFlashObj;
        private float      _flashTimer;

        // ── Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (fpc == null)
                fpc = GetComponentInParent<FirstPersonController>();
            if (cameraTransform == null)
                cameraTransform = transform;

            var holderGO = new GameObject("FlareGunHolder");
            _holder = holderGO.transform;
            _holder.SetParent(transform, false);
            _holder.localPosition = idlePosition;
            _holder.localRotation = Quaternion.identity;
            _holder.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_equipped || _holder == null) return;

            // Sway + bob + recoil
            Vector3 targetPos = idlePosition + ComputeSway();
            targetPos.y += ComputeBob();

            // Recover recoil offset
            _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, Time.deltaTime * recoilRecovery);
            targetPos += _recoilOffset;

            _holder.localPosition = Vector3.Lerp(
                _holder.localPosition, targetPos, Time.deltaTime * 8f);

            // Muzzle flash timer
            if (_muzzleFlashObj != null)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    Destroy(_muzzleFlashObj);
                    _muzzleFlashObj = null;
                }
            }
        }

        private void Update()
        {
            if (!_equipped) return;

            // Block firing when UI is open
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                TryFire();
        }

        // ── Public API ─────────────────────────────────────────────────

        public bool IsEquipped => _equipped;

        public void Equip()
        {
            if (_equipped) return;
            _equipped = true;

            BuildModel();
            _holder.gameObject.SetActive(true);
        }

        public void Unequip()
        {
            if (!_equipped) return;
            _equipped = false;

            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }
            if (_muzzleFlashObj != null)
            {
                Destroy(_muzzleFlashObj);
                _muzzleFlashObj = null;
            }
            _holder.gameObject.SetActive(false);
        }

        // ── Firing ─────────────────────────────────────────────────────

        private void TryFire()
        {
            if (Time.time < _lastFireTime + fireCooldown) return;
            if (flareProjectileDef == null || ProjectilePool.Instance == null) return;

            _lastFireTime = Time.time;

            // Spawn projectile from camera position + direction
            Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.5f;
            Vector3 dir    = cameraTransform.forward;

            Projectile proj = ProjectilePool.Instance.Spawn(flareProjectileDef, origin, dir, gameObject);
            if (proj != null)
            {
                // Add in-flight light to the projectile
                var flightLight = proj.gameObject.AddComponent<Light>();
                flightLight.type      = LightType.Point;
                flightLight.color     = new Color(1.0f, 0.4f, 0.12f);
                flightLight.intensity = 120f;
                flightLight.range     = 20f;
                flightLight.shadows   = LightShadows.None;

                // Add emissive glow to the projectile model
                var renderers = proj.GetComponentsInChildren<Renderer>();
                foreach (var rend in renderers)
                {
                    rend.material.color = new Color(1.0f, 0.4f, 0.1f);
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", new Color(1.0f, 0.4f, 0.1f) * 4f);
                }

                // Hook embed callback to spawn persistent FlareLight
                proj.OnEmbedded += (hitPoint, hitEnemy) =>
                {
                    FlareLight.Spawn(hitPoint, Vector3.up);
                };
            }

            // Recoil kick
            _recoilOffset = new Vector3(0f, recoilKickUp, -recoilKickBack);

            // Muzzle flash
            ShowMuzzleFlash();
        }

        private void ShowMuzzleFlash()
        {
            if (_muzzleFlashObj != null)
                Destroy(_muzzleFlashObj);

            _muzzleFlashObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _muzzleFlashObj.name = "FlareFlash";
            _muzzleFlashObj.transform.SetParent(_modelRoot.transform, false);
            _muzzleFlashObj.transform.localPosition = new Vector3(0f, 0.04f, 0.22f);
            _muzzleFlashObj.transform.localScale    = new Vector3(0.06f, 0.06f, 0.06f);

            var col = _muzzleFlashObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _muzzleFlashObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(rend.sharedMaterial)
                {
                    color = new Color(1.0f, 0.7f, 0.2f)
                };
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", new Color(1.0f, 0.6f, 0.1f) * 8f);
            }

            _flashTimer = muzzleFlashDuration;
        }

        // ── Procedural model ───────────────────────────────────────────

        private void BuildModel()
        {
            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }

            _modelRoot = new GameObject("FlareGunModel");
            _modelRoot.transform.SetParent(_holder, false);
            _modelRoot.transform.localPosition = Vector3.zero;
            _modelRoot.transform.localRotation = Quaternion.Euler(-5f, 0f, 5f);

            Transform root = _modelRoot.transform;

            // ── Barrel — wide, stubby cylinder ─────────────────────────
            MakePart(PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.02f, 0.10f),
                new Vector3(0.038f, 0.10f, 0.038f),
                Quaternion.Euler(90f, 0f, 0f),
                new Color(0.25f, 0.25f, 0.28f));

            // ── Barrel bore — dark interior ────────────────────────────
            MakePart(PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.02f, 0.20f),
                new Vector3(0.030f, 0.008f, 0.030f),
                Quaternion.Euler(90f, 0f, 0f),
                new Color(0.08f, 0.08f, 0.08f));

            // ── Receiver / frame — blocky body ─────────────────────────
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, -0.005f, 0.01f),
                new Vector3(0.042f, 0.050f, 0.12f),
                Quaternion.identity,
                new Color(0.30f, 0.28f, 0.26f));

            // ── Grip — angled down ─────────────────────────────────────
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, -0.06f, -0.02f),
                new Vector3(0.034f, 0.07f, 0.032f),
                Quaternion.Euler(10f, 0f, 0f),
                new Color(0.22f, 0.16f, 0.10f));

            // ── Grip base ──────────────────────────────────────────────
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, -0.095f, -0.025f),
                new Vector3(0.038f, 0.012f, 0.038f),
                Quaternion.identity,
                new Color(0.18f, 0.14f, 0.08f));

            // ── Trigger guard — thin curved piece ──────────────────────
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, -0.035f, 0.02f),
                new Vector3(0.008f, 0.025f, 0.04f),
                Quaternion.identity,
                new Color(0.20f, 0.20f, 0.22f));

            // ── Hammer / latch at the rear ─────────────────────────────
            MakePart(PrimitiveType.Cube, root,
                new Vector3(0f, 0.03f, -0.04f),
                new Vector3(0.020f, 0.018f, 0.018f),
                Quaternion.identity,
                new Color(0.35f, 0.32f, 0.30f));

            // ── Loaded flare tip — bright red/orange visible in barrel ─
            var flareTip = MakePart(PrimitiveType.Sphere, root,
                new Vector3(0f, 0.02f, 0.195f),
                new Vector3(0.028f, 0.028f, 0.015f),
                Quaternion.identity,
                new Color(0.95f, 0.25f, 0.05f));

            var tipRend = flareTip.GetComponent<Renderer>();
            if (tipRend != null)
            {
                tipRend.material.EnableKeyword("_EMISSION");
                tipRend.material.SetColor("_EmissionColor", new Color(0.9f, 0.2f, 0.05f) * 1.5f);
            }
        }

        private GameObject MakePart(PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Quaternion localRot, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.transform.localRotation = localRot;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material = new Material(rend.sharedMaterial) { color = color };

            return go;
        }

        // ── Sway & Bob ────────────────────────────────────────────────

        private Vector3 ComputeSway()
        {
            if (Mouse.current == null) return Vector3.zero;

            Vector2 delta = Mouse.current.delta.ReadValue();
            float targetX = -delta.x * swayAmount * 0.008f;
            float targetY = -delta.y * swayAmount * 0.008f;

            _currentSwayOffset = Vector3.Lerp(
                _currentSwayOffset,
                new Vector3(targetX, targetY, 0f),
                Time.deltaTime * swaySmoothing);

            return _currentSwayOffset;
        }

        private float ComputeBob()
        {
            if (fpc == null) return 0f;

            float speed = fpc.Velocity.magnitude;
            if (speed < 0.2f)
            {
                _bobTimer = 0f;
                return 0f;
            }

            _bobTimer += Time.deltaTime * bobSpeed * Mathf.Clamp01(speed / 5f);
            return Mathf.Sin(_bobTimer) * bobAmount;
        }
    }
}
