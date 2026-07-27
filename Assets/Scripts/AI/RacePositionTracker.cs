using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Keeps the live running order of the whole field and exposes each entrant's position. Progress =
// lapsCompleted * trackLength + distanceAlongTrack, so it stays correct across the start/finish line.
//
// Enumerates every car tagged "Vehicle" by transform (not via RaceField/SplineDriver), so it works the SAME on
// the host and on clients: a client's AI are NetworkTransform puppets with their brains disabled (so they're not
// in RaceField), but their transforms still move — distance is sampled from the car's world position. Other human
// players are counted too. The safety car is excluded. Renders a small "P x / N" readout for the local player.
public class RacePositionTracker : MonoBehaviour
{
    public static RacePositionTracker Instance { get; private set; }

    [Tooltip("Track the field runs on. Auto-found if empty.")]
    public TrackBuilder track;
    public string playerName = "You";
    [Tooltip("Draw the player's position on-screen.")]
    public bool showHud = true;
    [Tooltip("Authored position readout (auto-wired in editor). 'P x / N' is written here each frame; its panel is hidden until the local player is known.")]
    public Text positionText;
    [Tooltip("How often (s) to rescan the scene for cars joining/leaving. Progress updates every frame regardless.")]
    public float membershipRefresh = 0.5f;

    public class Entry
    {
        public Transform tf;
        public SplineDriver spline;   // present on host AI (precise distance); null/disabled on client puppets
        public bool isPlayer;         // the LOCAL player's car
        public string name;
        public int carNumber;
        public int lap;
        public float prevDist;
        public bool hasPrev;
        public float prevProgress;
        public float progress;
        public float speedMps;
        public float gapToLeaderSec;
        public int position;          // 1 = leader
    }

    readonly List<Entry> _entries = new();
    readonly Dictionary<Transform, Entry> _byTf = new();
    Entry _playerEntry;
    float _len;
    float _refreshTimer;

    public IReadOnlyList<Entry> Order => _entries;
    public int FieldSize => _entries.Count;
    // Main centerline length (m). Lets callers turn an entry's progress back into a fraction of a lap.
    public float TrackLength => _len;
    public int PlayerPosition => _playerEntry != null ? _playerEntry.position : 0;
    public int PositionOf(SplineDriver d)
    {
        if (d == null) return 0;
        for (int i = 0; i < _entries.Count; i++) if (_entries[i].spline == d) return _entries[i].position;
        return 0;
    }

    // Current lap (0-based, as counted across the line) for a car by its transform. 0 if unknown.
    public int LapOf(Transform tf)
    {
        if (tf == null) return 0;
        return _byTf.TryGetValue(tf, out var e) ? e.lap : 0;
    }

    // Move the local-player flag to another car (mid-race team switching). The flag is otherwise sticky —
    // RefreshIdentity never demotes a car once it's been the player — so an explicit hand-over is the only
    // way to transfer it. Position/lap history stays with each car; only "which car is the player" moves.
    public void SetLocalPlayer(Transform tf)
    {
        RefreshMembership();   // make sure the target car has an entry before flagging it
        _playerEntry = null;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.isPlayer = e.tf == tf;
            if (e.isPlayer) _playerEntry = e;
        }
    }

    void Awake() { Instance = this; ResolveRefs(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        if (track != null)
        {
            var s = track.SampleCenterline();
            _len = s.Count > 0 ? s[s.Count - 1].distance : 0f;
        }
        RefreshMembership();
    }

    void Update()
    {
        if (track == null || _len <= 0f) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f) { RefreshMembership(); _refreshTimer = membershipRefresh; }

        for (int i = 0; i < _entries.Count; i++) UpdateProgress(_entries[i]);

        _entries.Sort((a, b) => b.progress.CompareTo(a.progress));
        for (int i = 0; i < _entries.Count; i++) _entries[i].position = i + 1;

        if (_entries.Count > 0)
        {
            var leader = _entries[0];
            float leadSpeed = Mathf.Max(leader.speedMps, 8f);
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].gapToLeaderSec = (leader.progress - _entries[i].progress) / leadSpeed;
        }

        UpdateHud();
    }

    void RefreshMembership()
    {
        var cars = GameObject.FindGameObjectsWithTag("Vehicle");
        for (int i = 0; i < cars.Length; i++)
        {
            var go = cars[i];
            if (go == null) continue;
            if (go.GetComponent<SafetyCar>() != null) continue;          // not a competitor
            var tf = go.transform;
            if (_byTf.ContainsKey(tf)) { RefreshIdentity(_byTf[tf], go); continue; }

            var e = new Entry { tf = tf, spline = go.GetComponent<SplineDriver>() };
            RefreshIdentity(e, go);
            _byTf[tf] = e;
            _entries.Add(e);
        }

        // Also pull registered drivers (single-player AI may not be tagged "Vehicle" but are in RaceField).
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null) continue;
            var go = d.gameObject;
            if (go.GetComponent<SafetyCar>() != null) continue;
            var tf = go.transform;
            if (_byTf.ContainsKey(tf)) continue;
            var e = new Entry { tf = tf, spline = d };
            RefreshIdentity(e, go);
            _byTf[tf] = e;
            _entries.Add(e);
        }

        // Drop despawned cars (destroyed transforms compare == null).
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].tf == null)
            {
                if (_entries[i] == _playerEntry) _playerEntry = null;
                _entries.RemoveAt(i);
            }
        }
        // Prune dictionary keys whose transform died.
        var dead = new List<Transform>();
        foreach (var kv in _byTf) if (kv.Key == null) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) _byTf.Remove(dead[i]);
    }

    // Name/number + local-player flag. The local player's car is the only enabled PlayerVehicleController with no
    // AI SplineInputDriver (remote players are disabled; AI carry a SplineInputDriver).
    void RefreshIdentity(Entry e, GameObject go)
    {
        var pvc = go.GetComponent<PlayerVehicleController>();
        bool isLocal = pvc != null && pvc.enabled && go.GetComponent<SplineInputDriver>() == null;
        // Sticky: once a car has been identified as the local player it stays the player. Drive-off /
        // Crew Chief mode hands the car to an AI brain (SplineInputDriver added, PVC driven externally),
        // which would otherwise demote it and lose the player's position/results identity.
        if (e.isPlayer) isLocal = true;
        e.isPlayer = isLocal;
        if (isLocal) _playerEntry = e;

        var player = go.GetComponent<NetworkedCarBindings>();
        if (player != null)
        {
            e.name = isLocal ? playerName : (string.IsNullOrEmpty(player.DisplayLabel) ? "Player" : player.DisplayLabel);
            e.carNumber = player.Number;
            return;
        }

        var label = go.GetComponent<DriverLabel>();
        if (label != null)
        {
            e.name = !string.IsNullOrEmpty(label.driverName) ? label.driverName : go.name;
            e.carNumber = label.carNumber;
            return;
        }

        e.name = isLocal ? playerName : go.name;
    }

    void UpdateProgress(Entry e)
    {
        if (e.tf == null) return;

        float len, dist;
        bool rebaseToLine = true;   // false for pit-space distances, which aren't on the main centerline
        if (e.spline != null && e.spline.enabled && e.spline.TrackLength > 0f)
        {
            len = e.spline.TrackLength;
            dist = e.spline.DistanceOnTrack;
        }
        else
        {
            len = _len;
            // A spline-less car in the pit lane (the player's parked car pre-race) is located by pit
            // distance, matching what AI splines report while on pit. Projecting a pit box onto the MAIN
            // centerline can land just short of the start/finish line — a near-full-lap progress that
            // ranked the parked player car P1.
            bool onPit = track.IsOnPitSurface(e.tf.position);
            rebaseToLine = !onPit;
            dist = onPit
                ? track.NearestPitDistance(e.tf.position)
                : track.NearestCenterlineDistance(e.tf.position);
        }

        // Distances are measured from the start of segment[0], but the lap rolls at the painted
        // start/finish line — which sits partway down the main straight on most tracks. Rebase so the
        // wrap (and therefore the lap count, lap timing and the race finish) happens at the line.
        if (rebaseToLine && len > 0f)
        {
            float sf = (track != null && track.track != null) ? track.track.startFinishDistance : 0f;
            if (sf != 0f) dist = Mathf.Repeat(dist - sf, len);
        }

        if (e.hasPrev)
        {
            if (e.prevDist - dist > len * 0.5f) e.lap++;          // forward wrap across the line
            else if (dist - e.prevDist > len * 0.5f) e.lap--;     // small backward nudge across the line
        }
        e.prevDist = dist;
        e.hasPrev = true;
        e.progress = e.lap * len + dist;

        float dt = Time.deltaTime;
        if (dt > 0f) e.speedMps = Mathf.Clamp((e.progress - e.prevProgress) / dt, 0f, 200f);
        e.prevProgress = e.progress;
    }

    // Writes the player's running position into the authored readout and hides the panel until it's known.
    void UpdateHud()
    {
        if (positionText == null) return;
        bool show = showHud && _playerEntry != null && _entries.Count > 0;

        // Toggle the backing Panel (the text's parent) so the backdrop hides with the label.
        var panel = positionText.transform.parent;
        var panelGo = (panel != null && panel != transform) ? panel.gameObject : positionText.gameObject;
        if (panelGo.activeSelf != show) panelGo.SetActive(show);

        if (show) positionText.text = $"P{_playerEntry.position} / {_entries.Count}";
    }

    // Locate the authored readout. Runtime fallback + editor baking.
    void ResolveRefs()
    {
        if (positionText == null)
        {
            var t = transform.Find("Panel/PositionText");
            if (t != null) positionText = t.GetComponent<Text>();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) ResolveRefs();
    }
#endif
}
