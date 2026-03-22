using UnityEditor;
using UnityEngine;
using Voidborne.Automation;

/// <summary>
/// Editor utilities for the Furnace:
///   Voidborne > Create Furnace Prefab      — creates Assets/Prefabs/Furnace.prefab
///   Voidborne > Place Furnace In Scene     — instantiates the prefab in the current scene
///   Voidborne > Full Furnace Setup         — recipes + prefab + scene placement
/// </summary>
public static class FurnaceSetup
{
    private const string PrefabPath    = "Assets/Prefabs/Furnace.prefab";
    private const string PrefabsFolder = "Assets/Prefabs";

    // ------------------------------------------------------------------
    //  1. Create the Furnace prefab asset
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Create Furnace Prefab")]
    public static void CreateFurnacePrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject go = BuildFurnaceGO();

        bool success;
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
        Object.DestroyImmediate(go);

        if (success)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[FurnaceSetup] Furnace prefab created at {PrefabPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        else
        {
            Debug.LogError("[FurnaceSetup] Failed to create Furnace prefab.");
        }
    }

    // ------------------------------------------------------------------
    //  2. Place a Furnace in the current scene
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Place Furnace In Scene")]
    public static void PlaceFurnaceInScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[FurnaceSetup] Furnace prefab not found. Creating it first.");
            CreateFurnacePrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        instance.transform.position = new Vector3(3f, 0.5f, 3f);
        instance.name = "Furnace";

        Undo.RegisterCreatedObjectUndo(instance, "Place Furnace");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();

        Debug.Log("[FurnaceSetup] Furnace placed in scene at (3, 0.5, 3).");
    }

    // ------------------------------------------------------------------
    //  3. Full setup: recipes + prefab + scene placement
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Full Furnace Setup")]
    public static void FullSetup()
    {
        SmeltingRecipeCreator.CreateSmeltingRecipes();
        CreateFurnacePrefab();
        PlaceFurnaceInScene();
        Debug.Log("[FurnaceSetup] Full furnace setup complete. " +
                  "Don't forget to assign the SmeltingRecipe assets and Coal ItemDefinition " +
                  "to the FurnaceBlock component in the Inspector.");
    }

    // ------------------------------------------------------------------
    //  Helper: build the furnace GameObject in memory
    // ------------------------------------------------------------------
    private static GameObject BuildFurnaceGO()
    {
        GameObject go = new GameObject("Furnace");

        // Visual: a cube
        GameObject cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeGO.name = "FurnaceMesh";
        cubeGO.transform.SetParent(go.transform, false);
        cubeGO.transform.localPosition = Vector3.zero;
        cubeGO.transform.localScale    = new Vector3(1f, 1f, 1f);

        // Dark grey / stone-like material
        Renderer rend = cubeGO.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));

            mat.color = new Color(0.25f, 0.22f, 0.20f); // dark stone / charcoal

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string matPath = "Assets/Materials/FurnaceMaterial.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        // Remove auto-added collider and replace with one on the root
        Object.DestroyImmediate(cubeGO.GetComponent<Collider>());

        BoxCollider boxCol = go.AddComponent<BoxCollider>();
        boxCol.center    = Vector3.zero;
        boxCol.size      = Vector3.one;
        boxCol.isTrigger = false;

        // FurnaceBlock component
        FurnaceBlock furnace = go.AddComponent<FurnaceBlock>();
        // The 'recipes' list and 'fuelItem' must be assigned in the Inspector after creation.
        // interactRange defaults to 3f (set in the FurnaceBlock field initializer).

        return go;
    }
}
