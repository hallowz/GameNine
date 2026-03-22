# Master Prompt: Rust-Style Building System for Unity

## PURPOSE

This document is an implementation spec for a Rust-style socket-based building system in Unity. Read this entire document before writing any code. Follow every measurement, enum value, and socket position exactly. Do not improvise dimensions or socket layouts — they are calculated to tile perfectly.

---

## 1. GLOBAL CONSTANTS

```
GRID_UNIT          = 3.0 meters       (every piece is based on a 3m module)
WALL_HEIGHT        = 3.0 meters
WALL_THICKNESS     = 0.1 meters
FOUNDATION_HEIGHT  = 0.5 meters
FLOOR_THICKNESS    = 0.1 meters
SNAP_DISTANCE      = 0.15 meters      (max distance for socket matching)
SOCKET_DOT_MIN     = 0.95             (minimum dot product for facing alignment)
TRIANGLE_SIDE      = 3.0 meters       (equilateral triangle side length)
TRIANGLE_HEIGHT    = 2.598 meters     (sqrt(3)/2 * 3.0)
```

All pieces use **meters** in Unity (1 Unity unit = 1 meter). Ensure Unity project scale is set accordingly.

---

## 2. MATERIAL TIERS

Every building piece exists in three material tiers. These are cosmetic/health variants of the same prefab geometry. They share identical socket layouts and colliders.

| Tier   | Prefab Suffix | Example              |
|--------|---------------|----------------------|
| Wood   | `_Wood`       | `Wall_Wood`          |
| Stone  | `_Stone`      | `Wall_Stone`         |
| Iron   | `_Iron`       | `Wall_Iron`          |

Each tier has its own inventory item (e.g., `item_wall_wood`, `item_wall_stone`). Material tier affects only the mesh/material and health — **never** the socket positions or collider sizes.

---

## 3. BUILDING PIECE CATALOG

### 3.1 Foundation (Square)

**Mesh:** Box 3.0 × 0.5 × 3.0 (X × Y × Z). Pivot at bottom-center (Y = 0 is the bottom face).

**Collider:** BoxCollider matching mesh exactly (center Y = 0.25, size 3.0 × 0.5 × 3.0).

**Sockets (8 total):**

| Socket Name         | Local Position         | Local Forward   | Type               |
|----------------------|------------------------|-----------------|--------------------|
| `Edge_North`        | (0, 0.5, 1.5)         | (0, 0, 1)      | `FoundationEdge`   |
| `Edge_South`        | (0, 0.5, -1.5)        | (0, 0, -1)     | `FoundationEdge`   |
| `Edge_East`         | (1.5, 0.5, 0)         | (1, 0, 0)      | `FoundationEdge`   |
| `Edge_West`         | (-1.5, 0.5, 0)        | (-1, 0, 0)     | `FoundationEdge`   |
| `Corner_NE`         | (1.5, 0.5, 1.5)       | (1, 0, 1).norm | `FoundationCorner` |
| `Corner_NW`         | (-1.5, 0.5, 1.5)      | (-1, 0, 1).norm| `FoundationCorner` |
| `Corner_SE`         | (1.5, 0.5, -1.5)      | (1, 0, -1).norm| `FoundationCorner` |
| `Corner_SW`         | (-1.5, 0.5, -1.5)     | (-1, 0, -1).norm| `FoundationCorner`|

- Edge sockets sit on the **top surface** at the midpoint of each edge, facing outward.
- Corner sockets sit at each top corner — used for pillar snapping.
- `FoundationEdge` sockets accept: walls, doorways, windows, other foundations (side-by-side), and low walls.

**Neighbor Foundation Tiling:** When a second foundation snaps to `Edge_North`, the second foundation's `Edge_South` aligns to the first's `Edge_North`. Both sockets get marked occupied. The second foundation's pivot is placed at `firstFoundation.position + Vector3(0, 0, 3.0)`.

---

### 3.2 Foundation (Triangle)

**Mesh:** Equilateral triangle prism. Base edge = 3.0m, height (Y) = 0.5m. Vertices of the triangle (top-down):
- A = (0, 0, 1.732)       — apex (north)
- B = (-1.5, 0, 0)        — bottom-left
- C = (1.5, 0, 0)         — bottom-right

Pivot at centroid bottom: (0, 0, 0.577). This means the centroid sits at Y = 0, and the three vertices are at the distances above relative to the centroid.

Recalculated vertex positions relative to centroid-bottom pivot:
- A = (0, 0, 1.155)
- B = (-1.5, 0, -0.577)
- C = (1.5, 0, -0.577)

**Collider:** MeshCollider (convex) matching the triangular prism.

**Sockets (6 total):**

| Socket Name         | Local Position                  | Local Forward    | Type               |
|----------------------|---------------------------------|------------------|--------------------|
| `Edge_BC`           | (0, 0.5, -0.577)               | (0, 0, -1)      | `FoundationEdge`   |
| `Edge_AB`           | (-0.75, 0.5, 0.289)            | (-0.866, 0, 0.5)| `FoundationEdge`   |
| `Edge_AC`           | (0.75, 0.5, 0.289)             | (0.866, 0, 0.5) | `FoundationEdge`   |
| `Corner_A`          | (0, 0.5, 1.155)                | (0, 0, 1)       | `FoundationCorner` |
| `Corner_B`          | (-1.5, 0.5, -0.577)            | (-1, 0, 0)      | `FoundationCorner` |
| `Corner_C`          | (1.5, 0.5, -0.577)             | (1, 0, 0)       | `FoundationCorner` |

- Edge sockets are at midpoints of each edge, on the top face, facing outward perpendicular to the edge.
- `FoundationEdge` on a triangle is compatible with `FoundationEdge` on a square — this allows triangle foundations to tile against square foundations along their 3m edges.

---

### 3.3 Wall

**Mesh:** Box 3.0 × 3.0 × 0.1 (width × height × depth). Pivot at bottom-center (Y = 0 is the bottom edge, centered on X and Z).

**Collider:** BoxCollider center (0, 1.5, 0), size (3.0, 3.0, 0.1).

**Sockets (2 total):**

| Socket Name    | Local Position   | Local Forward | Type            |
|----------------|------------------|---------------|-----------------|
| `Bottom`       | (0, 0, 0)       | (0, 0, 1)    | `WallBottom`    |
| `Top`          | (0, 3.0, 0)     | (0, 0, 1)    | `WallTop`       |

- `Bottom` snaps to `FoundationEdge` or `FloorEdge`.
- `Top` accepts `FloorEdge` (for placing a floor/ceiling above).
- The wall's local Z+ is the "outside" face. When snapping to a foundation edge, the wall's Z+ matches the edge's outward-facing direction.

**Snap Behavior:** When placing a wall on `Foundation.Edge_North`:
1. Wall's `Bottom` socket aligns to the foundation's `Edge_North` socket.
2. Wall position = `Edge_North.position` (which is (0, 0.5, 1.5) in foundation local space, meaning the wall base sits on top of the foundation surface).
3. Wall rotation = foundation rotation × edge socket's rotation (so the wall faces outward from that edge).

---

### 3.4 Doorway

**Mesh:** Same outer dimensions as Wall (3.0 × 3.0 × 0.1) but with a rectangular hole cut out. Door hole: 1.0m wide × 2.2m tall, centered on X, bottom flush with wall bottom.

Hole bounds (local): X from -0.5 to 0.5, Y from 0 to 2.2.

**Collider:** Use a compound of BoxColliders or a MeshCollider to represent the frame (not the hole).

**Sockets:** Identical to Wall — same 2 sockets, same positions, same types. This makes doorways perfectly interchangeable with walls in the socket system.

---

### 3.5 Window

**Mesh:** Same outer dimensions as Wall (3.0 × 3.0 × 0.1) with a rectangular hole. Window hole: 1.4m wide × 1.0m tall, centered on X, bottom at Y = 1.2 (so the window opening spans Y 1.2–2.2).

Hole bounds (local): X from -0.7 to 0.7, Y from 1.2 to 2.2.

**Collider:** Compound BoxColliders or MeshCollider for the frame.

**Sockets:** Identical to Wall — same 2 sockets, same positions, same types.

---

### 3.6 Floor / Ceiling (Square)

**Mesh:** Box 3.0 × 0.1 × 3.0 (same footprint as foundation, but thin). Pivot at bottom-center.

**Collider:** BoxCollider center (0, 0.05, 0), size (3.0, 0.1, 3.0).

**Sockets (4 total):**

| Socket Name    | Local Position     | Local Forward | Type          |
|----------------|--------------------|---------------|---------------|
| `Edge_North`   | (0, 0, 1.5)       | (0, 0, 1)    | `FloorEdge`   |
| `Edge_South`   | (0, 0, -1.5)      | (0, 0, -1)   | `FloorEdge`   |
| `Edge_East`    | (1.5, 0, 0)       | (1, 0, 0)    | `FloorEdge`   |
| `Edge_West`    | (-1.5, 0, 0)      | (-1, 0, 0)   | `FloorEdge`   |

- `FloorEdge` connects to `WallTop` (floor sits on top of walls).
- `FloorEdge` also connects to other `FloorEdge` sockets (tiling floors side-by-side).
- When a floor's `Edge_South` snaps to a wall's `Top` socket, the floor is positioned so its bottom surface sits exactly at Y = 3.0 relative to the foundation top.

---

### 3.7 Floor / Ceiling (Triangle)

**Mesh:** Same triangular footprint as Triangle Foundation but only 0.1m thick.

**Collider:** MeshCollider (convex).

**Sockets (3 total):**

| Socket Name    | Local Position                  | Local Forward     | Type          |
|----------------|---------------------------------|-------------------|---------------|
| `Edge_BC`      | (0, 0, -0.577)                 | (0, 0, -1)       | `FloorEdge`   |
| `Edge_AB`      | (-0.75, 0, 0.289)              | (-0.866, 0, 0.5) | `FloorEdge`   |
| `Edge_AC`      | (0.75, 0, 0.289)               | (0.866, 0, 0.5)  | `FloorEdge`   |

- Same compatibility rules as square floor. Triangle floors can tile against square floors along matching 3m edges.

---

### 3.8 Stairs / Ramp

**Mesh:** A sloped surface or stepped mesh. Bounding box: 3.0 × 3.0 × 3.0 (occupies a full wall-height, foundation-width slot). The ramp goes from Y = 0 at one edge to Y = 3.0 at the opposite edge.

Pivot at bottom-center of the lower edge.

**Collider:** MeshCollider matching stair geometry (for walking on) plus a simplified BoxCollider trigger for placement validation.

**Sockets (2 total):**

| Socket Name    | Local Position     | Local Forward | Type            |
|----------------|--------------------|---------------|-----------------|
| `Bottom`       | (0, 0, 0)         | (0, 0, -1)   | `WallBottom`    |
| `Top`          | (0, 3.0, 3.0)     | (0, 0, 1)    | `FloorEdge`     |

- `Bottom` snaps to `FoundationEdge` like a wall.
- `Top` provides a `FloorEdge` socket at the elevated end so a floor piece can connect at the upper level.

---

### 3.9 Pillar

**Mesh:** Cylinder or thin box. Dimensions: 0.2 × 3.0 × 0.2 (thin column, full wall height). Pivot at bottom-center.

**Collider:** BoxCollider or CapsuleCollider matching the pillar.

**Sockets (2 total):**

| Socket Name    | Local Position   | Local Forward | Type             |
|----------------|------------------|---------------|------------------|
| `Bottom`       | (0, 0, 0)       | (0, -1, 0)   | `PillarBottom`   |
| `Top`          | (0, 3.0, 0)     | (0, 1, 0)    | `PillarTop`      |

- `PillarBottom` snaps to `FoundationCorner`.
- `PillarTop` can accept roof or floor corner connections (future expansion).

---

### 3.10 Half Wall (Low Wall)

**Mesh:** Box 3.0 × 1.5 × 0.1 (half the height of a full wall). Pivot at bottom-center.

**Collider:** BoxCollider center (0, 0.75, 0), size (3.0, 1.5, 0.1).

**Sockets (1 total):**

| Socket Name    | Local Position   | Local Forward | Type            |
|----------------|------------------|---------------|-----------------|
| `Bottom`       | (0, 0, 0)       | (0, 0, 1)    | `WallBottom`    |

- Snaps to `FoundationEdge` exactly like a wall, just shorter. No `Top` socket — nothing stacks on a half wall.

---

## 4. SOCKET TYPE ENUM AND COMPATIBILITY

### 4.1 Socket Type Enum

```csharp
public enum SocketType
{
    FoundationEdge,
    FoundationCorner,
    WallBottom,
    WallTop,
    FloorEdge,
    PillarBottom,
    PillarTop
}
```

### 4.2 Compatibility Matrix

A socket on piece A can connect to a socket on piece B **only** if their types appear as a pair in this list. Order does not matter — (X, Y) means X↔Y.

```
FoundationEdge  ↔  FoundationEdge    (tiling foundations side-by-side)
FoundationEdge  ↔  WallBottom        (placing walls/doorways/windows on foundation edges)
WallTop         ↔  FloorEdge         (placing floors on top of walls)
FloorEdge       ↔  FloorEdge         (tiling floors side-by-side)
FloorEdge       ↔  WallBottom        (placing walls on floor edges for upper stories)
FoundationCorner ↔ PillarBottom      (placing pillars at foundation corners)
```

Implement this as a static lookup — a `HashSet<(SocketType, SocketType)>` or a 2D bool array.

---

## 5. SNAP SOCKET COMPONENT

```csharp
public class SnapSocket : MonoBehaviour
{
    public SocketType socketType;
    public bool isOccupied;
    public BuildingPiece ownerPiece;       // the piece this socket belongs to
    public BuildingPiece connectedPiece;   // the piece snapped into this socket (null if unoccupied)
}
```

Each socket is a child GameObject of the building piece prefab with:
- Transform positioned and rotated exactly per the tables above.
- A `SnapSocket` component.
- **No collider** — sockets are found via spatial query, not physics.
- The socket's **local forward (Z+)** is the outward-facing normal as specified in the tables.

---

## 6. BUILDING PIECE COMPONENT

```csharp
public class BuildingPiece : MonoBehaviour
{
    public BuildingPieceType pieceType;    // enum: Foundation, TriFoundation, Wall, Doorway, Window, Floor, TriFloor, Stairs, Pillar, HalfWall
    public MaterialTier materialTier;      // enum: Wood, Stone, Iron
    public float health;
    public float maxHealth;
    public SnapSocket[] sockets;           // populated in Awake() via GetComponentsInChildren<SnapSocket>()
    
    // Unique ID for network/save
    public string buildingPieceId;
}
```

---

## 7. BUILDING PIECE DATA (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Building/Piece Data")]
public class BuildingPieceData : ScriptableObject
{
    public BuildingPieceType pieceType;
    public GameObject prefab;              // the actual building piece prefab
    public GameObject ghostPrefab;         // semi-transparent preview prefab
    public string inventoryItemId;         // links to the inventory item
    public bool canPlaceOnTerrain;         // true only for foundations
    public float[] healthPerTier;          // index 0=Wood, 1=Stone, 2=Iron
}
```

Create one ScriptableObject per piece type per material tier, OR one per piece type with material variants handled internally.

---

## 8. BUILDING MANAGER (Singleton)

This is the central controller. It handles input, raycasting, socket matching, ghost display, and final placement.

### 8.1 State Machine

```
Idle → BuildMode (when player selects a building item from hotbar)
BuildMode → Idle (when player deselects / presses escape / switches to non-building item)
```

In `BuildMode`, every frame:
1. Cast a ray from camera center forward.
2. Determine what the ray hits (terrain or existing `BuildingPiece`).
3. Position the ghost piece using snapping logic (Section 9).
4. Color the ghost green (valid) or red (invalid).
5. On left-click, if valid, place the piece.

### 8.2 Socket Spatial Query

Maintain a flat list or spatial hash of all placed sockets in the world. When the raycast hits near an existing building piece:

```
List<SnapSocket> nearbySockets = AllSockets.Where(s => 
    !s.isOccupied && 
    Vector3.Distance(s.transform.position, rayHitPoint) < SEARCH_RADIUS
);
```

`SEARCH_RADIUS` = 5.0m (generous — we narrow down by actual snap distance later).

For each nearby socket, check if any socket on the ghost piece is compatible and within `SNAP_DISTANCE`. Pick the closest valid pair.

### 8.3 Piece Placement

When placement is confirmed:
1. Instantiate the real prefab at the ghost's position and rotation.
2. Mark both sockets (on the new piece and on the existing piece) as occupied and set their `connectedPiece` references.
3. Deduct the item from inventory.
4. Register all new sockets in the spatial query system.

---

## 9. SNAPPING MATH

### 9.1 Snapping to an Existing Socket

Given:
- `targetSocket` — a SnapSocket on an existing placed piece
- `ghostSocket` — a SnapSocket on the ghost piece that is compatible with `targetSocket`

The goal: position the ghost piece so `ghostSocket` lands exactly on `targetSocket`, with the two sockets facing each other (opposing normals).

```csharp
// Step 1: Calculate the rotation that aligns ghostSocket's forward to face OPPOSITE of targetSocket's forward
// (sockets face outward, so two connecting sockets point away from each other)
Quaternion targetForward = targetSocket.transform.rotation;
Quaternion ghostForward = ghostSocket.transform.rotation;

// The ghost socket should face opposite the target socket
Quaternion desiredGhostSocketRotation = targetForward * Quaternion.Euler(0, 180, 0);

// Rotation offset from ghost root to ghost socket
Quaternion ghostRootToSocket = Quaternion.Inverse(ghostPiece.transform.rotation) * ghostSocket.transform.rotation;

// Final ghost root rotation
Quaternion finalRotation = desiredGhostSocketRotation * Quaternion.Inverse(ghostRootToSocket);
ghostPiece.transform.rotation = finalRotation;

// Step 2: Position so the sockets overlap
// After rotation is set, recalculate the socket's world position, then offset
Vector3 ghostSocketWorldPos = ghostPiece.transform.TransformPoint(ghostSocket.transform.localPosition);
Vector3 offset = targetSocket.transform.position - ghostSocketWorldPos;
ghostPiece.transform.position += offset;
```

**IMPORTANT:** Apply rotation first, then position. If done in reverse, the position will be wrong because the socket's world offset depends on the piece's rotation.

### 9.2 Foundation Placement on Terrain

Only foundations (square and triangle) can be placed on bare terrain.

1. Raycast hits terrain at point `hitPoint`.
2. Snap the ghost foundation so its bottom face is at `hitPoint.y` (or slightly below for embedding — subtract 0.05m).
3. Snap Y-rotation to 90° increments: `Mathf.Round(playerYRotation / 90) * 90`. The player presses **R** to cycle: 0°, 90°, 180°, 270°.
4. X and Z rotation are always 0 (foundations are always level, ignoring terrain slope).
5. Position X and Z follow the raycast hit directly — no grid snapping on terrain for the first foundation.

**Second+ foundations on terrain near existing foundations:** Even on a terrain raycast, also check for nearby `FoundationEdge` sockets. If one is within range, prefer socket snapping over raw terrain placement. This keeps buildings aligned.

---

## 10. ROTATION CONTROLS

| Context                                  | R Key Behavior                                   |
|------------------------------------------|--------------------------------------------------|
| Foundation on terrain                    | Cycles Y rotation: 0°, 90°, 180°, 270°          |
| Wall/Doorway/Window on FoundationEdge    | Flips 180° (inside face vs outside face)         |
| Floor on WallTop                         | No rotation needed — locked by socket alignment  |
| Foundation on FoundationEdge             | No rotation needed — locked by socket alignment  |
| Stairs on FoundationEdge                 | Flips 180° (stair direction up vs down)          |

When a piece has multiple valid snap points in range, **scroll wheel** cycles between them (e.g., a wall could snap to any of the 4 edges of a foundation — scroll to pick which one).

---

## 11. PLACEMENT VALIDATION

Before allowing placement, check:

1. **Socket match exists** — at least one valid compatible socket pair within `SNAP_DISTANCE`, or terrain hit for foundations.
2. **No overlap** — the ghost piece's trigger collider does not overlap with any existing building piece colliders. Use `Physics.OverlapBox` or a trigger collider with `OnTriggerStay`. **Exclude** the piece(s) being snapped to from this check (they will always touch at the seam).
3. **Correct piece type** — only foundations on terrain; everything else requires a socket.
4. **Inventory check** — player has the item in inventory.

The ghost material should be:
- **Green semi-transparent** when all checks pass.
- **Red semi-transparent** when any check fails.

Use a shared material instance on the ghost and swap the color — do not create new materials every frame.

---

## 12. GHOST PREFAB SETUP

For each building piece prefab, create a ghost variant:
- Same mesh geometry.
- All colliders set to **isTrigger = true** (no physics interaction).
- Material: a single transparent shader (e.g., `Unlit/Transparent` or custom) with color property.
- No `BuildingPiece` component — has a `GhostPiece` component instead that holds references to its snap sockets and a validity flag.
- Disable shadows (cast and receive).

---

## 13. INVENTORY INTEGRATION

The building system hooks into the existing inventory/hotbar:

1. Each building piece has an inventory item (e.g., `item_wall_wood`, `item_foundation_stone`).
2. The item's data references a `BuildingPieceData` ScriptableObject.
3. When the player selects this item on the hotbar, `BuildingManager.EnterBuildMode(BuildingPieceData data)` is called.
4. When the player deselects or switches items, `BuildingManager.ExitBuildMode()` is called.
5. On successful placement, `Inventory.RemoveItem(itemId, 1)` is called.

**Do not modify the core inventory system.** The building system should only need:
- A way to check if an item is a building item (bool flag or type check on the item data).
- A way to get the `BuildingPieceData` from an inventory item.
- A method to remove an item by ID and amount.

---

## 14. PREFAB HIERARCHY (for each piece)

Example for `Wall_Wood`:

```
Wall_Wood (GameObject)
  ├── Model (child: mesh renderer + mesh filter)
  ├── Collider (child: BoxCollider, not trigger)
  ├── Socket_Bottom (child: SnapSocket component, transform at (0, 0, 0), forward (0,0,1))
  └── Socket_Top (child: SnapSocket component, transform at (0, 3, 0), forward (0,0,1))
```

- The root has `BuildingPiece` component.
- Model is a child so the mesh can be swapped without affecting socket positions.
- Each socket is a child with a `SnapSocket` component and the exact local transform from the tables in Section 3.

---

## 15. FILE STRUCTURE

```
Assets/
  Building/
    Scripts/
      BuildingManager.cs
      BuildingPiece.cs
      BuildingPieceData.cs
      SnapSocket.cs
      SocketType.cs
      SocketCompatibility.cs
      GhostPiece.cs
      BuildingPieceType.cs
      MaterialTier.cs
    Data/
      Foundation_Wood.asset
      Foundation_Stone.asset
      Foundation_Iron.asset
      TriFoundation_Wood.asset
      ... (one ScriptableObject per piece per tier)
    Prefabs/
      Pieces/
        Foundation_Wood.prefab
        Foundation_Stone.prefab
        Foundation_Iron.prefab
        TriFoundation_Wood.prefab
        Wall_Wood.prefab
        Wall_Stone.prefab
        Wall_Iron.prefab
        Doorway_Wood.prefab
        Window_Wood.prefab
        Floor_Wood.prefab
        TriFloor_Wood.prefab
        Stairs_Wood.prefab
        Pillar_Wood.prefab
        HalfWall_Wood.prefab
        ... (all tiers for all pieces)
      Ghosts/
        Ghost_Foundation.prefab
        Ghost_Wall.prefab
        Ghost_Doorway.prefab
        Ghost_Window.prefab
        Ghost_Floor.prefab
        Ghost_TriFoundation.prefab
        Ghost_TriFloor.prefab
        Ghost_Stairs.prefab
        Ghost_Pillar.prefab
        Ghost_HalfWall.prefab
    Materials/
      Ghost_Valid.mat       (green, transparent)
      Ghost_Invalid.mat     (red, transparent)
      Wood/
        Wood_Wall.mat
        Wood_Foundation.mat
        ...
      Stone/
        ...
      Iron/
        ...
```

---

## 16. IMPLEMENTATION ORDER

Follow this sequence. Test each step before proceeding.

1. **Enums and data classes:** `SocketType`, `BuildingPieceType`, `MaterialTier`, `SocketCompatibility` (static compatibility lookup).
2. **SnapSocket component:** Simple MonoBehaviour with the fields from Section 5.
3. **BuildingPiece component:** MonoBehaviour per Section 6. Gathers sockets in `Awake()`.
4. **BuildingPieceData ScriptableObject:** Per Section 7.
5. **Foundation prefab (Wood only first):** Build the prefab with exact dimensions and socket placements from Section 3.1. Verify socket positions in the Scene view.
6. **GhostPiece component and ghost prefab:** Per Section 12.
7. **BuildingManager — terrain placement:** Implement raycasting and foundation placement on terrain per Sections 8 and 9.2. Test placing foundations.
8. **BuildingManager — socket snapping:** Implement socket matching and snap math per Section 9.1. Test placing a wall on a foundation.
9. **Wall, Doorway, Window prefabs:** Build with exact specs from Sections 3.3–3.5.
10. **Floor prefab:** Build per Section 3.6. Test multi-story building (foundation → walls → floor → walls → floor).
11. **Triangle pieces:** Build triangle foundation and floor per Sections 3.2 and 3.7.
12. **Stairs, Pillar, HalfWall:** Build remaining pieces.
13. **Rotation controls:** Implement R-key and scroll-wheel per Section 10.
14. **Placement validation:** Implement overlap checks per Section 11.
15. **Inventory hookup:** Connect to existing inventory system per Section 13.
16. **Material tiers:** Duplicate prefabs for Stone and Iron with different meshes/materials.
17. **Polish:** Ghost coloring, audio, particle effects on placement.

---

## 17. CRITICAL RULES

- **Never hardcode socket positions in scripts.** Socket positions live on the prefab transforms. Scripts read them at runtime.
- **All pieces must use consistent 3m module.** If a piece does not tile at 3m, the entire system breaks.
- **Rotation is always determined by sockets** except for foundations on terrain.
- **Socket occupied flags must be bidirectional.** When piece A snaps to piece B, mark sockets on BOTH pieces.
- **Ghost pieces never have rigidbodies.**
- **Do not use physics joints for building connections.** Connections are logical (references in SnapSocket), not physical.
- **No structural integrity.** Any piece can exist as long as it has a valid snap or is a foundation on terrain. Floating pieces are allowed.
- **Test with the following build sequence to verify:** Foundation → Wall on each edge → Floor on top → Walls on floor → Second floor → Stairs connecting floors → Triangle foundation adjacent to square → Pillar on corner.

---

## 18. EDGE CASES TO HANDLE

- **Stacking foundations vertically:** Not allowed. Foundations only go on terrain or side-by-side.
- **Walls on floor edges (upper stories):** `FloorEdge ↔ WallBottom` is a valid pair. A wall placed on a floor edge behaves identically to a wall on a foundation edge.
- **Removing pieces:** When a piece is destroyed, mark all its sockets as unoccupied, and clear `connectedPiece` on the partner sockets. Do NOT cascade-destroy connected pieces (no structural integrity).
- **Overlapping ghost with terrain:** Allow partial terrain intersection for foundations (they embed). For all other pieces, terrain intersection = invalid.
- **Pieces placed at different terrain heights:** Each foundation adjusts Y independently to terrain height. Walls between foundations at different heights may gap — this is acceptable (same as Rust).
