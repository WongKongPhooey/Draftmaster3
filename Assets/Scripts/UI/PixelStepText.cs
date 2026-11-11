using TMPro;
using UnityEngine;

// Sizes a pixel-font TMP text in whole-screen-pixel steps instead of one fixed point size.
//
// A pixel face is only sharp when each of its font pixels covers a whole number of screen pixels. On a
// canvas scaled by an integer s (640x360 kit canvas: 3 at 1080p, 4 at 1440p), size `nativePx` puts s
// screen pixels on every font pixel. One step up is s + 1 of them, which is a different design size at
// every scale:
//
//     fontSize = nativePx * (s + step) / s        16px monogram, step 1: 21.33 at 1080p, 20 at 1440p
//
// So the size is worked out from the live canvas scale rather than authored. Whole multiples of nativePx
// (step = s, 2s...) are the only fixed sizes that stay crisp at every scale, and the first of those
// doubles the text. From the Draftmaster3_UI_Unity design handoff (its IronOvalPixelText), 2026-11-11.
//
// Needs an integer canvas scale to be crisp at all; at a fractional one (a window 900 px tall) it still
// rounds the scale and nothing on that canvas is pixel-exact anyway. No TMP auto-size alongside it.
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class PixelStepText : MonoBehaviour
{
    [Tooltip("The font's native pixel grid (monogram 16, Silkscreen 8).")]
    public int nativePx = 16;
    [Tooltip("Extra screen pixels per font pixel. 0 = native size.")]
    public int step = 1;

    TMP_Text _text;
    Canvas _canvas;
    int _lastScale = -1, _lastStep = int.MinValue, _lastNative = -1;

    void OnEnable()
    {
        _text = GetComponent<TMP_Text>();
        _canvas = GetComponentInParent<Canvas>();
        _lastScale = -1;
        Apply();
    }

    void OnValidate() => _lastScale = -1;

    void LateUpdate() => Apply();

    public void Apply()
    {
        if (_text == null) return;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        float raw = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
        int scale = Mathf.Max(1, Mathf.RoundToInt(raw));
        if (scale == _lastScale && step == _lastStep && nativePx == _lastNative) return;
        _lastScale = scale; _lastStep = step; _lastNative = nativePx;

        _text.enableAutoSizing = false;
        _text.fontSize = nativePx * (float)Mathf.Max(1, scale + step) / scale;
    }
}
