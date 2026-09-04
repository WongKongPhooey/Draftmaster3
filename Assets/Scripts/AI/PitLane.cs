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
    // Signed lateral offset (m) of the parked "box lane" from the pit centerline (positive = wall side).
    // Parked cars sit here, in front of their box props; cars DRIVING the lane keep the centerline, so a
    // moving car never shares a line with a parked one.
    public static float ParkLateral { get; private set; }

    // The human player's reserved box index. -1 = the player has no pit box this session.
    public static int PlayerBox { get; private set; } = -1;

    // Raised whenever the ladder changes shape — a field arriving, a field leaving. The pit crews are built
    // from it, and a weekend changes hands several times in one scene: the Trucks practise, then everybody
    // goes away, then the National cars come out. Anything built per box has to be able to follow that.
    public static event System.Action Changed;

    public static void Configure(float exitGap, float spacing, int boxCount, float parkLateral = 0f)
    {
        ExitGap = exitGap;
        Spacing = spacing;
        BoxCount = Mathf.Max(1, boxCount);
        ParkLateral = parkLateral;
        PlayerBox = -1;   // GridSpawner re-publishes it after reserving the box
        Configured = true;
        Changed?.Invoke();
    }

    // The field has gone: there are no boxes, because there is nobody to have one. Pit road between
    // sessions is empty tarmac, not forty crews stood over nothing.
    public static void Clear()
    {
        if (!Configured) return;
        Configured = false;
        BoxCount = 1;
        PlayerBox = -1;
        Changed?.Invoke();
    }

    public static void SetPlayerBox(int idx) => PlayerBox = idx;

    // Distance along the pit lane (m) for a box. Box 0 = nearest the exit; higher index = nearer the entrance.
    public static float BoxDistance(int idx, float pitLength) => Mathf.Max(0f, pitLength - ExitGap - idx * Spacing);

    // Same maths against an explicit fit, for callers (the editor preview, the painted lines) working
    // before Configure ran.
    public static float BoxDistance(int idx, float pitLength, in BoxFit fit) =>
        Mathf.Max(0f, pitLength - fit.exitGap - idx * fit.spacing);

    // Distance of the dividing line at boundary `i` — 0 is the line at box 0's exit side, `boxes` the one
    // behind the last box, so a fit of N boxes has N+1 lines. Clamped into [stripFrom, stripTo]: the fit
    // keeps the box CENTRES a margin inside the grey strip, but box 0's front line still lands past its
    // end, and paint hanging off the tarmac reads as a bug.
    public static float BoxLineDistance(int i, float pitLength, in BoxFit fit, float stripFrom, float stripTo) =>
        Mathf.Clamp(BoxDistance(0, pitLength, fit) + fit.spacing * 0.5f - i * fit.spacing, stripFrom, stripTo);

    // The box closest to the START of the pit lane (entrance). The pace car parks here.
    public static int LastBox => Mathf.Max(0, BoxCount - 1);

    // ---- Box fitting ---------------------------------------------------------------------------
    // The single copy of "where do the boxes land". GridSpawner runs it at spawn and publishes the
    // result through Configure; the editor gizmo and the fit debug log run it at edit time, so the
    // preview in the scene view is the same ladder the cars park on.

    public const float BandMargin = 3f;         // keep the end boxes off the grey strip's very edges
    public const float MinSpacing = 4.5f;
    public const float MaxSpacing = 10f;
    const float FallbackExitGap = 12f;          // tracks with no box-lane strip
    const float FallbackSpanFrom = 6f;
    const float FallbackSpacing = 12f;

    public struct BoxFit
    {
        public int boxes;
        public float exitGap;      // m from the pit-lane end to box 0
        public float spacing;      // m between adjacent boxes (clamped)
        public float rawSpacing;   // pre-clamp, for diagnosing a field that doesn't fit
        public float spanFrom;     // first usable distance along the lane (entrance end)
        public float spanTo;       // last usable distance along the lane (exit end)
        public float usable;
        public float Span => Mathf.Max(0, boxes - 1) * spacing;
        public float Overflow => Mathf.Max(0f, Span - usable);
    }

    public static BoxFit FitBoxes(TrackBuilder track, float pitLength, int totalBoxes)
    {
        var fit = new BoxFit
        {
            boxes = Mathf.Max(1, totalBoxes),
            exitGap = FallbackExitGap,
            spacing = FallbackSpacing,
            spanFrom = FallbackSpanFrom,
            spanTo = pitLength - FallbackExitGap,
        };

        // The grey strip can start well after the pit entry and end well before the exit ramp, so box 0's
        // exit gap comes from the strip's end offset — a fixed gap would park the pole car on the ramp.
        if (track != null && track.HasPitBoxLane && pitLength > 0f)
        {
            fit.spanFrom = track.PitBoxLaneFrom(pitLength) + BandMargin;
            fit.spanTo = Mathf.Max(fit.spanFrom, track.PitBoxLaneTo(pitLength) - BandMargin);
            fit.exitGap = pitLength - fit.spanTo;
        }

        fit.usable = Mathf.Max(0f, fit.spanTo - fit.spanFrom);
        fit.rawSpacing = fit.boxes > 1 ? fit.usable / (fit.boxes - 1) : 0f;
        if (pitLength > 0f && fit.boxes > 1) fit.spacing = Mathf.Clamp(fit.rawSpacing, MinSpacing, MaxSpacing);
        return fit;
    }
}
