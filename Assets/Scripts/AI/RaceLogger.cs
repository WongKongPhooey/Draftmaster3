using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Telemetry for the whole field, the player's car included, so a session can be read back afterwards: where the AI
// loses time to the player and to each other, and what held each car down there - its own speed plan, a follow or
// avoidance cap, the grip governor, the throttle, the tow or the dirty air.
//
// Editor only (a shipped game has no use for the files), self-installing like PlayerLapRecorder. Writes, per session:
//   <persistentDataPath>/RaceLogs/<trackId>_<yyyyMMdd_HHmmss>.csv       every car, SampleHz times a second
//   <persistentDataPath>/RaceLogs/<trackId>_<yyyyMMdd_HHmmss>_laps.csv  one row per completed lap per car
// A file is opened when cars first appear on track and closed when they all go (or play stops).
[DefaultExecutionOrder(110)]   // after the cars' own physics step, so a sample is the state it produced
public class RaceLogger : MonoBehaviour
{
    const float SampleHz = 5f;
    const float FlushSeconds = 3f;

    class Car
    {
        public string name;
        public PlayerVehicleController pvc;
        public SplineDriver spline;          // null for the human's car if it has no brain
        public SplineInputDriver input;
        public AIRacingBehaviour racing;
        public float lastD = -1f, lapStart = -1f;
        public int lap = -1;
        public double mphSum; public int mphN;
        public float minMph = float.MaxValue;
    }

    static RaceLogger _instance;
    readonly List<Car> _cars = new List<Car>();
    StreamWriter _samples, _laps;
    TrackBuilder _track;
    float _trackLength, _nextSample, _nextScan, _nextFlush;
    string _base;

    public static string Folder => Path.Combine(Application.persistentDataPath, "RaceLogs");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Application.isEditor || _instance != null) return;
        var go = new GameObject("RaceLogger");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RaceLogger>();
    }

    void FixedUpdate()
    {
        float now = Time.time;
        if (now >= _nextScan) { _nextScan = now + 1f; Scan(); }
        if (_cars.Count == 0 || _track == null || _trackLength <= 0f) return;
        if (_samples == null) Open();
        if (_samples == null) return;

        bool sample = now >= _nextSample;
        if (sample) _nextSample = now + 1f / SampleHz;

        var ci = CultureInfo.InvariantCulture;
        for (int i = _cars.Count - 1; i >= 0; i--)
        {
            var c = _cars[i];
            if (c.pvc == null) { _cars.RemoveAt(i); continue; }
            if (!c.pvc.isActiveAndEnabled) continue;
            bool human = !c.pvc.externalInput;

            Vector3 pos = c.pvc.transform.position;
            float d, lat;
            if (!human && c.spline != null) { d = c.spline.DistanceOnTrack; lat = c.spline.LateralOnTrack; }
            else
            {
                d = _track.NearestCenterlineDistance(pos);
                var s = _track.SampleAt(d);
                Vector2 local = _track.transform.InverseTransformPoint(pos);
                lat = Vector2.Dot(local - s.position, new Vector2(s.tangent.y, -s.tangent.x));
            }
            float mph = c.pvc.SpeedMph;
            bool onPit = c.spline != null ? c.spline.IsOnPit : _track.IsOnPitSurface(pos);

            // Laps, by the line crossing (a big backward jump in distance).
            if (c.lastD >= 0f && d < c.lastD - _trackLength * 0.5f)
            {
                if (c.lap >= 0 && c.lapStart >= 0f)
                    _laps.WriteLine(string.Format(ci, "{0:0.00},{1},{2},{3},{4:0.000},{5:0.0},{6:0.0},{7:0.000},{8:0.000},{9}",
                        now, c.name, human ? 1 : 0, c.lap, now - c.lapStart, c.mphN > 0 ? c.mphSum / c.mphN : 0.0,
                        c.minMph < float.MaxValue ? c.minMph : 0f,
                        c.spline != null ? c.spline.paceMultiplier : 0f, c.spline != null ? c.spline.cornerCommitment : 0f,
                        RaceStart.Current));
                c.lap++;
                c.lapStart = now;
                c.mphSum = 0; c.mphN = 0; c.minMph = float.MaxValue;
            }
            c.lastD = d;
            c.mphSum += mph; c.mphN++;
            c.minMph = Mathf.Min(c.minMph, mph);

            if (!sample) continue;
            var sp = c.spline;
            var inp = c.input;
            float plan = sp != null && !human ? sp.RacePaceMphAt(d) : 0f;
            float cap = sp != null && !human ? Mathf.Min(999f, sp.aiMaxSpeedMph) : 0f;
            _samples.WriteLine(string.Format(ci,
                "{0:0.00},{1},{2},{3},{4},{5:0.0},{6:0.00},{7:0.0},{8:0.00},{9:0.00},{10:0.0},{11:0.0},{12:0.0},{13:0.0},{14:0.0},{15:0.0},{16},{17:0.0},{18:0.0},{19},{20:0.00},{21:0.000},{22:0.000},{23:0.000},{24}",
                now, RaceStart.Current, c.name, human ? 1 : 0, c.lap, d, lat, mph,
                human ? c.pvc.ThrottleInput : inp != null ? inp.LastThrottle : 0f,
                human ? c.pvc.BrakeInput : inp != null ? inp.LastBrake : 0f,
                plan,
                sp != null && !human ? sp.DesiredMph : 0f,
                cap,
                inp != null && !human ? inp.LastProfileMps * 2.237f : 0f,
                inp != null && !human ? Mathf.Min(999f, inp.LastGripCapMps * 2.237f) : 0f,
                inp != null && !human ? inp.LastCommandedMps * 2.237f : 0f,
                sp != null ? sp.ovalLane.ToString() : "",
                sp != null && !human ? Mathf.Min(999f, sp.TrainGap) : 0f,
                sp != null && !human ? sp.TrainWantedGap : 0f,
                sp != null && !human && sp.TrainEmergency ? 1 : 0,
                c.pvc.TowFactor, c.pvc.DirtyAirGrip,
                sp != null ? sp.paceMultiplier : 0f,
                sp != null ? sp.cornerCommitment : 0f,
                onPit ? 1 : 0));
        }

        if (now >= _nextFlush)
        {
            _nextFlush = now + FlushSeconds;
            _samples.Flush();
            _laps.Flush();
        }
    }

    void Scan()
    {
        foreach (var pvc in FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None))
        {
            if (pvc == null || pvc.track == null) continue;
            bool known = false;
            foreach (var c in _cars) if (c.pvc == pvc) { known = true; break; }
            if (known) continue;
            _cars.Add(new Car
            {
                name = pvc.externalInput ? pvc.gameObject.name : "PLAYER_" + pvc.gameObject.name,
                pvc = pvc,
                spline = pvc.GetComponent<SplineDriver>(),
                input = pvc.GetComponent<SplineInputDriver>(),
                racing = pvc.GetComponent<AIRacingBehaviour>(),
            });
            if (_track == null || _track != pvc.track)
            {
                _track = pvc.track;
                var samples = _track.SampleCenterline();
                _trackLength = samples.Count > 0 ? samples[samples.Count - 1].distance : 0f;
            }
        }
        _cars.RemoveAll(c => c.pvc == null);
        if (_cars.Count == 0) Close();
    }

    void Open()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var pkg = TrackPackage.Active;
            string trackId = pkg != null && !string.IsNullOrEmpty(pkg.trackId) ? pkg.trackId
                           : _track.track != null ? _track.track.name : "Unknown";
            _base = Path.Combine(Folder, $"{trackId}_{DateTime.Now:yyyyMMdd_HHmmss}");
            _samples = new StreamWriter(_base + ".csv", false, new UTF8Encoding(false));
            _samples.WriteLine($"# track={trackId} trackLength={_trackLength.ToString("0.00", CultureInfo.InvariantCulture)} " +
                               $"aiGrip={TrackConditions.AiGripMultiplier:0.###} aiPace={TrackConditions.AiPaceMultiplier:0.###} " +
                               $"aiPower={TrackConditions.AiPowerScale:0.###} dirtyAirLoss={TrackConditions.DirtyAirLoss:0.###} " +
                               $"optimisedLine={TrackConditions.AiOptimisedOvalLine}");
            _samples.WriteLine("t,phase,car,human,lap,d,lat,mph,throttle,brake,planMph,desiredMph,capMph,brainMph,gripCapMph,commandMph,lane,trainGap,trainWanted,trainEmergency,tow,dirtyAirGrip,pace,commitment,onPit");
            _laps = new StreamWriter(_base + "_laps.csv", false, new UTF8Encoding(false));
            _laps.WriteLine("t,car,human,lap,lapSeconds,avgMph,minMph,pace,commitment,phase");
            Debug.Log($"RaceLogger: logging the field to {_base}.csv");
        }
        catch (Exception e)
        {
            Debug.LogWarning("RaceLogger: couldn't open the log: " + e.Message);
            Close();
            enabled = false;
        }
    }

    void Close()
    {
        if (_samples != null) { try { _samples.Dispose(); } catch { } _samples = null; }
        if (_laps != null) { try { _laps.Dispose(); } catch { } _laps = null; }
        if (_base != null) Debug.Log($"RaceLogger: closed {_base}.csv");
        _base = null;
        _track = null;
        _trackLength = 0f;
    }

    void OnDestroy() => Close();
    void OnApplicationQuit() => Close();
}
