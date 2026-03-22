using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// ScriptableObject defining one smelting recipe: an input item, an output item,
    /// and the time in seconds it takes to smelt one unit.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSmeltingRecipe", menuName = "Voidborne/Automation/Smelting Recipe")]
    public class SmeltingRecipe : ScriptableObject
    {
        [Header("Recipe")]
        public ItemDefinition inputItem;
        public ItemDefinition outputItem;

        [Header("Timing")]
        [Tooltip("Time in seconds to smelt one unit of the input item.")]
        public float smeltTime = 5f;

        [Header("Output")]
        [Tooltip("How many output items are produced per smelt. Default 1. Dust recipes use 2 for 1.5x yield approximation.")]
        public int outputQuantity = 1;

        /// <summary>Returns true if the given ItemStack contains at least one unit of the input item.</summary>
        public bool Matches(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            if (inputItem == null) return false;
            return stack.item == inputItem && stack.quantity >= 1;
        }
    }
}
