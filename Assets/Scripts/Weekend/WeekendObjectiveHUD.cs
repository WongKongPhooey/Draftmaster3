using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// Where you are due, and how far away it is.
//
// Committing to something on the timetable does not run it any more — it books it, and the player has to
// walk there. This is the only thing on screen that says so: a strip at the top with the booking, the place
// and the distance left, and an arrow at the edge of the screen pointing at it while it is off camera.
//
// T walks you there. The paddock is a big place and the schedule already charges you the hour; making
// somebody cross it four times a day at 3 m/s is a tax on the second playthrough rather than a feature, so
// the skip is always available and always visible.
//
// Self-installing, drawn with PixelGUI like the rest of the race-side HUD, and silent whenever there is no
// appointment.
public class WeekendObjectiveHUD : MonoBehaviour
{
    public static WeekendObjectiveHUD Instance { get; private set; }

    public const Key TravelKey = Key.T;

    // How close counts as "you are here" for the strip's own wording. The host's own arrive range is what
    // actually gates the conversation.
    const float HereMetres = 4f;

    GUIStyle _title, _detail;

    // What the marker is currently hung on, so it can be moved when the booking changes.
    string _markedId = "";
    Transform _marked;

    // The banner waits its turn behind the spawn card, so arriving at a track reads "Watkins Glen — Friday,
    // 9:30 AM" and only then "TEAM PLAN MEETING".
    string _bannerTitle, _bannerSubtitle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("WeekendObjectiveHUD");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeekendObjectiveHUD>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable() { PlacedNPC.CutsceneFinished += OnCutsceneFinished; }
    void OnDisable() { PlacedNPC.CutsceneFinished -= OnCutsceneFinished; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // The liaison has finished telling the player where they are due. Make sure that is what is booked, and
    // throw the marker's fly-in again so the eye goes from her to the direction she just sent them in.
    void OnCutsceneFinished(PlacedNPC who)
    {
        if (who == null || who.role != PlacedNPC.Role.TeamLiaison) return;

        WeekendDirector.BookNextUp(replaceExisting: true);
        _markedId = "";   // force the marker to be rebuilt and pulsed on the next tick
    }

    void Update()
    {
        SyncMarker();
        PumpBanner();

        if (!Showing) return;
        var kb = Keyboard.current;
        if (kb != null && kb[TravelKey].wasPressedThisFrame) TravelThere();
    }

    // Hang the objective on the game's own marker system rather than drawing a second set of arrows: the
    // spawn card already puts an edge-clamped icon, a distance and a fly-in on whatever the player is meant
    // to walk to, and the weekend's bookings are exactly that.
    void SyncMarker()
    {
        var intro = SpawnIntroUI.Instance;
        if (intro == null) { _markedId = ""; _marked = null; return; }

        var activity = WeekendAppointment.Pending;
        string id = activity != null ? activity.id : "";
        if (id == _markedId && (_marked != null || id == "")) return;

        if (_marked != null) intro.RemoveMarker(_marked);
        _marked = null;
        _markedId = id;

        if (activity == null) return;

        var target = WeekendAppointment.Target();
        if (target == null) return;

        _marked = target;
        intro.AddMarker(target, MarkerIcon(target), hideWithinMetres: 3f);
        intro.PulseMarker(target);

        // The banner says what it is; the line under it says where and when, in the same shape the spawn
        // card uses. Together they are the whole instruction: this thing, over there, at that time.
        _bannerTitle = activity.title;
        _bannerSubtitle = $"{Capitalise(WeekendVenues.Directions(WeekendVenues.For(activity.kind)))}  ·  {activity.Clock}";
    }

    // Put the pending banner up as soon as the card in front of it has finished.
    void PumpBanner()
    {
        if (_bannerTitle == null) return;

        var intro = SpawnIntroUI.Instance;
        if (intro == null || intro.TitleBusy) return;

        intro.ShowTitle(_bannerTitle, _bannerSubtitle);
        if (_marked != null) intro.PulseMarker(_marked);
        _bannerTitle = null;
        _bannerSubtitle = null;
    }

    // Whatever the target already draws with — the car's paint scheme, a host's sprite — so the marker is a
    // picture of the thing rather than a generic pip.
    static Sprite MarkerIcon(Transform target)
    {
        var sprite = target.GetComponentInChildren<SpriteRenderer>();
        return sprite != null ? sprite.sprite : null;
    }

    // Nothing to say when there is no appointment, when the schedule or a conversation is up, or when the
    // player is not on foot to walk anywhere.
    bool Showing
    {
        get
        {
            if (WeekendScheduleUI.IsOpen || WeekendModal.AnyOpen) return false;
            if (NPCInteractable.AnyConversationActive || DialogueChoiceUI.IsOpen) return false;
            var a = WeekendAppointment.Pending;
            if (a == null) return false;
            return WeekendAppointment.Target() != null && WeekendVenueAnchor.OnFootPlayer() != null;
        }
    }

    // Put the player at the venue's standing mark. Not a cheat on the clock — the weekend's cost is the
    // hour the booking takes, which is charged when it is done, not the walk.
    public static bool TravelThere()
    {
        var anchor = WeekendAppointment.Where();
        var target = WeekendAppointment.Target();
        var player = WeekendVenueAnchor.OnFootPlayer();
        if (target == null || player == null) return false;

        // Stand on the venue's own mark where there is one; a session has no mark, so pull up beside the car.
        Vector3 to = anchor != null ? anchor.StandPosition : target.position + new Vector3(0f, -3f, 0f);
        to.z = player.position.z;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.position = to;           // the body owns the pose; moving only the transform snaps back
        player.position = new Vector3(to.x, to.y, player.position.z);
        return true;
    }

    void OnGUI()
    {
        if (!Showing) return;
        EnsureStyles();

        var activity = WeekendAppointment.Pending;
        if (activity == null || WeekendAppointment.Target() == null) return;

        float distance = WeekendAppointment.DistanceRemaining();
        bool here = distance >= 0f && distance <= HereMetres;

        float w = PixelGUI.Px(230f);
        float h = PixelGUI.Px(34f);
        var box = new Rect(Mathf.Round((Screen.width - w) * 0.5f), PixelGUI.Px(6f), w, h);

        PixelGUI.Panel(box, focused: false);
        var c = PixelGUI.PanelContent(box, 4f);

        GUI.Label(new Rect(c.x, c.y, c.width, PixelGUI.LineH), activity.title, _title);

        string detail = here
            ? "You're here — press E to " + Verb(activity)
            : $"{Capitalise(WeekendVenues.Directions(WeekendVenues.For(activity.kind)))}  ·  {Mathf.RoundToInt(Mathf.Max(0f, distance))} m";
        GUI.Label(new Rect(c.x, c.y + PixelGUI.LineH, c.width, PixelGUI.LineH), detail, _detail);

        if (!here)
            GUI.Label(new Rect(c.x, c.y + PixelGUI.LineH * 2f, c.width, PixelGUI.LineH),
                      $"{activity.Clock}  ·  [T] TRAVEL THERE", PixelGUI.Footer);
        else
            GUI.Label(new Rect(c.x, c.y + PixelGUI.LineH * 2f, c.width, PixelGUI.LineH),
                      activity.Clock + "  ·  " + WeekendAppointment.TargetLabel(), PixelGUI.Footer);
    }

    static string Verb(WeekendActivity a)
    {
        if (a.IsOnTrack) return "get in";
        if (ActivityKinds.IsFanDuty(a.kind)) return "start signing";
        if (ActivityKinds.IsSpectate(a.kind)) return "sit down and watch";
        if (ActivityKinds.IsMedia(a.kind)) return "take their questions";
        return "start";
    }

    static string Capitalise(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(PixelGUI.Heading);
        _detail = new GUIStyle(PixelGUI.Data);
    }
}
