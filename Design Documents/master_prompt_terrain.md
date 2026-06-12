# MASTER PROMPT — Terrain Overhaul & Liquid Simulation (v1)

**Goal:** Infinite generated terrain with ores, biomes, placed structures and roads, plus full-3D
liquid simulation (works in caves and on overhangs — explicitly NOT a column/heightfield system),
all sustaining **100+ FPS while moving fast through the world**.

**Companion to:** `master_prompt.md` v4 (Volumes 12/13 define biome + structure content; this doc
defines the terrain *engine* work those volumes run on). Follows the same agent workflow and
critical rules as the master prompt.

---

## PART 1 — CURRENT STATE AUDIT (2026-06-11, post-forgework merge)

A full code review of `Assets/Scripts/World/` was done before writing this plan. Summary of what
exists, what works, and what is broken/dead. **Do not re-implement things in the KEEP list.**

### 1.1 What we have (KEEP — this is already good)

| System | Where | Status |
|--------|-------|--------|
| GPU marching cubes, async | `MarchingCubesAdapter.cs` | 12 concurrent generation slots, AsyncGPUReadback everywhere, ArrayPool'd triangle readback, bounds computed off-thread. **No main-thread GPU stalls in the happy path.** |
| 5-tier LOD | `ChunkManager.cs:29-41` | LOD0 (3,2,2) full detail → LOD3 (14,3,1) coarse MC → LOD4 (22,4,1) CPU greedy mesh. `LodToModifier()` steps the MC sampling (1/2/4). |
| Frame budgeting | `ChunkFrameBudget.cs` | Adaptive 2–6 ms main-thread budget for chunk callbacks; shared by `MainThreadDispatcher` + `ChunkCallbackQueue`. |
| Velocity-biased streaming | `ChunkLoader.cs:389-398` | Chunks ahead of player movement load first (bias 0.6). Clipmap-delta requeue for LOD1+ rings. |
| Occlusion culling | `ChunkVisibilityGraph.cs` + `ChunkOcclusionCuller.cs` | Minecraft-style 15-bit face-connectivity flood fill (Burst) + BFS from camera every 10 frames. Active and correct. |
| Compact vertices | `CompactVertex.cs` | 24-byte vertex (half pos, octahedral normal, packed biome/ore/tint) for LOD0. |
| Async colliders | `ChunkRenderer.cs:75-99` | `Physics.BakeMesh` on background thread, LOD0 only, assignment via high-priority queue. |
| Voxel classification | `VoxelClassificationJob.cs` | Burst IJobParallelFor: 5D climate k-d tree biome lookup + ore scatter + grass/dirt/rock layering, ~3-5 ms off-thread per chunk. |
| Palette compression | `PaletteStorage.cs` | Single/Palette/Direct modes for ore+biome fields; 80-90% memory savings; zero-alloc NativeArray interop. |
| Full-3D density | `DensityFunction.cs` | 7 vertical zones (-1000 to +800), spaghetti/cheese/noodle caves, ravines, sky island bands, Voronoi road + tunnel carving already in generation. |
| Decoration rendering | `GrassRenderer.cs`, `TreeRenderer.cs` | GPU compute-culled instancing, sub-allocated persistent buffers, grass 3-LOD, tree billboards at 80m+. Wind already hooked for grass. |

### 1.2 What is broken, dead, or missing (the overhaul targets)

| # | Problem | Evidence | Impact |
|---|---------|----------|--------|
| G1 | **LOD seam cracks** — `LodSeamStitcher.AddSkirts()` exists but is never called | no call sites found | Visible holes/cracks at every LOD ring boundary |
| G2 | **Region batching disabled** — LOD1+ draw-call combiner commented out due to z-fighting (source renderers were left enabled) | `ChunkManager.cs:1350` | Thousands of small draw calls at high render distance; CPU submission cost |
| G3 | **GPU Resident Drawer OFF** | `Assets/Settings/PC_RPAsset.asset:86` (`m_GPUResidentDrawerMode: 0`) | Free CPU savings unclaimed, matters most once trees/rocks/props scale up |
| G4 | **No persistence** — edits live only in `ChunkData.densityField`; unload = edits lost | no save path in Chunks/ | "Infinite world" is impossible without it; blocks M6+ |
| G5 | **No liquid system at all** — `SurfacePoint.underwater` is hard-coded false | `ChunkMeshBuilder.cs:434` | The headline feature of this overhaul |
| G6 | **Activation throttle** — `maxOreCompletionsPerFrame = 2` regardless of speed | `ChunkManager.cs:59` | At fast movement the activation queue backlogs; visible pop-in |
| G7 | **Dead/legacy code** — `Assets/Scripts/World/MarchingCubes/Scripts/ChunkManager.cs` (gtaharaedmonds original) duplicates our manager; `DirectionalSubmeshBuilder` value unproven (index rebuild on camera turn vs ~5-10% tri savings) | audit | Confusion + maintenance cost; possible negative-value work per camera turn |
| G8 | **Deformation is single-thread C#** | `TerrainDeformer.cs` | Fine today (0.5-2 ms per dig), becomes a problem when liquid wake-up + bigger brushes land |

### 1.3 Frame budget math (the target we engineer against)

100 FPS = **10 ms** total. Working budget on main thread:

| Slice | Budget |
|-------|--------|
| Rendering submission + URP overhead | 3.5 ms |
| Gameplay (player, machines, power, AI) | 2.0 ms |
| Chunk streaming callbacks (existing `ChunkFrameBudget`) | 2.0 ms |
| Liquid sim main-thread share (apply/remesh dispatch only) | 0.5 ms |
| Decoration + occlusion + face culling | 1.0 ms |
| Headroom (GC, spikes, OS) | 1.0 ms |

Everything heavy (density, classification, liquid ticks, mesh assembly, collider bake) stays on
GPU/jobs/background threads — that architecture already exists and every new system MUST use it.

---

## PART 2 — THE OVERHAUL PLAN

Phases are ordered by dependency and risk-reduction. P0–P2 pay down debt and buy headroom;
P3 spends that headroom on liquids; P4–P5 finish the "infinite world" promise.

### Phase P0 — Cleanup & Free Wins (1–2 sessions)

**Chunk P0.1 — Delete legacy MC scripts.**
Remove `Assets/Scripts/World/MarchingCubes/Scripts/{ChunkManager,ChunkInstance,DensityBehaviour,NoiseDensityBehaviour,NoiseTextureBehaviour}.cs` (keep `Resources/` compute shaders — those are the live MC kernels). Fix any references.
*Acceptance:* zero compile errors, terrain generates identically (same seed → same mesh hash on 5 sample chunks).

**Chunk P0.2 — Enable GPU Resident Drawer.**
Set `m_GPUResidentDrawerMode: 1` (InstancedDrawing) in PC_RPAsset (leave Mobile off), enable per-instance occlusion (`GPU occlusion culling` toggle) and verify SRP Batcher compatibility of `TriplanarTerrainCompact`, `TriplanarTerrain`, `DistantTerrain`, grass/tree shaders (no per-material `MaterialPropertyBlock` — already the case per `ChunkRenderer.cs:106`).
*Acceptance:* Frame Debugger shows BRG batches for trees/rocks; no visual change; measure CPU render-thread delta and record it in this doc.

**Chunk P0.3 — Measure-or-remove DirectionalSubmeshBuilder.**
Profile 2 scenarios (open plains, cave) with face culling on/off. If main-thread cost of `RebuildIndices()` on camera turns exceeds GPU savings (likely — we are not vertex-bound), delete it; otherwise keep and document the win.
*Acceptance:* decision recorded with profiler numbers; dead path removed if losing.

### Phase P1 — LOD Quality & Draw-Call Scalability (the "far render distance" phase)

**Chunk P1.1 — Wire LOD skirts.**
Call `LodSeamStitcher.AddSkirts()` during mesh finalization for any chunk whose `ComputeBoundaryMask()` ≠ 0 (it already computes neighbor-LOD masks). Skirts hang 1 voxel into solid ground; with triplanar shading they are visually free and they kill cracks without Transvoxel complexity.
*Acceptance:* fly the LOD0/1 and LOD1/2 boundaries — zero visible cracks against sky or caves behind.

**Chunk P1.2 — Fix region batching for LOD2+.**
Root-cause of the z-fighting was both the combined region mesh AND the per-chunk renderers drawing. Fix: when `ChunkRegion.CombineMeshes()` succeeds, disable the source `MeshRenderer`s (keep GameObjects for state); re-enable on region dirty/split. Combine only LOD2-4 (LOD1 remeshes too often near the ring edge). 4×4×4 chunk regions as already coded.
*Acceptance:* draw call count at default distances drops ≥40% in Frame Debugger; no z-fighting when spinning the camera at distance.

**Chunk P1.3 — Transvoxel evaluation (OPTIONAL — only if P1.1 skirts look bad).**
Time-boxed spike: implement Lengyel transition cells for the LOD0→1 boundary only (tables from transvoxel.org). If skirts are visually acceptable after P1.1, skip permanently — do not gold-plate.
*Acceptance:* explicit go/no-go note in this doc.

**Chunk P1.4 — Fast-movement streaming hardening (fixes G6).**
- Make ore/activation throughput adaptive: `maxOreCompletionsPerFrame = 2` base, scale to 6 when player speed > 15 m/s or backlog > 24 chunks, using the existing `ChunkFrameBudget` headroom signal.
- Raise `velocityBias` toward 0.8 above 20 m/s (vehicles).
- While speed > 25 m/s, temporarily shrink LOD0 ring to (2,2,2) and let LOD1 cover the difference; restore on slowdown. (LOD0 colliders+decorations are the expensive part; you can't mine at 25 m/s anyway.)
*Acceptance:* fly at 30 m/s for 60 s over varied terrain: no frame > 16 ms, no visible holes in the direction of travel.

### Phase P2 — Persistence & True Infinite World (fixes G4)

**Chunk P2.1 — Region save files.**
`World/Persistence/{RegionFile,ChunkSerializer,WorldSave}.cs`. Minecraft-style region files: 16×16×16 chunks per region file under `Saves/<world>/region/`. Only chunks with `isDirty`-ever (player-modified density, depleted ores, liquid state) are written — pristine chunks regenerate from seed. Format per chunk: version byte, RLE-compressed *delta* of densityField vs regenerated baseline (regenerate-on-load + apply delta keeps files tiny), `OreField`/`BiomeField` via existing `PaletteStorage` serialization, liquid field (P3) raw RLE.
Write on: chunk unload (queued to background thread) + autosave timer + quit. Read on: chunk load before generation completes (delta applies after density readback).
*Acceptance:* dig a tunnel, quit, relaunch — tunnel persists. Unmodified world: zero region files. Save/load of 100 modified chunks < 1 frame hitch (all IO off-thread).

**Chunk P2.2 — Deterministic structure hooks (Volume 13 enabler).**
`StructurePlanGrid` queries must happen inside generation, not after: add a generation-time stamp pass that (a) flattens density under planned structure cells (already how roads carve — same pattern, `DensityFunction.ApplyRoadCarving` is the template), (b) registers a "spawn blueprint here" callback fired on first LOD0 activation of the cell's anchor chunk. Persistence of spawned-structure state rides P2.1.
*Acceptance:* a test 16×16 m pad flattens terrain at a planned cell and survives save/reload; roads (already carved by Voronoi in `DensityFunction`) connect to it.

### Phase P3 — LIQUID SIMULATION (the headline)

#### Design decisions (locked)

1. **Full 3D cellular automata on the chunk grid** — liquid works in caves, on overhangs, on
   ceilings of dug tunnels. No column/heightfield representation anywhere.
2. **Liquid is data, not physics.** One byte per voxel = fill level 0–255 (0 = none). Buoyancy,
   swimming, machine intake all *sample the field*; there are no liquid colliders ever.
3. **Sparse storage.** `ChunkData.liquidField` is `null` until the first liquid cell enters the
   chunk (most chunks never allocate). Flat `NativeArray<byte>` (32 KB) while active; collapses
   back through `PaletteStorage` when the chunk settles (usually Single-mode = 1 byte).
4. **Sleep/wake activity model (the perf core).** A chunk simulates ONLY if it is in the
   `activeLiquidSet`. Chunks enter the set when: liquid flows across their border, terrain is
   deformed adjacent to their liquid, or a seal to a virtual source breaks. A chunk leaves the set
   after N ticks (default 4) with zero cell changes. Steady-state cost of a placid lake: **zero**.
5. **Fixed tick, off-thread.** 8 Hz sim tick. Each tick schedules one Burst `IJob` per active
   chunk (double-buffered read/write fields, neighbor border slices passed as read-only). Main
   thread only: schedule + completion poll + dirty-remesh enqueue, inside the existing 0.5 ms slice.
6. **Virtual infinite sources for oceans/aquifers.** Large water bodies are NOT simulated cells.
   A per-region `LiquidSourceVolume` (AABB + surface Y + liquid type) acts as boundary condition:
   any unsealed face adjacent to the volume emits full-level cells into the CA. Dig into an
   underground lake (the -256 to -512 mega-cavern band per master prompt) → the breach chunk
   wakes, water pours in from the virtual boundary, flows/settles by CA rules, and the source
   never drains. This is how we get "oceans for free" without the column hack.
7. **Liquids are typed.** Water, acid (FungalMarshes pools), petroleum (Badlands seeps), milk
   (machines — already a fluid in the crafting layer). Type byte lives per-chunk (one liquid type
   per chunk — cross-contamination resolves to the heavier type; keeps storage at 1 byte/voxel).
8. **Coop-ready.** Sim is server-authoritative and deterministic per tick given identical fields;
   clients receive chunk liquid deltas like terrain edits. (Constraint from master_prompt coop
   rules — design now, netcode lands Volume 21.)

#### CA rules (per cell, per tick — Burst job)

```
1. FALL:    if cell below has capacity → move up to min(level, capacity) down. Gravity first.
2. SPREAD:  else distribute excess to the 4 lateral neighbors with lower level
            (equal split, viscosity-scaled max transfer per tick; water=64, petroleum=16).
3. SETTLE:  if no transfer occurred and level < MIN_VISIBLE (4) and no support → evaporate to 0
            (kills infinite shimmer from 1-level films).
4. PRESSURE (cheap U-tube pass, every 4th tick): within a chunk, scan vertical runs of
   full cells; if a column's head is higher than a connected column's head by ≥2, transfer 1
   level from high head to low head. Approximate, local, good enough for "water finds its level."
```

Solid check is `densityField[i] > 0` — liquid and terrain share the voxel index space, no
duplicate occupancy data. Terrain deformation (dig) → `TerrainDeformer` already marks the chunk
dirty; same call now also wakes the chunk + 6 neighbors in `activeLiquidSet`.

#### Rendering

- **Mesh:** liquid surface comes from the SAME GPU MC pipeline. Liquid density for MC =
  `liquidLevel/255 - isoOffset`, masked to air voxels only. Reuse `MarchingCubesAdapter` with
  **2 reserved generation slots** for liquid remesh (terrain keeps ≥8 LOD0 slots per
  `ChunkLoader.cs:38` arbitration — extend that arbitration, don't fork it). Smooth MC water,
  not Minecraft cubes, and we inherit async readback + pooling for free.
- **Remesh throttle:** a liquid chunk remeshes at most every 2 sim ticks (4 Hz) while active;
  on settle, one final high-quality remesh. Visual flow between remeshes is faked in-shader
  (scrolling normals + vertex wobble) so 4 Hz geometry looks continuous.
- **Shader:** `Shaders/LiquidSurface.shader` — URP transparent queue, depth-fade color, screen-space
  refraction (URP opaque texture), per-type tint/viscosity params, foam at terrain intersection
  via depth delta. One material per liquid type, SRP-batcher compatible, no per-chunk MPBs.
- **LOD:** liquid renders ONLY within the LOD0+LOD1 rings. Beyond that, `LiquidSourceVolume`s
  render as a flat alpha plane (cheap quad per region) — at 200+ m you cannot tell. Distant
  *dynamic* puddles simply don't draw (they're below visual significance at that range).
- **Underwater:** full-screen pass triggered by camera-in-liquid sample (fog, tint, distortion).
  `SurfacePoint.underwater` finally gets computed for real → decoration skips submerged grass.

#### Gameplay integration (minimum slice in this phase)

- Player swim state + buoyancy from field sampling (`Player/PlayerSwimming.cs`).
- Bucket item: pick up / place 255-level cell (wakes chunk).
- Machine intake: Steam Boiler can pump from an adjacent liquid cell ≥ threshold (replaces
  hand-fed water/milk for automation — ties into M2's boiler chain).
- Acid/petroleum damage-and-status touch effects (constants in `GameConstants.cs`).

#### Phase P3 chunk breakdown

| Chunk | Deliverable | Files |
|-------|------------|-------|
| P3.1 | Liquid field + storage + save format | `World/Liquid/{LiquidField,LiquidTypes}.cs`, ChunkData ext, P2.1 serializer ext |
| P3.2 | CA sim job + activity set + tick scheduler | `World/Liquid/{LiquidSimJob,LiquidSimulator,LiquidActivitySet}.cs` |
| P3.3 | MC liquid meshing via adapter slots + remesh throttle | `World/Liquid/LiquidMesher.cs`, `MarchingCubesAdapter` slot reservation |
| P3.4 | Liquid shader + underwater pass | `Shaders/LiquidSurface.shader`, `Shaders/UnderwaterPost.shader` |
| P3.5 | Virtual sources + worldgen placement (marsh pools, badlands seeps, underground lakes, sky oasis pools) | `World/Liquid/LiquidSourceVolume.cs`, `DensityFunction` zone hooks |
| P3.6 | Gameplay: swim, bucket, boiler intake, hazard liquids | `Player/PlayerSwimming.cs`, item + machine hooks |

*Phase acceptance:* dig into an underground lake wall → water pours through the tunnel, flows
around overhangs, fills the dug pit, settles flat, sim cost returns to 0 ms; placid lake +
swimming + boiler pumping all hold 100+ FPS; quit/reload preserves the flooded tunnel.

#### Liquid perf budget (hard limits, enforced by tests where possible)

| Cost | Limit |
|------|-------|
| Active liquid chunks (typical play) | ≤ 8 (worst-case burst 32, then arbitrated by distance) |
| Sim tick CPU (workers, 8 active chunks) | ≤ 1.5 ms per tick @ 8 Hz (≈0.1 ms/frame amortized) |
| Main-thread liquid work per frame | ≤ 0.5 ms (schedule/poll/dispatch only) |
| Liquid remeshes per frame | ≤ 2 (reserved adapter slots) |
| Memory per liquid-active chunk | 64 KB (double-buffered byte field), 1 byte settled-single |

### Phase P4 — Generation Throughput (only if profiling demands it)

Candidates, in order of expected value — **do not start until P0–P3 metrics show a need:**
- Burstify `TerrainDeformer` (G8) — needed if liquid wake-ups make big digs spike.
- Density LOD early-out: LOD3/4 chunks skip cave/road/ore noise channels entirely (surface shell
  only) — cuts the ~270K noise evals/chunk by ~60% for the largest rings.
- Empty-chunk fast path: closed-form min/max bound per zone band to skip full 33³ sampling for
  provably-all-air / all-solid chunks (huge for sky + deep bands).
- `maxOreCompletionsPerFrame`/slot counts auto-tune from `ChunkFrameBudget` history.

### Phase P5 — Polish & Future-Proofing

- Weather hooks: rain raises wake-rate of surface liquid chunks slightly (puddle fill), ties to
  Volume 12.4. Blizzard = frozen surface variant (rendering only, no sim).
- Wind interaction: liquid shader wave amplitude from `GrassWindSettings` global so water and
  grass agree on storm intensity.
- Vord/abyssal liquid type (story-gated, Volume 16+): same system, new type byte + shader params.

---

## PART 3 — RULES FOR AGENTS WORKING THIS DOC

1. **Never block the main thread on GPU or jobs.** Follow the existing async patterns
   (`AsyncGPUReadback`, `IsCompleted`-poll-then-`Complete()`, background `Task.Run` + callback
   queues). Any PR adding a synchronous readback or unconditional `.Complete()` in Update is wrong.
2. **The MC compute shaders remain untouched** (master prompt rule). Liquid meshing goes through
   `MarchingCubesAdapter` like everything else.
3. **Measure before and after every phase.** Record: avg/p99 frame time at default settings,
   draw calls, chunk activation latency at 30 m/s, in a table appended to this doc. The 100 FPS
   target is judged on the worktree machine at 1440p, default render distances.
4. **Liquid never allocates in the steady state.** No liquid = null field; settled = palette
   single byte; only active chunks hold flat buffers. Tests must assert allocation-free ticks
   for an all-settled world.
5. **Determinism:** same seed + same edit log + same tick count ⇒ identical liquid fields
   (required for coop later). No `UnityEngine.Random` in sim code; hash cell coords + tick.
6. Update the STATUS table below as chunks complete.

## STATUS

| Phase | Chunk | Status |
|-------|-------|--------|
| P0 | P0.1 legacy delete | ✓ 2026-06-11 — Scripts/ folder removed, compute shaders kept, no scene/prefab GUID refs |
| P0 | P0.2 GPU Resident Drawer | ✓ 2026-06-11 — mode=InstancedDrawing + GPU occlusion in PC_RPAsset; GraphicsSettings BRG variants set to Keep All (required, editor errors otherwise); renderer already Forward+ |
| P0 | P0.3 directional submesh decision | ☐ deferred — needs in-game profiling (plains + cave, culling on/off); do alongside the P1 acceptance playtest |
| P1 | P1.1 skirts wired | ✓ 2026-06-11 — design change vs. plan: skirts are ALWAYS-ON for all 6 faces of LOD1-3 (not neighbor-mask-based) so seams self-heal when neighbors change LOD without remeshing; depth scales with LOD step (2/4); double-skirt guarded via ChunkData.lastSkirtedMesh |
| P1 | P1.2 region batching fixed | ✓ 2026-06-11 — root cause confirmed (source renderers left on); ChunkRenderer.SetBatched + occlusion-visible flags compose; batches LOD2/3 only; handles LOD transitions incl. LOD4 path and post-deform remesh |
| P1 | P1.3 Transvoxel go/no-go | ☐ pending playtest of P1.1 skirts |
| P1 | P1.4 fast-movement hardening | ✓ 2026-06-11 (partial) — activation cap triples at speed>15 m/s or backlog>24; velocity bias ramps to 0.8 at >20 m/s. LOD0-ring-shrink-at-speed REJECTED: speed oscillation would thrash LOD0↔LOD1 remeshes; revisit only if playtest shows LOD0 cost at speed |
| P2 | P2.1 region save files | ✓ 2026-06-11 — design change vs. plan: persists SPARSE EDIT OVERLAYS (voxelIndex→value recorded at the edit sites) instead of diff-vs-regenerated-baseline, which would suffer GPU-vs-CPU float mismatch. `World/Persistence/{ChunkSerializer,RegionFile,WorldPersistence}.cs`; 16³-chunk region files, atomic rewrite, pristine world = zero files; stash on unload, 60s async autosave, sync flush on quit; edited chunks remesh once post-activation via the deformation path. BONUS: fixes pre-existing bug where LOD transitions regenerated density pristine and silently wiped deformation edits. 15 EditMode tests |
| P2 | P2.2 structure hooks | ☐ |
| P3 | P3.1–P3.6 liquid system | ☐ |
| P4 | (gated on profiling) | ☐ |
| P5 | polish | ☐ |

**Verification log:**
- 2026-06-11 (P0.1+P0.2+P1.1+P1.2+P1.4): full diff applied to the editor project — compiles clean, 410/410 EditMode tests pass. Runtime acceptance (seam flyby, Frame Debugger draw-call count, 30 m/s flythrough) still needs a manual playtest session.
- 2026-06-11 (P2.1): compiles clean, 425/425 EditMode tests pass (15 new persistence tests). Runtime acceptance (dig → quit → relaunch → tunnel persists) needs a manual playtest; save files land in `%USERPROFILE%/AppData/LocalLow/<company>/<product>/Saves/world_<seed>/region/`.
