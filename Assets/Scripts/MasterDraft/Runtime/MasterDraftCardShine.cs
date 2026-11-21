using UnityEngine;
using UnityEngine.UI;

// The shine on a shiny card — a car card wearing an alternate paint (CardDefinition.Shiny, PackGenerator.AltPaintOneIn).
// MasterDraftCardView.Build adds it; nothing else needs to know it is there. Three layers, stepped and whole-pixel
// like the rest of the kit's animation, on unscaled time:
//  * Holo frame — the card's frame steps through a pink / gold / mint / cyan / violet cycle.
//  * Sheen — a slanted double gleam sweeps across the card face every couple of seconds (clipped to the face).
//  * Glitter — little four-point sparkles twinkle in and out over the face, each somewhere new every time.
// Each card starts at its own point in the cycle, so a row of shiny cards doesn't pulse in lockstep.
public class MasterDraftCardShine : MonoBehaviour
{
    static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xff);

    // The holo cycle, in order.
    public static readonly Color[] Holo =
    {
        Hex(0xff7ad9), Hex(0xffd84a), Hex(0x6cf0b0), Hex(0x5ad8ff), Hex(0xb48cff),
    };

    public static Color CalloutColour => Holo[0];

    const float FrameStep = 0.14f;                    // seconds per holo colour
    const float SheenPeriod = 2.6f, SheenSweep = 0.7f; // a gleam every SheenPeriod, crossing in SheenSweep
    const int SheenSteps = 10;                         // positions it steps through on the way across
    const float SheenAngle = -25f;
    const int SparkleCount = 7;
    const float SparkleMinPeriod = 0.8f, SparkleMaxPeriod = 1.6f;

    Image _frame;
    Graphic _tag;
    RectTransform _face, _sheen;
    Sparkle[] _sparkles = new Sparkle[0];
    System.Random _rng;
    float _time;

    class Sparkle
    {
        public RectTransform root, across, down;
        public float period, phase;
        public int cycle = -1;
    }

    // What the shine is showing right now (EditMode tests).
    public Color FrameColour => _frame != null ? _frame.color : Color.clear;
    public bool SheenShowing => _sheen != null && _sheen.gameObject.activeSelf;
    public int SparklesShowing
    {
        get
        {
            int n = 0;
            foreach (var s in _sparkles) if (s.root.gameObject.activeSelf) n++;
            return n;
        }
    }

    // Puts the shine on a built card: frame = the card's frame image, face = the inner panel the text sits on
    // (clipped from now on, so the sheen stays on the card). tag (optional) cycles with the frame.
    public static MasterDraftCardShine Add(RectTransform card, Image frame, RectTransform face, Graphic tag, int seed)
    {
        var shine = card.gameObject.AddComponent<MasterDraftCardShine>();
        shine._frame = frame;
        shine._tag = tag;
        shine._face = face;
        shine._rng = new System.Random(seed);
        if (face.GetComponent<RectMask2D>() == null) face.gameObject.AddComponent<RectMask2D>();
        shine.BuildLayers();
        shine._time = (float)shine._rng.NextDouble() * SheenPeriod;
        shine.Tick(0f);
        return shine;
    }

    void BuildLayers()
    {
        var size = _face.rect.size;
        float diagonal = Mathf.Ceil(Mathf.Sqrt(size.x * size.x + size.y * size.y));

        // The gleam: a broad band and a thin one beside it, slanted, white and see-through.
        _sheen = NewRect("Sheen", _face);
        _sheen.anchorMin = _sheen.anchorMax = _sheen.pivot = new Vector2(0.5f, 0.5f);
        _sheen.sizeDelta = new Vector2(16f, diagonal);
        _sheen.localRotation = Quaternion.Euler(0f, 0f, SheenAngle);
        Bar(_sheen, "Broad", new Color(1f, 1f, 1f, 0.22f), new Vector2(-4f, 0f), new Vector2(9f, diagonal));
        Bar(_sheen, "Thin", new Color(1f, 1f, 1f, 0.30f), new Vector2(6f, 0f), new Vector2(2f, diagonal));

        _sparkles = new Sparkle[SparkleCount];
        for (int i = 0; i < SparkleCount; i++)
        {
            var colour = i % 3 == 1 ? Hex(0xfff1b0) : Color.white;
            var root = NewRect("Sparkle" + i, _face);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(5f, 5f);
            var s = new Sparkle
            {
                root = root,
                across = Bar(root, "Across", colour, Vector2.zero, new Vector2(5f, 1f)),
                down = Bar(root, "Down", colour, Vector2.zero, new Vector2(1f, 5f)),
                period = SparkleMinPeriod + (float)_rng.NextDouble() * (SparkleMaxPeriod - SparkleMinPeriod),
                phase = (float)_rng.NextDouble(),
            };
            _sparkles[i] = s;
        }
    }

    void Update() => Tick(Time.unscaledDeltaTime);

    // Moves the shine on by dt seconds (Update in play; EditMode tests call it directly).
    public void Tick(float dt)
    {
        _time += dt;

        // Holo frame.
        var colour = Holo[Mathf.FloorToInt(_time / FrameStep) % Holo.Length];
        if (_frame != null) _frame.color = colour;
        if (_tag != null) _tag.color = colour;

        if (_face == null) return;
        var size = _face.rect.size;

        // Sheen: across in SheenSteps whole-pixel steps, then gone until the next one.
        float t = _time % SheenPeriod;
        bool sweeping = t < SheenSweep;
        _sheen.gameObject.SetActive(sweeping);
        if (sweeping)
        {
            float k = Mathf.Floor(t / SheenSweep * SheenSteps) / (SheenSteps - 1);
            float span = size.x + size.y * 0.5f + 16f;
            _sheen.anchoredPosition = new Vector2(Mathf.Round(-span / 2f + k * span), 0f);
        }

        // Glitter: each sparkle hides for half its cycle, then grows to a four-point star and shrinks away. A new
        // cycle puts it somewhere new on the face.
        foreach (var s in _sparkles)
        {
            float cycles = _time / s.period + s.phase;
            int cycle = Mathf.FloorToInt(cycles);
            float f = cycles - cycle;
            if (cycle != s.cycle)
            {
                s.cycle = cycle;
                s.root.anchoredPosition = new Vector2(
                    Mathf.Round(3f + (float)_rng.NextDouble() * Mathf.Max(0f, size.x - 6f)),
                    Mathf.Round(3f + (float)_rng.NextDouble() * Mathf.Max(0f, size.y - 6f)));
            }

            int stage = f < 0.5f ? 0 : f < 0.62f ? 1 : f < 0.78f ? 2 : f < 0.9f ? 1 : 0;
            s.root.gameObject.SetActive(stage > 0);
            if (stage == 0) continue;
            float arm = stage == 2 ? 5f : 3f;
            s.across.sizeDelta = new Vector2(arm, 1f);
            s.down.sizeDelta = new Vector2(1f, arm);
        }
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Bar(RectTransform parent, string name, Color colour, Vector2 offset, Vector2 size)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        return rt;
    }
}
