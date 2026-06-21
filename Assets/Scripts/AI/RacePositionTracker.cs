using System.Collections.Generic;
using UnityEngine;

// Keeps the live running order of the whole field (AI + the player) and exposes each entrant's position.
// Progress = lapsCompleted * trackLength + distanceAlongTrack, so it stays correct across the start/finish line.
// The safety car is excluded (it's not a competitor). Renders a small "P x / N" readout for the player.
public class RacePositionTracker : MonoBehaviour
{
    public static RacePositionTracker Instance { get; private set; }

    [Tooltip("Track the field runs on. Auto-found if empty.")]
    public TrackBuilder track;
    [Tooltip("The human player's car. Auto-found (a PlayerVehicleController with no AI SplineInputDriver) if empty.")]
    public PlayerVehicleController playerCar;
    public string playerName = "You";
    [Tooltip("Draw the player's position on-screen.")]
    public bool showHud = true;

    public class Entry
    {
        public SplineDriver spline;   // null for the player
        public Transform tf;
        public bool isPlayer;
        public string name;
        public int carNumber;
        public int lap;
        public float prevDist;
        public bool hasPrev;
        public float progress;
        public float speedMps;
        public float gapToLeaderSec;
        public int position;          // 1 = leader
    }

    readonly List<Entry> _entries = new();
    readonly Dictionary<SplineDriver, Entry> _bySpline = new();
    Entry _playerEntry;
    float _len;

    public IReadOnlyList<Entry> Order => _entries;
    public int FieldSize => _entries.Count;
    public int PlayerPosition => _playerEntry != null ? _playerEntry.position : 0;
    public int PositionOf(SplineDriver d) => (d != null && _bySpline.TryGetValue(d, out var e)) ? e.position : 0;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        if (track != null)
        {
            var s = track.SampleCenterline();
            _len = s.Count > 0 ? s[s.Count - 1].distance : 0f;
        }
    }

    void Update()
    {
        if (track == null || _len <= 0f) return;
        SyncEntries();

        for (int i = 0; i < _entries.Count; i++)
            UpdateProgress(_entries[i]);

        _entries.Sort((a, b) => b.progress.CompareTo(a.progress));
        for (int i = 0; i < _entries.Count; i++) _entries[i].position = i + 1;

        // Time gap to the leader = distance behind / leader's speed.
        if (_entries.Count > 0)
        {
            var leader = _entries[0];
            float leadSpeed = Mathf.Max(leader.speedMps, 8f);
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].gapToLeaderSec = (leader.progress - _entries[i].progress) / leadSpeed;
        }
    }

    void SyncEntries()
    {
        // Player.
        if (_playerEntry == null)
        {
            if (playerCar == null) playerCar = FindPlayerCar();
            if (playerCar != null)
            {
                _playerEntry = new Entry { isPlayer = true, tf = playerCar.transform, name = playerName };
                _entries.Add(_playerEntry);
            }
        }

        // AI / field drivers from RaceField. Skip the safety car (not a competitor).
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || _bySpline.ContainsKey(d)) continue;
            if (d.GetComponent<SafetyCar>() != null) continue;

            var label = d.GetComponent<DriverLabel>();
            var e = new Entry
            {
                spline = d,
                tf = d.transform,
                name = label != null && !string.IsNullOrEmpty(label.driverName) ? label.driverName : d.name,
                carNumber = label != null ? label.carNumber : 0,
            };
            _bySpline[d] = e;
            _entries.Add(e);
        }

        // Drop despawned drivers.
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.isPlayer) continue;
            if (e.spline == null) { _bySpline.Remove(e.spline); _entries.RemoveAt(i); }
        }
    }

    void UpdateProgress(Entry e)
    {
        float len, dist;
        if (e.isPlayer)
        {
            if (e.tf == null) return;
            len = _len;
            dist = track.NearestCenterlineDistance(e.tf.position);
            e.speedMps = playerCar != null ? playerCar.SpeedMps : 0f;
        }
        else
        {
            if (e.spline == null || e.spline.TrackLength <= 0f) return;
            len = e.spline.TrackLength;
            dist = e.spline.DistanceOnTrack;
            e.speedMps = e.spline.SpeedMps;
        }

        if (e.hasPrev)
        {
            // Detect a forward wrap across the start/finish line (distance drops by ~a lap).
            if (e.prevDist - dist > len * 0.5f) e.lap++;
            else if (dist - e.prevDist > len * 0.5f) e.lap--; // small backwards nudge across the line
        }
        e.prevDist = dist;
        e.hasPrev = true;
        e.progress = e.lap * len + dist;
    }

    PlayerVehicleController FindPlayerCar()
    {
        var all = FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            // AI cars carry a SplineInputDriver; the human's car does not.
            if (all[i].GetComponent<SplineInputDriver>() == null && all[i].enabled) return all[i];
        }
        return null;
    }

    void OnGUI()
    {
        if (!showHud || _playerEntry == null || _entries.Count == 0) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight,
        };
        style.normal.textColor = Color.white;
        var rect = new Rect(Screen.width - 280f, 16f, 260f, 50f);
        GUI.Label(rect, $"P{_playerEntry.position} / {_entries.Count}", style);
    }
}
