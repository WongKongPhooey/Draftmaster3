using System.Collections.Generic;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// Every venue on the three calendars, checked as geometry rather than as a list of hopes.
//
// The point of these is that a track can be WRONG in ways that look fine in the inspector: a lap that
// does not shut, a corner sequence that turns through 500 degrees, a "link" that the solver quietly
// crushed into a hairpin, a road two metres wide. All of those produce an asset that loads, builds a
// mesh, and is undriveable. So the checks below are on the solved geometry, per venue, by name.
public class TrackDimensionsTests
{
    // ---------------------------------------------------------------- the table itself

    [Test]
    public void EveryVenueHasAUniqueId()
    {
        var seen = new HashSet<string>();
        foreach (var dim in TrackDimensions.All)
            Assert.IsTrue(seen.Add(dim.id), $"Duplicate track id '{dim.id}' in TrackDimensions.");
    }

    [Test]
    public void EveryVenueHasPlausibleDimensions()
    {
        foreach (var dim in TrackDimensions.All)
        {
            Assert.Greater(dim.lapMiles, 0.2f, $"{dim.id}: lap length too short to be a racetrack.");
            Assert.Less(dim.lapMiles, 5f, $"{dim.id}: lap length longer than any circuit on the calendars.");

            // Nothing on these calendars is narrower than Lime Rock (11 m) or wider than Michigan (73 ft).
            Assert.GreaterOrEqual(dim.widthMetres, 10f, $"{dim.id}: {dim.widthMetres:0.0} m is too narrow.");
            Assert.LessOrEqual(dim.widthMetres, 23f, $"{dim.id}: {dim.widthMetres:0.0} m is too wide.");

            Assert.GreaterOrEqual(dim.turnBankingDeg, 0f, $"{dim.id}: negative banking.");
            Assert.LessOrEqual(dim.turnBankingDeg, 36f, $"{dim.id}: steeper than Talladega.");
            Assert.AreNotEqual(SeriesVisits.None, dim.series, $"{dim.id}: no championship visits it.");
            Assert.Greater(dim.cupLaps, 0, $"{dim.id}: no scheduled distance.");
        }
    }

    // The bug this whole table exists to fix: width used to come from the track TYPE, so every
    // superspeedway was 18 m. If these ever come out equal again, the per-track width has been lost.
    [Test]
    public void WidthIsPerTrackNotPerType()
    {
        Assert.IsTrue(TrackDimensions.TryGet("Michigan", out var michigan));
        Assert.IsTrue(TrackDimensions.TryGet("Dover", out var dover));
        Assert.IsTrue(TrackDimensions.TryGet("Daytona", out var daytona));
        Assert.IsTrue(TrackDimensions.TryGet("Talladega", out var talladega));

        // Both "speedways", 33 feet apart.
        Assert.Greater(michigan.widthMetres, dover.widthMetres + 9f,
                       "Michigan (73 ft) should be far wider than Dover (40 ft).");

        // Both superspeedways; Talladega is the wider one, which is why its packs are bigger.
        Assert.Greater(talladega.widthMetres, daytona.widthMetres + 1.5f,
                       "Talladega (48 ft) should be wider than Daytona (40 ft).");
    }

    // ---------------------------------------------------------------- ovals

    [Test]
    public void EveryOvalSolvesToAClosedLapOfTheRightLength()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.kind == TrackKind.RoadCourse) continue;
            if (RoadCourseLayouts.Has(dim.id)) continue;   // Pocono: a triangle, solved on the authored path

            var spec = OvalGeometry.Preset(dim.kind, dim.id, dim.displayName,
                                           dim.lapMiles, dim.turnBankingDeg, dim.cupLaps);
            var segments = OvalGeometry.Build(spec);
            var check = OvalGeometry.Validate(segments);

            Assert.AreEqual(dim.lapMiles, check.lapMiles, 0.01f,
                            $"{dim.id}: lap measures {check.lapMiles:0.###} mi, published {dim.lapMiles:0.###}.");
            Assert.AreEqual(360f, Mathf.Abs(check.totalTurnDegrees), 0.5f,
                            $"{dim.id}: turns sum to {check.totalTurnDegrees:0.#}°, not one revolution.");

            // A few metres out over a lap of thousands is invisible; a few hundred is a broken track.
            float tolerance = Mathf.Max(5f, dim.LapMetres * 0.01f);
            Assert.Less(check.closureErrorMetres, tolerance,
                        $"{dim.id}: lap misses its own start by {check.closureErrorMetres:0.0} m.");
        }
    }

    // The generator takes its width from the table now, not from the track type.
    [Test]
    public void EveryOvalIsBuiltToItsPublishedWidth()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.kind == TrackKind.RoadCourse || RoadCourseLayouts.Has(dim.id)) continue;
            var spec = OvalGeometry.Preset(dim.kind, dim.id, dim.displayName,
                                           dim.lapMiles, dim.turnBankingDeg, dim.cupLaps);
            Assert.AreEqual(dim.widthMetres, spec.roadWidth, 0.01f,
                            $"{dim.id}: generated at {spec.roadWidth:0.0} m, published {dim.widthMetres:0.0} m.");
            Assert.AreEqual(dim.turnBankingDeg, spec.turnBanking, 0.01f, $"{dim.id}: banking lost.");
        }
    }

    // Wide-in / tight-apex / wide-out only works if the AI lines actually fit inside the road. On a
    // 40 ft track a fixed 1.5 m margin left them almost on top of each other.
    [Test]
    public void RacingLinesFitInsideTheRoad()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.kind == TrackKind.RoadCourse || RoadCourseLayouts.Has(dim.id)) continue;
            var spec = OvalGeometry.Preset(dim.kind, dim.id, dim.displayName,
                                           dim.lapMiles, dim.turnBankingDeg, dim.cupLaps);
            float half = spec.roadWidth * 0.5f;

            foreach (var seg in OvalGeometry.Build(spec))
            {
                foreach (float offset in new[] { seg.line.idealEntry, seg.line.idealApex, seg.line.idealExit,
                                                 seg.line.leftApex, seg.line.rightApex })
                    Assert.LessOrEqual(Mathf.Abs(offset), half,
                                       $"{dim.id}/{seg.label}: racing line {offset:0.0} m is outside the road.");
            }
        }
    }

    // ---------------------------------------------------------------- road courses

    [Test]
    public void WatkinsGlenIsNeverGenerated()
    {
        Assert.IsFalse(RoadCourseLayouts.Has("WatkinsGlen"),
                       "Watkins Glen was measured by hand off satellite imagery — it must never be " +
                       "overwritten by a generated approximation.");
    }

    [Test]
    public void EveryRoadCourseOnTheCalendarHasALayout()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.kind != TrackKind.RoadCourse) continue;
            if (dim.id == RoadCourseLayouts.HandAuthored) continue;
            Assert.IsTrue(RoadCourseLayouts.Has(dim.id), $"{dim.id}: road course with no authored layout.");
        }
    }

    [Test]
    public void EveryRoadCourseSolvesToAClosedLapOfTheRightLength()
    {
        foreach (string id in RoadCourseLayouts.Ids)
        {
            var spec = RoadCourseLayouts.Spec(id);
            Assert.IsNotNull(spec, $"{id}: no spec.");

            var solved = RoadCourseGeometry.Solve(spec);
            var check = RoadCourseGeometry.Validate(solved);

            Assert.AreEqual(spec.lengthMiles, check.lapMiles, 0.005f,
                            $"{id}: lap measures {check.lapMiles:0.###} mi, published {spec.lengthMiles:0.###}.");
            Assert.AreEqual(360f, Mathf.Abs(check.totalTurnDegrees), 0.5f,
                            $"{id}: turns sum to {check.totalTurnDegrees:0.#}°, not one revolution.");
            Assert.Less(check.closureErrorMetres, 1f,
                        $"{id}: lap misses its own start by {check.closureErrorMetres:0.00} m.");
        }
    }

    [Test]
    public void RoadCourseCornerCountsMatchTheRealCircuits()
    {
        foreach (string id in RoadCourseLayouts.Ids)
        {
            Assert.IsTrue(TrackDimensions.TryGet(id, out var dim));
            if (dim.kind != TrackKind.RoadCourse) continue;   // ovals have no numbered corner map
            var check = RoadCourseGeometry.Validate(RoadCourseGeometry.Solve(RoadCourseLayouts.Spec(id)));

            // Chicanes and complexes are authored as two pieces, so allow a little slack either way —
            // what this is guarding against is a circuit losing half its corners to a bad edit.
            Assert.AreEqual(dim.cornerCount, check.namedCorners, 3,
                            $"{id}: {check.namedCorners} authored corners, circuit map says {dim.cornerCount}.");
        }
    }

    // The residual curvature is meant to land on the connectors as a gentle sweep. If closure ever
    // crushes one short enough to become a hairpin, the circuit gains a corner nobody authored.
    [Test]
    public void ResidualCurvatureStaysAGentleSweep()
    {
        foreach (string id in RoadCourseLayouts.Ids)
        {
            var check = RoadCourseGeometry.Validate(RoadCourseGeometry.Solve(RoadCourseLayouts.Spec(id)));
            Assert.Greater(check.tightestLinkRadius, 150f,
                           $"{id}: a connector solved to a {check.tightestLinkRadius:0} m radius — that is a " +
                           "corner, not a sweep. Rebalance the authored corner angles.");
        }
    }

    [Test]
    public void NoRoadCoursePieceIsTooShortToDrive()
    {
        foreach (string id in RoadCourseLayouts.Ids)
        {
            foreach (var piece in RoadCourseGeometry.Solve(RoadCourseLayouts.Spec(id)))
                Assert.Greater(piece.length, 20f,
                               $"{id}/{piece.label}: {piece.length:0.0} m is shorter than the car.");
        }
    }

    // A circuit that crosses itself would need a bridge, and none of these have one. Checked on a coarse
    // polyline of the solved centreline.
    [Test]
    public void NoRoadCourseCrossesItself()
    {
        foreach (string id in RoadCourseLayouts.Ids)
        {
            var points = Centreline(RoadCourseGeometry.Solve(RoadCourseLayouts.Spec(id)), 10f);
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;   // the closing seam meets by design
                    Assert.IsFalse(SegmentsCross(points[i], points[(i + 1) % n], points[j], points[(j + 1) % n]),
                                   $"{id}: the circuit crosses itself near piece {i}.");
                }
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    static List<Vector2> Centreline(List<RoadPiece> pieces, float step)
    {
        var points = new List<Vector2>();
        Vector2 pos = Vector2.zero;
        float heading = 0f;

        foreach (var piece in pieces)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(piece.length / step));
            if (!piece.IsTurn)
            {
                var dir = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                for (int i = 1; i <= steps; i++) points.Add(pos + dir * (piece.length * i / steps));
                pos = points[points.Count - 1];
                continue;
            }

            float angle = piece.angle * Mathf.Deg2Rad;
            float radius = piece.length / Mathf.Abs(angle);
            float sign = piece.angle >= 0f ? 1f : -1f;
            var centre = pos + new Vector2(-sign * Mathf.Sin(heading), sign * Mathf.Cos(heading)) * radius;
            float start = Mathf.Atan2(pos.y - centre.y, pos.x - centre.x);
            for (int i = 1; i <= steps; i++)
            {
                float a = start + angle * i / steps;
                points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            pos = points[points.Count - 1];
            heading += angle;
        }
        return points;
    }

    static bool SegmentsCross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d = (a2.x - a1.x) * (b2.y - b1.y) - (a2.y - a1.y) * (b2.x - b1.x);
        if (Mathf.Abs(d) < 1e-9f) return false;
        float t = ((b1.x - a1.x) * (b2.y - b1.y) - (b1.y - a1.y) * (b2.x - b1.x)) / d;
        float u = ((b1.x - a1.x) * (a2.y - a1.y) - (b1.y - a1.y) * (a2.x - a1.x)) / d;
        return t > 1e-5f && t < 1f - 1e-5f && u > 1e-5f && u < 1f - 1e-5f;
    }
}
