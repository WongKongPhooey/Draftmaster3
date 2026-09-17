using Draftmaster.Controls;
using UnityEngine;

// Rides on a world-space prompt icon (InputPromptIcon) and keeps it showing the device in the player's hands:
// the keycap on the keyboard, the pad's own button once a pad is picked up. Checks InputGlyphs.Version rather
// than re-resolving the sprite every frame, so a paddock full of prompts costs one int compare each.
public class InputPromptGlyph : MonoBehaviour
{
    SpriteRenderer _renderer;
    Sprite _keyboard;
    PadButton _pad;
    float _worldHeight;
    int _version = -1;

    public static InputPromptGlyph Attach(SpriteRenderer sr, Sprite keyboard, PadButton pad, float worldHeight)
    {
        var g = sr.gameObject.AddComponent<InputPromptGlyph>();
        g._renderer = sr;
        g._keyboard = keyboard;
        g._pad = pad;
        g._worldHeight = worldHeight;
        g.Apply();
        return g;
    }

    void LateUpdate()
    {
        if (_version != InputGlyphs.Version) Apply();
    }

    void Apply()
    {
        _version = InputGlyphs.Version;
        if (_renderer == null) return;

        var sprite = InputGlyphs.UsingGamepad ? InputGlyphs.Icon(_pad) : null;
        if (sprite == null) sprite = _keyboard;
        if (_renderer.sprite == sprite) return;

        _renderer.sprite = sprite;
        InputPromptIcon.Fit(transform, sprite, _worldHeight);
    }
}
