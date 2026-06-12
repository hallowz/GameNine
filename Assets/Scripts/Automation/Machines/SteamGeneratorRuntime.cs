using UnityEngine;
using Voidborne.Power;
using Voidborne.UI;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Steam Generator (Picky_Specialty). Special-case: the steam
    /// generator does NOT actually craft anything -- it converts an adjacent
    /// boiler's activity into electricity, via the sibling
    /// <see cref="SteamGenerator"/> PowerGenerator component attached by
    /// the V8.3 BlockPlacer.AttachPowerNodeIfPowerItem path.
    ///
    /// <para>This runtime exists to give the steam_generator placed prefab an
    /// IInteractable hook so the player can "open" it and see at a glance
    /// whether it's producing power. The Machine UI shows the (empty) grid +
    /// the standard power gauge; for a real production-meter UI a follow-up
    /// chunk can swap the panel.</para>
    /// </summary>
    public class SteamGeneratorRuntime : MachineRuntime
    {
        private SteamGenerator _steamGen;

        protected override void OnRuntimeInitialized(MachineDefinition def)
        {
            base.OnRuntimeInitialized(def);
            if (_steamGen == null) _steamGen = GetComponent<SteamGenerator>();
        }

        /// <summary>The sibling SteamGenerator component, or null if it hasn't been attached yet.</summary>
        public SteamGenerator SteamGen => _steamGen != null ? _steamGen : GetComponent<SteamGenerator>();

        /// <summary>True if the generator is currently producing power (adjacent boiler is running).</summary>
        public bool IsProducingPower
        {
            get
            {
                var sg = SteamGen;
                return sg != null && sg.IsBoilerActive();
            }
        }
    }
}
