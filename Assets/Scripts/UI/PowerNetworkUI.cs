using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Building.Electricity;

namespace Voidborne.UI
{
    /// <summary>
    /// Floating HUD shown when looking at any PowerNode within 5 units.
    /// Displays: network generation, consumption, battery %, top consumers, alerts.
    /// Attaches to the player's Canvas. Controlled by PlayerInteraction or similar.
    /// </summary>
    public class PowerNetworkUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;
        public Text       networkIdText;
        public Text       generationText;
        public Text       consumptionText;
        public Text       batteryText;
        public Text       statusText;
        public Text       vordAlertText;
        public Transform  consumerListParent;
        public GameObject consumerRowPrefab; // Text prefab

        [Header("Settings")]
        public float maxInspectDistance = 5f;
        public LayerMask powerNodeLayer;

        private Camera      _cam;
        private PowerNetwork _shownNetwork;

        private readonly List<GameObject> _consumerRows = new List<GameObject>();

        private void Awake()
        {
            _cam = Camera.main;
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            var node = GetLookedAtNode();
            if (node != null && node.Network != null)
            {
                _shownNetwork = node.Network;
                ShowPanel();
                RefreshData();
            }
            else
            {
                _shownNetwork = null;
                HidePanel();
            }
        }

        private PowerNode GetLookedAtNode()
        {
            if (_cam == null) return null;
            Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxInspectDistance, powerNodeLayer))
                return hit.collider.GetComponentInParent<PowerNode>();
            return null;
        }

        private void ShowPanel()
        {
            if (panel != null && !panel.activeSelf)
                panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (panel != null && panel.activeSelf)
                panel.SetActive(false);
        }

        private void RefreshData()
        {
            if (_shownNetwork == null) return;
            var s = _shownNetwork.GetNetworkSummary();

            if (networkIdText  != null) networkIdText.text  = $"Network #{s.networkId}";
            if (generationText != null) generationText.text = $"Gen: {s.totalGeneration:F0} W";
            if (consumptionText!= null) consumptionText.text= $"Draw: {s.totalConsumption:F0} W";

            float battPct = s.batteryCapacity > 0f ? s.batteryLevel / s.batteryCapacity * 100f : 0f;
            if (batteryText != null) batteryText.text = $"Battery: {battPct:F0}%";

            if (statusText != null)
            {
                float net = s.totalGeneration - s.totalConsumption;
                if (net >= 0f) statusText.text = "<color=green>SURPLUS</color>";
                else           statusText.text = "<color=red>OVERDRAW</color>";
            }

            if (vordAlertText != null)
            {
                if (s.voidFilamentActive)
                    vordAlertText.text = "<color=#CC44FF>⚠ VORD ALERT — Void Filament active</color>";
                else
                    vordAlertText.text = string.Empty;
            }

            RefreshConsumerList();
        }

        private void RefreshConsumerList()
        {
            foreach (var r in _consumerRows) Destroy(r);
            _consumerRows.Clear();

            if (consumerListParent == null || consumerRowPrefab == null) return;

            var consumers = _shownNetwork.GetTopConsumers(5);
            foreach (var node in consumers)
            {
                var row = Instantiate(consumerRowPrefab, consumerListParent);
                var t = row.GetComponent<Text>();
                if (t != null)
                    t.text = $"{node.gameObject.name}: {node.GetCurrentDraw():F0}W";
                _consumerRows.Add(row);
            }
        }
    }
}
