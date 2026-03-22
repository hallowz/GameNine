using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Circuit Etcher — Tier 2 machine that produces Circuit Boards.
    ///
    /// Circuit Boards are the bottleneck component for all electronics. The process is slow (8 s
    /// per board) and power-hungry (100 W) but irreplaceable for late-game progression.
    ///
    /// Inputs: Copper Plates + Silica (assign via etching recipes in the Inspector).
    /// Output: Circuit Boards.
    ///
    /// Locked behind mid-game progression (crafted from Architect Relic components).
    /// </summary>
    [RequireComponent(typeof(PowerConsumer))]
    public class CircuitEtcher : MonoBehaviour, IAutomationNode, IInteractable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Etching Recipes")]
        [Tooltip("Each EtchingRecipe defines: inputA (Copper Plate), inputB (Silica), output (Circuit Board), processTime.")]
        [SerializeField] private List<EtchingRecipe> etchingRecipes = new List<EtchingRecipe>();

        [Header("Buffers")]
        [SerializeField] private int inputCapacity  = 16;
        [SerializeField] private int outputCapacity = 32;

        [Header("Status Display (optional)")]
        [SerializeField] private TMPro.TMP_Text statusText;

        // ── Runtime ────────────────────────────────────────────────────────

        private PowerConsumer _power;

        private readonly List<ItemStack> _inputBufferA = new List<ItemStack>(); // e.g. Copper Plates
        private readonly List<ItemStack> _inputBufferB = new List<ItemStack>(); // e.g. Silica
        private readonly List<ItemStack> _outputBuffer = new List<ItemStack>();

        private EtchingRecipe _activeRecipe;
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

        private EtchingRecipe FindMatchingRecipe()
        {
            foreach (var recipe in etchingRecipes)
            {
                if (recipe == null) continue;
                if (HasInBuffer(_inputBufferA, recipe.inputItemA) &&
                    HasInBuffer(_inputBufferB, recipe.inputItemB))
                    return recipe;
            }
            return null;
        }

        private static bool HasInBuffer(List<ItemStack> buffer, ItemDefinition item)
        {
            if (item == null) return true; // optional slot
            foreach (var s in buffer)
                if (s.item == item && s.quantity >= 1) return true;
            return false;
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

            ConsumeOneFrom(_inputBufferA, _activeRecipe.inputItemA);
            if (_activeRecipe.inputItemB != null) ConsumeOneFrom(_inputBufferB, _activeRecipe.inputItemB);
            AddToBuffer(_outputBuffer, new ItemStack(_activeRecipe.outputItem, _activeRecipe.outputQuantity));

            _activeRecipe = null;
            _processTimer = 0f;
            OnStateChanged?.Invoke();
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;
            foreach (var recipe in etchingRecipes)
            {
                if (recipe == null) continue;
                if (recipe.inputItemA == item)
                {
                    int cnt = CountInBuffer(_inputBufferA, item);
                    return cnt < inputCapacity;
                }
                if (recipe.inputItemB == item)
                {
                    int cnt = CountInBuffer(_inputBufferB, item);
                    return cnt < inputCapacity;
                }
            }
            return false;
        }

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            foreach (var recipe in etchingRecipes)
            {
                if (recipe == null) continue;
                if (recipe.inputItemA == stack.item && CountInBuffer(_inputBufferA, stack.item) < inputCapacity)
                { AddToBuffer(_inputBufferA, stack); OnStateChanged?.Invoke(); return true; }
                if (recipe.inputItemB == stack.item && CountInBuffer(_inputBufferB, stack.item) < inputCapacity)
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

        // ── IInteractable ──────────────────────────────────────────────────

        public string InteractPrompt => "Press E to view Circuit Etcher";
        public bool CanInteract(Vector3 fromPosition) => Vector3.Distance(fromPosition, transform.position) <= 3f;
        public void Interact(GameObject interactor) { }

        // ── Buffer helpers ─────────────────────────────────────────────────

        private static int CountInBuffer(List<ItemStack> buffer, ItemDefinition item)
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
            string recipe = _activeRecipe != null ? _activeRecipe.name : "Idle";
            statusText.text =
                $"Etching: {recipe}\nProgress: {ProcessProgress * 100f:F0}%\n" +
                $"Out: {TotalItems(_outputBuffer)}\n" +
                $"Power: {(_power.IsPowered ? "ON" : "OFF")}";
        }

        private static int TotalItems(List<ItemStack> buffer)
        {
            int total = 0;
            foreach (var s in buffer) total += s.quantity;
            return total;
        }

        public List<EtchingRecipe> EtchingRecipes => etchingRecipes;
        public List<ItemStack>     OutputBuffer    => _outputBuffer;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recipe data for the Circuit Etcher. Two inputs, one output.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEtchingRecipe", menuName = "Voidborne/Automation/Etching Recipe")]
    public class EtchingRecipe : ScriptableObject
    {
        [Header("Inputs")]
        public ItemDefinition inputItemA;         // e.g. Copper Plate
        public ItemDefinition inputItemB;         // e.g. Silica (optional — null = not required)

        [Header("Output")]
        public ItemDefinition outputItem;         // e.g. Circuit Board
        public int            outputQuantity = 1;

        [Header("Timing")]
        [Tooltip("Time in seconds per cycle. Circuit Boards default to 8 s.")]
        public float processTime = 8f;
    }
}
