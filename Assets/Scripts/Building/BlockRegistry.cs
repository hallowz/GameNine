using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.1 — Per-cell lookup for placed blocks.
    ///
    /// Stores a <see cref="PlacedBlock"/> at every occupied <see cref="Vector3Int"/>
    /// cell. Queries like "is this cell occupied" and "what's adjacent to this
    /// cell" are O(1) here, which keeps the per-frame ghost preview lookup
    /// cheap even with hundreds of placed blocks in view.
    /// </summary>
    /// <remarks>
    /// Coop note: this mirror is local. V21 will replicate the placement /
    /// unregister events via NetworkVariable + ServerRpc, but the per-cell
    /// dictionary lives on every peer as a read-through cache.
    ///
    /// Double-placement policy: <see cref="Register"/> refuses a second
    /// registration at an occupied cell and returns false. The caller (the
    /// <see cref="BlockPlacer"/>) checks <see cref="IsCellOccupied"/> first so
    /// the ghost preview never shows valid over an occupied cell.
    /// </remarks>
    public class BlockRegistry : MonoBehaviour
    {
        private static BlockRegistry _instance;

        /// <summary>
        /// Resolves (and lazily creates) the active <see cref="BlockRegistry"/>.
        /// </summary>
        public static BlockRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = FindObjectOfType<BlockRegistry>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }
                var go = new GameObject("BlockRegistry");
                _instance = go.AddComponent<BlockRegistry>();
                return _instance;
            }
        }

        /// <summary>EditMode test seam — reset the singleton between fixtures.</summary>
        public static void ResetInstance()
        {
            _instance = null;
        }

        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private readonly Dictionary<Vector3Int, PlacedBlock> _byCell = new Dictionary<Vector3Int, PlacedBlock>(512);

        /// <summary>Total number of registered blocks across all cells.</summary>
        public int Count => _byCell.Count;

        /// <summary>
        /// Read-only view over every currently registered block. The collection
        /// is the registry's live dictionary value set — iterating it while
        /// concurrently calling <see cref="Register"/> / <see cref="Unregister"/>
        /// is unsafe, same as any Dictionary.Values enumeration.
        /// </summary>
        public IReadOnlyCollection<PlacedBlock> AllBlocks => _byCell.Values;

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>Raised when a block is added to the registry. V21 hook for net replication.</summary>
        public event Action<PlacedBlock> OnBlockRegistered;

        /// <summary>Raised when a block is removed from the registry. V21 hook for net replication.</summary>
        public event Action<PlacedBlock> OnBlockUnregistered;

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
        //  API
        // ---------------------------------------------------------------

        /// <summary>
        /// Register <paramref name="block"/> at its <see cref="PlacedBlock.Cell"/>.
        /// Returns false if the cell is already occupied or if the block is
        /// null. The caller is expected to have called
        /// <see cref="PlacedBlock.OnPlaced"/> first.
        /// </summary>
        public bool Register(PlacedBlock block)
        {
            if (block == null) return false;
            Vector3Int cell = block.Cell;
            if (_byCell.ContainsKey(cell))
            {
                Debug.LogWarning($"[BlockRegistry] Cell {cell} already occupied by '{_byCell[cell].ItemId}'; refusing duplicate '{block.ItemId}'.", block);
                return false;
            }
            _byCell[cell] = block;
            OnBlockRegistered?.Invoke(block);
            return true;
        }

        /// <summary>
        /// Remove the registration for <paramref name="block"/>. Returns false
        /// if the block was never registered. Safe to call on a block that
        /// will be destroyed in the same frame.
        /// </summary>
        public bool Unregister(PlacedBlock block)
        {
            if (block == null) return false;
            Vector3Int cell = block.Cell;
            if (!_byCell.TryGetValue(cell, out var existing)) return false;
            if (existing != block) return false; // someone else owns this cell
            _byCell.Remove(cell);
            OnBlockUnregistered?.Invoke(block);
            return true;
        }

        /// <summary>Remove the registration at <paramref name="cell"/>. Returns the block that was unregistered, or null.</summary>
        public PlacedBlock UnregisterAt(Vector3Int cell)
        {
            if (!_byCell.TryGetValue(cell, out var existing)) return null;
            _byCell.Remove(cell);
            OnBlockUnregistered?.Invoke(existing);
            return existing;
        }

        /// <summary>Returns the block registered at <paramref name="cell"/>, or null.</summary>
        public PlacedBlock GetAt(Vector3Int cell)
        {
            _byCell.TryGetValue(cell, out var existing);
            return existing;
        }

        /// <summary>True iff a block is currently registered at <paramref name="cell"/>.</summary>
        public bool IsCellOccupied(Vector3Int cell)
        {
            return _byCell.ContainsKey(cell);
        }

        /// <summary>Wipe every registration. EditMode test seam.</summary>
        public void Clear()
        {
            _byCell.Clear();
        }
    }
}
