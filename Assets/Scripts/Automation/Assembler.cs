using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;
using Voidborne.Crafting;

namespace Voidborne.Automation
{
    /// <summary>
    /// Assembler — auto-crafts any CraftingRecipe encoded on a SchematicCard.
    ///
    /// The player inserts a SchematicCard into the recipe slot (via AssemblerUI or manually).
    /// When the input buffer holds the required ingredients and the output buffer has room,
    /// the Assembler crafts one batch per cycle.
    ///
    /// Input buffer: up to 9 distinct item types (3×3 recipe buffer), each capped at stackSize.
    /// Output buffer: 3 slots.
    /// Power draw: 75 W.
    ///
    /// I/O uses IAutomationNode. Hoppers connected to the input port insert ingredients;
    /// Hoppers connected to the output port extract finished items.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class Assembler : MonoBehaviour, IAutomationNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Buffers")]
        [Tooltip("Maximum items per ingredient type in the input buffer.")]
        [SerializeField] private int inputSlotStackSize = 64;

        [Tooltip("Maximum total items in the output buffer.")]
        [SerializeField] private int outputCapacity = 64;

        [Header("Craft Speed")]
        [Tooltip("Seconds per craft cycle.")]
        [SerializeField] private float craftTime = 1.5f;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer _power;

        // Input buffer: keyed by ItemDefinition for fast ingredient lookup.
        private readonly Dictionary<ItemDefinition, int> _inputBuffer =
            new Dictionary<ItemDefinition, int>();

        // Output buffer: list of stacks.
        private readonly List<ItemStack> _outputBuffer = new List<ItemStack>();

        // Recipe slot: holds a SchematicCard stack (quantity 1).
        private ItemStack _recipeSlot;

        private float _craftTimer;
        private bool  _canCraft;

        // items-per-minute tracking
        private int   _itemsProducedThisMinute;
        private float _minuteTimer;

        // ── Public state ───────────────────────────────────────────────────

        public ItemStack    RecipeSlot    => _recipeSlot;
        public SchematicCard ActiveSchematic => _recipeSlot.item as SchematicCard;
        public CraftingRecipe ActiveRecipe  => ActiveSchematic?.encodedRecipe;

        public float CraftProgress =>
            (_canCraft && craftTime > 0f)
                ? 1f - Mathf.Clamp01(_craftTimer / craftTime)
                : 0f;

        public bool IsRunning => _power != null && _power.IsPowered && _canCraft;
        public int  ItemsPerMinute => _itemsProducedThisMinute;

        public event Action OnStateChanged;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake() => _power = GetComponent<PowerConsumer>();

        private void Update()
        {
            _minuteTimer += Time.deltaTime;
            if (_minuteTimer >= 60f) { _itemsProducedThisMinute = 0; _minuteTimer = 0f; }

            UpdateStatus();

            if (!_power.IsPowered) { _canCraft = false; return; }

            var recipe = ActiveRecipe;
            if (recipe == null) { _canCraft = false; return; }

            _canCraft = HasIngredients(recipe) && CanOutputAccept(recipe.result);
            if (!_canCraft) { _craftTimer = craftTime; return; }

            _craftTimer -= Time.deltaTime;
            if (_craftTimer <= 0f) CompleteCraft(recipe);
        }

        // ── Crafting logic ─────────────────────────────────────────────────

        private bool HasIngredients(CraftingRecipe recipe)
        {
            if (recipe.ingredients == null) return false;

            // Count required items from the recipe grid (may have duplicates).
            var required = new Dictionary<ItemDefinition, int>();
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing == null) continue;
                required.TryGetValue(ing, out int existing);
                required[ing] = existing + 1;
            }

            // Check input buffer holds enough of each.
            foreach (var kv in required)
            {
                _inputBuffer.TryGetValue(kv.Key, out int held);
                if (held < kv.Value) return false;
            }
            return true;
        }

        private bool CanOutputAccept(ItemStack result)
        {
            if (result.IsEmpty || result.item == null) return false;

            int existing = 0;
            foreach (var s in _outputBuffer)
                if (s.item == result.item) existing += s.quantity;

            int maxTotal = result.item.maxStackSize > 0 ? result.item.maxStackSize * 3 : outputCapacity;
            return (existing + result.quantity) <= maxTotal;
        }

        private void CompleteCraft(CraftingRecipe recipe)
        {
            // Consume ingredients.
            var required = new Dictionary<ItemDefinition, int>();
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing == null) continue;
                required.TryGetValue(ing, out int ex);
                required[ing] = ex + 1;
            }
            foreach (var kv in required)
            {
                _inputBuffer[kv.Key] -= kv.Value;
                if (_inputBuffer[kv.Key] <= 0) _inputBuffer.Remove(kv.Key);
            }

            // Produce output.
            AddToOutputBuffer(recipe.result);
            _itemsProducedThisMinute += recipe.result.quantity;

            _craftTimer = craftTime;
            OnStateChanged?.Invoke();
        }

        private void AddToOutputBuffer(ItemStack stack)
        {
            for (int i = 0; i < _outputBuffer.Count; i++)
            {
                if (_outputBuffer[i].item == stack.item)
                {
                    _outputBuffer[i] = new ItemStack(_outputBuffer[i].item,
                                                     _outputBuffer[i].quantity + stack.quantity);
                    return;
                }
            }
            _outputBuffer.Add(stack);
        }

        // ── IAutomationNode ────────────────────────────────────────────────
        // The Assembler exposes ONE combined node: Hoppers insert ingredients into the input
        // buffer (CanAccept checks the active recipe), and extract from the output buffer.

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            var recipe = ActiveRecipe;
            if (recipe?.ingredients == null) return false;

            // Item must be one of the recipe's ingredients.
            bool isIngredient = false;
            foreach (var ing in recipe.ingredients)
                if (ing == item) { isIngredient = true; break; }
            if (!isIngredient) return false;

            // Must not already have a full stack of this ingredient.
            _inputBuffer.TryGetValue(item, out int held);
            return held < inputSlotStackSize;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (!CanAccept(stack.item)) return false;
            _inputBuffer.TryGetValue(stack.item, out int existing);
            _inputBuffer[stack.item] = existing + stack.quantity;
            OnStateChanged?.Invoke();
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            foreach (var s in _outputBuffer)
                if (filter == null || s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            for (int i = 0; i < _outputBuffer.Count; i++)
            {
                if (filter == null || _outputBuffer[i].item == filter)
                {
                    var src = _outputBuffer[i];
                    var result = new ItemStack(src.item, 1);
                    _outputBuffer[i] = new ItemStack(src.item, src.quantity - 1);
                    if (_outputBuffer[i].quantity <= 0) _outputBuffer.RemoveAt(i);
                    OnStateChanged?.Invoke();
                    return result;
                }
            }
            return new ItemStack(null, 0);
        }

        // ── IInteractable ──────────────────────────────────────────────────

        public string InteractPrompt => "Press E to open Assembler";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= 3f;

        public void Interact(GameObject interactor)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OpenAssembler(this);
        }

        // ── Recipe slot API (called by AssemblerUI) ────────────────────────

        /// <summary>Insert or replace the schematic card in the recipe slot.</summary>
        public void SetRecipeSlot(ItemStack stack)
        {
            _recipeSlot   = stack;
            _craftTimer   = craftTime;
            _canCraft     = false;
            OnStateChanged?.Invoke();
        }

        /// <summary>Remove and return the schematic card from the recipe slot.</summary>
        public ItemStack TakeRecipeSlot()
        {
            var card    = _recipeSlot;
            _recipeSlot = new ItemStack(null, 0);
            _canCraft   = false;
            OnStateChanged?.Invoke();
            return card;
        }

        // ── Status display ─────────────────────────────────────────────────

        private void UpdateStatus()
        {
            if (statusText == null) return;
            string recipe = ActiveRecipe != null ? ActiveRecipe.recipeName : "No Schematic";
            statusText.text =
                $"Recipe: {recipe}\nProgress: {CraftProgress * 100f:F0}%\n" +
                $"Out: {OutputBufferCount()}/{outputCapacity}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}  {_itemsProducedThisMinute}/min";
        }

        private int OutputBufferCount()
        {
            int total = 0;
            foreach (var s in _outputBuffer) total += s.quantity;
            return total;
        }

        // ── Public accessors ───────────────────────────────────────────────

        public Dictionary<ItemDefinition, int> InputBuffer  => _inputBuffer;
        public List<ItemStack>                 OutputBuffer => _outputBuffer;
    }
}
