using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Daytona's paint and the paving round the racing surface's outside edge.
//
//   - The bottom of the track is a double yellow line - two thin yellow lines either side of the racing surface's
//     infield edge, where the apron starts - instead of the white edge line TrackBuilder paints there. The white
//     line moves to the apron's other side, where the apron meets the infield grass.
//   - The outside wall stands on paving: a strip of the racing surface's asphalt from the road's outside edge to
//     past the back of the SAFER barrier and its catch fence, so no grass shows under or in front of the wall.
//   - In front of the joined tri-oval grandstand (DaytonaPackageAuthoring pulls it in to TriOvalStandSetback off
//     the road and makes it TriOvalStandDepth deep) the ground from the road's edge to the front row is paved too.
//
// Every piece is a TrackEnvironment strip, anchored to an edge of the road so it follows the traced shape. The
// paving strips are labelled "paved", which TrackEnvironmentBuilder registers as tarmac run-off for the physics; the
// paint strips are not (a TarmacRunoff footprint over the racing surface would read the road as run-off).
//
// Applied by Author Daytona and, without re-laying the paddock, by
// Draftmaster > Tracks > Daytona > Apron Lines, Wall Paving + Tri-oval Crowd (DaytonaPackageAuthoring).
public static class DaytonaTrackMarkings
{
    public const string YellowTrackSideLabel = "Yellow line (track side)";
    public const string YellowApronSideLabel = "Yellow line (apron side)";
    public const string WhiteLineLabel = "White line (apron to grass)";
    public const string WallPavingLabel = "Outside wall (paved)";
    public const string FrontagePavingLabel = "Tri-oval frontage (paved)";

    // Each yellow line, and the gap between them, in metres - two pixels at the 12.8 px/m standard.
    public const float YellowLineWidth = 0.15f;
    public const float YellowLineGap = 0.15f;

    // The tri-oval crowd: its front row this far off the road's outside edge (was the standard 12 m), and twice the
    // standard 14 m deep.
    public const float TriOvalStandSetback = 5f;
    public const float TriOvalStandDepth = 28f;
    // The frontage paving runs this far under the stand's front row, so no grass shows at the seam.
    const float FrontagePavingUnderStand = 1f;
    // And the wall paving this far past the back of the barrier (or its catch fence, whichever reaches further).
    const float WallPavingPastBarrier = 0.5f;
    // Half TrackEnvironmentBuilder's barrier collider: the wall's face stands this far inside its offset line.
    const float BarrierFaceInset = 0.5f;

    const string YellowMaterialPath = "Assets/Materials/Yellow.mat";
    const string WhiteMaterialPath = "Assets/Materials/White.mat";

    static readonly string[] Labels =
        { YellowTrackSideLabel, YellowApronSideLabel, WhiteLineLabel, WallPavingLabel, FrontagePavingLabel };

    // How far the wall paving reaches out from the road's outside edge.
    public static float WallPavingWidth(TrackEnvironment env)
    {
        float offset = env.innerEdgeOffset;
        if (env.barrierOffsets != null)
            foreach (var o in env.barrierOffsets) offset = Mathf.Max(offset, o.innerOffset);
        float reach = Mathf.Max(0.05f, env.barrierWidth);
        var safer = env.saferBarrier ? SaferBarrierStyle.Default : null;
        if (safer != null)
            foreach (var layer in safer.Layers()) reach = Mathf.Max(reach, layer.to);
        return Mathf.Max(0f, offset - BarrierFaceInset) + reach + WallPavingPastBarrier;
    }

    // Writes the strips into the environment asset and turns off TrackBuilder's own left (infield) edge line.
    // frontStandAt is a lap distance inside the joined front-stretch stand's run (DaytonaPackageAuthoring's
    // start/finish line). The caller rebuilds the package's TrackEnvironmentBuilder and TrackBuilder.
    public static string Apply(TrackEnvironment env, TrackBuilder builder, float frontStandAt)
    {
        var track = builder.track;
        var segs = track.segments;
        int last = segs.Length - 1;
        float lap = track.TotalLength();
        var notes = new List<string>();

        var strips = new List<TrackEnvironment.Strip>(env.strips ?? new TrackEnvironment.Strip[0]);
        strips.RemoveAll(s => System.Array.IndexOf(Labels, s.label) >= 0);

        var yellow = AssetDatabase.LoadAssetAtPath<Material>(YellowMaterialPath);
        var white = track.edgeLineMaterial != null ? track.edgeLineMaterial
                  : AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
        float whiteWidth = track.edgeLineWidth > 0f ? track.edgeLineWidth : 0.3f;
        int paintOrder = track.edgeLineSortingOrder;
        float apron = DaytonaPackageAuthoring.ApronMetres;

        TrackEnvironment.Strip Lap(string label, TrackEnvironment.LateralAnchor anchor, float offset, float width,
                                   int order, Material material) => new TrackEnvironment.Strip
        {
            label = label,
            useSpline = TrackEnvironment.SplineRef.Main,
            anchor = anchor,
            startSegmentIndex = 0,
            startDistance = 0f,
            endSegmentIndex = last,
            endDistance = segs[last].length,
            lateralOffset = offset,
            width = width,
            sortingOrder = order,
            material = material,
            uvLengthScale = 1f,
        };

        // The double yellow, straddling the infield edge (positive offset = right of travel = onto the road).
        float yellowCentre = (YellowLineGap + YellowLineWidth) * 0.5f;
        strips.Add(Lap(YellowTrackSideLabel, TrackEnvironment.LateralAnchor.LeftEdge, yellowCentre, YellowLineWidth,
                       paintOrder, yellow));
        strips.Add(Lap(YellowApronSideLabel, TrackEnvironment.LateralAnchor.LeftEdge, -yellowCentre, YellowLineWidth,
                       paintOrder, yellow));
        // The white line along the apron's grass edge, its outer edge on the apron's.
        strips.Add(Lap(WhiteLineLabel, TrackEnvironment.LateralAnchor.LeftEdge, -(apron - whiteWidth * 0.5f), whiteWidth,
                       paintOrder, white));
        track.drawLeftEdgeLine = false;
        EditorUtility.SetDirty(track);
        notes.Add($"double yellow {YellowLineWidth} m lines {YellowLineGap} m apart on the infield edge ({(yellow != null ? yellow.name : "NO MATERIAL")}), " +
                  $"white line {whiteWidth} m at the apron's grass edge {apron} m in, TrackBuilder left edge line off");

        // Asphalt under the outside wall, all the way round.
        float wallPaving = WallPavingWidth(env);
        strips.Add(Lap(WallPavingLabel, TrackEnvironment.LateralAnchor.RightEdge, wallPaving * 0.5f, wallPaving,
                       env.runoffSortingOrder + 1, builder.surfaceMaterial));
        notes.Add($"outside wall paved {wallPaving:F2} m out from the road's edge");

        // Asphalt in front of the tri-oval stand, over the stand's run. Drawn above the wall paving so the two
        // asphalt strips never fight over the same pixels.
        if (TrackDressingFactory.JoinedRunSpan(builder, frontStandAt, out float from, out float length) && length > 1f)
        {
            float width = TriOvalStandSetback + FrontagePavingUnderStand;
            int pieces = 0;
            foreach (var span in SplitAtSeam(from, from + length, lap))
            {
                var a = ToSegment(segs, span.x);
                var b = ToSegment(segs, span.y);
                strips.Add(new TrackEnvironment.Strip
                {
                    label = FrontagePavingLabel,
                    useSpline = TrackEnvironment.SplineRef.Main,
                    anchor = TrackEnvironment.LateralAnchor.RightEdge,
                    startSegmentIndex = a.index,
                    startDistance = a.offset,
                    endSegmentIndex = b.index,
                    endDistance = b.offset,
                    lateralOffset = width * 0.5f,
                    width = width,
                    sortingOrder = env.runoffSortingOrder + 2,
                    material = builder.surfaceMaterial,
                    uvLengthScale = 1f,
                });
                pieces++;
            }
            notes.Add($"tri-oval frontage paved {width:F1} m out over {Mathf.Repeat(from, lap):F0}-{Mathf.Repeat(from + length, lap):F0} m ({pieces} strip(s))");
        }
        else notes.Add("NO joined front-stretch run found - tri-oval frontage NOT paved");

        env.strips = strips.ToArray();
        EditorUtility.SetDirty(env);
        return string.Join("; ", notes);
    }

    // [start, end] (end may run past the lap) as one or two spans inside [0, lap].
    static List<Vector2> SplitAtSeam(float start, float end, float lap)
    {
        var spans = new List<Vector2>();
        float s = Mathf.Repeat(start, lap);
        float e = s + (end - start);
        if (e <= lap) spans.Add(new Vector2(s, e));
        else
        {
            spans.Add(new Vector2(s, lap));
            spans.Add(new Vector2(0f, e - lap));
        }
        return spans;
    }

    // A lap distance as (segment index, metres from that segment's start).
    static (int index, float offset) ToSegment(TrackInfoV2.TrackSegment[] segs, float distance)
    {
        float start = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            if (distance <= start + segs[i].length || i == segs.Length - 1)
                return (i, Mathf.Clamp(distance - start, 0f, segs[i].length));
            start += segs[i].length;
        }
        return (0, 0f);
    }
}
