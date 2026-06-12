# Game Design Implementation Spec
## Project Overview

Unity open-world survival game with Minecraft-style crafting, marching cubes smooth terrain, electricity systems, processing machines, and quest-driven progression. The central mechanic is upgrading **The Index** — an artifact that called the player (themselves, not a fictional character) from another dimension to address a crisis in this world.

---

## 1. Core Narrative Context

Keep lore simple and diegetic — no cutscenes. Delivered via:
- **Device readings**: short 1-2 line HUD notifications triggered by proximity or upgrade events. e.g. *"Residual Architect signature detected. Recent."*
- **Data tablets**: collectible inventory items containing short readable paragraphs, found in ruins and enemy locations
- **Environmental storytelling**: half-built structures, deactivated constructs, craters — the world implies its history

**Story summary**: The Architect was an advanced being who built this world using the device. Something went wrong — the Architect is gone, the world is degrading, and wrong things are filling the power vacuum. The device reached across dimensions and chose the player as its host (the first compatible person it found). The device has fragmented intelligence — it remembers what it built but not why things broke.

**Act structure** (no cutscenes, all environmental/diegetic):
1. Arrive, survive, learn the world, receive device fragments as visions (brief screen effects, not cinematics)
2. Upgrade device, encounter Remnants, discover the Architect was containing something
3. Unlock pocket dimension generator, find the Architect's indexed dimension with a message left for the next host
4. Final upgrade: full Architect mode — reshape the world, face the ending choice (restore / transcend / dismantle)

---

## 2. The Index — Upgrade Path

**Physical form:** Wrist bracer on the left forearm. Angular, utilitarian — form follows function. Folding mechanical display panels hinge open for inventory/crafting interfaces. Physical screens, no holograms. Additive segments are bolted on per tier upgrade.

**Diegetic UI:** All HUD elements render on The Index's physical screens:
- 5-slot hotbar on the bracer face (always visible)
- Health/stamina indicators on the bracer edge
- Device messages scroll on the main screen
- Inventory/crafting on fold-out panels (arm raises, panels unfold mechanically)

**Personality:** Impersonal, terse, technical. Reports status — never refers to itself as "I", never emotes. The Architect built for function.

The device is the progression spine. Each tier unlocks new abilities AND a narrative beat.

| Tier | Upgrade Name | Gameplay Unlock | Narrative Beat |
|---|---|---|---|
| 1 | Basic Terrain Shaping | Ore-based terrain add/remove | First device vision — glimpse of the Architect building |
| 2 | Electrical Grid Interface | Place/connect electrical cables and basic machines | Device starts reading old Architect infrastructure |
| 3 | Processing Machine Sync | Decode corrupted Architect data logs | Data tablets become readable at Architect sites |
| 4 | Dimensional Scanner | Detects the pocket dimension but can't open it | Device confirms something is indexed there |
| 5 | Pocket Dimension Generator | Create and open pocket dimensions | Architect's message found — real story begins |
| 6 | Full Architect Mode | Reshape terrain at scale, access final quest | Ending branch choice unlocked |

---

## 3. Electrical System

Modelled after Rust's electricity + Mekanism (modded Minecraft) tiered cables. No shorts, no circuit failures — just throughput caps and supply/demand balance.

### Cable Tiers
| Tier | Material | Throughput | Visual |
|---|---|---|---|
| 1 | Copper Wire | Low | Thin, dull orange |
| 2 | Insulated Cable | Medium | Medium, white |
| 3 | Architect Conduit | High | Thick, faint blue pulse |

### Rules
- Power sources output a wattage value
- Cables have a throughput cap — if demand exceeds cable capacity, machines slow or stop (no damage, no drama)
- Machines have a power draw value
- If supply < demand, machines throttle proportionally
- **Batteries/Capacitors** act as buffers — essential for pocket dimension charging (needs sustained power over time, not a spike)

### Power Sources (by progression)
1. **Combustion Generator** — burns fuel, early game, low output
2. **Wind Turbine / Solar Panel** — mid game, passive, weather-dependent
3. **Architect Reactor** — late game, runs on dimensional ore looted from pocket dimensions, high output

### Pocket Dimension Power Loop
- Small pocket dimension = low sustained power, basic capacitor banks + combustion generators
- Large pocket dimension = Tier 3 conduit required, reactors, serious infrastructure
- The Architect's indexed dimension requires a power threshold only reachable with mid/late game infrastructure
- Dimensional ore from pocket dimensions fuels reactors → self-sustaining expansion loop

### Implementation Notes
- Cables are **manually placed objects** that run along surfaces — not auto-routed
- Junction boxes placed at corners/junctions
- Machines have a visible power port on one face — cable snaps to port
- Power network is visible and readable: cable tier = visual thickness + color

---

## 4. Building System

### Design Philosophy
- No structural integrity simulation — players can build whatever they want, no piece will "fail" due to physics
- Snap-to-grid by default, toggle for free placement
- Snap priority: nearest valid snap point, **not** pixel-perfect aiming (avoid No Man's Sky jank)
- Foundations snap to a virtual flat grid regardless of uneven terrain beneath — engine resolves the visual gap with a "skirt" mesh
- One build tool does everything: place, remove, upgrade, repair
- Right-click = radial category menu
- Hover over placed piece = shows tier and HP
- No workbench radius requirement

### Material Tiers
| Tier | Name | Source | Visual | Notes |
|---|---|---|---|---|
| 1 | Salvage | Scavenged/early game | Rough, improvised | Low HP |
| 2 | Refined | Processed alloys | Intentional, sturdy | Required for mid-tier electrical |
| 3 | Architect Alloy | Pocket dimensions only | Sleek, faintly luminescent | Required for high-draw electrical + large portals |

- Upgrading in place is supported: highlight piece → upgrade without rebuilding

### Full Piece List

**Structural**
- Foundations: square, triangle, hex
- Walls: full, half, quarter, angled/diagonal
- Floors: full, half, triangle, grate (light passes through)
- Ceilings: full, half, triangle, grate
- Columns: short, tall, tapered
- Beams: horizontal, diagonal brace
- Arches: small, large

**Openings**
- Door frames: standard, wide, tall
- Window frames: square, wide slit, porthole
- Hatches: floor-mounted, ceiling-mounted
- Gates: large (outdoor walls)

**Terrain-Integrated**
- Retaining wall (flat face one side, rough rock face other — sits flush against carved terrain)
- Tunnel mouth (arch that blends into carved rock)
- Embedded floor (no underside — sits directly on shaped ground)
- Cave wall panel (irregular edge — lines inside of carved spaces)

**Exterior / Defense**
- Battlements (crenellated wall top)
- Wall catwalk (narrow floor attaches to wall top)
- Watchtower base
- Barricades (low cover, non-structural)
- Wire/fence sections

**Functional Furniture**
- Workbench
- Storage crate, locker
- Machine footprint (base plate, machines snap onto it)
- Cable junction box
- Power switch
- Light fixtures: ceiling, wall sconce, floor lamp
- Door panels (placed into door frames)

**Atmospheric / Lore**
- Architect pillar (ornate, found in ruins — player can craft at Tier 3)
- Data tablet stand
- Dimension portal frame (large 2-wide doorway shape)
- Conduit housing (decorative cable cover)
- Hanging cables (decorative)

**Total: ~60 pieces across all tiers**

### Enemy Location Design
All enemy strongholds, ruins, and quest locations are built using this same building system. This means:
- Players can loot pieces contextually from enemy locations
- The developer builds locations with the same tools as the player
- Location character comes from *terrain shaping + piece selection*, not from unique assets

Example location types:
- **Underground bunker**: Stamp-hollowed hill interior, cave wall panels, embedded floors, tunnel mouth entrance
- **Cliffside outpost**: Retaining walls flush against carved rock, battlements on top, catwalks between carved ledges
- **Architect ruin**: Dense rock carved into smooth organic shapes, Architect pillars, data tablet stands — Architect Alloy material
- **Surface camp**: Barricades, crates, wall sections on flat ground — no terrain work, improvised look

---

## 5. Terrain Manipulation

Terrain is entirely made of ores (dirt, gravel, rock, dense rock, architect stone, etc). Marching cubes density field — adding/removing density changes the terrain mesh.

### Mode 1 — Ore Shaping (Held Material, Tier 1 Device)
- Hold a terrain ore in hand
- Hold place button near terrain → slowly **adds density** (builds up)
- Hold remove button → slowly **subtracts density** (carves away)
- Ore type determines visual material of the result
- **Adding terrain consumes ore from inventory**
- **Removing terrain returns ore to inventory**
- Two brush radius sizes toggled with a key (small / large)
- Ore types and behavior:
  - Dirt: fast, soft, gentle natural slopes
  - Gravel: medium speed, slightly rougher
  - Rock: slow, holds sharper edges, good for cliff faces
  - Dense rock: very slow, very sharp edges, good for flat carved walls
  - Architect stone: found only in ruins/pocket dimensions, produces smooth luminescent surfaces

### Mode 2 — Architect's Shaper (Mid-game Device Upgrade, Tier 3+)
Four discrete operations, no complex brush UI:

1. **Flatten** — samples height at aimed point, flattens a radius to that height. Primary tool for building pads.
2. **Smooth** — softens jagged density transitions across a radius. Cleans up rough cave interiors.
3. **Stamp** — define a box volume, then either fully fill or fully hollow it. Primary tool for carving bunker rooms or raising hills.
4. **Raise/Lower Column** — raises or lowers a cylindrical column of terrain uniformly. For terraced platforms, craters, dramatic geography.

---

## 6. Enemies

All enemies reflect world degradation — nothing is purely evil, everything is *wrong in a specific way*.

### Enemy Types

**Remnants**
- Architect-built constructs that lost their purpose directive
- Behavior reflects their original function (a terrain-sculpting Remnant carves endlessly and destroys anything in its path)
- Can be **reprogrammed** using the device — key mid-game mechanic
- Reprogrammed Remnants can be anchored to player stronghold as living defense systems

**The Hungry Static**
- The thing the Architect was containing
- No fixed form — manifests as world glitches: flickering terrain, machines running backwards, corrupted creature behavior
- Prolonged exposure causes player-side effects: brief UI distortions, device giving incorrect readings
- Cosmic horror kept subtle — unsettling, not jump-scary
- Primary late-game threat

**Claim Beasts**
- Fauna that evolved in the Architect's world, now territorial around ruins
- Not supernatural — just dangerous
- Guard resources the player needs, creating combat/stealth tension around loot

**Echoes**
- Look identical to the player
- What happens when the Hungry Static tries to copy a host and fails
- Use the player's own device abilities against them, badly/incorrectly
- Most unsettling enemy — fighting one feels like a broken mirror

---

## 7. Quest Structure

Quests feel like **the device pulling you somewhere**, not a quest board.

### Quest Types

**Signal Quests** — Device detects something anomalous. Player investigates. Could be a Remnant, data cache, Static incursion, or Architect site. Unknown until arrival.

**Stabilization Quests** — A region is degrading. Build infrastructure, defeat corrupted Remnants, restore power flow before the area collapses.

**Reprogramming Quests** — A Remnant is causing havoc. Destroy it OR get close enough to interface with it (stealth/patience challenge). Reward for reprogramming: powerful stronghold ally.

**Memory Quests** — An Architect memory fragment surfaces. Physically reconstruct what the vision showed (using the building system) to unlock the full memory. Building as puzzle-solving.

**Echo Hunts** — An Echo of the player has appeared. Find and eliminate it before it reaches an active Remnant site and causes a cascade failure.

---

## 8. Implementation Priority Order

Suggested order based on dependencies:

1. **Terrain system** — marching cubes density field, ore-based add/remove (Mode 1 shaping)
2. **Building system** — snap grid, piece placement, Tier 1 pieces, build tool
3. **Electrical system** — cable placement, power sources, machines, throughput logic
4. **Device upgrade path** — tier gates, device UI, reading notifications
5. **Architect's Shaper** — Mode 2 terrain tools (flatten, smooth, stamp, raise/lower)
6. **Pocket dimension generator** — instanced dimensions, power threshold gate, size scaling
7. **Enemy AI** — Remnants first (simplest behavior), then Claim Beasts, Echoes, Hungry Static last
8. **Quest system** — signal detection, quest triggers, stabilization logic, memory reconstruction
9. **Lore delivery** — data tablets, device readings, environmental props
10. **Reactor + Architect Alloy loop** — late game power/resource cycle
