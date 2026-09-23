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
// set of them loitering at the garage would be the same five people in two places.
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

    // The activity this gathers for. Only the strategy briefing — the phone orientation is held at the same
    // venue, and that is the chief showing a new driver their phone, not a team meeting.
    public const ActivityKind Gathers = ActivityKind.TeamBriefing;

    readonly List<GameObject> _crew = new();
    Transform _root;
    float _timer;
    bool _dressed;
    bool _stayForThisMeeting;

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

    // Whether the crew should be stood here now: the briefing is the booking in hand at this venue.
    public static bool Wanted(WeekendActivity pending, WeekendVenue venue) =>
        pending != null && pending.kind == Gathers && WeekendVenues.For(pending.kind) == venue;

    void Awake()
    {
        if (chief == null) chief = GetComponent<WeekendVenueHost>();
    }

    void Update()
    {
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

        if (wanted && _crew.Count == 0) Gather();
        if (_root != null && _root.gameObject.activeSelf != wanted)
        {
            _root.gameObject.SetActive(wanted);
            if (wanted) Place();
        }
        if (wanted && !_dressed) Dress();
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
