using System;
using System.Collections.Generic;
using Voidborne.Crafting;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.4 — in-memory placeholder implementation of
    /// <see cref="IMachineInputProvider"/>. Used by EditMode tests and by any
    /// early integration that wants to drive MachineUI before V9.1 lands the
    /// real MachineRuntime.
    ///
    /// All state is plain C# fields. Mutators raise <see cref="OnStateChanged"/>
    /// so the UI can refresh.
    ///
    /// Coop note: client-local stub. The real provider in V9.1 will bridge
    /// ServerRpc state from MachineRuntime; this stub never goes to the wire.
    /// </summary>
    public sealed class StubMachineInputProvider : IMachineInputProvider
    {
        private readonly List<ItemStack> _inputs;
        private readonly List<ItemStack> _outputs;

        public StubMachineInputProvider(int inputSlotCount = 9, int outputSlotCount = 1)
        {
            if (inputSlotCount < 0)  inputSlotCount  = 0;
            if (outputSlotCount < 0) outputSlotCount = 0;

            _inputs  = new List<ItemStack>(inputSlotCount);
            _outputs = new List<ItemStack>(outputSlotCount);

            for (int i = 0; i < inputSlotCount;  i++) _inputs.Add(default);
            for (int i = 0; i < outputSlotCount; i++) _outputs.Add(default);
        }

        // ---------------------------------------------------------------
        //  IMachineInputProvider
        // ---------------------------------------------------------------

        public IReadOnlyList<ItemStack> Inputs  => _inputs;
        public IReadOnlyList<ItemStack> Outputs => _outputs;

        public float Progress { get; private set; }
        public bool  NeedsPower { get; set; }
        public float PowerSatisfaction { get; private set; } = 1f;
        public bool  NeedsFuel { get; set; }
        public ItemStack FuelSlot { get; private set; }

        public event Action OnStateChanged;

        public RecipeDefinition ActiveRecipe { get; private set; }

        public void TryStartRecipe(RecipeDefinition recipe)
        {
            ActiveRecipe = recipe;
            Progress = 0f;
            Raise();
        }

        public void CancelRecipe()
        {
            ActiveRecipe = null;
            Progress = 0f;
            Raise();
        }

        // ---------------------------------------------------------------
        //  Test / integration mutators (not on the interface)
        // ---------------------------------------------------------------

        public void SetInput(int index, ItemStack stack)
        {
            if (index < 0 || index >= _inputs.Count) return;
            _inputs[index] = stack;
            Raise();
        }

        public void SetOutput(int index, ItemStack stack)
        {
            if (index < 0 || index >= _outputs.Count) return;
            _outputs[index] = stack;
            Raise();
        }

        public void SetFuel(ItemStack stack)
        {
            FuelSlot = stack;
            Raise();
        }

        public void SetProgress(float normalized)
        {
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            Progress = normalized;
            Raise();
        }

        public void SetPowerSatisfaction(float normalized)
        {
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            PowerSatisfaction = normalized;
            Raise();
        }

        /// <summary>Manually trigger an OnStateChanged broadcast. Useful when a
        /// test mutates several values via the property setters and wants a
        /// single refresh at the end.</summary>
        public void Raise() => OnStateChanged?.Invoke();
    }
}
