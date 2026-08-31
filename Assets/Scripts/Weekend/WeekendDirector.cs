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

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlacedNPC.CutsceneFinished += OnCutsceneFinished;
        _holdDeadline = Time.unscaledTime + BriefingGraceSeconds;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlacedNPC.CutsceneFinished -= OnCutsceneFinished;
    }

    // Somebody has finished a walk-up beat. If it was the person who hands the driver their day, the day is
    // handed over: the wait ends and the first obligation books itself onto the map.
    void OnCutsceneFinished(PlacedNPC who)
    {
        if (who == null || !who.givesTheDaysObjective) return;
        BookNextUp(replaceExisting: true);
    }

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

        // A fresh scene is a fresh chance for somebody to be waiting at the motorhome door with the day's
        // plan; whether one is there is answered by OpeningCastBuilt a moment later.
        _giverStoodDown = false;
        _holdDeadline = Time.unscaledTime + BriefingGraceSeconds;

        // Whatever is next on the sheet is booked without being asked for, so the player always has
        // somewhere to be. F10 is for changing your mind, not for finding out you had somewhere to be.
        // The exception is the first morning of a weekend, which is somebody's job to hand over in person.
        BookNextUp();
    }

    // ------------------------------------------------------------------ waiting to be told

    // The day's first obligation is handed over by a person (the team liaison at the motorhome door — any
    // PlacedNPC with `givesTheDaysObjective`), not by the sheet booking itself while the player is still
    // asleep. Until she has said it, nothing books. WeekendBriefing holds the rule and the memory of it.

    // How long to wait for that person to turn up before giving up on them. Only reachable in a paddock
    // with no such NPC in it and no PitLaneStart to say so — a scene the player drove straight into, say.
    // Without it, a missing liaison would mean a weekend with no objective at all.
    const float BriefingGraceSeconds = 8f;

    static bool _giverStoodDown;   // the cast is up and nobody is coming to hand the day over
    static float _holdDeadline;
    static float _rebookPoll;      // next second at which an unbooked-but-briefed weekend tries again

    public static bool Briefed => WeekendBriefing.Briefed(RaceWeekend.WeekendId);

    // The day has been handed over: by the liaison finishing her piece, by the player booking something
    // off the sheet themselves, or by the weekend moving on without either.
    public static void MarkBriefed() => WeekendBriefing.MarkBriefed(RaceWeekend.WeekendId);

    // True while the objective strip should stay empty because somebody is on their way over to fill it.
    public static bool WaitingToBeTold()
        => WeekendBriefing.WaitingToBeTold(
               briefed: Briefed,
               routed: !string.IsNullOrEmpty(PendingRouteId),
               weekendUnderway: WeekendLedger.DoneCount > 0 || WeekendLedger.MissedCount > 0,
               atTheVenue: AtTheVenue(),
               giverComing: !_giverStoodDown);

    // The scene flow has stood the opening cast up (PitLaneStart.BuildCast). If nobody in it hands the day
    // over, the weekend goes back to booking for itself, immediately — no grace period, no empty strip.
    public static void OpeningCastBuilt(bool someoneWillBrief)
    {
        if (someoneWillBrief) { _holdDeadline = Time.unscaledTime + BriefingGraceSeconds; return; }

        _giverStoodDown = true;
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
        // Asking outright is being told: the liaison finishing her piece, an obligation settling, the player
        // committing to something off the sheet. Anything that passes replaceExisting has a person or a
        // decision behind it, so it ends the wait rather than being held by it.
        if (replaceExisting) MarkBriefed();

        // The first morning of a weekend is handed over in person. Book nothing until it has been.
        if (WaitingToBeTold()) return null;

        // Make sure there is a sheet to read before reading it. Pressing Play straight into the race scene
        // raises no sceneLoaded for this object, so nothing had built the timetable — and with none built,
        // "what is next" answers *nothing*, which sent the loop below rolling half-day after half-day until
        // the weekend was over before the player had left their motorhome.
        _ = Timetable;
        if (WeekendLedger.Timetable == null) return null;

        // A booking the player made themselves stands — unless the weekend has moved past it, which is how
        // a stale appointment from an earlier day survives into the next one and leaves the marker pointing
        // at something that already happened.
        var pending = WeekendAppointment.Pending;
        if (!replaceExisting && pending != null && WeekendLedger.CanDo(pending, out _)) return pending;

        var next = WeekendSchedulePlan.NextWorthDoing();

        // Nothing left in this half-day: roll on to the next one that has something in it.
        //
        // A half-day empties faster than it looks like it should — finishing an obligation moves the clock
        // to the end of its hour and sweeps up everything the clock walked past — so the player would finish
        // a sponsor session at 11:00 and be stood in the paddock with no marker, nothing on the sheet they
        // could still do, and no way to tell that the answer was "the morning is over". Only reachable when
        // the slot is genuinely exhausted (everything in it done, missed, or behind the clock), so the sweep
        // AdvanceSlot runs on the way out has nothing left to take.
        bool rolled = false;
        for (int guard = WeekendSlots.Count; next == null && guard > 0 && !WeekendLedger.WeekendOver; guard--)
        {
            WeekendLedger.AdvanceSlot();
            rolled = true;
            next = WeekendSchedulePlan.NextWorthDoing();
        }

        if (next == null)
        {
            WeekendAppointment.Clear();
            if (WeekendLedger.WeekendOver)
                WeekendScheduleUI.Toast("That is the weekend done.");
            return null;
        }

        if (rolled)
            WeekendScheduleUI.Toast(WeekendSlots.Label(WeekendLedger.CurrentSlot) + " — " +
                                    WeekendSchedulePlan.Describe(next) + ".");

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

        // The day has been handed over but no marker went up with it. That happens when the booking was
        // made before the paddock was finished being staged — the venue it would point at was not standing
        // yet, so BookNextUp declined rather than aim at nothing. Try again while there is something on the
        // sheet worth booking: with NextWorthDoing non-null, BookNextUp cannot roll the half-day on, so this
        // retry can never march the weekend forward by itself.
        if (!WaitingToBeTold() && WeekendAppointment.Pending == null && Time.unscaledTime >= _rebookPoll
            && AtTheVenue() && WeekendSchedulePlan.NextWorthDoing() != null)
        {
            _rebookPoll = Time.unscaledTime + 1f;
            BookNextUp();
        }

        // Nobody turned up to hand the day over and nothing said they weren't going to — a paddock with no
        // liaison in it and no PitLaneStart to report the cast. Give up waiting rather than leave the player
        // in a weekend with no objective in it at all.
        if (WaitingToBeTold() && Time.unscaledTime >= _holdDeadline
            && !PlacedNPC.AnyCutsceneArmed && PlacedNPC.ObjectiveGiver() == null)
        {
            _giverStoodDown = true;
            BookNextUp();
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

        // A player who opened the sheet and picked something has told themselves where they are due. The
        // liaison is welcome to have her say, but nothing is waiting on her any more.
        MarkBriefed();
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
        // The sheet has put the player in the car for this hour: the track goes live with them.
        RaceWeekend.SessionLive = true;
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

        // The one booking that explains the phone leaves its own crib sheet inside it, so the answer is
        // still there in three weekends' time when the card has long gone.
        if (a.kind == ActivityKind.Orientation) LeavePhoneCribSheet();

        // One thing leads to the next: the moment this is settled, the following booking is the live
        // objective and its marker is already on screen.
        BookNextUp(replaceExisting: true);
    }

    // The orientation's own summary, left inside the app it spent fifteen minutes pointing at. It lands in
    // NOTES unread, so the tile carries a badge until the player has actually opened it once.
    static void LeavePhoneCribSheet()
    {
        string k = WeekendScripts.PhoneKeyName();

        PhoneNotes.Record(
            "phone.orientation",
            "How the phone works",
            "Crew chief",
            $"{k} opens it while you're on foot - arrows to move, E to open a tile, Esc to back out. " +
            "SCHEDULE is what's on today. TASKS is everything outstanding, and the number on it counts the " +
            "jobs that are finished and want handing back to whoever asked. NOTES is this - every favour " +
            "you agree to in the paddock, with the name of who wanted it.");
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

        // Session over: the track empties and the car goes back to being parked scenery until the sheet
        // sends the player out again.
        RaceWeekend.SessionLive = false;

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
