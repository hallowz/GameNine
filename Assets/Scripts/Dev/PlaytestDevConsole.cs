using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Voidborne.Automation;
using Voidborne.Building;
using Voidborne.Player;
using Voidborne.Power;
using Voidborne.World.Chunks;

namespace Voidborne.Dev
{
    /// <summary>
    /// M2 Playtest — F-key dev hotkeys layered ON TOP of the existing
    /// player input bindings. None of these keys collide with WASD /
    /// mouse / Tab / Esc / E / J / B / number-row hotbar / F10 terrain
    /// leveler.
    ///
    /// Bindings:
    /// <list type="bullet">
    /// <item><c>F1</c> — toggle help overlay (controls + canonical milk loop).</item>
    /// <item><c>F2</c> — refresh starter loadout (re-grant items).</item>
    /// <item><c>F3</c> — spawn a 'boiler rig' near the player (boiler + generator + battery + power_sink wired).</item>
    /// <item><c>F4</c> — teleport the player to (0, 80, 0) and snap to surface.</item>
    /// <item><c>F5</c> — toggle fly mode (CharacterController-friendly noclip lift).</item>
    /// <item><c>F12</c> — toggle debug overlay (FPS, chunk count, PowerNodes, PlacedBlocks).</item>
    /// </list>
    ///
    /// Coop note: every binding is client-local (UI / debug / fly mode).
    /// F2 refreshes the local owner's loadout; F3 spawns world entities
    /// owner-authoritative for now (V21 will gate placements server-side).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaytestDevConsole : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Tooltip("Player GameObject. Auto-resolved if blank (looks for PlayerManager singleton).")]
        [SerializeField] private GameObject playerGameObject;

        [Tooltip("Fly mode upward thrust applied while Space is held.")]
        [SerializeField] private float flyVerticalSpeed = 8f;

        // Planar (WASD) movement is delegated to FirstPersonController so the
        // controller's existing acceleration / friction curve stays consistent
        // whether fly mode is on or off — we only override vertical thrust here.

        // ---------------------------------------------------------------
        //  Runtime
        // ---------------------------------------------------------------

        private Canvas _devCanvas;
        private GameObject _helpPanel;
        private GameObject _debugPanel;
        private Text _debugText;

        private bool _helpVisible;
        private bool _debugVisible;
        private bool _flyMode;

        private float _fpsAccum;
        private int   _fpsFrames;
        private float _fpsTimer;
        private float _cachedFps;

        // Fly mode state
        private CharacterController _controller;
        private FirstPersonController _fps;

        // Cached references
        private PlaytestLoadout _loadout;
        private PlaytestSpawnSafety _spawnSafety;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            ResolvePlayer();
            BuildDevCanvas();
            BuildHelpPanel();
            BuildDebugPanel();
        }

        private void ResolvePlayer()
        {
            if (playerGameObject == null && PlayerManager.Instance != null)
                playerGameObject = PlayerManager.Instance.gameObject;
            if (playerGameObject == null)
                playerGameObject = GameObject.FindGameObjectWithTag("Player");

            if (playerGameObject != null)
            {
                _controller  = playerGameObject.GetComponent<CharacterController>();
                _fps         = playerGameObject.GetComponent<FirstPersonController>();
                _loadout     = playerGameObject.GetComponent<PlaytestLoadout>();
                _spawnSafety = playerGameObject.GetComponent<PlaytestSpawnSafety>();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Re-resolve once if the player was missing (e.g., spawned late)
            if (playerGameObject == null) ResolvePlayer();

            HandleHotkeys();
            UpdateFps();
            UpdateFlyMode();
            UpdateDebugOverlay();
        }

        // ---------------------------------------------------------------
        //  Hotkey dispatch
        // ---------------------------------------------------------------

        private void HandleHotkeys()
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
                ToggleHelp();

            if (Keyboard.current.f2Key.wasPressedThisFrame)
                RefreshLoadout();

            if (Keyboard.current.f3Key.wasPressedThisFrame)
                SpawnBoilerRig();

            if (Keyboard.current.f4Key.wasPressedThisFrame)
                TeleportHome();

            if (Keyboard.current.f5Key.wasPressedThisFrame)
                ToggleFlyMode();

            if (Keyboard.current.f12Key.wasPressedThisFrame)
                ToggleDebug();
        }

        // ---------------------------------------------------------------
        //  F1 — help overlay
        // ---------------------------------------------------------------

        public void ToggleHelp()
        {
            _helpVisible = !_helpVisible;
            if (_helpPanel != null) _helpPanel.SetActive(_helpVisible);
        }

        // ---------------------------------------------------------------
        //  F2 — refresh loadout
        // ---------------------------------------------------------------

        public void RefreshLoadout()
        {
            if (_loadout != null)
            {
                _loadout.Refresh();
                Debug.Log("[PlaytestDevConsole] F2 — starter loadout refreshed.");
            }
            else
            {
                Debug.LogWarning("[PlaytestDevConsole] F2 — PlaytestLoadout not found on player.");
            }
        }

        // ---------------------------------------------------------------
        //  F3 — boiler rig
        // ---------------------------------------------------------------

        /// <summary>
        /// Spawn a steam_boiler + steam_generator (adjacent) + battery_basic
        /// + power_sink near the player and wire them with cable segments.
        /// Uses the same low-level path as the M2 acceptance test: direct
        /// Instantiate of the placed prefabs + manual PlacedBlock registration
        /// + ConfigurePowerNode + CableSegment.Init.
        /// </summary>
        public void SpawnBoilerRig()
        {
            if (playerGameObject == null)
            {
                Debug.LogWarning("[PlaytestDevConsole] F3 — no player found.");
                return;
            }

            ItemDatabase db = ItemDatabase.GetOrLoad();
            if (db == null)
            {
                Debug.LogWarning("[PlaytestDevConsole] F3 — ItemDatabase not loaded.");
                return;
            }

            ItemDefinition boilerDef = db.GetItem("steam_boiler");
            ItemDefinition genDef    = db.GetItem("steam_generator");
            ItemDefinition battDef   = db.GetItem("battery_basic");
            ItemDefinition sinkDef   = db.GetItem("power_sink");

            // Boiler and generator must have placedPrefabs (they're machine-kind).
            // Battery and Sink are component-kind in the Core 60 and currently
            // ship without a placedPrefab — F3 builds primitive hosts for them
            // (mirrors PowerNetworkTests.AddPowerComponent).
            if (boilerDef == null || genDef == null || battDef == null || sinkDef == null ||
                boilerDef.placedPrefab == null || genDef.placedPrefab == null)
            {
                Debug.LogWarning("[PlaytestDevConsole] F3 — one or more rig items / placedPrefabs missing in ItemDatabase.");
                return;
            }

            // Anchor the rig 3m in front of the player, snapped to integer cells.
            Vector3 forward = playerGameObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 anchor = playerGameObject.transform.position + forward * 3f;
            Vector3Int anchorCell = new Vector3Int(Mathf.RoundToInt(anchor.x), Mathf.RoundToInt(anchor.y), Mathf.RoundToInt(anchor.z));

            // Ensure BuildGrid origin is pinned so subsequent placements snap consistently.
            BuildGrid grid = BuildGrid.Instance;
            Vector3 anchorWorld = new Vector3(anchorCell.x, anchorCell.y, anchorCell.z);
            if (grid != null && !grid.IsOriginSet)
                grid.SetOriginIfUnset(anchorWorld);

            // Layout (one cell apart, all on the same Y plane):
            //   boiler -> generator -> battery -> sink (line)
            Vector3Int cBoiler = anchorCell;
            Vector3Int cGen    = anchorCell + new Vector3Int(1, 0, 0);
            Vector3Int cBatt   = anchorCell + new Vector3Int(2, 0, 0);
            Vector3Int cSink   = anchorCell + new Vector3Int(3, 0, 0);

            GameObject boilerGo = PlaceMachine(boilerDef, cBoiler);
            GameObject genGo    = PlaceMachine(genDef,    cGen);
            GameObject battGo   = PlaceMachine(battDef,   cBatt);
            GameObject sinkGo   = PlaceMachine(sinkDef,   cSink);

            if (boilerGo == null || genGo == null || battGo == null || sinkGo == null)
            {
                Debug.LogWarning("[PlaytestDevConsole] F3 — one or more machines failed to spawn (likely cell occupied).");
                return;
            }

            // Wire generator <-> battery <-> sink with cable segments.
            // Boiler doesn't need power (it's a thermal machine); the
            // generator's IsBoilerActive scans BlockRegistry adjacency.
            PowerNode genNode  = genGo.GetComponent<PowerNode>();
            PowerNode battNode = battGo.GetComponent<PowerNode>();
            PowerNode sinkNode = sinkGo.GetComponent<PowerNode>();

            if (genNode != null && battNode != null)
                SpawnCable(genNode, battNode, "rig_gen_to_batt");
            if (battNode != null && sinkNode != null)
                SpawnCable(battNode, sinkNode, "rig_batt_to_sink");

            Debug.Log($"[PlaytestDevConsole] F3 — boiler rig spawned at {anchorCell}. " +
                      "Drop milk + coal into the boiler (interact with E) to start the loop.");
        }

        private GameObject PlaceMachine(ItemDefinition def, Vector3Int cell)
        {
            if (def == null) return null;
            BlockRegistry registry = BlockRegistry.Instance;
            if (registry == null) return null;
            if (registry.IsCellOccupied(cell)) return null;

            Vector3 placePos = new Vector3(cell.x, cell.y, cell.z);
            GameObject instance;
            if (def.placedPrefab != null)
            {
                instance = Instantiate(def.placedPrefab, placePos, Quaternion.identity);
            }
            else
            {
                // Component-kind power items (battery_basic, power_sink) have
                // no placedPrefab in the Core 60. Build a minimal cube host so
                // the PowerNode and PlacedBlock have a transform to live on.
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.position = placePos;
                instance.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            }
            instance.name = $"{def.itemId}_rig";

            PlacedBlock block = instance.GetComponent<PlacedBlock>();
            if (block == null) block = instance.AddComponent<PlacedBlock>();
            block.OnPlaced(def.itemId, cell, Quaternion.identity);

            // Attach the right runtime / power node for the item id. We
            // mirror BlockPlacer's dispatch — it's safer than rewiring
            // private statics and matches the M2 test path exactly.
            switch (def.itemId)
            {
                case "steam_boiler":
                    if (instance.GetComponent<MachineCraftingStation>() == null)
                    {
                        var st = instance.AddComponent<MachineCraftingStation>();
                        MachineRegistry mreg = MachineRegistry.Instance;
                        if (mreg != null)
                        {
                            var mdef = mreg.GetById(def.itemId);
                            if (mdef != null) st.Init(mdef);
                        }
                    }
                    if (instance.GetComponent<Voidborne.Automation.Machines.SteamBoilerRuntime>() == null)
                        instance.AddComponent<Voidborne.Automation.Machines.SteamBoilerRuntime>();
                    break;
                case "steam_generator":
                    if (instance.GetComponent<SteamGenerator>() == null)
                        instance.AddComponent<SteamGenerator>();
                    if (instance.GetComponent<Voidborne.Automation.Machines.SteamGeneratorRuntime>() == null)
                        instance.AddComponent<Voidborne.Automation.Machines.SteamGeneratorRuntime>();
                    break;
                case "battery_basic":
                    if (instance.GetComponent<Battery>() == null)
                        instance.AddComponent<Battery>();
                    break;
                case "power_sink":
                    if (instance.GetComponent<PowerSink>() == null)
                        instance.AddComponent<PowerSink>();
                    break;
            }

            registry.Register(block);
            return instance;
        }

        private void SpawnCable(PowerNode a, PowerNode b, string name)
        {
            if (a == null || b == null) return;
            GameObject cableGo = new GameObject(name);
            cableGo.transform.position = Vector3.Lerp(a.transform.position, b.transform.position, 0.5f);
            CableSegment seg = cableGo.AddComponent<CableSegment>();
            seg.Init(a, b, PowerCableTier.T1);
        }

        // ---------------------------------------------------------------
        //  F4 — teleport home
        // ---------------------------------------------------------------

        public void TeleportHome()
        {
            if (playerGameObject == null) return;

            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null) _controller.enabled = false;
            playerGameObject.transform.position = new Vector3(0f, 80f, 0f);
            if (_controller != null && wasEnabled) _controller.enabled = true;

            if (_spawnSafety != null) _spawnSafety.SnapNow();

            Debug.Log("[PlaytestDevConsole] F4 — teleported to (0, 80, 0); spawn-safety retrying.");
        }

        // ---------------------------------------------------------------
        //  F5 — fly mode
        // ---------------------------------------------------------------

        public void ToggleFlyMode()
        {
            _flyMode = !_flyMode;
            Debug.Log($"[PlaytestDevConsole] F5 — fly mode {(_flyMode ? "ON" : "OFF")}. " +
                      "Space = up, LeftCtrl = down. WASD planar movement still works.");
        }

        private void UpdateFlyMode()
        {
            if (!_flyMode) return;
            if (playerGameObject == null || _controller == null) return;

            float dy = 0f;
            if (Keyboard.current.spaceKey.isPressed)     dy += flyVerticalSpeed;
            if (Keyboard.current.leftCtrlKey.isPressed)  dy -= flyVerticalSpeed;

            if (Mathf.Abs(dy) > 0.001f)
                _controller.Move(new Vector3(0f, dy * Time.deltaTime, 0f));
        }

        // ---------------------------------------------------------------
        //  F12 — debug overlay
        // ---------------------------------------------------------------

        public void ToggleDebug()
        {
            _debugVisible = !_debugVisible;
            if (_debugPanel != null) _debugPanel.SetActive(_debugVisible);
        }

        private void UpdateFps()
        {
            _fpsFrames++;
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _cachedFps = _fpsFrames / _fpsAccum;
                _fpsFrames = 0;
                _fpsAccum  = 0f;
                _fpsTimer  = 0f;
            }
        }

        private void UpdateDebugOverlay()
        {
            if (!_debugVisible || _debugText == null) return;

            int activeChunks = 0;
            if (ChunkManager.Instance != null)
                activeChunks = ChunkManager.Instance.ActiveChunkObjectCount;

            // PowerNetwork doesn't expose a node count via a public getter, so
            // we fall back to a scene scan — cheap enough for a dev overlay.
            int powerNodes = FindObjectsByType<PowerNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

            int placedBlocks = BlockRegistry.Instance != null ? BlockRegistry.Instance.Count : 0;

            Vector3 ppos = playerGameObject != null ? playerGameObject.transform.position : Vector3.zero;

            _debugText.text =
                $"FPS: {_cachedFps:F0}\n" +
                $"Pos: ({ppos.x:F1}, {ppos.y:F1}, {ppos.z:F1})\n" +
                $"Active chunks: {activeChunks}\n" +
                $"PowerNodes: {powerNodes}\n" +
                $"PlacedBlocks: {placedBlocks}\n" +
                $"Fly mode: {(_flyMode ? "ON" : "OFF")}";
        }

        // ---------------------------------------------------------------
        //  Canvas + panels
        // ---------------------------------------------------------------

        private void BuildDevCanvas()
        {
            GameObject canvasGo = new GameObject("PlaytestDevCanvas");
            canvasGo.transform.SetParent(transform, false);

            _devCanvas = canvasGo.AddComponent<Canvas>();
            _devCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _devCanvas.sortingOrder = 500; // above the main UI

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        private void BuildHelpPanel()
        {
            _helpPanel = new GameObject("HelpOverlay", typeof(RectTransform));
            _helpPanel.transform.SetParent(_devCanvas.transform, false);

            RectTransform rt = _helpPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 560f);
            rt.anchoredPosition = Vector2.zero;

            Image bg = _helpPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_helpPanel.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 20f);
            textRt.offsetMax = new Vector2(-20f, -20f);

            Text txt = textGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 16;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.UpperLeft;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = BuildHelpText();

            _helpPanel.SetActive(false);
        }

        private static string BuildHelpText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("VOIDBORNE — M2 PLAYTEST HELP   (F1 to close)");
            sb.AppendLine();
            sb.AppendLine("MOVEMENT");
            sb.AppendLine("  WASD     Walk     |  Shift   Sprint");
            sb.AppendLine("  Space    Jump     |  Ctrl    Crouch");
            sb.AppendLine();
            sb.AppendLine("INTERACTION");
            sb.AppendLine("  Tab / I  Inventory     |  E       Interact / open machine");
            sb.AppendLine("  B        Open backpack |  J       Quest log");
            sb.AppendLine("  Esc      Pause / close");
            sb.AppendLine("  LMB      Mine / chop / place block / arm cable");
            sb.AppendLine("  RMB      Rotate ghost / pickup backpack");
            sb.AppendLine("  1..5     Hotbar select");
            sb.AppendLine();
            sb.AppendLine("DEV HOTKEYS (F-row)");
            sb.AppendLine("  F1       Toggle this help overlay");
            sb.AppendLine("  F2       Refresh starter loadout");
            sb.AppendLine("  F3       Spawn boiler rig in front of player");
            sb.AppendLine("  F4       Teleport to (0,80,0); snap to surface");
            sb.AppendLine("  F5       Toggle fly mode (Space up, LeftCtrl down)");
            sb.AppendLine("  F10      Terrain leveler (carve flat platform)");
            sb.AppendLine("  F12      Toggle debug overlay (FPS / chunks / power)");
            sb.AppendLine();
            sb.AppendLine("CANONICAL M2 TEST — Milk -> Boiler -> Power");
            sb.AppendLine("  1. Tab. Select steam_boiler in hotbar. Place it.");
            sb.AppendLine("  2. Place a steam_generator adjacent (1 cell over).");
            sb.AppendLine("  3. Place a battery_basic and a power_sink nearby.");
            sb.AppendLine("  4. Select copper_cable_t1. Click generator -> battery, battery -> sink.");
            sb.AppendLine("  5. Place a hand_crank_generator and crank it (E + hold) to bootstrap.");
            sb.AppendLine("  6. Walk up to the boiler. Press E. Pick the boil recipe.");
            sb.AppendLine("     Drop COAL + WATER into the input slots. It should start.");
            sb.AppendLine("  7. Replace water with MILK. The recipe still runs but at 0.4x");
            sb.AppendLine("     efficiency. The tooltip says 'Substituted milk -> Liquid_Aqueous'.");
            sb.AppendLine("  8. Smile.");
            sb.AppendLine();
            sb.AppendLine("Shortcut: press F3 to spawn a pre-wired boiler rig instead of step 1-4.");
            return sb.ToString();
        }

        private void BuildDebugPanel()
        {
            _debugPanel = new GameObject("DebugOverlay", typeof(RectTransform));
            _debugPanel.transform.SetParent(_devCanvas.transform, false);

            RectTransform rt = _debugPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(280f, 140f);
            rt.anchoredPosition = new Vector2(-10f, -10f);

            Image bg = _debugPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_debugPanel.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 8f);
            textRt.offsetMax = new Vector2(-8f, -8f);

            _debugText = textGo.AddComponent<Text>();
            _debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _debugText.fontSize = 13;
            _debugText.color    = new Color(0.85f, 1f, 0.85f, 1f);
            _debugText.alignment = TextAnchor.UpperLeft;
            _debugText.verticalOverflow = VerticalWrapMode.Overflow;
            _debugText.text = "FPS: --";

            _debugPanel.SetActive(false);
        }
    }
}

