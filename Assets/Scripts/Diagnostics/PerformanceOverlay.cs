using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.InputSystem;

namespace Voidborne.Diagnostics
{
    /// <summary>
    /// In-game performance overlay toggled with backtick (`).
    /// Shows real-time FPS, frame times, memory, GC, and game system stats.
    /// Uses OnGUI for zero-dependency rendering (no canvas/TMP needed).
    /// Remove before shipping.
    /// </summary>
    public class PerformanceOverlay : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool startVisible = false;
        [SerializeField] private int fontSize = 14;

        private bool _visible;
        private GUIStyle _boxStyle;
        private GUIStyle _textStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _warnStyle;
        private bool _stylesBuilt;

        // ── Frame timing ────────────────────────────────────────────────────
        private readonly float[] _frameBuffer = new float[300]; // ~5 seconds at 60fps
        private int _frameIndex;
        private int _frameCount;
        private float _worstFrameRecent;
        private float _bestFrameRecent = float.MaxValue;
        private float _resetTimer;

        // ── FPS smoothing ───────────────────────────────────────────────────
        private float _smoothFPS;
        private float _onePercentLow;
        private readonly float[] _sortedFrameBuffer = new float[300]; // reused for 1% low calculation
        private int _fpsStatsSkip; // only sort every N frames

        // ── GC tracking ─────────────────────────────────────────────────────
        private int _lastGCCount;
        private int _gcPerSecond;
        private int _gcThisSecond;
        private float _gcTimer;

        // ── Console log interval ────────────────────────────────────────────
        [Header("Console Logging")]
        [Tooltip("Seconds between console log reports (0 = disabled).")]
        [SerializeField] private float consoleLogInterval = 10f;
        private float _consoleLogTimer;
        private readonly StringBuilder _logSb = new StringBuilder(512);

        // ── System probe (once per second to avoid overhead) ────────────────
        private float _probeTimer;
        private int _activeChunks;
        private int _totalChunks;
        private int _freeGPUSlots;
        private int _totalGPUSlots;
        private int _treeCount;
        private int _enemyCount;
        private long _totalMemMB;
        private long _gfxMemMB;
        private long _monoUsedMB;
        private long _monoHeapMB;

        // ── Stutter detection ───────────────────────────────────────────────
        private int _stutterCount;       // >33ms frames in last 5s
        private int _severeStutterCount; // >50ms frames in last 5s
        private float _stutterResetTimer;

        // ── Frame time histogram (rolling) ──────────────────────────────────
        private readonly int[] _histogram = new int[7];
        private static readonly string[] HistLabels = { "<8", "8-11", "11-16", "16-22", "22-33", "33-50", ">50" };
        private static readonly float[] HistThresholds = { 0.008f, 0.011f, 0.016f, 0.022f, 0.033f, 0.050f, float.MaxValue };

        // Pre-built bar strings to avoid per-frame string allocation in OnGUI
        private static readonly string[] BarStrings;
        static PerformanceOverlay()
        {
            BarStrings = new string[21]; // 0..20 chars of '#'
            for (int i = 0; i < BarStrings.Length; i++)
                BarStrings[i] = new string('#', i);
        }

        private void Start()
        {
            _visible = startVisible;
            _lastGCCount = System.GC.CollectionCount(0);
        }

        private void Update()
        {
            // Toggle with backtick (new Input System only — project has switched off legacy input)
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
                _visible = !_visible;

            float dt = Time.unscaledDeltaTime;

            // Frame buffer (circular)
            _frameBuffer[_frameIndex] = dt;
            _frameIndex = (_frameIndex + 1) % _frameBuffer.Length;
            if (_frameCount < _frameBuffer.Length) _frameCount++;

            // Best/worst (reset every 5s)
            if (dt > _worstFrameRecent) _worstFrameRecent = dt;
            if (dt < _bestFrameRecent) _bestFrameRecent = dt;
            _resetTimer += dt;
            if (_resetTimer >= 5f)
            {
                _resetTimer = 0f;
                _worstFrameRecent = 0f;
                _bestFrameRecent = float.MaxValue;
            }

            // Smooth FPS + 1% lows
            ComputeFPSStats();

            // GC tracking
            int currentGC = System.GC.CollectionCount(0);
            _gcThisSecond += currentGC - _lastGCCount;
            _lastGCCount = currentGC;
            _gcTimer += dt;
            if (_gcTimer >= 1f)
            {
                _gcTimer -= 1f;
                _gcPerSecond = _gcThisSecond;
                _gcThisSecond = 0;
            }

            // Stutter tracking
            if (dt > 0.033f) _stutterCount++;
            if (dt > 0.050f) _severeStutterCount++;
            _stutterResetTimer += dt;
            if (_stutterResetTimer >= 5f)
            {
                _stutterResetTimer = 0f;
                _stutterCount = 0;
                _severeStutterCount = 0;
            }

            // Histogram
            for (int b = 0; b < _histogram.Length; b++)
            {
                if (dt <= HistThresholds[b]) { _histogram[b]++; break; }
            }

            // System probes (1/s)
            _probeTimer += dt;
            if (_probeTimer >= 1f)
            {
                _probeTimer -= 1f;
                ProbeGameSystems();
            }

            // Periodic console log
            if (consoleLogInterval > 0f)
            {
                _consoleLogTimer += dt;
                if (_consoleLogTimer >= consoleLogInterval)
                {
                    _consoleLogTimer -= consoleLogInterval;
                    LogToConsole();
                }
            }
        }

        private void LogToConsole()
        {
            int totalFrames = 0;
            foreach (int b in _histogram) totalFrames += b;
            if (totalFrames == 0) totalFrames = 1;

            _logSb.Clear();
            _logSb.Append("[PerfOverlay] ");
            _logSb.Append($"FPS={_smoothFPS:F0} 1%Low={_onePercentLow:F0} ");
            _logSb.Append($"Frame={Time.unscaledDeltaTime * 1000f:F1}ms ");
            _logSb.Append($"Best={_bestFrameRecent * 1000f:F1}ms Worst={_worstFrameRecent * 1000f:F1}ms | ");
            _logSb.Append($"Mem={_totalMemMB}MB GFX={_gfxMemMB}MB Mono={_monoUsedMB}/{_monoHeapMB}MB GC/s={_gcPerSecond} | ");
            _logSb.Append($"Chunks={_activeChunks}/{_totalChunks} GPUSlots={_freeGPUSlots}/{_totalGPUSlots} ");
            _logSb.Append($"Trees={_treeCount} Enemies={_enemyCount} | ");
            _logSb.Append($"Stutters(5s)={_stutterCount}(>33ms) {_severeStutterCount}(>50ms) | ");
            _logSb.Append("Hist: ");
            for (int b = 0; b < _histogram.Length; b++)
            {
                float pct = 100f * _histogram[b] / totalFrames;
                _logSb.Append($"{HistLabels[b]}={pct:F0}% ");
            }

            Debug.Log(_logSb.ToString());
        }

        private void ComputeFPSStats()
        {
            if (_frameCount == 0) return;

            float sum = 0f;
            int count = _frameCount;
            for (int i = 0; i < count; i++) sum += _frameBuffer[i];
            float avg = sum / count;
            _smoothFPS = 1f / avg;

            // 1% low: expensive sort — only recompute every 30 frames
            _fpsStatsSkip++;
            if (_fpsStatsSkip >= 30)
            {
                _fpsStatsSkip = 0;
                System.Array.Copy(_frameBuffer, _sortedFrameBuffer, count);
                System.Array.Sort(_sortedFrameBuffer, 0, count);
                int p99Idx = Mathf.Min((int)(count * 0.99f), count - 1);
                _onePercentLow = 1f / _sortedFrameBuffer[p99Idx];
            }
        }

        private void ProbeGameSystems()
        {
            // Memory
            _totalMemMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            _gfxMemMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024);
            _monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
            _monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

            // Chunks
            var cm = Voidborne.World.Chunks.ChunkManager.Instance;
            if (cm != null)
            {
                _activeChunks = cm.ActiveChunkObjectCount;
                _totalChunks = cm.ChunkCount;
                _freeGPUSlots = cm.FreeGenerationSlots;
                _totalGPUSlots = cm.TotalGenerationSlots;
            }

            // Trees
            var tr = Voidborne.World.Decoration.TreeRenderer.Instance;
            if (tr != null) _treeCount = tr.GetTotalTreeCount();

            // Enemies — FindObjectsByType with no sorting is significantly faster than FindObjectsOfType
            _enemyCount = Voidborne.Enemies.EnemyManager.Instance != null
                ? Voidborne.Enemies.EnemyManager.Instance.ActiveCount
                : 0;
        }

        // ── OnGUI rendering ─────────────────────────────────────────────────

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.82f));

            _textStyle = new GUIStyle(GUI.skin.label);
            _textStyle.fontSize = fontSize;
            _textStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            _textStyle.richText = true;
            _textStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);

            _headerStyle = new GUIStyle(_textStyle);
            _headerStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            _headerStyle.fontStyle = FontStyle.Bold;

            _warnStyle = new GUIStyle(_textStyle);
            _warnStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            BuildStyles();

            float w = 380f;
            float h = 520f;
            float x = Screen.width - w - 10f;
            float y = 10f;

            GUI.Box(new Rect(x, y, w, h), "", _boxStyle);

            float cx = x + 10f;
            float cy = y + 8f;
            float lineH = fontSize + 4f;

            // Title
            GUI.Label(new Rect(cx, cy, w, lineH), "PERFORMANCE OVERLAY  [` to toggle]", _headerStyle);
            cy += lineH + 4f;

            // FPS
            float dt = Time.unscaledDeltaTime;
            float instantFPS = dt > 0 ? 1f / dt : 0f;
            Color fpsColor = _smoothFPS >= 60f ? Color.green : _smoothFPS >= 30f ? Color.yellow : Color.red;
            string fpsHex = ColorUtility.ToHtmlStringRGB(fpsColor);

            GUI.Label(new Rect(cx, cy, w, lineH), $"<color=#{fpsHex}>FPS: {_smoothFPS:F0}</color>  1% Low: {_onePercentLow:F0}  Frame: {dt * 1000f:F1}ms", _textStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Best: {_bestFrameRecent * 1000f:F1}ms  Worst: {_worstFrameRecent * 1000f:F1}ms  (5s window)", _textStyle);
            cy += lineH;

            string stutterColor = _severeStutterCount > 0 ? "FF6633" : _stutterCount > 0 ? "FFAA33" : "88FF88";
            GUI.Label(new Rect(cx, cy, w, lineH), $"<color=#{stutterColor}>Stutters(5s): {_stutterCount} (>33ms)  {_severeStutterCount} (>50ms)</color>", _textStyle);
            cy += lineH + 6f;

            // Memory
            GUI.Label(new Rect(cx, cy, w, lineH), "MEMORY", _headerStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Total: {_totalMemMB}MB  GFX: {_gfxMemMB}MB", _textStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Mono: {_monoUsedMB}/{_monoHeapMB}MB  GC/s: {_gcPerSecond}", _textStyle);
            cy += lineH + 6f;

            // Game systems
            GUI.Label(new Rect(cx, cy, w, lineH), "WORLD", _headerStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Chunks: {_activeChunks} active / {_totalChunks} data", _textStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"GPU slots: {_freeGPUSlots} free / {_totalGPUSlots} total", _textStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Trees (instanced): {_treeCount}", _textStyle);
            cy += lineH;
            GUI.Label(new Rect(cx, cy, w, lineH), $"Enemies alive: {_enemyCount}", _textStyle);
            cy += lineH + 6f;

            // Histogram
            GUI.Label(new Rect(cx, cy, w, lineH), "FRAME TIME DISTRIBUTION", _headerStyle);
            cy += lineH;

            int maxBucket = 1;
            int totalFrames = 0;
            foreach (int b in _histogram) { if (b > maxBucket) maxBucket = b; totalFrames += b; }
            if (totalFrames == 0) totalFrames = 1;

            for (int b = 0; b < _histogram.Length; b++)
            {
                float pct = 100f * _histogram[b] / totalFrames;
                int barLen = Mathf.Clamp((int)(20f * _histogram[b] / maxBucket), 0, 20);
                string bar = BarStrings[barLen];
                string color = b >= 5 ? "FF6633" : b >= 4 ? "FFAA33" : "88FF88";
                GUI.Label(new Rect(cx, cy, w, lineH),
                    $"<color=#{color}>{HistLabels[b],5}ms |{bar,-20} {pct,5:F1}%</color>", _textStyle);
                cy += lineH;
            }

            cy += 4f;

            // Mini frame graph (last 120 frames)
            GUI.Label(new Rect(cx, cy, w, lineH), "FRAME GRAPH (last 120 frames)", _headerStyle);
            cy += lineH;

            float graphW = w - 20f;
            float graphH = 50f;
            Rect graphRect = new Rect(cx, cy, graphW, graphH);
            GUI.Box(graphRect, "", _boxStyle);

            // Draw 16.6ms and 33.3ms reference lines
            float maxMs = 50f;
            float line60 = graphH * (1f - 16.66f / maxMs);
            float line30 = graphH * (1f - 33.33f / maxMs);

            // Reference lines
            Color oldColor = GUI.color;
            DrawLine(new Vector2(cx, cy + line60), new Vector2(cx + graphW, cy + line60), new Color(0f, 1f, 0f, 0.3f));
            DrawLine(new Vector2(cx, cy + line30), new Vector2(cx + graphW, cy + line30), new Color(1f, 0f, 0f, 0.3f));

            // Frame bars
            int drawCount = Mathf.Min(_frameCount, 120);
            float barWidth = graphW / 120f;
            int startIdx = (_frameIndex - drawCount + _frameBuffer.Length) % _frameBuffer.Length;

            for (int i = 0; i < drawCount; i++)
            {
                int idx = (startIdx + i) % _frameBuffer.Length;
                float ms = _frameBuffer[idx] * 1000f;
                float barH = Mathf.Clamp01(ms / maxMs) * graphH;
                float bx = cx + i * barWidth;
                float by = cy + graphH - barH;

                Color barColor = ms < 16.66f ? new Color(0.3f, 0.9f, 0.3f, 0.8f)
                    : ms < 33.33f ? new Color(0.9f, 0.9f, 0.2f, 0.8f)
                    : new Color(0.9f, 0.3f, 0.2f, 0.8f);

                GUI.DrawTexture(new Rect(bx, by, Mathf.Max(barWidth - 1f, 1f), barH), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, barColor, 0f, 0f);
            }

            // Labels
            GUI.Label(new Rect(cx + graphW - 40f, cy + line60 - lineH * 0.5f, 40f, lineH), "<color=#44FF44><size=10>60fps</size></color>", _textStyle);
            GUI.Label(new Rect(cx + graphW - 40f, cy + line30 - lineH * 0.5f, 40f, lineH), "<color=#FF4444><size=10>30fps</size></color>", _textStyle);

            GUI.color = oldColor;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y, length, 1f), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, a);
            GUI.color = prev;
        }
    }
}
