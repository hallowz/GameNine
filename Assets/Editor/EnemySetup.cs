#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Voidborne.Enemies;

/// <summary>
/// Editor setup script for Volume 6.3 — Enemy AI Foundation.
/// Menu: Voidborne > Setup Enemies (Vol 6.3)
///
/// Creates:
///  • EnemyManager root GameObject with EnemySpawner
///  • A NavMeshSurface on the terrain root (if found)
///  • Placeholder enemy prefab (cube) for quick testing
///  • Sample EnemyDefinition asset for an Optimized Patrol enemy
/// </summary>
public static class EnemySetup
{
    [MenuItem("Voidborne/Setup Enemies (Vol 6.3)")]
    public static void SetupEnemies()
    {
        // -----------------------------------------------------------------------
        // 1. EnemyManager root
        // -----------------------------------------------------------------------

        GameObject manager = GameObject.Find("EnemyManager");
        if (manager == null)
        {
            manager = new GameObject("EnemyManager");
            Undo.RegisterCreatedObjectUndo(manager, "Create EnemyManager");
        }

        EnemySpawner spawner = manager.GetComponent<EnemySpawner>();
        if (spawner == null)
        {
            spawner = manager.AddComponent<EnemySpawner>();
            Debug.Log("[EnemySetup] Added EnemySpawner to EnemyManager.");
        }

        // -----------------------------------------------------------------------
        // 2. NavMeshSurface on terrain root
        // -----------------------------------------------------------------------

        GameObject terrainRoot = GameObject.Find("ChunkRoot") ?? GameObject.Find("Terrain");
        if (terrainRoot == null)
            terrainRoot = new GameObject("TerrainNavMeshSurface");

        NavMeshSurface surface = terrainRoot.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = terrainRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry    = NavMeshCollectGeometry.PhysicsColliders;
            Debug.Log("[EnemySetup] Added NavMeshSurface to " + terrainRoot.name +
                      ". Bake it manually (Window > AI > Navigation > Bake) or call " +
                      "EnemyNavigation.BakeNavMeshOnSurface() at runtime after chunk load.");
        }

        // -----------------------------------------------------------------------
        // 3. Sample enemy prefab folder
        // -----------------------------------------------------------------------

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Enemies");
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects/Enemies");

        // -----------------------------------------------------------------------
        // 4. Placeholder prefab for OptimizedPatrol
        // -----------------------------------------------------------------------

        string prefabPath = "Assets/Prefabs/Enemies/OptimizedPatrol_Placeholder.prefab";
        if (!System.IO.File.Exists(prefabPath))
        {
            GameObject go = CreatePlaceholderEnemy("OptimizedPatrol", Color.cyan);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[EnemySetup] Created placeholder prefab at " + prefabPath);
        }

        // -----------------------------------------------------------------------
        // 5. Sample EnemyDefinition asset
        // -----------------------------------------------------------------------

        string defPath = "Assets/ScriptableObjects/Enemies/OptimizedPatrolDefinition.asset";
        if (!System.IO.File.Exists(defPath))
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.enemyName      = "Optimized Patrol";
            def.category       = EnemyCategory.Optimized;
            def.maxHealth      = 80f;
            def.moveSpeed      = 4f;
            def.attackDamage   = 12f;
            def.attackRange    = 1.8f;
            def.detectionRange = 18f;
            def.armor          = 2f;
            def.populationCap  = 20;
            def.groupSizeMin   = 3;
            def.groupSizeMax   = 5;
            def.prefab         = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            AssetDatabase.CreateAsset(def, defPath);
            Debug.Log("[EnemySetup] Created OptimizedPatrolDefinition at " + defPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Enemy Setup Complete",
            "EnemyManager with EnemySpawner created.\n" +
            "NavMeshSurface added to " + terrainRoot.name + ".\n\n" +
            "Next steps:\n" +
            "1. Bake NavMesh (Window > AI > Navigation > Bake)\n" +
            "2. Assign EnemyDefinition assets to the EnemySpawner rules\n" +
            "3. Assign enemy prefabs with EnemyEntity + Capsule Collider\n" +
            "4. Press Play to test spawning",
            "OK"
        );
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GameObject CreatePlaceholderEnemy(string name, Color color)
    {
        GameObject root = new GameObject(name);

        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        Renderer rend = body.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }

        // Eye indicator (shows facing direction)
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(root.transform);
        eye.transform.localPosition = new Vector3(0f, 1.6f, 0.35f);
        eye.transform.localScale    = new Vector3(0.2f, 0.2f, 0.2f);
        Renderer eyeRend = eye.GetComponent<Renderer>();
        if (eyeRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = Color.red;
            eyeRend.sharedMaterial = mat;
        }

        // Required components
        root.AddComponent<EnemyEntity>();

        // Add a CharacterController for physical presence
        CharacterController cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 1f, 0f);
        cc.radius = 0.4f;
        cc.height = 2f;

        return root;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
