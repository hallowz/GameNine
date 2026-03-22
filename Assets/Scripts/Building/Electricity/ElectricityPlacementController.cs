using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Activated by ElectricityItemHandler when the player selects an ElectricityItem.
    /// Shows a semi-transparent ghost preview of the device.
    /// Left-click places (consumes 1 from hotbar). Right-click rotates 90°.
    /// </summary>
    public class ElectricityPlacementController : MonoBehaviour
    {
        [Header("Placement")]
        public Camera    playerCamera;
        public LayerMask placementMask = ~0;
        public float     maxPlacementDistance = 12f;

        [Header("Ghost Materials")]
        public Material ghostValidMaterial;
        public Material ghostInvalidMaterial;

        // ── Runtime ────────────────────────────────────────────────────────

        private ElectricityItem _activeItem;
        private GameObject      _ghost;
        private Renderer[]      _ghostRenderers;
        private int             _rotStep;
        private bool            _isActive;

        private PlayerInventory _inventory;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            _inventory = GetComponentInParent<PlayerInventory>() ?? FindObjectOfType<PlayerInventory>();
            EnsureMaterials();
        }

        private void Update()
        {
            if (!_isActive || _activeItem == null) return;
            UpdateGhost();
            HandleInput();
        }

        private void OnDisable()
        {
            DestroyGhost();
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void Activate(ElectricityItem item)
        {
            _activeItem = item;
            _rotStep    = 0;
            _isActive   = true;
            SpawnGhost(item);
        }

        public void Deactivate()
        {
            _isActive   = false;
            _activeItem = null;
            DestroyGhost();
        }

        // ── Ghost ──────────────────────────────────────────────────────────

        private void SpawnGhost(ElectricityItem item)
        {
            DestroyGhost();

            if (item.devicePrefab == null)
            {
                Debug.LogWarning($"[ElectricityPlacementController] '{item.displayName}' has no devicePrefab assigned — cannot show ghost or place.");
                return;
            }
            _ghost = Instantiate(item.devicePrefab);

            // Strip colliders from ghost so it doesn't block raycasts.
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                Destroy(col);

            // Strip any PowerNode behaviour so it doesn't auto-register.
            foreach (var node in _ghost.GetComponentsInChildren<PowerNode>())
                Destroy(node);

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
            ApplyGhostMaterial(ghostValidMaterial);
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost          = null;
            _ghostRenderers = null;
        }

        private void ApplyGhostMaterial(Material mat)
        {
            if (_ghostRenderers == null || mat == null) return;
            foreach (var r in _ghostRenderers) r.sharedMaterial = mat;
        }

        // ── Update loop ────────────────────────────────────────────────────

        private void UpdateGhost()
        {
            if (_ghost == null || playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask))
            {
                _ghost.SetActive(false);
                return;
            }

            _ghost.SetActive(true);
            Vector3    pos = hit.point;
            Quaternion rot = Quaternion.Euler(0f, _rotStep * 90f, 0f);
            _ghost.transform.SetPositionAndRotation(pos, rot);

            bool canPlace = HasOneInHotbar() && !IsOverlapping(pos);
            ApplyGhostMaterial(canPlace ? ghostValidMaterial : ghostInvalidMaterial);
        }

        private void HandleInput()
        {
            var mouse    = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                TryPlace();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                _rotStep = (_rotStep + 1) % 4;
        }

        private void TryPlace()
        {
            if (_activeItem == null || playerCamera == null || !HasOneInHotbar()) return;

            Ray ray = playerCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask)) return;

            Vector3    pos = hit.point;
            Quaternion rot = Quaternion.Euler(0f, _rotStep * 90f, 0f);

            if (IsOverlapping(pos)) return;

            // Spawn real device.
            GameObject prefab = _activeItem.devicePrefab;
            if (prefab == null) return;

            Instantiate(prefab, pos, rot);

            // Consume one from inventory.
            if (_inventory != null)
                _inventory.RemoveItem(_activeItem.itemId, 1);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static Vector3 SnapToGrid(Vector3 world, float snap)
        {
            return new Vector3(
                Mathf.Round(world.x / snap) * snap,
                Mathf.Round(world.y / snap) * snap,
                Mathf.Round(world.z / snap) * snap);
        }

        private bool HasOneInHotbar()
        {
            if (_inventory == null || _activeItem == null) return true;
            return _inventory.CountAllItem(_activeItem.itemId) > 0;
        }

        private static bool IsOverlapping(Vector3 pos)
        {
            // Use a small overlap sphere to detect existing PowerNode devices.
            var cols = Physics.OverlapSphere(pos, 0.4f);
            foreach (var c in cols)
                if (c.GetComponentInParent<PowerNode>() != null)
                    return true;
            return false;
        }

        // ── Material setup ─────────────────────────────────────────────────

        private void EnsureMaterials()
        {
            if (ghostValidMaterial == null)
                ghostValidMaterial = MakeTransparent(new Color(0f, 0.8f, 1f, 0.35f));
            if (ghostInvalidMaterial == null)
                ghostInvalidMaterial = MakeTransparent(new Color(1f, 0.2f, 0.2f, 0.35f));
        }

        private static Material MakeTransparent(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh ?? Shader.Find("Diffuse"));

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
            return mat;
        }
    }
}
