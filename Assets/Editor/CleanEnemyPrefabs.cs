using UnityEditor;
using UnityEngine;
using Voidborne.Enemies;

public static class CleanEnemyPrefabs
{
    // Map prefab path → definition asset path
    private static readonly (string prefab, string definition)[] s_Entries =
    {
        ("Assets/Prefabs/Enemies/OptimizedPatrol.prefab",   "Assets/ScriptableObjects/Enemies/OptimizedPatrolDefinition.asset"),
        ("Assets/Prefabs/Enemies/OptimizedHeavy.prefab",    "Assets/ScriptableObjects/Enemies/OptimizedHeavyDefinition.asset"),
        ("Assets/Prefabs/Enemies/OptimizedRanged.prefab",   "Assets/ScriptableObjects/Enemies/OptimizedRangedDefinition.asset"),
        ("Assets/Prefabs/Enemies/DirectedSentinel.prefab",  "Assets/ScriptableObjects/Enemies/DirectedSentinelDefinition.asset"),
        ("Assets/Prefabs/Enemies/DirectedCrafter.prefab",   "Assets/ScriptableObjects/Enemies/DirectedCrafterDefinition.asset"),
        ("Assets/Prefabs/Enemies/CaveStalker.prefab",       "Assets/ScriptableObjects/Enemies/CaveStalkerDefinition.asset"),
    };

    [MenuItem("Voidborne/Wire Enemy Prefab Definitions")]
    public static void WireDefinitions()
    {
        int wired = 0;

        foreach (var (prefabPath, defPath) in s_Entries)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(defPath);
            if (def == null)
            {
                Debug.LogWarning($"[CleanEnemyPrefabs] Definition not found at {defPath}");
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CleanEnemyPrefabs] Prefab not found at {prefabPath}");
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;

                // Ensure EnemyEntity exists
                var entity = root.GetComponent<EnemyEntity>();
                if (entity == null)
                    entity = root.AddComponent<EnemyEntity>();

                // Wire the _definition field via reflection
                var field = typeof(EnemyEntity).GetField("_definition",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(entity, def);
                    EditorUtility.SetDirty(entity);
                    wired++;
                    Debug.Log($"[CleanEnemyPrefabs] Wired {def.enemyName} → {prefabPath}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CleanEnemyPrefabs] Done. Wired {wired} definitions.");
    }
}
