using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Voidborne.Diagnostics;
using Voidborne.World.Biomes;
using Voidborne.World.Chunks;
using Voidborne.World.Generation;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// GPU-instanced grass renderer with compute-shader culling and vertex-shader blades.
    ///
    /// Architecture:
    ///   CPU: generates per-blade instance data on chunk load, sub-allocated in a persistent GPU buffer
    ///   GPU (compute): frustum + distance culling, sorts blades into 3 LOD append buffers
    ///   GPU (vertex): transforms pre-built blade mesh vertices using per-instance data
    ///   GPU (fragment): per-biome gradient color + URP lighting
    ///
    /// Buffer management: incremental sub-allocation.
    ///   Chunk activate  → partial SetData (only new chunk's blades)
    ///   Chunk deactivate → invalidate slot (zero scale), periodic compaction
    ///   No full-buffer rebuild on every chunk change.
    ///
    /// LOD bands:
    ///   LOD 0 (0–35 m)   — 5-segment curved blade (11 verts)
    ///   LOD 1 (35–65 m)  — 3-segment curved blade (7 verts)
    ///   LOD 2 (65–100 m) — 1-segment triangle blade (3 verts)
    ///   Beyond 100 m     — culled
    /// </summary>
    public class GrassRenderer : MonoBehaviour
    {
        public static GrassRenderer Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [SerializeField] private GrassWindSettings windSettings;
        [SerializeField] private Material grassMaterial;          // Shader: Voidborne/GrassInstanced
        [SerializeField] private ComputeShader cullingShader;     // GrassCulling.compute

        [Header("Density")]
        [SerializeField] private int bladesPerPoint = 40;
        [SerializeField] private float spreadRadius = 0.6f;

        [Header("LOD Distances")]
        [SerializeField] private float lod1Distance = 35f;
        [SerializeField] private float lod2Distance = 65f;
        [SerializeField] private float cullDistance  = 100f;

        // ── Per-biome grass appearance ───────────────────────────────────────
        [System.Serializable]
        public struct BiomeGrassConfig
        {
            public Color topColor;
            public Color bottomColor;
            [Range(0f, 1f)] public float windMultiplier;
        }

        [Header("Biome Grass (0=Plains, 1=Dense Forest, 2=River Valley, 3=Savanna, 4=Highlands, 5=Grand Hills, 6=Towering Bluffs)")]
        [SerializeField] private BiomeGrassConfig[] biomeGrass = new BiomeGrassConfig[]
        {
            new BiomeGrassConfig { topColor = new Color(0.18f, 0.45f, 0.06f, 1f), bottomColor = new Color(0.06f, 0.14f, 0.02f, 1f), windMultiplier = 1.0f },
            new BiomeGrassConfig { topColor = new Color(0.06f, 0.30f, 0.05f, 1f), bottomColor = new Color(0.02f, 0.08f, 0.01f, 1f), windMultiplier = 0.3f },
            new BiomeGrassConfig { topColor = new Color(0.12f, 0.40f, 0.08f, 1f), bottomColor = new Color(0.04f, 0.12f, 0.02f, 1f), windMultiplier = 0.7f },
            new BiomeGrassConfig { topColor = new Color(0.38f, 0.35f, 0.10f, 1f), bottomColor = new Color(0.14f, 0.10f, 0.03f, 1f), windMultiplier = 1.0f },
            new BiomeGrassConfig { topColor = new Color(0.22f, 0.32f, 0.12f, 1f), bottomColor = new Color(0.08f, 0.12f, 0.04f, 1f), windMultiplier = 0.8f },
            new BiomeGrassConfig { topColor = new Color(0.14f, 0.42f, 0.10f, 1f), bottomColor = new Color(0.05f, 0.14f, 0.03f, 1f), windMultiplier = 0.8f },
            new BiomeGrassConfig { topColor = new Color(0.32f, 0.30f, 0.14f, 1f), bottomColor = new Color(0.12f, 0.10f, 0.04f, 1f), windMultiplier = 1.0f },
        };

        // ── Instance data struct (must match HLSL layout exactly) ────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct GrassInstanceData
        {
            public Vector3 position;
            public Vector3 normal;
            public float   scale;
            public float   colorVariant;
        }
        private static readonly int Stride = Marshal.SizeOf<GrassInstanceData>();

        // ── Per-chunk blade storage ──────────────────────────────────────────
        private class GrassChunkData
        {
            public Vector3Int chunkPos;
            public GrassInstanceData[] blades;
        }

        private readonly Dictionary<Vector3Int, GrassChunkData> _grassChunks
            = new Dictionary<Vector3Int, GrassChunkData>();

        // ── Sub-allocated GPU buffer ─────────────────────────────────────────
        // Instead of rebuilding the entire buffer when any chunk changes, each
        // chunk owns a contiguous slot. Loads do a partial SetData; unloads
        // invalidate the slot (scale=0) and the compute shader skips them.
        // Periodic compaction reclaims gaps when fragmentation is high.

        private ComputeBuffer _allBladesBuffer;
        private int           _bufferCapacity;      // allocated size
        private int           _bufferHighWater;     // end of used region (includes dead slots)
        private int           _liveBladeCount;      // blades that are alive (for stats)
        private int           _deadBladeCount;      // invalidated blades (for compaction trigger)

        private readonly Dictionary<Vector3Int, (int offset, int count)> _slotMap
            = new Dictionary<Vector3Int, (int, int)>();

        // Reusable zero array for invalidating freed slots
        private GrassInstanceData[] _zeroArray;
        private const int ZERO_ARRAY_SIZE = 8192;

        // Compaction threshold: rebuild when >30% of buffer is dead blades
        private const float CompactionThreshold = 0.30f;

        // ── Append / draw buffers ────────────────────────────────────────────
        private ComputeBuffer _appendLod0, _appendLod1, _appendLod2;
        private ComputeBuffer _argLod0, _argLod1, _argLod2;
        private readonly uint[] _argsTemplate = new uint[5];

        private Material _matLod0, _matLod1, _matLod2;
        private MaterialPropertyBlock _mpbLod0, _mpbLod1, _mpbLod2;

        private int _appendCapacity;
        private Mesh _bladeMeshLod0, _bladeMeshLod1, _bladeMeshLod2;

        // Compute shader kernel + property IDs
        private int _csKernel;
        private static readonly int PropAllBlades     = Shader.PropertyToID("_AllBlades");
        private static readonly int PropLod0Out       = Shader.PropertyToID("_Lod0Out");
        private static readonly int PropLod1Out       = Shader.PropertyToID("_Lod1Out");
        private static readonly int PropLod2Out       = Shader.PropertyToID("_Lod2Out");
        private static readonly int PropCamPos        = Shader.PropertyToID("_CamPos");
        private static readonly int PropLod1DistSq    = Shader.PropertyToID("_Lod1DistSq");
        private static readonly int PropLod2DistSq    = Shader.PropertyToID("_Lod2DistSq");
        private static readonly int PropLod3DistSq    = Shader.PropertyToID("_Lod3DistSq");
        private static readonly int PropBladeCount    = Shader.PropertyToID("_BladeCount");
        private static readonly int PropFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int PropGrassBuffer   = Shader.PropertyToID("_GrassBuffer");
        private static readonly int PropWindFreq      = Shader.PropertyToID("_WindFrequency");
        private static readonly int PropWindStrength   = Shader.PropertyToID("_WindStrength");
        private static readonly int PropBiomeTop0     = Shader.PropertyToID("_BiomeTop0");
        private static readonly int PropBiomeTop1     = Shader.PropertyToID("_BiomeTop1");
        private static readonly int PropBiomeTop2     = Shader.PropertyToID("_BiomeTop2");
        private static readonly int PropBiomeTop3     = Shader.PropertyToID("_BiomeTop3");
        private static readonly int PropBiomeTop4     = Shader.PropertyToID("_BiomeTop4");
        private static readonly int PropBiomeTop5     = Shader.PropertyToID("_BiomeTop5");
        private static readonly int PropBiomeTop6     = Shader.PropertyToID("_BiomeTop6");
        private static readonly int PropBiomeBot0     = Shader.PropertyToID("_BiomeBot0");
        private static readonly int PropBiomeBot1     = Shader.PropertyToID("_BiomeBot1");
        private static readonly int PropBiomeBot2     = Shader.PropertyToID("_BiomeBot2");
        private static readonly int PropBiomeBot3     = Shader.PropertyToID("_BiomeBot3");
        private static readonly int PropBiomeBot4     = Shader.PropertyToID("_BiomeBot4");
        private static readonly int PropBiomeBot5     = Shader.PropertyToID("_BiomeBot5");
        private static readonly int PropBiomeBot6     = Shader.PropertyToID("_BiomeBot6");
        private static readonly int PropBiomeWindMult  = Shader.PropertyToID("_BiomeWindMult");
        private static readonly int PropBiomeWindMult2 = Shader.PropertyToID("_BiomeWindMult2");

        private readonly Plane[]   _frustumPlanesArr = new Plane[6];
        private readonly Vector4[] _frustumVec4      = new Vector4[6];

        private static readonly HashSet<int> GrassBiomes = new HashSet<int>
            { 4, 5, 6, 7, 10, 13, 14 };

        // Sky islands: grass is allowed regardless of surface biome when above this Y.
        // Uses biome index 0 (Grasslands green) for coloring.
        private const float SkyStartY = 100f;

        private Camera _cachedCam;

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("GrassRenderer.Update");

        private const int GRASS_BIOME_COUNT = 7;
        private readonly Vector4[] _biomeTopArr = new Vector4[GRASS_BIOME_COUNT];
        private readonly Vector4[] _biomeBotArr = new Vector4[GRASS_BIOME_COUNT];

        private static readonly BiomeGrassConfig[] ExtraBiomeDefaults = new BiomeGrassConfig[]
        {
            new BiomeGrassConfig { topColor = new Color(0.22f, 0.32f, 0.12f, 1f), bottomColor = new Color(0.08f, 0.12f, 0.04f, 1f), windMultiplier = 0.8f },
            new BiomeGrassConfig { topColor = new Color(0.14f, 0.42f, 0.10f, 1f), bottomColor = new Color(0.05f, 0.14f, 0.03f, 1f), windMultiplier = 0.8f },
            new BiomeGrassConfig { topColor = new Color(0.32f, 0.30f, 0.14f, 1f), bottomColor = new Color(0.12f, 0.10f, 0.04f, 1f), windMultiplier = 1.0f },
        };

        private void EnsureBiomeGrassArray()
        {
            if (biomeGrass != null && biomeGrass.Length >= GRASS_BIOME_COUNT) return;
            var expanded = new BiomeGrassConfig[GRASS_BIOME_COUNT];
            int existing = biomeGrass?.Length ?? 0;
            for (int i = 0; i < existing; i++) expanded[i] = biomeGrass[i];
            for (int i = existing; i < GRASS_BIOME_COUNT; i++)
            {
                int extraIdx = i - 4;
                expanded[i] = (extraIdx >= 0 && extraIdx < ExtraBiomeDefaults.Length)
                    ? ExtraBiomeDefaults[extraIdx] : ExtraBiomeDefaults[0];
            }
            biomeGrass = expanded;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            EnsureBiomeGrassArray();
            BuildBladeMeshes();
            CreateLODMaterials();

            _mpbLod0 = new MaterialPropertyBlock();
            _mpbLod1 = new MaterialPropertyBlock();
            _mpbLod2 = new MaterialPropertyBlock();

            if (cullingShader != null)
                _csKernel = cullingShader.FindKernel("CSMain");

            // Pre-allocate zero array for invalidating freed slots
            _zeroArray = new GrassInstanceData[ZERO_ARRAY_SIZE];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseGPUBuffers();
            if (_matLod0 != null) Destroy(_matLod0);
            if (_matLod1 != null) Destroy(_matLod1);
            if (_matLod2 != null) Destroy(_matLod2);
            if (_bladeMeshLod0 != null) Destroy(_bladeMeshLod0);
            if (_bladeMeshLod1 != null) Destroy(_bladeMeshLod1);
            if (_bladeMeshLod2 != null) Destroy(_bladeMeshLod2);
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            if (grassMaterial == null || cullingShader == null) { RuntimeProfiler.End(s_prof); return; }

            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) { RuntimeProfiler.End(s_prof); return; }

            if (_liveBladeCount == 0 && _bufferHighWater == 0) { RuntimeProfiler.End(s_prof); return; }

            UploadPerFrameData();
            GPUCullAndDraw();
            RuntimeProfiler.End(s_prof);
        }

        // ── Public API — called by WorldDecorationManager ───────────────────

        public void OnChunkActivated(Vector3Int chunkPos, SurfacePoint[] points)
        {
            if (_grassChunks.ContainsKey(chunkPos))
                OnChunkDeactivating(chunkPos);

            var blades = GenerateBlades(chunkPos, points);
            if (blades == null || blades.Length == 0) return;

            _grassChunks[chunkPos] = new GrassChunkData
            {
                chunkPos = chunkPos,
                blades   = blades
            };

            // Sub-allocate: append to end of GPU buffer
            AppendToBuffer(chunkPos, blades);
        }

        public void OnChunkDeactivating(Vector3Int chunkPos)
        {
            if (!_grassChunks.Remove(chunkPos)) return;

            // Invalidate the slot in the GPU buffer (set scale to 0)
            InvalidateSlot(chunkPos);

            // Compact if too fragmented
            if (_deadBladeCount > 0 && _bufferHighWater > 0
                && (float)_deadBladeCount / _bufferHighWater > CompactionThreshold)
            {
                CompactBuffer();
            }
        }

        public void OnChunksRebuilt(Vector3Int chunkPos, SurfacePoint[] newPoints)
        {
            OnChunkDeactivating(chunkPos);
            OnChunkActivated(chunkPos, newPoints);
        }

        // ── Sub-allocated buffer management ──────────────────────────────────

        private void EnsureBufferCapacity(int requiredTotal)
        {
            if (_allBladesBuffer != null && _bufferCapacity >= requiredTotal) return;

            int newCap = Mathf.Max(requiredTotal, 32768);
            // Over-allocate by 50% to reduce future resizes
            newCap = Mathf.Max(newCap, (int)(_bufferCapacity * 1.5f));

            var newBuffer = new ComputeBuffer(newCap, Stride);

            // Copy existing data from old buffer if any
            if (_allBladesBuffer != null && _bufferHighWater > 0)
            {
                // Must go through CPU — no GPU-to-GPU copy for ComputeBuffers
                if (_cpuMirror == null || _cpuMirror.Length < _bufferHighWater)
                    _cpuMirror = new GrassInstanceData[newCap];
                _allBladesBuffer.GetData(_cpuMirror, 0, 0, _bufferHighWater);
                newBuffer.SetData(_cpuMirror, 0, 0, _bufferHighWater);
            }

            _allBladesBuffer?.Release();
            _allBladesBuffer = newBuffer;
            _bufferCapacity = newCap;

            EnsureAppendBuffers(newCap);
        }

        // CPU mirror for buffer resize (only used during resize, not per-frame)
        private GrassInstanceData[] _cpuMirror;

        private void AppendToBuffer(Vector3Int chunkPos, GrassInstanceData[] blades)
        {
            int count = blades.Length;
            int needed = _bufferHighWater + count;
            EnsureBufferCapacity(needed);

            // Upload just this chunk's blades to the GPU buffer
            _allBladesBuffer.SetData(blades, 0, _bufferHighWater, count);
            _slotMap[chunkPos] = (_bufferHighWater, count);
            _bufferHighWater += count;
            _liveBladeCount += count;
        }

        private void InvalidateSlot(Vector3Int chunkPos)
        {
            if (!_slotMap.TryGetValue(chunkPos, out var slot)) return;
            _slotMap.Remove(chunkPos);

            int offset = slot.offset;
            int count = slot.count;

            // Upload zeroed blades (scale=0) so the compute shader skips them
            int remaining = count;
            int writePos = offset;
            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, ZERO_ARRAY_SIZE);
                _allBladesBuffer.SetData(_zeroArray, 0, writePos, batch);
                writePos += batch;
                remaining -= batch;
            }

            _liveBladeCount -= count;
            _deadBladeCount += count;

            // If the freed slot was at the very end, shrink the high water mark
            if (offset + count == _bufferHighWater)
            {
                _bufferHighWater = offset;
                _deadBladeCount -= count; // they're gone, not dead
            }
        }

        private void CompactBuffer()
        {
            // Full rebuild — same as the old approach, but happens rarely (<1x per minute)
            int total = 0;
            foreach (var kv in _grassChunks)
                total += kv.Value.blades.Length;

            if (total == 0)
            {
                _bufferHighWater = 0;
                _liveBladeCount = 0;
                _deadBladeCount = 0;
                _slotMap.Clear();
                return;
            }

            EnsureBufferCapacity(total);

            if (_cpuMirror == null || _cpuMirror.Length < total)
                _cpuMirror = new GrassInstanceData[Mathf.Max(total, 32768)];

            _slotMap.Clear();
            int writeOffset = 0;
            foreach (var kv in _grassChunks)
            {
                var blades = kv.Value.blades;
                System.Array.Copy(blades, 0, _cpuMirror, writeOffset, blades.Length);
                _slotMap[kv.Key] = (writeOffset, blades.Length);
                writeOffset += blades.Length;
            }

            _allBladesBuffer.SetData(_cpuMirror, 0, 0, total);
            _bufferHighWater = total;
            _liveBladeCount = total;
            _deadBladeCount = 0;
        }

        private void EnsureAppendBuffers(int capacity)
        {
            if (_appendCapacity >= capacity) return;

            _appendLod0?.Release();
            _appendLod1?.Release();
            _appendLod2?.Release();
            _argLod0?.Release();
            _argLod1?.Release();
            _argLod2?.Release();

            _appendCapacity = capacity;
            _appendLod0 = new ComputeBuffer(capacity, Stride, ComputeBufferType.Append);
            _appendLod1 = new ComputeBuffer(capacity, Stride, ComputeBufferType.Append);
            _appendLod2 = new ComputeBuffer(capacity, Stride, ComputeBufferType.Append);

            _argLod0 = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argLod1 = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argLod2 = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        // ── GPU culling + draw ──────────────────────────────────────────────

        private void GPUCullAndDraw()
        {
            Camera cam = _cachedCam;
            if (cam == null || _allBladesBuffer == null || _bufferHighWater == 0) return;

            Vector3 camPos = cam.transform.position;

            _appendLod0.SetCounterValue(0);
            _appendLod1.SetCounterValue(0);
            _appendLod2.SetCounterValue(0);

            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanesArr);
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = _frustumPlanesArr[i].normal;
                _frustumVec4[i] = new Vector4(n.x, n.y, n.z, _frustumPlanesArr[i].distance);
            }

            cullingShader.SetBuffer(_csKernel, PropAllBlades, _allBladesBuffer);
            cullingShader.SetBuffer(_csKernel, PropLod0Out, _appendLod0);
            cullingShader.SetBuffer(_csKernel, PropLod1Out, _appendLod1);
            cullingShader.SetBuffer(_csKernel, PropLod2Out, _appendLod2);
            cullingShader.SetVector(PropCamPos, camPos);
            cullingShader.SetFloat(PropLod1DistSq, lod1Distance * lod1Distance);
            cullingShader.SetFloat(PropLod2DistSq, lod2Distance * lod2Distance);
            cullingShader.SetFloat(PropLod3DistSq, cullDistance * cullDistance);
            cullingShader.SetInt(PropBladeCount, _bufferHighWater);
            cullingShader.SetVectorArray(PropFrustumPlanes, _frustumVec4);

            int groups = Mathf.CeilToInt(_bufferHighWater / 256f);
            cullingShader.Dispatch(_csKernel, groups, 1, 1);

            Bounds drawBounds = new Bounds(camPos, Vector3.one * (cullDistance * 2f));
            DrawLOD(_bladeMeshLod0, _matLod0, _appendLod0, _argLod0, _mpbLod0, drawBounds);
            DrawLOD(_bladeMeshLod1, _matLod1, _appendLod1, _argLod1, _mpbLod1, drawBounds);
            DrawLOD(_bladeMeshLod2, _matLod2, _appendLod2, _argLod2, _mpbLod2, drawBounds);
        }

        private void DrawLOD(
            Mesh mesh,
            Material mat,
            ComputeBuffer appendBuf,
            ComputeBuffer argsBuf,
            MaterialPropertyBlock mpb,
            Bounds drawBounds)
        {
            if (mesh == null || mat == null) return;

            _argsTemplate[0] = (uint)mesh.GetIndexCount(0);
            _argsTemplate[1] = 0;
            _argsTemplate[2] = (uint)mesh.GetIndexStart(0);
            _argsTemplate[3] = (uint)mesh.GetBaseVertex(0);
            _argsTemplate[4] = 0;
            argsBuf.SetData(_argsTemplate);

            ComputeBuffer.CopyCount(appendBuf, argsBuf, sizeof(uint));

            mpb.SetBuffer(PropGrassBuffer, appendBuf);

            Graphics.DrawMeshInstancedIndirect(
                mesh, 0, mat, drawBounds, argsBuf, 0, mpb);
        }

        // ── Per-frame data upload ───────────────────────────────────────────

        private void UploadPerFrameData()
        {
            for (int i = 0; i < GRASS_BIOME_COUNT; i++)
            {
                if (i < biomeGrass.Length)
                {
                    Color t = biomeGrass[i].topColor;
                    Color b = biomeGrass[i].bottomColor;
                    _biomeTopArr[i] = new Vector4(t.r, t.g, t.b, t.a);
                    _biomeBotArr[i] = new Vector4(b.r, b.g, b.b, b.a);
                }
            }

            Vector4 windMults = new Vector4(
                biomeGrass.Length > 0 ? biomeGrass[0].windMultiplier : 1f,
                biomeGrass.Length > 1 ? biomeGrass[1].windMultiplier : 1f,
                biomeGrass.Length > 2 ? biomeGrass[2].windMultiplier : 1f,
                biomeGrass.Length > 3 ? biomeGrass[3].windMultiplier : 1f);
            Vector4 windMults2 = new Vector4(
                biomeGrass.Length > 4 ? biomeGrass[4].windMultiplier : 1f,
                biomeGrass.Length > 5 ? biomeGrass[5].windMultiplier : 1f,
                biomeGrass.Length > 6 ? biomeGrass[6].windMultiplier : 1f, 0f);

            Vector4 windFreq = Vector4.zero;
            float windStr = 0.3f;
            if (windSettings != null)
            {
                Vector2 dir = windSettings.windDirection.sqrMagnitude > 0.001f
                    ? windSettings.windDirection.normalized : Vector2.right;
                windFreq = new Vector4(
                    dir.x * windSettings.windSpeed * 0.05f,
                    dir.y * windSettings.windSpeed * 0.05f, 0f, 0f);
                windStr = windSettings.windAmplitude * 2f;
            }

            UploadBiomeGlobals(windMults, windMults2);
            UploadToMaterial(_matLod0, windFreq, windStr);
            UploadToMaterial(_matLod1, windFreq, windStr);
            UploadToMaterial(_matLod2, windFreq, windStr);
        }

        private void UploadToMaterial(Material mat, Vector4 freq, float str)
        {
            if (mat == null) return;
            mat.SetVector(PropWindFreq, freq);
            mat.SetFloat(PropWindStrength, str);
        }

        private void UploadBiomeGlobals(Vector4 windMults, Vector4 windMults2)
        {
            Shader.SetGlobalVector(PropBiomeTop0, _biomeTopArr[0]);
            Shader.SetGlobalVector(PropBiomeTop1, _biomeTopArr[1]);
            Shader.SetGlobalVector(PropBiomeTop2, _biomeTopArr[2]);
            Shader.SetGlobalVector(PropBiomeTop3, _biomeTopArr[3]);
            Shader.SetGlobalVector(PropBiomeTop4, _biomeTopArr[4]);
            Shader.SetGlobalVector(PropBiomeTop5, _biomeTopArr[5]);
            Shader.SetGlobalVector(PropBiomeTop6, _biomeTopArr[6]);
            Shader.SetGlobalVector(PropBiomeBot0, _biomeBotArr[0]);
            Shader.SetGlobalVector(PropBiomeBot1, _biomeBotArr[1]);
            Shader.SetGlobalVector(PropBiomeBot2, _biomeBotArr[2]);
            Shader.SetGlobalVector(PropBiomeBot3, _biomeBotArr[3]);
            Shader.SetGlobalVector(PropBiomeBot4, _biomeBotArr[4]);
            Shader.SetGlobalVector(PropBiomeBot5, _biomeBotArr[5]);
            Shader.SetGlobalVector(PropBiomeBot6, _biomeBotArr[6]);
            Shader.SetGlobalVector(PropBiomeWindMult, windMults);
            Shader.SetGlobalVector(PropBiomeWindMult2, windMults2);
        }

        // ── Blade mesh construction (replaces geometry shader) ──────────────

        private void BuildBladeMeshes()
        {
            _bladeMeshLod0 = BuildBladeMesh("GrassBlade_LOD0", 5);
            _bladeMeshLod1 = BuildBladeMesh("GrassBlade_LOD1", 3);
            _bladeMeshLod2 = BuildBladeMesh("GrassBlade_LOD2", 1);
        }

        /// <summary>
        /// Builds a parametric blade strip mesh with the given segment count.
        /// Vertex positions encode: x = side factor (-1/+1), y = height fraction (0-1).
        /// The vertex shader transforms these into world space using per-instance data.
        /// </summary>
        private static Mesh BuildBladeMesh(string name, int segments)
        {
            int vertCount = segments * 2 + 1; // left+right per segment + tip
            var verts = new Vector3[vertCount];
            var uvs   = new Vector2[vertCount];

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                int li = i * 2;
                int ri = i * 2 + 1;
                verts[li] = new Vector3(-1f, t, 0f);
                verts[ri] = new Vector3( 1f, t, 0f);
                uvs[li]   = new Vector2(0f, t);
                uvs[ri]   = new Vector2(1f, t);
            }

            // Tip vertex
            int tipIdx = segments * 2;
            verts[tipIdx] = new Vector3(0f, 1f, 0f);
            uvs[tipIdx]   = new Vector2(0.5f, 1f);

            // Triangles: quads between segment pairs + tip triangle
            int triCount = (segments - 1) * 2 + 1; // (seg-1) quads of 2 tris + 1 tip tri
            var indices = new int[triCount * 3];
            int idx = 0;

            for (int i = 0; i < segments - 1; i++)
            {
                int bl = i * 2;
                int br = i * 2 + 1;
                int tl = (i + 1) * 2;
                int tr = (i + 1) * 2 + 1;

                indices[idx++] = bl; indices[idx++] = tl; indices[idx++] = br;
                indices[idx++] = br; indices[idx++] = tl; indices[idx++] = tr;
            }

            // Tip triangle from last segment
            int lastL = (segments - 1) * 2;
            int lastR = (segments - 1) * 2 + 1;
            indices[idx++] = lastL; indices[idx++] = tipIdx; indices[idx++] = lastR;

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return mesh;
        }

        private void CreateLODMaterials()
        {
            if (grassMaterial == null) return;

            _matLod0 = new Material(grassMaterial);
            _matLod0.name = "GrassInstanced_LOD0";

            _matLod1 = new Material(grassMaterial);
            _matLod1.name = "GrassInstanced_LOD1";
            _matLod1.EnableKeyword("_GRASS_LOD1");

            _matLod2 = new Material(grassMaterial);
            _matLod2.name = "GrassInstanced_LOD2";
            _matLod2.EnableKeyword("_GRASS_LOD2");
        }

        // ── GPU buffer management ───────────────────────────────────────────

        private void ReleaseGPUBuffers()
        {
            _allBladesBuffer?.Release(); _allBladesBuffer = null;
            _appendLod0?.Release(); _appendLod0 = null;
            _appendLod1?.Release(); _appendLod1 = null;
            _appendLod2?.Release(); _appendLod2 = null;
            _argLod0?.Release(); _argLod0 = null;
            _argLod1?.Release(); _argLod1 = null;
            _argLod2?.Release(); _argLod2 = null;
            _bufferCapacity = 0;
            _bufferHighWater = 0;
            _liveBladeCount = 0;
            _deadBladeCount = 0;
            _appendCapacity = 0;
        }

        // ── Blade generation (unchanged) ────────────────────────────────────

        private GrassInstanceData[] GenerateBlades(
            Vector3Int chunkPos, SurfacePoint[] surfacePoints)
        {
            int count = Mathf.Max(1, bladesPerPoint);
            var result = new List<GrassInstanceData>(surfacePoints.Length * count);
            float seedBase = WorldSeed.SeedOffset(30);
            float radius = Mathf.Max(0.1f, spreadRadius);

            foreach (var pt in surfacePoints)
            {
                if (pt.biome == null) continue;
                bool isSky = pt.worldPos.y > SkyStartY;
                if (!isSky && !GrassBiomes.Contains(pt.biome.biomeId)) continue;
                if (pt.normal.y < 0.7f) continue;
                if (pt.roadInfluence > 0.3f) continue;
                if (pt.underwater) continue;
                if (!OreGenerator.IsGrass(pt.oreType)) continue;

                int biomeIdx = isSky ? 0 : BiomeIdToIndex(pt.biome.biomeId);
                float invNy = 1f / Mathf.Max(pt.normal.y, 0.01f);

                for (int b = 0; b < count; b++)
                {
                    int h0 = HashInt((int)(pt.worldPos.x * 73.1f) ^ (int)(pt.worldPos.z * 37.7f) ^ (b * 16807));
                    int h1 = HashInt(h0 ^ (int)(pt.worldPos.y * 53.3f) ^ (b * 48271));

                    int densityHash = HashInt(h0 ^ (int)seedBase);
                    float densityThreshold = pt.biome.biomeId == 10 ? 0.70f : 0.95f;
                    if ((densityHash & 0xFF) / 255f > densityThreshold) continue;

                    float angle = ((h0 & 0xFFFF) / 65535f) * 6.28318f;
                    float dist  = Mathf.Sqrt(((h1 & 0xFFFF) / 65535f)) * radius;
                    float jx = Mathf.Cos(angle) * dist;
                    float jz = Mathf.Sin(angle) * dist;
                    float jy = -(pt.normal.x * jx + pt.normal.z * jz) * invNy;

                    float scale  = 0.3f + (((h1 >> 16) & 0xFF) / 255f) * 1.0f;
                    float random = ((h1 >> 24) & 0xFF) / 255f;
                    float colorVariant = biomeIdx + random * 0.999f;

                    result.Add(new GrassInstanceData
                    {
                        position     = pt.worldPos + new Vector3(jx, jy, jz),
                        normal       = pt.normal,
                        scale        = scale,
                        colorVariant = colorVariant
                    });
                }
            }

            return result.ToArray();
        }

        private static int BiomeIdToIndex(int biomeId)
        {
            switch (biomeId)
            {
                case 4:  return 0;
                case 5:  return 1;
                case 6:  return 2;
                case 10: return 3;
                case 7:  return 4;
                case 13: return 5;
                case 14: return 6;
                default: return 0;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        public int GetTotalTreeCount() => _liveBladeCount; // For PerformanceOverlay compatibility

        private static int HashInt(int n)
        {
            n = (n << 13) ^ n;
            return n * (n * n * 15731 + 789221) + 1376312589;
        }
    }
}
