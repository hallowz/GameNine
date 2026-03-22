using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne;

namespace Voidborne.Player
{
    /// <summary>
    /// Handles player interaction with IInteractable world objects.
    /// Uses an OverlapSphere each frame when E is pressed to find the nearest
    /// interactable within range, then calls Interact() on it.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactLayers = ~0;

        // Cached collider buffer to avoid allocations every frame
        private readonly Collider[] _hitBuffer = new Collider[16];

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.eKey.wasPressedThisFrame) return;

            // Don't interact while inventory or crafting UI is blocking input
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            TryInteract();
        }

        private void TryInteract()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, interactRange, _hitBuffer, interactLayers);

            IInteractable best    = null;
            float         bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                IInteractable interactable = _hitBuffer[i].GetComponent<IInteractable>();
                if (interactable == null) continue;
                if (!interactable.CanInteract(transform.position)) continue;

                float distSq = (transform.position - _hitBuffer[i].transform.position).sqrMagnitude;
                if (distSq < bestDist)
                {
                    bestDist = distSq;
                    best     = interactable;
                }
            }

            best?.Interact(gameObject);
        }
    }
}
