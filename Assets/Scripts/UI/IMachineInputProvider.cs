using System;
using System.Collections.Generic;
using Voidborne.Crafting;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.4 — runtime contract between MachineUI and the underlying
    /// machine simulation. MachineUI is client-local and never owns simulation
    /// state; this interface is the bridge it talks through.
    ///
    /// V9.1 will provide the real implementation on top of MachineRuntime
    /// (server-authoritative). V4.4 ships a <see cref="StubMachineInputProvider"/>
    /// for tests and early integration.
    ///
    /// All numeric values are read-only from the UI's point of view; writes
    /// flow through the explicit Try* methods (which forward to the simulation
    /// via ServerRpc in V9 / V21 co-op terms).
    /// </summary>
    public interface IMachineInputProvider
    {
        /// <summary>Current contents of the machine's input grid.
        /// Size matches <c>MachineDefinition.gridWidth * gridHeight</c>.</summary>
        IReadOnlyList<ItemStack> Inputs { get; }

        /// <summary>Current contents of the machine's output slot(s).</summary>
        IReadOnlyList<ItemStack> Outputs { get; }

        /// <summary>Normalized 0..1 progress of the currently-active recipe.
        /// 0 if no recipe is running.</summary>
        float Progress { get; }

        /// <summary>True if this machine requires electrical power to operate.</summary>
        bool NeedsPower { get; }

        /// <summary>Normalized 0..1 power satisfaction. 1 = fully powered,
        /// 0 = no power. UI shows this as a gauge under the progress bar.</summary>
        float PowerSatisfaction { get; }

        /// <summary>True if this machine consumes fuel (thermal machines).</summary>
        bool NeedsFuel { get; }

        /// <summary>Current contents of the dedicated fuel slot. Empty when
        /// <see cref="NeedsFuel"/> is false.</summary>
        ItemStack FuelSlot { get; }

        /// <summary>Fired when any of the above values change. MachineUI
        /// subscribes to drive its refresh.</summary>
        event Action OnStateChanged;

        /// <summary>Attempt to start the given recipe on this machine. The UI
        /// invokes this when the player clicks a recipe tab and the inputs
        /// match. The simulation is the source of truth for whether the start
        /// actually happens — this is a one-way request, not a query.</summary>
        void TryStartRecipe(RecipeDefinition recipe);

        /// <summary>Abort the currently-running recipe (if any).</summary>
        void CancelRecipe();
    }
}
