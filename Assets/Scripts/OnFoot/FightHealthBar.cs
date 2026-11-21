using UnityEngine;
using Draftmaster.Fights;

// A fighter's composure bar for the length of a scrap, drawn the way a fighting game draws it: across the top
// of the screen, the player's on the left and the rival's on the right, either side of the objective recall
// tab (WeekendObjectiveHUD.DrawRecallTab) that sits at the top centre. Each bar is anchored at its outer edge
// and drains toward the centre, so the damage on both sides piles up in the middle where the eye already is.
//
// The fill is a fixed green-to-yellow gradient — green at the centre end, the part a hit takes first, yellow
// at the outer edge — so a fighter running low is visibly down to the yellow. When a chunk is taken the
// green drops at once and the lost chunk shows red, holds for a beat so the hit reads, then drains away.
//
// IMGUI through PixelGUI, like the other on-foot HUD pieces, so it sizes off the same kit-pixel scale as the
// tab it sits beside on every screen, phone included.
public class FightHealthBar : MonoBehaviour
{
    [Tooltip("Longest a bar may be, in kit pixels (the HUD is laid out on a 640x360 grid).")]
    public float maxWidthKitPx = 220f;
    [Tooltip("Height of the coloured part of the bar, in kit pixels.")]
    public float heightKitPx = 9f;
    [Tooltip("Kit pixels from the top of the screen to the bar's frame.")]
    public float topKitPx = 6f;
    [Tooltip("Kit pixels kept clear either side of the screen centre, for the recall tab.")]
    public float centreGapKitPx = 26f;
    [Tooltip("Kit pixels kept clear at the screen's outer edge.")]
    public float edgeMarginKitPx = 8f;
    [Tooltip("Seconds the red 'just lost' chunk holds before it starts draining.")]
    public float redHoldSeconds = 0.5f;
    [Tooltip("Seconds the red chunk would take to drain a full bar.")]
    public float redDrainSeconds = 0.6f;
    [Tooltip("Colour at the centre end of the bar — the full-health end.")]
    public Color fullColour = new Color(0.30f, 0.86f, 0.32f);
    [Tooltip("Colour at the outer end of the bar — what is left when a fighter is nearly done.")]
    public Color lowColour = new Color(1f, 0.88f, 0.22f);
    [Tooltip("The chunk a hit has just taken.")]
    public Color damageColour = new Color(0.86f, 0.16f, 0.14f);

    Fighter _fighter;
    bool _left;
    float _target = 1f;   // composure right now
    float _trail = 1f;    // where the red chunk reaches to
    float _holdUntil;
    Texture2D _gradient;
    Color _builtFull, _builtLow;
    GUIStyle _nameStyle;

    // The player's bar goes on the left, everybody else's on the right.
    public static FightHealthBar Attach(Fighter fighter) =>
        Attach(fighter, fighter != null && fighter.isPlayerControlled);

    public static FightHealthBar Attach(Fighter fighter, bool leftSide)
    {
        if (fighter == null) return null;
        var go = new GameObject($"FightHealthBar ({fighter.displayName})");
        var bar = go.AddComponent<FightHealthBar>();
        bar._fighter = fighter;
        bar._left = leftSide;
        bar.SetFraction(FightRules.HealthFraction(fighter.Health), true);
        fighter.HealthChanged += bar.OnHealthChanged;
        return bar;
    }

    public void Detach()
    {
        if (_fighter != null) _fighter.HealthChanged -= OnHealthChanged;
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_fighter != null) _fighter.HealthChanged -= OnHealthChanged;
        if (_gradient != null) Destroy(_gradient);
    }

    void OnHealthChanged(Fighter fighter, float health, float max)
        => SetFraction(max > 0f ? Mathf.Clamp01(health / max) : 0f, false);

    public void SetFraction(float fraction, bool instant)
    {
        fraction = Mathf.Clamp01(fraction);
        if (instant || fraction >= _trail)
        {
            // A reset or a recovery: no damage to show.
            _target = _trail = fraction;
            return;
        }
        // A hit. The red runs from the new value up to wherever the last red chunk still reached, so a
        // combo stacks into one long red stretch, and the hold restarts on each blow.
        _target = fraction;
        _holdUntil = Time.unscaledTime + redHoldSeconds;
    }

    void Update()
    {
        if (_fighter == null) { Destroy(gameObject); return; }
        if (_trail > _target && Time.unscaledTime >= _holdUntil)
        {
            float rate = redDrainSeconds > 0f ? Time.unscaledDeltaTime / redDrainSeconds : 1f;
            _trail = Mathf.MoveTowards(_trail, _target, rate);
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (_fighter == null || RacePauseMenu.IsPaused || PhoneUI.IsOpen) return;

        float centre = Screen.width * 0.5f;
        float gap = PixelGUI.Px(centreGapKitPx);
        float border = PixelGUI.Px(1f);
        float width = Mathf.Round(Mathf.Min(PixelGUI.Px(maxWidthKitPx), centre - gap - PixelGUI.Px(edgeMarginKitPx)));
        float height = PixelGUI.Px(heightKitPx);
        if (width <= border * 4f) return;

        // The coloured area; the frame is drawn around it.
        float x = _left ? Mathf.Round(centre - gap - width) : Mathf.Round(centre + gap);
        var inner = new Rect(x, PixelGUI.Px(topKitPx) + border * 2f, width, height);

        // Frame: an ink outline, a gold rim, the deep plate as the empty track.
        PixelGUI.Fill(Grow(inner, border * 2f), PixelGUI.Ink);
        PixelGUI.Fill(Grow(inner, border), PixelGUI.Gold);
        PixelGUI.Fill(inner, PixelGUI.PlateDeep);

        // Both bars are anchored at the outer edge. The red chunk sits just past the fill, toward the centre.
        PixelGUI.Fill(Span(inner, 0f, _trail), damageColour);
        DrawGradient(Span(inner, 0f, _target), _target);

        // A one-pixel highlight along the top of whatever is lit, for a bit of shine.
        var lit = Span(inner, 0f, Mathf.Max(_target, _trail));
        if (lit.width > 0f) PixelGUI.Fill(new Rect(lit.x, lit.y, lit.width, border), new Color(1f, 1f, 1f, 0.28f));

        DrawName(inner, border);
    }

    // The part of the bar from `from` to `to` (0..1 measured from the outer edge).
    Rect Span(Rect bar, float from, float to)
    {
        float a = Mathf.Round(bar.width * Mathf.Clamp01(from));
        float b = Mathf.Round(bar.width * Mathf.Clamp01(to));
        if (b <= a) return new Rect(bar.x, bar.y, 0f, bar.height);
        return _left
            ? new Rect(bar.x + a, bar.y, b - a, bar.height)
            : new Rect(bar.xMax - b, bar.y, b - a, bar.height);
    }

    static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

    // The gradient is fixed to the bar, not squashed into the fill: the outer edge is always yellow and the
    // centre end always green, so as the fill shrinks the green goes first.
    void DrawGradient(Rect r, float fraction)
    {
        if (r.width <= 0f) return;
        var tex = Gradient();
        // Texture u runs yellow (0) to green (1); the fill covers u 0..fraction. The right bar's outer edge is
        // its right-hand end, so its u runs the other way across the rect.
        var uv = _left ? new Rect(0f, 0f, fraction, 1f) : new Rect(fraction, 0f, -fraction, 1f);
        GUI.DrawTextureWithTexCoords(r, tex, uv);
    }

    Texture2D Gradient()
    {
        if (_gradient != null && _builtFull == fullColour && _builtLow == lowColour) return _gradient;
        if (_gradient == null)
        {
            _gradient = new Texture2D(64, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // stepped, so it stays pixel art at every scale
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
        for (int i = 0; i < _gradient.width; i++)
            _gradient.SetPixel(i, 0, Color.Lerp(lowColour, fullColour, i / (float)(_gradient.width - 1)));
        _gradient.Apply();
        _builtFull = fullColour;
        _builtLow = lowColour;
        return _gradient;
    }

    // The driver's name under the bar, flush with its outer edge, with a hard ink drop shadow.
    void DrawName(Rect bar, float border)
    {
        if (_nameStyle == null)
            _nameStyle = new GUIStyle(PixelGUI.Label) { wordWrap = false, clipping = TextClipping.Overflow };
        _nameStyle.alignment = _left ? TextAnchor.UpperLeft : TextAnchor.UpperRight;

        string name = _fighter.displayName;
        float h = _nameStyle.CalcSize(new GUIContent(name)).y;
        var r = new Rect(bar.x, bar.yMax + border * 2f + PixelGUI.Px(3f), bar.width, h);

        var colour = _nameStyle.normal.textColor;
        _nameStyle.normal.textColor = PixelGUI.Ink;
        GUI.Label(new Rect(r.x + border, r.y + border, r.width, r.height), name, _nameStyle);
        _nameStyle.normal.textColor = colour;
        GUI.Label(r, name, _nameStyle);
    }
}
