#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor setup script for Vol 10.1 — Quest System Framework.
/// Menu: Voidborne > Setup Quests Vol 10.1
///
/// Creates:
///   • QuestManager prefab (with QuestEventBridge sibling)
///   • Six main-story quest SOs (MSQ_01–MSQ_06)
///   • Ensures Resources/Quests/ folder exists
/// </summary>
public static class QuestSetup101
{
    [MenuItem("Voidborne/Quests/Setup Quests Vol 10.1")]
    public static void SetupQuests()
    {
        // ---------------------------------------------------------------
        // 1. Ensure folders
        // ---------------------------------------------------------------
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Quests");
        EnsureFolder("Assets/Prefabs/Quests");

        // ---------------------------------------------------------------
        // 2. QuestManager + QuestEventBridge prefab
        // ---------------------------------------------------------------
        string prefabPath = "Assets/Prefabs/Quests/QuestManager.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing == null)
        {
            var go = new GameObject("QuestManager");
            go.AddComponent<QuestManager>();
            go.AddComponent<QuestEventBridge>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[QuestSetup] Created prefab: {prefabPath}");
        }
        else
        {
            Debug.Log($"[QuestSetup] Prefab already exists: {prefabPath}");
        }

        // ---------------------------------------------------------------
        // 3. Main-story quest SOs
        // ---------------------------------------------------------------
        CreateMSQ("MSQ_01_Awakening",
            id:          "msq_01_awakening",
            questName:   "Awakening",
            desc:        "Find food, craft a basic tool, find shelter before dark.",
            mainStory:   true);

        CreateMSQ("MSQ_02_FindAshfen",
            id:          "msq_02_find_ashfen",
            questName:   "Follow the Signal",
            desc:        "Follow a weak signal to Ashfen. Meet ElderMoss. Learn about 'the Maker.'",
            mainStory:   true,
            prereqs:     new[] { "msq_01_awakening" });

        CreateMSQ("MSQ_03_StrongholdOne",
            id:          "msq_03_stronghold_one",
            questName:   "Stronghold One",
            desc:        "Find Stronghold 1. Navigate to it. Find and install Module 1 — The Pulse.",
            mainStory:   true,
            prereqs:     new[] { "msq_02_find_ashfen" });

        CreateMSQ("MSQ_04_TheDelve",
            id:          "msq_04_the_delve",
            questName:   "Descent",
            desc:        "Descend underground, locate The Delve. Meet ForgeKeeperTar and ArchivistCell.",
            mainStory:   true,
            prereqs:     new[] { "msq_03_stronghold_one" });

        CreateMSQ("MSQ_05_StrongholdTwo",
            id:          "msq_05_stronghold_two",
            questName:   "Stronghold Two",
            desc:        "Find Stronghold 2 near The Delve. Find and install Module 2 — The Shard Index.",
            mainStory:   true,
            prereqs:     new[] { "msq_04_the_delve" });

        CreateMSQ("MSQ_06_SkyIslands",
            id:          "msq_06_sky_islands",
            questName:   "Spire's Rest",
            desc:        "Ascend to sky islands, locate Spire's Rest. Find and install Module 4 — The Conductor.",
            mainStory:   true,
            prereqs:     new[] { "msq_05_stronghold_two" });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[QuestSetup] Vol 10.1 setup complete. " +
                  "Add the QuestManager prefab to your scene and assign quest SOs in the Inspector.");
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static void CreateMSQ(string fileName, string id, string questName,
                                   string desc, bool mainStory = false,
                                   string[] prereqs = null)
    {
        string path = $"Assets/Resources/Quests/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (existing != null)
        {
            Debug.Log($"[QuestSetup] Already exists: {path}");
            return;
        }

        var so = ScriptableObject.CreateInstance<QuestDefinition>();
        so.questId   = id;
        so.questName = questName;
        so.description = desc;
        so.isMainStory = mainStory;

        if (prereqs != null)
            so.prerequisiteQuestIds.AddRange(prereqs);

        AssetDatabase.CreateAsset(so, path);
        Debug.Log($"[QuestSetup] Created: {path}");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string folder = path.Substring(lastSlash + 1);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
