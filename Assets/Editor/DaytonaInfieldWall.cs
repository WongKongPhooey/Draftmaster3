using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Daytona's infield wall and its paved back straight.
//
// The infield wall stands three track widths in from the racing surface's infield (left) edge, all the way round,
// so the apron runs onto a wide strip of grass before the wall rather than straight into it. Pit road crosses
// that line twice, where the chord peels off the front stretch and where it comes back, and the wall is only
// opened there (TrackDressingFactory.PitGaps with a set-back wall measures the crossings and cuts no fixed window
// round the pit nodes, which sit inside the wall). Along the middle of the front stretch the wall runs between
// the racing surface and pit road.
//
// The infield between the road and the wall on the back straight (the longest straight) is paved with the
// racing surface's own asphalt: a TarmacRunoff area, so the car physics reads it as tarmac, not grass.
//
// Applied by Author Daytona (DaytonaPackageAuthoring) and on its own, without re-laying the paddock, by
// Draftmaster > Tracks > Daytona > Infield Wall + Paved Back Straight (TrackWallRebuildMenu), which also
// re-scatters the scenery so nothing stands between the road and the wall (TrackSceneryGenerator keeps that band
// clear for any wall set further back than its road clearance).
public static class DaytonaInfieldWall
{
    public const string TrackId = "Daytona";
    // How far the infield wall stands off the racing surface's infield edge, in track widths.
    public const float TrackWidthsBack = 3f;
    public const string PavingLabel = "Back straight infield (paved)";
    // The paving runs this far past the wall's track face, under the wall, so no grass shows at the seam.
    const float PavingUnderWall = 0.5f;
    // Rail spacing along the straight (m).
    const float PavingStep = 10f;
    // A segment turning less than this (degrees) counts as straight - traced ovals author every piece as a Turn.
    const float StraightMaxAngle = 2f;

    public static float RoadWidth(TrackInfoV2 track)
    {
        if (track == null || track.segments == null || track.segments.Length == 0) return 0f;
        float sum = 0f, len = 0f;
        foreach (var s in track.segments) { sum += s.width * s.length; len += s.length; }
        return len > 0f ? sum / len : track.segments[0].width;
    }

    public static float WallOffset(TrackInfoV2 track) => TrackWidthsBack * RoadWidth(track);

    // Index of the longest straight segment (a Straight, or a Turn of under StraightMaxAngle), -1 if none.
    public static int LongestStraight(TrackInfoV2 track)
    {
        int best = -1;
        if (track == null || track.segments == null) return best;
        for (int i = 0; i < track.segments.Length; i++)
        {
            var s = track.segments[i];
            bool straight = s.type == TrackInfoV2.SegmentType.Straight || Mathf.Abs(s.angle) < StraightMaxAngle;
            if (straight && (best < 0 || s.length > track.segments[best].length)) best = i;
        }
        return best;
    }

    // Turns the infield wall on at WallOffset, re-cuts the pit-road gaps for it and lays the back-straight paving.
    // Writes the environment asset only; the caller rebuilds the package's TrackEnvironmentBuilder.
    public static string Apply(TrackEnvironment env, TrackBuilder builder)
    {
        var track = builder.track;
        // Left of travel on an anticlockwise oval is BarrierSide.Outer - the infield wall.
        env.outerSideBarrier = true;
        env.outerEdgeOffset = WallOffset(track);
        if (env.barrierOffsets != null)
        {
            // A per-segment outer offset would pull the wall back in on that segment.
            var keep = new List<TrackEnvironment.BarrierOffsetOverride>();
            foreach (var o in env.barrierOffsets)
            {
                var copy = o;
                copy.outerOffset = env.outerEdgeOffset;
                keep.Add(copy);
            }
            env.barrierOffsets = keep.ToArray();
        }
        env.barrierGaps = TrackDressingFactory.PitGaps(builder, env.innerEdgeOffset, env.outerEdgeOffset);

        var areas = new List<TrackEnvironment.RunoffArea>(env.runoffAreas ?? new TrackEnvironment.RunoffArea[0]);
        areas.RemoveAll(a => a.label == PavingLabel);
        int seg = LongestStraight(track);
        var outline = seg >= 0 ? PavingOutline(builder, seg, env.outerEdgeOffset) : null;
        if (outline != null)
            areas.Add(new TrackEnvironment.RunoffArea
            {
                label = PavingLabel,
                surface = TrackEnvironment.SurfaceType.TarmacRunoff,
                points = outline,
                materialOverride = builder.surfaceMaterial,
            });
        env.runoffAreas = areas.ToArray();
        EditorUtility.SetDirty(env);

        return $"infield wall {env.outerEdgeOffset:F1} m off the infield edge ({TrackWidthsBack} x {RoadWidth(track):F1} m), " +
               $"{env.barrierGaps.Length} wall gaps, back straight = segment {seg}" +
               (outline != null ? $" paved ({outline.Length} points, {(builder.surfaceMaterial != null ? builder.surfaceMaterial.name : "NO MATERIAL")})" : " NOT paved");
    }

    // The infield between the road's left edge and the wall, over one segment, in the track's local space: down
    // the road edge, back up under the wall.
    public static Vector2[] PavingOutline(TrackBuilder builder, int segIndex, float wallOffset)
    {
        var segs = builder.track.segments;
        float start = 0f;
        for (int i = 0; i < segIndex; i++) start += segs[i].length;
        float end = start + segs[segIndex].length;
        int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) / PavingStep));

        var edge = new List<Vector2>();
        var wall = new List<Vector2>();
        for (int k = 0; k <= steps; k++)
        {
            var s = builder.SampleAt(Mathf.Lerp(start, end, k / (float)steps));
            // Sample normals point right of travel; the infield (left) is -normal.
            edge.Add(s.position - s.normal * (s.width * 0.5f));
            wall.Add(s.position - s.normal * (s.width * 0.5f + wallOffset + PavingUnderWall));
        }
        wall.Reverse();
        edge.AddRange(wall);
        return edge.ToArray();
    }
}
