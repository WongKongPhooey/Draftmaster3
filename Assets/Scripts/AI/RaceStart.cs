using System;
using UnityEngine;

// Global race-procedure phase gate. One source of truth that the formation-lap systems read so
// they don't need cross-references to each other.
//
//   PreGrid   — cars parked (AI in pit boxes, safety car at pit exit); nobody moves.
//   Formation — safety car laps at cruise pace; AI file out and form a weaving train behind it.
//   Green     — race is on; AI race normally, player released.
//   Caution   — full-course yellow on an oval (FullCourseCaution): the safety car is back out, the field runs
//               single file behind it on the formation brain, pit road is open, and the green comes again at the
//               line once the safety car has pitted. Appended last so the replicated byte values of the first
//               three do not move.
//
// Defaults to Green so any scene WITHOUT a FormationDirector behaves exactly as before
// (AI race the instant they spawn).
public static class RaceStart
{
    public enum Phase { PreGrid, Formation, Green, Caution }

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

    // Wave the green now, even if the phase already reads Green — a race started from the grid in the middle
    // of a weekend (the one watched from a grandstand) still wants its rolling-start launch window.
    public static void GreenFlagNow()
    {
        if (_current != Phase.Green) Current = Phase.Green;
        else GreenTime = Time.time;
    }

    public static bool IsGreen => _current == Phase.Green;
    public static bool IsFormation => _current == Phase.Formation;
    public static bool IsPreGrid => _current == Phase.PreGrid;
    public static bool IsCaution => _current == Phase.Caution;
    // The race is under way: green, or under a full-course caution. Pit road is open in both, and damage counts.
    public static bool IsRunning => _current == Phase.Green || _current == Phase.Caution;
    // A safety car leads the field: the formation lap, or a caution.
    public static bool UnderSafetyCar => _current == Phase.Formation || _current == Phase.Caution;

    // A fresh scene load shares this static with the previous one. Reset so a second race doesn't
    // inherit Green from the last. FormationDirector.Awake sets the real starting phase.
    public static void ResetToDefault() { GreenTime = -1f; Current = Phase.Green; }
}
