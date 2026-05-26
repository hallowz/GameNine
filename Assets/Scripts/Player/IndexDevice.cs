using System;
using UnityEngine;

/// <summary>
/// Core state tracker for the Index Device system.
/// Lives on the Player root GameObject alongside IndexAbilities and IndexDevice_PocketDimension.
///
/// Responsibilities:
///   • Tracks which of the six modules are installed (bool[6], indexed 0–5).
///   • Swaps wrist-mesh child GameObjects as modules are restored.
///   • Tracks which installed module is currently "active" (the one Q fires).
///   • Exposes InstallModule / IsModuleInstalled / ActivateModule API.
///   • Fires OnModuleInstalled for QuestEventBridge and the HUD.
///
/// Input handling (Tab to cycle, Q to activate) lives in IndexAbilities.
/// </summary>
public class IndexDevice : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------

    public const int ModuleCount = 6;

    // ---------------------------------------------------------------
    //  Events
    // ---------------------------------------------------------------

    /// <summary>Fired when a module is installed. Arg: module index (0–5).</summary>
    public static event Action<int> OnModuleInstalled;

    /// <summary>Fired when the player cycles the active module. Arg: new active index (−1 = none).</summary>
    public static event Action<int> OnActiveModuleChanged;

    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Header("Wrist Model — child meshes revealed per module (index 0–5)")]
    [Tooltip("Assign 6 child GameObjects that represent the restored wrist-device parts.")]
    [SerializeField] private GameObject[] wristMeshes = new GameObject[ModuleCount];

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------

    private readonly bool[] _installed     = new bool[ModuleCount];
    private int              _activeModule  = -1;   // -1 = no active selection

    // ---------------------------------------------------------------
    //  Properties
    // ---------------------------------------------------------------

    /// <summary>True if the player has the Index Device (i.e. this component exists).</summary>
    public bool HasDevice => true;

    /// <summary>Currently selected module index, or −1 if none.</summary>
    public int ActiveModuleIndex => _activeModule;

    public int InstalledCount
    {
        get
        {
            int n = 0;
            foreach (bool b in _installed) if (b) n++;
            return n;
        }
    }

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        // Ensure all wrist meshes start hidden (only show after installation).
        foreach (var mesh in wristMeshes)
            if (mesh != null) mesh.SetActive(false);
    }

    private void Start()
    {
        // Install all modules for testing the wheel and abilities.
        for (int i = 0; i < ModuleCount; i++)
            InstallModule(i);
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    public bool IsModuleInstalled(int moduleIndex)
    {
        if (moduleIndex < 0 || moduleIndex >= ModuleCount) return false;
        return _installed[moduleIndex];
    }

    /// <summary>
    /// Install a module. No-op if already installed.
    /// Reveals the corresponding wrist mesh and auto-selects the module if none is active.
    /// </summary>
    public void InstallModule(int moduleIndex)
    {
        if (moduleIndex < 0 || moduleIndex >= ModuleCount) return;
        if (_installed[moduleIndex]) return;

        _installed[moduleIndex] = true;

        // Reveal wrist mesh segment.
        if (moduleIndex < wristMeshes.Length && wristMeshes[moduleIndex] != null)
            wristMeshes[moduleIndex].SetActive(true);

        // Auto-select if no module is active yet.
        if (_activeModule == -1)
            SetActiveModule(moduleIndex);

        OnModuleInstalled?.Invoke(moduleIndex);

        // Notify quest system.
        QuestEventBridge.RaiseModuleInstalled(moduleIndex);

        Debug.Log($"[IndexDevice] Module {moduleIndex} installed. Active: {_activeModule}");
    }

    /// <summary>
    /// Cycle to the next installed module. Wraps around.
    /// Called by IndexAbilities on Tab input.
    /// </summary>
    public void CycleActiveModule()
    {
        if (InstalledCount == 0) return;

        int start = _activeModule;
        int next  = start;

        for (int i = 1; i <= ModuleCount; i++)
        {
            next = (start + i) % ModuleCount;
            if (_installed[next])
            {
                SetActiveModule(next);
                return;
            }
        }
    }

    /// <summary>
    /// Directly set the active module. Fires OnActiveModuleChanged.
    /// Called by IndexAbilities when the player selects a module via 1–6 keys.
    /// </summary>
    public void SetActiveModule(int moduleIndex)
    {
        if (moduleIndex < -1 || moduleIndex >= ModuleCount) return;
        if (moduleIndex != -1 && !_installed[moduleIndex]) return;

        _activeModule = moduleIndex;
        OnActiveModuleChanged?.Invoke(_activeModule);
        Debug.Log($"[IndexDevice] Active module set to {_activeModule}.");
    }

    /// <summary>
    /// Trigger the currently active module's ability.
    /// Delegates to IndexAbilities. Called by IndexAbilities on Q input.
    /// </summary>
    public void ActivateModule(int moduleIndex)
    {
        if (!IsModuleInstalled(moduleIndex)) return;
        // IndexAbilities listens to this call via its own Update; no direct coupling needed.
        // This method is here as a public API entry point for external systems (cutscenes, etc.).
        var abilities = GetComponent<IndexAbilities>();
        if (abilities != null)
            abilities.TriggerAbility(moduleIndex);
    }
}
