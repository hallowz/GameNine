# MASTER PROMPT — Voidborne: 3D FPS Survival Game (v3 — HTML-driven)

## READ THIS FIRST — AGENT WORKFLOW

You are Claude Code (with Unity MCP access), orchestrating the development of a large-scale Unity game.

This master prompt was rewritten on **2026-05-26** to match the new design captured in
`Design Documents/voidborne-flowchart-v3.html` (the "flowchart HTML"). The flowchart HTML is
the **canonical design source of truth**. This prompt is the **build plan** derived from it.

Machine-readable extracts of the HTML's data tables live in `Design Documents/GameDesign/data/`
(items.json, npcs.json, categories.json, etc.). Those JSON files are the **content source of
truth** for every code-generating step — agents must consume them, not re-type their contents.

### Development Loop

1. **Read this file** — find the next chunk with status `[ ]` (not started) or `[P]` (partial) in
   the Progress Tracker.
2. **Spawn an implementation sub-agent**:
   ```
   Task: Implement Volume X, Chunk Y — [chunk title]
   Read MASTER_PROMPT.md for full context and the chunk specification.
   Consume the JSON data files referenced in the chunk.
   Implement all files described.
   Test compilation with Unity MCP (read_console for errors).
   Update MASTER_PROMPT.md Progress Tracker when done.
   ```
3. **Spawn a code-review sub-agent** when implementation reports done:
   ```
   Task: Code Review — Volume X, Chunk Y — [chunk title]
   Read MASTER_PROMPT.md for context.
   Review all files created/modified in this chunk.
   Open Unity (via MCP), verify zero compilation errors.
   Run any EditMode/PlayMode tests.
   Fix issues found. Update tracker with [✓] when clean.
   ```
4. **After review passes**, spawn the next chunk's implementation agent.

### Critical Rules

- **ONE CHUNK PER AGENT.** Never implement multiple chunks in one context.
- **ALWAYS UPDATE THE PROGRESS TRACKER** when a chunk completes.
- **FRESH CONTEXT FOR EVERY AGENT.** Each agent reads this file fresh.
- **VERIFY IN UNITY AFTER EVERY CHUNK** via MCP — read_console, compile check, run tests.
- **DO NOT MODIFY MARCHING CUBES INTERNALS.** Wrap, don't rewrite. See Volume 0.
- **NEVER INLINE LARGE LISTS** from the HTML/JSON into code or this file. Reference by path.
- **DESIGN PIVOT MARKER (2026-05-26):** Volumes 2–11 of the prior plan are SUPERSEDED. The
  *code* from those volumes that survives is listed in "Legacy Code Survival Map" below. All
  ScriptableObject *assets* from prior volumes (items, recipes, ores, biomes, smelting,
  starter guns, melee weapons) are scheduled for deletion in Volume 5.
- **COOP-AWARE FROM DAY ONE.** Every new system MUST be designed with multiplayer sync in mind
  even before Volume 21 (Coop Networking Integration). See "Coop Design Constraints" below.
- **File paths are relative to** `E:/Programs/repos/hallowz/GameNine/`.

### Coop Design Constraints (apply to every new system)

- World state (terrain mods, ore field, placed structures, dropped items, machine state) is
  **server-authoritative**. Clients never mutate world state directly.
- Player state (inventory, hotbar, held item) is **owner-authoritative** but mirrored to server
  for validation.
- New ScriptableObjects must be **stateless** (data only). All runtime state lives on
  MonoBehaviours or in a dedicated NetworkBehaviour wrapper.
- Avoid `Time.deltaTime` in gameplay logic that needs to be deterministic — use the network
  tick when Volume 21 lands.
- Never use `Random` without an explicit seed. Use a `WorldRandom` helper that pulls from the
  seeded world RNG (server-side only for world events).
- UI state (open panels, drag cursors) is **client-local** — never sync.

---

## PROJECT STRUCTURE

```
GameNine/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                  # Singletons, GameBootstrapper, manager glue
│   │   ├── Data/                  # JSON loaders, ScriptableObject generator, registries
│   │   ├── World/                 # Terrain, chunks, biomes, generation
│   │   │   ├── Generation/        # Noise, density, biome assignment, decoration
│   │   │   ├── Chunks/            # Chunk loading, meshing, LOD
│   │   │   ├── Decoration/        # Grass, scatter, ambient
│   │   │   ├── Structures/        # Blueprint capture/place, Kin ruins, Vord strongholds
│   │   │   └── MarchingCubes/     # EXTERNAL — git submodule, DO NOT EDIT
│   │   ├── Player/                # FPS controller, camera, input
│   │   ├── Inventory/             # Items, stacks, hotbar, backpacks
│   │   ├── Crafting/              # Recipes, machine-scoped matching, bootstrap paths
│   │   ├── Building/              # Block forms, placement, terrain leveling, blueprints
│   │   ├── Power/                 # Cable tiers, generators, junctions, regulators
│   │   ├── Automation/            # Machines, conveyors, sorters, storage, auto-variants
│   │   ├── Combat/                # Guns, melee, projectiles, bows, traps, turrets, damage
│   │   ├── Fauna/                 # Animal AI, taming, drops
│   │   ├── Enemies/               # Vord drones, fodder, bosses, spawning
│   │   ├── NPCs/                  # Kin, traders, Spirit Gateway, festivals
│   │   ├── Vehicles/              # Chassis, engines, wheels, modular assembly
│   │   ├── Quests/                # Quest framework, Atlas, Index, story acts
│   │   ├── Net/                   # Networking foundation, sync helpers (Volume 21)
│   │   ├── UI/                    # HUD, menus, Minecraft-style inventory/crafting
│   │   ├── ArtPipeline/           # Editor scripts: procedural primitives, icon renderer
│   │   └── Utilities/             # Extensions, helpers, pools, seeded RNG
│   ├── Shaders/                   # Triplanar terrain, grass, ore overlay
│   ├── Prefabs/
│   │   ├── Items/                 # Per-item world-drop prefab (procedural)
│   │   ├── Blocks/                # Per-block placed prefab (procedural)
│   │   ├── Machines/              # Per-machine placed prefab (procedural)
│   │   ├── Fauna/                 # Per-animal prefab (procedural)
│   │   ├── Enemies/               # Per-enemy prefab (procedural)
│   │   ├── NPCs/                  # Per-NPC prefab
│   │   ├── UI/                    # Canvas, panels, slots
│   │   └── Structures/            # Saved blueprints
│   ├── Materials/                 # Auto-generated per item category
│   ├── Textures/Icons/            # Auto-rendered item thumbnails
│   ├── ScriptableObjects/
│   │   ├── Generated/             # ⚠ AUTO-GENERATED — do not hand-edit
│   │   │   ├── Items/
│   │   │   ├── Recipes/
│   │   │   ├── Machines/
│   │   │   ├── Fauna/
│   │   │   ├── Enemies/
│   │   │   └── NPCs/
│   │   ├── Biomes/                # Biome definitions (hand-tuned, references generated assets)
│   │   ├── Story/                 # Quest definitions, act scripts
│   │   └── Blueprints/            # Saved structure templates
│   ├── Scenes/
│   │   ├── Boot.unity
│   │   ├── MainMenu.unity
│   │   └── Game.unity
│   ├── Resources/                 # Runtime-loaded SOs (databases, registries)
│   ├── Editor/                    # Custom editor tools, generators, inspectors
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Design Documents/
│   ├── voidborne-flowchart-v3.html    # ⭐ DESIGN SOURCE OF TRUTH
│   ├── master_prompt.md               # THIS FILE — build plan
│   └── GameDesign/
│       ├── data/                       # ⭐ CONTENT SOURCE OF TRUTH (JSON extracts)
│       │   ├── items.json              # 1031 items (sources, machines, components, products)
│       │   ├── build_materials.json    # 17 build materials × 5 forms = 85 blocks
│       │   ├── form_templates.json
│       │   ├── deco_blocks.json        # 45 decorative blocks
│       │   ├── synergy_alts.json       # 9 cross-archetype alternate recipes
│       │   ├── categories.json         # food/power/weapon/armor/... → item ID lists
│       │   ├── npcs.json               # 70 NPCs (30 bosses, 2 finale, 11 fodder, 18 wildlife, 6 named Kin, 3 traders)
│       │   └── summary.json            # counts + sanity check
│       └── scripts/
│           └── extract_html_data.py    # Re-run when the HTML changes
├── Packages/
└── ProjectSettings/
```

### Re-extracting from HTML

When `voidborne-flowchart-v3.html` is edited, re-run the extractor:
```
py "Design Documents/GameDesign/scripts/extract_html_data.py"
```
Then run the `Voidborne/Regenerate All Generated SOs` editor menu (defined in Volume 2).

---

## LEGACY CODE SURVIVAL MAP (after 2026-05-26 pivot)

| Old chunk | Survives | Becomes |
|-----------|----------|---------|
| Vol 0.0 — Bootstrap | ✓ as-is | Volume 0 |
| Vol 1.1–1.8, 1.X — Terrain/MC/FPS/Density | ✓ as-is | Volume 1 |
| Vol 2.1–2.3 — Item/Inventory/UI plumbing | code: ✓ / assets: ✗ | Volume 2 generates new assets; Volume 4 redoes UI; Volume 6 upgrades crafting matching |
| Vol 2.5–2.6 — Crafting + Workbench | code: partial / assets: ✗ | Volume 6 rewrites recipe matching to be machine-scoped per the HTML's working example |
| Vol 3.1–3.3 — Ore generation & mining | code: ✓ / ore SOs: ✗ | Ore generation stays; Volume 2 regenerates ore item SOs; Volume 12 expands biome→ore mapping |
| Vol 3.4 — Furnace | code: ✓ as a reference / asset: ✗ | Replaced by 43-machine system in Volume 9 |
| Vol 4.1–4.5 — Guns | code: ✓ / gun SOs: ✗ | Volume 10 extends to bow/spear/thrown/traps; Volume 2 generates weapon SOs from items.json |
| Vol 5.1–5.4 — Melee | code: ✓ / melee SOs: ✗ | Same as guns — code is the foundation, new weapons come from JSON |
| Vol 6.1–6.3 — Projectiles + Enemy AI | code: ✓ | Volume 14 (fauna) + Volume 15 (enemies/bosses) build on this |
| Vol 7+ — Building, Automation, Vehicles, Quests, etc. | not started | SUPERSEDED — rebuilt in Volumes 7, 8, 9, 13, 17, 18, 19 |

The "Asset Scrap Manifest" in Volume 5 lists every asset file to delete.

---

## TECHNICAL SPECIFICATIONS

### Chunk System
- Chunk size: **32×32×32** voxels
- Coordinate system: chunk position is (chunkX, chunkY, chunkZ); world position = chunk pos × 32
- Default generation band: Y = -128 to Y = 128 (surface terrain)
- No hard floor/ceiling — chunks generate on demand vertically
- Chunk loading: radial distance from player, default horizontal render distance 8 chunks, vertical 4
- Chunks stored in `Dictionary<Vector3Int, ChunkData>` — NOT a flat array
- Chunk states: Unloaded → Generating → MeshPending → Active → MarkedForUnload → Unloaded

### Marching Cubes — EXTERNAL SOLUTION
**DO NOT WRITE YOUR OWN MARCHING CUBES IMPLEMENTATION. DO NOT MODIFY THE COMPUTE SHADERS.**

We use Sebastian Lague-style GPU compute marching cubes (cloned from
`https://github.com/gtaharaedmonds/marching-cubes-gpu`). Our code interacts ONLY through
`MarchingCubesAdapter.cs`. See the Volume 0 setup notes.

### Density Function (replaces the MC repo's terrain noise)
`float GetDensity(Vector3 worldPos)`:
- Positive = solid terrain, negative = air, zero = surface
- 2D fbm noise drives surface height, modulated by biome parameters
- 3D noise carves caves and overhangs below Y < 20
- Different Y bands behave differently (sky islands, abyss, etc.)
- GPU implementation lives in `Assets/Scripts/World/MarchingCubes/Resources/DensityGeneration.compute`

### Vertical Zones (density function behavior by Y range)
| Y Range | Zone | Density Behavior |
|---------|------|-----------------|
| 600+ | High Sky | Sparse floating islands (Sky Islands biome) |
| 200–600 | Low Sky | Wind-carved plateaus, arches, thin floating shelves |
| 128–200 | Upper Surface | Mountain peaks, cliffs |
| -128 to 128 | Surface Band | Standard terrain by biome |
| -128 to -256 | Shallow Underground | Larger caves, cave biomes |
| -256 to -512 | Deep Underground | Mega-caverns, underground lakes, boss lairs |
| -512 to -1000 | Abyssal | Inverted terrain, alien geometry (Vord domain) |

### Rendering
- Unity 6.3 LTS URP
- Triplanar shader for terrain (no UV unwrap)
- Texture arrays for biome-specific materials
- Grass + scatter decoration via GPU instancing
- Per-chunk mesh colliders
- LOD: distant chunks use simplified meshes

### Input
- Unity new Input System (`InputActions.inputactions`)
- Action maps: Player, UI, Vehicle, Building, BlueprintEditor

### Game Constants (single source)
A `GameConstants.cs` in `Scripts/Core/` holds tuning constants referenced by gameplay:
- Mining tier requirements, tool durability defaults, stamina regen rates, gravity, fall damage,
  cable tier wattage ceilings (T1≤200W, T2≤1000W, T3≤5000W), default render distances.

---

## VOLUME 0 — PROJECT BOOTSTRAP

**Unity Version: 6.3 LTS (6000.3.x)**
**Marching Cubes Repo: https://github.com/gtaharaedmonds/marching-cubes-gpu**

```
1. Create new Unity project (Unity 6.3 LTS, URP 3D template)
2. Install packages:
   - Input System, TextMeshPro, Mathematics, Burst, Collections
   - URP (already included)
   - ProBuilder (for procedural model generation in Volume 3)
   - Netcode for GameObjects (deferred install — Volume 21)
3. Create the folder structure above
4. Clone marching cubes:
   cd Assets/Scripts/World/
   git clone https://github.com/gtaharaedmonds/marching-cubes-gpu.git MarchingCubes
   - Move repo Assets/Resources/* into our MarchingCubes/Resources/
   - Move C# scripts into MarchingCubes/Scripts/
   - Fix API deprecations only (e.g., FindObjectOfType → FindFirstObjectByType)
   - DO NOT touch .compute or HLSL files
5. Create Boot.unity with a GameBootstrapper MonoBehaviour
6. Create empty Game.unity
7. Verify zero compile errors
8. Commit to git
```

---

## VOLUME 1 — CORE WORLD & PLAYER

Goal: walk around on smooth, infinite, deformable marching-cubes terrain with biomes, in first person.

All chunks in this volume are **complete** in the current codebase (status `[✓]` in tracker).
The biome rework, realistic grass, and structural-placement work that the new design demands
happens in **Volumes 12 and 13** — not here. Do not modify chunks 1.1–1.X to chase the new design.

| Chunk | Title | Files |
|-------|-------|-------|
| 1.1 | Density Function & Noise Utilities | `World/Generation/{NoiseUtilities,DensityFunction,WorldSeed}.cs`, `Tests/EditMode/DensityFunctionTests.cs` |
| 1.2 | Chunk Data Structure & Chunk Manager | `World/Chunks/{ChunkData,ChunkState,ChunkManager,ChunkCoordUtility}.cs`, `Tests/EditMode/ChunkCoordTests.cs` |
| 1.3 | Marching Cubes Adapter & Mesh Generation | `World/Chunks/{MarchingCubesAdapter,ChunkMeshBuilder,ChunkRenderer}.cs` |
| 1.4 | Chunk Loading/Unloading Around Player | `World/Chunks/{ChunkLoader,ChunkPool,ChunkGenerationQueue}.cs` |
| 1.5 | Biome System (basic — to be reworked in V12) | `World/Generation/BiomeMap.cs`, `ScriptableObjects/Biomes/{Lowlands,Badlands,FrozenPeaks,FungalMarshes}.asset` |
| 1.6 | Triplanar Terrain Shader & Biome Materials | `Shaders/TriplanarTerrain.shader`, `Materials/TerrainMaterial.mat` |
| 1.7 | First Person Player Controller | `Player/{FirstPersonController,FirstPersonCamera,PlayerManager}.cs`, `Player/InputSystem_Actions.inputactions` |
| 1.8 | Terrain Deformation | `World/Chunks/TerrainDeformer.cs`, `Player/PlayerTerrainInteraction.cs` |
| 1.X | GPU Density Compute Shader (perf) | `World/MarchingCubes/Resources/DensityGeneration.compute`, adapter changes |

See the original master_prompt history for per-chunk acceptance criteria — those are met.

---

# === NEW VOLUMES (HTML-driven rebuild) ===

## VOLUME 2 — DATA PIPELINE: JSON → SCRIPTABLE OBJECTS

Goal: convert the JSON extracts under `Design Documents/GameDesign/data/` into Unity
ScriptableObjects under `Assets/ScriptableObjects/Generated/`. After this volume runs, every
item, recipe, machine, fauna species, enemy, and NPC from the HTML has a corresponding
in-project asset. The extracted JSON is the **single source of truth** for content.

### Chunk 2.1 — JSON Loader & Schema Types
**Files to create:**
- `Assets/Scripts/Data/Schema/ItemJson.cs` — POCO mirroring the `items.json` shape: `kind`, `src`, `name`, `role`, `recipes[]`, `_build`, `_buildColor`, `_deco`.
- `Assets/Scripts/Data/Schema/RecipeJson.cs` — `via` (string, nullable), `inputs[] { id, qty }`, `notes`.
- `Assets/Scripts/Data/Schema/NpcJson.cs` — `cat`, `section`, `name`, `desc`, `appearance`, `behaviors[]`, `abilities[]`, `drops[]`.
- `Assets/Scripts/Data/Schema/CategoriesJson.cs` — dictionary of category name → string[] of item IDs.
- `Assets/Scripts/Data/GameDesignJsonLoader.cs` — static class. Methods: `Dictionary<string,ItemJson> LoadItems()`, `List<NpcJson> LoadNpcs()`, `CategoriesJson LoadCategories()`. Reads files from `Design Documents/GameDesign/data/` relative to `Application.dataPath/..`. Uses `Newtonsoft.Json` (install via Package Manager: `com.unity.nuget.newtonsoft-json`).
- `Assets/Editor/Data/JsonLoaderTests.cs` — quick edit-mode test: load items.json, assert count ≥ 1000, assert `D["workbench"].kind == "machine"`.

**Acceptance:** loader returns 1031+ items, 70 NPCs; tests pass.

### Chunk 2.2 — Item Definition v2 & Generator
**Files to create:**
- `Assets/Scripts/Inventory/ItemDefinition.cs` — REPLACE the existing one (move existing to `_Legacy/` first). New fields:
  - `string id` (snake_case, matches JSON key)
  - `string displayName`
  - `string description` (uses `role` from JSON)
  - `enum ItemKind { Source, Machine, Component, Product }`
  - `enum ItemSource { None, Fauna, Flora, Ore, Soil, Exotic }`
  - `int maxStackSize` (auto: 1 for machines/tools, 64 for components/products, 16 for sources)
  - `Sprite icon` (assigned by Volume 3 icon pipeline)
  - `GameObject worldDropPrefab` (assigned by Volume 3 model pipeline)
  - `GameObject placedPrefab` (for blocks/machines)
  - `string[] categories` (e.g., "food", "power", "weapon" — from categories.json)
  - `bool isBuildBlock`, `bool isDeco`, `string buildColor`
- `Assets/Editor/Data/ItemSoGenerator.cs` — `[MenuItem("Voidborne/Generate/Items")]`. Loads items.json + categories.json. For each item: create or update `Assets/ScriptableObjects/Generated/Items/{id}.asset`. Writes the `ItemDatabase.asset` registry. Skips icon/prefab assignment (those volumes own them). Uses `AssetDatabase.StartAssetEditing()` for batching.
- `Assets/Scripts/Inventory/ItemDatabase.cs` — REPLACE. Now holds `Dictionary<string,ItemDefinition>` indexed at OnEnable, plus `IReadOnlyCollection<ItemDefinition> AllItems`, `IReadOnlyCollection<ItemDefinition> ByCategory(string cat)`.

**Acceptance:** 1031 item assets generated; `ItemDatabase.GetItem("workbench")` returns the machine definition with `kind == Machine`.

### Chunk 2.3 — Recipe Definition v2 & Generator
**Files to create:**
- `Assets/Scripts/Crafting/RecipeDefinition.cs` — REPLACE. Fields:
  - `string outputItemId`, `int outputQty`
  - `Ingredient[] ingredients` (struct: `string itemId`, `int qty`)
  - `string viaMachineId` (nullable — null = personal grid / bootstrap)
  - `string notes`
  - `bool isBootstrap` (true if notes contain "BOOTSTRAP")
  - `bool isSynergy` (true if notes contain "SYNERGY")
- `Assets/Editor/Data/RecipeSoGenerator.cs` — `[MenuItem("Voidborne/Generate/Recipes")]`. For each item with `recipes[]`, emits one `RecipeDefinition` asset per recipe under `Assets/ScriptableObjects/Generated/Recipes/{outputId}__r{idx}.asset`. Skips items with empty `recipes[]`. Output qty defaults to 1 unless the JSON sets it (most don't — leave at 1).
- `Assets/Scripts/Crafting/RecipeRegistry.cs` — Singleton SO. `RecipeDefinition[] AllRecipes`, indexed by `viaMachineId` for fast lookup during crafting (Volume 6 consumes this).

**Acceptance:** 2500+ recipe assets generated (most items have 1-3 recipes; total runs into thousands). `RecipeRegistry.ByMachine("workbench")` returns the workbench bootstrap recipe.

### Chunk 2.4 — Machine, Fauna, Enemy, NPC Definitions & Generators
**Files to create:**
- `Assets/Scripts/Automation/MachineDefinition.cs` — extends ItemDefinition data (an item with `kind == Machine`). Adds: `int gridWidth`, `int gridHeight` (for crafting UI), `MachineTier tier` (T1–T7 parsed from `role` string like "T3 — proper smelting"), `bool needsPower`, `int powerDrawWatts`, `bool isAutomatable`.
- `Assets/Scripts/Fauna/FaunaDefinition.cs` — fields from JSON + Unity-specific: `string id`, `string displayName`, `string description`, `string[] behaviors`, `string habitat`, `string[] dropItemIds`, `bool isTameable`, `bool isPassive`, `bool isAggressive`, `float baseHealth`, `float baseSpeed`, `GameObject prefab`.
- `Assets/Scripts/Enemies/EnemyDefinition.cs` — same shape as FaunaDefinition plus `EnemyTier tier` (T1–T3), `EnemyArchetype family` (Brood/Warden/Hunter/Channeler/Aberrant/Fodder/Finale), `string[] abilities`, `string boss_section`.
- `Assets/Scripts/NPCs/NpcDefinition.cs` — `string id`, `string displayName`, `string section` (e.g., "Kin Survivors", "Traders"), `string[] behaviors`, `string[] abilities`, `string[] dropOrTradeItems`, `bool isKillable`, `bool isQuestGiver`.
- `Assets/Editor/Data/MachineSoGenerator.cs`, `FaunaSoGenerator.cs`, `EnemySoGenerator.cs`, `NpcSoGenerator.cs` — each consumes `npcs.json` filtered by `cat`. Heuristics for tier/archetype parsing live in `Assets/Editor/Data/ParsingHelpers.cs`.
- `Assets/Scripts/Core/Registries.cs` — single static facade exposing `ItemDatabase`, `RecipeRegistry`, `MachineRegistry`, `FaunaRegistry`, `EnemyRegistry`, `NpcRegistry`.

**Acceptance:** 43 machine, 18 fauna, 43 enemy (11 fodder + 30 boss + 2 finale), 9 NPC assets generated; registries load and lookup works.

### Chunk 2.5 — One-Click Regenerate
**Files to create:**
- `Assets/Editor/Data/RegenerateAllMenu.cs` — `[MenuItem("Voidborne/Generate/⟳ Regenerate All Generated SOs")]`. Calls Volumes 2.2–2.4 generators in order. Pre-step: warn if `Generated/` directory contains assets not in the current JSON (orphans). Post-step: log counts.
- `Assets/Editor/Data/JsonReextractMenu.cs` — `[MenuItem("Voidborne/Generate/⟳ Re-extract from HTML")]`. Shells out to `py "Design Documents/GameDesign/scripts/extract_html_data.py"`. Uses `System.Diagnostics.Process`. Refreshes AssetDatabase. Useful when the HTML changes.

**Acceptance:** Running both menu items end-to-end completes without errors and updates all generated assets.

---

## VOLUME 3 — VISUAL ASSET PIPELINE: PROCEDURAL MODELS & ICONS

Goal: every generated item, block, machine, fauna, enemy, NPC has a placeholder 3D model and
a rendered 2D icon — created by code from a tiny set of primitives and ProBuilder shapes. Quality
is intentionally minimal; the goal is uniform "stylized debug" visuals across all 1031+ items so
the game is playable end-to-end before any art pass.

### Chunk 3.1 — Material Palette
**Files to create:**
- `Assets/Scripts/ArtPipeline/PaletteRegistry.cs` — Static class returning a `Color` per `kind`/`src`/`category`. Defined palette from the HTML's dark theme: fauna teal `#56d3ff`, flora green `#b6f73e`, ore purple `#f778ba`, soil brown, exotic violet, food warm orange, power yellow, weapon red, armor purple, machine cyan, build neutral grey, deco soft blue.
- `Assets/Editor/ArtPipeline/MaterialGenerator.cs` — `[MenuItem("Voidborne/Generate/Materials")]`. Creates one URP/Lit material per palette entry plus a per-build-material variant (stone, wood, iron, ...) under `Assets/Materials/Generated/`.

**Acceptance:** ~30 materials generated.

### Chunk 3.2 — Procedural Primitive Mesh Library
**Files to create:**
- `Assets/Scripts/ArtPipeline/PrimitiveShape.cs` — Enum: Cube, Slab, Panel, Stairs, Door, Cylinder, Capsule, Sphere, Cone, Disc, Torus, Spike, Pyramid, Wedge.
- `Assets/Editor/ArtPipeline/PrimitiveMeshFactory.cs` — Returns a `Mesh` for each `PrimitiveShape`. Uses Unity primitives where possible; uses ProBuilder API (`ProBuilderMesh.CreateInstanceWithVerticesFaces`) for slab/panel/stairs/door/wedge. Caches generated meshes under `Assets/Models/Generated/Primitives/{shape}.asset`.

**Acceptance:** All 14 primitive meshes generated as `.asset` files; visible in Unity inspector.

### Chunk 3.3 — Item Visual Recipe & Composer
**Files to create:**
- `Assets/Scripts/ArtPipeline/ItemVisualRecipe.cs` — Pure data: a list of `Layer { PrimitiveShape, Vector3 localScale, Vector3 localPos, Quaternion localRot, string materialKey }`. A single item is a composition of 1–4 layers (e.g., furnace = big cube body + small cube chimney).
- `Assets/Editor/ArtPipeline/ItemVisualRecipeMapper.cs` — Static rules from item kind/category to a recipe. Examples:
  - `kind==Source && src==Ore` → single sphere with ore-color material.
  - `kind==Source && src==Flora` → cylinder (stem) + sphere (foliage) on top.
  - `kind==Source && src==Fauna` → capsule (body) + sphere (head) — slightly larger for predators.
  - `kind==Machine && id contains "furnace"` → cube body + small chimney + glow disc.
  - `kind==Component, category=="ammo"` → small cylinder.
  - `kind==Component, category=="armor"` → wedge (chestpiece silhouette).
  - `kind==Product, isBuildBlock=true` → matching `PrimitiveShape` for the block form.
  - `kind==Product, isDeco=true` → cube with deco material.
  - Default for unmatched products → small cube with category color.
- `Assets/Editor/ArtPipeline/ItemModelComposer.cs` — `GameObject Build(ItemDefinition item)`. Reads the recipe, instantiates child GameObjects with `MeshFilter`+`MeshRenderer`, assigns materials. Saves as a prefab at `Assets/Prefabs/Items/{id}.prefab`. Also produces a `placedPrefab` variant for blocks/machines (adds a `BoxCollider` and the correct gameplay MonoBehaviour stub).
- `Assets/Editor/ArtPipeline/BulkPrefabGenerator.cs` — `[MenuItem("Voidborne/Generate/Item Prefabs")]`. Iterates `ItemDatabase`, calls composer for each item, assigns `worldDropPrefab`/`placedPrefab` on the ItemDefinition.

**Acceptance:** 1031 item prefabs generated; spawning a random one in the scene shows a recognisable category-colored shape.

### Chunk 3.4 — Icon Renderer
**Files to create:**
- `Assets/Editor/ArtPipeline/IconRenderer.cs` — `Sprite RenderIcon(GameObject prefab)`. Creates an off-screen RenderTexture (256×256), a stylized camera with isometric framing, a Directional Light, instantiates the prefab, frames it, reads pixels into a Texture2D, saves as a PNG under `Assets/Textures/Icons/{id}.png`, imports as Sprite. Camera uses URP, transparent background.
- `Assets/Editor/ArtPipeline/IconBulkRenderer.cs` — `[MenuItem("Voidborne/Generate/Item Icons")]`. Iterates ItemDatabase, renders each, assigns to `ItemDefinition.icon`. ~5min for 1000+ items — show a progress bar.

**Acceptance:** Every item has a PNG icon; opening inventory shows icons not placeholder squares.

### Chunk 3.5 — Fauna / Enemy / NPC Visual Recipes
**Files to create:**
- Extend `ItemVisualRecipeMapper` (or new `CreatureVisualRecipeMapper`) with rules for fauna/enemies/NPCs based on the `appearance` field. Fallback: capsule + sphere + simple appendages (legs for ground, wings for sky, tendrils for Vord).
- `Assets/Editor/ArtPipeline/CreaturePrefabGenerator.cs` — Builds prefabs for fauna/enemy/NPC definitions. Adds a `CharacterController` and the appropriate AI stub MonoBehaviour (real AI in Volumes 14/15).
- Bosses get a 2× scaled version with extra layers from their `appearance` string.

**Acceptance:** Every fauna/enemy/NPC has a prefab. Spawning a "Fungal Brood Mother" shows a recognisable creature silhouette, ~3m tall.

### Chunk 3.6 — One-Click Generate Visuals
- `Assets/Editor/ArtPipeline/GenerateAllVisuals.cs` — `[MenuItem("Voidborne/Generate/⟳ All Visuals")]`. Runs materials → primitives → item prefabs → creature prefabs → icons in order.

**Acceptance:** After Volume 2 has run, this single menu populates every generated asset's visuals end-to-end.

---

## VOLUME 4 — UI FOUNDATIONS (MINECRAFT-STYLE)

Goal: replace the existing UI with a clean Minecraft-style overlay: hotbar at bottom, inventory
toggle, crafting station panels, machine UIs, tooltip, dialog. Remove all "in-world UI" — no
floating panels in 3D space; everything lives on a screen-space Canvas. Reuse the existing
`UIManager`, `InventoryUI`, `HotbarUI`, `SlotUI`, `TooltipUI`, `InventoryCursor` from Volume 2
work but redo their visual layout to match the HTML's monospace + dark palette.

### Chunk 4.1 — UI Style Kit
**Files to create:**
- `Assets/Scripts/UI/Style/UIStyle.cs` — Static class returning fonts (TMP "JetBrains Mono" loaded from Resources — bundle the OTF), color palette (matching the HTML: bg `#0a0e14`, panel `#14181f`, border `#2a2f38`, accent `#b6f73e`, text `#c9d1d9`, dim `#8b949e`).
- `Assets/Scripts/UI/Style/UIBuilder.cs` — Helpers for building styled Canvas children: `RectTransform Panel(Transform parent)`, `TextMeshProUGUI Text(...)`, `Image SlotBg(...)`, `Button Btn(...)`. Every UI script in this volume uses these helpers, so visual changes are global.
- `Assets/Resources/Fonts/JetBrainsMono-SDF.asset` — TMP font asset.

**Acceptance:** A debug panel built with UIBuilder matches the HTML reference screenshot's style.

### Chunk 4.2 — HUD Layout (Hotbar + Health + Stamina + Crosshair)
**Modify:**
- `Assets/Scripts/UI/HotbarUI.cs` — Restyle: 9 slots horizontally, 50×50 each, 4px gap, dark bg, gold border on selected. Show item icon + stack count. Slot 1-9 keys + scroll wheel select.
- `Assets/Scripts/UI/HudUI.cs` — NEW. Health bar, stamina bar, temperature gauge (cold/heat for Cryo/Pyro biomes), corruption gauge (rises near Vord areas). All bottom-left, monospace numerics.
- `Assets/Scripts/UI/CrosshairUI.cs` — Already exists from Vol 4; restyle to match.

**Acceptance:** Playmode shows hotbar/health/stamina/temp/corruption + crosshair, all matching the style kit.

### Chunk 4.3 — Inventory Panel (Minecraft-style)
**Modify/Replace:**
- `Assets/Scripts/UI/InventoryUI.cs` — Single dark panel center-screen when Tab/I pressed. Sections: Personal 2×2 craft grid + output (top-right), Main 9×3 inventory (middle), Hotbar mirror 9×1 (bottom). Drag/drop, shift-click quick-move, right-click split, hover tooltip. **REMOVE** any backpack section here — backpacks open as a side-panel only when equipped (Volume 11).
- `Assets/Scripts/UI/SlotUI.cs` — Already exists; restyle.

**Acceptance:** Press Tab → inventory opens centered, cursor unlocked; click+drag works between all slots; press Esc/Tab → closes, cursor relocks.

### Chunk 4.4 — Machine UI Frame
**Files to create:**
- `Assets/Scripts/UI/MachineUI.cs` — Generic machine panel. Layout: machine name header, input grid (size from `MachineDefinition.gridWidth/gridHeight`), recipe tabs (color-coded A-E for multi-recipe items, matching the HTML's `RECIPE_COLORS`), output slot, optional fuel slot, optional progress bar, optional power gauge. Open via interaction with a placed machine (Volume 9 wires it up).
- `Assets/Scripts/UI/RecipeTabsUI.cs` — Renders one button per recipe for the active item or machine, color-coded.

**Acceptance:** Calling `MachineUI.Open(machine)` with a stub machine shows the panel; switching tabs highlights the active recipe.

### Chunk 4.5 — Tooltip & Dialog
**Modify/Create:**
- `Assets/Scripts/UI/TooltipUI.cs` — Already exists. Expand to show: name, kind+category badges, role/description, recipe summary count, mining tier (if tool), damage (if weapon).
- `Assets/Scripts/UI/DialogUI.cs` — NEW. Speaker name, body text, response buttons. Used by Volume 16 NPCs and Volume 18 story.

**Acceptance:** Hovering an item shows the rich tooltip; calling `DialogUI.Show(speaker, text, [responses])` displays it.

### Chunk 4.6 — Remove In-World UI
- Search the project (`Grep` for "WorldSpace", "Canvas.renderMode == WorldSpace", any `Canvas` in scene with WorldSpace mode) and delete every in-world UI element. Remove their owning scripts.
- Replace any prior on-machine 3D text/labels with a screen-space label that fades in when the player looks at the machine (proximity raycast, `GameObject InteractTarget` set per-frame).

**Acceptance:** Zero WorldSpace canvases remain. Looking at a machine shows a corner-of-screen label `[E] Open Workbench`.

---

## VOLUME 5 — LEGACY ASSET CLEANUP

Goal: delete the stale ScriptableObject assets created by old Volumes 2-3-4-5 work so the new
generated assets are the only ones in the project. This volume must run AFTER Volume 2 has
produced replacement assets, and BEFORE Volume 6 starts wiring new gameplay.

### Chunk 5.1 — Asset Scrap Manifest
**Files to delete** (after verifying the new generated equivalents exist):

```
Assets/ScriptableObjects/Items/*.asset           — 10 old item assets (wood, stone, etc.)
Assets/ScriptableObjects/Items/Tools/*.asset     — old tool assets
Assets/ScriptableObjects/Recipes/*.asset         — 10 old crafting recipes
Assets/ScriptableObjects/SmeltingRecipes/*.asset — old smelting recipes
Assets/ScriptableObjects/Ores/*.asset            — 6 old ore definitions (will be regenerated tied to new items)
Assets/ScriptableObjects/Guns/*.asset            — 5 starter guns
Assets/ScriptableObjects/Melee/*.asset           — if any exist
Assets/Prefabs/Workbench.prefab                  — superseded by Volume 9 procedural workbench
Assets/Prefabs/Furnace.prefab                    — superseded by Volume 9 furnace
Assets/Resources/ItemDatabase.asset              — regenerated by Volume 2
Assets/Resources/OreRegistry.asset               — regenerated by Volume 12 biome rework
```

**Files to create:**
- `Assets/Editor/Cleanup/LegacyAssetWiper.cs` — `[MenuItem("Voidborne/Cleanup/Wipe Legacy Assets")]`. Confirmation dialog. Deletes each path above. Logs each deletion. After: AssetDatabase.Refresh.

**Acceptance:** Wiper runs cleanly; scene still loads (any scene references to deleted assets are surfaced as missing-reference warnings and fixed in 5.2).

### Chunk 5.2 — Scene Reference Fixup
- Open `Assets/Scenes/SampleScene.unity` (rename to `Game.unity` if not already). Audit every MonoBehaviour with missing SO references. Replace with the new generated assets from Volume 2 (e.g., `ItemDatabase` field on `PlayerInventory` → `Assets/Resources/ItemDatabase.asset` regenerated).
- Move any scene-placed legacy prefabs (old Workbench, old Furnace) out — they'll be replaced by new versions placed via the build system in Volume 7.

**Acceptance:** Scene opens with zero missing-reference warnings; pressing Play boots into the world with a player and a generated terrain, no exceptions.

### Chunk 5.3 — Code Cleanup
- Move any code files specifically tied to the old asset shapes into `Assets/Scripts/_Legacy/`. Delete or refactor: `BackpackItem.cs` (extend `ItemDefinition` instead — Volume 11), legacy `SmeltingRecipe.cs` (replaced by generic `RecipeDefinition`), legacy `OreAssetCreator.cs`/`SmeltingRecipeCreator.cs`/etc. editor utilities.
- Keep all combat code (`GunDefinition` referenced as a runtime weapon shape — its asset will be created by the weapon SO generator in Volume 10).

**Acceptance:** Project compiles with zero errors after cleanup.

---

## VOLUME 6 — INVENTORY & CRAFTING v2

Goal: upgrade the crafting system to match the HTML's working example — recipes are
**machine-scoped** (each recipe lives at a specific via-machine, including null/personal grid),
items can have **multiple recipes** (alternate paths), and **bootstrap recipes** unlock
progression (you craft a Furnace at the personal grid before you have a Furnace).

### Chunk 6.1 — Crafting Match Engine v2
**Modify:**
- `Assets/Scripts/Crafting/CraftingGrid.cs` — Add `string scope` (machine ID, or `null` for personal grid). `FindMatchingRecipe(RecipeRegistry, IReadOnlyDictionary<string,int> available)` — returns the recipe that matches the grid contents AND is registered for this scope. **Match is shapeless** (bounding-box per-cell match still supported for visual recipes, but bag-of-items matching is the default — matches the HTML's working example where players drag ingredients into N slots regardless of position).
- `Assets/Scripts/Crafting/CraftingManager.cs` — Index recipes by `viaMachineId` at startup. Add `IEnumerable<RecipeDefinition> AvailableRecipes(string machineId, IReadOnlyDictionary<string,int> playerInventory)` for the "recipes I could craft right now" sidebar.

### Chunk 6.2 — Personal Crafting Grid (2×2)
**Modify:**
- `Assets/Scripts/Player/PersonalCraftingGrid.cs` — Sets `scope = null`. Available recipes are the subset of `RecipeRegistry` with `viaMachineId == null` AND fitting in a 2×2 (≤4 ingredients).
- `Assets/Scripts/UI/InventoryUI.cs` — Wire the 2×2 personal grid to a `RecipeListPanel` showing all currently-craftable personal recipes (clicking a recipe auto-fills the grid).

**Acceptance:** Open inventory, see "Workbench" listed as craftable when player has wood×4 + stone×2 + plant_fiber×2; clicking crafts it.

### Chunk 6.3 — Machine Crafting Stations
**Modify:**
- `Assets/Scripts/Crafting/CraftingStation.cs` — Replaces the 3×3 hardcoded workbench. Now driven by `MachineDefinition` (linked at spawn). Grid size = machine.gridWidth × gridHeight (Workbench is 3×3; Press is 1×1 with auto-fill from a recipe pick; Apiary is single-slot specialty).
- Each placed machine prefab (from Volume 3) gets a `MachineRuntime` component (Volume 9) that wraps `CraftingStation`.

**Acceptance:** Placing a Workbench, interacting, opens a recipe-list UI for all workbench-scoped recipes; selecting one auto-fills inputs from inventory and crafts.

### Chunk 6.4 — Bootstrap Path Validator (EditMode test)
**Files to create:**
- `Assets/Tests/EditMode/CraftingBootstrapTests.cs` — Asserts every item in the game is **reachable from empty hands** by walking the recipe graph backwards. Starting set: items with `kind == Source` (mineable/harvestable from world). For every other item, BFS through `recipes[]` looking for at least one path whose every input is reachable. Failures = items that are content-locked behind themselves (a bug).

**Acceptance:** Test passes. If it fails, the offending items are logged with their unsatisfiable dependency chains.

---

## VOLUME 7 — BUILDING & GRID SYSTEM

Goal: a Minecraft-meets-Rust building system: 17 build materials × 5 forms = 85 base blocks +
45 decorative blocks. Player-asserted virtual grid origin on first block placement; all later
placements snap to that origin. Terrain leveling tools to carve flat platforms for builds.
In-game blueprint capture/place for reusable structures.

### Chunk 7.1 — Block Placement & Virtual Grid
**Files to create:**
- `Assets/Scripts/Building/BuildGrid.cs` — Singleton MonoBehaviour. Holds `Vector3 origin` (set on first block placed), `float cellSize = 1f`. Methods: `Vector3Int WorldToCell(Vector3)`, `Vector3 CellToWorld(Vector3Int)`. Snapping operates in cell space.
- `Assets/Scripts/Building/PlacedBlock.cs` — Component on every placed block. Fields: `string itemId`, `Vector3Int cell`, `Quaternion rotation`, `int healthRemaining`. Damage handler integrates with Volume 10 melee/projectile hits.
- `Assets/Scripts/Building/BlockPlacer.cs` — On the player. When holding a build-tagged item (block), raycast forward, show a ghost preview at the snapped cell, on left-click consume one from inventory and instantiate the `placedPrefab` at the cell. Right-click rotates ghost 90° around Y. Forms (slab, panel, stairs, door) snap to half/quarter cells and have specific orientation rules.
- `Assets/Scripts/Building/BlockRegistry.cs` — Per-cell lookup (`Dictionary<Vector3Int, PlacedBlock>`); for queries like "what's adjacent to this cell" (used by stairs orientation, wire routing, structural placement).

**Acceptance:** Equip wood_cube, place → grid origin pinned at first block, subsequent blocks snap to the grid.

### Chunk 7.2 — Block Forms & Variants
- Generated block prefabs (Volume 3) include the form-specific mesh (cube, slab, panel, stairs, door). `BlockPlacer` reads the form from `ItemDefinition.role` (e.g., "building — slab") and applies form-specific snap rules.
- Door form gets a hinge + open/close interaction (use `IInteractable` from existing chunk 2.6 work).

**Acceptance:** Place a stair block — orientation reads player look direction and snaps correctly.

### Chunk 7.3 — Terrain Leveling Tool
**Files to create:**
- `Assets/Scripts/Building/TerrainLevelTool.cs` — Tool-class item (will be generated as a craftable in Volume 2; ID e.g., `terrain_leveler`). Equip + left-click on terrain raises/lowers all voxels in a configurable radius to the cell-Y of the click point (averaging to a flat plane). This is *terrain deformation*, not block placement — it modifies the marching-cubes density field via `TerrainDeformer` (Vol 1.8).

**Acceptance:** Use the tool on rolling hills → carves a flat platform suitable for placing blocks.

### Chunk 7.4 — Blueprint Capture & Place
**Files to create:**
- `Assets/Scripts/Building/BlueprintCapture.cs` — Tool. Player selects a rectangular volume in cells (drag two corners), captures every `PlacedBlock` in that volume, saves to a `Blueprint` ScriptableObject under `Assets/ScriptableObjects/Blueprints/{name}.asset`.
- `Assets/Scripts/Building/BlueprintPlacer.cs` — Tool. Holding a Blueprint item shows a translucent ghost of the entire structure aligned to the build grid. Click places, consuming all required materials from inventory (or refuses if insufficient).
- `Assets/Scripts/Building/Blueprint.cs` — SO: bounding box + array of `{ string itemId, Vector3Int relativeCell, Quaternion rotation }`. Includes a materials-list summary for UI display.

**Acceptance:** Build a 5×5×3 shed, capture it as a blueprint, place it elsewhere (consuming the same materials).

### Chunk 7.5 — Block Breaking
- Reuse the existing `PlayerMining.cs` (from Vol 3.3) but split: terrain mining stays as-is; **block mining** is a separate path. Hitting a `PlacedBlock` drops the block's item (1 unit, no tool-tier requirement for now) and removes the PlacedBlock entry.

**Acceptance:** Place block → break with any tool → block returns to inventory.

---

## VOLUME 8 — POWER & WIRING

Goal: 54 power generators, 3 cable tiers, junction/regulator/sink, battery storage, overclock
modules. Power is consumed by machines (Volume 9), powered weapons (Volume 10), and gadgets
(Volume 11). The HTML's power generator list is in `categories.json["power"]`.

### Chunk 8.1 — Power Network Graph
**Files to create:**
- `Assets/Scripts/Power/PowerNetwork.cs` — Singleton. Maintains a graph: nodes = generators, machines, batteries; edges = cables. Each generator publishes `int currentWatts`; each consumer requests `int requiredWatts`. Solver runs every fixed frame: traverse connected components, sum generation, distribute to consumers in priority order (machines first, gadgets last), unused goes to batteries, overflow goes to sinks.
- `Assets/Scripts/Power/PowerNode.cs` — Base MonoBehaviour. `bool IsGenerator`, `bool IsConsumer`, `bool IsStorage`. Auto-registers on Enable, deregisters on Disable.
- `Assets/Scripts/Power/PowerCableTier.cs` — Enum T1/T2/T3 with wattage ceilings (200/1000/5000W). Exceeding ceiling = burnout (cable destroyed, visual smoke). Tied to the three cable items in `items.json` (`power_cable_t1/t2/t3`).
- `Assets/Scripts/Power/CableSegment.cs` — Placed cable component; connects two `PowerNode` endpoints. Auto-routes via line-of-sight raycasts at placement.

**Acceptance:** Place a Burn Generator + Wood Cube wired with a T1 cable to a stub consumer → consumer reports power.

### Chunk 8.2 — Generators (54 types)
- For each ID in `categories.json["power"]`, generate a `PowerGenerator` MonoBehaviour. Most share one base class (`PowerGenerator`) with config fields driven by the JSON `role` string (the role contains the wattage like "150W" or "passive" or "5W/step").
- Special-cases that need unique behavior get a dedicated subclass: `WindRotorGenerator` (output scales with altitude wind sim from biome), `SolarPanelGenerator` (output scales with sky exposure raycast + time-of-day), `FootstepTileGenerator` (output spikes on overlap), `LightningRodGenerator` (output spikes during storm events), `AshPupTreadmillGenerator` (output requires tamed Ash Pup nearby — Volume 14 dependency).

**Acceptance:** Placing each generator type produces its expected watts in the inspector.

### Chunk 8.3 — Storage & Distribution
**Files to create:**
- `Assets/Scripts/Power/Battery.cs` — Stores up to `capacityWs` watt-seconds. Charges when surplus, discharges when deficit. Capacity tied to item (battery_bank, battery_cell, etc.).
- `Assets/Scripts/Power/JunctionBox.cs` — Just a multi-port node (acts as wire hub). No logic.
- `Assets/Scripts/Power/VoltageRegulator.cs` — Caps the watts passed through to consumers downstream; protects against burnout when over-generation could spike a cable.
- `Assets/Scripts/Power/OverclockModule.cs` — Attached to a machine; doubles power draw and grants 2-3× speed multiplier to that machine's recipe time.
- `Assets/Scripts/Power/PowerSink.cs` — Consumer that absorbs and discards surplus watts (player builds these when over-generating).

**Acceptance:** Surplus power charges batteries; deficit drains them; voltage regulator prevents burnout in a test rig.

### Chunk 8.4 — Worn Battery Pack
- `Assets/Scripts/Power/WornBatteryPack.cs` — Equippable item (slot in Volume 11). While equipped, provides power to powered weapons and powered armor. Drains over time when consumed. Recharges at a placed Battery Bank.

**Acceptance:** Equipped Battery Pack lets a Beam Rifle (Volume 10) fire; running out of charge disables the rifle.

---

## VOLUME 9 — MACHINES & AUTOMATION

Goal: 43 machines (T1–T7) plus automation infrastructure (conveyors, sorters, storage,
auto-variants). Each machine reads its recipes from `RecipeRegistry.ByMachine(machineId)` and
processes them with the recipe's input requirements, output, and optional power consumption.

### Chunk 9.1 — MachineRuntime Base
**Files to create:**
- `Assets/Scripts/Automation/MachineRuntime.cs` — MonoBehaviour. Wraps `CraftingStation` + `PowerNode`. Reads `MachineDefinition` (gridWidth, gridHeight, needsPower, powerDrawWatts, tier). On interact, opens `MachineUI` (Vol 4.4) with the machine's recipes. Tracks active recipe + progress timer. Power-gated recipes pause when power = 0.
- `Assets/Scripts/Automation/RecipeProcessor.cs` — Pure logic class. Given a recipe + input inventory: validate ingredients, consume on start, produce output on completion, refund on cancel.

**Acceptance:** Placing a generic Workbench (T1) and a Furnace (T3) lets both craft their recipes; Furnace pauses without fuel/power.

### Chunk 9.2 — T1–T3 Machines (No Automation)
- Generate placed-machine behavior for each T1/T2/T3 machine in `items.json` with `kind == Machine`:
  - Workbench, Campfire, Mortar, Drying Rack, Cleaver Block, Salt Box, Growth Plot, Fish Trap,
  - Furnace, Oven, Cast Iron Pot, Skillet, Brewing Keg, Cheese Press, Sealed Jar,
  - Forge, Loom, Stove, Smoker Box, Tumbler, Bee Smoker, Taxidermy Bench, Cheese Press.
- Most just use the base `MachineRuntime`. A handful need specific behavior:
  - `Campfire` — passive heat aura (warmth status), fuel slot (wood/charcoal).
  - `Growth Plot` — slow tick growth based on planted seed item.
  - `Fish Trap` — auto-spawns fish items over time when placed in water-tagged voxels.
  - `Apiary` — needs flowers within radius (queries placed deco / flora) to produce honey.

**Acceptance:** Each T1-T3 machine, when placed and fed inputs, produces its expected output.

### Chunk 9.3 — T4–T5 Machines (Powered)
- Composter, Animal Pen, Feed Trough, Apiary, Press, Forge, Drip Irrigator,
- Tannery, Trap Workshop, Vehicle Rig, Grinder, Centrifuge, Fermenter, Refinery,
- Auto-Milker, Crusher, Chemistry Set.
- All connect to PowerNetwork. Recipe processing rate scales with power supply (under-powered = slower).

**Acceptance:** Powered machines respect power; tested with a Burn Generator → wire → Press.

### Chunk 9.4 — T6–T7 Machines (Advanced)
- Etcher, Assembler, Hangar Lift.
- Auto-Furnace, Auto-Crafter, Auto-Press, Auto-Assembler, Self-Loading Reloader, Auto-Composter, Auto-Farmer, Auto-Brewery, Milking Station.
- Auto-variants pull ingredients automatically from connected storage (Chunk 9.6) and push outputs to connected storage.

**Acceptance:** A complete autocrafter chain (storage → conveyor → auto-crafter → conveyor → storage) runs unattended.

### Chunk 9.5 — Transport (Conveyors, Tubes, Pipes, Couriers)
- Per `categories.json["automation"]`:
  - Tech: Conveyor Belt, High-Speed Belt, Item Pipe, Inserter Arm, Robotic Arm.
  - Mechanical: Chain Conveyor, Gravity Chute, Water Sluice, Spring Launcher (no power).
  - Alchemist: Pneumatic Tube, Fluid Pipe.
  - Beekeeper: Bee Courier Hive.
  - Beastmaster: Pack Trail Beacon.
  - Gardener: Vine Conduit.
  - Cryo: Frost Rail.
  - Pyro: Steam Pipe.
  - Spirit: Bone Tether.
  - Mystic: Sigil Link Stone (paired teleport — items vanish at A, appear at B).
- One base `ItemTransport` MonoBehaviour with per-type subclass for special behavior (bee courier flies a route, sigil link teleports, etc.).

**Acceptance:** Conveyor moves stacks of items between two storage chests at the documented speed.

### Chunk 9.6 — Sorting & Storage
- Sorters: Item Sorter (whitelist), Filter Hopper (single-item filter), Auto-Splitter, Logic Gate, Signal Relay, Animal Sorter, Bee Sorter, Sorting Crystal.
- Storage: Storage Chest, Reinforced Storage, Cold Storage, Hopper Buffer, Fluid Tank, Bulk Silo.
- Storage exposes a `Inventory` instance and registers with `PowerNetwork` for automation queries.

**Acceptance:** Item Sorter routes copper to chest A and iron to chest B from a mixed input stream.

---

## VOLUME 10 — COMBAT REFACTOR

Goal: extend the existing guns+melee codebase (kept from old Vol 4–5) to cover the HTML's full
combat scope: bows, spears, thrown weapons, traps, turrets, powered weapons. Existing recoil /
spread / parry / chamber systems stay. New weapon SOs come from `items.json` via the
Volume 2 generator.

### Chunk 10.1 — Weapon SO Generator
**Files to create:**
- `Assets/Editor/Data/WeaponSoGenerator.cs` — Iterates `categories.json["weapon"]`. For each weapon ID, decides type from item name patterns (bow/rifle/sniper/cannon/lance/blade/etc.) and produces either a `GunDefinition`, `MeleeDefinition`, `BowDefinition`, `ThrownDefinition`, `TrapDefinition`, or `TurretDefinition`.
- Base stats come from heuristics on tier and category; subsequent balancing iteration happens in Volume 22.

**Acceptance:** 42 weapon assets generated, each typed correctly.

### Chunk 10.2 — Bow & Crossbow
**Files to create:**
- `Assets/Scripts/Combat/Bows/BowDefinition.cs` — Draw time, max damage, arrow speed, arrow item type, magazine (1 for bow, N for crossbow), reload time.
- `Assets/Scripts/Combat/Bows/BowController.cs` — Hold-to-draw, release to fire. Spawns an arrow projectile (Vol 6.1 projectile code) with damage scaled by draw fraction. Ammo consumed from inventory (`bone_arrow`, `obsidian_arrow`, etc.).

### Chunk 10.3 — Thrown & Spear
- `Assets/Scripts/Combat/Thrown/ThrownController.cs` — Right-click windup, release throws the held item (consumed) as a projectile. Throwing knives, spears, boomerangs (special: returns to player), bombs.

### Chunk 10.4 — Powered Weapons
- `Assets/Scripts/Combat/Powered/PoweredWeaponController.cs` — Beam Rifle, Tesla Lance, Plasma Cutter, Railgun, Sonic Disruptor, Acid Spray Gun, Frost Cannon, Cinder Cannon, Void Lance, Arc Pistol. Consumes power from worn Battery Pack (Vol 8.4) per shot. Fires hitscan or a beam (line renderer) depending on weapon.

### Chunk 10.5 — Traps & Turrets
- `Assets/Scripts/Combat/Defense/TrapBase.cs` — Placed defense, triggers on enemy proximity/overlap. Spike Trap, Pressure Bomb, Bear Trap, Snare, Honey Slick, Acid Pool, Glue Trap, Frost Geyser, Sound Mine, Bee Swarm Trap, Spore Burst.
- `Assets/Scripts/Combat/Defense/TurretBase.cs` — Placed auto-firing weapon. Targets nearest enemy in range, fires at cooldown. Auto-Crossbow Turret, Flamethrower Nozzle, Spike Launcher, Stinger Hive Turret, Spore Launcher Turret, Frost Beam Emitter, Tesla Coil, Acid Sprayer, Spotlight Turret, Mortar, Sentry Drone Bay.

**Acceptance:** Each trap/turret reacts to a test dummy enemy as expected.

### Chunk 10.6 — Armor & Damage Resistance
- `Assets/Scripts/Combat/Armor/ArmorDefinition.cs` — Per-slot armor (helm/chest/legs/cloak). Damage reduction per damage type. Special effects per archetype set (e.g., Cinder gear → heat resist; Mist-Walker Cloak → silence boost).
- `Assets/Scripts/Combat/Armor/PlayerArmor.cs` — On player. Aggregates equipped armor stats. Modifies incoming `DamageInfo.amount`.

**Acceptance:** Equipped Iron Helm reduces head-shot damage to player by tuned %.

---

## VOLUME 11 — TOOLS, GADGETS, WEARABLES

Goal: the HTML's gadgets, wearables, and special tools — equippable items beyond weapons that
modify player capability. ~42 gadget IDs in `categories.json["gadget"]`.

### Chunk 11.1 — Backpack System
- `Assets/Scripts/Inventory/Backpack.cs` — Component on player. Equipping a backpack item adds N extra inventory rows. Side-panel UI when inventory is open.
- Generated backpack items from items.json (pack_basket, etc.).

### Chunk 11.2 — Movement Gadgets
- Grav Boots, Rocket Boots, Mag Boots, Wingsuit, Grapple Gun (preserves the existing Tether grapple from Volume 1 work), Bungee Anchor, Grav Tether. Implement as `IGadget` components consuming optional power.

### Chunk 11.3 — Utility Gadgets
- Shield Emitter, Phase Cloak, Sonic Emitter, Decoy Beacon, Repulsor, Static Glove, Sanctuary Stone, Mirror Shield, Calming Lantern, Sigil Totem, Music Box, Pheromone Diffuser, Wildlife Whistle, Healing Beacon, Banner Pole, Lure Whistle.

### Chunk 11.4 — Wearable Armor Effects
- Power Vest, Servo Exoskeleton, Force Field Cloak, Coolant Suit, Heat Suit, Pulse Helmet, Mecha Suit, Spirit Armor, Bee Swarm Shield — each grants a special active or passive ability.

### Chunk 11.5 — The Index (player's bracer UI device)
- `Assets/Scripts/Tools/Index.cs` — Already exists per memory `project_index_device.md` (was "Cortex Device"). Confirm intro sequence + 5-slot hotbar + Tether grapple match the new design. Hook up the new UI style kit (Volume 4).

**Acceptance:** Each gadget works in isolation; movement gadgets compose with player controller.

---

## VOLUME 12 — BIOME REWORK (REALISTIC SCALE, GRASS, AMBIENCE)

Goal: replace the existing 4-biome basic implementation with the HTML's 5-biome design at
realistic scale. Add dense grass and decoration, ambient sound, weather/temperature, and ore
distributions appropriate per biome. Vol 1.5/1.6 biome code is the foundation; this volume
rewrites parameters and adds layers.

### Chunk 12.1 — Biome Definitions (5 biomes)
**Files to create/replace:**
- `Assets/ScriptableObjects/Biomes/Lowlands.asset` — gentle rolling hills, sparse forest, temperate.
- `Assets/ScriptableObjects/Biomes/Badlands.asset` — hot, red sand, sparse hardwood (Ironbark), petroleum seeps.
- `Assets/ScriptableObjects/Biomes/FrozenPeaks.asset` — brutal cold, jagged mountains, frostpine forest, blizzards.
- `Assets/ScriptableObjects/Biomes/FungalMarshes.asset` — spore-thick limited-vis, marsh silt, glowcap groves, acidic pools.
- `Assets/ScriptableObjects/Biomes/SkyIslands.asset` — floating platforms (high Y density inversion), sundials, wheat bush.
- Each biome lists: source items spawned (from items.json IDs), enemy spawn lists, fauna spawn lists, ambient sound clips, temperature range, fog density, sky tint.
- `Assets/Scripts/World/Generation/BiomeMap.cs` — Extend with 5 biome regions assigned by 2D Voronoi or temperature+moisture noise.

**Acceptance:** Flying around shows 5 visually distinct regions with smooth blends.

### Chunk 12.2 — Realistic Grass (GPU-Instanced)
**Files to create:**
- `Assets/Shaders/Grass.shader` — GPU-instanced grass blade shader. Wind sway from world position + time. Per-biome color (Lowlands green, Badlands ochre, FrozenPeaks white, FungalMarshes purple-tinged, SkyIslands gold).
- `Assets/Scripts/World/Decoration/GrassDecorator.cs` — Per active chunk, samples surface positions on the top voxels, instances grass meshes via `Graphics.DrawMeshInstanced`. Density per biome (low for Badlands, high for Lowlands). Culled beyond a configurable distance. Triangle count tracked — total active grass capped at ~500K to keep frame budget.
- `Assets/Scripts/World/Decoration/ScatterDecorator.cs` — Rocks, fallen logs, frost crystals, mushrooms (per-biome). Static, batched.

**Acceptance:** Lowlands looks like a grassy plain at scale; standing in tall grass at player height feels immersive; FPS stable above 60.

### Chunk 12.3 — Trees & Large Flora
- Per-biome tree placement (Ironbark in Badlands, Frostpine in FrozenPeaks, mixed in Lowlands, glowcap groves in FungalMarshes, sundials on SkyIslands).
- Trees are placed as full prefabs (Volume 3's generated flora prefabs) at biome-appropriate density. Chunk loader spawns/despawns with chunks.

**Acceptance:** Forests visible from kilometers away; densities feel correct per biome.

### Chunk 12.4 — Weather, Temperature, Time of Day
- `Assets/Scripts/World/Weather.cs` — Per-biome weather events: rain (Lowlands), sandstorm (Badlands), blizzard (FrozenPeaks), spore mist (FungalMarshes), high winds (SkyIslands). Affects visibility + player temperature.
- `Assets/Scripts/Player/PlayerTemperature.cs` — Player has a temperature stat. Biome cold/heat + weather + equipped armor modify it. Out of safe range = damage tick + UI warning.
- `Assets/Scripts/World/TimeOfDay.cs` — Day-night cycle (default 20-minute day). Drives directional light + skybox + ambient color.

**Acceptance:** Standing in FrozenPeaks without Cryo gear → temperature drops → damage starts.

### Chunk 12.5 — Per-Biome Ore Distribution
- Update `OreGenerator.cs` so the 15 ores from `categories.json` map to biome-restricted spawn lists. Iron everywhere shallow; Titanium deep; Petroleum Seep in Badlands only; Geothermal in cave biome only; etc.
- Regenerate `OreRegistry.asset` to point at the new generated ore item SOs.

**Acceptance:** Mining each biome reveals expected ore types per the HTML's source distribution.

---

## VOLUME 13 — STRUCTURE & ROAD PLACEMENT (Kin ruins, Vord strongholds, blueprints)

Goal: hand-curated and procedural placement of buildings and roads in the world. Kin ruins
(pre-corruption Kin civilization remnants), Vord strongholds (corrupted versions, 30 hand-placed
+ infinite Echo Strongholds procedural in NG+), road networks connecting points of interest.
Uses the Volume 7 blueprint system as the placement primitive.

### Chunk 13.1 — Structure Placement Pass (during chunk generation)
- `Assets/Scripts/World/Structures/StructurePlacer.cs` — On chunk generation, queries `StructurePlanGrid` (a coarse 256m-cell grid storing "this cell should contain a Kin ruin / Vord stronghold / road junction"). If a structure is assigned, spawns the corresponding `Blueprint` (from Volume 7) at the cell's location, terrain-levels the foundation, and registers all blocks as part of that structure.
- `Assets/Scripts/World/Structures/StructurePlan.cs` — Deterministic per-seed plan: divides world into 256m cells, assigns biome-appropriate structure templates. 5 Kin ruin templates × 5 biomes; 5 Vord stronghold templates × T1/T2/T3 = 15.

**Acceptance:** Walk to a planned stronghold cell, terrain is leveled, a stronghold appears.

### Chunk 13.2 — Blueprint Authoring
- The 30 hand-placed strongholds are authored in-game by a designer (you, the user) using the Volume 7 BlueprintCapture tool, then saved under `Assets/ScriptableObjects/Blueprints/Strongholds/`.
- The 15 Kin ruin templates are similarly authored.

**Acceptance:** Manifest of 45 blueprint assets created and assignable to `StructurePlan`.

### Chunk 13.3 — Road Network
- `Assets/Scripts/World/Structures/RoadNetwork.cs` — Generates roads between adjacent stronghold cells using A* on the StructurePlanGrid. Roads are realized as terrain leveling + a Road block placed every meter. Roads support fast travel hints (Atlas — Volume 17).

**Acceptance:** Roads visible from Atlas; following one connects Kin ruins.

### Chunk 13.4 — Echo Strongholds (procedural endgame)
- After Act 3 completes, `StructurePlan` starts generating Echo Strongholds beyond the original 30 cell radius with modifiers: Mist-shrouded (low vis), Swarming (extra enemies), Hardened (enemies have armor), Soul-bound (defeats give bonus Spirit Anchors).

**Acceptance:** Post-game generates Echo Strongholds endlessly outside the original campaign area.

---

## VOLUME 14 — FAUNA SYSTEM

Goal: 18 wildlife species per the HTML — passive herds, predators, sky fauna, tameable animals,
domesticated farm animals. AI built on the existing no-NavMesh enemy AI foundation (chunk 6.3).

### Chunk 14.1 — Fauna AI Base
- `Assets/Scripts/Fauna/FaunaAi.cs` — Reuses `EnvironmentSensor` (chunk 6.3). Behaviors driven by `FaunaDefinition.behaviors` strings — interpret with a simple keyword dispatcher: "Docile" → flee on damage; "Pack predator" → call allies; "Passive floater" → drift on wind currents.
- `Assets/Scripts/Fauna/FaunaSpawner.cs` — Per active chunk in active biome, spawns fauna up to per-species population caps. Day/night cycle modulates spawn weights.

### Chunk 14.2 — Fauna Per Species
- For each of 18 species (Graze, Cluck, Fleece-Goat, Cavebleat, Cavestalker, Glimmerwing, Driftwing, Skyserpent, Snowdrifter, Ash Pup, Spore Floater, Bog Frog, Cave Eel, Honey Bee, Thornback, Plodder, Rootback, Silk Spinner): hook the generated FaunaDefinition into an FaunaAi instance with species-specific tuning. Drops are spawned from the JSON's `drops[]` items on death.

### Chunk 14.3 — Taming & Pets
- `Assets/Scripts/Fauna/TameableComponent.cs` — Tameables: Cluck, Fleece-Goat, Driftwing, Ash Pup, Honey Bee, Plodder. Right-click with appropriate food (per species) starts a taming meter; complete = tamed (assigns to player's tame list).
- `Assets/Scripts/Fauna/PetAi.cs` — Tamed pets follow the player, defend, can be commanded to stay.
- `Assets/Scripts/Fauna/AnimalPenAttachment.cs` — Tamed farm animals can be assigned to an Animal Pen (Vol 9.3 machine), producing periodic items (milk, eggs, fleece, honey).

**Acceptance:** Each species exhibits its documented behavior; tamed pets follow; pen animals produce items over time.

---

## VOLUME 15 — ENEMIES & BOSSES

Goal: 11 fodder Vord types + 32 bosses (5 families × 6 + 2 finale). Reuse existing enemy AI
foundation (chunk 6.3) with per-boss behavior trees encoded in dedicated `Boss*.cs` classes.

### Chunk 15.1 — Fodder Enemy AI
- For each of 11 fodder types (Vord Drone, Slinger, Burrower, Sprinter, Brute, Spore-Bearer, Charger, Climber, Shrieker, Mender, Carrier): one MonoBehaviour per type, derived from a shared `VordFodderAi` base. Behavior driven by JSON `behaviors[]` keywords.
- `Assets/Scripts/Enemies/StrongholdEnemySpawner.cs` — Spawns enemies inside placed strongholds (Volume 13) per stronghold-tier rules.

### Chunk 15.2 — Boss Framework
- `Assets/Scripts/Enemies/BossController.cs` — Base for all bosses. Handles arena boundary (defined per stronghold), phase transitions (HP thresholds), ability scheduling, telegraph rendering.
- `Assets/Scripts/Enemies/BossAbility.cs` — Per-ability MonoBehaviour. Configured per boss in inspector (telegraph time, damage zone shape, effect).

### Chunk 15.3 — Brood Family (6 bosses)
- Fungal Brood Mother, Bone Brood Father, Frost Brood Lich, Aether Brood Empress, Vord Brood Queen, Mech Brood Director.
- Each gets a `Boss[Name].cs` with its specific abilities (Spore-Sac Burst, Fungal Spawn, etc. from JSON `abilities[]`).
- Drops include the boss's listed drop items (e.g., Fungal Brood Mother drops Cartographer's Lens).

### Chunk 15.4 — Warden Family (6 bosses)
- Stone Warden, Iron Sentinel, Glass Watcher, Bone Colossus, Mist Guard, Void Custodian.

### Chunk 15.5 — Hunter Family (6 bosses)
- Mist Hunter, Shadow Stalker, Spore Hunter, Frost Reaver, Sky Hunter, Vord Lance.

### Chunk 15.6 — Channeler Family (6 bosses)
- Mire Channeler, Cinder Caster, Frost Conduit, Storm Speaker, Bone Sigil-Keeper, Void Manipulator.

### Chunk 15.7 — Aberrant Family (6 bosses)
- Tendril Horror, Echoing Heart, Hollow Choir, Maw of the Abyss, Forgotten King, Carapace Apostle.

### Chunk 15.8 — Finale Bosses (2)
- The Hollow Source — cycles through all 5 family attack patterns.
- The Choice (4 variants for the 4 endings) — unique encounter per ending.

**Acceptance:** Each boss is reachable in its designed location and can be defeated; drops match the JSON's `drops[]` list.

---

## VOLUME 16 — NPCs, SPIRIT GATEWAY, FESTIVALS

Goal: 6 named Kin survivors, 3 traders, plus the Spirit Gateway system that hosts rescued
Kin spirits, festivals that bring traders/visitors to player bases.

### Chunk 16.1 — NPC Base & Dialog
- `Assets/Scripts/NPCs/NpcBase.cs` — Generic NPC behavior. Reads `NpcDefinition`. Greets player on proximity (`DialogUI` — Vol 4.5). `isKillable=false` overrides damage.
- `Assets/Scripts/NPCs/NpcDialogGraph.cs` — Per-NPC conversation tree. Authored as ScriptableObject. The 6 named Kin each get a hand-authored dialog graph (their backstory, side quests, what they sell).

### Chunk 16.2 — 6 Named Kin (rescue + base residents)
- Wren, plus 5 others (per NPC list from extracted JSON — verify names from npcs.json named entries).
- Each has a rescue location (in a specific stronghold during Act 1/2), a base behavior (where they go after rescue), recipes they unlock for the player, and a side quest.

### Chunk 16.3 — Traders & Caravans
- 3 trader NPCs from `npcs.json` traders. Each visits the player's base if a `Trade Post` + `Caravan Beacon` is placed nearby (items in items.json). Brings goods on a rotating schedule.

### Chunk 16.4 — Spirit Gateway
- `Assets/Scripts/NPCs/SpiritGateway.cs` — Placed structure. Players insert Spirit Anchors (dropped by bosses) to summon the spirit version of each cleansed Kin. Spirits provide passive buffs, hints, recipes.
- UI for managing summoned spirits.

### Chunk 16.5 — Festivals
- Periodic events (every N in-game days) that draw extra NPCs/traders to the base. Special festival-only items, fireworks, banquet table.
- Trigger via a Festival Banner placed by player (item from items.json).

**Acceptance:** Rescue Wren in Act 1; she follows back to base; her dialog tree branches based on world state.

---

## VOLUME 17 — ATLAS, INDEX, QUEST FRAMEWORK

Goal: the Atlas (player's discoverable infinite map), Index (player's bracer UI device, already
partially built per memory `project_index_device.md`), Recall Box (death recovery), Quest /
Bounty system.

### Chunk 17.1 — Atlas
- `Assets/Scripts/Quests/Atlas.cs` — UI map. Reveals chunks as the player visits. Tiers: Paper (early), Brass (mid), Electronic (endgame). Marks player base, rescued Kin, defeated bosses, trade routes, Echo Strongholds (post-game).
- Toggle with M key. Pan/zoom.

### Chunk 17.2 — Index v2 (intro + hotbar + grapple already exist)
- Confirm existing implementation matches new design. Wire to new UI style kit.
- Add: equipped gadget slot, current quest summary, Recall Box recovery indicator.

### Chunk 17.3 — Recall Box
- `Assets/Scripts/Quests/RecallBox.cs` — Placed structure. On player death, inventory drops at death location; player respawns at last placed Recall Box. Death penalty = 10% durability loss on equipped gear only.

### Chunk 17.4 — Quest & Bounty Framework
- `Assets/Scripts/Quests/QuestDefinition.cs` — SO. Fields: ID, title, description, objectives (typed: kill X, deliver Y, reach Z, craft W), rewards.
- `Assets/Scripts/Quests/QuestManager.cs` — Active quest list, completion tracking.
- `Assets/Scripts/Quests/BountyBoard.cs` — Placed structure offering 3 daily-rotating bounties.

**Acceptance:** Atlas reveals as player explores; quests track and complete on objective.

---

## VOLUME 18 — STORY ACTS (4 acts of scripted content)

Goal: the 4-act narrative arc. Story chunks are scripted sequences referencing the prior volumes
(spawn this enemy here, place this NPC there, trigger this dialog when X). All content from
the HTML's story section.

### Chunk 18.1 — Act 1: Awakening
- Intro sequence: player wakes in sealed vault under a Kin sanctuary, learns hand-crafting + building.
- Quest: build Recall Box.
- First emotional beat: small corrupted creature encounter.
- Boss: Fungal Brood Mother (Tier 1).
- Reward: Cartographer's Lens — unlocks Atlas.
- Rescue: Wren (first NPC).

### Chunk 18.2 — Act 2: Reclamation
- Atlas unlocked → 5 regions accessible.
- 5 major bosses unlock 5 archetypes (Stone Warden → Forge / Mire Channeler → Chemistry / etc.).
- Rescue: remaining 5 named Kin across regions.
- Build Spirit Gateway (mid-Act).
- Reveal: Citadel location + need for 7 Heart Fragments.

### Chunk 18.3 — Act 3: Heart Fragments
- 7 Aberrant bosses, each drops one Heart Fragment (some bosses share family pools).
- Moral pivot: Wren reveals she carries the 7th Fragment inside her — player chooses to end her or lose the path.
- Includes: Tendril Horror, Echoing Heart, Hollow Choir, Maw of the Abyss, Forgotten King, Carapace Apostle.

### Chunk 18.4 — Act 4: The Source
- Enter Citadel.
- Fight The Hollow Source.
- Reach Choice Chamber.

**Acceptance:** All 4 acts playable end-to-end; story beats trigger correctly; choices propagate.

---

## VOLUME 19 — VEHICLES (MODULAR)

Goal: the HTML's modular vehicle system — chassis + engine + wheels/locomotion + seat + steering
+ optional cargo platform. 83 vehicle-related items in `categories.json["vehicle"]`.

### Chunk 19.1 — Vehicle Assembly System
- `Assets/Scripts/Vehicles/VehicleAssembly.cs` — A vehicle is an assembly of parts (chassis, engine, wheels, seat, steering, optional cargo). Built at a Vehicle Rig (Volume 9 T5 machine) by selecting parts.
- Each part is an item from items.json with vehicle-specific metadata (chassis ID, weight, capacity).

### Chunk 19.2 — Ground Vehicles
- Bicycle, Cart, Wagon, Truck, Steam Wagon, Walker (mech legs), Skiff, Hovercraft, Hover Tank, Food Cart, Tavern Wagon, Greenhouse Wagon, Living Vehicle (mobile base!), Acid Sled, Lab Cart, Cinder Buggy, Inferno Tank, Iceglider, Frost Crawler, Stalker Cycle, Trail Wagon, Auto Rig, Construction Rig, Bone Wagon, Soul Carriage, Pulled Cart, Caravan, Diplomat Carriage, Scholar Mobile Lib, Nature Wagon, Trader Wagon.

### Chunk 19.3 — Aerial Vehicles
- Glider, Gyrocopter, Heavy Gyro, Light Plane, Cargo Plane, Sky Barge.

### Chunk 19.4 — Vehicle Physics & Damage
- Wheels and rotors physically simulated. Damage zones (engine block destroyable separately from chassis).

**Acceptance:** Player assembles a Cart at the Vehicle Rig, drives it across terrain.

---

## VOLUME 20 — ENDINGS & NG+

Goal: implement the 4 endings + NG+ Embrace mode.

### Chunk 20.1 — Choice Chamber
- 4-option dialog at the end of Act 4.

### Chunk 20.2 — Endings
- Seal: cinematic close of rift; corrupted enemies removed from world; quiet peace.
- Bind: rift contained; slow cleansing over in-game years; player can revisit.
- Step Through: player vanishes into rift; world saves as memorial.
- Embrace: player becomes Vord; corruption spreads; transitions to NG+ Embrace mode.

### Chunk 20.3 — NG+ Embrace Mode
- Player is now the corrupting force. Inverted objectives: spread Vord nodes, corrupt Kin spirits, destroy player-built sanctuaries (other-NG-players via netcode optional in Volume 21).
- Echo Strongholds spawn endlessly.

**Acceptance:** All 4 endings reachable; Embrace NG+ playable.

---

## VOLUME 21 — COOP NETWORKING

Goal: full multiplayer support. Co-design has been a constraint since Volume 2; this volume
makes it real.

### Chunk 21.1 — Netcode Foundation
- Install `com.unity.netcode.gameobjects` package.
- `Assets/Scripts/Net/NetManager.cs` — NetworkManager wrapper. Host/join, transport setup (default Unity Transport, optional Relay for NAT).
- `Assets/Scripts/Net/PlayerSpawner.cs` — Spawns player prefab per connected client.

### Chunk 21.2 — World Sync
- ChunkManager broadcasts terrain deformations and placed blocks.
- StructurePlacer + RoadNetwork run server-side only; clients receive results.
- WorldRandom remains server-only.

### Chunk 21.3 — Player Sync
- PlayerController + camera replicated. ClientNetworkTransform for movement.
- Inventory/hotbar are owner-authoritative with server validation.

### Chunk 21.4 — Combat Sync
- Hits validated server-side. Damage events broadcast.

### Chunk 21.5 — Automation Sync
- Machines tick server-side; clients receive state snapshots.

### Chunk 21.6 — UI & Lobby
- Main menu: Host / Join (Relay code or IP).
- In-game player list, friend invite stub.

**Acceptance:** Two players in the same world, see each other, share inventory storage, fight same enemies.

---

## VOLUME 22 — POLISH

Goal: final pass. Audio, VFX, balance, juice.

### Chunk 22.1 — Audio Pass
- Per-biome ambient soundscapes (synthesised or sourced).
- Per-action SFX (place block, mine ore, eat food, fire weapon).
- NPC voice grunts.
- Music: 5 biome themes + 4 boss themes + 1 menu theme.

### Chunk 22.2 — VFX Pass
- Weather particles per biome.
- Hit effects, muzzle flashes (per gun), magic effects (per powered weapon).
- Death/explosion VFX.

### Chunk 22.3 — Balance Pass
- Iterate on weapon damage, machine recipe times, generator wattages.
- Run the `CraftingBootstrapTests` after balance changes to ensure no item is locked out.

### Chunk 22.4 — Festival Polish
- Festival visual: lanterns, fireworks, decorations.
- Festival merchant flavor dialog.

---

## PROGRESS TRACKER

Status codes:
- `[ ]` = Not started
- `[P]` = Partial / In progress
- `[D]` = Done, awaiting review
- `[R]` = Review in progress
- `[✓]` = Done and reviewed
- `[X]` = Blocked (note reason)
- `[L]` = Legacy code preserved, asset/data layer superseded

### Volume 0 — Project Bootstrap
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 0.0 | Project setup, packages, folder structure, MC submodule | [✓] | |

### Volume 1 — Core World & Player
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 1.1 | Density Function & Noise Utilities | [✓] | |
| 1.2 | Chunk Data Structure & Chunk Manager | [✓] | |
| 1.3 | Marching Cubes Adapter & Mesh Generation | [✓] | |
| 1.4 | Chunk Loading/Unloading Around Player | [✓] | |
| 1.5 | Biome System (basic, will be reworked in V12) | [✓] | Foundation only — V12 expands to 5 biomes per HTML |
| 1.6 | Triplanar Terrain Shader & Biome Materials | [✓] | |
| 1.7 | First Person Player Controller | [✓] | |
| 1.8 | Terrain Deformation | [✓] | |
| 1.X | GPU Density Compute Shader | [✓] | |

### Volume 2 — Data Pipeline (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 2.1 | JSON Loader & Schema Types | [✓] | Newtonsoft.Json installed (3.2.1); 9/9 EditMode tests pass; reviewed 2026-05-26 |
| 2.2 | Item Definition v2 & Generator | [✓] | Replaces old ItemDefinition; 1031 item SOs + ItemDatabase regenerated |
| 2.3 | Recipe Definition v2 & Generator | [✓] | 1555 RecipeDefinition assets + RecipeRegistry generated; 5/5 new tests pass; reviewed 2026-05-26 |
| 2.4 | Machine/Fauna/Enemy/NPC Definitions & Generators | [✓] | 43 machine + 18 fauna + 43 enemy + 9 NPC SOs + 6 registries; 50 new EditMode tests pass (268/268 total); reviewed 2026-05-26 |
| 2.5 | One-Click Regenerate menus | [✓] | RegenerateAllMenu + JsonReextractMenu; 270/270 EditMode pass (+2 new); end-to-end menu run logs 1031/1555/43/18/43/9; reviewed 2026-05-26 |

### Volume 3 — Visual Asset Pipeline (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 3.1 | Material Palette | [✓] | 35 materials generated (15 category + 20 build); 8 new EditMode tests pass (278/278); reviewed 2026-05-26 |
| 3.2 | Procedural Primitive Mesh Library | [✓] | ProBuilder 6.0.5 installed; 14 mesh assets generated; 3/3 new EditMode tests pass (281/281); reviewed 2026-05-26 |
| 3.3 | Item Visual Recipe & Composer | [ ] | |
| 3.4 | Icon Renderer | [ ] | Long-running editor task |
| 3.5 | Fauna/Enemy/NPC Visual Recipes | [ ] | |
| 3.6 | One-Click Generate Visuals | [ ] | |

### Volume 4 — UI Foundations (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 4.1 | UI Style Kit | [ ] | JetBrains Mono font asset required |
| 4.2 | HUD Layout (Hotbar + Health + Stamina + Crosshair) | [ ] | |
| 4.3 | Inventory Panel | [ ] | Replaces existing layout |
| 4.4 | Machine UI Frame | [ ] | |
| 4.5 | Tooltip & Dialog | [ ] | Extend existing TooltipUI |
| 4.6 | Remove In-World UI | [ ] | Audit + delete all WorldSpace canvases |

### Volume 5 — Legacy Asset Cleanup (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 5.1 | Asset Scrap Manifest + Wiper | [ ] | Must run AFTER Volume 2 |
| 5.2 | Scene Reference Fixup | [ ] | |
| 5.3 | Code Cleanup | [ ] | |

### Volume 6 — Inventory & Crafting v2 (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 6.1 | Crafting Match Engine v2 | [ ] | Machine-scoped, bag-of-items matching |
| 6.2 | Personal Crafting Grid | [ ] | |
| 6.3 | Machine Crafting Stations | [ ] | Drives MachineUI |
| 6.4 | Bootstrap Path Validator test | [ ] | |

### Volume 7 — Building & Grid System (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 7.1 | Block Placement & Virtual Grid | [ ] | |
| 7.2 | Block Forms & Variants | [ ] | |
| 7.3 | Terrain Leveling Tool | [ ] | |
| 7.4 | Blueprint Capture & Place | [ ] | |
| 7.5 | Block Breaking | [ ] | |

### Volume 8 — Power & Wiring (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 8.1 | Power Network Graph | [ ] | |
| 8.2 | Generators (54 types) | [ ] | Heavy chunk — may need sub-chunks 8.2a/8.2b |
| 8.3 | Storage & Distribution | [ ] | |
| 8.4 | Worn Battery Pack | [ ] | |

### Volume 9 — Machines & Automation (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 9.1 | MachineRuntime Base | [ ] | |
| 9.2 | T1–T3 Machines (no automation) | [ ] | |
| 9.3 | T4–T5 Machines (powered) | [ ] | |
| 9.4 | T6–T7 Machines (advanced + auto-variants) | [ ] | |
| 9.5 | Transport (conveyors, tubes, pipes, couriers) | [ ] | |
| 9.6 | Sorting & Storage | [ ] | |

### Volume 10 — Combat Refactor
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| (old) 4.1-4.5 — Guns code | [L] | Code preserved; asset SOs regenerated by 10.1 |
| (old) 5.1-5.4 — Melee code | [L] | Same |
| (old) 6.1-6.3 — Projectiles + Enemy AI | [L] | Same |
| 10.1 | Weapon SO Generator | [ ] | |
| 10.2 | Bow & Crossbow | [ ] | |
| 10.3 | Thrown & Spear | [ ] | |
| 10.4 | Powered Weapons | [ ] | Depends on Volume 8.4 |
| 10.5 | Traps & Turrets | [ ] | |
| 10.6 | Armor & Damage Resistance | [ ] | |

### Volume 11 — Tools, Gadgets, Wearables (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 11.1 | Backpack System | [ ] | |
| 11.2 | Movement Gadgets | [ ] | |
| 11.3 | Utility Gadgets | [ ] | |
| 11.4 | Wearable Armor Effects | [ ] | |
| 11.5 | The Index v2 | [P] | Intro/hotbar/grapple exist per memory; restyle to V4 |

### Volume 12 — Biome Rework (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 12.1 | Biome Definitions (5 biomes) | [ ] | Replaces V1.5 biomes |
| 12.2 | Realistic Grass (GPU-instanced) | [ ] | |
| 12.3 | Trees & Large Flora | [ ] | |
| 12.4 | Weather, Temperature, Time of Day | [ ] | |
| 12.5 | Per-Biome Ore Distribution | [ ] | Updates OreRegistry |

### Volume 13 — Structure & Road Placement (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 13.1 | Structure Placement Pass | [ ] | |
| 13.2 | Blueprint Authoring (designer task) | [ ] | 45 blueprints (5×5 Kin ruins + 15 Vord strongholds + 25 misc) |
| 13.3 | Road Network | [ ] | |
| 13.4 | Echo Strongholds (procedural endgame) | [ ] | |

### Volume 14 — Fauna System (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 14.1 | Fauna AI Base | [ ] | |
| 14.2 | Fauna Per Species (18) | [ ] | |
| 14.3 | Taming & Pets | [ ] | |

### Volume 15 — Enemies & Bosses (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 15.1 | Fodder Enemy AI (11 types) | [ ] | |
| 15.2 | Boss Framework | [ ] | |
| 15.3 | Brood Family (6 bosses) | [ ] | |
| 15.4 | Warden Family (6 bosses) | [ ] | |
| 15.5 | Hunter Family (6 bosses) | [ ] | |
| 15.6 | Channeler Family (6 bosses) | [ ] | |
| 15.7 | Aberrant Family (6 bosses) | [ ] | |
| 15.8 | Finale Bosses (2) | [ ] | |

### Volume 16 — NPCs, Spirit Gateway, Festivals (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 16.1 | NPC Base & Dialog | [ ] | |
| 16.2 | 6 Named Kin | [ ] | |
| 16.3 | Traders & Caravans | [ ] | |
| 16.4 | Spirit Gateway | [ ] | |
| 16.5 | Festivals | [ ] | |

### Volume 17 — Atlas, Index, Quest Framework (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 17.1 | Atlas | [ ] | |
| 17.2 | Index v2 (already partial) | [P] | Restyle existing |
| 17.3 | Recall Box | [ ] | |
| 17.4 | Quest & Bounty Framework | [ ] | |

### Volume 18 — Story Acts (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 18.1 | Act 1: Awakening | [ ] | |
| 18.2 | Act 2: Reclamation | [ ] | |
| 18.3 | Act 3: Heart Fragments | [ ] | |
| 18.4 | Act 4: The Source | [ ] | |

### Volume 19 — Vehicles (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 19.1 | Vehicle Assembly System | [ ] | |
| 19.2 | Ground Vehicles | [ ] | |
| 19.3 | Aerial Vehicles | [ ] | |
| 19.4 | Vehicle Physics & Damage | [ ] | |

### Volume 20 — Endings & NG+ (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 20.1 | Choice Chamber | [ ] | |
| 20.2 | Endings (4) | [ ] | |
| 20.3 | NG+ Embrace Mode | [ ] | |

### Volume 21 — Coop Networking (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 21.1 | Netcode Foundation | [ ] | |
| 21.2 | World Sync | [ ] | |
| 21.3 | Player Sync | [ ] | |
| 21.4 | Combat Sync | [ ] | |
| 21.5 | Automation Sync | [ ] | |
| 21.6 | UI & Lobby | [ ] | |

### Volume 22 — Polish (NEW)
| Chunk | Description | Status | Notes |
|-------|-------------|--------|-------|
| 22.1 | Audio Pass | [ ] | |
| 22.2 | VFX Pass | [ ] | |
| 22.3 | Balance Pass | [ ] | |
| 22.4 | Festival Polish | [ ] | |

---

## RECOMMENDED EXECUTION ORDER

Strict dependency order — do not skip ahead:

1. **Volumes 2 → 3 → 4 → 5** (data pipeline → visuals → UI → cleanup)
2. **Volume 6** (crafting v2 — depends on data + UI)
3. **Volumes 7 → 8 → 9** (building → power → machines — depend on data + crafting)
4. **Volumes 10 → 11** (combat refactor → gadgets)
5. **Volumes 12 → 13** (biome rework → structures)
6. **Volumes 14 → 15 → 16** (fauna → enemies → NPCs)
7. **Volume 17** (Atlas/Index/quests)
8. **Volume 18** (story acts — depends on all prior gameplay systems)
9. **Volume 19** (vehicles — independent, can run earlier in parallel if needed)
10. **Volume 20** (endings — depends on 18)
11. **Volume 21** (coop — touches every system; design constraints applied from day one)
12. **Volume 22** (polish — last)

Parallelisation opportunities (independent agents can work concurrently):
- Volume 3 (visuals) parallel with Volume 4 (UI) parallel with Volume 5 (cleanup).
- Volume 8 (power) parallel with Volume 10 (combat) — different systems, no dependency.
- Volume 14 (fauna) parallel with Volume 15 (enemies) parallel with Volume 16 (NPCs).
- Volume 19 (vehicles) anytime after Volume 9.

---

## DESIGN REFERENCE

### Coop Design Constraints (full version)
See "Coop Design Constraints" near the top of this file. The single most important rule:
**every gameplay write that affects more than one player MUST go through the server-authoritative
path.** When in doubt: make it a `ServerRpc` and let the host execute.

### Gun Feel Targets (preserved from old plan)
- Time from click to hit: 0ms (hitscan, instant)
- Recoil pattern length: 15-30 shots before it loops
- Recoil recovery rate: ~3× the application rate
- First shot accuracy: 100% standing still / crouched / not recently moving
- ADS transition: 150-200ms
- Weapon switch: 300-500ms
- Reload: 1.5-3s depending on weapon
- TTK targets: 400-800ms rifles, 200ms shotgun close, 0ms sniper headshot

### Melee Timing Targets (preserved)
- Windup: 300-500ms (fast weapons low end, slow high end)
- Release (active hit window): 200-400ms
- Recovery: 300-500ms
- Parry window: 200ms
- Riposte speed bonus: 30% faster windup
- Stagger duration: 500ms
- Feint window: during windup only, 10 stamina cost
- Chamber window: first 100ms of opponent's release phase

### Enemy AI Architecture (preserved from chunk 6.3)
**No NavMesh** — marching-cubes terrain deforms at runtime. Use three stacked layers instead:
1. **Grounded steering** — raycast down to find surface, project velocity onto slope normal.
2. **5-ray obstacle fan** — forward fan of short raycasts deflects toward clearest opening.
3. **EnvironmentSensor lidar** — 26 Fibonacci-hemisphere rays via `RaycastCommand.ScheduleBatch`,
   staggered by `instanceID % scanInterval`. Results cached for O(1) main-thread queries.

Cover-seeking: `EnvironmentSensor.GetBestCoverPoint(dangerPos)` from face normals. Validated by
`Physics.Linecast` (blocked = good) + `Physics.CheckSphere` (clearance).

PersonalityProfile: three static instances — `Optimized` (decisive, group flanker), `Directed`
(hesitant, panic-suppresses), `WildCreature` (fastest, no cover, terrain-following).

### Marching Cubes Integration Notes
**Repo: https://github.com/gtaharaedmonds/marching-cubes-gpu**

- **Keep:** compute shader pipeline, lookup tables, GPU dispatch, mesh buffer readback.
- **Replace:** their terrain noise with our DensityFunction (CPU + GPU paths exist).
- **Wrap:** `MarchingCubesAdapter.cs` is the only file that touches the external code.
- **Adapt grid size:** our chunks are 32³ → 33³ density (one extra row for edge stitching).
- **Edge stitching:** adapter samples one extra row from adjacent chunks via ChunkManager.

If it stops compiling on a Unity upgrade: fix C# API deprecations only. Never rewrite the
algorithm. Fall back to Sebastian Lague's, Scrawk's, or keijiro's implementations.

---

## AGENT NOTES LOG

Append below as agents complete work.

```
[TEMPLATE]
Date: YYYY-MM-DD
Agent: [Implementation/Review] Volume X Chunk Y
Notes:
-
```

Date: 2026-05-26
Agent: Implementation Volume 2 Chunk 2.3 (Recipe Definition v2 & Generator)
Notes:
- Created Assets/Scripts/Crafting/RecipeDefinition.cs (new SO: outputItemId/outputQty/ingredients[]/viaMachineId/notes/isBootstrap/isSynergy + Ingredient struct).
- Created Assets/Scripts/Crafting/RecipeRegistry.cs (singleton SO with allRecipes + ByMachine/ByOutput/Bootstrap APIs; null and "" normalised to same personal-grid bucket).
- Created Assets/Editor/Data/RecipeSoGenerator.cs (Voidborne/Generate/Recipes menu; idempotent; pre-cleans orphan .asset files; writes Assets/Resources/RecipeRegistry.asset).
- Created Assets/Tests/EditMode/RecipeRegistryTests.cs (5 tests: registry loads, workbench bootstrap, count floor, output existence HARD-FAIL, ingredient existence SOFT-WARN).
- Moved Assets/Scripts/Crafting/CraftingRecipe.cs to Assets/Scripts/_Legacy/Crafting/ (kept .meta GUID intact; removed [CreateAssetMenu]; added legacy header). DEVIATION: did NOT rename the class to LegacyCraftingRecipe because ~10 consumer files (CraftingManager/Grid/Station, PersonalCraftingGrid, CraftingUI, SchematicCard/Fragment, Assembler, WorkbenchSetup, CraftingRecipeCreator, ThreeByThreeRecipeCreator) bind to the type name; renaming would cascade. The new V2 type is `RecipeDefinition`, a different identifier, so there is no collision. Volume 6 will retire the legacy consumers.
- Generated 1555 recipe assets (not the spec's "≥2000 target 2500+" — items.json carries 1555 recipes across 969 items; verified by counting). Sanity floor in the test lowered to 1500 with a comment to raise when the HTML expands.
- Bootstrap detection: spec said "isBootstrap = notes contains 'BOOTSTRAP'". Actual JSON has BOOTSTRAP in 5 recipe `notes` (furnace/etcher/servo crude paths) but the canonical workbench has it in the ITEM `role` only. Generator now flags isBootstrap when (a) notes contains BOOTSTRAP OR (b) recipe via==null AND parent item.role contains BOOTSTRAP. Captures the workbench correctly.
- Ingredient resolution: every one of 4421 ingredient references resolves to an item in ItemDatabase. Soft-warn path in test 5 not currently triggered.
- All 218 EditMode tests pass (5 new + 213 pre-existing). Zero compile errors.
- For V2.4: Machine/Fauna/Enemy/NPC generators will need similar marker-parsing strategies (T1-T7 tier from item.role; archetype from npc.section). The role-parsing helper logic in RecipeSoGenerator (ContainsMarker) can be promoted to ParsingHelpers.cs.
```

```
Date: 2026-05-26
Agent: Review Volume 2 Chunk 2.3 (Recipe Definition v2 & Generator)
Notes:
- Verdict: PASS. Flipped V2.3 tracker [D] -> [✓].
- Reviewed RecipeDefinition.cs, RecipeRegistry.cs, RecipeSoGenerator.cs, RecipeRegistryTests.cs,
  _Legacy/Crafting/CraftingRecipe.cs, Resources/RecipeRegistry.asset, and spot-checked
  workbench__r0 / furnace__r0 / furnace__r1 / campfire__r0 / spear__r0.
- Spec compliance: all required fields present (outputItemId, outputQty=1, Ingredient[],
  viaMachineId, notes, isBootstrap, isSynergy). Ingredient is a [Serializable] struct.
  [CreateAssetMenu] present on both SOs.
- Spot-check results:
  - workbench__r0.asset: outputItemId=workbench, viaMachineId=null (empty in YAML),
    isBootstrap=1, ingredients = wood4/stone2/plant_fiber2. Matches items.json.
  - furnace__r0/r1: viaMachineId=workbench, isBootstrap=0 (correct — these are post-
    workbench builds, not BOOTSTRAP themselves).
  - campfire__r0: viaMachineId=workbench, notes empty, both flags 0.
  - spear__r0: weapon recipe with viaMachineId=workbench, correct shape.
- Bootstrap detection: 7 assets flagged isBootstrap=1 — 5 explicit BOOTSTRAP-in-notes,
  1 lowercase "Bootstrap:" in etcher__r0 notes (case-insensitive contains works), and
  1 inferred from item.role (workbench__r0 with via=null). Logic is sound.
- Synergy detection: 5+ assets flagged via case-insensitive notes contains "SYNERGY".
- Registry: ByMachine(null) and ByMachine("") both resolve to the personal-grid bucket
  via NormalizeMachineKey. Public Reindex() exposed for the generator. Indexes built in
  OnEnable + count-tracked EnsureIndexed.
- Generator: idempotent — orphan cleanup runs BEFORE StartAssetEditing (safe, no .meta
  race with newly-created assets in the same pass). StartAssetEditing/StopAssetEditing
  wrap the create/update loop. Registry write happens after the editing block.
- Tests: 218/218 EditMode pass (5 new + 213 pre-existing). Unity console 0 errors,
  0 warnings after refresh.
- Legacy preservation: Assets/Scripts/_Legacy/Crafting/CraftingRecipe.cs exists with
  LEGACY header, [CreateAssetMenu] removed, .meta GUID preserved. Class name kept as
  `CraftingRecipe` (deviation from V2.2's rename approach) — verified that 9 live
  consumers (Assembler, SchematicCard, SchematicFragment, CraftingUI, CraftingStation,
  PersonalCraftingGrid, CraftingManager, CraftingGrid, plus the legacy SO itself) all
  still bind by name and compile clean. No collision with the new RecipeDefinition.
- Spec acceptance phrasing note: spec says `RecipeRegistry.ByMachine("workbench")`
  returns the workbench bootstrap recipe — that's a spec misstatement (the workbench's
  own recipe has via=null, so it sits in the personal-grid bucket). Agent correctly
  implemented to match the JSON. The test asserts ByMachine(null) for workbench, which
  is right.
- Acceptance count: spec said "2500+", actual is 1555. Agent documented the discrepancy
  (current items.json carries 1555 recipes across 969 items). Test floor at 1500 is
  appropriately defensive. No action needed; raise floor when HTML expands.
- No fixes applied — implementation is clean.
- For V2.4: ParsingHelpers.cs should absorb ContainsMarker; tier parsing from item.role
  ("T1 — ...", "T3 — ...") is the next obvious extraction. Watch for the same null-vs-
  empty-string normalisation pattern when bucketing machines by tier.
```

```
Date: 2026-05-26
Agent: Master prompt rewrite (this file)
Notes:
- Rewrote master prompt v2 → v3 to align with voidborne-flowchart-v3.html.
- HTML data extracted to Design Documents/GameDesign/data/ via extract_html_data.py.
- 1031 items, 70 NPCs (30 bosses + 2 finale + 11 fodder + 18 wildlife + 6 named Kin + 3 traders),
  43 machines, 54 power generators, 130 build blocks, 45 deco blocks captured.
- Volumes 0-1 preserved (code + assets valid).
- Volumes 2-11 of v2 plan: code preserved (marked [L] Legacy) where reusable; asset layer scrapped
  in Volume 5 and regenerated from JSON in Volume 2.
- 22 volumes total in the new plan; ~110 chunks; ~6-12 months of agent work at one chunk/day.
- Coop networking is a design constraint from day one (Volume 21 makes it real).
- All visual assets (1000+ item prefabs + icons, 70 creature prefabs) generated procedurally
  in Volume 3 using Unity primitives + ProBuilder, rendered to icons via off-screen camera.
```

```
Date: 2026-05-26
Agent: Implementation Volume 2 Chunk 2.1 — JSON Loader & Schema Types
Notes:
- Added com.unity.nuget.newtonsoft-json 3.2.1 to Packages/manifest.json.
- Created Assets/Scripts/Data/Schema/{ItemJson,RecipeJson,NpcJson,CategoriesJson}.cs
  (POCOs; namespace Voidborne.Data.Schema; no Unity references; coop-safe).
  RecipeJson.cs also defines IngredientJson (id, qty).
- Created Assets/Scripts/Data/GameDesignJsonLoader.cs (namespace Voidborne.Data,
  wrapped in #if UNITY_EDITOR). Uses Newtonsoft JsonConvert. Provides editor-session
  cache (ClearCache to invalidate). Methods: LoadItems, LoadNpcs, LoadCategories,
  LoadCategoriesWrapped, LoadBuildMaterials, LoadDecoBlocks, LoadFormTemplates,
  LoadSynergyAlts, LoadSummary. Also defines BuildMaterialJson with [JsonProperty("base")]
  mapping for the reserved-keyword field.
- Tests placed at Assets/Tests/EditMode/JsonLoaderTests.cs (existing
  EditModeTests.asmdef already references Voidborne + nunit.framework — no asmdef
  changes needed).
- Verified via Unity MCP: refresh + compile produced 0 errors / 0 warnings.
  Ran EditMode tests via run_tests — 9/9 passed (132 ms total). Confirmed
  items.json count is well over 1000, workbench is a machine with a bootstrap
  recipe (via=null), npcs.json count is exactly 70, categories.json contains
  food/weapon/build/deco, build_materials.json has stone+wood entries.
- Deviations: added 6 extra tests beyond the spec's three for tighter shape
  coverage (build flag, bootstrap via=null, NPC shape sanity, categories
  wrapper, build_materials); they all pass. No SOs generated yet — that is
  Chunks 2.2-2.4.
- No #if UNITY_EDITOR wrap on schema POCOs (pure data, harmless in runtime
  assembly, future SO generators in editor assemblies still reference them).
```

```
Date: 2026-05-26
Agent: Implementation Volume 2 Chunk 2.2 — Item Definition v2 & Generator
Notes:
- Moved legacy ItemDefinition + ItemDatabase to Assets/Scripts/_Legacy/Inventory/
  and renamed classes to LegacyItemDefinition / LegacyItemDatabase (keeps file
  GUIDs intact; legacy assets are now de-bound from script, scheduled for wipe
  in Volume 5). LEGACY headers added per spec.
- Created Assets/Scripts/Inventory/ItemDefinition.cs (new v2):
  - ItemKind {Source,Machine,Component,Product}, ItemSource {None,Fauna,Flora,
    Ore,Soil,Exotic} enums defined here.
  - Fields per spec: itemId, displayName, description, kind, source, categories,
    isBuildBlock, isDeco, buildColor, maxStackSize, icon, modelPrefab,
    placedPrefab. Read-only aliases `id`, `worldDropPrefab`, helper `HasCategory`.
  - DEVIATION: kept the legacy ItemType enum AND added itemType + weight fields
    on the new class. 13 existing scripts (TooltipUI, ItemIconGenerator, WorldItem,
    ChestSetup, multiple setup utilities) read/write ItemType / weight — keeping
    them avoids touching 13 files outside this chunk's scope. The generator
    derives itemType from kind+categories. weight defaults to 1f (unused by new
    code, present only for backward compat with TooltipUI / VehicleBase).
  - DEVIATION: kept serialized field names `itemId` and `modelPrefab` rather
    than renaming to `id`/`worldDropPrefab`. 22 files write to itemId, 3 files
    write to modelPrefab — read-only aliases on the spec names are exposed.
  - CreateAssetMenu set to "Voidborne/Items/Item Definition (v2)" per spec.
- Created Assets/Scripts/Inventory/ItemDatabase.cs (new v2):
  - Singleton, loaded via Resources at "ItemDatabase".
  - Preserves legacy public field name `items` (List<ItemDefinition>) since
    AutomationSetup81/82/85, BuildingItemSetup, RegisterVehiclePartsEditor,
    ChestSetup all write `db.items.Add(...)`. The spec's `allItems` is exposed
    as the read-only `AllItems` (IReadOnlyList) view.
  - API: Instance, GetItem, AllItems, ByCategory, ByKind, GetOrLoad, Reindex.
  - BuildLookup tracks `_lookupBuiltForCount` and rebuilds when items.Count
    changes — necessary because OnEnable fires before the generator populates
    items.
- Created Assets/Editor/Data/ItemSoGenerator.cs (editor-only, #if UNITY_EDITOR):
  - MenuItem "Voidborne/Generate/Items".
  - Loads items.json + categories.json via GameDesignJsonLoader, builds reverse
    item->categories index, iterates id-sorted item keys.
  - Per item: LoadAssetAtPath or CreateInstance + CreateAsset at
    Assets/ScriptableObjects/Generated/Items/{id}.asset, populates all v2 fields,
    derives ItemType for legacy compat, applies stack-size policy
    (Machine/weapon/vehicle=1, Source=16, else=64).
  - Wrapped in AssetDatabase.StartAssetEditing/StopAssetEditing.
  - End of pass: rebuilds Assets/Resources/ItemDatabase.asset with id-sorted
    item list; deletes the legacy asset first if its script binding is broken.
  - DEVIATION: created new ItemDatabase.asset GUID (legacy GUID was tied to
    LegacyItemDatabase script which now has no [CreateAssetMenu]).
- Created Assets/Tests/EditMode/ItemDatabaseTests.cs with 6 tests covering
  Resources load, workbench-is-machine, count>=1000, stone_cube build flag,
  graze fauna source classification, machine stack size.
- Verified via Unity MCP (GameNine@2a29bfb34d33a2b2):
  - 0 compile errors, 0 new warnings after refresh.
  - Voidborne/Generate/Items menu executed cleanly:
    "[ItemSoGenerator] Generated/updated 1031 items (1031 newly created, 0 updated)."
  - Full EditMode test suite 213/213 PASS (was 207 — 6 new tests added,
    0 regressions). Total duration 0.26s.
  - 1031 .asset files present under Assets/ScriptableObjects/Generated/Items/.
  - Assets/Resources/ItemDatabase.asset rebuilt with 1031 references.
- No emojis. No runtime state on SOs (coop-safe).
```

```
Date: 2026-05-26
Agent: Review Volume 2 Chunk 2.1 — JSON Loader & Schema Types
Notes:
- Checked schema POCOs against real JSON shapes:
  - ItemJson covers kind/src/name/role/recipes, plus [JsonProperty] mappings
    for _build/_buildColor/_deco. Verified items.json starts with raw
    source-fauna entries (no recipes), workbench at line 362 is kind="machine"
    with role "T1 — 3×3 crafting (BOOTSTRAP)" and a single via=null recipe,
    mortar/drying_rack/cleaver_block all have multiple recipes, and there are
    items with "_build": true / "_buildColor": "build" (verified at line 30178+).
  - NpcJson covers cat/section/name/desc/appearance/behaviors/abilities/drops;
    matches the npcs.json shape (flat array; first entry = Fungal Brood Mother).
  - CategoriesJson exposes Dict<string,List<string>> map plus a safe Get()
    that returns Array.Empty<string> for unknown keys.
  - BuildMaterialJson uses [JsonProperty("base")] to dodge the C# reserved word.
- Loader uses Newtonsoft JsonConvert (not Unity JsonUtility), constructs paths
  via Application.dataPath + ".." + "Design Documents/GameDesign/data", is
  fully wrapped in #if UNITY_EDITOR, and the session cache is invalidatable
  via ClearCache (tests SetUp clears it before every run).
- Voidborne.asmdef has autoReferenced=true and no overrideReferences, so the
  loader picks up the Newtonsoft precompiled DLL without an explicit reference.
  EditModeTests.asmdef references Voidborne; tests only touch the public API.
- Coop constraints honored: POCOs are pure data, no Unity refs, no Time.deltaTime,
  no Random. No emojis in code/comments.
- Verified via Unity MCP (instance GameNine@2a29bfb3, Unity 6000.3.9f1):
  forced refresh + compile → 0 errors / 0 warnings in console.
  run_tests EditMode → 207/207 pass (0.31s), including all 9 JsonLoaderTests.
  The three spec-required assertions are all green:
    LoadItems_ReturnsAtLeast1000Entries, LoadItems_WorkbenchIsMachine,
    LoadNpcs_ReturnsExactly70Entries.
- Minor deviation noted (not a defect): LoadCategories returns the raw
  Dictionary rather than CategoriesJson; LoadCategoriesWrapped provides the
  typed wrapper. Both work; documented in the impl agent's own notes.
- No fixes applied — implementation is clean. Tracker advanced to [✓].
```

```
Date: 2026-05-26
Agent: Review Volume 2 Chunk 2.2 — Item Definition v2 & Generator
Notes:
- Spec compliance: ItemKind/ItemSource enums defined on ItemDefinition.cs.
  All spec field names present (kind, source, categories, isBuildBlock,
  isDeco, buildColor). Pragmatic deviation accepted: serialized fields kept
  legacy names itemId/modelPrefab; spec aliases `id` and `worldDropPrefab`
  exposed as read-only getters returning the correct values. ItemType +
  weight retained for back-compat with 13 consumer scripts (TooltipUI etc.).
- Generator (Assets/Editor/Data/ItemSoGenerator.cs): editor-only, wrapped
  in #if UNITY_EDITOR, namespaced Voidborne.Editor.Data. Uses
  AssetDatabase.StartAssetEditing/StopAssetEditing for batched I/O. Skips
  icon/modelPrefab/placedPrefab (Volume 3 territory). Idempotent —
  LoadAssetAtPath then CreateInstance fallback. Stable id-sorted ordering.
  Unknown kind/src values warn-and-default rather than crash. LoadOrCreateRegistry
  deletes a stale legacy-bound ItemDatabase.asset before CreateAsset, which
  is sound.
- Spot-checked generated assets:
  - workbench.asset: kind=Machine(1), source=None(0), maxStackSize=1,
    description contains the "T" prefix, itemType=Machine(6). PASS.
  - graze.asset: kind=Source(0), source=Fauna(1), maxStackSize=16. PASS.
  - iron_ore.asset: kind=Source(0), source=Ore(3), maxStackSize=16. PASS.
  - wood.asset: kind=Source(0), source=Flora(2), maxStackSize=16. PASS.
  - stone_cube.asset: kind=Product(3), isBuildBlock=true, buildColor="build",
    categories=[build]. PASS.
  - holo_display.asset: kind=Product, isBuildBlock=true, isDeco=true,
    buildColor="build", categories=[build, deco]. PASS.
  - sling.asset: categories=[weapon], maxStackSize=1 (weapon override). PASS.
- ItemDatabase.cs: singleton via Resources.Load("ItemDatabase") with
  GetOrLoad fallback. OnEnable builds id->ItemDefinition dict. GetItem,
  AllItems, ByCategory, ByKind, Reindex all present. Reindex is correctly
  required because OnEnable fires before the generator populates items
  during a single editor pass; the lazy-rebuild guard
  (_lookupBuiltForCount != items.Count) also catches that.
- Legacy preservation: Assets/Scripts/_Legacy/Inventory/ contains
  ItemDefinition.cs (LegacyItemDefinition) and ItemDatabase.cs
  (LegacyItemDatabase) with LEGACY headers; .meta files dated 2026-03-15
  (original GUIDs preserved); no [CreateAssetMenu] on legacy classes
  (avoids menu collision with the new v2 menu entry).
- Resources/ItemDatabase.asset: exactly 1031 references in the items
  list (verified by grep on the YAML).
- Compile/tests via Unity MCP (instance auto-selected):
  refresh_unity (compile=request, wait_for_ready) returned ready;
  read_console with error+warning filter returned 0 entries;
  run_tests EditMode -> 213/213 PASS in 0.33s (matches impl agent's
  reported count exactly). All 6 ItemDatabaseTests included.
- Codebase rules: no emojis in any new file; editor code under
  Assets/Editor/ and #if UNITY_EDITOR-wrapped; ItemDefinition is a
  stateless data SO (no runtime mutation surface, coop-safe).
- No fixes applied — implementation is clean. Tracker advanced to [✓].
```

```
Date: 2026-05-26
Agent: Implementation Volume 2 Chunk 2.4 — Machine/Fauna/Enemy/NPC Definitions & Generators
Notes:
- Created Assets/Editor/Data/ParsingHelpers.cs (ParseTier with token-boundary rules; ContainsMarker; SlugifyName; IsAggressive/IsPassive/IsTameable predicates). Consolidates the V2.3-era private ContainsMarker into a single shared helper.
- Created Assets/Editor/Data/Voidborne.Editor.Data.asmdef — NEW editor asmdef wrapping the four V2.4 generators + V2.2/V2.3 generators + ParsingHelpers so EditMode tests can reference ParsingHelpers directly. EditModeTests.asmdef now references Voidborne.Editor.Data. DEVIATION: spec did not call this out; needed because Assets/Editor/ scripts default to Assembly-CSharp-Editor which EditMode tests can't reference.
- Created 4 new SOs + 4 registries:
  - Assets/Scripts/Automation/MachineDefinition.cs + MachineRegistry.cs (Voidborne.Automation namespace).
  - Assets/Scripts/Fauna/FaunaDefinition.cs + FaunaRegistry.cs (new folder).
  - Assets/Scripts/Enemies/EnemyDefinitionV2.cs + EnemyRegistry.cs in NEW namespace Voidborne.Enemies.V2. DEVIATION: a legacy EnemyDefinition class already lives in Voidborne.Enemies (used by EnemySpawner/EnemyBrain/EnemyManager; ~50 consumer files). Spec was unaware of the collision. Chose namespace disambiguation over rename to avoid touching the live enemy system — Volume 15 will consolidate.
  - Assets/Scripts/NPCs/NpcDefinition.cs + NpcRegistry.cs (new folder).
- Created Assets/Scripts/Core/Registries.cs — static facade exposing Items / Recipes / Machines / Fauna / Enemies / Npcs (singleton lookup via each registry's own Instance/GetOrLoad). Voidborne.Core namespace.
- Created 4 generators in Assets/Editor/Data/: MachineSoGenerator (filter kind==machine), FaunaSoGenerator (cat=="wildlife"), EnemySoGenerator (cat in {boss,finale,fodder}), NpcSoGenerator (cat in {named,trader}). All idempotent, orphan-cleaning, ordered output, StartAssetEditing/StopAssetEditing wrapped, identical plumbing patterns to V2.3 RecipeSoGenerator.
- Heuristics: MachineSoGenerator implements all V2.4 spec rules (3x3 default grid; 1x1 specials; 5x5 assembler; 1x2 press; needsPower if tier>=4 OR "powered" in role OR starts with auto_/electric; powerDraw = 50*tier; isAutomatable if id starts with auto_). EnemySoGenerator parses tier from desc "Tier N" phrase first, then from section via ParseTier; defaults to 1 for unannotated bosses and 0 for fodder/finale.
- Tests added at Assets/Tests/EditMode/ParsingHelpersTests.cs (28 tests covering every public method) and RegistriesTests.cs (22 tests covering count, lookup, archetype/family resolution, tier parsing for canonical entries).
- Verified via Unity MCP: refresh+compile -> 0 errors, 0 new warnings. Ran each generator menu in order: ItemSoGenerator 1031, RecipeSoGenerator 1555, MachineSoGenerator 43, FaunaSoGenerator 18, EnemySoGenerator 43 (30 boss + 2 finale + 11 fodder), NpcSoGenerator 9 (6 named + 3 trader). All counts match spec acceptance criteria exactly.
- run_tests EditMode -> 268/268 PASS in 0.35s (was 218 — +50 V2.4 tests). One iteration: initial ParsingHelpers test had a bad expectation (assumed "Stalks" matched marker "stalker") — corrected to use "stalker" verbatim.
- Spot-checked workbench (tier=1, 3x3, Workbench cat, !needsPower), assembler (tier=7, 5x5, Assembler cat, 350W, needsPower), furnace (tier=3, 3x3, Furnace cat — !needsPower because role doesn't include "powered" marker; spec's "needsPower if tier>=4" leaves T3 furnace unpowered, which matches the in-fiction "primitive smelting" use case), vord_brood_queen (Brood, tier=3, boss), the_hollow_source (Finale, isFinale, isBoss), ash_pup (tameable=true via "bonds with player"), vesh_the_smith (Kin Survivors, !killable, isQuestGiver=true).
- For V2.5: RegenerateAll menu can simply chain Items -> Recipes -> Machines -> Fauna -> Enemies -> NPCs. For V14/V15/V16/V18: the dropItemIds / dropOrTradeItems fields are still free-form strings (the JSON drops are bullet text like "Spore Cluster x3-5"). A future chunk needs a JSON-side parser to extract real item IDs + quantities from these bullets — flagged this for V20 (Quest framework / loot resolution).
- The legacy Voidborne.Enemies.EnemyDefinition will need a name decision by V15 — either rename legacy to LegacyEnemyDefinition and promote V2's namespace, or keep the V2 namespace permanently. No immediate action.
```

```
Date: 2026-05-26
Agent: Review Volume 2 Chunk 2.4 — Machine/Fauna/Enemy/NPC Definitions & Generators
Verdict: PASS — flipped V2.4 [D] -> [✓].
Checks:
- File inventory: all 5 editor scripts (ParsingHelpers + 4 generators + asmdef), all 8 runtime files (4 SOs + 4 registries + Registries facade), 2 test files present and read.
- Asset counts on disk via ls/grep: Machines=43, Fauna=18, Enemies=43, NPCs=9 — exact spec match.
- Spec compliance: 4 SOs carry all spec-required fields; MachineCategory (8 values) + EnemyArchetype (8 values) enums declared per spec; 4 generator [MenuItem] paths under Voidborne/Generate/ correct; all 6 registries follow Resources.Load lazy singleton + OnEnable Reindex + EnsureIndexed pattern; Registries.Core facade exposes Items/Recipes/Machines/Fauna/Enemies/Npcs.
- Spot-check asset YAML reads: workbench.asset (tier=1, gridW/H=3, needsPower=0, category=0 Workbench), assembler.asset (tier=7, 5x5, needsPower=1, powerDrawWatts=350, category=5 Assembler), furnace.asset (tier=3, category=1 Furnace), ash_pup.asset (isTameable=1, isAggressive=0, isPassive=0), the_hollow_source.asset (family=6 Finale, isBoss=1, isFinale=1), vord_brood_queen.asset (tier=3, family=0 Brood, isBoss=1), wren.asset (isKillable=0, isQuestGiver=1, section="Kin Survivors").
- ParsingHelpers: ParseTier boundary rules verified by 8 unit tests including ART3 negative and "T2 then T5" picks-first; SlugifyName tested for spaces/apostrophes/parens/multi-space; IsAggressive/IsPassive/IsTameable markers cover behaviors, desc, and "(tame)" name suffix.
- Unity MCP: refresh + read_console -> 0 errors, 0 warnings; run_tests EditMode -> 268/268 PASS (0.33s) — no regression.
- Namespace deviation: Voidborne.Enemies.V2 cleanly isolates new EnemyDefinition from legacy Voidborne.Enemies.EnemyDefinition (still consumed by EnemySpawner/EnemyBrain). Already flagged in agent log + comment block at top of EnemyDefinitionV2.cs for V15 consolidation.
- Codebase rules: no emojis; all generators gated by #if UNITY_EDITOR and asmdef Editor-only; SOs are pure data containers (no runtime state).
Fixes:
- MachineSoGenerator.cs: removed dead-code local `bool isAuto = ...` (computed but never read; only `startsWithAuto` was used downstream). Re-ran 268/268 EditMode tests post-fix — all pass.
Concerns for V2.5:
- V2.5 RegenerateAllMenu must chain in order Items -> Recipes -> Machines -> Fauna -> Enemies -> NPCs and call GameDesignJsonLoader.ClearCache() between phases (each generator already does this internally — safe).
- Orphan warning step described in V2.5 spec should reuse the per-folder pattern already implemented in each V2.4 generator's DeleteOrphans (could refactor to a shared helper in ParsingHelpers or a new GeneratorPlumbing helper).
- dropItemIds / dropOrTradeItems remain free-form bullet strings (not real item IDs); V20 loot-resolution pass owns that work — already flagged in implementation log.
```

```
Date: 2026-05-26
Agent: Implementation Volume 2 Chunk 2.5 — One-Click Regenerate
Notes:
- All 6 existing generators (ItemSoGenerator, RecipeSoGenerator, MachineSoGenerator, FaunaSoGenerator, EnemySoGenerator, NpcSoGenerator) were ALREADY exposed as `public static class` with `public static void Generate()` per V2.4 implementation. Zero refactoring needed on existing generators — Option A's "public static API exists" path was already met. The [MenuItem] attributes sit directly on Generate() rather than on a wrapping OnMenu() method; that's fine because Unity invokes them as void Action delegates regardless. No edits to the 6 files.
- Created Assets/Editor/Data/RegenerateAllMenu.cs — `[MenuItem("Voidborne/Generate/⟳ Regenerate All Generated SOs")]` -> private wrapper -> public static Run(). Run() does: (1) log header, (2) WarnAboutOrphans pre-scan, (3) GameDesignJsonLoader.ClearCache (defensive — each generator also clears), (4) RunStage(...) ×6 in Items->Recipes->Machines->Fauna->Enemies->NPCs order, each followed by AssetDatabase.SaveAssets, (5) try/finally guarantees SaveAssets+Refresh at the end even on partial failure, (6) LogSummary reads counts via Registries facade.
- Orphan pre-scan is non-destructive (warning only). Computes the live-id set for each folder from JSON: Items=keys(items.json), Recipes=`<id>__r<idx>` where idx<recipes.Count, Machines=keys with kind=="machine", Fauna/Enemy/NPC=SlugifyName(npcs.json entries filtered by cat). Caps per-folder enumeration to 10 names in the log. Pre-scan failure is non-fatal — logged as Warning and the run continues.
- Created Assets/Editor/Data/JsonReextractMenu.cs — `[MenuItem("Voidborne/Generate/⟳ Re-extract from HTML")]`. Resolves project_root/Design Documents/GameDesign/scripts/extract_html_data.py, tries `py` then falls back to `python`, streams stdout/stderr to console via async readers (avoiding the classic stderr-pipe deadlock), refreshes AssetDatabase on success, EditorUtility.DisplayDialog on success and failure. If neither launcher works, error dialog points at https://www.python.org/.
- Created Assets/Tests/EditMode/RegenerateAllTests.cs with 2 tests: RegenerateAll_CallsGeneratorsInOrder (Assert.DoesNotThrow on Run + all 6 registries populated to spec floors), RegenerateAll_Idempotent (run twice, counts must match exactly). Tests target the public static Run() — not EditorApplication.ExecuteMenuItem (Unicode/dispatch unreliable in test mode). No automated test for JsonReextractMenu because it spawns a real Python subprocess — flagged for manual smoke test.
- DEVIATION: spec says "warn (don't delete) if any folder contains assets not corresponding to current JSON. Most generators already do orphan cleanup, but..." — actually the V2.4 generators (Machine/Fauna/Enemy/NPC) DO clean orphans, and V2.3 RecipeSoGenerator does, but V2.2 ItemSoGenerator explicitly does NOT (the comment in its file says "orphan cleanup is Volume 2.5's responsibility"). My implementation only WARNS; if an Items orphan exists, it will be reported in the log every regenerate but never auto-deleted. Leaving destructive item-orphan cleanup as a future toggle; the spec wording "warn (don't delete)" supports the non-destructive choice. No items-folder orphans were observed during verification (1031 assets exactly match 1031 JSON keys).
- Verified via Unity MCP: refresh+compile -> 0 errors, 0 warnings. Executed menu Voidborne/Generate/⟳ Regenerate All Generated SOs via execute_menu_item -> Unicode arrow passed through cleanly; log captured 1031/1555/43/18/43/9 (matches spec acceptance exactly). run_tests EditMode -> 270/270 PASS in 69s (was 268 — +2 V2.5 tests, no regression).
- Did not run JsonReextractMenu (Python subprocess; not required for V2.5 acceptance and a re-extract would touch the canonical JSON files).
- Nothing blocking Volume 3 (visual asset pipeline) — V3 consumes the same registries (Items/Machines/Fauna/Enemies/NPCs) and will gain ownership of icon + worldDropPrefab + placedPrefab fields that V2 intentionally leaves null.
```

```
Date: 2026-05-26
Agent: Review Volume 2 Chunk 2.5 + Volume 2 final verification
Verdict: PASS — flipped V2.5 [D] -> [✓]. Volume 2 is COMPLETE.

V2.5 review:
- RegenerateAllMenu.cs: [MenuItem("Voidborne/Generate/⟳ Regenerate All Generated SOs")] uses the Unicode U+27F3 arrow (not an emoji) verbatim. Pre-step WarnAboutOrphans is non-destructive and wrapped in try/catch so pre-scan failure is non-fatal. Generation order is Items -> Recipes -> Machines -> Fauna -> Enemies -> NPCs with SaveAssets between each stage. try/finally guarantees SaveAssets+Refresh on partial failure; failure stage is logged with stage name. LogSummary reads counts via Registries facade with SafeCount guard returning -1 on null. GameDesignJsonLoader.ClearCache called once up front (each generator also clears defensively).
- JsonReextractMenu.cs: [MenuItem("Voidborne/Generate/⟳ Re-extract from HTML")] resolves project_root + Design Documents/GameDesign/scripts/extract_html_data.py. Tries `py` launcher, falls back to `python`. Uses async OutputDataReceived/ErrorDataReceived with BeginOutputReadLine/BeginErrorReadLine BEFORE WaitForExit — avoids the classic stderr-pipe-stall deadlock. Stdout/stderr always echoed to console regardless of exit code (good for diagnosing extractor warnings). Non-zero exit -> error log + DisplayDialog. Success path: ClearCache + AssetDatabase.Refresh + success dialog pointing user at "Regenerate All" next. Both Win32 launch failures fall through to a single error dialog with python.org URL. Not exercised in tests because real subprocess spawn (per impl notes) — acceptable.
- RegenerateAllTests.cs: 2 tests — CallsGeneratorsInOrder (DoesNotThrow + 6 registry GreaterOrEqual floors) and Idempotent (run twice, AreEqual counts). Correctly bypasses EditorApplication.ExecuteMenuItem for Unicode-path / deferred-dispatch reasons documented in the file.
- Asset folder counts on disk: Items=1031, Recipes=1555, Machines=43, Fauna=18, Enemies=43, NPCs=9 — exact spec match.
- Resources registries: ItemDatabase, RecipeRegistry, MachineRegistry, FaunaRegistry, EnemyRegistry, NpcRegistry — all 6 .asset files present.
- Unity MCP verification: refresh_unity force+compile -> ready_for_tools=true, read_console errors=0 warnings=0. execute_menu_item "Voidborne/Generate/⟳ Regenerate All Generated SOs" -> Unicode arrow routed cleanly through Unity; log captured the full 6-stage sequence plus "Regenerate All complete:" summary; counts in log match 1031/1555/43/18/43/9 exactly; 0 orphan warnings (folders fully aligned with JSON). run_tests EditMode -> 270/270 PASS in 66.4s (RegenerateAllTests included; no regressions).
- DEVIATION accepted: ItemSoGenerator does NOT delete orphan item assets — only warns via the pre-scan. Spec wording "warn (don't delete) if any folder contains assets not corresponding to current JSON" supports the non-destructive choice. No items-folder orphans exist today (1031 == 1031), so no log noise in practice.
- Codebase rules: no emojis (U+27F3 is intentional, not an emoji); both menu scripts wrapped in #if UNITY_EDITOR; both live under Assets/Editor/Data/; SOs remain stateless (coop-safe).
- No fixes applied — implementation is clean.

VOLUME 2 COMPLETE:
- All 5 chunks [✓]: 2.1 JSON Loader (9 tests), 2.2 Items (6 tests), 2.3 Recipes (5 tests), 2.4 Machine/Fauna/Enemy/NPC (50 tests), 2.5 Regenerate menus (2 tests).
- Final asset counts: 1031 Items, 1555 Recipes, 43 Machines, 18 Fauna, 43 Enemies, 9 NPCs (= 2699 generated SOs), plus 6 Resources registries.
- EditMode tests: 270/270 PASS (was 198 pre-V2, +72 new V2 tests across 2.1-2.5).
- Next: Volume 3 (Visual Asset Pipeline). V3 fields are already in place on every SO (ItemDefinition.icon Sprite, ItemDefinition.modelPrefab + .placedPrefab GameObjects, plus equivalents on Fauna/Enemy/NPC). All folders exist; no V2 design choice blocks V3.

V3 concerns / heads-up:
- ItemDefinition exposes `worldDropPrefab` only as a READ-ONLY alias for `modelPrefab` (=> get-only property). V3 spec says "Populate `icon`, `worldDropPrefab`, `placedPrefab`" — editor scripts must write to `modelPrefab` (the underlying serialized field), not `worldDropPrefab`. This is documented in ItemDefinition.cs but worth re-stating for V3 implementers.
- The V2.5 orphan pre-scan understands Items/Recipes/Machines/Fauna/Enemies/NPCs folders only. When V3.6 ("One-Click Generate Visuals") starts populating Assets/Materials/Generated/, Assets/Models/Generated/Primitives/, and any V3 icon/prefab output folders, those will not be covered by V2.5's pre-scan. V3.6 should grow its own orphan logic (or extend RegenerateAllMenu.WarnAboutOrphans).
- Volume 3 will create per-item meshes and run an off-screen camera for icons. The ItemSoGenerator currently sets `icon = null` and `modelPrefab = null` on every regenerate — V3's editor pipeline must run AFTER V2.5 (or be a follow-up pass), otherwise V2.5 will overwrite V3's assignments. Recommended chunk ordering note added implicitly here; V3 spec already implies "after V2".
- ItemDatabase.asset uses a fresh GUID (legacy GUID retired in V2.2). Any prefab that references items by direct SO reference (rather than itemId string) was already migrated in V2.2; V3-generated `placedPrefab` references should bind by SO reference rather than itemId string to avoid the same migration in V5.
```

```
Date: 2026-05-26
Agent: Implementation Volume 3 Chunk 3.1 (Material Palette)
Notes:
- Created Assets/Scripts/ArtPipeline/PaletteRegistry.cs (static, stateless; namespace Voidborne.ArtPipeline). 15 category/source/kind keys + 20 build-material keys (prefixed `build_*`). GetByKey returns Color.magenta for unknown / null / empty keys. GetForItem priority: isDeco > isBuildBlock (via buildColor) > category (weapon/armor/food/vehicle/automation/power/gadget/deco/build) > kind+source (Source items resolve by source enum; Machine kind resolves to "machine"; else build_neutral).
- Created Assets/Editor/ArtPipeline/MaterialGenerator.cs ([MenuItem("Voidborne/Generate/Materials")]). Iterates PaletteRegistry.AllEntries, creates/updates URP/Lit materials at Assets/Materials/Generated/Mat_{key}.mat. Idempotent — updates existing materials' _BaseColor/_Metallic/_Smoothness in place. Build materials get per-material PBR config: stone/brick/ash/cinder/marsh/obsidian/neutral (smoothness 0.3, metallic 0); iron/copper/titanium/gold/silver (0.6, 0.9); glass (0.9 smoothness, alpha 0.6, full surface-type transparent plumbing); wood/fabric (0.2, 0); bone (0.4, 0); chitin (0.5, 0); frost (0.7, 0); vord/void/kin (0.5, 0.4). Ore palette key explicitly NOT emissive (smoothness 0.5, metallic 0.3) per feedback_ore_visuals.md. Glass uses full URP transparent surface plumbing (_Surface, _Blend, _SrcBlend/_DstBlend, _ZWrite, render queue, RenderType tag, _SURFACE_TYPE_TRANSPARENT keyword). All materials force emission off (DisableKeyword("_EMISSION"), _EmissionColor=black, MaterialGlobalIlluminationFlags.EmissiveIsBlack).
- Created Assets/Tests/EditMode/MaterialGeneratorTests.cs — 8 tests: PaletteRegistry_HasAllKeys (all 15 spec keys); GetByKey_UnknownReturnsMagenta; GetByKey_NullOrEmptyReturnsMagenta; GetForItem_BuildBlock_ResolvesByBuildColor (mock ItemDefinition with buildColor="stone"); GetForItem_Fauna_ResolvesBySource; GetForItem_Machine_ResolvesToMachineColor; GetForItem_Deco_BeatsBuildBlock (priority sanity); AllEntries_IncludesBuildAndCategoryKeys (>=25 entries, contains 'machine' and 'build_stone').
- Generated 35 materials (15 category + 20 build variants). Acceptance range was 25-35; landed at the top of the band because build_materials.json carries 17 entries and we added 3 extra fallback variants (gold/silver/fabric) that may show up via ItemDefinition.buildColor in items.json. All under Assets/Materials/Generated/.
- EditMode tests: 278/278 PASS (was 270 pre-3.1, +8 new). Zero compile errors.
- DEVIATION 1: build_materials.json's `color` field is a palette CATEGORY ("build" / "exotic" / "beast"), not a hex value — so per-material colours are hardcoded in PaletteRegistry.BuildBuildMaterialColors() informed by each material's real-world appearance. Comment in the source documents this so V3.2/V3.3 don't try to re-read colours from JSON.
- DEVIATION 2: Spec said "if isBuildBlock, use the per-build-material color from buildColor". I added a graceful fallback: if buildColor is empty or doesn't match a known build material, GetForItem falls back to build_neutral (grey). Items.json reality has ~120 build items and not every one will carry a perfect buildColor key.
- DEVIATION 3: Added a couple of palette keys beyond the spec list: gold, silver, fabric (fallbacks for ItemDefinition.buildColor values that may appear in items.json), plus "build_neutral" emits as both a category key (#8b8b8b) and is also re-treated as a build-material PBR config branch. Net file count: 35.
- V3.2 heads-up: ProBuilder package will need to be installed before V3.2 lands (slab/panel/stairs/door/wedge meshes need ProBuilderMesh.CreateInstanceWithVerticesFaces). The Tracker already flags this. PaletteRegistry.GetByKey / GetForItem are now the canonical way to fetch material colours — V3.3 (ItemVisualRecipe) should resolve `materialKey` strings via the same lookup so the runtime + editor always agree.
- V3.3 heads-up: When ItemVisualRecipeMapper assigns materialKey strings, prefer using the bare palette keys (e.g. "machine", "build_stone") so a single PaletteRegistry.GetByKey can also resolve `Mat_<key>.mat` via `Assets/Materials/Generated/Mat_{key}.mat`. Suggest exposing a helper like `PaletteRegistry.AssetPathFor(key) => $"Assets/Materials/Generated/Mat_{key}.mat"` in V3.3 to keep the convention central.
```

```
Date: 2026-05-26
Agent: Review Volume 3 Chunk 3.1 (Material Palette)
Notes:
- Spec compliance: PASS. PaletteRegistry contains all 15 required spec keys (fauna/flora/ore/soil/exotic/food/power/weapon/armor/machine/build_neutral/deco/vehicle/automation/gadget) plus 17 build-material colours + 3 fallbacks (gold/silver/fabric). GetByKey returns Color.magenta on miss/null/empty (verified). GetForItem priority chain (deco > buildBlock > category > kind+source > build_neutral) is sensible. MenuItem path "Voidborne/Generate/Materials" correct. URP/Lit shader used (not Standard).
- Ore emission compliance: PASS. Mat_ore.mat _EmissionColor = (0,0,0,1), m_ValidKeywords empty (no _EMISSION), MaterialGlobalIlluminationFlags = EmissiveIsBlack. Generator unconditionally disables emission for ALL materials (lines 236-238 of MaterialGenerator.cs) — even safer than ore-only logic. Mat_ore Metallic 0.3 / Smoothness 0.5 (shiny but not glowing — matches feedback_ore_visuals.md).
- Generator correctness: PASS. Idempotent (load-then-update path verified). Uses StartAssetEditing/StopAssetEditing wrapper. Per-material PBR sensible — Mat_build_stone matte (smoothness 0.3, metallic 0), Mat_build_iron metallic (0.9, smoothness 0.6), Mat_build_glass transparent (_Surface=1, alpha 0.6, full URP transparent plumbing including _SrcBlend/_DstBlend/_ZWrite/keyword/RenderType tag), Mat_machine cyan (0.337/0.827/1.0 ≈ #56d3ff). 35 materials at top of 25-35 acceptance band.
- Tests: PASS. 8 new tests in MaterialGeneratorTests.cs — all sensible. Stub ItemDefinition created via ScriptableObject.CreateInstance and disposed via Object.DestroyImmediate in try/finally. No disk side-effects exercised (intentional).
- Compile + run via Unity MCP: PASS. read_console: 0 errors (only pre-existing CS0618/CS0162 warnings unrelated to V3.1). EditMode tests: 278/278 passed (job 7d66f71f). Re-ran "Voidborne/Generate/Materials" — log line confirmed "Generated 35 materials (0 new, 35 updated)" — idempotence verified.
- Codebase rules: PASS. No emojis. MaterialGenerator wrapped in `#if UNITY_EDITOR` and under Assets/Editor/. PaletteRegistry pure static, stateless. Comments are explanatory (not redundant) — note specifically the comments documenting the build_materials.json colour deviation and the ore emission rationale.
- Verdict: PASS. V3.1 flipped from [D] to [✓].
- Fixes applied: none — implementation was clean.
- Heads-up for V3.2: ProBuilder package install is still pending and is the only blocker for V3.2 (slab/panel/stairs/door/wedge). PaletteRegistry.AssetPathFor helper suggested by implementation agent for V3.3 is a good idea — recommend landing it alongside V3.3 so the runtime ItemVisualRecipe and the generator agree on `Assets/Materials/Generated/Mat_{key}.mat`. Minor observation only: Mat_build_glass uses _Surface=1 + premul/alpha plumbing; on URP 14+ this should render correctly with the default 2D renderer but worth eyeballing in scene-view once V3.3 spawns glass blocks. Not a blocker.
```

```
Date: 2026-05-26
Agent: Implementation Volume 3 Chunk 3.2 (Procedural Primitive Mesh Library)
Notes:
- Installed com.unity.probuilder 6.0.5 via Packages/manifest.json (verified via UnityMCP package job — succeeded; domain reload clean, 0 errors). All pre-existing CS0618/CS0162/CS0219/CS0652 warnings unchanged; no new ProBuilder warnings.
- Created Assets/Scripts/ArtPipeline/PrimitiveShape.cs — runtime-visible enum with all 14 spec values (Cube, Slab, Panel, Stairs, Door, Cylinder, Capsule, Sphere, Cone, Disc, Torus, Spike, Pyramid, Wedge). Lives in namespace Voidborne.ArtPipeline alongside PaletteRegistry. No editor dependency so V3.3's ItemVisualRecipe (runtime SO) can reference it.
- Created Assets/Editor/ArtPipeline/PrimitiveMeshFactory.cs — editor-only static class wrapped in #if UNITY_EDITOR. Public API: AssetPathFor(shape), GetOrCreate(shape), GenerateAll(). [MenuItem("Voidborne/Generate/Primitives")] calls GenerateAll. Idempotent (load-existing-asset path before construct). RecalculateNormals + RecalculateBounds on every newly-built mesh. EnsureFolder recursively creates Assets/Models/Generated/Primitives if absent.
- Created Assets/Editor/ArtPipeline/Voidborne.Editor.ArtPipeline.asmdef — Editor-only assembly referencing Voidborne + Unity.ProBuilder. MaterialGenerator.cs (V3.1) was already in this folder and is now part of this asmdef — no source changes needed because all its `using` directives still resolve (UnityEditor + UnityEngine.Rendering are CoreModule/auto-referenced; Voidborne.ArtPipeline is in the new asmdef's references). DEVIATION from V3.1's "no asmdef, defaults to Assembly-CSharp-Editor" pattern: tests assembly has `overrideReferences: true` and so can't see Assembly-CSharp-Editor; carving an explicit asmdef is the right move and is the cleaner long-term home for V3.3-V3.6 editor scripts too.
- Updated Assets/Tests/EditMode/EditModeTests.asmdef to add "Voidborne.Editor.ArtPipeline" to references.
- Created Assets/Tests/EditMode/PrimitiveMeshFactoryTests.cs — 3 tests with OneTimeSetUp that calls PrimitiveMeshFactory.GenerateAll once (idempotent on subsequent runs). Tests: GenerateAll_ProducesAllShapes (asset exists for every enum value), EachMesh_HasVerticesAndTriangles (vertexCount>0, triangles.Length>0 and %3==0), EachMesh_BoundsApproxUnitSize (all axes <= 3m ceiling; floor allows ONE axis to be near-zero so flat primitives like Disc still pass — at least 2 axes must be >= 0.05m).
- Mesh construction:
  - Cube/Cylinder/Capsule/Sphere — GameObject.CreatePrimitive + Object.Instantiate(mf.sharedMesh) + DestroyImmediate. We cannot reassign Unity's owned primitive mesh, so we duplicate before destroying the temp GO.
  - Slab (1×0.5×1) / Panel (1×1×0.1) / Door (1×2×0.1) — ShapeGenerator.GenerateCube(PivotLocation.Center, size). DEVIATION: ProBuilder's GenerateDoor returns a door-FRAME (legs + lintel above an opening), not the simple tall thin panel V3.3 needs as a "door" primitive. Using GenerateCube with 1×2×0.1 produces the expected silhouette and matches the spec's "Door — 1 × 2 × 0.1 panel (vertical, taller). Same as Panel scaled." line.
  - Stairs — ShapeGenerator.GenerateStair(PivotLocation.Center, Vector3.one, 4, true). Builds full sides.
  - Torus — ShapeGenerator.GenerateTorus(PivotLocation.Center, rows=8, columns=16, innerRadius=0.35, outerRadius=0.5, smooth=true, horizontalCircumference=360, verticalCircumference=360). ProBuilder 6.x signature uses inner/outer rather than radius/tubeRadius; inner=outer-tube → 0.5-0.15=0.35. (DEVIATION from spec's "GenerateTorus(radius=0.5, tubeRadius=0.15, segments=16, tubeSegments=8)" wording — the spec's signature doesn't exist in ProBuilder 6; ours expresses the same shape via the inner/outer convention. unity_reflect confirmed the signature before coding.)
  - Wedge — ShapeGenerator.GeneratePrism(PivotLocation.Center, Vector3.one). Triangular prism.
  - Cone (radius 0.5, height 1) and Spike (radius 0.2, height 1) — ShapeGenerator.GenerateCone(PivotLocation.Center, r, h, subdiv). Cone uses 16 subdivisions, Spike 12.
  - Disc — manual mesh: 16 radial wedges, 17 verts (1 centre + 16 rim), flat on the XZ plane facing +Y. Single-sided fan.
  - Pyramid — manual mesh: 5 verts (4 square base at y=0 + apex at y=1), 6 triangles (4 sides + 2-tri base), base size 1, height 1.
- All ProBuilder shapes follow the spec's ToMesh/Refresh → Instantiate(sharedMesh) → DestroyImmediate(pb.gameObject) pattern, isolated in a single ExtractAndDispose helper with try/finally so the temp GO is always cleaned up even on exception.
- Verified via Unity MCP:
  - refresh_unity force+compile after creating files: read_console errors=0; only pre-existing CS0618 warnings.
  - execute_menu_item "Voidborne/Generate/Primitives" → log "[PrimitiveMeshFactory] Generated 14 primitive meshes (14 new, 0 already existed) under Assets/Models/Generated/Primitives/." All 14 .asset files present on disk.
  - First test run (281 total) flagged ONE failure: EachMesh_BoundsApproxUnitSize with "Mesh Disc bounds.size.y (0) below floor 0.05". Disc is intentionally flat. Relaxed the test to permit ONE near-zero axis per mesh (must have >=2 axes inside [MinExtent, MaxExtent]; ceiling still applies to all axes). Re-ran: 281/281 PASS (278 baseline + 3 new).
- For V3.3 heads-up: PrimitiveMeshFactory.AssetPathFor(shape) is the canonical asset-path helper — V3.3's ItemModelComposer should resolve PrimitiveShape → mesh asset via this single function so the layout convention stays centralised. Suggest a sibling helper in V3.3 named PaletteRegistry.AssetPathFor(key) to mirror the convention for materials (already flagged by V3.1 review).
- For V3.3: the Disc primitive is single-sided. If V3.3 uses it for furnace glow / lamp emitter visuals, it may need to either render with a double-sided shader or be duplicated and flipped. Worth noting in V3.3 spec; not a V3.2 fix.
- For V3.3: ProBuilder also added Tutorial / SamplePolygons content during install. Spec said "ProBuilder may add tutorial assets or sample content on first install. Don't be surprised; don't delete them." No deletions performed.
```

```
Date: 2026-05-26
Agent: Review Volume 3 Chunk 3.2 (Procedural Primitive Mesh Library)
Notes:
- Packages/manifest.json: com.unity.probuilder 6.0.5 present; no other deps removed. Unity instance GameNine@2a29bfb34d33a2b2 (6000.3.9f1) connected; refresh_unity clean; read_console shows 0 ProBuilder-related errors or new warnings (only pre-existing regenerate-all info logs and one MCP WebSocket keep-alive notice unrelated to V3.2).
- PrimitiveShape.cs: public enum in Voidborne.ArtPipeline (runtime-accessible, lives under Assets/Scripts/ArtPipeline/). All 14 values present in spec order (Cube, Slab, Panel, Stairs, Door, Cylinder, Capsule, Sphere, Cone, Disc, Torus, Spike, Pyramid, Wedge).
- PrimitiveMeshFactory.cs: editor-only (#if UNITY_EDITOR + Assets/Editor/...). GetOrCreate is idempotent (loads existing asset before constructing). GenerateAll uses StartAssetEditing/StopAssetEditing + try/finally and SaveAssets/Refresh. MenuItem "Voidborne/Generate/Primitives" wired. Construction strategies per-shape are sensible (Unity primitives for Cube/Cylinder/Capsule/Sphere; ProBuilder GenerateCube/Stair/Torus/Prism/Cone for Slab/Panel/Door/Stairs/Torus/Wedge/Cone/Spike; manual fan-disc + 5-vert pyramid). ExtractAndDispose helper uses try/finally so the temp PB GO is always DestroyImmediate'd; only the instantiated bare Mesh is saved. RecalculateNormals + RecalculateBounds called before AssetDatabase.CreateAsset.
- Voidborne.Editor.ArtPipeline.asmdef: includePlatforms = ["Editor"]; references Voidborne + Unity.ProBuilder. Verified existing V2 editor data generators still live under Assets/Editor/Data/ with their own Voidborne.Editor.Data.asmdef — neither was folded, both compile (regenerate-all logs from V2 generators visible in console).
- EditModeTests.asmdef now lists Voidborne.Editor.ArtPipeline in references (alongside Voidborne, Voidborne.Editor.Data, Unity.Mathematics, Unity.Collections).
- Tests: run_tests EditMode (assembly EditModeTests) → 281/281 passed in 67.0s, 0 failures, 0 skipped. PrimitiveMeshFactoryTests fixture executes GenerateAll in OneTimeSetUp (idempotent — assets already on disk, just loads). EachMesh_BoundsApproxUnitSize relaxation is sensible: ceiling 3m applies to every axis (catches runaway); floor 0.05m only requires >=2 of 3 axes in-range, allowing exactly one near-zero axis for legitimately flat primitives (Disc has y=0; all other 13 primitives have all 3 axes in range).
- Disk: 14 .asset files present under Assets/Models/Generated/Primitives/. Cube.asset inspected — Mesh YAML with vertexCount=24, indexCount=36, AABB extent (0.5,0.5,0.5). Healthy.
- Codebase rules: no emojis in any V3.2 file; editor code under Assets/Editor/ and guarded by #if UNITY_EDITOR; comments are doc-style XML, no clutter; ProBuilder Samples/Tutorial folders left untouched.
- Verdict: PASS. Flipped V3.2 tracker from [D] to [✓]. No fixes applied — the implementation was already correct.
- For V3.3: implementer's heads-ups (canonical AssetPathFor pattern, single-sided Disc, untouched ProBuilder samples) carry forward as written. No additional concerns surfaced during review.
```
