using System.Collections.Generic;
using Draftmaster.Data;
using UnityEngine;

// Puts every driver in the field somewhere the player can actually walk up to. Reads the roster and
// the parked row from DriverMotorhomeLot and gives each driver exactly one presence:
//
//   InCar   — already sat in their car in the pit box. Nothing is spawned; an NPCInteractable is
//             bolted onto the car itself, so walking up and pressing E is a window-down chat.
//   AtRV    — stood a couple of metres outside their own motorhome door, facing the aisle.
//   Walking — wandering the open aisle in front of their row (PaddockWalker), stopping to talk.
//
// Which driver does what is seeded from their car number and the weekend id: stable across a
// reload of the same session, reshuffled next weekend.
//
// Self-installing via DriverMotorhomeLot — that component creates one of these once its row is
// built, so there is no scene wiring and no ordering problem.
public class DriverPresenceDirector : MonoBehaviour
{
    public enum Presence { InCar, AtRV, Walking }

    [Header("Mix")]
    [Tooltip("Relative weight of drivers sat in their cars.")]
    public float weightInCar = 1f;
    [Tooltip("Relative weight of drivers stood outside their motorhome.")]
    public float weightAtRV = 1f;
    [Tooltip("Relative weight of drivers wandering the lot.")]
    public float weightWalking = 1f;

    [Header("Placement")]
    [Tooltip("How far out from their motorhome door a driver stands (m).")]
    public float rvStandDistance = 2.4f;
    [Tooltip("Walk speed for drivers wandering the lot (m/s). Slower than crew — they're killing time.")]
    public float walkSpeed = 0.9f;
    [Tooltip("Talk range for a driver on foot (m).")]
    public float footTalkRange = 2.2f;
    [Tooltip("Talk range for a driver sat in their car (m). Bigger than a person's: the car is metres long and the player stands off its flank.")]
    public float carTalkRange = 4f;
    [Tooltip("How high the speech bubble floats above a car (m). Clears the bodywork without leaving the top of the on-foot camera (ortho size 3.5).")]
    public float carBubbleHeight = 2f;

    [Header("Look")]
    [Tooltip("Paper-doll outfit library. Falls back to Resources/NPC/NPCPartLibrary, then a coloured placeholder.")]
    public NPCPartLibrary partLibrary;
    [Tooltip("World height a driver renders at. Matches the on-foot player and the pit crew.")]
    public float driverHeightM = PitCrewSpawner.OnFootPersonHeight;
    public string sortingLayerName = "Vehicles";
    [Tooltip("Drivers draw above the cars (car sorting order is ~5) and the motorhomes.")]
    public int baseSortingOrder = 10;

    [Header("Fights")]
    [Tooltip("Let a driver you've fallen out with on track square up to you in the paddock (DriverFight). Off = rivals still argue, but it never goes further.")]
    public bool allowFights = true;

    [Header("Budget")]
    [Tooltip("Cap on spawned driver NPCs. Drivers past the cap are left sat in their cars, which costs nothing — or, between sessions when there are no cars, left out of the paddock entirely. Their motorhome is parked either way. 0 = no cap.")]
    public int maxSpawnedDrivers = 16;

    Material _unlit;
    Transform _root;

    // A driver sat in a parked car, and where that car was parked when they were seated.
    struct SeatedDriver { public NPCInteractable talk; public Vector3 parkedAt; }
    readonly List<SeatedDriver> _seated = new();
    float _seatPoll;

    // Built by DriverMotorhomeLot the moment its row exists.
    public static DriverPresenceDirector Create(DriverMotorhomeLot lot)
    {
        var go = new GameObject("DriverPresenceDirector");
        var d = go.AddComponent<DriverPresenceDirector>();
        d.Populate(lot);
        return d;
    }

    public void Populate(DriverMotorhomeLot lot)
    {
        if (lot == null) return;
        if (partLibrary == null) partLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");

        _root = new GameObject("PaddockDrivers").transform;
        _root.SetParent(transform, false);

        int weekend = RaceWeekend.WeekendId;
        int spawned = 0, inCar = 0, atRv = 0, walking = 0;

        foreach (var slot in lot.Slots)
        {
            if (slot.isPlayer) continue;      // the player is the player — they're not an NPC in their own paddock

            var presence = Pick(slot.carNumber, weekend);
            bool budgetLeft = maxSpawnedDrivers <= 0 || spawned < maxSpawnedDrivers;
            // No car to sit in (a driver with no entry on track) forces them out into the paddock;
            // a spent budget forces the opposite.
            if (presence == Presence.InCar && slot.car == null) presence = Presence.AtRV;
            if (presence != Presence.InCar && !budgetLeft) presence = Presence.InCar;
            if (presence == Presence.InCar && slot.car == null) continue;   // nowhere to put them at all

            switch (presence)
            {
                case Presence.InCar: SeatInCar(slot); inCar++; break;
                case Presence.AtRV: StandAtRV(slot); atRv++; spawned++; break;
                case Presence.Walking: WalkTheLot(slot); walking++; spawned++; break;
            }
        }

        Debug.Log($"DriverPresenceDirector: {inCar} in their cars, {atRv} at their motorhomes, {walking} walking the lot.", this);
    }

    // Deterministic per driver per weekend, so the paddock doesn't reshuffle every time the scene reloads.
    Presence Pick(int carNumber, int weekendId)
    {
        float a = Mathf.Max(0f, weightInCar);
        float b = Mathf.Max(0f, weightAtRV);
        float c = Mathf.Max(0f, weightWalking);
        float total = a + b + c;
        if (total <= 0f) return Presence.InCar;

        var rng = new System.Random(carNumber * 92821 + weekendId * 7717 + 13);
        float roll = (float)rng.NextDouble() * total;
        if (roll < a) return Presence.InCar;
        if (roll < a + b) return Presence.AtRV;
        return Presence.Walking;
    }

    // ---------------------------------------------------------------- presences

    // Sat in the car in their pit box. The car already exists — it just becomes talkable.
    void SeatInCar(DriverMotorhomeLot.Slot slot)
    {
        if (slot.car.GetComponent<NPCInteractable>() != null) return;  // already talkable, don't stack

        var talk = slot.car.AddComponent<NPCInteractable>();
        talk.speakerName = slot.fullName;
        talk.lines = LinesFor(slot, Presence.InCar);
        talk.interactRange = carTalkRange;
        talk.bubbleHeadHeight = carBubbleHeight;
        talk.turnsToFace = false;   // the car is parked on its heading — never swivel it at the player
        _seated.Add(new SeatedDriver { talk = talk, parkedAt = slot.car.transform.position });
    }

    void StandAtRV(DriverMotorhomeLot.Slot slot)
    {
        Vector3 pos = slot.doorPosition + (Vector3)(slot.doorDirection * rvStandDistance);
        pos.z = -0.6f;   // in front of the motorhome body (rvZ -0.5), same band the player walks in

        var go = BuildDriver(slot, pos, "Driver_AtRV");
        // Face out of the door, into the aisle, so they're looking at whoever walks up.
        OnFootController.ApplyFacing(go.transform, go.GetComponent<Rigidbody2D>(), slot.doorDirection, 90f);

        var talk = MakeTalkable(go, slot, Presence.AtRV);
        talk.interactRange = footTalkRange;
    }

    void WalkTheLot(DriverMotorhomeLot.Slot slot)
    {
        Vector3 pos = slot.aisleCenter;
        pos.z = -0.6f;

        var go = BuildDriver(slot, pos, "Driver_Walking");

        var talk = MakeTalkable(go, slot, Presence.Walking);
        talk.interactRange = footTalkRange;

        // Confined to the open band in front of their own row, so they never walk through a parked
        // motorhome and stay recognisably "that driver, outside their rig".
        var walker = go.AddComponent<PaddockWalker>();
        walker.speed = walkSpeed;
        walker.conversation = talk;   // stand still and turn to face whoever stops them
        walker.Configure(slot.aisleCenter, slot.aisleAlong, slot.aisleOut, slot.aisleHalfLen, slot.aisleHalfDepth);
    }

    // A driver you can walk up to is a RivalDriverNPC, not a plain NPCInteractable: it behaves exactly like
    // one until the pair's relationship falls past DriverRelationships.RivalThreshold, at which point the
    // conversation turns into an argument and offers the option to square up (see DriverFight).
    //
    // Only drivers who are actually stood on their feet get this — a driver sat in their car is talked to
    // through the window and has nowhere to fight from.
    RivalDriverNPC MakeTalkable(GameObject go, DriverMotorhomeLot.Slot slot, Presence presence)
    {
        var talk = go.AddComponent<RivalDriverNPC>();
        talk.speakerName = slot.fullName;
        talk.lines = LinesFor(slot, presence);
        talk.allowFights = allowFights;

        // The relationship identity MUST be the name this driver races under (their DriverLabel), not their
        // full name: DriverRelationships keys on the label, so "Ross Chastain" and "Chastain" are two
        // different people to it and a feud earned on track would never reach the paddock.
        var d = RosterLookup.ByCarNumber(slot.carNumber);
        string label = RosterLookup.LabelName(d);
        if (string.IsNullOrEmpty(label)) label = !string.IsNullOrEmpty(slot.shortName) ? slot.shortName : slot.fullName;
        talk.driverName = label;

        if (d != null) talk.aggression = Mathf.Clamp(d.Aggression, 1, 20);
        return talk;
    }

    // ---------------------------------------------------------------- construction

    GameObject BuildDriver(DriverMotorhomeLot.Slot slot, Vector3 pos, string prefix)
    {
        var go = new GameObject($"{prefix}_{slot.carNumber}_{slot.shortName}");
        go.transform.SetParent(_root, false);
        go.transform.position = pos;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Same paper-doll build as the paddock crowd and the autograph fans, seeded off the car
        // number so a given driver looks the same every time you see them.
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = partLibrary;
        layered.layerMaterial = UnlitSprite();
        layered.sortingLayerName = sortingLayerName;
        layered.baseSortingOrder = baseSortingOrder;

        if (partLibrary != null && layered.Build(slot.carNumber * 31 + 7))
        {
            float frameWorldH = Mathf.Max(0.01f, partLibrary.frameHeight / Mathf.Max(1f, partLibrary.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (driverHeightM / frameWorldH);
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(new Color(0.35f, 0.6f, 0.9f), driverHeightM);   // blue blob = driver
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
        }

        return go;
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

    // A driver is only chattable while their car is actually stood in its box. Drop the conversation
    // once the car leaves — a practice stint, the formation lap, the race. Keyed off the car having
    // MOVED rather than the race phase, because a practice session already sits at RaceStart.Green
    // while the whole field is still parked.
    void Update()
    {
        if (_seated.Count == 0) return;

        _seatPoll -= Time.deltaTime;
        if (_seatPoll > 0f) return;
        _seatPoll = 0.5f;

        const float rolledAway = 4f;   // metres; comfortably past any settling nudge from the physics
        for (int i = _seated.Count - 1; i >= 0; i--)
        {
            var s = _seated[i];
            if (s.talk == null) { _seated.RemoveAt(i); continue; }
            if ((s.talk.transform.position - s.parkedAt).sqrMagnitude < rolledAway * rolledAway) continue;

            s.talk.EndConversation();
            Destroy(s.talk);
            _seated.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- dialogue

    // Lines are built per driver so they land as that person: their number, their team, and a tone
    // taken from the roster stats (a 19-Aggression driver talks like one). "#player" marks the
    // driver's own reply, spoken in the player's bubble.
    string[] LinesFor(DriverMotorhomeLot.Slot slot, Presence presence)
    {
        var d = RosterLookup.ByCarNumber(slot.carNumber);
        var rng = new System.Random(slot.carNumber * 5449 + (int)presence * 101);

        string team = string.IsNullOrEmpty(slot.teamName) ? "the team" : slot.teamName;
        string num = slot.carNumber > 0 ? $"the {slot.carNumber}" : "my car";

        // Tone buckets, from the stats the roster already carries.
        int aggression = d != null ? d.Aggression : 10;
        int prestige = d != null ? d.Prestige : 10;
        bool spiky = aggression >= 15;
        bool veteran = prestige >= 15;

        string[][] pool;
        switch (presence)
        {
            case Presence.InCar:
                pool = spiky ? kInCarSpiky : veteran ? kInCarVeteran : kInCarNeutral;
                break;
            case Presence.AtRV:
                pool = spiky ? kAtRvSpiky : veteran ? kAtRvVeteran : kAtRvNeutral;
                break;
            default:
                pool = spiky ? kWalkingSpiky : veteran ? kWalkingVeteran : kWalkingNeutral;
                break;
        }

        var chosen = pool[rng.Next(pool.Length)];
        var lines = new string[chosen.Length];
        for (int i = 0; i < chosen.Length; i++)
            lines[i] = chosen[i].Replace("{team}", team).Replace("{num}", num);
        return lines;
    }

    static readonly string[][] kInCarNeutral =
    {
        new[] { "Seat time. Every minute in here is a minute I'm not guessing later.", "Anything working for you? #player", "Front end's honest. Ask me again when the tyres go off." },
        new[] { "Don't mind me — I'm just sat here running the corners in my head.", "Does that actually help? #player", "More than the data does, some weeks." },
        new[] { "{team} reckon we're half a second off. Feels closer than that from in here.", "It usually does. #player", "Ha. Fair." },
    };

    static readonly string[][] kInCarVeteran =
    {
        new[] { "Been climbing into one of these for twenty years and it still gets me on a Friday.", "Never wears off? #player", "Not once. You'll see." },
        new[] { "Sat in {num} longer than some of these kids have had licences.", "That's a lot of laps. #player", "And I remember every bad one. Good luck out there." },
    };

    static readonly string[][] kInCarSpiky =
    {
        new[] { "You're stood in my mirror.", "Sorry — just walking through. #player", "Then walk. I'm working." },
        new[] { "Whatever you're about to ask, the answer's I'll see you out there.", "Wasn't going to ask anything. #player", "Good. Keep it that way and we'll get on fine." },
        new[] { "Heard you were quick in the last one. We'll find out.", "That's the idea. #player", "Just leave me room in three. Or don't — your call." },
    };

    static readonly string[][] kAtRvNeutral =
    {
        new[] { "Morning. Coffee's on if {team} haven't drunk it all.", "I'd better get to the car. #player", "Go on then. Have a good one out there." },
        new[] { "Never sleep well the night before. You?", "Slept fine, actually. #player", "Must be nice. See you in the pit lane." },
        new[] { "Motorhome's the only quiet place at this circuit.", "Beats the hauler. #player", "Beats everything. Right — see you out there." },
    };

    static readonly string[][] kAtRvVeteran =
    {
        new[] { "Same patch of grass every year, this. Feels like home by now.", "How many have you done here? #player", "Enough to know turn one lies to you. Watch it." },
        new[] { "Take the advice or don't, but nobody wins this on Friday.", "Noted. #player", "Good. Now go earn Sunday." },
    };

    static readonly string[][] kAtRvSpiky =
    {
        new[] { "You lost?", "Just passing. #player", "Then keep passing. Save it for the track." },
        new[] { "You've got a lot of front, knocking on my door.", "It's an open paddock. #player", "So it is. Enjoy it while it's quiet." },
    };

    static readonly string[][] kWalkingNeutral =
    {
        new[] { "Stretching the legs before we get strapped in.", "Long day ahead. #player", "Always is. Good luck out there." },
        new[] { "Looking for my engineer — he owes me a setup sheet and a coffee.", "Haven't seen him. #player", "Story of my weekend. Catch you later." },
        new[] { "Lot's busier than last year. {team} brought half the factory.", "Good sign, surely? #player", "Or a nervous one. We'll see on Sunday." },
    };

    static readonly string[][] kWalkingVeteran =
    {
        new[] { "Walk the paddock every morning. Keeps me honest about who's here.", "Anyone worrying you? #player", "One or two. You, if you get {num} right." },
        new[] { "You'll learn this: the racing's the easy part.", "What's the hard part? #player", "The other twenty-three hours. See you out there." },
    };

    static readonly string[][] kWalkingSpiky =
    {
        new[] { "There you are. We're not done from last time.", "I gave you the room. #player", "You gave me the wall. I'll remember it." },
        new[] { "Nice paint. Shame about the driving.", "Big words in a car park. #player", "They'll be bigger on the track. Go on, off you go." },
    };
}
