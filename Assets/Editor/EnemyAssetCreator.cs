#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Enemies;

/// <summary>
/// Editor script — Volume 6.4: Core Enemy Types.
/// Menu: Voidborne > Create Enemy Type Assets (Vol 6.4)
///
/// Creates:
///   Loot items:  DirectiveShard, ReinforcedPlating, CortexComponents,
///                EnergyCell, WeaponComponents, Chitin,
///                FracturedMemoryShard, SchematicFragment
///   Enemy defs:  6 × EnemyDefinition ScriptableObjects
///   Prefabs:     6 × compositePrimitive prefabs with distinctive silhouettes
/// </summary>
public static class EnemyAssetCreator
{
    private const string ItemsPath   = "Assets/ScriptableObjects/Items/Enemy";
    private const string EnemiesPath = "Assets/ScriptableObjects/Enemies";
    private const string PrefabsPath = "Assets/Prefabs/Enemies";
    private const string MatsPath    = "Assets/Materials/Enemies";

    // ═══════════════════════════════════════════════════════════════════════════
    // SCENE SETUP
    // ═══════════════════════════════════════════════════════════════════════════

    [MenuItem("Voidborne/Setup Enemy Scene (Vol 6.4)")]
    public static void SetupEnemyScene()
    {
        AddTagIfMissing("Enemy");
        AddTagIfMissing("Head");
        AddTagIfMissing("Limb");

        // Patch existing prefabs so Torso/Body children carry the Enemy tag
        PatchPrefabTags();

        // Place test instances of each enemy type in the scene
        PlaceEnemyInstances();

        // Configure EnemySpawner with all 6 rules
        ConfigureEnemySpawner();

        EditorUtility.DisplayDialog("Enemy Scene Setup Complete",
            "6 enemy types placed in scene.\n" +
            "EnemySpawner rules configured.\n\n" +
            "Press Play to test.", "OK");
    }

    static void PatchPrefabTags()
    {
        string[] prefabPaths =
        {
            $"{PrefabsPath}/OptimizedPatrol.prefab",
            $"{PrefabsPath}/OptimizedHeavy.prefab",
            $"{PrefabsPath}/OptimizedRanged.prefab",
            $"{PrefabsPath}/DirectedSentinel.prefab",
            $"{PrefabsPath}/DirectedCrafter.prefab",
            $"{PrefabsPath}/CaveStalker.prefab",
        };

        foreach (string p in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) continue;

            using var scope = new PrefabUtility.EditPrefabContentsScope(p);
            var root = scope.prefabContentsRoot;

            // Tag every renderer child that isn't a special sub-component
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                if (child.GetComponent<WeakPoint>() != null) continue; // WeakPoint already tagged
                if (child.GetComponent<Renderer>() != null)
                    child.gameObject.tag = "Enemy";
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyAssetCreator] Patched Enemy tags on all 6 prefabs.");
    }

    static void PlaceEnemyInstances()
    {
        // Remove any stale test instances first
        string[] testNames = {
            "TEST_OptimizedPatrol", "TEST_OptimizedPatrol2", "TEST_OptimizedPatrol3",
            "TEST_OptimizedHeavy", "TEST_OptimizedRanged", "TEST_OptimizedRanged2",
            "TEST_DirectedSentinel", "TEST_DirectedCrafter",
            "TEST_CaveStalker", "TEST_OptimizedPatrol_Damaged"
        };
        foreach (string n in testNames)
        {
            var existing = GameObject.Find(n);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        var patrolDef   = LoadDef("OptimizedPatrolDefinition");
        var heavyDef    = LoadDef("OptimizedHeavyDefinition");
        var rangedDef   = LoadDef("OptimizedRangedDefinition");
        var sentinelDef = LoadDef("DirectedSentinelDefinition");
        var crafterDef  = LoadDef("DirectedCrafterDefinition");
        var stalkerDef  = LoadDef("CaveStalkerDefinition");

        // 3 × OptimizedPatrol — cluster east
        SpawnTestEnemy("TEST_OptimizedPatrol",         patrolDef,   new Vector3( 8, 0, 5));
        SpawnTestEnemy("TEST_OptimizedPatrol2",        patrolDef,   new Vector3(10, 0, 5));
        SpawnTestEnemy("TEST_OptimizedPatrol_Damaged", patrolDef,   new Vector3(12, 0, 5));

        // 1 × OptimizedHeavy — further east
        SpawnTestEnemy("TEST_OptimizedHeavy",          heavyDef,    new Vector3(20, 0, 0));

        // 2 × OptimizedRanged — south, spread out
        SpawnTestEnemy("TEST_OptimizedRanged",         rangedDef,   new Vector3( 5, 0, -12));
        SpawnTestEnemy("TEST_OptimizedRanged2",        rangedDef,   new Vector3(10, 0, -12));

        // 1 × DirectedSentinel — northwest
        SpawnTestEnemy("TEST_DirectedSentinel",        sentinelDef, new Vector3(-10, 0, 8));

        // 1 × DirectedCrafter — near damaged patrol group
        SpawnTestEnemy("TEST_DirectedCrafter",         crafterDef,  new Vector3(15, 0, 5));

        // 1 × CaveStalker — above and ahead (ceiling ambush position)
        SpawnTestEnemy("TEST_CaveStalker",             stalkerDef,  new Vector3(0, 4, 10));

        Debug.Log("[EnemyAssetCreator] Placed 9 test enemy instances in scene.");
    }

    static void SpawnTestEnemy(string goName, EnemyDefinition def, Vector3 pos)
    {
        if (def == null || def.prefab == null)
        {
            Debug.LogWarning($"[EnemyAssetCreator] Cannot spawn {goName}: definition or prefab missing.");
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(def.prefab);
        go.name = goName;
        go.transform.position = pos;

        // Wire the private _definition field via reflection — more reliable than
        // SerializedObject on prefab instances, which can fail to persist overrides.
        var eb = go.GetComponent<EnemyEntity>();
        if (eb != null)
        {
            var field = typeof(EnemyEntity).GetField("_definition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                Undo.RecordObject(eb, $"Set EnemyDefinition on {goName}");
                field.SetValue(eb, def);
                EditorUtility.SetDirty(eb);
                PrefabUtility.RecordPrefabInstancePropertyModifications(eb);
            }
        }

        Undo.RegisterCreatedObjectUndo(go, $"Spawn {goName}");
    }

    static void ConfigureEnemySpawner()
    {
        var manager = GameObject.Find("EnemyManager");
        if (manager == null) { Debug.LogWarning("[EnemyAssetCreator] EnemyManager not found."); return; }

        var spawner = manager.GetComponent<EnemySpawner>();
        if (spawner == null) spawner = manager.AddComponent<EnemySpawner>();

        var so    = new SerializedObject(spawner);
        var rules = so.FindProperty("_rules");
        rules.ClearArray();

        AddSpawnRule(rules, "OptimizedPatrolDefinition",  30f, 20f, 60f);
        AddSpawnRule(rules, "OptimizedHeavyDefinition",   60f, 25f, 70f);
        AddSpawnRule(rules, "OptimizedRangedDefinition",  40f, 20f, 65f);
        AddSpawnRule(rules, "DirectedSentinelDefinition", 90f, 30f, 80f);
        AddSpawnRule(rules, "DirectedCrafterDefinition",  90f, 25f, 70f);
        AddSpawnRule(rules, "CaveStalkerDefinition",      25f, 15f, 50f);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);
        Debug.Log("[EnemyAssetCreator] EnemySpawner configured with 6 rules.");
    }

    static void AddSpawnRule(SerializedProperty rulesArray, string defName,
        float interval, float minDist, float maxDist)
    {
        var def = LoadDef(defName);
        if (def == null) return;

        int idx = rulesArray.arraySize;
        rulesArray.InsertArrayElementAtIndex(idx);
        var rule = rulesArray.GetArrayElementAtIndex(idx);
        rule.FindPropertyRelative("enemyDefinition").objectReferenceValue = def;
        rule.FindPropertyRelative("spawnInterval").floatValue    = interval;
        rule.FindPropertyRelative("minSpawnDistance").floatValue  = minDist;
        rule.FindPropertyRelative("maxSpawnDistance").floatValue  = maxDist;
    }

    static EnemyDefinition LoadDef(string fileName) =>
        AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemiesPath}/{fileName}.asset");

    // ═══════════════════════════════════════════════════════════════════════════
    // FORCE REBUILD — deletes existing prefabs so new colors/geometry bake in
    // ═══════════════════════════════════════════════════════════════════════════

    [MenuItem("Voidborne/Rebuild Enemy Prefabs (Fix Colors)")]
    public static void RebuildEnemyPrefabs()
    {
        string[] names = { "OptimizedPatrol", "OptimizedHeavy", "OptimizedRanged",
                           "DirectedSentinel", "DirectedCrafter", "CaveStalker" };

        foreach (string n in names)
        {
            string path = $"{PrefabsPath}/{n}.prefab";
            if (AssetDatabase.AssetPathExists(path))
                AssetDatabase.DeleteAsset(path);
        }

        // Also delete old enemy material assets so they are recreated fresh
        if (AssetDatabase.IsValidFolder(MatsPath))
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { MatsPath });
            foreach (string g in matGuids)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        }

        AssetDatabase.SaveAssets();
        CreateAllEnemyAssets();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TAG HELPER
    // ═══════════════════════════════════════════════════════════════════════════
    // ONE-CLICK FIX — patches existing prefabs + live scene enemies
    // ═══════════════════════════════════════════════════════════════════════════

    [MenuItem("Voidborne/Fix Enemy Colliders & Tags")]
    public static void FixEnemyCollidersAndTags()
    {
        AddTagIfMissing("Enemy");
        AddTagIfMissing("Head");

        int prefabsFixed = 0;
        int sceneFixed   = 0;

        // ── Patch saved prefabs ──────────────────────────────────────────────
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            // Only touch our enemy prefabs (they all have EnemyEntity on root)
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null || prefabRoot.GetComponent<EnemyEntity>() == null) continue;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(assetPath))
            {
                var contents = scope.prefabContentsRoot;
                PatchEnemyGO(contents);
                prefabsFixed++;
            }
        }

        // ── Patch live scene instances ────────────────────────────────────────
        var sceneEnemies = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
        foreach (var eb in sceneEnemies)
        {
            Undo.RecordObject(eb.gameObject, "Fix Enemy Colliders & Tags");
            PatchEnemyGO(eb.gameObject);
            EditorUtility.SetDirty(eb.gameObject);
            sceneFixed++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Enemy Colliders & Tags Fixed",
            $"Patched {prefabsFixed} prefab(s) and {sceneFixed} scene instance(s).\n\n" +
            "Enemies now have:\n" +
            "• Root tagged 'Enemy' (CharacterController body hits)\n" +
            "• Head child tagged 'Head' with SphereCollider (headshots)",
            "OK");
    }

    /// <summary>
    /// Applies the tag + collider fixes to a single enemy root GameObject.
    /// Safe to call on both prefab contents and live scene objects.
    /// </summary>
    static void PatchEnemyGO(GameObject root)
    {
        // Root must be "Enemy" so CharacterController body hits are detected
        root.tag = "Enemy";

        // Find head candidate (name varies per enemy type)
        Transform head = root.transform.Find("Head")
                      ?? root.transform.Find("Helmet")
                      ?? root.transform.Find("SensorCluster");

        if (head != null)
        {
            head.gameObject.tag = "Head";
            if (head.GetComponent<Collider>() == null)
            {
                var sc = head.gameObject.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════

    static void AddTagIfMissing(string tag)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;

        int idx = tagsProp.arraySize;
        tagsProp.InsertArrayElementAtIndex(idx);
        tagsProp.GetArrayElementAtIndex(idx).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[EnemyAssetCreator] Registered tag: {tag}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ASSET CREATION
    // ═══════════════════════════════════════════════════════════════════════════

    [MenuItem("Voidborne/Create Enemy Type Assets (Vol 6.4)")]
    public static void CreateAllEnemyAssets()
    {
        // Register tags first so prefabs can use them immediately
        AddTagIfMissing("Enemy");
        AddTagIfMissing("Head");
        AddTagIfMissing("Limb");

        EnsureFolder("Assets/ScriptableObjects/Items");
        EnsureFolder(ItemsPath);
        EnsureFolder(EnemiesPath);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabsPath);

        // -----------------------------------------------------------------------
        // 1. Loot item ScriptableObjects
        // -----------------------------------------------------------------------

        var directiveShard   = GetOrCreateItem<ItemDefinition>("DirectiveShard",
            "Directive Shard",
            "Crystallised operational directive extracted from a decommissioned Optimized unit.",
            ItemType.Resource, 64);

        var reinforcedPlating = GetOrCreateItem<ItemDefinition>("ReinforcedPlating",
            "Reinforced Plating",
            "Heavy armour plate recovered from an OptimizedHeavy. Dense and impact-resistant.",
            ItemType.Resource, 32);

        var cortexComponents  = GetOrCreateItem<ItemDefinition>("CortexComponents",
            "Cortex Components",
            "Internal processing nodes from an Optimized unit. Useful for advanced crafting.",
            ItemType.Resource, 32);

        var energyCell        = GetOrCreateItem<ItemDefinition>("EnergyCell",
            "Energy Cell",
            "Compact energy storage unit used to power VORD ranged weapons.",
            ItemType.Resource, 64);

        var weaponComponents  = GetOrCreateItem<ItemDefinition>("WeaponComponents",
            "Weapon Components",
            "Salvaged parts from a VORD energy weapon.",
            ItemType.Resource, 32);

        var chitin            = GetOrCreateItem<ItemDefinition>("Chitin",
            "Chitin",
            "Dense biological armour plating shed by underground creatures.",
            ItemType.Resource, 64);

        var memoryShard       = GetOrCreateSpecialItem<FracturedMemoryShard>(
            "FracturedMemoryShard_Generic",
            "Fractured Memory Shard",
            "A shard of corrupted memory data from a former Kin. Right-click to read.",
            ItemType.Resource, 1,
            s =>
            {
                s.memoryAuthor = "Unknown Kin";
                s.loreText =
                    "I remember the gate. The eastern one. I had a name for the stones. " +
                    "They had names for me. Now there is only the directive. " +
                    "Directive is everything. [OVERWRITE COMPLETE]";
            });

        var schematicFragment = GetOrCreateSpecialItem<SchematicFragment>(
            "SchematicFragment_Generic",
            "Schematic Fragment",
            "Partial manufacturing directive. Examine at a Terminal to decode.",
            ItemType.Resource, 1,
            f =>
            {
                f.schematicDescription =
                    "Partial assembly data — appears to describe a compact energy converter. " +
                    "Examine at Terminal to decode.";
            });

        AssetDatabase.SaveAssets();

        // -----------------------------------------------------------------------
        // 2. Enemy prefabs (distinctive composite models)
        // -----------------------------------------------------------------------

        string patrolPath   = BuildPatrolPrefab();
        string heavyPath    = BuildHeavyPrefab();
        string rangedPath   = BuildRangedPrefab();
        string sentinelPath = BuildSentinelPrefab();
        string crafterPath  = BuildCrafterPrefab();
        string stalkerPath  = BuildStalkerPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // -----------------------------------------------------------------------
        // 3. EnemyDefinition ScriptableObjects
        // -----------------------------------------------------------------------

        var patrol = GetOrCreateDef("OptimizedPatrolDefinition");
        patrol.enemyName      = "Optimized Patrol";
        patrol.category       = EnemyCategory.Optimized;
        patrol.maxHealth      = 80f;
        patrol.moveSpeed      = 4.5f;
        patrol.attackDamage   = 14f;
        patrol.attackRange    = 1.8f;
        patrol.detectionRange = 18f;
        patrol.armor          = 2f;
        patrol.attackCooldown = 1.4f;
        patrol.attackWindup   = 0.25f;
        patrol.populationCap  = 20;
        patrol.groupSizeMin   = 3;
        patrol.groupSizeMax   = 5;
        patrol.prefab         = LoadPrefab(patrolPath);
        SetLootTable(patrol, (directiveShard, 1, 2, 0.8f), (weaponComponents, 1, 1, 0.35f));
        EditorUtility.SetDirty(patrol);

        var heavy = GetOrCreateDef("OptimizedHeavyDefinition");
        heavy.enemyName      = "Optimized Heavy";
        heavy.category       = EnemyCategory.Optimized;
        heavy.maxHealth      = 320f;
        heavy.moveSpeed      = 2.5f;
        heavy.attackDamage   = 30f;
        heavy.attackRange    = 2.2f;
        heavy.detectionRange = 15f;
        heavy.armor          = 15f;
        heavy.attackCooldown = 2.2f;
        heavy.attackWindup   = 0.6f;
        heavy.populationCap  = 8;
        heavy.groupSizeMin   = 1;
        heavy.groupSizeMax   = 2;
        heavy.prefab         = LoadPrefab(heavyPath);
        SetLootTable(heavy, (reinforcedPlating, 1, 3, 0.9f), (cortexComponents, 1, 2, 0.7f));
        EditorUtility.SetDirty(heavy);

        var ranged = GetOrCreateDef("OptimizedRangedDefinition");
        ranged.enemyName      = "Optimized Ranged";
        ranged.category       = EnemyCategory.Optimized;
        ranged.maxHealth      = 70f;
        ranged.moveSpeed      = 4.0f;
        ranged.attackDamage   = 0f;
        ranged.attackRange    = 15f;
        ranged.detectionRange = 22f;
        ranged.armor          = 1f;
        ranged.attackCooldown = 1.8f;
        ranged.attackWindup   = 0.1f;
        ranged.populationCap  = 12;
        ranged.groupSizeMin   = 2;
        ranged.groupSizeMax   = 3;
        ranged.prefab         = LoadPrefab(rangedPath);
        SetLootTable(ranged, (energyCell, 1, 2, 0.9f), (weaponComponents, 1, 2, 0.6f));
        EditorUtility.SetDirty(ranged);

        var sentinel = GetOrCreateDef("DirectedSentinelDefinition");
        sentinel.enemyName      = "Directed Sentinel";
        sentinel.category       = EnemyCategory.Directed;
        sentinel.maxHealth      = 110f;
        sentinel.moveSpeed      = 3.8f;
        sentinel.attackDamage   = 18f;
        sentinel.attackRange    = 2.0f;
        sentinel.detectionRange = 20f;
        sentinel.armor          = 4f;
        sentinel.attackCooldown = 1.5f;
        sentinel.attackWindup   = 0.3f;
        sentinel.populationCap  = 10;
        sentinel.groupSizeMin   = 1;
        sentinel.groupSizeMax   = 2;
        sentinel.canBeRestored  = true;
        sentinel.converseLines  = new[]
        {
            "...this route is still under my protection. State your clearance.",
            "Unauthorised presence. I am giving you one warning. Stand down.",
            "I remember this path. I walked it for years. Why are you here?",
            "Something — something is wrong with me. Do not make me act on directive."
        };
        sentinel.prefab = LoadPrefab(sentinelPath);
        SetLootTable(sentinel, (memoryShard, 1, 1, 0.6f), (directiveShard, 1, 1, 0.5f));
        EditorUtility.SetDirty(sentinel);

        var crafter = GetOrCreateDef("DirectedCrafterDefinition");
        crafter.enemyName      = "Directed Crafter";
        crafter.category       = EnemyCategory.Directed;
        crafter.maxHealth      = 90f;
        crafter.moveSpeed      = 3.2f;
        crafter.attackDamage   = 10f;
        crafter.attackRange    = 1.6f;
        crafter.detectionRange = 16f;
        crafter.armor          = 2f;
        crafter.attackCooldown = 1.8f;
        crafter.attackWindup   = 0.4f;
        crafter.populationCap  = 6;
        crafter.groupSizeMin   = 1;
        crafter.groupSizeMax   = 1;
        crafter.canBeRestored  = true;
        crafter.converseLines  = new[]
        {
            "Still making things. The directive says repair. I repair.",
            "I built the water tower on Level 3. I do not know why I remember that.",
            "You should not be here. Leave, and I will not escalate."
        };
        crafter.prefab = LoadPrefab(crafterPath);
        SetLootTable(crafter, (schematicFragment, 1, 1, 0.4f), (cortexComponents, 1, 2, 0.8f));
        EditorUtility.SetDirty(crafter);

        var stalker = GetOrCreateDef("CaveStalkerDefinition");
        stalker.enemyName      = "Cave Stalker";
        stalker.category       = EnemyCategory.WildCreature;
        stalker.maxHealth      = 55f;
        stalker.moveSpeed      = 6.5f;
        stalker.attackDamage   = 22f;
        stalker.attackRange    = 1.5f;
        stalker.detectionRange = 12f;
        stalker.armor          = 0f;
        stalker.attackCooldown = 0.9f;
        stalker.attackWindup   = 0.2f;
        stalker.populationCap  = 15;
        stalker.groupSizeMin   = 1;
        stalker.groupSizeMax   = 3;
        stalker.minDepthY      = -9999f;
        stalker.maxDepthY      = -20f;
        stalker.prefab         = LoadPrefab(stalkerPath);
        SetLootTable(stalker, (chitin, 1, 3, 0.9f));
        var rawMeat = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/ScriptableObjects/Items/RawMeat.asset");
        if (rawMeat != null) AppendLootEntry(stalker, rawMeat, 1, 2, 0.8f);
        EditorUtility.SetDirty(stalker);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Enemy Type Assets — Vol 6.4",
            "6 enemy types created with distinctive models.\n\n" +
            "NEXT: Run 'Voidborne > Setup Enemy Scene (Vol 6.4)' to place them in the scene.",
            "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODEL BUILDERS — one per enemy type
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// OptimizedPatrol — Slim military bipedal. Cyan.
    /// Distinctive features: narrow silhouette, prominent red visor strip,
    /// small arm stubs angled outward.
    /// </summary>
    static string BuildPatrolPrefab()
    {
        string path = $"{PrefabsPath}/OptimizedPatrol.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("OptimizedPatrol");
        Color teal = new Color(0.0f, 0.78f, 0.85f);
        Color visor = new Color(1.0f, 0.1f, 0.1f);

        // Torso — narrow capsule
        AddPrimitive(root, PrimitiveType.Capsule, "Torso", teal,
            pos: new Vector3(0, 1.0f, 0), scale: new Vector3(0.75f, 1.0f, 0.75f));

        // Head — sphere
        AddPrimitive(root, PrimitiveType.Sphere, "Head", teal,
            pos: new Vector3(0, 2.1f, 0), scale: Vector3.one * 0.38f);

        // Visor — flat red strip across head (facing forward)
        AddPrimitive(root, PrimitiveType.Cube, "Visor", visor,
            pos: new Vector3(0, 2.1f, 0.22f), scale: new Vector3(0.32f, 0.07f, 0.06f));

        // Left arm stub
        AddPrimitive(root, PrimitiveType.Capsule, "ArmL", teal,
            pos: new Vector3(-0.6f, 1.1f, 0), scale: new Vector3(0.18f, 0.35f, 0.18f),
            rot: new Vector3(0, 0, 25f));

        // Right arm stub
        AddPrimitive(root, PrimitiveType.Capsule, "ArmR", teal,
            pos: new Vector3(0.6f, 1.1f, 0), scale: new Vector3(0.18f, 0.35f, 0.18f),
            rot: new Vector3(0, 0, -25f));

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: false,
            addCrafter: false, addStalker: false);
    }

    /// <summary>
    /// OptimizedHeavy — Wide, armoured, imposing. Dark grey.
    /// Distinctive features: wide stocky torso, prominent shoulder pads,
    /// chest armour plate, angular helmet. WeakPoint collider on back.
    /// </summary>
    static string BuildHeavyPrefab()
    {
        string path = $"{PrefabsPath}/OptimizedHeavy.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("OptimizedHeavy");
        Color steel  = new Color(0.32f, 0.34f, 0.38f);
        Color accent = new Color(0.55f, 0.56f, 0.60f);

        // Torso — wide and short capsule
        AddPrimitive(root, PrimitiveType.Capsule, "Torso", steel,
            pos: new Vector3(0, 0.95f, 0), scale: new Vector3(1.35f, 0.9f, 1.1f));

        // Helmet — cube (angular)
        AddPrimitive(root, PrimitiveType.Cube, "Helmet", steel,
            pos: new Vector3(0, 2.05f, 0), scale: new Vector3(0.55f, 0.45f, 0.55f));

        // Visor slot on helmet
        AddPrimitive(root, PrimitiveType.Cube, "Visor", new Color(0.9f, 0.3f, 0.1f),
            pos: new Vector3(0, 2.05f, 0.28f), scale: new Vector3(0.4f, 0.1f, 0.05f));

        // Shoulder pad L
        AddPrimitive(root, PrimitiveType.Cube, "ShoulderL", accent,
            pos: new Vector3(-0.82f, 1.72f, 0), scale: new Vector3(0.45f, 0.14f, 0.55f));

        // Shoulder pad R
        AddPrimitive(root, PrimitiveType.Cube, "ShoulderR", accent,
            pos: new Vector3(0.82f, 1.72f, 0), scale: new Vector3(0.45f, 0.14f, 0.55f));

        // Chest plate
        AddPrimitive(root, PrimitiveType.Cube, "ChestPlate", accent,
            pos: new Vector3(0, 1.25f, 0.42f), scale: new Vector3(0.65f, 0.5f, 0.1f));

        // Arm stubs — wide
        AddPrimitive(root, PrimitiveType.Capsule, "ArmL", steel,
            pos: new Vector3(-0.75f, 1.1f, 0), scale: new Vector3(0.28f, 0.38f, 0.28f),
            rot: new Vector3(0, 0, 15f));
        AddPrimitive(root, PrimitiveType.Capsule, "ArmR", steel,
            pos: new Vector3(0.75f, 1.1f, 0), scale: new Vector3(0.28f, 0.38f, 0.28f),
            rot: new Vector3(0, 0, -15f));

        // WeakPoint child collider on back
        var wpGO = new GameObject("WeakPoint");
        wpGO.transform.SetParent(root.transform);
        wpGO.transform.localPosition = new Vector3(0, 1.0f, -0.5f);
        wpGO.tag = "Enemy";
        var wpCol = wpGO.AddComponent<CapsuleCollider>();
        wpCol.center = Vector3.zero; wpCol.radius = 0.4f; wpCol.height = 1.4f;
        wpGO.AddComponent<WeakPoint>();

        // Visual arrow indicator on back (glowing red — weak point hint)
        AddPrimitive(wpGO, PrimitiveType.Cube, "WeakIndicator", new Color(0.8f, 0.0f, 0.0f),
            pos: new Vector3(0, 0, -0.05f), scale: new Vector3(0.18f, 0.35f, 0.04f));

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: false,
            addCrafter: false, addStalker: false);
    }

    /// <summary>
    /// OptimizedRanged — Slim, energy-rifle arm forward. Light blue.
    /// Distinctive features: narrow body, prominent glowing rifle barrel
    /// held forward, muzzle glow sphere.
    /// </summary>
    static string BuildRangedPrefab()
    {
        string path = $"{PrefabsPath}/OptimizedRanged.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("OptimizedRanged");
        Color skyBlue  = new Color(0.25f, 0.60f, 1.0f);
        Color energyGlow = new Color(0.0f, 0.9f, 1.0f);

        // Torso — narrow
        AddPrimitive(root, PrimitiveType.Capsule, "Torso", skyBlue,
            pos: new Vector3(0, 1.0f, 0), scale: new Vector3(0.65f, 1.0f, 0.65f));

        // Head — small sphere
        AddPrimitive(root, PrimitiveType.Sphere, "Head", skyBlue,
            pos: new Vector3(0, 2.1f, 0), scale: Vector3.one * 0.33f);

        // Sensor band on head
        AddPrimitive(root, PrimitiveType.Cube, "SensorBand", energyGlow,
            pos: new Vector3(0, 2.1f, 0.2f), scale: new Vector3(0.3f, 0.06f, 0.06f));

        // Right weapon arm — raised and forward
        AddPrimitive(root, PrimitiveType.Capsule, "ArmR_Upper", skyBlue,
            pos: new Vector3(0.48f, 1.3f, 0.1f), scale: new Vector3(0.16f, 0.3f, 0.16f),
            rot: new Vector3(-40f, 0, -20f));

        // Rifle barrel — cylinder pointing forward from right hand
        AddPrimitive(root, PrimitiveType.Cylinder, "RifleBarrel", new Color(0.2f, 0.2f, 0.3f),
            pos: new Vector3(0.38f, 1.35f, 0.55f),
            scale: new Vector3(0.07f, 0.35f, 0.07f),
            rot: new Vector3(90f, 0, 0));

        // Muzzle glow
        AddPrimitive(root, PrimitiveType.Sphere, "MuzzleGlow", energyGlow,
            pos: new Vector3(0.38f, 1.35f, 0.9f), scale: Vector3.one * 0.1f);

        // Left arm relaxed
        AddPrimitive(root, PrimitiveType.Capsule, "ArmL", skyBlue,
            pos: new Vector3(-0.5f, 1.1f, 0), scale: new Vector3(0.16f, 0.3f, 0.16f),
            rot: new Vector3(0, 0, 20f));

        // Tag muzzle point for RangedEnemyAttack
        var muzzleGO = new GameObject("MuzzlePoint");
        muzzleGO.transform.SetParent(root.transform);
        muzzleGO.transform.localPosition = new Vector3(0.38f, 1.35f, 0.9f);

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: true,
            addCrafter: false, addStalker: false, muzzleGO: muzzleGO);
    }

    /// <summary>
    /// DirectedSentinel — Former guardian, dignified upright posture. Golden yellow.
    /// Distinctive features: standard build, triangular shoulder emblems,
    /// trailing cloak panel behind body.
    /// </summary>
    static string BuildSentinelPrefab()
    {
        string path = $"{PrefabsPath}/DirectedSentinel.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("DirectedSentinel");
        Color gold    = new Color(0.92f, 0.78f, 0.15f);
        Color darkGold = new Color(0.55f, 0.46f, 0.08f);

        // Torso
        AddPrimitive(root, PrimitiveType.Capsule, "Torso", gold,
            pos: new Vector3(0, 1.0f, 0), scale: new Vector3(0.85f, 1.0f, 0.85f));

        // Head — slightly larger
        AddPrimitive(root, PrimitiveType.Sphere, "Head", gold,
            pos: new Vector3(0, 2.15f, 0), scale: Vector3.one * 0.44f);

        // Eye slit
        AddPrimitive(root, PrimitiveType.Cube, "EyeSlit", new Color(1f, 0.4f, 0f),
            pos: new Vector3(0, 2.15f, 0.24f), scale: new Vector3(0.35f, 0.06f, 0.05f));

        // Shoulder emblem L (rotated cube = diamond shape)
        AddPrimitive(root, PrimitiveType.Cube, "EmblemL", darkGold,
            pos: new Vector3(-0.62f, 1.72f, 0.1f), scale: new Vector3(0.2f, 0.2f, 0.06f),
            rot: new Vector3(0, 0, 45f));

        // Shoulder emblem R
        AddPrimitive(root, PrimitiveType.Cube, "EmblemR", darkGold,
            pos: new Vector3(0.62f, 1.72f, 0.1f), scale: new Vector3(0.2f, 0.2f, 0.06f),
            rot: new Vector3(0, 0, 45f));

        // Cloak panel behind torso
        AddPrimitive(root, PrimitiveType.Cube, "Cloak", darkGold,
            pos: new Vector3(0, 0.85f, -0.42f), scale: new Vector3(0.6f, 1.0f, 0.06f));

        // Arms
        AddPrimitive(root, PrimitiveType.Capsule, "ArmL", gold,
            pos: new Vector3(-0.63f, 1.1f, 0), scale: new Vector3(0.19f, 0.36f, 0.19f),
            rot: new Vector3(0, 0, 18f));
        AddPrimitive(root, PrimitiveType.Capsule, "ArmR", gold,
            pos: new Vector3(0.63f, 1.1f, 0), scale: new Vector3(0.19f, 0.36f, 0.19f),
            rot: new Vector3(0, 0, -18f));

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: false,
            addCrafter: false, addStalker: false);
    }

    /// <summary>
    /// DirectedCrafter — Shorter, tool-bearing, with backpack. Orange.
    /// Distinctive features: slightly shorter frame, oversized cylindrical tool arm
    /// extending forward, boxy backpack module on back.
    /// </summary>
    static string BuildCrafterPrefab()
    {
        string path = $"{PrefabsPath}/DirectedCrafter.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("DirectedCrafter");
        Color orange = new Color(0.92f, 0.48f, 0.08f);
        Color dark   = new Color(0.35f, 0.18f, 0.03f);

        // Torso — slightly shorter/stouter
        AddPrimitive(root, PrimitiveType.Capsule, "Torso", orange,
            pos: new Vector3(0, 0.9f, 0), scale: new Vector3(0.9f, 0.88f, 0.9f));

        // Head
        AddPrimitive(root, PrimitiveType.Sphere, "Head", orange,
            pos: new Vector3(0, 1.95f, 0), scale: Vector3.one * 0.40f);

        // Goggles
        AddPrimitive(root, PrimitiveType.Cube, "Goggles", dark,
            pos: new Vector3(0, 1.95f, 0.23f), scale: new Vector3(0.38f, 0.1f, 0.06f));

        // Tool arm — right arm extends forward with large cylindrical end-effector
        AddPrimitive(root, PrimitiveType.Capsule, "ArmR", orange,
            pos: new Vector3(0.5f, 1.1f, 0.15f), scale: new Vector3(0.18f, 0.38f, 0.18f),
            rot: new Vector3(-30f, 0, -15f));

        // Tool head (large cylinder tip)
        AddPrimitive(root, PrimitiveType.Cylinder, "ToolHead", dark,
            pos: new Vector3(0.5f, 1.0f, 0.55f), scale: new Vector3(0.2f, 0.15f, 0.2f),
            rot: new Vector3(90f, 0, 0));

        // Left arm (relaxed)
        AddPrimitive(root, PrimitiveType.Capsule, "ArmL", orange,
            pos: new Vector3(-0.55f, 1.05f, 0), scale: new Vector3(0.18f, 0.32f, 0.18f),
            rot: new Vector3(0, 0, 22f));

        // Backpack module
        AddPrimitive(root, PrimitiveType.Cube, "Backpack", dark,
            pos: new Vector3(0, 1.0f, -0.48f), scale: new Vector3(0.38f, 0.5f, 0.22f));

        // Utility belt
        AddPrimitive(root, PrimitiveType.Cube, "Belt", dark,
            pos: new Vector3(0, 0.55f, 0), scale: new Vector3(0.82f, 0.1f, 0.82f));

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: false,
            addCrafter: true, addStalker: false);
    }

    /// <summary>
    /// CaveStalker — Quadruped creature, low to ground. Purple/dark violet.
    /// Distinctive features: horizontal elongated body, four splayed legs,
    /// upward-curving tail. No upright head — sensor cluster at front.
    /// </summary>
    static string BuildStalkerPrefab()
    {
        string path = $"{PrefabsPath}/CaveStalker.prefab";
        if (File.Exists(path)) return path;

        var root = new GameObject("CaveStalker");
        Color violet = new Color(0.50f, 0.18f, 0.65f);
        Color dark   = new Color(0.22f, 0.08f, 0.30f);

        // Body — horizontal capsule (like a beast torso)
        AddPrimitive(root, PrimitiveType.Capsule, "Body", violet,
            pos: new Vector3(0, 0.5f, 0), scale: new Vector3(0.7f, 1.1f, 0.7f),
            rot: new Vector3(90f, 0, 0));        // rotated horizontal

        // Sensor cluster (head) at front
        AddPrimitive(root, PrimitiveType.Sphere, "SensorCluster", dark,
            pos: new Vector3(0, 0.55f, 0.8f), scale: Vector3.one * 0.32f);

        // Eye pairs — two red spheres
        AddPrimitive(root, PrimitiveType.Sphere, "EyeL", new Color(0.9f, 0.1f, 0.1f),
            pos: new Vector3(-0.12f, 0.62f, 0.96f), scale: Vector3.one * 0.09f);
        AddPrimitive(root, PrimitiveType.Sphere, "EyeR", new Color(0.9f, 0.1f, 0.1f),
            pos: new Vector3(0.12f, 0.62f, 0.96f), scale: Vector3.one * 0.09f);

        // Four legs — cylinders angled outward and downward
        // Front-left
        AddPrimitive(root, PrimitiveType.Cylinder, "LegFL", dark,
            pos: new Vector3(-0.5f, 0.25f, 0.45f), scale: new Vector3(0.1f, 0.3f, 0.1f),
            rot: new Vector3(0, 0, 35f));
        // Front-right
        AddPrimitive(root, PrimitiveType.Cylinder, "LegFR", dark,
            pos: new Vector3(0.5f, 0.25f, 0.45f), scale: new Vector3(0.1f, 0.3f, 0.1f),
            rot: new Vector3(0, 0, -35f));
        // Rear-left
        AddPrimitive(root, PrimitiveType.Cylinder, "LegRL", dark,
            pos: new Vector3(-0.5f, 0.25f, -0.45f), scale: new Vector3(0.1f, 0.3f, 0.1f),
            rot: new Vector3(0, 0, 35f));
        // Rear-right
        AddPrimitive(root, PrimitiveType.Cylinder, "LegRR", dark,
            pos: new Vector3(0.5f, 0.25f, -0.45f), scale: new Vector3(0.1f, 0.3f, 0.1f),
            rot: new Vector3(0, 0, -35f));

        // Tail — cylinder curving upward at rear
        AddPrimitive(root, PrimitiveType.Cylinder, "Tail", violet,
            pos: new Vector3(0, 0.75f, -0.8f), scale: new Vector3(0.1f, 0.4f, 0.1f),
            rot: new Vector3(-40f, 0, 0));

        // Tail tip
        AddPrimitive(root, PrimitiveType.Sphere, "TailTip", dark,
            pos: new Vector3(0, 1.1f, -1.0f), scale: Vector3.one * 0.14f);

        return FinaliseEnemyPrefab(root, path, addWeakPoint: false, addRanged: false,
            addCrafter: false, addStalker: true, ccCenter: new Vector3(0, 0.5f, 0),
            ccHeight: 1.0f, ccRadius: 0.5f);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SHARED PREFAB FINALISER
    // ═══════════════════════════════════════════════════════════════════════════

    static string FinaliseEnemyPrefab(
        GameObject root, string path,
        bool addWeakPoint, bool addRanged, bool addCrafter, bool addStalker,
        GameObject muzzleGO = null,
        Vector3? ccCenter = null, float ccHeight = 2.0f, float ccRadius = 0.4f)
    {
        // CharacterController
        var cc    = root.AddComponent<CharacterController>();
        cc.center = ccCenter ?? new Vector3(0, 1f, 0);
        cc.height = ccHeight;
        cc.radius = ccRadius;

        // Navigation + AI
        root.AddComponent<EnemyEntity>();
        root.AddComponent<AudioSource>();

        // Type-specific extras
        if (addRanged)
        {
            var rea = root.AddComponent<RangedEnemyAttack>();
            if (muzzleGO != null)
            {
                var so = new SerializedObject(rea);
                var p  = so.FindProperty("_muzzlePoint");
                if (p != null) { p.objectReferenceValue = muzzleGO.transform; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        if (addCrafter) root.AddComponent<DirectedCrafterBehavior>();
        if (addStalker) root.AddComponent<CaveStalkerBehavior>();

        // Tag root as "Enemy" so raycast hits on the CharacterController are classified correctly.
        root.tag = "Enemy";

        // Tag body child as Enemy for HitEffects (visual child, kept for legacy PatchPrefabTags)
        var body = root.transform.Find("Torso") ?? root.transform.Find("Body");
        if (body != null) body.gameObject.tag = "Enemy";

        // Add a head collider and "Head" tag so headshots register.
        // "Head" is used by patrol/ranged/sentinel/crafter; "Helmet" by heavy; "SensorCluster" by stalker.
        var head = root.transform.Find("Head")
                ?? root.transform.Find("Helmet")
                ?? root.transform.Find("SensorCluster");
        if (head != null)
        {
            head.gameObject.tag = "Head";
            if (head.GetComponent<Collider>() == null)
            {
                var sc = head.gameObject.AddComponent<SphereCollider>();
                sc.radius = 0.5f; // natural sphere-primitive radius; scaled by head's localScale
            }
        }

        // Save prefab
        var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[EnemyAssetCreator] Prefab saved: {path}");
        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIMITIVE HELPER
    // ═══════════════════════════════════════════════════════════════════════════

    static void AddPrimitive(GameObject parent, PrimitiveType type, string objName,
        Color color, Vector3 pos, Vector3 scale,
        Vector3 rot = default, bool removeCollider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objName;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        if (rot != default) go.transform.localEulerAngles = rot;

        // Remove auto-added colliders (CharacterController handles physics)
        if (removeCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // Save material as a proper project asset so it persists in prefabs.
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = GetOrCreateColorMaterial(color);
    }

    /// <summary>
    /// Returns a saved .mat asset matching the given color.
    /// Reuses existing assets with the same name (color-keyed) to avoid duplicates.
    /// Materials are stored under Assets/Materials/Enemies/.
    /// </summary>
    static Material GetOrCreateColorMaterial(Color color)
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder(MatsPath);

        // Key by color components so identical colors share one material
        string matName = $"EnemyMat_{(int)(color.r*255):D3}_{(int)(color.g*255):D3}_{(int)(color.b*255):D3}";
        string matPath = $"{MatsPath}/{matName}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null) return mat;

        // Try URP Lit first, fall back to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");

        mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
        // Set color for both URP (_BaseColor) and Standard (_Color)
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);

        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    // Overload that accepts a child-of-child parent
    static void AddPrimitive(GameObject parent, PrimitiveType type, string objName,
        Color color, Vector3 pos, Vector3 scale, bool removeCollider)
        => AddPrimitive(parent, type, objName, color, pos, scale, default, removeCollider);

    // ═══════════════════════════════════════════════════════════════════════════
    // ASSET HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    static T GetOrCreateItem<T>(string fileName, string displayName, string desc,
                                 ItemType type, int maxStack) where T : ItemDefinition
    {
        string path = $"{ItemsPath}/{fileName}.asset";
        T item = AssetDatabase.LoadAssetAtPath<T>(path);
        if (item != null) return item;

        item = ScriptableObject.CreateInstance<T>();
        item.itemId = fileName.ToLower();
        item.displayName = displayName;
        item.description = desc;
        item.itemType = type;
        item.maxStackSize = maxStack;
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    static T GetOrCreateSpecialItem<T>(string fileName, string displayName, string desc,
                                        ItemType type, int maxStack,
                                        System.Action<T> configure) where T : ItemDefinition
    {
        string path = $"{ItemsPath}/{fileName}.asset";
        T item = AssetDatabase.LoadAssetAtPath<T>(path);
        bool isNew = item == null;
        if (isNew)
        {
            item = ScriptableObject.CreateInstance<T>();
            item.itemId = fileName.ToLower();
            item.displayName = displayName;
            item.description = desc;
            item.itemType = type;
            item.maxStackSize = maxStack;
        }
        configure?.Invoke(item);
        if (isNew) AssetDatabase.CreateAsset(item, path);
        else EditorUtility.SetDirty(item);
        return item;
    }

    static EnemyDefinition GetOrCreateDef(string fileName)
    {
        string path = $"{EnemiesPath}/{fileName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (def != null) return def;
        def = ScriptableObject.CreateInstance<EnemyDefinition>();
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    static GameObject LoadPrefab(string path) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static void SetLootTable(EnemyDefinition def,
        params (ItemDefinition item, int min, int max, float chance)[] entries)
    {
        def.lootTable.Clear();
        foreach (var e in entries)
            if (e.item != null)
                def.lootTable.Add(new LootEntry
                    { item = e.item, minQuantity = e.min, maxQuantity = e.max, dropChance = e.chance });
    }

    static void AppendLootEntry(EnemyDefinition def, ItemDefinition item,
        int min, int max, float chance) =>
        def.lootTable.Add(new LootEntry
            { item = item, minQuantity = min, maxQuantity = max, dropChance = chance });

    static void EnsureFolder(string path)
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
