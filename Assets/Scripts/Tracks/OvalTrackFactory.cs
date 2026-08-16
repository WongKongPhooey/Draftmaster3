using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Tracks;
using UnityEngine;

// Turns a catalogue row into a drivable TrackInfoV2.
//
// The geometry itself is solved in Draftmaster.Tracks.OvalGeometry (its own assembly, unit tested); this is
// the adapter that speaks the game's asset types — it maps solved segments onto TrackInfoV2's segment
// struct, wires the pit lane onto real segment indices, and bakes the derived distances.
//
// Road courses are not generated: there is no formula for the Bus Stop. Author those by hand.
public static class OvalTrackFactory
{
    public const float MetresPerMile = OvalGeometry.MetresPerMile;

    // Build a spec straight from a catalogue row — the path that scales to a full calendar: every oval in
    // the Tracks table can be generated without anyone typing a number twice.
    public static OvalSpec FromCatalogue(Track row)
    {
        if (row == null) return new OvalSpec();
        return OvalGeometry.Preset(TrackProfile.KindOf(row.Type), row.Name, row.DisplayName,
                                   row.LengthMiles, row.BankingDegrees, row.DefaultLaps);
    }

    public static TrackInfoV2 Build(OvalSpec spec)
    {
        var track = ScriptableObject.CreateInstance<TrackInfoV2>();
        track.name = spec != null ? spec.trackId : "NewOval";
        Populate(track, spec);
        return track;
    }

    // Fills an existing asset in place, so regenerating keeps its GUID — and every scene, package and
    // calendar reference pointing at it.
    public static void Populate(TrackInfoV2 track, OvalSpec spec)
    {
        if (track == null) return;
        if (spec == null) spec = new OvalSpec();

        var solved = OvalGeometry.Build(spec);
        var segments = new TrackInfoV2.TrackSegment[solved.Count];
        for (int i = 0; i < solved.Count; i++) segments[i] = ToAssetSegment(solved[i]);

        track.trackName = string.IsNullOrEmpty(spec.displayName) ? spec.trackId : spec.displayName;
        track.trackLaps = Mathf.Max(1, spec.defaultLaps);
        track.topSpeed = spec.topSpeedMph;
        track.startPosition = Vector2.zero;
        track.startHeading = 0f;
        track.startFinishDistance = 0f;
        track.defaultWidth = spec.roadWidth;
        track.closedLoop = true;
        track.samplesPerSegment = 8;
        track.maxArcStepMetres = 4f;      // ovals are long sweeps; no need for a sample every 2 m
        track.segments = segments;

        track.drawEdgeLines = true;
        track.drawLeftEdgeLine = true;
        track.drawRightEdgeLine = true;
        track.edgeLineWidth = 0.15f;
        track.edgeLineInset = 0.075f;

        BuildPitLane(track, spec, solved);
    }

    static TrackInfoV2.TrackSegment ToAssetSegment(OvalSegment s)
    {
        return new TrackInfoV2.TrackSegment
        {
            label = s.label,
            type = s.isTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
            length = s.length,
            angle = s.angle,
            banking = s.banking,
            leadIn = s.leadIn,
            leadOut = s.leadOut,
            maxSpeed = s.maxSpeedMph,
            width = s.width,
            racingLine = new TrackInfoV2.SegmentRacingLine
            {
                idealEntry = s.line.idealEntry,
                idealApex = s.line.idealApex,
                idealExit = s.line.idealExit,
                leftEntry = s.line.leftEntry,
                leftApex = s.line.leftApex,
                leftExit = s.line.leftExit,
                rightEntry = s.line.rightEntry,
                rightApex = s.line.rightApex,
                rightExit = s.line.rightExit,
            },
        };
    }

    // Pit road runs alongside the front stretch: it leaves at the exit of the final corner and rejoins at
    // the end of the front stretch, which for a tri-oval is the last of its three pieces.
    static void BuildPitLane(TrackInfoV2 track, OvalSpec spec, List<OvalSegment> solved)
    {
        track.hasPitLane = spec.pitLane;
        if (!spec.pitLane || solved.Count == 0)
        {
            track.pitSegments = new TrackInfoV2.TrackSegment[0];
            return;
        }

        float frontLength = 0f;
        int frontLast = OvalGeometry.FrontStretchLastIndex(solved);
        for (int i = 0; i <= frontLast && i < solved.Count; i++) frontLength += solved[i].length;

        var lane = OvalGeometry.BuildPitLane(spec, frontLength);
        var pitSegments = new TrackInfoV2.TrackSegment[lane.Count];
        for (int i = 0; i < lane.Count; i++) pitSegments[i] = ToAssetSegment(lane[i]);

        track.pitSegments = pitSegments;
        track.pitDefaultWidth = OvalGeometry.PitWidth(spec);
        track.pitSpeedLimit = spec.pitSpeedLimitMph;
        track.pitStartHeadingOffset = 0f;
        track.pitEntrySegmentIndex = Mathf.Max(0, solved.Count - 1);   // exit of the final corner
        track.pitExitSegmentIndex = frontLast;                          // end of the front stretch
        track.pitEntryOffset = 0f;
        track.pitExitOffset = 0f;
        track.pitExitLineDistance = OvalGeometry.PitExitLineDistance(spec, frontLength);
        track.RebakePitDistances();   // OnValidate doesn't fire on an asset built in code
    }

    // Sanity report for a built asset, in the same terms OvalGeometry checks a solve.
    public static OvalCheck Validate(TrackInfoV2 track)
    {
        var check = new OvalCheck();
        if (track == null || track.segments == null) return check;

        check.lapMetres = track.TotalLength();
        check.lapMiles = check.lapMetres / MetresPerMile;
        for (int i = 0; i < track.segments.Length; i++) check.totalTurnDegrees += track.segments[i].angle;

        track.SampleAuthoredSpline(check.lapMetres, out var end, out _);
        check.closureErrorMetres = Vector2.Distance(end, track.startPosition);
        check.hasPitLane = track.hasPitLane && track.pitSegments != null && track.pitSegments.Length > 0;
        return check;
    }
}
