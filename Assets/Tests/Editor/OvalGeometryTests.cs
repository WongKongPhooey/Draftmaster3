using NUnit.Framework;
using UnityEngine;
using Draftmaster.Tracks;

// EditMode coverage for the generated-track maths. A layout that doesn't close, or comes out the wrong
// length, is otherwise only visible after building the mesh and driving it — these catch it at the numbers.
public class OvalGeometryTests
{
    static OvalSpec Superspeedway() =>
        OvalGeometry.Preset(TrackKind.Superspeedway, "Daytona", "Daytona International Speedway", 2.5f, 31f, 200);

    static OvalSpec ShortTrack() =>
        OvalGeometry.Preset(TrackKind.ShortTrack, "Martinsville", "Martinsville Speedway", 0.526f, 12f, 500);

    static OvalSpec Intermediate() =>
        OvalGeometry.Preset(TrackKind.Speedway, "Kansas", "Kansas Speedway", 1.5f, 17f, 267);

    [Test]
    public void LapComesOutTheRequestedLength()
    {
        foreach (var spec in new[] { Superspeedway(), ShortTrack(), Intermediate() })
        {
            var check = OvalGeometry.Validate(OvalGeometry.Build(spec));
            Assert.AreEqual(spec.lengthMiles, check.lapMiles, 0.01f,
                            $"{spec.trackId} should measure {spec.lengthMiles} miles round");
        }
    }

    [Test]
    public void CornersSumToExactlyOneLapOfHeading()
    {
        foreach (var spec in new[] { Superspeedway(), ShortTrack(), Intermediate() })
        {
            var check = OvalGeometry.Validate(OvalGeometry.Build(spec));
            // The tri-oval dog-leg nets to zero, so a left-hand oval always totals +360.
            Assert.AreEqual(360f, check.totalTurnDegrees, 0.5f, $"{spec.trackId} heading must close");
        }
    }

    [Test]
    public void LoopClosesBackOntoTheStartLine()
    {
        foreach (var spec in new[] { Superspeedway(), ShortTrack(), Intermediate() })
        {
            var check = OvalGeometry.Validate(OvalGeometry.Build(spec));
            // The corner-skew solve should shut the loop properly, not merely nearly: a couple of metres
            // over a 4 km lap is a seam TrackBuilder stitches invisibly, tens of metres is a kinked
            // start/finish you can see and drive into.
            Assert.Less(check.closureErrorMetres, 2f,
                        $"{spec.trackId} should end where it started (gap {check.closureErrorMetres:0.0} m)");
        }
    }

    [Test]
    public void RightHandedOvalMirrorsTheHeading()
    {
        var spec = Intermediate();
        spec.leftHanded = false;
        var check = OvalGeometry.Validate(OvalGeometry.Build(spec));
        Assert.AreEqual(-360f, check.totalTurnDegrees, 0.5f);
    }

    [Test]
    public void PaperclipHasTwoCornersAndTwoStraights()
    {
        var segments = OvalGeometry.Build(ShortTrack());
        int turns = 0, straights = 0;
        foreach (var seg in segments) { if (seg.isTurn) turns++; else straights++; }
        Assert.AreEqual(2, turns, "a paperclip has one 180 at each end");
        Assert.AreEqual(2, straights);
    }

    [Test]
    public void TriOvalAddsADogLegThatNetsToZero()
    {
        var segments = OvalGeometry.Build(Superspeedway());
        float frontAngle = 0f, frontLength = 0f;
        int frontPieces = 0;
        foreach (var seg in segments)
        {
            string label = seg.label ?? "";
            if (!label.StartsWith("Front Stretch") && label != "Tri-Oval") continue;
            frontAngle += seg.angle;
            frontLength += seg.length;
            frontPieces++;
        }
        Assert.AreEqual(5, frontPieces, "taper out, leg, bend, leg, taper in");
        Assert.AreEqual(0f, frontAngle, 0.01f, "the dog-leg must not steal heading from the corners");

        // The bow is what makes the front stretch longer than the back — that asymmetry is solved for,
        // never authored, so check it actually came out.
        float backLength = 0f;
        foreach (var seg in segments) if (seg.label == "Back Stretch") backLength = seg.length;
        Assert.Greater(frontLength, backLength, "a tri-oval front stretch covers more ground than the back");
    }

    [Test]
    public void AnOvalWithNoDogLegHasEqualStraights()
    {
        float front = 0f, back = 0f;
        foreach (var seg in OvalGeometry.Build(Intermediate()))
        {
            if (seg.label == "Front Stretch") front = seg.length;
            else if (seg.label == "Back Stretch") back = seg.length;
        }
        Assert.Greater(front, 0f);
        // Two straights joined by two semicircular ends can only close when they match — the solver has to
        // land on that, whatever the spec asked for.
        Assert.AreEqual(front, back, 1f);
    }

    [Test]
    public void RacingLineStaysOnTheRoad()
    {
        var spec = Superspeedway();
        float half = spec.roadWidth * 0.5f;

        foreach (var seg in OvalGeometry.Build(spec))
        {
            var l = seg.line;
            foreach (float offset in new[] { l.idealEntry, l.idealApex, l.idealExit,
                                             l.leftEntry, l.leftApex, l.leftExit,
                                             l.rightEntry, l.rightApex, l.rightExit })
                Assert.LessOrEqual(Mathf.Abs(offset), half, $"{seg.label}: line offset {offset} is off the road");
        }
    }

    [Test]
    public void ApexIsOnTheInsideOfTheCorner()
    {
        foreach (var seg in OvalGeometry.Build(Superspeedway()))
        {
            // Real corners only; the shallow tri-oval legs are excluded by the angle test.
            if (!seg.isTurn || seg.angle < 45f) continue;
            Assert.Less(seg.line.idealApex, 0f, $"{seg.label}: a left-hand apex sits left of centre");
            Assert.Greater(seg.line.idealEntry, 0f, $"{seg.label}: entry should be out by the wall");
        }
    }

    [Test]
    public void PitLaneIsBuiltAlongsideTheFrontStretch()
    {
        var spec = Superspeedway();
        float front = OvalGeometry.FrontStretchLength(spec);
        var lane = OvalGeometry.BuildPitLane(spec, front);

        Assert.AreEqual(3, lane.Count, "entry taper, pit road, exit taper");
        Assert.Less(lane[0].angle * lane[2].angle, 0f, "the tapers turn opposite ways, in and back out");
        Assert.Greater(lane[1].length, front * 0.5f, "pit road runs most of the front stretch");

        float exitLine = OvalGeometry.PitExitLineDistance(spec, front);
        float laneLength = lane[0].length + lane[1].length + lane[2].length;
        Assert.Greater(exitLine, 0f);
        Assert.Less(exitLine, laneLength, "the limiter releases before the end of the lane");
    }

    [Test]
    public void PitLaneCanBeTurnedOff()
    {
        var spec = ShortTrack();
        spec.pitLane = false;
        Assert.AreEqual(0, OvalGeometry.BuildPitLane(spec, 400f).Count);
    }

    [Test]
    public void BankingRaisesTheGeneratedCornerSpeed()
    {
        int flat = OvalGeometry.CornerSpeedMph(400f, 90f, 0f);
        int banked = OvalGeometry.CornerSpeedMph(400f, 90f, 31f);
        Assert.Greater(banked, flat, "31 degrees of banking is worth a lot of corner speed");
    }

    [Test]
    public void TighterCornersAreSlower()
    {
        int bullring = OvalGeometry.CornerSpeedMph(180f, 180f, 12f);
        int sweeper = OvalGeometry.CornerSpeedMph(900f, 90f, 12f);
        Assert.Less(bullring, sweeper);
    }

    [Test]
    public void PresetsMatchTheTrackTheyName()
    {
        var daytona = Superspeedway();
        Assert.Greater(daytona.frontKinkDegrees, 0f, "Daytona is a tri-oval");
        Assert.AreEqual(4, daytona.corners);

        var martinsville = ShortTrack();
        Assert.AreEqual(2, martinsville.corners, "Martinsville is a paperclip");
        Assert.AreEqual(0f, martinsville.frontKinkDegrees, 1e-3f);
        Assert.Less(martinsville.roadWidth, daytona.roadWidth, "a bullring is a narrower road");
    }

    [Test]
    public void TuningSeparatesTheTrackTypes()
    {
        var superspeedway = TrackTuning.For(TrackKind.Superspeedway);
        var shortTrack = TrackTuning.For(TrackKind.ShortTrack);

        Assert.Greater(superspeedway.draftScale, shortTrack.draftScale, "the draft is the superspeedway race");
        Assert.Greater(shortTrack.tyreWearScale, superspeedway.tyreWearScale, "bullrings eat tyres");
        Assert.Greater(superspeedway.roadWidth, shortTrack.roadWidth);
        Assert.Greater(superspeedway.racingZoom, shortTrack.racingZoom);
    }

    [Test]
    public void PerTrackOverridesLayerOnTopOfTheType()
    {
        Assert.Greater(TrackTuning.ForTrack("Talladega", TrackKind.Superspeedway).draftScale,
                       TrackTuning.ForTrack("Daytona", TrackKind.Superspeedway).draftScale);
        Assert.Greater(TrackTuning.ForTrack("Bristol", TrackKind.ShortTrack).tyreWearScale,
                       TrackTuning.ForTrack("Martinsville", TrackKind.ShortTrack).tyreWearScale);
        // An unlisted track just gets its type's numbers.
        Assert.AreEqual(TrackTuning.For(TrackKind.Speedway).draftScale,
                        TrackTuning.ForTrack("Kansas", TrackKind.Speedway).draftScale, 1e-4f);
    }
}
