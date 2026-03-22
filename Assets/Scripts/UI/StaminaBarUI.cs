using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Player;

/// <summary>
/// Thin stamina bar above the hotbar.
/// Shrinks symmetrically from both ends toward the center.
/// Flashes red when fully depleted. Fades out when full.
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    private FirstPersonController _controller;
    private Image        _fill;
    private RectTransform _fillRT;
    private CanvasGroup  _group;

    // How far inward each side can retract (half the usable width minus padding)
    // Computed in Build() from the bar's width.
    private float _maxRetract;

    // Minimum half-width left at center when stamina = 0
    private const float MinHalfWidth = 6f;
    private const float Padding      = 2f;
    private const float FadeSpeed    = 4f;

    private static readonly Color ColFull     = new Color(0.22f, 0.78f, 0.35f, 1f);
    private static readonly Color ColEmpty    = new Color(0.9f,  0.25f, 0.10f, 1f);
    private static readonly Color ColFlashOn  = new Color(1f,   0.1f,  0.1f,  1f);

    private bool  _wasEmpty;
    private Coroutine _flashRoutine;

    public void Init(FirstPersonController controller)
    {
        _controller = controller;
        Build();
    }

    private void Build()
    {
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        // Dark background
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        // Fill quad — we manipulate offsetMin/offsetMax to shrink from both sides
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(transform, false);

        _fill  = fillGO.GetComponent<Image>();
        _fill.color = ColFull;
        _fill.raycastTarget = false;

        _fillRT = fillGO.GetComponent<RectTransform>();
        _fillRT.anchorMin = Vector2.zero;
        _fillRT.anchorMax = Vector2.one;
        _fillRT.offsetMin = new Vector2(Padding, Padding);
        _fillRT.offsetMax = new Vector2(-Padding, -Padding);

        // Total usable half-width: barWidth/2 - Padding - MinHalfWidth
        // barWidth is set by UIManager to 200
        float halfUsable = 100f - Padding - MinHalfWidth;
        _maxRetract = halfUsable;
    }

    private void Update()
    {
        if (_controller == null || _fill == null) return;

        float ratio = Mathf.Clamp01(_controller.CurrentStamina / _controller.MaxStamina);

        // Retract both sides symmetrically: 0=full bar, _maxRetract=only centre nub left
        float retract = (1f - ratio) * _maxRetract;
        _fillRT.offsetMin = new Vector2(Padding + retract, Padding);
        _fillRT.offsetMax = new Vector2(-Padding - retract, -Padding);

        // Colour: green → red as stamina drops (skip during flash)
        if (_flashRoutine == null)
            _fill.color = Color.Lerp(ColEmpty, ColFull, ratio);

        // Flash when newly depleted
        bool isEmpty = ratio <= 0.001f;
        if (isEmpty && !_wasEmpty)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRed());
        }
        _wasEmpty = isEmpty;

        // Fade out when full
        float targetAlpha = ratio < 0.999f ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, FadeSpeed * Time.deltaTime);
    }

    private IEnumerator FlashRed()
    {
        int flashes = 3;
        float halfPeriod = 0.12f;
        for (int i = 0; i < flashes; i++)
        {
            _fill.color = ColFlashOn;
            yield return new WaitForSeconds(halfPeriod);
            _fill.color = ColEmpty;
            yield return new WaitForSeconds(halfPeriod);
        }
        _flashRoutine = null;
    }
}
