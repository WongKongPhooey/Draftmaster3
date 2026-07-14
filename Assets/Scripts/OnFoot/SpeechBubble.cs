using System.Collections;
using System.Text;
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
    [Tooltip("How far in front of the actor (negative z, toward the camera) the bubble sits. Must clear whatever the actor is standing on — the opaque z=0 ground depth-tests the box away without this.")]
    public float zLift = 0.6f;

    Transform _actor;
    TextMesh _label;
    MeshRenderer _labelRenderer;
    SpriteRenderer _bg;
    Coroutine _reveal;
    string _full = "";
    Vector2 _boxSize = new Vector2(0.55f, 0.4f);
    Vector2 _textOrigin;                            // label's top-left anchor position inside the box

    public bool IsRevealing { get; private set; }

    public static SpeechBubble Attach(Transform actor)
    {
        var go = new GameObject("SpeechBubble (" + actor.name + ")");
        var sb = go.AddComponent<SpeechBubble>();
        sb._actor = actor;
        sb.Build();
        go.SetActive(false);
        return sb;
    }

    void Build()
    {
        _bg = new GameObject("BG").AddComponent<SpriteRenderer>();
        _bg.transform.SetParent(transform, false);
        _bg.transform.localPosition = new Vector3(0f, 0f, 0.01f); // just behind the text
        var custom = Resources.Load<Sprite>("OnFoot/SpeechBox");
        _bg.sprite = custom != null ? custom : BubbleSprite();
        _bg.drawMode = SpriteDrawMode.Sliced;
        // A hand-textured box is shown as authored; the procedural fallback gets the dark tint.
        _bg.color = custom != null ? Color.white : new Color(0.06f, 0.06f, 0.09f, 0.92f);
        _bg.sortingLayerName = "Vehicles";
        _bg.sortingOrder = 60;
        SetUnlit(_bg);

        var labelGo = new GameObject("Text");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        _label = labelGo.AddComponent<TextMesh>();
        // Anchored at its top-left so lines type out left-aligned and hang from a fixed origin.
        _label.anchor = TextAnchor.UpperLeft;
        _label.alignment = TextAlignment.Left;
        // TextMesh world height per line ≈ fontSize * characterSize * 0.1, so ~0.16 units/line here.
        _label.fontSize = 72;
        _label.characterSize = 0.022f;
        _label.color = Color.white;
        _labelRenderer = labelGo.GetComponent<MeshRenderer>();
        _labelRenderer.sortingLayerName = "Vehicles";
        _labelRenderer.sortingOrder = 61;
    }

    public void Speak(string text)
    {
        gameObject.SetActive(true);
        _full = WordWrap(text, wrapChars);

        // Size the box to the finished line once, so it doesn't pulse while the text types in:
        // show the full text, measure it, then wind back to empty and reveal.
        _label.text = _full;
        Vector3 b = _labelRenderer.bounds.size;
        _boxSize = new Vector2(Mathf.Max(0.55f, b.x + padding.x * 2f), Mathf.Max(0.4f, b.y + padding.y * 2f));
        _textOrigin = new Vector2(-b.x * 0.5f, b.y * 0.5f); // text block centred in the box, left-aligned
        _label.transform.localPosition = new Vector3(_textOrigin.x, _textOrigin.y, -0.01f);

        if (_reveal != null) StopCoroutine(_reveal);
        _reveal = StartCoroutine(Reveal());
    }

    // First press while typing fills the line instantly (instead of advancing the conversation).
    public void CompleteInstantly()
    {
        if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        if (_label != null) _label.text = _full;
        IsRevealing = false;
    }

    public void Hide()
    {
        if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        IsRevealing = false;
        gameObject.SetActive(false);
    }

    IEnumerator Reveal()
    {
        IsRevealing = true;
        var sb = new StringBuilder();
        _label.text = "";
        foreach (char c in _full)
        {
            sb.Append(c);
            _label.text = sb.ToString();
            yield return new WaitForSeconds(charInterval);
        }
        IsRevealing = false;
        _reveal = null;
    }

    void LateUpdate()
    {
        if (_actor == null) { Destroy(gameObject); return; } // actor despawned — clean up
        transform.position = _actor.position
            + Vector3.up * (headHeight + _boxSize.y * 0.5f)
            + Vector3.back * zLift; // in front of the ground/actor so the box isn't depth-culled
        transform.rotation = Quaternion.identity;            // stay upright even if the actor turns to face
        transform.localScale = Vector3.one;

        if (_bg != null) _bg.size = _boxSize;
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
