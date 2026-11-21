using Draftmaster.MasterDraft;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the cards during a Master Draft race. Spawned into the race scene only in GameSession.Mode.MasterDraft
// (with a race handed off by MasterDraftRaceLauncher), so nothing about it exists in any other mode.
//
//  * The team: the car card trims the player's car (PlayerVehicleController.card*Scale), the crew chief
//    trims pit-stop time (PlayerPitService), fuel burn (FuelTank) and tyre wear — all within a few percent
//    (TeamEffects).
//  * The flag: when the results go up, coins are paid through the backend by finishing position, and the results screen offers the way back to the Master Draft menu (RaceDirector).
//
// A championship round (MasterDraftRaceLauncher.LaunchSeriesRound, session.Pending.series) has no cards: the host
// sets the race distance before the green, gives the player's car its class step (CarProgression.PowerScale /
// GripScale through card*Scale), and at the flag scores the round (SeriesRace.RecordResult -> RaceRewards), saves
// the profile and leaves the result for the Series screen.
//
// A challenge chapter (MasterDraftRaceLauncher.LaunchChallenge, session.Pending.challenge) runs the same way with
// no cards, and at the flag records the chapter (ChallengeRace.RecordResult -> RaceRewards.Challenge, which pays the
// prize when one is due) and leaves the result for the Challenges screen's prize reveal.
public class MasterDraftRaceHost : MonoBehaviour
{
    public static MasterDraftRaceHost Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !GameSession.IsMasterDraft) return;
        var session = MasterDraftSession.Instance;
        if (session == null || !session.InRace || session.Profile == null) return;
        if (Object.FindFirstObjectByType<GridSpawner>() == null) return;   // not a race scene
        if (Instance != null) return;
        new GameObject("MasterDraftRaceHost").AddComponent<MasterDraftRaceHost>();
    }

    bool _quickRace;
    SeriesRacePlan _series;
    MasterDraftSession.SeriesOutcome _seriesOutcome;
    ChallengeRacePlan _challenge;
    MasterDraftSession.ChallengeOutcome _challengeOutcome;
    MasterDraftSession _session;
    CardDefinition _carCard, _chiefCard, _driverCard;

    PlayerVehicleController _car;
    FuelTank _fuel;
    float _fuelBase, _idleBase, _wearBase = 1f;
    PlayerPitService _pit;
    float _pitBase = -1f;

    bool _awarded;
    int _coinsAwarded = -1;

    // The line the results screen shows under the classification.
    public static string ResultLine
    {
        get
        {
            var host = Instance;
            if (host == null) return "";
            if (host._series != null) return host.SeriesLine();
            if (host._challenge != null) return host.ChallengeLine();
            if (host._coinsAwarded < 0) return "Paying out…";
            return $"Coins +{host._coinsAwarded}";
        }
    }

    // Results screen > Back to Master Draft.
    public static void ReturnToMenu()
    {
        if (DriveOrientationController.HoldSceneChangeForLandscape(ReturnToMenu)) return;
        RaceWeekend.SessionLive = false;
        if (Application.CanStreamedLevelBeLoaded(MasterDraftRaceLauncher.MenuScene))
            LoadingScreen.Load(MasterDraftRaceLauncher.MenuScene);
        else
            LoadingScreen.Load(0);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _session = MasterDraftSession.Instance;
        var profile = _session.Profile;
        _series = _session.Pending?.series;
        _challenge = _session.Pending?.challenge;
        if (_series != null || _challenge != null) return;   // a championship round or a challenge: no cards, no team
        var team = profile.team;
        _driverCard = profile.Find(_session.Pending?.driverUid)?.def;
        _carCard = profile.Find(team.car)?.def;
        _chiefCard = profile.Find(team.crewChief)?.def;
        _quickRace = true;
    }

    void OnDestroy()
    {
        ReleaseCar();
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (_series != null || _challenge != null)
        {
            FindCar();
            if (_car != null && !_awarded)
            {
                _car.cardPowerScale = _series != null ? _series.powerScale : _challenge.powerScale;
                _car.cardGripScale = _series != null ? _series.gripScale : _challenge.gripScale;
                _car.cardDraftScale = 1f;
            }
            SetRaceDistance(_series != null ? _series.laps : _challenge.laps);
            if (_series != null) ScoreRoundAtTheFlag();
            else ScoreChallengeAtTheFlag();
            return;
        }
        if (!_quickRace) return;
        FindCar();
        ApplyTeam();
        PayOutAtTheFlag();
    }

    RacePositionTracker.Entry PlayerEntry()
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null) return null;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].isPlayer && order[i].tf != null) return order[i];
        return null;
    }

    void FindCar()
    {
        var entry = PlayerEntry();
        var car = entry != null ? entry.tf.GetComponent<PlayerVehicleController>() : PlayerVehicleController.Human;
        if (car == _car) return;

        ReleaseCar();
        _car = car;
        if (_car == null) return;
        _wearBase = _car.wearRateScale;
        _fuel = _car.GetComponent<FuelTank>();
        if (_fuel != null) { _fuelBase = _fuel.litresPerSecAtFull; _idleBase = _fuel.idleBurn; }
    }

    // Hands the car back stock (a team switch, or the race ending).
    void ReleaseCar()
    {
        if (_car != null)
        {
            _car.cardPowerScale = _car.cardGripScale = _car.cardDraftScale = 1f;
            _car.wearRateScale = _wearBase;
            if (_fuel != null) { _fuel.litresPerSecAtFull = _fuelBase; _fuel.idleBurn = _idleBase; }
        }
        _car = null;
        _fuel = null;
    }

    void ApplyTeam()
    {
        if (_car != null)
        {
            _car.cardPowerScale = TeamEffects.PowerScale(_carCard);
            _car.cardGripScale = TeamEffects.GripScale(_carCard);
            _car.cardDraftScale = TeamEffects.DraftScale(_carCard);
            _car.wearRateScale = _wearBase * TeamEffects.TyreWearScale(_chiefCard);
            if (_fuel != null)
            {
                float burn = TeamEffects.FuelBurnScale(_chiefCard);
                _fuel.litresPerSecAtFull = _fuelBase * burn;
                _fuel.idleBurn = _idleBase * burn;
            }
        }

        if (_pit == null)
        {
            _pit = FindFirstObjectByType<PlayerPitService>();
            if (_pit != null) _pitBase = _pit.serviceSeconds;
        }
        if (_pit != null && _pitBase > 0f)
            _pit.serviceSeconds = _pitBase * TeamEffects.PitTimeScale(_chiefCard);
    }

    // RaceDirector reads raceLaps once, at the green (as AIOnlyRaceWatch sets it).
    void SetRaceDistance(int laps)
    {
        var director = RaceDirector.Instance;
        if (director != null && laps > 0 && director.ScheduledLaps < 0 && director.raceLaps != laps)
            director.raceLaps = laps;
    }

    async void ScoreRoundAtTheFlag()
    {
        if (_awarded || !RaceDirector.InResults || _session == null) return;
        _awarded = true;
        ReleaseCar();   // a victory lap after the flag runs stock

        var outcome = new MasterDraftSession.SeriesOutcome { plan = _series };
        try
        {
            var def = _session.Series?.Get(_series.seriesId);
            outcome.rewards = SeriesRace.RecordResult(_session.Profile, def, _series.seriesId,
                RaceDirector.ClassifiedCarNumbers(), RaceDirector.PlayerFinishPosition, _session.Cars);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            outcome.error = "This round couldn't be scored.";
        }
        _session.LastSeries = outcome;
        _seriesOutcome = outcome;
        if (outcome.rewards != null && _session.Backend != null) await _session.Backend.SaveProfile();
    }

    async void ScoreChallengeAtTheFlag()
    {
        if (_awarded || !RaceDirector.InResults || _session == null) return;
        _awarded = true;
        ReleaseCar();   // a victory lap after the flag runs stock

        var outcome = new MasterDraftSession.ChallengeOutcome { plan = _challenge };
        try
        {
            outcome.rewards = ChallengeRace.RecordResult(_session.Profile, _session.Challenges, _challenge,
                RaceDirector.PlayerFinishPosition, _session.Cars);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            outcome.error = "This challenge couldn't be scored.";
        }
        _session.LastChallenge = outcome;
        _challengeOutcome = outcome;
        if (outcome.rewards != null && _session.Backend != null) await _session.Backend.SaveProfile();
    }

    string ChallengeLine()
    {
        var o = _challengeOutcome;
        if (o == null) return $"{_challenge.eventName}: {_challenge.chapterName}";
        if (o.rewards == null) return o.error;
        var r = o.rewards;
        string line = r.challenge.completed ? "Challenge complete!" : "Win to complete the challenge";
        line += $"   XP +{r.xp}";
        if (r.money > 0) line += $"   +{r.money:N0}";
        if (r.prizes.Count > 0) line += "   Prize waiting in Master Draft";
        return line;
    }

    string SeriesLine()
    {
        var o = _seriesOutcome;
        if (o == null) return $"{_series.seriesName}  Round {_series.round}/{_series.rounds}";
        if (o.rewards == null) return o.error;
        var r = o.rewards;
        string line = $"Round {r.round.round}/{_series.rounds}  +{r.round.playerPoints} pts  XP +{r.xp}";
        if (r.championshipOver) line += $"   Champ P{r.championshipPosition}  +{r.money:N0}";
        return line;
    }

    async void PayOutAtTheFlag()
    {
        if (_awarded || !RaceDirector.InResults || _session?.Backend == null) return;
        _awarded = true;

        int position = RaceDirector.PlayerFinishPosition;
        var outcome = new MasterDraftSession.RaceOutcome
        {
            trackId = _session.Pending?.trackId ?? TrackSelection.CurrentId,
            position = position,
            fieldSize = RaceDirector.ClassifiedCount,
        };
        var result = await _session.Backend.AwardRace(position);
        outcome.coinsAwarded = result.ok ? result.awarded : 0;
        _session.LastRace = outcome;
        _coinsAwarded = outcome.coinsAwarded;
        ReleaseCar();   // a victory lap after the flag runs stock
    }
}
