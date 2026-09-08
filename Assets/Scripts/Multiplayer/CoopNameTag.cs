using TMPro;
using UnityEngine;

// The other player's name, on a small tag above their head.
//
// Built the same way SpeechBubble and FightHealthBar are — an independent world-space object made of a
// SpriteRenderer plate plus a TextMeshPro label, not a world-space Canvas — for the two reasons those give:
// the spline scenes render through the 3D URP renderer, where a Canvas needs wiring and a font asset before
// it draws at all; and the body this sits over rotates to face where it is walking, so a parented tag would
// spin with it.
//
// The name itself is whatever that player typed on the OPTIONS screen, sent over by their own machine —
// see CoopBodies. This class never reads a name from anywhere; it is handed one.
public class CoopNameTag : MonoBehaviour
{
    [Tooltip("World metres above the body's position the tag floats. The on-foot characters are ~0.63m tall, so this clears the head.")]
    public float headHeight = 0.82f;

    [Tooltip("How far toward the camera the tag sits, so the opaque ground plane can't depth-cull it. Same lift SpeechBubble uses.")]
    public float zLift = 0.6f;

    [Tooltip("Name text size in world metres.")]
    public float textSize = 0.14f;

    [Tooltip("Plate border around the text, per side, in world metres.")]
    public Vector2 padding = new Vector2(0.09f, 0.035f);

    [Tooltip("Colour of the name itself. Gold by default, matching the speaker name SpeechBubble draws above a line.")]
    public Color textColor = new Color(1f, 0.83f, 0.42f, 1f);

    // What a peer who has never named themselves is called. Better than an empty tag, and it still says
    // which of the two of you it is.
    public const string HostFallback = "PLAYER 1";
    public const string GuestFallback = "PLAYER 2";

    Transform _body;
    TextMeshPro _label;
    SpriteRenderer _plate;
    string _shown = "";
    float _labelScale = 1f;

    public static CoopNameTag Attach(Transform body, string label)
    {
        if (body == null) return null;

        var go = new GameObject($"CoopNameTag ({body.name})");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.UI);   // world-space, but it is UI — keep it off the scene root
        var tag = go.AddComponent<CoopNameTag>();
        tag._body = body;
        tag.Build();
        tag.SetName(label);
        tag.Follow();                                     // in place from the first frame, not sliding in from the origin
        return tag;
    }

    public void Detach()
    {
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    // The name shown, given what the other machine sent. Empty means they have never been through OPTIONS,
    // so they get a placeholder that at least says which player they are.
    public static string LabelFor(string sent, bool isHostPeer)
    {
        string trimmed = string.IsNullOrWhiteSpace(sent) ? "" : sent.Trim();
        if (trimmed.Length > 0) return trimmed;
        return isHostPeer ? HostFallback : GuestFallback;
    }

    public void SetName(string label)
    {
        label = label ?? "";
        if (label == _shown) return;
        _shown = label;
        if (_label == null) return;

        _label.text = label;
        _label.ForceMeshUpdate();
        SizePlate();
    }

    void Build()
    {
        var theme = PixelUITheme.Instance;

        var plateGo = new GameObject("Plate");
        plateGo.transform.SetParent(transform, false);
        plateGo.transform.localPosition = new Vector3(0f, 0f, 0.01f);   // just behind the text
        _plate = plateGo.AddComponent<SpriteRenderer>();
        var themed = theme != null && theme.windowPlain != null ? theme.windowPlain
                   : theme != null ? theme.window : null;
        _plate.sprite = themed != null && themed.border.sqrMagnitude > 0f ? themed : PlateSprite();
        // Sliced draw needs a 9-slice border; without one the renderer ignores `size` and draws the sprite
        // at its native (tiny) world size — the trap SpeechBubble and FightHealthBar both guard against.
        _plate.drawMode = _plate.sprite != null && _plate.sprite.border.sqrMagnitude > 0f
            ? SpriteDrawMode.Sliced
            : SpriteDrawMode.Simple;
        _plate.color = _plate.sprite == themed && themed != null
            ? Color.white                                             // a hand-textured plate is shown as authored
            : (theme != null ? theme.plateDeep : new Color(0.06f, 0.07f, 0.1f, 0.92f));
        _plate.sortingLayerName = "Vehicles";
        _plate.sortingOrder = 80;
        SetUnlit(_plate);

        var labelGo = new GameObject("Name");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        // TextMeshPro rather than TextMesh: TextMesh can only take a dynamic Font, which renders
        // anti-aliased and fights the pixel art. TMP carries the theme's RASTER bitmap face.
        _label = labelGo.AddComponent<TextMeshPro>();
        if (theme != null && theme.body != null)
        {
            _label.font = theme.body;
            if (theme.body.material != null) _label.fontSharedMaterial = theme.body.material;
        }
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = false;
        _label.color = textColor;
        var labelRenderer = labelGo.GetComponent<MeshRenderer>();
        labelRenderer.sortingLayerName = "Vehicles";
        labelRenderer.sortingOrder = 81;
        _labelScale = FitToMetres(_label, textSize);
    }

    // The plate is grown to whatever the name measured, so "Jo" and "Josh Van Der Berg" both sit in a tag
    // that fits them rather than in one fixed box that clips the long one.
    void SizePlate()
    {
        if (_label == null || _plate == null) return;

        Vector2 preferred = _label.GetPreferredValues();
        _label.rectTransform.sizeDelta = preferred;

        float w = preferred.x * _labelScale + padding.x * 2f;
        float h = preferred.y * _labelScale + padding.y * 2f;

        if (_plate.drawMode == SpriteDrawMode.Sliced)
        {
            _plate.size = new Vector2(w, h);
            _plate.transform.localScale = Vector3.one;
        }
        else
        {
            var s = _plate.sprite;
            float nw = s != null ? s.rect.width / s.pixelsPerUnit : 1f;
            float nh = s != null ? s.rect.height / s.pixelsPerUnit : 1f;
            _plate.transform.localScale = new Vector3(nw > 0.0001f ? w / nw : 1f, nh > 0.0001f ? h / nh : 1f, 1f);
        }

        _plate.enabled = _shown.Length > 0;
        _label.enabled = _shown.Length > 0;
    }

    void LateUpdate()
    {
        if (_body == null) { Destroy(gameObject); return; }   // the puppet went — the tag goes with it
        Follow();
    }

    void Follow()
    {
        if (_body == null) return;
        transform.position = _body.position + Vector3.up * headHeight + Vector3.back * zLift;
        transform.rotation = Quaternion.identity;             // never inherit the body's facing spin
    }

    static void SetUnlit(SpriteRenderer sr)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) sr.sharedMaterial = new Material(sh);
    }

    // Same measure-and-scale trick SpeechBubble and FightHealthBar use: TMP's fontSize is not world metres,
    // so render a probe at the font's native point size and scale by what it actually measured.
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
    static Sprite _plateSprite;
    static Sprite PlateSprite()
    {
        if (_plateSprite != null) return _plateSprite;
        int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        _plateSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s,
                                     0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return _plateSprite;
    }
}
