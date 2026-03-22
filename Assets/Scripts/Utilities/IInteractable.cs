using UnityEngine;

namespace Voidborne
{
    /// <summary>
    /// Implemented by any world object that the player can interact with (press E).
    /// </summary>
    public interface IInteractable
    {
        void Interact(GameObject interactor);
        bool CanInteract(Vector3 fromPosition);
        string InteractPrompt { get; }
    }
}
