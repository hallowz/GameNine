using UnityEditor;
using UnityEngine;
using Voidborne.Building;

namespace Voidborne.Editor
{
    /// <summary>
    /// Voidborne > Setup Building Prefabs
    /// Creates building piece prefabs with exact socket positions per the master spec.
    /// All pieces use 3m module. Socket positions are on child GameObjects with SnapSocket.
    /// Run AFTER "Voidborne > Setup Building System".
    /// </summary>
    public static class BuildingPrefabSetup
    {
        private const string PrefabDir   = "Assets/Prefabs/Building/Pieces";
        private const string GhostDir    = "Assets/Prefabs/Building/Ghosts";
        private const string DataDir     = "Assets/ScriptableObjects/Building";
        private const string MaterialDir = "Assets/Materials/Building";
        private const string MeshDir     = "Assets/Meshes/Building";

        private static readonly Color WoodColour  = new Color(0.76f, 0.58f, 0.38f);
        private static readonly Color FrameColour = new Color(0.60f, 0.44f, 0.28f);
        private static readonly Color GlassColour = new Color(0.55f, 0.80f, 0.95f);

        private static Material _matWood;
        private static Material _matFrame;
        private static Material _matGlass;

        [MenuItem("Voidborne/Setup Building Prefabs")]
        public static void Setup()
        {
            EnsureDirectory(PrefabDir);
            EnsureDirectory(GhostDir);
            EnsureDirectory(MaterialDir);
            EnsureDirectory(MeshDir);

            _matWood  = EnsureMaterial("Wood_Building",  WoodColour);
            _matFrame = EnsureMaterial("Frame_Building", FrameColour);
            _matGlass = EnsureMaterial("Glass_Building", GlassColour);
            AssetDatabase.SaveAssets();

            // Build each piece type
            BuildAndSave("Foundation_Wood",    BuildFoundation());
            BuildAndSave("TriFoundation_Wood", BuildTriFoundation());
            BuildAndSave("Wall_Wood",          BuildWall());
            BuildAndSave("Doorway_Wood",       BuildDoorway());
            BuildAndSave("Window_Wood",        BuildWindow());
            BuildAndSave("Floor_Wood",         BuildFloor());
            BuildAndSave("TriFloor_Wood",      BuildTriFloor());
            BuildAndSave("Stairs_Wood",        BuildStairs());
            BuildAndSave("Pillar_Wood",        BuildPillar());
            BuildAndSave("HalfWall_Wood",      BuildHalfWall());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildingPrefabSetup] All building prefabs created with socket children.");
        }

        // ── Piece Builders ──────────────────────────────────────────────────────

        /// <summary>Foundation: 3.0 × 0.5 × 3.0 box. Pivot at bottom-center.</summary>
        static GameObject BuildFoundation()
        {
            var root = new GameObject();

            // Model child — mesh offset so pivot is at bottom-center
            var model = AddCube(root, new Vector3(0f, 0.25f, 0f), new Vector3(3f, 0.5f, 3f), _matWood);
            model.name = "Model";

            // Collider child
            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.25f, 0f);
            bc.size = new Vector3(3f, 0.5f, 3f);

            // Sockets (8 total) — all on top surface (y=0.5)
            AddSocket(root, "Edge_North", new Vector3(0f, 0.5f, 1.5f),   Quaternion.LookRotation(Vector3.forward),  SocketType.FoundationEdge);
            AddSocket(root, "Edge_South", new Vector3(0f, 0.5f, -1.5f),  Quaternion.LookRotation(Vector3.back),     SocketType.FoundationEdge);
            AddSocket(root, "Edge_East",  new Vector3(1.5f, 0.5f, 0f),   Quaternion.LookRotation(Vector3.right),    SocketType.FoundationEdge);
            AddSocket(root, "Edge_West",  new Vector3(-1.5f, 0.5f, 0f),  Quaternion.LookRotation(Vector3.left),     SocketType.FoundationEdge);

            AddSocket(root, "Corner_NE", new Vector3(1.5f, 0.5f, 1.5f),   Quaternion.LookRotation(new Vector3(1, 0, 1).normalized),  SocketType.FoundationCorner);
            AddSocket(root, "Corner_NW", new Vector3(-1.5f, 0.5f, 1.5f),  Quaternion.LookRotation(new Vector3(-1, 0, 1).normalized), SocketType.FoundationCorner);
            AddSocket(root, "Corner_SE", new Vector3(1.5f, 0.5f, -1.5f),  Quaternion.LookRotation(new Vector3(1, 0, -1).normalized), SocketType.FoundationCorner);
            AddSocket(root, "Corner_SW", new Vector3(-1.5f, 0.5f, -1.5f), Quaternion.LookRotation(new Vector3(-1, 0, -1).normalized),SocketType.FoundationCorner);

            // Vertical stacking sockets — top faces up, bottom faces down
            AddSocket(root, "Stack_Top",    new Vector3(0f, 0.5f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward),   SocketType.FoundationStack);
            AddSocket(root, "Stack_Bottom", new Vector3(0f, 0f, 0f),   Quaternion.LookRotation(Vector3.down, Vector3.forward), SocketType.FoundationStack);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Triangle Foundation: equilateral triangle prism, base=3m, h=0.5m. Pivot at centroid bottom.</summary>
        static GameObject BuildTriFoundation()
        {
            var root = new GameObject();

            // Procedural triangle prism mesh — saved as asset so it persists in prefab
            Mesh triMesh = SaveMeshAsset(CreateTrianglePrismMesh(0.5f), "TriFoundation_Mesh");

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            var mf = model.AddComponent<MeshFilter>();
            var mr = model.AddComponent<MeshRenderer>();
            mf.sharedMesh = triMesh;
            if (_matWood != null) mr.sharedMaterial = _matWood;

            // Collider — convex MeshCollider using the same mesh
            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = triMesh;
            mc.convex = true;

            // Sockets (6 total) — positions use the equilateral triangle constants
            AddSocket(root, "Edge_BC", new Vector3(0f, 0.5f, TriBaseZ),          Quaternion.LookRotation(Vector3.back),                       SocketType.FoundationEdge);
            AddSocket(root, "Edge_AB", new Vector3(-0.75f, 0.5f, 0.433f),       Quaternion.LookRotation(new Vector3(-0.866f, 0f, 0.5f)),      SocketType.FoundationEdge);
            AddSocket(root, "Edge_AC", new Vector3(0.75f, 0.5f, 0.433f),        Quaternion.LookRotation(new Vector3(0.866f, 0f, 0.5f)),       SocketType.FoundationEdge);

            AddSocket(root, "Corner_A", new Vector3(0f, 0.5f, TriApexZ),         Quaternion.LookRotation(Vector3.forward), SocketType.FoundationCorner);
            AddSocket(root, "Corner_B", new Vector3(-TriHalfBase, 0.5f, TriBaseZ),Quaternion.LookRotation(Vector3.left),    SocketType.FoundationCorner);
            AddSocket(root, "Corner_C", new Vector3(TriHalfBase, 0.5f, TriBaseZ), Quaternion.LookRotation(Vector3.right),   SocketType.FoundationCorner);

            // Vertical stacking
            AddSocket(root, "Stack_Top",    new Vector3(0f, 0.5f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward),   SocketType.FoundationStack);
            AddSocket(root, "Stack_Bottom", new Vector3(0f, 0f, 0f),   Quaternion.LookRotation(Vector3.down, Vector3.forward), SocketType.FoundationStack);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Wall: 3.0 × 3.0 × 0.1 box. Pivot at bottom-center.</summary>
        static GameObject BuildWall()
        {
            var root = new GameObject();

            var model = AddCube(root, new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 0.1f), _matWood);
            model.name = "Model";

            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(3f, 3f, 0.1f);

            // Sockets (4)
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 3f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallTop);
            AddSocket(root, "Socket_Left",   new Vector3(-1.5f, 1.5f, 0f), Quaternion.LookRotation(Vector3.left),  SocketType.WallSide);
            AddSocket(root, "Socket_Right",  new Vector3(1.5f, 1.5f, 0f),  Quaternion.LookRotation(Vector3.right), SocketType.WallSide);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Doorway: 3.0 × 3.0 × 0.1 frame with door hole (1.0w × 2.2h). Same sockets as Wall.</summary>
        static GameObject BuildDoorway()
        {
            var root = new GameObject();

            // Door opening: 1.2m wide × 2.5m tall, bottom flush with wall bottom.
            // Frame pieces (compound cubes approximating the doorway frame)
            // Top bar: full width, from y=2.5 to y=3.0
            AddCube(root, new Vector3(0f, 2.75f, 0f), new Vector3(3f, 0.5f, 0.1f), _matFrame).name = "TopBar";
            // Left post: from y=0 to y=2.5, width=(3-1.2)/2 = 0.9m, centered at x=-1.05
            AddCube(root, new Vector3(-1.05f, 1.25f, 0f), new Vector3(0.9f, 2.5f, 0.1f), _matFrame).name = "LeftPost";
            // Right post
            AddCube(root, new Vector3(1.05f, 1.25f, 0f), new Vector3(0.9f, 2.5f, 0.1f), _matFrame).name = "RightPost";

            // Colliders — top bar + posts so the opening is passable
            var colTop = new GameObject("Collider_Top");
            colTop.transform.SetParent(root.transform, false);
            var bcTop = colTop.AddComponent<BoxCollider>();
            bcTop.center = new Vector3(0f, 2.75f, 0f);
            bcTop.size = new Vector3(3f, 0.5f, 0.1f);

            var colLeft = new GameObject("Collider_Left");
            colLeft.transform.SetParent(root.transform, false);
            var bcLeft = colLeft.AddComponent<BoxCollider>();
            bcLeft.center = new Vector3(-1.05f, 1.25f, 0f);
            bcLeft.size = new Vector3(0.9f, 2.5f, 0.1f);

            var colRight = new GameObject("Collider_Right");
            colRight.transform.SetParent(root.transform, false);
            var bcRight = colRight.AddComponent<BoxCollider>();
            bcRight.center = new Vector3(1.05f, 1.25f, 0f);
            bcRight.size = new Vector3(0.9f, 2.5f, 0.1f);

            // Sockets — identical to Wall
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 3f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallTop);
            AddSocket(root, "Socket_Left",   new Vector3(-1.5f, 1.5f, 0f), Quaternion.LookRotation(Vector3.left),  SocketType.WallSide);
            AddSocket(root, "Socket_Right",  new Vector3(1.5f, 1.5f, 0f),  Quaternion.LookRotation(Vector3.right), SocketType.WallSide);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Window: 3.0 × 3.0 × 0.1 with window hole (1.4w × 1.0h at y=1.2-2.2). Same sockets as Wall.</summary>
        static GameObject BuildWindow()
        {
            var root = new GameObject();

            // Bottom panel
            AddCube(root, new Vector3(0f, 0.6f, 0f), new Vector3(3f, 1.2f, 0.1f), _matFrame).name = "BottomPanel";
            // Top panel
            AddCube(root, new Vector3(0f, 2.6f, 0f), new Vector3(3f, 0.8f, 0.1f), _matFrame).name = "TopPanel";
            // Left post
            AddCube(root, new Vector3(-1.2f, 1.7f, 0f), new Vector3(0.6f, 1f, 0.1f), _matFrame).name = "LeftPost";
            // Right post
            AddCube(root, new Vector3(1.2f, 1.7f, 0f), new Vector3(0.6f, 1f, 0.1f), _matFrame).name = "RightPost";
            // Glass pane
            AddCube(root, new Vector3(0f, 1.7f, 0f), new Vector3(1.4f, 1f, 0.04f), _matGlass).name = "Glass";

            var colObj = new GameObject("Collider");
            colObj.transform.SetParent(root.transform, false);
            var bc = colObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(3f, 3f, 0.1f);

            // Sockets — identical to Wall
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 3f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.WallTop);
            AddSocket(root, "Socket_Left",   new Vector3(-1.5f, 1.5f, 0f), Quaternion.LookRotation(Vector3.left),  SocketType.WallSide);
            AddSocket(root, "Socket_Right",  new Vector3(1.5f, 1.5f, 0f),  Quaternion.LookRotation(Vector3.right), SocketType.WallSide);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Floor: 3.0 × 0.1 × 3.0 slab. Pivot at bottom-center.</summary>
        static GameObject BuildFloor()
        {
            var root = new GameObject();

            var model = AddCube(root, new Vector3(0f, 0.05f, 0f), new Vector3(3f, 0.1f, 3f), _matWood);
            model.name = "Model";

            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.05f, 0f);
            bc.size = new Vector3(3f, 0.1f, 3f);

            // Sockets (4)
            AddSocket(root, "Edge_North", new Vector3(0f, 0f, 1.5f),   Quaternion.LookRotation(Vector3.forward), SocketType.FloorEdge);
            AddSocket(root, "Edge_South", new Vector3(0f, 0f, -1.5f),  Quaternion.LookRotation(Vector3.back),    SocketType.FloorEdge);
            AddSocket(root, "Edge_East",  new Vector3(1.5f, 0f, 0f),   Quaternion.LookRotation(Vector3.right),   SocketType.FloorEdge);
            AddSocket(root, "Edge_West",  new Vector3(-1.5f, 0f, 0f),  Quaternion.LookRotation(Vector3.left),    SocketType.FloorEdge);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Triangle Floor: same triangle footprint as TriFoundation but 0.1m thick.</summary>
        static GameObject BuildTriFloor()
        {
            var root = new GameObject();

            // Procedural triangle prism mesh — saved as asset so it persists in prefab
            Mesh triMesh = SaveMeshAsset(CreateTrianglePrismMesh(0.1f), "TriFloor_Mesh");

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            var mf = model.AddComponent<MeshFilter>();
            var mr = model.AddComponent<MeshRenderer>();
            mf.sharedMesh = triMesh;
            if (_matWood != null) mr.sharedMaterial = _matWood;

            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = triMesh;
            mc.convex = true;

            // Sockets (3) — positions match equilateral triangle constants
            AddSocket(root, "Edge_BC", new Vector3(0f, 0f, TriBaseZ),    Quaternion.LookRotation(Vector3.back),                    SocketType.FloorEdge);
            AddSocket(root, "Edge_AB", new Vector3(-0.75f, 0f, 0.433f), Quaternion.LookRotation(new Vector3(-0.866f, 0f, 0.5f)),   SocketType.FloorEdge);
            AddSocket(root, "Edge_AC", new Vector3(0.75f, 0f, 0.433f),  Quaternion.LookRotation(new Vector3(0.866f, 0f, 0.5f)),    SocketType.FloorEdge);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Stairs: 3.0 × 3.0 × 3.0 bounding box ramp. Pivot at bottom-center of lower edge.</summary>
        static GameObject BuildStairs()
        {
            var root = new GameObject();

            // 15 steps over 3m height / 3m depth — each step is 0.2m tall, 0.2m deep.
            // Width = 2.6m (narrower than 3m foundation edge so walls fit alongside).
            int steps = 15;
            float stairWidth = 2.6f;
            float stepH = 3f / steps;  // 0.2m
            float stepD = 3f / steps;  // 0.2m

            for (int i = 0; i < steps; i++)
            {
                float treadY = (i + 1) * stepH;
                float z = i * stepD + stepD * 0.5f;

                // Visual tread (thin visible step)
                AddCube(root, new Vector3(0f, treadY - stepH * 0.5f, z),
                    new Vector3(stairWidth, stepH, stepD), _matWood).name = $"Step_{i}";
            }

            // Per-step box colliders so the character controller can walk up
            var colParent = new GameObject("Colliders");
            colParent.transform.SetParent(root.transform, false);
            for (int i = 0; i < steps; i++)
            {
                float treadY = (i + 1) * stepH;
                float z = i * stepD + stepD * 0.5f;
                var stepCol = new GameObject($"StepCol_{i}");
                stepCol.transform.SetParent(colParent.transform, false);
                var bc = stepCol.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, treadY * 0.5f, z);
                bc.size = new Vector3(stairWidth, treadY, stepD);
            }

            // Sockets (2)
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),  Quaternion.LookRotation(Vector3.back),    SocketType.WallBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 3f, 3f),  Quaternion.LookRotation(Vector3.forward), SocketType.FloorEdge);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Pillar: 0.2 × 3.0 × 0.2 column. Pivot at bottom-center.</summary>
        static GameObject BuildPillar()
        {
            var root = new GameObject();

            var model = AddCube(root, new Vector3(0f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f), _matWood);
            model.name = "Model";

            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(0.2f, 3f, 0.2f);

            // Sockets (2)
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),  Quaternion.LookRotation(Vector3.down),  SocketType.PillarBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 3f, 0f),  Quaternion.LookRotation(Vector3.up),    SocketType.PillarTop);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        /// <summary>Half Wall: 3.0 × 1.5 × 0.1. Pivot at bottom-center.</summary>
        static GameObject BuildHalfWall()
        {
            var root = new GameObject();

            var model = AddCube(root, new Vector3(0f, 0.75f, 0f), new Vector3(3f, 1.5f, 0.1f), _matWood);
            model.name = "Model";

            var col = new GameObject("Collider");
            col.transform.SetParent(root.transform, false);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.75f, 0f);
            bc.size = new Vector3(3f, 1.5f, 0.1f);

            // Sockets (4) — bottom, top, and sides
            AddSocket(root, "Socket_Bottom", new Vector3(0f, 0f, 0f),      Quaternion.LookRotation(Vector3.forward), SocketType.WallBottom);
            AddSocket(root, "Socket_Top",    new Vector3(0f, 1.5f, 0f),    Quaternion.LookRotation(Vector3.forward), SocketType.HalfWallTop);
            AddSocket(root, "Socket_Left",   new Vector3(-1.5f, 0.75f, 0f), Quaternion.LookRotation(Vector3.left),  SocketType.WallSide);
            AddSocket(root, "Socket_Right",  new Vector3(1.5f, 0.75f, 0f),  Quaternion.LookRotation(Vector3.right), SocketType.WallSide);

            root.AddComponent<BuildingPiece>();
            return root;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        static void AddSocket(GameObject parent, string name, Vector3 localPos, Quaternion localRot, SocketType type)
        {
            var socketObj = new GameObject(name);
            socketObj.transform.SetParent(parent.transform, false);
            socketObj.transform.localPosition = localPos;
            socketObj.transform.localRotation = localRot;

            var socket = socketObj.AddComponent<SnapSocket>();
            socket.socketType = type;
        }

        static void BuildAndSave(string pieceName, GameObject go)
        {
            go.name = pieceName;

            // Save as piece prefab
            string piecePrefabPath = $"{PrefabDir}/{pieceName}.prefab";
            bool ok;
            GameObject savedPiece = PrefabUtility.SaveAsPrefabAsset(go, piecePrefabPath, out ok);

            if (!ok)
            {
                Debug.LogError($"[BuildingPrefabSetup] Failed to save piece prefab {piecePrefabPath}");
                Object.DestroyImmediate(go);
                return;
            }

            // Create ghost variant (same geometry, no BuildingPiece, add GhostPiece)
            go.name = "Ghost_" + pieceName.Replace("_Wood", "").Replace("_Stone", "").Replace("_Iron", "");
            var bp = go.GetComponent<BuildingPiece>();
            if (bp != null) Object.DestroyImmediate(bp);
            go.AddComponent<GhostPiece>();

            // Set colliders to trigger on ghost
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            string ghostName = go.name;
            string ghostPrefabPath = $"{GhostDir}/{ghostName}.prefab";
            bool ghostOk;
            GameObject savedGhost = PrefabUtility.SaveAsPrefabAsset(go, ghostPrefabPath, out ghostOk);
            Object.DestroyImmediate(go);

            if (!ghostOk)
            {
                Debug.LogWarning($"[BuildingPrefabSetup] Failed to save ghost prefab {ghostPrefabPath}");
            }

            // Assign to BuildingPieceData
            string dataPath = $"{DataDir}/{pieceName}.asset";
            BuildingPieceData data = AssetDatabase.LoadAssetAtPath<BuildingPieceData>(dataPath);
            if (data != null)
            {
                data.prefab = savedPiece;
                if (ghostOk) data.ghostPrefab = savedGhost;
                EditorUtility.SetDirty(data);
                Debug.Log($"[BuildingPrefabSetup] {pieceName} — piece + ghost prefabs saved and assigned.");
            }
            else
            {
                Debug.LogWarning($"[BuildingPrefabSetup] No BuildingPieceData at {dataPath} — run 'Setup Building System' first.");
            }
        }

        /// <summary>
        /// Creates an equilateral triangle prism mesh with centroid-bottom pivot.
        /// Vertices: A=(0,0,1.155), B=(-1.5,0,-0.577), C=(1.5,0,-0.577).
        /// </summary>
        // ── Triangle geometry constants ──────────────────────────────────────
        // Equilateral triangle, ALL edges = 3m (matches square foundation edges).
        // Any edge snaps flush to any square foundation edge.
        // Height = √3/2 × 3 = 2.598m.  Pivot at centroid bottom.
        //
        // Vertices relative to centroid:
        //   A (apex, north)  = ( 0,    0, +1.732)   — √3 above centroid
        //   B (bottom-left)  = (-1.5,  0, -0.866)   — √3/2 below centroid
        //   C (bottom-right) = (+1.5,  0, -0.866)
        //
        // Verify: AB = √(1.5² + 2.598²) = √(2.25 + 6.75) = √9 = 3 ✓
        //
        // Edge midpoints (for sockets):
        //   BC mid = ( 0,    0, -0.866)
        //   AB mid = (-0.75, 0,  0.433)
        //   AC mid = ( 0.75, 0,  0.433)
        //
        // Edge outward normals (⊥ to edge, away from opposite vertex):
        //   BC: ( 0,     0, -1)
        //   AB: (-0.866, 0,  0.5)
        //   AC: ( 0.866, 0,  0.5)

        private const float TriApexZ    =  1.732051f; // √3           (2/3 of height)
        private const float TriBaseZ    = -0.866025f; // -√3/2        (1/3 of height)
        private const float TriHalfBase =  1.5f;      // half of 3m side

        static Mesh CreateTrianglePrismMesh(float height)
        {
            Vector3 a0 = new Vector3(0f,           0f, TriApexZ);
            Vector3 b0 = new Vector3(-TriHalfBase, 0f, TriBaseZ);
            Vector3 c0 = new Vector3( TriHalfBase, 0f, TriBaseZ);
            Vector3 a1 = a0 + Vector3.up * height;
            Vector3 b1 = b0 + Vector3.up * height;
            Vector3 c1 = c0 + Vector3.up * height;

            // Outward-facing side normals: Cross(Up, edge) points outward
            Vector3 nBC = Vector3.Cross(Vector3.up, c0 - b0).normalized;
            Vector3 nCA = Vector3.Cross(Vector3.up, a0 - c0).normalized;
            Vector3 nAB = Vector3.Cross(Vector3.up, b0 - a0).normalized;

            // 18 vertices (separate per face for flat shading)
            var verts = new Vector3[]
            {
                // Bottom face (0-2) — normal down
                c0, a0, b0,
                // Top face (3-5) — normal up
                a1, c1, b1,
                // Side BC (6-9)
                c0, b0, b1, c1,
                // Side CA (10-13)
                a0, c0, c1, a1,
                // Side AB (14-17)
                b0, a0, a1, b1,
            };

            var tris = new int[]
            {
                0, 1, 2,                           // Bottom
                3, 4, 5,                           // Top
                6, 7, 8,   6, 8, 9,               // Side BC
                10, 11, 12, 10, 12, 13,            // Side CA
                14, 15, 16, 14, 16, 17,            // Side AB
            };

            var normals = new Vector3[]
            {
                Vector3.down, Vector3.down, Vector3.down,
                Vector3.up,   Vector3.up,   Vector3.up,
                nBC, nBC, nBC, nBC,
                nCA, nCA, nCA, nCA,
                nAB, nAB, nAB, nAB,
            };

            var mesh = new Mesh { name = "TrianglePrism" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Saves a procedural mesh as a .asset file so it persists in prefabs.</summary>
        static Mesh SaveMeshAsset(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Update existing asset in place
                existing.Clear();
                existing.vertices  = mesh.vertices;
                existing.triangles = mesh.triangles;
                existing.normals   = mesh.normals;
                existing.uv        = mesh.uv;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject AddCube(GameObject parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            cube.transform.localRotation = Quaternion.identity;
            var r = cube.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return cube;
        }

        static Material EnsureMaterial(string matName, Color colour)
        {
            string path = $"{MaterialDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard")
                             ?? Shader.Find("Diffuse");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureDirectory(string path)
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
