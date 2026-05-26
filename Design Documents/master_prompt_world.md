# MASTER PROMPT — WORLD & TERRAIN SYSTEMS
## Voidborne: 3D FPS Survival Game

> **Purpose:** This document is a dedicated implementation guide for all world decoration,
> biome expansion, vegetation, water, and road systems. It is a companion to `master_prompt.md`.
> Read `master_prompt.md` first for core architecture (chunk system, marching cubes, biome map).

---

## ARCHITECTURAL CONTEXT (read before implementing anything)

Before implementing any system here, internalize these constraints from the existing codebase:

- **Chunk system:** 32×32×32 voxels. Chunks are loaded/unloaded dynamically via `ChunkLoader`.
  Surface band is Y -128 to +128.
- **Terrain is fully deformable.** `TerrainDeformer.DeformSphere()` modifies density fields at runtime,
  marks chunks dirty, triggers async mesh rebuilds. World objects placed on terrain MUST respond to this.
- **Biome is climate-based.** `BiomeMap` assigns biomes via temperature + moisture 2D noise (channels 10/11).
  Climate frequency is `0.0008f` — biome regions are large. Use `BiomeMap.GetBiome()` or
  `GetBlendedBiomeData()` for placing objects in the right biome.
- **BiomeData.Default is used for mesh generation** (not per-chunk biome data) to prevent seams.
  Per-biome visual variation is applied via per-vertex colors post-generation.
- **No Marching Cubes reimplementation.** The external GPU MC solution (MarchingCubesAdapter) is sacred.
  Do not touch it for world decoration — surface detection must use raycasting or density sampling.
- **Coordinate system:** World positions are continuous floats. Chunk coords are `Vector3Int`.
  Use `ChunkCoordUtility` for conversions. Negative coords require floor division (already handled).
- **WorldSeed.cs** provides deterministic `SeedOffset(channel)` — use unused channels (≥20) for
  decoration seeding to avoid conflicting with terrain noise (channels 0-11 are taken).
- **Performance budget:** The game already runs GPU marching cubes async with 4 concurrent chunk slots.
  All decoration must use object pooling, LODs, GPU instancing, and distance culling.
  Never place per-object `Update()` loops for static world decoration.

---

## VOLUME W1 — EXPANDED BIOME SYSTEM

### W1.1 — New Biome Definitions (BiomeRegistry + ScriptableObjects)

Add the following biomes to `BiomeRegistry.cs` and create corresponding `.asset` files
in `Assets/ScriptableObjects/Biomes/`. Expand the climate space so the 4 existing + 8 new
biomes tile organically across the world.

Existing biomes for reference:
- Lowlands (id=0): temp 0.3–0.7, moist 0.3–0.7, heightScale 28
- Badlands (id=1): temp 0.7–1.0, moist 0.0–0.3, heightScale 50
- Frozen Peaks (id=2): temp 0.0–0.2, moist 0.4–1.0, heightScale 65, Y -64 to 300
- Fungal Marshes (id=3): temp 0.5–0.8, moist 0.7–1.0, heightScale 22

**New biomes (ids 4–11):**

| ID | Name | Temp Range | Moist Range | heightScale | heightFreq | Notes |
|----|------|-----------|-------------|-------------|------------|-------|
| 4 | Plains | 0.3–0.6 | 0.2–0.5 | 10 | 0.003 | Very flat, great for roads |
| 5 | Dense Forest | 0.4–0.7 | 0.5–0.8 | 20 | 0.004 | Gentle hills, dense trees |
| 6 | River Valley | 0.2–0.6 | 0.6–1.0 | 15 | 0.002 | Low, wide valleys, rivers |
| 7 | Highlands | 0.1–0.4 | 0.3–0.6 | 45 | 0.006 | Rugged hills, plateaus |
| 8 | Volcanic Wastes | 0.8–1.0 | 0.0–0.2 | 55 | 0.008 | Jagged, rock formations, hot |
| 9 | Overhang Cliffs | 0.2–0.5 | 0.3–0.7 | 40 | 0.005 | Dramatic overhangs, arches |
| 10 | Savanna | 0.6–0.9 | 0.1–0.4 | 18 | 0.003 | Open, dry, long grass |
| 11 | Tundra | 0.0–0.15 | 0.0–0.4 | 12 | 0.003 | Flat, icy, sparse |

**BiomeDefinition.cs additions** — add these fields if not present:
```csharp
// Whether this biome generates overhangs (3D noise layer added on top of 2D height)
public bool hasOverhangs;
public float overhangStrength;   // 0–1
public float overhangFrequency;  // 3D noise freq for overhang blobs
public float overhangMinY;       // Only add overhangs above this Y
```

**DensityFunction.cs changes** — in `SurfaceBandDensity()`, after computing the base height
density, check biome `hasOverhangs` and add a 3D noise layer that creates overhanging rock
formations. Sample 3D noise at `(worldPos × overhangFrequency)`, threshold against
`overhangStrength`, add it only where `worldPos.y > overhangMinY` and above the 2D surface
height by at least 4 units. This creates natural cliff overhangs and arches.

Assign Overhang Cliffs biome `hasOverhangs = true`, `overhangStrength = 0.35f`,
`overhangFrequency = 0.018f`, `overhangMinY = 20f`.

---

### W1.2 — Valley & River Shape Generation

Rivers and valleys are terrain-shape features generated in `DensityFunction.cs`, not separate
GameObjects. They carve negative density into the terrain.

**Add to `DensityFunction.cs`:**

```
RiverMap: sample 2D Simplex noise at (worldPosXZ × 0.0005f, channel 20) → ridge transform
          (abs(noise) inverted) → values near 0 = river centerlines
```

River carving:
- In `SurfaceBandDensity()`, compute `riverValue = abs(RidgedNoise2D(worldPosXZ, freq:0.0005f, channel:20))`
- If `riverValue < riverWidth` (e.g. 0.05f) AND biome is River Valley or Lowlands or Plains:
  - Compute `carveDepth = lerp(8f, 0f, riverValue / riverWidth)` — deep at center, 0 at edge
  - Apply biome moisture modulation: wetter biomes carve deeper
  - Subtract `carveDepth` from the density (carves channel into terrain)
  - At the carved riverbed, water placement system (see W3) will detect and fill

Valley generation:
- Add `valleyNoise = Noise2D(worldPosXZ, freq:0.0006f, channel:21)` — large-scale undulation
- In River Valley biome, `heightScale` is already low (15). Additionally apply:
  `density -= max(0, valleyNoise × 12f)` to create wide bowl shapes with gentle sides.

---

### W1.3 — Road Generation System

Roads are terrain-carved paths guaranteed to be drivable. They run through ALL biomes.

**Concept:** Roads follow a procedural spline network generated from the same seed as the world.
They are NOT separate meshes — they are carved into the terrain density field so they use
the existing marching cubes pipeline. Roads appear as flattened strips of terrain.

**RoadMap.cs** (`Assets/Scripts/World/Generation/RoadMap.cs`):
```csharp
// Static utility, no MonoBehaviour
public static class RoadMap
{
    // Road network uses Worley/Voronoi-like cell centers as waypoints
    // Cells are large (e.g. 512 unit cells)
    // Roads connect nearest-neighbor cell centers with smoothed splines

    public static float GetRoadInfluence(float2 worldXZ);
    // Returns 0 = no road, 1 = road center, 0–1 = road edge blend
    // Sampling is cheap: compute 3×3 cell neighborhood, find 2 nearest centers,
    // compute distance to segment between them (line-to-point distance)

    public static float GetRoadElevation(float2 worldXZ, float naturalTerrainHeight);
    // Given natural terrain height at XZ, compute what the road surface should be.
    // Roads follow terrain height but are CLAMPED to max slope:
    //   - Sample terrain height along road direction every 8 units
    //   - Smooth with running average (window = 64 units) to eliminate steep grades
    //   - Result is a gently sloping road elevation
    // Returns NaN if not on a road

    // Cell size and road width constants
    const float CellSize = 512f;     // Voronoi cell size in world units
    const float RoadHalfWidth = 6f;  // Road is 12 units wide
    const float BlendZone = 4f;      // Blend from road to terrain over 4 units
}
```

**Integration into DensityFunction.cs:**
In `SurfaceBandDensity()`, after all biome height and cave computations:
```
float roadInfluence = RoadMap.GetRoadInfluence(worldPosXZ);
if (roadInfluence > 0)
{
    float roadY = RoadMap.GetRoadElevation(worldPosXZ, naturalTerrainHeight);
    // Blend density toward a flat road surface:
    // If worldPos.y < roadY  → density = lerp(density, +solidStrength, roadInfluence)
    // If worldPos.y > roadY  → density = lerp(density, -airStrength, roadInfluence)
    // This fills terrain below road and carves air above it
}
```

Road rules (enforce in RoadMap):
- Max grade: 15% slope (rise/run ≤ 0.15). Clamp elevation smoothing window until grade is met.
- Roads always connect. No dead ends. At minimum: every Voronoi cell connects to 2 neighbors.
- Roads are NOT too steep even in Frozen Peaks, Highlands, or Volcanic Wastes —
  they switchback rather than climb directly (implement by extending spline length around steep terrain).
- Roads CAN be bumpy (marching cubes smooths imperfectly, that's fine) — just not impossible to drive.
- Roads ignore rivers — a simple bridge placeholder (road elevation stays above river carve).
  Full bridge meshes are a future feature.

**RoadSurface material:** Roads should use a different triplanar texture index (e.g. materialIndex 8)
assigned via vertex color channel. Add road surface detection to `ChunkMeshBuilder.ComputeBiomeColors()`
by checking `RoadMap.GetRoadInfluence()` per vertex and overriding the surface material weight.

---

## VOLUME W2 — VEGETATION SYSTEMS

### W2.1 — World Decoration Manager

**WorldDecorationManager.cs** (`Assets/Scripts/World/Decoration/WorldDecorationManager.cs`)

Singleton MonoBehaviour that listens to chunk activation events and places vegetation/props
on newly active chunks. Removal on chunk unload. Uses object pooling throughout.

```csharp
public class WorldDecorationManager : MonoBehaviour
{
    // Called by ChunkManager when a chunk becomes Active (after mesh applied)
    public void OnChunkActivated(ChunkData chunk, Mesh mesh);

    // Called before ChunkManager unloads a chunk
    public void OnChunkDeactivating(Vector3Int chunkPos);

    // Called when terrain is deformed (chunks that were rebuilt)
    public void OnChunksRebuilt(HashSet<Vector3Int> affectedChunks);
}
```

**Surface Point Detection:**
Use raycasting downward from above the chunk to find surface points. Cast rays on a
grid (e.g. every 2 units in XZ within the chunk's XZ extent). For each hit:
- Check hit normal — must be upward-facing (normal.y > 0.5f) for ground placement
- Sample `BiomeMap.GetBiome(worldXZ)` at hit point for biome type
- Use `WorldSeed.SeedOffset(channel)` + position hash for deterministic random placement
- Batch all placement into a coroutine spread across frames to avoid hitches

**Pooling:** One `ObjectPool<T>` per prefab type (tree variants, rock variants, grass clumps).
Max pool sizes configurable per type.

**ChunkManager.cs changes:**
Add `[SerializeField] WorldDecorationManager decorationManager;` reference.
In `ApplyMesh()` (ChunkRenderer), after mesh is applied, fire:
`WorldDecorationManager.Instance?.OnChunkActivated(chunkData, mesh);`
In `RemoveChunk()`, fire `OnChunkDeactivating(chunkPos)` before returning to pool.
In `RegenerateDirtyChunks()`, fire `OnChunksRebuilt(dirtySet)` after rebuilds complete.

---

### W2.2 — Tree System

**Overview:** Trees are GPU-instanced LOD prefabs placed by WorldDecorationManager.
Three variants share a common LOD structure. Not physically simulated, but interact
with terrain deformation (see W2.2.5).

**Folder:** `Assets/Prefabs/World/Trees/`

#### Tree Variants

All trees use a single mesh per LOD level, rendered via GPU instancing with
`Graphics.DrawMeshInstancedIndirect` or Unity's `LODGroup` component.

**Variant 1 — Broadleaf (common, temperate)**
- Trunk: Brown cylinder, slightly tapered, 2–4 units tall, 0.3–0.5 unit radius
- Canopy: 3–5 overlapping sphere-ish meshes tinted green (dark green, mid green, light green)
- LOD0: Full mesh (trunk + 4 leaf spheres, ~200 tris)
- LOD1 (25+ units): Simplified (trunk + 2 leaf spheres, ~80 tris)
- LOD2 (60+ units): Billboard quad with pre-baked texture
- LOD3 (120+ units): Cull

**Variant 2 — Pine/Conifer (cold biomes, highlands)**
- Trunk: Brown cylinder, tall (4–7 units), thin (0.2 unit radius)
- Canopy: 3 stacked cones tapering up, dark green
- LOD0: Full mesh (~180 tris)
- LOD1 (25+ units): Simplified cones (~60 tris)
- LOD2 (70+ units): Billboard
- LOD3 (130+ units): Cull

**Variant 3 — Scrub/Dead Tree (dry, volcanic, badlands)**
- Trunk: Gnarled cylinder + 2 branch stubs, dark brown/grey
- Canopy: No leaves, just branches
- LOD0: Full mesh (~120 tris)
- LOD1 (30+ units): Simplified (~40 tris)
- LOD2 (80+ units): Billboard
- LOD3 (150+ units): Cull

**Billboard textures:** Pre-baked at edit time. Store in
`Assets/Textures/Vegetation/Billboards/`. Each billboard is a 512×512 RGBA with alpha for
transparency. Use Unity's `BillboardRenderer` asset or simple quad with alpha-cutout shader.

#### Tree Placement Rules

Per surface hit point during chunk decoration:
1. Reject if normal.y < 0.6 (too steep for trees)
2. Reject if on a road (RoadMap.GetRoadInfluence > 0.3)
3. Reject if underwater (Y below water table — see W3)
4. Sample biome → choose tree variant:
   - Lowlands, Plains, Dense Forest, River Valley → Broadleaf (density 0.04/m²)
   - Frozen Peaks, Tundra, Highlands → Pine (density 0.03/m²)
   - Badlands, Volcanic Wastes, Savanna → Scrub (density 0.02/m²)
   - Fungal Marshes → No standard trees (mushrooms — future volume)
5. Deterministic skip: hash(worldX, worldZ, seed) % 100 > densityThreshold → skip
6. Randomize rotation (Y axis only), scale ±20%

**Tree prefab structure:**
```
TreeBroadleaf (prefab root)
├── TreeRoot (MonoBehaviour: WorldTree)
├── LODGroup component
├── LOD0 (child)
│   ├── TrunkMesh (MeshFilter + MeshRenderer, brown material)
│   └── CanopyMesh (MeshFilter + MeshRenderer, green material)
├── LOD1 (child)
│   └── SimplifiedMesh
└── LOD2 (child)
    └── BillboardQuad
```

**WorldTree.cs** (`Assets/Scripts/World/Decoration/WorldTree.cs`):
```csharp
public class WorldTree : MonoBehaviour
{
    public Vector3 anchorWorldPos;  // Base of trunk in world space
    public Vector3Int hostChunkPos; // Chunk this tree belongs to

    // Called by WorldDecorationManager when host chunk terrain is deformed
    // tree checks if it still has solid terrain beneath it
    public void OnTerrainDeformed(TerrainDeformer.DeformEvent evt);
    // → Raycasts down from anchorWorldPos + up offset
    // → If no terrain hit within 1 unit below anchor: DestroyOrPool()
    // → If terrain shifted: snap Y to new surface, or DestroyOrPool()
}
```

#### W2.2.5 — Tree Terrain Deformation Response

`WorldDecorationManager.OnChunksRebuilt(affectedChunks)`:
1. Collect all `WorldTree` instances whose `hostChunkPos` is in `affectedChunks`
2. For each: call `tree.OnTerrainDeformed(evt)`
3. Trees that lose footing are returned to pool

This is an async process spread over multiple frames — use a coroutine.
Do NOT iterate all world trees every frame. Use a `Dictionary<Vector3Int, List<WorldTree>>`
to look up only the affected chunks' trees.

---

### W2.3 — Rock System

**Overview:** Small rocks placed on terrain surfaces. Initially static (no physics).
Become physics-enabled when terrain beneath them is deformed away.
Player can pick up rocks with `E`.

**WorldRock.cs** (`Assets/Scripts/World/Decoration/WorldRock.cs`):

```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(MeshCollider))]
public class WorldRock : MonoBehaviour, IInteractable
{
    public enum RockState { Static, Physics, Held }

    [SerializeField] RockState state = RockState.Static;
    [SerializeField] Vector3 anchorWorldPos;
    [SerializeField] Vector3Int hostChunkPos;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SetState(RockState.Static);  // Start frozen
    }

    public void SetState(RockState newState)
    {
        state = newState;
        switch (newState)
        {
            case RockState.Static:
                rb.isKinematic = true;
                rb.detectCollisions = false;  // No physics cost at all
                break;
            case RockState.Physics:
                rb.isKinematic = false;
                rb.detectCollisions = true;
                break;
            case RockState.Held:
                rb.isKinematic = true;
                rb.detectCollisions = false;
                break;
        }
    }

    // IInteractable
    public string InteractPrompt => "Pick up";
    public void Interact(PlayerController player)
    {
        if (state == RockState.Static || state == RockState.Physics)
            player.InventoryController.TryPickupItem(this);
            // Or: attach to player's hold point
    }

    // Called when host chunk terrain is deformed
    public void OnTerrainDeformed()
    {
        if (state != RockState.Static) return;
        // Cast ray down from 0.5 units above anchor
        bool hasGround = Physics.Raycast(anchorWorldPos + Vector3.up * 0.5f,
                                          Vector3.down, 1.2f, terrainLayerMask);
        if (!hasGround)
            SetState(RockState.Physics);  // Wake up, fall
    }
}
```

**Rock variants (3 mesh variants, 2 size classes = 6 prefab variants):**
- Small Rock A: Rounded, roughly 0.3 unit diameter
- Small Rock B: Flat-ish, disc-shaped, 0.4 unit diameter
- Small Rock C: Angular, jagged, 0.35 unit diameter
- Each variant has a Medium version (~0.6 unit diameter) placed less frequently

**Rock materials:** One shared stone material with slight color variation via MaterialPropertyBlock
(grey range: warm grey, cool grey, dark grey). No per-rock material instances.

**Rock placement rules:**
- Density: ~0.08 rocks/m² (much denser than trees, rocks are small)
- Any biome except underwater zones
- Normal.y > 0.4 (can be on steeper slopes than trees)
- Roads: allow rocks on road edges (roadInfluence < 0.5) but not center
- Deterministic: hash(x, z, seed+50) % 100 > rockDensityThreshold

**Performance:** At 200 unit render distance with chunk-based culling, thousands of rocks are
possible. CRITICAL: `rb.detectCollisions = false` + `rb.isKinematic = true` on static rocks
means ZERO physics cost. Only rocks in `Physics` or `Held` state have physics cost.
Enable physics on at most 32 rocks simultaneously world-wide (track a counter).

---

### W2.4 — Grass System

Grass renders as GPU-instanced quads / thin billboard meshes on grass-type terrain surfaces.
Must handle 10,000+ grass blades within render distance with stable 60fps.

**GrassRenderer.cs** (`Assets/Scripts/World/Decoration/GrassRenderer.cs`)

MonoBehaviour singleton. Uses `Graphics.DrawMeshInstancedIndirect` with a compute shader
to cull and render grass per frame. No per-blade GameObjects.

**Architecture:**
```
GrassChunkData (per terrain chunk)
    └── List<GrassInstanceData> blades  (position, normal, scale, color)

Each frame:
    1. CPU-side frustum cull grass chunks (coarse AABB against camera frustum)
    2. Upload surviving instances to GPU structured buffer
    3. GPU vertex shader: billboard each blade toward camera, apply wind offset
    4. Single DrawMeshInstancedIndirect call (or one per LOD band)
```

**GrassInstanceData (struct, GPU-safe):**
```csharp
struct GrassInstanceData
{
    float3 position;    // World-space blade root
    float3 normal;      // Surface normal (blade grows in this direction)
    float scale;        // 0.5–1.2 variation
    float colorVariant; // 0–1, lerped between two grass colors in shader
}
```

**Grass LOD bands:**
- Band 0 (0–25 units): Full blade mesh (4 quads crossed, 16 tris per blade)
- Band 1 (25–60 units): Single crossed quad (8 tris per blade)
- Band 2 (60–120 units): Single upright quad (4 tris per blade)
- Band 3 (120+ units): Cull entirely
- Transition is sharp (distance-based switch in the compute shader cull pass)

**Grass placement:**
- Only on upward-facing surfaces (normal.y > 0.7)
- Only in grass biomes: Lowlands, Plains, Dense Forest, River Valley, Savanna
- Do NOT place in Frozen Peaks, Tundra, Volcanic Wastes, Badlands, Fungal Marshes
- Very high density: 1 blade per 0.5m² within Band 0, thinned procedurally in Bands 1–2
- Deterministic placement: compute via hash from chunk seed, avoid allocating a list per blade
  in hot path — pre-generate per chunk on chunk activation

**Grass shader (HLSL/URP):**
```
GrassBladeShader (custom URP unlit/lit blend):
- Input: GrassInstanceData from StructuredBuffer
- Vertex: position blade root + extrude upward along normal × height
           Apply wind offset: sin(Time * windSpeed + position.x/z × windFrequency)
- Fragment: sample gradient texture (dark at root, bright at tip)
            lerp between two grass colors by colorVariant
- Alpha cutout at tip (soft edge)
- Receive shadows (important for visual quality)
- Cast shadows: off (performance)
```

**Wind:** Global wind direction/speed from a single `GrassWindSettings` ScriptableObject.
No per-blade wind state — pure procedural in shader.

**GrassRenderer integration with WorldDecorationManager:**
- On `OnChunkActivated`: generate grass instances for chunk, store in `_grassChunks` dict
- On `OnChunkDeactivating`: remove from dict
- On `OnChunksRebuilt`: remove + regenerate grass for affected chunks
- Grass instance generation: run on thread pool (all pure math, no Unity API)
  except final upload to GPU buffer (main thread, fast memcpy)

---

## VOLUME W3 — WATER SYSTEM (VOXEL FIELD + TICK PHYSICS)

### Design Philosophy

Water is stored as a **per-voxel byte field** (`WaterField`) in each chunk, exactly like
the ore system (`OreField`). Where water goes is decided during biome/terrain generation,
then stored as data. No runtime noise queries, no cave detection heuristics.

**Two MC passes per chunk:**
1. **Terrain pass** — solid density only. Water voxels are just air (density < 0).
2. **Water pass** — builds a 33³ density field from `WaterField`, runs MC to produce the
   water surface mesh. Reuses existing `GenerateMeshAsync` rebuild-slot path.

**WaterTickManager** simulates flow physics: water moves downward into available space,
volume is conserved, affected chunk meshes are regenerated.

Result: two meshes per chunk (terrain + water) from two MC passes.
Terrain mesh goes to MeshFilter + MeshCollider (opaque).
Water mesh goes to a child Water GameObject (transparent, no collider).
Solid terrain faces under water are ALWAYS rendered — water shader is transparent.

---

### W3.1 — WaterField (Data Storage)

**ChunkData.cs** — add alongside existing `OreField`:
```csharp
public byte[] WaterField;  // 0 = no water, 1-255 = fill level (255 = full voxel)
```
Initialize in constructor: `WaterField = new byte[VOLUME];`

Helpers: `GetWater(x,y,z)` / `SetWater(x,y,z,value)` — same index layout as density.

Fill level encodes sub-voxel water height for smooth MC surfaces:
- 255 = fully submerged voxel
- ~128 = water surface crosses mid-voxel → MC iso-surface here
- 0 = no water

---

### W3.2 — WaterGenerator (Burst Job — mirrors OreGenerator)

**WaterGenerator.cs** (`Assets/Scripts/World/Generation/WaterGenerator.cs`)

Static class with `WaterGenerationJob : IJobParallelFor` (Burst-compiled).

```csharp
public static JobHandle ScheduleAsync(
    ChunkData chunk,
    out NativeArray<byte> resultWaterField,
    out NativeArray<float> densityNative)
```

**Per-voxel logic:**
1. Solid voxel (`density > 0`) → `WaterField = 0`, return
2. Compute world position, determine water surface Y at this XZ:
   - **Ocean:** `waterY = SeaLevel (0f)` — only if voxel is not deep underground
     (surface height noise check: `worldY > surfaceHeight - 2` → open to sky)
   - **River:** channel 20 abs-ridged noise, `waterY = RiverSurface (5f)` in channels
   - **Lake:** channel 21 valley noise, `waterY = LakeLevel (-5f)` in bowls
3. Pick highest applicable waterY. If `worldY >= waterY` → 0
4. Fully submerged (`waterY - worldY >= 1.0`) → 255
5. Surface voxel → `(byte)(clamp(waterY - worldY, 0, 1) * 255)`

Same noise channels (20, 21) and parameters as DensityFunction river/valley carving —
water fills the carved shapes exactly.

**Runtime query helpers** (no noise — pure WaterField lookup via ChunkManager):
```csharp
public static bool IsUnderwater(Vector3 worldPos)   // WaterField > 0 at position
public static byte GetWaterLevel(Vector3 worldPos)   // returns the fill level byte
```

---

### W3.3 — Water Mesh Generation (Second MC Pass)

**ChunkMeshBuilder.cs** — replace old water methods with:

```csharp
public bool GenerateWaterMeshFromField(ChunkData chunk, ChunkRenderer renderer)
```

1. `BuildExpandedWaterField(chunk)` → builds `float[33³]` from WaterField:
   - `density = (level / 127.5f) - 1.0f` → 255→+1.0, 128→~0 (surface), 0→-1.0
   - Boundary voxels (pos 32) read from neighbor chunks' WaterField
   - Missing neighbors default to 0 (no water at edges)
2. Calls existing `adapter.GenerateMeshAsync(waterDensity33, onComplete)` — reuses
   rebuild-slot MC path. No new GPU kernels needed.
3. Callback: `renderer.ApplyWaterMesh(mesh)`

**ChunkManager integration:**
- `PendingWaterJob` struct (parallel to `PendingOreJob`)
- Scheduled in LoadChunk callback after terrain mesh ready
- `ProcessPendingWaterJobs()` called from Update — polls completion,
  copies WaterField, triggers water MC
- `RegenerateWaterMesh(Vector3Int chunkPos)` — public, called by WaterTickManager

---

### W3.4 — Water Shader

**WaterShader.shader** (`Assets/Shaders/WaterShader.shader`) — URP Custom Lit:

```hlsl
// Vertex:
//   Gentle sine-wave surface ripple (amplitude 0.05–0.15 units — subtle, water shape
//   comes from MC mesh not vertex displacement). Two layered waves, offset in time/direction.

// Fragment:
//   Depth-based color: sample _CameraDepthTexture to find scene depth behind surface.
//     shallow (depth < 1u) = light aqua (0.4, 0.8, 0.9)
//     deep    (depth > 8u) = dark blue (0.05, 0.15, 0.35)
//   Fresnel: pow(1 - dot(viewDir, normal), 4) → reflect skybox color at grazing angle
//   Foam: where MC water mesh is thin / near top surface, drive foam alpha from a
//         foam noise texture scrolling with wave direction
//   Normal map: 2 scrolling UVs, different directions, blended for ripple detail
//   Refraction: sample _CameraOpaqueTexture, offset UVs by normal map (strength 0.02)
//   Alpha: 0.65 base — semi-transparent so terrain beneath is clearly visible
//   Render queue: Transparent (3000), ZWrite Off, Cull Back
```

---

### W3.5 — WaterTickManager (Flow Physics)

**WaterTickManager.cs** (`Assets/Scripts/World/Water/WaterTickManager.cs`)

MonoBehaviour singleton. Ticks every `tickInterval` (default 0.5s).

```csharp
public class WaterTickManager : MonoBehaviour
{
    [SerializeField] float tickInterval = 0.5f;
    [SerializeField] int maxChunksPerTick = 4;
}
```

**Tick logic (SimulateWaterTick):**

Process loaded chunks near player. For each water voxel (bottom-to-top scan order):

1. **Gravity flow:** Check voxel below (y-1). If air (density < 0) and `WaterField < 255`:
   transfer `min(current, 255 - belowLevel)` downward. Mark both chunks dirty.
2. **Lateral spread:** If below is full/solid, try 4 horizontal neighbors.
   Equalize fill levels between source and each lower neighbor.
3. **Cross-chunk:** When local coords cross chunk boundary, query neighbor chunk
   via `ChunkManager.Instance.GetChunk()`. Read/write their WaterField directly.
4. **Volume conservation:** Each transfer is exact. No water created or destroyed.

**Dirty tracking:** `HashSet<Vector3Int> dirtyWaterChunks`. After tick, call
`ChunkManager.Instance.RegenerateWaterMesh(pos)` for each dirty chunk (capped per frame).

---

### W3.6 — Water Collision & Player Interaction

**Player water detection** — query WaterField directly (no noise, no triggers):
```csharp
// PlayerController.Update():
bool isUnderwater = WaterGenerator.IsUnderwater(transform.position);

if (isUnderwater)
{
    // Slow movement (0.5× speed multiplier)
    // Disable jump, apply upward buoyancy force
    // Enable swim controls (future volume)
}
```

Reflects current world state instantly (including flow changes from WaterTickManager).

**Physics rocks in water** — fold buoyancy into WorldRock:
```csharp
// WorldRock.FixedUpdate() — only runs when state == RockState.Physics:
byte waterLevel = WaterGenerator.GetWaterLevel(transform.position);
if (waterLevel > 0)
{
    float submersion = waterLevel / 255f;
    rb.AddForce(Vector3.up * buoyancyForce * submersion, ForceMode.Acceleration);
}
```

**Grass and tree underwater checks:**
```csharp
bool underwater = WaterGenerator.IsUnderwater(surfacePoint);
if (underwater) continue;  // Skip tree/grass/rock placement
```

---

### W3.7 — Underwater Visual Effect

**WaterPostProcess.cs** (`Assets/Scripts/World/Water/WaterPostProcess.cs`):
```csharp
public class WaterPostProcess : MonoBehaviour
{
    [SerializeField] Camera cam;

    void LateUpdate()
    {
        bool underwater = WaterGenerator.IsUnderwater(cam.transform.position);
        // Toggle _UNDERWATER shader keyword
        // Swap fog settings when entering/leaving water
    }
}
```

---

### W3.8 — Files Removed by This Rewrite

These files existed under the old noise-based water system and are **deleted**:
- `WaterDensityFunction.cs` — replaced by WaterGenerator + WaterField lookup
- `WaterSystem.cs` — ocean plane, water body registration, raycast IsUnderwater all gone
- `WaterCullPlane.cs` — legacy duplicate of WaterPostProcess
- `WaterBuoyancy.cs` — WorldRock has inline buoyancy
- `DensityGeneration.compute` `ComputeWaterDensity` kernel — removed (water MC is CPU-driven)
- `MarchingCubesAdapter.GenerateWaterMeshAsync()` — removed (reuses `GenerateMeshAsync`)

---

## VOLUME W4 — INTEGRATION & PERFORMANCE

### W4.1 — Layered Activation Order

When a chunk becomes `Active`, WorldDecorationManager runs these steps IN ORDER
(each can be a coroutine yield to spread across frames):

1. **Surface scan:** Cast rays to collect surface points (main thread — Physics.Raycast)
2. **Biome classification:** Tag each surface point with biome type (cheap, BiomeMap lookup)
3. **Road & water check:** Tag surface points near roads or underwater (RoadMap + WaterGenerator.IsUnderwater queries)
4. **Tree placement:** Instantiate/pool trees at qualifying points (main thread, Unity API)
5. **Rock placement:** Instantiate/pool rocks at qualifying points (main thread, Unity API)
6. **Grass generation:** Generate GrassInstanceData list (thread pool), upload to GPU buffer (main thread)

NOTE: Water does NOT have a decoration step. Water meshes are generated inside ChunkMeshBuilder
(from WaterField → MC), not in WorldDecorationManager. The decoration pass only uses
WaterGenerator.IsUnderwater() as a query to skip placing vegetation/rocks underwater.

Maximum surface sample points per chunk: 256 (16×16 grid within the 32×32 chunk XZ).
This is sufficient resolution and keeps the scan fast.

### W4.2 — Performance Targets

| System | Max Cost | Strategy |
|--------|----------|----------|
| Trees (LOD0) | ≤ 500 visible | LOD + instancing + chunk culling |
| Rocks (static) | ≤ 5000 visible | Zero physics, shared material |
| Rocks (physics) | ≤ 32 simultaneous | Strict cap, queue physics activation |
| Grass blades | ≤ 50,000 visible | GPU instancing + cull compute |
| Water meshes | Same budget as terrain | Per-chunk MC pass, culled by ChunkLoader |
| Decoration scan | ≤ 2ms/chunk | Spread over 4+ frames via coroutine |

### W4.3 — Shader & Material Asset Inventory

Create the following shader/material assets:

| Asset | Path | Type |
|-------|------|------|
| GrassBladeShader | Assets/Shaders/GrassBladeShader.shader | URP Custom |
| GrassBladeMaterial | Assets/Materials/GrassBladeMaterial.mat | Uses GrassBladeShader |
| WaterShader | Assets/Shaders/WaterShader.shader | URP Custom Lit |
| WaterMaterial | Assets/Materials/WaterMaterial.mat | Uses WaterShader, Transparent queue |
| TreeTrunkMaterial | Assets/Materials/Trees/TreeTrunk.mat | URP/Lit |
| TreeLeafMaterial | Assets/Materials/Trees/TreeLeaf.mat | URP/Lit, alpha cutout |
| RockMaterial | Assets/Materials/RockMaterial.mat | URP/Lit |
| BillboardMaterial | Assets/Materials/Trees/TreeBillboard.mat | URP/Lit, alpha cutout |

### W4.4 — ScriptableObject & Prefab Inventory

```
Assets/
├── Prefabs/World/
│   ├── Trees/
│   │   ├── TreeBroadleaf.prefab
│   │   ├── TreePine.prefab
│   │   └── TreeScrub.prefab
│   ├── Rocks/
│   │   ├── RockSmallA.prefab
│   │   ├── RockSmallB.prefab
│   │   ├── RockSmallC.prefab
│   │   ├── RockMediumA.prefab
│   │   ├── RockMediumB.prefab
│   │   └── RockMediumC.prefab
│   └── (no Water/ prefab — water meshes are generated per-chunk by ChunkMeshBuilder)
├── ScriptableObjects/
│   ├── Biomes/
│   │   ├── Plains.asset
│   │   ├── DenseForest.asset
│   │   ├── RiverValley.asset
│   │   ├── Highlands.asset
│   │   ├── VolcanicWastes.asset
│   │   ├── OverhangCliffs.asset
│   │   ├── Savanna.asset
│   │   └── Tundra.asset
│   └── World/
│       └── GrassWindSettings.asset
└── Scripts/World/
    ├── Decoration/
    │   ├── WorldDecorationManager.cs
    │   ├── WorldTree.cs
    │   ├── WorldRock.cs
    │   └── GrassRenderer.cs
    ├── Water/
    │   ├── WaterPostProcess.cs    (underwater post-process toggle on Main Camera)
    │   └── WaterTickManager.cs    (flow physics simulation — W3.5)
    └── Generation/
        ├── RoadMap.cs              (new — W1.3)
        └── WaterGenerator.cs      (Burst job + IsUnderwater/GetWaterLevel — W3.2)
```

---

## VOLUME W5 — IMPLEMENTATION ORDER

Implement in this sequence. Each chunk is independently testable.

**W5.1** — BiomeRegistry + BiomeDefinition expansion (W1.1)
- Add 8 new biomes to registry
- Add `hasOverhangs` fields to BiomeDefinition
- Add overhang density modification to DensityFunction (W1.1 overhang logic)
- Test: walk the world, observe new biome visual variety

**W5.2** — River & Valley carving (W1.2)
- Add river noise + carving to DensityFunction
- Add valley noise to River Valley biome
- Test: see carved river channels in terrain

**W5.3** — Road System (W1.3)
- Implement RoadMap.cs (Voronoi cells, road influence, elevation smoothing)
- Integrate road carving into DensityFunction
- Test: roads connect across chunks, are drivable (no extreme slopes)

**W5.4** — WorldDecorationManager scaffold (W2.1)
- Hook into ChunkManager events
- Surface scan coroutine
- No actual decoration yet — just scan + log surface points

**W5.5** — Tree System (W2.2)
- Create tree prefabs (3 variants, LODs, billboards)
- Placement logic in WorldDecorationManager
- WorldTree deformation response
- Test: trees appear on new chunks, removed when terrain dug out

**W5.6** — Rock System (W2.3)
- Create rock prefabs (6 variants)
- Placement + WorldRock physics activation
- IInteractable pickup with E
- Test: rocks sit static, become physics when terrain dug beneath them, can be picked up

**W5.7** — Grass System (W2.4)
- GrassRenderer compute + shader
- Integration with WorldDecorationManager
- LOD bands
- Wind
- Test: grass renders at high density, stable framerate, responds to wind

**W5.8** — Water System (W3) — **REWRITE IN PROGRESS**
Previous noise-based implementation replaced with voxel field approach.
- ChunkData: add `byte[] WaterField` (like OreField), remove density33Cache/waterMesh
- WaterGenerator.cs: Burst job populating WaterField from biome rules (ocean/river/lake)
  + static IsUnderwater/GetWaterLevel helpers (WaterField lookup, no noise)
- ChunkMeshBuilder: `GenerateWaterMeshFromField()` builds 33³ density from WaterField → MC
- ChunkManager: PendingWaterJob pipeline (mirrors PendingOreJob), RegenerateWaterMesh()
- WaterTickManager.cs: flow physics (gravity + lateral spread, volume conservation)
- DensityGeneration.compute: remove ComputeWaterDensity kernel + water uniforms
- MarchingCubesAdapter: remove GenerateWaterMeshAsync (reuse GenerateMeshAsync)
- Update consumers: WaterPostProcess, WorldRock, GrassRenderer → WaterGenerator queries
- Delete: WaterDensityFunction.cs, WaterSystem.cs, WaterCullPlane.cs, WaterBuoyancy.cs
- ChunkRenderer: Water child GO with MeshFilter + MeshRenderer (no collider) — KEEP
- WaterShader + WaterMaterial (transparent, depth color, Fresnel, foam, refraction) — KEEP
- Test: water visible at sea level, in river channels, in valley bowls; terrain visible
  through water; digging below sea level → water flows into hole over ticks

**W5.9** — Integration pass (W4)
- Connect deformation events: trees, rocks, grass all respond to TerrainDeformer
- Verify performance targets (W4.2)
- Fix any visual seams between terrain and water
- Verify roads connect between chunks seamlessly (check chunk boundary continuity in RoadMap)

---

## KEY CONSTRAINTS SUMMARY

1. **Never reimplement Marching Cubes.** All terrain shape changes go through DensityFunction.
2. **Roads and rivers are density-carved** into terrain — not separate meshes.
3. **Rocks are static by default.** Zero physics cost until terrain is modified beneath them.
4. **Grass is GPU-only.** No grass GameObjects. Only GrassInstanceData in GPU buffers.
5. **Trees use LODGroup + GPU instancing.** No per-tree Update() loops.
6. **Water is a voxel byte field (like ore).** `ChunkData.WaterField` (byte[32³]) stores
   fill levels (0=empty, 255=full). Populated by `WaterGenerator` Burst job during generation.
   Second MC pass generates water mesh from WaterField. `WaterTickManager` handles flow physics.
   `WaterGenerator.IsUnderwater()` / `GetWaterLevel()` query WaterField directly (no noise).
   Terrain meshes under water are NOT culled — water shader's transparency shows them.
7. **All placement is deterministic.** Same seed = same world every time. Use WorldSeed channels ≥ 20.
8. **ChunkMeshBuilder uses BiomeData.Default** for mesh generation — do not change this.
   Road surface material variation goes through vertex color override at mesh build time.
9. **Decoration spreads over multiple frames.** WorldDecorationManager uses coroutines.
   Never scan + place everything for a chunk in a single frame.
10. **RoadMap is stateless and cheap to query.** No precomputation, no caching required.
    It must be callable from DensityFunction (which runs per-voxel per-chunk).

---

## UNITY MCP WORKFLOW

After implementing any system in this document, **always verify in the Unity Editor via MCP** before marking work complete. Do not rely solely on code review — runtime behavior (missing references, physics issues, shader errors) only surfaces in-editor.

### Standard Check Sequence

After writing or modifying scripts:

```
1. refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)
2. read_console(types=["error", "warning"], count=20, include_stacktrace=True)
   → Fix ALL errors before proceeding. Warnings that affect gameplay must also be fixed.
3. If scene setup is needed, use manage_gameobject / manage_components to wire up singletons.
4. manage_editor(action="enter_play_mode")
5. Wait a few seconds for chunk generation to run, then:
   read_console(types=["error", "warning"], count=30, include_stacktrace=True)
6. manage_scene(action="screenshot", include_image=True, max_resolution=512)
   → Visually verify the scene looks correct (terrain, decoration, water visible).
7. manage_editor(action="exit_play_mode")
```

### Scene Setup Checklist (for a fresh test scene)

When testing world systems, ensure the following GameObjects/components exist in the scene:

- **WorldManager** (or GameBootstrapper) — root singleton that bootstraps all systems
- **ChunkManager** — with `WorldDecorationManager` reference assigned
- **WorldDecorationManager** — with `treePrefabs[]`, `rockPrefabs[]`, `grassRenderer` assigned
- **GrassRenderer** — with `grassMaterial` (GrassBladeMaterial) and `windSettings` assigned
- **WaterPostProcess** — on the Main Camera (uses WaterGenerator.IsUnderwater)
- **WaterTickManager** — singleton (tickInterval, maxChunksPerTick)
- **Player** (or Camera) — so ChunkLoader has a target to load around

Use `find_gameobjects` to locate existing objects before creating duplicates.
Use `manage_components` to assign serialized references rather than dragging in Editor.

### Fixing Runtime Errors

- **NullReferenceException in decoration coroutines:** Check singleton `Instance` not null before calling methods. Use null-conditional `?.`
- **Missing shader variants:** After adding new shaders, call `manage_asset(action="refresh")` and check Shader compilation in console.
- **Grass not rendering:** Confirm `_GrassBuffer` is set on the material before `DrawMeshInstancedIndirect`. Procedural instancing requires `#pragma instancing_options procedural:SetupProcedural` in the shader AND `material.enableInstancing = true` in C#.
- **Water plane z-fighting:** Increase `globalSeaLevel` or add a small positive offset to water Y.
- **Physics rocks ignoring cap:** Verify `s_ActivePhysicsCount` is a static field (shared across all WorldRock instances).

### When to Screenshot

Take a screenshot (`manage_scene(action="screenshot", include_image=True)`) after:
- Trees or rocks first appear on chunks
- Grass renders for the first time
- Water plane is visible
- Any visual artifact or suspected rendering issue

---

*End of master_prompt_world.md*
