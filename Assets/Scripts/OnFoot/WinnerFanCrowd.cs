using System.Collections.Generic;
using Draftmaster.Chatter;
using Draftmaster.Fans;
using UnityEngine;

// The race winner gets mobbed. The first time the player is on foot after winning (climbed out in the box, or
// stood beside the car by END RACE) a crowd of fans comes streaming in from every side — after an autograph or a
// photo with the winner, shouting their name, phones going off — and follows them round the paddock: a pack at
// their heels while they walk, packed in on every side the moment they stop.
//
// Each fan is a WinnerFan, so any of them can be talked to (E): the last line signs their programme or takes the
// picture, and they go off happy, the rest shuffling up to fill the gap. Nobody gives up and sulks — a winner's
// fans would sooner keep following — but the crowd melts away if the winner gets back in a car.
//
// Added by RaceDirector.Start, so it only exists in a single-player race scene, and does nothing unless
// RaceDirector.PlayerWon. One Update walks the whole crowd, as FanMob does. Fans have no physics and no colliders:
// the player walks through them, which is how you get through a crowd that wants you.
public class WinnerFanCrowd : MonoBehaviour
{
    [Header("Crowd size (scales with FanAppeal)")]
    [Tooltip("Fans who come for a winner at 0 appeal.")]
    public int minFans = 10;
    [Tooltip("Fans who come for a winner at 100 appeal.")]
    public int maxFans = 16;

    [Header("Arrival")]
    [Tooltip("Fans appear between these distances (m) from the winner and walk in.")]
    public float spawnMin = 14f;
    public float spawnMax = 22f;
    [Tooltip("Seconds over which the whole crowd turns up, so it reads as people arriving, not appearing.")]
    public float arriveSeconds = 5f;

    [Header("Walking")]
    public float walkSpeed = 1.7f;
    [Tooltip("Speed (m/s) of a fan more than jogBeyond metres from their place — fast enough to keep a walking winner in sight, slower than a running one.")]
    public float jogSpeed = 3.1f;
    public float jogBeyond = 4f;
    public float walkFrameRate = 6f;
    [Tooltip("The winner counts as on the move above this speed (m/s), and the crowd trails rather than rings them.")]
    public float movingSpeed = 0.6f;

    [Header("Noise")]
    [Tooltip("Fans call out while the winner is within this distance (m) of any of them.")]
    public float barkRadius = 10f;
    [Tooltip("Fans this close (m) to a standing winner snap pictures of them on their own.")]
    public float photoRadius = 4.5f;
    [Tooltip("Seconds without anyone on foot (back in a car) before the crowd gives up and goes home.")]
    public float leaveAfterDriving = 8f;

    class Fan
    {
        public Transform t;
        public WinnerFan talk;
        public NPCLayeredAppearance look;
        public bool leaving;
        public Vector3 leaveTo;
        public float leftAt;
        public float frameTimer;
        public int frame;
    }

    readonly List<Fan> _fans = new();
    readonly List<Fan> _ringed = new();   // scratch: fans still after the winner, in slot order
    bool _started;
    int _toSpawn;
    float _nextSpawnAt;
    float _spawnGap;
    int _seed;

    AutographFanSpawner _dresser;   // owns the fans' wardrobe
    TrackBuilder _track;
    Transform _root;

    Vector2 _lastWinnerPos;
    Vector2 _winnerVel;
    bool _haveWinnerPos;
    float _noPlayerFor;

    float _nextBarkAt;
    float _nextPhotoAt;
    SpeechBubble _bubble;
    float _bubbleHideAt;

    struct Pop { public SpriteRenderer sr; public float born; }
    readonly List<Pop> _pops = new();
    const float PopLife = 0.14f;
    const float LeaveSeconds = 12f;

    void Update()
    {
        var player = OnFootController.Current;
        float now = Time.time;

        if (!_started)
        {
            if (player != null && RaceDirector.PlayerWon) Begin(player, now);
            return;
        }

        TickPops(now);
        if (_bubble != null && now >= _bubbleHideAt) HideBubble();

        if (player == null)
        {
            // Back in a car: everyone stands where they are, and after a while gives up and goes home.
            _noPlayerFor += Time.deltaTime;
            _haveWinnerPos = false;
            if (_noPlayerFor >= leaveAfterDriving) SendEveryoneHome();
            StepLeavers();
            Finish();
            return;
        }
        _noPlayerFor = 0f;

        // Indoors in the motorhome: the crowd waits outside the door.
        bool indoors = RVInterior.Occupied != null;

        Vector2 winner = player.transform.position;
        TrackWinner(winner);
        bool moving = _winnerVel.magnitude > movingSpeed;

        if (_toSpawn > 0 && now >= _nextSpawnAt && !indoors)
        {
            Spawn(winner);
            _toSpawn--;
            _nextSpawnAt = now + _spawnGap;
        }

        bool talking = NPCInteractable.AnyConversationActive;
        _ringed.Clear();
        foreach (var f in _fans)
        {
            if (f.t == null || f.leaving) continue;
            if (f.talk == null || f.talk.Satisfied) { GoHome(f, winner); continue; }
            _ringed.Add(f);
        }

        for (int i = 0; i < _ringed.Count; i++)
        {
            var f = _ringed[i];
            Vector2 toWinner = winner - (Vector2)f.t.position;
            // Mid-conversation (this fan's or anybody's) or the winner indoors: hold still and watch.
            if (talking || indoors || f.talk.IsTalking)
            {
                if (!indoors) Face(f.t, toWinner);
                Idle(f);
                continue;
            }
            Vector2 slot = WinnerCrowdLayout.Slot(i, _ringed.Count, winner, _winnerVel, moving);
            float dist = Vector2.Distance(slot, f.t.position);
            float speed = dist > jogBeyond ? jogSpeed : walkSpeed;
            if (!Walk(f, slot, speed, moving ? (Vector2?)null : toWinner)) Face(f.t, toWinner);
        }

        StepLeavers();

        if (!indoors && !talking && _ringed.Count > 0)
        {
            if (!moving && now >= _nextPhotoAt)
            {
                SnapAPicture(winner);
                _nextPhotoAt = now + 0.8f + Random.value * 2.2f;
            }
            if (now >= _nextBarkAt)
            {
                Bark(winner, moving);
                _nextBarkAt = now + 3.5f + Random.value * 3.5f;
            }
        }

        Finish();
    }

    void Begin(OnFootController player, float now)
    {
        _started = true;
        _dresser = FindFirstObjectByType<AutographFanSpawner>();
        _track = FindFirstObjectByType<TrackBuilder>();
        // No fan wardrobe means no pit lane and no paddock to be mobbed in.
        if (_dresser == null) { enabled = false; return; }

        _root = new GameObject("WinnerFans").transform;
        _root.SetParent(transform, false);
        _toSpawn = FanAppeal.FanCountForAppeal(FanAppeal.Value, minFans, maxFans);
        _spawnGap = arriveSeconds / Mathf.Max(1, _toSpawn);
        _nextSpawnAt = now;
        _seed = 7000 + Random.Range(0, 10000);
        _nextBarkAt = now + 1.5f;
        _nextPhotoAt = now + 3f;
    }

    // All the fans have been and gone: the crowd is over for this scene.
    void Finish()
    {
        if (_toSpawn > 0 || _fans.Count > 0) return;
        HideBubble();
        if (_pops.Count == 0) enabled = false;
    }

    void TrackWinner(Vector2 pos)
    {
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        if (!_haveWinnerPos)
        {
            _haveWinnerPos = true;
            _lastWinnerPos = pos;
            _winnerVel = Vector2.zero;
            return;
        }
        Vector2 v = (pos - _lastWinnerPos) / dt;
        // A teleport (through a door, into a car and out) is not a walk.
        if (v.sqrMagnitude > 20f * 20f) v = Vector2.zero;
        _winnerVel = Vector2.Lerp(_winnerVel, v, 1f - Mathf.Exp(-dt * 6f));
        _lastWinnerPos = pos;
    }

    // ------------------------------------------------------------------ the people

    void Spawn(Vector2 winner)
    {
        Vector3 pos = Vector3.zero;
        bool found = false;
        for (int tries = 0; tries < 16 && !found; tries++)
        {
            Vector2 p = winner + Random.insideUnitCircle.normalized * Random.Range(spawnMin, spawnMax);
            pos = new Vector3(p.x, p.y, -0.1f);   // toward the camera, in front of the ground and tarmac
            found = !AutographFan.OnTrack(_track, pos);
        }
        if (!found) return;

        int seed = _seed++;
        var go = new GameObject("WinnerFan");
        go.transform.SetParent(_root, false);
        go.transform.position = pos;
        _dresser.BuildAppearance(go, seed);

        var talk = go.AddComponent<WinnerFan>();
        talk.crowd = this;
        talk.wantsPhoto = Random.value < 0.45f;
        var pool = talk.wantsPhoto ? kPhotoLines : kAutographLines;
        talk.lines = pool[Mathf.Abs(seed) % pool.Length];
        talk.speakerName = "Fan";

        var f = new Fan { t = go.transform, talk = talk, look = go.GetComponent<NPCLayeredAppearance>() };
        Face(f.t, winner - (Vector2)pos);
        _fans.Add(f);
    }

    void GoHome(Fan f, Vector2 from)
    {
        f.leaving = true;
        f.leftAt = Time.time;
        Vector2 away = (Vector2)f.t.position - from;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
        if (away.sqrMagnitude < 1e-6f) away = Vector2.down;
        Vector2 to = (Vector2)f.t.position + away.normalized * 18f;
        f.leaveTo = new Vector3(to.x, to.y, f.t.position.z);
    }

    void SendEveryoneHome()
    {
        foreach (var f in _fans)
            if (f.t != null && !f.leaving) GoHome(f, _haveWinnerPos ? _lastWinnerPos : (Vector2)transform.position);
        _toSpawn = 0;
    }

    void StepLeavers()
    {
        for (int i = _fans.Count - 1; i >= 0; i--)
        {
            var f = _fans[i];
            if (f.t == null) { _fans.RemoveAt(i); continue; }
            if (!f.leaving) continue;
            // Blocked by the track: try the other way along it. Anyone still about after LeaveSeconds has
            // long since walked off screen.
            if (!Walk(f, f.leaveTo, walkSpeed, null))
            {
                Vector2 here = f.t.position, away = (Vector2)f.leaveTo - here;
                Vector2 side = new Vector2(-away.y, away.x);
                f.leaveTo = new Vector3(here.x + side.x, here.y + side.y, f.leaveTo.z);
            }
            if (Vector2.Distance(f.t.position, f.leaveTo) < 0.1f || Time.time - f.leftAt > LeaveSeconds)
            {
                Destroy(f.t.gameObject);
                _fans.RemoveAt(i);
            }
        }
    }

    // One step toward `target`, kept off the track (a fan slides along the edge of the tarmac rather than
    // following the winner onto it). False when there was nowhere to go — arrived, or blocked by the track.
    bool Walk(Fan f, Vector2 target, float speed, Vector2? faceWhenThere)
    {
        Vector3 here = f.t.position;
        Vector2 to = target - (Vector2)here;
        float dist = to.magnitude;
        if (dist < 0.08f)
        {
            if (faceWhenThere.HasValue) Face(f.t, faceWhenThere.Value);
            Idle(f);
            return true;
        }

        float stepLen = Mathf.Min(dist, speed * Time.deltaTime);
        Vector2 next = FanFooting.Step(here, to / dist, stepLen, p => AutographFan.OnTrack(_track, p));
        Vector2 moved = next - (Vector2)here;
        if (moved.sqrMagnitude < 1e-10f)
        {
            Idle(f);
            return false;
        }
        f.t.position = new Vector3(next.x, next.y, here.z);
        Face(f.t, moved);
        Animate(f);
        return true;
    }

    static void Face(Transform t, Vector2 dir)
    {
        if (t == null || dir.sqrMagnitude < 1e-6f) return;
        OnFootController.ApplyFacing(t, null, dir, 90f);
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

    // ------------------------------------------------------------------ pictures

    // Somebody near the front gets one of the winner on their phone.
    void SnapAPicture(Vector2 winner)
    {
        Fan who = null;
        int n = 0;
        foreach (var f in _ringed)
            if (Vector2.Distance(f.t.position, winner) <= photoRadius && Random.Range(0, ++n) == 0) who = f;
        if (who == null) return;
        Vector2 dir = winner - (Vector2)who.t.position;
        FlashAt((Vector2)who.t.position + dir.normalized * 0.3f);
    }

    // A posed photo with the winner: the shutter goes a couple of times, as it always does.
    public void PhotoTaken(WinnerFan fan)
    {
        if (fan == null) return;
        Vector2 at = fan.transform.position;
        FlashAt(at + Random.insideUnitCircle * 0.15f);
        FlashAt(at + Random.insideUnitCircle * 0.15f, 0.25f);
    }

    void FlashAt(Vector2 at, float delay = 0f)
    {
        var go = new GameObject("PhotoFlash");
        go.transform.SetParent(_root != null ? _root : transform, false);
        go.transform.position = new Vector3(at.x, at.y, -0.7f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FanMob.FlashSprite();
        sr.sharedMaterial = FanMob.FlashMaterial();
        sr.sortingLayerName = "Vehicles";
        sr.sortingOrder = 40;
        sr.enabled = delay <= 0f;
        _pops.Add(new Pop { sr = sr, born = Time.time + delay });
    }

    void TickPops(float now)
    {
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var p = _pops[i];
            if (p.sr == null) { _pops.RemoveAt(i); continue; }
            if (now < p.born) continue;
            p.sr.enabled = true;
            float k = Mathf.Clamp01((now - p.born) / PopLife);
            p.sr.transform.localScale = Vector3.one * (0.7f + k * 0.6f);
            p.sr.color = new Color(1f, 1f, 1f, 1f - k);
            if (k >= 1f) { Destroy(p.sr.gameObject); _pops.RemoveAt(i); }
        }
    }

    // ------------------------------------------------------------------ noise

    void Bark(Vector2 winner, bool moving)
    {
        Fan who = null;
        int n = 0;
        foreach (var f in _ringed)
            if (Vector2.Distance(f.t.position, winner) <= barkRadius && Random.Range(0, ++n) == 0) who = f;
        if (who == null) return;

        var pool = moving ? kChaseBarks : kBarks;
        string line = SpeakerIdentity.Fill(pool[Random.Range(0, pool.Length)]);

        HideBubble();
        _bubble = SpeechBubble.Attach(who.t);
        if (!_bubble.Speak(line, null, Draftmaster.Sim.SpeechPriority.Ambient))
        {
            Destroy(_bubble.gameObject);
            _bubble = null;
            return;
        }
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
    }

    void OnDestroy() => HideBubble();

    static readonly string[] kBarks =
    {
        "{playerfirst}! Over here! {playerfirst}!",
        "You won! You actually won!",
        "Sign my program, please!",
        "One picture with the winner!",
        "What a last lap!",
        "Winner, winner! Sign my hat!",
        "Look this way — smile!",
        "I knew you'd do it, {playerfirst}!",
        "Can you sign my shirt? I'll never wash it!",
        "Hold still, the flash didn't go off!",
    };

    static readonly string[] kChaseBarks =
    {
        "Wait up, {playerfirst}!",
        "{playerfirst}! One autograph!",
        "Don't go — just one photo!",
        "Winner! Over here!",
        "Slow down, I've got a Sharpie!",
    };

    // A line ending "#player" is the winner's. Passing the last line signs the autograph.
    static readonly string[][] kAutographLines =
    {
        new[] { "{playerfirst}! You won! Will you sign my program?", "Sure thing. #player", "Unbelievable race. I'm framing this!" },
        new[] { "I called it! I said you'd win today! Autograph, please?", "Thanks for the support. #player", "Wait till my buddies see this!" },
        new[] { "Sign my hat? Right on the brim — for the winner!", "There you go. #player", "Best day of my life!" },
        new[] { "Could you sign this for my kid? They screamed the whole last lap.", "Who do I make it out to? #player", "To the loudest fan in the grandstand. Thank you!" },
        new[] { "I've got your diecast right here. Sign the roof?", "Hold it still. #player", "A winner's signature! It's worth double now!" },
    };

    // A line ending "#player" is the winner's. Passing the last line takes the picture.
    static readonly string[][] kPhotoLines =
    {
        new[] { "A picture with the race winner? Please?!", "Get in here. #player", "Say \"victory lane\"! ...Got it!" },
        new[] { "{playerfirst}, one selfie — my phone's ready!", "Quick one. #player", "That's going straight on my wall." },
        new[] { "Hold up a number one for the photo?", "Number one. #player", "Perfect! Thank you!!" },
        new[] { "My mom will never believe this. Photo?", "Let's prove it to her. #player", "She's gonna lose it. Thank you!" },
    };
}
