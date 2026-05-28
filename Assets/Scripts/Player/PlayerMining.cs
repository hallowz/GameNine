using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Voidborne.Building;
using Voidborne.Diagnostics;
using Voidborne.World.Chunks;
using Voidborne.World.Decoration;
using Voidborne.World.Generation;

namespace Voidborne.Player
{
    /// <summary>
    /// Handles the left-click progressive mining and chopping system.
    /// The player holds left-click on terrain (pickaxe) or trees (axe)
    /// and a progress bar fills based on equipped tool tier.
    /// When full, terrain is removed / tree is felled and items are spawned.
    ///
    /// This component takes over the left-click path from PlayerTerrainInteraction by setting
    /// OverrideLeftClick = true on that component during Awake.
    ///
    /// Durability is tracked per hotbar slot via a dictionary keyed on slot index.
    /// </summary>
    public class PlayerMining : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector Settings
        // ---------------------------------------------------------------

        [Header("Mining Settings")]
        [Tooltip("Base mining progress gained per second with no tool equipped.")]
        [SerializeField] private float baseProgressRate = 0.2f;

        [Tooltip("Radius of the sphere deformation used when removing terrain.")]
        [SerializeField] private float deformRadius = 1.5f;

        [Tooltip("Deformation intensity applied when removing terrain.")]
        [SerializeField] private float deformIntensity = -1.5f;

        [Tooltip("Maximum raycast distance for mining interaction.")]
        [SerializeField] private float maxRaycastDistance = 8f;

        [Header("Chopping Settings")]
        [Tooltip("Base chopping progress gained per second with a wood axe.")]
        [SerializeField] private float baseChopRate = 0.15f;

        [Header("UI")]
        [Tooltip("Height of the mining progress bar in pixels.")]
        [SerializeField] private float progressBarHeight = 18f;

        [Tooltip("Width of the mining progress bar in pixels.")]
        [SerializeField] private float progressBarWidth = 300f;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private float miningProgress = 0f;
        private bool  isMining       = false;

        // Cached references
        private Camera             playerCamera;
        private PlayerInventory    playerInventory;
        private PlayerTerrainInteraction terrainInteraction;
        private InputSystem_Actions inputActions;
        private InputAction         attackAction;

        // Per-slot durability tracking (keyed by hotbar slot index)
        private readonly Dictionary<int, int> slotDurability = new Dictionary<int, int>();

        // Cached item references to avoid ItemDatabase lookups every mine
        private ItemDefinition _cachedStoneItem;
        private ItemDefinition _cachedWoodItem;

        // ---------------------------------------------------------------
        //  UI Elements (built in code)
        // ---------------------------------------------------------------

        private Canvas        miningCanvas;
        private Image         progressBarBg;
        private Image         progressBarFill;
        private RectTransform progressBarFillRT;
        private GameObject    progressBarRoot;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            // Claim left-click from PlayerTerrainInteraction
            terrainInteraction = GetComponent<PlayerTerrainInteraction>();
            if (terrainInteraction != null)
            {
                terrainInteraction.OverrideLeftClick = true;
            }
            else
            {
                Debug.LogWarning("[PlayerMining] PlayerTerrainInteraction not found on same GameObject. " +
                                 "Left-click may be handled by both systems.");
            }

            // Find camera
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;

            // Find inventory
            playerInventory = GetComponent<PlayerInventory>();
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();
            attackAction = inputActions.Player.Attack;
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Disable();
                inputActions.Dispose();
                inputActions = null;
            }
        }

        private void Start()
        {
            BuildProgressBarUI();
            SetProgressBarVisible(false);
        }

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("PlayerMining.Update");

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            bool leftHeld = attackAction != null && attackAction.IsPressed();

            if (!leftHeld)
            {
                ResetProgress();
                RuntimeProfiler.End(s_prof);
                return;
            }

            // Block mining when any UI is open
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            {
                ResetProgress();
                RuntimeProfiler.End(s_prof);
                return;
            }

            if (playerCamera == null)
            {
                ResetProgress();
                RuntimeProfiler.End(s_prof);
                return;
            }

            // Determine which tool is equipped
            ToolDefinition pickaxe = GetEquippedTool(ToolType.Pickaxe);
            ToolDefinition axe     = GetEquippedTool(ToolType.Axe);

            // Try tree chopping first (axe equipped + ray hits tree)
            if (axe != null && TreeRenderer.Instance != null)
            {
                Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                if (TreeRenderer.Instance.TryRaycastTree(ray, maxRaycastDistance, out var treeHit))
                {
                    isMining = true;
                    SetProgressBarVisible(true);
                    SetProgressBarColor(new Color(0.55f, 0.35f, 0.1f, 1f)); // brown for chopping

                    float rate = CalculateChopRate(axe);
                    miningProgress += rate * Time.deltaTime;
                    miningProgress = Mathf.Clamp01(miningProgress);
                    UpdateProgressBarFill(miningProgress);

                    if (miningProgress >= 1f)
                    {
                        CompleteChop(treeHit);
                        ResetProgress();
                    }
                    RuntimeProfiler.End(s_prof);
                    return;
                }
            }

            // Raycast forward. The hit is shared between the block-break (V7.5)
            // and terrain-mine paths; a single Physics.Raycast keeps the cost
            // constant per frame.
            Ray mineRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(mineRay, out RaycastHit hit, maxRaycastDistance))
            {
                ResetProgress();
                RuntimeProfiler.End(s_prof);
                return;
            }

            // V7.5 — Block-mining branch.
            // If the raycast hit a PlacedBlock, route damage through
            // BlockBreaker instead of TerrainDeformer. No tool-tier
            // requirement: any tool (or no tool) chips blocks at the same
            // rate for M2. Tool tiers land in M7 polish.
            PlacedBlock placedBlock = hit.collider != null
                ? hit.collider.GetComponentInParent<PlacedBlock>()
                : null;
            if (placedBlock != null)
            {
                isMining = true;
                SetProgressBarVisible(true);
                SetProgressBarColor(new Color(0.4f, 0.7f, 1f, 1f)); // blue for block-break

                float blockRate = baseProgressRate * 2f; // M2: constant rate
                miningProgress += blockRate * Time.deltaTime;
                miningProgress = Mathf.Clamp01(miningProgress);
                UpdateProgressBarFill(miningProgress);

                if (miningProgress >= 1f)
                {
                    int dmg = PlacedBlock.DefaultHealth; // one hit cycle = one block (M2)
                    BlockBreaker.ApplyDamage(placedBlock, dmg);
                    ResetProgress();
                }
                RuntimeProfiler.End(s_prof);
                return;
            }

            // Fall through to terrain mining (pickaxe equipped + ray hits terrain)
            if (pickaxe == null)
            {
                ResetProgress();
                RuntimeProfiler.End(s_prof);
                return;
            }

            // We have a valid terrain hit — advance progress
            isMining = true;
            SetProgressBarVisible(true);
            SetProgressBarColor(new Color(1f, 0.75f, 0.1f, 1f)); // orange for mining

            float mineRate = CalculateMiningRate();
            miningProgress += mineRate * Time.deltaTime;
            miningProgress = Mathf.Clamp01(miningProgress);
            UpdateProgressBarFill(miningProgress);

            if (miningProgress >= 1f)
            {
                CompleteMine(hit);
                ResetProgress();
            }
            RuntimeProfiler.End(s_prof);
        }

        // ---------------------------------------------------------------
        //  Mining Logic
        // ---------------------------------------------------------------

        /// <summary>
        /// Calculates mining progress rate per second based on equipped tool.
        /// Formula: baseRate * (1 + toolTier * 0.5) * miningSpeedMultiplier
        /// </summary>
        private float CalculateMiningRate()
        {
            ToolDefinition tool = GetEquippedTool(ToolType.Pickaxe);
            if (tool == null)
                return baseProgressRate;

            int tier = tool.GetMiningTierRequirement();
            return baseProgressRate * (1f + tier * 0.5f) * tool.miningSpeedMultiplier;
        }

        /// <summary>
        /// Calculates chopping progress rate per second based on equipped axe.
        /// </summary>
        private float CalculateChopRate(ToolDefinition axe)
        {
            int tier = axe.GetMiningTierRequirement();
            return baseChopRate * (1f + tier * 0.5f) * axe.miningSpeedMultiplier;
        }

        /// <summary>
        /// Returns the ToolDefinition of the equipped item if it matches the given type, otherwise null.
        /// </summary>
        private ToolDefinition GetEquippedTool(ToolType type)
        {
            if (playerInventory == null) return null;
            ItemStack activeStack = playerInventory.ActiveHotbarItem;
            if (activeStack.IsEmpty) return null;
            if (activeStack.item is ToolDefinition tool && tool.toolType == type)
                return tool;
            return null;
        }

        /// <summary>
        /// Called when mining progress reaches 1.0. Removes terrain, looks up ore, spawns drop.
        /// </summary>
        private void CompleteMine(RaycastHit hit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 rayDir   = playerCamera.transform.forward;

            // Deform the terrain (dig)
            Vector3 deformCenter = hitPoint + rayDir * 0.1f;
            TerrainDeformer.DeformSphere(deformCenter, deformRadius, deformIntensity);

            // Determine spawn position (slightly above the hit point)
            Vector3 spawnPos = hitPoint + Vector3.up * 0.5f;

            // Look up the voxel at the hit position to find any ore
            ItemDefinition dropItem = GetOreItemAtPosition(hitPoint);

            // Reduce tool durability
            ReduceToolDurability();

            // Spawn the item drop
            if (dropItem != null)
            {
                WorldItemSpawner.SpawnItem(new ItemStack(dropItem, 1), spawnPos);
            }
        }

        /// <summary>
        /// Called when chopping progress reaches 1.0. Removes tree and spawns wood items.
        /// </summary>
        private void CompleteChop(TreeRenderer.TreeHitResult treeHit)
        {
            // Remove tree from GPU renderer
            TreeRenderer.Instance.RemoveTree(treeHit.position);

            // Spawn wood drops (scaled by tree size)
            Vector3 spawnPos = treeHit.position + Vector3.up * 0.5f;
            ItemDefinition woodItem = GetWoodItem();
            if (woodItem != null)
            {
                int dropCount = 2 + Mathf.FloorToInt(treeHit.scale * 2f);
                WorldItemSpawner.SpawnItem(new ItemStack(woodItem, dropCount), spawnPos);
            }

            // Reduce tool durability
            ReduceToolDurability();
        }

        /// <summary>
        /// Checks the OreField at the world position.
        /// Returns the associated ItemDefinition if:
        ///   - There is ore at the voxel AND
        ///   - The equipped tool tier is sufficient to mine it.
        /// Returns the stone item if no ore or tier insufficient.
        /// </summary>
        private ItemDefinition GetOreItemAtPosition(Vector3 worldPos)
        {
            ChunkManager cm = ChunkManager.Instance;
            if (cm == null) return GetStoneItem();

            // Convert world pos to chunk position
            Vector3Int chunkPos = new Vector3Int(
                Mathf.FloorToInt(worldPos.x / ChunkData.SIZE),
                Mathf.FloorToInt(worldPos.y / ChunkData.SIZE),
                Mathf.FloorToInt(worldPos.z / ChunkData.SIZE)
            );

            ChunkData chunk = cm.GetChunk(chunkPos);
            if (chunk == null) return GetStoneItem();

            // Local voxel coords
            int lx = Mathf.FloorToInt(worldPos.x) - chunkPos.x * ChunkData.SIZE;
            int ly = Mathf.FloorToInt(worldPos.y) - chunkPos.y * ChunkData.SIZE;
            int lz = Mathf.FloorToInt(worldPos.z) - chunkPos.z * ChunkData.SIZE;

            lx = Mathf.Clamp(lx, 0, ChunkData.SIZE - 1);
            ly = Mathf.Clamp(ly, 0, ChunkData.SIZE - 1);
            lz = Mathf.Clamp(lz, 0, ChunkData.SIZE - 1);

            int idx = lx + ly * ChunkData.SIZE + lz * ChunkData.SIZE * ChunkData.SIZE;

            byte oreId = chunk.OreField.Get(idx);

            if (oreId == 0)
            {
                // No ore — clear the voxel ore field (already 0) and drop stone
                return GetStoneItem();
            }

            // There is ore at this voxel — check if the registry can identify it
            OreRegistry registry = cm.OreRegistry;
            if (registry == null) return GetStoneItem();

            OreDefinition oreDef = registry.GetOreById(oreId);
            if (oreDef == null) return GetStoneItem();

            // Check tool tier requirement
            ToolDefinition tool = GetEquippedTool(ToolType.Pickaxe);
            int toolTier = tool != null ? tool.GetMiningTierRequirement() : 0;

            // Determine ore's required tier from its position in the ToolTier enum.
            // Convention: oreTypeId maps to ToolTier — ore IDs 1..4 correspond to tiers 1..4.
            // Terrain types (sand=7, dirt=8, grass=9, rock=10) require no special tier.
            bool isTerrainType = oreId >= OreGenerator.SandOreId;
            int requiredTier = isTerrainType ? 0 : Mathf.Clamp(oreId, 1, (int)ToolTier.Void);

            // Clear the ore voxel now that we've mined it
            chunk.OreField.Set(idx, 0);
            chunk.isDirty = true;

            if (toolTier >= requiredTier)
            {
                // Tier sufficient — return the ore's item
                return oreDef.associatedItem != null ? oreDef.associatedItem : GetStoneItem();
            }
            else
            {
                // Tier insufficient — drop stone only
                return GetStoneItem();
            }
        }

        /// <summary>
        /// Loads and returns the "stone" ItemDefinition from the ItemDatabase.
        /// </summary>
        private ItemDefinition GetStoneItem()
        {
            if (_cachedStoneItem != null) return _cachedStoneItem;
            ItemDatabase db = ItemDatabase.GetOrLoad();
            if (db == null) return null;
            _cachedStoneItem = db.GetItem("stone");
            return _cachedStoneItem;
        }

        /// <summary>
        /// Loads and returns the "wood" ItemDefinition from the ItemDatabase.
        /// </summary>
        private ItemDefinition GetWoodItem()
        {
            if (_cachedWoodItem != null) return _cachedWoodItem;
            ItemDatabase db = ItemDatabase.GetOrLoad();
            if (db == null) return null;
            _cachedWoodItem = db.GetItem("wood");
            return _cachedWoodItem;
        }

        // ---------------------------------------------------------------
        //  Durability Tracking
        // ---------------------------------------------------------------

        /// <summary>
        /// Reduces the durability of the currently equipped tool by 1.
        /// Durability is tracked per hotbar slot in slotDurability.
        /// When durability reaches 0, the item is removed from the hotbar slot.
        /// </summary>
        private void ReduceToolDurability()
        {
            if (playerInventory == null) return;

            ItemStack activeStack = playerInventory.ActiveHotbarItem;
            if (activeStack.IsEmpty) return;
            if (!(activeStack.item is ToolDefinition tool)) return;

            int slotIdx = playerInventory.SelectedHotbarIndex;

            // Initialize durability for this slot if not yet tracked
            if (!slotDurability.TryGetValue(slotIdx, out int currentDur))
            {
                currentDur = tool.maxDurability;
                slotDurability[slotIdx] = currentDur;
            }

            currentDur -= 1;

            if (currentDur <= 0)
            {
                // Tool is broken — remove from hotbar slot
                slotDurability.Remove(slotIdx);
                playerInventory.Hotbar.SetSlot(slotIdx, new ItemStack(null, 0));
                Debug.Log($"[PlayerMining] {tool.displayName} broke!");
            }
            else
            {
                slotDurability[slotIdx] = currentDur;
            }
        }

        // ---------------------------------------------------------------
        //  Progress State
        // ---------------------------------------------------------------

        private void ResetProgress()
        {
            if (isMining || miningProgress > 0f)
            {
                miningProgress = 0f;
                isMining = false;
                SetProgressBarVisible(false);
                UpdateProgressBarFill(0f);
            }
        }

        // ---------------------------------------------------------------
        //  UI — Progress Bar (built in code)
        // ---------------------------------------------------------------

        /// <summary>
        /// Creates a screen-space Canvas with a simple horizontal fill progress bar
        /// centered at the bottom of the screen. Called once in Start().
        /// </summary>
        private void BuildProgressBarUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("MiningProgressCanvas");
            DontDestroyOnLoad(canvasGO);
            miningCanvas = canvasGO.AddComponent<Canvas>();
            miningCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            miningCanvas.sortingOrder = 10;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Root rect (anchored center-bottom)
            progressBarRoot = new GameObject("MiningProgressBar");
            progressBarRoot.transform.SetParent(canvasGO.transform, false);

            RectTransform rootRect = progressBarRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot     = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 122f); // above stamina bar (hotbar=72 + gap=6 + stamina=8 + gap=6
            rootRect.sizeDelta = new Vector2(progressBarWidth + 4f, progressBarHeight + 4f);

            // Background (dark grey)
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(progressBarRoot.transform, false);
            progressBarBg = bgGO.AddComponent<Image>();
            progressBarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill bar (orange-yellow) — width driven by anchorMax.x = progress (0..1)
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(progressBarRoot.transform, false);
            progressBarFill = fillGO.AddComponent<Image>();
            progressBarFill.color = new Color(1f, 0.75f, 0.1f, 1f);

            progressBarFillRT = fillGO.GetComponent<RectTransform>();
            progressBarFillRT.anchorMin = Vector2.zero;
            progressBarFillRT.anchorMax = new Vector2(0f, 1f); // zero width initially
            progressBarFillRT.offsetMin = Vector2.zero;
            progressBarFillRT.offsetMax = Vector2.zero;
        }

        private void SetProgressBarVisible(bool visible)
        {
            if (progressBarRoot != null)
                progressBarRoot.SetActive(visible);
        }

        private void SetProgressBarColor(Color color)
        {
            if (progressBarFill != null)
                progressBarFill.color = color;
        }

        private void UpdateProgressBarFill(float amount)
        {
            if (progressBarFillRT != null)
                progressBarFillRT.anchorMax = new Vector2(amount, 1f);
        }
    }
}
