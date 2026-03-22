using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Lower-third dialogue panel.
/// Subscribes to DialogueRunner events and renders speaker name, typewriter text,
/// and 1-4 response buttons. Created procedurally by UIManager.BuildDialogueUI().
/// </summary>
public class DialogueUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  References (wired by BuildPanel)
    // ---------------------------------------------------------------

    private Text      _speakerText;
    private Text      _bodyText;
    private Button[]  _choiceButtons;
    private Text[]    _choiceLabels;
    private GameObject _continueHint;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------

    private Coroutine _typeCoroutine;
    private string    _fullText;
    private bool      _typingDone;
    private List<int> _visibleChoices;
    private DialogueNode _pendingNode;

    private const float TypewriterSpeed = 35f;  // chars/sec
    private const float PanelHeight     = 240f;

    // ---------------------------------------------------------------
    //  Initialization (called by UIManager after AddComponent)
    // ---------------------------------------------------------------

    public void Init(Canvas parentCanvas)
    {
        BuildPanel();
        gameObject.SetActive(false);

        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnNodePresented += HandleNodePresented;
            DialogueRunner.Instance.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnDestroy()
    {
        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnNodePresented -= HandleNodePresented;
            DialogueRunner.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    // ---------------------------------------------------------------
    //  Update — keyboard shortcuts
    // ---------------------------------------------------------------

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (Keyboard.current == null) return;

        // Space — skip typewriter or continue (no-choice node)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!_typingDone)
                FinishTypewriter();
            else
                DialogueRunner.Instance?.RequestSkip();
        }

        // ESC — exit dialogue
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            DialogueRunner.Instance?.EndDialogue();

        // Number keys 1-4 for choice selection
        if (_typingDone && _visibleChoices != null)
        {
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            for (int i = 0; i < keys.Length && i < _visibleChoices.Count; i++)
            {
                if (Keyboard.current[keys[i]].wasPressedThisFrame)
                {
                    DialogueRunner.Instance?.SelectChoice(i);
                    return;
                }
            }
        }
    }

    // ---------------------------------------------------------------
    //  DialogueRunner callbacks
    // ---------------------------------------------------------------

    private void HandleNodePresented(DialogueNode node, List<int> visibleChoices)
    {
        gameObject.SetActive(true);
        _pendingNode    = node;
        _visibleChoices = visibleChoices;

        // Speaker name
        string speaker = !string.IsNullOrEmpty(node.speakerName)
            ? node.speakerName
            : DialogueRunner.Instance?.CurrentNPC?.npcName ?? "???";
        _speakerText.text = speaker;

        // Hide choices until typing finishes
        SetChoicesVisible(false);
        if (_continueHint != null) _continueHint.SetActive(false);

        // Start typewriter
        _fullText   = node.text;
        _typingDone = false;
        _bodyText.text = "";
        if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
        _typeCoroutine = StartCoroutine(TypewriterRoutine(_fullText));
    }

    private void HandleDialogueEnded()
    {
        if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  Typewriter
    // ---------------------------------------------------------------

    private IEnumerator TypewriterRoutine(string text)
    {
        float interval = 1f / TypewriterSpeed;
        foreach (char c in text)
        {
            _bodyText.text += c;
            yield return new WaitForSeconds(interval);
        }
        OnTypingComplete();
    }

    private void FinishTypewriter()
    {
        if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
        _bodyText.text = _fullText;
        OnTypingComplete();
    }

    private void OnTypingComplete()
    {
        _typingDone = true;
        bool hasChoices = _visibleChoices != null && _visibleChoices.Count > 0;

        if (hasChoices && _pendingNode != null)
            SetChoicesVisible(true);

        if (_continueHint != null)
            _continueHint.SetActive(!hasChoices);
    }

    // ---------------------------------------------------------------
    //  Choice button display
    // ---------------------------------------------------------------

    private void SetChoicesVisible(bool show)
    {
        for (int i = 0; i < _choiceButtons.Length; i++)
        {
            bool active = show
                       && _visibleChoices != null
                       && i < _visibleChoices.Count
                       && _pendingNode != null;

            _choiceButtons[i].gameObject.SetActive(active);

            if (!active) continue;

            int realIdx = _visibleChoices[i];
            _choiceLabels[i].text = $"{i + 1}.  {_pendingNode.choices[realIdx].text}";

            int captured = i;
            _choiceButtons[i].onClick.RemoveAllListeners();
            _choiceButtons[i].onClick.AddListener(() =>
                DialogueRunner.Instance?.SelectChoice(captured));
        }
    }

    // ---------------------------------------------------------------
    //  Procedural UI construction
    // ---------------------------------------------------------------

    private void BuildPanel()
    {
        // ---- Root RT + background ----
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, PanelHeight);

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

        // ---- Speaker bar (top strip) ----
        var speakerBar = MakeChild("SpeakerBar");
        var sbRT = speakerBar.GetComponent<RectTransform>();
        sbRT.anchorMin        = new Vector2(0f, 1f);
        sbRT.anchorMax        = new Vector2(1f, 1f);
        sbRT.pivot            = new Vector2(0.5f, 1f);
        sbRT.anchoredPosition = Vector2.zero;
        sbRT.sizeDelta        = new Vector2(0f, 30f);
        var sbBg = speakerBar.AddComponent<Image>();
        sbBg.color = new Color(0.1f, 0.1f, 0.22f, 0.97f);

        var speakerTextGO = MakeChildOf(speakerBar, "SpeakerText");
        _speakerText = AddText(speakerTextGO, 16, FontStyle.Bold, new Color(0.9f, 0.85f, 0.5f), TextAnchor.MiddleLeft);
        var sRT = _speakerText.GetComponent<RectTransform>();
        sRT.anchorMin = Vector2.zero; sRT.anchorMax = Vector2.one;
        sRT.offsetMin = new Vector2(12f, 0f); sRT.offsetMax = new Vector2(-8f, 0f);

        // ---- Body text ----
        var bodyGO = MakeChild("BodyText");
        var bRT    = bodyGO.GetComponent<RectTransform>();
        bRT.anchorMin        = new Vector2(0f, 1f);
        bRT.anchorMax        = new Vector2(1f, 1f);
        bRT.pivot            = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -32f);
        bRT.sizeDelta        = new Vector2(-24f, 108f);
        _bodyText = AddText(bodyGO, 15, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow   = VerticalWrapMode.Overflow;

        // ---- Choice buttons (max 4) ----
        _choiceButtons = new Button[4];
        _choiceLabels  = new Text[4];
        float btnH  = 24f;
        float startY = -143f;
        for (int i = 0; i < 4; i++)
        {
            var btnGO  = MakeChild($"Choice{i + 1}");
            var btnRT  = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin        = new Vector2(0.03f, 1f);
            btnRT.anchorMax        = new Vector2(0.97f, 1f);
            btnRT.pivot            = new Vector2(0.5f, 1f);
            btnRT.anchoredPosition = new Vector2(0f, startY - i * (btnH + 3f));
            btnRT.sizeDelta        = new Vector2(0f, btnH);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.14f, 0.24f, 0.92f);

            var btn = btnGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.14f, 0.14f, 0.24f, 1f);
            cb.highlightedColor = new Color(0.24f, 0.26f, 0.42f, 1f);
            cb.pressedColor     = new Color(0.08f, 0.08f, 0.16f, 1f);
            btn.colors          = cb;
            btn.targetGraphic   = btnImg;

            var lblGO = MakeChildOf(btnGO, "Label");
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(10f, 0f); lblRT.offsetMax = new Vector2(-8f, 0f);
            var lbl = AddText(lblGO, 13, FontStyle.Normal, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleLeft);

            _choiceButtons[i] = btn;
            _choiceLabels[i]  = lbl;
            btnGO.SetActive(false);
        }

        // ---- Continue hint ----
        _continueHint = MakeChild("ContinueHint");
        var hRT = _continueHint.GetComponent<RectTransform>();
        hRT.anchorMin        = new Vector2(1f, 0f);
        hRT.anchorMax        = new Vector2(1f, 0f);
        hRT.pivot            = new Vector2(1f, 0f);
        hRT.anchoredPosition = new Vector2(-12f, 8f);
        hRT.sizeDelta        = new Vector2(240f, 20f);
        var hint = AddText(_continueHint, 12, FontStyle.Normal,
            new Color(0.55f, 0.55f, 0.55f), TextAnchor.LowerRight);
        hint.text = "[ Space ] Continue   [ Esc ] Exit";
        _continueHint.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private GameObject MakeChild(string childName)
        => MakeChildOf(gameObject, childName);

    private static GameObject MakeChildOf(GameObject parent, string childName)
    {
        var go = new GameObject(childName, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static Text AddText(GameObject go, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        var t = go.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize  = size;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = anchor;
        return t;
    }
}
