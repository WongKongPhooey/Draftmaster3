using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Flight recorder for the AI. Every AI car keeps the last few seconds of what its controller saw and did;
// the moment one spins, leaves the surface, hits a wall or hits another car hard, those seconds (plus a
// second of aftermath) are written out. So "the AI keep crashing" turns into a list of incidents with the
// track position, the speed the car was asked for, the grip ceiling, the slip angle and the inputs on the
// way in — which is the difference between tuning the controller and guessing at it.
//
// Development only: installs itself in the editor and in development builds, costs a few hundred bytes per
// car, and does nothing in a release build.
//
// Output: <persistentDataPath>/ai_incidents/<session>.csv (one row per sample, grouped by incident) and one
// "[AIIncident]" line per incident in the log. A hotspot table — incidents by track position — is logged
// when the session ends.
[DefaultExecutionOrder(100)]   // after the cars' own physics step, so a sample is the state it produced
public class AIIncidentRecorder : MonoBehaviour
{
    const float SampleInterval = 0.1f;   // 10 Hz
    const int HistorySamples = 40;        // 4 s before the incident
    const int AftermathSamples = 10;      // 1 s after it
    const float Cooldown = 4f;            // one incident per car per this long — a crash is one event, not ten
    const float WallMinClosing = 3f;      // m/s
    const float CarMinSeverity = 0.15f;
    const float HotspotBucketMetres = 100f;

    struct Sample
    {
        public float time, distance, speed, profile, gripCap, commanded, radius;
        public float noseErr, slip, steer, throttle, brake, lateralAbs, trackErr, damage;
        public bool recovering, onSurface;
    }

    class Car
    {
        public SplineInputDriver input;
        public SplineDriver spline;
        public PlayerVehicleController pvc;
        public VehicleCollision collision;
        public VehicleDamage damage;
        public readonly Sample[] ring = new Sample[HistorySamples + AftermathSamples];
        public int head, count;
        public bool wasRecovering, wasOnSurface = true;
        public float lastIncident = -999f;
        public int pendingId = -1, pendingLeft;
        public string pendingType;
        public float pendingDistance;
        public System.Action<float> onBarrier;
        public System.Action<VehicleCollision.ContactEvent> onContact;
    }

    static AIIncidentRecorder _instance;
    readonly List<Car> _cars = new List<Car>();
    readonly Dictionary<int, int> _hotspots = new Dictionary<int, int>();
    readonly Dictionary<string, int> _byType = new Dictionary<string, int>();
    float _nextSample, _nextScan;
    int _incidents;
    string _path;
    StringBuilder _pending = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (_instance != null) return;
        var go = new GameObject("AIIncidentRecorder");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AIIncidentRecorder>();
    }

    void FixedUpdate()
    {
        float now = Time.time;
        if (now >= _nextScan) { _nextScan = now + 2f; Scan(); }
        if (now < _nextSample) return;
        _nextSample = now + SampleInterval;

        for (int i = _cars.Count - 1; i >= 0; i--)
        {
            var car = _cars[i];
            if (car.input == null || car.spline == null || car.pvc == null) { Forget(i); continue; }
            if (!car.input.isActiveAndEnabled || car.spline.IsOnPit || car.spline.IsHeldStill)
            {
                car.count = 0;            // a parked or pitting car starts a fresh history when it comes out
                car.wasRecovering = false;
                car.wasOnSurface = true;
                continue;
            }

            var s = Measure(car, now);
            Push(car, s);

            if (car.pendingId >= 0)
            {
                if (--car.pendingLeft <= 0) Flush(car);
                continue;
            }

            if (s.recovering && !car.wasRecovering) Trigger(car, "spin", now);
            else if (!s.onSurface && car.wasOnSurface) Trigger(car, "off", now);
            car.wasRecovering = s.recovering;
            car.wasOnSurface = s.onSurface;
        }
    }

    Sample Measure(Car car, float now)
    {
        var sp = car.spline;
        var track = sp.track;
        Vector3 pos = car.pvc.transform.position;
        float lateralAbs = 0f;
        bool onSurface = track == null || track.IsOnSurface(pos, out lateralAbs);
        float trackErr = 0f;
        if (track != null)
        {
            Vector3 cmd = track.transform.TransformPoint(new Vector3(sp.CommandedLocalPos.x, sp.CommandedLocalPos.y, 0f));
            trackErr = Vector2.Distance(cmd, pos);
        }
        float radius = sp.CurvatureRadiusAhead(Mathf.Max(8f, car.pvc.SpeedMps * 0.7f));

        return new Sample
        {
            time = now,
            distance = sp.DistanceOnTrack,
            speed = car.pvc.SpeedMps,
            profile = car.input.LastProfileMps,
            gripCap = car.input.LastGripCapMps,
            commanded = car.input.LastCommandedMps,
            radius = radius,
            noseErr = car.input.LastNoseErrorDeg,
            slip = car.input.LastSlipDeg,
            steer = car.input.LastSteer,
            throttle = car.input.LastThrottle,
            brake = car.input.LastBrake,
            recovering = car.input.IsRecovering,
            onSurface = onSurface,
            lateralAbs = lateralAbs,
            trackErr = trackErr,
            damage = car.damage != null ? car.damage.DamageLevel : 0f,
        };
    }

    void Trigger(Car car, string type, float now)
    {
        if (car.pendingId >= 0 || now - car.lastIncident < Cooldown || car.count == 0) return;
        car.lastIncident = now;
        car.pendingId = ++_incidents;
        car.pendingType = type;
        car.pendingLeft = AftermathSamples;
        car.pendingDistance = car.spline.DistanceOnTrack;

        // One line now, while it's happening: what the car was doing in the sample before it went.
        var s = Latest(car);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[AIIncident] #{0} {1} {2} at {3:0}m: speed {4:0.0} m/s, asked {5:0.0} (profile {6:0.0}, grip cap {7}), " +
            "slip {8:0.0}°, nose err {9:0.0}°, off line {10:0.0} m, R ahead {11}, dmg {12:0.00}",
            car.pendingId, car.pvc.name, type, car.pendingDistance, s.speed, s.commanded, s.profile,
            s.gripCap >= float.MaxValue ? "none" : s.gripCap.ToString("0.0", CultureInfo.InvariantCulture),
            s.slip, s.noseErr, s.trackErr,
            s.radius >= float.MaxValue ? "straight" : s.radius.ToString("0", CultureInfo.InvariantCulture), s.damage));

        _byType.TryGetValue(type, out int n);
        _byType[type] = n + 1;
        int bucket = Mathf.FloorToInt(car.pendingDistance / HotspotBucketMetres);
        _hotspots.TryGetValue(bucket, out int h);
        _hotspots[bucket] = h + 1;
    }

    void Flush(Car car)
    {
        if (_path == null)
        {
            string dir = Path.Combine(Application.persistentDataPath, "ai_incidents");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            File.WriteAllText(_path, "incident,car,type,t,distance,speed,profile,gripCap,commanded,radiusAhead," +
                                     "noseErr,slip,steer,throttle,brake,recovering,onSurface,lateralAbs,trackErr,damage\n");
            Debug.Log($"[AIIncident] recording to {_path}");
        }

        _pending.Clear();
        float t0 = car.lastIncident;
        int start = (car.head - car.count + car.ring.Length) % car.ring.Length;
        for (int k = 0; k < car.count; k++)
        {
            var s = car.ring[(start + k) % car.ring.Length];
            _pending.AppendFormat(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3:0.00},{4:0.0},{5:0.00},{6:0.00},{7},{8:0.00},{9},{10:0.0},{11:0.0},{12:0.00},{13:0.00},{14:0.00},{15},{16},{17:0.00},{18:0.00},{19:0.00}\n",
                car.pendingId, car.pvc.name, car.pendingType, s.time - t0, s.distance, s.speed, s.profile,
                s.gripCap >= float.MaxValue ? "" : s.gripCap.ToString("0.00", CultureInfo.InvariantCulture),
                s.commanded, s.radius >= float.MaxValue ? "" : s.radius.ToString("0.0", CultureInfo.InvariantCulture),
                s.noseErr, s.slip, s.steer, s.throttle, s.brake, s.recovering ? 1 : 0, s.onSurface ? 1 : 0,
                s.lateralAbs, s.trackErr, s.damage);
        }
        try { File.AppendAllText(_path, _pending.ToString()); }
        catch (IOException e) { Debug.LogWarning($"[AIIncident] could not write {_path}: {e.Message}"); }

        car.pendingId = -1;
        car.count = 0;   // the next incident gets its own history, not this one's
    }

    static void Push(Car car, in Sample s)
    {
        car.ring[car.head] = s;
        car.head = (car.head + 1) % car.ring.Length;
        car.count = Mathf.Min(car.count + 1, car.ring.Length);
    }

    static Sample Latest(Car car) => car.ring[(car.head - 1 + car.ring.Length) % car.ring.Length];

    // Pick up cars as they spawn (grids build over several frames, practice cycles them out of the boxes).
    void Scan()
    {
        var found = FindObjectsByType<SplineInputDriver>(FindObjectsSortMode.None);
        foreach (var input in found)
        {
            bool known = false;
            foreach (var c in _cars) if (c.input == input) { known = true; break; }
            if (known) continue;

            var car = new Car
            {
                input = input,
                spline = input.GetComponent<SplineDriver>(),
                pvc = input.GetComponent<PlayerVehicleController>(),
                collision = input.GetComponent<VehicleCollision>(),
                damage = input.GetComponentInChildren<VehicleDamage>(),
            };
            if (car.collision != null)
            {
                car.onBarrier = closing => { if (closing >= WallMinClosing && car.input != null && car.input.isActiveAndEnabled) Trigger(car, "wall", Time.time); };
                car.onContact = e => { if (e.otherIsCar && e.severity >= CarMinSeverity && car.input != null && car.input.isActiveAndEnabled) Trigger(car, "car", Time.time); };
                car.collision.BarrierHit += car.onBarrier;
                car.collision.Contacted += car.onContact;
            }
            _cars.Add(car);
        }
    }

    void Forget(int index)
    {
        var car = _cars[index];
        if (car.collision != null)
        {
            car.collision.BarrierHit -= car.onBarrier;
            car.collision.Contacted -= car.onContact;
        }
        _cars.RemoveAt(index);
    }

    void OnApplicationQuit() => Summarise();

    void OnDestroy()
    {
        Summarise();
        if (_instance == this) _instance = null;
    }

    bool _summarised;
    void Summarise()
    {
        if (_summarised || _incidents == 0) return;
        _summarised = true;
        foreach (var car in _cars) if (car.pendingId >= 0) Flush(car);

        int trained = 0, total = 0;
        foreach (var car in _cars) if (car.spline != null) { total++; if (car.spline.TrainedLineInUse) trained++; }
        var sb = new StringBuilder($"[AIIncident] session summary: {_incidents} incidents, trained racing line in use on {trained}/{total} cars (");
        foreach (var kv in _byType) sb.Append($"{kv.Key} {kv.Value}  ");
        sb.Append(")\n  hotspots by track position:");
        var buckets = new List<KeyValuePair<int, int>>(_hotspots);
        buckets.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (int i = 0; i < buckets.Count && i < 12; i++)
            sb.Append($"\n    {buckets[i].Key * HotspotBucketMetres:0}-{(buckets[i].Key + 1) * HotspotBucketMetres:0} m: {buckets[i].Value}");
        if (_path != null) sb.Append($"\n  detail: {_path}");
        Debug.Log(sb.ToString());
    }
}
