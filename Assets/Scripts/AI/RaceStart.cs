using System;
using UnityEngine;

// Global race-procedure phase gate. One source of truth that the formation-lap systems read so
// they don't need cross-references to each other.
//
//   PreGrid   — cars parked (AI in pit boxes, safety car at pit exit); nobody moves.
//   Formation — safety car laps at cruise pace; AI file out and form a weaving train behind it.
//   Green     — race is on; AI race normally, player released.
//
// Defaults to Green so any scene WITHOUT a FormationDirector behaves exactly as before
// (AI race the instant they spawn).
public static class RaceStart
{
    public enum Phase { PreGrid, Formation, Green }

    static Phase _current = Phase.Green;

    public static Phase Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            if (value == Phase.Green) GreenTime = Time.time;
            PhaseChanged?.Invoke(value);
        }
    }

    public static event Action<Phase> PhaseChanged;

    // Time.time at which the race last went green (-1 before then). Lets the AI run a brief rolling-start launch
    // window where follow-distance caps relax so the whole field accelerates together instead of accordioning out.
    public static float GreenTime { get; private set; } = -1f;
    public static float SecondsSinceGreen => (_current == Phase.Green && GreenTime >= 0f) ? Time.time - GreenTime : -1f;

    public static bool IsGreen => _current == Phase.Green;
    public static bool IsFormation => _current == Phase.Formation;
    public static bool IsPreGrid => _current == Phase.PreGrid;

    // A fresh scene load shares this static with the previous one. Reset so a second race doesn't
    // inherit Green from the last. FormationDirector.Awake sets the real starting phase.
    public static void ResetToDefault() { GreenTime = -1f; Current = Phase.Green; }
}
