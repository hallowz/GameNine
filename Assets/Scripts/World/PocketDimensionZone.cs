using UnityEngine;

/// <summary>
/// Represents the physical space a pocket dimension occupies.
/// Placed at Y = ZoneBaseY + id * ZoneSpacing in the main scene.
///
/// All standard game systems work inside a zone (gravity, crafting, building, etc.)
/// Items and machines placed here persist between visits.
///
/// The HOME zone is a special, hand-authored PocketDimensionZone whose
/// interactive elements are managed by HOMEZone.cs.
/// </summary>
public class PocketDimensionZone : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Header("Zone Identity")]
    [SerializeField] private int    dimensionId   = -1;
    [SerializeField] private string dimensionName = "Unnamed Dimension";
    [SerializeField] private float  zoneSize      = 32f;   // side length metres

    [Header("Boundaries")]
    [Tooltip("If true, a boundary barrier prevents the player falling out of the void.")]
    [SerializeField] private bool hasBoundaryWalls = true;

    // ---------------------------------------------------------------
    //  Properties
    // ---------------------------------------------------------------

    public int    DimensionId   => dimensionId;
    public string DimensionName => dimensionName;
    public float  ZoneSize      => zoneSize;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Start()
    {
        if (hasBoundaryWalls)
            BuildBoundaryWalls();
    }

    // ---------------------------------------------------------------
    //  Player callbacks
    // ---------------------------------------------------------------

    /// <summary>Called by CortexDevice_PocketDimension when the player enters.</summary>
    public virtual void OnPlayerEnter()
    {
        Debug.Log($"[PocketDimensionZone] Player entered '{dimensionName}'.");
    }

    /// <summary>Called when the player exits back to the world.</summary>
    public virtual void OnPlayerExit()
    {
        Debug.Log($"[PocketDimensionZone] Player exited '{dimensionName}'.");
    }

    // ---------------------------------------------------------------
    //  Boundary walls
    // ---------------------------------------------------------------

    /// <summary>Spawns 6 invisible collider walls forming a sealed box around the zone.</summary>
    private void BuildBoundaryWalls()
    {
        float h = zoneSize;
        float t = 1f;  // wall thickness

        CreateWall("WallFloor",  new Vector3(0f,   -t * 0.5f, 0f),  new Vector3(h, t, h));
        CreateWall("WallCeil",   new Vector3(0f,  h + t * 0.5f, 0f), new Vector3(h, t, h));
        CreateWall("WallNorth",  new Vector3(0f, h * 0.5f,  h * 0.5f + t * 0.5f), new Vector3(h, h, t));
        CreateWall("WallSouth",  new Vector3(0f, h * 0.5f, -h * 0.5f - t * 0.5f), new Vector3(h, h, t));
        CreateWall("WallEast",   new Vector3( h * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, h));
        CreateWall("WallWest",   new Vector3(-h * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, h));
    }

    private void CreateWall(string wallName, Vector3 localPos, Vector3 size)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var col = go.AddComponent<BoxCollider>();
        col.size = size;
    }
}
