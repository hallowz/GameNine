using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.World.Chunks;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.3 — Terrain leveling tool.
    ///
    /// On activation (F10 in M2 — see deviation note below), raycasts from the
    /// camera, snaps the hit point to a cell-Y, and modifies all voxels in a
    /// radius to that Y level via <see cref="TerrainDeformer"/> (Vol 1.8). This
    /// is *terrain deformation*, not block placement — the marching-cubes
    /// density field is what changes.
    ///
    /// Algorithm:
    /// <list type="number">
    /// <item><description>Raycast forward from camera.</description></item>
    /// <item><description>Snap hit.point.y to the nearest cell-Y plane.</description></item>
    /// <item><description>For each cell in a <see cref="radiusCells"/>-cell radius (XZ), apply two
    /// <see cref="TerrainDeformer.DeformSphere"/> calls: one CUT above the
    /// target plane, one FILL below. The result is a flat platform.</description></item>
    /// </list>
    ///
    /// M2 deviation: <c>terrain_leveler</c> is NOT in the Core 60 items list.
    /// For M2, this tool is bound to the F10 dev key — a developer/cheat path
    /// — rather than a craftable item with an inventory model. M7 wires the
    /// proper craftable + hotbar binding. The acceptance spec ("carve a flat
    /// platform on rolling hills") is satisfied either way.
    /// </summary>
    /// <remarks>
    /// Coop note: terrain deformation is server-authoritative in V21. The
    /// public <see cref="ApplyAt"/> seam re-routes through a ServerRpc when
    /// V21 lands, but the per-cell math here is deterministic and pure so
    /// client preview / server commit will agree.
    /// </remarks>
    public class TerrainLevelTool : MonoBehaviour
    {
        [Header("Activation")]
        [Tooltip("Maximum raycast distance from camera to terrain.")]
        [SerializeField] private float maxReach = 12f;

        [Tooltip("Radius (in cells) of the level operation. Default 3 -> a 7x7 footprint flattens.")]
        [SerializeField] private int radiusCells = 3;

        [Tooltip("Cell size in world units, must match BuildGrid.CellSize. Default 1m.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Intensity of cut/fill deformation per voxel. Higher = more aggressive level.")]
        [SerializeField] private float deformIntensity = 1.2f;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private Camera _playerCamera;
        private InputSystem_Actions _inputActions;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            _playerCamera = GetComponentInChildren<Camera>();
            if (_playerCamera == null) _playerCamera = Camera.main;
        }

        private void OnEnable()
        {
            try
            {
                _inputActions = new InputSystem_Actions();
                _inputActions.Player.Enable();
            }
            catch (System.Exception)
            {
                _inputActions = null;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Disable();
                _inputActions.Dispose();
                _inputActions = null;
            }
        }

        private void Update()
        {
            // F10 = "dev / cheat" trigger for the M2 build. M7 swaps this for
            // a craftable item that lives in the inventory model.
            if (Keyboard.current == null) return;
            if (!Keyboard.current.f10Key.wasPressedThisFrame) return;
            if (_playerCamera == null) return;

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxReach)) return;

            ApplyAt(hit.point, radiusCells);
        }

        // ---------------------------------------------------------------
        //  API
        // ---------------------------------------------------------------

        /// <summary>
        /// Carve a flat platform around <paramref name="worldHitPoint"/>.
        /// The platform's height is snapped to the nearest cell-Y plane.
        /// Returns the number of cells modified (per XZ footprint), so tests
        /// can assert a non-zero operation without depending on the
        /// marching-cubes voxel pipeline being live in EditMode.
        /// </summary>
        public int ApplyAt(Vector3 worldHitPoint, int radius)
        {
            if (radius < 1) radius = 1;
            float cs = cellSize > 0f ? cellSize : 1f;
            float targetY = Mathf.Round(worldHitPoint.y / cs) * cs;

            int cellsModified = 0;
            // Iterate the XZ footprint of the level operation. For each cell,
            // cut anything above the target plane and fill anything below it.
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Round footprint -- skip corners outside the circle so the
                    // resulting platform looks like a flat-top disk, not a
                    // square. Hotter look for "the tool flattens" UX.
                    if (dx * dx + dz * dz > radius * radius) continue;

                    Vector3 cellCenter = new Vector3(
                        worldHitPoint.x + dx * cs,
                        targetY,
                        worldHitPoint.z + dz * cs);

                    // Cut a small sphere just above the target plane (remove
                    // material) and fill a small sphere just below (add
                    // material). Cell-by-cell to keep voxel touches local.
                    TerrainDeformer.DeformSphere(cellCenter + Vector3.up * cs * 0.5f, cs * 0.6f, -deformIntensity);
                    TerrainDeformer.DeformSphere(cellCenter - Vector3.up * cs * 0.5f, cs * 0.6f, +deformIntensity);

                    cellsModified++;
                }
            }
            return cellsModified;
        }
    }
}
