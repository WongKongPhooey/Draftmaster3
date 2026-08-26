using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runs the race weekend: owns the timetable, opens the schedule screen, starts whichever activity the
// player picks, and settles what it earned them.
//
// Self-installing and scene-independent. The weekend deliberately crosses scene loads - picking your own
// practice session hands off to the race scene, which reloads again for qualifying and again for the race -
// so the director lives on a DontDestroyOnLoad object and everything it needs to remember is in
// WeekendLedger (PlayerPrefs), not in a field here.
//
// The split with the core assembly (Draftmaster.Weekend) is deliberate: the timetable, the ledger and the
// simulated results are pure and testable, and this file is the only thing that knows about scenes,
// PlayerWallet, PlayerStatsLedger and the race flow.
public class WeekendDirector : MonoBehaviour
{
    public static WeekendDirector Instance { get; private set; }

    // Opens the schedule from anywhere. F10 is the first free function key - F1-F9 are all taken by the
    // race panels (see Docs/Editor-Handbook.md), P is the phone, Esc is the pause menu, V is drive/broadcast.
    public const Key OpenKey = Key.F10;

    // ------------------------------------------------------------------ routing across scene loads

    // The activity the player is currently out doing on track. Set before the race scene loads and read when
    // the session ends, so the ledger can credit the right booking. PlayerPrefs because the scene reload
    // between practice, qualifying and the race would eat a static.
    const string RouteKey = "weekend.route";

    public static string PendingRouteId
    {
        get => PlayerPrefs.GetString(RouteKey, "");
        set { PlayerPrefs.SetString(RouteKey, value ?? ""); PlayerPrefs.Save(); }
    }

    public static void ClearRoute() => PendingRouteId = "";

    // ------------------------------------------------------------------ the timetable

    static WeekendTimetable _timetable;
    static int _builtForWeekend = -1;
    static RacingSeries _builtForSeries;

    // The sheet for the weekend currently in progress. Rebuilt whenever the weekend id or the player's
    // championship changes; identical every time for the same pair, so this is cheap to call.
    public static WeekendTimetable Timetable
    {
        get
        {
            int id = RaceWeekend.WeekendId;
            var series = SeriesCatalog.PlayerSeries;
            if (_timetable == null || _builtForWeekend != id || _builtForSeries != series)
            {
                _timetable = WeekendTimetable.Build(series, id, TrackSelection.CurrentDisplayName);
                _builtForWeekend = id;
                _builtForSeries = series;
                WeekendLedger.EnsureWeekend(id, series);
                WeekendLedger.Timetable = _timetable;
            }
            return _timetable;
        }
    }

    public static void Invalidate() { _timetable = null; _builtForWeekend = -1; }

    // ------------------------------------------------------------------ install

    static bool _hooksInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        InstallHooks();
        Ensure();
    }

    // Money, career counters and driver relationships live in this assembly; the ledger cannot reach them,
    // so it calls back out through these.
    static void InstallHooks()
    {
        if (_hooksInstalled) return;
        _hooksInstalled = true;

        WeekendLedger.MoneyHook = amount =>
        {
            if (amount > 0) PlayerWallet.Add(amount);
            else if (amount < 0) PlayerWallet.Add(amount);   // a fine can take the bank negative; that is the point
        };

        WeekendLedger.StatHook = (key, by) =>
        {
            if (!string.IsNullOrEmpty(key)) PlayerStatsLedger.Increment(key, by);
        };

        WeekendLedger.RelationshipHook = (driver, delta) =>
        {
            if (string.IsNullOrEmpty(driver)) return;
            DriverRelationships.Modify(DriverRelationships.PlayerName, driver, delta);
        };
    }

    public static WeekendDirector Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("WeekendDirector");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeekendDirector>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InstallHooks();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        // Nothing that was open in the scene we just left is open in this one. The schedule screen lives on
        // a DontDestroyOnLoad object, so without this it survives the load — drawn over a screen it knows
        // nothing about, and, worse, still holding the modal clock at zero. Quitting to the title with the
        // timetable up used to hand the title screen (and the next race loaded from it) a frozen game.
        // Reset rather than Pop: whatever depth the panels were stacked to, the load ends all of it.
        WeekendScheduleUI.Close();
        WeekendModal.Reset();

        // A scene load in the middle of a weekend rebuilds the sheet against whatever weekend id is now on
        // file, so the ledger and the timetable never drift apart.
        Invalidate();
        _ = Timetable;

        if (PlayerPrefs.GetInt(OpenOnLoadKey, 0) == 1)
        {
            PlayerPrefs.SetInt(OpenOnLoadKey, 0);
            PlayerPrefs.Save();
            WeekendScheduleUI.Open();
            return;
        }

        EnterRoundIfAtTheVenue();
        GreetOnArrival();

        // Whatever is next on the sheet is booked without being asked for, so the player always has
        // somewhere to be. F10 is for changing your mind, not for finding out you had somewhere to be.
        BookNextUp();
    }

    // ------------------------------------------------------------------ the season's calendar

    // A weekend only becomes a round of three championships once the player has turned up at the venue.
    // The title screen and the garage know the weekend id long before there is a race meeting to score,
    // and a round on the calendar is a round the other two championships raced (SeasonChampionships), so
    // entering one from the main menu would hand out a full weekend's points for a weekend nobody went to.
    int _enteredRound = -1;
    float _venuePoll;

    void EnterRoundIfAtTheVenue()
    {
        if (_enteredRound == RaceWeekend.WeekendId) return;
        if (!AtTheVenue()) return;

        _enteredRound = RaceWeekend.WeekendId;
        SeasonChampionships.EnterRound(RaceWeekend.WeekendId, TrackSelection.CurrentId, TrackSelection.CurrentDisplayName);
    }

    // Is this scene a race meeting? Same test the arrival greeting uses.
    static bool AtTheVenue() =>
        Object.FindFirstObjectByType<GridSpawner>() != null || Object.FindFirstObjectByType<PitLaneStart>() != null;

    // Arriving at the track for a weekend nothing has happened in yet puts the timetable up once, because
    // that is the moment a driver is handed their schedule for the three days. It is suppressed when the
    // player is here to drive (a routed session) and once per weekend after that - F10 from then on.
    const string GreetedKey = "weekend.greeted";

    void GreetOnArrival()
    {
        if (!string.IsNullOrEmpty(PendingRouteId)) return;
        if (WeekendLedger.DoneCount > 0 || WeekendLedger.MissedCount > 0) return;
        if (PlayerPrefs.GetInt(GreetedKey, -1) == RaceWeekend.WeekendId) return;

        if (!AtTheVenue()) return;

        PlayerPrefs.SetInt(GreetedKey, RaceWeekend.WeekendId);
        PlayerPrefs.Save();

        // No longer opens the sheet over the top of the player's first steps. Arriving at a track is met by
        // the team liaison walking over and telling them where they are due (PlacedNPCDefaults' team
        // liaison); the sheet is there on F10 for anyone who wants to read the whole three days.
        WeekendScheduleUI.Toast("Three days at " + TrackSelection.CurrentDisplayName + ".");
    }

    // ------------------------------------------------------------------ the chain

    // Book whatever is next on the sheet, if nothing is booked already. This is what makes the weekend a
    // route rather than a list: finish a thing and the next one is already waiting with a marker on it.
    public static WeekendActivity BookNextUp(bool replaceExisting = false)
    {
        // A booking the player made themselves stands — unless the weekend has moved past it, which is how
        // a stale appointment from an earlier day survives into the next one and leaves the marker pointing
        // at something that already happened.
        var pending = WeekendAppointment.Pending;
        if (!replaceExisting && pending != null && WeekendLedger.CanDo(pending, out _)) return pending;

        var next = WeekendSchedulePlan.NextWorthDoing();
        if (next == null) { WeekendAppointment.Clear(); return null; }

        // Nowhere to walk to from here (the title screen, the garage): leave it unbooked rather than
        // pointing at a place that is not in this scene.
        if (!next.IsOnTrack && WeekendVenueAnchor.Find(WeekendVenues.For(next.kind)) == null) return null;

        WeekendAppointment.Make(next);
        return next;
    }

    // ------------------------------------------------------------------ opening the sheet

    void Update()
    {
        // The scene the editor was left in when Play was pressed never raised sceneLoaded for this object,
        // so the venue check gets one poll a second until it finds a paddock. Once the round is on the
        // calendar this costs nothing.
        if (_enteredRound != RaceWeekend.WeekendId && Time.unscaledTime >= _venuePoll)
        {
            _venuePoll = Time.unscaledTime + 1f;
            EnterRoundIfAtTheVenue();
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        // Not while an obligation is actually happening: mid-conversation with the crew chief, or sat in
        // the stand watching somebody else's race.
        if (kb[OpenKey].wasPressedThisFrame && !NPCInteractable.AnyConversationActive && !GrandstandSpectate.Watching)
            WeekendScheduleUI.Toggle();
    }

    // ------------------------------------------------------------------ starting an activity

    // The player picked something off the sheet. On-track sessions hand off to the race scene; everything
    // else runs as a panel over whatever scene we are already in.
    public static void Begin(WeekendActivity a)
    {
        if (a == null) return;
        if (!WeekendLedger.CanDo(a, out string why))
        {
            WeekendScheduleUI.Toast(why);
            return;
        }

        if (a.IsOnTrack) { BeginOnTrack(a); return; }

        // An hour off is not somewhere you go. Everything else is.
        if (a.kind == ActivityKind.Rest)
        {
            WeekendScheduleUI.Close();
            Finish(a, WeekendOutcome.Nothing.WithHeadline("Took the window off."));
            return;
        }

        // Everything else is a place in the paddock: the pit box, your own motorhome, the drivers' room,
        // the fan fence, the hospitality tent, the intro stage, a seat in a grandstand. Committing books
        // it and points you at it — the obligation itself happens when you are stood there and talk to
        // whoever is waiting (WeekendVenueHost), not here.
        var venue = WeekendVenues.For(a.kind);
        if (WeekendVenueAnchor.Find(venue) == null)
        {
            // Off at the title screen or in the garage: there is no paddock to walk across from here.
            WeekendScheduleUI.Toast($"{WeekendVenues.Label(venue)} is at the circuit — head to the track first.");
            return;
        }

        WeekendAppointment.Make(a);
        WeekendScheduleUI.Close();
        Ensure();
    }

    // Your own car, your own session. Point RaceWeekend at it and load the race scene; the session's own
    // director credits the booking when it ends.
    static void BeginOnTrack(WeekendActivity a)
    {
        PendingRouteId = a.id;
        RaceWeekend.Current = a.kind switch
        {
            ActivityKind.Qualifying => RaceWeekend.Session.Qualifying,
            ActivityKind.Race => RaceWeekend.Session.Race,
            _ => RaceWeekend.Session.Practice,
        };

        WeekendScheduleUI.Close();

        // Already in a race scene: reload it so the session flow restarts under the new session kind.
        // Anywhere else (title, garage, travel map): go to the shared race scene.
        var active = SceneManager.GetActiveScene();
        bool inRaceScene = Object.FindFirstObjectByType<GridSpawner>() != null
                        || Object.FindFirstObjectByType<PitLaneStart>() != null;
        WeekendModal.Reset();
        SceneManager.LoadScene(inRaceScene ? active.name : "RaceScene");
    }

    // ------------------------------------------------------------------ finishing an activity

    // Credit a booking and show what it did. Every obligation ends here.
    //
    // inWorld is what an obligation done in the paddock passes: the card reports back without stopping the
    // world or throwing the player back to the sheet, because they are stood in front of the person they
    // just finished talking to.
    public static void Finish(WeekendActivity a, WeekendOutcome outcome, bool inWorld = false)
    {
        if (a == null) return;

        // Turning up is worth the appearance fee whatever the score was.
        if (a.appearanceFee > 0) outcome.money += a.appearanceFee;

        WeekendLedger.Complete(a, outcome);
        WeekendResultCard.Show(a, outcome, inWorld);

        // One thing leads to the next: the moment this is settled, the following booking is the live
        // objective and its marker is already on screen.
        BookNextUp(replaceExisting: true);
    }

    // The on-track session that was routed here has ended. Called by PracticeDirector when a
    // practice/qualifying session is closed out, and by RaceDirector when the race is classified.
    //
    // showCard is off for the race: the race has its own results screen, and stacking the little activity
    // card over the top of it would bury the classification.
    public static void FinishRoutedSession(WeekendOutcome outcome, bool showCard = true)
    {
        string id = PendingRouteId;
        if (string.IsNullOrEmpty(id)) return;
        ClearRoute();

        var a = Timetable.ById(id);
        if (a == null) return;

        if (showCard) { Finish(a, outcome); return; }

        if (a.appearanceFee > 0) outcome.money += a.appearanceFee;
        WeekendLedger.Complete(a, outcome);
    }

    // Ask for the schedule to be up as soon as the next scene finishes loading. The weekend crosses scene
    // loads, so "go back to the sheet" sometimes has to survive one.
    const string OpenOnLoadKey = "weekend.openonload";

    public static void OpenAfterLoad()
    {
        PlayerPrefs.SetInt(OpenOnLoadKey, 1);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ weekend end

    // Every half-day spent. Called by the schedule screen when the player advances past Sunday afternoon.
    public static bool WeekendComplete => WeekendLedger.WeekendOver;

    // Start the next weekend: bump the weekend id (which resets AppearanceConditions' once-per-weekend
    // memory too), wipe the sheet, and rebuild.
    public static void NextWeekend()
    {
        RaceWeekend.ResetWeekend();
        Invalidate();
        WeekendLedger.EnsureWeekend(RaceWeekend.WeekendId, SeriesCatalog.PlayerSeries);
        WeekendLedger.Timetable = Timetable;
    }
}
