using UnityEngine;

/// <summary>
/// Place on a trigger collider in the world to fire a ReachLocation quest objective.
/// Set locationId to match the QuestObjective.locationId field in the relevant QuestDefinition.
/// Fires once per player entry (resets on scene reload).
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuestLocationTrigger : MonoBehaviour
{
    [Tooltip("Must match QuestObjective.locationId exactly.")]
    public string locationId;

    [Tooltip("If true, fires every time the player enters. If false, fires only once.")]
    public bool repeatable = false;

    private bool _fired;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_fired && !repeatable) return;
        if (QuestManager.Instance == null) return;

        _fired = true;
        QuestManager.Instance.UpdateObjective(
            ObjectiveType.ReachLocation,
            locationOrInteractableId: locationId);
    }
}
