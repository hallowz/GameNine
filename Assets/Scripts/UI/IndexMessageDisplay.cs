using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Voidborne.UI
{
    /// <summary>
    /// Displays terse device messages from The Index on the bracer screen.
    /// Messages queue up and display one at a time with a typewriter effect,
    /// then fade out. Used for intro text, device readings, and quest prompts.
    ///
    /// The Index is impersonal and technical — never refers to itself as "I",
    /// never emotes. It reports status.
    /// </summary>
    public class IndexMessageDisplay : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Constants
        // ---------------------------------------------------------------

        private const float TypeSpeed        = 0.03f;  // seconds per character
        private const float DisplayDuration  = 3.5f;   // seconds message stays visible after typing
        private const float FadeDuration     = 0.8f;   // seconds to fade out
        private const float MessageGap       = 0.5f;   // pause between queued messages

        // ---------------------------------------------------------------
        //  Colors
        // ---------------------------------------------------------------

        private static readonly Color TextColor      = new(0.45f, 0.85f, 0.90f, 1f);
        private static readonly Color TextColorFaded  = new(0.45f, 0.85f, 0.90f, 0f);

        // ---------------------------------------------------------------
        //  Singleton
        // ---------------------------------------------------------------

        public static IndexMessageDisplay Instance { get; private set; }

        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private Text _messageText;
        private RectTransform _textRect;
        private readonly Queue<string> _queue = new();
        private Coroutine _displayRoutine;
        private bool _isDisplaying;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------
        //  Init (called by UIManager or IndexBracerController)
        // ---------------------------------------------------------------

        /// <summary>
        /// Initialize with a parent RectTransform (e.g., the bracer screen canvas).
        /// Builds the text element.
        /// </summary>
        public void Init(RectTransform parent)
        {
            BuildText(parent);
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Queue a device message for display. Messages play one at a time.
        /// </summary>
        public static void Show(string message)
        {
            if (Instance == null)
            {
                Debug.Log($"[Index] {message}");
                return;
            }
            Instance.Enqueue(message);
        }

        /// <summary>
        /// Queue a message with a custom display duration.
        /// </summary>
        public static void Show(string message, float duration)
        {
            if (Instance == null)
            {
                Debug.Log($"[Index] {message}");
                return;
            }
            Instance.Enqueue(message, duration);
        }

        /// <summary>Clear all queued messages and hide the current one.</summary>
        public static void Clear()
        {
            if (Instance == null) return;
            Instance._queue.Clear();
            if (Instance._displayRoutine != null)
                Instance.StopCoroutine(Instance._displayRoutine);
            Instance._isDisplaying = false;
            if (Instance._messageText != null)
                Instance._messageText.text = "";
        }

        // ---------------------------------------------------------------
        //  Internal
        // ---------------------------------------------------------------

        private void Enqueue(string message, float duration = -1f)
        {
            _queue.Enqueue(message);
            if (!_isDisplaying)
                _displayRoutine = StartCoroutine(ProcessQueue(duration));
        }

        private IEnumerator ProcessQueue(float customDuration)
        {
            _isDisplaying = true;

            while (_queue.Count > 0)
            {
                string msg = _queue.Dequeue();
                yield return StartCoroutine(DisplayMessage(msg, customDuration > 0 ? customDuration : DisplayDuration));
                yield return new WaitForSecondsRealtime(MessageGap);
            }

            _isDisplaying = false;
        }

        private IEnumerator DisplayMessage(string message, float duration)
        {
            if (_messageText == null) yield break;

            _messageText.color = TextColor;
            _messageText.text = "";

            // Typewriter effect
            for (int i = 0; i < message.Length; i++)
            {
                _messageText.text = message[..( i + 1)];
                yield return new WaitForSecondsRealtime(TypeSpeed);
            }

            // Hold
            yield return new WaitForSecondsRealtime(duration);

            // Fade out
            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _messageText.color = Color.Lerp(TextColor, TextColorFaded, t / FadeDuration);
                yield return null;
            }

            _messageText.text = "";
        }

        // ---------------------------------------------------------------
        //  Build UI
        // ---------------------------------------------------------------

        private void BuildText(RectTransform parent)
        {
            var go = new GameObject("IndexMessage", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            _textRect = go.GetComponent<RectTransform>();
            // Position in the lower portion of the bracer screen
            _textRect.anchorMin = new Vector2(0.05f, 0.05f);
            _textRect.anchorMax = new Vector2(0.95f, 0.45f);
            _textRect.offsetMin = Vector2.zero;
            _textRect.offsetMax = Vector2.zero;

            _messageText = go.GetComponent<Text>();
            _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _messageText.fontSize = 10;
            _messageText.color = TextColor;
            _messageText.alignment = TextAnchor.MiddleLeft;
            _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _messageText.verticalOverflow = VerticalWrapMode.Truncate;
            _messageText.raycastTarget = false;
            _messageText.text = "";
        }
    }
}
