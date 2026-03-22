using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Voidborne.Combat;
using Voidborne.UI;

/// <summary>
/// One-click setup for all Volume 4 combat components.
/// Run via: Voidborne > Setup Combat (Volume 4)
///
/// What this does:
///   1. Adds an AudioSource to the Player for gun sounds
///   2. Adds GunController, ReloadSystem, WeaponSwitcher to Player
///   3. Adds RecoilSystem, ADSController to PlayerCamera
///   4. Creates a HitEffects GameObject in the scene
///   5. Creates a WeaponUI GameObject with HitmarkerUI + CrosshairUI
///   6. Wires all component references together
///   7. Marks the scene dirty so Unity saves it
/// </summary>
public static class CombatSetup
{
    [MenuItem("Voidborne/Setup Combat (Volume 4)")]
    public static void SetupCombat()
    {
        // ----------------------------------------------------------------
        // 1. Find Player and PlayerCamera
        // ----------------------------------------------------------------
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Combat Setup Failed",
                "Could not find a GameObject named 'Player' in the scene.\n" +
                "Make sure SampleScene is open.", "OK");
            return;
        }

        Transform playerCameraTransform = player.transform.Find("PlayerCamera");
        if (playerCameraTransform == null)
        {
            EditorUtility.DisplayDialog("Combat Setup Failed",
                "Could not find 'PlayerCamera' as a child of Player.", "OK");
            return;
        }
        GameObject playerCamera = playerCameraTransform.gameObject;
        Camera cam = playerCamera.GetComponent<Camera>();

        // ----------------------------------------------------------------
        // 2. AudioSource on Player (for gun fire / reload sounds)
        // ----------------------------------------------------------------
        AudioSource audioSource = GetOrAdd<AudioSource>(player);
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D — first-person, no 3D positioning needed

        // ----------------------------------------------------------------
        // 3. RecoilSystem on PlayerCamera
        //    RecoilSystem no longer rotates the camera directly — it only
        //    tracks the offset. FirstPersonCamera reads it each LateUpdate.
        // ----------------------------------------------------------------
        RecoilSystem recoilSystem = GetOrAdd<RecoilSystem>(playerCamera);
        SetPrivateSerializedField(recoilSystem, "recoveryRate", 5f);

        // ----------------------------------------------------------------
        // 4. Wire RecoilSystem into FirstPersonCamera
        //    FirstPersonCamera integrates the recoil offset each LateUpdate.
        // ----------------------------------------------------------------
        Voidborne.Player.FirstPersonCamera fpsCam = playerCamera.GetComponent<Voidborne.Player.FirstPersonCamera>();
        if (fpsCam != null)
            SetPrivateSerializedField(fpsCam, "recoilSystem", recoilSystem);
        else
            Debug.LogWarning("[CombatSetup] FirstPersonCamera not found on PlayerCamera — recoil will not apply.");

        // ----------------------------------------------------------------
        // 5. ADSController on PlayerCamera
        // ----------------------------------------------------------------
        ADSController adsController = GetOrAdd<ADSController>(playerCamera);
        SetPrivateSerializedField(adsController, "playerCamera", cam);
        SetPrivateSerializedField(adsController, "normalFOV", cam != null ? cam.fieldOfView : 90f);
        SetPrivateSerializedField(adsController, "adsSmoothSpeed", 10f);

        // ----------------------------------------------------------------
        // 5. ReloadSystem on Player
        // ----------------------------------------------------------------
        ReloadSystem reloadSystem = GetOrAdd<ReloadSystem>(player);
        SetPrivateSerializedField(reloadSystem, "audioSource", audioSource);

        // ----------------------------------------------------------------
        // 6. GunController on Player
        // ----------------------------------------------------------------
        GunController gunController = GetOrAdd<GunController>(player);
        SetPrivateSerializedField(gunController, "recoilSystem",    recoilSystem);
        SetPrivateSerializedField(gunController, "cameraTransform", playerCameraTransform);
        SetPrivateSerializedField(gunController, "audioSource",     audioSource);
        SetPrivateSerializedField(gunController, "reloadSystem",    reloadSystem);
        SetPrivateSerializedField(gunController, "adsController",   adsController);
        // Set shootableLayers to "Default" layer so terrain and world objects are hit
        SetPrivateSerializedField(gunController, "shootableLayers", (LayerMask)(1 << 0));
        // Leave defaultGun null — WeaponSwitcher drives equipping via inventory

        // ----------------------------------------------------------------
        // 7. WeaponSwitcher on Player
        // ----------------------------------------------------------------
        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        WeaponSwitcher weaponSwitcher = GetOrAdd<WeaponSwitcher>(player);
        SetPrivateSerializedField(weaponSwitcher, "gunController",      gunController);
        SetPrivateSerializedField(weaponSwitcher, "adsController",      adsController);
        SetPrivateSerializedField(weaponSwitcher, "reloadSystem",       reloadSystem);
        SetPrivateSerializedField(weaponSwitcher, "playerInventory",    playerInventory);
        SetPrivateSerializedField(weaponSwitcher, "useInventoryHotbar", true);

        // ----------------------------------------------------------------
        // 8. HitEffects singleton GameObject
        // ----------------------------------------------------------------
        EnsureSingletonGO<HitEffects>("HitEffects");

        // ----------------------------------------------------------------
        // 9. WeaponUI GameObject — HitmarkerUI + CrosshairUI
        //    Both components build their own fallback canvas in Start()
        //    if no RectTransform refs are assigned, so no additional wiring needed.
        // ----------------------------------------------------------------
        GameObject weaponUI = EnsureSingletonGO("WeaponUI");
        HitmarkerUI hitmarkerUI = GetOrAdd<HitmarkerUI>(weaponUI);
        CrosshairUI crosshairUI = GetOrAdd<CrosshairUI>(weaponUI);
        // Wire CrosshairUI → GunController so it reads CurrentSpread
        SetPrivateSerializedField(crosshairUI, "gunController", gunController);

        // ----------------------------------------------------------------
        // 10. Mark scene dirty and save
        // ----------------------------------------------------------------
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(playerCamera);
        EditorUtility.SetDirty(weaponUI);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[CombatSetup] Volume 4 combat components added and wired successfully.");
        EditorUtility.DisplayDialog("Combat Setup Complete",
            "Volume 4 combat setup finished!\n\n" +
            "Added to Player:\n" +
            "  • AudioSource (gun sounds)\n" +
            "  • GunController\n" +
            "  • ReloadSystem\n" +
            "  • WeaponSwitcher → PlayerInventory\n\n" +
            "Added to PlayerCamera:\n" +
            "  • RecoilSystem\n" +
            "  • ADSController\n\n" +
            "New scene GameObjects:\n" +
            "  • HitEffects (singleton)\n" +
            "  • WeaponUI → HitmarkerUI + CrosshairUI\n\n" +
            "Next steps:\n" +
            "1. Run 'Voidborne > Create Starter Guns'\n" +
            "2. Run 'Voidborne > Create Weapon Items'\n" +
            "3. Save the scene (Ctrl+S)\n" +
            "4. Hit Play — guns will be in your hotbar!",
            "OK");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    /// <summary>Gets an existing component or adds a new one.</summary>
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
            comp = Undo.AddComponent<T>(go);
        return comp;
    }

    /// <summary>Creates or finds a GameObject by name in the active scene root.</summary>
    private static GameObject EnsureSingletonGO(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }
        return go;
    }

    /// <summary>Creates or finds a root GameObject and ensures it has component T.</summary>
    private static T EnsureSingletonGO<T>(string name) where T : Component
    {
        GameObject go = EnsureSingletonGO(name);
        return GetOrAdd<T>(go);
    }

    /// <summary>
    /// Sets a private or protected [SerializeField] field via SerializedObject
    /// so Unity tracks the change properly (works in Editor mode, not play mode).
    /// </summary>
    private static void SetPrivateSerializedField(Object target, string fieldName, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[CombatSetup] Field '{fieldName}' not found on {target.GetType().Name}. Skipping.");
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
                Debug.LogWarning($"[CombatSetup] Unhandled property type {prop.propertyType} for '{fieldName}'.");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
