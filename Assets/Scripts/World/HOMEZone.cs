using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The Architect's pre-built pocket dimension. Extends PocketDimensionZone.
///
/// Interactive elements:
///   • The Desk        — opens ChronologicalLogUI showing all Architect journal entries.
///   • The Build Wall  — static visual display of original stronghold blueprints.
///   • The Workshop Bench — CraftingStation with HOME-exclusive schematics.
///   • The Back Room   — sealed case with an ambiguous optional component (player choice).
///   • The Window      — trigger zone plays the single ambient voice log once on first approach.
///
/// All five elements are expected as direct children of this GameObject.
/// Assign them in the Inspector or let Awake auto-locate by name tag.
/// </summary>
public class HOMEZone : PocketDimensionZone
{
    // ---------------------------------------------------------------
    //  Inspector references
    // ---------------------------------------------------------------

    [Header("HOME Interactive Elements")]
    [Tooltip("The Desk — opens the ChronologicalLogUI. Assign its root GO.")]
    [SerializeField] private GameObject theDesk;

    [Tooltip("The Workshop Bench — assign the CraftingStation GO.")]
    [SerializeField] private GameObject theWorkshopBench;

    [Tooltip("The Back Room — sealed case GO with player choice trigger.")]
    [SerializeField] private GameObject theBackRoom;

    [Tooltip("The Window — trigger zone that plays the Architect voice log once.")]
    [SerializeField] private GameObject theWindow;

    [Header("Audio")]
    [Tooltip("The single ambient voice log. Plays once on first approach to The Window.")]
    [SerializeField] private AudioClip  architectVoiceLog;

    [Header("Events")]
    [Tooltip("Fires when the player makes their choice at The Back Room (true = took it).")]
    public UnityEvent<bool> OnBackRoomChoiceMade;

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------

    private bool _voiceLogPlayed;
    private bool _backRoomChoiceMade;
    private AudioSource _audioSource;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        AutoLocateElements();
        SetupWindowTrigger();
        SetupDeskInteraction();
        SetupBackRoomInteraction();
    }

    // ---------------------------------------------------------------
    //  Override
    // ---------------------------------------------------------------

    public override void OnPlayerEnter()
    {
        base.OnPlayerEnter();
        Debug.Log("[HOMEZone] Welcome to HOME.");
    }

    // ---------------------------------------------------------------
    //  Setup helpers
    // ---------------------------------------------------------------

    private void AutoLocateElements()
    {
        if (theDesk          == null) theDesk          = FindChildByName("TheDesk");
        if (theWorkshopBench == null) theWorkshopBench = FindChildByName("TheWorkshopBench");
        if (theBackRoom      == null) theBackRoom      = FindChildByName("TheBackRoom");
        if (theWindow        == null) theWindow        = FindChildByName("TheWindow");
    }

    private GameObject FindChildByName(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.gameObject : null;
    }

    // ---------------------------------------------------------------
    //  The Window — one-shot voice log
    // ---------------------------------------------------------------

    private void SetupWindowTrigger()
    {
        if (theWindow == null) return;

        // Ensure trigger collider exists.
        var col = theWindow.GetComponent<Collider>();
        if (col == null)
        {
            col = theWindow.AddComponent<SphereCollider>();
            ((SphereCollider)col).radius = 3f;
        }
        col.isTrigger = true;

        // Attach detector component.
        var listener = theWindow.AddComponent<HomeTriggerListener>();
        listener.Initialize(() => TryPlayVoiceLog());
    }

    private void TryPlayVoiceLog()
    {
        if (_voiceLogPlayed) return;
        _voiceLogPlayed = true;

        if (architectVoiceLog != null && _audioSource != null)
            _audioSource.PlayOneShot(architectVoiceLog);

        Debug.Log("[HOMEZone] The Window voice log playing (one-shot).");
    }

    // ---------------------------------------------------------------
    //  The Desk — opens ChronologicalLogUI
    // ---------------------------------------------------------------

    private void SetupDeskInteraction()
    {
        if (theDesk == null) return;

        var interactable = theDesk.GetComponent<HomeInteractable>();
        if (interactable == null)
            interactable = theDesk.AddComponent<HomeInteractable>();

        interactable.OnInteract += OpenJournal;
    }

    private void OpenJournal()
    {
        var logUI = FindFirstObjectByType<ChronologicalLogUI>();
        if (logUI != null)
            logUI.Show();
        else
            Debug.LogWarning("[HOMEZone] ChronologicalLogUI not found in scene.");
    }

    // ---------------------------------------------------------------
    //  The Back Room — player choice
    // ---------------------------------------------------------------

    private void SetupBackRoomInteraction()
    {
        if (theBackRoom == null) return;

        var interactable = theBackRoom.GetComponent<HomeInteractable>();
        if (interactable == null)
            interactable = theBackRoom.AddComponent<HomeInteractable>();

        interactable.OnInteract += PresentBackRoomChoice;
    }

    private void PresentBackRoomChoice()
    {
        if (_backRoomChoiceMade) return;
        // Prompt UI handled externally (DialogueUI with a yes/no choice is sufficient).
        // For now we log; the actual dialogue integration is wired up during scene setup.
        Debug.Log("[HOMEZone] The Back Room: player approaches sealed case. (choice TBD via dialogue)");
    }

    /// <summary>Call from DialogueAction or a choice button to record the player's decision.</summary>
    public void RecordBackRoomChoice(bool tookComponent)
    {
        if (_backRoomChoiceMade) return;
        _backRoomChoiceMade = true;
        OnBackRoomChoiceMade?.Invoke(tookComponent);
        Debug.Log($"[HOMEZone] Back Room choice: {(tookComponent ? "took component" : "left it")}.");
    }
}

// -----------------------------------------------------------------------
//  Helper components — kept in this file to reduce file count
// -----------------------------------------------------------------------

/// <summary>Fires a callback once when the player enters the trigger.</summary>
public class HomeTriggerListener : MonoBehaviour
{
    private System.Action _callback;
    private bool _fired;

    public void Initialize(System.Action callback) => _callback = callback;

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;
        _fired = true;
        _callback?.Invoke();
    }
}

/// <summary>Simple interactable component for HOME zone elements.</summary>
public class HomeInteractable : MonoBehaviour
{
    public event System.Action OnInteract;

    [Tooltip("Displayed when player looks at this object.")]
    public string interactPrompt = "Interact [E]";

    /// <summary>Called by PlayerInteraction when the player presses E on this object.</summary>
    public void Interact() => OnInteract?.Invoke();
}
