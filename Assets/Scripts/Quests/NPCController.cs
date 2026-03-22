using UnityEngine;
using Voidborne;
using Voidborne.Player;

/// <summary>
/// MonoBehaviour attached to Safehold NPC GameObjects.
/// Handles idle look-around, player detection, interact prompt, and starting dialogue.
/// Implements IInteractable so PlayerInteraction picks it up.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class NPCController : MonoBehaviour, IInteractable
{
    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [SerializeField] public NPCDefinition definition;

    [Header("Behaviour")]
    [SerializeField] private float lookAroundInterval  = 3f;
    [SerializeField] private float lookTurnSpeed       = 90f;   // deg/s
    [SerializeField] private float interactRange       = 5f;

    // ---------------------------------------------------------------
    //  IInteractable
    // ---------------------------------------------------------------

    public string InteractPrompt => definition != null ? $"Talk to {definition.npcName}" : "Talk";

    public bool CanInteract(Vector3 fromPosition)
        => Vector3.Distance(transform.position, fromPosition) <= interactRange;

    public void Interact(GameObject interactor)
    {
        if (definition == null || definition.defaultDialogue == null)
        {
            Debug.LogWarning($"[NPCController] {name}: no NPCDefinition or DialogueTree assigned.");
            return;
        }

        if (UIManager.Instance == null) return;

        // Fire InteractWith objective for quests
        if (QuestManager.Instance != null)
            QuestManager.Instance.UpdateObjective(
                ObjectiveType.InteractWith,
                locationOrInteractableId: definition.npcId);

        UIManager.Instance.OpenDialogue(definition, interactor);
    }

    // ---------------------------------------------------------------
    //  Idle look-around
    // ---------------------------------------------------------------

    private float _lookTimer;
    private Quaternion _targetRot;

    private void Awake()
    {
        // Ensure the collider is a trigger so it doesn't block physics
        SphereCollider sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius    = interactRange;

        _targetRot = transform.rotation;
    }

    private void Update()
    {
        // Look towards player when close
        Transform player = GetNearbyPlayer();
        if (player != null)
        {
            Vector3 dir    = player.position - transform.position;
            dir.y          = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, lookTurnSpeed * Time.deltaTime);
            }
            _lookTimer = 0f;
            return;
        }

        // Idle: pick a new random yaw direction periodically
        _lookTimer -= Time.deltaTime;
        if (_lookTimer <= 0f)
        {
            float yaw  = Random.Range(-90f, 90f);
            _targetRot = Quaternion.Euler(0f, transform.eulerAngles.y + yaw, 0f);
            _lookTimer = lookAroundInterval + Random.Range(-1f, 1f);
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, _targetRot, lookTurnSpeed * Time.deltaTime);
    }

    private Transform GetNearbyPlayer()
    {
        // Cheap check: find first-person controller as player proxy
        FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc == null) return null;
        return Vector3.Distance(transform.position, fpc.transform.position) <= interactRange
            ? fpc.transform : null;
    }

    // ---------------------------------------------------------------
    //  Gizmo
    // ---------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(0.2f, 1f, 0.4f, 0.25f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, interactRange);
        if (definition != null)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"{definition.npcName} ({definition.safeholdName})");
        }
    }
#endif
}
