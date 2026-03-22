using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne;
using Voidborne.Crafting;

/// <summary>
/// Editor utility:
///   Voidborne > Create Workbench Prefab  — creates Assets/Prefabs/Workbench.prefab
///   Voidborne > Place Workbench In Scene — instantiates the prefab in the current scene
///   Voidborne > Full 3x3 Setup          — recipes + prefab + scene placement + adds recipes to CraftingManager
/// </summary>
public static class WorkbenchSetup
{
    private const string PrefabPath  = "Assets/Prefabs/Workbench.prefab";
    private const string PrefabsFolder = "Assets/Prefabs";

    // ------------------------------------------------------------------
    //  1. Create the Workbench prefab asset
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Create Workbench Prefab")]
    public static void CreateWorkbenchPrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Build the GameObject in memory.
        GameObject go = BuildWorkbenchGO();

        // Save as prefab.
        bool success;
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
        Object.DestroyImmediate(go);

        if (success)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[WorkbenchSetup] Workbench prefab created at {PrefabPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        else
        {
            Debug.LogError("[WorkbenchSetup] Failed to create Workbench prefab.");
        }
    }

    // ------------------------------------------------------------------
    //  2. Place a Workbench in the current scene
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Place Workbench In Scene")]
    public static void PlaceWorkbenchInScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[WorkbenchSetup] Workbench prefab not found. Creating it first.");
            CreateWorkbenchPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        // Place it 5 units in front of origin.
        instance.transform.position = new Vector3(5f, 0.5f, 5f);
        instance.name = "Workbench";

        Undo.RegisterCreatedObjectUndo(instance, "Place Workbench");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();

        Debug.Log("[WorkbenchSetup] Workbench placed in scene at (5, 0.5, 5).");
    }

    // ------------------------------------------------------------------
    //  3. Full setup: recipes + prefab + place in scene + wire CraftingManager
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Full 3x3 Setup")]
    public static void FullSetup()
    {
        ThreeByThreeRecipeCreator.Create3x3Recipes();
        CreateWorkbenchPrefab();
        PlaceWorkbenchInScene();
        AddRecipesToCraftingManager();
        Debug.Log("[WorkbenchSetup] Full 3×3 setup complete.");
    }

    // ------------------------------------------------------------------
    //  4. Add the 5 new recipes to the CraftingManager in the scene
    // ------------------------------------------------------------------
    [MenuItem("Voidborne/Add 3x3 Recipes To CraftingManager")]
    public static void AddRecipesToCraftingManager()
    {
        CraftingManager cm = Object.FindFirstObjectByType<CraftingManager>();
        if (cm == null)
        {
            Debug.LogWarning("[WorkbenchSetup] No CraftingManager found in the scene.");
            return;
        }

        string[] assetNames = { "StoneFurnace", "IronPickaxe", "IronSword", "WoodenDoor", "Chest" };
        SerializedObject so = new SerializedObject(cm);
        SerializedProperty recipesProp = so.FindProperty("recipes");

        foreach (string name in assetNames)
        {
            string path = $"Assets/ScriptableObjects/Recipes/{name}.asset";
            CraftingRecipe recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
            if (recipe == null)
            {
                Debug.LogWarning($"[WorkbenchSetup] Recipe asset not found: {path}. Run 'Create 3x3 Recipes' first.");
                continue;
            }

            // Check if already present
            bool alreadyIn = false;
            for (int i = 0; i < recipesProp.arraySize; i++)
            {
                if (recipesProp.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
                {
                    alreadyIn = true;
                    break;
                }
            }

            if (!alreadyIn)
            {
                recipesProp.arraySize++;
                recipesProp.GetArrayElementAtIndex(recipesProp.arraySize - 1).objectReferenceValue = recipe;
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(cm);

        Debug.Log("[WorkbenchSetup] 3×3 recipes added to CraftingManager.");
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static GameObject BuildWorkbenchGO()
    {
        // Root object
        GameObject go = new GameObject("Workbench");

        // Visual: a cube representing the workbench
        GameObject cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeGO.name = "WorkbenchMesh";
        cubeGO.transform.SetParent(go.transform, false);
        cubeGO.transform.localPosition = Vector3.zero;
        cubeGO.transform.localScale    = new Vector3(1f, 1f, 1f);

        // Apply a brown/wood-ish material color
        Renderer rend = cubeGO.GetComponent<Renderer>();
        if (rend != null)
        {
            // Create a simple material with a brownish color
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback if URP shader not found
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = new Color(0.55f, 0.35f, 0.15f); // wood brown
            rend.sharedMaterial = mat;

            // Save the material as an asset so it persists in the prefab
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            string matPath = "Assets/Materials/WorkbenchMaterial.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        // Remove the Collider added by CreatePrimitive — we'll add our own
        Object.DestroyImmediate(cubeGO.GetComponent<Collider>());

        // BoxCollider on the root (solid, non-trigger — player can stand on it)
        BoxCollider boxCol = go.AddComponent<BoxCollider>();
        boxCol.center    = Vector3.zero;
        boxCol.size      = Vector3.one;
        boxCol.isTrigger = false;

        // CraftingStation component
        CraftingStation station = go.AddComponent<CraftingStation>();
        // gridWidth and gridHeight are serialized fields — set via SerializedObject
        SerializedObject so = new SerializedObject(station);
        so.FindProperty("gridWidth").intValue  = 3;
        so.FindProperty("gridHeight").intValue = 3;
        so.FindProperty("interactRange").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }
}
