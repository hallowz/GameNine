using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Diagnostics
{
    /// <summary>
    /// Lightweight runtime script profiler that measures per-script execution time.
    /// Toggle with Tab key. Shows the top-N most expensive scripts sorted by avg ms.
    ///
    /// Usage in any MonoBehaviour:
    ///   private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("MyScript.Update");
    ///
    ///   void Update() {
    ///       RuntimeProfiler.Begin(s_prof);
    ///       // ... existing code ...
    ///       RuntimeProfiler.End(s_prof);
    ///   }
    ///
    /// Zero-overhead when the overlay is hidden (Begin/End early-out on a static bool).
    /// Remove before shipping.
    /// </summary>
    public class RuntimeProfiler : MonoBehaviour
    {
        // ── Static API ───────────────────────────────────────────────────────

        private static bool _active;
        private static readonly List<ProfileEntry> _entries = new List<ProfileEntry>(64);
        private static readonly Dictionary<string, int> _nameToIndex = new Dictionary<string, int>(64);
        private static readonly Stopwatch _sw = new Stopwatch();

        public struct Token
        {
            internal int index;
        }

        private class ProfileEntry
        {
            public string name;
            public long accumulatedTicks;   // ticks this sample window
            public int callCount;           // calls this sample window
            public double avgMs;            // computed at display time
            public double peakMs;           // worst single call this window
            public long currentStartTick;   // for nested safety
        }

        /// <summary>
        /// Register a profiling point. Call once (static field). Returns a token for Begin/End.
        /// </summary>
        public static Token Register(string name)
        {
            lock (_entries)
            {
                if (_nameToIndex.TryGetValue(name, out int idx))
                    return new Token { index = idx };

                int newIdx = _entries.Count;
                _entries.Add(new ProfileEntry { name = name });
                _nameToIndex[name] = newIdx;
                return new Token { index = newIdx };
            }
        }

        /// <summary>Start timing. No-op when profiler overlay is hidden.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Begin(Token token)
        {
            if (!_active) return;
            _entries[token.index].currentStartTick = _sw.ElapsedTicks;
        }

        /// <summary>Stop timing. No-op when profiler overlay is hidden.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void End(Token token)
        {
            if (!_active) return;
            var entry = _entries[token.index];
            long elapsed = _sw.ElapsedTicks - entry.currentStartTick;
            entry.accumulatedTicks += elapsed;
            entry.callCount++;

            double ms = elapsed * _tickToMs;
            if (ms > entry.peakMs) entry.peakMs = ms;
        }

        // ── Instance (MonoBehaviour) ─────────────────────────────────────────

        [SerializeField] private int fontSize = 13;
        [SerializeField] private int maxDisplayEntries = 25;
        [SerializeField] private float sampleWindowSeconds = 1f;

        [Header("Console Logging")]
        [Tooltip("Log profiler results to console every N seconds (0 = disabled). Works even when overlay is hidden.")]
        [SerializeField] private float consoleLogInterval = 5f;

        private bool _visible;
        private float _sampleTimer;
        private float _consoleLogTimer;
        private readonly StringBuilder _logSb = new StringBuilder(1024);
        private GUIStyle _boxStyle;
        private GUIStyle _textStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _hotStyle;
        private bool _stylesBuilt;

        // Snapshot for display (updated every sampleWindow)
        private readonly List<DisplayEntry> _display = new List<DisplayEntry>(64);
        private float _totalScriptMs;
        private float _totalPerFrameMs;   // average total profiled ms per frame
        private int   _frameCount;        // frames counted in current sample window

        // Frame time tracking — shows non-script overhead (rendering, GPU, GC, Unity internals)
        private double _frameTimeAccumMs;
        private double _frameTimePeakMs;
        private double _displayFrameAvgMs;
        private double _displayFramePeakMs;
        private double _displayNonScriptMs;

        private static double _tickToMs;

        private struct DisplayEntry
        {
            public string name;
            public double avgMs;
            public double peakMs;
            public int callCount;
        }

        private void Awake()
        {
            _sw.Start();
            _tickToMs = 1000.0 / Stopwatch.Frequency;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _visible = !_visible;

            // Active when overlay is visible OR console logging is enabled
            _active = _visible || consoleLogInterval > 0f;

            if (!_active) return;

            _frameCount++;
            double frameDtMs = Time.unscaledDeltaTime * 1000.0;
            _frameTimeAccumMs += frameDtMs;
            if (frameDtMs > _frameTimePeakMs) _frameTimePeakMs = frameDtMs;

            _sampleTimer += Time.unscaledDeltaTime;
            if (_sampleTimer >= sampleWindowSeconds)
            {
                _sampleTimer = 0f;
                BuildSnapshot();
            }

            // Console logging on a separate timer
            if (consoleLogInterval > 0f)
            {
                _consoleLogTimer += Time.unscaledDeltaTime;
                if (_consoleLogTimer >= consoleLogInterval)
                {
                    _consoleLogTimer = 0f;
                    LogToConsole();
                }
            }
        }

        private void BuildSnapshot()
        {
            _display.Clear();
            _totalScriptMs = 0f;
            int frames = _frameCount;
            _frameCount = 0;

            lock (_entries)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    double totalMs = e.accumulatedTicks * _tickToMs;
                    double avg = e.callCount > 0 ? totalMs / e.callCount : 0;

                    _display.Add(new DisplayEntry
                    {
                        name = e.name,
                        avgMs = avg,
                        peakMs = e.peakMs,
                        callCount = e.callCount
                    });

                    _totalScriptMs += (float)totalMs;

                    // Reset for next window
                    e.accumulatedTicks = 0;
                    e.callCount = 0;
                    e.peakMs = 0;
                }
            }

            // Per-frame average: total profiled ms / frames in this window
            _totalPerFrameMs = frames > 0 ? _totalScriptMs / frames : 0f;

            // Frame time tracking
            _displayFrameAvgMs  = frames > 0 ? _frameTimeAccumMs / frames : 0;
            _displayFramePeakMs = _frameTimePeakMs;
            _displayNonScriptMs = _displayFrameAvgMs - _totalPerFrameMs;
            _frameTimeAccumMs   = 0;
            _frameTimePeakMs    = 0;

            // Sort by total cost (avgMs * callCount) descending
            _display.Sort((a, b) =>
            {
                double costA = a.avgMs * a.callCount;
                double costB = b.avgMs * b.callCount;
                return costB.CompareTo(costA);
            });
        }

        private void LogToConsole()
        {
            if (_display.Count == 0) return;

            int count = Mathf.Min(_display.Count, maxDisplayEntries);
            for (int i = 0; i < count; i++)
            {
                var e = _display[i];
                double totalMs = e.avgMs * e.callCount;
                if (totalMs < 0.01 && e.peakMs < 0.01) continue;

                UnityEngine.Debug.Log($"[ScriptProfiler] #{i + 1} {e.name} | avg={e.avgMs:F3}ms peak={e.peakMs:F3}ms calls={e.callCount} total={totalMs:F2}ms");
            }

            UnityEngine.Debug.Log($"[ScriptProfiler] ── Scripts: {_totalPerFrameMs:F2}ms/frame | Frame: avg={_displayFrameAvgMs:F1}ms peak={_displayFramePeakMs:F1}ms | Non-script: {_displayNonScriptMs:F1}ms/frame ──");
        }

        // ── OnGUI ────────────────────────────────────────────────────────────

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.88f));

            _textStyle = new GUIStyle(GUI.skin.label);
            _textStyle.fontSize = fontSize;
            _textStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _textStyle.richText = true;
            _textStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);

            _headerStyle = new GUIStyle(_textStyle);
            _headerStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            _headerStyle.fontStyle = FontStyle.Bold;

            _hotStyle = new GUIStyle(_textStyle);
            _hotStyle.normal.textColor = new Color(1f, 0.5f, 0.2f);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            BuildStyles();

            float lineH = fontSize + 3f;
            int count = Mathf.Min(_display.Count, maxDisplayEntries);
            float w = 620f;
            float h = lineH * (count + 4) + 30f;
            float x = 10f;
            float y = 10f;

            GUI.Box(new Rect(x, y, w, h), "", _boxStyle);

            float cx = x + 8f;
            float cy = y + 6f;

            GUI.Label(new Rect(cx, cy, w, lineH),
                $"SCRIPT PROFILER  [F1 toggle]  Scripts: {_totalPerFrameMs:F2}ms  Frame: {_displayFrameAvgMs:F1}ms (peak {_displayFramePeakMs:F0}ms)  Non-script: {_displayNonScriptMs:F1}ms", _headerStyle);
            cy += lineH + 2f;

            // Column headers
            string header = string.Format("{0,-35} {1,8} {2,8} {3,10} {4,10}",
                "Script", "Avg(ms)", "Peak(ms)", "Calls", "Total(ms)");
            GUI.Label(new Rect(cx, cy, w, lineH), header, _headerStyle);
            cy += lineH;

            // Separator
            GUI.Label(new Rect(cx, cy, w, lineH),
                "─────────────────────────────────────────────────────────────────────────────", _textStyle);
            cy += lineH;

            for (int i = 0; i < count; i++)
            {
                var e = _display[i];
                double totalMs = e.avgMs * e.callCount;
                bool hot = totalMs > 2.0 || e.peakMs > 5.0;

                string line = string.Format("{0,-35} {1,8:F3} {2,8:F3} {3,10} {4,10:F2}",
                    e.name.Length > 35 ? e.name.Substring(0, 35) : e.name,
                    e.avgMs,
                    e.peakMs,
                    e.callCount,
                    totalMs);

                GUI.Label(new Rect(cx, cy, w, lineH), line, hot ? _hotStyle : _textStyle);
                cy += lineH;
            }
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
