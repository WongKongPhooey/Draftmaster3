using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

// World-space speech bubble that hovers above an actor and reveals its line letter-by-letter (typewriter),
// reproducing the original Phoenix dialogue feel. Built from a TextMesh + a 9-sliced sprite so it renders
// reliably under this scene's 3D URP renderer (a world-space UI Canvas is finicky here). It is an independent
// object (not parented to the actor) so the actor's scale or facing rotation never distorts it.
//
// The box is sized to the FULL line up front (not auto-grown while typing) and the text is left-aligned
// inside it, dialogue-box style. The background sprite is loaded from Resources/OnFoot/SpeechBox.png when
// present — texture that PNG to restyle every bubble (keep its 9-slice border import settings; regenerate
// a fresh placeholder via Draftmaster > UI > Build Speech Box Texture). Without it, a procedural rounded
// rect is used.
public class SpeechBubble : MonoBehaviour
{
    public float charInterval = 0.04f;             // seconds per character
    public float headHeight = 0.75f;               // world metres above the actor's position
    public int wrapChars = 34;                     // soft word-wrap width (characters per line)
    public Vector2 padding = new Vector2(0.18f, 0.12f); // box border around the text block, per side
    [Tooltip("Gap (world m) between the top of the box and the speaker's name sat above it.")]
    public float nameGap = 0.045f;
    [Tooltip("Colour of the speaker-name line above the box.")]
    public Color nameColor = new Color(1f, 0.83f, 0.42f, 1f);
    [Tooltip("How far in front of the actor (negative z, toward the camera) the bubble sits. Must clear whatever the actor is standing on — the opaque z=0 ground depth-tests the box away without this.")]
    public float zLift = 0.6f;
    [Tooltip("Dialogue text size in world units. The bubble floats above a 0.625m-tall character, so it " +
             "is deliberately smaller than the world's own pixel scale — it is a UI element in the scene, " +
             "not painted onto it.")]
    public float textSize = 0.22f;

    Transform _actor;
    TextMeshPro _label, _nameLabel;
    MeshRenderer _labelRenderer;
    SpriteRenderer _bg;
    SpriteRenderer _caret;                          // blinking "press to continue" marker
    Coroutine _reveal;
    string _full = "";
    Vector2 _boxSize = new Vector2(0.55f, 0.4f);
    float _labelScale = 1f, _nameScale = 1f;        // TMP local units -> world metres, measured in Build

    public bool IsRevealing { get; private set; }

    public static SpeechBubble Attach(Transform actor)
    {
        var go = new GameObject("SpeechBubble (" + actor.name + ")");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.UI); // world-space, but it is UI - keep it out of the scene root
        var sb = go.AddComponent<SpeechBubble>();
        sb._actor = actor;
        sb.Build();
        go.SetActive(false);
        return sb;
    }

    void Build()
    {
        var theme = PixelUITheme.Instance;

        _bg = new GameObject("BG").AddComponent<SpriteRenderer>();
        _bg.transform.SetParent(transform, false);
        _bg.transform.localPosition = new Vector3(0f, 0f, 0.01f); // just behind the text
        // Prefer the shared pixel-UI window so a paddock conversation is framed in the same plate as
        // every menu, instead of a one-off bubble that only this system knows about.
        var themed = theme != null ? theme.window : null;
        var custom = themed != null ? themed : Resources.Load<Sprite>("OnFoot/SpeechBox");
        _bg.sprite = custom != null ? custom : BubbleSprite();
        // Sliced draw needs a 9-slice border on the sprite; without one the renderer ignores `size` and
        // draws at the sprite's native world size, which for a 24px plate is a fingernail-sized box.
        if (_bg.sprite != null && _bg.sprite.border.sqrMagnitude > 0f)
        {
            _bg.drawMode = SpriteDrawMode.Sliced;
        }
        else
        {
            _bg.drawMode = SpriteDrawMode.Simple;
            Debug.LogWarning($"SpeechBubble: '{(_bg.sprite != null ? _bg.sprite.name : "null")}' has no " +
                             "9-slice border, so the dialogue plate cannot stretch. Re-run " +
                             "Draftmaster > Art > Set Up Pixel UI Kit.", this);
        }
        // A hand-textured box is shown as authored; the procedural fallback gets the dark tint.
        _bg.color = custom != null ? Color.white : new Color(0.06f, 0.06f, 0.09f, 0.92f);
        _bg.sortingLayerName = "Vehicles";
        _bg.sortingOrder = 60;
        SetUnlit(_bg);

        var labelGo = new GameObject("Text");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        // TextMeshPro rather than TextMesh: TextMesh can only take a dynamic Font, which renders
        // anti-aliased and fights the pixel art. TMP can carry the RASTER bitmap font from the theme,
        // so dialogue is drawn from the same pixel grid as everything else.
        _label = labelGo.AddComponent<TextMeshPro>();
        ApplyFont(_label, theme != null ? theme.body : null);
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.enableWordWrapping = false;   // wrapping is done up front so the box can be pre-sized
        _label.color = theme != null ? theme.text : Color.white;
        _labelRenderer = labelGo.GetComponent<MeshRenderer>();
        _labelRenderer.sortingLayerName = "Vehicles";
        _labelRenderer.sortingOrder = 61;
        _labelScale = FitToMetres(_label, textSize);

        // Who's talking, in a smaller face sat above the box — so a paddock full of named drivers
        // reads at a glance without a UI panel. Positioned in Speak, once the box is sized.
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(transform, false);
        _nameLabel = nameGo.AddComponent<TextMeshPro>();
        ApplyFont(_nameLabel, theme != null ? theme.body : null);
        _nameLabel.alignment = TextAlignmentOptions.TopLeft;
        _nameLabel.enableWordWrapping = false;
        _nameLabel.color = theme != null ? theme.gold : nameColor;
        var nameRenderer = nameGo.GetComponent<MeshRenderer>();
        nameRenderer.sortingLayerName = "Vehicles";
        nameRenderer.sortingOrder = 61;
        _nameScale = FitToMetres(_nameLabel, textSize * 0.75f);

        // JRPG furniture: a marker in the box's bottom-right that appears once the line has finished
        // typing, so the player can tell "still talking" from "waiting on you".
        if (theme != null && theme.cursor != null)
        {
            _caret = new GameObject("Caret").AddComponent<SpriteRenderer>();
            _caret.transform.SetParent(transform, false);
            _caret.sprite = theme.cursor;
            _caret.color = theme.gold;
            _caret.sortingLayerName = "Vehicles";
            _caret.sortingOrder = 62;
            // The cursor art points right; rotate it to point down, the usual "continue" direction.
            _caret.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            SetUnlit(_caret);
            _caret.enabled = false;
        }
    }

    static void ApplyFont(TextMeshPro label, TMP_FontAsset font)
    {
        if (font == null) return;
        label.font = font;
        // The theme's font ships a point-filtered bitmap material; using it directly keeps glyph edges
        // hard. Without this TMP would fall back to the SDF material and soften every letter.
        if (font.material != null) label.fontSharedMaterial = font.material;
    }

    // Sizes a label so one line comes out `metres` tall, and returns the transform scale used.
    //
    // TMP's fontSize is NOT world metres -- the 3D component applies its own factor on top of the font
    // asset's point size, so setting fontSize to a metre value renders text far too small to see. Rather
    // than hard-code that factor (it differs between the 3D and UGUI components and across TMP versions),
    // this renders a probe line at the font's native point size and measures what it actually came out at.
    //
    // Native point size also matters for crispness: a RASTER bitmap font sampled at its authored size maps
    // one atlas texel to one glyph pixel, and the transform scale then enlarges it in whole steps.
    static float FitToMetres(TextMeshPro label, float metres)
    {
        float pointSize = label.font != null && label.font.faceInfo.pointSize > 0
            ? label.font.faceInfo.pointSize
            : 16f;
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

    // speaker names the actor talking; empty hides the name line entirely.
    public void Speak(string text, string speaker = null)
    {
        gameObject.SetActive(true);
        _full = WordWrap(text, wrapChars);
        SetSpeaker(speaker);

        // Size the box to the finished line once, so it doesn't pulse while the text types in.
        _label.text = _full;
        // A TMP object aligns its text inside its RectTransform, which defaults to 20x5 world units --
        // far bigger than the bubble. Fitting the rect to the text makes TopLeft alignment mean "the top
        // left of the text block" and centres that block on the label's transform.
        Vector2 local = _label.GetPreferredValues();
        _label.rectTransform.sizeDelta = local;
        _label.ForceMeshUpdate();

        Vector2 b = local * _labelScale;   // preferred values are pre-scale, so convert to metres
        _boxSize = new Vector2(Mathf.Max(0.55f, b.x + padding.x * 2f), Mathf.Max(0.4f, b.y + padding.y * 2f));
        _label.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        if (_caret != null)
        {
            _caret.enabled = false;
            _caret.transform.localPosition = new Vector3(
                _boxSize.x * 0.5f - padding.x, -_boxSize.y * 0.5f + padding.y, -0.02f);
            // The cursor sprite is authored in UI pixels, so scale it from its own native world size
            // rather than assuming one.
            float caretNative = _caret.sprite.rect.height / _caret.sprite.pixelsPerUnit;
            float caretScale = caretNative > 0.0001f ? (textSize * 0.8f) / caretNative : 1f;
            _caret.transform.localScale = new Vector3(caretScale, caretScale, 1f);
        }

        // Name hangs off the box's top-left corner, so it stays glued to the box however wide the line is.
        if (_nameLabel != null && _nameLabel.gameObject.activeSelf)
        {
            Vector2 nameLocal = _nameLabel.GetPreferredValues();
            _nameLabel.rectTransform.sizeDelta = nameLocal;
            _nameLabel.ForceMeshUpdate();
            Vector2 nameSize = nameLocal * _nameScale;
            _nameLabel.transform.localPosition = new Vector3(
                -_boxSize.x * 0.5f + nameSize.x * 0.5f + padding.x,
                _boxSize.y * 0.5f + nameGap + nameSize.y * 0.5f,
                -0.01f);
        }

        if (_reveal != null) StopCoroutine(_reveal);
        _reveal = StartCoroutine(Reveal());
    }

    void SetSpeaker(string speaker)
    {
        if (_nameLabel == null) return;
        bool show = !string.IsNullOrEmpty(speaker);
        _nameLabel.gameObject.SetActive(show);
        if (!show) return;
        _nameLabel.text = speaker;
        // Keep the theme's gold set in Build — reassigning nameColor here would undo it.
        var theme = PixelUITheme.Instance;
        _nameLabel.color = theme != null ? theme.gold : nameColor;
    }

    // First press while typing fills the line instantly (instead of advancing the conversation).
    public void CompleteInstantly()
    {
        if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        if (_label != null)
        {
            _label.text = _full;
            _label.maxVisibleCharacters = int.MaxValue;   // clear the typewriter's reveal clamp
        }
        IsRevealing = false;
        if (_caret != null) _caret.enabled = true;
    }

    public void Hide()
    {
        if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        IsRevealing = false;
        if (_caret != null) _caret.enabled = false;
        gameObject.SetActive(false);
    }

    IEnumerator Reveal()
    {
        IsRevealing = true;
        // The whole line is set once and revealed with TMP's maxVisibleCharacters, so the mesh is built
        // a single time instead of re-laid-out on every character.
        _label.text = _full;
        _label.maxVisibleCharacters = 0;
        _label.ForceMeshUpdate();
        int total = _label.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            _label.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charInterval);
        }
        _label.maxVisibleCharacters = int.MaxValue;
        IsRevealing = false;
        _reveal = null;
        if (_caret != null) _caret.enabled = true;
    }

    void LateUpdate()
    {
        if (_actor == null) { Destroy(gameObject); return; } // actor despawned — clean up
        transform.position = _actor.position
            + Vector3.up * (headHeight + _boxSize.y * 0.5f)
            + Vector3.back * zLift; // in front of the ground/actor so the box isn't depth-culled
        transform.rotation = Quaternion.identity;            // stay upright even if the actor turns to face
        transform.localScale = Vector3.one;

        if (_bg != null)
        {
            if (_bg.drawMode == SpriteDrawMode.Sliced) _bg.size = _boxSize;
            else
            {
                // Simple draw ignores `size`, so stretch the transform instead — ugly corners, but a
                // readable box beats an unreadable one.
                var s = _bg.sprite;
                float nw = s != null ? s.rect.width / s.pixelsPerUnit : 1f;
                float nh = s != null ? s.rect.height / s.pixelsPerUnit : 1f;
                _bg.transform.localScale = new Vector3(
                    nw > 0.0001f ? _boxSize.x / nw : 1f, nh > 0.0001f ? _boxSize.y / nh : 1f, 1f);
            }
        }

        // Blink the continue marker so a finished line reads as "your move" at a glance.
        if (_caret != null && _caret.enabled)
        {
            var c = _caret.color;
            c.a = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f ? 1f : 0.25f;
            _caret.color = c;
        }
    }

    // Greedy word wrap so multi-word lines don't run off the side (TextMesh has no auto-wrap).
    static string WordWrap(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) return text;
        var words = text.Split(' ');
        var sb = new StringBuilder();
        int lineLen = 0;
        foreach (var w in words)
        {
            if (lineLen > 0 && lineLen + 1 + w.Length > width) { sb.Append('\n'); lineLen = 0; }
            else if (lineLen > 0) { sb.Append(' '); lineLen++; }
            sb.Append(w); lineLen += w.Length;
        }
        return sb.ToString();
    }

    static void SetUnlit(SpriteRenderer sr)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) sr.sharedMaterial = new Material(sh);
    }

    // Procedural rounded-rect with a 9-slice border, so SpriteDrawMode.Sliced scales it without distorting corners.
    static Sprite _bubble;
    static Sprite BubbleSprite()
    {
        if (_bubble != null) return _bubble;
        int s = 64, r = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Max(r - x, x - (s - 1 - r), 0);
                int dy = Mathf.Max(r - y, y - (s - 1 - r), 0);
                bool inside = (dx * dx + dy * dy) <= r * r;
                px[y * s + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _bubble = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s,
                                0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _bubble;
    }
}
