using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Voidborne.Combat;
using Voidborne.Combat.Melee;
using Voidborne.Player;
using Voidborne.UI;

/// <summary>
/// One-click setup for all Volume 5 melee components.
/// Run via: Voidborne > Setup Melee (Volume 5)
///
/// What this does:
///   1. Adds MeleeStagger, ParrySystem, MeleeController, FeintSystem, KickAbility to Player
///   2. Wires cameraTransform and audioSource on the components that need them
///   3. Wires MeleeController into the existing WeaponSwitcher
///   4. Adds MeleeWeaponView to PlayerCamera (procedural weapon visuals)
///   5. Sets camera near clip to 0.05 to reduce weapon model clipping
///   6. Marks the scene dirty
///
/// Prerequisites: run "Voidborne > Setup Combat (Volume 4)" first so Player/PlayerCamera exist.
/// </summary>
public static class MeleeSetup
{
    [MenuItem("Voidborne/Setup Melee (Volume 5)")]
    public static void SetupMelee()
    {
        // ----------------------------------------------------------------
        // 1. Find Player and PlayerCamera
        // ----------------------------------------------------------------
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            ShowError("Could not find a GameObject named 'Player' in the scene.\n" +
                      "Open SampleScene and run 'Setup Combat (Volume 4)' first.");
            return;
        }

        Transform camTransform = player.transform.Find("PlayerCamera");
        if (camTransform == null)
        {
            ShowError("Could not find 'PlayerCamera' as a child of Player.");
            return;
        }
        GameObject playerCamera = camTransform.gameObject;

        // ----------------------------------------------------------------
        // 2. AudioSource on Player (reuse if it already exists)
        // ----------------------------------------------------------------
        AudioSource audioSource = GetOrAdd<AudioSource>(player);
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;

        // ----------------------------------------------------------------
        // 3. Add all melee components to Player in dependency order
        //    (Awake() methods auto-resolve sibling refs via GetComponent,
        //     but we also set them explicitly for robustness.)
        // ----------------------------------------------------------------
        MeleeStagger    meleeStagger    = GetOrAdd<MeleeStagger>(player);
        ParrySystem     parrySystem     = GetOrAdd<ParrySystem>(player);
        MeleeController meleeController = GetOrAdd<MeleeController>(player);
        GetOrAdd<FeintSystem>(player);
        KickAbility kickAbility = GetOrAdd<KickAbility>(player);

        // ----------------------------------------------------------------
        // 4. Wire MeleeController
        // ----------------------------------------------------------------
        Set(meleeController, "cameraTransform", camTransform);
        Set(meleeController, "audioSource",     audioSource);
        Set(meleeController, "parrySystem",     parrySystem);
        Set(meleeController, "meleeStagger",    meleeStagger);
        Set(meleeController, "hitLayers",       (LayerMask)(~0));

        // ----------------------------------------------------------------
        // 5. Wire ParrySystem
        // ----------------------------------------------------------------
        Set(parrySystem, "audioSource",     audioSource);
        Set(parrySystem, "meleeController", meleeController);
        Set(parrySystem, "stagger",         meleeStagger);

        // ----------------------------------------------------------------
        // 6. Wire KickAbility
        // ----------------------------------------------------------------
        Set(kickAbility, "cameraTransform", camTransform);
        Set(kickAbility, "audioSource",     audioSource);

        // ----------------------------------------------------------------
        // 7. Wire MeleeController into WeaponSwitcher
        //    WeaponSwitcher.Start() also auto-resolves, but setting it here
        //    ensures the Inspector always shows the reference cleanly.
        // ----------------------------------------------------------------
        WeaponSwitcher weaponSwitcher = player.GetComponent<WeaponSwitcher>();
        if (weaponSwitcher != null)
            Set(weaponSwitcher, "meleeController", meleeController);
        else
            Debug.LogWarning("[MeleeSetup] WeaponSwitcher not found on Player — run 'Setup Combat (Volume 4)' first.");

        // ----------------------------------------------------------------
        // 8. MeleeDirectionUI — add to WeaponUI GO (same as HitmarkerUI / CrosshairUI)
        // ----------------------------------------------------------------
        GameObject weaponUI = GameObject.Find("WeaponUI") ?? new GameObject("WeaponUI");
        GetOrAdd<MeleeDirectionUI>(weaponUI);
        EditorUtility.SetDirty(weaponUI);

        // ----------------------------------------------------------------
        // 9. MeleeWeaponView on PlayerCamera
        //    Auto-resolves MeleeController and FirstPersonController via
        //    GetComponentInParent() in its own Awake().
        // ----------------------------------------------------------------
        GetOrAdd<MeleeWeaponView>(playerCamera);

        // ----------------------------------------------------------------
        // 10. Reduce camera near clip so weapon models clip less through walls
        // ----------------------------------------------------------------
        Camera cam = playerCamera.GetComponent<Camera>();
        if (cam != null)
            cam.nearClipPlane = 0.05f;

        // ----------------------------------------------------------------
        // 10. Mark scene dirty and save
        // ----------------------------------------------------------------
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(playerCamera);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[MeleeSetup] Volume 5 melee components added and wired successfully.");
        EditorUtility.DisplayDialog("Melee Setup Complete",
            "Volume 5 melee setup finished!\n\n" +
            "Added to Player:\n" +
            "  • MeleeStagger (camera shake on hit)\n" +
            "  • ParrySystem (directional parry + riposte)\n" +
            "  • MeleeController (directional attack FSM)\n" +
            "  • FeintSystem (feints, morphs, chambers)\n" +
            "  • KickAbility (middle mouse / F — unblockable)\n\n" +
            "Added to PlayerCamera:\n" +
            "  • MeleeWeaponView (procedural weapon models)\n\n" +
            "Added to WeaponUI:\n" +
            "  • MeleeDirectionUI (4-arrow direction indicator)\n\n" +
            "WeaponSwitcher → MeleeController wired.\n\n" +
            "Next steps:\n" +
            "1. Voidborne > Create Starter Melee Weapons\n" +
            "2. Save the scene (Ctrl+S)\n" +
            "3. Hit Play — select a melee weapon from the hotbar!\n" +
            "   (digits 1-9 or scroll wheel to cycle)\n\n" +
            "Note: weapon models render through walls at close range.\n" +
            "A dedicated weapon camera layer is a future improvement.",
            "OK");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
            comp = Undo.AddComponent<T>(go);
        return comp;
    }

    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog("Melee Setup Failed", message, "OK");
    }

    /// <summary>
    /// Sets a private/protected [SerializeField] field via SerializedObject so
    /// Unity tracks the change properly (same pattern as CombatSetup).
    /// </summary>
    private static void Set(Object target, string fieldName, object value)
    {
        SerializedObject   so   = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[MeleeSetup] Field '{fieldName}' not found on {target.GetType().Name} — skipping.");
            return;
        }

        switch (prop.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.Float:
                prop.floatValue = (float)value;
                break;
            case SerializedPropertyType.Boolean:
                prop.boolValue = (bool)value;
                break;
            case SerializedPropertyType.Integer:
                prop.intValue = (int)value;
                break;
            case SerializedPropertyType.LayerMask:
                prop.intValue = (LayerMask)value;
                break;
            default:
                Debug.LogWarning($"[MeleeSetup] Unhandled type {prop.propertyType} for '{fieldName}'.");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
