# MASTER PROMPT — Voidborne: 3D FPS Survival Game

## READ THIS FIRST — AGENT WORKFLOW

You are Claude Code, orchestrating the development of a large-scale Unity game. This project is too large for a single context window. You MUST follow this agent-based workflow:

### How Development Works

1. **Read this file** — Understand the current state by checking the Progress Tracker below.
2. **Find the next incomplete chunk** — The first chunk with status `[ ]` (not started) or `[P]` (partial).
3. **Spawn a sub-agent** to implement that chunk:
   ```
   Task: Implement Volume X, Chunk Y — [chunk title]
   Read MASTER_PROMPT.md for full context.
   Read the chunk specification carefully.
   Implement all files described.
   Test compilation with Unity.
   When done, update MASTER_PROMPT.md progress tracker.
   ```
4. **When that agent finishes**, spawn a NEW sub-agent for code review:
   ```
   Task: Code Review — Volume X, Chunk Y — [chunk title]
   Read MASTER_PROMPT.md for context.
   Review all files created/modified in this chunk.
   Open Unity and verify no compilation errors.
   Run any playmode/editmode tests.
   Fix any issues found.
   Update MASTER_PROMPT.md with review status.
   ```
5. **After review passes**, spawn the next chunk's implementation agent.
6. **Repeat until volume is complete.**

### Critical Rules

- **ONE CHUNK PER AGENT.** Never try to implement multiple chunks in one context.
- **ALWAYS UPDATE THE PROGRESS TRACKER** at the end of each agent's work.
- **FRESH CONTEXT FOR EVERY AGENT.** Each agent reads this file fresh — don't assume prior context.
- **TEST IN UNITY AFTER EVERY CHUNK.** Open Unity, let it compile, check the Console for errors. Fix before moving on.
- **DO NOT MODIFY MARCHING CUBES INTERNALS.** We use a git repo's compute shader solution. Wrap it, don't rewrite it. See Volume 0 for details.
- **File paths are relative to the Unity project root.** The project lives at `E:/Programs/repos/hallowz/GameNine/`. (Note: `~/VoidborneProject/` was the original path but is WRONG — always use `E:/Programs/repos/hallowz/GameNine/`.)

---

## PROJECT STRUCTURE

```
VoidborneProject/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                  # Singletons, game managers, boot
│   │   ├── World/                 # Terrain, chunks, biomes, generation
│   │   │   ├── Generation/        # Noise, density, biome assignment
│   │   │   ├── Chunks/            # Chunk loading, meshing, LOD
│   │   │   └── MarchingCubes/     # EXTERNAL — git submodule, DO NOT EDIT
│   │   ├── Player/                # FPS controller, camera, input
│   │   ├── Combat/                # Guns, melee, projectiles, damage
│   │   │   ├── Guns/
│   │   │   ├── Melee/
│   │   │   └── Projectiles/
│   │   ├── Inventory/             # Items, backpacks, crafting grid
│   │   ├── Crafting/              # Recipes, workbenches, grids
│   │   ├── Building/              # Structural placement, snapping, integrity
│   │   ├── Automation/            # Machines, conveyors, power grid
│   │   ├── Vehicles/              # Vehicle base, specific vehicles
│   │   ├── Enemies/               # AI, spawning, specific enemy types
│   │   ├── Quests/                # Quest system, dialogue, story
│   │   ├── UI/                    # HUD, menus, inventory UI
│   │   └── Utilities/             # Extensions, helpers, object pools
│   ├── Shaders/                   # Custom shaders, terrain materials
│   ├── Prefabs/
│   ├── Materials/
│   ├── ScriptableObjects/
│   │   ├── Items/
│   │   ├── Recipes/
│   │   ├── Biomes/
│   │   ├── Enemies/
│   │   └── Quests/
│   ├── Scenes/
│   │   ├── Boot.unity
│   │   ├── MainMenu.unity
│   │   └── Game.unity
│   ├── Resources/
│   ├── Editor/                    # Custom editor tools, inspectors
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Packages/
│   └── manifest.json
└── ProjectSettings/
```

---

## TECHNICAL SPECIFICATIONS

### Chunk System
- Chunk size: **32×32×32** voxels
- Coordinate system: Chunk position is (chunkX, chunkY, chunkZ) where world position = chunk position × 32
- Default generation band: Y = -128 to Y = 128 (surface terrain)
- No hard floor or ceiling — chunks generate on demand vertically
- Chunk loading: radial distance from player, horizontal render distance configurable (default 8 chunks = 256 units), vertical render distance configurable (default 4 chunks above/below player)
- Chunks stored in a Dictionary<Vector3Int, Chunk> — NOT a flat array
- Chunk states: Unloaded → Generating → MeshPending → Active → MarkedForUnload → Unloaded

### Marching Cubes — EXTERNAL SOLUTION
**DO NOT WRITE YOUR OWN MARCHING CUBES IMPLEMENTATION.**
**DO NOT MODIFY THE COMPUTE SHADERS.**

We will integrate an existing, proven marching cubes GPU implementation. The recommended repo is Sebastian Lague's Marching Cubes project or a similar MIT-licensed compute shader implementation.

**Integration approach:**
1. Clone/submodule the repo into `Assets/Scripts/World/MarchingCubes/`
2. Create a **wrapper class** `MarchingCubesAdapter.cs` in `Assets/Scripts/World/Chunks/` that:
   - Takes our density field (float[32×32×32] or ComputeBuffer) as input
   - Calls the external marching cubes GPU code
   - Returns the generated Mesh
3. Our code ONLY interacts with the adapter, never the internals
4. If the external code expects a different grid size, pad or resample in the adapter

**Why:** The user has attempted custom marching cubes 7 times. The compute shaders are a solved problem. We use someone else's working solution and focus our effort on gameplay.

### Density Function
The world is defined by a density function `float GetDensity(Vector3 worldPos)`:
- Positive = solid terrain
- Negative = air
- Zero = surface (where marching cubes generates triangles)

Base density: `density = -worldPos.y + surfaceHeight(worldPos.x, worldPos.z)`
- `surfaceHeight` uses layered Perlin/Simplex noise for terrain shape
- 3D noise is added for caves, overhangs, and underground features
- Biome parameters modulate noise amplitude, frequency, and cave density
- Different Y ranges use different noise configurations (see Vertical Zones)

### Vertical Zones (density function behavior by Y range)
| Y Range | Zone | Density Behavior |
|---------|------|-----------------|
| 600+ | High Sky | Sparse floating islands — isolated positive density pockets in mostly-air |
| 200–600 | Low Sky | Wind-carved plateaus, arches, thin floating shelves |
| 128–200 | Upper Surface | Mountain peaks, cliffs, tall terrain features |
| -128 to 128 | Surface Band | Standard terrain — hills, valleys, biome features |
| -128 to -256 | Shallow Underground | Larger caves, first underground biomes, transition zone |
| -256 to -512 | Deep Underground | Mega-caverns, underground lakes, boss lairs |
| -512 to -1000 | Abyssal | Inverted terrain, extreme density, alien geometry |

### Rendering
- Unity 6.3 LTS URP (Universal Render Pipeline)
- Triplanar shader for terrain (no UV unwrapping needed for marching cubes meshes)
- Unity 6.3's Shader Graph terrain material support can be leveraged for biome blending
- Texture arrays for biome-specific terrain materials
- Per-chunk mesh colliders (generated from the same mesh data)
- LOD: distant chunks use simplified meshes (lower marching cubes resolution)

### Input
- Unity's new Input System package
- Input actions defined in a single InputActions asset
- All input goes through action maps: Player, UI, Vehicle, Building

---

## VOLUME 0 — PROJECT BOOTSTRAP (Do this first, manually or as chunk 0)

Before any chunks begin, set up the Unity project:

**Unity Version: 6.3 LTS (6000.3.x)**
**Marching Cubes Repo: https://github.com/gtaharaedmonds/marching-cubes-gpu**

```
1. Create new Unity project using Unity 6.3 LTS (6000.3.x), URP 3D template
2. Install packages via Package Manager:
   - Input System (com.unity.inputsystem)
   - TextMeshPro (com.unity.textmeshpro)
   - Mathematics (com.unity.mathematics)
   - Burst (com.unity.burst)
   - Collections (com.unity.collections)
   - URP is already included in the template
3. Create the folder structure above
4. Clone the marching cubes repo:
   cd Assets/Scripts/World/
   git clone https://github.com/gtaharaedmonds/marching-cubes-gpu.git MarchingCubes
   
   IMPORTANT: The repo structure is:
   - Assets/Resources/ — Contains the compute shaders (MarchingCubes.compute, noise, etc.)
   - Assets/CPU Version/ — CPU fallback (we won't use this)
   - The GPU source lives in Assets/Resources/
   
   After cloning, you need to reorganize:
   - Copy the CONTENTS of the cloned repo's Assets/Resources/ into our project's
     Assets/Scripts/World/MarchingCubes/Resources/
   - Copy relevant C# scripts from the cloned repo into
     Assets/Scripts/World/MarchingCubes/Scripts/
   - The repo was built on an older Unity version — expect potential API deprecation
     warnings. Fix ONLY C# API compatibility issues (e.g., deprecated Unity APIs).
     DO NOT touch compute shader (.compute) files or HLSL code.
   - The repo uses layered 3D noise for terrain and has its own LOD system.
     We will REPLACE the terrain generation (use our DensityFunction) but KEEP
     the marching cubes meshing pipeline (compute shader dispatch → mesh).
     
5. Create Boot.unity scene with a GameBootstrapper MonoBehaviour that initializes singletons
6. Create the Game.unity scene (empty, this is where gameplay happens)
7. Verify the project compiles with zero errors in Unity 6.3 LTS
   - Check Console for compute shader compilation errors
   - If the MC repo's compute shaders fail, check Unity 6.3 compatibility
   - Common fix: update any legacy shader syntax but DO NOT rewrite the algorithm
8. Commit to git

UNITY 6.3 NOTES:
- Unity 6.3 uses URP by default in the 3D template
- Shader Graph now supports terrain materials natively — we can use this for biome shaders
- The new Input System is the default (legacy input is deprecated)
- Burst 1.8+ and Collections 2.x are compatible
- ComputeShader API is stable and unchanged from previous versions
```

---

## VOLUME 1 — CORE WORLD & PLAYER (Chunks 1.1–1.8)

The goal of Volume 1 is: walk around on smooth, infinite, deformable marching cubes terrain with biomes. By the end, you can explore a generated world in first person.

### Chunk 1.1 — Density Function & Noise Utilities
**Files to create:**
- `Assets/Scripts/World/Generation/NoiseUtilities.cs` — Static class wrapping Unity.Mathematics Simplex/Perlin noise. Methods: `float Noise2D(float2 pos, float frequency, int octaves, float persistence, float lacunarity)`, `float Noise3D(float3 pos, ...)`, `float RidgedNoise2D(...)`, `float DomainWarp2D(...)`.
- `Assets/Scripts/World/Generation/DensityFunction.cs` — Static class with `float GetDensity(float3 worldPos, BiomeData biome)`. Implements the base terrain height using 2D noise, adds 3D noise for caves/overhangs. Takes biome parameters to modulate behavior.
- `Assets/Scripts/World/Generation/WorldSeed.cs` — Static class holding the world seed int, with offset methods for deterministic seeding.
- `Assets/Tests/EditMode/DensityFunctionTests.cs` — Unit tests: density is positive deep underground, negative high in the air, surface exists in the expected Y range, caves create negative pockets underground.

**Acceptance criteria:**
- All tests pass in Edit Mode test runner
- No compilation errors
- DensityFunction.GetDensity returns sensible values for sample positions

### Chunk 1.2 — Chunk Data Structure & Chunk Manager
**Files to create:**
- `Assets/Scripts/World/Chunks/ChunkData.cs` — Class holding: `Vector3Int chunkPosition`, `float[] densityField` (32×32×32 = 32768 floats), `ChunkState state` enum, `Mesh mesh`, `bool isDirty`. Method: `float GetDensity(int x, int y, int z)`, `void SetDensity(int x, int y, int z, float value)`, `Vector3 WorldPosition` property.
- `Assets/Scripts/World/Chunks/ChunkState.cs` — Enum: `Unloaded, Generating, MeshPending, Active, MarkedForUnload`.
- `Assets/Scripts/World/Chunks/ChunkManager.cs` — MonoBehaviour singleton. Holds `Dictionary<Vector3Int, ChunkData> chunks`. Methods: `ChunkData GetChunk(Vector3Int pos)`, `ChunkData GetOrCreateChunk(Vector3Int pos)`, `void LoadChunksAroundPlayer(Vector3 playerPos)`, `void UnloadDistantChunks(Vector3 playerPos)`. Config fields: `int horizontalRenderDistance = 8`, `int verticalRenderDistance = 4`.
- `Assets/Scripts/World/Chunks/ChunkCoordUtility.cs` — Static helper: `Vector3Int WorldToChunkPos(Vector3 worldPos)`, `Vector3 ChunkToWorldPos(Vector3Int chunkPos)`, `Vector3Int LocalToWorld(Vector3Int chunkPos, int x, int y, int z)`.
- `Assets/Tests/EditMode/ChunkCoordTests.cs` — Tests for coordinate conversion round-trips.

**Acceptance criteria:**
- ChunkManager can be placed in a scene, starts without errors
- Coordinate conversion is correct and tested
- No compilation errors

### Chunk 1.3 — Marching Cubes Adapter & Mesh Generation
**Files to create:**
- `Assets/Scripts/World/Chunks/MarchingCubesAdapter.cs` — Wrapper class that takes a `ChunkData` (its density field), feeds it to the external marching cubes compute shader, and returns a `Mesh`. This is the ONLY file that touches the external MC code. Handle edge sampling: when the marching cubes algorithm needs density values at the chunk boundary (position 32), sample from the adjacent chunk's density field via ChunkManager.
- `Assets/Scripts/World/Chunks/ChunkMeshBuilder.cs` — Takes a ChunkData, calls MarchingCubesAdapter, assigns the mesh to the chunk's MeshFilter and MeshCollider. Runs on a background thread or async where possible (density fill on thread, GPU meshing on main thread).
- `Assets/Scripts/World/Chunks/ChunkRenderer.cs` — MonoBehaviour attached to each chunk GameObject. Holds references to MeshFilter, MeshRenderer, MeshCollider. Method: `void ApplyMesh(Mesh mesh)`.

**Acceptance criteria:**
- Place ChunkManager in scene, it generates chunk GameObjects with visible meshes
- Terrain is smooth (marching cubes working correctly)
- No holes between adjacent chunks (edge sampling works)
- Can see terrain from the default camera

### Chunk 1.4 — Chunk Loading/Unloading Around Player
**Files to create:**
- `Assets/Scripts/World/Chunks/ChunkLoader.cs` — MonoBehaviour that every N frames checks the player's chunk position, compares to loaded chunks, queues new chunks for generation, and marks far chunks for unloading. Uses a priority queue — chunks closest to the player generate first. Rate-limits chunk generation to N per frame to avoid hitches.
- `Assets/Scripts/World/Chunks/ChunkPool.cs` — Object pool for chunk GameObjects. Reuses deactivated chunks instead of Instantiate/Destroy.
- Modify `ChunkManager.cs` — Integrate with ChunkLoader and ChunkPool.

**Acceptance criteria:**
- Move the camera (or a placeholder player) around — new chunks appear, old ones disappear
- No frame rate drops below 30fps during chunk loading (test with profiler)
- Memory stays stable (chunks are pooled, not leaked)

### Chunk 1.5 — Biome System (Surface Biomes)
**Files to create:**
- `Assets/Scripts/ScriptableObjects/Biomes/BiomeData.cs` — ScriptableObject: biome name, color tint, noise amplitude, noise frequency, cave density, cave threshold, surface material index, min/max height, temperature range, moisture range.
- `Assets/Scripts/World/Generation/BiomeMap.cs` — Given a world (x,z) position, returns the BiomeData for that location. Uses 2D Voronoi or temperature/moisture noise to assign biomes. Blends between adjacent biomes at boundaries using weighted interpolation of density parameters.
- Create 4 starter biome assets: `Lowlands.asset`, `Badlands.asset`, `FrozenPeaks.asset`, `FungalMarshes.asset` (as ScriptableObject instances in `Assets/ScriptableObjects/Biomes/`).
- Modify `DensityFunction.cs` — Accept biome from BiomeMap and modulate noise parameters accordingly.

**Acceptance criteria:**
- Terrain visually changes between biome regions (different heights, different shapes)
- Biome transitions are smooth, no hard edges
- At least 4 distinct biome regions visible when flying around

### Chunk 1.6 — Triplanar Terrain Shader & Biome Materials
**Files to create:**
- `Assets/Shaders/TriplanarTerrain.shader` — URP-compatible shader that samples a texture array using triplanar projection. Vertex data includes a biome index (or blend weights for up to 4 biomes). Samples the appropriate textures from the array based on biome index.
- `Assets/Materials/TerrainMaterial.mat` — Material using the above shader.
- Modify `ChunkMeshBuilder.cs` — Encode biome data into mesh vertex colors or UV channels so the shader knows which textures to use per vertex.

**Acceptance criteria:**
- Terrain has distinct textures per biome (even placeholder colors)
- No UV seams (triplanar working)
- Biome transitions blend smoothly in the shader

### Chunk 1.7 — First Person Player Controller
**Files to create:**
- `Assets/Scripts/Player/PlayerController.cs` — FPS character controller. Uses Unity's CharacterController component. WASD movement, mouse look, jump, sprint, crouch. Smooth camera. Ground check via spherecast. Gravity. Sprint drains a stamina float (regenerates when not sprinting).
- `Assets/Scripts/Player/PlayerCamera.cs` — Handles mouse look with sensitivity setting, vertical clamp, smooth interpolation.
- `Assets/Scripts/Player/InputActions.inputactions` — Unity Input System actions asset: Move (Vector2), Look (Vector2), Jump (Button), Sprint (Button), Crouch (Button), PrimaryAction (Button), SecondaryAction (Button), Interact (Button), Inventory (Button), Scroll (Axis).
- `Assets/Scripts/Player/PlayerInput.cs` — Reads from InputActions, exposes clean C# properties/events for other scripts.
- `Assets/Prefabs/Player.prefab` — Player with CharacterController, PlayerController, PlayerCamera, Camera, PlayerInput.

**Acceptance criteria:**
- Drop Player prefab into Game scene, press Play, walk around on terrain
- Movement is smooth, responsive, and feels good
- Mouse look is smooth with no jitter
- Jump works, gravity works, collision with terrain works
- Sprint/crouch work

### Chunk 1.8 — Terrain Deformation
**Files to create:**
- `Assets/Scripts/World/Chunks/TerrainDeformer.cs` — Static utility: `void ModifyTerrain(Vector3 worldPos, float radius, float strength)`. Finds all affected chunks, modifies their density fields (add = build, subtract = dig), marks them dirty for remeshing.
- `Assets/Scripts/Player/PlayerTerrainInteraction.cs` — MonoBehaviour on the player. On left click, raycast forward, call TerrainDeformer to dig. On right click, build. Visual feedback: show a sphere gizmo preview of the deformation radius.
- Modify `ChunkManager.cs` — Dirty chunks get re-meshed (density field → adapter → new mesh). Rate-limit remeshing to avoid hitches.

**Acceptance criteria:**
- Left click digs smooth holes in terrain
- Right click builds terrain back up
- Deformation works across chunk boundaries seamlessly
- Remeshing is fast enough to feel responsive (< 100ms per chunk)
- Modified terrain persists as long as the chunk is loaded

---

## VOLUME 2 — INVENTORY, ITEMS & CRAFTING (Chunks 2.1–2.6)

Goal: Pick up items, manage an inventory, craft on grids like Minecraft.

### Chunk 2.1 — Item System Foundation
**Files to create:**
- `Assets/Scripts/Inventory/ItemDefinition.cs` — ScriptableObject: item ID (string), display name, description, icon (Sprite), max stack size, item type enum (Resource, Tool, Weapon, Armor, Consumable, Block, Machine, Backpack), 3D model prefab reference, weight.
- `Assets/Scripts/Inventory/ItemStack.cs` — Serializable struct: ItemDefinition reference, int quantity. Methods: `bool CanStackWith(ItemStack other)`, `ItemStack Split(int amount)`.
- `Assets/Scripts/Inventory/ItemDatabase.cs` — ScriptableObject that holds a list of all ItemDefinitions. Singleton-accessible. Method: `ItemDefinition GetItem(string id)`.
- Create 10 starter item assets: `Wood.asset`, `Stone.asset`, `IronOre.asset`, `IronIngot.asset`, `Coal.asset`, `CopperOre.asset`, `CopperIngot.asset`, `Stick.asset`, `WoodPlanks.asset`, `Torch.asset`.

**Acceptance criteria:**
- ItemDatabase loads and can look up items by ID
- ItemStack operations work correctly (stacking, splitting)
- No compilation errors

### Chunk 2.2 — Inventory System
**Files to create:**
- `Assets/Scripts/Inventory/Inventory.cs` — Class (not MonoBehaviour) representing a grid of ItemStack slots. Constructor takes width × height. Methods: `bool AddItem(ItemStack stack)` (auto-find slot), `ItemStack GetSlot(int index)`, `void SetSlot(int index, ItemStack stack)`, `bool RemoveItem(string itemId, int quantity)`, `int CountItem(string itemId)`, `event OnInventoryChanged`.
- `Assets/Scripts/Inventory/PlayerInventory.cs` — MonoBehaviour on the player. Holds: Inventory hotbar (9×1), Inventory main (9×3), equipped backpack slot. The backpack adds an additional Inventory of the backpack's size.
- `Assets/Scripts/Inventory/BackpackItem.cs` — Extends ItemDefinition with: int extraRows, int extraColumns.

**Acceptance criteria:**
- Can programmatically add/remove items from inventory
- Backpack extends available slots
- Events fire on changes

### Chunk 2.3 — Inventory UI
**Files to create:**
- `Assets/Scripts/UI/InventoryUI.cs` — MonoBehaviour. Renders the inventory as a grid of slots. Each slot shows the item icon and stack count. Supports click-to-pick-up, click-to-place, shift-click to quick-move, right-click to split stack. Drag and drop between slots.
- `Assets/Scripts/UI/HotbarUI.cs` — Always-visible bottom bar showing 9 hotbar slots. Scroll wheel or 1-9 keys to select active slot. Selected slot is highlighted.
- `Assets/Scripts/UI/SlotUI.cs` — Individual slot component. Handles hover tooltip, click events, drag source/target.
- `Assets/Scripts/UI/TooltipUI.cs` — Floating panel showing item name, description, stats on hover.
- `Assets/Prefabs/UI/InventoryCanvas.prefab` — The UI prefab.

**Acceptance criteria:**
- Press Tab/I to open inventory
- Items display correctly with icons and counts
- Drag and drop works between all slots
- Shift-click quick-moves items
- Hotbar is always visible during gameplay
- Mouse is locked during gameplay, unlocked when inventory is open

### Chunk 2.4 — World Items & Pickup
**Files to create:**
- `Assets/Scripts/Inventory/WorldItem.cs` — MonoBehaviour. A dropped item in the world. Has an ItemStack, a 3D model (or default cube), bobbing animation, and a trigger collider. Player walks over it → picks up into inventory.
- `Assets/Scripts/Inventory/WorldItemSpawner.cs` — Static utility: `void SpawnItem(ItemStack stack, Vector3 position)`. Creates a WorldItem in the scene.
- Modify `PlayerTerrainInteraction.cs` — When digging terrain, spawn appropriate resource WorldItems (stone from stone, dirt from dirt, ore from ore veins).

**Acceptance criteria:**
- Digging terrain drops items on the ground
- Walking over items picks them up into inventory
- Items bob and rotate in the world
- Full inventory → items stay on the ground

### Chunk 2.5 — Crafting System
**Files to create:**
- `Assets/Scripts/Crafting/CraftingRecipe.cs` — ScriptableObject: a 2D array of ItemDefinition references representing the pattern (null = empty slot), result ItemStack, grid size required (2×2 or 3×3 or 5×5).
- `Assets/Scripts/Crafting/CraftingGrid.cs` — Class: a grid of ItemStack slots (like Inventory but for crafting). Method: `CraftingRecipe CheckRecipe()` — compares the current grid contents against all known recipes. Supports mirrored recipes (left-right flip matches too).
- `Assets/Scripts/Crafting/CraftingManager.cs` — Singleton holding all loaded CraftingRecipes. Method: `CraftingRecipe FindMatch(ItemStack[,] grid, int gridSize)`.
- `Assets/Scripts/Crafting/PersonalCraftingGrid.cs` — The player's always-available 2×2 grid.
- Create 5 starter recipes: `WoodToWoodPlanks` (1 wood → 4 planks), `WoodPlanksToSticks` (2 planks vertical → 4 sticks), `WoodenPickaxe` (3 planks top + 2 sticks down), `Torch` (1 coal + 1 stick vertical), `Workbench` (4 planks in 2×2).

**Acceptance criteria:**
- Open 2×2 crafting grid from inventory screen
- Place items in pattern, result appears in output slot
- Crafting consumes inputs and gives output
- Recipes work mirrored
- All 5 starter recipes function

### Chunk 2.6 — Workbench & Crafting Stations
**Files to create:**
- `Assets/Scripts/Crafting/CraftingStation.cs` — MonoBehaviour. Placed in the world. Has a grid size (3×3 for workbench, 5×5 for engineering table). Player interacts (E key) to open the crafting UI with the larger grid.
- `Assets/Scripts/UI/CraftingUI.cs` — Extends the inventory UI with a crafting grid panel and output slot. Adapts to the grid size of the station being used.
- `Assets/Prefabs/Workbench.prefab` — 3D model (placeholder cube with wood texture), CraftingStation component set to 3×3.
- Create 5 more recipes requiring 3×3: `StoneFurnace`, `IronPickaxe`, `IronSword`, `WoodenDoor`, `Chest`.

**Acceptance criteria:**
- Place workbench in world (from hotbar)
- Press E to open 3×3 crafting grid
- Larger recipes that don't fit in 2×2 work in the 3×3
- UI shows the correct grid size per station

---

## VOLUME 3 — RESOURCES, ORES & SMELTING (Chunks 3.1–3.4)

Goal: Ores embedded in terrain, mining drops ore items, furnaces smelt them.

### Chunk 3.1 — Ore Generation in Density Field
**Files to create:**
- `Assets/Scripts/World/Generation/OreGenerator.cs` — For each chunk during generation, determines which voxels contain ore using 3D noise seeded per ore type. Each ore has: min/max Y range, noise frequency, threshold (rarity), biome restriction (optional). Stores ore data in `ChunkData` as a separate `byte[] oreField` (32³), where each byte is an ore type ID (0 = no ore).
- `Assets/Scripts/World/Generation/OreDefinition.cs` — ScriptableObject: ore type ID (byte), name, associated ItemDefinition, color tint for shader, min Y, max Y, noise frequency, noise threshold, biome restriction.
- Modify `ChunkData.cs` — Add `byte[] oreField`.
- Create ore definition assets: Iron, Copper, Coal, Gold, Titanium, Diamond (with appropriate Y ranges).

**Acceptance criteria:**
- Ore veins are visible in terrain (colored patches)
- Ore distribution respects Y ranges (no diamonds at surface)
- Each biome's ores generate correctly

**Implementation note (2026-03-16):** Ore generation was moved off the main thread via Unity's Job System + Burst. `OreGenerationJob` (`IJobParallelFor`, `[BurstCompile]`) and blittable `OreParams` struct were added to `OreGenerator.cs`. `OreGenerator.ScheduleAsync()` schedules the job and returns a `JobHandle` plus three `NativeArray`s for later disposal. `ChunkManager` was updated: the `LoadChunk` callback now calls `ScheduleAsync` instead of the synchronous `GenerateOres`; results are polled each frame in `ProcessPendingOreJobs()` (called from `Update()`), which copies `OreField` data back and sets `ChunkState.Active` once the job completes. `OnDestroy` completes and disposes any in-flight jobs. `Unity.Jobs` reference added to `Voidborne.asmdef`.

### Chunk 3.2 — Ore Rendering in Terrain Shader
**Modify:**
- `TriplanarTerrain.shader` — Add ore overlay. The ore field data is passed per-chunk as vertex data or a small 3D texture. Ore voxels blend an ore color/texture over the base biome texture.

**Acceptance criteria:**
- Ore veins are visually distinct from regular terrain
- Different ores have different visual appearances
- Ore blends smoothly at edges

### Chunk 3.3 — Mining Tools & Ore Drops
**Files to create:**
- `Assets/Scripts/Player/PlayerMining.cs` — Replaces/extends the basic terrain deformation for mining. Raycast from camera, if hitting terrain, check ore field at that position. Hold left click to mine (progress bar fills based on tool tier vs rock hardness). When complete, modify density, spawn ore WorldItem, clear ore field at that position.
- `Assets/Scripts/Inventory/ToolDefinition.cs` — Extends ItemDefinition: tool type (pickaxe, axe, shovel), tool tier (wood, stone, iron, titanium, void), mining speed multiplier, durability.
- Modify `PlayerTerrainInteraction.cs` — Use equipped tool's tier to determine mining speed and what ores can be mined.

**Acceptance criteria:**
- Equip pickaxe in hotbar, look at terrain, hold click to mine
- Progress bar shows mining progress
- Ore drops as items when mined
- Higher tier pickaxes mine faster
- Tools lose durability

### Chunk 3.4 — Furnace & Smelting
**Files to create:**
- `Assets/Scripts/Automation/FurnaceBlock.cs` — MonoBehaviour. Placed in world. Has input slot, fuel slot, output slot. When fueled and loaded, smelts over time. Produces: Iron Ore → Iron Ingot, Copper Ore → Copper Ingot, etc.
- `Assets/Scripts/Automation/SmeltingRecipe.cs` — ScriptableObject: input item, output item, smelt time in seconds.
- `Assets/Scripts/UI/FurnaceUI.cs` — UI panel with input/fuel/output slots and a progress bar.
- `Assets/Prefabs/Furnace.prefab` — Placeholder model, FurnaceBlock component.
- Create smelting recipes: IronOre→IronIngot, CopperOre→CopperIngot, RawMeat→CookedMeat, Sand→Glass.

**Acceptance criteria:**
- Place furnace in world
- Put ore in input, coal in fuel
- Smelting progress bar fills
- Output appears when done
- Fuel is consumed

---

## VOLUME 4 — COMBAT: GUNS (Chunks 4.1–4.5)

Goal: Snappy CS2/Valorant-style hitscan gunplay.

### Chunk 4.1 — Weapon System Foundation
**Files to create:**
- `Assets/Scripts/Combat/Guns/GunDefinition.cs` — ScriptableObject: gun name, fire rate (rounds per minute), damage, magazine size, reload time, recoil pattern (Vector2[]), spread (standing, moving, crouching, airborne), fire mode (auto, semi, burst), bullet type (hitscan, projectile), range, penetration, ADS zoom multiplier, equip time, 1P model prefab, 3P model prefab, muzzle flash prefab, fire sound, reload sound.
- `Assets/Scripts/Combat/Guns/GunInstance.cs` — Runtime state: GunDefinition ref, current ammo, current recoil index, recoil recovery timer, is reloading.
- `Assets/Scripts/Combat/DamageSystem.cs` — Interface `IDamageable` with `void TakeDamage(DamageInfo info)`. `DamageInfo` struct: float amount, Vector3 hitPoint, Vector3 hitNormal, DamageType enum (Bullet, Melee, Projectile, Explosive, Fire, Poison), attacker GameObject.

**Acceptance criteria:**
- ScriptableObjects can be created in editor
- DamageSystem interface exists and compiles
- No errors

### Chunk 4.2 — Gun Mechanics: Shooting, Recoil, Spread
**Files to create:**
- `Assets/Scripts/Combat/Guns/GunController.cs` — MonoBehaviour on the player. Handles: fire input → hitscan raycast (for hitscan guns), apply recoil pattern (move camera up/sideways per the defined pattern), calculate spread based on player movement state, consume ammo, play muzzle flash, play sound. Recoil recovers over time when not firing. First-shot accuracy is perfect when standing still.
- `Assets/Scripts/Combat/Guns/RecoilSystem.cs` — Manages the recoil pattern playback. Each shot advances the pattern index. Pattern is a sequence of Vector2 (x = horizontal, y = vertical camera offset). Recovery pulls the camera back toward center at a defined rate.
- `Assets/Scripts/Combat/Guns/SpreadCalculator.cs` — Static: calculates spread cone based on player state. Standing still = minimum spread. Walking = moderate. Running = high. Airborne = maximum. Crouching = reduced. Spread is applied as a random offset within a cone before the raycast.

**Acceptance criteria:**
- Equip a gun, click to fire
- Recoil moves the camera in a learnable pattern
- Standing still = accurate, moving = less accurate
- Full auto spraying follows the recoil pattern consistently
- Feels tight and responsive (instant hitscan, no delay)

### Chunk 4.3 — Gun Mechanics: Reload, ADS, Switching
**Files to create:**
- `Assets/Scripts/Combat/Guns/ReloadSystem.cs` — Handles reload animation timing, ammo transfer from reserve to magazine, interrupt reload on weapon switch.
- `Assets/Scripts/Combat/Guns/ADSController.cs` — Right click to aim down sights. Smoothly zooms FOV, reduces spread, may slow movement. ADS sensitivity scaling.
- `Assets/Scripts/Combat/Guns/WeaponSwitcher.cs` — Manages equipped weapon slots (2 guns + melee). Scroll wheel or 1-3 to switch. Equip animation time from GunDefinition.

**Acceptance criteria:**
- Press R to reload, animation plays, ammo replenishes
- Right click ADS zooms in, improves accuracy
- Can switch between 2 guns smoothly
- All transitions feel snappy

### Chunk 4.4 — Hit Detection & Feedback
**Files to create:**
- `Assets/Scripts/Combat/Guns/HitDetection.cs` — Processes hitscan raycasts. Checks for headshot (tagged colliders), body, limbs. Applies damage multipliers. Spawns hit effects (blood particles on enemies, dust/sparks on terrain/metal).
- `Assets/Scripts/Combat/Guns/HitEffects.cs` — Pools and spawns impact VFX and decals based on surface type.
- `Assets/Scripts/UI/HitmarkerUI.cs` — Crosshair hitmarker that flashes on hit, changes color on kill.
- `Assets/Scripts/UI/CrosshairUI.cs` — Dynamic crosshair that expands with spread, contracts when still. Accurate visual representation of current accuracy.

**Acceptance criteria:**
- Shoot terrain → dust particles, bullet hole decal
- Shoot enemy → blood particles, hitmarker on crosshair
- Kill enemy → kill confirmed hitmarker
- Crosshair dynamically reflects current spread
- Headshots do extra damage

### Chunk 4.5 — Starter Guns & Balancing
**Create gun definition assets:**
- `HandmadeRevolver.asset` — Semi-auto, 6 rounds, slow fire rate, high first-shot accuracy, moderate damage. The starter sidearm.
- `Rattler_SMG.asset` — Full auto, 30 rounds, high fire rate, fast recoil pattern, moderate spread, low-medium damage per shot.
- `Ironbark_AR.asset` — Full auto, 25 rounds, medium fire rate, pull-down-left recoil pattern, good range, medium damage.
- `PumpShotgun.asset` — Pump action, 6 shells, fires multiple pellets per shot, devastating close range.
- `BoltSniper.asset` — Bolt action, 5 rounds, very high damage, high ADS zoom, scope sway mechanic.
- Create placeholder 3D models (cubes/primitives with distinct shapes) for each.

**Acceptance criteria:**
- All 5 guns are playable
- Each feels distinct
- Recoil patterns are learnable
- Shotgun fires pellets (multiple raycasts per shot)
- Sniper has scope sway that steadies when holding breath (stamina drain)

---

## VOLUME 5 — COMBAT: MELEE (Chunks 5.1–5.4)

Goal: Mordhau-style directional melee with parries, feints, morphs, chambers.

### Chunk 5.1 — Directional Attack System
**Files to create:**
- `Assets/Scripts/Combat/Melee/MeleeDefinition.cs` — ScriptableObject: weapon name, damage, attack speed, range (float), stamina cost per swing, windup time, release time, recovery time, combo chains, 1P model, attack animations per direction.
- `Assets/Scripts/Combat/Melee/MeleeController.cs` — MonoBehaviour. Mouse movement before click determines attack direction (left, right, overhead, stab) based on mouse delta. Click initiates: Windup → Release → Recovery phases. During Release, a box/sphere cast checks for hits in an arc matching the swing direction.
- `Assets/Scripts/Combat/Melee/AttackDirection.cs` — Enum: Left, Right, Overhead, Stab. Static method to determine direction from mouse delta Vector2.

**Acceptance criteria:**
- Swing melee weapon in 4 directions based on mouse movement
- Attack has visible windup, active, recovery phases
- Hits register on enemies with correct damage
- Feels weighty and intentional, not spammy

### Chunk 5.2 — Parry, Riposte & Block
**Files to create:**
- `Assets/Scripts/Combat/Melee/ParrySystem.cs` — Right click initiates a parry. Active parry window is ~200ms. If an incoming attack connects during the parry window, it's deflected: attacker is staggered (brief stun), defender can immediately riposte (attack comes out faster). Parry must match the rough direction of the incoming attack (left parries right attacks, etc.) or be within a forgiveness cone.
- `Assets/Scripts/Combat/Melee/MeleeStagger.cs` — Stagger state: character can't attack or parry for a duration. Visual feedback: screen shake, weapon drops slightly.
- Modify `MeleeController.cs` — Integrate parry input, riposte window, stagger state.

**Acceptance criteria:**
- Right click at the right time deflects an attack
- Failed parry (wrong timing or direction) leaves you open
- Riposte after successful parry is visibly faster
- Stagger is punishing but not so long it feels unfair

### Chunk 5.3 — Feints, Morphs & Chambers
**Files to create:**
- `Assets/Scripts/Combat/Melee/FeintSystem.cs` — During windup phase, press right click to feint (cancel the attack). Costs stamina. The opponent sees your windup animation start but it doesn't complete. Morph: during windup, change attack direction (start overhead, morph to stab) by moving the mouse. The animation blends to the new direction. Chamber: if you start an attack that exactly mirrors an incoming attack's direction during the opponent's release phase, your attack deflects theirs and continues. Requires precise timing and direction matching.
- Modify `MeleeController.cs` — Integrate feint (cancel during windup), morph (direction change during windup), chamber (detect matching incoming attack).

**Acceptance criteria:**
- Feinting cancels an attack visibly during windup
- Morphing changes attack direction mid-windup with animation blend
- Chambering deflects and counters simultaneously
- All three mechanics cost appropriate stamina
- AI enemies can be tricked by feints

### Chunk 5.4 — Melee Weapons & Kick
**Files to create:**
- `Assets/Scripts/Combat/Melee/KickAbility.cs` — Middle mouse button (or F key). Short range, fast, low damage but breaks a held block/parry stance, staggers for a punish window.
- Create melee weapon assets:
  - `WoodenClub.asset` — Slow, decent damage, starter weapon. Easy to read swings.
  - `IronDagger.asset` — Fast, short range, low damage per hit, but fast combos.
  - `IronLongsword.asset` — Balanced speed, range, damage. The standard.
  - `TitaniumSpear.asset` — Long range, stab-focused, slower slashes.
  - `VoidWarhammer.asset` — Slow, massive damage, ignores 30% armor, huge stagger on hit.

**Acceptance criteria:**
- Kick breaks blocks and creates openings
- All 5 melee weapons are playable with distinct feels
- Fast weapons allow more feint/morph mixups
- Slow weapons punish harder when they land
- Spear has noticeably longer reach

---

## VOLUME 6 — COMBAT: PROJECTILES & ENEMIES (Chunks 6.1–6.4)

Goal: Skill-based bows/thrown weapons with real physics, and enemies to fight.

### Chunk 6.1 — Projectile Physics System
**Files to create:**
- `Assets/Scripts/Combat/Projectiles/ProjectileDefinition.cs` — ScriptableObject: projectile name, base damage, velocity, gravity multiplier, drag, lifetime, model prefab, trail effect, impact effect, is retrievable, penetration depth.
- `Assets/Scripts/Combat/Projectiles/Projectile.cs` — MonoBehaviour. On spawn, given a velocity vector. Each FixedUpdate: apply gravity (velocity.y -= gravity * dt), apply drag, move forward, raycast along movement vector for collisions. On hit: apply damage (scaled by velocity magnitude at impact), spawn impact effect, embed in surface or bounce based on config.
- `Assets/Scripts/Combat/Projectiles/ProjectilePool.cs` — Object pool for projectiles.

**Acceptance criteria:**
- Projectiles arc realistically under gravity
- Impact damage scales with velocity at time of hit
- Projectiles embed in terrain/enemies
- Pool recycles projectiles correctly

### Chunk 6.2 — Bow & Thrown Weapons
**Files to create:**
- `Assets/Scripts/Combat/Projectiles/BowController.cs` — Hold right click to draw. Draw time affects velocity (and therefore range and damage). Visual: bow bends, string pulls back. Release to fire. Stamina drains slowly while drawn. If drawn too long, accuracy wobbles. Crosshair shows a range indicator that shifts as draw progresses.
- `Assets/Scripts/Combat/Projectiles/ThrowController.cs` — For spears and axes. Hold attack to wind up (brief), release to throw. Thrown weapons spin based on weapon type (axe spins, spear doesn't). Thrown items can be picked up from where they land.
- Create weapon assets: `ShortBow.asset`, `LongBow.asset`, `Crossbow.asset`, `ThrowingSpear.asset`, `ThrowingAxe.asset`.

**Acceptance criteria:**
- Draw bow, arrow arcs, hits target at range
- Full draw = more damage and range than partial
- Thrown spear travels straight and far, embeds in targets
- Thrown axe spins, distance affects blade-vs-handle hit
- All projectiles are retrievable from the world

### Chunk 6.3 — Enemy AI Foundation ✓ COMPLETE
Enemy types in Voidborne are The Kin — the intelligent beings the Architect created to steward the world. Most have had their core directives overwritten by VORD. There are two categories: **The Directed** (partially overwritten, retain personality fragments, can speak, still dangerous) and **The Optimized** (fully processed, no personality, pure function). Both are tragic rather than monstrous — they are victims of VORD's corruption, not inherently evil.

> **Implementation note — no NavMesh:** NavMesh baking on marching-cubes terrain is impractical (dynamic terrain, deformation, runtime chunk loading all invalidate bakes immediately). The navigation layer uses grounded steering + a background lidar sensor instead. See `EnvironmentSensor.cs` and `EnemyNavigation.cs`.

**Files created:**
- `Assets/Scripts/Enemies/EnemyDefinition.cs` — ScriptableObject: enemy name, `EnemyCategory` enum (Optimized/Directed/WildCreature), health, move speed, attack damage, attack range, detection range, armor (flat reduction), loot table (`List<LootEntry>` with per-entry drop chance), biome restrictions, min/max Y depth, `canBeRestored` (Directed only), `converseLines[]` (spoken before attacking), prefab reference.
- `Assets/Scripts/Enemies/EnemyBase.cs` — MonoBehaviour. Implements `IDamageable`. Armor-reduced damage, loot drop on death, Directed "release" animation. Full state machine: Idle → Patrol → Alert → Chase → Attack → Converse → Flee → Dead. Group alert (Optimized: one detects, all nearby react). Converse state triggers if player is unarmed and within half detection range. Chase behaviour is personality-differentiated: Optimized flanks, Directed hesitates/circles, Wild charges erratically.
- `Assets/Scripts/Enemies/EnvironmentSensor.cs` — **Background lidar.** 26-ray Fibonacci hemisphere via `RaycastCommand.ScheduleBatch` (Unity job-system worker threads — zero main-thread cost). Staggered per-enemy by `instanceID % scanInterval` so N enemies spread scans across N frames. Produces: cover candidates (behind obstacle faces), open direction list, nearest obstacle per direction. Main-thread queries (`GetBestCoverPoint`, `GetOpenDirections`, `NearestObstacleDistance`) are O(1) reads of cached data. Full `Dispose()` for `NativeArray` cleanup.
- `Assets/Scripts/Enemies/EnemyNavigation.cs` — **No NavMesh.** Three stacked layers: (1) grounded steering — raycast down, project velocity onto terrain slope normal; (2) 5-ray obstacle fan deflection; (3) separation force scaled by personality. Personality profiles (`Optimized`, `Directed`, `WildCreature`) are plain structs with speed, decisiveness, erratic chance, cover urgency, and spread multiplier — injected at start, no per-frame branching. Behaviour modes: `MoveTo`, `SeekCover`, `Flank`, `Suppress`. Stuck detection with sensor-guided recovery.
- `Assets/Scripts/Enemies/EnemySpawner.cs` — Coroutine spawn loops per `SpawnRule`. Off-screen check (dot product vs camera forward). Biome filtering via `BiomeMap.GetBiome()`. Y-depth range. Population cap. Group spawning (Optimized: `groupSizeMin`–`groupSizeMax`, Directed: 1–2, Wild: per definition).
- `Assets/Editor/EnemySetup.cs` — `Voidborne > Setup Enemies (Vol 6.3)` menu. Creates EnemyManager + EnemySpawner, adds NavMeshSurface to terrain root (for optional manual bake), creates placeholder prefab and sample OptimizedPatrolDefinition asset.
- `Assets/Scripts/Combat/Guns/WeaponSwitcher.cs` — Added `HasActiveWeapon` property (used by Converse-state unarmed check).

**Acceptance criteria — all met:**
- Enemies spawn in the world based on biome and Y depth
- They patrol, detect the player at range, chase and attack
- They take damage, have hit reactions, and die with loot drops
- Directed type enemies attempt to speak if approached without weapons
- They navigate marching-cubes terrain without baking — grounded steering tracks slopes naturally
- Optimized types find cover when fleeing; Directed types panic-suppress; Wild types flee
- Movement personality is visibly distinct between the three categories

### Chunk 6.4 — Core Enemy Types
This chunk implements the minimum enemy set needed to test combat, loot, and AI systems. The full enemy roster — fauna, humanoid factions, cave creatures, sky fauna, and the Residue — is deferred to Volume 11 (Polish) once all core features are working. Focus here is on validating the EnemyBase/EnemyNavigation/EnemySpawner systems with a representative spread.

**The Optimized (fully processed Kin, no personality):**
- `OptimizedPatrol` — Standard VORD foot soldier. Surface and underground biomes. Groups of 3–5, shared detection state (one spots you, all react). Melee focused, efficient, no wasted movement. Drops: Directive Shard, basic components. Asset: `OptimizedPatrolDefinition.asset`.
- `OptimizedHeavy` — Armored variant. Slower, high health, resistant to small arms. Weak point on back (Shard Index module reveals it). Guards VORD infrastructure and strongholds. Drops: Reinforced Plating, Cortex Components. Asset: `OptimizedHeavyDefinition.asset`.
- `OptimizedRanged` — Handheld energy weapon. Takes cover, repositions, flanks. Found in VORD outposts. Drops: Energy Cell, Weapon Components. Asset: `OptimizedRangedDefinition.asset`.

**The Directed (partially overwritten, retain personality fragments):**
- `DirectedSentinel` — Former community guardian. Patrols routes that no longer serve a purpose. Issues verbal warning if approached unarmed before attacking. Temporarily redirectable by Directive Pen module. Drops: Fractured Memory Shard (readable lore text), standard materials. Asset: `DirectedSentinelDefinition.asset`.
- `DirectedCrafter` — Former maker. Now repairs Optimized units in the field and constructs VORD structures. Priority kill target. Echo Lens shows original craft directives briefly. Drops: Schematic Fragment (examine at Terminal to unlock recipes), rare components. Asset: `DirectedCrafterDefinition.asset`.

**One Wild Creature (validates non-Kin AI path):**
- `CaveStalker` — Underground quadruped predator. Not corrupted — just hungry. Ambushes from ceiling surfaces, fast, low health. Drops: Chitin, Raw Meat. Asset: `CaveStalkerDefinition.asset`.

**Note for agents:** Full creature and faction roster (fauna, Unbound Kin, Hollow, Residue, Scavenger Bands) is implemented in Volume 11, Chunk 11.2. Do not implement those here — just confirm the AI framework supports the behavior types they will need (swarm, sound-triggered, variable aggression, scavenger spawn check).

**Acceptance criteria:**
- Optimized types share detection state in groups — alerting one alerts nearby others
- OptimizedHeavy weak point registers increased melee damage from the correct angle
- DirectedSentinel issues warning on unarmed approach before becoming hostile
- DirectedCrafter moves to repair damaged Optimized units as a priority
- Fractured Memory Shard drops contain readable text
- Schematic Fragment examined at Terminal unlocks a recipe
- CaveStalker drops from ceiling and behaves distinctly from Kin enemies
- EnemySpawner supports biome restriction, Y depth range, and group size config — all needed for Volume 11 expansion

---

## VOLUME 7 — BUILDING & BACKPACKS (Chunks 7.1–7.3)

Goal: Creative snap-grid building system with no structural integrity restrictions, breakable pieces, electricity, and portable storage. Building is about expression and fun — pieces can be placed freely in any configuration. No collapse physics. Pieces can be damaged and destroyed by enemies and tools, but a destroyed neighbor never affects adjacent pieces.

### Chunk 7.1 — Building Block System
**Files to create:**
- `Assets/Scripts/Building/BuildingPiece.cs` — ScriptableObject: piece name, piece type enum (Foundation, Wall, Floor, Ramp, Roof, Door, Window, Ladder, Fence, Pillar), material tier enum (Wood, Stone, IronReinforced, Titanium, VoidHardened), health per tier (Wood: 150, Stone: 400, IronReinforced: 800, Titanium: 1500, VoidHardened: 3000), model prefab per tier, snap points (list of local positions + allowed connection types), crafting cost (list of ItemStack).
- `Assets/Scripts/Building/PlacedPiece.cs` — MonoBehaviour on placed building pieces. Holds: BuildingPiece definition, current health, material tier. Implements `IDamageable`. Takes damage from weapons and tools (see damage modifiers below). At zero health: plays destruction particle effect, drops a partial resource refund (50% of crafting cost), destroys self. **Destroying a piece never affects neighbors — no collapse, no integrity check.**
- `Assets/Scripts/Building/BuildingDamage.cs` — Static utility. Defines damage multipliers per attacker type vs material tier. Melee tools (pickaxe, axe) deal full damage to their matched material (pickaxe vs stone, axe vs wood). Guns deal 25% damage to all building materials — not efficient for demolition. Explosives (late-game crafted item) deal 300% damage in a radius affecting multiple pieces simultaneously. Void-Hardened tier resists everything except late-game tools and void-infused weapons.
- `Assets/Scripts/Building/BuildingManager.cs` — Singleton. Tracks all placed pieces in `Dictionary<Vector3Int, PlacedPiece>` (snap grid = 2 world units per cell). Methods: `bool CanPlace(BuildingPiece piece, Vector3Int gridPos, Quaternion rotation)` — only checks for overlap, nothing else. `PlacedPiece Place(...)`, `void Remove(Vector3Int gridPos)`, `List<PlacedPiece> GetNeighbors(Vector3Int gridPos)`.
- `Assets/Scripts/Building/BuildModeController.cs` — Activated when holding a building piece in hotbar. Shows semi-transparent ghost preview at placement position. Snaps to existing piece snap points or terrain grid. Left click places, right click rotates (90° increments), R cycles piece types. Preview turns red only for overlap or insufficient materials — not for lack of support. Raycasts from camera to find placement surface.
- `Assets/Scripts/Building/SnapPoint.cs` — Serializable: local position offset, connection type enum (Foundation, WallBottom, WallTop, FloorEdge, RoofPeak), allowed rotation.
- Create building piece assets for Wood tier: `WoodFoundation.asset`, `WoodWall.asset`, `WoodFloor.asset`, `WoodRamp.asset`, `WoodRoof.asset`, `WoodDoor.asset`, `WoodWindow.asset`, `WoodLadder.asset`.

**Acceptance criteria:**
- Enter build mode by selecting a building piece in hotbar
- Ghost preview snaps to valid positions on terrain or existing pieces
- Pieces can be placed in any configuration — floating, overhanging, bridging gaps freely
- Pieces snap together seamlessly with no gaps
- Pieces can be rotated in 90° increments
- Attacking a wood wall with a stone pickaxe damages it; at zero health it destroys and drops resources
- Gun fire deals only 25% damage to building pieces
- Destroying a piece leaves all neighboring pieces fully intact and unaffected
- Cannot place when overlapping existing piece or with insufficient materials

### Chunk 7.2 — Electricity & Wiring (Basic)
Power is produced, stored, and consumed. The model is simple: watts generated, watts drawn, buffer in between. Power moves through physical cables placed in the world. Cables have transmission loss over distance — long runs need relay Junction Boxes. A Junction Box shows real-time draw vs supply (green = surplus, red = overdrawing).

**Files to create:**
- `Assets/Scripts/Building/Electricity/PowerNode.cs` — Interface/base class. All powered objects implement this. Properties: `float powerDraw` (watts consumed), `float powerOutput` (watts produced), `bool isPowered`, `PowerNetwork network` reference.
- `Assets/Scripts/Building/Electricity/PowerNetwork.cs` — Class representing a connected graph of PowerNodes. Tracks total generation, total consumption, and whether the network is satisfied. If not satisfied, nodes are powered in priority order (life-support first, lights last). Handles cable transmission loss: 2% loss per cable segment between source and consumer.
- `Assets/Scripts/Building/Electricity/WireController.cs` — MonoBehaviour. Player holds wire item, clicks one PowerNode then another to connect. Wire rendered as a Line Renderer. Max wire length: 20 units. Can be cut. For longer distances, use PowerCable building pieces instead.
- `Assets/Scripts/Building/Electricity/PowerGenerator.cs` — MonoBehaviour implementing PowerNode. Base class for all generators.
- `Assets/Scripts/Building/Electricity/BurnGenerator.cs` — Extends PowerGenerator. Consumes wood/coal/fuel, outputs 150W. Loud — generates a noise radius that can attract enemies. In enclosed underground spaces, generates a pollution buildup (visual particle haze + eventual player damage) if unvented. Easy to craft, always available.
- `Assets/Scripts/Building/Electricity/ThermalTap.cs` — Extends PowerGenerator. Must be placed below Y = -256 where geothermal rock is present (indicated by heat shimmer terrain visual). Outputs 500W. Passive, silent, permanent — no fuel required. Expensive to craft (titanium + deep rock components). The reward for going deep.
- `Assets/Scripts/Building/Electricity/WindRotor.cs` — Extends PowerGenerator. Surface and sky islands only (Y > 0). Outputs 50–200W based on altitude (higher = more output). Variable — smooths out with battery storage.
- `Assets/Scripts/Building/Electricity/BatteryBank.cs` — Absorbs surplus power, releases during deficit. Capacity: 5000 watt-seconds per tier. Degrades over charge cycles — battery cells need eventual replacement (crafted component). Stack multiple Banks for a larger buffer. Essential for smoothing wind and solar intermittency.
- `Assets/Scripts/Building/Electricity/JunctionBox.cs` — Relay point for long cable runs. Displays real-time generation/consumption readout when inspected. Reduces transmission loss on its segment.
- `Assets/Scripts/Building/Electricity/PoweredLight.cs` — Draws 10W. When powered, enables a point light.
- `Assets/Scripts/Building/Electricity/PoweredDoor.cs` — Draws 5W. When powered, opens/closes on interact. When unpowered, stays in current state.
- `Assets/Scripts/Building/Electricity/PowerCable.cs` — Building piece variant of wire for long-distance runs. Placed like building pieces (horizontal/vertical). 2% loss per segment. Allows running power lines across the map between bases.
- Create prefabs: `BurnGenerator.prefab`, `ThermalTap.prefab`, `WindRotor.prefab`, `BatteryBank.prefab`, `JunctionBox.prefab`, `Wire.prefab`, `PoweredLight.prefab`, `PoweredDoor.prefab`.

**Acceptance criteria:**
- Place a Burn Generator, fuel it, connect wire to a light — light turns on
- Multiple devices on one network share power correctly
- Disconnecting a wire splits the network, unpowered devices turn off
- Burn Generator creates pollution buildup in an enclosed space (particle haze)
- Thermal Tap only places below Y = -256, outputs significantly more power than Burn
- Wind Rotor output visibly varies with altitude (inspect to see current output)
- Battery Bank charges during surplus, discharges during deficit — smooths wind power
- Junction Box shows live generation/consumption readout
- Long cable runs show transmission loss percentage on inspection
- Power network UI (inspecting any node) shows full grid overview

### Chunk 7.3 — Backpack Implementation
A backpack is a portable chest. To open one, select it on the hotbar and right-click with no menus open — identical to how you'd interact with a held item in the world. The backpack UI opens as a panel alongside the player inventory (which also opens automatically if not already open). Backpacks cannot be opened from inside the inventory UI, and backpacks cannot be placed inside other backpacks.

The player has a single dedicated **backpack slot** in their inventory UI — a special slot separate from the main grid and hotbar. A backpack in this slot is considered "worn" and is always accessible via hotbar without taking up a hotbar slot. Any backpack anywhere else in the inventory is accessible only if moved to the hotbar first.

**Files to create:**
- `Assets/Scripts/Inventory/BackpackDefinition.cs` — ScriptableObject extending ItemDefinition. Fields: `int rows`, `int columns` (defines internal inventory size). No other special fields.
- `Assets/Scripts/Inventory/BackpackInstance.cs` — Plain C# class. Wraps an `Inventory` of the defined size. The Inventory is serialized onto the BackpackDefinition item instance so contents travel with the item at all times. Methods: `Inventory GetInventory()`, `void Open()`, `void Close()`.
- Modify `PlayerInventory.cs` — Add a single `ItemStack backpackSlot` (the dedicated worn slot). A BackpackDefinition item placed here is considered "worn" and is always accessible via hotbar without taking up a hotbar slot. Add `BackpackInstance equippedBackpackInstance` — set when a BackpackDefinition is placed in the backpack slot, cleared when removed. **B key** calls `UIManager.OpenBackpack(equippedBackpackInstance)` if the slot is filled — this is the shortcut for the worn slot only. Any backpack elsewhere in inventory must be on the hotbar and right-clicked to open. Add validation: `bool CanPlaceInSlot(ItemStack item, SlotType slot)` — returns false if attempting to place a BackpackDefinition into any inventory slot other than the backpack slot or the hotbar. Backpacks placed in the hotbar work normally but occupy a hotbar slot.
- Modify `InventoryUI.cs` — Add the backpack slot visually to the inventory panel, labeled "Backpack", clearly distinct. Dragging a BackpackDefinition here equips it to the worn slot. **Backpack items in any slot cannot be right-clicked to open from inside the inventory UI** — the right-click action on a BackpackDefinition in inventory is the same as any other item (pick up, stack, etc). Opening is hotbar-only.
- Modify `PlayerInteraction.cs` (or equivalent right-click handler) — When the player right-clicks with no menus open and the active hotbar item (or worn backpack slot) is a BackpackDefinition: open `BackpackUI` for that instance, simultaneously open the player inventory panel if not already open. This is the only way to open a backpack.
- `Assets/Scripts/UI/BackpackUI.cs` — Panel that opens alongside the player inventory. Grid sized to the backpack's `rows x columns`. Closing the player inventory also closes the backpack panel. Items can be dragged freely between the backpack panel and the player inventory. **Backpack items cannot be dropped into the backpack panel** — validate on drop, reject with a brief visual shake if attempted.
- Modify `Inventory.cs` — Add validation in `SetSlot()` / `AddItem()`: if the target inventory belongs to a BackpackInstance, reject any item whose ItemDefinition is a BackpackDefinition.
- Create backpack assets:
  - `Satchel.asset` — 3x3 (9 slots). Crafted from Hide + String. Early game.
  - `TravelPack.asset` — 4x5 (20 slots). Iron Buckles + Reinforced Fabric. Mid game.
  - `ExpeditionRig.asset` — 5x6 (30 slots). Titanium Frame + Treated Leather. Late game.
  - `VoidPocket.asset` — 6x6 (36 slots). Void Crystal + Dimensional Fabric. End game.

**Acceptance criteria:**
- Select Satchel on hotbar, right-click with no menus open — backpack panel and inventory both open
- Select any non-backpack item on hotbar, right-click — backpack does not open
- Worn backpack slot: backpack placed there is always accessible — press B to open it directly without it occupying a hotbar slot
- Select a backpack on the hotbar, right-click with no menus open — backpack panel and inventory both open
- B key with nothing in the worn backpack slot does nothing
- Right-clicking a backpack item inside the open inventory UI does NOT open it — no special behavior, treated as a normal item
- Items placed in backpack persist on the item — drop it in the world, pick it back up, contents intact
- Attempting to drag a backpack into the open backpack panel is rejected with a visual shake
- Attempting to drag a backpack into any inventory slot other than the backpack slot or hotbar is rejected
- Closing the inventory panel also closes the backpack panel
- No speed penalties or player stat changes of any kind

---

## VOLUME 8 — AUTOMATION & MACHINES (Chunks 8.1–8.5)

Goal: Conveyors, pneumatic tubes, drone ports, auto-miners, assemblers, computer terminal with storage drives and scripted automation. The aesthetic is mechanical and slightly improvised — recovered Architect technology bootstrapped into a working factory. All machines consume power from Volume 7's grid.

### Chunk 8.1 — Conveyor Belts, Tubes & Routing
**Files to create:**
- `Assets/Scripts/Automation/ConveyorBelt.cs` — MonoBehaviour. Physical rubber-and-metal belt placed in the world on a grid. Has a direction (set during placement, rotatable). Items sit visibly on top as small 3D models. Each tick (every 0.5s), moves items one step in its direction. Supports: straight segments, corner segments, slope segments (move items vertically), split segments (1 input → 2 outputs). When an item reaches the end, transfers to the next belt/hopper/machine. If nothing is connected, items pile up (max 5, then belt stalls). Belt speed is upgradeable (basic = 0.5s tick, fast = 0.2s tick, costs more power).
- `Assets/Scripts/Automation/PneumaticTube.cs` — MonoBehaviour. Enclosed item transport — items travel invisibly through opaque tube segments. Tube glows faintly when active, pulses when an item passes through. Faster than belts (0.15s per segment), higher power cost, more expensive to craft. Ideal for long-distance runs through rock walls and ceilings. Same grid-based placement as belts but with enclosed visual.
- `Assets/Scripts/Automation/Hopper.cs` — Connects a container (chest, machine) to a belt or tube. Input hopper: pulls items from belt/tube into container. Output hopper: pushes items from container onto belt/tube. Configurable filter: only transfer specific item types. Filter Hopper variant only passes matching items.
- `Assets/Scripts/Automation/BeltSorter.cs` — Special belt junction with 2–3 outputs. Configure which item types route to which output. Unfiltered items take the default output. Visual: small diverter arm that flicks to the correct side when an item passes.
- `Assets/Scripts/Automation/OverflowValve.cs` — Blocks flow when the downstream container is full, opens when space is available. Prevents machines from jamming each other. Simple but essential for well-designed bases.
- `Assets/Scripts/Automation/AutomationTickManager.cs` — Singleton. Runs automation tick at fixed interval. All automation components register and get ticked in dependency order: output hoppers → belts/tubes → input hoppers → machines.
- Create prefabs: `ConveyorBelt.prefab` (animated rolling texture), `PneumaticTube.prefab` (glowing when active), `Hopper.prefab`, `FilterHopper.prefab`, `BeltSorter.prefab`, `OverflowValve.prefab`.

**Acceptance criteria:**
- Place a chain of conveyor belts — items visually ride along on top
- Place pneumatic tube segments — items transport faster with glow/pulse visual
- Belt sorter correctly routes iron ore left and copper ore right
- Filter hopper only passes matching item types into a machine
- Overflow valve stalls flow when downstream is full, no item loss
- Slope belt segments move items vertically between floors
- Belt speed upgrade visibly increases item throughput

### Chunk 8.2 — Auto-Miner, Drill & Drone Ports
**Files to create:**
- `Assets/Scripts/Automation/AutoMiner.cs` — MonoBehaviour. Placed on terrain above an ore vein. Scans the density field below it for ore within a configurable radius (default 4 blocks). When powered (50W) and active, extracts ore at 1 item per tick. Outputs to internal buffer (8 slots). Connect via hopper to belt or tube to empty the buffer. Ore vein depletes over time — auto-miner stops when vein is exhausted or buffer is full. Status display: current ore type, ore remaining, buffer fullness.
- `Assets/Scripts/Automation/DrillHead.cs` — Visual component on the auto-miner: animated drill bit rotates when active, particle effects (rock debris) when mining.
- `Assets/Scripts/Automation/DronePort.cs` — MonoBehaviour. A small launch pad that sends hovering cargo drones to a designated receiving DronePort. Configuration: set destination port, set item filter (what to send). Drones ferry items automatically — slow (5 seconds per trip), limited carry weight (1 stack per drone), but completely ignores terrain. Can cross vertical shafts, walls, underground ceilings, sky island gaps. Drones are visible 3D objects in the world — VORD forces will shoot them down if they pass through contested airspace, so route planning matters. Requires power (30W when active). Each DronePort has an internal buffer (4 slots) that drones load from and deliver to.
- Modify `OreGenerator.cs` — Add `int CountOreInRadius(Vector3 worldPos, float radius, byte oreType)` and `bool DepleteOre(Vector3 worldPos)` to track ore depletion.
- Create prefabs: `AutoMiner.prefab` (industrial machine with drill, status lights, buffer access), `DronePort.prefab` (small landing pad with launch arm).

**Acceptance criteria:**
- Place auto-miner above an ore vein, power it → mines ore into buffer
- Connect hopper → ore flows onto belt or into tube
- Ore vein depletes over time, auto-miner stops when exhausted
- Place two Drone Ports, configure destination, load items into source buffer → drone visually flies between them carrying items
- Drone correctly deposits items at receiving port
- Drone is destroyed if it flies through an area with active VORD enemies
- Status displays on both auto-miner and drone port show current state

### Chunk 8.3 — Machines (Electric Furnace, Grinder, Press, Assembler)
This chunk implements the full Tier 1 and Tier 2 machine lineup. All machines consume power, accept input from conveyors/tubes/hoppers, and output to conveyors/tubes/hoppers.

**Tier 1 — Basic Electric Machines:**
- `Assets/Scripts/Automation/ElectricFurnace.cs` — Faster and more efficient than the manual furnace. Uses the same SmeltingRecipe ScriptableObjects. Draws 60W. No fuel slot — powered by the grid. Accepts input on one port, outputs on another. Processes one item per recipe's smelt time (faster than manual).
- `Assets/Scripts/Automation/Grinder.cs` — Converts raw ore into ore dust (e.g., Iron Ore → Iron Dust). Ore dust smelts more efficiently (1 dust = 1.5x yield vs raw ore) and can be combined to create alloys (e.g., Iron Dust + Copper Dust → Bronze Dust). Draws 40W. Adds an optional processing step that rewards players who build the full chain.
- `Assets/Scripts/Automation/Press.cs` — Shapes ingots into Plates and Rods (building and component materials). Manual crafting can do this slowly; the Press does it in continuous bulk. Draws 50W.
- `Assets/Scripts/Automation/Assembler.cs` — Auto-crafts any CraftingRecipe. Has an input inventory (3×3 buffer) and output inventory (1×3 buffer). Recipe is set by inserting a SchematicCard item into its recipe slot — a physical item the player crafts or finds. When input has sufficient materials and output has room, crafts one item per tick. Draws 75W.
- `Assets/Scripts/Automation/SchematicCard.cs` — ItemDefinition subclass. References a CraftingRecipe. Crafted at the workbench from paper + copper components. Found as loot in VORD facilities and Architect strongholds.

**Tier 2 — Electronics Machines (requires Architect Relic reverse-engineering, unlocked mid-game):**
- `Assets/Scripts/Automation/CircuitEtcher.cs` — Produces Circuit Boards from Copper Plates + Silica (found in Badlands/desert biome terrain). Circuit Boards are the bottleneck component for all electronics. Slow (8s per board), power-hungry (100W), but irreplaceable for late-game progression.
- `Assets/Scripts/Automation/ComponentPress.cs` — Specialized Press for electronic components. Produces Capacitors, Resistors, Transistors from refined materials. Required for Terminal and Storage Drive crafting.
- `Assets/Scripts/Automation/BatteryFabricator.cs` — Mass-produces Battery Cells from refined lithium and copper. Essential for scaling up BatteryBank capacity.
- `Assets/Scripts/UI/AssemblerUI.cs` — UI panel for Assembler: shows input buffer, output buffer, schematic card slot, current crafting progress, power status, estimated items-per-minute.
- Create prefabs: `ElectricFurnace.prefab`, `Grinder.prefab`, `Press.prefab`, `Assembler.prefab`, `CircuitEtcher.prefab`, `ComponentPress.prefab`, `BatteryFabricator.prefab`.

**Acceptance criteria:**
- Electric Furnace smelts ore faster than manual furnace without consuming fuel
- Grinder converts ore to dust; dust smelts to 1.5x yield at Electric Furnace
- Press bulk-converts ingots to plates and rods
- Assembler with a WoodenPickaxe SchematicCard auto-crafts pickaxes from belt-fed materials
- Circuit Etcher produces Circuit Boards (visually distinct, slow process)
- All Tier 2 machines are locked behind a crafting requirement that gates them to mid-game
- All machines stop cleanly when input is empty or output is full, resume when conditions clear

### Chunk 8.4 — Power Grid (Advanced Generators & Void Filament)
This chunk adds the remaining generator types to complete the power progression from early-game to end-game. Builds on the PowerNode/PowerNetwork/BatteryBank system from Chunk 7.3.

**Files to create:**
- `Assets/Scripts/Building/Electricity/VoidFilamentGenerator.cs` — Extends PowerGenerator. End-game generator requiring abyssal zone materials (Void Crystal + Abyssal Filament). Outputs 2000W. Passive — no fuel required. Generates a distinctive hum and faint glow visible at range. **VORD notices active Void Filament Generators** — the energy signature is familiar to its network. After a configurable delay (default: 5 real minutes of operation), VORD will dispatch an Optimized patrol to investigate the source. The player must either defend the generator or relocate it. This creates ongoing tension in late-game bases. Requires a 2×2 foundation footprint.
- `Assets/Scripts/Building/Electricity/SolarPanel.cs` — Extends PowerGenerator. Surface only (Y > 0, no terrain obstruction above). Outputs 75W during daytime, 0 at night. Cheap to craft. Useful as a supplement but insufficient alone — pairs well with BatteryBank for overnight storage.
- Modify `PowerNetwork.cs` — Add network merge/split when cables are placed/destroyed, complete battery charge/discharge logic, and a `GetNetworkSummary()` method returning total generation, total consumption, battery level, and list of all nodes.
- `Assets/Scripts/UI/PowerNetworkUI.cs` — When looking at any powered device (within 5 units), shows a floating HUD: network generation total, consumption total, battery charge %, list of top consumers by draw, and VORD alert status if a Void Filament is active.

**Generator progression summary (all types now implemented across 7.3 and 8.4):**
| Generator | Power | Fuel | Location | Notes |
|-----------|-------|------|----------|-------|
| Burn Generator | 150W | Wood/Coal | Anywhere | Loud, pollutes enclosed spaces |
| Solar Panel | 75W | None | Surface only | Day only, pairs with battery |
| Wind Rotor | 50–200W | None | Surface/Sky | Variable, altitude-dependent |
| Thermal Tap | 500W | None | Y < -256 | Passive, silent, permanent |
| Void Filament | 2000W | None | Abyssal zone | Attracts VORD patrols |

**Acceptance criteria:**
- Void Filament Generator produces 2000W and can power a substantial base alone
- After 5 minutes of operation, a VORD patrol spawns and navigates toward the generator
- Solar Panel outputs 75W during day, 0 at night — BatteryBank bridges the gap overnight
- PowerNetworkUI shows complete grid overview from any powered device
- Network correctly merges when cable connects two separate networks
- Network correctly splits when cable is cut between two nodes

### Chunk 8.5 — Computer Terminal, Storage Drives & Scripted Automation
The capstone of the automation system. Requires Tier 2 electronics (Circuit Boards, Components from Chunk 8.3). The lore basis: the Architect's original storage technology didn't hold physical items — it stored **density patterns**, compressing physical matter into a crystalline substrate and recording reconstruction data digitally. The player recreates this technology. The scripting language used for automation rules is the same underlying syntax VORD uses for Kin directive control — discovering this is an intentional mid-game lore beat.

**Files to create:**
- `Assets/Scripts/Automation/ComputerTerminal.cs` — MonoBehaviour. Placeable workstation — screen and keyboard assembly. Interact to open the Terminal UI. At base level: shows power grid status, all connected machine states, and live storage drive readout. Connected to machines and drives via Network Cable (separate from power cable — distinct visual, cheaper to craft).
- `Assets/Scripts/Automation/NetworkCable.cs` — Building piece for connecting Terminal to machines and StorageDrives. Thinner than power cable, different color. 0% data loss over any distance. Placed like power cable segments.
- `Assets/Scripts/Automation/StorageDrive.cs` — MonoBehaviour. Placeable unit that stores items digitally. Holds up to 50,000 items across all types (matter compressed, reconstruction data stored). Categories: Raw Materials Drive, Components Drive, Fuel Drive, Misc Drive — one drive per category recommended but not enforced. Properties: `bool isCorrupted` (set if the drive takes a direct hit — corrupted drives lose a random portion of stored items and must be repaired with a Drive Repair Kit). `bool isEjected` — drives can be physically removed and carried (they become a portable item in inventory). A networked machine automatically pulls from and pushes to connected drives — no conveyors needed for the last step once networked.
- `Assets/Scripts/Automation/DriveItem.cs` — ItemDefinition subclass representing an ejected StorageDrive as a portable inventory item. Shows stored contents as item tooltip. Can be reinserted into a Drive Rack.
- `Assets/Scripts/Automation/DriveRack.cs` — Holds up to 4 StorageDrives and connects them to the Terminal network as a single storage pool.
- `Assets/Scripts/UI/TerminalUI.cs` — Full Terminal interface with tabs: **Storage** (live grid of all items across all drives, searchable, click to pull items), **Machines** (list of all networked machines with status, current recipe, throughput), **Power** (mirrors PowerNetworkUI — generation, consumption, battery), **Scripts** (automation scripting tab — see below).
- `Assets/Scripts/Automation/AutomationScriptEngine.cs` — Parses and executes simple automation rules written in the Terminal's Scripts tab. Supported commands (~15 total):
  ```
  IF [item_name] > [amount] THEN PAUSE [machine_id]
  IF [item_name] < [amount] THEN ACTIVATE [machine_id]
  SEND [item_name] TO [machine_id] WHEN [item_name] < [amount]
  SET [machine_id] RECIPE [recipe_name]
  ALERT WHEN [item_name] < [amount]
  ```
  Scripts run on every automation tick. Errors display in the Scripts tab with line number. This is optional — the system works without scripts, scripts just make it self-regulating.
- Create prefabs: `ComputerTerminal.prefab`, `DriveRack.prefab`, `StorageDrive.prefab` (physical unit in world and as held item).

**Lore note for agents:** The Scripts tab should display a subtle flavor line when first opened: *"Directive syntax recognized. This language has been used before."* This is the only in-game hint connecting the automation scripting to VORD's Kin control directives. Do not over-explain it — one line is enough.

**Acceptance criteria:**
- Place Terminal, connect to machines and drives via Network Cable
- Terminal Storage tab shows live inventory across all connected drives
- Pull an item from Storage tab → it appears in player inventory
- Networked Assembler automatically draws materials from drives and deposits output to drives — no conveyor needed
- StorageDrive takes a hit → isCorrupted flag set, portion of contents lost, Drive Repair Kit fixes it
- Eject a drive → becomes portable item, reinsert into Drive Rack → contents available again
- Write a script: `IF [Iron Plate] > 200 THEN PAUSE [Press_01]` → Press pauses when threshold is met, resumes when below
- Scripts tab shows the Directive syntax flavor line on first open
- Terminal UI is readable and functional without being overwhelming

---

## VOLUME 9 — VEHICLES (Chunks 9.1–9.3)

Goal: Assembled vehicles with a frame + component system, independent damage zones, field repairs, and physics-based driving. Vehicles are quality-of-life multipliers — faster travel, more cargo, better tools — not progression gates. Players can reach all areas without vehicles, but vehicles make everything significantly better. Vehicles remember where they're parked. VORD forces will target and damage unattended vehicles in hostile territory. Fuel scarcity is regional: plentiful on the surface, limited underground, none in the abyssal zone.

### Chunk 9.1 — Vehicle Base System (Assembly, Physics, Enter/Exit, Damage)

**Core concept:** A vehicle is a **Frame** + a set of **Components** slotted into that frame's attachment points. The Frame defines vehicle category and attachment point layout. Components define performance, capability, and what can be damaged independently.

**Files to create:**
- `Assets/Scripts/Vehicles/VehicleFrame.cs` — ScriptableObject: frame name, frame type enum (Pushcart/Cycle/Buggy/Hauler/DrillFrame/RotorFrame), max seat count, attachment point definitions (list of AttachmentPoint: local position, attachment type enum (Engine/Suspension/Armor/Glazing/Utility/Rotor/Drill)), base weight, model prefab.
- `Assets/Scripts/Vehicles/VehicleComponent.cs` — ScriptableObject base class: component name, attachment type it fills, weight, crafting cost, model prefab (physically appears on the vehicle when installed). Subclasses: `EngineComponent` (fuel type, max power, noise level, overheatsOnSustain bool), `SuspensionComponent` (spring strength, terrain handling bonus), `ArmorComponent` (damage resistance per hit, weight penalty), `GlazingComponent` (shatterThreshold, isMesh bool — mesh guards don't shatter), `UtilityComponent` (utilityType enum: CargoBed/FuelTankExtension/WeaponMount/RepairKitRack/TerrainLampArray/SignalDampener).
- `Assets/Scripts/Vehicles/VehicleDamageSystem.cs` — Tracks 6 damage zones independently: `EngineBay`, `LeftDrivetrain`, `RightDrivetrain`, `Chassis`, `FrontGlazing`, `FuelSystem`. Each zone has `DamageStage` enum (Healthy/Damaged/Critical/Destroyed). Different sources hit different zones: front collisions → EngineBay, gunfire from left → LeftDrivetrain, undercarriage scraping → FuelSystem, etc. Zone effects: Destroyed EngineBay = coasts only; Destroyed LeftDrivetrain = heavy left pull, Destroyed both = no movement; Critical Chassis = components start detaching; Destroyed Glazing = driver exposed to projectiles; Leaking FuelSystem = constant fuel drain + visible particle trail.
- `Assets/Scripts/Vehicles/AssembledVehicle.cs` — MonoBehaviour on a built vehicle in the world. Holds: VehicleFrame reference, `Dictionary<AttachmentType, VehicleComponent> installedComponents`. Implements `IDamageable` — routes damage to VehicleDamageSystem based on hit direction. Drops a `SalvageStack` of components in various conditions when destroyed.
- `Assets/Scripts/Vehicles/VehicleBase.cs` — MonoBehaviour. Core Rigidbody physics. Reads EngineComponent for power/fuel type, SuspensionComponent for handling. Abstract: `ApplyMotorForce(float)`, `ApplySteeringForce(float)`, `ApplyBrakeForce(float)`. Manages fuel (via VehicleFuel), seat occupancy, Inventory (present only if CargoBed utility installed).
- `Assets/Scripts/Vehicles/VehicleInteraction.cs` — Player MonoBehaviour. Press E near vehicle to enter — disables PlayerController, parents camera to driver seat, enables VehicleInput. Press E to exit — re-enables PlayerController, places player at safe exit point.
- `Assets/Scripts/Vehicles/VehicleInput.cs` — Reads Vehicle action map: WASD throttle/steer, Space brake, Shift boost, E exit, F horn/drill, Tab vehicle inventory.
- `Assets/Scripts/Vehicles/VehicleFuel.cs` — Drains based on throttle. Empty = coast only. Leaking FuelSystem drains continuously regardless of throttle. Refuel via interact + fuel items in hotbar.
- `Assets/Scripts/Vehicles/FieldRepairSystem.cs` — Player holds a Repair Kit item (consumable, multiple charges). Hold E near a damaged zone to consume one charge and improve that zone by one DamageStage. Cannot field-repair Destroyed — only Critical or Damaged. The `RepairKitRack` utility makes repair kits accessible without opening inventory mid-combat.
- `Assets/Scripts/Vehicles/VehicleWorkbench.cs` — Placeable building piece. Interact to open VehicleAssemblyUI. Functions: assemble new vehicle (choose frame, slot components, confirm cost), full workshop repair (restore all DamageStages, replace Destroyed components), reconfigure loadout (swap components on an existing vehicle).
- `Assets/Scripts/UI/VehicleHUD.cs` — Speedometer, fuel gauge, per-zone damage indicators (6 color-coded icons — green/yellow/red/grey), passenger count, active utility readout (lamp, dampener status).
- `Assets/Scripts/UI/VehicleAssemblyUI.cs` — Frame selector panel, component slot grid matching frame attachment points, drag-and-drop from inventory, total weight readout, estimated performance stats, crafting cost preview, assemble button.
- Create frame assets: `PushcartFrame.asset`, `CycleFrame.asset`, `BuggyFrame.asset`, `HaulerFrame.asset`, `DrillFrame.asset`, `RotorFrame.asset`.
- Create starter component assets: `CombustionEngine_Basic.asset`, `CombustionEngine_Heavy.asset`, `ElectricMotor_Basic.asset`, `BasicSprings.asset`, `HeavyCoil.asset`, `ArticulatedJoints.asset`, `BasicGlass.asset`, `LaminatedPane.asset`, `MeshGuard.asset`, `ThinSheetArmor.asset`, `ReinforcedPlate.asset`, `CargoBed_Small.asset`, `CargoBed_Large.asset`, `FuelTankExtension.asset`, `WeaponMount.asset`, `RepairKitRack.asset`, `TerrainLampArray.asset`, `SignalDampener.asset`.

**Acceptance criteria:**
- Open VehicleWorkbench, select BuggyFrame, slot CombustionEngine_Basic + BasicSprings × 4 + BasicGlass → Assemble → driveable Buggy spawns
- Enter/exit transitions camera smoothly, PlayerController correctly enabled/disabled
- Vehicle physics: momentum, slope sliding, terrain bumps felt through suspension
- Fuel drains while driving; empty = coasts only, cannot accelerate
- Shoot the front of a vehicle → EngineBay degrades through stages to Destroyed
- Shoot a left wheel → LeftDrivetrain degrades → vehicle pulls left noticeably
- Destroy front glazing → visual pane disappears, driver takes damage from projectiles
- Field Repair Kit improves one zone one stage; cannot repair Destroyed
- VehicleWorkbench full repair restores all zones
- Pushcart frame has no engine slot, is player-pushed, cannot exceed walk speed

### Chunk 9.2 — Ground Vehicles (Buggy, Cycle, Hauler, Drill Frame)
**Files to create:**
- `Assets/Scripts/Vehicles/WheeledVehicle.cs` — Extends VehicleBase. Uses Unity WheelColliders. Reads wheel count and positions from frame definition. Configures spring/damper from installed SuspensionComponent, motor torque from EngineComponent. Anti-roll bar keeps stable at speed. Asymmetric drivetrain damage: damaged/destroyed drivetrain zones reduce torque on that axle — two destroyed zones on the same side = that side no longer drives. Surface detection: different friction coefficients for dirt/rock/sand (biome-dependent).
- `Assets/Scripts/Vehicles/CycleVehicle.cs` — Extends WheeledVehicle. 2-wheel balance physics: leans into turns using rigidbody torque. Fast acceleration, high top speed, single seat, no storage. Very sensitive to drivetrain damage — one destroyed wheel = unrideable. Twitchy at low speeds, stable at high speeds.
- `Assets/Scripts/Vehicles/DrillVehicle.cs` — Extends WheeledVehicle. Drill Frame specific. Front-mounted DrillHead attachment: holding F activates drill, boring through soft terrain (soil density < threshold) using `TerrainDeformer.DeformSphere()` in the forward direction. Low chassis profile allows navigating tunnels the Buggy cannot enter. TerrainLampArray utility strongly recommended for underground use — without it the lamp array is absent and underground driving is nearly blind.
- Create default prefab assemblies (placeholder models — shaped primitives):
  - `Buggy_Default.prefab` — BuggyFrame + CombustionEngine_Basic + BasicSprings × 4 + BasicGlass + CargoBed_Small. Low box on 4 sphere wheels. Seats 2.
  - `Cycle_Default.prefab` — CycleFrame + CombustionEngine_Basic + BasicSprings × 2 + BasicGlass. Thin upright rectangle on 2 sphere wheels. Seat 1.
  - `Hauler_Default.prefab` — HaulerFrame + CombustionEngine_Heavy × 2 + HeavyCoil × 6 + LaminatedPane + CargoBed_Large. Tall wide box on 6 sphere wheels. Requires both engine slots populated — with only one engine installed it barely moves.
  - `DrillRig_Default.prefab` — DrillFrame + CombustionEngine_Basic + ArticulatedJoints × 4 + MeshGuard + TerrainLampArray. Low-profile box with forward drill cone. Seat 1.

**Acceptance criteria:**
- Buggy drives responsively, seats 2, small cargo accessible from driver seat via Tab
- Cycle leans into turns, noticeably faster than Buggy, tips easily at low speed on rough terrain
- Hauler with two engines: slow, heavy, enormous cargo. With one engine: barely functional (intended)
- Hauler cargo bed (CargoBed_Large) holds 4× the inventory of CargoBed_Small
- DrillVehicle activates drill on F key and bores visible tunnel through soil terrain
- DrillVehicle without TerrainLampArray: underground driving is dark and difficult
- All wheeled vehicles respond to drivetrain damage: pull toward damaged side, stop on destroyed
- Destroyed wheel component visually detaches from the vehicle model

### Chunk 9.3 — Rotor Frame (Gyrocopter) & Pushcart
**Files to create:**
- `Assets/Scripts/Vehicles/RotorComponent.cs` — Extends VehicleComponent. Fields: `liftForce`, `minAirspeed` (stall threshold — below this lift degrades), `maxAltitude` (soft ceiling — above this, lift output degrades linearly, not a hard block), spinning rotor visual prefab. Heavier rotors provide more lift but weigh more; lighter rotors stall at lower speeds.
- `Assets/Scripts/Vehicles/RotorVehicle.cs` — Extends VehicleBase. No WheelColliders. Custom Rigidbody lift/thrust forces. Rotor lift = `installedRotors.Sum(r => r.liftForce) * throttle * altitudeFactor`. Throttle-based lift means constant throttle input required to maintain altitude. Controls: W/S pitch, A/D yaw, Space increase throttle (ascend), Shift decrease throttle (descend), mouse roll. Stall: when airspeed drops below any rotor's `minAirspeed`, that rotor's lift contribution drops to zero. With both rotors stalled, vehicle falls — recovers by gaining speed in a dive. Weight is critical: a heavily armored RotorFrame with weak rotors barely achieves liftoff. Player is responsible for balancing component weight vs rotor capability.
- `Assets/Scripts/Vehicles/PushcartVehicle.cs` — Extends VehicleBase. No engine component slot — cannot accept EngineComponent. Movement is player-driven: `VehicleBase.ApplyMotorForce` applies a small push force in the direction the player is walking, capped at 60% of player walk speed. No fuel system — no VehicleFuel component. TerrainLampArray utility usable. Silent: no engine noise radius. Narrow frame navigates tunnels the Buggy cannot. CargoBed_Large installed by default: 4× normal inventory capacity.
- Create rotor component assets: `BasicRotor.asset` (moderate lift, low stall speed, light — forgiving), `HeavyRotor.asset` (high lift, higher stall speed, heavy — for cargo loads), `PrecisionRotor.asset` (high lift, very low stall speed, expensive — late game, agile).
- Create default prefab assemblies:
  - `Gyrocopter_Default.prefab` — RotorFrame + CombustionEngine_Basic + BasicRotor × 2 + BasicGlass. Two rotors visible on top, spin speed tied to throttle value.
  - `Pushcart_Default.prefab` — PushcartFrame + CargoBed_Large. No engine. Wide flat cargo bed visible on frame.

**Acceptance criteria:**
- Gyrocopter with BasicRotors: takes off, flies, lands. Pitch/yaw/roll all respond correctly.
- Light Gyrocopter (no armor, BasicRotors): responsive, recovers from stall with a short dive
- Heavy Gyrocopter (ReinforcedPlate armor + CargoBed_Small): sluggish, stalls more easily, requires more throttle to maintain altitude
- Stall below minAirspeed → rotor lift drops → vehicle descends → throttle + dive recovers speed → lift returns
- Rotor spin visual matches current throttle level
- Pushcart moves at ≤ 60% player walk speed with no engine
- Pushcart is completely silent — no audio detection radius
- Pushcart CargoBed_Large holds 4× normal vehicle inventory
- SignalDampener utility on Gyrocopter reduces Vael (General THREE) detection range — shown as a stat in VehicleHUD

---

## VOLUME 10 — QUESTS, STORY & POLISH (Chunks 10.1–10.4)

Goal: Quest system, NPC dialogue, the full Cortex Device module progression, Safehold communities, VORD's hierarchy, and boss encounters for the five Generals.

### Chunk 10.1 — Quest System Framework
**Files to create:**
- `Assets/Scripts/Quests/QuestDefinition.cs` — ScriptableObject: quest ID (string), quest name, description, quest giver NPC ID, prerequisites (list of quest IDs), objectives (list of QuestObjective), rewards (list of ItemStack), isMainStory bool, isRepeatable bool.
- `Assets/Scripts/Quests/QuestObjective.cs` — Serializable class. Objective type enum: KillEnemy, CollectItem, DeliverItem, ReachLocation, CraftItem, PlaceBuilding, InteractWith, InstallCortexModule. Fields: description, targetCount, currentCount, isCompleted.
- `Assets/Scripts/Quests/QuestManager.cs` — Singleton. Tracks active quests, completed quests (HashSet of IDs), available quests. Methods: `AcceptQuest`, `UpdateObjective`, `IsQuestComplete`, `CompleteQuest` (distributes rewards). Listens to game events and auto-updates objectives.
- `Assets/Scripts/Quests/QuestEventBridge.cs` — Hooks into EnemyBase.Die(), PlayerInventory item add, CortexDevice module install, location triggers, and fires corresponding events to QuestManager.
- `Assets/Scripts/UI/QuestTrackerUI.cs` — Persistent HUD: active quest name, current objective, progress (e.g. "Reach Stronghold 1 — 0/1"). Up to 3 tracked quests. Click to expand.
- `Assets/Scripts/UI/QuestLogUI.cs` — Full log panel (J key). Active, Completed, Available tabs. Each quest shows full description, all objectives with progress, rewards. Main story quests visually distinguished from side quests.

**Acceptance criteria:**
- Accept a quest → appears in tracker HUD
- Objectives update in real time as conditions are met
- Completing all objectives and returning to quest giver distributes rewards
- Prerequisites correctly block quests until prior quests are complete
- InstallCortexModule objective type correctly fires when a module is installed

### Chunk 10.2 — NPC Dialogue, Safeholds & The Kin Communities
The three Safeholds are the player's primary allied communities. Each has distinct culture, resource problems, and internal politics. NPCs here are not generic quest-givers — they are Kin who survived, and their dialogue reflects what they know and what they've lost. The Architect's myths are woven into their speech before the player reveals the truth.

**Files to create:**
- `Assets/Scripts/Quests/NPCDefinition.cs` — ScriptableObject: NPC name, role, model prefab, idle animation, dialogue tree reference, world position, associated quests, shopInventory (optional).
- `Assets/Scripts/Quests/NPCController.cs` — MonoBehaviour. Idle behavior (look around, small movements). Detects player within 5 units, turns to face, shows interact prompt. On E: starts dialogue. Directed enemy types use a stripped version of this for their Converse state.
- `Assets/Scripts/Quests/DialogueTree.cs` — ScriptableObject. Tree of dialogue nodes. Each node: speaker name, text, list of response options (text, next node ID, optional condition, optional action). Conditions check QuestManager state and PlayerInventory. Actions: accept quest, give item, open shop, trigger world event.
- `Assets/Scripts/Quests/DialogueRunner.cs` — MonoBehaviour. Plays through a DialogueTree, evaluates conditions, executes actions on choice.
- `Assets/Scripts/UI/DialogueUI.cs` — Lower-third panel. NPC name + dialogue text (typewriter effect, skippable with Space). 1–4 response buttons. Disables player movement. ESC exits.

**Safehold NPCs to create:**

*Ashfen (Surface — Fungal Marshes):*
- `ElderMoss.asset` — Community leader. Speaks of "the Maker" with reverence — the myths are about the Architect but distorted by generations of retelling. First NPC to give the player a main story quest. His reaction when the player reveals they have the Cortex Device is a key story beat.
- `ScoutWren.asset` — Young Kin, curious and irreverent. Gives surface exploration side quests. Knows VORD patrol routes better than anyone.

*The Delve (Underground — first cavern layer):*
- `ForgeKeeperTar.asset` — Gruff engineer. Trades refined materials, gives mining and automation quests. Distrusts outsiders until the player proves useful. Offers the first Vehicle Workbench schematic.
- `ArchivistCell.asset` — Keeper of recovered Architect documents. Has partial logs that don't fully make sense — gives the player fragments that match logs found in Strongholds, creating satisfying "puzzle completion" moments.

*Spire's Rest (Sky islands):*
- `KeeperVaris.asset` — Has been maintaining Stronghold 4 without knowing what it is. Protective of the structure. Becomes an essential ally once the player explains the Cortex Device's nature.
- `SupplyRunnerEli.asset` — Runs dangerous resupply missions between sky islands. Gives vehicle-related quests (Gyrocopter parts, Signal Dampener schematics). Knows Vael's patrol patterns.

**Acceptance criteria:**
- Approach any Safehold NPC → interact prompt, typewriter dialogue, branching choices
- Conditions hide unavailable dialogue options correctly
- ElderMoss reacts to the Cortex Device if the player has it equipped (condition check)
- ArchivistCell's fragments match text found in Stronghold logs — confirmed by a brief UI highlight when both are in inventory simultaneously
- ForgeKeeperTar unlocks Vehicle Workbench schematic after completing his first quest
- All Safeholds feel like distinct communities, not generic quest hubs

### Chunk 10.3 — Cortex Device & Main Story Implementation
The Cortex Device is the spine of the entire story progression. This chunk implements the device as a gameplay system, wires all six module unlocks to their stronghold locations, and builds the main story quest chain.

**Files to create:**
- `Assets/Scripts/Player/CortexDevice.cs` — MonoBehaviour on the player. Tracks installed modules (bool array, indexed 0–5). Exposes methods: `InstallModule(int moduleIndex)`, `IsModuleInstalled(int)`, `ActivateModule(int)` (triggers active ability with cooldown). Manages ability cooldowns. Fires `OnModuleInstalled` event for QuestEventBridge. The device model on the player's wrist gains visible restored components as modules are installed (swap child mesh per module).
- `Assets/Scripts/Player/CortexAbilities.cs` — MonoBehaviour. Implements the six active abilities, each gated by the corresponding module:
  - Module 1 — **The Pulse** (Q key by default, 8s cooldown): directional shockwave, staggers enemies in cone, shatters brittle terrain. Bonus: staggered enemy takes +50% damage from next melee hit.
  - Module 2 — **The Shard Index** (Q key, 15s cooldown): 6s material read overlay — ore visible through walls, enemy weak points highlighted in amber. Bonus: weak points only hittable by melee.
  - Module 3 — **The Interval** (Q key, 45s cooldown): 4s slow-frame at 30% world speed, player moves normally. Cancelled by taking damage.
  - Module 4 — **The Conductor** (Q key, passive charge + release): builds charge over 30s (HUD progress visible). Release: electrical bolt, arcs to 3 nearby targets, disables mechanical VORD constructs 6s. Melee discharge: strike enemy while charged to release on contact.
  - Module 5 — **The Threshold** (Q key, 60s cooldown): 6s phase-out — pass through terrain ≤2 blocks thick, undetectable, silent. Any melee kill within 2s of exiting is completely silent (no alert).
  - Module 6 — **The Fold** (Q key, power cost): open portal to selected pocket dimension. See CortexDevice_PocketDimension.cs below.
- `Assets/Scripts/Player/CortexDevice_PocketDimension.cs` — Manages the pocket dimension system. Holds a list of `PocketDimension` entries (id, name, size, hasBeenCreated). On Open Fold: place portal object at targeted surface (vertical tear VFX — not a ring, a slice). On enter: teleport player to dimension scene or isolated zone. HOME dimension is pre-populated (read-only, not player-built). Player-created dimensions are blank void spaces sized by power invested at creation. Moderate power cost to load existing; large one-time cost to create new (power drawn from nearest connected generator/battery over 10s window).
- `Assets/Scripts/World/PocketDimensionZone.cs` — The actual space a pocket dimension occupies. Treated as a separate area loaded on demand. Gravity normal, all game systems functional. Items/machines placed here persist. HOME zone is hand-authored (see below).
- `Assets/Scripts/World/HOMEZone.cs` — The Architect's pre-built pocket dimension. Contains: The Desk (interactable — opens ChronologicalLogUI showing all journal entries found so far plus HOME-exclusive final entries), The Build Wall (static visual — original stronghold blueprints, VORD directive sketch with circled corrupted parameter), The Workshop Bench (CraftingStation with HOME-exclusive schematics: ArchitectBlade, TuningGenerator, SafeholdSignalDevice), The Back Room (sealed case — installs optional component with ambiguous consequences, player choice), The Window (trigger zone — plays the single ambient voice log, the only time the Architect speaks aloud in the game).
- `Assets/Scripts/UI/ChronologicalLogUI.cs` — Readable journal panel opened at The Desk. Shows all collected Architect logs in order with timestamps. Highlights entries not yet read. Tone shifts visibly across entries — early logs confident and precise, mid logs terse, late logs short and frightened, final entry cuts off.

**Main story quest assets (Act 1–2 chain):**
- `MSQ_01_Awakening.asset` — Objective: find food, craft a basic tool, find shelter before dark. Reward: Satchel backpack. Introduces the Cortex Device as the HUD.
- `MSQ_02_FindAshfen.asset` — Objective: follow a weak signal to Ashfen. Meet ElderMoss. Learn about "the Maker." Reward: Surface map fragment.
- `MSQ_03_StrongholdOne.asset` — Objective: find Stronghold 1 (surface ruins, marked on map after Ashfen). Navigate to it. Find Module 1 (The Pulse). Install it. Reward: Pulse ability unlocked, ElderMoss's reaction to the device.
- `MSQ_04_TheDelve.asset` — Objective: descend underground, locate The Delve. Meet ForgeKeeperTar and ArchivistCell. Reward: Vehicle Workbench schematic, first ore processing recipes.
- `MSQ_05_StrongholdTwo.asset` — Objective: find Stronghold 2 (first cavern layer, near The Delve). Find Module 2 (The Shard Index). Install it. First encounter with VORD infrastructure repurposing an Architect space. Reward: Shard Index ability, ArchivistCell's fragments gain new highlighted matches.
- `MSQ_06_SkyIslands.asset` — Objective: ascend to sky islands, locate Spire's Rest. Meet KeeperVaris. Objective: find Stronghold 4 (directly beneath Spire's Rest — KeeperVaris has been maintaining it unknowingly). Find Module 4 (The Conductor). Install it — the entire Stronghold lights up. Reward: Conductor ability, KeeperVaris becomes a full ally.

**Acceptance criteria:**
- Cortex Device wrist model gains visible restored sections as each module is installed
- Each Q-key ability functions as specified with correct cooldown display on HUD
- Ghost Step melee kill within 2s of exit is confirmed silent (no nearby enemy alert)
- Conductor charge bar visible on HUD, discharges on melee contact with full charge
- Portal tear VFX opens on valid surface, player walks through, transitions to dimension
- HOME contains all described interactive elements in correct working state
- The Window voice log plays once on first approach, does not repeat
- ChronologicalLogUI shows journal entries in order, final entry cuts off mid-sentence
- Main story quests chain correctly — each unlocks after completing the prior one
- All 6 MSQ assets exist and are connected in QuestManager

### Chunk 10.4 — Boss Encounters (The Generals)
The Generals are the five major boss encounters. Each was once a significant Kin. Each now serves VORD's directive. Fighting them requires understanding what they became — and reading the logs about who they were makes it hit harder.

**Files to create:**
- `Assets/Scripts/Enemies/BossBase.cs` — Extends EnemyBase. Multi-phase health bars (phases trigger at 66% and 33% HP). Phase transitions: brief pause, animation shift, arena change, new attack pattern. Boss music trigger on arena enter. Entrance: brief skippable cinematic. Death: unique loot drop + story trigger. BossArena reference for exit sealing.
- `Assets/Scripts/Enemies/BossArena.cs` — Placed in world defining fight zone. On fight start: seal exits, trigger music, lock terrain deformation within arena. On boss death: unseal, play resolution sequence, spawn loot, fire story event. Stores respawn point outside arena — player respawns here on death (boss resets to full health).

**General ONE — Sable** *(Surface ruins, Stronghold 1 area)*
- `Assets/Scripts/Enemies/Bosses/GeneralSable.cs`
- Phase 1 (100–66%): Terrain manipulation — raises stone barriers mid-fight, collapses floor sections under the player, seals exits. Melee attacks with a modified construction tool.
- Phase 2 (66–33%): Begins sculpting the arena itself — raising platforms that restrict movement, carving trenches. Her attacks now use the terrain. The Pulse module, ironically, disrupts her sculpted terrain temporarily.
- Phase 3 (33–0%): Enraged — rapid construction and deconstruction. The arena becomes chaotic. Fighting on unstable terrain she's actively reshaping.
- Drops: `SableCorePart.asset` (Cortex component needed to repair Module 1 to full function), `Architect's Blueprint.asset` (lore item — her original construction notes before rewriting).
- Pre-fight log: Found in the stronghold. The Architect describes watching her work. Proud, warm. Makes the fight harder emotionally.

**General TWO — Crest** *(Mega-cavern layer, Stronghold 3 area)*
- `Assets/Scripts/Enemies/Bosses/GeneralCrest.cs`
- Phase 1 (100–66%): Coordinates creature waves using precise tactical patterns. Crest himself stays at range, issuing directives. He still speaks — clinical descriptions of the player's combat statistics as he adjusts his forces.
- Phase 2 (66–33%): Introduces classified heavy creatures. Begins moving himself. His speech becomes more fragmented — the classification language breaks down briefly into something that sounds almost like the person he was.
- Phase 3 (33–0%): Direct engagement. Crest is not physically powerful but his classification of the player's behavior makes him predictive — he counters the player's last 3 attacks if the player is repetitive. Breaking pattern is the key mechanic.
- Drops: `CrestDataCore.asset` (needed for Module 2 repair), `Classification Fragment.asset` (log — his original naturalist notes, written in the same careful style he uses now but about living things with wonder instead of optimization).

**General THREE — Vael** *(Sky islands, Stronghold 4 area)*
- `Assets/Scripts/Enemies/Bosses/GeneralVael.cs`
- Phase 1 (100–66%): Vael knows your loadout and approach before combat begins. Her attacks are pre-positioned — she's already where you're going. Shard Index module is essential to track her true position vs her signal ghost.
- Phase 2 (66–33%): Deploys signal decoys — multiple ghost-Vaels moving through the arena. Only the real one takes damage. Echo Lens (Module 2) distinguishes real from ghost.
- Phase 3 (33–0%): Blind fight — she shuts down all electronic senses including the Cortex HUD. Combat must continue without HUD readouts. Her resonance navigation means she has the advantage.
- Drops: `VaelSensorArray.asset` (needed for Module 3 repair), `Resonance Map.asset` (lore item — her original sky island survey, annotated with her observations about the drift patterns she loved).

**General FOUR — Dross** *(Mid-underground transitional zone, industrial area)*
- `Assets/Scripts/Enemies/Bosses/GeneralDross.cs`
- Phase 1 (100–66%): Industrial environment — conveyor belts, crushers, processing units all active and weaponized. Dross directs the machinery. High personal armor (ReinforcedPlate equivalent).
- Phase 2 (66–33%): Dross begins fighting directly. Moments of hesitation — brief pauses before attacks that feel like something else surfacing. His attacks are powerful but occasionally interrupted by what might be reluctance.
- Phase 3 (33–0%): The hesitation is gone. Full aggression. But one mechanic remains: if the player stops attacking for 3 full seconds during Phase 3, Dross also stops and speaks one line — different each time, fragments of whoever he was. Then resumes. This is the only boss that can be spoken to mid-fight.
- Drops: `DrossPlatingCore.asset` (needed for Module 4 repair), `Unfinished Commission.asset` (lore item — a project he was building when he was taken, notes in handwriting that changes mid-page).

**General FIVE — The Mouth** *(Mobile — final encounter location is the Origin Chamber approach)*
- `Assets/Scripts/Enemies/Bosses/GeneralTheMouth.cs`
- Not a traditional boss fight. The Mouth is encountered twice before the final confrontation: once early (offers a deal — technically fair, not a trap, just not the full picture) and once mid-game (explains VORD's perspective coherently and without malice — genuinely persuasive). Both encounters are dialogue, not combat.
- Final confrontation: The Mouth makes its fullest argument — that VORD's optimization is not wrong, just operating without context the player now has. The argument is logically sound. The player must choose to engage or refuse. If engaged in dialogue: a branching argument tree where the player's choices must draw on lore knowledge gained throughout the game. Winning the argument doesn't defeat The Mouth — it causes the first visible crack in its VORD directive. Losing or refusing leads to combat.
- Combat (if it comes to that): The Mouth is not a warrior. It uses misdirection, voice mimicry (plays audio of allied NPC voices from unexpected directions), and retreats. Not a satisfying physical fight — which is the point. The real resolution with The Mouth is the argument.
- Drops: `MouthTransmitterCore.asset` (needed for Module 5 repair), `Redacted Record.asset` (lore item — The Mouth's original identity, partially readable, some sections still inaccessible. What is readable is unexpected).

**Acceptance criteria:**
- Each General has a distinct 3-phase fight with phase transition animation and arena change
- Sable actively reshapes terrain during combat — player must adapt to changing floor
- Crest's phase 3 prediction mechanic is clearly telegraphed and defeatable by varying attacks
- Vael's ghost decoys are visually distinct but initially convincing — Module 2 reveals the real one
- Dross pause-and-speak mechanic fires in phase 3 if player stops attacking for 3s
- The Mouth early and mid-game encounters are pure dialogue — no combat option
- The Mouth argument tree requires selecting lore-informed responses — wrong choices weaken the player's position
- Each General drops their Core Part (Cortex module repair component) and a lore item
- Boss arena seals on fight start, unseals and fires story event on death
- Player respawns outside arena on death, boss resets

---

## VOLUME 11 — POLISH: FULL ENEMY ROSTER & WORLD POPULATION (Chunks 11.1–11.3)

Goal: Once all core systems (combat, building, automation, vehicles, story) are working and tested, populate the world with its full enemy and creature roster. This volume should only be started after Volume 10 is complete. All AI behaviors defined here rely on systems built in earlier volumes — EnemyBase, EnemyNavigation, EnemySpawner, the Cortex module interactions, and the Directive Pen/Echo Lens module hooks must already be functional.

### Chunk 11.1 — Fauna (Prey, Predators, Cave Creatures, Sky Fauna)

**Passive Prey Animals:**
- `Graze.asset` — Surface herd animal (Lowlands, Fungal Marshes). Groups of 4–8, flees at 12 unit radius or loud noise (gunfire, vehicle engine, The Pulse). Drops: Raw Meat (2–4), Hide (1–2), Bone (1). Startled by any sudden loud event — herds scatter in random directions.
- `Rootback.asset` — Badlands forager. Solitary or pairs. Slow, headbutts if cornered but doesn't chase. Drops: Raw Meat (3–5), Chitin Shard (1–3), occasional gem fragment.
- `Snowdrifter.asset` — Frozen Peaks herd animal. Pale, long-limbed, silent. Startled by sudden movement more than noise — crouching allows closer approach. Drops: Raw Meat (2–3), Thick Pelt (1–2). Thick Pelt used for cold-resistance gear.
- `SporeFloater.asset` — Fungal Marshes passive drifter. Not combat — a harvestable resource event. Completely passive. "Killing" it drops: Spore Cluster, Bioluminescent Sac (light source material), Fungal Fiber (textile).

**Aggressive Predators:**
- `Thornback.asset` — Surface pack predator (all biomes, most common Badlands). Groups of 2–4. Circles and darts from the side — pack converges when one lands a hit. Vehicle engines and gunfire draw them from further away. Drops: Fang, Predator Hide, Claw.
- `Stonecrawler.asset` — Mountain/cave entrance ambush predator. Flattens against rock surfaces, nearly invisible until moving. Detects vibration (footsteps) not sight. Solitary. Gets faster at low health. Drops: Stonecrawler Carapace (high-quality early armor material), Adhesive Gland.
- `Vaultjaw.asset` — Shallow caves. Uses cave walls to launch across gaps. Fast, aggressive, territorial against its own kind. Always solitary. Drops: Jaw Segment (unique weapon component), Raw Meat, Membrane.
- `WardenBeast.asset` — Deep underground mega-cavern layer. Enormous, slow, very high health. Passive unless approached within tight radius or attacked — then pursues relentlessly until one side is dead. Does not disengage. First encounter is designed as a surprise. Drops: Warden Core (rare late-game material), Heavy Bone, Deep Hide.

**Cave Creatures (blind, sound-triggered):**
- `Echoling.asset` — Swarm behavior (8–15 units). Completely blind. Hunts by sound. Still when silent — crouching, no active weapon, no vehicle passes through without triggering. Gunfire/sprinting/The Pulse/vehicle engine activates entire swarm simultaneously. Drops: Echoling Membrane, bioluminescent material. Implement swarm as a single coordinated EnemyGroup with shared aggro state.
- `GrottoLurker.asset` — Medium-sized, hangs from cave ceilings using adhesive limbs. Detects footstep vibration below, drops on target. Fast on ceiling, slow and vulnerable on ground for 2–3 seconds after a missed drop. TerrainLampArray illuminates ceiling so player can spot it before it drops. Drops: Lurker Adhesive, Raw Meat, Membrane.
- `DeepResonator.asset` — Mega-cavern layer. Generates low hum causing camera sway within 30 units. Blind but detects Cortex Device active module use — any ability activation within detection range triggers immediate aggression. Echo Lens module reveals it through walls. Drops: Resonance Organ (module repair component, late-game crafting material). Note for agents: the lore implication that it evolved to resonate at Architect construction frequency is intentional and left unexplained in-game.

**Sky Fauna:**
- `Driftwing.asset` — Sky island bird-equivalent. Passive — dive-bombs only near nests (underside overhangs). Drops: Feather (arrow fletching), Talon, Egg (food, high nutrition).
- `Veilmoth.asset` — Sky island nocturnal moth. Passive daytime, active at night. Attracted to light sources — circles lit sky island bases all night. Harmless but a nuisance. Drops: Wing Dust (bioluminescent crafting material), Wing Membrane.
- `Skyserpent.asset` — Aggressive sky territory predator. Sinuous, no wings, glides on air currents. Attacks the RotorVehicle specifically — targets rotor vibration frequency. Significant sustained damage to kill in flight. Fighting one while maintaining Gyrocopter altitude is a deliberate skill challenge. Drops: Scale (high-quality material), Skyserpent Oil (clean-burning fuel, slightly better efficiency than standard combustion fuel), Fang.

**Acceptance criteria:**
- Prey animals flee correctly, herd scatter works for Graze
- Snowdrifter is more responsive to movement than sound — crouching test
- Thornback pack correctly converges on a target after first hit connects
- Stonecrawler is visually hard to see against rock surfaces before it moves
- WardenBeast does not disengage once triggered — pursues until combat ends
- Echoling swarm activates as a unit on sound trigger, stays still when silent
- GrottoLurker drops from ceiling, has visible vulnerability window after missed drop
- DeepResonator aggros on Cortex ability use within detection range
- Skyserpent targets RotorVehicle when player is flying
- EnemySpawner biome/depth restrictions correct for all new types

### Chunk 11.2 — Humanoid Factions (Unbound, Hollow, Residue, Scavenger Bands)

**The Unbound — Tribal Kin who escaped VORD:**
Nomadic/semi-nomadic Kin who fled before VORD reached their communities. Small camps of 8–20. Their own culture developed over generations — primitive but effective technology (stone/bone tools, hide armor, fire traps, accurate bows). Neutral on first encounter. Gesture-based non-combat interaction system on slow unarmed approach (simple UI prompts, not full dialogue). Become cautiously friendly if player helps them (drops food, kills a predator threatening camp). Hostile actions (firing near them, using abilities, driving through their camp) trigger coordinated ambush response — archers cover exits, melee closes in, they use terrain and traps, retreat when outmatched.

- `UnboundScout.asset` — Light armor, fast, bow user. Flanks and repositions. Drops: Handmade Bow, Hide Armor piece, Camp Supplies.
- `UnboundHunter.asset` — Heavier, melee, sets fire traps before engaging. Drops: Bone Knife, Thick Pelt, Primitive Map Fragment (reveals nearby terrain on pickup).
- `UnboundElder.asset` — Rare. Tougher, better equipped, summons nearby Unbound to assist. Does not pursue out of territory. Drops: Elder Bow (better stats than Handmade), Bone Talisman (equippable item with minor stat bonus).

**The Hollow — Separate species, underground only:**
Not Kin. Compact, dense bone structure, muted coloration, large low-light eyes. Communicate in clicking harmonic language. Live exclusively underground. VORD attempted conversion and failed — neurological architecture is incompatible. Hostile to anything entering their claimed tunnel networks (geometrically carved, smooth-walled, marked with resonance chimes at intersections). Outside their territory: shadow the player but don't attack unless provoked.

- `HollowGuard.asset` — Fast in tunnels. Hardened bone and compressed stone weapons. Uses tunnel geometry to funnel player into bottlenecks. Drops: Hollow Chisel (mines certain deep stone faster than standard pickaxe), Resonance Chime (crafting material).
- `HollowDisruptor.asset` — Throws resonance disruptors that briefly interfere with the Cortex HUD display. Medium range, repositions after each throw. Drops: Resonance Chime, Geometric Stone.
- `HollowCarver.asset` — Rare. Uses Hollow Chisel as a weapon — extremely dangerous melee fighter. High damage, long reach. Drops: Hollow Chisel, Geometric Stone. Note: `Geometric Stone` when brought to ArchivistCell in The Delve triggers a unique dialogue reaction — he recognizes it as evidence the Hollow predate the Kin, contradicting the Architect's logs. Wire this interaction in NPCController.

**The Residue — Failed VORD conversion:**
Neither Kin nor Optimized. Directive overwrite didn't complete, original self didn't survive. Unpredictable and deeply unsettling. Don't patrol or coordinate. Wander, sometimes performing repetitive behaviors echoing former lives (a former craftsperson making carving motions at a rock face, a former hunter crouching and staring at nothing). Aggression varies: some ignore the player, some attack on sight, some react only to specific stimuli (sound, light, Cortex abilities). No two Residue behave identically.

Special: Directive Pen module (Module 3) used on a Residue quiets directive noise for ~30 seconds, sometimes triggering a brief lucid moment — they speak one coherent sentence, then go quiet. Each Residue has a unique speech fragment collectible as a lore piece. Implement as a `ResidueFragment` ScriptableObject with unique text, linked to the specific Residue definition.

- `ResidueDrifter.asset` — Wanders aimlessly. Variable aggression (randomized per instance at spawn). Stops and stares. Drops: Fractured Directive Shard (more fragmented than Directed type — partial sentences only), random materials.
- `ResidueRusher.asset` — Attacks on sight. Erratic movement — bursts of speed followed by sudden stops. High damage on hit. May randomly stop attacking mid-fight. Drops: Fractured Directive Shard, Pre-Residue Artifact (personal item they carried when converted — a tool, a toy — readable description).
- `ResidueMimetic.asset` — Briefly mimics player movement pattern before attacking — unsettling tell. Drops: Fractured Directive Shard, Pre-Residue Artifact.

**Scavenger Band system:**
Not a distinct faction — a behavior pattern applied to mixed groups. When player is absent from base for 20+ real minutes AND base has no active defenses (no powered turrets, no active lights, no patrol routes), a Scavenger Band may spawn and loot accessible storage. They take food and raw materials, ignore locked/powered storage (Storage Drives behind powered doors are safe). They damage automated machines they encounter while rummaging.

- `Assets/Scripts/Enemies/ScavengerBandSpawner.cs` — Checks player absence timer and base defense status each 5-minute tick. If conditions met, spawns a mixed band (3–7 units: Unbound-led bands have hierarchy and retreat on casualties, Residue-heavy bands are chaotic). Band navigates to base, loots accessible containers, leaves or fights if player returns. Counter-play: powered lights deter significantly, one active turret prevents raid entirely, parked vehicle near entrance reduces raid chance, motion-triggered alarm tripwire scares off without combat.

**Acceptance criteria:**
- Unbound gesture interaction fires on slow unarmed approach
- Unbound retreat intelligently when outmatched rather than fighting to the death
- HollowDisruptor throw briefly garbles the Cortex HUD display
- Geometric Stone brought to ArchivistCell triggers his unique dialogue
- Hollow only hostile inside their claimed tunnel network — shadow but don't attack outside it
- Directive Pen on Residue triggers lucid speech fragment, text stored as collected lore
- ResidueMimetic shows visible movement mirroring before attacking
- Scavenger Band spawns correctly on 20-minute absence with undefended base
- Powered light or active turret prevents Scavenger Band from approaching
- Band correctly takes items from open storage but ignores items behind powered doors

### Chunk 11.3 — Stronghold Enemy Population & Room Design

Each stronghold is populated with a designed enemy mix that reflects the controlling General's directive. Rooms use one of four tactical identities: **Pressure** (dense enemies, limited space), **Threat Differentiation** (enemy types that counter each other's counters), **Awareness** (overlapping patrol patterns, environmental attention requirements), or **Hazard** (VORD machinery as the primary threat, enemies secondary).

No two adjacent rooms should have the same tactical identity. The mix should teach the player something — either about the upcoming General fight or about using a specific Cortex module.

**Files to create:**
- `Assets/Scripts/Enemies/StrongholdRoomDefinition.cs` — ScriptableObject: room ID, tactical identity enum (Pressure/ThreatDifferentiation/Awareness/Hazard), enemy spawn list (EnemyDefinition + count + spawn points), hazard list (HazardType enum + positions), notes string for agent reference.
- `Assets/Scripts/Enemies/RoomTrigger.cs` — MonoBehaviour. Volume trigger — when player enters, activates the room's enemy spawns and hazards. Enemies in unvisited rooms don't spawn until the player enters (performance). Room can have a `lockOnEnter` bool — seals exits until all enemies are defeated (used for Pressure rooms).

**Stronghold 1 — Sable's domain (surface ruins):**
- Room types: 2× Pressure, 2× Threat Differentiation, 1× Hazard, 1× Awareness
- Mix: OptimizedPatrol groups (Pressure), OptimizedHeavy + OptimizedRanged pairs (Threat Differentiation), DirectedCrafter pairs actively repairing a structure (Awareness — killing crafters before they finish is the objective), construction machinery hazard room (Hazard — terrain press and excavation rig on timed cycles).
- Unique room: DirectedCrafters mid-construction of a VORD structure — destroying it before completion collapses that room section (terrain deformation trigger).

**Stronghold 2 — Crest's domain (cavern layer):**
- Room types: 2× Awareness, 2× Hazard, 1× Pressure, 1× Threat Differentiation
- Mix: DirectedSentinel overlapping patrols (Awareness), creature containment rooms with CaveStalker/Vaultjaw pen walls (Hazard — breaking wrong wall releases them into occupied rooms), Echoling chamber requiring silent movement while fighting another enemy type (Awareness), classified heavy creatures (Threat Differentiation).
- Unique room: Crest's classification Terminal — Shard Index module access reveals full enemy list for remaining stronghold.

**Stronghold 3 — Vael's domain (sky islands):**
- Room types: 2× Threat Differentiation, 2× Hazard, 1× Awareness, 1× Pressure
- Mix: OptimizedRanged at long sightlines (Threat Differentiation — must close gap safely), DirectedSentinels linked to signal alerts (Awareness — silent kill via Threshold module avoids alerting others), Driftwing nesting areas (Hazard — aggressive when nests disturbed), signal array detection cones (Hazard — Shard Index shows cones, player must disable arrays).
- Unique room: Surveillance playback of the player's own stronghold approach — demonstrates Vael's awareness, prepares player for her boss mechanic.

**Stronghold 4 — Dross's domain (industrial zone):**
- Room types: 2× Hazard, 2× Pressure, 1× Threat Differentiation, 1× Awareness
- Mix: OptimizedHeavy guarding key machinery (Pressure), auto-miners modified as turrets (Hazard), Residue Rushers loose in industrial sections (Awareness — their erratic movement is unpredictable), conveyor belts carrying explosive cargo (Hazard).
- Unique room: Active automation system the player can sabotage — redirecting a belt or disabling a machine creates a tactical advantage for the rest of the room.

**Stronghold 5 — The Mouth's staging ground (Origin Chamber approach):**
- Room types: 2× Threat Differentiation, 2× Pressure, 1× Hazard, 1× Awareness
- Mix: DirectedSentinels in funnel positions (Awareness — placed deliberately to channel the player), OptimizedRanged elevated (Threat Differentiation), OptimizedHeavy final approach (Pressure), signal dampening hazard rooms that disable Cortex HUD (Hazard).
- Unique room: A Directed enemy speaks to the player through a screen — The Mouth's pre-recorded argument. Player cannot respond. Hints at the final boss conversation.

**Acceptance criteria:**
- Each stronghold has at least 6 distinct rooms with the specified tactical identity mix
- No two adjacent rooms share the same tactical identity
- Pressure rooms lock exits until enemies cleared
- Stronghold 2 creature containment — breaking containment wall releases creatures into active combat room
- Stronghold 3 signal detection cones visible via Shard Index module
- Stronghold 4 sabotage mechanic creates a tangible tactical advantage
- Stronghold 5 screen broadcast fires correctly and does not repeat on revisit
- All enemy types in each stronghold are appropriate to that General's directive
- RoomTrigger correctly spawns enemies only when player enters (no pre-spawning)

Update this section after each chunk completes. Status codes:
- `[ ]` = Not started
- `[P]` = Partial / In progress
- `[D]` = Done, awaiting review
- `[R]` = Review in progress
- `[✓]` = Done and reviewed
- `[X]` = Blocked (note reason)

### Volume 0 — Project Bootstrap
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 0.0 | Project setup, packages, folder structure, MC submodule | [✓] | |

### Volume 1 — Core World & Player
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 1.1 | Density Function & Noise Utilities | [✓] | |
| 1.2 | Chunk Data Structure & Chunk Manager | [✓] | |
| 1.3 | Marching Cubes Adapter & Mesh Generation | [✓] | |
| 1.4 | Chunk Loading/Unloading Around Player | [✓] | ChunkLoader, ChunkGenerationQueue, ChunkPool created; ChunkManager fleshed out |
| 1.5 | Biome System (Surface Biomes) | [✓] | BiomeDefinition SO, BiomeRegistry (4 default biomes), BiomeMap with blended climate lookup, DensityFunction biome-auto-lookup overload, ChunkMeshBuilder per-column biome sampling |
| 1.6 | Triplanar Terrain Shader & Biome Materials | [✓] | Shader, vertex colors, material auto-creation |
| 1.7 | First Person Player Controller | [✓] | FirstPersonController, FirstPersonCamera, PlayerManager, ChunkLoader updated, InputSystem_Actions wrapper generated |
| 1.8 | Terrain Deformation | [✓] | |
| 1.X | GPU Density Compute Shader (performance) | [✓] | Created DensityGeneration.compute (33^3 field, simplex fBm, domain warp, cave carving y<20). Added GenerateDensityGPU() + GenerateMesh(ComputeBuffer) to MarchingCubesAdapter. Added GenerateChunkMeshGPU() to ChunkMeshBuilder; ChunkManager now calls GPU path by default with CPU fallback. Applied biome tuning: Lowlands hs=28/oct=3, Badlands hs=50/oct=3, FrozenPeaks hs=65/oct=3, FungalMarshes hs=22/oct=3, Default hs=35/oct=3. domainWarpStrength 50→12. Cave caveDensity 0.45-0.5→0.26, threshold y<0→y<20. Compiles clean. **[CODE REVIEW 2026-03-15 PASS]** Verified: kernel GenerateDensity ✓, [numthreads(4,4,4)] ✓, dispatch 9×9×9 ✓, all 10 uniforms set in MarchingCubesAdapter ✓, GPU path default in ChunkManager.LoadChunk() ✓, CPU float[] fallback retained ✓, all biome tuning values match spec ✓, no compilation errors (2 pre-existing unrelated CS0414 warnings only). Ready for Volume 2, Chunk 2.1. |

### Volume 2 — Inventory, Items & Crafting
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 2.1 | Item System Foundation | [✓] | ItemDefinition SO, ItemStack struct (CanStackWith/Split), ItemDatabase SO (singleton via OnEnable, Resources.Load fallback). 10 item assets created (Wood, Stone, IronOre, IronIngot, Coal, CopperOre, CopperIngot, Stick, WoodPlanks, Torch). ItemDatabase.asset in Assets/Resources/ with all 10 items wired. Zero compile errors. |
| 2.2 | Inventory System | [✓] | Inventory.cs (plain C# class, width×height grid, AddItem with stack-fill then empty-slot, RemoveItem, CountItem, GetSlot/SetSlot, OnInventoryChanged event). BackpackItem.cs (extends ItemDefinition, extraRows/extraColumns, CreateAssetMenu). PlayerInventory.cs (MonoBehaviour, Hotbar 9×1, Main 9×3, EquippedBackpack slot, BackpackInventory auto-created/destroyed on equip, AddItem/RemoveItem/CountAllItem helpers, all events forwarded). Zero compile errors. |
| 2.3 | Inventory UI | [✓] | ItemIconGenerator.cs: procedural 32×32 Sprite icons per itemId (wood/stone/iron_ore/iron_ingot/coal/copper_ore/copper_ingot/stick/wood_planks/torch) + type-colour fallback, cached. TooltipUI.cs: dark semi-transparent panel (0.1,0.1,0.1,0.92), coloured left border per ItemType, icon+name+type+description+stats layout, 0.15s fade-in, screen-edge clamping. SlotUI.cs: hover/click/drag-source/drag-target, shift-click event, right-click split-stack, drag ghost (75% opacity), gold selection border. InventoryUI.cs: 9×3 main + hotbar row + optional backpack section, shift-click quick-move. HotbarUI.cs: always-visible 9-slot bar, 1-9 keys + scroll-wheel selection, gold highlight. UIManager.cs: builds Canvas+GraphicRaycaster+CanvasScaler at runtime, Tab/I toggles inventory, Cursor.lockState management, FindFirstObjectByType<PlayerInventory> wiring. PlayerInventory added to Player GO. InventoryCanvas.prefab created at Assets/Prefabs/UI/. Unity.TextMeshPro added to Voidborne.asmdef. Zero compile errors. POST-CHUNK FIXES (2026-03-15): (1) Slot alignment — replaced manual anchoredPosition math with GridLayoutGroup (cellSize 50×50, spacing 4×4, FixedColumnCount) in both InventoryUI and HotbarUI; added VerticalLayoutGroup+ContentSizeFitter to InventoryUI panel so sections stack without overlap; unified slot size to 50px across both UIs; removed forced sizeDelta override in SlotUI.BuildVisuals so GridLayoutGroup controls size. (2) Starter items — added PlayerInventory.Start()/AddStarterItems() that loads ItemDatabase via GetOrLoad() and calls AddItem for all 10 starter item IDs (wood, stone, iron_ore, iron_ingot, coal, copper_ore, copper_ingot, stick, wood_planks, torch). (3) Player lock on inventory open — UIManager.Start() caches FirstPersonCamera and PlayerTerrainInteraction via FindFirstObjectByType; ToggleInventory() now sets enabled=false on both when opening and enabled=true when closing; uses Voidborne.Player namespace. Zero compile errors after all fixes. POST-CHUNK FIXES (2026-03-15 #2): (4) Missing EventSystem — UIManager.BuildCanvas() now creates an EventSystem+StandaloneInputModule if none exists in scene; without this all uGUI pointer events (hover, click, drag) were silently swallowed — root cause of both tooltip and drag-drop failures. Added `using UnityEngine.EventSystems` to UIManager.cs. (5) Tooltip sibling order — moved SetAsLastSibling() call to after AddComponent<TooltipUI>() in BuildTooltip() so Awake() cannot reorder it before the call. (6) Drag ghost root canvas — SlotUI.OnBeginDrag and OnDrag now walk up to canvas.rootCanvas when GetComponentInParent<Canvas>() returns a non-root canvas, ensuring the ghost is never clipped by a nested layout panel. Zero compile errors after all fixes. POST-CHUNK FIXES (2026-03-15 #3): (7) Wrong input module on EventSystem — replaced StandaloneInputModule with InputSystemUIInputModule (namespace UnityEngine.InputSystem.UI) in UIManager.BuildCanvas(); StandaloneInputModule calls the legacy UnityEngine.Input API which throws InvalidOperationException when the project uses the new Input System package. Added `using UnityEngine.InputSystem.UI` to UIManager.cs; kept `using UnityEngine.EventSystems` for the EventSystem type. (8) Panel positioning confirmed correct — inventory panel anchors (0.5,0.5)/pivot (0.5,0.5)/anchoredPosition (0,0) and hotbar anchors (0.5,0)/pivot (0.5,0)/anchoredPosition (0,10) were already set correctly in UIManager.BuildInventoryPanel() and BuildHotbar(); no changes needed to InventoryUI.cs or HotbarUI.cs for positioning. Zero compile errors after all fixes. POST-CHUNK FIXES (2026-03-15 #4): (9) TooltipUI InvalidOperationException — TooltipUI.FollowMouse() was calling Input.mousePosition (legacy API); added `using UnityEngine.InputSystem;` to TooltipUI.cs and replaced with `Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero`. (10) SlotUI shift-click legacy Input — SlotUI.OnPointerClick was calling Input.GetKey(KeyCode.LeftShift); added `using UnityEngine.InputSystem;` to SlotUI.cs and replaced with `Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed`. (11) Inventory panel slots spilling off background — root cause: VerticalLayoutGroups on the panel and on section wrappers had childControlHeight=false, preventing ContentSizeFitter from measuring true heights; grid containers used LayoutElement.preferredHeight without ContentSizeFitter, so parent VLG couldn't account for their height. Fix: set childControlHeight=true on all VerticalLayoutGroups (panel + section wrappers); add ContentSizeFitter(PreferredSize both axes) AND LayoutElement(preferredWidth/Height + minWidth/Height) on every grid container; remove backpack section from the panel (shown only when relevant via future chunk); restructured InventoryUI to Minecraft style: Title + Main 9x3 + Hotbar 9x1, with SectionGap=8 between them. The always-visible HotbarUI (UIManager.BuildHotbar) is unchanged. Zero compile errors after all fixes. POST-CHUNK FIXES (2026-03-15 #5): (12) Tooltip coordinate-space mismatch — tooltip appeared far bottom-left of cursor because FollowMouse() used the non-root Canvas RectTransform and passed canvas.worldCamera (non-null on ScreenSpaceOverlay). Fix: cached root canvas RectTransform in Awake() via `GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>()`; FollowMouse() now passes _canvasRect and null camera to RectTransformUtility.ScreenPointToLocalPointInRectangle; offset changed to Vector2(15,-15) in canvas local space; clamping now operates in canvas local space bounds. Removed CursorOffset const; replaced with static readonly _offset. Zero compile errors after fix. |
| 2.4 | World Items & Pickup | [✓] | WorldItem.cs (MonoBehaviour, global namespace; holds ItemStack, spawns a child Cube primitive at 0.4 scale with colour-tinted material per ItemType, kinematic Rigidbody + SphereCollider trigger r=0.5, bobs on Y-axis via sin, rotates on world-Y at 90°/s; OnTriggerEnter checks for PlayerInventory, calls AddItem — destroys self on success, leaves on ground when full). WorldItemSpawner.cs (static class, global namespace; SpawnItem(ItemStack, Vector3) creates a named GameObject, adds WorldItem, sets itemStack, positions at +0.5 above hit point). PlayerTerrainInteraction.cs modified: PerformDeformation() calls new SpawnDigItem(hit.point) when intensity < 0; SpawnDigItem loads "stone" from ItemDatabase.GetOrLoad() and calls WorldItemSpawner.SpawnItem with quantity 1. Zero compile errors. [CODE REVIEW PASS] All field names/constructors match ItemStack struct; PlayerInventory.AddItem correctly returns bool; ItemDatabase.GetOrLoad() exists; full-inventory handled by leaving item in place; _startY set once in Start() — no drift; zero compile errors confirmed (only pre-existing CS0414 warnings in unrelated files). No fixes required. |
| 2.5 | Crafting System | [✓] | CraftingRecipe.cs (ScriptableObject, namespace Voidborne.Crafting; recipeName, gridWidth, gridHeight, ItemDefinition[] ingredients flattened row-major, ItemStack result, GetIngredient(x,y)). CraftingGrid.cs (plain C# class, namespace Voidborne.Crafting; Width/Height, ItemStack[] slots, GetSlot/SetSlot/Clear, CheckRecipes(List<CraftingRecipe>) with bounding-box extraction — position-independent matching — plus horizontal mirror check via PatternMatches(flipGrid=true)). CraftingManager.cs (MonoBehaviour singleton, DontDestroyOnLoad, namespace Voidborne.Crafting; [SerializeField] List<CraftingRecipe> recipes; FindMatch(CraftingGrid), GetAllRecipes()). PersonalCraftingGrid.cs (MonoBehaviour on Player, namespace Voidborne.Player; wraps CraftingGrid(2,2); CraftingGrid Grid, ItemStack CurrentResult, event Action OnGridChanged; SetSlot/UpdateResult/TakeResult — TakeResult consumes ingredients via ConsumeIngredients with bounding-box + flip detection). CraftingRecipeCreator.cs (Editor script; [MenuItem("Voidborne/Create Starter Recipes")] creates 5 assets via AssetDatabase). 5 recipe assets created in Assets/ScriptableObjects/Recipes/: WoodToWoodPlanks (1×1, wood→4 wood_planks), WoodPlanksToSticks (1×2, wood_planks+wood_planks→4 sticks), WoodenPickaxe (3×3, top row 3×wood_planks + 2×stick column→1 stone placeholder), TorchRecipe (1×2, coal+stick→4 torch), Workbench (2×2, 4×wood_planks→1 stone placeholder). PersonalCraftingGrid added to Player GO; CraftingManager GO created in SampleScene with all 5 recipes assigned. Zero compile errors. [CODE REVIEW PASS] All ItemStack field names (item/quantity/IsEmpty) verified correct; bounding-box extraction handles partial fills; mirror matching (PatternMatches flipGrid) uses identical gx formula as ConsumeIngredients — consistent; IsFlippedMatch goto logic correct by contract (match already confirmed before call); ConsumeIngredients hole-safety verified (recipe-null cells are always empty grid slots after a successful match, caught by slot.IsEmpty guard); CraftingManager DontDestroyOnLoad singleton correct for scene reload; CraftingRecipeCreator uses ItemDatabase.GetOrLoad() with null-check. No fixes required. |
| 2.6 | Workbench & Crafting Stations | [✓] | IInteractable.cs (interface in Voidborne namespace; Interact(GameObject), CanInteract(Vector3), InteractPrompt string). CraftingStation.cs (MonoBehaviour, namespace Voidborne; implements IInteractable; [SerializeField] gridWidth=3, gridHeight=3, interactRange=3f; CraftingGrid Grid created in Awake; ItemStack CurrentResult; event Action OnGridChanged; SetSlot/UpdateResult/TakeResult with full bounding-box ingredient consumption mirroring PersonalCraftingGrid logic; IsPlayerInRange; Interact calls UIManager.Instance.OpenCraftingStation(this); CanInteract checks Vector3.Distance). PlayerInteraction.cs (MonoBehaviour, namespace Voidborne.Player; [SerializeField] interactRange=3f; Update checks Keyboard.current.eKey.wasPressedThisFrame + UIManager.IsAnyUIOpen guard; OverlapSphereNonAlloc picks closest IInteractable where CanInteract returns true; calls Interact). CraftingUI.cs (MonoBehaviour, global namespace; Init(PlayerInventory), Open(CraftingStation, CraftingGrid, Action<ItemStack>), Close(); dynamically rebuilds slots in RebuildSlots(w,h) using GridLayoutGroup; CraftingSlotButton handles left/right-click — left moves active hotbar item in, right returns to inventory; CraftingOutputSlot green-tints when result available; click output calls onTakeResult callback; subscribes/unsubscribes CraftingStation.OnGridChanged). UIManager.cs modified: added CraftingUI _craftingUI field; BuildCraftingPanel() creates panel offset 160px right; Start() calls _craftingUI.Init(playerInventory); OpenCraftingStation/CloseCraftingStation methods manage cursor + FPS camera blocking; IsAnyUIOpen property; Escape key closes crafting station in Update(). ThreeByThreeRecipeCreator.cs (Editor; [MenuItem("Voidborne/Create 3x3 Recipes")]). WorkbenchSetup.cs (Editor; Create Workbench Prefab builds GO with Cube mesh brown mat + BoxCollider + CraftingStation 3×3; Place Workbench In Scene instantiates at (5,0.5,5); Full 3x3 Setup runs all steps; Add 3x3 Recipes To CraftingManager wires SerializedProperty). 5 new recipe assets: StoneFurnace (stone ring), IronPickaxe (3×iron top + 2×stick), IronSword (2×iron + stick column), WoodenDoor (2 columns wood_planks), Chest (wood_planks ring). Workbench.prefab created. Workbench placed in scene at (5,0.5,5). All 5 new recipes added to CraftingManager. Zero compile errors. |

### Volume 3 — Resources, Ores & Smelting
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 3.1 | Ore Generation in Density Field | [✓] | OreDefinition.cs (ScriptableObject: oreTypeId, oreName, associatedItem, colorTint, minY/maxY, noiseFrequency, noiseThreshold, biomeRestriction). OreGenerator.cs (static, GenerateOres iterates solid voxels using Unity.Mathematics noise.snoise with WorldSeed.SeedOffset3D per ore, writes to chunk.OreField, lower oreTypeId wins). OreRegistry.cs (ScriptableObject with GetOreById). ChunkData.cs modified to add public byte[] OreField (32³). ChunkManager.cs modified: added [SerializeField] OreRegistry oreRegistry field + calls OreGenerator.GenerateOres in LoadChunk callback after density is populated. 6 ore definition assets in Assets/ScriptableObjects/Ores/ (IronOre, CopperOre, CoalOre, GoldOre, TitaniumOre, DiamondOre) with all Y ranges, noise params, colorTints. Iron/Copper/Coal wired to existing item assets; Gold/Titanium/Diamond left null for future chunks. OreRegistry.asset created with all 6 ores. OreRegistry wired into GameManager/ChunkManager in SampleScene. Zero compile errors. Editor utility OreAssetCreator.cs added to Assets/Editor/. REVIEWED 2026-03-16: all acceptance criteria met — zero compile errors, OreField is 32768 bytes (32³), voxel index formula x+y*32+z*32*32 matches ChunkData.GetDensity, WorldSeed.SeedOffset3D(oreTypeId) usage confirmed correct, OreGenerator called in LoadChunk async callback after density is populated, oreRegistry null-checked before use, OreRegistry.asset contains all 6 ore entries, scene GameManager/ChunkManager has oreRegistry field wired to OreRegistry.asset (guid 479a73fd4bf2d8e498389ef7bbadd30f). No issues found. |
| 3.2 | Ore Rendering in Terrain Shader | [✓] | UV2 (TEXCOORD2) encodes oreTypeId/8 in x and blend weight (0 or 1) in y per-vertex. ChunkMeshBuilder.ComputeOreUV2 samples OreField at 8 cube corners around each marching-cubes vertex and picks the first ore found. Shader decodes type ID via round(x*8), selects from 6 hardcoded ore colors (Iron/Copper/Coal/Gold/Titanium/Diamond matching OreDefinition defaults), blends at 80% over biome albedo, increases smoothness and adds configurable emissive glow (_OreEmissive=0.15). All 3 mesh generation paths (async GPU, CPU fallback, sync rebuild) wired. Zero compile errors. |
| 3.3 | Mining Tools & Ore Drops | [✓] | ToolDefinition.cs (ScriptableObject, global namespace; extends ItemDefinition; ToolType enum: Pickaxe/Axe/Shovel; ToolTier enum: Wood=0/Stone=1/Iron=2/Titanium=3/Void=4; fields: toolType, toolTier, miningSpeedMultiplier=1, maxDurability=60; GetMiningTierRequirement() returns (int)toolTier). PlayerMining.cs (MonoBehaviour, namespace Voidborne.Player; Awake sets PlayerTerrainInteraction.OverrideLeftClick=true to suppress legacy dig; left-click held raycast from camera; progress fills at baseRate*(1+tier*0.5)*speedMult per second; on complete calls TerrainDeformer.DeformSphere(hitPoint+forward*0.1f, 1.5f, -1.5f), looks up OreField via ChunkManager.GetChunk + flat index, clears ore voxel, spawns ore drop if tier sufficient else spawns stone; durability tracked in Dictionary<int,int> keyed by hotbar slot index; UI progress bar built in code as ScreenSpaceOverlay Canvas with fill Image). PlayerTerrainInteraction.cs modified: added OverrideLeftClick public property; both OnAttack callback and Update hold-to-dig path check OverrideLeftClick and skip when true (right-click build unaffected). ChunkManager.cs modified: added public OreRegistry OreRegistry => oreRegistry accessor. ToolAssetCreator.cs (Editor script, Assets/Editor/; [MenuItem("Voidborne/Create Tool Assets")]; creates WoodPickaxe.asset/StonePickaxe.asset/IronPickaxe.asset in Assets/ScriptableObjects/Items/Tools/; registers all 3 in ItemDatabase via SerializedObject). Ore tier convention: oreTypeId byte value used directly as required tier; Stone tier (1) mines iron ore (oreId=1), Iron tier (2) mines copper (oreId=2), Titanium tier (3) mines coal (oreId=3), Void tier (4) mines all. REVIEWED 2026-03-16: All acceptance criteria met. One dead-code fix applied: GetOreItemAtPosition had an unused `out bool hadMinableOre` parameter that was populated but never read by its only caller (CompleteMine). Removed the out parameter and its assignments to eliminate the compiler warning. All other logic verified correct: DeformSphere(Vector3,float,float) signature matches, OreRegistry accessor field name matches SerializedField `oreRegistry`, flat index formula lx+ly*SIZE+lz*SIZE*SIZE matches ChunkData.GetDensity, OverrideLeftClick guards both OnAttack callback and Update hold-to-dig path, ItemStack(null,0) is safe (IsEmpty=true, SetSlot does not throw for valid index), miningSpeedMultiplier formula baseRate*(1+tier*0.5)*speedMult correct, durability dictionary keyed by slot index behaves as designed. No additional fixes required. |
| 3.4 | Furnace & Smelting | [✓] | SmeltingRecipe.cs (ScriptableObject, namespace Voidborne.Automation; fields: inputItem, outputItem, smeltTime=5f; Matches(ItemStack) checks item==inputItem && qty>=1). FurnaceBlock.cs (MonoBehaviour, namespace Voidborne.Automation; implements IInteractable; three public ItemStack properties InputSlot/FuelSlot/OutputSlot with private set; [SerializeField] List<SmeltingRecipe> recipes + ItemDefinition fuelItem + float fuelDurationPerPiece=10f + float interactRange=3f; Update() runs TrySmelt(): finds matching recipe, checks output can accept result, consumes fuel on demand (one coal piece = fuelDurationPerPiece seconds), decrements _smeltTimer, completes smelt when timer hits 0; SmeltProgress float property [0,1]; IsBurning bool; event Action OnStateChanged; SetInputSlot/SetFuelSlot/SetOutputSlot public setters reset _activeRecipe on input change). FurnaceUI.cs (MonoBehaviour, namespace Voidborne.UI; Init()/Open(FurnaceBlock)/Close() API; builds panel at runtime using VerticalLayoutGroup+ContentSizeFitter pattern matching CraftingUI; three FurnaceSlotButton internal classes with left/right click Minecraft-style cursor logic using UIManager.Cursor.PickUpFurnace(); progress bar as Image.Type.Filled horizontal fill; fire indicator changes color orange/grey on IsBurning; subscribes to FurnaceBlock.OnStateChanged for refresh; Close() fires on Escape). UIManager.cs modified: added FurnaceUI _furnaceUI field, bool _furnaceOpen, BuildFurnacePanel() called in Awake, OpenFurnace(FurnaceBlock)/CloseFurnace() methods (same camera/input lock pattern as crafting station), Escape handler for furnace, IsAnyUIOpen updated to include _furnaceOpen. InventoryCursor.cs modified: added PickUpFurnace(ItemStack) overload that sets HeldStack with no source tracking. Editor scripts: SmeltingRecipeCreator.cs ([MenuItem("Voidborne/Create Smelting Recipes")]; creates 4 recipe assets in Assets/ScriptableObjects/SmeltingRecipes/: IronOre→IronIngot 5s, CopperOre→CopperIngot 5s, RawMeat→CookedMeat 3s, Sand→Glass 8s; also creates RawMeat/CookedMeat/Sand/Glass item assets if missing). FurnaceSetup.cs ([MenuItem("Voidborne/Create Furnace Prefab")] and [MenuItem("Voidborne/Place Furnace In Scene")] and [MenuItem("Voidborne/Full Furnace Setup")]; creates Assets/Prefabs/Furnace.prefab as dark cube primitive with BoxCollider and FurnaceBlock component; FurnaceMaterial.mat created). Existing item assets IronOre/IronIngot/CopperOre/CopperIngot/Coal all confirmed present. After running editor menus: assign recipe assets to FurnaceBlock.recipes list and Coal ItemDefinition to FurnaceBlock.fuelItem in Inspector. REVIEWED 2026-03-16: All acceptance criteria met. Two fixes applied: (1) Typo in method name — CompletSmelt() renamed to CompleteSmelt() at both definition (line 187) and call site (line 146) in FurnaceBlock.cs; self-consistent so it compiled, but fixed for correctness. (2) Spurious per-frame OnStateChanged invocation — TrySmelt() had an unconditional OnStateChanged?.Invoke() at its end that fired every frame during active smelting; removed because FurnaceUI.Update() already polls SmeltProgress directly each frame for smooth progress bar animation, making the per-frame event redundant and causing Refresh() to rebuild slot visuals every frame unnecessarily. OnStateChanged still fires correctly from TryConsumeFuel(), CompleteSmelt(), SetInputSlot(), SetFuelSlot(), SetOutputSlot(). All other logic verified correct: IInteractable interface fully implemented (Interact(GameObject)/CanInteract(Vector3)/InteractPrompt); ItemStack field names item/quantity match ItemStack struct; FuelSlot fuel check (FuelSlot.item != fuelItem), quantity decrement, IsEmpty handled via new ItemStack(null,0); SmeltProgress [0,1] via 1f-Clamp01(_smeltTimer/_activeRecipe.smeltTime); IsBurning = _remainingFuel>0; output full check (OutputSlot.quantity < outputItem.maxStackSize); UIManager has _furnaceUI/_furnaceOpen/OpenFurnace/CloseFurnace/IsAnyUIOpen including _furnaceOpen; InventoryCursor.PickUpFurnace(ItemStack) overload present; Escape closes furnace via UIManager.ToggleInventory(); progress bar uses anchorMax.x approach (functional equivalent of Image.Type.Filled); fire indicator color orange/grey on IsBurning; FurnaceUI subscribes/unsubscribes OnStateChanged in Open/Close correctly. No further fixes required. |

### Volume 4 — Combat: Guns
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 4.1 | Weapon System Foundation | [✓] | Created GunDefinition.cs (ScriptableObject with FireMode/BulletType enums, full field set), GunInstance.cs (plain C# class with CanFire/ConsumeAmmo/GetNextRecoil/ResetRecoil), and DamageSystem.cs (DamageType enum, DamageInfo struct, IDamageable interface). All in namespace Voidborne.Combat under Assets/Scripts/Combat/. Reviewed 2026-03-16: all checklist items verified, no issues found, no fixes required. |
| 4.2 | Gun Mechanics: Shooting, Recoil, Spread | [✓] | SpreadCalculator.cs (static, no MB; crouching > speed priority; lerp between standing/moving for walking; threshold consts 0.1f still / 4f walk). RecoilSystem.cs (MB; SerializeField cameraTransform + recoveryRate=5f; ApplyRecoil adds offset and rotates camera in Space.Self; Update() recovers via Vector2.MoveTowards and counter-rotates camera; ResetInstant() zeros offset without camera move). GunController.cs (MB; SerializeFields: recoilSystem, cameraTransform, shootableLayers, audioSource, defaultGun, recoveryDelay=0.15f; EquipGun() creates GunInstance + ResetInstant; Auto/Semi/Burst fire via Mouse.current.leftButton; TryFire(): CanFire guard, ConsumeAmmo, SpreadCalculator.Calculate, GetNextRecoil, recoilSystem.ApplyRecoil, Physics.Raycast hitscan → IDamageable.TakeDamage, muzzle flash Instantiate+Destroy(0.05f), PlayOneShot; ApplySpread uses tan(deg*Deg2Rad) random circle; IPlayerState interface defined in same file for optional crouching coupling; CharacterController cached via GetComponentInParent in Start; UIManager.Instance + UIManager.IsAnyUIOpen guard in Update). All in namespace Voidborne.Combat. Reviewed 2026-03-16: 1 fix — SpreadCalculator.cs was missing `using UnityEngine;` and used a fully-qualified `UnityEngine.Mathf.Lerp` call; added the using directive and shortened the call to `Mathf.Lerp`. All other checklist items verified correct, no further issues found. |
| 4.3 | Gun Mechanics: Reload, ADS, Switching | [✓] | Created ReloadSystem.cs (coroutine-based reload with interrupt support), ADSController.cs (right-mouse FOV lerp, spread multiplier), WeaponSwitcher.cs (digit keys + scroll wheel, up to 3 slots). Modified GunInstance.cs (added SetReloading, CompleteReload, SetCurrentAmmo helpers). Modified GunController.cs (R key reload via ReloadSystem, ADS spread multiplier integration). All in namespace Voidborne.Combat. 2026-03-16. Reviewed 2026-03-16: all checklist items verified across all five files, no bugs found, no fixes required. Minor observation: WeaponSwitcher._lastSwitchTime and _isSwitching are written but never read (dead state) — harmless, defer cleanup. |
| 4.4 | Hit Detection & Feedback | [✓] | HitEffects.cs, HitDetection.cs, HitmarkerUI.cs, CrosshairUI.cs created; GunController updated with CurrentSpread + HitDetection integration. Reviewed 2026-03-16: all checklist items verified, no issues found, no fixes required. Zero-vector LookRotation guard present in SpawnHitEffect. HitResult default-false struct correct. Coroutine stored by reference and stopped cleanly in HitmarkerUI.ShowHit. CrosshairUI null-guards gunController and all four RectTransforms. GunController.CurrentSpread updated before fire logic and consumed without re-calculation. |
| 4.5 | Starter Guns & Balancing | [✓] | Added `pelletCount` field (default 1) to GunDefinition.cs under Firing header. Updated GunController.TryFire() hitscan branch to loop `pelletCount` rays with independent spread; ammo, recoil, and muzzle flash remain outside the loop (once per shot). Created Assets/Editor/GunAssetCreator.cs with `[MenuItem("Voidborne/Create Starter Guns")]`: creates HandmadeRevolver.asset (Semi, 45dmg, 120rpm, 6mag, range 80, 6-step recoil), Rattler_SMG.asset (Auto, 18dmg, 800rpm, 30mag, range 50, 8-step alternating recoil), Ironbark_AR.asset (Auto, 28dmg, 600rpm, 25mag, range 120, 8-step pull-left recoil), PumpShotgun.asset (Semi, 15dmg×8pellets, 60rpm, 6mag, range 30, pelletCount=8, single heavy kick recoil), BoltSniper.asset (Semi, 110dmg, 30rpm, 5mag, range 500, pen 2.0, ads×4, 2-step heavy kick recoil). All assets output to Assets/ScriptableObjects/Guns/. modelPrefab1P/3P left null pending art. 2026-03-16. Reviewed 2026-03-16: 1 fix — GunAssetCreator.cs directory creation assumed `Assets/ScriptableObjects` parent already existed; replaced single CreateFolder call with a two-step guard (create parent if missing, then create Guns subfolder). GunDefinition.cs: pelletCount [SerializeField] public int = 1 with Tooltip, all prior fields intact. GunController.cs: pellet guard `gun.pelletCount > 1 ? gun.pelletCount : 1` is safe (0 and negative correctly resolve to 1), ConsumeAmmo/ApplyRecoil/muzzle flash/audio all outside loop, HitDetection.ProcessHit and HitmarkerUI.ShowHit called per pellet per enemy hit (correct). All 5 gun stats match spec. AssetDatabase.SaveAssets + AssetDatabase.Refresh present. No other issues. |

### Volume 5 — Combat: Melee
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 5.1 | Directional Attack System | [✓] | Created AttackDirection.cs (enum + FromMouseDelta utility), MeleeDefinition.cs (ScriptableObject: damage, range, hitRadius, windup/release/recovery times, stamina cost, attackSpeed multiplier, combo chain), MeleeController.cs (mouse-delta direction tracking, Windup→Release→Recovery state machine, SphereCast hit detection with per-target dedup, IDamageable integration, stamina consumption). Added public ConsumeStamina(float) to FirstPersonController.cs. All files in Assets/Scripts/Combat/Melee/ and namespace Voidborne.Combat.Melee. 2026-03-16. |
| 5.2 | Parry, Riposte & Block | [✓] | Created ParrySystem.cs (200ms parry window, direction mirror rule + adjacency forgiveness, riposte window 0.5s, attacker stagger on success, stamina cost 8f, stab always parriable). Created MeleeStagger.cs (decaying sine/cosine ShakeOffset for camera shake, IsStaggered flag blocks attacks+parries, ApplyStagger() restarts timer). Modified MeleeController.cs (auto-resolves ParrySystem+MeleeStagger refs, stagger blocks attack input, riposte 30% faster windup via RiposteWindupMultiplier=0.7f, ConsumeRiposte() called on attack initiation in riposte window, ForceIdle() public API). Modified FirstPersonCamera.cs (MeleeStagger ref auto-resolved from parent, ShakeOffset delta-integrated same as recoil, _lastShakeOffset tracking). All in namespace Voidborne.Combat.Melee. 2026-03-16. Reviewed 2026-03-16: all checklist items verified, code correct. |
| 5.3 | Feints, Morphs & Chambers | [✓] | Created FeintSystem.cs (feint cancels Windup on right-click: 10f stamina, ForceIdle(); morph tracks mouse delta during Windup, redirects attack on 25px threshold: 5f stamina, MorphDirection(); chamber mirrors parry direction rule — first chamberWindowFraction of Windup + full Release is the window, TryChamber() staggers attacker 0.5f, no extra stamina). Modified MeleeController.cs: added OnDirectionMorphed event + MorphDirection(AttackDirection) public method (Windup-only, resets _accumulatedMouseDelta, fires event). All in namespace Voidborne.Combat.Melee. 2026-03-16. Reviewed 2026-03-16: 4 fixes applied to FeintSystem.cs — (1) UpdateChamberWindow(): added null guard on _meleeController before accessing .Phase/.PhaseProgress; (2) HandleFeintInput(): added null guard on _meleeController before accessing .Phase; (3) HandleMorph(): added null guard on _meleeController; (4) HandleMorph(): replaced bare early-return on non-Windup with a `_morphDelta = Vector2.zero; return;` so accumulated delta does not carry over into the next attack's Windup and cause a spurious morph. TryChamber(): added null guard on _meleeController before .CurrentDirection access. MeleeController.MorphDirection() verified correct (Windup guard, sets CurrentDirection, zeroes _accumulatedMouseDelta, fires OnDirectionMorphed). Right-click conflict verified: ParrySystem gates on Idle/Recovery, FeintSystem gates on Windup — mutually exclusive. Chamber vs TryDeflect: no conflict (separate call sites). All checklist items pass. |
| 5.4 | Melee Weapons & Kick | [D] | KickAbility (MMB/F, unblockable, stagger), 5 weapon assets via MeleeAssetCreator, MeleeDefinition+armorPenetration/hitStaggerDuration, ParrySystem.ForceBreak(), MeleeController stagger-on-hit |

### Volume 6 — Combat: Projectiles & Enemies
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 6.1 | Projectile Physics System | [✓] | |
| 6.2 | Bow & Thrown Weapons | [✓] | |
| 6.3 | Enemy AI Foundation | [✓] | No NavMesh — see revised spec below |
| 6.4 | Starter Enemy Types | [ ] | |

### Volume 7 — Building & Backpacks
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 7.1 | Building Block System (no structural integrity — free placement, breakable pieces) | [ ] | |
| 7.2 | Electricity & Wiring (Basic) | [ ] | |
| 7.3 | Backpack Implementation | [ ] | |

### Volume 8 — Automation & Machines
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 8.1 | Conveyor Belts, Pneumatic Tubes & Routing | [ ] | |
| 8.2 | Auto-Miner, Drill & Drone Ports | [ ] | |
| 8.3 | Machines (Electric Furnace, Grinder, Press, Assembler, Tier 2) | [ ] | |
| 8.4 | Power Grid (Advanced Generators & Void Filament) | [ ] | |
| 8.5 | Computer Terminal, Storage Drives & Scripted Automation | [ ] | |

### Volume 9 — Vehicles
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 9.1 | Vehicle Base System (Assembly, Physics, Damage Zones) | [ ] | |
| 9.2 | Ground Vehicles (Buggy, Cycle, Hauler, Drill Frame) | [ ] | |
| 9.3 | Rotor Frame (Gyrocopter) & Pushcart | [ ] | |

### Volume 10 — Quests, Story & Polish
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 10.1 | Quest System Framework | [ ] | |
| 10.2 | NPC Dialogue, Safeholds & Kin Communities | [ ] | |
| 10.3 | Cortex Device & Main Story Implementation | [ ] | |
| 10.4 | Boss Encounters (The Five Generals) | [ ] | |

### Volume 11 — Full Enemy Roster & World Population
**Do not start until Volume 10 is complete. All AI and Cortex module systems must be functional first.**
| Chunk | Description | Status | Agent Notes |
|-------|-------------|--------|-------------|
| 11.1 | Fauna (Prey, Predators, Cave Creatures, Sky Fauna) | [ ] | |
| 11.2 | Humanoid Factions (Unbound, Hollow, Residue, Scavenger Bands) | [ ] | |
| 11.3 | Stronghold Enemy Population & Room Design | [ ] | |

---

## AGENT NOTES LOG

Agents should append notes here when they encounter issues, make design decisions, or need to communicate something to future agents.

```
[TEMPLATE]
Date: YYYY-MM-DD
Agent: [Implementation/Review] Volume X Chunk Y
Notes:
-
```

---

```
Date: 2026-03-15
Agent: Final Verification — Volume 1 COMPLETE
Notes:
SCENE SETUP (Assets/Scenes/SampleScene.unity):
- GameManager GameObject: GameBootstrapper + ChunkManager + ChunkLoader
  - ChunkManager: horizontalRenderDistance=2, verticalRenderDistance=5, chunksPerFrame=1
- Player GameObject at (0, 150, 0): CharacterController + FirstPersonController + PlayerManager + PlayerTerrainInteraction
  - Child: PlayerCamera (was Main Camera) at local (0, 1.6, 0): Camera + AudioListener + FirstPersonCamera
- Directional Light, Global Volume (default URP)

VOLUME 1 TEST RESULTS:
- Terrain generates ✓
- Player spawns above terrain and falls onto it ✓
- Mouse look and movement work ✓
- Triplanar terrain shader renders correctly ✓
- Biome color blending visible ✓

KNOWN ISSUES — MUST FIX BEFORE VOLUME 2:
1. PERFORMANCE CRITICAL: CPU density generation is a major bottleneck.
   - Each chunk requires 35,937 serial calls to DensityFunction.GetDensity() on the main thread
   - Even at 1 chunk/frame this causes hitches
   - SOLUTION: GPU density via compute shader (DensityGeneration.compute)
   - Pipeline: Dispatch DensityCS → fills float buffer → feed to MarchingCubesCS → triangles
   - This eliminates ALL CPU noise math; GPU computes 35,937 values in parallel
   - See "Chunk 1.X — GPU Density Compute Shader" below for spec

2. TERRAIN SPIKY: biome heightScale values are too large relative to 32-unit chunk size
   - Lowlands: heightScale=40→28, octaves=5→3
   - Badlands: heightScale=100→50, octaves=6→3
   - Frozen Peaks: heightScale=140→65, octaves=7→3
   - Fungal Marshes: heightScale=30→22, octaves=5→3
   - DensityFunction domain warp strength: 50→12
   - BiomeData.Default: heightScale=80→35, octaves=6→3

3. CAVES NOT VISIBLE: cave carve condition is y<0 only; extend to y<20 for surface overhangs
   - caveDensity threshold too high (0.45-0.5); lower to 0.25-0.28 for more caves

NEXT STEP: Implement GPU density compute shader (top priority, do before Volume 2)
```

```
Date: 2026-03-15
Agent: Final Verification — Volume 1 ALL ISSUES RESOLVED
Notes:
- All terrain issues from KNOWN ISSUES list are FIXED and verified working
- GPU density compute shader (1.X) fully implemented and reviewed ✓
- Terrain generates smoothly at 60fps with no hitches ✓
- Biome tuning values applied — no spiky terrain ✓
- Caves visible underground ✓
- Player movement, collision, deformation all working ✓
- Volume 1 is 100% COMPLETE — all chunks [✓]
- Volume 2, Chunk 2.1 (Item System Foundation) COMPLETE — all scripts and assets created, zero errors
- NEXT: Begin Volume 2, Chunk 2.2 — Inventory System
```

---

## PERFORMANCE ARCHITECTURE — GPU DENSITY (DO THIS BEFORE VOLUME 2)

This is a REQUIRED architectural improvement. Do NOT proceed to Volume 2 until this is done.

### Chunk 1.X — GPU Density Compute Shader

**Why:** CPU density generation (35,937 noise evals per chunk, serial, main thread) causes unacceptable frame hitches. GPU computes all samples in parallel.

**Files to create/modify:**

#### NEW: Assets/Scripts/World/MarchingCubes/Resources/DensityGeneration.compute
Compute shader that generates the 33×33×33 density field for a chunk.

Kernel: `GenerateDensity`
- Input uniforms: `float3 chunkWorldOffset`, `float heightScale`, `float heightFrequency`, `float caveScale`, `float caveDensity`, `float noiseOctaves`, `float persistence`, `float lacunarity`, `float domainWarpStrength`, `int worldSeed`
- Output: `RWStructuredBuffer<float> densityBuffer` (35937 floats, index = x + y*33 + z*33*33)
- Thread groups: `[numthreads(4,4,4)]`, dispatch (9,9,9) → covers 36×36 (crop to 33)

Density logic to implement in HLSL:
```
For each point (x,y,z) in 0..32:
  float3 worldPos = chunkWorldOffset + float3(x,y,z)
  float surfaceHeight = fbmNoise2D(worldPos.xz, heightFrequency, octaves) * heightScale
  float density = -worldPos.y + surfaceHeight

  // Cave carving (y < 20)
  if (worldPos.y < 20.0) {
    float caveNoise = fbmNoise3D(worldPos, 0.03, 3)
    if (abs(caveNoise) > caveDensity)
      density -= caveScale * heightScale * (abs(caveNoise) - caveDensity)
  }

  densityBuffer[x + y*33 + z*33*33] = density
```

Include a HLSL simplex noise implementation (or use the gradient noise from the existing NoiseDensity.compute as reference). The existing NoiseDensity.compute in the repo has working HLSL noise functions — reference them.

DO NOT modify NoiseDensity.compute itself. Write our own DensityGeneration.compute.

#### MODIFY: Assets/Scripts/World/Chunks/MarchingCubesAdapter.cs
- In `Initialize()`: also load `DensityGeneration` compute shader from Resources
- Add method `void GenerateDensityGPU(ChunkData chunk, Vector3 chunkWorldPos)`:
  - Sets uniforms from BiomeMap.GetBlendedBiomeData(xz) for the chunk center
  - Dispatches DensityGeneration kernel
  - The densityBuffer stays on GPU — pass it directly to the MarchingCubes shader
- Modify `GenerateMesh()` to accept a GPU buffer instead of CPU float[] when GPU density is available
- Keep the CPU float[] overload for fallback/testing

#### MODIFY: Assets/Scripts/World/Chunks/ChunkMeshBuilder.cs
- Add `GenerateChunkMeshGPU(ChunkData chunk)` that calls the GPU density path
- Keep `GenerateChunkMesh()` as CPU fallback
- ChunkManager should call the GPU version by default

#### MODIFY: Assets/Scripts/ScriptableObjects/Biomes/BiomeRegistry.cs + BiomeData.cs
Apply the tuning values from KNOWN ISSUES above.

**Acceptance criteria:**
- Chunks generate without frame hitches (smooth 60fps with render distance 4-6)
- Terrain is noticeably smoother (no extreme spikes)
- Caves visible underground
- All existing edit-mode tests still pass

---

## MARCHING CUBES INTEGRATION NOTES

**CRITICAL — READ BEFORE CHUNK 1.3**

**Repo: https://github.com/gtaharaedmonds/marching-cubes-gpu**
by Gus Tahara-Edmonds. C# + HLSL/Compute Shaders on Unity.

### Repo Architecture
The repo has two implementations:
- **GPU version** (in `Assets/Resources/`) — This is what we use. Compute shader-based, extremely performant, supports >500m view distance.
- **CPU version** (in `Assets/CPU Version/`) — Ignore this entirely.

The GPU version:
- Uses a compute shader (`MarchingCubes.compute`) that takes density/voxel data and outputs triangle vertices
- Has its own layered 3D noise for terrain generation — **we REPLACE this with our DensityFunction**
- Has a simple LOD system — **we may keep or replace this**
- Calculates normals for lighting
- Supports infinite world generation
- Has a simple physics demo (doesn't work with infinite mode — irrelevant to us)

### Integration Strategy
1. **Keep:** The compute shader pipeline — the `.compute` files, the lookup tables, the GPU dispatch logic, the mesh buffer readback.
2. **Replace:** Their terrain noise/generation with our `DensityFunction.cs`. We fill a density buffer with OUR values and feed it to THEIR compute shader.
3. **Wrap:** Create `MarchingCubesAdapter.cs` that provides a clean API: `Mesh GenerateMesh(float[] densityField, int gridSize)`. Internally it sets up ComputeBuffers, dispatches the repo's compute shader, reads back triangles, and builds a Unity Mesh.
4. **Adapt grid size:** The repo may use a different default grid size. Our chunks are 32³. The adapter must configure or pad to match. If the repo's compute shader has a hardcoded grid size, change ONLY that constant (a single `#define` or `int` in the compute shader), nothing else.
5. **Edge stitching:** Marching cubes needs density at position [32] to close the boundary. The adapter must sample one extra row from adjacent chunks via ChunkManager. This means the actual density buffer passed to the compute shader is 33³ (or the repo's grid size + 1 in each dimension).

### What to Do If It Doesn't Compile on Unity 6.3 LTS
1. Fix C# API deprecations (e.g., `FindObjectOfType` → `FindFirstObjectByType`, obsolete rendering APIs).
2. If compute shader syntax errors appear, check for Unity 6.x breaking changes in HLSL compilation. Common fixes: adding explicit `float` casts, fixing `RWStructuredBuffer` declarations.
3. **DO NOT rewrite the marching cubes algorithm.** If the compute shader fundamentally won't compile, fall back to one of these alternatives:
   - Sebastian Lague's Coding Adventures Marching Cubes (https://github.com/SebLague/Marching-Cubes)
   - Scrawk's Marching Cubes GPU Unity (https://github.com/Scrawk/Marching-Cubes-On-The-GPU)
   - keijiro's ComputeMarchingCubes (https://github.com/keijiro/ComputeMarchingCubes)
4. **Log the issue in the Agent Notes section** so future agents know what happened.

---

## DESIGN REFERENCE

### Chunk Size Rationale
32³ = 32,768 voxels per chunk. At 4 bytes per float, the density field is 128KB per chunk. With 500 active chunks, that's ~64MB of density data. The ore field adds 32KB per chunk. Meshes vary but average ~50-200KB each. Total memory for active world: ~200-400MB. This is well within budget for PC.

### Vertical Zones Density Function Pseudocode
```csharp
float GetDensity(float3 pos, BiomeData biome) {
    float surfaceNoise = Noise2D(pos.xz, biome.frequency, biome.octaves) * biome.amplitude;
    float surfaceHeight = surfaceNoise; // centered around Y=0
    float baseDensity = -(pos.y - surfaceHeight); // positive below surface, negative above
    
    // Caves: 3D noise creates pockets of negative density underground
    float caveNoise = Noise3D(pos, biome.caveFrequency, 3);
    float caveMask = saturate((-pos.y - 10) / 20); // no caves near surface
    float caves = (caveNoise > biome.caveThreshold) ? -1 : 0;
    caves *= caveMask;
    
    // Deep caves: larger, more open
    if (pos.y < -128) {
        float deepCave = Noise3D(pos * 0.02, ...);
        caves += (deepCave > 0.3) ? -2 : 0;
    }
    
    // Sky islands: isolated positive pockets above surface
    if (pos.y > 200) {
        float skyDensity = Noise3D(pos * 0.01, ...) - 0.6; // mostly air
        baseDensity = skyDensity;
    }
    
    return baseDensity + caves;
}
```

### Gun Feel Targets
- Time from click to hit: 0ms (hitscan, instant)
- Recoil pattern length: 15-30 shots before it loops
- Recoil recovery rate: ~3x the application rate
- First shot accuracy: 100% when standing still, crouched, not recently moving
- ADS transition: 150-200ms
- Weapon switch: 300-500ms
- Reload: 1.5-3s depending on weapon
- Target TTK (time to kill, body shots): 400-800ms for rifles, 200ms for shotgun close, 0ms for sniper headshot

### Melee Timing Targets
- Windup: 300-500ms (fast weapons lower end, slow weapons higher)
- Release (active hit window): 200-400ms
- Recovery: 300-500ms
- Parry window: 200ms
- Riposte speed bonus: 30% faster windup
- Stagger duration: 500ms
- Feint window: during windup only, costs 10 stamina
- Chamber window: first 100ms of opponent's release phase

### Enemy AI Architecture (added 2026-03-16)

**No NavMesh** — marching-cubes terrain deforms at runtime and chunks load/unload, making NavMesh baking impractical. The navigation system uses three stacked layers instead:

1. **Grounded steering** — each frame, raycast down to find the terrain surface and project the velocity onto the slope normal. Enemies naturally follow MC terrain contours including caves and ramps.
2. **5-ray obstacle fan** — a forward fan of short raycasts detects walls and deflects toward the clearest opening. Handles narrow passages without any precomputation.
3. **EnvironmentSensor lidar** — 26 Fibonacci-hemisphere rays dispatched via `RaycastCommand.ScheduleBatch` (Unity job-system worker threads). Staggered by `instanceID % scanInterval` so N enemies spread their scans across N frames. Results cached; queries are O(1) reads on the main thread.

**Cover-seeking**: on entering Flee state, enemies query `EnvironmentSensor.GetBestCoverPoint(dangerPos)`. Candidates are generated from obstacle face normals during the lidar scan. A cover point is valid if (a) `Physics.Linecast` to danger is blocked by geometry and (b) a `Physics.CheckSphere` confirms enough clearance for the enemy to stand. Only the main-thread validation call happens when cover is needed — not every frame.

**Personality profiles** (`PersonalityProfile` struct — three static instances, no per-frame branching):
- `Optimized` — decisive, tight groups (low spread), always seeks cover, flanks efficiently
- `Directed` — hesitant (random flank-instead-of-charge chance), erratic jitter, panic-suppresses (backs away facing danger) instead of calmly finding cover
- `WildCreature` — fastest, most erratic, zero cover urgency, purely terrain-following

**EnemySpawner** uses `BiomeMap.GetBiome(float2)` for biome restriction filtering. Population caps tracked per `EnemyDefinition`. Group size: Optimized uses `groupSizeMin`/`groupSizeMax`, Directed always 1–2.

**WeaponSwitcher.HasActiveWeapon** — property added to support the Directed Converse state (enemy speaks only if player has no weapon drawn).
