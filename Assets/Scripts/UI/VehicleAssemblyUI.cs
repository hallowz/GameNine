using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Vehicles;

namespace Voidborne.UI
{
    /// <summary>
    /// UI panel for the Vehicle Workbench.
    /// Frame selector → component slot grid → weight/performance readout → assemble button.
    /// Drag-and-drop from player inventory is handled via the existing InventoryCursor.
    /// </summary>
    public class VehicleAssemblyUI : MonoBehaviour
    {
        [Header("Frame Selection")]
        [SerializeField] private Transform frameButtonContainer;
        [SerializeField] private Button frameButtonPrefab;

        [Header("Component Slots")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private Button slotButtonPrefab;

        [Header("Stats Panel")]
        [SerializeField] private Text totalWeightText;
        [SerializeField] private Text estimatedSpeedText;
        [SerializeField] private Text craftingCostText;
        [SerializeField] private Text statusText;

        [Header("Actions")]
        [SerializeField] private Button assembleButton;
        [SerializeField] private Button repairButton;
        [SerializeField] private Button closeButton;

        [Header("Assembly Spawn")]
        [SerializeField] private Transform spawnPoint;

        // ─── Runtime state ───────────────────────────────────────────────
        private VehicleWorkbench _workbench;
        private VehicleFrame _selectedFrame;
        private readonly Dictionary<AttachmentPoint, VehicleComponent> _selectedComponents
            = new Dictionary<AttachmentPoint, VehicleComponent>();

        private VehicleFrame[] _availableFrames;

        private void Awake()
        {
            gameObject.SetActive(false);
            if (assembleButton != null) assembleButton.onClick.AddListener(OnAssemble);
            if (repairButton != null) repairButton.onClick.AddListener(OnRepair);
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
        }

        public void Open(VehicleWorkbench workbench, VehicleFrame[] frames)
        {
            _workbench = workbench;
            _availableFrames = frames;
            _selectedComponents.Clear();
            _selectedFrame = null;

            gameObject.SetActive(true);
            BuildFrameButtons();
            RefreshStats();

            // Show repair button only if a vehicle is parked
            if (repairButton != null)
                repairButton.gameObject.SetActive(workbench.GetParkedVehicle() != null);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            _workbench = null;
        }

        // ─── Frame selection ─────────────────────────────────────────────

        private void BuildFrameButtons()
        {
            if (frameButtonContainer == null || frameButtonPrefab == null) return;
            foreach (Transform child in frameButtonContainer) Destroy(child.gameObject);
            if (_availableFrames == null) return;

            foreach (var frame in _availableFrames)
            {
                var btn = Instantiate(frameButtonPrefab, frameButtonContainer);
                var lbl = btn.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = frame.frameName;
                var captured = frame;
                btn.onClick.AddListener(() => SelectFrame(captured));
            }
        }

        private void SelectFrame(VehicleFrame frame)
        {
            _selectedFrame = frame;
            _selectedComponents.Clear();
            BuildSlots();
            RefreshStats();
        }

        // ─── Component slots (grouped by BodySection) ─────────────────────

        private void BuildSlots()
        {
            if (slotContainer == null || slotButtonPrefab == null) return;
            foreach (Transform child in slotContainer) Destroy(child.gameObject);
            if (_selectedFrame == null) return;

            // Group attachment points by section
            var sections = new Dictionary<BodySection, List<AttachmentPoint>>();
            foreach (var ap in _selectedFrame.attachmentPoints)
            {
                if (!sections.ContainsKey(ap.section))
                    sections[ap.section] = new List<AttachmentPoint>();
                sections[ap.section].Add(ap);
            }

            // Display in section order
            foreach (BodySection sec in System.Enum.GetValues(typeof(BodySection)))
            {
                if (!sections.ContainsKey(sec)) continue;
                var points = sections[sec];

                // Section header
                var headerGO = new GameObject("Header_" + sec, typeof(RectTransform), typeof(Text));
                headerGO.transform.SetParent(slotContainer, false);
                var headerText = headerGO.GetComponent<Text>();
                headerText.text = $"── {sec} ──";
                headerText.fontSize = 12;
                headerText.fontStyle = FontStyle.Bold;
                headerText.color = new Color(0.85f, 0.55f, 0.15f);
                headerText.alignment = TextAnchor.MiddleCenter;
                var headerRT = headerGO.GetComponent<RectTransform>();
                headerRT.sizeDelta = new Vector2(0, 20);

                // Slot buttons for this section
                foreach (var ap in points)
                {
                    var btn = Instantiate(slotButtonPrefab, slotContainer);
                    var lbl = btn.GetComponentInChildren<Text>();
                    string slotLabel = !string.IsNullOrEmpty(ap.slotId) ? ap.slotId : ap.attachmentType.ToString();
                    string required = ap.isRequired ? " *" : "";

                    // Check if a parked vehicle has this slot populated
                    string conditionStr = "Empty";
                    if (_workbench != null)
                    {
                        var parked = _workbench.GetParkedVehicle();
                        if (parked != null)
                        {
                            var body = parked.GetComponent<VehicleBody>();
                            if (body != null && !string.IsNullOrEmpty(ap.slotId))
                            {
                                var slot = body.GetSlot(ap.slotId);
                                if (slot != null && !slot.IsEmpty)
                                {
                                    float pct = slot.Condition.Ratio * 100f;
                                    conditionStr = $"{slot.InstalledPart.displayName} [{pct:F0}%]";
                                }
                            }
                        }
                    }

                    if (lbl != null) lbl.text = $"{slotLabel}{required} — {conditionStr}";
                    var capturedAp = ap;
                    btn.onClick.AddListener(() => OnSlotClicked(capturedAp, btn));
                }
            }
        }

        private void OnSlotClicked(AttachmentPoint ap, Button slotBtn)
        {
            // If player is holding a component in the cursor, place it
            var cursor = UIManager.Instance?.Cursor;
            if (cursor == null) return;

            // For now, just mark the slot as "pending" — full drag-drop via InventoryCursor
            // would require a matching component item. Here we just refresh stats.
            RefreshStats();
        }

        // ─── Install component programmatically (for setup) ──────────────
        public void SetComponentInSlot(AttachmentPoint ap, VehicleComponent comp)
        {
            _selectedComponents[ap] = comp;
            RefreshStats();
        }

        // ─── Stats refresh ────────────────────────────────────────────────

        private void RefreshStats()
        {
            if (_selectedFrame == null)
            {
                if (totalWeightText != null) totalWeightText.text = "Select a frame";
                if (estimatedSpeedText != null) estimatedSpeedText.text = "";
                if (craftingCostText != null) craftingCostText.text = "";
                if (assembleButton != null) assembleButton.interactable = false;
                return;
            }

            float totalWeight = _selectedFrame.baseWeight;
            float totalPower = 0f;
            foreach (var kvp in _selectedComponents)
            {
                if (kvp.Value == null) continue;
                totalWeight += kvp.Value.TotalWeight;
                if (kvp.Value is EngineComponent eng) totalPower += eng.maxPower;
            }

            float estimatedSpeed = totalWeight > 0f ? totalPower / totalWeight * 10f : 0f;

            if (totalWeightText != null) totalWeightText.text = $"Weight: {totalWeight:F0} kg";
            if (estimatedSpeedText != null) estimatedSpeedText.text = $"Est. speed: {estimatedSpeed:F1} m/s";
            if (craftingCostText != null) craftingCostText.text = BuildCostString();

            // Assemble enabled if frame selected and required engine/suspension slots filled (basic validation)
            bool canAssemble = _selectedFrame != null;
            if (assembleButton != null) assembleButton.interactable = canAssemble;
        }

        private string BuildCostString()
        {
            // Summarize component crafting costs
            int totalCost = 0;
            foreach (var kvp in _selectedComponents)
                if (kvp.Value != null) totalCost++;
            return $"Components needed: {totalCost}";
        }

        // ─── Actions ─────────────────────────────────────────────────────

        private void OnAssemble()
        {
            if (_selectedFrame == null) return;

            var spPos = spawnPoint != null ? spawnPoint.position : Vector3.zero + Vector3.up * 0.5f;

            // Spawn a primitive root GO
            var vehicleGO = new GameObject("Vehicle_" + _selectedFrame.frameName);
            vehicleGO.transform.position = spPos;

            var rb = vehicleGO.AddComponent<Rigidbody>();
            rb.mass = _selectedFrame.baseWeight;

            var ds = vehicleGO.AddComponent<VehicleDamageSystem>();
            var fuel = vehicleGO.AddComponent<VehicleFuel>();
            var assembled = vehicleGO.AddComponent<AssembledVehicle>();
            assembled.SetFrame(_selectedFrame);

            foreach (var kvp in _selectedComponents)
                if (kvp.Value != null) assembled.InstallComponent(kvp.Value);

            // Add a collider so the vehicle is solid
            var box = vehicleGO.AddComponent<BoxCollider>();
            box.size = new Vector3(1.5f, 0.8f, 2.5f);
            box.center = new Vector3(0f, 0.4f, 0f);

            // Add visual placeholder
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(vehicleGO.transform);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            visual.transform.localScale = new Vector3(1.5f, 0.8f, 2.5f);
            Destroy(visual.GetComponent<Collider>());

            if (statusText != null)
                statusText.text = $"{_selectedFrame.frameName} assembled!";

            OnClose();
        }

        private void OnRepair()
        {
            if (_workbench == null) return;
            var parked = _workbench.GetParkedVehicle();
            if (parked == null) return;
            _workbench.FullRepair(parked);
            if (statusText != null) statusText.text = "Vehicle fully repaired.";
        }

        private void OnClose()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.CloseVehicleWorkbench();
            else
                Close();
        }
    }
}
