using System.Collections.Generic;
using UnityEngine;

// Who is racing out of each pit box.
//
// Everything built around a box — the pit box stand on the wall, the crew working the stop — has to know
// whose box it is standing in before it can paint itself in that car's colours, and none of them can be
// told at build time: the grid spawns over several frames and is re-parked into fitted boxes after that,
// so a box is built before its car exists. They all end up asking the same question every frame until it
// answers, which is a scene-wide search per asker per frame across a forty-box pit road.
//
// So the search happens once here and the answer is shared. A car's box IS its grid / qualifying slot;
// the human's box is whatever GridSpawner reserved (PitLane.PlayerBox) and wins its slot outright.
public static class PitBoxCars
{
    // Cars move between boxes only when the field is re-seeded, so a fresh scan four times a second keeps
    // up with anything that changes while still costing nothing when forty boxes all ask at once.
    const float RescanInterval = 0.25f;

    static readonly Dictionary<int, DriverLabel> _byBox = new();
    static float _scannedAt = float.NegativeInfinity;

    // The car assigned to a box, or null while nothing has claimed it yet.
    public static DriverLabel Label(int boxIndex)
    {
        if (boxIndex < 0) return null;
        Rescan();
        return _byBox.TryGetValue(boxIndex, out var label) && label != null ? label : null;
    }

    static void Rescan()
    {
        if (Time.time - _scannedAt < RescanInterval) return;
        _scannedAt = Time.time;
        _byBox.Clear();

        foreach (var driver in Object.FindObjectsByType<SplineDriver>(FindObjectsSortMode.None))
        {
            if (driver == null || driver.qualifyingPosition < 0) continue;
            var label = driver.GetComponent<DriverLabel>();
            if (label != null) _byBox[driver.qualifyingPosition] = label;
        }

        if (PitLane.PlayerBox >= 0)
        {
            var player = Object.FindFirstObjectByType<PlayerVehicleController>();
            var own = player != null ? player.GetComponent<DriverLabel>() : null;
            if (own != null) _byBox[PitLane.PlayerBox] = own;
        }
    }

    // Statics outlive a play session; Time.time does not. Without this the first scan of the second run
    // is skipped as "recent" and every box paints itself from the last session's field.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlaySession()
    {
        _byBox.Clear();
        _scannedAt = float.NegativeInfinity;
    }
}
