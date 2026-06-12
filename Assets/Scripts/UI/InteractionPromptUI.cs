using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.UI.Style;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.6 — screen-space interaction prompt label.
    ///
    /// Replaces the old in-world Canvas labels that floated above interactable
    /// objects. Renders as a compact pill in the bottom-center of the screen
    /// (above the hotbar) showing what action the player can take, e.g.
    /// <c>[E] Open Workbench</c>.
    ///
    /// Use:
    /// <list type="bullet">
    ///   <item><see cref="Show(string)"/> — show with the existing IInteractable prompt text.</item>
    ///   <item><see cref="Show(string, string)"/> — show with explicit verb + target.</item>
    ///   <item><see cref="Hide"/> — hide the prompt.</item>
    /// </list>
    ///
    /// Coop note: the prompt is client-local. Owner-authoritative interaction
    /// raycast lives in <c>PlayerInteractionPromptDriver</c>.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Singleton (optional — first instance to Awake wins)
        // ---------------------------------------------------------------

        public static InteractionPromptUI Instance { get; private set; }

        // ---------------------------------------------------------------
        //  Built children
        // ---------------------------------------------------------------

        private RectTransform _rectTransform;
        private Image         _bg;
        private TextMeshProUGUI _label;

        // ---------------------------------------------------------------
        //  Layout constants
        // ---------------------------------------------------------------

        private const float PanelWidth  = 280f;
        private const float PanelHeight = 28f;

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureBuilt();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------
        //  Build
        // ---------------------------------------------------------------

        /// <summary>
        /// Idempotent build hook. EditMode tests call this directly because
        /// AddComponent in EditMode does not invoke Awake.
        /// </summary>
        public void EnsureBuilt()
        {
            if (Instance == null) Instance = this;
            if (_rectTransform != null) return;
            Build();
        }

        private void Build()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
                _rectTransform = gameObject.AddComponent<RectTransform>();

            // Default anchor — bottom-center, sits above the hotbar (which is
            // anchored at the bottom-center with y=10, height ~62). Place the
            // prompt with y = 10 + hotbar height + small gap.
            _rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _rectTransform.pivot     = new Vector2(0.5f, 0f);
            _rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _rectTransform.anchoredPosition = new Vector2(0f, 86f);

            _bg = GetComponent<Image>();
            if (_bg == null) _bg = gameObject.AddComponent<Image>();
            _bg.color = UIStyle.PanelLight;
            _bg.raycastTarget = false;
            UIBuilder.Border(_rectTransform, UIStyle.Border);

            _label = UIBuilder.Text(transform, string.Empty,
                UIStyle.FontSizeBody, UIStyle.Text, "Label");
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontStyle = FontStyles.Bold;
            RectTransform lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 2f);
            lrt.offsetMax = new Vector2(-8f, -2f);
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Show the prompt with the existing IInteractable prompt text. The
        /// caller is expected to pass a string that already includes the
        /// "[E]" prefix (the legacy <c>IInteractable.InteractPrompt</c>
        /// convention).
        /// </summary>
        public void Show(string prompt)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(prompt))
            {
                Hide();
                return;
            }
            _label.text = prompt;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Show the prompt formatted as <c>[E] {verb} {target}</c>. Used by
        /// callers that have a structured verb/target split (V4.6+ design).
        /// </summary>
        public void Show(string verb, string target)
        {
            EnsureBuilt();
            string built;
            if (string.IsNullOrEmpty(verb) && string.IsNullOrEmpty(target))
            {
                Hide();
                return;
            }
            if (string.IsNullOrEmpty(target))
            {
                built = $"[E] {verb}";
            }
            else if (string.IsNullOrEmpty(verb))
            {
                built = $"[E] {target}";
            }
            else
            {
                built = $"[E] {verb} {target}";
            }
            _label.text = built;
            gameObject.SetActive(true);
        }

        /// <summary>Hide the prompt label.</summary>
        public void Hide()
        {
            if (_label != null) _label.text = string.Empty;
            gameObject.SetActive(false);
        }

        /// <summary>Read-only view of the current label text (test introspection).</summary>
        public string CurrentText => _label != null ? _label.text : string.Empty;

        /// <summary>Read-only view of the TMP component (test introspection).</summary>
        public TextMeshProUGUI Label => _label;

        /// <summary>True while the prompt panel is visible.</summary>
        public bool IsVisible => gameObject.activeSelf;
    }
}
