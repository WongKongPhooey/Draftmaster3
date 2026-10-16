using System.Diagnostics;
using System.Text;
using Draftmaster.Crowd;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Frame-time flight recorder for the phone. Every two seconds one "[Perf]" line goes to the log (logcat tag
// Unity), and any single frame over DipMs gets a line of its own straight away — each carrying what was going
// on when it happened, so a dip can be pinned on a pile-up, a full field or a busy paddock from the log alone:
//
//   adb logcat -v time -s Unity:V | grep "\[Perf\]"
//
// The counters are plain statics bumped by the systems being watched (VehicleCollision, VehicleDamage), so
// recording costs a few increments a frame. Off in the editor, where the Profiler does this job properly.
public class PerfLog : MonoBehaviour
{
    const float WindowSeconds = 2f;
    const float DipMs = 25f;              // a 60 fps frame is 16.7 ms; 25 is a visible hitch, not jitter
    const float SlowMs = 18f;             // a frame that missed 60
    const float DipLineGapSeconds = 0.5f; // a ten-frame stall is one story, not ten lines

    // Bumped from elsewhere.
    public static int CarsActive;
    public static int Impacts;            // car contacts above the damage threshold
    public static int Dents;              // presses that actually moved bodywork
    static long _damageTicks;             // Stopwatch ticks spent inside VehicleDamage.OnImpact

    public static void AddDamageTicks(long ticks) => _damageTicks += ticks;
    public static bool Enabled { get; private set; }

    int _frames, _slow, _fixedSteps, _fixedThisFrame, _maxFixedInFrame;
    float _sum, _worst, _windowStart, _lastDipLine = -10f;
    int _impactsAtDip, _dentsAtDip;
    readonly StringBuilder _sb = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Application.isEditor || !Application.isMobilePlatform) return;
        Enabled = true;
        var go = new GameObject("PerfLog");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfLog>();
        Debug.Log($"[Perf] recording: {SystemInfo.deviceModel}, {SystemInfo.graphicsDeviceType}, " +
                  $"target {Application.targetFrameRate} fps, fixed step {Time.fixedDeltaTime * 1000f:0.#} ms");
    }

    void FixedUpdate() { _fixedThisFrame++; }

    void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        _frames++;
        _sum += ms;
        if (ms > _worst) _worst = ms;
        if (ms > SlowMs) _slow++;
        _fixedSteps += _fixedThisFrame;
        if (_fixedThisFrame > _maxFixedInFrame) _maxFixedInFrame = _fixedThisFrame;

        if (ms > DipMs && Time.unscaledTime - _lastDipLine > DipLineGapSeconds)
        {
            _lastDipLine = Time.unscaledTime;
            Line("dip", $"{ms:0} ms frame, {_fixedThisFrame} physics steps, " +
                        $"+{Impacts - _impactsAtDip} impacts/+{Dents - _dentsAtDip} dents since last dip");
            _impactsAtDip = Impacts;
            _dentsAtDip = Dents;
        }
        _fixedThisFrame = 0;

        if (Time.unscaledTime - _windowStart < WindowSeconds) return;

        float avgMs = _sum / Mathf.Max(1, _frames);
        Line("2s", $"{1000f / Mathf.Max(0.01f, avgMs):0.0} fps avg, worst {_worst:0} ms, {_slow}/{_frames} slow, " +
                   $"physics {_fixedSteps} steps (max {_maxFixedInFrame}/frame), " +
                   $"impacts {Impacts}, dents {Dents}, damage {_damageTicks * 1000.0 / Stopwatch.Frequency:0.0} ms");

        _windowStart = Time.unscaledTime;
        _frames = _slow = _fixedSteps = _maxFixedInFrame = 0;
        _sum = _worst = 0f;
        Impacts = Dents = 0;
        _impactsAtDip = _dentsAtDip = 0;
        _damageTicks = 0;
    }

    void Line(string kind, string body)
    {
        _sb.Clear();
        _sb.Append("[Perf] ").Append(kind).Append(": ").Append(body)
           .Append(" | cars ").Append(CarsActive)
           .Append(", ").Append(WeekendVenueAnchor.OnFootPlayer() != null ? "on foot" : "driving")
           .Append(", npcs awake ").Append(AwakeNpcs()).Append('/').Append(CrowdActor.All.Count)
           .Append(", scene ").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        // No stack trace: on device every Debug.Log carries one, which quadruples the line count and costs
        // the frame it is measuring.
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", _sb.ToString());
    }

    static int AwakeNpcs()
    {
        int n = 0;
        var all = CrowdActor.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].Lod != CrowdLod.Frozen) n++;
        return n;
    }
}
