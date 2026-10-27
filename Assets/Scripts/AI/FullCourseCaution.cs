using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// Full-course cautions on ovals. A yellow flag on an oval — a car stopped on the track (CautionWatch) — brings the
// safety car back out and puts the race under caution (RaceStart.Phase.Caution):
//
//   * the safety car reappears on the track just ahead of the race leader and runs cruise pace (SafetyCar.DeployForCaution);
//   * every AI car goes back on the formation brain, single file behind it (FormationController), and the player is
//     held behind the car in front (PaceLapAssist);
//   * pit road stays open — a damaged car comes in for repairs (PitStopController.cautionRepairDamage, PlayerPitService);
//   * once the safety car has run at least minCautionLaps laps and the track has been clear for clearSeconds, it
//     closes the field up and pits, and the green waves as the leader reaches the line (FormationDirector) — the
//     same restart as the end of the pace lap.
//
// Road courses keep their local yellows only. Single player only: the multiplayer phase replication knows nothing
// of a caution. Added at runtime by FormationDirector, so it only ever runs in the player's own race.
public class FullCourseCaution : MonoBehaviour
{
    [Tooltip("Run full-course cautions on ovals. Off = local yellows only, as on a road course.")]
    public bool cautionsOnOvals = true;
    [Tooltip("Least number of laps run behind the safety car before the green, so every car gets the chance to pit.")]
    public float minCautionLaps = 2f;
    [Tooltip("A car still stopped on the track holds the caution out past minCautionLaps — but never past this many laps.")]
    public float maxCautionLaps = 5f;
    [Tooltip("How long (s) a car must have been flagged as stopped on the track (CautionWatch) before the caution comes out. A spin the driver gets going again from straight away is a local yellow, not a caution.")]
    public float declareAfterSeconds = 2f;
    [Tooltip("No caution this soon (s) after a green — the start, or the restart after the last caution.")]
    public float minSecondsAfterGreen = 5f;
    [Tooltip("The track must have been clear of stopped cars this long (s) before the safety car may come in.")]
    public float clearSeconds = 3f;
    [Tooltip("The safety car comes out this far (m) ahead of the race leader.")]
    public float pickupGapM = 80f;
    [Tooltip("Road (m) the safety car must have to itself, behind and ahead, where it comes out. Pushed on up the road until it has it.")]
    public float clearRoadBehindM = 12f;
    public float clearRoadAheadM = 25f;

    // Seconds into the current caution; -1 when there is none.
    public float CautionSeconds { get; private set; } = -1f;
    // Laps the safety car has run under this caution.
    public float LapsRun { get; private set; }

    FormationDirector _director;
    float _clearFor = -1f;
    float _cautionMsgTimer;
    int _oval = -1;   // -1 unresolved, 0 no, 1 yes

    // How long each car has been flagged as stopped on the track (Time.time it was first seen).
    readonly Dictionary<Transform, float> _stoppedSince = new();
    // Cars that were still stopped when the last caution went green: they have had their caution, and bring out
    // no more until they have got going again.
    readonly HashSet<Transform> _spent = new();
    readonly List<Transform> _scratch = new();
    readonly List<Transform> _gone = new();

    void Awake() => _director = GetComponent<FormationDirector>();

    void OnEnable() => RaceStart.PhaseChanged += OnPhaseChanged;
    void OnDisable() => RaceStart.PhaseChanged -= OnPhaseChanged;

    void OnPhaseChanged(RaceStart.Phase phase)
    {
        if (phase == RaceStart.Phase.Caution) return;
        // Green again (or a new race): whatever is still stopped has had its caution.
        if (CautionSeconds >= 0f)
        {
            _spent.Clear();
            foreach (var kv in _stoppedSince) if (kv.Key != null) _spent.Add(kv.Key);
        }
        CautionSeconds = -1f;
        LapsRun = 0f;
        _clearFor = -1f;
    }

    bool OnOval()
    {
        if (_oval >= 0) return _oval == 1;
        var tb = _director != null ? _director.track : null;
        if (tb == null || tb.track == null) return false;
        string id = tb.track.name.Replace("(Clone)", "").Trim();
        bool oval = Draftmaster.Tracks.TrackDimensions.TryGet(id, out var row) &&
                    (row.kind == Draftmaster.Tracks.TrackKind.ShortTrack ||
                     row.kind == Draftmaster.Tracks.TrackKind.Speedway ||
                     row.kind == Draftmaster.Tracks.TrackKind.Superspeedway);
        _oval = oval ? 1 : 0;
        return oval;
    }

    void FixedUpdate()
    {
        if (_director == null || !_director.isActiveAndEnabled || GameSession.IsMultiplayer) return;
        float now = Time.time;
        float longest = TrackStopped(now);

        if (RaceStart.IsCaution)
        {
            StepCaution(_stoppedSince.Count > 0, Time.fixedDeltaTime);
            return;
        }

        bool raceOver = RaceDirector.Progress01 >= 1f || RaceDirector.InResults;
        if (CautionRules.Declare(cautionsOnOvals && OnOval(), RaceStart.IsGreen, raceOver, RaceStart.SecondsSinceGreen,
                                 minSecondsAfterGreen, longest, declareAfterSeconds))
            Declare();
    }

    // Seconds the longest-stopped car on the track (not one that has already had its caution) has been stopped;
    // -1 for none. Keeps the per-car clocks up to date.
    float TrackStopped(float now)
    {
        var watch = CautionWatch.Instance;
        _scratch.Clear();
        if (watch != null)
        {
            var incidents = watch.Incidents;
            for (int i = 0; i < incidents.Count; i++)
                if (incidents[i].car != null) _scratch.Add(incidents[i].car);
        }

        // Forget cars no longer stopped — and let a spent car bring out a caution again once it has moved.
        _gone.Clear();
        foreach (var kv in _stoppedSince) if (kv.Key == null || !_scratch.Contains(kv.Key)) _gone.Add(kv.Key);
        for (int i = 0; i < _gone.Count; i++) { _stoppedSince.Remove(_gone[i]); _spent.Remove(_gone[i]); }
        _spent.RemoveWhere(t => t == null);

        float longest = -1f;
        for (int i = 0; i < _scratch.Count; i++)
        {
            var car = _scratch[i];
            if (!_stoppedSince.TryGetValue(car, out float since)) { since = now; _stoppedSince[car] = now; }
            if (_spent.Contains(car)) continue;
            longest = Mathf.Max(longest, now - since);
        }
        return longest;
    }

    void Declare()
    {
        var sc = _director.SafetyCarInstance;
        var scSpline = sc != null ? sc.GetComponent<SplineDriver>() : null;
        if (scSpline == null || scSpline.TrackLength <= 0f) return;   // no safety car to send out: local yellows only

        float lap = scSpline.TrackLength;
        float leader = LeaderTrackDistance();
        if (leader < 0f) return;
        float at = ClearSpot(Mathf.Repeat(leader + pickupGapM, lap), lap, scSpline);

        sc.DeployForCaution(at, minCautionLaps);
        CautionSeconds = 0f;
        LapsRun = 0f;
        _clearFor = -1f;
        _cautionMsgTimer = 4f;

        // The player's own car, if the AI has it (Drive/Broadcast, crew chief): it has no formation brain while the
        // race is green, and a bare SplineDriver would run race pace into the back of the train.
        var player = _director.PlayerCar;
        if (player != null && !player.enabled)
        {
            var ps = player.GetComponent<SplineDriver>();
            if (ps != null && ps.isActiveAndEnabled && ps.GetComponent<FormationController>() == null)
                FormationController.TakeOver(ps, player.GridPosition);
        }

        RaceStart.Current = RaceStart.Phase.Caution;
        Debug.Log($"[Caution] full-course caution: safety car out at {at:0} m (leader {leader:0} m), " +
                  $"at least {minCautionLaps:0.#} laps");
    }

    void StepCaution(bool somethingStopped, float dt)
    {
        CautionSeconds += dt;
        var sc = _director.SafetyCarInstance;
        if (sc == null) return;
        var spline = sc.GetComponent<SplineDriver>();
        float lap = spline != null ? spline.TrackLength : 0f;
        LapsRun = lap > 0f ? sc.Travelled / lap : 0f;

        if (somethingStopped) _clearFor = -1f;
        else _clearFor = _clearFor < 0f ? 0f : _clearFor + dt;
        sc.HoldOut = CautionRules.HoldOut(_clearFor, clearSeconds, LapsRun, maxCautionLaps);
    }

    // Where the race leader is along the main spline (the scale every SplineDriver works in), skipping cars in the
    // pit lane and cars stopped on the track. -1 with no tracker / nobody running.
    float LeaderTrackDistance()
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null || rt.TrackLength <= 0f) return -1f;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null || _stoppedSince.ContainsKey(e.tf)) continue;
            bool inPits = e.spline != null && e.spline.enabled ? e.spline.IsOnPit
                        : rt.track != null && rt.track.IsOnPitSurface(e.tf.position);
            if (inPits) continue;
            return Mathf.Repeat(e.trackDistance, rt.TrackLength);
        }
        return -1f;
    }

    // `from`, or the first spot past it with the road clear behind and ahead, in 10 m steps up to a quarter lap on.
    float ClearSpot(float from, float lap, SplineDriver self)
    {
        for (float d = 0f; d <= lap * 0.25f; d += 10f)
        {
            float at = Mathf.Repeat(from + d, lap);
            if (RoadClear(at, lap, self)) return at;
        }
        return from;
    }

    bool RoadClear(float at, float lap, SplineDriver self)
    {
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || d.IsOnPit || !d.isActiveAndEnabled) continue;
            if (Mathf.Abs(d.TrackLength - lap) > 0.5f) continue;
            if (Blocks(PackAvoidance.SignedGap(d.CentreDistanceOnTrack, at, lap))) return false;
        }
        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.ObstacleTrack != self.track) continue;
            if (Blocks(PackAvoidance.SignedGap(p.TrackDistance, at, lap))) return false;
        }
        return true;

        bool Blocks(float gap) => gap > -clearRoadBehindM && gap < clearRoadAheadM;
    }

    void Update()
    {
        if (_cautionMsgTimer > 0f) _cautionMsgTimer -= Time.deltaTime;
    }

    // The caution banner when it comes out, then a slim plate for as long as it lasts.
    void OnGUI()
    {
        if (!RaceStart.IsCaution) return;
        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        var yellow = new Color(1f, 0.85f, 0.1f, 1f);

        if (_cautionMsgTimer > 0f)
        {
            float alpha = Mathf.Clamp01(_cautionMsgTimer);
            float h = PixelGUI.Px(34f);
            float y = Mathf.Round(Screen.height * 0.18f);
            float kerb = PixelGUI.Px(4f);
            var prevGui = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            PixelGUI.Fill(new Rect(0f, y, Screen.width, h), PixelGUI.PlateDeep);
            PixelGUI.Kerb(new Rect(0f, y - kerb, Screen.width, kerb));
            PixelGUI.Kerb(new Rect(0f, y + h, Screen.width, kerb));
            style.normal.textColor = new Color(yellow.r, yellow.g, yellow.b, alpha);
            GUI.Label(new Rect(0f, y, Screen.width, h), "Caution · Safety Car", style);
            GUI.color = prevGui;
        }
        else
        {
            var sc = _director != null ? _director.SafetyCarInstance : null;
            string text = sc != null && (sc.ClosingUp || sc.Pitted) ? "CAUTION · GREEN AT THE LINE"
                        : sc != null && sc.HoldOut && LapsRun >= minCautionLaps ? "CAUTION · TRACK NOT CLEAR"
                        : "Caution · Pit Road Open";
            float h = PixelGUI.Px(22f);
            float y = Mathf.Round(Screen.height * 0.72f);   // where the pace lap's throttle prompt sits
            PixelGUI.Fill(new Rect(0f, y, Screen.width, h), PixelGUI.PlateDeep);
            style.normal.textColor = yellow;
            GUI.Label(new Rect(0f, y, Screen.width, h), text, style);
        }
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
    }
}
