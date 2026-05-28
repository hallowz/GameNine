using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Crafting;
using Voidborne.UI;

namespace Voidborne.Automation
{
    /// <summary>
    /// V6.3 — runtime crafting wrapper for a placed machine.
    ///
    /// Implements <see cref="IMachineInputProvider"/> so MachineUI (V4.4) can
    /// drive its slot grid + progress bar + recipe tabs against this component
    /// without any conditional code paths. The station owns:
    /// <list type="bullet">
    /// <item><description>A <see cref="MachineDefinition"/> reference (the SO,
    /// driving gridWidth/Height, processType, needsPower, etc).</description></item>
    /// <item><description>An <see cref="ItemStack"/> array sized to the
    /// machine's input grid; a single-slot output array.</description></item>
    /// <item><description>The V6.1 <see cref="ICraftingMatchEngine"/> used to
    /// gate <see cref="TryStartRecipe"/>: a recipe only starts if the engine
    /// matches the current input bag through the machine's <c>processType</c>.</description></item>
    /// <item><description>A tick coroutine that advances <see cref="Progress"/>
    /// from 0 to 1 over <c>baseSeconds / efficiency</c> seconds, then deposits
    /// the output stack.</description></item>
    /// </list>
    ///
    /// V9.1 (MachineRuntime) will instantiate this on placed prefabs and wire
    /// up server-authoritative consumption. For V6.3 it stands alone so tests
    /// can drive it programmatically.
    /// </summary>
    /// <remarks>
    /// Coop note: state lives locally (this is a MonoBehaviour, not a
    /// NetworkBehaviour). The deterministic V6.1 engine means the server +
    /// client will agree on whether a recipe starts and what efficiency it
    /// runs at; only progress (a server-tick value) needs syncing. V21 wires
    /// that.
    /// </remarks>
    public class MachineCraftingStation : MonoBehaviour, IMachineInputProvider
    {
        // ---------------------------------------------------------------
        //  Inspector / configuration
        // ---------------------------------------------------------------

        [Tooltip("Machine definition driving grid size, process type, and power profile. Required.")]
        [SerializeField] private MachineDefinition machineDef;

        /// <summary>The machine SO backing this station. Set via Init() or the inspector.</summary>
        public MachineDefinition Machine => machineDef;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private ItemStack[] _inputs;
        private ItemStack[] _outputs;
        private float _progress01;
        private RecipeDefinition _activeRecipe;
        private RecipeMatchResult _activeMatch;
        private float _activeDurationSeconds;
        private float _activeElapsedSeconds;
        private Coroutine _tickRoutine;
        private ItemStack _fuelSlot;
        private float _powerSatisfaction = 1f;

        private readonly DefaultCraftingMatchEngine _engine = new DefaultCraftingMatchEngine();

        // ---------------------------------------------------------------
        //  IMachineInputProvider
        // ---------------------------------------------------------------

        public IReadOnlyList<ItemStack> Inputs => _inputs ?? Array.Empty<ItemStack>();
        public IReadOnlyList<ItemStack> Outputs => _outputs ?? Array.Empty<ItemStack>();
        public float Progress => _progress01;
        public bool NeedsPower => machineDef != null && machineDef.needsPower;
        public float PowerSatisfaction => _powerSatisfaction;
        public bool NeedsFuel => RequiresFuel(machineDef);
        public ItemStack FuelSlot => _fuelSlot;

        public event Action OnStateChanged;

        /// <summary>True when a recipe is currently advancing.</summary>
        public bool IsRunning => _activeRecipe != null;

        /// <summary>The recipe currently advancing, or null when idle.</summary>
        public RecipeDefinition ActiveRecipe => _activeRecipe;

        /// <summary>The match result associated with the active recipe (efficiency, bindings). Null when idle.</summary>
        public RecipeMatchResult ActiveMatch => _activeMatch;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (machineDef != null) AllocateSlots();
        }

        private void OnDisable()
        {
            // Stop the tick coroutine if the GO goes inactive mid-recipe.
            // The recipe state persists; ReSubscribe-style logic in V21 will
            // re-emit progress from the server snapshot.
            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }
        }

        // ---------------------------------------------------------------
        //  Setup
        // ---------------------------------------------------------------

        /// <summary>
        /// Bind this station to a machine definition. Re-allocates the input /
        /// output arrays to the machine's grid dimensions. Safe to call
        /// post-Awake (re-init).
        /// </summary>
        public void Init(MachineDefinition def)
        {
            machineDef = def;
            AllocateSlots();
            RaiseStateChanged();
        }

        private void AllocateSlots()
        {
            int width = machineDef != null ? Mathf.Max(1, machineDef.gridWidth) : 1;
            int height = machineDef != null ? Mathf.Max(1, machineDef.gridHeight) : 1;
            int inputCount = width * height;
            _inputs = new ItemStack[inputCount];
            _outputs = new ItemStack[1];
        }

        // ---------------------------------------------------------------
        //  Slot mutators (used by MachineUI, tests, V9.1 server bridge)
        // ---------------------------------------------------------------

        /// <summary>
        /// Set an input slot. Does not auto-start a recipe; the caller is
        /// expected to invoke <see cref="TryStartRecipe"/> when ready.
        /// </summary>
        public void SetInput(int index, ItemStack stack)
        {
            if (_inputs == null || index < 0 || index >= _inputs.Length) return;
            _inputs[index] = stack;
            RaiseStateChanged();
        }

        /// <summary>Replace the entire input array (useful for bulk test setup).</summary>
        public void SetInputs(IReadOnlyList<ItemStack> stacks)
        {
            if (_inputs == null || stacks == null) return;
            int n = Mathf.Min(stacks.Count, _inputs.Length);
            for (int i = 0; i < n; i++) _inputs[i] = stacks[i];
            RaiseStateChanged();
        }

        public void SetFuel(ItemStack stack)
        {
            _fuelSlot = stack;
            RaiseStateChanged();
        }

        public void SetPowerSatisfaction(float normalized)
        {
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            _powerSatisfaction = normalized;
            RaiseStateChanged();
        }

        /// <summary>Force the output slot (test seam; production code goes through DepositOutput).</summary>
        public void SetOutput(int index, ItemStack stack)
        {
            if (_outputs == null || index < 0 || index >= _outputs.Length) return;
            _outputs[index] = stack;
            RaiseStateChanged();
        }

        // ---------------------------------------------------------------
        //  TryStartRecipe / CancelRecipe (IMachineInputProvider)
        // ---------------------------------------------------------------

        /// <summary>
        /// Gate a recipe through the V6.1 engine with the machine's
        /// <c>processType</c>. On Matched: consume bound inputs, start the
        /// tick coroutine. On no-match (picky machine refusing a substitution,
        /// missing ingredient, etc): swallow the request silently, leaving
        /// state untouched. The reason is available via <c>engine.TryMatch</c>
        /// for UI surfacing if the caller cares.
        /// </summary>
        public void TryStartRecipe(RecipeDefinition recipe)
        {
            if (recipe == null) return;
            if (machineDef == null) return;
            if (_activeRecipe != null) return; // ignore re-start while running

            Dictionary<string, int> bag = BuildAvailableBag();
            ItemDatabase itemDb = ItemDatabase.GetOrLoad();
            IReadOnlyList<ItemDefinition> dbItems = itemDb != null
                ? itemDb.AllItems
                : Array.Empty<ItemDefinition>();

            RecipeMatchResult result = _engine.TryMatch(
                recipe,
                machineDef.processType,
                bag,
                dbItems);

            if (result == null || result.recipe == null) return; // refused

            // Consume the bound inputs deterministically. Per V6.1 review note:
            // specific matches walk recipe.ingredients[]; property fallback
            // walks result.inputBindings.Values keyed by the property tag.
            if (!ConsumeMatchedInputs(result))
            {
                // Shouldn't happen (the engine already verified the bag), but
                // guard against a partial consumption leaving the station in
                // an inconsistent state. If it does happen, ignore the start.
                return;
            }

            _activeRecipe = result.recipe;
            _activeMatch = result;

            float baseSeconds = MachineRecipeTimeProvider.GetBaseSeconds(machineDef, result.recipe);
            float effEfficiency = result.efficiency > 0.01f ? result.efficiency : 1f;
            _activeDurationSeconds = baseSeconds / effEfficiency;
            _activeElapsedSeconds = 0f;
            _progress01 = 0f;

            // Kick the tick coroutine. If we're not active in the hierarchy
            // (rare; some tests instantiate disabled), the coroutine won't
            // run -- callers in that mode can drive AdvanceTick() manually.
            if (isActiveAndEnabled)
            {
                if (_tickRoutine != null) StopCoroutine(_tickRoutine);
                _tickRoutine = StartCoroutine(TickRoutine());
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// Abort the active recipe and refund consumed inputs back into the
        /// input grid. If refund overflows the grid (free space gone), the
        /// excess is dropped on the floor -- matches Rust/Minecraft conventions.
        /// </summary>
        public void CancelRecipe()
        {
            if (_activeRecipe == null) return;

            RefundConsumedInputs(_activeMatch);

            _activeRecipe = null;
            _activeMatch = null;
            _activeDurationSeconds = 0f;
            _activeElapsedSeconds = 0f;
            _progress01 = 0f;

            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }

            RaiseStateChanged();
        }

        // ---------------------------------------------------------------
        //  Tick coroutine (Unity time-driven; EditMode tests use AdvanceTick)
        // ---------------------------------------------------------------

        private IEnumerator TickRoutine()
        {
            while (_activeRecipe != null && _activeElapsedSeconds < _activeDurationSeconds)
            {
                yield return null;
                AdvanceTick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Drive the active recipe forward by <paramref name="deltaSeconds"/>.
        /// EditMode tests call this directly (no Unity Update loop). When the
        /// elapsed time crosses <c>activeDurationSeconds</c>, the output is
        /// deposited and the recipe is cleared.
        /// </summary>
        public void AdvanceTick(float deltaSeconds)
        {
            if (_activeRecipe == null) return;
            if (deltaSeconds <= 0f) return;

            _activeElapsedSeconds += deltaSeconds;
            if (_activeDurationSeconds <= 0f)
            {
                // Defensive: avoid div-by-zero on Progress.
                _progress01 = 1f;
            }
            else
            {
                _progress01 = Mathf.Clamp01(_activeElapsedSeconds / _activeDurationSeconds);
            }

            if (_progress01 >= 1f)
            {
                CompleteActiveRecipe();
            }
            else
            {
                RaiseStateChanged();
            }
        }

        /// <summary>
        /// EditMode test seam — force the active recipe to completion in one
        /// frame. Equivalent to AdvanceTick(activeDurationSeconds) but doesn't
        /// require the test to know the duration constant.
        /// </summary>
        public void ForceCompleteActiveRecipe()
        {
            if (_activeRecipe == null) return;
            _activeElapsedSeconds = _activeDurationSeconds;
            _progress01 = 1f;
            CompleteActiveRecipe();
        }

        private void CompleteActiveRecipe()
        {
            RecipeDefinition recipe = _activeRecipe;
            RecipeMatchResult match = _activeMatch;

            DepositOutput(recipe, match);

            _activeRecipe = null;
            _activeMatch = null;
            _activeDurationSeconds = 0f;
            _activeElapsedSeconds = 0f;
            _progress01 = 0f;

            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }

            RaiseStateChanged();
        }

        private void DepositOutput(RecipeDefinition recipe, RecipeMatchResult match)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.outputItemId)) return;
            ItemDatabase itemDb = ItemDatabase.GetOrLoad();
            if (itemDb == null) return;
            ItemDefinition outDef = itemDb.GetItem(recipe.outputItemId);
            if (outDef == null) return;

            float outMod = match != null ? match.outputModifier : recipe.outputModifier;
            int baseQty = recipe.outputQty > 0 ? recipe.outputQty : 1;
            int finalQty = Mathf.FloorToInt(baseQty * outMod);
            if (finalQty < 1) finalQty = 1;

            // Single output slot for V6.3; merge if the same item already there.
            if (_outputs == null || _outputs.Length == 0) _outputs = new ItemStack[1];

            if (_outputs[0].IsEmpty)
            {
                _outputs[0] = new ItemStack(outDef, finalQty);
            }
            else if (_outputs[0].item == outDef)
            {
                _outputs[0] = new ItemStack(outDef, _outputs[0].quantity + finalQty);
            }
            else
            {
                // Output slot occupied by a different item; drop the new stack
                // on the floor metaphorically (V9.1 will plumb belt-out / hopper
                // pickup). For now, overwrite is the wrong call -- skip.
            }
        }

        // ---------------------------------------------------------------
        //  Input bag helpers
        // ---------------------------------------------------------------

        private Dictionary<string, int> BuildAvailableBag()
        {
            var bag = new Dictionary<string, int>(_inputs != null ? _inputs.Length : 0);
            if (_inputs == null) return bag;
            for (int i = 0; i < _inputs.Length; i++)
            {
                ItemStack s = _inputs[i];
                if (s.IsEmpty || s.item == null) continue;
                string id = s.item.itemId;
                if (string.IsNullOrEmpty(id)) continue;
                bag.TryGetValue(id, out int have);
                bag[id] = have + s.quantity;
            }
            return bag;
        }

        private bool ConsumeMatchedInputs(RecipeMatchResult result)
        {
            if (result == null || result.recipe == null) return false;

            // Specific path: bindings are id -> id; consume recipe.ingredients[].
            // Property path: bindings are propertyName -> id; consume per
            // recipe.inputProperties[i].qty for the bound id.
            bool specific = IsSpecificBindings(result);
            if (specific)
            {
                var ingredients = result.recipe.ingredients;
                if (ingredients == null) return true;
                for (int i = 0; i < ingredients.Length; i++)
                {
                    int qty = ingredients[i].qty > 0 ? ingredients[i].qty : 1;
                    if (!DecrementFromInputs(ingredients[i].itemId, qty)) return false;
                }
                return true;
            }

            var props = result.recipe.inputProperties;
            if (props == null || result.inputBindings == null) return true;
            for (int i = 0; i < props.Length; i++)
            {
                int qty = props[i].qty > 0 ? props[i].qty : 1;
                string key = props[i].property.ToString();
                if (!result.inputBindings.TryGetValue(key, out string itemId)) return false;
                if (!DecrementFromInputs(itemId, qty)) return false;
            }
            return true;
        }

        private void RefundConsumedInputs(RecipeMatchResult result)
        {
            // Symmetric refund: walk the same bindings we used to consume,
            // adding the qty back into the first matching / empty slot.
            if (result == null || result.recipe == null) return;
            bool specific = IsSpecificBindings(result);
            if (specific)
            {
                var ingredients = result.recipe.ingredients;
                if (ingredients == null) return;
                for (int i = 0; i < ingredients.Length; i++)
                {
                    int qty = ingredients[i].qty > 0 ? ingredients[i].qty : 1;
                    IncrementIntoInputs(ingredients[i].itemId, qty);
                }
                return;
            }
            var props = result.recipe.inputProperties;
            if (props == null || result.inputBindings == null) return;
            for (int i = 0; i < props.Length; i++)
            {
                int qty = props[i].qty > 0 ? props[i].qty : 1;
                string key = props[i].property.ToString();
                if (!result.inputBindings.TryGetValue(key, out string itemId)) continue;
                IncrementIntoInputs(itemId, qty);
            }
        }

        private bool DecrementFromInputs(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return true;
            int remaining = qty;
            if (_inputs == null) return false;
            for (int i = 0; i < _inputs.Length && remaining > 0; i++)
            {
                ItemStack s = _inputs[i];
                if (s.IsEmpty || s.item == null) continue;
                if (s.item.itemId != itemId) continue;
                int take = Mathf.Min(remaining, s.quantity);
                int newQty = s.quantity - take;
                _inputs[i] = newQty > 0 ? new ItemStack(s.item, newQty) : default;
                remaining -= take;
            }
            return remaining == 0;
        }

        private void IncrementIntoInputs(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return;
            if (_inputs == null) return;
            ItemDatabase db = ItemDatabase.GetOrLoad();
            if (db == null) return;
            ItemDefinition def = db.GetItem(itemId);
            if (def == null) return;

            int remaining = qty;
            // First pass: merge into existing same-item slots up to maxStackSize.
            for (int i = 0; i < _inputs.Length && remaining > 0; i++)
            {
                ItemStack s = _inputs[i];
                if (s.IsEmpty || s.item == null) continue;
                if (s.item != def) continue;
                int space = def.maxStackSize - s.quantity;
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                _inputs[i] = new ItemStack(def, s.quantity + add);
                remaining -= add;
            }
            // Second pass: drop into empty slots.
            for (int i = 0; i < _inputs.Length && remaining > 0; i++)
            {
                if (!_inputs[i].IsEmpty) continue;
                int add = Mathf.Min(def.maxStackSize, remaining);
                _inputs[i] = new ItemStack(def, add);
                remaining -= add;
            }
            // Overflow is silently dropped (consistent with the design's
            // "drop on the floor" convention for full-inventory crafts).
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private static bool IsSpecificBindings(RecipeMatchResult result)
        {
            if (result == null || result.recipe == null) return false;
            var bindings = result.inputBindings;
            if (bindings == null || bindings.Count == 0) return false;
            foreach (var kv in bindings)
            {
                if (kv.Key != kv.Value) return false;
            }
            return true;
        }

        private static bool RequiresFuel(MachineDefinition def)
        {
            if (def == null) return false;
            // Thermal forgiving machines burn fuel; everything else is electric
            // / mechanical / hand-cranked. The dedicated fuel slot exists for
            // these two process types specifically.
            switch (def.processType)
            {
                case MachineProcessType.Forgiving_Thermal_DryBurn:
                case MachineProcessType.Forgiving_Thermal_Boil:
                    return true;
                default:
                    return false;
            }
        }

        private void RaiseStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
