using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Grinder — converts raw ore into ore dust.
    ///
    /// Ore dust can be:
    ///   • Smelted in the Electric Furnace for ~1.5× ingot yield (via outputQuantity = 2 on the
    ///     dust SmeltingRecipe).
    ///   • Combined in the Assembler to create alloy dusts (e.g., Iron Dust + Copper Dust → Bronze Dust).
    ///
    /// Draws 40 W from the power grid.  Input and output are exposed as IAutomationNode ports
    /// for Hoppers and Conveyor Belts to connect to.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class Grinder : MonoBehaviour, IAutomationNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Recipes")]
        [SerializeField] private List<GrinderRecipe> recipes = new List<GrinderRecipe>();

        [Header("Buffers")]
        [SerializeField] private int inputCapacity  = 8;
        [SerializeField] private int outputCapacity = 8;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer _power;

        private readonly List<ItemStack> _inputBuffer  = new List<ItemStack>();
        private readonly List<ItemStack> _outputBuffer = new List<ItemStack>();

        private GrinderRecipe _activeRecipe;
        private float         _processTimer;

        // ── Public state ───────────────────────────────────────────────────

        public float ProcessProgress =>
            (_activeRecipe != null && _activeRecipe.processTime > 0f)
                ? 1f - Mathf.Clamp01(_processTimer / _activeRecipe.processTime)
                : 0f;

        public bool IsRunning => _power != null && _power.IsPowered && _activeRecipe != null;

        public event Action OnStateChanged;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake() => _power = GetComponent<PowerConsumer>();

        private void Update()
        {
            UpdateStatus();

            if (!_power.IsPowered) { _activeRecipe = null; return; }

            if (_activeRecipe == null)
            {
                _activeRecipe = FindMatchingRecipe();
                if (_activeRecipe != null)
                    _processTimer = _activeRecipe.processTime;
            }

            if (_activeRecipe == null) return;

            if (!CanOutputAccept(_activeRecipe.outputItem)) return;

            _processTimer -= Time.deltaTime;
            if (_processTimer <= 0f) CompleteProcess();
        }

        // ── Processing logic ───────────────────────────────────────────────

        private GrinderRecipe FindMatchingRecipe()
        {
            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                foreach (var stack in _inputBuffer)
                    if (recipe.Matches(stack)) return recipe;
            }
            return null;
        }

        private bool CanOutputAccept(ItemDefinition item)
        {
            if (item == null) return false;
            int existing = 0;
            foreach (var s in _outputBuffer)
                if (s.item == item) existing += s.quantity;
            return existing < outputCapacity * (item.maxStackSize > 0 ? item.maxStackSize : 64);
        }

        private void CompleteProcess()
        {
            if (_activeRecipe == null) return;

            ConsumeOneFrom(_inputBuffer, _activeRecipe.inputItem);
            AddToBuffer(_outputBuffer, new ItemStack(_activeRecipe.outputItem, 1));

            _activeRecipe = null;
            _processTimer = 0f;
            OnStateChanged?.Invoke();
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            int cap = inputCapacity * (item.maxStackSize > 0 ? item.maxStackSize : 64);
            if (BufferCount(_inputBuffer) >= cap) return false;
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

        public string InteractPrompt => "Press E to view Grinder";

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
            string recipe = _activeRecipe != null ? _activeRecipe.name : "Idle";
            statusText.text =
                $"Recipe: {recipe}\nProgress: {ProcessProgress * 100f:F0}%\n" +
                $"In: {BufferCount(_inputBuffer)}  Out: {BufferCount(_outputBuffer)}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}";
        }

        public List<GrinderRecipe>  Recipes       => recipes;
        public List<ItemStack>      InputBuffer   => _inputBuffer;
        public List<ItemStack>      OutputBuffer  => _outputBuffer;
    }
}
