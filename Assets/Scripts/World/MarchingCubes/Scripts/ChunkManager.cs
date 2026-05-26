/*
    Author: Gus Tahara-Edmonds (original)
    Modified for Unity 6.3 compatibility
    Purpose: Decides the chunks to be generated. Capable of just making a grid or an infinite world.
    Calls the marching cubes compute shader to actually create the mesh.
*/

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Voidborne.World.MarchingCubes
{
    public class ChunkManager : MonoBehaviour
    {
        [Header("Size")]
        public int voxelsPerAxis;
        public float actualDimension;
        [Header("Shape")]
        public float surfaceLevel;
        public bool smooth;
        public bool invert;
        [Header("View")]
        public bool infiniteWorld;
        public int viewDistance;
        public Vector3 fixedChunkDimensions;
        [Header("LOD")]
        public LODProfile[] lodProfiles;
        [Header("Misc")]
        public bool useCollisions;

        DensityBehaviour db;
        ComputeShader marchCompute;

        RenderTexture densityData;
        ComputeBuffer triangleBuffer;
        ComputeBuffer triCountBuffer;

        int pointsPerAxis;
        int threadsPerAxis;
        int sqrViewDis;
        bool generateOnUpdate;
        new Camera camera;

        List<ChunkInstance> chunks;
        List<Vector3> chunkCoords;
        Queue<ChunkInstance> chunksToRecycle = new Queue<ChunkInstance>();

        void Start()
        {
            if (Mathf.Round(voxelsPerAxis / 8f) != voxelsPerAxis / 8f)
            {
                Debug.LogError("Voxels Per Axis must be divisible by 8");
            }

            if (infiniteWorld)
            {
                generateOnUpdate = true;
            }

            pointsPerAxis = voxelsPerAxis + 1;
            threadsPerAxis = pointsPerAxis / 8;
            sqrViewDis = viewDistance * viewDistance;
            camera = Camera.main;
            chunks = new List<ChunkInstance>();
            chunkCoords = new List<Vector3>();

            marchCompute = Resources.Load<ComputeShader>("MarchingCubes");

            InitDensityBehaviour();
            InitMarchingCubesCompute();

            if (infiniteWorld)
            {
                GenerateVisibleChunks();
            }
            else
            {
                GenerateFixedChunks();
            }
        }

        void Update()
        {
            pointsPerAxis = voxelsPerAxis + 1;

            // Debug keys disabled — use new Input System for runtime control
            // Legacy Input.GetKeyDown calls removed to prevent exceptions

            if (generateOnUpdate)
            {
                GenerateVisibleChunks();
            }
        }

        #region Chunk generators
        void GenerateVisibleChunks()
        {
            Vector3 p = camera.transform.position;
            Vector3 pCoord = WorldPos2Coord(p);

            foreach (ChunkInstance c in chunks)
            {
                float sqrDis = Vector3.SqrMagnitude(ChunkPos2Centre(c.coord) - p);
                if (sqrDis > sqrViewDis || GetLODProfile(sqrDis) != c.lodIndex)
                {
                    chunksToRecycle.Enqueue(c);
                    chunkCoords.Remove(c.coord);
                }
            }

            int maxChunksInView = Mathf.CeilToInt(viewDistance / (float)actualDimension);
            for (int x = -maxChunksInView; x <= maxChunksInView; x++)
            {
                for (int y = -maxChunksInView; y <= maxChunksInView; y++)
                {
                    for (int z = -maxChunksInView; z <= maxChunksInView; z++)
                    {
                        Vector3 worldPos = pCoord + new Vector3(x, y, z) * actualDimension;

                        if (chunkCoords.Contains(worldPos))
                        {
                            continue;
                        }

                        Vector3 centre = ChunkPos2Centre(worldPos);
                        float sqrDis = Vector3.SqrMagnitude(centre - p);
                        if (sqrDis <= sqrViewDis)
                        {
                            Bounds bounds = new Bounds(centre, Vector3.one * actualDimension);
                            if (IsVisible(bounds))
                            {
                                db.Generate(threadsPerAxis + 1, worldPos);
                                int lodProfile = GetLODProfile(sqrDis);

                                if (chunksToRecycle.Count > 0)
                                {
                                    ChunkInstance c = chunksToRecycle.Dequeue();
                                    c.name = GetName(worldPos);
                                    c.transform.position = transform.position + worldPos;
                                    c.coord = worldPos;
                                    c.lodIndex = lodProfile;
                                    GenerateChunkMesh(c);
                                    chunkCoords.Add(worldPos);
                                }
                                else
                                {
                                    ChunkInstance c = CreateNewChunk(worldPos, lodProfile);
                                    GenerateChunkMesh(c);
                                    chunks.Add(c);
                                    chunkCoords.Add(worldPos);
                                }
                            }
                        }
                    }
                }
            }

            while (chunksToRecycle.Count > 0)
            {
                ChunkInstance c = chunksToRecycle.Dequeue();
                chunks.Remove(c);
                Destroy(c.gameObject);
            }
        }

        void GenerateFixedChunks()
        {
            for (int x = 0; x < fixedChunkDimensions.x; x++)
            {
                for (int y = 0; y < fixedChunkDimensions.y; y++)
                {
                    for (int z = 0; z < fixedChunkDimensions.z; z++)
                    {
                        Vector3 worldPos = new Vector3(x, y, z) * actualDimension;
                        db.Generate(threadsPerAxis + 1, worldPos);
                        ChunkInstance c = CreateNewChunk(worldPos, 0);
                        GenerateChunkMesh(c);
                    }
                }
            }
        }
        #endregion

        #region Chunk gen helper functions
        ChunkInstance CreateNewChunk(Vector3 worldPos, int lodProfile)
        {
            GameObject newChunk = new GameObject(GetName(worldPos));
            newChunk.transform.parent = transform;
            newChunk.transform.position = worldPos + transform.position;
            ChunkInstance c = newChunk.AddComponent<ChunkInstance>();
            c.Setup(worldPos, lodProfile, useCollisions);
            return c;
        }

        Vector3 ChunkPos2Centre(Vector3 coord)
        {
            return coord + Vector3.one * actualDimension / 2;
        }

        Vector3 WorldPos2Coord(Vector3 worldPos)
        {
            return actualDimension * new Vector3(
                Mathf.Round(worldPos.x / actualDimension),
                Mathf.Round(worldPos.y / actualDimension),
                Mathf.Round(worldPos.z / actualDimension));
        }

        string GetName(Vector3 pos)
        {
            return "Chunk at (" + pos.x + ", " + pos.y + ", " + pos.z + ")";
        }

        public bool IsVisible(Bounds bounds)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        int GetLODProfile(float sqrDis)
        {
            for (int i = 0; i < lodProfiles.Length; i++)
            {
                float lodDis = lodProfiles[i].maxActiveDistance;
                if (sqrDis < lodDis * lodDis)
                {
                    return i;
                }
            }

            return lodProfiles.Length - 1;
        }
        #endregion

        #region Compute shader init
        void InitDensityBehaviour()
        {
            densityData = new RenderTexture(pointsPerAxis, pointsPerAxis, 0)
            {
                format = RenderTextureFormat.RFloat,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = pointsPerAxis,
                enableRandomWrite = true
            };
            densityData.Create();

            db = GetComponent<DensityBehaviour>();
            db.Init(densityData, pointsPerAxis, actualDimension / voxelsPerAxis);
        }

        void InitMarchingCubesCompute()
        {
            int maxNumTriangles = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis * 5;
            triangleBuffer = new ComputeBuffer(maxNumTriangles, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            marchCompute.SetInt("pointsPerAxis", pointsPerAxis);
            marchCompute.SetFloat("scale", actualDimension / voxelsPerAxis);
            UpdateMarchingCubesParams();
        }

        void UpdateMarchingCubesParams()
        {
            marchCompute.SetFloat("surfaceLevel", surfaceLevel);
            marchCompute.SetBool("smooth", smooth);
            marchCompute.SetBool("invert", invert);
            marchCompute.SetTexture(0, "densityData", densityData);
            marchCompute.SetBuffer(0, "triangles", triangleBuffer);
            marchCompute.SetInt("lodModifier", 1);
        }
        #endregion

        #region Single chunk updaters
        void GenerateChunkMesh(ChunkInstance c)
        {
            int lodModifier = lodProfiles[c.lodIndex].lodModifier;
            c.SetMat(lodProfiles[c.lodIndex].mat);
            marchCompute.SetInt("lodModifier", lodModifier);

            triangleBuffer.SetCounterValue(0);
            int threads = Mathf.Max(threadsPerAxis / lodModifier, 1);
            marchCompute.Dispatch(0, threads, threads, threads);

            ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
            int[] triCountArray = { 0 };
            triCountBuffer.GetData(triCountArray);
            int numTris = triCountArray[0];

            Triangle[] triangles = new Triangle[numTris];
            triangleBuffer.GetData(triangles, 0, 0, numTris);

            Vector3[] vertices = new Vector3[numTris * 3];
            int[] meshTriangles = new int[numTris * 3];
            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    meshTriangles[i * 3 + j] = i * 3 + j;
                    vertices[i * 3 + j] = triangles[i][j];
                }
            }

            c.mesh.Clear();
            c.mesh.vertices = vertices;
            c.mesh.triangles = meshTriangles;
            c.mesh.RecalculateNormals();

            if (useCollisions)
            {
                c.collider.enabled = false;
                c.collider.enabled = true;
            }
        }
        #endregion

        #region Buffer management
        void OnDestroy()
        {
            if (Application.isPlaying)
            {
                ReleaseBuffers();
            }
        }

        void ReleaseBuffers()
        {
            if (triangleBuffer != null)
            {
                densityData.Release();
                triangleBuffer.Release();
                triCountBuffer.Release();
            }
        }
        #endregion

        #region structs/classes
        [System.Serializable]
        public class LODProfile
        {
            public float maxActiveDistance;
            public int lodModifier;
            public Material mat;
        }

        struct Triangle
        {
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;

            public Vector3 this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0:
                            return a;
                        case 1:
                            return b;
                        default:
                            return c;
                    }
                }
            }
        }
        #endregion
    }
}
