using TMPro;
using UnityEngine;
using Draftmaster.Fights;

// The composure bar that floats over a fighter's head for the length of a scrap. Built the same way as
// SpeechBubble — an independent world-space object made of SpriteRenderers plus a TextMeshPro name, rather
// than a world-space Canvas — because the spline scenes render through the 3D URP renderer where a Canvas
// needs wiring and a font asset, and because the fighter's own transform rotates as they turn to face each
// other (a parented bar would spin with them).
//
// Art comes from the shared PixelUITheme bar sprites when the kit is installed, so a fight bar matches the
// menus; without it, a flat procedural plate is used.
public class FightHealthBar : MonoBehaviour
{
    [Tooltip("Bar width in world metres. The on-foot characters are ~0.6m tall, so this is deliberately wider than the body — it reads as UI, not as a prop.")]
    public float width = 1.1f;
    [Tooltip("Bar height in world metres.")]
    public float height = 0.13f;
    [Tooltip("World metres above the fighter's position the bar floats.")]
    public float headHeight = 0.95f;
    [Tooltip("How far toward the camera the bar sits, so the opaque ground plane can't depth-cull it.")]
    public float zLift = 0.6f;
    [Tooltip("Seconds the fill takes to catch up to a change — a hit reads as a drain rather than a jump.")]
    public float drainSeconds = 0.25f;
    [Tooltip("Name text size in world metres.")]
    public float nameSize = 0.16f;

    Fighter _fighter;
    SpriteRenderer _track, _fill;
    TextMeshPro _nameLabel;
    float _shown = 1f, _target = 1f;
    float _nameScale = 1f;

    public static FightHealthBar Attach(Fighter fighter)
    {
        if (fighter == null) return null;
        var go = new GameObject($"FightHealthBar ({fighter.displayName})");
        var bar = go.AddComponent<FightHealthBar>();
        bar._fighter = fighter;
        bar.Build();
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
    }

    void OnHealthChanged(Fighter fighter, float health, float max)
        => SetFraction(max > 0f ? Mathf.Clamp01(health / max) : 0f, false);

    public void SetFraction(float fraction, bool instant)
    {
        _target = Mathf.Clamp01(fraction);
        if (instant) _shown = _target;
    }

    void Build()
    {
        var theme = PixelUITheme.Instance;

        _track = MakeBar("Track", theme != null ? theme.barTrack : null,
                         theme != null ? theme.plateDeep : new Color(0.06f, 0.07f, 0.1f, 0.95f), 70);
        _fill = MakeBar("Fill", theme != null ? theme.barGold : null,
                        theme != null ? theme.gold : new Color(1f, 0.83f, 0.42f, 1f), 71);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(transform, false);
        _nameLabel = nameGo.AddComponent<TextMeshPro>();
        if (theme != null && theme.body != null)
        {
            _nameLabel.font = theme.body;
            if (theme.body.material != null) _nameLabel.fontSharedMaterial = theme.body.material;
        }
        _nameLabel.alignment = TextAlignmentOptions.Center;
        _nameLabel.enableWordWrapping = false;
        _nameLabel.color = theme != null ? theme.text : Color.white;
        _nameLabel.text = _fighter != null ? _fighter.displayName : "";
        var nameRenderer = nameGo.GetComponent<MeshRenderer>();
        nameRenderer.sortingLayerName = "Vehicles";
        nameRenderer.sortingOrder = 72;
        _nameScale = FitToMetres(_nameLabel, nameSize);
        _nameLabel.rectTransform.sizeDelta = _nameLabel.GetPreferredValues();
        nameGo.transform.localPosition = new Vector3(0f, height * 0.5f + nameSize * 0.7f, -0.02f);
    }

    SpriteRenderer MakeBar(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : PlateSprite();
        sr.color = color;
        sr.sortingLayerName = "Vehicles";
        sr.sortingOrder = order;
        // Sliced draw needs a 9-slice border; without one the renderer ignores `size` and draws the sprite
        // at its native (tiny) world size, exactly as SpeechBubble has to guard against.
        sr.drawMode = sr.sprite != null && sr.sprite.border.sqrMagnitude > 0f
            ? SpriteDrawMode.Sliced
            : SpriteDrawMode.Simple;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) sr.sharedMaterial = new Material(sh);
        return sr;
    }

    void LateUpdate()
    {
        if (_fighter == null) { Destroy(gameObject); return; }

        transform.position = _fighter.transform.position + Vector3.up * headHeight + Vector3.back * zLift;
        transform.rotation = Quaternion.identity;   // never inherit the fighter's facing spin

        _shown = drainSeconds <= 0f
            ? _target
            : Mathf.MoveTowards(_shown, _target, Time.deltaTime / drainSeconds);

        Size(_track, width, height, 0f);
        // The fill shrinks from the left edge, so it slides left rather than shrinking about its centre.
        float fillWidth = Mathf.Max(0.0001f, width * _shown);
        Size(_fill, fillWidth, height, -(width - fillWidth) * 0.5f);
        _fill.color = FillColor(_shown);
        _fill.enabled = _shown > 0.001f;
    }

    void Size(SpriteRenderer sr, float w, float h, float xOffset)
    {
        if (sr == null) return;
        sr.transform.localPosition = new Vector3(xOffset, 0f, sr == _fill ? -0.01f : 0f);
        if (sr.drawMode == SpriteDrawMode.Sliced)
        {
            sr.size = new Vector2(w, h);
            sr.transform.localScale = Vector3.one;
        }
        else
        {
            var s = sr.sprite;
            float nw = s != null ? s.rect.width / s.pixelsPerUnit : 1f;
            float nh = s != null ? s.rect.height / s.pixelsPerUnit : 1f;
            sr.transform.localScale = new Vector3(nw > 0.0001f ? w / nw : 1f, nh > 0.0001f ? h / nh : 1f, 1f);
        }
    }

    // Gold while composed, amber as it goes, red when they're nearly done — the same status ramp the
    // rest of the UI uses, so "this fight is nearly over" reads at a glance.
    static Color FillColor(float fraction)
    {
        var theme = PixelUITheme.Instance;
        if (fraction > 0.5f) return theme != null ? theme.gold : new Color(1f, 0.83f, 0.42f);
        if (fraction > 0.25f) return theme != null ? theme.caution : new Color(0.91f, 0.55f, 0.18f);
        return theme != null ? theme.danger : new Color(0.9f, 0.28f, 0.3f);
    }

    // Same measure-and-scale trick SpeechBubble uses: TMP's fontSize is not world metres, so render a probe
    // at the font's native point size and scale by what it actually measured.
    static float FitToMetres(TextMeshPro label, float metres)
    {
        float pointSize = label.font != null && label.font.faceInfo.pointSize > 0 ? label.font.faceInfo.pointSize : 16f;
        label.fontSize = pointSize;

        string previous = label.text;
        label.text = "Ag";
        label.ForceMeshUpdate();
        float measured = label.GetPreferredValues().y;
        label.text = previous;
        label.ForceMeshUpdate();

        float scale = measured > 0.0001f ? metres / measured : 1f;
        label.transform.localScale = new Vector3(scale, scale, 1f);
        return scale;
    }

    // Flat 9-sliced white plate, used when the pixel UI kit isn't installed.
    static Sprite _plate;
    static Sprite PlateSprite()
    {
        if (_plate != null) return _plate;
        int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        _plate = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s,
                               0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return _plate;
    }
}
