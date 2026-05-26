# Vehicle System Overhaul -- Build Plan

## Context

The current vehicle system has a flat component model (Engine/Suspension/Armor/Glazing/Utility attached to a frame) with zone-based damage (6 zones, 4 stages) and basic WheelCollider physics. The goal is to evolve this into a **modular 3-section body system** with **realistic physics** (drift, roll, crash damage) where **every body part is an individually damageable, detachable, pickupable item** with persistent condition.

**Player scale reference**: 2.0m tall. Vehicles are sized accordingly (Buggy ~3.5m long, Hauler ~6m, Cycle ~2m).

---

## Phase 0: Foundation -- New Data Types

No existing behavior changes. All new types/enums.

### 0A. Extend enums in `VehicleFrame.cs`
- Add `BodySection` enum: `Front, Middle, Back, Undercarriage`
- Add to `AttachmentType`: `Hood, Bumper, CockpitShell, CockpitDoorLeft, CockpitDoorRight, TrunkDoor, Wheel`
- Extend `AttachmentPoint` with: `BodySection section`, `string slotId`, `Vector3 localRotation`, `bool isRequired`

### 0B. Add `VehiclePart` to `ItemType` enum
- **File**: `Assets/Scripts/Inventory/ItemDefinition.cs`
- Add `VehiclePart = 8`

### 0C. Create `VehiclePartItem` (new file)
- **File**: `Assets/Scripts/Vehicles/Items/VehiclePartItem.cs`
- Extends `ItemDefinition` with: `partType`, `fitsSection`, `slotId`, `maxCondition`, `attachedModelPrefab`, `worldModelPrefab`, optional `VehicleComponent wrappedComponent`
- `maxStackSize = 1` (parts never stack -- each has unique condition)

### 0D. Create `VehiclePartCondition` struct (new file)
- **File**: `Assets/Scripts/Vehicles/Items/VehiclePartCondition.cs`
- Serializable struct: `currentHP`, `maxHP`, `DamageStage stage`
- Carried by WorldItem instances and by VehicleBodyPartSlot at runtime
- `Fresh(maxHP)` factory, `Ratio` property, `IsDestroyed` check

### 0E. Add VehiclePart color to `ItemIconGenerator.cs`
- `ItemType.VehiclePart` -> `new Color(0.85f, 0.55f, 0.15f)` (amber/mechanical)

---

## Phase 1: 3-Section Body System

### 1A. Create `VehicleBodyPartSlot` (new file)
- **File**: `Assets/Scripts/Vehicles/VehicleBodyPartSlot.cs`
- MonoBehaviour placed as child GO at each attachment point position
- Holds: `VehiclePartItem` definition, runtime `VehiclePartCondition`, spawned visual GO
- Methods: `Install(part, condition)`, `Uninstall() -> (part, condition)`, `TakeDamage(float)`, `Detach()` (eject as WorldItem)
- At `Destroyed` stage, auto-calls `Detach()`
- At `Critical` stage + vehicle speed > 10 m/s, 5%/sec chance of spontaneous detachment

### 1B. Create `VehicleBody` section manager (new file)
- **File**: `Assets/Scripts/Vehicles/VehicleBody.cs`
- MonoBehaviour on vehicle root, reads `VehicleFrame.attachmentPoints`
- Creates child GOs with `VehicleBodyPartSlot` for each point
- Groups by `BodySection` -> `Dictionary<BodySection, List<VehicleBodyPartSlot>>`
- API: `GetSlot(slotId)`, `GetSlotsInSection(section)`, `InstallPart(slotId, part, condition)`, `RemovePart(slotId)`, `AllSlots`

### 1C. Integrate with `AssembledVehicle.cs`
- Add optional `VehicleBody` reference (null-check for backward compat with legacy vehicles)
- When `VehiclePartItem` has a `wrappedComponent`, also register it in old `_installed` dict so `GetTotalEnginePower()`, suspension config, armor reduction still work
- Replace `SpawnSalvageAndDestroy()`: iterate VehicleBody slots -> `Detach()` each -> proper WorldItems

### 1D. Wire `VehicleDamageSystem.cs` to body sections
- Zone-to-section mapping: EngineBay->Front, Chassis->Middle, FuelSystem->Back, Drivetrains->Undercarriage
- New method: `ReceivePartDamage(slotId, amount)` routes to specific slot
- `ApplyZoneEffect` propagates damage to relevant `VehicleBodyPartSlot` objects in matching section

### 1E. Buggy frame slot definitions

| slotId | section | type | localPos | Required |
|---|---|---|---|---|
| `front_bumper` | Front | Bumper | (0, 0.3, 1.65) | No |
| `hood` | Front | Hood | (0, 0.92, 1.0) | No |
| `engine` | Front | Engine | (0, 0.55, 1.0) | Yes |
| `cockpit` | Middle | CockpitShell | (0, 0.9, 0) | Yes |
| `door_left` | Middle | CockpitDoorLeft | (-0.93, 0.7, 0) | No |
| `door_right` | Middle | CockpitDoorRight | (0.93, 0.7, 0) | No |
| `trunk_door` | Back | TrunkDoor | (0, 0.6, -1.45) | No |
| `rear_bumper` | Back | Bumper | (0, 0.3, -1.65) | No |
| `wheel_fl` | Undercarriage | Wheel | (-0.85, 0.35, 1.1) | Yes |
| `wheel_fr` | Undercarriage | Wheel | (0.85, 0.35, 1.1) | Yes |
| `wheel_rl` | Undercarriage | Wheel | (-0.85, 0.35, -1.1) | Yes |
| `wheel_rr` | Undercarriage | Wheel | (0.85, 0.35, -1.1) | Yes |
| `suspension` | Undercarriage | Suspension | (0, 0.2, 0) | Yes |

---

## Phase 2: Parts as Items -- WorldItem Integration

### 2A. Extend `WorldItem.cs`
- Add field: `public VehiclePartCondition partCondition;`
- Skip merge for `VehiclePartItem` (each has unique condition)
- Add part-type model building in `BuildModel()`

### 2B. Detachment -> WorldItem pipeline (`VehicleBodyPartSlot.Detach()`)
1. Create GO at slot world position
2. Add WorldItem, set itemStack + partCondition
3. Apply ejection force (outward from vehicle center + up) + random torque
4. Remove visual from vehicle slot
5. Notify VehicleBody slot is empty

### 2C. Extend `TooltipUI.cs` for vehicle parts
- Add `VehiclePartItem` case: show part type, section, weight
- Add condition overload: `Show(ItemDefinition, VehiclePartCondition)` showing condition % and stage

### 2D. Add per-part icons to `ItemIconGenerator.cs`
- Wheel: dark circle + lighter hub
- Hood: flat trapezoid, metallic gray
- Bumper: horizontal bar
- Cockpit: dome/arch, tinted glass
- Door: rectangle + window cutout
- Engine: rectangle + pipe details
- Trunk door: small rectangle + handle

### 2E. Create VehiclePartItem ScriptableObjects & register in ItemDatabase
- Per vehicle frame, ~13 items (buggy_wheel, buggy_hood, buggy_engine, etc.)
- Convention: `{frame}_{parttype}` e.g. `buggy_front_bumper`, `hauler_engine`

---

## Phase 3: Realistic Physics

### 3A. Center of Mass recalculation
- **File**: `VehicleBase.cs` -- new `RecalculateCenterOfMass()`
- Called on Awake and on part install/remove
- Weighted average of frame center + each installed part's position * weight
- Missing engine shifts CoM back (wheelie-prone), missing door shifts CoM laterally (pull)

### 3B. Weight-based turning
- **File**: `WheeledVehicle.cs` -- modify `ApplySteeringForce`
- Speed-dependent steer reduction: at >70% max speed, steer angle drops to 60%
- Mass factor: `massSteerFactor = Clamp(600 / mass, 0.5, 1.0)` -- heavier = slower turn

### 3C. Drift / slide system
- **File**: `WheeledVehicle.cs` -- new `ApplyDriftPhysics()` in FixedUpdate
- Track lateral velocity via dot product with transform.right
- When lateral speed > 3 m/s: reduce sideways friction stiffness by 40%
- When braking at speed > 5 m/s: reduce forward friction by 50% (brake lock)
- Subtle counter-steer damping force for controllable drifts
- New fields: `driftThreshold=3`, `driftFrictionMultiplier=0.6`, `brakeLockSpeedThreshold=5`

### 3D. Rollover system
- **File**: `WheeledVehicle.cs`
- Replace fixed antiRollForce with tunable `rollResistance = 2500`
- Calculate lateral G-force; when > `rollThreshold` (8 m/s^2), reduce anti-roll to 20%
- `CheckRollover()`: when transform.up dot Vector3.up < -0.3 for 2+ seconds, force eject all passengers
- Normal driving stays stable; extreme maneuvers allow flips

### 3E. Crash damage from collisions (new file)
- **File**: `Assets/Scripts/Vehicles/VehicleCollisionDamage.cs`
- `OnCollisionEnter`: calculate impact force from `collision.impulse`
- Above `minCrashForce` (5000N): deal damage scaled by force to nearest section/part
- Route to VehicleDamageSystem + nearest VehicleBodyPartSlot
- High-speed crashes can knock off bumpers and doors
- Fields: `minCrashForce=5000`, `damagePerNewton=0.005`

---

## Phase 4: Existing System Compatibility Updates

### 4A. `WheeledVehicle.EjectWheels()` -> use VehicleBody
- Find wheel slots for destroyed side, call `slot.Detach()` for proper WorldItems
- Disable corresponding WheelCollider
- Fallback to old sphere debris for vehicles without VehicleBody

### 4B. `CycleVehicle` adaptation
- 2 wheel slots (front/rear), seat section (no cockpit shell), optional saddlebag
- Keep existing lean physics unchanged

### 4C. `VehicleAssemblyUI.cs` -- section-grouped display
- Group slots by BodySection with headers
- Show part icon, condition bar (green/yellow/orange/gray), name or "Empty"
- Click to install from inventory / remove to inventory

### 4D. `VehicleHUD.cs` -- part indicator overlay
- Vehicle outline showing each part slot as colored icon by damage stage
- Detached parts disappear from outline

### 4E. `FieldRepairSystem.cs` -- part-targeted repair
- Raycast to specific part collider -> repair that part
- Fallback to worst-zone repair when not targeting a specific part

---

## Phase 5: Placeholder Prefab Specs (MCP Creation)

### Buggy (3.5m x 2.0m x 1.5m)

| Part | Primitives | Dimensions | Color RGB |
|---|---|---|---|
| **Chassis** (frame, always present) | Box | 3.2 x 0.15 x 1.8, Y=0.25 | (0.25, 0.25, 0.28) dark steel |
| **Front Bumper** | Box | 1.8 x 0.25 x 0.15, at (0, 0.3, 1.65) | (0.30, 0.30, 0.32) gunmetal |
| **Rear Bumper** | Box | 1.8 x 0.25 x 0.15, at (0, 0.3, -1.65) | (0.30, 0.30, 0.32) gunmetal |
| **Hood** | Box | 1.6 x 0.08 x 1.0, at (0, 0.92, 1.0), rotX=-5 | (0.40, 0.55, 0.40) olive |
| **Engine** | Box 0.7x0.5x0.8 + Cyl r=0.08 h=0.3 (exhaust) | at (0, 0.55, 1.0) | (0.35, 0.35, 0.38) iron |
| **Cockpit** | Box floor 1.8x0.08x1.4 + Box backwall 1.8x0.8x0.08 + Box windshield 1.8x0.6x0.05 rotX=-25 | Middle area | (0.40, 0.55, 0.40) olive, windshield (0.4, 0.6, 0.7) alpha 0.5 |
| **Door Left** | Box 0.06x0.7x1.2 + window Box 0.04x0.25x0.6 | at (-0.93, 0.7, -0.1) | (0.40, 0.55, 0.40) olive |
| **Door Right** | Mirror of left | at (+0.93, 0.7, -0.1) | same |
| **Trunk Door** | Box 1.6x0.5x0.06 | at (0, 0.6, -1.45) | (0.40, 0.55, 0.40) olive |
| **Wheel** (x4) | Cyl r=0.35 w=0.22 (tire) + Cyl r=0.15 w=0.24 (hub) | at wheel positions | tire (0.15, 0.15, 0.15), hub (0.55, 0.55, 0.58) |
| **Spring visual** | Cyl r=0.04 h=0.25 | between wheel and chassis | (0.50, 0.50, 0.52) |

### Hauler (6.0m x 2.5m x 2.5m)
- Same part types but scaled up: wheels r=0.45 w=0.30, 6 wheels (FL/FR Z=+2, ML/MR Z=0, RL/RR Z=-2)
- Larger engine (1.0x0.7x1.0), dual exhaust cylinders
- Open truck bed instead of trunk: floor Box 2.3x0.12x2.0 + 3 side walls 0.5m tall

### Cycle (2.0m x 0.8m x 1.2m)
- Frame tube: Cyl r=0.05 length=1.6m tilted 15deg
- Seat: Box 0.3x0.1x0.4 at Y=0.85
- 2 wheels: Cyl r=0.30 w=0.12
- Small engine: Box 0.25x0.3x0.35
- Handlebars: Cyl r=0.03 w=0.6m horizontal at Y=1.05

### Pushcart (1.5m x 1.0m x 1.0m)
- Open wooden box: floor Box 1.2x0.06x0.8 at Y=0.4, 4 side walls Box 0.06x0.4x0.8 / 1.2x0.4x0.06
- Handle: 2 parallel Cyl r=0.03 length=0.8m tilted -30deg at rear, crossbar Cyl r=0.03 w=0.7m
- 2 wheels: Cyl r=0.25 w=0.1 at front (Z=+0.5)
- Small front leg: Box 0.04x0.2x0.04 at Y=0.2, Z=-0.5
- Colors: wood (0.55, 0.34, 0.14), iron fittings (0.40, 0.40, 0.42)

### DrillRig (4.0m x 2.2m x 1.8m)
- Chassis: Box 2.0x0.2x3.8 at Y=0.25, (0.30, 0.30, 0.32) gunmetal
- Low cab: Box 1.8x0.6x1.0 at (0, 0.6, -0.5), windshield Box 1.7x0.4x0.05 at front, olive
- Drill arm: Cyl r=0.12 length=1.5m tilted -20deg extending from front
- Drill head: Cone-approximated via Cyl r=0.25→0.05 at tip, (0.70, 0.50, 0.15) rust orange
- Engine: Box 0.8x0.5x0.7 at (0, 0.5, -1.2), iron
- 4 wheels: Cyl r=0.40 w=0.28 at (±0.95, 0.40, ±1.5)
- Terrain lamp: small Sphere r=0.06 at (0, 0.95, 0.3), (0.9, 0.9, 0.7) warm

### Gyrocopter (3.0m x 2.0m x 2.5m tall)
- Fuselage: Box 1.0x0.6x2.5 at Y=0.5, (0.30, 0.45, 0.55) blue-gray
- Cockpit bubble: Sphere-approximated via Cyl r=0.45 h=0.5 at (0, 0.9, 0.3), glass (0.4, 0.6, 0.7, 0.5)
- Tail boom: Cyl r=0.06 length=1.5m at (0, 0.6, -1.8), (0.30, 0.30, 0.32)
- Tail rotor: Cyl r=0.01 w=0.5m at (0, 0.7, -2.5), horizontal
- Main rotor mast: Cyl r=0.04 h=0.6 at (0, 1.3, 0)
- Main rotor blade: Box 3.0x0.02x0.15 at (0, 1.6, 0), (0.50, 0.50, 0.52)
- Landing skids: 2 Cyl r=0.03 length=1.8m at (±0.5, 0.1, 0), (0.40, 0.40, 0.42)
- Engine: Box 0.5x0.4x0.5 at (0, 0.5, -0.8), iron

---

## Phase 6: Realistic Drivetrain Simulation

### 6A. Extend `EngineComponent.cs` with drivetrain data
- `peakTorqueNm` (float): peak torque in Newton-metres (e.g. Buggy 180, Hauler 450, Cycle 85)
- `peakTorqueRPM` (float): RPM at which peak torque occurs (e.g. 3500)
- `redlineRPM` (float): max RPM before rev limiter (e.g. 6500)
- `idleRPM` (float): idle RPM when not throttling (e.g. 800)
- `gearRatios` (float[]): per-gear ratios (e.g. {3.5, 2.1, 1.4, 1.0, 0.8})
- `finalDriveRatio` (float): differential ratio (e.g. 3.7)
- `engineBrakingFactor` (float): deceleration when off-throttle (e.g. 0.3)
- Backward-compat: if gearRatios is empty/null, fall back to flat `maxPower` model

### 6B. Create `VehicleDrivetrain.cs` (new file)
- **File**: `Assets/Scripts/Vehicles/VehicleDrivetrain.cs`
- Runtime MonoBehaviour on vehicle, references EngineComponent
- State: `currentRPM`, `currentGear` (0-indexed), `clutchEngaged`
- **Torque curve**: parabolic approximation peaking at `peakTorqueRPM`
  - `EvaluateTorque(rpm)` = peakTorque * (1 - ((rpm - peakTorqueRPM) / peakTorqueRPM)^2)
  - Clamped to [0, peakTorque], zero above redline
- **RPM calculation**: `rpm = wheelAngularVelocity * gearRatio * finalDrive * 60 / (2π)`
  - Clamped to [idleRPM, redlineRPM]
- **Wheel torque output**: `engineTorque * gearRatio * finalDrive * throttle`
- **Auto-shift logic**:
  - Upshift when RPM > 85% of redline
  - Downshift when RPM < 35% of redline (and not in 1st)
  - Shift delay: 0.2s lockout to prevent hunting
- **Engine braking**: when throttle near zero, apply negative torque = rpm * engineBrakingFactor
- **Rev limiter**: cut fuel (zero torque) when rpm >= redlineRPM
- Public API: `float GetWheelTorque(float throttle)`, `int CurrentGear`, `float CurrentRPM`, `float RPMNormalized`
- **Horsepower readout**: `HP = torque * rpm / 5252` (display only)

### 6C. Integrate `VehicleDrivetrain` into `WheeledVehicle.ApplyMotorForce()`
- Replace flat `power * speedLimitFactor` with `drivetrain.GetWheelTorque(throttle)`
- Keep drivetrain damage factors (left/right torque multiplication unchanged)
- Keep fuel drain tied to |throttle|
- Fallback: if no drivetrain component, use old flat power model

### 6D. `VehicleHUD.cs` — RPM / gear display
- Add RPM bar (normalized 0–1 fill, red zone above 85%)
- Show current gear number (or "N" when idle, "R" for reverse)
- Show HP readout

### 6E. Engine audio hooks (future-ready)
- `VehicleDrivetrain` exposes `RPMNormalized` (0–1) for audio pitch mapping
- No audio implementation now, just the data hook

### Drivetrain presets per vehicle

| Vehicle | Peak Torque | Peak RPM | Redline | Gears | Final Drive | Top Speed |
|---------|-------------|----------|---------|-------|-------------|-----------|
| Buggy   | 180 Nm | 3500 | 6500 | {3.5, 2.1, 1.4, 1.0, 0.8} | 3.7 | ~25 m/s |
| Hauler  | 450 Nm | 2200 | 4500 | {4.0, 2.8, 2.0, 1.4, 1.0, 0.75} | 4.1 | ~18 m/s |
| Cycle   | 85 Nm  | 5000 | 9000 | {2.8, 1.9, 1.4, 1.1, 0.9, 0.75} | 3.2 | ~30 m/s |
| DrillRig| 350 Nm | 2000 | 4000 | {4.5, 3.0, 2.0, 1.4} | 4.5 | ~12 m/s |

---

## Implementation Order

```
Phase 0 (Foundation)           -- no dependencies, start here
  |
  v
Phase 1A-1B (Slot + Body)     -- depends on Phase 0
  |          \
  v           v
Phase 2A-2D   Phase 3A-3D     -- can run in parallel
(Items)       (Physics)
  |           |
  v           v
Phase 1C-1D (Integration)     -- wires old + new systems together
  |
  v
Phase 2E-2G (UI/Icons/DB)     -- tooltip, icons, item registration
  |
  v
Phase 3E-3F (Crash + rattle)  -- depends on Phase 1 + 3
  |
  v
Phase 4 (Compat updates)      -- last code changes
  |
  v
Phase 5 (MCP prefabs)         -- can start as early as Phase 1, finalize last
```

---

## Files Summary

### New Files (5 scripts + ~20 ScriptableObject assets)
| File | Purpose |
|---|---|
| `Assets/Scripts/Vehicles/Items/VehiclePartItem.cs` | ItemDefinition subclass for vehicle parts |
| `Assets/Scripts/Vehicles/Items/VehiclePartCondition.cs` | Runtime condition struct |
| `Assets/Scripts/Vehicles/VehicleBodyPartSlot.cs` | Per-slot MonoBehaviour on vehicle |
| `Assets/Scripts/Vehicles/VehicleBody.cs` | Section manager |
| `Assets/Scripts/Vehicles/VehicleCollisionDamage.cs` | Crash damage from collisions |

### Modified Files (12)
| File | Changes |
|---|---|
| `Assets/Scripts/Vehicles/VehicleFrame.cs` | BodySection enum, AttachmentPoint extension, AttachmentType additions |
| `Assets/Scripts/Inventory/ItemDefinition.cs` | Add VehiclePart to ItemType |
| `Assets/Scripts/Vehicles/VehicleBase.cs` | RecalculateCenterOfMass, VehicleBody ref |
| `Assets/Scripts/Vehicles/WheeledVehicle.cs` | Drift, roll, weight-steering, EjectWheels rewrite |
| `Assets/Scripts/Vehicles/AssembledVehicle.cs` | VehicleBody delegation, salvage rewrite |
| `Assets/Scripts/Vehicles/VehicleDamageSystem.cs` | Part-level routing, section mapping |
| `Assets/Scripts/Inventory/WorldItem.cs` | partCondition field, merge skip, part models |
| `Assets/Scripts/UI/ItemIconGenerator.cs` | VehiclePart color, per-part icons |
| `Assets/Scripts/UI/TooltipUI.cs` | VehiclePartItem condition display |
| `Assets/Scripts/UI/VehicleAssemblyUI.cs` | Section-grouped slot display |
| `Assets/Scripts/UI/VehicleHUD.cs` | Part indicator overlay |
| `Assets/Scripts/Vehicles/FieldRepairSystem.cs` | Part-targeted repair |

---

## Key Design Decisions

1. **Condition on instances, not SOs**: `VehiclePartItem` SO defines max HP / part type. Runtime condition lives in `VehiclePartCondition` struct on the slot/WorldItem. One `buggy_wheel` SO serves 4 wheel slots with different damage states.

2. **Backward compat via null-check**: Every access to `VehicleBody` checks for null first. Legacy prefabs without VehicleBody fall back to old `AssembledVehicle` dict path.

3. **VehiclePartItem wraps VehicleComponent**: Rather than replacing the old system, parts optionally wrap old components. Wrapped component registers in `AssembledVehicle._installed` so `GetTotalEnginePower()` etc. keep working.

4. **Anti-roll is tunable, not removed**: Normal driving feels stable; only extreme lateral G reduces anti-roll and allows flips.

---

## Verification

1. **Compile check**: After each phase, verify zero errors via `read_console` MCP tool
2. **Physics test**: Enter Buggy, drive in circles at max speed -- should drift. Sharp turn at speed should allow rollover. Braking at speed should slide.
3. **Part detachment**: Shoot a door with a weapon -- should break off, fall as WorldItem, be pickupable
4. **Condition persistence**: Pick up damaged part -> check tooltip shows condition % -> install on vehicle -> verify condition carries over
5. **Item system**: Open inventory with vehicle part -> verify icon, tooltip, correct ItemType
6. **Crash test**: Drive Buggy into wall at speed -> front bumper should take damage / detach, engine bay zone should take damage
7. **Rollover recovery**: Flip vehicle -> verify passenger ejected after 2s -> verify vehicle stays as physics object
8. **Legacy compat**: Load old Cycle/Gyrocopter prefabs -> verify they still function via old code path
