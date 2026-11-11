using UnityEngine;

// A keyboard-controls legend (key caps + what they do) that only exists for a keyboard player.
//
// Hidden on a phone or tablet — PixelGUI.Handheld, the one place every "is this a phone?" question goes,
// which in the editor means the Device Simulator tab — because a touch screen has no W or Enter. Also
// hidden while a pad is the last device touched (InputGlyphs.UsingGamepad, the same rule every prompt in
// the game follows), so a pad player isn't shown keys they aren't holding; it comes back on the next key
// press. Shown in edit mode so it can be laid out.
//
// Toggles a CanvasGroup rather than the GameObject so this component keeps running to bring it back.
// First used for the title screen's bottom-right legend (Draftmaster3_UI_Unity design handoff, 2026-11-11);
// built by Draftmaster > UI > Title Keyboard Legend.
//
// A keycap that sits inside something else (the laptop's close box: [ESC] X on a PC, just X on a phone)
// can also take that box in with it: `collapseBox` drops to `collapsedWidth` while the keys are hidden, and
// each of `stretchIntoGap` has its right edge moved along by the width given up, so nothing is left
// staring at an empty strip where the keycap was.
[RequireComponent(typeof(CanvasGroup))]
public class KeyboardLegend : MonoBehaviour
{
    [Tooltip("Optional: a box this legend sits in, narrowed while the keys are hidden.")]
    public RectTransform collapseBox;
    [Tooltip("collapseBox's width while the keys are hidden.")]
    public float collapsedWidth;
    [Tooltip("Rects whose right edge follows collapseBox's left edge when it narrows.")]
    public RectTransform[] stretchIntoGap;

    CanvasGroup _group;
    float _fullWidth;
    float[] _fullRight;
    int _shown = -1;   // -1 unknown, 0 hidden, 1 shown

    public static bool KeyboardPlayer => !PixelGUI.Handheld && !InputGlyphs.UsingGamepad;

    void OnEnable()
    {
        _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        if (collapseBox != null) _fullWidth = collapseBox.sizeDelta.x;
        if (stretchIntoGap != null)
        {
            _fullRight = new float[stretchIntoGap.Length];
            for (int i = 0; i < stretchIntoGap.Length; i++)
                _fullRight[i] = stretchIntoGap[i] != null ? stretchIntoGap[i].offsetMax.x : 0f;
        }
        _shown = -1;
        Apply();
    }

    void OnDisable()
    {
        // Put the layout back as it was authored, so a scene saved mid-play is never saved collapsed.
        if (_shown == 0) Layout(true);
        _shown = -1;
    }

    void Update() => Apply();

    void Apply()
    {
        if (_group == null || !Application.isPlaying) return;
        bool show = KeyboardPlayer;
        _group.alpha = show ? 1f : 0f;
        int state = show ? 1 : 0;
        if (state == _shown) return;
        _shown = state;
        Layout(show);
    }

    void Layout(bool full)
    {
        if (collapseBox == null) return;
        float given = full ? 0f : Mathf.Max(0f, _fullWidth - collapsedWidth);
        collapseBox.sizeDelta = new Vector2(_fullWidth - given, collapseBox.sizeDelta.y);
        if (stretchIntoGap == null || _fullRight == null) return;
        for (int i = 0; i < stretchIntoGap.Length && i < _fullRight.Length; i++)
        {
            var rt = stretchIntoGap[i];
            if (rt == null) continue;
            rt.offsetMax = new Vector2(_fullRight[i] + given, rt.offsetMax.y);
        }
    }
}
