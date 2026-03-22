using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple free-placement controller for PlaceableItem objects (vehicles, workbenches, etc.).
/// Shows a semi-transparent ghost preview; left-click to place, right-click to rotate 90°.
/// Consumes one item from inventory on placement.
///
/// Spawned objects are placed above the ground so wheels/colliders settle properly.
/// </summary>
public class PlaceablePlacementController : MonoBehaviour
{
    [Header("Raycast")]
    public Camera    playerCamera;
    public LayerMask placementMask = ~0;
    public float     maxPlacementDistance = 12f;

    [Header("Spawn")]
    [Tooltip("Height above the raycast hit point to spawn the object. Prevents ground clipping.")]
    public float spawnHeightOffset = 1.0f;

    [Header("Ghost Appearance")]
    public Color validColor   = new Color(0.2f, 0.9f, 0.2f, 0.35f);
    public Color invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.35f);

    // ── Runtime ──────────────────────────────────────────────────────────

    private PlaceableItem   _activeItem;
    private GameObject      _ghost;
    private Material        _ghostMat;
    private int             _rotStep;
    private bool            _isActive;
    private PlayerInventory _inventory;

    private void Awake()
    {
        _inventory = FindFirstObjectByType<PlayerInventory>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void Activate(PlaceableItem item)
    {
        if (_isActive && _activeItem == item) return;
        Deactivate();

        _activeItem = item;
        _isActive   = true;
        _rotStep    = 0;

        // Build ghost from prefab.
        // Temporarily deactivate the prefab so Awake() does NOT run during
        // Instantiate — we need to strip scripts before they initialise.
        bool wasActive = item.prefabToPlace.activeSelf;
        item.prefabToPlace.SetActive(false);

        _ghost = Instantiate(item.prefabToPlace);
        _ghost.name = "PlaceableGhost";

        item.prefabToPlace.SetActive(wasActive);

        // Strip ALL physics and logic while the ghost is still inactive.
        foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true))
            DestroyImmediate(mb);
        foreach (var rb in _ghost.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(rb);
        foreach (var wc in _ghost.GetComponentsInChildren<WheelCollider>(true))
            DestroyImmediate(wc);
        foreach (var col in _ghost.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(col);

        // Now activate the ghost — it's purely visual, no scripts/physics remain.
        _ghost.SetActive(true);

        // Put ghost on IgnoreRaycast so placement rays pass through it
        SetLayerRecursive(_ghost, LayerMask.NameToLayer("Ignore Raycast"));

        // Create shared ghost material
        _ghostMat = new Material(Shader.Find("Sprites/Default"));
        _ghostMat.color = validColor;

        foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = _ghostMat;
            r.materials = mats;
        }
    }

    public void Deactivate()
    {
        if (_ghost != null)
        {
            Destroy(_ghost);
            _ghost = null;
        }
        if (_ghostMat != null)
        {
            Destroy(_ghostMat);
            _ghostMat = null;
        }
        _isActive   = false;
        _activeItem = null;
    }

    // ── Update ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive || _ghost == null) return;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

        if (playerCamera == null)
            playerCamera = Camera.main;

        bool valid = UpdateGhostPosition();
        if (_ghostMat != null)
            _ghostMat.color = valid ? validColor : invalidColor;

        // Rotate
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            _rotStep = (_rotStep + 1) % 4;
        }

        // Place
        if (valid && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Place();
        }
    }

    private bool UpdateGhostPosition()
    {
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask))
        {
            _ghost.SetActive(false);
            return false;
        }

        _ghost.SetActive(true);
        _ghost.transform.position = hit.point + Vector3.up * spawnHeightOffset;
        _ghost.transform.rotation = Quaternion.Euler(0f, _rotStep * 90f, 0f);
        return true;
    }

    private void Place()
    {
        Vector3    pos = _ghost.transform.position;
        Quaternion rot = _ghost.transform.rotation;

        // Spawn the real object above the ground
        GameObject placed = Instantiate(_activeItem.prefabToPlace, pos, rot);

        // Zero out initial velocity so the vehicle doesn't launch from spawn forces
        var rb = placed.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Consume one item from inventory
        _inventory?.RemoveItem(_activeItem.itemId, 1);

        // If no more of this item, deactivate
        if (_inventory != null && _inventory.CountAllItem(_activeItem.itemId) <= 0)
            Deactivate();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
