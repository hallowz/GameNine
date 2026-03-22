using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Electric Furnace — automated smelting machine.
    ///
    /// Faster than the manual FurnaceBlock (default 2 s vs 5 s per smelt) and requires no fuel —
    /// draws 60 W from the power grid instead.  Uses the same SmeltingRecipe ScriptableObjects.
    ///
    /// The Electric Furnace acts as an IAutomationNode: a Hopper placed on its input side inserts
    /// raw ore; a Hopper on the output side extracts ingots.
    ///
    /// Ore dust recipes (outputQuantity = 2 on the SmeltingRecipe) yield extra ingots, rewarding
    /// players who route ore through the Grinder first.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class ElectricFurnace : MonoBehaviour, IAutomationNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Recipes")]
        [Tooltip("Smelting recipes this furnace can process.")]
        [SerializeField] private List<SmeltingRecipe> recipes = new List<SmeltingRecipe>();

        [Header("Buffers")]
        [SerializeField] private int inputCapacity  = 8;
        [SerializeField] private int outputCapacity = 8;

        [Header("Speed")]
        [Tooltip("Multiplier applied to recipe smeltTime. Values < 1 = faster than manual.")]
        [SerializeField] private float speedMultiplier = 0.4f; // 0.4 × 5 s = 2 s per smelt

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer _power;

        private readonly List<ItemStack> _inputBuffer  = new List<ItemStack>();
        private readonly List<ItemStack> _outputBuffer = new List<ItemStack>();

        private SmeltingRecipe _activeRecipe;
        private float          _smeltTimer;      // seconds remaining on current smelt
        private int            _itemsProducedThisMinute;
        private float          _minuteTimer;

        // ── Public state ───────────────────────────────────────────────────

        public float SmeltProgress =>
            (_activeRecipe != null && _activeRecipe.smeltTime * speedMultiplier > 0f)
                ? 1f - Mathf.Clamp01(_smeltTimer / (_activeRecipe.smeltTime * speedMultiplier))
                : 0f;

        public bool IsRunning => _power != null && _power.IsPowered && _activeRecipe != null;

        public int InputCount  => BufferCount(_inputBuffer);
        public int OutputCount => BufferCount(_outputBuffer);

        public event Action OnStateChanged;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake() => _power = GetComponent<PowerConsumer>();

        private void Update()
        {
            _minuteTimer += Time.deltaTime;
            if (_minuteTimer >= 60f)
            {
                _itemsProducedThisMinute = 0;
                _minuteTimer = 0f;
            }

            UpdateStatus();

            if (!_power.IsPowered) { _activeRecipe = null; return; }

            if (_activeRecipe == null)
            {
                _activeRecipe = FindMatchingRecipe();
                if (_activeRecipe != null)
                    _smeltTimer = _activeRecipe.smeltTime * speedMultiplier;
            }

            if (_activeRecipe == null) return;

            if (!CanOutputAccept(_activeRecipe.outputItem, _activeRecipe.outputQuantity)) return;

            _smeltTimer -= Time.deltaTime;
            if (_smeltTimer <= 0f) CompleteSmelt();
        }

        // ── Processing logic ───────────────────────────────────────────────

        private SmeltingRecipe FindMatchingRecipe()
        {
            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                foreach (var stack in _inputBuffer)
                    if (recipe.Matches(stack)) return recipe;
            }
            return null;
        }

        private bool CanOutputAccept(ItemDefinition item, int qty)
        {
            if (item == null) return false;
            // Check there is room for qty items in the output buffer.
            int existing = 0;
            foreach (var s in _outputBuffer)
                if (s.item == item) existing += s.quantity;
            return (existing + qty) <= outputCapacity * item.maxStackSize;
        }

        private void CompleteSmelt()
        {
            if (_activeRecipe == null) return;

            // Consume one input item.
            ConsumeOneFrom(_inputBuffer, _activeRecipe.inputItem);

            // Produce output.
            int qty = Mathf.Max(1, _activeRecipe.outputQuantity);
            AddToBuffer(_outputBuffer, new ItemStack(_activeRecipe.outputItem, qty));
            _itemsProducedThisMinute += qty;

            _activeRecipe = null;
            _smeltTimer   = 0f;
            OnStateChanged?.Invoke();
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            if (BufferCount(_inputBuffer) >= inputCapacity * (item.maxStackSize > 0 ? item.maxStackSize : 64)) return false;
            foreach (var recipe in recipes)
                if (recipe != null && recipe.inputItem == item) return true;
            return false;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (!CanAccept(stack.item)) return false;
            AddToBuffer(_inputBuffer, stack);
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

        public string InteractPrompt => "Press E to view Electric Furnace";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= 3f;

        public void Interact(GameObject interactor) { /* Future: open status UI */ }

        // ── Buffer helpers ─────────────────────────────────────────────────

        private static int BufferCount(List<ItemStack> buffer)
        {
            int total = 0;
            foreach (var s in buffer) total += s.quantity;
            return total;
        }

        private static void AddToBuffer(List<ItemStack> buffer, ItemStack stack)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].item == stack.item)
                {
                    buffer[i] = new ItemStack(buffer[i].item, buffer[i].quantity + stack.quantity);
                    return;
                }
            }
            buffer.Add(stack);
        }

        private static void ConsumeOneFrom(List<ItemStack> buffer, ItemDefinition item)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].item == item)
                {
                    buffer[i] = new ItemStack(buffer[i].item, buffer[i].quantity - 1);
                    if (buffer[i].quantity <= 0) buffer.RemoveAt(i);
                    return;
                }
            }
        }

        // ── Status display ─────────────────────────────────────────────────

        private void UpdateStatus()
        {
            if (statusText == null) return;
            string recipe  = _activeRecipe != null ? _activeRecipe.name : "Idle";
            string progress = _activeRecipe != null ? $"{SmeltProgress * 100f:F0}%" : "--";
            statusText.text =
                $"Recipe: {recipe}\nProgress: {progress}\n" +
                $"In: {InputCount}/{inputCapacity}  Out: {OutputCount}/{outputCapacity}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}  {_itemsProducedThisMinute}/min";
        }

        // ── Public accessors ───────────────────────────────────────────────

        public List<SmeltingRecipe> Recipes         => recipes;
        public List<ItemStack>      InputBuffer      => _inputBuffer;
        public List<ItemStack>      OutputBuffer     => _outputBuffer;
        public int                  ItemsPerMinute   => _itemsProducedThisMinute;
    }
}
