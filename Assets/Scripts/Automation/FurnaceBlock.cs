using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne;

namespace Voidborne.Automation
{
    /// <summary>
    /// World-placed furnace block. Has three ItemStack slots (input, fuel, output)
    /// and smelts the input item over time when fuel is present.
    ///
    /// Fuel model: Coal gives 10 seconds of burn time per piece.
    /// Smelting consumes fuel continuously; fuel is consumed one piece at a time as needed.
    ///
    /// Setup in the Unity Editor (or via the FurnaceSetup editor script):
    ///   1. Place the Furnace prefab in the scene (a primitive cube with BoxCollider and this component).
    ///   2. Assign SmeltingRecipe assets to the "recipes" list in the Inspector.
    ///   3. Assign an ItemDefinition for Coal to the "fuelItem" field in the Inspector.
    ///
    /// The player opens the furnace UI by pressing E while within interactRange.
    /// </summary>
    public class FurnaceBlock : MonoBehaviour, IInteractable
    {
        // ------------------------------------------------------------------
        //  Inspector fields
        // ------------------------------------------------------------------

        [SerializeField] private List<SmeltingRecipe> recipes = new List<SmeltingRecipe>();

        [Tooltip("The item that acts as fuel (e.g., Coal). Each piece provides fuelDurationPerPiece seconds.")]
        [SerializeField] private ItemDefinition fuelItem;

        [Tooltip("How many seconds of burn time one fuel item provides.")]
        [SerializeField] private float fuelDurationPerPiece = 10f;

        [Tooltip("Maximum distance at which the player can interact.")]
        [SerializeField] private float interactRange = 3f;

        // ------------------------------------------------------------------
        //  Slots
        // ------------------------------------------------------------------

        public ItemStack InputSlot  { get; private set; }
        public ItemStack FuelSlot   { get; private set; }
        public ItemStack OutputSlot { get; private set; }

        // ------------------------------------------------------------------
        //  Smelting state
        // ------------------------------------------------------------------

        /// <summary>Seconds of fuel remaining in the firebox.</summary>
        private float _remainingFuel;

        /// <summary>
        /// How many seconds remain until the current smelt completes.
        /// Set to recipe.smeltTime when a smelt begins; counts down to 0.
        /// </summary>
        private float _smeltTimer;

        /// <summary>The recipe currently being smelted (null when idle).</summary>
        private SmeltingRecipe _activeRecipe;

        // ------------------------------------------------------------------
        //  Public state
        // ------------------------------------------------------------------

        /// <summary>Smelt progress in [0,1]. 0 = just started, 1 = finished.</summary>
        public float SmeltProgress
        {
            get
            {
                if (_activeRecipe == null || _activeRecipe.smeltTime <= 0f) return 0f;
                return 1f - Mathf.Clamp01(_smeltTimer / _activeRecipe.smeltTime);
            }
        }

        /// <summary>True when the firebox has fuel remaining.</summary>
        public bool IsBurning => _remainingFuel > 0f;

        /// <summary>Fired whenever slot contents or smelting state change (for UI refresh).</summary>
        public event Action OnStateChanged;

        // ------------------------------------------------------------------
        //  IInteractable
        // ------------------------------------------------------------------

        public string InteractPrompt => "Press E to open Furnace";

        public bool CanInteract(Vector3 fromPosition) =>
            Vector3.Distance(fromPosition, transform.position) <= interactRange;

        public void Interact(GameObject interactor)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OpenFurnace(this);
        }

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------

        private void Update()
        {
            TrySmelt();
        }

        // ------------------------------------------------------------------
        //  Smelting logic
        // ------------------------------------------------------------------

        private void TrySmelt()
        {
            // Find a matching recipe if we don't have an active one
            if (_activeRecipe == null)
            {
                _activeRecipe = FindMatchingRecipe();
                if (_activeRecipe != null)
                    _smeltTimer = _activeRecipe.smeltTime;
            }

            if (_activeRecipe == null) return;

            // Check output slot can accept the result
            if (!CanOutputAccept(_activeRecipe.outputItem)) return;

            // Need fuel — try to consume a piece to refuel
            if (_remainingFuel <= 0f)
            {
                if (!TryConsumeFuel()) return; // no fuel available
            }

            // Burn fuel
            _remainingFuel -= Time.deltaTime;
            _smeltTimer    -= Time.deltaTime;

            // If fuel runs out mid-smelt, try to grab another piece immediately
            if (_remainingFuel <= 0f)
            {
                _remainingFuel = 0f;
                TryConsumeFuel(); // top up if possible; if not, smelting pauses next frame
            }

            // Smelt complete
            if (_smeltTimer <= 0f)
            {
                CompleteSmelt();
            }
        }

        private SmeltingRecipe FindMatchingRecipe()
        {
            if (InputSlot.IsEmpty) return null;
            foreach (SmeltingRecipe recipe in recipes)
            {
                if (recipe != null && recipe.Matches(InputSlot))
                    return recipe;
            }
            return null;
        }

        private bool CanOutputAccept(ItemDefinition outputItem)
        {
            if (outputItem == null) return false;
            if (OutputSlot.IsEmpty) return true;
            return OutputSlot.item == outputItem &&
                   OutputSlot.quantity < outputItem.maxStackSize;
        }

        private bool TryConsumeFuel()
        {
            if (FuelSlot.IsEmpty) return false;
            // Accept any fuel item if fuelItem is unassigned, otherwise match
            if (fuelItem != null && FuelSlot.item != fuelItem) return false;

            int newQty = FuelSlot.quantity - 1;
            FuelSlot = newQty > 0
                ? new ItemStack(FuelSlot.item, newQty)
                : new ItemStack(null, 0);

            _remainingFuel += fuelDurationPerPiece;
            OnStateChanged?.Invoke();
            return true;
        }

        private void CompleteSmelt()
        {
            if (_activeRecipe == null) return;

            // Consume one from input
            int newInputQty = InputSlot.quantity - 1;
            InputSlot = newInputQty > 0
                ? new ItemStack(InputSlot.item, newInputQty)
                : new ItemStack(null, 0);

            // Add result to output
            if (OutputSlot.IsEmpty)
                OutputSlot = new ItemStack(_activeRecipe.outputItem, 1);
            else
                OutputSlot = new ItemStack(OutputSlot.item, OutputSlot.quantity + 1);

            // Reset timer; check if we can continue with the next piece
            _activeRecipe = null;
            _smeltTimer   = 0f;

            OnStateChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        //  Slot setters (called by FurnaceUI when the player moves items)
        // ------------------------------------------------------------------

        public void SetInputSlot(ItemStack stack)
        {
            InputSlot     = stack;
            _activeRecipe = null; // re-evaluate on next Update
            _smeltTimer   = 0f;
            OnStateChanged?.Invoke();
        }

        public void SetFuelSlot(ItemStack stack)
        {
            FuelSlot = stack;
            OnStateChanged?.Invoke();
        }

        public void SetOutputSlot(ItemStack stack)
        {
            OutputSlot = stack;
            OnStateChanged?.Invoke();
        }
    }
}
