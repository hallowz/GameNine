using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.1 — player-side cable placement controller. Separate from
    /// <see cref="Voidborne.Building.BlockPlacer"/> per the V7 review heads-up:
    /// cables are edges (two endpoints), not single cells -- they don't fit
    /// the block-placement workflow.
    ///
    /// <para>UX flow:</para>
    /// <list type="number">
    /// <item><description>Select the <c>copper_cable_t1</c> item in the
    /// active hotbar slot.</description></item>
    /// <item><description>First left-click on a <see cref="PowerNode"/> -->
    /// source endpoint armed.</description></item>
    /// <item><description>Move cursor: a preview line draws from the source
    /// to the cursor's current world point.</description></item>
    /// <item><description>Second left-click on another PowerNode --> a
    /// <see cref="CableSegment"/> is instantiated, the source is unarmed,
    /// and one cable is consumed from inventory.</description></item>
    /// <item><description>Right-click or Escape cancels the source-armed
    /// state.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Coop note: cable placement is owner-authoritative for M2. V21 will
    /// route the second-click commit through a ServerRpc that re-validates
    /// the two endpoints + the inventory cost; the rest of this controller
    /// is local input prediction.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CablePlacer : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Header("Raycast")]
        [Tooltip("Maximum distance from the camera at which we can target a PowerNode.")]
        [SerializeField] private float reach = 8f;

        [Tooltip("Layer mask the placement raycast hits against. Default: everything.")]
        [SerializeField] private LayerMask placementMask = ~0;

        [Header("Item")]
        [Tooltip("Item id that must be in the active hotbar slot to enable cable placement. Default: copper_cable_t1.")]
        [SerializeField] private string cableItemId = "copper_cable_t1";

        [Tooltip("The cable tier produced by this placer. Default: T1 (M2 only ships T1).")]
        [SerializeField] private PowerCableTier tier = PowerCableTier.T1;

        // ---------------------------------------------------------------
        //  Runtime
        // ---------------------------------------------------------------

        private Camera _playerCamera;
        private PlayerInventory _inventory;
        private InputSystem_Actions _inputActions;
        private InputAction _placeAction;

        private PowerNode _armedSource;
        private LineRenderer _previewLine;

        // ---------------------------------------------------------------
        //  Test seams
        // ---------------------------------------------------------------

        /// <summary>The currently armed source node (or null). Public for tests.</summary>
        public PowerNode ArmedSource => _armedSource;

        /// <summary>The cable item id this placer requires. Public for tests.</summary>
        public string CableItemId => cableItemId;

        /// <summary>The cable tier this placer produces. Public for tests.</summary>
        public PowerCableTier Tier => tier;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            _playerCamera = GetComponentInChildren<Camera>();
            if (_playerCamera == null) _playerCamera = Camera.main;
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null) _inventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            // Wrap InputSystem setup in try/catch so EditMode test fixtures
            // (which lack a wired InputSystem context) don't crash on
            // component spin-up.
            try
            {
                _inputActions = new InputSystem_Actions();
                _inputActions.Player.Enable();
                _placeAction = _inputActions.Player.Attack; // primary commit
                // Cancel is mapped to right-click + Esc directly via the
                // Mouse / Keyboard devices below -- the generated Player
                // action map has no dedicated Cancel binding (UI map does,
                // but UI is disabled while the placer is active).
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
            DestroyPreviewLine();
            _armedSource = null;
        }

        private void Update()
        {
            if (_playerCamera == null || _inventory == null) { DropArm(); return; }

            // Only run when the active hotbar item is the cable item.
            ItemStack active = _inventory.ActiveHotbarItem;
            if (active.IsEmpty || active.item == null || active.item.itemId != cableItemId)
            {
                DropArm();
                return;
            }

            // Raycast forward; if nothing's hit, hide the preview and bail.
            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, placementMask))
            {
                if (_armedSource != null) UpdatePreviewLine(_armedSource.transform.position, ray.GetPoint(reach));
                else DestroyPreviewLine();
                return;
            }

            PowerNode target = hit.collider != null ? hit.collider.GetComponentInParent<PowerNode>() : null;

            // Right-click / Esc cancel.
            bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey != null
                    && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (rightClick || esc)
            {
                DropArm();
                return;
            }

            // Update preview line.
            if (_armedSource != null)
            {
                Vector3 cursorWorld = target != null ? target.transform.position : hit.point;
                UpdatePreviewLine(_armedSource.transform.position, cursorWorld);
            }
            else
            {
                DestroyPreviewLine();
            }

            // Left-click commit / first-click arm.
            if (_placeAction != null && _placeAction.WasPerformedThisFrame())
            {
                if (target == null) return;

                if (_armedSource == null)
                {
                    _armedSource = target;
                    return;
                }
                if (target == _armedSource) return; // self-loop refused
                TryCommit(_armedSource, target);
            }
        }

        // ---------------------------------------------------------------
        //  Programmatic commit (test seam + the input-driven path)
        // ---------------------------------------------------------------

        /// <summary>
        /// Spawn a CableSegment between the two endpoints, consume one
        /// cable from the active hotbar slot, and unarm the source.
        /// Public so EditMode tests can drive the placement without
        /// simulating input. Returns the new segment (or null on failure).
        /// </summary>
        public CableSegment TryCommit(PowerNode a, PowerNode b)
        {
            if (a == null || b == null) return null;
            if (a == b) return null;

            if (!ConsumeOneCable()) { DropArm(); return null; }

            // Spawn the cable host GO at the midpoint so its transform is
            // not on top of either endpoint.
            Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
            var go = new GameObject($"cable_{tier}");
            go.transform.position = mid;
            var seg = go.AddComponent<CableSegment>();
            seg.Init(a, b, tier);

            DropArm();
            return seg;
        }

        // ---------------------------------------------------------------
        //  Internal helpers
        // ---------------------------------------------------------------

        private bool ConsumeOneCable()
        {
            if (_inventory == null) return true; // headless / test path
            int idx = _inventory.SelectedHotbarIndex;
            ItemStack active = _inventory.Hotbar.GetSlot(idx);
            if (active.IsEmpty || active.item == null) return false;
            if (active.item.itemId != cableItemId) return false;
            int newQty = active.quantity - 1;
            _inventory.Hotbar.SetSlot(idx, newQty > 0
                ? new ItemStack(active.item, newQty)
                : new ItemStack(null, 0));
            return true;
        }

        private void DropArm()
        {
            _armedSource = null;
            DestroyPreviewLine();
        }

        private void UpdatePreviewLine(Vector3 from, Vector3 to)
        {
            if (_previewLine == null)
            {
                var go = new GameObject("CablePlacerPreview");
                go.transform.SetParent(transform, false);
                _previewLine = go.AddComponent<LineRenderer>();
                _previewLine.positionCount = 2;
                _previewLine.widthMultiplier = 0.04f;
                _previewLine.useWorldSpace = true;
            }
            _previewLine.SetPosition(0, from);
            _previewLine.SetPosition(1, to);
        }

        private void DestroyPreviewLine()
        {
            if (_previewLine == null) return;
            if (Application.isPlaying) Destroy(_previewLine.gameObject);
            else DestroyImmediate(_previewLine.gameObject);
            _previewLine = null;
        }
    }
}
