using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Voidborne.Combat;
using Voidborne.Combat.Projectiles;

/// <summary>
/// One-click setup for Volume 6.2 Bow & Thrown Weapons components.
/// Run via: Voidborne > Setup Ranged (Volume 6.2)
///
/// Prerequisites: run "Voidborne > Setup Combat (Volume 4)" and
/// "Voidborne > Setup Melee (Volume 5)" first so Player/PlayerCamera exist.
///
/// What this does:
///   1. Adds BowController and ThrowController to Player
///   2. Ensures ProjectilePool exists in the scene
///   3. Wires cameraTransform, fpsController, playerInventory, projectilePool references
///   4. Wires BowController and ThrowController into WeaponSwitcher
///   5. Marks scene dirty
/// </summary>
public static class RangedSetup
{
    [MenuItem("Voidborne/Setup Ranged (Volume 6.2)")]
    public static void SetupRanged()
    {
        // ----------------------------------------------------------------
        // 1. Find Player and PlayerCamera
        // ----------------------------------------------------------------
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            ShowError("Could not find a GameObject named 'Player' in the scene.\n" +
                      "Run 'Voidborne > Setup Combat (Volume 4)' first.");
            return;
        }

        Transform camTransform = player.transform.Find("PlayerCamera");
        if (camTransform == null)
        {
            ShowError("Could not find 'PlayerCamera' as a child of Player.");
            return;
        }

        // ----------------------------------------------------------------
        // 2. Ensure ProjectilePool exists
        // ----------------------------------------------------------------
        ProjectilePool pool = Object.FindFirstObjectByType<ProjectilePool>();
        if (pool == null)
        {
            GameObject poolGO = new GameObject("ProjectilePool");
            pool = Undo.AddComponent<ProjectilePool>(poolGO);
            EditorUtility.SetDirty(poolGO);
        }

        // ----------------------------------------------------------------
        // 3. PlayerInventory reference
        // ----------------------------------------------------------------
        var playerInventory = player.GetComponent<PlayerInventory>();

        // ----------------------------------------------------------------
        // 4. Add BowController
        // ----------------------------------------------------------------
        BowController bowController = GetOrAdd<BowController>(player);
        Set(bowController, "cameraTransform", camTransform);
        Set(bowController, "projectilePool",  pool);

        // fpsController — same GO
        var fps = player.GetComponent<Voidborne.Player.FirstPersonController>();
        if (fps != null)
            Set(bowController, "fpsController", fps);

        // ----------------------------------------------------------------
        // 5. Add ThrowController
        // ----------------------------------------------------------------
        ThrowController throwController = GetOrAdd<ThrowController>(player);
        Set(throwController, "cameraTransform",  camTransform);
        Set(throwController, "projectilePool",   pool);
        if (playerInventory != null)
            Set(throwController, "playerInventory", playerInventory);

        // ----------------------------------------------------------------
        // 6. Wire into WeaponSwitcher
        // ----------------------------------------------------------------
        WeaponSwitcher switcher = player.GetComponent<WeaponSwitcher>();
        if (switcher != null)
        {
            Set(switcher, "bowController",   bowController);
            Set(switcher, "throwController", throwController);
        }
        else
        {
            Debug.LogWarning("[RangedSetup] WeaponSwitcher not found on Player — run 'Setup Combat (Volume 4)' first.");
        }

        // ----------------------------------------------------------------
        // 7. Mark dirty
        // ----------------------------------------------------------------
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[RangedSetup] Volume 6.2 ranged components added and wired successfully.");
        EditorUtility.DisplayDialog("Ranged Setup Complete",
            "Volume 6.2 Bow & Thrown Weapons setup finished!\n\n" +
            "Added to Player:\n" +
            "  • BowController (draw-to-fire bow mechanic)\n" +
            "  • ThrowController (windup-and-throw mechanic)\n\n" +
            "WeaponSwitcher wired to both controllers.\n\n" +
            "Next steps:\n" +
            "1. Voidborne > Create Ranged Weapon Assets\n" +
            "2. Add those weapon items to the player's hotbar\n" +
            "3. Save scene (Ctrl+S) and hit Play\n" +
            "   - Bow: hold RMB to draw, release to fire\n" +
            "   - Thrown: hold LMB to wind up, release to throw",
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
        EditorUtility.DisplayDialog("Ranged Setup Failed", message, "OK");
    }

    private static void Set(Object target, string fieldName, object value)
    {
        SerializedObject   so   = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[RangedSetup] Field '{fieldName}' not found on {target.GetType().Name} — skipping.");
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
            default:
                Debug.LogWarning($"[RangedSetup] Unhandled type {prop.propertyType} for '{fieldName}'.");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
