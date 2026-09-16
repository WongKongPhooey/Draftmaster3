using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// Watching somebody else's session from the stand you walked to.
//
// The obligation is to watch, so the booking is done when the player gets up and goes back to the pits (E),
// not when they sit down: arriving used to complete it on the spot, which read as the quest ticking off
// before a single car had gone past. Until then the booking stays open — the sheet still has the player in
// the stand — and walking off some other way (a cutscene, another gate) leaves it undone. The camera pulls
// back off the player onto the vantage the track authored (GrandstandCamera) and the session plays out in
// front of it.
//
// AT SPEED. An hour on the sheet is not an hour of the player's evening: the session is held open against
// its own compressed clock — ten weekend minutes a minute, capped so nothing runs past six — and when that
// clock runs out the field comes in and the circuit goes cold. The cars themselves are never sped up; it is
// the LENGTH of the session that is compressed. Rules and numbers in GrandstandWatch.
//
// This is the in-world half of spectating, and deliberately not GrandstandSpectate — that one is the
// broadcast: it plants the player, simulates the session and draws a timing tower down the side of the
// screen. Here the cars going past are the real field the weekend put on track (GridSpawner's ambient
// session), the player can get up and walk about in the stand, and the interface is one prompt telling them
// how to get back plus a timing screen on a key.
public class GrandstandVisit : MonoBehaviour
{
    public static GrandstandVisit Active { get; private set; }
    public static bool Watching => Active != null;

    // Live timings for whatever is on track. F11 because it is a screen you put up and leave up, not a
    // panel with a button on it — and because F11 was the last function key nothing else had taken.
    const Key TimingKey = Key.F11;

    // The way out is drawn here rather than pushed at the shared control-hint strip. That strip is a
    // teaching aid — small, faint, and gone after a few seconds — and this is a standing instruction on a
    // screen that has nothing else on it. So it gets the kit's own furniture: gold frame, dithered plate,
    // a keycap, at the size every other panel in the game is drawn at.

    // The way out. E is the button the player already uses for everything they do in the world — get in the
    // car, talk to somebody, open the gate that sent them here — so getting up out of the seat is the same
    // press. It used to be T, which is the schedule's TRAVEL THERE key: one press then did both, and the
    // travel's wipe made this one drop its own request on the floor and leave the prompt on screen forever.
    const Key LeaveKey = Key.E;

    // How far out of the seat counts as no longer being in the stand. The viewing pocket a gate builds
    // around its destination is 14x10 m, so anything past this is not somebody stretching their legs: it is
    // a player who has been PUT somewhere else — travelled to the next booking, pulled into a cutscene —
    // and a prompt about a grandstand they are not in would sit on the screen for the rest of the weekend.
    const float LeftTheStandMetres = 25f;

    Vector3 _returnTo;
    float _startedAt;

    // Where the player sat down, so the visit can tell when they are no longer there.
    Vector3 _seat;

    // A leave asked for but not yet taken, because the screen was mid-wipe when it was asked for.
    bool _leaving;

    // The session being watched, its compressed length, and whether it has run out.
    WeekendActivity _activity;
    float _watchSeconds;
    float _watched;             // seconds of the compressed session run so far
    int _sessionMinutes;
    bool _sessionOver;
    bool _held;
    bool _closed;
    bool _done;                 // the booking has been settled (on the way out through E)

    GrandstandCamera _shot;

    // Arrive in the stand. `returnTo` is where the walk started — the gate in the paddock fence — and
    // `marker` is the gate's own marker, which is where the authored vantage lives.
    public static GrandstandVisit Begin(WeekendActivity a, Vector3 returnTo, WeekendMarker marker = null)
    {
        // Closed rather than destroyed: Destroy runs OnDestroy at the END of the frame, and an old visit
        // giving its session back then would release the hold this one is about to take.
        if (Active != null) Active.Close();

        var go = new GameObject("GrandstandVisit");
        var visit = go.AddComponent<GrandstandVisit>();
        visit._returnTo = returnTo;
        visit._startedAt = Time.unscaledTime;
        visit._activity = a;

        // Take the circuit off the clock. The player may have arrived before the session's start time on the
        // sheet, and finishing the booking on the way out shoves the clock to the end of it — the field is
        // spawned off that clock, so it is the hold that keeps the cars the player came to watch on track.
        if (a != null && a.IsSpectate)
        {
            visit._sessionMinutes = Mathf.Max(1, a.minutes);
            visit._watchSeconds = GrandstandWatch.WatchSeconds(visit._sessionMinutes);
            WeekendTrackState.Hold(a.series, WeekendTrackSessions.SessionKind(a.kind), a.id);
            visit._held = true;
        }

        // NOT completed here. The booking stays the appointment while the player is sat in the stand
        // (WeekendObjectiveHUD keeps its marker down meanwhile) and is settled by Leave — see GoBack.

        visit.OpenTheView(marker);
        return visit;
    }

    void OnEnable() { Active = this; }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        Teardown();
    }

    // Stop watching, right now, and give everything back. Safe to call twice, and called synchronously by
    // anything that needs the seat empty before the next frame.
    public void Close()
    {
        Teardown();
        Destroy(gameObject);
    }

    void Teardown()
    {
        if (_closed) return;
        _closed = true;

        ReleaseSession();

        if (_shot != null) { _shot.End(); _shot = null; }

        if (TimingScreenUI.Instance != null)
        {
            TimingScreenUI.Instance.Hide();
            TimingScreenUI.Instance.sessionLabel = "";
            TimingScreenUI.Instance.statusLine = "";
        }
    }

    // The camera pulls back onto the vantage, and the timing loop starts running on the field that is out.
    void OpenTheView(WeekendMarker marker)
    {
        var player = WeekendVenueAnchor.OnFootPlayer();
        Vector3 seat = player != null ? player.position
                     : marker != null ? marker.TeleportPosition
                     : _returnTo;

        _seat = seat;

        bool authored = marker != null && marker.HasCameraView;
        Vector3 view = authored ? marker.CameraViewPosition : seat;
        float zoom = marker != null ? marker.cameraZoom : 0f;
        float pan = marker != null ? marker.cameraPanSeconds : 2.2f;

        _shot = GrandstandCamera.Begin(seat, view, authored, zoom, pan);

        // Sit them square in the stand. The player arrives still pointing whichever way they walked into
        // the gate, which is across the seats as often as not.
        //
        // The stand itself is the answer, not the vantage: a grandstand is a block of seating with one
        // angle, every row in it faces the same way, and somebody sat in it is lined up with the structure
        // rather than aimed at a point. Pointing them at the camera's vantage was close at a stand square
        // to the road and visibly crooked at one on a curve, where the nearest piece of circuit is off to
        // one side of where the seats look.
        if (player != null && _shot != null)
        {
            var walker = player.GetComponent<OnFootController>();
            if (walker != null)
            {
                Vector2 look = StandFacing(seat, _shot.ViewPoint);
                walker.FaceToward(seat + new Vector3(look.x, look.y, 0f));
            }
        }

        // Which way the seats in this stand look, as a unit vector.
    //
    // The ANGLE comes from the stand — the structure the player is sat in — and only the SENSE of it comes
    // from the view, because a stand's rows are an axis and which side of it the road is on is the one
    // thing the geometry does not say. (The `flipFacing` flag says so for the artwork, but only for the
    // artwork, and a track is free to have rotated the whole object instead.) Taking the direction this
    // way is right whichever of those a package did.
    //
    // No stand near the seat — the gate's destination is somewhere else, or the track has none — and the
    // vantage stands in for it, which is what this used to do in every case.
    static Vector2 StandFacing(Vector3 seat, Vector3 view)
    {
        Vector2 toView = view - seat;
        Vector2 fallback = toView.sqrMagnitude > 0.0001f ? toView.normalized : Vector2.down;

        var stands = FindObjectsByType<Grandstand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (stands == null || stands.Length == 0) return fallback;

        Grandstand nearest = null;
        float best = float.MaxValue;
        foreach (var stand in stands)
        {
            if (stand == null) continue;
            float d = DistanceToStand(stand, seat);
            if (d >= best) continue;
            best = d;
            nearest = stand;
        }

        // Sat in it, not merely nearest to it. A seat further away than this is not in a stand at all and
        // borrowing that stand's angle would point the player at nothing in particular.
        const float SatInItMetres = 12f;
        if (nearest == null || best > SatInItMetres) return fallback;

        // Local +Y is the stand's depth, so the rows look along its Y axis; the road decides which end.
        Vector2 axis = nearest.transform.up;
        if (Vector2.Dot(axis, toView) < 0f) axis = -axis;
        return axis.sqrMagnitude > 0.0001f ? axis.normalized : fallback;
    }

    // How far the seat is from the stand's own footprint, rather than from its centre. A stand is up to
    // 120 m long, so the middle of it can be most of a straight away from somebody sitting at one end.
    static float DistanceToStand(Grandstand stand, Vector3 seat)
    {
        Vector3 local = stand.transform.InverseTransformPoint(seat);
        float halfLength = Mathf.Max(0.1f, stand.length * 0.5f);
        float halfDepth = Mathf.Max(0.1f, stand.depth * 0.5f);

        float x = Mathf.Abs(local.x) - halfLength;
        float y = Mathf.Abs(local.y) - halfDepth;
        return new Vector2(Mathf.Max(0f, x), Mathf.Max(0f, y)).magnitude;
    }

    // Nothing else times an ambient session — there is no practice or race director out here, because
        // none of it is the player's — so the stand brings its own lap timing. It reads the same field the
        // player would be timed against if they were in it.
        LapTimingManager.Ensure();
        var timing = TimingScreenUI.Ensure();
        timing.sessionLabel = SessionLabel();
        timing.Hide();
    }

    void Update()
    {
        // A moment's grace: the key that got you here should not also take you straight back out.
        if (Time.unscaledTime - _startedAt < 0.4f) return;

        // Gone from the stand by some other road than the way out — travelled to the next booking, moved by
        // a cutscene, put down somewhere else by another gate. The seat is empty either way, so stand down
        // rather than leaving a prompt about a grandstand over the top of the paddock.
        if (LeftTheStand()) { Close(); return; }

        // A leave that arrived mid-wipe, taken as soon as the screen is free. Dropping it instead was the
        // whole bug: the player ended up back in the pits with the stand's prompt still on screen.
        if (_leaving)
        {
            if (!ScreenFade.Busy) GoBack();
            return;
        }

        TickSession();

        var kb = Keyboard.current;
        if (kb != null && kb[TimingKey].wasPressedThisFrame) TimingScreenUI.Ensure().Toggle();

        // E is the world's do-something button, so it belongs to whatever is in front of the player first:
        // a conversation or a panel gets the press, not the seat.
        if (NPCInteractable.AnyConversationActive || DialogueChoiceUI.IsOpen ||
            WeekendScheduleUI.IsOpen || WeekendModal.AnyOpen) return;

        bool leave = kb != null && kb[LeaveKey].wasPressedThisFrame;

        var pad = Gamepad.current;
        if (!leave && pad != null) leave = pad.buttonNorth.wasPressedThisFrame;

        if (leave) Leave();
    }

    // The standing instruction, at the bottom of the screen, in the kit everything else is drawn in.
    //
    // Silent behind anything the player is actually reading — a conversation, the schedule, a panel — and
    // during the wipe out, because by then the button has already been pressed.
    void OnGUI()
    {
        if (_leaving || _closed) return;
        if (RacePauseMenu.IsPaused) return;
        if (NPCInteractable.AnyConversationActive || DialogueChoiceUI.IsOpen ||
            WeekendScheduleUI.IsOpen || WeekendModal.AnyOpen) return;
        if (ScreenFade.Busy) return;

        string key = Gamepad.current != null ? "Y" : "E";
        PixelGUI.Prompt(key, _sessionOver ? ChequeredLine()
                                          : "Return to the pits.  F11 for live timing.");
    }

    // Is the player still in the stand? Measured off where they sat down, because that is the one thing the
    // visit knows about the place; a missing player is not an answer, so it counts as still there and the
    // ordinary teardown handles it.
    bool LeftTheStand()
    {
        var player = WeekendVenueAnchor.OnFootPlayer();
        if (player == null) return false;

        Vector3 a = player.position, b = _seat;
        a.z = b.z = 0f;
        return (a - b).sqrMagnitude > LeftTheStandMetres * LeftTheStandMetres;
    }

    // The compressed hour, counted on scaled time: the cars circulating out there run on it too, so a
    // pause menu over the top of the stand stops the session and the field together rather than running the
    // race off behind a panel.
    void TickSession()
    {
        if (_activity == null || !_held) return;

        _watched += Time.deltaTime;
        float elapsed = _watched;
        var timing = TimingScreenUI.Instance;

        if (!_sessionOver && elapsed >= _watchSeconds)
        {
            // The field comes in, but the circuit is not handed back to the clock yet: the booking is still
            // open until the player gets up, so the clock has not moved past this session, and releasing now
            // would put the very same field straight back out. Held cold instead; GoBack releases it.
            _sessionOver = true;
            WeekendTrackState.HoldEmpty();
        }

        if (timing == null) return;
        timing.statusLine = _sessionOver
            ? "SESSION OVER"
            : $"{GrandstandWatch.SessionMinuteAt(elapsed, _sessionMinutes)}/{_sessionMinutes} MIN";
    }

    // Give the circuit back to the clock. The sheet already believes this hour is spent, so what the track
    // does next is whatever the weekend says — which, mid-morning, is nothing.
    void ReleaseSession()
    {
        if (!_held) return;
        _held = false;
        WeekendTrackState.Release();
    }

    // Back through the fence, behind a wipe, exactly as the walk out here was — and this is what completes
    // the booking.
    //
    // The prompt comes down on the press, not on the wipe: a leave asked for while another wipe is already
    // running has to wait its turn (Update takes it when the screen is free), and the player who pressed the
    // button should not be looking at the prompt for it in the meantime.
    public void Leave()
    {
        if (_leaving) return;
        _leaving = true;

        if (_shot != null) { _shot.End(); _shot = null; }
        if (TimingScreenUI.Instance != null) TimingScreenUI.Instance.Hide();

        if (!ScreenFade.Busy) GoBack();
    }

    // Everything happens at black, in this order: the player is put back at the gate, the booking is settled
    // (the clock jumps to the end of the session and the next booking goes up), and only then is the circuit
    // handed back — so the field clears and the result card arrives as the screen comes up on the paddock,
    // not while the player is still looking at the stand. The visit stays alive until then; Update leaves it
    // alone while `_leaving` and the wipe is running.
    void GoBack()
    {
        var player = WeekendVenueAnchor.OnFootPlayer();
        Vector3 to = _returnTo;

        ScreenFade.Cut(() =>
        {
            if (player != null)
            {
                to.z = player.position.z;
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.position = to;      // the body owns the pose; a transform write snaps back
                player.position = to;
            }

            if (this == null) return;                      // torn down mid-wipe (a scene change)
            Complete();
            Close();
        });
    }

    // Settle the booking the player came to watch. Only if it is still the appointment: a player who
    // committed to something else off the sheet while sat here has walked away from this one, and finishing
    // it now would also book over the thing they just chose.
    void Complete()
    {
        if (_done || _activity == null) return;
        _done = true;

        var pending = WeekendAppointment.Pending;
        if (pending == null || pending.id != _activity.id) return;

        WeekendAppointment.Clear();
        WeekendDirector.Finish(pending, Homework(pending), inWorld: true);
    }

    string SessionLabel()
    {
        if (_activity == null) return "";
        var kind = WeekendTrackSessions.SessionKind(_activity.kind);
        string what = kind switch
        {
            ActivityKind.Qualifying => "QUALIFYING",
            ActivityKind.Race => "RACE",
            _ => "PRACTICE",
        };
        return SeriesCatalog.Name(_activity.series).ToUpperInvariant() + " · " + what;
    }

    string ChequeredLine() =>
        _activity != null && WeekendTrackSessions.SessionKind(_activity.kind) == ActivityKind.Race
            ? "That's the chequered flag. Head back to the pits."
            : "Session over — they're coming in. Head back to the pits.";

    // What an hour in the stand is worth. The same homework GrandstandSpectate settles up, taken as watched
    // in full: a driver who stands there for a session learns where the track is going, and a race teaches
    // more than a practice because the tyre says more over a run than over one lap.
    static WeekendOutcome Homework(WeekendActivity a)
    {
        var o = WeekendOutcome.Nothing;
        o.score = 1f;
        o.setupGain = a.kind switch
        {
            ActivityKind.SpectateRace => 0.10f,
            ActivityKind.SpectateQualifying => 0.05f,
            _ => 0.035f,
        };
        o.teamMorale = 2f;   // the engineers appreciate a driver who does the homework
        o.headline = $"Watched the {SeriesCatalog.Nickname(a.series)} " +
                     $"{WeekendTrackSessions.SessionKind(a.kind).ToString().ToLowerInvariant()} from the stands.";
        return o;
    }
}
