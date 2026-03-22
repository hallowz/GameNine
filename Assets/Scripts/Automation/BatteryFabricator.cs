using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Battery Fabricator — Tier 2 machine that mass-produces Battery Cells.
    ///
    /// Essential for scaling up BatteryBank capacity in large bases.
    /// Inputs: refined Lithium + Copper Plates (configurable via fabrication recipes).
    /// Output: Battery Cells.
    ///
    /// Draws 80 W. Locked behind mid-game Architect Relic reverse-engineering.
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class BatteryFabricator : MonoBehaviour, IAutomationNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Fabrication Recipes")]
        [SerializeField] private List<FabricationRecipe> fabricationRecipes = new List<FabricationRecipe>();

        [Header("Buffers")]
        [SerializeField] private int inputCapacity  = 32;
        [SerializeField] private int outputCapacity = 64;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer _power;

        private readonly List<ItemStack> _inputBufferA = new List<ItemStack>(); // Lithium
        private readonly List<ItemStack> _inputBufferB = new List<ItemStack>(); // Copper Plates
        private readonly List<ItemStack> _outputBuffer = new List<ItemStack>();

        private FabricationRecipe _activeRecipe;
        private float             _processTimer;

        public float ProcessProgress =>
            (_activeRecipe != null && _activeRecipe.processTime > 0f)
                ? 1f - Mathf.Clamp01(_processTimer / _activeRecipe.processTime)
                : 0f;

        public bool IsRunning => _power != null && _power.IsPowered && _activeRecipe != null;
        public event Action OnStateChanged;

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
            if (!CanOutputAccept(_activeRecipe.outputItem, _activeRecipe.outputQuantity)) return;

            _processTimer -= Time.deltaTime;
            if (_processTimer <= 0f) CompleteProcess();
        }

        private FabricationRecipe FindMatchingRecipe()
        {
            foreach (var recipe in fabricationRecipes)
            {
                if (recipe == null) continue;
                bool hasA = HasInBuffer(_inputBufferA, recipe.inputItemA);
                bool hasB = recipe.inputItemB == null || HasInBuffer(_inputBufferB, recipe.inputItemB);
                if (hasA && hasB) return recipe;
            }
            return null;
        }

        private static bool HasInBuffer(List<ItemStack> buffer, ItemDefinition item)
        {
            if (item == null) return true;
            foreach (var s in buffer)
                if (s.item == item && s.quantity >= 1) return true;
            return false;
        }

        private bool CanOutputAccept(ItemDefinition item, int qty)
        {
            if (item == null) return false;
            int existing = 0;
            foreach (var s in _outputBuffer)
                if (s.item == item) existing += s.quantity;
            return (existing + qty) <= outputCapacity;
        }

        private void CompleteProcess()
        {
            if (_activeRecipe == null) return;

            ConsumeOneFrom(_inputBufferA, _activeRecipe.inputItemA);
            if (_activeRecipe.inputItemB != null)
                ConsumeOneFrom(_inputBufferB, _activeRecipe.inputItemB);
            AddToBuffer(_outputBuffer, new ItemStack(_activeRecipe.outputItem,
                                                     Mathf.Max(1, _activeRecipe.outputQuantity)));
            _activeRecipe = null;
            _processTimer = 0f;
            OnStateChanged?.Invoke();
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            foreach (var recipe in fabricationRecipes)
            {
                if (recipe == null) continue;
                if (recipe.inputItemA == item && CountIn(_inputBufferA, item) < inputCapacity) return true;
                if (recipe.inputItemB == item && CountIn(_inputBufferB, item) < inputCapacity) return true;
            }
            return false;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            foreach (var recipe in fabricationRecipes)
            {
                if (recipe == null) continue;
                if (recipe.inputItemA == stack.item && CountIn(_inputBufferA, stack.item) < inputCapacity)
                { AddToBuffer(_inputBufferA, stack); OnStateChanged?.Invoke(); return true; }
                if (recipe.inputItemB == stack.item && CountIn(_inputBufferB, stack.item) < inputCapacity)
                { AddToBuffer(_inputBufferB, stack); OnStateChanged?.Invoke(); return true; }
            }
            return false;
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

        public string InteractPrompt => "Press E to view Battery Fabricator";
        public bool CanInteract(Vector3 fromPosition) => Vector3.Distance(fromPosition, transform.position) <= 3f;
        public void Interact(GameObject interactor) { }

        private static int CountIn(List<ItemStack> buffer, ItemDefinition item)
        {
            int total = 0;
            foreach (var s in buffer)
                if (s.item == item) total += s.quantity;
            return total;
        }

        private static void AddToBuffer(List<ItemStack> buffer, ItemStack stack)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].item == stack.item)
                { buffer[i] = new ItemStack(buffer[i].item, buffer[i].quantity + stack.quantity); return; }
            }
            buffer.Add(stack);
        }

        private static void ConsumeOneFrom(List<ItemStack> buffer, ItemDefinition item)
        {
            if (item == null) return;
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

        private void UpdateStatus()
        {
            if (statusText == null) return;
            statusText.text =
                $"Recipe: {(_activeRecipe != null ? _activeRecipe.name : "Idle")}\n" +
                $"Progress: {ProcessProgress * 100f:F0}%\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}";
        }

        public List<FabricationRecipe> FabricationRecipes => fabricationRecipes;
        public List<ItemStack>         OutputBuffer        => _outputBuffer;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recipe data for the Battery Fabricator: two inputs, one output.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFabricationRecipe", menuName = "Voidborne/Automation/Fabrication Recipe")]
    public class FabricationRecipe : ScriptableObject
    {
        [Header("Inputs")]
        public ItemDefinition inputItemA;           // e.g. Lithium
        public ItemDefinition inputItemB;           // e.g. Copper Plate (optional)

        [Header("Output")]
        public ItemDefinition outputItem;           // e.g. Battery Cell
        public int            outputQuantity = 1;

        [Header("Timing")]
        public float processTime = 5f;
    }
}
