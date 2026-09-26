using System.Collections.Generic;
using UnityEngine;

// How strong the AI are at each track, as measured by Draftmaster > AI > Calibrate AI Pace.
//
// The calibrator drives an AI car round the track in the headless lap sim, raising its grip and power together
// until its lap matches the lap time the player gives it, and writes the result here. When a track loads,
// TrackPackage applies its entry to TrackConditions — the two AI-only knobs every AI car reads — so the whole
// field runs at that strength. A track with no entry keeps TrackConditions' defaults.
//
// Lives in Resources so a build can read it; the calibrator is the only thing that writes it.
[CreateAssetMenu(menuName = "Draftmaster/AI Pace Calibration", fileName = "AIPaceCalibration")]
public class AIPaceCalibration : ScriptableObject
{
    public const string ResourcePath = "AI/AIPaceCalibration";

    [System.Serializable]
    public class Entry
    {
        public string trackId;
        [Tooltip("Written to TrackConditions.AiGripMultiplier when this track loads.")]
        public float aiGrip = 1.2f;
        [Tooltip("Written to TrackConditions.AiPaceMultiplier when this track loads (engine power and target pace).")]
        public float aiPace = 1.2f;

        [Header("How it was measured")]
        [Tooltip("The lap time the AI was tuned to (seconds).")]
        public float targetLapSeconds;
        [Tooltip("What the simulated AI car lapped at these settings (seconds).")]
        public float simLapSeconds;
        [Tooltip("The driver rating the sim car was driven at. 1.04 = the best driver in the field, so the fastest " +
                 "AI match the target and the rest of the field spreads out behind them.")]
        public float referenceDriverPace = 1.04f;
        public string calibratedOn;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Find(string trackId)
    {
        if (string.IsNullOrEmpty(trackId)) return null;
        foreach (var e in entries)
            if (e != null && string.Equals(e.trackId, trackId, System.StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }

    static AIPaceCalibration _loaded;
    static bool _looked;

    public static AIPaceCalibration Load()
    {
        if (!_looked || _loaded == null)
        {
            _looked = true;
            _loaded = Resources.Load<AIPaceCalibration>(ResourcePath);
        }
        return _loaded;
    }

    // Set the AI strength for a track that has just loaded. Always resets to the defaults first, so moving from a
    // calibrated track to an uncalibrated one doesn't carry the last track's numbers along.
    public static void ApplyFor(string trackId)
    {
        TrackConditions.AiGripMultiplier = TrackConditions.DefaultAiGrip;
        TrackConditions.AiPaceMultiplier = TrackConditions.DefaultAiPace;

        var entry = Load() != null ? _loaded.Find(trackId) : null;
        if (entry == null) return;
        TrackConditions.AiGripMultiplier = entry.aiGrip;
        TrackConditions.AiPaceMultiplier = entry.aiPace;
        Debug.Log($"AIPaceCalibration: {trackId} AI grip x{entry.aiGrip:0.###}, pace x{entry.aiPace:0.###} " +
                  $"(tuned to {entry.targetLapSeconds:0.00}s on {entry.calibratedOn}).");
    }
}
