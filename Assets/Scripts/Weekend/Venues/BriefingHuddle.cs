using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The crew, gathered round the crew chief for the team strategy briefing.
//
// The briefing is where the weekend's plan is set, and a plan is set with the people who are going to carry
// it out. With only the chief at the box the player walked up to one man stood on his own, which read as a
// chat rather than a meeting. So while the briefing is booked the crew are there too: stood in an arc round
// him, facing in, in the car's colours, with the open side of the ring towards where the driver walks up —
// the player steps into the gap and the circle is complete.
//
// They come for the booking and go when it is done, like the engineer in the motorhome
// (WeekendVenueHostPresence): the rest of the weekend the crew are on pit road with the car, and a second
// set of them loitering at the garage would be the same five people in two places. Going means walking
// back there — each of them heads for their opposite number on the player's pit box and is folded into
// them on arrival — never blinking out of the ring in front of the player. With no box to walk to they
// stay where they stood.
//
// Scenery, not talkers — the chief runs the meeting. Lives on the PitBox host; the crew hang off their own
// root so switching them in and out never touches the host.
public class BriefingHuddle : MonoBehaviour
{
    [Tooltip("The host the crew gather round. Defaults to the WeekendVenueHost on this object.")]
    public WeekendVenueHost chief;

    [Tooltip("How many crew stand round the chief.")]
    [Range(1, 8)] public int crewCount = 5;

    [Tooltip("Distance from the chief to each of the crew, metres.")]
    public float radius = 1.5f;

    [Tooltip("Degrees of the ring left open, centred on the side the player walks up from.")]
    [Range(0f, 270f)] public float gapDegrees = 110f;

    [Tooltip("Rotation added to the facing angle so the sprite's drawn facing lines up, as PaddockWalker's.")]
    public float spriteFacingOffsetDeg = 90f;

    [Tooltip("Seconds between re-checks of the booking.")]
    public float pollSeconds = 0.25f;

    [Tooltip("Walking pace back to the pit box once the meeting is over, metres per second.")]
    public float walkSpeed = 1.2f;

    [Tooltip("How close (m) to their place on the pit box counts as back with the crew.")]
    public float arriveRadius = 0.3f;

    [Tooltip("Walk-cycle playback rate (frames/sec) on the way back.")]
    public float frameRate = 8f;

    [Tooltip("Seconds somebody may spend held up by the scenery on the way back before they give up and " +
             "stand where they are. They are only put away once the camera is no longer on them.")]
    public float stuckSeconds = 3f;

    // The activity this gathers for. Only the strategy briefing — the phone orientation is held at the same
    // venue, and that is the chief showing a new driver their phone, not a team meeting.
    public const ActivityKind Gathers = ActivityKind.TeamBriefing;

    readonly List<GameObject> _crew = new();
    readonly List<float> _stuck = new();   // per body: seconds held up on the way back
    Transform _root;
    PitCrewBox _box;                        // the box they are walking back to; null = stay put
    float _timer;
    bool _dressed;
    bool _stayForThisMeeting;
    bool _present;                          // stood in the ring for a meeting
    bool _leaving;                          // meeting over, walking back to the box

    // Where each of the crew stands, round a chief at `centre` with the player arriving from `towardPlayer`.
    // Pure, so the layout is testable without a scene: every place is `radius` from the chief, and none of
    // them is inside the gap left for the driver.
    public static Vector2[] Places(Vector2 centre, Vector2 towardPlayer, int count, float radius, float gapDegrees)
    {
        var places = new Vector2[Mathf.Max(0, count)];
        if (places.Length == 0) return places;

        Vector2 open = towardPlayer.sqrMagnitude > 1e-6f ? towardPlayer.normalized : Vector2.down;
        float openDeg = Mathf.Atan2(open.y, open.x) * Mathf.Rad2Deg;

        // The arc is everything but the gap. Crew sit at the middle of equal shares of it, so the two at the
        // ends stand half a share in from the gap's edges rather than on them.
        float arc = 360f - Mathf.Clamp(gapDegrees, 0f, 359f);
        float share = arc / places.Length;
        float start = openDeg + (360f - arc) * 0.5f;

        for (int i = 0; i < places.Length; i++)
        {
            float a = (start + share * (i + 0.5f)) * Mathf.Deg2Rad;
            places[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return places;
    }

    // One step of the walk back from `from` towards `to`: `speed * dt` along the line, never past the end.
    // Pure, so the walk is testable without a scene.
    public static Vector2 StepToward(Vector2 from, Vector2 to, float speed, float dt, float arriveRadius, out bool arrived)
    {
        Vector2 delta = to - from;
        float gap = delta.magnitude;
        float arrive = Mathf.Max(0f, arriveRadius);
        if (gap <= arrive) { arrived = true; return from; }
        float step = Mathf.Max(0f, speed) * Mathf.Max(0f, dt);
        arrived = step >= gap - arrive;
        return step >= gap ? to : from + delta / gap * step;
    }

    // Whether the crew should be stood here now: the briefing is the booking in hand at this venue.
    public static bool Wanted(WeekendActivity pending, WeekendVenue venue) =>
        pending != null && pending.kind == Gathers && WeekendVenues.For(pending.kind) == venue;

    void Awake()
    {
        if (chief == null) chief = GetComponent<WeekendVenueHost>();
    }

    void Update()
    {
        if (_leaving) WalkBack(Time.deltaTime);

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, pollSeconds);
        Apply();
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    void Apply()
    {
        if (chief == null) return;

        bool wanted = Wanted(WeekendAppointment.Pending, chief.venue);

        // Settling the briefing clears the appointment on its last line. The crew stay until the chief has
        // finished talking, or the ring breaks up round the player mid-goodbye.
        if (wanted) _stayForThisMeeting = true;
        else if (_stayForThisMeeting && chief.IsTalking) wanted = true;
        else _stayForThisMeeting = false;

        if (wanted)
        {
            if (_crew.Count == 0) Gather();
            // Back for another meeting — from the pit box, or from part way there.
            if (!_present)
            {
                _leaving = false;
                _root.gameObject.SetActive(true);
                foreach (var body in _crew) if (body != null) body.SetActive(true);
                Place();
                _present = true;
            }
            if (!_dressed) Dress();
        }
        else if (_present)
        {
            // The meeting is over. Rather than vanishing from round the player, the crew walk back to the car.
            _present = false;
            _leaving = true;
            _box = PitCrewRegistry.ForBox(PitLane.PlayerBox);
            for (int i = 0; i < _stuck.Count; i++) _stuck[i] = 0f;
        }
    }

    // Everybody heads for their opposite number on the player's pit box, and is put away on arrival — the
    // pit crew standing there are the same people, so one body simply becomes the other. Anybody who can't
    // get there (no box yet, or walled in by the scenery) stands where they are, and is only put away once
    // the camera has moved off them.
    void WalkBack(float dt)
    {
        var members = _box != null ? _box.GetComponentsInChildren<PitCrewMember>() : null;
        bool anyLeft = false;

        for (int i = 0; i < _crew.Count; i++)
        {
            var body = _crew[i];
            if (body == null || !body.activeSelf) continue;
            anyLeft = true;

            var rb = body.GetComponent<Rigidbody2D>();
            var look = body.GetComponent<NPCLayeredAppearance>();
            Vector2 pos = body.transform.position;

            if (_box == null)
            {
                Stand(look);
                continue;
            }

            Transform target = members != null && members.Length > 0 ? members[i % members.Length].transform : _box.transform;
            Vector2 next = StepToward(pos, target.position, walkSpeed, dt, arriveRadius, out bool arrived);
            if (arrived)
            {
                body.SetActive(false);
                continue;
            }

            // Round the bodywork, as every other walker in the paddock goes.
            bool blocked = !PaddockObstacles.TryStep(pos, next, 0.45f, out Vector2 stepped)
                           || (stepped - pos).sqrMagnitude < 1e-8f;
            if (blocked)
            {
                _stuck[i] += dt;
                Stand(look);
                if (_stuck[i] > Mathf.Max(0.5f, stuckSeconds) && !OnCamera(pos)) body.SetActive(false);
                continue;
            }
            _stuck[i] = 0f;

            // Moved outright, as Place() puts them down: this runs per frame, and MovePosition only lands on
            // the next physics step, so several frames' steps between two of them would collapse into one.
            body.transform.position = new Vector3(stepped.x, stepped.y, PaddockPerson.GroundZ);
            if (rb != null) rb.position = stepped;
            OnFootController.ApplyFacing(body.transform, rb, (stepped - pos).normalized, spriteFacingOffsetDeg);

            if (look != null && look.FrameCount > 0)
                look.SetFrame(Mathf.FloorToInt(Time.time * Mathf.Max(0.01f, frameRate)));
        }

        if (!anyLeft)
        {
            _leaving = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }

    static void Stand(NPCLayeredAppearance look)
    {
        if (look != null) look.SetFrame(0);
    }

    static bool OnCamera(Vector2 at)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(new Vector3(at.x, at.y, PaddockPerson.GroundZ));
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    // Build the bodies once. Their places are worked out each time they come back, in case the chief has
    // been moved in between.
    void Gather()
    {
        _root = new GameObject("BriefingCrew").transform;
        _root.SetParent(transform.parent, false);

        int seedBase = ("BriefingCrew" + chief.venue).GetHashCode();
        for (int i = 0; i < crewCount; i++)
        {
            var body = PaddockPerson.Spawn(_root, chief.transform.position, "Crew_" + (i + 1), seedBase + i * 7919);
            _crew.Add(body);
            _stuck.Add(0f);
        }
        Place();
    }

    void Place()
    {
        Vector2 centre = chief.transform.position;

        // The player walks up to the venue's own mark, and the chief stands just off it, so the open side of
        // the ring is the way back to the mark.
        var anchor = WeekendVenueAnchor.Find(chief.venue);
        Vector2 toward = anchor != null ? (Vector2)anchor.transform.position - centre : Vector2.down;

        var places = Places(centre, toward, _crew.Count, radius, gapDegrees);
        for (int i = 0; i < _crew.Count; i++)
        {
            var body = _crew[i];
            if (body == null) continue;

            // Out of the bodywork and inside the fence, as every other body placed in the paddock is.
            Vector2 at = PaddockObstacles.PushOut(places[i], 0.45f, 3f);
            if (PaddockBoundary.AnyActive) at = PaddockBoundary.ConstrainInside(at);

            body.transform.position = new Vector3(at.x, at.y, PaddockPerson.GroundZ);
            var rb = body.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = at;

            Vector2 face = centre - at;
            if (face.sqrMagnitude > 1e-6f)
                OnFootController.ApplyFacing(body.transform, rb, face.normalized, spriteFacingOffsetDeg);
        }
    }

    // The car's colours, as the crew on pit road wear them. The player's car may not have claimed its box
    // yet when the briefing starts, so keep asking until it has; until then they wear what they rolled.
    void Dress()
    {
        var label = PitBoxCars.Label(PitLane.PlayerBox);
        if (label == null) return;

        CarColours.For(label, out Color primary, out Color secondary);
        foreach (var body in _crew)
        {
            var look = body != null ? body.GetComponent<NPCLayeredAppearance>() : null;
            if (look != null) look.WearTeamColours(primary, secondary);
        }
        _dressed = true;
    }
}
