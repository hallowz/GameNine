using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Automation;
using Voidborne.Power;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.1 / V7.2 — Player-side block placement controller.
    ///
    /// Attached to the player GameObject. On every frame, peeks the
    /// currently-selected hotbar item; if that item is a build block
    /// (<see cref="ItemDefinition.isBuildBlock"/>) OR a placeable machine
    /// (<see cref="ItemDefinition.kind"/> == <see cref="ItemKind.Machine"/>),
    /// raycasts forward from the camera, snaps the hit point to a
    /// <see cref="BuildGrid"/> cell, and shows a translucent ghost of the
    /// item's placedPrefab there.
    ///
    /// On left-click: consume one from the active hotbar slot, Instantiate
    /// the placedPrefab at the cell, attach a <see cref="PlacedBlock"/>, and
    /// register in <see cref="BlockRegistry"/>. If the item is a Machine, also
    /// attach a <see cref="MachineCraftingStation"/> and call
    /// <see cref="MachineCraftingStation.Init"/> with the matching
    /// <see cref="MachineDefinition"/> (V6.3 wiring).
    ///
    /// On right-click: rotate the ghost 90 deg around Y. The rotation is
    /// carried into the next placement.
    /// </summary>
    /// <remarks>
    /// Coop note: placement is owner-authoritative for M2 — the player's
    /// inventory mutation + the world-spawn happen locally. V21 will gate
    /// every placement through a ServerRpc that re-runs the same checks
    /// (cell free? inventory has 1?) and broadcasts the resulting PlacedBlock
    /// state via NetworkBehaviour replication.
    /// </remarks>
    [DisallowMultipleComponent]
    public class BlockPlacer : MonoBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Maximum distance from the camera to place a block.")]
        [SerializeField] private float placementReach = 5f;

        [Tooltip("Layer mask the placement raycast hits against. Default: everything except the ghost itself.")]
        [SerializeField] private LayerMask placementMask = ~0;

        [Header("Ghost")]
        [Tooltip("Color tint for the ghost preview (RGBA). Default: pale cyan with low alpha.")]
        [SerializeField] private Color ghostTint = new Color(0.5f, 0.9f, 1f, 0.35f);

        [Tooltip("Color tint for the ghost preview when the cell is occupied / invalid.")]
        [SerializeField] private Color ghostBlockedTint = new Color(1f, 0.3f, 0.3f, 0.4f);

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private Camera _playerCamera;
        private PlayerInventory _inventory;
        private InputSystem_Actions _inputActions;
        private InputAction _placeAction;

        private GameObject _ghostInstance;
        private ItemDefinition _ghostForItem;
        private Quaternion _ghostRotation = Quaternion.identity;
        private bool _ghostBlocked; // true when the current ghost is over an occupied cell

        // ---------------------------------------------------------------
        //  Public test seams
        // ---------------------------------------------------------------

        /// <summary>The current ghost rotation (for the active ghost). Public for tests.</summary>
        public Quaternion CurrentGhostRotation => _ghostRotation;

        /// <summary>Public test seam: rotate the ghost +90 around Y without simulating input.</summary>
        public void RotateGhost90()
        {
            _ghostRotation *= Quaternion.AngleAxis(90f, Vector3.up);
            if (_ghostInstance != null) _ghostInstance.transform.rotation = _ghostRotation;
        }

        /// <summary>The current ghost instance (null when nothing is shown). Public for tests.</summary>
        public GameObject CurrentGhost => _ghostInstance;

        /// <summary>True when the ghost is currently rendered over an occupied / invalid cell.</summary>
        public bool IsGhostBlocked => _ghostBlocked;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            _playerCamera = GetComponentInChildren<Camera>();
            if (_playerCamera == null) _playerCamera = Camera.main;
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null) _inventory = FindObjectOfType<PlayerInventory>();
        }

        private void OnEnable()
        {
            // Wrap in try/catch so EditMode test fixtures (which don't have a
            // wired InputSystem context) can still spin up the component.
            try
            {
                _inputActions = new InputSystem_Actions();
                _inputActions.Player.Enable();
                _placeAction = _inputActions.Player.Attack;    // left click commits placement
            }
            catch (System.Exception)
            {
                _inputActions = null;
                _placeAction = null;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Disable();
                _inputActions.Dispose();
                _inputActions = null;
            }
            DestroyGhost();
        }

        private void Update()
        {
            // Don't place anything while a UI modal is up (chest, machine, etc).
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            {
                DestroyGhost();
                return;
            }

            if (_playerCamera == null || _inventory == null)
            {
                DestroyGhost();
                return;
            }

            ItemStack active = _inventory.ActiveHotbarItem;
            ItemDefinition def = active.item;
            if (active.IsEmpty || def == null || !IsPlaceable(def))
            {
                DestroyGhost();
                return;
            }

            // Raycast forward from the camera; if nothing's hit, hide the ghost.
            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, placementReach, placementMask))
            {
                DestroyGhost();
                return;
            }

            // Compute the target cell. Don't pin the origin yet — the origin
            // gets pinned on the first commit (left-click), not on the ghost.
            BuildGrid grid = BuildGrid.Instance;
            Vector3 anchor = hit.point + hit.normal * 0.01f; // bias outward from the surface
            Vector3Int cell = grid.WorldToCell(anchor);
            BlockFormResolver.Form form = BlockFormResolver.Resolve(def);
            Vector3 placePos = BlockFormResolver.GetPlacementPosition(grid, cell, form);

            // Refresh / spawn the ghost.
            EnsureGhost(def, placePos);

            // Check occupancy + paint ghost accordingly.
            bool occupied = BlockRegistry.Instance != null && BlockRegistry.Instance.IsCellOccupied(cell);
            ApplyGhostTint(occupied ? ghostBlockedTint : ghostTint);
            _ghostBlocked = occupied;

            // Right-click rotate: snap +90 around Y. Polled directly off
            // Mouse.current since the generated InputSystem_Actions has no
            // dedicated "rotate ghost" binding (M2 scope; M7 polish will
            // wire a proper action).
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                _ghostRotation *= Quaternion.AngleAxis(90f, Vector3.up);
                if (_ghostInstance != null) _ghostInstance.transform.rotation = _ghostRotation;
            }

            // Left-click commit.
            if (_placeAction != null && _placeAction.WasPerformedThisFrame())
            {
                if (!occupied)
                {
                    TryPlace(def, cell, placePos);
                }
            }
        }

        // ---------------------------------------------------------------
        //  Predicate
        // ---------------------------------------------------------------

        /// <summary>
        /// True iff the item is something the placer should ghost / place.
        /// V7.1 placeables: build blocks + machines. Machine items get a
        /// MachineCraftingStation attached on placement; build blocks do
        /// NOT — this is the V6.4 review heads-up's discriminator.
        /// </summary>
        public static bool IsPlaceable(ItemDefinition def)
        {
            if (def == null) return false;
            if (def.placedPrefab == null) return false;
            return def.isBuildBlock || def.kind == ItemKind.Machine;
        }

        // ---------------------------------------------------------------
        //  Placement
        // ---------------------------------------------------------------

        /// <summary>
        /// Spawn a real placed-prefab instance at <paramref name="cell"/>,
        /// attach <see cref="PlacedBlock"/>, register, attach
        /// <see cref="MachineCraftingStation"/> if the item is a Machine, and
        /// decrement one from the active hotbar slot. Public so tests can
        /// drive a placement without needing to simulate input.
        /// </summary>
        public PlacedBlock TryPlace(ItemDefinition def, Vector3Int cell, Vector3 placePos)
        {
            if (def == null || def.placedPrefab == null) return null;
            BlockRegistry registry = BlockRegistry.Instance;
            if (registry == null) return null;
            if (registry.IsCellOccupied(cell)) return null;

            BuildGrid grid = BuildGrid.Instance;
            if (grid != null && !grid.IsOriginSet)
            {
                grid.SetOriginIfUnset(placePos);
                // Re-resolve the cell + placePos using the now-pinned origin
                // so the very first block lands in cell (0,0,0).
                cell = grid.WorldToCell(placePos);
                BlockFormResolver.Form firstForm = BlockFormResolver.Resolve(def);
                placePos = BlockFormResolver.GetPlacementPosition(grid, cell, firstForm);
            }

            // Decrement inventory first so a failed Instantiate doesn't dupe
            // the item. If inventory mutation fails, abort the placement.
            if (!ConsumeOneFromActiveSlot(def)) return null;

            GameObject instance = Instantiate(def.placedPrefab, placePos, _ghostRotation);
            instance.name = $"{def.itemId}_placed";

            PlacedBlock block = instance.GetComponent<PlacedBlock>();
            if (block == null) block = instance.AddComponent<PlacedBlock>();
            block.OnPlaced(def.itemId, cell, _ghostRotation);

            // V6.4 review heads-up — Machine items get a MachineCraftingStation
            // wired on placement. Block items do NOT.
            if (def.kind == ItemKind.Machine)
            {
                // V9.5 — Conveyor segments are Machine-kind in items_core.json
                // but they don't behave like crafting machines (no recipes, no
                // station). Special-case them off the standard machine path.
                if (def.itemId == "conveyor_belt")
                {
                    AttachConveyorSegment(instance);
                }
                else
                {
                    AttachMachineStation(instance, def);
                }
            }

            // V8.3 — power items (generators, batteries, junctions, sinks)
            // get the right PowerNode subtype attached on placement so they
            // auto-register with the PowerNetwork.
            AttachPowerNodeIfPowerItem(instance, def);

            // V7.2 — Door form gets a hinge interaction.
            BlockFormResolver.Form form = BlockFormResolver.Resolve(def);
            if (form == BlockFormResolver.Form.Door && instance.GetComponent<DoorInteraction>() == null)
            {
                instance.AddComponent<DoorInteraction>();
            }

            registry.Register(block);
            return block;
        }

        private bool ConsumeOneFromActiveSlot(ItemDefinition def)
        {
            if (_inventory == null) return true; // tests w/o inventory still place
            int slotIdx = _inventory.SelectedHotbarIndex;
            ItemStack active = _inventory.Hotbar.GetSlot(slotIdx);
            if (active.IsEmpty || active.item != def) return false;
            int newQty = active.quantity - 1;
            _inventory.Hotbar.SetSlot(slotIdx, newQty > 0 ? new ItemStack(def, newQty) : new ItemStack(null, 0));
            return true;
        }

        private static void AttachMachineStation(GameObject instance, ItemDefinition def)
        {
            MachineRegistry mreg = MachineRegistry.Instance;
            if (mreg == null) return;
            MachineDefinition mdef = mreg.GetById(def.itemId);
            if (mdef == null) return;
            MachineCraftingStation station = instance.GetComponent<MachineCraftingStation>();
            if (station == null) station = instance.AddComponent<MachineCraftingStation>();
            station.Init(mdef);

            // V8.3 — if the machine needs power, attach a PowerConsumerNode
            // sibling so the V8 PowerNetwork can write CurrentWatts each tick.
            // MachineCraftingStation.NeedsPower flips on automatically when
            // _powerConsumer != null.
            if (mdef.needsPower)
            {
                var consumer = instance.GetComponent<PowerConsumerNode>();
                if (consumer == null) consumer = instance.AddComponent<PowerConsumerNode>();
                consumer.Configure(mdef.powerDrawWatts);
            }

            // V9.1 — attach the appropriate MachineRuntime subclass per item
            // id so the placed prefab gets its IInteractable hook + any
            // per-machine special behaviour. Falls back to the base
            // MachineRuntime when no specialised subclass is registered.
            AttachMachineRuntime(instance, mdef);
        }

        /// <summary>
        /// V9.1/V9.2/V9.5 — Attach the right <see cref="MachineRuntime"/>
        /// subclass for <paramref name="mdef"/>. Workbench is the safe
        /// default for any unhandled machine id.
        /// </summary>
        private static void AttachMachineRuntime(GameObject instance, MachineDefinition mdef)
        {
            if (instance == null || mdef == null) return;
            // If a runtime is already present (e.g. baked into the prefab),
            // honour that.
            if (instance.GetComponent<MachineRuntime>() != null) return;

            switch (mdef.itemId)
            {
                case "workbench":
                    instance.AddComponent<Voidborne.Automation.Machines.WorkbenchRuntime>();
                    break;
                case "campfire":
                    instance.AddComponent<Voidborne.Automation.Machines.CampfireRuntime>();
                    break;
                case "furnace":
                    instance.AddComponent<Voidborne.Automation.Machines.FurnaceRuntime>();
                    break;
                case "steam_boiler":
                    instance.AddComponent<Voidborne.Automation.Machines.SteamBoilerRuntime>();
                    break;
                case "steam_generator":
                    instance.AddComponent<Voidborne.Automation.Machines.SteamGeneratorRuntime>();
                    break;
                case "composter":
                    instance.AddComponent<Voidborne.Automation.Machines.ComposterRuntime>();
                    break;
                case "drying_rack":
                    instance.AddComponent<Voidborne.Automation.Machines.DryingRackRuntime>();
                    break;
                case "storage_chest":
                    instance.AddComponent<Voidborne.Automation.Machines.StorageChestRuntime>();
                    break;
                case "crusher":
                    instance.AddComponent<Voidborne.Automation.Machines.CrusherRuntime>();
                    break;
                case "inserter":
                    instance.AddComponent<Voidborne.Automation.Transport.Inserter>();
                    break;
                default:
                    // Unknown / not-yet-implemented machine id -- the base
                    // workbench runtime gives the player the standard
                    // Machine UI hookup.
                    instance.AddComponent<Voidborne.Automation.Machines.WorkbenchRuntime>();
                    break;
            }
        }

        /// <summary>
        /// V8.3 — Attach the correct <see cref="PowerNode"/> subtype for the
        /// power-item being placed. Driven by the item id (Core 60 power
        /// items are <c>steam_generator</c>, <c>hand_crank_generator</c>,
        /// <c>battery_basic</c>, <c>junction_box</c>, <c>power_sink</c>).
        /// Called by <see cref="TryPlace"/> after the placed prefab is
        /// instantiated and the PlacedBlock initialized.
        ///
        /// Note: M2 only routes Machine-kind items through this placer
        /// (<see cref="IsPlaceable"/> gate). The generator items
        /// (<c>steam_generator</c>, <c>hand_crank_generator</c>) ARE
        /// machines so they pass; battery/sink/junction are Component-kind
        /// and currently arrive via tests + V9.x extensions. Wiring them
        /// to the player-side placer is M7 polish.
        /// </summary>
        private static void AttachPowerNodeIfPowerItem(GameObject instance, ItemDefinition def)
        {
            if (instance == null || def == null) return;
            string id = def.itemId;
            if (string.IsNullOrEmpty(id)) return;

            switch (id)
            {
                case "steam_generator":
                    if (instance.GetComponent<SteamGenerator>() == null)
                        instance.AddComponent<SteamGenerator>();
                    break;
                case "hand_crank_generator":
                    if (instance.GetComponent<HandCrankGenerator>() == null)
                        instance.AddComponent<HandCrankGenerator>();
                    break;
                case "battery_basic":
                    if (instance.GetComponent<Battery>() == null)
                        instance.AddComponent<Battery>();
                    break;
                case "junction_box":
                    if (instance.GetComponent<JunctionBox>() == null)
                        instance.AddComponent<JunctionBox>();
                    break;
                case "power_sink":
                    if (instance.GetComponent<PowerSink>() == null)
                        instance.AddComponent<PowerSink>();
                    break;
            }
        }

        /// <summary>
        /// V9.5 — Attach a per-segment <see cref="Voidborne.Automation.Transport.ConveyorBelt"/>
        /// + a configured <see cref="PowerNode"/> (consumer role, 10W draw)
        /// to the placed prefab. Each segment is its own consumer so a long
        /// belt's draw aggregates naturally.
        /// </summary>
        private static void AttachConveyorSegment(GameObject instance)
        {
            if (instance == null) return;
            var belt = instance.GetComponent<Voidborne.Automation.Transport.ConveyorBelt>();
            if (belt == null) belt = instance.AddComponent<Voidborne.Automation.Transport.ConveyorBelt>();
            belt.EnsurePowerConsumerNode();
        }

        // ---------------------------------------------------------------
        //  Ghost
        // ---------------------------------------------------------------

        private void EnsureGhost(ItemDefinition def, Vector3 worldPos)
        {
            if (_ghostInstance != null && _ghostForItem == def)
            {
                _ghostInstance.transform.position = worldPos;
                _ghostInstance.transform.rotation = _ghostRotation;
                return;
            }
            DestroyGhost();
            if (def == null || def.placedPrefab == null) return;
            _ghostInstance = Instantiate(def.placedPrefab, worldPos, _ghostRotation);
            _ghostInstance.name = $"Ghost_{def.itemId}";
            _ghostForItem = def;

            // Strip every collider so the ghost doesn't block raycasts /
            // physics, and tint the materials translucent.
            foreach (Collider c in _ghostInstance.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
            foreach (Renderer r in _ghostInstance.GetComponentsInChildren<Renderer>(true))
            {
                // Use an instanced material so we don't mutate the source asset.
                var mat = r.material;
                mat.color = ghostTint;
                // Best-effort transparency hook for the URP / Standard lit
                // shaders; falls back silently on shaders without a _Color.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ghostTint);
            }
        }

        private void ApplyGhostTint(Color tint)
        {
            if (_ghostInstance == null) return;
            foreach (Renderer r in _ghostInstance.GetComponentsInChildren<Renderer>(true))
            {
                var mat = r.material;
                mat.color = tint;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            }
        }

        private void DestroyGhost()
        {
            if (_ghostInstance != null)
            {
                if (Application.isPlaying) Destroy(_ghostInstance);
                else DestroyImmediate(_ghostInstance);
            }
            _ghostInstance = null;
            _ghostForItem = null;
            _ghostBlocked = false;
        }
    }
}
