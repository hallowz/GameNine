using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// ScriptableObject defining one grinder recipe: an input item (ore), an output item (ore dust),
    /// and the processing time in seconds.
    ///
    /// Grinder recipes typically convert raw ore into ore dust.
    /// Ore dust fed into the Electric Furnace produces more ingots than raw ore (outputQuantity on the
    /// smelting recipe is set to 2, approximating the 1.5x yield).
    /// </summary>
    [CreateAssetMenu(fileName = "NewGrinderRecipe", menuName = "Voidborne/Automation/Grinder Recipe")]
    public class GrinderRecipe : ScriptableObject
    {
        [Header("Recipe")]
        public ItemDefinition inputItem;
        public ItemDefinition outputItem;

        [Header("Timing")]
        [Tooltip("Time in seconds to grind one unit of the input item.")]
        public float processTime = 3f;

        /// <summary>Returns true if the given ItemStack can be processed by this recipe.</summary>
        public bool Matches(ItemStack stack)
        {
            if (stack.IsEmpty || inputItem == null) return false;
            return stack.item == inputItem && stack.quantity >= 1;
        }
    }
}
