using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Fans;

// Places a handful of autograph-seeking fans (AutographFan) along the pit lane, on the wall side near the
// boxes where an on-foot player walks. The count scales with the player's FanAppeal — the more popular you
// are, the more turn up. Fans are paper-doll NPCs (same look as the paddock crowd) or coloured placeholders
// until the art exists, mirroring PitCrewSpawner / PaddockSpawner.
//
// Self-installing: no scene wiring needed. On each scene load, if a spline TrackBuilder with a pit lane is
// present and no spawner already exists, one is created and finds the track itself. So the feature lights up
// in every pit-lane track scene (WatkinsGlen, etc.) and stays out of legacy/menu scenes that have no pit lane.
public class AutographFanSpawner : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Track whose pit lane the fans stand along. Auto-found if null.")]
    public TrackBuilder track;
    [Tooltip("Paper-doll outfit library for the fans. Falls back to Resources/NPC/NPCPartLibrary, then a coloured placeholder.")]
    public NPCPartLibrary fanLibrary;

    [Header("Crowd size (scales with FanAppeal)")]
    [Tooltip("Fans shown at 0 appeal.")]
    public int minFans = 0;
    [Tooltip("Fans shown at 100 appeal.")]
    public int maxFans = 6;

    [Header("Placement")]
    [Tooltip("Sign of the pit-wall side the fans stand on. Flip to -1 if they end up on the racing side.")]
    public float wallSide = 1f;
    [Tooltip("Lateral distance (m) from the pit centerline to the fans. Clamped clear of any parked box lane.")]
    public float lateralFromCenter = 4.0f;
    [Tooltip("World height a paper-doll fan is normalised to, whatever the library's pixel size. Defaults to the same figure the on-foot player renders at, so fans don't tower over whoever they're asking for a signature.")]
    public float fanHeightM = PitCrewSpawner.OnFootPersonHeight;

    [Header("Sorting")]
    public string sortingLayerName = "Vehicles";
    [Tooltip("Fans draw above the cars (car sorting order is ~5).")]
    public int baseSortingOrder = 8;

    // The on-foot player currently in the scene (null while driving). AutographFan reads this to notice,
    // walk up to, and face the player — and so patience only bleeds when they could actually stop and sign.
    public static OnFootController OnFootPlayer { get; private set; }
    // True while the player is on foot in the scene.
    public static bool PlayerOnFoot => OnFootPlayer != null;

    Material _unlit;
    float _onFootPoll;
    bool _waveRunning;      // a spawn coroutine is in flight
    bool _stintTopUpDone;   // one top-up wave per on-foot stint
    Transform _root;

    // ----- self-install -----
    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        SceneManager.sceneLoaded += (_, __) => TryInstall();
        _hooked = true;
    }

    static void TryInstall()
    {
        if (FindObjectOfType<AutographFanSpawner>() != null) return;   // already present (authored or installed)
        var tb = FindObjectOfType<TrackBuilder>();
        if (tb == null || tb.track == null || !tb.track.hasPitLane) return; // only spline tracks with a pit lane
        var go = new GameObject("AutographFanSpawner");
        go.AddComponent<AutographFanSpawner>().track = tb;
    }

    void Start() => StartCoroutine(SpawnWhenReady());

    void Update()
    {
        // Cheap throttled poll for the on-foot player (no static registry on OnFootController).
        _onFootPoll -= Time.deltaTime;
        if (_onFootPoll <= 0f)
        {
            _onFootPoll = 0.5f;
            OnFootPlayer = FindObjectOfType<OnFootController>();

            // Top-up: if the player goes on foot and every fan from the load-time wave has already come and
            // gone (or none spawned), give this stint a fresh wave — once per stint, so a late pit walk
            // still meets fans instead of an empty wall.
            if (!PlayerOnFoot) _stintTopUpDone = false;
            else if (!_stintTopUpDone && !_waveRunning && GetComponentsInChildren<AutographFan>().Length == 0)
            {
                _stintTopUpDone = true;
                StartCoroutine(SpawnWhenReady());
            }
        }
    }

    IEnumerator SpawnWhenReady()
    {
        _waveRunning = true;
        try
        {
            if (track == null) track = FindObjectOfType<TrackBuilder>();
            if (fanLibrary == null) fanLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");

            // Give GridSpawner a chance to configure the pit boxes so we can span the grey box-lane strip.
            // Proceed regardless once it exists — a pit-walk scene may have no GridSpawner (unconfigured fallback).
            float timeout = 10f;
            while (timeout > 0f && (track == null || !PitLane.Configured))
            {
                if (track == null) track = FindObjectOfType<TrackBuilder>();
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (track == null || track.track == null || !track.track.hasPitLane) yield break;

            var pit = track.SamplePitCenterline();
            if (pit.Count < 2) yield break;
            float pitLength = pit[pit.Count - 1].distance;
            if (pitLength <= 0f) yield break;

            int count = FanAppeal.FanCountForAppeal(FanAppeal.Value, minFans, maxFans);
            if (count <= 0) yield break;

            // Spread fans across the box-lane span (or a central slice of the whole lane when there's no strip),
            // so they cluster near the boxes rather than out on the entry/exit ramps.
            float from = track.HasPitBoxLane ? track.PitBoxLaneFrom(pitLength) : pitLength * 0.15f;
            float to = track.HasPitBoxLane ? track.PitBoxLaneTo(pitLength) : pitLength * 0.85f;

            // Keep fans clear of any parked box lane so they don't stand inside the cars.
            float lateral = lateralFromCenter;
            if (PitLane.Configured) lateral = Mathf.Max(lateral, Mathf.Abs(PitLane.ParkLateral) + 1.5f);

            if (_root == null)
            {
                _root = new GameObject("AutographFans").transform;
                _root.SetParent(transform, false);
            }

            for (int i = 0; i < count; i++)
            {
                float d = count == 1 ? Mathf.Lerp(from, to, 0.5f)
                                     : Mathf.Lerp(from, to, (i + 0.5f) / count) + Random.Range(-1.5f, 1.5f);
                d = Mathf.Clamp(d, from, to);
                BuildFan(_root, d, lateral, pit, i);
            }
        }
        finally { _waveRunning = false; }
    }

    void BuildFan(Transform root, float dist, float lateral, List<TrackBuilder.Sample> pit, int seed)
    {
        var s = track.SamplePitAt(dist, pit);
        Vector3 basePos = track.transform.TransformPoint(new Vector3(s.position.x, s.position.y, 0f));
        Vector3 normalW = track.transform.TransformDirection(new Vector3(s.normal.x, s.normal.y, 0f)).normalized;
        Vector3 tangentW = track.transform.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f)).normalized;

        // A little lateral scatter so they don't stand in a perfect line along the wall.
        float lat = lateral + Random.Range(-0.4f, 0.4f);
        Vector3 pos = basePos + normalW * (wallSide * lat);
        pos.z = -0.1f; // toward the camera so the sprite draws in front of the pit tarmac
        // Nudge along the lane too, for a looser cluster.
        pos += tangentW * Random.Range(-0.5f, 0.5f);

        var go = new GameObject("AutographFan");
        go.transform.SetParent(root, false);
        go.transform.position = pos;

        // Face across the lane toward the racing side (where the player walks/drives past).
        Vector3 face = -normalW * wallSide;
        OnFootController.ApplyFacing(go.transform, null, new Vector2(face.x, face.y), 90f);

        BuildAppearance(go, seed);

        var fan = go.AddComponent<AutographFan>();
        fan.speakerName = "Fan";
        fan.lines = kDialogue[seed % kDialogue.Length];
    }

    void BuildAppearance(GameObject go, int seed)
    {
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = fanLibrary;
        layered.layerMaterial = UnlitSprite();
        layered.sortingLayerName = sortingLayerName;
        layered.baseSortingOrder = baseSortingOrder;

        if (fanLibrary != null && layered.Build(seed))
        {
            // Normalise to a person-height whatever the library's pixel size (tiny sheets → tiny fans otherwise).
            float frameWorldH = Mathf.Max(0.01f, fanLibrary.frameHeight / Mathf.Max(1f, fanLibrary.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (fanHeightM / frameWorldH);
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(new Color(0.85f, 0.75f, 0.25f), fanHeightM); // gold blob = fan
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
        }
    }

    Material UnlitSprite()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }

    static Sprite Placeholder(Color color, float metres)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        float ppu = s / Mathf.Max(0.05f, metres);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), ppu);
    }

    // A line ending with "#player" is spoken by the player. Signing happens when the last line is passed.
    static readonly string[][] kDialogue =
    {
        new[] { "Oh my gosh, it's really you! Can I get an autograph?", "Sure — happy to. #player", "You're my favourite driver out here, thank you!!" },
        new[] { "I've watched every race this season. Sign my cap?", "Of course, hold it steady. #player", "Amazing — I'll never wash it now!" },
        new[] { "No way, the real deal! One autograph, please?", "You got it. #player", "This is the best day ever. Good luck today!" },
        new[] { "My kid's your biggest fan — could you sign this for them?", "What's their name? #player", "You're a legend. They'll be over the moon!" },
        new[] { "Quick one before you head out — autograph?", "Always got time for a fan. #player", "Cheers! Go win it out there!" },
        new[] { "I drove three hours to see you race. Mind signing this?", "Three hours? Least I can do. #player", "Worth every mile. Thank you!" },
    };
}
