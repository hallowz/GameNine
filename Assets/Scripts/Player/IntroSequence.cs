using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Enemies;
using Voidborne.UI;
using Voidborne.World.Chunks;

namespace Voidborne.Player
{
    /// <summary>
    /// Handles the initial game intro (10-15 seconds, diegetic) and surface spawn.
    ///
    /// Flow:
    ///   1. Screen starts black, player input frozen.
    ///   2. Wait for terrain chunk under player to load.
    ///   3. Snap player to terrain surface.
    ///   4. Device text: "Host tethered. Signal locked."
    ///   5. Screen flash, fade to gameplay.
    ///   6. After 2s: "Local architecture indexed. Partial reconstruction possible. Module available: Tether."
    ///   7. Full player control.
    ///
    /// Plays once per save (uses PlayerPrefs). Subsequent loads skip to surface spawn only.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Constants
        // ---------------------------------------------------------------

        private const string IntroCompletedKey = "intro_completed";
        private const float  SurfaceSearchDepth = 128f;

        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private Canvas _overlayCanvas;
        private Image  _blackOverlay;
        private Image  _flashOverlay;
        private bool   _introPlaying;
        private DensityFieldNavigator _densityNav;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private IEnumerator Start()
        {
            _densityNav = new DensityFieldNavigator();

            // Wait for PlayerManager
            while (PlayerManager.Instance == null)
                yield return null;

            // Freeze player immediately
            SetPlayerFrozen(true);

            // Wait for terrain to generate under the player
            yield return StartCoroutine(WaitForTerrain());

            // Snap to surface
            SnapPlayerToSurface();

            // Check if intro already played
            bool introCompleted = PlayerPrefs.GetInt(IntroCompletedKey, 0) == 1;

            if (!introCompleted)
            {
                yield return StartCoroutine(PlayIntro());
                PlayerPrefs.SetInt(IntroCompletedKey, 1);
                PlayerPrefs.Save();
            }
            else
            {
                // Just unfade quickly
                yield return new WaitForSecondsRealtime(0.3f);
            }

            // Unfreeze player
            SetPlayerFrozen(false);

            // Clean up overlay
            if (_overlayCanvas != null)
                Destroy(_overlayCanvas.gameObject);
        }

        // ---------------------------------------------------------------
        //  Surface spawn
        // ---------------------------------------------------------------

        private IEnumerator WaitForTerrain()
        {
            if (ChunkManager.Instance == null)
            {
                Debug.LogWarning("[IntroSequence] ChunkManager not found, skipping terrain wait.");
                yield break;
            }

            Transform playerT = PlayerManager.Instance.PlayerTransform;
            Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(playerT.position);

            // Wait until the chunk at the player's position is active.
            float timeout = 30f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                ChunkData chunk = ChunkManager.Instance.GetChunk(chunkPos);
                if (chunk != null && chunk.state == ChunkState.Active)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogWarning("[IntroSequence] Timed out waiting for terrain chunk.");
        }

        private void SnapPlayerToSurface()
        {
            Transform playerT = PlayerManager.Instance.PlayerTransform;
            Vector3 pos = playerT.position;

            // Search from high above downward to find the surface.
            Vector3 searchStart = new Vector3(pos.x, pos.y + 64f, pos.z);
            float surfaceY = _densityNav.GetSurfaceHeightAt(searchStart, SurfaceSearchDepth);

            // Place player 1.5m above surface (half standing height + margin).
            playerT.position = new Vector3(pos.x, surfaceY + 1.5f, pos.z);
            Debug.Log($"[IntroSequence] Player snapped to surface at Y={surfaceY:F1}");
        }

        // ---------------------------------------------------------------
        //  Intro sequence
        // ---------------------------------------------------------------

        private IEnumerator PlayIntro()
        {
            _introPlaying = true;

            BuildOverlay();

            // 1. Black screen (1s)
            SetOverlayAlpha(1f);
            yield return new WaitForSecondsRealtime(1f);

            // 2. First device message (types on screen while still mostly black)
            IndexMessageDisplay.Show("Host tethered. Signal locked.", 2.5f);
            yield return new WaitForSecondsRealtime(2.5f);

            // 3. Screen flash
            yield return StartCoroutine(ScreenFlash(0.4f));

            // 4. Fade to gameplay over 1.5s
            yield return StartCoroutine(FadeOverlay(1f, 0f, 1.5f));

            // 5. Brief pause, then second message
            yield return new WaitForSecondsRealtime(2f);
            IndexMessageDisplay.Show("Local architecture indexed. Partial reconstruction possible. Module available: Tether.");

            _introPlaying = false;
        }

        // ---------------------------------------------------------------
        //  Overlay helpers
        // ---------------------------------------------------------------

        private void BuildOverlay()
        {
            var canvasGO = new GameObject("IntroOverlay");
            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 999; // on top of everything
            canvasGO.AddComponent<CanvasScaler>();

            // Black overlay
            var blackGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
            blackGO.transform.SetParent(canvasGO.transform, false);
            var blackRect = blackGO.GetComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;
            _blackOverlay = blackGO.GetComponent<Image>();
            _blackOverlay.color = Color.black;
            _blackOverlay.raycastTarget = false;

            // White flash overlay (starts transparent)
            var flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(canvasGO.transform, false);
            var flashRect = flashGO.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            _flashOverlay = flashGO.GetComponent<Image>();
            _flashOverlay.color = new Color(0.7f, 0.9f, 1f, 0f); // cold blue-white
            _flashOverlay.raycastTarget = false;
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (_blackOverlay != null)
                _blackOverlay.color = new Color(0f, 0f, 0f, alpha);
        }

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, t / duration);
                SetOverlayAlpha(a);
                yield return null;
            }
            SetOverlayAlpha(to);
        }

        private IEnumerator ScreenFlash(float duration)
        {
            if (_flashOverlay == null) yield break;

            // Flash in
            float half = duration * 0.3f;
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                _flashOverlay.color = new Color(0.7f, 0.9f, 1f, Mathf.Lerp(0f, 0.8f, t / half));
                yield return null;
            }

            // Flash out
            t = 0f;
            float fadeOut = duration - half;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                _flashOverlay.color = new Color(0.7f, 0.9f, 1f, Mathf.Lerp(0.8f, 0f, t / fadeOut));
                yield return null;
            }

            _flashOverlay.color = new Color(0.7f, 0.9f, 1f, 0f);
        }

        // ---------------------------------------------------------------
        //  Player control freeze
        // ---------------------------------------------------------------

        private void SetPlayerFrozen(bool frozen)
        {
            var fpc = PlayerManager.Instance?.Controller;
            if (fpc != null)
                fpc.enabled = !frozen;

            var cam = PlayerManager.Instance?.CameraController;
            if (cam != null)
                cam.enabled = !frozen;

            // Lock/unlock cursor
            UnityEngine.Cursor.lockState = frozen ? CursorLockMode.Locked : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }
}
