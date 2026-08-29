using System.Collections.Generic;
using Draftmaster.Tracks;
using UnityEngine;

// Turns an authored road-course layout into a drivable TrackInfoV2 — the road-course counterpart of
// OvalTrackFactory, and deliberately the same shape so both paths behave identically downstream.
//
// The geometry is solved in Draftmaster.Tracks.RoadCourseGeometry (own assembly, unit tested); this is
// the adapter that speaks the game's asset types.
//
// WatkinsGlen is never built through here. It was measured off satellite imagery by hand and is the
// reference the generated circuits are trying to be — Build() refuses it outright rather than trusting a
// caller to remember.
public static class RoadCourseFactory
{
    public const float MetresPerMile = RoadCourseGeometry.MetresPerMile;

    public static bool CanBuild(string trackId) => RoadCourseLayouts.Has(trackId);

    public static TrackInfoV2 Build(RoadCourseSpec spec)
    {
        var track = ScriptableObject.CreateInstance<TrackInfoV2>();
        track.name = spec != null ? spec.trackId : "NewCircuit";
        Populate(track, spec);
        return track;
    }

    // Fills an existing asset in place so regenerating keeps its GUID, and with it every scene, package
    // and calendar reference already pointing at the track.
    public static void Populate(TrackInfoV2 track, RoadCourseSpec spec)
    {
        if (track == null || spec == null) return;

        var solved = RoadCourseGeometry.Solve(spec);
        var segments = new TrackInfoV2.TrackSegment[solved.Count];
        for (int i = 0; i < solved.Count; i++) segments[i] = ToAssetSegment(solved[i], spec);

        track.trackName = string.IsNullOrEmpty(spec.displayName) ? spec.trackId : spec.displayName;
        track.trackLaps = Mathf.Max(1, spec.defaultLaps);
        track.topSpeed = spec.topSpeedMph;
        track.startPosition = Vector2.zero;
        track.startHeading = 0f;
        track.startFinishDistance = 0f;
        track.defaultWidth = spec.roadWidth;
        track.closedLoop = true;

        // A road course is corners, not long sweeps: sample it far more finely than an oval or the
        // hairpins come out faceted.
        track.samplesPerSegment = 10;
        track.maxArcStepMetres = 2f;
        track.segments = segments;

        track.drawEdgeLines = true;
        track.drawLeftEdgeLine = true;
        track.drawRightEdgeLine = true;
        track.edgeLineWidth = 0.15f;
        track.edgeLineInset = 0.075f;

        BuildPitLane(track, spec, solved);
    }

    static TrackInfoV2.TrackSegment ToAssetSegment(RoadPiece piece, RoadCourseSpec spec)
    {
        bool isTurn = piece.IsTurn;
        var line = RoadCourseGeometry.LineFor(piece, spec.roadWidth, spec.lineMargin);

        // Braking and turn-in happen before the apex; the lead-in/out is what SplineDriver reads to know
        // how far ahead to start slowing. Scale it with the corner rather than fixing it.
        float lead = isTurn ? Mathf.Clamp(piece.length * 0.4f, 10f, 90f) : 0f;

        return new TrackInfoV2.TrackSegment
        {
            label = piece.label,
            type = isTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
            length = piece.length,
            angle = piece.angle,
            banking = piece.banking,
            leadIn = lead,
            leadOut = lead,
            maxSpeed = RoadCourseGeometry.CornerSpeedMph(piece, spec.topSpeedMph),
            width = 0f,      // inherit the track default
            racingLine = new TrackInfoV2.SegmentRacingLine
            {
                idealEntry = line.idealEntry,
                idealApex = line.idealApex,
                idealExit = line.idealExit,
                leftEntry = line.leftEntry,
                leftApex = line.leftApex,
                leftExit = line.leftExit,
                rightEntry = line.rightEntry,
                rightApex = line.rightApex,
                rightExit = line.rightExit,
            },
        };
    }

    // Pit road runs alongside the piece that opens the lap — the pit straight. It leaves at the end of
    // the final segment and rejoins at the end of segment 0, the same wiring the ovals use.
    static void BuildPitLane(TrackInfoV2 track, RoadCourseSpec spec, List<RoadPiece> solved)
    {
        track.hasPitLane = spec.pitLane;
        if (!spec.pitLane || solved.Count == 0)
        {
            track.pitSegments = new TrackInfoV2.TrackSegment[0];
            return;
        }

        float pitStraight = solved[0].length;
        var lane = RoadCourseGeometry.BuildPitLane(spec, pitStraight);
        var pitSegments = new TrackInfoV2.TrackSegment[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var piece = lane[i];
            pitSegments[i] = new TrackInfoV2.TrackSegment
            {
                label = piece.label,
                type = piece.IsTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
                length = piece.length,
                angle = piece.angle,
                maxSpeed = spec.pitSpeedLimitMph,
                width = RoadCourseGeometry.PitWidth(spec),
            };
        }

        track.pitSegments = pitSegments;
        track.pitDefaultWidth = RoadCourseGeometry.PitWidth(spec);
        track.pitSpeedLimit = spec.pitSpeedLimitMph;
        track.pitStartHeadingOffset = 0f;
        track.pitEntrySegmentIndex = Mathf.Max(0, solved.Count - 1);
        track.pitExitSegmentIndex = 0;
        track.pitEntryOffset = 0f;
        track.pitExitOffset = 0f;
        track.pitExitLineDistance = RoadCourseGeometry.PitExitLineDistance(spec, pitStraight);
        track.RebakePitDistances();   // OnValidate does not fire on an asset built in code
    }

    // Sanity report for a built asset, in the same terms RoadCourseGeometry checks a solve.
    public static RoadCheck Validate(TrackInfoV2 track)
    {
        var check = new RoadCheck { tightestLinkRadius = float.PositiveInfinity };
        if (track == null || track.segments == null) return check;

        check.lapMetres = track.TotalLength();
        check.lapMiles = check.lapMetres / MetresPerMile;
        for (int i = 0; i < track.segments.Length; i++)
        {
            check.totalTurnDegrees += track.segments[i].angle;
            if (track.segments[i].type == TrackInfoV2.SegmentType.Turn) check.namedCorners++;
        }

        track.SampleAuthoredSpline(check.lapMetres, out var end, out _);
        check.closureErrorMetres = Vector2.Distance(end, track.startPosition);
        return check;
    }
}
