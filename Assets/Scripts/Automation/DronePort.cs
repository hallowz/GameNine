using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;
using Voidborne.Enemies;

namespace Voidborne.Automation
{
    /// <summary>
    /// A small launch pad that sends hovering cargo drones to a designated receiving
    /// DronePort. Each drone carries 1 ItemStack, flies in a straight line (ignoring
    /// terrain), takes ~5 s per trip, and is visible in the world.
    ///
    /// Power: 30 W when active.
    ///
    /// VORD hostility: if any active enemy is within <droneAgroRadius> of the
    /// drone's current world position, the drone is shot down (destroyed) and
    /// the carried item is dropped as a WorldItem.
    ///
    /// Internal buffer: 4 slots (both source and destination).
    /// A Hopper connected to the "buffer_port" AutomationConnector can load
    /// items into/out of the buffer.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class DronePort : MonoBehaviour, IAutomationNode
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Drone Port Settings")]
        [Tooltip("The DronePort this port sends drones to. Must be set for the source port.")]
        [SerializeField] private DronePort destination;

        [Tooltip("Only send/receive items of this type. Leave null for any item.")]
        [SerializeField] private ItemDefinition itemFilter;

        [Tooltip("Internal buffer capacity (slots).")]
        [SerializeField] private int bufferCapacity = 4;

        [Header("Drone Flight")]
        [Tooltip("World-space units per second the drone travels.")]
        [SerializeField] private float droneSpeed = 10f;

        [Tooltip("Hover height above the port when launching/landing.")]
        [SerializeField] private float hoverHeight = 2f;

        [Tooltip("Radius around the drone's position to check for active VORD enemies.")]
        [SerializeField] private float droneAgroRadius = 12f;

        [Header("Visuals")]
        [Tooltip("Prefab for the visible drone object in the world.")]
        [SerializeField] private GameObject dronePrefab;

        [Tooltip("Launch arm transform that rotates when a drone departs.")]
        [SerializeField] private Transform launchArm;

        [Header("Status")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer     _power;
        private readonly List<ItemStack> _buffer = new List<ItemStack>();
        private bool              _droneInFlight  = false;
        private Coroutine         _flightRoutine;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _power = GetComponent<PowerConsumer>();
        }

        private void Start()
        {
            // Power draw is set in editor on the PowerNode.
            StartCoroutine(DispatchLoop());
        }

        private void OnDisable()
        {
            if (_flightRoutine != null)
                StopCoroutine(_flightRoutine);
            _droneInFlight = false;
        }

        // ── Dispatch loop ──────────────────────────────────────────────────

        private IEnumerator DispatchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (!_power.IsPowered)        { UpdateStatus(); continue; }
                if (_droneInFlight)           { UpdateStatus(); continue; }
                if (destination == null)      { UpdateStatus(); continue; }
                if (!destination._power.IsPowered) { UpdateStatus(); continue; }

                // Find an item to send.
                ItemStack toSend = TryExtract(itemFilter);
                if (toSend.IsEmpty)           { UpdateStatus(); continue; }

                // Check destination buffer has room.
                if (!destination.CanAccept(toSend.item))
                {
                    TryInsert(toSend); // return to buffer
                    UpdateStatus();
                    continue;
                }

                // Launch!
                _droneInFlight = true;
                _flightRoutine = StartCoroutine(FlyDrone(toSend));
                UpdateStatus();
            }
        }

        // ── Drone flight coroutine ─────────────────────────────────────────

        private IEnumerator FlyDrone(ItemStack cargo)
        {
            // Spawn drone visual.
            Vector3 startPos = transform.position + Vector3.up * hoverHeight;
            Vector3 endPos   = destination.transform.position + Vector3.up * destination.hoverHeight;

            GameObject droneGO = null;
            if (dronePrefab != null)
                droneGO = Instantiate(dronePrefab, startPos, Quaternion.identity);

            // Animate launch arm.
            if (launchArm != null)
            {
                launchArm.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Fly to destination.
            float dist    = Vector3.Distance(startPos, endPos);
            float duration = dist / droneSpeed;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);

                if (droneGO != null)
                    droneGO.transform.position = Vector3.Lerp(startPos, endPos, t);

                // VORD check: if an enemy is nearby, shoot down the drone.
                if (droneGO != null && IsVordPresent(droneGO.transform.position))
                {
                    // Drop cargo as world item at current position.
                    SpawnDroppedItem(cargo, droneGO.transform.position);
                    Object.Destroy(droneGO);
                    _droneInFlight = false;
                    UpdateStatus();
                    yield break;
                }

                yield return null;
            }

            // Arrive: deposit into destination buffer.
            if (droneGO != null)
                Object.Destroy(droneGO);

            destination.TryInsert(cargo);

            if (launchArm != null)
                launchArm.localRotation = Quaternion.identity;

            _droneInFlight = false;
            UpdateStatus();
            destination.UpdateStatus();
        }

        // ── VORD detection ─────────────────────────────────────────────────

        private bool IsVordPresent(Vector3 pos)
        {
            // Overlap sphere and check for any EnemyEntity that is alive.
            Collider[] cols = Physics.OverlapSphere(pos, droneAgroRadius);
            foreach (var c in cols)
            {
                var enemy = c.GetComponentInParent<EnemyEntity>();
                if (enemy != null && !enemy.IsDead)
                    return true;
            }
            return false;
        }

        // ── Drop item ──────────────────────────────────────────────────────

        private static void SpawnDroppedItem(ItemStack stack, Vector3 worldPos)
        {
            WorldItemSpawner.SpawnItem(stack, worldPos);
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (itemFilter != null && item != itemFilter) return false;
            return BufferCount() < bufferCapacity;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (!CanAccept(stack.item)) return false;
            AddToBuffer(stack);
            UpdateStatus();
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            foreach (var s in _buffer)
                if (filter == null || s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (filter == null || _buffer[i].item == filter)
                {
                    var result = new ItemStack(_buffer[i].item, 1);
                    _buffer[i] = new ItemStack(_buffer[i].item, _buffer[i].quantity - 1);
                    if (_buffer[i].quantity <= 0) _buffer.RemoveAt(i);
                    UpdateStatus();
                    return result;
                }
            }
            return new ItemStack(null, 0);
        }

        // ── Buffer helpers ─────────────────────────────────────────────────

        private int BufferCount()
        {
            int t = 0;
            foreach (var s in _buffer) t += s.quantity;
            return t;
        }

        private void AddToBuffer(ItemStack stack)
        {
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i].item == stack.item)
                {
                    _buffer[i] = new ItemStack(_buffer[i].item, _buffer[i].quantity + stack.quantity);
                    return;
                }
            }
            _buffer.Add(stack);
        }

        // ── Status display ─────────────────────────────────────────────────

        private void UpdateStatus()
        {
            if (statusText == null) return;
            string dest   = destination != null ? destination.gameObject.name : "None";
            string filter = itemFilter  != null ? itemFilter.displayName          : "Any";
            statusText.text =
                $"Dest: {dest}\nFilter: {filter}\n" +
                $"Buffer: {BufferCount()}/{bufferCapacity}\n" +
                $"Drone: {(_droneInFlight ? "In Flight" : "Ready")}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}";
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void SetDestination(DronePort port) => destination = port;
        public void SetFilter(ItemDefinition filter) => itemFilter = filter;

        public bool  DroneInFlight => _droneInFlight;
        public int   BufferFill    => BufferCount();
        public int   BufferMax     => bufferCapacity;
    }
}
