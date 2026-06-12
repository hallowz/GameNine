using System.Collections;
using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.2 — Hinge interaction for door-form placed blocks (e.g. wood_door).
    ///
    /// Implements <see cref="IInteractable"/> so the existing V4.6
    /// InteractionPromptDriver picks it up automatically (press E in range).
    /// Toggles the door's local Y rotation between 0 deg (closed) and 90 deg
    /// (open) via a simple <see cref="Mathf.Lerp"/> over <see cref="openDurationSeconds"/>.
    /// No physics, no collision-disabling — the placed-prefab's BoxCollider
    /// stays active so opening into a wall still feels solid (M7 polish swaps
    /// in a swept-volume open check).
    /// </summary>
    /// <remarks>
    /// Coop note: open/close state runs locally for M2. V21 wires net replication
    /// via a NetworkVariable&lt;bool&gt; IsOpen with a ServerRpc Toggle.
    /// </remarks>
    [DisallowMultipleComponent]
    public class DoorInteraction : MonoBehaviour, Voidborne.IInteractable
    {
        [Tooltip("How long (seconds) the open/close lerp takes.")]
        [SerializeField] private float openDurationSeconds = 0.35f;

        [Tooltip("Open angle around local Y, in degrees. Positive opens 'outward', negative 'inward'.")]
        [SerializeField] private float openAngleDeg = 90f;

        [Tooltip("Maximum range at which the player can interact with the door.")]
        [SerializeField] private float interactRange = 3f;

        [Tooltip("True if the door is currently in (or animating to) the open state.")]
        [SerializeField] private bool isOpen;

        private Quaternion _closedRotation;
        private Coroutine _animRoutine;

        /// <summary>True if the door is currently open (or opening).</summary>
        public bool IsOpen => isOpen;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            _closedRotation = transform.localRotation;
        }

        // ---------------------------------------------------------------
        //  IInteractable
        // ---------------------------------------------------------------

        public string InteractPrompt => isOpen ? "Close Door" : "Open Door";

        public bool CanInteract(Vector3 fromPosition)
        {
            if (interactRange <= 0f) return true;
            float distSqr = (fromPosition - transform.position).sqrMagnitude;
            return distSqr <= interactRange * interactRange;
        }

        public void Interact(GameObject interactor)
        {
            Toggle();
        }

        // ---------------------------------------------------------------
        //  Toggle / state mutators
        // ---------------------------------------------------------------

        /// <summary>Open / close the door. Public so V7 tests + V21 ServerRpc can drive it.</summary>
        public void Toggle()
        {
            isOpen = !isOpen;
            StartAnim();
        }

        /// <summary>Force the door to the given state without animating (test seam + save/load).</summary>
        public void SetOpenImmediate(bool open)
        {
            isOpen = open;
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
            transform.localRotation = open
                ? _closedRotation * Quaternion.AngleAxis(openAngleDeg, Vector3.up)
                : _closedRotation;
        }

        // ---------------------------------------------------------------
        //  Internal animation
        // ---------------------------------------------------------------

        private void StartAnim()
        {
            if (!isActiveAndEnabled)
            {
                // EditMode / disabled GameObject path: snap to target.
                SetOpenImmediate(isOpen);
                return;
            }
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRotation());
        }

        private IEnumerator AnimateRotation()
        {
            Quaternion start = transform.localRotation;
            Quaternion target = isOpen
                ? _closedRotation * Quaternion.AngleAxis(openAngleDeg, Vector3.up)
                : _closedRotation;
            float duration = openDurationSeconds > 0.01f ? openDurationSeconds : 0.01f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                transform.localRotation = Quaternion.Slerp(start, target, u);
                yield return null;
            }
            transform.localRotation = target;
            _animRoutine = null;
        }
    }
}
