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

    GUIStyle _title, _detail, _footer;

    // What the marker is currently hung on, so it can be moved when the booking changes.
    string _markedId = "";
    Transform _marked;

    // The banner waits its turn behind the spawn card, so arriving at a track reads "Watkins Glen — Friday,
    // 9:30 AM" and only then "TEAM PLAN MEETING".
    string _bannerTitle, _bannerSubtitle;

    // What the strip is drawing, worked out once a frame in Update.
    //
    // OnGUI runs once per IMGUI event — a Layout and a Repaint every frame, plus one more for every key
    // and mouse event, so holding a walk key multiplies it. Resolving the booking in there meant the
    // timetable lookup, the venue search, the distance measure and four interpolated strings all ran
    // several times a frame, and the whole lot showed up as a hitch exactly when the player was walking
    // to a marker. It is one evaluation a frame now, and OnGUI only draws.
    WeekendActivity _shown;
    bool _onFoot;
    bool _here;
    int _metresLeft = -1;
    string _detailText = "", _footerText = "";

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
        // Whoever hands the day over, whatever their job title — the flag is the thing, not the role.
        if (who == null || !(who.givesTheDaysObjective || who.role == PlacedNPC.Role.TeamLiaison)) return;

        WeekendDirector.BookNextUp(replaceExisting: true);
        _markedId = "";   // force the marker to be rebuilt and pulsed on the next tick
    }

    void Update()
    {
        SyncMarker();
        PumpBanner();
        Refresh();

        if (!Showing) return;
        var kb = Keyboard.current;
        if (kb != null && kb[TravelKey].wasPressedThisFrame) TravelThere();
    }

    // The once-a-frame resolve. Everything that costs something — the booking, where it is, how far off
    // it is and the lines that say so — lands in fields here so OnGUI is pure drawing.
    void Refresh()
    {
        var activity = WeekendAppointment.Pending;
        if (activity == null)
        {
            _shown = null;
            _onFoot = false;
            _metresLeft = -1;
            return;
        }

        _onFoot = WeekendVenueAnchor.OnFootPlayer() != null;
        if (WeekendAppointment.Target() == null) { _shown = null; return; }
        var previous = _shown;
        _shown = activity;

        float distance = WeekendAppointment.DistanceRemaining();
        bool here = distance >= 0f && distance <= HereMetres;
        int metres = Mathf.RoundToInt(Mathf.Max(0f, distance));

        // The two lines only change on a whole metre, on arriving, or on the booking itself changing,
        // so they are rebuilt then rather than every frame — a walk across the paddock is otherwise a
        // few hundred dead strings.
        if (activity != previous || here != _here || metres != _metresLeft || _detailText.Length == 0)
        {
            _here = here;
            _metresLeft = metres;
            _detailText = here
                ? "You're here — press E to " + Verb(activity)
                : $"{Capitalise(WeekendVenues.Directions(WeekendVenues.For(activity.kind)))}  ·  {metres} m";
            _footerText = here
                ? activity.Clock + "  ·  " + WeekendAppointment.TargetLabel()
                : $"{activity.Clock}  ·  [T] TRAVEL THERE";
        }
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
        // Named, and outranking the pit-lane spawn's "your car" pip. The booking is the one place the player
        // is due; a second, unlabelled marker beside it is a puzzle rather than a direction.
        intro.AddMarker(target, MarkerIcon(target), hideWithinMetres: 3f,
                        label: activity.title, priority: 10);
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
    //
    // The panel/conversation gates stay live rather than cached: they are static reads, and a conversation
    // that opens after this component's Update has already run would otherwise get one frame of strip
    // drawn over it. The costly half comes from Refresh.
    bool Showing
    {
        get
        {
            if (WeekendScheduleUI.IsOpen || WeekendModal.AnyOpen) return false;
            if (NPCInteractable.AnyConversationActive || DialogueChoiceUI.IsOpen) return false;
            return _shown != null && _onFoot;
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
        if (ScreenFade.Busy) return false;

        // Stand on the venue's own mark where there is one; a session has no mark, so pull up beside the car.
        Vector3 to = anchor != null ? anchor.StandPosition : target.position + new Vector3(0f, -3f, 0f);
        to.z = player.position.z;

        // Behind a wipe rather than a jump cut. The paddock is one continuous place, and a player who blinks
        // and finds themselves two hundred metres away has to work out where they are looking from scratch;
        // a fade is the shorthand every game uses for "time and distance happened here".
        ScreenFade.Cut(() =>
        {
            if (player == null) return;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = to;       // the body owns the pose; moving only the transform snaps back
            player.position = new Vector3(to.x, to.y, player.position.z);
        });
        return true;
    }

    void OnGUI()
    {
        if (!Showing) return;
        EnsureStyles();

        var activity = _shown;

        // Rows are measured off the styles they are drawn in rather than assumed. The three lines are set
        // on three different faces — a 32px display heading over a 32px data line over a 16px footer — and
        // striding them all by LineH (20px at 2x) drew every one of them through the next. That is what the
        // top of the screen looked like: the booking, the directions and the travel prompt in one heap.
        float titleH = RowH(_title);
        float detailH = RowH(_detail);
        float footerH = RowH(_footer);

        float inset = PixelGUI.Px(8f);   // what PanelContent(box, 4f) takes off each side: Px(4) + Px(4)
        float textW = Mathf.Max(Width(_title, activity.title),
                                Mathf.Max(Width(_detail, _detailText), Width(_footer, _footerText)));

        // Sized to its longest line, so a wordy booking widens the strip instead of spilling out of it.
        float w = Mathf.Clamp(textW + inset * 2f + PixelGUI.Px(8f),
                              PixelGUI.Px(180f), Screen.width - PixelGUI.Px(16f));
        float h = titleH + detailH + footerH + inset * 2f;
        var box = new Rect(Mathf.Round((Screen.width - w) * 0.5f), PixelGUI.Px(6f), w, h);

        PixelGUI.Panel(box, focused: false);
        var c = PixelGUI.PanelContent(box, 4f);

        float y = c.y;
        GUI.Label(new Rect(c.x, y, c.width, titleH), activity.title, _title);
        y += titleH;
        GUI.Label(new Rect(c.x, y, c.width, detailH), _detailText, _detail);
        y += detailH;
        GUI.Label(new Rect(c.x, y, c.width, footerH), _footerText, _footer);
    }

    // The height one line of a style actually occupies, leading included.
    static float RowH(GUIStyle s) => s.fontSize + PixelGUI.Px(3f);

    static float Width(GUIStyle s, string text) =>
        string.IsNullOrEmpty(text) ? 0f : s.CalcSize(new GUIContent(text)).x;

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

        // Centred on the strip, and none of them wrapping: the panel sizes itself to the longest line, so a
        // long booking title makes a wider strip rather than a second line drawn over the one beneath it.
        _title = new GUIStyle(PixelGUI.Heading)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow,
        };
        _detail = new GUIStyle(PixelGUI.Data) { alignment = TextAnchor.UpperCenter, wordWrap = false };
        _footer = new GUIStyle(PixelGUI.Footer) { alignment = TextAnchor.UpperCenter, wordWrap = false };
    }
}
