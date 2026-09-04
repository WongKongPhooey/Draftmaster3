using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// Watching somebody else's session from the stand you walked to.
//
// The obligation was to be there, and the gate put you there, so the booking is DONE the moment you arrive:
// the sheet moves on, the weekend carries on around you, and what is left is a seat with a good view of the
// circuit. The camera pulls back off the player onto the vantage the track authored (GrandstandCamera) and
// the session plays out in front of it.
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

    const string HintId = "grandstand.return";

    // A second id, not a rewrite of the first: re-showing a live hint under the same id only refreshes its
    // timer (ControlHintUI.Push), so the chequered-flag line would never reach the screen.
    const string OverHintId = "grandstand.sessionover";

    // Live timings for whatever is on track. F11 because it is a screen you put up and leave up, not a
    // panel with a button on it — and because F11 was the last function key nothing else had taken.
    const Key TimingKey = Key.F11;

    Vector3 _returnTo;
    float _startedAt;

    // The session being watched, its compressed length, and whether it has run out.
    WeekendActivity _activity;
    float _watchSeconds;
    float _watched;             // seconds of the compressed session run so far
    int _sessionMinutes;
    bool _sessionOver;
    bool _held;
    bool _closed;

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

        // Take the circuit off the clock FIRST. Finishing the booking below shoves the weekend clock to the
        // end of the session, and the field is spawned off that clock — so without a hold in place the cars
        // the player came to watch are cleared out from under them on the frame they sit down.
        if (a != null && a.IsSpectate)
        {
            visit._sessionMinutes = Mathf.Max(1, a.minutes);
            visit._watchSeconds = GrandstandWatch.WatchSeconds(visit._sessionMinutes);
            WeekendTrackState.Hold(a.series, WeekendTrackSessions.SessionKind(a.kind), a.id);
            visit._held = true;
        }

        // Being here IS the booking. Completing it now rather than on the way out means the sheet is never
        // holding an obligation the player has already met, and nothing is lost if they wander off.
        if (a != null)
        {
            WeekendAppointment.Clear();
            WeekendDirector.Finish(a, Homework(a), inWorld: true);
        }

        visit.OpenTheView(marker);

        ControlHints.ShowSticky(HintId, "T", "Y", "Return to the pits. F11 for live timing.");
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

        ControlHints.Hide(HintId);
        ControlHints.Hide(OverHintId);
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

        bool authored = marker != null && marker.HasCameraView;
        Vector3 view = authored ? marker.CameraViewPosition : seat;
        float zoom = marker != null ? marker.cameraZoom : 0f;
        float pan = marker != null ? marker.cameraPanSeconds : 2.2f;

        _shot = GrandstandCamera.Begin(seat, view, authored, zoom, pan);

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

        TickSession();

        var kb = Keyboard.current;
        if (kb != null && kb[TimingKey].wasPressedThisFrame) TimingScreenUI.Ensure().Toggle();

        bool leave = kb != null && kb.tKey.wasPressedThisFrame;

        var pad = Gamepad.current;
        if (!leave && pad != null) leave = pad.buttonNorth.wasPressedThisFrame;

        if (leave) Leave();
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
            _sessionOver = true;
            ReleaseSession();
            ControlHints.Hide(HintId);
            ControlHints.ShowSticky(OverHintId, "T", "Y", ChequeredLine());
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

    // Back through the fence, behind a wipe, exactly as the walk out here was.
    public void Leave()
    {
        if (ScreenFade.Busy) return;

        var player = WeekendVenueAnchor.OnFootPlayer();
        Vector3 to = _returnTo;

        ControlHints.Hide(HintId);
        ControlHints.Hide(OverHintId);
        if (_shot != null) { _shot.End(); _shot = null; }

        ScreenFade.Cut(() =>
        {
            if (player == null) return;
            to.z = player.position.z;

            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = to;      // the body owns the pose; a transform write snaps back
            player.position = to;
        });

        Destroy(gameObject);
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
