using UnityEngine;

// Authoritative pit-box geometry, shared by every car so they all agree where the boxes are. GridSpawner fits the
// boxes to the pit-lane length and publishes the result here; the safety car and the pit-stop logic read it back.
//
// Box 0 sits nearest the pit EXIT (the pole car's box); each higher index steps back toward the pit ENTRANCE, so
// the highest index (LastBox) is the box closest to the START of the pit lane. A car's assigned box = its grid /
// qualifying position (the player uses its reserved slot); the pace car parks in LastBox at the end of the lap.
public static class PitLane
{
    public static bool Configured { get; private set; }
    public static float ExitGap { get; private set; }   // m from the pit-lane end (exit) to box 0 (pole)
    public static float Spacing { get; private set; }    // m between adjacent boxes
    public static int BoxCount { get; private set; } = 1; // total assigned boxes (one per race car)

    public static void Configure(float exitGap, float spacing, int boxCount)
    {
        ExitGap = exitGap;
        Spacing = spacing;
        BoxCount = Mathf.Max(1, boxCount);
        Configured = true;
    }

    // Distance along the pit lane (m) for a box. Box 0 = nearest the exit; higher index = nearer the entrance.
    public static float BoxDistance(int idx, float pitLength) => Mathf.Max(0f, pitLength - ExitGap - idx * Spacing);

    // The box closest to the START of the pit lane (entrance). The pace car parks here.
    public static int LastBox => Mathf.Max(0, BoxCount - 1);
}
