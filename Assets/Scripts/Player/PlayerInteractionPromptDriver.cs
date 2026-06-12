using UnityEngine;
using Voidborne.UI;

namespace Voidborne.Player
{
    /// <summary>
    /// Volume 4.6 — drives <see cref="InteractionPromptUI"/> from the player's
    /// forward-facing center-screen scan. Each frame, looks for the nearest
    /// <see cref="Voidborne.IInteractable"/> within range and tells the
    /// prompt UI to show or hide accordingly.
    ///
    /// This script intentionally lives alongside <see cref="PlayerInteraction"/>
    /// without modifying it — the existing E-key interaction logic stays
    /// owner-authoritative; this driver is purely cosmetic (client-local
    /// prompt rendering).
    ///
    /// Coop note: client-local. The interaction action itself remains in
    /// <see cref="PlayerInteraction"/> and is owner-authoritative.
    /// </summary>
    public class PlayerInteractionPromptDriver : MonoBehaviour
    {
        [Tooltip("Sphere radius around the player used to find IInteractable candidates.")]
        [SerializeField] private float scanRadius = 3f;

        [Tooltip("Layers considered when scanning for IInteractable components.")]
        [SerializeField] private LayerMask scanLayers = ~0;

        // Cached collider buffer to avoid per-frame allocations.
        private readonly Collider[] _hitBuffer = new Collider[16];

        private void Update()
        {
            // Suppress the prompt while modals are open — pointer focus is on
            // the inventory / machine UI, not the world.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            {
                HidePrompt();
                return;
            }

            IInteractable best = FindBestInteractable();
            if (best != null && !string.IsNullOrEmpty(best.InteractPrompt))
            {
                ShowPrompt(best.InteractPrompt);
            }
            else
            {
                HidePrompt();
            }
        }

        private IInteractable FindBestInteractable()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, scanRadius, _hitBuffer, scanLayers);

            IInteractable best    = null;
            float         bestSq  = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hitBuffer[i];
                if (hit == null) continue;

                IInteractable candidate = hit.GetComponent<IInteractable>();
                if (candidate == null) continue;
                if (!candidate.CanInteract(transform.position)) continue;

                float sq = (transform.position - hit.transform.position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best   = candidate;
                }
            }

            return best;
        }

        private static void ShowPrompt(string prompt)
        {
            InteractionPromptUI ui = InteractionPromptUI.Instance;
            if (ui == null) return;

            // Legacy IInteractable.InteractPrompt strings already contain
            // "Press E to ...". Normalise to the V4.6 corner-of-screen form
            // when the legacy "Press E to" prefix is present.
            string formatted = NormalizePrompt(prompt);
            ui.Show(formatted);
        }

        private static void HidePrompt()
        {
            InteractionPromptUI ui = InteractionPromptUI.Instance;
            if (ui == null) return;
            ui.Hide();
        }

        private static string NormalizePrompt(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            // Replace "Press E to" with "[E]" — the V4.6 design form. Most
            // IInteractable implementations use the legacy phrasing; this
            // gives every interactable the new shape for free.
            const string legacyPrefix = "Press E to ";
            if (raw.StartsWith(legacyPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                string tail = raw.Substring(legacyPrefix.Length);
                if (tail.Length == 0) return "[E]";
                // Capitalise the verb so "[E] Open Workbench" reads cleanly.
                char first = char.ToUpperInvariant(tail[0]);
                string rest = tail.Length > 1 ? tail.Substring(1) : string.Empty;
                return $"[E] {first}{rest}";
            }
            if (raw.StartsWith("[E]")) return raw;
            return $"[E] {raw}";
        }
    }
}
