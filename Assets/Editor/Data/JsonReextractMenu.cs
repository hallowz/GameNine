#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Volume 2.5 — shells out to the Python extractor at
    /// <c>Design Documents/GameDesign/scripts/extract_html_data.py</c> which
    /// re-parses <c>voidborne-flowchart-v3.html</c> and rewrites every JSON
    /// file under <c>Design Documents/GameDesign/data/</c>. After a successful
    /// run, <see cref="AssetDatabase.Refresh"/> is invoked so subsequent
    /// generators see the new content.
    /// </summary>
    /// <remarks>
    /// Runs synchronously (the extractor finishes in ~1s on a healthy
    /// machine). Editor freezing during that window is acceptable for an
    /// editor utility. If <c>py</c> isn't on PATH, the menu logs an error
    /// pointing the user at python.org and shows a dialog.
    ///
    /// The "⟳" character in the menu name is U+27F3, not an emoji.
    /// </remarks>
    public static class JsonReextractMenu
    {
        private const string ExtractorRelPath = "Design Documents/GameDesign/scripts/extract_html_data.py";
        private const string PythonLauncher = "py";
        private const string PythonFallback = "python";
        private const string PythonDownloadUrl = "https://www.python.org/";

        [MenuItem("Voidborne/Generate/⟳ Re-extract from HTML")]
        private static void OnMenu() => Run();

        /// <summary>
        /// Resolves the extractor, runs it, streams stdout/stderr to the
        /// Unity console, and refreshes the AssetDatabase on success.
        /// Public so EditMode automation can call it (note: actually invoking
        /// would spawn a real Python process, so tests skip this).
        /// </summary>
        public static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string extractorPath = Path.GetFullPath(Path.Combine(projectRoot, ExtractorRelPath));

            if (!File.Exists(extractorPath))
            {
                string msg = $"Extractor script not found at:\n{extractorPath}\n\n" +
                             $"Expected location: {ExtractorRelPath} (relative to project root).";
                Debug.LogError($"[JsonReextract] {msg}");
                EditorUtility.DisplayDialog("Re-extract failed", msg, "OK");
                return;
            }

            // Try the Windows "py" launcher first (preferred on Windows because
            // it picks the latest installed Python). Fall back to "python".
            if (!TryRun(PythonLauncher, extractorPath, projectRoot, out int exitCode, out string stdout, out string stderr))
            {
                Debug.LogWarning($"[JsonReextract] '{PythonLauncher}' not available — falling back to '{PythonFallback}'.");
                if (!TryRun(PythonFallback, extractorPath, projectRoot, out exitCode, out stdout, out stderr))
                {
                    string msg = $"Neither '{PythonLauncher}' nor '{PythonFallback}' could be launched.\n\n" +
                                 $"Install Python 3.x from {PythonDownloadUrl} and ensure it is on PATH.";
                    Debug.LogError($"[JsonReextract] {msg}");
                    EditorUtility.DisplayDialog("Re-extract failed", msg, "OK");
                    return;
                }
            }

            // Echo subprocess output regardless of exit code so the user can
            // diagnose extractor warnings even on success.
            if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[JsonReextract:stdout]\n{stdout}");
            if (!string.IsNullOrEmpty(stderr)) Debug.LogWarning($"[JsonReextract:stderr]\n{stderr}");

            if (exitCode != 0)
            {
                string msg = $"Extractor exited with code {exitCode}. See Console for stdout/stderr.";
                Debug.LogError($"[JsonReextract] {msg}");
                EditorUtility.DisplayDialog("Re-extract failed", msg, "OK");
                return;
            }

            // Bust the loader cache so the next generator pass sees fresh JSON.
            Voidborne.Data.GameDesignJsonLoader.ClearCache();

            AssetDatabase.Refresh();

            Debug.Log("[JsonReextract] Extractor completed successfully. JSON files refreshed.");
            EditorUtility.DisplayDialog(
                "Re-extract complete",
                "JSON files have been refreshed from the HTML.\n\n" +
                "Run 'Voidborne/Generate/Regenerate All Generated SOs' next to update assets.",
                "OK");
        }

        // ---------------------------------------------------------------------
        // Process plumbing — returns false if the executable itself could not
        // be launched (e.g., not on PATH). A non-zero exit code is reported via
        // the out params, not the return value.
        // ---------------------------------------------------------------------

        private static bool TryRun(
            string exe,
            string extractorPath,
            string workingDirectory,
            out int exitCode,
            out string stdout,
            out string stderr)
        {
            exitCode = -1;
            stdout = string.Empty;
            stderr = string.Empty;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"\"{extractorPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Win32 "file not found" lands here when 'py' / 'python' is
                // missing from PATH. Caller decides whether to fall back.
                Debug.LogWarning($"[JsonReextract] Failed to launch '{exe}': {ex.Message}");
                return false;
            }

            if (proc == null) return false;

            // Read both streams to completion before WaitForExit to avoid the
            // classic deadlock where a full stderr pipe stalls the child.
            var stdoutBuf = new StringBuilder();
            var stderrBuf = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuf.AppendLine(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderrBuf.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            proc.WaitForExit();
            // After WaitForExit, drain remaining async reads.
            proc.WaitForExit();

            exitCode = proc.ExitCode;
            stdout = stdoutBuf.ToString();
            stderr = stderrBuf.ToString();
            return true;
        }
    }
}
#endif
