using UnityEditor;
using UnityEngine;
using Voidborne.World.Generation;

/// <summary>
/// One-shot editor utility to wire sky ore definitions into the OreRegistry
/// and create tree prefabs for sky biome tree variants.
/// Run from menu: Voidborne > Setup Sky Biomes
/// </summary>
public static class SkyBiomeSetup
{
    [MenuItem("Voidborne/Setup Sky Biomes")]
    public static void SetupAll()
    {
        WireOreRegistry();
        CreateTreePrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SkyBiomeSetup] Setup complete!");
    }

    private static void WireOreRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<OreRegistry>(
            "Assets/ScriptableObjects/Ores/OreRegistry.asset");
        if (registry == null)
        {
            Debug.LogError("[SkyBiomeSetup] OreRegistry not found!");
            return;
        }

        var aetherium = AssetDatabase.LoadAssetAtPath<OreDefinition>(
            "Assets/ScriptableObjects/Ores/AetheriumOre.asset");
        var celestium = AssetDatabase.LoadAssetAtPath<OreDefinition>(
            "Assets/ScriptableObjects/Ores/CelestiumOre.asset");
        var voidstone = AssetDatabase.LoadAssetAtPath<OreDefinition>(
            "Assets/ScriptableObjects/Ores/VoidstoneOre.asset");

        if (aetherium == null || celestium == null || voidstone == null)
        {
            Debug.LogError("[SkyBiomeSetup] Missing sky ore assets!");
            return;
        }

        var so = new SerializedObject(registry);
        var arr = so.FindProperty("oreDefinitions");

        // Check if already wired
        for (int i = 0; i < arr.arraySize; i++)
        {
            var elem = arr.GetArrayElementAtIndex(i);
            if (elem.objectReferenceValue == aetherium)
            {
                Debug.Log("[SkyBiomeSetup] Sky ores already wired.");
                return;
            }
        }

        int startIdx = arr.arraySize;
        arr.arraySize = startIdx + 3;
        arr.GetArrayElementAtIndex(startIdx).objectReferenceValue = aetherium;
        arr.GetArrayElementAtIndex(startIdx + 1).objectReferenceValue = celestium;
        arr.GetArrayElementAtIndex(startIdx + 2).objectReferenceValue = voidstone;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);
        Debug.Log($"[SkyBiomeSetup] Added 3 sky ores to registry (indices {startIdx}-{startIdx + 2}).");
    }

    private static void CreateTreePrefabs()
    {
        CreateSkyTreePrefab("TreeWindswept", 0.3f, 3.0f, 0.15f, 1.8f, 1.2f,
            new Vector3(0.5f, 3.0f, 0f), new Color(0.45f, 0.35f, 0.2f), new Color(0.5f, 0.7f, 0.4f));
        CreateSkyTreePrefab("TreeShimmerleaf", 0.2f, 4.0f, 0.12f, 2.2f, 1.8f,
            new Vector3(0f, 3.5f, 0f), new Color(0.55f, 0.45f, 0.35f), new Color(0.4f, 0.75f, 0.65f));
        CreateSkyTreePrefab("TreeSkyCedar", 0.45f, 4.5f, 0.25f, 1.5f, 3.0f,
            new Vector3(0f, 4.0f, 0f), new Color(0.4f, 0.3f, 0.15f), new Color(0.25f, 0.55f, 0.3f));
    }

    private static void CreateSkyTreePrefab(string name, float trunkBaseR, float trunkH,
        float trunkTopR, float foliageR, float foliageH, Vector3 foliageCenter,
        Color trunkColor, Color foliageColor)
    {
        string path = $"Assets/Prefabs/World/Trees/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"[SkyBiomeSetup] {name} prefab already exists, skipping.");
            return;
        }

        var root = new GameObject(name);

        // Trunk child
        var trunkObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkObj.name = "Trunk";
        trunkObj.transform.SetParent(root.transform);
        trunkObj.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
        trunkObj.transform.localScale = new Vector3(trunkBaseR * 2f, trunkH * 0.5f, trunkBaseR * 2f);
        Object.DestroyImmediate(trunkObj.GetComponent<Collider>());

        // Set trunk material color
        var trunkMat = new Material(Shader.Find("Standard"));
        trunkMat.color = trunkColor;
        trunkMat.name = $"{name}_Trunk";
        AssetDatabase.CreateAsset(trunkMat, $"Assets/Prefabs/World/Trees/{name}_Trunk.mat");
        trunkObj.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

        // Foliage child
        var foliageObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliageObj.name = "Foliage";
        foliageObj.transform.SetParent(root.transform);
        foliageObj.transform.localPosition = foliageCenter;
        foliageObj.transform.localScale = new Vector3(foliageR * 2f, foliageH, foliageR * 2f);
        Object.DestroyImmediate(foliageObj.GetComponent<Collider>());

        var foliageMat = new Material(Shader.Find("Standard"));
        foliageMat.color = foliageColor;
        foliageMat.name = $"{name}_Foliage";
        AssetDatabase.CreateAsset(foliageMat, $"Assets/Prefabs/World/Trees/{name}_Foliage.mat");
        foliageObj.GetComponent<MeshRenderer>().sharedMaterial = foliageMat;

        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[SkyBiomeSetup] Created prefab: {path}");
    }
}
