using System.Collections.Generic;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// Reading a circuit's shape off a traced centreline.
//
// The tests build a lap of known straights and corners, walk it into a dense line of points the way a mapper
// traces one, and then ask the segmenter to find the lap again. That round trip is the whole contract: what
// comes back has to be the shape that went in, to within the slop a hand-drawn trace carries.
//
// It matters because the alternative — published dimensions — cannot describe a shape. "A 1,551 ft back
// stretch on a 1.022 mile lap" is true of a long thin oval and of a rounded triangle alike, and Phoenix is
// the second one but was generated as the first: corners 34m too tight, straights 92m too long.
public class OsmTrackGeometryTests
{
    // Walk a lap into points, the way a trace of it would look.
    static List<Vector2> Trace(IEnumerable<LapGeometry.Piece> lap, float step = 2f)
    {
        var pts = new List<Vector2>();
        Vector2 at = Vector2.zero;
        float heading = 0f;
        pts.Add(at);

        foreach (var piece in lap)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(piece.length / step));
            for (int i = 0; i < steps; i++)
            {
                if (piece.isTurn) heading += piece.angle / steps;
                float h = heading * Mathf.Deg2Rad;
                at += new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * (piece.length / steps);
                pts.Add(at);
            }
        }
        return pts;
    }

    static List<LapGeometry.Piece> Oval()
    {
        // A plain oval: two 400m straights and two 180 degree ends.
        return new List<LapGeometry.Piece>
        {
            new LapGeometry.Piece(false, 400f, 0f),
            new LapGeometry.Piece(true, 420f, 180f),
            new LapGeometry.Piece(false, 400f, 0f),
            new LapGeometry.Piece(true, 420f, 180f),
        };
    }

    static List<LapGeometry.Piece> RoundedTriangle()
    {
        // Phoenix's shape: a shallow dogleg and two unequal ends.
        return new List<LapGeometry.Piece>
        {
            new LapGeometry.Piece(false, 70f, 0f),
            new LapGeometry.Piece(true, 120f, 41f),
            new LapGeometry.Piece(false, 250f, 0f),
            new LapGeometry.Piece(true, 460f, 166f),
            new LapGeometry.Piece(false, 370f, 0f),
            new LapGeometry.Piece(true, 390f, 153f),
        };
    }

    // ------------------------------------------------------------------ reading a shape back

    [Test]
    public void APlainOvalComesBackAsTwoStraightsAndTwoEnds()
    {
        var found = OsmTrackGeometry.Segment(Trace(Oval()));

        Assert.AreEqual(4, found.Count,
                        "A traced oval didn't come back as two straights and two corners: " + Describe(found));
        Assert.AreEqual(360f, LapGeometry.TotalAngle(found), 6f, "The lap doesn't turn through a full circle.");
        Assert.AreEqual(1640f, LapGeometry.TotalLength(found), 30f, "The lap came back the wrong length.");

        foreach (var piece in found)
            if (piece.isTurn)
                Assert.AreEqual(180f, Mathf.Abs(piece.angle), 8f, "A 180 degree end read as something else.");
            else
                Assert.AreEqual(400f, piece.length, 40f, "A 400m straight read as the wrong length.");
    }

    [Test]
    public void TheDoglegSurvivesTheTrace()
    {
        // The whole point. A 41 degree kink is the difference between Phoenix and a generic oval, and it is
        // shallow enough that a careless threshold reads it as more straight.
        var found = OsmTrackGeometry.Segment(Trace(RoundedTriangle()));
        var turns = found.FindAll(p => p.isTurn);

        Assert.AreEqual(3, turns.Count, "The rounded triangle didn't come back with three corners: " + Describe(found));

        turns.Sort((a, b) => Mathf.Abs(a.angle).CompareTo(Mathf.Abs(b.angle)));
        Assert.AreEqual(41f, Mathf.Abs(turns[0].angle), 10f, "The dogleg was lost or swallowed by a straight.");
        Assert.AreEqual(153f, Mathf.Abs(turns[1].angle), 12f);
        Assert.AreEqual(166f, Mathf.Abs(turns[2].angle), 12f);
    }

    [Test]
    public void ASloppyTraceStillReadsAsTheSameLap()
    {
        // Mappers click by hand and nodes land a metre or two off the true line. Without smoothing every one
        // of those wobbles reads as a corner and a straight comes back sawn into a dozen pieces.
        var rng = new System.Random(1234);
        var noisy = new List<Vector2>();
        foreach (var p in Trace(RoundedTriangle()))
            noisy.Add(p + new Vector2((float)(rng.NextDouble() - 0.5) * 1.6f,
                                      (float)(rng.NextDouble() - 0.5) * 1.6f));

        var found = OsmTrackGeometry.Segment(noisy);

        // What is promised under noise is that the lap survives as a lap — not that every piece is recovered.
        // A short straight pinched between two corners can be swallowed when the trace is this rough, and
        // pretending otherwise would be a test that fails on somebody else's tracing rather than on a bug.
        Assert.LessOrEqual(found.Count, 8, "The trace shattered into slivers: " + Describe(found));
        Assert.AreEqual(360f, LapGeometry.TotalAngle(found), 15f,
                        "The lap stopped turning through a full circle: " + Describe(found));

        var ends = found.FindAll(p => p.isTurn && Mathf.Abs(p.angle) > 120f);
        Assert.GreaterOrEqual(ends.Count, 2, "Both big ends should survive any plausible trace: " + Describe(found));
        Assert.AreEqual(1640f, LapGeometry.TotalLength(found), 60f, "The lap came back the wrong length.");
    }

    [Test]
    public void ResamplingMeasuresTheRoadRatherThanTheMapper()
    {
        // Nodes cluster round corners and thin out down straights, so raw spacing describes the tracing and
        // not the circuit. Every sample out of Resample should be the same distance from the last.
        var uneven = new List<Vector2> { new Vector2(0, 0), new Vector2(100, 0), new Vector2(101, 0),
                                         new Vector2(102, 0), new Vector2(300, 0) };
        var even = OsmTrackGeometry.Resample(uneven, 10f);

        Assert.Greater(even.Count, 25, "Resampling didn't fill in the long gaps.");
        for (int i = 1; i < even.Count; i++)
            Assert.AreEqual(10f, Vector2.Distance(even[i - 1], even[i]), 0.5f,
                            "Samples aren't evenly spaced, so curvature would measure the mapper.");
    }

    [Test]
    public void LatitudeAndLongitudeBecomeMetres()
    {
        // A tenth of a degree of latitude is about 11.1km anywhere; the same in longitude shrinks with the
        // cosine of the latitude. Getting that backwards would stretch every circuit along one axis.
        var pts = OsmTrackGeometry.Project(new[]
        {
            new OsmTrackGeometry.LatLon(33.30, -112.30),
            new OsmTrackGeometry.LatLon(33.40, -112.30),
            new OsmTrackGeometry.LatLon(33.30, -112.20),
        });

        Assert.AreEqual(11119f, Vector2.Distance(pts[0], pts[1]), 60f, "A tenth of a degree of latitude is ~11.1km.");
        Assert.AreEqual(11119f * Mathf.Cos(33.35f * Mathf.Deg2Rad), Vector2.Distance(pts[0], pts[2]), 80f,
                        "Longitude wasn't scaled by the cosine of the latitude.");
    }

    // ------------------------------------------------------------------ making it join up

    [Test]
    public void AMeasuredLapIsMadeToCloseByMovingOnlyItsStraights()
    {
        // Phoenix's hand-measured lap misses its own start by 93m, because the old game was one-dimensional
        // and nothing ever required the plan view to join up.
        var lap = new List<LapGeometry.Piece>
        {
            new LapGeometry.Piece(false, 100f, 0f),
            new LapGeometry.Piece(true, 120f, 50f),
            new LapGeometry.Piece(false, 240f, 0f),
            new LapGeometry.Piece(true, 400f, 170f),
            new LapGeometry.Piece(false, 380f, 0f),
            new LapGeometry.Piece(true, 360f, 140f),
        };
        var corners = lap.FindAll(p => p.isTurn);

        Assert.Greater(LapGeometry.ClosureGap(lap), 50f, "This lap is supposed to start out badly open.");
        Assert.IsTrue(LapGeometry.CloseByStraights(lap), "The lap couldn't be closed.");
        Assert.Less(LapGeometry.ClosureGap(lap), 0.5f, "The lap still doesn't join up.");

        // The corners are the part that was actually measured, and they have to come through untouched.
        var after = lap.FindAll(p => p.isTurn);
        for (int i = 0; i < corners.Count; i++)
        {
            Assert.AreEqual(corners[i].length, after[i].length, 1e-3f, "A corner's length was changed to close the lap.");
            Assert.AreEqual(corners[i].angle, after[i].angle, 1e-3f, "A corner's angle was changed to close the lap.");
        }
        foreach (var piece in lap) Assert.Greater(piece.length, 0f, "A segment was folded away to nothing.");
    }

    [Test]
    public void RescalingAClosedLapKeepsItClosed()
    {
        var lap = Oval();
        LapGeometry.Rescale(lap, 2500f);

        Assert.AreEqual(2500f, LapGeometry.TotalLength(lap), 0.1f);
        Assert.Less(LapGeometry.ClosureGap(lap), 0.5f, "Scaling opened the lap up.");
    }

    [Test]
    public void TurnAnglesAreSpreadToAFullCircle()
    {
        // A traced lap comes back a few degrees short purely from resampling, and a few degrees is the
        // difference between a road that closes and one that spirals.
        var lap = new List<LapGeometry.Piece>
        {
            new LapGeometry.Piece(false, 400f, 0f),
            new LapGeometry.Piece(true, 420f, 177f),
            new LapGeometry.Piece(false, 400f, 0f),
            new LapGeometry.Piece(true, 420f, 179f),
        };
        LapGeometry.NormaliseTurnAngles(lap);

        Assert.AreEqual(360f, LapGeometry.TotalAngle(lap), 1e-3f);
        Assert.Greater(lap[3].angle, lap[1].angle, "The bigger corner should still be the bigger one.");
    }

    [Test]
    public void APlainOvalClosesEvenThoughItsStraightsCannotDoIt()
    {
        // An oval's two straights are antiparallel, so between them they can only move the far end along one
        // axis and the perpendicular error has nowhere to go. On an oval that error IS the corner radius, so
        // the corners have to take it — which is what Close falls back to when the straights alone fail.
        var lap = new List<LapGeometry.Piece>
        {
            new LapGeometry.Piece(false, 400f, 0f),
            new LapGeometry.Piece(true, 420f, 180f),
            new LapGeometry.Piece(false, 430f, 0f),   // 30m longer, so the lap no longer joins up
            new LapGeometry.Piece(true, 420f, 180f),
        };

        Assert.Greater(LapGeometry.ClosureGap(lap), 10f, "This oval is supposed to start out open.");
        Assert.IsFalse(LapGeometry.CloseByStraights(lap), "Two antiparallel straights should not be able to close it.");
        Assert.IsTrue(LapGeometry.Close(lap), "The fallback didn't close a plain oval.");
        Assert.Less(LapGeometry.ClosureGap(lap), 0.5f, "The oval still doesn't join up.");
        foreach (var piece in lap) Assert.Greater(piece.length, 0f, "A segment was folded away to nothing.");
    }

    [Test]
    public void ALapWithNoStraightsCannotBeClosedAndSaysSo()
    {
        // A circle is already closed and has nothing to move; the caller needs to know rather than be given
        // a silently unchanged lap it believes was fixed.
        var lap = new List<LapGeometry.Piece> { new LapGeometry.Piece(true, 1000f, 359f) };
        Assert.IsFalse(LapGeometry.CloseByStraights(lap), "Claimed to close a lap that has no straights in it.");
    }

    static string Describe(IEnumerable<LapGeometry.Piece> lap)
    {
        var parts = new List<string>();
        foreach (var p in lap)
            parts.Add(p.isTurn ? $"Turn {p.length:0}m/{p.angle:0}deg" : $"Straight {p.length:0}m");
        return string.Join(", ", parts);
    }
}
