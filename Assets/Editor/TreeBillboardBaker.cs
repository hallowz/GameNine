using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility that bakes billboard textures from tree prefabs.
/// Renders each prefab from a slight angle with URP-compatible setup,
/// and saves the result as a PNG with alpha in Assets/Textures/Trees/.
/// </summary>
public static class TreeBillboardBaker
{
    private const int TexSize = 512;
    private const string OutputFolder = "Assets/Resources/Trees";

    private static readonly string[] PrefabPaths = {
        "Assets/Prefabs/World/Trees/TreeBroadleaf.prefab",
        "Assets/Prefabs/World/Trees/TreePine.prefab",
        "Assets/Prefabs/World/Trees/TreeScrub.prefab"
    };

    private static readonly string[] OutputNames = {
        "BillboardBroadleaf",
        "BillboardPine",
        "BillboardScrub"
    };

    [MenuItem("Tools/Bake Tree Billboards")]
    public static void Bake()
    {
        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
        }

        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
            if (prefab == null)
            {
                Debug.LogWarning($"TreeBillboardBaker: Could not load prefab at {PrefabPaths[i]}");
                continue;
            }

            Texture2D billboard = RenderPrefabToBillboard(prefab);
            if (billboard == null) continue;

            string path = $"{OutputFolder}/{OutputNames[i]}.png";
            byte[] png = billboard.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(billboard);

            Debug.Log($"TreeBillboardBaker: Saved {path}");
        }

        AssetDatabase.Refresh();

        // Set texture import settings for all billboards
        for (int i = 0; i < OutputNames.Length; i++)
        {
            string path = $"{OutputFolder}/{OutputNames[i]}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = TexSize;
                importer.SaveAndReimport();
            }
        }

        Debug.Log("TreeBillboardBaker: Done! Assign textures in TreeRenderer.billboardTextures[]");
    }

    private static Texture2D RenderPrefabToBillboard(GameObject prefab)
    {
        // Instantiate prefab far from origin to avoid interference
        Vector3 spawnPos = new Vector3(5000f, 0f, 5000f);
        GameObject instance = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        instance.hideFlags = HideFlags.HideAndDontSave;

        // Ensure all renderers are on a dedicated layer-independent rendering
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0; // Default layer

        // Compute bounds of all renderers
        Bounds bounds = new Bounds(spawnPos, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
        {
            Object.DestroyImmediate(instance);
            return null;
        }

        // Create temporary camera
        GameObject camGo = new GameObject("BillboardBakeCamera");
        camGo.hideFlags = HideFlags.HideAndDontSave;
        Camera cam = camGo.AddComponent<Camera>();

        // Add URP camera data for proper pipeline rendering
        var urpCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpCamData.renderShadows = false;
        urpCamData.renderPostProcessing = false;

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = ~0;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = bounds.size.magnitude * 4f;
        cam.enabled = false; // We render manually

        // Frame the tree — use the widest dimension (X or Z) for width
        float height = bounds.size.y;
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float maxDim = Mathf.Max(height, width) * 1.15f; // small padding
        cam.orthographicSize = maxDim * 0.5f;

        // Position camera at a slight horizontal angle (20 degrees) for a more
        // natural silhouette that shows some depth/volume of the tree
        Vector3 center = bounds.center;
        float camDist = bounds.size.magnitude * 2f;
        float angleRad = 20f * Mathf.Deg2Rad;
        Vector3 camOffset = new Vector3(
            Mathf.Sin(angleRad) * camDist,
            0f,
            Mathf.Cos(angleRad) * camDist
        );
        cam.transform.position = center + camOffset;
        cam.transform.LookAt(center, Vector3.up);

        // Create render texture
        RenderTexture rt = new RenderTexture(TexSize, TexSize, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        // Add temporary directional light for consistent lighting
        GameObject lightGo = new GameObject("BillboardBakeLight");
        lightGo.hideFlags = HideFlags.HideAndDontSave;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // Render
        cam.Render();

        // Read pixels
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, TexSize, TexSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        // Cleanup
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        Object.DestroyImmediate(instance);

        return tex;
    }
}
