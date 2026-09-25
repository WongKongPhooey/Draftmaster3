using System.Collections.Generic;
using Draftmaster.Chatter;
using UnityEngine;

// A driver caught in the open paddock by their fans: a knot of people packed round them, holding out hats and
// phones, one after another getting their autograph or stepping in beside them for a photo, and the next
// person in line shuffling up to take their place.
//
// Only happens while the player's own championship is off the track (see DriverPresenceDirector) — with no
// session to be in, a driver standing still anywhere public gets found.
//
// One Update runs the whole mob, fans included, and it does nothing at all unless an on-foot player is near
// enough to see it: from further off, or from the cockpit, it is a still picture of a crowd, which is what it
// looks like from there anyway. The fans have no physics and no colliders, so they never wall the player off
// from the driver in the middle — the player walks through the ring the way you would shoulder through one.
public class FanMob : MonoBehaviour
{
    [Tooltip("Front row: how far from the driver the nearest fans stand (m).")]
    public float innerRadius = 1.3f;
    [Tooltip("Back row, for a crowd too big to fit in one ring (m).")]
    public float outerRadius = 2.1f;
    [Tooltip("Fans that fit in the front row before the rest stand behind them.")]
    public int frontRowCapacity = 6;
    [Tooltip("The open mouth left in the ring, in degrees, so there is an obvious way in to the driver.")]
    public float mouthDegrees = 70f;
    [Tooltip("Beyond this distance from the on-foot player the mob stands still (m).")]
    public float wakeRadius = 28f;
    [Tooltip("Fans start calling out once the on-foot player is this close (m).")]
    public float barkRadius = 9f;
    public float walkSpeed = 1.3f;
    public float walkFrameRate = 6f;

    // ------------------------------------------------------------------ the people

    enum FanState { Arriving, Waiting, Turn, Leaving }

    class Fan
    {
        public Transform t;
        public NPCLayeredAppearance look;
        public FanState state;
        public int slot;                 // ring position index
        public Vector3 target;           // where they are walking to
        public float frameTimer;
        public int frame;
        public float fidgetAt;           // next small shuffle
    }

    readonly List<Fan> _fans = new();
    readonly List<Vector3> _slots = new();

    Transform _driver;
    Rigidbody2D _driverBody;
    NPCInteractable _driverTalk;
    string _driverFirst;
    Vector2 _mouth;                      // world direction the mouth of the ring opens toward

    System.Func<int, GameObject> _makeFan;
    System.Random _rng;
    int _fanSeed;

    // The fan whose turn it is, and what they are doing with it.
    Fan _turn;
    bool _selfie;
    bool _shotTaken;
    float _turnEndsAt;
    float _nextTurnAt;
    float _nextFlashAt;
    float _nextBarkAt;
    bool _dispersing;

    SpeechBubble _bubble;
    Transform _bubbleOwner;
    float _bubbleHideAt;

    // Phone flashes going off: each one a white pop that grows and fades over a tenth of a second or so.
    struct Pop { public SpriteRenderer sr; public float born; }
    readonly List<Pop> _pops = new();
    const float PopLife = 0.14f;

    static Sprite _flashSprite;
    static Material _flashMaterial;

    // `makeFan(seed)` builds one fan's body (paper doll + scale) — the director owns the look, the mob owns
    // what they do.
    public static FanMob Create(Transform parent, Vector3 centre, GameObject driver, string driverFullName,
                                int fanCount, int seed, System.Func<int, GameObject> makeFan)
    {
        var go = new GameObject($"FanMob_{driver.name}");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;

        var mob = go.AddComponent<FanMob>();
        mob._rng = new System.Random(seed);
        mob._fanSeed = seed * 131 + 17;
        mob._makeFan = makeFan;
        mob._driver = driver.transform;
        mob._driverBody = driver.GetComponent<Rigidbody2D>();
        mob._driverTalk = driver.GetComponent<NPCInteractable>();
        mob._driverFirst = FirstName(driverFullName);
        driver.transform.SetParent(go.transform, true);
        driver.transform.position = centre;

        float a = (float)mob._rng.NextDouble() * Mathf.PI * 2f;
        mob._mouth = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        mob.LayOutSlots(Mathf.Max(1, fanCount));

        for (int i = 0; i < mob._slots.Count; i++)
        {
            var fan = mob.SpawnFan(i, mob._slots[i]);
            if (fan == null) continue;
            fan.state = FanState.Waiting;
            mob.FaceDriver(fan);
        }

        // The driver faces out through the gap, as if they had just turned to the next person in line.
        mob.FaceDriverTowards(mob._mouth);

        float now = Time.time;
        mob._nextTurnAt = now + 1f + (float)mob._rng.NextDouble() * 2f;
        mob._nextFlashAt = now + (float)mob._rng.NextDouble() * 2f;
        mob._nextBarkAt = now + 2f;
        return mob;
    }

    static string FirstName(string full)
    {
        if (string.IsNullOrEmpty(full)) return "Hey";
        int sp = full.IndexOf(' ');
        return sp > 0 ? full.Substring(0, sp) : full;
    }

    // Two rings round the driver, both leaving the same mouth open.
    void LayOutSlots(int count)
    {
        _slots.Clear();
        int front = Mathf.Min(count, Mathf.Max(1, frontRowCapacity));
        AddRing(front, innerRadius, 0f);
        if (count > front) AddRing(count - front, outerRadius, 0.5f);
    }

    void AddRing(int n, float radius, float stagger)
    {
        float mouth = Mathf.Atan2(_mouth.y, _mouth.x);
        float half = mouthDegrees * 0.5f * Mathf.Deg2Rad;
        float span = Mathf.PI * 2f - half * 2f;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f + stagger * 0.5f) / n;
            float ang = mouth + half + span * Mathf.Clamp01(t);
            float r = radius + ((float)_rng.NextDouble() - 0.5f) * 0.25f;
            _slots.Add(transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * r);
        }
    }

    Fan SpawnFan(int slot, Vector3 at)
    {
        var go = _makeFan != null ? _makeFan(_fanSeed++) : null;
        if (go == null) return null;
        go.transform.SetParent(transform, true);
        go.transform.position = at;

        var fan = new Fan
        {
            t = go.transform,
            look = go.GetComponent<NPCLayeredAppearance>(),
            slot = slot,
            target = at,
            fidgetAt = Time.time + 3f + (float)_rng.NextDouble() * 6f,
        };
        _fans.Add(fan);
        return fan;
    }

    // ------------------------------------------------------------------ running it

    void Update()
    {
        if (_driver == null) { Destroy(gameObject); return; }

        var player = AutographFanSpawner.OnFootPlayer;
        if (player == null && !_dispersing) return;   // player's in the car — a still crowd reads fine from there

        float playerDist = player != null
            ? Vector2.Distance(player.transform.position, transform.position)
            : float.MaxValue;
        if (playerDist > wakeRadius && !_dispersing) return;

        float now = Time.time;
        TickPops(now);

        for (int i = _fans.Count - 1; i >= 0; i--)
        {
            var f = _fans[i];
            if (f.t == null) { _fans.RemoveAt(i); continue; }
            Step(f, now);
        }

        if (_dispersing)
        {
            if (_fans.Count == 0) Destroy(gameObject);
            return;
        }

        bool driverBusy = _driverTalk != null && _driverTalk.IsTalking;
        if (driverBusy)
        {
            // Everyone waits while the driver talks to the player — and the driver looks at the player, which
            // their own conversation already handles.
            if (_turn != null) EndTurn();
            _nextTurnAt = now + 2f;
        }
        else
        {
            if (_turn == null && now >= _nextTurnAt) BeginTurn(now);
            else if (_turn != null) RunTurn(now);
        }

        if (now >= _nextFlashAt)
        {
            Flash();
            _nextFlashAt = now + 1.2f + (float)_rng.NextDouble() * 3.2f;
        }

        if (_bubbleOwner != null && now >= _bubbleHideAt) HideBubble();
        if (!driverBusy && playerDist <= barkRadius && now >= _nextBarkAt && !NPCInteractable.AnyConversationActive)
        {
            Bark();
            _nextBarkAt = now + 5f + (float)_rng.NextDouble() * 5f;
        }
    }

    void Step(Fan f, float now)
    {
        Vector3 to = f.target - f.t.position;
        to.z = 0f;
        float dist = to.magnitude;

        if (dist > 0.05f)
        {
            Vector3 step = to / dist * Mathf.Min(dist, walkSpeed * Time.deltaTime);
            f.t.position += step;
            Face(f.t, step);
            Animate(f);
            return;
        }

        switch (f.state)
        {
            case FanState.Arriving:
                f.state = FanState.Waiting;
                FaceDriver(f);
                Idle(f);
                break;
            case FanState.Leaving:
                Destroy(f.t.gameObject);
                _fans.Remove(f);
                return;
            case FanState.Waiting:
                Idle(f);
                // The small shuffle of somebody waiting in a crowd: a half step and back, now and then.
                if (now >= f.fidgetAt)
                {
                    Vector2 j = Random.insideUnitCircle * 0.18f;
                    f.target = _slots[f.slot] + new Vector3(j.x, j.y, 0f);
                    f.fidgetAt = now + 4f + (float)_rng.NextDouble() * 7f;
                }
                else FaceDriver(f);
                break;
            default:
                Idle(f);
                break;
        }
    }

    // Somebody's turn: an autograph where they stand, or in beside the driver for a photo.
    void BeginTurn(float now)
    {
        _turn = null;
        int waiting = 0;
        foreach (var f in _fans) if (f.state == FanState.Waiting) waiting++;
        if (waiting == 0) { _nextTurnAt = now + 2f; return; }

        // Front row first — the people at the back are waiting for exactly this.
        int pick = _rng.Next(waiting);
        foreach (var f in _fans)
        {
            if (f.state != FanState.Waiting) continue;
            if (pick-- == 0) { _turn = f; break; }
        }
        if (_turn == null) return;

        _turn.state = FanState.Turn;
        _shotTaken = false;
        _selfie = _rng.NextDouble() < 0.45;
        if (_selfie)
        {
            // In at the driver's shoulder, both of them turned toward the mouth of the ring — where the phone is.
            Vector2 side = new Vector2(-_mouth.y, _mouth.x) * (_rng.NextDouble() < 0.5 ? 1f : -1f);
            _turn.target = _driver.position + (Vector3)(side * 0.55f);
            _turnEndsAt = now + 4f;
        }
        else
        {
            // Stepping in half a pace with a hat to sign.
            Vector3 toDriver = _driver.position - _slots[_turn.slot];
            _turn.target = _slots[_turn.slot] + toDriver * 0.3f;
            _turnEndsAt = now + 2.5f + (float)_rng.NextDouble() * 1.5f;
        }
    }

    void RunTurn(float now)
    {
        if (_turn.t == null) { _turn = null; return; }

        bool arrived = (_turn.target - _turn.t.position).sqrMagnitude < 0.01f;
        if (_selfie)
        {
            FaceDriverTowards(_mouth);
            if (arrived)
            {
                Face(_turn.t, _mouth);
                // The photo itself, once, a moment after they've lined up.
                if (!_shotTaken && now >= _turnEndsAt - 2f)
                {
                    _shotTaken = true;
                    FlashAt(_turn.t.position + (Vector3)(_mouth * 0.35f));
                }
            }
        }
        else
        {
            FaceDriverTowards(_turn.t.position - _driver.position);
        }

        if (arrived && now >= _turnEndsAt) EndTurn();
    }

    // Done: this fan heads off through the mouth and the next person in the crowd walks up into their spot.
    void EndTurn()
    {
        var done = _turn;
        _turn = null;
        _nextTurnAt = Time.time + 1.5f + (float)_rng.NextDouble() * 2.5f;
        if (done == null || done.t == null) return;

        int slot = done.slot;
        done.state = FanState.Leaving;
        done.target = transform.position + (Vector3)(Rotate(_mouth, ((float)_rng.NextDouble() - 0.5f) * 60f) * 14f);

        Vector3 from = transform.position + (Vector3)(Rotate(_mouth, ((float)_rng.NextDouble() - 0.5f) * 90f) * 12f);
        var next = SpawnFan(slot, from);
        if (next == null) return;
        next.state = FanState.Arriving;
        next.target = _slots[slot];
    }

    // Everyone goes home: the driver is wanted in the car, or the clock has moved on.
    public void Disperse()
    {
        if (_dispersing) return;
        _dispersing = true;
        _turn = null;
        HideBubble();
        foreach (var f in _fans)
        {
            if (f.t == null) continue;
            f.state = FanState.Leaving;
            Vector2 away = (Vector2)(f.t.position - transform.position);
            if (away.sqrMagnitude < 0.01f) away = _mouth;
            f.target = f.t.position + (Vector3)(away.normalized * 14f);
        }
        if (_driver != null) Destroy(_driver.gameObject);
        _driver = transform;   // keeps Update's null guard quiet while the fans walk off
    }

    // ------------------------------------------------------------------ noise

    void Flash()
    {
        Fan who = null;
        int n = 0;
        foreach (var f in _fans)
            if (f.state == FanState.Waiting && _rng.Next(++n) == 0) who = f;
        if (who == null) return;
        Vector3 dir = _driver.position - who.t.position;
        dir.z = 0f;
        FlashAt(who.t.position + dir.normalized * 0.3f);
    }

    // A phone camera going off: a white pop that is gone in a couple of frames.
    void FlashAt(Vector3 at)
    {
        var go = new GameObject("PhotoFlash");
        go.transform.SetParent(transform, false);
        at.z = -0.7f;
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FlashSprite();
        if (_flashMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _flashMaterial = new Material(sh);
        }
        sr.sharedMaterial = _flashMaterial;
        sr.sortingLayerName = "Vehicles";
        sr.sortingOrder = 40;
        _pops.Add(new Pop { sr = sr, born = Time.time });
    }

    void TickPops(float now)
    {
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var p = _pops[i];
            if (p.sr == null) { _pops.RemoveAt(i); continue; }
            float k = Mathf.Clamp01((now - p.born) / PopLife);
            p.sr.transform.localScale = Vector3.one * (0.7f + k * 0.6f);
            p.sr.color = new Color(1f, 1f, 1f, 1f - k);
            if (k >= 1f) { Destroy(p.sr.gameObject); _pops.RemoveAt(i); }
        }
    }

    static Sprite FlashSprite()
    {
        if (_flashSprite != null) return _flashSprite;
        const int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f - 0.5f, s * 0.5f - 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (s * 0.5f);
                // A hard white core and a four-point glint, pixel-art style.
                bool core = d < 0.35f;
                bool ray = (Mathf.Abs(x - c.x) < 0.6f || Mathf.Abs(y - c.y) < 0.6f) && d < 1f;
                px[y * s + x] = core || ray ? new Color32(255, 255, 240, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        // 16 px across ≈ 0.6 m, just under a head.
        _flashSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s / 0.6f);
        return _flashSprite;
    }

    static readonly string[] kBarks =
    {
        "{driver}! Over here! {driver}!",
        "Can I get a picture? Just one!",
        "Sign my hat, please!",
        "My kid's got your diecast on his bedroom shelf!",
        "Win it Sunday, {driver}!",
        "I drove nine hours for this!",
        "Can you sign my arm? I'm getting it tattooed.",
        "Is that a real Sharpie? Do the big loop!",
        "We love you, {driver}!",
        "Hold still — the flash didn't go off!",
    };

    static readonly string[] kDriverBarks =
    {
        "One at a time, folks — I'm not going anywhere.",
        "Who's got a pen that works?",
        "Thanks for coming out, seriously.",
        "Say cheese!",
    };

    // One voice from the crowd at a time, only with the player close enough to hear it — and never over a
    // real conversation.
    void Bark()
    {
        Transform who;
        string line;
        if (_rng.NextDouble() < 0.2 && _driverTalk != null)
        {
            who = _driver;
            line = kDriverBarks[_rng.Next(kDriverBarks.Length)];
        }
        else
        {
            Fan f = null;
            int n = 0;
            foreach (var x in _fans)
                if ((x.state == FanState.Waiting || x.state == FanState.Turn) && _rng.Next(++n) == 0) f = x;
            if (f == null) return;
            who = f.t;
            line = kBarks[_rng.Next(kBarks.Length)].Replace("{driver}", _driverFirst);
        }

        HideBubble();
        _bubble = SpeechBubble.Attach(who);
        string speaker = who == _driver && _driverTalk != null ? _driverTalk.speakerName : null;
        if (!_bubble.Speak(line, speaker, Draftmaster.Sim.SpeechPriority.Ambient))
        {
            Destroy(_bubble.gameObject);
            _bubble = null;
            return;
        }
        _bubbleOwner = who;
        _bubbleHideAt = Time.time + AmbientChatter.ReadSeconds(line);
    }

    void HideBubble()
    {
        if (_bubble != null)
        {
            _bubble.Hide();
            Destroy(_bubble.gameObject);
        }
        _bubble = null;
        _bubbleOwner = null;
    }

    void OnDestroy() => HideBubble();

    // ------------------------------------------------------------------ bodies

    void FaceDriver(Fan f)
    {
        if (_driver == null || f.t == null) return;
        Face(f.t, _driver.position - f.t.position);
    }

    void FaceDriverTowards(Vector2 dir)
    {
        if (_driver == null || _driver == transform || dir.sqrMagnitude < 1e-4f) return;
        // A driver on their feet is never mid-conversation here (the busy check above), so turning is ours.
        OnFootController.ApplyFacing(_driver, _driverBody, dir, 90f);
    }

    static void Face(Transform t, Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        OnFootController.ApplyFacing(t, null, new Vector2(dir.x, dir.y), 90f);
    }

    void Animate(Fan f)
    {
        if (f.look == null || f.look.FrameCount == 0) return;
        f.frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(0.01f, walkFrameRate);
        while (f.frameTimer >= step)
        {
            f.frameTimer -= step;
            f.frame++;
            f.look.SetFrame(f.frame);
        }
    }

    static void Idle(Fan f)
    {
        if (f.frame == 0) return;
        f.frame = 0;
        f.look?.SetFrame(0);
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
