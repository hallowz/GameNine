using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.World.Chunks;

namespace Voidborne.Building
{
    /// <summary>
    /// Singleton. Central controller for the Rust-style socket-based building system.
    /// Handles input, raycasting, socket matching, ghost display, and final placement.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        // ── Global Constants ─────────────────────────────────────────────────────
        public const float GridUnit         = 3.0f;
        public const float WallHeight       = 3.0f;
        public const float WallThickness    = 0.1f;
        public const float FoundationHeight = 0.5f;
        public const float FloorThickness   = 0.1f;
        public const float SnapDistance      = 0.15f;
        public const float SocketDotMin      = 0.95f;
        public const float SearchRadius      = 5.0f;

        // Keep GridSize for backward compatibility with ElectricityItem
        public const float GridSize = GridUnit;

        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("References")]
        public Camera playerCamera;
        public LayerMask placementMask = ~0;
        public LayerMask terrainMask;

        [Header("Settings")]
        public float maxPlacementDistance = 15f;

        [Header("Ghost Materials")]
        public Material ghostValidMaterial;
        public Material ghostInvalidMaterial;

        // ── State ────────────────────────────────────────────────────────────────
        private enum BuildState { Idle, BuildMode }
        private BuildState _state = BuildState.Idle;

        private BuildingPieceData _activeData;
        private MaterialTier _activeTier;
        private GameObject _ghostObject;
        private GhostPiece _ghostPiece;
        private Renderer[] _ghostRenderers;
        private bool _ghostValid;

        // Rotation
        private int _rotationStep; // 0-3 for foundations on terrain (0°, 90°, 180°, 270°)
        private bool _flipped;     // for walls: 180° flip

        // Socket cycling
        private readonly List<SnapPair> _snapCandidates = new List<SnapPair>();
        private int _selectedSnapIndex = -1;

        // All registered sockets in the world
        private readonly List<SnapSocket> _allSockets = new List<SnapSocket>();
        private readonly List<BuildingPiece> _allPieces = new List<BuildingPiece>();

        // Reusable collections to avoid per-frame allocations
        private readonly List<SnapSocket> _nearbySockets = new List<SnapSocket>();
        private readonly HashSet<BuildingPiece> _neighbourPieces = new HashSet<BuildingPiece>();
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        private PlayerInventory _playerInventory;

        // ── Chunk-based building visibility ─────────────────────────────────────
        // Buildings are shown/hidden with the chunk column they sit on so they
        // aren't rendered from far away (same LOD treatment as decorations).

        /// <summary>Pieces grouped by their chunk column (X,Z only — Y=0 key).</summary>
        private readonly Dictionary<Vector2Int, List<BuildingPiece>> _chunkBuildings
            = new Dictionary<Vector2Int, List<BuildingPiece>>();

        /// <summary>Chunk columns that currently have an active LOD0 chunk.</summary>
        private readonly HashSet<Vector2Int> _activeColumns = new HashSet<Vector2Int>();

        private struct SnapPair
        {
            public SnapSocket targetSocket; // on existing placed piece
            public SnapSocket ghostSocket;  // on ghost piece
            public float distance;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (playerCamera == null)
                playerCamera = Camera.main;

            _playerInventory = FindObjectOfType<PlayerInventory>();
            EnsureGhostMaterials();
        }

        private void Update()
        {
            if (_state != BuildState.BuildMode) return;
            UpdateGhostPlacement();
            HandleInput();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void EnterBuildMode(BuildingPieceData data, MaterialTier tier = MaterialTier.Wood)
        {
            if (_state == BuildState.BuildMode && _activeData == data) return;

            _activeData = data;
            _activeTier = tier;
            _rotationStep = 0;
            _flipped = false;
            _selectedSnapIndex = -1;
            _state = BuildState.BuildMode;

            SpawnGhost(data);
        }

        public void ExitBuildMode()
        {
            _state = BuildState.Idle;
            _activeData = null;
            _selectedSnapIndex = -1;
            DestroyGhost();
        }

        public void RegisterPiece(BuildingPiece piece)
        {
            _allPieces.Add(piece);
            foreach (var s in piece.sockets)
                _allSockets.Add(s);

            // Track by chunk column for distance-based visibility.
            var col = WorldToColumn(piece.transform.position);
            if (!_chunkBuildings.TryGetValue(col, out var list))
            {
                list = new List<BuildingPiece>();
                _chunkBuildings[col] = list;
            }
            list.Add(piece);

            // Match current chunk visibility.
            piece.gameObject.SetActive(_activeColumns.Contains(col));
        }

        public void UnregisterPiece(BuildingPiece piece)
        {
            _allPieces.Remove(piece);
            foreach (var s in piece.sockets)
                _allSockets.Remove(s);

            var col = WorldToColumn(piece.transform.position);
            if (_chunkBuildings.TryGetValue(col, out var list))
            {
                list.Remove(piece);
                if (list.Count == 0) _chunkBuildings.Remove(col);
            }
        }

        // ── Chunk visibility callbacks ──────────────────────────────────────────

        private static Vector2Int WorldToColumn(Vector3 worldPos)
        {
            const int SIZE = ChunkData.SIZE;
            int ix = Mathf.FloorToInt(worldPos.x);
            int iz = Mathf.FloorToInt(worldPos.z);
            int cx = (ix >= 0) ? ix / SIZE : (ix - SIZE + 1) / SIZE;
            int cz = (iz >= 0) ? iz / SIZE : (iz - SIZE + 1) / SIZE;
            return new Vector2Int(cx, cz);
        }

        /// <summary>
        /// Called by ChunkManager when a LOD0 chunk becomes Active.
        /// Shows all building pieces in that chunk column.
        /// </summary>
        public void OnChunkColumnActivated(Vector3Int chunkPos)
        {
            var col = new Vector2Int(chunkPos.x, chunkPos.z);
            _activeColumns.Add(col);

            if (_chunkBuildings.TryGetValue(col, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) list[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Called by ChunkManager when a chunk column no longer has any LOD0 chunk.
        /// Hides all building pieces in that column.
        /// </summary>
        public void OnChunkColumnDeactivated(Vector3Int chunkPos)
        {
            var col = new Vector2Int(chunkPos.x, chunkPos.z);
            _activeColumns.Remove(col);

            if (_chunkBuildings.TryGetValue(col, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) list[i].gameObject.SetActive(false);
            }
        }

        // ── Ghost Management ─────────────────────────────────────────────────────

        private void SpawnGhost(BuildingPieceData data)
        {
            DestroyGhost();

            GameObject prefab = data.ghostPrefab != null ? data.ghostPrefab : data.prefab;
            if (prefab == null) return;

            _ghostObject = Instantiate(prefab);
            _ghostObject.name = "GhostPreview";

            // Ensure it has GhostPiece component
            _ghostPiece = _ghostObject.GetComponent<GhostPiece>();
            if (_ghostPiece == null)
                _ghostPiece = _ghostObject.AddComponent<GhostPiece>();

            // Put ghost on Ignore Raycast layer so placement raycasts don't hit it
            SetLayerRecursive(_ghostObject, LayerMask.NameToLayer("Ignore Raycast"));

            // Remove ALL colliders — ghost should never interact with physics.
            // We use manual OverlapBox for validation instead.
            foreach (var col in _ghostObject.GetComponentsInChildren<Collider>())
                Destroy(col);

            // Remove rigidbodies
            foreach (var rb in _ghostObject.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            // Disable shadows
            _ghostRenderers = _ghostObject.GetComponentsInChildren<Renderer>();
            foreach (var r in _ghostRenderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            ApplyGhostMaterial(ghostValidMaterial);
            _ghostObject.SetActive(false);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void DestroyGhost()
        {
            if (_ghostObject != null) Destroy(_ghostObject);
            _ghostObject = null;
            _ghostPiece = null;
            _ghostRenderers = null;
        }

        private void ApplyGhostMaterial(Material mat)
        {
            if (_ghostRenderers == null || mat == null) return;
            foreach (var r in _ghostRenderers)
                r.sharedMaterial = mat;
        }

        // ── Core Update Loop ─────────────────────────────────────────────────────

        private void UpdateGhostPlacement()
        {
            if (_ghostObject == null || _activeData == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // Ignore triggers so the ghost (and other triggers) don't intercept the ray
            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementMask, QueryTriggerInteraction.Ignore))
            {
                _ghostObject.SetActive(false);
                return;
            }

            _ghostObject.SetActive(true);

            bool snapped = false;

            // Try socket snapping — search near the hit point for compatible sockets
            if (_ghostPiece != null && _ghostPiece.sockets.Length > 0)
            {
                snapped = TrySocketSnap(hit.point);
            }

            // Foundation on terrain fallback (only foundations can be placed on bare terrain)
            if (!snapped && _activeData.canPlaceOnTerrain)
            {
                PlaceGhostOnTerrain(hit.point);
                snapped = true;
            }

            if (!snapped)
            {
                _ghostObject.SetActive(false);
                _ghostValid = false;
                return;
            }

            // Validate placement
            _ghostValid = ValidatePlacement();
            ApplyGhostMaterial(_ghostValid ? ghostValidMaterial : ghostInvalidMaterial);
        }

        private bool TrySocketSnap(Vector3 searchOrigin)
        {
            _snapCandidates.Clear();

            // Find all unoccupied sockets within search radius
            _nearbySockets.Clear();
            float searchRadiusSq = SearchRadius * SearchRadius;
            for (int i = _allSockets.Count - 1; i >= 0; i--)
            {
                var s = _allSockets[i];
                if (s == null) { _allSockets.RemoveAt(i); continue; }
                if (s.isOccupied) continue;
                if ((s.transform.position - searchOrigin).sqrMagnitude < searchRadiusSq)
                    _nearbySockets.Add(s);
            }

            if (_nearbySockets.Count == 0 || _ghostPiece == null || _ghostPiece.sockets == null)
                return false;

            // Find all compatible pairs between nearby world sockets and ghost sockets
            foreach (var worldSocket in _nearbySockets)
            {
                foreach (var ghostSocket in _ghostPiece.sockets)
                {
                    if (!SocketCompatibility.AreCompatible(worldSocket.socketType, ghostSocket.socketType))
                        continue;

                    _snapCandidates.Add(new SnapPair
                    {
                        targetSocket = worldSocket,
                        ghostSocket = ghostSocket,
                        distance = Vector3.Distance(worldSocket.transform.position, searchOrigin)
                    });
                }
            }

            if (_snapCandidates.Count == 0)
                return false;

            // Sort by distance
            _snapCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Clamp selected index
            if (_selectedSnapIndex < 0 || _selectedSnapIndex >= _snapCandidates.Count)
                _selectedSnapIndex = 0;

            // Apply snap
            var pair = _snapCandidates[_selectedSnapIndex];
            SnapGhostToSocket(pair.targetSocket, pair.ghostSocket);
            return true;
        }

        /// <summary>
        /// Position the ghost piece so ghostSocket lands exactly on targetSocket,
        /// with the two sockets facing each other (opposing normals).
        /// </summary>
        private void SnapGhostToSocket(SnapSocket targetSocket, SnapSocket ghostSocket)
        {
            Transform ghostRoot = _ghostObject.transform;

            // Step 1: Rotation — align ghost socket forward to face OPPOSITE of target socket forward
            Quaternion targetForward = targetSocket.transform.rotation;
            Quaternion desiredGhostSocketRotation = targetForward * Quaternion.Euler(0, 180, 0);

            // Apply flip if applicable (walls can flip inside/outside)
            if (_flipped)
                desiredGhostSocketRotation *= Quaternion.Euler(0, 180, 0);

            // Rotation offset from ghost root to ghost socket
            Quaternion ghostRootToSocket = Quaternion.Inverse(ghostRoot.rotation) * ghostSocket.transform.rotation;
            Quaternion finalRotation = desiredGhostSocketRotation * Quaternion.Inverse(ghostRootToSocket);
            ghostRoot.rotation = finalRotation;

            // Step 2: Position — after rotation, place so sockets overlap
            Vector3 ghostSocketWorldPos = ghostSocket.transform.position;
            Vector3 offset = targetSocket.transform.position - ghostSocketWorldPos;
            ghostRoot.position += offset;
        }

        private void PlaceGhostOnTerrain(Vector3 hitPoint)
        {
            // Snap Y-rotation to 90° increments
            float yRotation = _rotationStep * 90f;

            // Position: X/Z follow raycast, Y at terrain surface (embed slightly)
            Vector3 pos = new Vector3(hitPoint.x, hitPoint.y - 0.05f, hitPoint.z);

            _ghostObject.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yRotation, 0f));
        }

        // ── Validation ───────────────────────────────────────────────────────────

        private bool ValidatePlacement()
        {
            if (_activeData == null) return false;

            // 1. Must have a socket match or be a foundation on terrain
            bool hasSocketMatch = _snapCandidates.Count > 0 && _selectedSnapIndex >= 0;
            bool isTerrainPlacement = _activeData.canPlaceOnTerrain && !hasSocketMatch;

            if (!hasSocketMatch && !isTerrainPlacement)
                return false;

            // 2. No overlap — check ghost colliders against existing building pieces
            if (!CheckNoOverlap())
                return false;

            // 3. Inventory check
            if (!HasRequiredItem())
                return false;

            return true;
        }

        private bool CheckNoOverlap()
        {
            if (_ghostObject == null || _activeData == null) return true;

            // Determine bounding box from piece type — shrink generously to avoid
            // false positives at seams and adjacent pieces
            Vector3 halfExtents = GetPieceHalfExtents(_activeData.pieceType) * 0.75f;
            Vector3 center = _ghostObject.transform.TransformPoint(GetPieceCenterOffset(_activeData.pieceType));

            int overlapCount = Physics.OverlapBoxNonAlloc(
                center, halfExtents, _overlapBuffer, _ghostObject.transform.rotation, placementMask, QueryTriggerInteraction.Ignore);

            // Build set of ALL pieces that own sockets in the snap candidates —
            // these are neighbours at the seam and should never block placement.
            _neighbourPieces.Clear();
            for (int ci = 0; ci < _snapCandidates.Count; ci++)
            {
                var candidate = _snapCandidates[ci];
                if (candidate.targetSocket != null && candidate.targetSocket.ownerPiece != null)
                    _neighbourPieces.Add(candidate.targetSocket.ownerPiece);
            }

            for (int oi = 0; oi < overlapCount; oi++)
            {
                var overlap = _overlapBuffer[oi];
                // Skip ghost
                if (overlap.transform.IsChildOf(_ghostObject.transform)) continue;

                var piece = overlap.GetComponentInParent<BuildingPiece>();
                if (piece == null) continue;

                // Skip any piece that has a compatible socket nearby (neighbour at seam)
                if (_neighbourPieces.Contains(piece)) continue;

                return false;
            }
            return true;
        }

        private static Vector3 GetPieceHalfExtents(BuildingPieceType type)
        {
            switch (type)
            {
                case BuildingPieceType.Foundation:    return new Vector3(1.5f, 0.25f, 1.5f);
                case BuildingPieceType.TriFoundation: return new Vector3(1.5f, 0.25f, 1.3f); // bounding box of equilateral tri (actual shape is smaller)
                case BuildingPieceType.Wall:
                case BuildingPieceType.Doorway:
                case BuildingPieceType.Window:        return new Vector3(1.5f, 1.5f, 0.05f);
                case BuildingPieceType.Floor:         return new Vector3(1.5f, 0.05f, 1.5f);
                case BuildingPieceType.TriFloor:      return new Vector3(1.5f, 0.05f, 1.3f);
                case BuildingPieceType.Stairs:        return new Vector3(1.3f, 1.5f, 1.5f);
                case BuildingPieceType.Pillar:        return new Vector3(0.1f, 1.5f, 0.1f);
                case BuildingPieceType.HalfWall:      return new Vector3(1.5f, 0.75f, 0.05f);
                default:                              return new Vector3(1.5f, 1.5f, 1.5f);
            }
        }

        private static Vector3 GetPieceCenterOffset(BuildingPieceType type)
        {
            switch (type)
            {
                case BuildingPieceType.Foundation:    return new Vector3(0f, 0.25f, 0f);
                case BuildingPieceType.TriFoundation: return new Vector3(0f, 0.25f, 0f);
                case BuildingPieceType.Wall:
                case BuildingPieceType.Doorway:
                case BuildingPieceType.Window:        return new Vector3(0f, 1.5f, 0f);
                case BuildingPieceType.Floor:
                case BuildingPieceType.TriFloor:      return new Vector3(0f, 0.05f, 0f);
                case BuildingPieceType.Stairs:        return new Vector3(0f, 1.5f, 1.5f);
                case BuildingPieceType.Pillar:        return new Vector3(0f, 1.5f, 0f);
                case BuildingPieceType.HalfWall:      return new Vector3(0f, 0.75f, 0f);
                default:                              return Vector3.zero;
            }
        }

        private bool HasRequiredItem()
        {
            if (_playerInventory == null) return true;
            if (_activeData == null || string.IsNullOrEmpty(_activeData.inventoryItemId)) return true;
            return _playerInventory.CountAllItem(_activeData.inventoryItemId) > 0;
        }

        private void ConsumeItem()
        {
            if (_playerInventory == null) return;
            if (_activeData == null || string.IsNullOrEmpty(_activeData.inventoryItemId)) return;
            _playerInventory.RemoveItem(_activeData.inventoryItemId, 1);
        }

        // ── Input ────────────────────────────────────────────────────────────────

        private void HandleInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // Left-click: place
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                TryPlace();

            // R: rotate / cycle
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                HandleRotation();

            // Scroll wheel: cycle snap candidates
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0f && _snapCandidates.Count > 0)
                    _selectedSnapIndex = (_selectedSnapIndex + 1) % _snapCandidates.Count;
                else if (scroll < 0f && _snapCandidates.Count > 0)
                    _selectedSnapIndex = (_selectedSnapIndex - 1 + _snapCandidates.Count) % _snapCandidates.Count;
            }
        }

        private void HandleRotation()
        {
            if (_activeData == null) return;

            bool isTerrainMode = _activeData.canPlaceOnTerrain && _snapCandidates.Count == 0;

            if (isTerrainMode)
            {
                // Foundation on terrain: cycle 0°, 90°, 180°, 270°
                _rotationStep = (_rotationStep + 1) % 4;
            }
            else
            {
                // Wall/Doorway/Window/Stairs on socket: flip 180°
                var pt = _activeData.pieceType;
                if (pt == BuildingPieceType.Wall || pt == BuildingPieceType.Doorway ||
                    pt == BuildingPieceType.Window || pt == BuildingPieceType.Stairs)
                {
                    _flipped = !_flipped;
                }
            }
        }

        private void TryPlace()
        {
            if (!_ghostValid || _ghostObject == null || _activeData == null) return;

            Vector3 pos = _ghostObject.transform.position;
            Quaternion rot = _ghostObject.transform.rotation;

            // Instantiate the real prefab
            GameObject prefab = _activeData.prefab;
            if (prefab == null) return;

            GameObject placed = Instantiate(prefab, pos, rot);
            placed.transform.SetParent(transform);
            SetLayerRecursive(placed, LayerMask.NameToLayer("Building"));

            var piece = placed.GetComponent<BuildingPiece>();
            if (piece == null) piece = placed.AddComponent<BuildingPiece>();
            piece.Initialize(_activeData, _activeTier);

            // Register
            RegisterPiece(piece);

            // Mark sockets occupied (bidirectional)
            if (_selectedSnapIndex >= 0 && _selectedSnapIndex < _snapCandidates.Count)
            {
                var pair = _snapCandidates[_selectedSnapIndex];

                // Find the matching socket on the newly placed piece
                // (ghost sockets are different objects, so match by local position)
                SnapSocket placedSocket = FindMatchingSocket(piece, pair.ghostSocket);
                if (placedSocket != null && pair.targetSocket != null)
                {
                    placedSocket.isOccupied = true;
                    placedSocket.connectedPiece = pair.targetSocket.ownerPiece;

                    pair.targetSocket.isOccupied = true;
                    pair.targetSocket.connectedPiece = piece;
                }
            }

            // Consume item from inventory
            ConsumeItem();

            // Reset snap selection
            _selectedSnapIndex = -1;
        }

        /// <summary>
        /// Find the socket on a placed piece that matches a ghost socket by local position and type.
        /// </summary>
        private SnapSocket FindMatchingSocket(BuildingPiece piece, SnapSocket ghostSocket)
        {
            float bestDist = float.MaxValue;
            SnapSocket best = null;

            foreach (var s in piece.sockets)
            {
                if (s.socketType != ghostSocket.socketType) continue;
                float dist = Vector3.Distance(s.transform.localPosition, ghostSocket.transform.localPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = s;
                }
            }
            return best;
        }

        // ── Material creation ────────────────────────────────────────────────────

        private void EnsureGhostMaterials()
        {
            if (ghostValidMaterial == null)
                ghostValidMaterial = CreateTransparentMaterial("GhostValid", new Color(0f, 1f, 0f, 0.35f));
            if (ghostInvalidMaterial == null)
                ghostInvalidMaterial = CreateTransparentMaterial("GhostInvalid", new Color(1f, 0f, 0f, 0.35f));
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");

            var mat = new Material(shader) { name = name };

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);

            return mat;
        }
    }
}
