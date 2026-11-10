using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.MasterDraft;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runs the cards during a Master Draft race. Spawned into the race scene only in GameSession.Mode.MasterDraft
// (with a race handed off by MasterDraftRaceLauncher), so nothing about it exists in any other mode.
//
//  * The hand: the team's action deck, shuffled at load (RaceHand). Every time the player's car crosses the
//    start/finish line after the green — read off RacePositionTracker's lap count for the player's entry,
//    the same count RaceDirector classifies by — one card is drawn. Keys 1-5 play a card; on a pad, Select
//    steps through the hand and South plays the highlighted card; on a phone each card is a button.
//  * The team: the car card trims the player's car (PlayerVehicleController.card*Scale), the crew chief
//    trims pit-stop time (PlayerPitService), fuel burn (FuelTank) and tyre wear — all within a few percent
//    (TeamEffects).
//  * The flag: when the results go up, coins are paid through the backend by finishing position plus a bonus
//    per card played, and the results screen offers the way back to the Master Draft menu (RaceDirector).
//
// A championship round (MasterDraftRaceLauncher.LaunchSeriesRound, session.Pending.series) has no cards: the host
// sets the race distance before the green, gives the player's car its class step (CarProgression.PowerScale /
// GripScale through card*Scale), and at the flag scores the round (SeriesRace.RecordResult -> RaceRewards), saves
// the profile and leaves the result for the Series screen.
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

    RaceHand _hand;
    SeriesRacePlan _series;
    MasterDraftSession.SeriesOutcome _seriesOutcome;
    readonly ActiveEffects _effects = new();
    MasterDraftSession _session;
    CardDefinition _carCard, _chiefCard, _driverCard;

    PlayerVehicleController _car;
    FuelTank _fuel;
    float _fuelBase, _idleBase, _wearBase = 1f;
    PlayerPitService _pit;
    float _pitBase = -1f;

    bool _baselined;
    int _lastLap;
    int _selected;
    bool _awarded;
    int _coinsAwarded = -1;

    string _toast = "";
    float _toastUntil;

    // The line the results screen shows under the classification.
    public static string ResultLine
    {
        get
        {
            var host = Instance;
            if (host == null) return "";
            if (host._series != null) return host.SeriesLine();
            if (host._coinsAwarded < 0) return "Paying out…";
            return $"Coins +{host._coinsAwarded}   Cards played {host._hand?.CardsPlayed ?? 0}";
        }
    }

    // Results screen > Back to Master Draft.
    public static void ReturnToMenu()
    {
        if (DriveOrientationController.HoldSceneChangeForLandscape(ReturnToMenu)) return;
        RaceWeekend.SessionLive = false;
        if (Application.CanStreamedLevelBeLoaded(MasterDraftRaceLauncher.MenuScene))
            SceneManager.LoadScene(MasterDraftRaceLauncher.MenuScene);
        else
            SceneManager.LoadScene(0);
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
        if (_series != null) return;   // a championship round: no cards, no team
        var team = profile.team;
        _driverCard = profile.Find(_session.Pending?.driverUid)?.def;
        _carCard = profile.Find(team.car)?.def;
        _chiefCard = profile.Find(team.crewChief)?.def;

        var deck = new List<CardDefinition>();
        foreach (var uid in team.actionDeck)
        {
            var card = profile.Find(uid);
            if (card != null && card.def.type == CardType.Action) deck.Add(card.def);
        }
        _hand = new RaceHand(deck, System.DateTime.UtcNow.Ticks);
        PlayerPitService.Serviced += OnServiced;
    }

    void OnDestroy()
    {
        PlayerPitService.Serviced -= OnServiced;
        ReleaseCar();
        if (Instance == this) Instance = null;
    }

    void OnServiced(PlayerVehicleController car)
    {
        if (!_effects.PerfectPitPending) return;
        _effects.PitStopUsed();
        Toast("Perfect pit stop!");
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (_series != null)
        {
            FindCar();
            if (_car != null)
            {
                _car.cardPowerScale = _series.powerScale;
                _car.cardGripScale = _series.gripScale;
                _car.cardDraftScale = 1f;
            }
            SetRaceDistance();
            ScoreRoundAtTheFlag();
            return;
        }
        if (_hand == null) return;
        FindCar();
        ApplyTeamAndEffects();
        CountCrossings();
        PayOutAtTheFlag();
        if (!RacePauseMenu.IsPaused && !RaceDirector.InResults) ReadInput();
    }

    bool Racing => RaceDirector.Instance != null && RaceDirector.Progress01 >= 0f && !RaceDirector.InResults;

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

    void ApplyTeamAndEffects()
    {
        float now = Time.time;
        if (_car != null)
        {
            _car.cardPowerScale = TeamEffects.PowerScale(_carCard) * _effects.PowerScale(now);
            _car.cardGripScale = TeamEffects.GripScale(_carCard);
            _car.cardDraftScale = TeamEffects.DraftScale(_carCard) * _effects.DraftScale(now);
            _car.wearRateScale = _wearBase * TeamEffects.TyreWearScale(_chiefCard);
            if (_fuel != null)
            {
                float burn = TeamEffects.FuelBurnScale(_chiefCard) * _effects.FuelBurnScale(now);
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
            _pit.serviceSeconds = _pitBase * TeamEffects.PitTimeScale(_chiefCard) * _effects.PitTimeScale;
    }

    // One draw per crossing of the line, counted from the green.
    void CountCrossings()
    {
        if (!Racing) return;
        var entry = PlayerEntry();
        if (entry == null) return;
        if (!_baselined)
        {
            _baselined = true;
            _lastLap = entry.lap;
            return;
        }
        while (entry.lap > _lastLap)
        {
            _lastLap++;
            var drawn = _hand.OnStartFinishCrossed();
            Toast(drawn != null ? $"Drew {drawn.name}" : _hand.Hand.Count >= RaceHand.HandCap ? "Hand full — no card drawn" : "Deck empty");
        }
    }

    // RaceDirector reads raceLaps once, at the green (as AIOnlyRaceWatch sets it).
    void SetRaceDistance()
    {
        var director = RaceDirector.Instance;
        if (director != null && _series.laps > 0 && director.ScheduledLaps < 0 && director.raceLaps != _series.laps)
            director.raceLaps = _series.laps;
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
            cardsPlayed = _hand.CardsPlayed,
        };
        var result = await _session.Backend.AwardRace(position, _hand.CardsPlayed);
        outcome.coinsAwarded = result.ok ? result.awarded : 0;
        _session.LastRace = outcome;
        _coinsAwarded = outcome.coinsAwarded;
        ReleaseCar();   // a victory lap after the flag runs stock
    }

    // ------------------------------------------------------------------ playing cards

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Play(0);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Play(1);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Play(2);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Play(3);
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) Play(4);
        }

        if (Gamepad.current != null)
        {
            if (PadInput.WasPressed(PadBindings.NextCard) && _hand.Hand.Count > 0)
                _selected = (_selected + 1) % _hand.Hand.Count;
            if (PadInput.WasPressed(PadBindings.PlayCard)) Play(_selected);
        }
    }

    void Play(int index)
    {
        if (!Racing) { Toast("Cards play once the race is green."); return; }
        if (index < 0 || index >= _hand.Hand.Count) return;
        var spec = ActionCatalog.Get(_hand.Hand[index].effect);
        if (spec == null || !spec.Implemented) { Toast("That card can't be played yet."); return; }
        if (spec.Effect == ActionEffect.PerfectPitStop && _effects.PerfectPitPending)
        {
            Toast("A perfect stop is already waiting.");
            return;
        }

        var card = _hand.Play(index);
        if (card == null) return;
        Apply(spec);
        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _hand.Hand.Count - 1));
        Toast($"Played {card.name}");
    }

    void Apply(ActionSpec spec)
    {
        switch (spec.Effect)
        {
            case ActionEffect.FreshRubber:
                if (_car == null) return;
                float keep = 1f - Mathf.Clamp01(spec.Magnitude);
                var tyres = _car.GetComponent<TireModel>();
                if (tyres != null) for (int i = 0; i < tyres.wear.Length; i++) tyres.wear[i] *= keep;
                _car.wearFront *= keep;
                _car.wearRear *= keep;
                return;

            case ActionEffect.QuickRepair:
                if (_car == null) return;
                var body = _car.GetComponentInChildren<VehicleDamage>();
                if (body != null) body.Repair(spec.Magnitude);
                return;

            default:
                // Timed boosts and the next-stop flag.
                _effects.Start(spec, Time.time);
                return;
        }
    }

    void Toast(string message)
    {
        _toast = message;
        _toastUntil = Time.unscaledTime + 2.5f;
    }

    // ------------------------------------------------------------------ HUD

    void OnGUI()
    {
        if (_hand == null || RacePauseMenu.IsPaused || RaceDirector.InResults) return;

        bool pad = InputGlyphs.UsingGamepad;
        float w = PixelGUI.Px(92f), h = PixelGUI.MenuButtonH, gap = PixelGUI.Px(4f);
        int n = RaceHand.HandCap;
        float total = n * w + (n - 1) * gap;
        float x0 = Mathf.Round((Screen.width - total) * 0.5f);
        float y = Screen.height - h - PixelGUI.Px(PixelGUI.Handheld ? 64f : 12f);

        // Status line over the hand: draw pile, running effects, the last thing that happened.
        float now = Time.time;
        var running = new List<string>();
        foreach (var e in _effects.Running(now))
            running.Add($"{ActionCatalog.Get(e)?.Name} {Mathf.CeilToInt(_effects.Remaining(e, now))}s");
        if (_effects.PerfectPitPending) running.Add("Perfect Stop ready");
        string status = $"Deck {_hand.DrawPileCount}" + (running.Count > 0 ? "   " + string.Join("   ", running) : "");
        if (Time.unscaledTime < _toastUntil) status = _toast;
        var line = new Rect(x0, y - PixelGUI.DataLineH - PixelGUI.Px(2f), total, PixelGUI.DataLineH);
        PixelGUI.Fill(line, new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(line, " " + status, PixelGUI.Data);

        for (int i = 0; i < n; i++)
        {
            var r = new Rect(x0 + i * (w + gap), y, w, h);
            if (i >= _hand.Hand.Count)
            {
                PixelGUI.Fill(r, new Color(0f, 0f, 0f, 0.35f));
                PixelGUI.Frame(r, PixelGUI.PlateLight);
                continue;
            }
            var card = _hand.Hand[i];
            string key = pad ? "" : $"{i + 1} ";
            if (PixelGUI.Button(r, key + card.name)) Play(i);
            var rarity = MasterDraftCardView.RarityColour(card.rarity);
            PixelGUI.Frame(r, pad && i == _selected ? PixelGUI.Gold : rarity, pad && i == _selected ? PixelGUI.Px(2f) : PixelGUI.Px(1f));
        }

        if (pad && _hand.Hand.Count > 0)
        {
            var hint = new Rect(x0, y + h + PixelGUI.Px(2f), total, PixelGUI.DataLineH);
            GUI.Label(hint, $" {InputGlyphs.PadName(PadBindings.NextCard)} Next card   {InputGlyphs.PadName(PadBindings.PlayCard)} Play", PixelGUI.DataDim);
        }
    }
}
