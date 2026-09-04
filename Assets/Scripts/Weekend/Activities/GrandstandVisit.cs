using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// Watching somebody else's session from the stand you walked to.
//
// The obligation was to be there, and the gate put you there, so the booking is DONE the moment you arrive:
// the sheet moves on, the weekend carries on around you, and what is left is a seat with a good view of the
// circuit and no clock on it. Stay for a lap or stay for the hour.
//
// This is the in-world half of spectating, and deliberately not GrandstandSpectate — that one is the
// broadcast: it plants the player, simulates the session and draws a timing tower down the side of the
// screen. Here the cars going past are the real field the weekend put on track (GridSpawner's ambient
// session), the player can walk about in the stand, and the only interface is one prompt telling them how
// to get back.
public class GrandstandVisit : MonoBehaviour
{
    public static GrandstandVisit Active { get; private set; }

    const string HintId = "grandstand.return";

    Vector3 _returnTo;
    float _startedAt;

    // Arrive in the stand. `returnTo` is where the walk started — the gate in the paddock fence.
    public static GrandstandVisit Begin(WeekendActivity a, Vector3 returnTo)
    {
        if (Active != null) Destroy(Active.gameObject);

        var go = new GameObject("GrandstandVisit");
        var visit = go.AddComponent<GrandstandVisit>();
        visit._returnTo = returnTo;
        visit._startedAt = Time.unscaledTime;

        // Being here IS the booking. Completing it now rather than on the way out means the sheet is never
        // holding an obligation the player has already met, and nothing is lost if they wander off.
        if (a != null)
        {
            WeekendAppointment.Clear();
            WeekendDirector.Finish(a, Homework(a), inWorld: true);
        }

        ControlHints.ShowSticky(HintId, "T", "Y", "Return to the pits. Stay as long as you like.");
        return visit;
    }

    void OnEnable() { Active = this; }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        ControlHints.Hide(HintId);
    }

    void Update()
    {
        // A moment's grace: the key that got you here should not also take you straight back out.
        if (Time.unscaledTime - _startedAt < 0.4f) return;

        var kb = Keyboard.current;
        bool leave = kb != null && kb.tKey.wasPressedThisFrame;

        var pad = Gamepad.current;
        if (!leave && pad != null) leave = pad.buttonNorth.wasPressedThisFrame;

        if (leave) Leave();
    }

    // Back through the fence, behind a wipe, exactly as the walk out here was.
    public void Leave()
    {
        if (ScreenFade.Busy) return;

        var player = WeekendVenueAnchor.OnFootPlayer();
        Vector3 to = _returnTo;

        ControlHints.Hide(HintId);
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
