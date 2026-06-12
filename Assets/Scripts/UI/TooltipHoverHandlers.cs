using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Data;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.5 — generic IPointerEnter / IPointerExit handler that shows
    /// <see cref="TooltipUI"/> for an <see cref="ItemDefinition"/>. Used by
    /// <see cref="MachineUI"/>'s input grid + output slot to surface item
    /// tooltips on hover (V4.4 heads-up).
    ///
    /// The bound item is mutable so callers can swap it in lockstep with a
    /// slot's visual refresh.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Optional supplier that returns the current item on hover.
        /// Preferred over <see cref="Item"/> when the slot's bound item
        /// changes frame-to-frame (machine inputs/outputs).</summary>
        public Func<ItemDefinition> ItemSupplier;

        /// <summary>Fallback item when <see cref="ItemSupplier"/> is null.</summary>
        public ItemDefinition Item;

        public void OnPointerEnter(PointerEventData ev)
        {
            ItemDefinition def = ItemSupplier != null ? ItemSupplier() : Item;
            if (def == null) return;
            Vector2 pos = (ev != null) ? ev.position : ReadMouseFallback();
            TooltipUI.Show(def, pos);
        }

        public void OnPointerExit(PointerEventData ev)
        {
            TooltipUI.Hide();
        }

        private static Vector2 ReadMouseFallback()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }
    }

    /// <summary>
    /// Volume 4.5 — IPointerEnter / IPointerExit handler that shows a
    /// machine-family explainer tooltip when the player hovers over the
    /// MachineUI's processType badge.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipMachineHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public MachineDefinition Machine;
        public Func<MachineDefinition> MachineSupplier;

        public void OnPointerEnter(PointerEventData ev)
        {
            MachineDefinition def = MachineSupplier != null ? MachineSupplier() : Machine;
            if (def == null) return;
            Vector2 pos = (ev != null) ? ev.position : Vector2.zero;
            TooltipUI.Show(def, pos);
        }

        public void OnPointerExit(PointerEventData ev)
        {
            TooltipUI.Hide();
        }
    }

    /// <summary>
    /// Volume 4.5 — IPointerEnter / IPointerExit handler that shows a tooltip
    /// for a <see cref="RecipeDefinition"/>. The tooltip surfaces the recipe's
    /// output ItemDefinition (via <see cref="ItemDatabase"/>) so the player
    /// can read its description, properties, and recipe count.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipRecipeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RecipeDefinition Recipe;

        public void OnPointerEnter(PointerEventData ev)
        {
            if (Recipe == null) return;

            ItemDatabase db = ItemDatabase.Instance;
            ItemDefinition output = (db != null) ? db.GetItem(Recipe.OutputId) : null;
            Vector2 pos = (ev != null) ? ev.position : Vector2.zero;

            if (output != null)
            {
                TooltipUI.Show(output, pos);
            }
            else
            {
                string title = !string.IsNullOrEmpty(Recipe.OutputId) ? Recipe.OutputId : "Recipe";
                TooltipUI.Show(title, "Recipe", pos);
            }
        }

        public void OnPointerExit(PointerEventData ev)
        {
            TooltipUI.Hide();
        }
    }

    /// <summary>
    /// Volume 4.5 — IPointerEnter / IPointerExit handler that shows a
    /// <see cref="MaterialProperties"/> description tooltip on hover. Used by
    /// the TooltipUI's own property badges so the player can drill into a
    /// property's meaning.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipPropertyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public MaterialProperties Property;

        public void OnPointerEnter(PointerEventData ev)
        {
            Vector2 pos = (ev != null) ? ev.position : Vector2.zero;
            TooltipUI.Show(Property, pos);
        }

        public void OnPointerExit(PointerEventData ev)
        {
            TooltipUI.Hide();
        }
    }
}
