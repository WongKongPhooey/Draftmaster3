using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The title logo's entrance. Nothing is on screen when the title opens: DRAFT whips in from the left, MASTER
// from the right a beat later, then the 3 drops in from the top, and the tagline fades up once it has landed.
// Each move is short and decelerates hard into place — the pieces arrive, they do not glide.
//
// The logo is the DraftmasterLogo prefab (DraftmasterLogoBuilder): DRAF / T / M / ASTER faces with a shadow
// copy of each under Wordmark/Shadow, a bolt on each line, the tagline under Wordmark, and the 3 at the root.
// The pieces are found by those names and moved by their own anchored positions, so the prefab and the title
// scene stay exactly as authored.
//
// Each word lands with a metal crunch (Resources/Audio/metal-crunch), timed to the moment it visibly hits its
// mark rather than the end of the slide: the ease puts it there about halfway through. The 3 hits lowest.
//
// Self-installing on whichever scene carries a DraftmasterLogo, so it needs no scene edit.
public class TitleLogoIntro : MonoBehaviour
{
    [Tooltip("Seconds after the title opens before DRAFT sets off.")]
    public float startDelay = 0.25f;
    [Tooltip("Seconds each word takes to slide in.")]
    public float slideSeconds = 0.27f;
    [Tooltip("Seconds between one piece setting off and the next.")]
    public float stagger = 0.33f;
    [Tooltip("Seconds the tagline takes to fade up after the 3 lands.")]
    public float taglineFadeSeconds = 0.25f;
    [Tooltip("How far through its slide a word is when it reads as locked in place, 0..1. The quintic ease has " +
             "covered ~98% of the distance by 0.55, so the crunch lands on the visible stop, not the tail.")]
    [Range(0f, 1f)] public float lockAt = 0.55f;
    [Tooltip("Volume of the crunch as each word locks in.")]
    [Range(0f, 1f)] public float crunchVolume = 0.9f;

    const string CrunchClip = "Audio/metal-crunch";
    // DRAFT, MASTER, then the 3 a little heavier.
    static readonly float[] CrunchPitch = { 1.05f, 0.97f, 0.86f };
    // One source per word: a one-shot follows its source's pitch while it plays, and the words land a third
    // of a second apart, so a shared source would bend the last crunch's tail.
    readonly AudioSource[] _audio = new AudioSource[3];
    AudioClip _crunch;
    readonly bool[] _crunched = new bool[3];

    // The longest any one frame may advance the intro. The first frame after a cold boot carries the whole
    // scene's setup as its delta, which would otherwise spend the entire entrance before it drew once.
    const float MaxFrameSeconds = 0.05f;

    class Piece
    {
        public RectTransform rt;
        public Vector2 home;
        public Vector2 from;
    }

    readonly List<Piece> _draft = new List<Piece>();
    readonly List<Piece> _master = new List<Piece>();
    readonly List<Piece> _three = new List<Piece>();
    CanvasGroup _tagline;
    float _t;
    bool _done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var logo = FindDeep(root.transform, "DraftmasterLogo");
            if (logo == null) continue;
            if (logo.GetComponent<TitleLogoIntro>() == null) logo.gameObject.AddComponent<TitleLogoIntro>();
            return;
        }
    }

    void Start()
    {
        var word = transform.Find("Wordmark");
        var shadow = word != null ? word.Find("Shadow") : null;
        var canvas = GetComponentInParent<Canvas>();
        var canvasRt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;

        // Far enough to be off the screen from anywhere the piece sits: a whole screen's width (or height),
        // converted into the parent's own units.
        float screenW = canvasRt != null ? canvasRt.rect.width * canvasRt.lossyScale.x : 20f;
        float screenH = canvasRt != null ? canvasRt.rect.height * canvasRt.lossyScale.y : 12f;

        Collect(_draft, new Vector2(-screenW, 0f), word, "DRAF", "T", "BoltTop");
        Collect(_draft, new Vector2(-screenW, 0f), shadow, "DRAF", "T");
        Collect(_master, new Vector2(screenW, 0f), word, "M", "ASTER", "BoltBottom");
        Collect(_master, new Vector2(screenW, 0f), shadow, "M", "ASTER");
        Collect(_three, new Vector2(0f, screenH), transform, "Three");

        var tag = word != null ? word.Find("Tagline") : null;
        if (tag != null)
        {
            _tagline = tag.GetComponent<CanvasGroup>();
            if (_tagline == null) _tagline = tag.gameObject.AddComponent<CanvasGroup>();
        }

        _crunch = Resources.Load<AudioClip>(CrunchClip);
        if (_crunch != null)
            for (int i = 0; i < _audio.Length; i++)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;    // a title sting, not a sound in the world
                a.pitch = CrunchPitch[i];
                _audio[i] = a;
            }
        TitleAudio.EnsureListener();

        Apply();
    }

    // Each piece's start is its home pushed off screen by `worldOffset`, expressed in its parent's space.
    static void Collect(List<Piece> into, Vector3 worldOffset, Transform parent, params string[] names)
    {
        if (parent == null) return;
        var scale = parent.lossyScale;
        var local = new Vector2(scale.x != 0f ? worldOffset.x / scale.x : 0f,
                                scale.y != 0f ? worldOffset.y / scale.y : 0f);
        foreach (var name in names)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null) continue;
            into.Add(new Piece { rt = rt, home = rt.anchoredPosition, from = rt.anchoredPosition + local });
        }
    }

    void Update()
    {
        if (_done) return;
        _t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds);
        Apply();
    }

    void Apply()
    {
        float draftAt = startDelay;
        float masterAt = draftAt + stagger;
        float threeAt = masterAt + stagger;
        float tagAt = threeAt + slideSeconds;

        Slide(_draft, draftAt);
        Slide(_master, masterAt);
        Slide(_three, threeAt);

        Crunch(0, draftAt, _draft);
        Crunch(1, masterAt, _master);
        Crunch(2, threeAt, _three);

        float tag = taglineFadeSeconds > 0f ? Mathf.Clamp01((_t - tagAt) / taglineFadeSeconds) : (_t >= tagAt ? 1f : 0f);
        if (_tagline != null) _tagline.alpha = tag;

        if (_t >= tagAt + taglineFadeSeconds) _done = true;
    }

    void Slide(List<Piece> pieces, float at)
    {
        float k = slideSeconds > 0f ? Mathf.Clamp01((_t - at) / slideSeconds) : (_t >= at ? 1f : 0f);
        // Quintic ease-out: nearly all of the distance in the first third, then a hard stop on the mark.
        float e = 1f - Mathf.Pow(1f - k, 5f);
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.rt != null) p.rt.anchoredPosition = Vector2.LerpUnclamped(p.from, p.home, e);
        }
    }

    // The word has hit its mark: one crunch, once.
    void Crunch(int i, float at, List<Piece> pieces)
    {
        if (_crunched[i] || pieces.Count == 0) return;
        if (_t < at + slideSeconds * lockAt) return;
        _crunched[i] = true;
        if (_audio[i] == null || _crunch == null) return;
        _audio[i].PlayOneShot(_crunch, crunchVolume);
    }

    // Leaving early (a scene change mid-entrance) puts everything back where the prefab has it.
    void OnDisable()
    {
        foreach (var list in new[] { _draft, _master, _three })
            foreach (var p in list)
                if (p.rt != null) p.rt.anchoredPosition = p.home;
        if (_tagline != null) _tagline.alpha = 1f;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindDeep(t.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
