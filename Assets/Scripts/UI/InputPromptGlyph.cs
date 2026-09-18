using Draftmaster.Controls;
using UnityEngine;

// Rides on every world-space prompt icon (InputPromptIcon) and keeps two things right about it.
//
// Which art: the keycap on the keyboard, the pad's own button once a pad is picked up. Checks
// InputGlyphs.Version rather than re-resolving the sprite every frame, so a paddock full of prompts costs one
// int compare each.
//
// How big: the same size ON SCREEN wherever it is. Prompts used to be sized in world metres under whatever
// they were parented to, so the one on the parked car (a child of a transform scaled 6x) came out five metres
// tall, while the laptop and satnav in the RV came out smaller than the paddock's NPCs even though all of
// them asked for the same height. A key prompt is UI: it is sized off the camera, as a fraction of the screen
// height, snapped to a whole number of screen pixels per art pixel so the 16px keycap stays crisp, and with
// its parents' scale cancelled out.
public class InputPromptGlyph : MonoBehaviour
{
    SpriteRenderer _renderer;
    Sprite _keyboard;
    PadButton _pad;
    float _fallbackWorldHeight;
    int _version = -1;
    float _appliedHeight = -1f;

    public static InputPromptGlyph Attach(SpriteRenderer sr, Sprite keyboard, PadButton pad, float fallbackWorldHeight)
    {
        var g = sr.gameObject.AddComponent<InputPromptGlyph>();
        g._renderer = sr;
        g._keyboard = keyboard;
        g._pad = pad;
        g._fallbackWorldHeight = fallbackWorldHeight;
        g.ApplyArt();
        g.ApplySize();
        return g;
    }

    void LateUpdate()
    {
        if (_version != InputGlyphs.Version) ApplyArt();
        ApplySize();
    }

    void ApplyArt()
    {
        _version = InputGlyphs.Version;
        if (_renderer == null) return;

        var sprite = _pad != PadButton.None && InputGlyphs.UsingGamepad ? InputGlyphs.Icon(_pad) : null;
        if (sprite == null) sprite = _keyboard;
        if (_renderer.sprite == sprite) return;

        _renderer.sprite = sprite;
        _appliedHeight = -1f;   // new art, new pixel grid: re-fit
    }

    // Cheap when nothing moved: the camera zoom and the parents' scale are compared against what was last
    // applied, and the transform is only written when the answer changes.
    void ApplySize()
    {
        if (_renderer == null || _renderer.sprite == null) return;
        float height = InputPromptIcon.ScreenHeightInWorld(Camera.main, transform.position, _renderer.sprite,
                                                           _fallbackWorldHeight);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float key = height / Mathf.Max(1e-5f, Mathf.Abs(parentScale.y));
        if (Mathf.Abs(key - _appliedHeight) <= _appliedHeight * 1e-3f) return;
        _appliedHeight = key;
        InputPromptIcon.Fit(transform, _renderer.sprite, height);
    }
}
