using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

/// <summary>
/// Manages the pocket dimension system for Index Module 5 — The Fold.
///
/// Each pocket dimension is an isolated zone placed at a far Y-offset
/// (y = ZoneBaseY + id * ZoneSpacing) so it is always loaded in the same scene
/// without interfering with the main world. The player is teleported in/out.
///
/// HOME dimension (index 0) is pre-created and populated by HOMEZone.
/// Player-created dimensions are blank void spaces whose size scales with the
/// power invested at creation time.
///
/// Power cost for The Fold:
///   • Entering an existing dimension : FoldPowerCostExisting watts drawn immediately.
///   • Creating a new dimension       : FoldPowerCostCreate drawn from nearest
///     connected BatteryBank over FoldCreateDrawTime seconds.
///
/// Portal VFX: a vertical tear (scale x=0.2, y=2, z=1) spawned at the targeted surface.
/// </summary>
[RequireComponent(typeof(IndexDevice))]
public class IndexDevice_PocketDimension : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------

    public const int   HomeId            = 0;
    public const float ZoneBaseY         = 10_000f;
    public const float ZoneSpacing       = 500f;
    public const float PortalOpenTime    = 8f;    // seconds portal stays open
    public const float PortalRange       = 6f;    // max distance from player to use portal
    public const float FoldPowerCostExisting = 200f;
    public const float FoldPowerCostCreate   = 800f;
    public const float FoldCreateDrawTime    = 10f;
    public const float PowerScanRadius       = 40f;
    public const float DefaultBlankSize      = 32f;

    // ---------------------------------------------------------------
    //  Data types
    // ---------------------------------------------------------------

    [Serializable]
    public class PocketDimension
    {
        public int    id;
        public string dimensionName;
        public float  size;           // side length of the blank void cube in metres
        public bool   hasBeenCreated;
        public bool   isHome;
    }

    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Header("Portal")]
    [Tooltip("Prefab for the vertical-tear portal object. If null, a primitive is spawned.")]
    [SerializeField] private GameObject portalPrefab;

    [Header("Dimensions")]
    [SerializeField] private List<PocketDimension> dimensions = new();

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------

    private IndexDevice _device;
    private GameObject   _activePortal;
    private bool         _portalOpen;
    private Vector3      _portalWorldPos;
    private Vector3      _returnPosition;   // where the player stood before entering
    private bool         _inDimension;
    private int          _currentDimId = -1;
    private int          _selectedDimId = HomeId;
    private Coroutine    _portalRoutine;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        _device = GetComponent<IndexDevice>();

        // Ensure HOME dimension exists.
        if (dimensions.Count == 0 || !dimensions.Exists(d => d.isHome))
        {
            dimensions.Insert(0, new PocketDimension
            {
                id             = HomeId,
                dimensionName  = "HOME",
                size           = 64f,
                hasBeenCreated = true,
                isHome         = true
            });
        }
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>Called by IndexAbilities when the player presses Q with Module 5 active.</summary>
    public void TryOpenPortal()
    {
        if (!_device.IsModuleInstalled(5)) return;

        if (_inDimension)
        {
            // Already inside — open an exit portal back to the world.
            ExitDimension();
            return;
        }

        if (_portalOpen)
        {
            // Portal already open — cancel it.
            CancelPortal();
            return;
        }

        // Raycast to find a valid surface.
        Ray   ray = Camera.main != null
            ? Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(transform.position + Vector3.up, transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            Debug.Log("[Fold] No surface in range.");
            return;
        }

        // Check power cost for entering existing dimension.
        PocketDimension dim = GetDimension(_selectedDimId);
        if (dim == null) { Debug.LogWarning("[Fold] Selected dimension not found."); return; }

        if (dim.hasBeenCreated)
        {
            if (!TryDrawPower(FoldPowerCostExisting))
            {
                Debug.Log("[Fold] Insufficient power to open portal.");
                return;
            }
            SpawnPortal(hit.point, hit.normal, dim);
        }
        else
        {
            // Create new dimension — larger power draw over 10s.
            StartCoroutine(CreateDimensionRoutine(dim, hit.point, hit.normal));
        }
    }

    /// <summary>Select which dimension to open to (called from UI or console).</summary>
    public void SelectDimension(int dimId)
    {
        if (dimensions.Exists(d => d.id == dimId))
            _selectedDimId = dimId;
    }

    /// <summary>Register a new blank dimension. Called when the player creates one via the Fold.</summary>
    public PocketDimension CreateNewDimension(string name, float size)
    {
        int newId = dimensions.Count;
        var dim = new PocketDimension
        {
            id             = newId,
            dimensionName  = name,
            size           = size,
            hasBeenCreated = true,
            isHome         = false
        };
        dimensions.Add(dim);
        return dim;
    }

    public IReadOnlyList<PocketDimension> Dimensions => dimensions;
    public bool IsInsideDimension => _inDimension;
    public int  CurrentDimensionId => _currentDimId;

    // ---------------------------------------------------------------
    //  Portal logic
    // ---------------------------------------------------------------

    private void SpawnPortal(Vector3 surfacePoint, Vector3 surfaceNormal, PocketDimension dim)
    {
        _portalWorldPos = surfacePoint + surfaceNormal * 0.05f;
        Quaternion rot  = Quaternion.LookRotation(-surfaceNormal, Vector3.up);

        if (portalPrefab != null)
        {
            _activePortal = Instantiate(portalPrefab, _portalWorldPos, rot);
        }
        else
        {
            // Fallback: a thin vertical quad acting as the tear.
            _activePortal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _activePortal.transform.SetPositionAndRotation(_portalWorldPos, rot);
            _activePortal.transform.localScale = new Vector3(0.15f, 2f, 1f);

            var r = _activePortal.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = new Material(Shader.Find("Standard"));
                r.material.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            }

            // Remove the box collider so it doesn't block movement.
            Destroy(_activePortal.GetComponent<Collider>());

            // Add trigger collider for player detection.
            var trig = _activePortal.AddComponent<BoxCollider>();
            trig.isTrigger = true;
        }

        // Attach portal trigger listener.
        var handler = _activePortal.AddComponent<PortalTriggerHandler>();
        handler.Initialize(this, dim.id);

        _portalOpen = true;

        if (_portalRoutine != null) StopCoroutine(_portalRoutine);
        _portalRoutine = StartCoroutine(AutoClosePortal());

        Debug.Log($"[Fold] Portal to '{dim.dimensionName}' opened.");
    }

    private IEnumerator AutoClosePortal()
    {
        yield return new WaitForSeconds(PortalOpenTime);
        CancelPortal();
    }

    private void CancelPortal()
    {
        if (_activePortal != null) Destroy(_activePortal);
        _portalOpen   = false;
        _portalRoutine = null;
    }

    // ---------------------------------------------------------------
    //  Dimension travel
    // ---------------------------------------------------------------

    /// <summary>Called by PortalTriggerHandler when the player walks through.</summary>
    public void EnterDimension(int dimId)
    {
        PocketDimension dim = GetDimension(dimId);
        if (dim == null) return;

        _returnPosition = transform.position;
        _inDimension    = true;
        _currentDimId   = dimId;

        // Teleport player to the dimension zone.
        Vector3 dimCenter = new Vector3(0f, ZoneBaseY + dimId * ZoneSpacing, 0f);
        transform.position = dimCenter + Vector3.up * 2f;

        CancelPortal();

        // Find and activate the zone component.
        PocketDimensionZone zone = FindZone(dimId);
        zone?.OnPlayerEnter();

        Debug.Log($"[Fold] Entered dimension '{dim.dimensionName}'.");
    }

    /// <summary>Open an exit portal inside the dimension, then teleport back.</summary>
    public void ExitDimension()
    {
        if (!_inDimension) return;

        _inDimension    = false;
        _currentDimId   = -1;

        PocketDimensionZone zone = FindZone(_currentDimId);
        zone?.OnPlayerExit();

        transform.position = _returnPosition;
        Debug.Log("[Fold] Exited dimension, returned to world.");
    }

    // ---------------------------------------------------------------
    //  New dimension creation
    // ---------------------------------------------------------------

    private IEnumerator CreateDimensionRoutine(PocketDimension dim, Vector3 surfacePoint, Vector3 surfaceNormal)
    {
        Debug.Log("[Fold] Draining power to create new dimension...");

        float remaining = FoldPowerCostCreate;
        float elapsed   = 0f;

        while (elapsed < FoldCreateDrawTime && remaining > 0f)
        {
            float draw = (FoldPowerCostCreate / FoldCreateDrawTime) * Time.deltaTime;
            if (TryDrawPower(draw))
                remaining -= draw;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (remaining > FoldPowerCostCreate * 0.5f)
        {
            Debug.Log("[Fold] Not enough power — dimension creation cancelled.");
            yield break;
        }

        dim.hasBeenCreated = true;
        dim.size           = DefaultBlankSize + (FoldPowerCostCreate - remaining) * 0.05f;

        Debug.Log($"[Fold] Dimension '{dim.dimensionName}' created (size {dim.size:F0}m).");
        SpawnPortal(surfacePoint, surfaceNormal, dim);
    }

    // ---------------------------------------------------------------
    //  Power helpers
    // ---------------------------------------------------------------

    private bool TryDrawPower(float amount)
    {
        // Scan for BatteryBank components within range and drain from the first viable one.
        Collider[] nearby = Physics.OverlapSphere(transform.position, PowerScanRadius);
        foreach (Collider col in nearby)
        {
            var battery = col.GetComponent<BatteryBank>();
            if (battery == null) continue;
            if (battery.StoredEnergy >= amount)
            {
                battery.Discharge(amount);
                return true;
            }
        }
        // If no power available, allow it anyway (device has minimal self-power).
        // Gameplay feedback (HUD warning) is handled by IndexHUD.
        return true;
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private PocketDimension GetDimension(int id) =>
        dimensions.Find(d => d.id == id);

    private PocketDimensionZone FindZone(int dimId)
    {
        var allZones = FindObjectsByType<PocketDimensionZone>(FindObjectsSortMode.None);
        foreach (var z in allZones)
            if (z.DimensionId == dimId) return z;
        return null;
    }
}

/// <summary>
/// Lightweight trigger component attached to the spawned portal object.
/// Detects when the player enters and notifies IndexDevice_PocketDimension.
/// </summary>
public class PortalTriggerHandler : MonoBehaviour
{
    private IndexDevice_PocketDimension _pocket;
    private int _dimId;
    private bool _used;

    public void Initialize(IndexDevice_PocketDimension pocket, int dimId)
    {
        _pocket = pocket;
        _dimId  = dimId;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag("Player")) return;

        _used = true;
        _pocket.EnterDimension(_dimId);
    }
}
