# Voidborne — M2 Playtest Guide (2026-05-28)

Welcome. M2 (the Synergy Sandbox) is mechanically complete. The canonical
test below proves the milk-in-boiler-feeds-power loop end to end.
Press Play in `Assets/Scenes/Game.unity` and walk it through.

---

## The Test

The whole point of M2: prove that the forgiving-thermal boiler will accept
milk in place of water, run at reduced efficiency, and still produce real
power that charges a real battery.

1. **Press Play.** Game.unity loads. Player drops to the terrain surface
   via PlaytestSpawnSafety (raycast from y=200). Starter loadout populates.
2. **Open inventory (`Tab`).** The Index bracer foldout opens — you should
   see the Core 8 machines (workbench, furnace, steam_boiler, steam_generator,
   composter, drying_rack, crusher, storage_chest) plus extras.
3. **Place machines.** Drag the steam_boiler onto your hotbar (slot 1).
   Look at the ground. Click LMB to place. Repeat for steam_generator
   (place it ONE cell away — adjacency matters), battery_basic, power_sink.
4. **Wire it up.** Put `copper_cable_t1` on the hotbar. LMB on the generator,
   then LMB on the battery → cable spawns. LMB on the battery, then LMB on
   the power_sink → second cable.
5. **Bootstrap power.** Drop a `hand_crank_generator` next to a junction
   and wire it to the network. Press `E` and hold to crank.
6. **Open the boiler.** Walk up. Press `E`. The Machine UI opens with input
   slots and a recipe picker.
7. **Coal + water.** Drop one coal_ore and one water from your inventory
   into the input slots. The boil recipe fires — the boiler is now `IsRunning`.
   The adjacent steam_generator outputs **100 W**. The battery starts to
   accumulate stored Ws.
8. **Now swap water → milk.** Cancel, re-input with one milk instead. The
   boiler accepts the substitution via the `InputProperty` fallback (milk
   matches `Liquid_Aqueous` at efficiency × 0.4). The tooltip text reads:
   `Substituted milk → Liquid_Aqueous (efficiency ×0.4)`. The generator
   still outputs 100 W; the recipe just runs slower.
9. **Smile.** This is the canonical M2 acceptance moment.

Shortcut: press **F3** to spawn a pre-wired boiler rig (boiler + generator +
battery + power_sink + two cables) 3 m in front of you. Skips steps 3-4.

---

## Controls

### Movement
| Key            | Action                       |
|----------------|------------------------------|
| `W A S D`      | Walk                         |
| `Shift`        | Sprint                       |
| `Space`        | Jump                         |
| `Ctrl`         | Crouch                       |
| `Mouse`        | Look                         |

### Interaction
| Key             | Action                                    |
|-----------------|-------------------------------------------|
| `Tab` / `I`     | Toggle inventory (opens bracer foldout)   |
| `E`             | Interact / open machine UI                |
| `B`             | Open worn backpack                        |
| `J`             | Toggle quest log                          |
| `Esc`           | Pause menu / close open UI                |
| `LMB`           | Mine / chop / place block / arm cable     |
| `RMB`           | Rotate placement ghost / pick up backpack |
| `1` … `5`       | Select hotbar slot                        |
| Scroll wheel    | Cycle hotbar                              |

### Dev hotkeys (F-row, layered on top of normal input)
| Key   | Action                                                          |
|-------|-----------------------------------------------------------------|
| `F1`  | Toggle this help overlay (in-game version of this doc)          |
| `F2`  | Refresh starter loadout (re-grant items)                        |
| `F3`  | Spawn a boiler rig in front of the player (boiler+gen+batt+sink)|
| `F4`  | Teleport to (0, 80, 0) and snap to surface                      |
| `F5`  | Toggle fly mode (`Space` up, `LeftCtrl` down)                   |
| `F10` | Terrain leveler — carve a flat platform                         |
| `F12` | Toggle debug overlay (FPS, chunk count, PowerNodes, blocks)     |

---

## If Something's Broken

- **Fell through the world?** Press `F4` to teleport home and snap to surface.
- **Out of resources?** Press `F2` to re-grant the starter loadout.
- **Want to see what's happening?** Press `F12` for the debug overlay.
- **Want to fly to scout the world?** Press `F5`.
- **Need a clean rig to test the milk loop?** Press `F3`.
- **Cursor is stuck visible?** Press `Tab` twice or `Esc` to re-lock.

---

## Known Gaps

- **Terrain leveler is F10 dev-key** — not craftable yet (M7 polish).
- **Combat polish is M3** (next milestone). Weapons exist and fire, but
  hit reactions, ragdolls, and impact decals are M3 work.
- **Vehicles are M4** — no chase scenes, no vehicle combat yet.
- **Story / NPCs / dialogue are M6** — Wren the survivor and the Spirit
  Gateway aren't in the scene yet.
- **No save/load yet** — every Play session starts fresh.
- **No tool durability tracking in starter set** — the starter loadout
  doesn't include pickaxes/axes because those are ScriptableObject
  `ToolDefinition` assets, not Core 60 items. You can still mine with
  bare hands (slow) or use mining-related machines.
- **PowerNode node-count in F12 overlay** is via a scene scan, not a
  PowerNetwork API. It's accurate, just a touch slow.

---

## Test Targets (the manual playtest checklist)

- [ ] Mine ore from terrain (LMB hold on visible ore deposits).
- [ ] Open inventory (`Tab`). Confirm Core 8 machines + 30 cables are present.
- [ ] Build workbench from inventory's wood / stone / plant_fiber via the
      personal 2×2 crafting grid embedded in the inventory panel.
- [ ] Place workbench. Open it (`E`). Right-click craft a furnace.
- [ ] Place the furnace. Open it. Smelt iron_ore → iron_ingot using coal_ore
      as fuel.
- [ ] Place a steam_boiler.
- [ ] Place a steam_generator one cell away (adjacency required).
- [ ] Cable battery_basic + power_sink into the steam_generator with
      copper_cable_t1.
- [ ] Place a hand_crank_generator. Crank it (`E` hold) to bootstrap.
- [ ] Open the boiler (`E`). Pick the boil recipe. Drop coal + water in.
      Confirm the generator outputs 100 W and the battery charges.
- [ ] Replace water with milk. Re-craft. Watch efficiency drop to 0.4×.
      The tooltip should display:
      `Substituted milk → Liquid_Aqueous (efficiency ×0.4)`.
- [ ] **This is the canonical M2 acceptance moment. Smile.**

Stretch goals (if M2 still feels fun):
- [ ] Place a conveyor_belt + inserter chain. Feed iron_ingots from a
      storage_chest into a crusher.
- [ ] Place a campfire. Cook raw_meat over it.
- [ ] Place a drying_rack with wood → plank.
- [ ] Place a composter with plant_fiber → fertilizer.
- [ ] Use `F10` to carve a flat platform on a hillside.
