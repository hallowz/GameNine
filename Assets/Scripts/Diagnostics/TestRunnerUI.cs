using UnityEngine;
using System;
using System.Collections.Generic;

namespace Voidborne.Diagnostics
{
    /// <summary>
    /// In-game IMGUI overlay for displaying automated test results.
    /// Toggle visibility with F9. Singleton accessed via Instance.
    /// </summary>
    public class TestRunnerUI : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────────────────
        public static TestRunnerUI Instance { get; private set; }

        // ── Test result data ────────────────────────────────────────────

        public enum TestStatus { Pending, Running, Passed, Failed }

        public class TestResult
        {
            public string   Name;
            public TestStatus Status;
            public float    Duration;
            public string   ErrorMessage;
            public string   Timestamp;
        }

        private readonly List<TestResult> _results = new List<TestResult>();

        // ── UI state ────────────────────────────────────────────────────

        private bool _visible = false;
        private Vector2 _scrollPosition;
        private bool _autoScroll = true;

        // ── Style cache ─────────────────────────────────────────────────

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _summaryStyle;
        private bool _stylesInitialized;

        // ── Counts ──────────────────────────────────────────────────────

        public int TotalCount   => _results.Count;
        public int PassedCount  { get; private set; }
        public int FailedCount  { get; private set; }
        public int RunningCount { get; private set; }

        // ─── Unity Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current != null &&
                    UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
                    _visible = !_visible;
            }
            catch { }
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>Mark a test as running.</summary>
        public void StartTest(string testName)
        {
            var existing = _results.Find(r => r.Name == testName);
            if (existing != null)
            {
                existing.Status    = TestStatus.Running;
                existing.Duration  = 0f;
                existing.ErrorMessage = null;
                existing.Timestamp = DateTime.Now.ToString("HH:mm:ss");
            }
            else
            {
                _results.Add(new TestResult
                {
                    Name      = testName,
                    Status    = TestStatus.Running,
                    Duration  = 0f,
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                });
            }

            RecalcCounts();
            _autoScroll = true;
            Debug.Log($"[TEST] STARTED: {testName}");
        }

        /// <summary>Record a completed test result.</summary>
        public void AddTestResult(string testName, bool passed, float duration, string errorMessage = null)
        {
            var existing = _results.Find(r => r.Name == testName);
            if (existing != null)
            {
                existing.Status       = passed ? TestStatus.Passed : TestStatus.Failed;
                existing.Duration     = duration;
                existing.ErrorMessage = errorMessage;
                existing.Timestamp    = DateTime.Now.ToString("HH:mm:ss");
            }
            else
            {
                _results.Add(new TestResult
                {
                    Name         = testName,
                    Status       = passed ? TestStatus.Passed : TestStatus.Failed,
                    Duration     = duration,
                    ErrorMessage = errorMessage,
                    Timestamp    = DateTime.Now.ToString("HH:mm:ss"),
                });
            }

            RecalcCounts();
            _autoScroll = true;

            string prefix = passed ? "PASS" : "FAIL";
            string msg = $"[TEST] {prefix}: {testName} ({duration:F2}s)";
            if (!string.IsNullOrEmpty(errorMessage))
                msg += $" - {errorMessage}";

            if (passed)
                Debug.Log(msg);
            else
                Debug.LogError(msg);
        }

        /// <summary>Clear all results.</summary>
        public void ClearResults()
        {
            _results.Clear();
            RecalcCounts();
            _scrollPosition = Vector2.zero;
            Debug.Log("[TEST] Results cleared.");
        }

        /// <summary>Show the overlay.</summary>
        public void Show() => _visible = true;

        /// <summary>Hide the overlay.</summary>
        public void Hide() => _visible = false;

        /// <summary>Read-only access to results for external consumers.</summary>
        public IReadOnlyList<TestResult> Results => _results;

        // ─── GUI ────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            InitStyles();

            // Full-screen semi-transparent backdrop
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float margin  = 20f;
            float panelW  = Mathf.Min(Screen.width - margin * 2, 900f);
            float panelH  = Screen.height - margin * 2;
            float panelX  = (Screen.width - panelW) / 2f;
            float panelY  = margin;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH));

            // ── Title ───────────────────────────────────────────────────
            GUILayout.Label("TEST RUNNER", _headerStyle);
            GUILayout.Space(4);

            // ── Summary bar ─────────────────────────────────────────────
            DrawSummaryBar(panelW);
            GUILayout.Space(6);

            // ── Buttons ─────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run All Tests", _buttonStyle, GUILayout.Width(140), GUILayout.Height(28)))
                OnRunAllTestsClicked();
            GUILayout.Space(8);
            if (GUILayout.Button("Clear Results", _buttonStyle, GUILayout.Width(140), GUILayout.Height(28)))
                ClearResults();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Press F9 to toggle", _labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // ── Results scroll view ─────────────────────────────────────
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_results.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label("No test results yet. Click 'Run All Tests' or trigger tests from code.", _labelStyle);
                GUI.color = Color.white;
            }
            else
            {
                for (int i = 0; i < _results.Count; i++)
                    DrawTestRow(_results[i], panelW - 30f);
            }

            // Auto-scroll to bottom
            if (_autoScroll)
            {
                _scrollPosition.y = float.MaxValue;
                _autoScroll = false;
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // ─── Drawing helpers ────────────────────────────────────────────

        private void DrawSummaryBar(float width)
        {
            GUILayout.BeginHorizontal(_boxStyle);

            DrawSummaryItem("Total",   TotalCount,   Color.white);
            DrawSummaryItem("Passed",  PassedCount,  new Color(0.2f, 0.9f, 0.2f));
            DrawSummaryItem("Failed",  FailedCount,  new Color(1f, 0.3f, 0.3f));
            DrawSummaryItem("Running", RunningCount, new Color(1f, 0.9f, 0.2f));

            int pendingCount = TotalCount - PassedCount - FailedCount - RunningCount;
            if (pendingCount > 0)
                DrawSummaryItem("Pending", pendingCount, new Color(0.7f, 0.7f, 0.7f));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawSummaryItem(string label, int count, Color color)
        {
            GUI.color = color;
            GUILayout.Label($"{label}: {count}", _summaryStyle, GUILayout.Width(100));
            GUI.color = Color.white;
        }

        private void DrawTestRow(TestResult result, float width)
        {
            Color rowColor;
            string statusText;

            switch (result.Status)
            {
                case TestStatus.Passed:
                    rowColor   = new Color(0.15f, 0.35f, 0.15f);
                    statusText = "PASS";
                    break;
                case TestStatus.Failed:
                    rowColor   = new Color(0.4f, 0.12f, 0.12f);
                    statusText = "FAIL";
                    break;
                case TestStatus.Running:
                    rowColor   = new Color(0.35f, 0.32f, 0.1f);
                    statusText = "RUN ";
                    break;
                default:
                    rowColor   = new Color(0.2f, 0.2f, 0.2f);
                    statusText = "PEND";
                    break;
            }

            // Row background
            GUI.color = rowColor;
            GUILayout.BeginVertical(_boxStyle);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();

            // Status badge
            Color badgeColor = GetStatusColor(result.Status);
            GUI.color = badgeColor;
            GUILayout.Label($"[{statusText}]", _labelStyle, GUILayout.Width(54));
            GUI.color = Color.white;

            // Test name
            GUILayout.Label(result.Name, _labelStyle, GUILayout.MinWidth(200));

            GUILayout.FlexibleSpace();

            // Duration
            if (result.Status == TestStatus.Passed || result.Status == TestStatus.Failed)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUILayout.Label($"{result.Duration:F3}s", _labelStyle, GUILayout.Width(70));
                GUI.color = Color.white;
            }

            // Timestamp
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label(result.Timestamp, _labelStyle, GUILayout.Width(70));
            GUI.color = Color.white;

            GUILayout.EndHorizontal();

            // Error message (if any)
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                GUI.color = new Color(1f, 0.5f, 0.5f);
                GUILayout.Label($"  -> {result.ErrorMessage}", _errorStyle);
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private Color GetStatusColor(TestStatus status)
        {
            switch (status)
            {
                case TestStatus.Passed:  return new Color(0.2f, 1f, 0.2f);
                case TestStatus.Failed:  return new Color(1f, 0.3f, 0.3f);
                case TestStatus.Running: return new Color(1f, 0.9f, 0.2f);
                default:                 return Color.white;
            }
        }

        // ─── Run All Tests button ───────────────────────────────────────

        private void OnRunAllTestsClicked()
        {
            Debug.Log("[TEST] Run All Tests requested.");

            // Look for an AutomatedTestRunner in the scene
            var runner = FindObjectOfType<AutomatedTestRunner>();
            if (runner != null)
            {
                runner.RunAllTests();
            }
            else
            {
                Debug.LogWarning("[TEST] No AutomatedTestRunner found in scene. " +
                    "Add AutomatedTestRunner to a GameObject to enable automated tests.");
            }
        }

        // ─── Internal ───────────────────────────────────────────────────

        private void RecalcCounts()
        {
            PassedCount  = 0;
            FailedCount  = 0;
            RunningCount = 0;

            for (int i = 0; i < _results.Count; i++)
            {
                switch (_results[i].Status)
                {
                    case TestStatus.Passed:  PassedCount++;  break;
                    case TestStatus.Failed:  FailedCount++;  break;
                    case TestStatus.Running: RunningCount++; break;
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin  = new RectOffset(0, 0, 1, 1),
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal   = { textColor = Color.white },
            };

            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Italic,
                wordWrap  = true,
                normal    = { textColor = new Color(1f, 0.5f, 0.5f) },
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
            };

            _summaryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
