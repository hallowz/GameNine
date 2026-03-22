using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// ScriptableObject defining one press recipe: an input item (ingot) shaped into an output item
    /// (plate or rod) over a processing time.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPressRecipe", menuName = "Voidborne/Automation/Press Recipe")]
    public class PressRecipe : ScriptableObject
    {
        [Header("Recipe")]
        public ItemDefinition inputItem;
        public ItemDefinition outputItem;
        [Tooltip("How many output items are produced per press cycle.")]
        public int outputQuantity = 1;

        [Header("Timing")]
        [Tooltip("Time in seconds to press one unit.")]
        public float processTime = 4f;

        /// <summary>Returns true if the given ItemStack can be processed by this recipe.</summary>
        public bool Matches(ItemStack stack)
        {
            if (stack.IsEmpty || inputItem == null) return false;
            return stack.item == inputItem && stack.quantity >= 1;
        }
    }
}
