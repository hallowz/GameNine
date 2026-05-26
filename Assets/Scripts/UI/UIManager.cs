using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Voidborne;
using Voidborne.Automation;
using Voidborne.Player;
using Voidborne.UI;
using Voidborne.Vehicles;

/// <summary>
/// Bootstraps the entire in-game UI: hotbar, inventory panel, tooltip, crafting panel.
/// Handles Tab/I to toggle inventory, Escape to close crafting station, and cursor lock/unlock.
///
/// Also owns the shared InventoryCursor and a floating cursor-item image that tracks
/// the mouse position while an item is held.
///
/// Place on a persistent GameObject in the Game scene.
/// </summary>
public class UIManager : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Singleton
    // ---------------------------------------------------------------
    public static UIManager Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Cursor (shared Minecraft-style pick-up/place state)
    // ---------------------------------------------------------------
    public InventoryCursor Cursor { get; private set; } = new InventoryCursor();

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------
    private bool _inventoryOpen;
    private bool _craftingStationOpen;
    private bool _furnaceOpen;
    private bool _chestOpen;
    private bool _assemblerOpen;
    private bool _terminalOpen;
    private bool _vehicleWorkbenchOpen;
    private bool _vehicleCargoOpen;
    private bool _questLogOpen;
    private bool _dialogueOpen;
    private bool _pauseMenuOpen;

    // ---------------------------------------------------------------
    //  UI Components
    // ---------------------------------------------------------------
    private Canvas       _canvas;
    private HotbarUI     _hotbarUI;
    private StaminaBarUI _staminaBarUI;
    private InventoryUI  _inventoryUI;
    private CraftingUI   _craftingUI;
    private FurnaceUI    _furnaceUI;
    private ChestUI      _chestUI;
    private BackpackUI   _backpackUI;
    private Voidborne.UI.AssemblerUI _assemblerUI;
    private Voidborne.UI.TerminalUI  _terminalUI;
    private Voidborne.UI.VehicleAssemblyUI _vehicleAssemblyUI;
    private Voidborne.UI.VehicleHUD        _vehicleHUD;
    private VehicleFrame[] _registeredFrames;
    private QuestTrackerUI _questTrackerUI;
    private QuestLogUI     _questLogUI;
    private DialogueUI     _dialogueUI;
    private DialogueRunner _dialogueRunner;
    private GameObject     _pauseMenuPanel;

    // Floating cursor-item image
    private Image         _cursorImage;
    private RectTransform _rootCanvasRect;

    // Bracer integration
    private IndexBracerController _bracer;
    private Transform _inventoryOverlayParent;
    private Transform _craftingOverlayParent;
    private Vector2   _inventoryOverlayAnchorPos;
    private Vector2   _craftingOverlayAnchorPos;

    // ---------------------------------------------------------------
    //  Player component references (cached in Start)
    // ---------------------------------------------------------------
    private FirstPersonController    _fpsController;
    private FirstPersonCamera        _fpsCamera;
    private PlayerTerrainInteraction _terrainInteraction;
    private PersonalCraftingGrid     _personalCraftingGrid;

    // ---------------------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildCanvas();
        BuildHotbar();
        BuildStaminaBar();
        BuildAmmoUI();
        BuildInventoryPanel();
        BuildCraftingPanel();
        BuildFurnacePanel();
        BuildChestPanel();
        BuildBackpackPanel();
        BuildAssemblerPanel();
        BuildTerminalPanel();
        BuildVehicleWorkbenchPanel();
        BuildVehicleHUD();
        BuildQuestUI();
        BuildDialogueUI();
        BuildTooltip();
        BuildCursorItem();
        BuildPauseMenu();
    }

    private void Start()
    {
        // Find PlayerInventory (on Player GameObject)
        PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogWarning("[UIManager] No PlayerInventory found in scene. UI will not be wired.");
            return;
        }

        _hotbarUI.Init(playerInventory);
        _inventoryUI.Init(playerInventory);
        _craftingUI.Init(playerInventory);

        // Cache player components so we can enable/disable them on inventory toggle
        _fpsController        = FindFirstObjectByType<FirstPersonController>();
        _fpsCamera            = FindFirstObjectByType<FirstPersonCamera>();
        _terrainInteraction   = FindFirstObjectByType<PlayerTerrainInteraction>();
        _personalCraftingGrid = FindFirstObjectByType<PersonalCraftingGrid>();

        if (_personalCraftingGrid == null)
            Debug.LogWarning("[UIManager] No PersonalCraftingGrid found on Player — personal crafting will not be available in the inventory panel.");

        if (_fpsController != null)
            _staminaBarUI.Init(_fpsController);
        else
            Debug.LogWarning("[UIManager] FirstPersonController not found — stamina bar will not update.");

        if (_fpsCamera == null)
            Debug.LogWarning("[UIManager] FirstPersonCamera not found — camera will not be locked on inventory open.");
        if (_terrainInteraction == null)
            Debug.LogWarning("[UIManager] PlayerTerrainInteraction not found — terrain interaction will not be locked on inventory open.");

        // Wire bracer integration (hotbar on bracer screen, messages, foldout hooks)
        // Runs as coroutine because IndexBracerController.Start() is also a coroutine
        // that waits for PlayerManager before building — we need to wait for it.
        StartCoroutine(WireBracerIntegrationDeferred());

        // Gameplay starts with cursor locked
        SetCursorLocked(true);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Escape: close open UI, or toggle pause menu
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_pauseMenuOpen)
                ClosePauseMenu();
            else if (_inventoryOpen)
                ToggleInventory();
            else if (_questLogOpen)
                CloseQuestLog();
            else
                OpenPauseMenu();
        }

        // Block all other input while paused
        if (_pauseMenuOpen) return;

        bool togglePressed = Keyboard.current.tabKey.wasPressedThisFrame
                          || Keyboard.current.iKey.wasPressedThisFrame;

        if (togglePressed) ToggleInventory();

        // J key — toggle quest log
        if (Keyboard.current.jKey.wasPressedThisFrame)
            ToggleQuestLog();

        // B key — open worn backpack (only when inventory is NOT open)
        if (Keyboard.current.bKey.wasPressedThisFrame && !_inventoryOpen)
            HandleBKey();

        // Right mouse button — open backpack from hotbar (only when no UI is open)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !IsAnyUIOpen)
            HandleHotbarRightClick();

        // Update floating cursor-item image position and visibility
        UpdateCursorItemImage();
    }

    private void LateUpdate()
    {
        // Safety net in LateUpdate: Unity's built-in ESC handling unlocks the cursor
        // after Update(), so we must re-lock here to prevent a visible cursor flicker.
        if (!IsAnyUIOpen && UnityEngine.Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLocked(true);
            if (_fpsCamera != null)          _fpsCamera.enabled          = true;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = true;
        }
    }

    // ---------------------------------------------------------------
    //  Toggle
    // ---------------------------------------------------------------

    public void ToggleInventory()
    {
        _inventoryOpen = !_inventoryOpen;

        if (_inventoryOpen)
        {
            _inventoryUI.Show();

            // Open bracer foldout (diegetic animation)
            if (_bracer != null)
            {
                _bracer.OpenFoldout();
                ReparentToBracer();
            }

            // Show personal crafting grid alongside the inventory panel
            // (only when no crafting station or furnace is already open)
            if (_personalCraftingGrid != null && !_craftingStationOpen && !_furnaceOpen)
                _craftingUI.Open(null, _personalCraftingGrid.Grid, OnTakeResultFromStation);

            SetCursorLocked(false);

            // Disable camera look and terrain editing while inventory is open
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
        else
        {
            // Return any held item before closing
            if (Cursor.IsHolding)
                Cursor.CancelAndReturn();

            _inventoryUI.Hide();

            // Return UIs to overlay before closing foldout
            if (_bracer != null)
            {
                ReparentToOverlay();
                _bracer.CloseFoldout();
            }

            // Close backpack panel when inventory closes
            if (_backpackUI != null && _backpackUI.IsOpen)
                _backpackUI.Close();

            // Close all attached panels
            if (_chestOpen)
            {
                _chestOpen = false;
                _chestUI.Close();
            }
            if (_furnaceOpen)
            {
                _furnaceOpen = false;
                _furnaceUI.Close();
            }
            if (_assemblerOpen)
            {
                _assemblerOpen = false;
                _assemblerUI.Close();
            }
            if (_terminalOpen)
            {
                _terminalOpen = false;
                _terminalUI.Close();
            }
            if (_craftingStationOpen)
            {
                _craftingStationOpen = false;
                _craftingUI.Close();
            }
            else
            {
                _craftingUI.Close(); // close personal crafting
            }

            SetCursorLocked(true);

            // Re-enable camera look and terrain editing when inventory closes
            if (_fpsCamera != null)          _fpsCamera.enabled          = true;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = true;
        }
    }

    public bool IsInventoryOpen => _inventoryOpen;

    /// <summary>True when any UI panel (inventory or crafting station) is blocking gameplay input.</summary>
    public bool IsAnyUIOpen => _inventoryOpen || _craftingStationOpen || _furnaceOpen || _chestOpen || _assemblerOpen || _terminalOpen || _vehicleWorkbenchOpen || _vehicleCargoOpen || _questLogOpen || _dialogueOpen || _pauseMenuOpen;

    // ---------------------------------------------------------------
    //  Crafting Station
    // ---------------------------------------------------------------

    public void OpenCraftingStation(CraftingStation station)
    {
        if (_craftingStationOpen) CloseCraftingStation();

        _craftingStationOpen = true;
        _craftingUI.Open(station, station.Grid, OnTakeResultFromStation);

        // Crafting station always opens alongside the inventory
        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseCraftingStation()
    {
        if (!_craftingStationOpen) return;
        _craftingStationOpen = false;

        if (Cursor.IsHolding)
            Cursor.CancelAndReturn();

        _craftingUI.Close();
        // Don't restore cursor/camera — inventory stays open
    }

    // ---------------------------------------------------------------
    //  Furnace
    // ---------------------------------------------------------------

    public void OpenFurnace(FurnaceBlock furnace)
    {
        if (_furnaceOpen) CloseFurnace();

        _furnaceOpen = true;
        _furnaceUI.Open(furnace);

        // Close personal crafting — furnace panel takes its place
        if (!_craftingStationOpen)
            _craftingUI.Close();

        // Furnace always opens alongside the inventory
        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)         _fpsCamera.enabled         = false;
            if (_terrainInteraction != null) _terrainInteraction.enabled = false;
        }
    }

    public void CloseFurnace()
    {
        if (!_furnaceOpen) return;
        _furnaceOpen = false;

        if (Cursor.IsHolding)
            Cursor.CancelAndReturn();

        _furnaceUI.Close();
        // Don't restore cursor/camera — inventory stays open
    }

    // ---------------------------------------------------------------
    //  Chest
    // ---------------------------------------------------------------

    public void OpenChest(ChestBlock chest)
    {
        if (_chestOpen) CloseChest();

        _chestOpen = true;
        PlayerInventory playerInv = FindFirstObjectByType<PlayerInventory>();
        _chestUI.Open(chest, playerInv);

        // Chest always opens alongside the inventory
        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseChest()
    {
        if (!_chestOpen) return;
        _chestOpen = false;

        if (Cursor.IsHolding)
            Cursor.CancelAndReturn();

        _chestUI.Close();
        // Don't restore cursor/camera — inventory stays open
    }

    public bool IsChestOpen(ChestBlock chest) =>
        _chestOpen && _chestUI.CurrentChest == chest;

    // ---------------------------------------------------------------
    //  Assembler
    // ---------------------------------------------------------------

    public void OpenAssembler(Voidborne.Automation.Assembler assembler)
    {
        if (_assemblerOpen) CloseAssembler();

        _assemblerOpen = true;
        _assemblerUI.Open(assembler);

        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseAssembler()
    {
        if (!_assemblerOpen) return;
        _assemblerOpen = false;

        if (Cursor.IsHolding)
            Cursor.CancelAndReturn();

        _assemblerUI.Close();
    }

    // ---------------------------------------------------------------
    //  Terminal
    // ---------------------------------------------------------------

    public void OpenTerminal(Voidborne.Automation.ComputerTerminal terminal)
    {
        if (_terminalOpen) CloseTerminal();

        _terminalOpen = true;
        _terminalUI.Open(terminal);

        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseTerminal()
    {
        if (!_terminalOpen) return;
        _terminalOpen = false;

        if (Cursor.IsHolding)
            Cursor.CancelAndReturn();

        _terminalUI.Close();
    }

    // ---------------------------------------------------------------
    //  Vehicle Workbench
    // ---------------------------------------------------------------

    public void RegisterVehicleFrames(VehicleFrame[] frames) => _registeredFrames = frames;

    public void OpenVehicleWorkbench(Voidborne.Vehicles.VehicleWorkbench workbench)
    {
        if (_vehicleWorkbenchOpen) CloseVehicleWorkbench();

        _vehicleWorkbenchOpen = true;
        _vehicleAssemblyUI.Open(workbench, _registeredFrames);

        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseVehicleWorkbench()
    {
        if (!_vehicleWorkbenchOpen) return;
        _vehicleWorkbenchOpen = false;
        _vehicleAssemblyUI.Close();
    }

    // ---------------------------------------------------------------
    //  Vehicle Cargo
    // ---------------------------------------------------------------

    public void OpenVehicleCargo(Inventory cargoInventory)
    {
        if (cargoInventory == null) return;
        _vehicleCargoOpen = true;
        // Reuse the chest UI to display vehicle cargo (same slot-grid pattern)
        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseVehicleCargo()
    {
        if (!_vehicleCargoOpen) return;
        _vehicleCargoOpen = false;
    }

    // ---------------------------------------------------------------
    //  Backpack
    // ---------------------------------------------------------------

    private PlayerInventory _cachedPlayerInventory;

    private PlayerInventory GetPlayerInventory()
    {
        if (_cachedPlayerInventory == null)
            _cachedPlayerInventory = FindFirstObjectByType<PlayerInventory>();
        return _cachedPlayerInventory;
    }

    public void OpenBackpack(BackpackInstance instance)
    {
        if (instance == null) return;
        _backpackUI.Open(instance);

        // Always open the player inventory alongside the backpack
        if (!_inventoryOpen)
        {
            _inventoryOpen = true;
            _inventoryUI.Show();
            SetCursorLocked(false);
            if (_fpsCamera != null)          _fpsCamera.enabled          = false;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
        }
    }

    public void CloseBackpack()
    {
        _backpackUI.Close();
    }

    public bool IsBackpackOpen => _backpackUI != null && _backpackUI.IsOpen;

    private void HandleBKey()
    {
        PlayerInventory playerInv = GetPlayerInventory();
        if (playerInv == null) return;

        BackpackInstance inst = playerInv.WornBackpackInstance;
        if (inst == null) return;

        OpenBackpack(inst);
    }

    private void HandleHotbarRightClick()
    {
        PlayerInventory playerInv = GetPlayerInventory();
        if (playerInv == null) return;

        ItemStack active = playerInv.ActiveHotbarItem;
        if (active.IsEmpty || !(active.item is BackpackItem)) return;

        BackpackInstance inst = playerInv.GetOrCreateHotbarBackpackInstance(playerInv.SelectedHotbarIndex);
        OpenBackpack(inst);
    }

    private void OnTakeResultFromStation(ItemStack result)
    {
        if (result.IsEmpty) return;
        PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null) return;

        // Try hotbar first, then main
        if (!TryAddToInventory(playerInventory.Hotbar, result))
            TryAddToInventory(playerInventory.Main, result);

        // Notify quest system of crafted item
        QuestEventBridge.RaiseItemCrafted(result.item, result.quantity);
    }

    // ---------------------------------------------------------------
    //  Quest Log
    // ---------------------------------------------------------------

    public void ToggleQuestLog()
    {
        if (_questLogOpen) CloseQuestLog();
        else               OpenQuestLog();
    }

    public void OpenQuestLog()
    {
        if (_questLogUI == null) return;
        _questLogOpen = true;
        _questLogUI.Show();
        SetCursorLocked(false);
        if (_fpsCamera != null)         _fpsCamera.enabled         = false;
        if (_terrainInteraction != null) _terrainInteraction.enabled = false;
    }

    public void CloseQuestLog()
    {
        if (_questLogUI == null) return;
        _questLogOpen = false;
        _questLogUI.Hide();
        if (!IsAnyUIOpen)
        {
            SetCursorLocked(true);
            if (_fpsCamera != null)         _fpsCamera.enabled         = true;
            if (_terrainInteraction != null) _terrainInteraction.enabled = true;
        }
    }

    // ---------------------------------------------------------------
    //  Dialogue
    // ---------------------------------------------------------------

    public void OpenDialogue(NPCDefinition npc, GameObject interactor)
    {
        if (_dialogueRunner == null || _dialogueUI == null) return;
        if (_dialogueOpen) CloseDialogue();

        _dialogueOpen = true;
        SetCursorLocked(false);
        if (_fpsCamera != null)          _fpsCamera.enabled          = false;
        if (_terrainInteraction != null)  _terrainInteraction.enabled = false;

        _dialogueRunner.OnDialogueEnded += OnDialogueRunnerEnded;
        _dialogueRunner.StartDialogue(npc, interactor);
    }

    private void OnDialogueRunnerEnded()
    {
        _dialogueRunner.OnDialogueEnded -= OnDialogueRunnerEnded;
        CloseDialogue();
    }

    public void CloseDialogue()
    {
        if (!_dialogueOpen) return;
        _dialogueOpen = false;
        if (_dialogueRunner != null && _dialogueRunner.IsRunning)
            _dialogueRunner.EndDialogue();
        if (!IsAnyUIOpen)
        {
            SetCursorLocked(true);
            if (_fpsCamera != null)          _fpsCamera.enabled          = true;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = true;
        }
    }

    private bool TryAddToInventory(Inventory inv, ItemStack stack)
    {
        for (int i = 0; i < inv.SlotCount; i++)
        {
            ItemStack slot = inv.GetSlot(i);
            if (slot.IsEmpty || slot.item != stack.item) continue;
            int space = slot.item.maxStackSize - slot.quantity;
            int move  = Mathf.Min(space, stack.quantity);
            if (move <= 0) continue;
            inv.SetSlot(i, new ItemStack(slot.item, slot.quantity + move));
            return true;
        }
        for (int i = 0; i < inv.SlotCount; i++)
        {
            if (!inv.GetSlot(i).IsEmpty) continue;
            inv.SetSlot(i, new ItemStack(stack.item, stack.quantity));
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------
    //  Cursor image (floating item icon that follows the mouse)
    // ---------------------------------------------------------------

    private void UpdateCursorItemImage()
    {
        if (_cursorImage == null) return;

        if (Cursor.IsHolding)
        {
            _cursorImage.enabled = true;

            // Tint the image by item type colour, using the same icon as the slot
            _cursorImage.sprite = ItemIconGenerator.GetIcon(Cursor.HeldStack.item);
            _cursorImage.color  = Color.white;

            // Follow mouse in canvas local space
            if (Mouse.current != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rootCanvasRect,
                    Mouse.current.position.ReadValue(),
                    null,   // overlay canvas — no camera needed
                    out Vector2 localPoint);

                _cursorImage.rectTransform.anchoredPosition = localPoint;
            }
        }
        else
        {
            _cursorImage.enabled = false;
        }
    }

    // ---------------------------------------------------------------
    //  System cursor lock
    // ---------------------------------------------------------------

    private void SetCursorLocked(bool locked)
    {
        UnityEngine.Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        UnityEngine.Cursor.visible   = !locked;
    }

    // ---------------------------------------------------------------
    //  Pause Menu
    // ---------------------------------------------------------------

    public void OpenPauseMenu()
    {
        if (_pauseMenuOpen) return;
        _pauseMenuOpen = true;
        _pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
        SetCursorLocked(false);
        if (_fpsCamera != null)          _fpsCamera.enabled          = false;
        if (_terrainInteraction != null)  _terrainInteraction.enabled = false;
    }

    public void ClosePauseMenu()
    {
        if (!_pauseMenuOpen) return;
        _pauseMenuOpen = false;
        _pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
        if (!IsAnyUIOpen)
        {
            SetCursorLocked(true);
            if (_fpsCamera != null)          _fpsCamera.enabled          = true;
            if (_terrainInteraction != null)  _terrainInteraction.enabled = true;
        }
    }

    // ---------------------------------------------------------------
    //  Bracer Integration
    // ---------------------------------------------------------------

    private System.Collections.IEnumerator WireBracerIntegrationDeferred()
    {
        // Wait until IndexBracerController has finished building.
        float timeout = 10f;
        float elapsed = 0f;
        IndexBracerController bracer = null;
        while (elapsed < timeout)
        {
            bracer = FindFirstObjectByType<IndexBracerController>();
            if (bracer != null && bracer.BracerScreenRect != null)
                break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _bracer = bracer;
        if (_bracer == null || _bracer.BracerScreenRect == null)
        {
            Debug.Log("[UIManager] IndexBracerController not found — hotbar stays on overlay canvas.");
            yield break;
        }

        // Reparent HotbarUI to the bracer screen canvas
        if (_hotbarUI != null)
        {
            RectTransform hotbarRT = _hotbarUI.GetComponent<RectTransform>();
            hotbarRT.SetParent(_bracer.BracerScreenRect, false);

            // Anchor to upper portion of bracer screen
            hotbarRT.anchorMin = new Vector2(0.5f, 1f);
            hotbarRT.anchorMax = new Vector2(0.5f, 1f);
            hotbarRT.pivot     = new Vector2(0.5f, 1f);
            hotbarRT.anchoredPosition = new Vector2(0f, -4f);
        }

        // Reparent StaminaBar to the bracer screen canvas (below hotbar)
        if (_staminaBarUI != null)
        {
            RectTransform barRT = _staminaBarUI.GetComponent<RectTransform>();
            barRT.SetParent(_bracer.BracerScreenRect, false);

            barRT.anchorMin = new Vector2(0.5f, 1f);
            barRT.anchorMax = new Vector2(0.5f, 1f);
            barRT.pivot     = new Vector2(0.5f, 1f);
            barRT.sizeDelta = new Vector2(160f, 6f);
            barRT.anchoredPosition = new Vector2(0f, -72f);
        }

        // Initialize IndexMessageDisplay on the bracer screen
        var msgDisplay = gameObject.GetComponent<IndexMessageDisplay>();
        if (msgDisplay == null)
            msgDisplay = gameObject.AddComponent<IndexMessageDisplay>();
        msgDisplay.Init(_bracer.BracerScreenRect);

        // Cache overlay parents for reparenting inventory/crafting on open/close
        if (_inventoryUI != null)
        {
            _inventoryOverlayParent = _inventoryUI.transform.parent;
            _inventoryOverlayAnchorPos = _inventoryUI.GetComponent<RectTransform>().anchoredPosition;
        }
        if (_craftingUI != null)
        {
            _craftingOverlayParent = _craftingUI.transform.parent;
            _craftingOverlayAnchorPos = _craftingUI.GetComponent<RectTransform>().anchoredPosition;
        }

        Debug.Log("[UIManager] Bracer integration wired — hotbar and messages on bracer screen.");
    }

    private void ReparentToBracer()
    {
        if (_bracer == null) return;

        // Inventory → Panel A
        if (_inventoryUI != null && _bracer.FoldoutRectA != null)
        {
            RectTransform rt = _inventoryUI.GetComponent<RectTransform>();
            rt.SetParent(_bracer.FoldoutRectA, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        // Crafting → Panel B (only personal crafting, not station UIs)
        if (_craftingUI != null && _bracer.FoldoutRectB != null && !_craftingStationOpen)
        {
            RectTransform rt = _craftingUI.GetComponent<RectTransform>();
            rt.SetParent(_bracer.FoldoutRectB, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void ReparentToOverlay()
    {
        // Inventory → overlay canvas
        if (_inventoryUI != null && _inventoryOverlayParent != null)
        {
            RectTransform rt = _inventoryUI.GetComponent<RectTransform>();
            rt.SetParent(_inventoryOverlayParent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = _inventoryOverlayAnchorPos;
        }

        // Crafting → overlay canvas
        if (_craftingUI != null && _craftingOverlayParent != null)
        {
            RectTransform rt = _craftingUI.GetComponent<RectTransform>();
            rt.SetParent(_craftingOverlayParent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = _craftingOverlayAnchorPos;
        }
    }

    // ---------------------------------------------------------------
    //  Canvas construction
    // ---------------------------------------------------------------

    private void BuildCanvas()
    {
        // EventSystem is required for ALL uGUI pointer events (hover, click, drag).
        // Create one if none exists in the scene.
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        GameObject canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution    = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight     = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        _rootCanvasRect = canvasGO.GetComponent<RectTransform>();
    }

    private void BuildHotbar()
    {
        GameObject hotbarGO = new GameObject("HotbarUI", typeof(RectTransform));
        hotbarGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = hotbarGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0);
        rt.anchorMax        = new Vector2(0.5f, 0);
        rt.pivot            = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 10);

        _hotbarUI = hotbarGO.AddComponent<HotbarUI>();
    }

    private void BuildStaminaBar()
    {
        // Hotbar sits at anchoredPosition.y = 10, height ≈ 62 (SlotSize 50 + padding 12)
        // Place stamina bar 6px above that
        const float hotbarHeight = 62f;
        const float barHeight    = 8f;
        const float barWidth     = 200f;
        const float gap          = 6f;

        GameObject barGO = new GameObject("StaminaBarUI", typeof(RectTransform));
        barGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = barGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0);
        rt.anchorMax        = new Vector2(0.5f, 0);
        rt.pivot            = new Vector2(0.5f, 0);
        rt.sizeDelta        = new Vector2(barWidth, barHeight);
        rt.anchoredPosition = new Vector2(0, 10 + hotbarHeight + gap);

        _staminaBarUI = barGO.AddComponent<StaminaBarUI>();
    }

    private void BuildAmmoUI()
    {
        // Position: bottom-right corner, safely clear of the hotbar (which is centre-bottom).
        // Height = AmmoRowHeight(42) + ReloadHeight(20) = 62px, matching the hotbar height tier.
        const float width  = 180f;
        const float height = 62f;
        const float margin = 16f;

        GameObject ammoGO = new GameObject("AmmoUI", typeof(RectTransform));
        ammoGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = ammoGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);  // bottom-right anchor
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);  // grows left and up
        rt.sizeDelta        = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-margin, margin);

        ammoGO.AddComponent<Voidborne.UI.AmmoUI>();
    }

    private void BuildInventoryPanel()
    {
        GameObject invGO = new GameObject("InventoryPanel", typeof(RectTransform));
        invGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = invGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        _inventoryUI = invGO.AddComponent<InventoryUI>();
    }

    private void BuildCraftingPanel()
    {
        GameObject craftGO = new GameObject("CraftingPanel", typeof(RectTransform));
        craftGO.transform.SetParent(_canvas.transform, false);

        // Inventory panel is centered at (0,0) and ~502px wide (9 cols × 50 + gaps + padding).
        // Anchor the crafting panel to the right of it with a small gap.
        // pivot left so it grows rightward from the anchor point.
        RectTransform rt = craftGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);   // left-edge pivot
        rt.anchoredPosition = new Vector2(262f, 0f);   // just right of the inventory panel

        _craftingUI = craftGO.AddComponent<CraftingUI>();
    }

    private void BuildFurnacePanel()
    {
        GameObject furnaceGO = new GameObject("FurnacePanel", typeof(RectTransform));
        furnaceGO.transform.SetParent(_canvas.transform, false);

        // Same position as the crafting panel — right of the inventory panel
        RectTransform rt = furnaceGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);   // left-edge pivot, grows rightward
        rt.anchoredPosition = new Vector2(262f, 0f);   // just right of the inventory panel

        _furnaceUI = furnaceGO.AddComponent<FurnaceUI>();
        _furnaceUI.Init();
    }

    private void BuildChestPanel()
    {
        GameObject chestGO = new GameObject("ChestPanel", typeof(RectTransform));
        chestGO.transform.SetParent(_canvas.transform, false);

        // Position to the LEFT of the inventory panel (inventory is centred at 0,0)
        RectTransform rt = chestGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);   // right-edge pivot, grows leftward
        rt.anchoredPosition = new Vector2(-262f, 0f);  // just left of the inventory panel

        _chestUI = chestGO.AddComponent<ChestUI>();
        _chestUI.Init();
    }

    private void BuildBackpackPanel()
    {
        GameObject backpackGO = new GameObject("BackpackPanel", typeof(RectTransform));
        backpackGO.transform.SetParent(_canvas.transform, false);

        // Position to the LEFT of the inventory panel (mirroring chest panel position)
        RectTransform rt = backpackGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);   // right-edge pivot, grows leftward
        rt.anchoredPosition = new Vector2(-262f, 0f);

        _backpackUI = backpackGO.AddComponent<BackpackUI>();
        _backpackUI.Init(GetPlayerInventory());
    }

    private void BuildAssemblerPanel()
    {
        GameObject assemblerGO = new GameObject("AssemblerPanel", typeof(RectTransform));
        assemblerGO.transform.SetParent(_canvas.transform, false);

        // Position to the right of the inventory panel — same as FurnacePanel
        RectTransform rt = assemblerGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(262f, 0f);

        _assemblerUI = assemblerGO.AddComponent<Voidborne.UI.AssemblerUI>();
        _assemblerUI.Init();
    }

    private void BuildTerminalPanel()
    {
        GameObject terminalGO = new GameObject("TerminalPanel", typeof(RectTransform));
        terminalGO.transform.SetParent(_canvas.transform, false);

        // Full-screen overlay — Terminal manages its own internal layout
        RectTransform rt = terminalGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        _terminalUI = terminalGO.AddComponent<Voidborne.UI.TerminalUI>();
    }

    private void BuildVehicleWorkbenchPanel()
    {
        GameObject go = new GameObject("VehicleAssemblyPanel", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 600f);
        rt.anchoredPosition = Vector2.zero;
        _vehicleAssemblyUI = go.AddComponent<Voidborne.UI.VehicleAssemblyUI>();
    }

    private void BuildVehicleHUD()
    {
        GameObject go = new GameObject("VehicleHUD", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(300f, 200f);
        rt.anchoredPosition = new Vector2(10f, 80f);
        _vehicleHUD = go.AddComponent<Voidborne.UI.VehicleHUD>();
    }

    private void BuildTooltip()
    {
        GameObject ttGO = new GameObject("TooltipUI", typeof(RectTransform));
        ttGO.transform.SetParent(_canvas.transform, false);

        // Anchor to bottom-left so anchoredPosition is in screen-space
        RectTransform rt = ttGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot     = Vector2.zero;

        ttGO.AddComponent<TooltipUI>();

        // Must be last sibling so it renders on top of all other canvas children.
        // Set AFTER AddComponent so Awake() cannot accidentally reorder it.
        ttGO.transform.SetAsLastSibling();
    }

    private void BuildQuestUI()
    {
        // ---- Quest Tracker (persistent top-right HUD) ----
        var trackerGO = new GameObject("QuestTrackerUI", typeof(RectTransform));
        trackerGO.transform.SetParent(_canvas.transform, false);
        var trackerRt = trackerGO.GetComponent<RectTransform>();
        trackerRt.anchorMin        = new Vector2(1, 1);
        trackerRt.anchorMax        = new Vector2(1, 1);
        trackerRt.pivot            = new Vector2(1, 1);
        trackerRt.anchoredPosition = new Vector2(-10, -10);
        trackerRt.sizeDelta        = new Vector2(300, 10); // height grown dynamically
        _questTrackerUI = trackerGO.AddComponent<QuestTrackerUI>();

        // ---- Quest Log (J-key full panel) ----
        var logGO = new GameObject("QuestLogUI", typeof(RectTransform));
        logGO.transform.SetParent(_canvas.transform, false);
        _questLogUI = logGO.AddComponent<QuestLogUI>();
        // QuestLogUI.Awake() sets its own size/anchor and starts hidden
    }

    private void BuildDialogueUI()
    {
        // DialogueRunner — persistent singleton, lives on UIManager GO
        _dialogueRunner = gameObject.AddComponent<DialogueRunner>();

        // DialogueUI — lower-third panel parented to canvas
        var dlgGO = new GameObject("DialogueUI", typeof(RectTransform));
        dlgGO.transform.SetParent(_canvas.transform, false);
        _dialogueUI = dlgGO.AddComponent<DialogueUI>();
        _dialogueUI.Init(_canvas);
    }

    private void BuildCursorItem()
    {
        // Floating image that tracks the mouse while cursor is holding an item.
        // Lives at the very top of the canvas hierarchy so it draws above everything.
        GameObject cursorGO = new GameObject("CursorItem", typeof(RectTransform), typeof(Image));
        cursorGO.transform.SetParent(_canvas.transform, false);

        _cursorImage = cursorGO.GetComponent<Image>();
        _cursorImage.raycastTarget = false;  // must not block pointer events on slots
        _cursorImage.enabled       = false;
        _cursorImage.preserveAspect = true;

        RectTransform rt = cursorGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40, 40);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        // Render on top of tooltip and everything else
        cursorGO.transform.SetAsLastSibling();
    }

    private void BuildPauseMenu()
    {
        // Semi-transparent dark overlay + centered panel with Resume / Quit buttons.
        _pauseMenuPanel = new GameObject("PauseMenu", typeof(RectTransform));
        _pauseMenuPanel.transform.SetParent(_canvas.transform, false);

        RectTransform panelRT = _pauseMenuPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Full-screen dark overlay
        Image overlay = _pauseMenuPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.6f);
        overlay.raycastTarget = true;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(_pauseMenuPanel.transform, false);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text      = "PAUSED";
        titleText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize  = 36;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color     = Color.white;
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRT.pivot            = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = new Vector2(0, 80);
        titleRT.sizeDelta        = new Vector2(300, 50);

        // Resume button
        CreatePauseButton("ResumeBtn", "Resume", new Vector2(0, 0), () => ClosePauseMenu());

        // Quit button
        CreatePauseButton("QuitBtn", "Quit", new Vector2(0, -60), () =>
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        _pauseMenuPanel.SetActive(false);
    }

    private void CreatePauseButton(string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(_pauseMenuPanel.transform, false);

        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRT.pivot            = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = position;
        btnRT.sizeDelta        = new Vector2(200, 44);

        Image btnImage = btnGO.GetComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text txt = labelGO.AddComponent<Text>();
        txt.text      = label;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 22;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
    }
}
