using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Singleton manager that ticks all registered automation components at a fixed interval.
    ///
    /// Tick ordering (TickPriority, ascending):
    ///   10 — Output Hoppers  (push items from containers onto belts/tubes)
    ///   20 — ConveyorBelts / PneumaticTubes  (move items along)
    ///   25 — BeltSorters  (route items at junctions)
    ///   28 — OverflowValves  (gate flow when downstream is full)
    ///   30 — Input Hoppers  (pull items from belts/tubes into containers)
    ///   40 — Machines  (process items)
    ///
    /// Belt speeds:
    ///   Basic belt: ticks every 0.5 s
    ///   Fast belt:  ticks every 0.2 s
    ///   Pneumatic tube: ticks every 0.15 s
    ///
    /// All components register themselves in OnEnable and unregister in OnDisable.
    /// The manager runs two coroutines: a slow one (0.5 s base) and a fast one (0.1 s)
    /// for tubes/fast belts. Components on the fast list implement IFastTickable.
    /// </summary>
    public class AutomationTickManager : MonoBehaviour
    {
        public static AutomationTickManager Instance { get; private set; }

        [Tooltip("Tick interval for basic belts and hoppers (seconds).")]
        [SerializeField] private float basicTickInterval = 0.5f;

        [Tooltip("Tick interval for fast belts and pneumatic tubes (seconds).")]
        [SerializeField] private float fastTickInterval = 0.15f;

        // ── Registered components ──────────────────────────────────────────

        private readonly List<ITickable> _basic = new List<ITickable>();
        private readonly List<ITickable> _fast  = new List<ITickable>();

        // HashSets for O(1) membership checks during registration
        private readonly HashSet<ITickable> _basicSet = new HashSet<ITickable>();
        private readonly HashSet<ITickable> _fastSet  = new HashSet<ITickable>();

        // Scratch list for sorted dispatch — avoids per-tick allocation after first sort.
        private readonly List<ITickable> _sortedBasic = new List<ITickable>();
        private readonly List<ITickable> _sortedFast  = new List<ITickable>();
        private bool _basicDirty = true;
        private bool _fastDirty  = true;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(BasicTickRoutine());
            StartCoroutine(FastTickRoutine());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Registration ───────────────────────────────────────────────────

        /// <summary>Register a component for the basic (0.5 s) tick.</summary>
        public void Register(ITickable tickable)
        {
            if (_basicSet.Add(tickable))
            {
                _basic.Add(tickable);
                _basicDirty = true;
            }
        }

        /// <summary>Register a component for the fast (0.15 s) tick (tubes, fast belts).</summary>
        public void RegisterFast(ITickable tickable)
        {
            if (_fastSet.Add(tickable))
            {
                _fast.Add(tickable);
                _fastDirty = true;
            }
        }

        public void Unregister(ITickable tickable)
        {
            if (_basicSet.Remove(tickable) && _basic.Remove(tickable)) _basicDirty = true;
            if (_fastSet.Remove(tickable) && _fast.Remove(tickable))   _fastDirty  = true;
        }

        // ── Tick coroutines ────────────────────────────────────────────────

        private IEnumerator BasicTickRoutine()
        {
            var wait = new WaitForSeconds(basicTickInterval);
            while (true)
            {
                yield return wait;
                DispatchTick(_basic, _sortedBasic, ref _basicDirty);
            }
        }

        private IEnumerator FastTickRoutine()
        {
            var wait = new WaitForSeconds(fastTickInterval);
            while (true)
            {
                yield return wait;
                DispatchTick(_fast, _sortedFast, ref _fastDirty);
            }
        }

        private static void DispatchTick(List<ITickable> source, List<ITickable> sorted, ref bool dirty)
        {
            if (source.Count == 0) return;

            if (dirty)
            {
                sorted.Clear();
                sorted.AddRange(source);
                sorted.Sort((a, b) => a.TickPriority.CompareTo(b.TickPriority));
                dirty = false;
            }

            // Iterate by index so removals mid-tick don't crash.
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i] != null)
                    sorted[i].AutomationTick();
            }
        }
    }

    // ── Tick interface ─────────────────────────────────────────────────────

    /// <summary>
    /// Implement this on any MonoBehaviour that wants to be ticked by AutomationTickManager.
    /// </summary>
    public interface ITickable
    {
        /// <summary>Called each automation tick. Do item movement / machine processing here.</summary>
        void AutomationTick();

        /// <summary>
        /// Lower priority = ticked first within the same tick cycle.
        /// 10=OutputHopper, 20=Belt/Tube, 25=BeltSorter, 28=OverflowValve, 30=InputHopper, 40=Machine.
        /// </summary>
        int TickPriority { get; }
    }
}
