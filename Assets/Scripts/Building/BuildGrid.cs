using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.1 — Virtual building grid singleton.
    ///
    /// Holds the world-space origin used by all block placements. The origin is
    /// "pinned" on the first block placed (player-asserted grid origin), and
    /// every subsequent placement snaps to a 1m³ cell relative to that origin.
    /// World-to-cell and cell-to-world conversions both go through this
    /// component so the grid is the single source of truth.
    /// </summary>
    /// <remarks>
    /// Coop note: in singleplayer the origin is set locally on first placement.
    /// In coop (V21), the server is authoritative for setting the origin — the
    /// first placement RPC carries the origin proposal and the server pins it
    /// for the session, then replicates to clients via a NetworkVariable. The
    /// API here ( <see cref="SetOriginIfUnset"/> ) is idempotent, so client
    /// replays that race the server can call it safely.
    /// </remarks>
    public class BuildGrid : MonoBehaviour
    {
        private static BuildGrid _instance;

        /// <summary>
        /// Resolves (and lazily creates) the active <see cref="BuildGrid"/>.
        /// If no instance exists in the scene a hidden host GameObject is
        /// spawned so tests / runtime code can place blocks before any scene
        /// fixture is wired.
        /// </summary>
        public static BuildGrid Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = FindObjectOfType<BuildGrid>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }
                var go = new GameObject("BuildGrid");
                _instance = go.AddComponent<BuildGrid>();
                return _instance;
            }
        }

        /// <summary>
        /// Reset the singleton reference. EditMode test seam — between tests
        /// each fixture wants a fresh BuildGrid with an unset origin.
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }

        // ---------------------------------------------------------------
        //  Configuration
        // ---------------------------------------------------------------

        [Tooltip("World-space size of one grid cell. Default 1m³ to match the V3.3 placedPrefab footprint.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("True once the first block has been placed (origin pinned). Save/load reads this.")]
        [SerializeField] private bool originSet;

        [Tooltip("World-space origin pinned by the first block placement. Snaps to the nearest cell-aligned point.")]
        [SerializeField] private Vector3 origin;

        /// <summary>World-space size of one grid cell.</summary>
        public float CellSize => cellSize;

        /// <summary>True once <see cref="SetOriginIfUnset"/> has been called.</summary>
        public bool IsOriginSet => originSet;

        /// <summary>The pinned origin. Until <see cref="IsOriginSet"/> is true this is <see cref="Vector3.zero"/>.</summary>
        public Vector3 Origin => origin;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---------------------------------------------------------------
        //  Origin management
        // ---------------------------------------------------------------

        /// <summary>
        /// Pin the grid origin to a cell-aligned point near
        /// <paramref name="firstBlockPos"/>. Idempotent — subsequent calls
        /// are ignored so the origin survives session-long invariants.
        /// </summary>
        /// <param name="firstBlockPos">World-space position of the first block
        /// (typically the raycast hit point at placement time). The actual
        /// origin is snapped to the nearest cellSize-aligned position so the
        /// grid is always axis-aligned with the world.</param>
        public void SetOriginIfUnset(Vector3 firstBlockPos)
        {
            if (originSet) return;
            originSet = true;
            origin = SnapToCellAligned(firstBlockPos);
        }

        /// <summary>
        /// Explicit reset — clears the pinned origin. Used by save/load when
        /// reloading into a fresh world, and by EditMode tests between cases.
        /// </summary>
        public void ClearOrigin()
        {
            originSet = false;
            origin = Vector3.zero;
        }

        // ---------------------------------------------------------------
        //  Coordinate conversion
        // ---------------------------------------------------------------

        /// <summary>
        /// Convert a world-space position to integer cell coordinates relative
        /// to the pinned origin. Safe to call before the origin is pinned —
        /// in that case the world origin (0,0,0) is used as a placeholder, and
        /// the cell index is computed against it. The next call to
        /// <see cref="SetOriginIfUnset"/> will pin the origin.
        /// </summary>
        public Vector3Int WorldToCell(Vector3 worldPos)
        {
            Vector3 rel = worldPos - origin;
            float cs = cellSize > 0f ? cellSize : 1f;
            return new Vector3Int(
                Mathf.FloorToInt(rel.x / cs),
                Mathf.FloorToInt(rel.y / cs),
                Mathf.FloorToInt(rel.z / cs));
        }

        /// <summary>
        /// Convert integer cell coordinates back to a world-space position at
        /// the cell's centre. Cell (0,0,0)'s centre is offset from the origin
        /// by +cellSize/2 on every axis, so blocks visually sit ON the grid
        /// rather than at its corner.
        /// </summary>
        public Vector3 CellToWorld(Vector3Int cell)
        {
            float cs = cellSize > 0f ? cellSize : 1f;
            return origin + new Vector3(
                (cell.x + 0.5f) * cs,
                (cell.y + 0.5f) * cs,
                (cell.z + 0.5f) * cs);
        }

        /// <summary>
        /// Convenience: returns the world-space corner (min) of the cell, NOT
        /// the centre. Forms that snap to an edge or half-height use this to
        /// compute their offset.
        /// </summary>
        public Vector3 CellCornerToWorld(Vector3Int cell)
        {
            float cs = cellSize > 0f ? cellSize : 1f;
            return origin + new Vector3(
                cell.x * cs,
                cell.y * cs,
                cell.z * cs);
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private Vector3 SnapToCellAligned(Vector3 worldPos)
        {
            float cs = cellSize > 0f ? cellSize : 1f;
            return new Vector3(
                Mathf.Floor(worldPos.x / cs) * cs,
                Mathf.Floor(worldPos.y / cs) * cs,
                Mathf.Floor(worldPos.z / cs) * cs);
        }
    }
}
