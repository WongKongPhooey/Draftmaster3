using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Draftmaster.Data;
using Draftmaster.Fans;

// Play-time driver dossier. A draggable on-screen panel (HandlingTuner's sibling — press F5) showing
// everything the game knows about the player's driver: identity (name / number / team from the car's
// DriverLabel), the matching Drivers-table row with all skill stats and ability ratings, fan appeal with
// autograph-fan diagnostics, the career ledger counters, and current rivalry standings.
//
// Self-bootstraps in every scene like HandlingTuner. It's a dev/insight tool — read-only, changes nothing.
public class DriverInfoPanel : MonoBehaviour
{
    public static DriverInfoPanel Instance { get; private set; }

    public bool startVisible = false;
    public Key toggleKey = Key.F5;   // F2 is the leaderboard (iRacing layout)
    [Tooltip("Seconds between data refreshes (DB lookup, ledger read, scene scans).")]
    public float refreshSeconds = 2f;

    bool _show;
    Rect _win = new Rect(410, 80, 400, 0);
    Vector2 _scroll;
    GUIStyle _head, _label, _mono;

    // Cached snapshot, rebuilt every refreshSeconds so OnGUI never hits the DB per frame.
    float _nextRefresh;
    string _playerName = "You";
    DriverLabel _playerLabel;
    Driver _driverRow;
    bool _dbReady;
    int _liveFans;
    bool _fanSpawnerPresent;
    readonly List<(string other, float value, DriverRelationships.Standing standing)> _rivalries = new();

    // Career ledger keys shown, in order. Any counter PlayerStatsLedger tracks can be added here.
    static readonly (string key, string label)[] LedgerRows =
    {
        ("starts",            "Race starts"),
        ("races",             "Races finished"),
        ("wins",              "Wins"),
        ("top5s",             "Top 5s"),
        ("top10s",            "Top 10s"),
        ("contacts.caused",   "Contacts caused"),
        ("contacts.received", "Contacts received"),
        ("paybacks.against",  "Paybacks against"),
        ("teamswitches",      "Team switches"),
        ("travelstops",       "Travel stops"),
        ("locations",         "Locations visited"),
        ("partsbought",       "Parts bought"),
        ("walkabout",         "Walkabouts"),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("DriverInfoPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<DriverInfoPanel>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _show = startVisible;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
            _show = !_show;

        if (_show && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.25f, refreshSeconds);
            Refresh();
        }
    }

    void Refresh()
    {
        var rt = FindObjectOfType<RacePositionTracker>();
        _playerName = rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : TeamSwitchController.kPlaceholderName;

        _playerLabel = FindPlayerLabel();
        string lookup = _playerLabel != null && !string.IsNullOrEmpty(_playerLabel.driverName)
            ? _playerLabel.driverName : _playerName;

        var dbm = DatabaseManager.Instance;
        _dbReady = dbm != null && dbm.IsReady;
        // The car number is the reliable key (the roster pins one driver per number); the name is only
        // a fallback for cars spawned without roster data.
        int lookupNumber = _playerLabel != null ? _playerLabel.carNumber : 0;
        _driverRow = _dbReady ? FindDriverRow(dbm, lookup, lookupNumber) : null;

        // Name the driver, not the seat: whoever races this car number, with "You" only as a last resort.
        if (_driverRow != null) _playerName = $"{_driverRow.FirstName} {_driverRow.LastName}".Trim();
        else if (_playerLabel != null && !string.IsNullOrEmpty(_playerLabel.driverName)) _playerName = _playerLabel.driverName;

        _liveFans = FindObjectsByType<AutographFan>(FindObjectsSortMode.None).Count(f => f.enabled);
        _fanSpawnerPresent = FindObjectOfType<AutographFanSpawner>() != null;

        _rivalries.Clear();
        foreach (var (a, b, value) in DriverRelationships.AllPairs())
        {
            bool aIsPlayer = DriverRelationships.IsPlayerName(a);
            bool bIsPlayer = DriverRelationships.IsPlayerName(b);
            if (aIsPlayer == bIsPlayer) continue; // want exactly one side to be the player
            _rivalries.Add((aIsPlayer ? b : a, value, DriverRelationships.StandingOf(value)));
        }
        _rivalries.Sort((x, y) => x.value.CompareTo(y.value)); // worst blood first
    }

    // The human car's label: a PlayerVehicleController with no AI input driver.
    static DriverLabel FindPlayerLabel()
    {
        var all = FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].GetComponent<SplineInputDriver>() == null)
                return all[i].GetComponent<DriverLabel>();
        return null;
    }

    static Driver FindDriverRow(DatabaseManager dbm, string name, int carNumber)
    {
        if (carNumber > 0)
            foreach (var d in dbm.Connection.Table<Driver>())
                if (d.CarNumber == carNumber) return d;

        if (string.IsNullOrWhiteSpace(name)) return null;
        string n = name.Trim().ToLowerInvariant();
        foreach (var d in dbm.Connection.Table<Driver>())
        {
            string last = (d.LastName ?? "").Trim().ToLowerInvariant();
            string shortName = (d.ShortName ?? "").Trim().ToLowerInvariant();
            string full = $"{d.FirstName} {d.LastName}".Trim().ToLowerInvariant();
            string nick = (d.Nickname ?? "").Trim().ToLowerInvariant();
            if (n == last || n == full || (shortName.Length > 0 && n == shortName) || (nick.Length > 0 && n == nick)) return d;
        }
        return null;
    }

    void OnGUI()
    {
        if (!_show) return;
        EnsureStyles();
        _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Driver Info  (F5)");
    }

    void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(390), GUILayout.Height(540));

        // --- Identity ---
        GUILayout.Label("Identity", _head);
        Row("Driver", _playerName);
        if (_playerLabel != null)
        {
            if (_playerLabel.carNumber != 0) Row("Car #", _playerLabel.carNumber.ToString());
            if (!string.IsNullOrEmpty(_playerLabel.carset)) Row("Carset", _playerLabel.carset);
            Row("Team", _playerLabel.teamId < 0
                ? "unaffiliated"
                : $"{(string.IsNullOrEmpty(_playerLabel.teamName) ? "Team" : _playerLabel.teamName)} (id {_playerLabel.teamId})");
        }
        else GUILayout.Label("(no player car in scene)", _label);

        // --- Database stats ---
        GUILayout.Space(6);
        GUILayout.Label("Stats  (Drivers table)", _head);
        if (!_dbReady) GUILayout.Label("(database not ready)", _label);
        else if (_driverRow == null)
            GUILayout.Label($"(no Drivers row matches \"{_playerName}\" — the player isn't a rostered driver)", _label);
        else
        {
            var d = _driverRow;
            Row("Name", $"{d.FirstName} {d.LastName}" + (string.IsNullOrEmpty(d.Nickname) ? "" : $" '{d.Nickname}'"));
            Row("Age", d.Age.ToString() + (d.PeakAge > 0 ? $"  (peak {d.PeakAge})" : ""));
            if (d.DebutSeason > 0) Row("Debut season", d.DebutSeason.ToString());
            if (d.Retired) Row("Retired", $"season {d.RetiredSeason}");
            StatBar("Current ability", d.CurrentAbility, Driver.AbilityMax);
            StatBar("Potential", d.PotentialAbility, Driver.AbilityMax);

            GUILayout.Space(4);
            GUILayout.Label("Track types", _label);
            StatBar("Short tracks", d.ShortTracks, Driver.StatMax);
            StatBar("Speedways", d.Speedways, Driver.StatMax);
            StatBar("Superspeedways", d.Superspeedways, Driver.StatMax);
            StatBar("Road courses", d.RoadCourses, Driver.StatMax);
            StatBar("Dirt courses", d.DirtCourses, Driver.StatMax);
            StatBar("Open wheel", d.OpenWheel, Driver.StatMax);

            GUILayout.Space(4);
            GUILayout.Label("Craft", _label);
            StatBar("Fuel management", d.FuelManagement, Driver.StatMax);
            StatBar("Tyre management", d.TyreManagement, Driver.StatMax);
            StatBar("Qualifying", d.Qualifying, Driver.StatMax);
            StatBar("Consistency", d.Consistency, Driver.StatMax);
            StatBar("Aggression", d.Aggression, Driver.StatMax);
            StatBar("Awareness", d.Awareness, Driver.StatMax);
            StatBar("Adaptability", d.Adaptability, Driver.StatMax);

            GUILayout.Space(4);
            GUILayout.Label("Standing", _label);
            StatBar("Sponsor appeal", d.SponsorAppeal, Driver.StatMax);
            StatBar("Fan support", d.FanSupport, Driver.StatMax);
            StatBar("Prestige", d.Prestige, Driver.StatMax);
        }

        // --- Fan appeal + autograph diagnostics ---
        GUILayout.Space(6);
        GUILayout.Label("Fan appeal", _head);
        StatBar("Appeal", Mathf.RoundToInt(FanAppeal.Value), (int)FanAppeal.Max);
        Row("Fans next wave", FanAppeal.FanCountForAppeal(FanAppeal.Value, 0, 6).ToString());
        Row("Fans in scene", _liveFans.ToString());
        Row("Fan spawner", _fanSpawnerPresent ? "present" : "not installed (no pit-lane track?)");
        Row("Pit lane", PitLane.Configured ? "configured" : "not configured");

        // --- Career ledger ---
        GUILayout.Space(6);
        GUILayout.Label("Career", _head);
        foreach (var (key, label) in LedgerRows)
            Row(label, PlayerStatsLedger.Get(key).ToString());

        // --- Rivalries ---
        GUILayout.Space(6);
        GUILayout.Label("Rivalries", _head);
        if (_rivalries.Count == 0) GUILayout.Label("(none on record)", _label);
        else
            foreach (var r in _rivalries.Take(10))
                Row(r.other, $"{r.value:+0.0;-0.0}  {r.standing}");

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }

    void Row(string name, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, _label, GUILayout.Width(160));
        GUILayout.Label(value, _label);
        GUILayout.EndHorizontal();
    }

    void StatBar(string name, int value, int max)
    {
        const int cells = 10;
        int filled = Mathf.Clamp(Mathf.RoundToInt(cells * value / (float)Mathf.Max(1, max)), 0, cells);
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, _label, GUILayout.Width(160));
        GUILayout.Label(new string('█', filled) + new string('░', cells - filled), _mono, GUILayout.Width(120));
        GUILayout.Label($"{value}/{max}", _label);
        GUILayout.EndHorizontal();
    }

    void EnsureStyles()
    {
        if (_head != null) return;
        _head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        _label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        _label.normal.textColor = Color.white;
        _mono = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        _mono.normal.textColor = new Color(0.55f, 0.9f, 1f);
    }
}
