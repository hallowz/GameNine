using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Voidborne.Building;

namespace Voidborne.Editor
{
    /// <summary>
    /// Voidborne > Setup Building System
    /// Creates BuildingPieceData assets, adds BuildingManager to the scene,
    /// and adds BuildingItemHandler to the Player.
    /// </summary>
    public static class BuildingSetup
    {
        private const string DataDir = "Assets/ScriptableObjects/Building";

        private struct PieceEntry
        {
            public string name;
            public BuildingPieceType pieceType;
            public bool canPlaceOnTerrain;
            public float[] healthPerTier;
        }

        private static readonly PieceEntry[] Pieces =
        {
            new PieceEntry { name = "Foundation_Wood",    pieceType = BuildingPieceType.Foundation,    canPlaceOnTerrain = true,  healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "TriFoundation_Wood", pieceType = BuildingPieceType.TriFoundation, canPlaceOnTerrain = true,  healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "Wall_Wood",          pieceType = BuildingPieceType.Wall,          canPlaceOnTerrain = false, healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "Doorway_Wood",       pieceType = BuildingPieceType.Doorway,       canPlaceOnTerrain = false, healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "Window_Wood",        pieceType = BuildingPieceType.Window,        canPlaceOnTerrain = false, healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "Floor_Wood",         pieceType = BuildingPieceType.Floor,         canPlaceOnTerrain = false, healthPerTier = new[] { 100f, 300f, 600f } },
            new PieceEntry { name = "TriFloor_Wood",      pieceType = BuildingPieceType.TriFloor,      canPlaceOnTerrain = false, healthPerTier = new[] { 100f, 300f, 600f } },
            new PieceEntry { name = "Stairs_Wood",        pieceType = BuildingPieceType.Stairs,        canPlaceOnTerrain = false, healthPerTier = new[] { 150f, 400f, 800f } },
            new PieceEntry { name = "Pillar_Wood",        pieceType = BuildingPieceType.Pillar,        canPlaceOnTerrain = false, healthPerTier = new[] { 200f, 500f, 1000f } },
            new PieceEntry { name = "HalfWall_Wood",      pieceType = BuildingPieceType.HalfWall,      canPlaceOnTerrain = false, healthPerTier = new[] { 100f, 250f, 500f } },
        };

        [MenuItem("Voidborne/Setup Building System")]
        public static void Setup()
        {
            EnsureDirectory(DataDir);
            CreatePieceData();
            EnsureBuildingManagerInScene();
            EnsurePlayerComponents();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildingSetup] Building system setup complete.");
        }

        private static void CreatePieceData()
        {
            foreach (var entry in Pieces)
            {
                string path = $"{DataDir}/{entry.name}.asset";
                BuildingPieceData data = AssetDatabase.LoadAssetAtPath<BuildingPieceData>(path);

                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<BuildingPieceData>();
                    AssetDatabase.CreateAsset(data, path);
                    Debug.Log($"[BuildingSetup] Created {path}");
                }

                data.pieceType = entry.pieceType;
                data.canPlaceOnTerrain = entry.canPlaceOnTerrain;
                data.healthPerTier = entry.healthPerTier;
                EditorUtility.SetDirty(data);
            }
        }

        private static void EnsureBuildingManagerInScene()
        {
            if (Object.FindObjectOfType<BuildingManager>() != null)
            {
                Debug.Log("[BuildingSetup] BuildingManager already in scene.");
                return;
            }

            var go = new GameObject("BuildingManager");
            go.AddComponent<BuildingManager>();
            Debug.Log("[BuildingSetup] BuildingManager added to scene.");
        }

        private static void EnsurePlayerComponents()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[BuildingSetup] No 'Player' GameObject found — skipping BuildingItemHandler setup.");
                return;
            }

            BuildingItemHandler bih = player.GetComponent<BuildingItemHandler>();
            if (bih == null)
            {
                bih = player.AddComponent<BuildingItemHandler>();
                Debug.Log("[BuildingSetup] Added BuildingItemHandler to Player.");
            }

            SerializedObject so = new SerializedObject(bih);
            so.FindProperty("playerInventory").objectReferenceValue =
                player.GetComponent<PlayerInventory>() ?? Object.FindObjectOfType<PlayerInventory>();
            so.ApplyModifiedProperties();

            // Wire BuildingManager camera
            var mgr = Object.FindObjectOfType<BuildingManager>();
            if (mgr != null && mgr.playerCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Transform camT = player.transform.Find("PlayerCamera");
                    if (camT != null) cam = camT.GetComponent<Camera>();
                }
                mgr.playerCamera = cam;
                EditorUtility.SetDirty(mgr);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[BuildingSetup] Player building components wired.");
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
