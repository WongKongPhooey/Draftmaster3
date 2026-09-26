using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Records every lap the player drives, so the best one can teach the AI (Draftmaster > AI > Use My Lap For AI).
//
// Where the car is on the road, how fast, and what the pedals and wheel are doing, every physics step, keyed to
// distance along the centreline. Each completed lap is written to
//   <persistentDataPath>/PlayerLaps/<trackId>/<time>_<date>.csv
// with its lap time and whether the lap timer counted it valid. Nothing to press: drive, and pick the lap later.
//
// Editor only — a shipped game has no use for the files, and writing them from a player's machine would be a
// surprise. Self-installing, like the other dev tools.
public class PlayerLapRecorder : MonoBehaviour
{
    static PlayerLapRecorder _instance;

    PlayerVehicleController _car;
    TrackBuilder _track;
    float _trackLength;
    float _findTimer;

    bool _recording;          // a lap is being recorded (the first line crossing starts one)
    float _lapClock;
    float _lastD = -1f;
    bool _touchedPit;
    readonly List<PlayerLapFile.Sample> _samples = new List<PlayerLapFile.Sample>();

    // A finished lap waits a moment for LapTimingManager to rule on it before it is written.
    PlayerLapFile _pending;
    float _pendingAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Application.isEditor || _instance != null) return;
        var go = new GameObject("PlayerLapRecorder");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlayerLapRecorder>();
    }

    void Update()
    {
        if (_pending != null && Time.unscaledTime - _pendingAt > 0.75f) WritePending();

        if (_car != null && _car.isActiveAndEnabled && !_car.externalInput) return;
        _findTimer -= Time.unscaledDeltaTime;
        if (_findTimer > 0f) return;
        _findTimer = 1f;
        FindCar();
    }

    void FindCar()
    {
        var prevCar = _car;
        _car = null;
        foreach (var pvc in FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None))
            if (pvc.isActiveAndEnabled && !pvc.externalInput && pvc.track != null) { _car = pvc; break; }
        if (_car != prevCar) ResetLap();
        if (_car == null) return;
        _track = _car.track;
        var samples = _track.SampleCenterline();
        _trackLength = samples.Count > 0 ? samples[samples.Count - 1].distance : 0f;
    }

    void ResetLap()
    {
        _recording = false;
        _lastD = -1f;
        _samples.Clear();
    }

    void FixedUpdate()
    {
        if (_car == null || !_car.isActiveAndEnabled || _car.externalInput || _track == null || _trackLength <= 0f)
        {
            if (_recording) ResetLap();
            return;
        }

        Vector3 pos = _car.transform.position;
        float d = _track.NearestCenterlineDistance(pos);

        // Crossing the line: a big backward jump in distance. It ends the lap being recorded and starts the next.
        if (_lastD >= 0f && d < _lastD - _trackLength * 0.5f)
        {
            if (_recording && _samples.Count > 50) FinishLap();
            _recording = true;
            _lapClock = 0f;
            _touchedPit = false;
            _samples.Clear();
        }
        _lastD = d;
        if (!_recording) return;

        _lapClock += Time.fixedDeltaTime;
        if (_track.IsOnPitSurface(pos)) _touchedPit = true;

        var s = _track.SampleAt(d);
        Vector2 local = _track.transform.InverseTransformPoint(pos);
        float lat = Vector2.Dot(local - s.position, new Vector2(s.tangent.y, -s.tangent.x));
        _samples.Add(new PlayerLapFile.Sample
        {
            t = _lapClock, d = d, lat = lat, mps = _car.SpeedMps,
            throttle = _car.ThrottleInput, brake = _car.BrakeInput, steer = _car.SteerInput,
        });
    }

    void FinishLap()
    {
        var pkg = TrackPackage.Active;
        string trackId = pkg != null && !string.IsNullOrEmpty(pkg.trackId)
            ? pkg.trackId
            : (_track.track != null ? _track.track.name : "Unknown");
        _pending = new PlayerLapFile
        {
            trackId = trackId,
            lapSeconds = _lapClock,
            trackLength = _trackLength,
            recordedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            pitLap = _touchedPit,
            samples = new List<PlayerLapFile.Sample>(_samples),
        };
        _pendingAt = Time.unscaledTime;
    }

    // The lap timer's verdict (track limits, walls) arrives on its own schedule, so ask it after the lap rather
    // than at the line. A lap it didn't time at all is kept but marked invalid.
    void WritePending()
    {
        var lap = _pending;
        _pending = null;
        lap.valid = false;
        var timing = LapTimingManager.Instance;
        if (timing != null)
            foreach (var row in timing.Rows)
            {
                if (!row.isPlayer) continue;
                lap.timerLapSeconds = row.lastLap;
                if (Mathf.Abs(row.lastLap - lap.lapSeconds) < 0.5f) lap.valid = row.lastValid;
                break;
            }
        if (lap.pitLap) lap.valid = false;

        try
        {
            string path = lap.Save();
            Debug.Log($"PlayerLapRecorder: {PlayerLapFile.FormatTime(lap.lapSeconds)} at {lap.trackId} " +
                      $"({(lap.valid ? "valid" : "invalid")}) -> {path}");
        }
        catch (Exception e) { Debug.LogWarning("PlayerLapRecorder: couldn't save the lap: " + e.Message); }
    }
}

// One recorded lap, and the CSV it lives in.
public class PlayerLapFile
{
    public struct Sample { public float t, d, lat, mps, throttle, brake, steer; }

    public string trackId;
    public float lapSeconds;
    public float timerLapSeconds = -1f;   // what LapTimingManager timed the same lap at (-1 = it didn't)
    public float trackLength;
    public bool valid;
    public bool pitLap;
    public string recordedUtc;
    public List<Sample> samples = new List<Sample>();
    public string path;

    public static string Folder(string trackId) =>
        Path.Combine(Application.persistentDataPath, "PlayerLaps", trackId);

    public static string FormatTime(float s) =>
        s >= 60f ? $"{(int)(s / 60f)}:{s % 60f:00.000}" : $"{s:0.000}s";

    public string Save()
    {
        string folder = Folder(trackId);
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        path = Path.Combine(folder, $"{lapSeconds:000.000}_{stamp}{(valid ? "" : "_invalid")}.csv");

        var sb = new StringBuilder(samples.Count * 48 + 256);
        var ci = CultureInfo.InvariantCulture;
        sb.AppendLine($"# track={trackId}");
        sb.AppendLine("# lapSeconds=" + lapSeconds.ToString("0.000", ci));
        sb.AppendLine("# timerLapSeconds=" + timerLapSeconds.ToString("0.000", ci));
        sb.AppendLine("# trackLength=" + trackLength.ToString("0.00", ci));
        sb.AppendLine($"# valid={valid}");
        sb.AppendLine($"# pitLap={pitLap}");
        sb.AppendLine($"# recordedUtc={recordedUtc}");
        sb.AppendLine("t,d,lat,mps,throttle,brake,steer");
        foreach (var s in samples)
            sb.Append(s.t.ToString("0.000", ci)).Append(',')
              .Append(s.d.ToString("0.00", ci)).Append(',')
              .Append(s.lat.ToString("0.000", ci)).Append(',')
              .Append(s.mps.ToString("0.00", ci)).Append(',')
              .Append(s.throttle.ToString("0.00", ci)).Append(',')
              .Append(s.brake.ToString("0.00", ci)).Append(',')
              .Append(s.steer.ToString("0.000", ci)).Append('\n');
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    public static PlayerLapFile Load(string path)
    {
        var lap = new PlayerLapFile { path = path };
        var ci = CultureInfo.InvariantCulture;
        foreach (var raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#"))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(1, eq - 1).Trim(), val = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "track": lap.trackId = val; break;
                    case "lapSeconds": float.TryParse(val, NumberStyles.Float, ci, out lap.lapSeconds); break;
                    case "trackLength": float.TryParse(val, NumberStyles.Float, ci, out lap.trackLength); break;
                    case "valid": bool.TryParse(val, out lap.valid); break;
                    case "pitLap": bool.TryParse(val, out lap.pitLap); break;
                    case "recordedUtc": lap.recordedUtc = val; break;
                }
                continue;
            }
            if (char.IsLetter(line[0])) continue;   // the column header
            var f = line.Split(',');
            if (f.Length < 7) continue;
            lap.samples.Add(new Sample
            {
                t = float.Parse(f[0], ci), d = float.Parse(f[1], ci), lat = float.Parse(f[2], ci),
                mps = float.Parse(f[3], ci), throttle = float.Parse(f[4], ci), brake = float.Parse(f[5], ci),
                steer = float.Parse(f[6], ci),
            });
        }
        return lap;
    }

    // Every lap saved for a track, fastest valid first, then the invalid ones.
    public static List<PlayerLapFile> List(string trackId)
    {
        var laps = new List<PlayerLapFile>();
        string folder = Folder(trackId);
        if (!Directory.Exists(folder)) return laps;
        foreach (var file in Directory.GetFiles(folder, "*.csv"))
        {
            try { laps.Add(LoadHeader(file)); } catch { /* a half-written file: skip it */ }
        }
        laps.Sort((a, b) => a.valid != b.valid ? (a.valid ? -1 : 1) : a.lapSeconds.CompareTo(b.lapSeconds));
        return laps;
    }

    // Just the header lines — listing a folder of laps shouldn't parse thousands of samples each.
    static PlayerLapFile LoadHeader(string path)
    {
        var lap = new PlayerLapFile { path = path };
        var ci = CultureInfo.InvariantCulture;
        foreach (var raw in File.ReadLines(path))
        {
            if (!raw.StartsWith("#")) break;
            int eq = raw.IndexOf('=');
            if (eq < 0) continue;
            string key = raw.Substring(1, eq - 1).Trim(), val = raw.Substring(eq + 1).Trim();
            if (key == "track") lap.trackId = val;
            else if (key == "lapSeconds") float.TryParse(val, NumberStyles.Float, ci, out lap.lapSeconds);
            else if (key == "trackLength") float.TryParse(val, NumberStyles.Float, ci, out lap.trackLength);
            else if (key == "valid") bool.TryParse(val, out lap.valid);
            else if (key == "recordedUtc") lap.recordedUtc = val;
        }
        return lap;
    }
}
