using System.Collections.Generic;
using UnityEngine;

public class SplineDriver : MonoBehaviour, IVehicleSpeedReadout, ICollisionResponder, IFormationMember
{
    public float SpeedMps => speed;

    Vector2 _lastWorldRight = Vector2.right;
    Vector2 _lastWorldForward = Vector2.right;
    float _collisionLateral;
    float _collisionLongitudinal;
    bool _contactHold;

    public float Mass => vehicleInfo != null ? vehicleInfo.mass : 1500f;

    public void ApplyContact(Vector2 worldMtv, Vector2 contactPoint, float severity)
    {
        // Spline cars stay glued to the path; the lever arm can't spin them freely. contactPoint is ignored here.
        // Persist the push as offsets along BOTH spline axes (transform is rebuilt from the spline each frame).
        // The longitudinal offset is what unsticks cars overlapping nose-to-tail — the old code dropped it entirely.
        _collisionLateral = Mathf.Clamp(_collisionLateral + Vector2.Dot(worldMtv, _lastWorldRight), -8f, 8f);
        _collisionLongitudinal = Mathf.Clamp(_collisionLongitudinal + Vector2.Dot(worldMtv, _lastWorldForward), -8f, 8f);
        _contactHold = true; // hold the offsets through the next Place (skip one decay step) so contact separates

        // Scrub speed only by how head-on the hit is. Glancing scrapes barely slow; square-on loses more. Kept GENTLE
        // (contactSpeedScrub) — a hard scrub on a nose-to-tail tap in a tight pack made the tapped car lurch slow,
        // the car behind brake to match, and the wave amplified into a pile-up. A soft touch barely bleeds speed.
        Vector2 n = worldMtv.sqrMagnitude > 1e-6f ? worldMtv.normalized : Vector2.zero;
        float headOn = Mathf.Abs(Vector2.Dot(_lastWorldForward, n)); // 0 = parallel glance, 1 = square
        _currentMph *= Mathf.Clamp01(1f - severity * headOn * contactSpeedScrub);
    }

    // Spline cars are glued to the path, so momentum transfer doesn't apply — just separate and scrub speed
    // the same way a barrier contact does. (During racing the AI run the dynamic model, which gets the full
    // 2-body impulse via PlayerVehicleController.ApplyCarImpact instead.)
    public void ApplyCarImpact(Vector2 worldMtv, Vector2 contactPoint, Vector2 worldDeltaV, float severity)
        => ApplyContact(worldMtv, contactPoint, severity);

    // Glued to the path: a hit never turns it.
    public float YawInertia => float.PositiveInfinity;

    public TrackBuilder track;
    public VehicleInfo vehicleInfo;
    [Tooltip("Scales the target speed at every point on track. <1 slows the car down, >1 speeds it up. Driver stats (qualifying/consistency) feed this.")]
    [Range(0.5f, 1.2f)]
    public float paceMultiplier = 1f;
    [Tooltip("Racing-line variant: -1 = leftmost line, 0 = ideal, +1 = rightmost line. Anything in between blends. Used as seed for the smoothed line.")]
    [Range(-1f, 1f)]
    public float lineFactor = 0f;
    [Tooltip("Use this track's TRAINED racing line as the ideal, when one exists (Resources/RacingLines/<track>.json, written by Draftmaster > AI > Train Racing Lines). That line is what a few hundred simulated laps settled on: late apexes, straight-line braking, the exit opened up — quicker AND more natural than the authored arc. lineFactor still blends off it toward the leftmost/rightmost lines. Off falls back to the authored ideal plus minimum-curvature relaxation.")]
    public bool useTrainedLine = true;

    /// True when Rebuild found a trained line for this track and is driving it.
    public bool TrainedLineInUse { get; private set; }
    /// True when Rebuild found a trained line that fits the road as built, driven or not (a player's lap gives way to
    /// the optimised line on a road course - see optimiseLine).
    public bool TrainedLineFound { get; private set; }

    [Header("Racing Line Smoothing")]
    [Tooltip("How many Gauss-Seidel passes to relax the line toward minimum curvature. 0 = follow authored ideal exactly (rigid). 30-80 = realistic smoothed line.")]
    [Range(0, 200)]
    public int smoothingIterations = 60;
    [Tooltip("Relaxation passes applied to a TRAINED racing line — just enough to round off a kink where the line is clamped to a road narrower than the one it was trained on, without flattening its apexes.")]
    public int trainedLineSmoothingPasses = 30;
    [Tooltip("A racing line copied from a player's lap carries the legal corridor it was driven in (road, kerb, tarmac run-off). On: the car follows that line onto the run-off. Off: the line is kept on the road. Before this switch the line used the run-off but the car was held on the road, so wherever the two parted (Watkins Glen's Turn 1 exit, 11 m) every car was pinned to the road edge at the speed of the wider line, slid and ran into the one ahead.")]
    public bool lineUsesRunOff = false;
    [Tooltip("Where the AI does not race in lanes (road courses), the car may use the whole road, edge to edge less RoadEdgeMargin, and not only the band between the authored leftmost and rightmost lines. Those lines are where lineFactor spreads the field's lines, not the road: at Watkins Glen they held every car to the left 5 m of the 14 m front straight - nobody used the inside into Turn 1 but the player - and to 3.6 m of Turn 1's 10 m exit, where a field coming out two-wide was squeezed single file.")]
    public bool roadCourseFullWidth = true;
    [Tooltip("On an oval whose authored leftmost / rightmost lines pinch (somewhere narrower than 40% of the road - the traced ovals: Michigan, Bristol, Dover, Phoenix, Gateway, Iowa, Bowman Gray), the AI's bounds are the road edge to edge less RoadEdgeMargin. Off = the authored band, which squeezed every car onto the centreline for a quarter of the lap: at Michigan to 1.2 m of 19.5 m mid-Turn 2, a 75 m kink in the line where the field braked to 87 mph and ran into each other.")]
    public bool ovalFullWidth = true;
    [Tooltip("On such a pinched oval, drive the road's minimum-curvature line as the ideal in place of the trained one. Off: quicker (Michigan 41.5 s against 44-45) but it swings across the lanes, and the field touched more (16 cars x 4 laps, two rosters, Dover HARD 1-4 -> 8-10, Phoenix 1 -> 6-7). With the band widened, the trained line races cleanly: passes pinched -> wide Bristol 6-9 -> 25-36, Dover 17-19 -> 51-76, Phoenix 2-5 -> 20-25, Michigan 1-6 -> 21-25.")]
    public bool optimisePinchedOvals = false;
    // The authored band pinches somewhere on this oval (see ovalFullWidth).
    public bool BoundsPinched { get; private set; }
    [Tooltip("On a road course with no trained line (or only a player's recorded lap), drive the road's minimum-curvature line (Draftmaster.Sim.MinCurvatureLine) as the ideal: in from the outside, the inside edge at the apex, out to the outside edge on the exit, edge to edge less RoadEdgeMargin. Off = the authored ideal with its lateral offset smoothed, which follows the centreline's arc and apexed metres wide (Watkins Glen authored apexes average 2.5 m off the inside edge).")]
    public bool optimiseLine = true;
    [Tooltip("On an oval raced in lanes (not a plate pack) at least this long (m), drive the road's minimum-curvature line as the ideal in place of the trained one. Off (huge) by default. Alone it is quicker on the mile-and-up ovals (solo, 2026-11-01: Michigan -2.9 s, Pocono -1.8, Phoenix -1.1, Atlanta -0.9, Charlotte -0.6), but in a field it swings 7 m across the lanes the pack races in: Atlanta passes 15-34 -> 5-6 and hard contacts 0-1 -> 3-6, Charlotte the same way. Short tracks: the trained lines are quicker anyway.")]
    public float optimiseOvalFromMetres = float.MaxValue;
    [Tooltip("How far (m) past the road edge a car avoiding a wreck may go onto the paved run-off, where the track's player-lap line records it (TrainedRacingLine corridor). Only while AIRacingBehaviour sets allowRunOff.")]
    public float runOffEscapeMetres = 8f;
    // Set each step by AIRacingBehaviour while it is driving round a stopped car: the bounds take in the run-off.
    [System.NonSerialized] public bool allowRunOff;
    [Tooltip("Per-pass relaxation factor. 0 = no movement, 1 = full averaging. ~0.3 is stable.")]
    [Range(0f, 1f)]
    public float smoothingRelaxation = 0.3f;
    [Tooltip("Current speed in metres per second. Driven by the simulation when vehicleInfo is assigned; otherwise treated as a constant.")]
    public float speed = 40f;
    [Tooltip("Distance along the spline to spawn at, in metres.")]
    public float startDistance = 0f;
    [Tooltip("Lateral offset from centerline, in metres. Applied on top of any racing-line offset.")]
    public float lateralOffset = 0f;
    [Tooltip("Loop back to the start when the lap completes.")]
    public bool loop = true;
    [Tooltip("Sprite faces +Y by default. Set false if your sprite faces +X.")]
    public bool spriteFacesUp = true;
    [Tooltip("Extra rotation applied to the sprite, in degrees.")]
    public float angleOffsetDeg = 0f;
    [Tooltip("Drive on the pit lane spline instead of the main spline.")]
    public bool usePitLane = false;

    [Header("Pit Grid Spawn")]
    [Tooltip("If true, spawn car in pit lane at qualifyingPosition's pit box.")]
    public bool spawnInPit = false;
    [Tooltip("Grid index. 0 = pole (closest to pit exit). Used to place car in pit box.")]
    public int qualifyingPosition = 0;
    [Tooltip("Distance (m) between pit boxes along pit lane.")]
    public float pitBoxSpacing = 12f;
    [Tooltip("Distance (m) from end of pit lane to the pole-sitter's pit box.")]
    public float pitBoxExitGap = 20f;
    [Tooltip("How much of the pit-lane length the cars actually need to traverse before merging back onto track (sets the auto-exit threshold).")]
    [Range(0.5f, 1f)] public float pitExitThreshold = 0.98f;
    [Tooltip("When on the pit lane and past pitExitThreshold, hop back onto the main spline. Disable for a safety car that drives INTO the pit to park.")]
    public bool autoPitExit = true;
    [Tooltip("On an AI takeover (EngageFromCurrentPose), the car engages on the PIT lane instead of the racing line if it's within this many metres of the pit centerline. Stops a handover in the pit driving through the pit wall onto the track.")]
    public float pitEngageLateralMax = 6f;
    [Tooltip("After rejoining the main spline at pit exit, how fast (m/s) the car eases from its pit-exit line onto the racing line. Stops the merge being an instant sideways pop.")]
    public float pitMergeEaseSpeed = 2.5f;
    [Tooltip("How fast (deg/sec) the car's heading eases from its pit-exit facing onto the main-spline tangent. The pit exit isn't geometrically locked to the main spline, so without this the heading SNAPS on rejoin (a visible rotation pop). Lower = longer, smoother turn-in.")]
    public float pitMergeHeadingEaseDegPerSec = 25f;

    [Header("Formation Start")]
    [Tooltip("Hold this car perfectly still (parked in its box / at pit exit) while RaceStart.Current is PreGrid. Released automatically once the phase advances to Formation.")]
    public bool freezeUntilFormation = false;
    [Tooltip("While on the pit lane, hold the car stopped (a service stop). Set by PitStopController at the pit box.")]
    public bool pitStopHold = false;
    [Tooltip("Distance (m) along the pit lane to hard-park at (e.g. the safety car parking near the entrance). -1 = no park; the car drives the pit normally. Once reached the car is pinned here and stopped, regardless of decel/vehicleInfo.")]
    public float pitParkDistance = -1f;
    [Tooltip("Hard-park pin, independent of the race phase (practice cars waiting in their box). Zero speed, transform pinned to the commanded point until cleared.")]
    public bool parkedHold = false;
    [Tooltip("Speed (m/s) a car that has just pulled up in its box settles onto its exact parked spot. The hold starts from wherever the car actually stopped and eases the last bit, rather than jumping onto the spot in one frame.")]
    public float holdSettleSpeed = 1.2f;
    [Tooltip("Rate (deg/s) a car that has just pulled up in its box straightens onto its parked heading.")]
    public float holdSettleDegPerSec = 35f;

    // True whenever the car is being held stationary — either the pre-grid freeze or an explicit park
    // (practice cars in their box). SplineInputDriver keeps re-seeding the dynamic model while this holds,
    // otherwise the model integrates freely against a zero speed command and the car creeps out of its box.
    public bool IsHeldStill => parkedHold || (freezeUntilFormation && RaceStart.Current == RaceStart.Phase.PreGrid);

    public bool IsOnPit => _onPit;
    public float PitProgress01 => (_onPit && _pitLength > 0f) ? Mathf.Clamp01(_distance / _pitLength) : 0f;
    public float PitLength => _pitLength;

    // Called every tick by a controller that has called this car in, until it is on the lane. The car brakes so it
    // reaches the entry node at the pit limit (the cap only bites once the node is inside its braking distance),
    // and over the last `lateralLeadM` eases across toward the side the lane peels off to. Without it the car ran the racing line flat out off
    // the last corner, committed 25 m from the node, and then had to shed 130 mph and dive across the track on
    // the lane itself — they were barely making the entry.
    public void ApproachPitEntry(float decelMps2, float lateralLeadM)
    {
        if (_onPit || track == null || track.track == null || _mainLength <= 0f) { ClearPitApproach(); return; }

        float gap = track.track.PitEntryDistanceOnLap - DistanceOnTrack;
        if (gap < 0f) gap += _mainLength;
        // Down on the apron and past the node, still running to where pit road comes down to meet it.
        if (gap > _mainLength * 0.5f && _apronEntryActive && ApronGapToPitJoin() > -5f) gap = 0f;
        if (gap > _mainLength * 0.5f) { ClearPitApproach(); return; }

        // v² = v_pit² + 2·a·d: the fastest the car can be going here and still be at the limit at the node.
        float pitMps = (track.track.pitSpeedLimit > 0f ? track.track.pitSpeedLimit : 50f) * MphToMps;
        float capMps = Mathf.Sqrt(pitMps * pitMps + 2f * Mathf.Max(0.5f, decelMps2) * gap);
        _pitApproachCapMph = capMps / MphToMps;

        // An oval of a mile or more: get down onto the apron first, then slow there (OvalPitApron).
        if (ApronPitRoad) { ApproachOnApron(gap, pitMps, decelMps2); return; }

        if (gap > lateralLeadM) { _pitApproachBlend = 0f; return; }
        if (_pitSide == 0f) _pitSide = MeasurePitSide();
        // Half-way to the pit-side edge (the bounds clamp keeps it on the tarmac): enough to be on the lane's
        // side of the track at the node, not so far that the car overshoots a lane that starts mid-track.
        var at = track.SampleAt(DistanceOnTrack, _mainSamples);
        _pitApproachLateral = _pitSide * at.width * 0.3f;
        float t = 1f - gap / Mathf.Max(1f, lateralLeadM);
        _pitApproachBlend = t * t * (3f - 2f * t);
    }

    public void ClearPitApproach()
    {
        _pitApproachCapMph = float.MaxValue;
        _pitApproachBlend = 0f;
        _apronEntryActive = false;
    }

    // ---------------------------------------------------------------- oval pit apron
    //
    // On an oval of a mile or more (OvalPitApron.Applies) pit road is reached along the apron. Coming in, the car
    // drops below the racing surface BEFORE it starts braking, so it never slows in the groove with the field
    // behind it at full speed. Going out, it leaves pit road where the lane comes up level with the apron and
    // runs the apron through the next turns, coming back up onto the racing surface only on the back straight,
    // and only when nothing is coming. Pit road at Daytona rejoined the main spline on its centreline 220 m
    // short of turn 1 - every car leaving the pits popped out in the middle of the pack.

    [Header("Oval Pit Apron")]
    [Tooltip("On an oval with a paved apron, how far (m) below the inside edge of the racing surface a car pitting runs - its centre, so 1.8 puts a 2 m car fully on a 3.5 m apron.")]
    public float apronCarOffset = 1.8f;
    [Tooltip("On an oval WITHOUT a paved apron, how far (m) in from the inside edge of the road the pitting car's centre runs - as low as it can go without wheels on the grass.")]
    public float apronEdgeInset = 1.2f;
    [Tooltip("Lateral speed (m/s) a car coming in to pit moves down across the track to the apron.")]
    public float apronDropRate = 3f;
    [Tooltip("Extra run (m) on the apron before braking for pit road starts, so the car is settled down there first.")]
    public float apronSettleM = 40f;
    [Tooltip("Share of the move per second (0-1 of the blend) a car leaving the pits comes back up onto the racing line once it is on the back straight and clear.")]
    public float apronMergeBlendPerSec = 0.3f;
    [Tooltip("Longest (m) a car leaving the pits waits on the apron past the start of the back straight for a gap before it merges anyway.")]
    public float apronMergeMaxWaitM = 500f;
    [Tooltip("Seconds of closing speed a car coming from behind is given before the merge back up onto the racing surface is called clear.")]
    public float apronMergeLookSec = 3f;

    int _apronApplies = -1;          // -1 not resolved yet; 0 no; 1 this track uses the apron for pit road
    bool _apronGeometryReady;
    float _apronInsideSide;          // +1 / -1: which side of the track the infield (and the apron) is on
    // +1 / -1: which side of the track (lateral sign) the infield is on - the way the lap turns.
    public float InfieldSide { get { EnsureApronGeometry(); return _apronInsideSide; } }
    bool _apronPaved;                // a paved apron below the racing surface (else the lane hugs the road edge)
    float _apronPitCutover = -1f;    // pit-lane distance a car leaving runs off onto the apron (-1 = no apron exit)
    float _apronBackStraight = -1f;  // main distance the back straight starts at, after the pit exit
    float _apronCutInPastNode = -1f; // main metres past the entry node where pit road meets the apron (-1 = at the node)
    bool _apronEntryActive;
    float _apronEntryLat;            // the commanded lateral on the drop to the apron, moved at apronDropRate
    float _apronExitBlend;           // 1 = on the apron after pit exit, eased to 0 to merge back up
    bool _apronExitMerging;
    float _apronExitToGo;            // metres still to run to the back straight
    float _apronExitWaited;          // metres run past the start of the back straight waiting for a gap

    // True when this track sends pitting cars along the apron (an oval of a mile or more).
    public bool ApronPitRoad
    {
        get
        {
            if (_apronApplies < 0)
            {
                string id = track != null && track.track != null ? track.track.name.Replace("(Clone)", "").Trim() : null;
                _apronApplies = Draftmaster.Tracks.OvalPitApron.AppliesTo(id) ? 1 : 0;
            }
            return _apronApplies == 1;
        }
    }

    // The lateral a pitting car runs at on the apron, at a main-spline distance.
    float ApronLaneAt(float distance)
    {
        EnsureApronGeometry();
        var s = track.SampleAt(distance, _mainSamples);
        return Draftmaster.Tracks.OvalPitApron.LaneLateral(_apronInsideSide, s.width, _apronPaved, apronCarOffset, apronEdgeInset);
    }

    // Resolved on first use rather than in Rebuild: whether the apron is paved is read off the surface field,
    // which the track's environment builder may not have filled yet when the car is built.
    void EnsureApronGeometry()
    {
        if (_apronGeometryReady || track == null || _mainSamples == null || _mainSamples.Count < 2) return;
        _apronGeometryReady = true;

        // The infield side: whichever way the lap turns, weighted by how hard. An oval turns 360 degrees one way.
        float sum = 0f;
        if (_curvatureProfile != null && _insideSideProfile != null)
            for (int i = 0; i < _curvatureProfile.Length && i < _insideSideProfile.Length; i++)
                sum += _curvatureProfile[i] * _insideSideProfile[i];
        _apronInsideSide = sum > 0f ? 1f : -1f;

        // Paved apron: probe below the inside edge at a few points round the lap.
        int paved = 0, probes = 0;
        for (int k = 0; k < 8; k++)
        {
            var s = track.SampleAt(_mainLength * k / 8f, _mainSamples);
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            Vector2 local = s.position + right * (_apronInsideSide * (s.width * 0.5f + apronCarOffset));
            Vector3 world = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            probes++;
            if (SurfaceField.TryGetSurface(world, out var surf) && surf == TrackEnvironment.SurfaceType.TarmacRunoff) paved++;
        }
        _apronPaved = probes > 0 && paved * 2 > probes;

        _apronPitCutover = -1f;
        _apronBackStraight = -1f;
        _apronCutInPastNode = -1f;
        if (_pitSamples == null || _pitSamples.Count < 2 || _pitLength <= 0f || track.track == null) return;

        // Pit entry off the apron: the first point where pit road has gone down level with the apron lane. A car
        // on the apron joins pit road there, where it already is, instead of at the entry node out on the
        // centreline. A lane that never gets down there (it runs along the racing surface) is joined at the node.
        float entry = track.track.PitEntryDistanceOnLap;
        for (float pd = 0f; pd <= _pitLength * 0.5f; pd += 2f)
        {
            if (!PitLaneAgainstMain(pd, out float md, out float lat, out float lane)) break;
            if (lat * _apronInsideSide >= Mathf.Abs(lane) - 0.5f)
            {
                float past = Draftmaster.Tracks.OvalPitApron.Ahead(entry, md, _mainLength);
                if (past < _mainLength * 0.25f) _apronCutInPastNode = past;
                break;
            }
        }

        // Pit exit onto the apron: the first point past the pit exit line where pit road has come up level with
        // the apron lane, on the infield side. A lane that never does (it rejoins on the far side of the track,
        // or out in the run-off) keeps the plain exit at the end of the lane.
        float from = Mathf.Max(track.track.PitExitLineDistance, _pitLength * 0.5f);
        float to = _pitLength * pitExitThreshold;
        for (float pd = from; pd <= to; pd += 2f)
        {
            if (!PitLaneAgainstMain(pd, out float md, out float lat, out float lane)) break;
            if (lat * _apronInsideSide >= 0f && Mathf.Abs(lat) <= Mathf.Abs(lane) + 0.5f)
            {
                _apronPitCutover = pd;
                int n = _mainSamples.Count;
                var dist = new float[n];
                var heading = new float[n];
                for (int i = 0; i < n; i++)
                {
                    dist[i] = _mainSamples[i].distance;
                    heading[i] = Mathf.Atan2(_mainSamples[i].tangent.y, _mainSamples[i].tangent.x) * Mathf.Rad2Deg;
                }
                _apronBackStraight = Draftmaster.Tracks.OvalPitApron.BackStraightStart(dist, heading, _mainLength, md);
                if (_apronBackStraight < 0f) _apronPitCutover = -1f;
                return;
            }
        }
    }

    // ---- Pit exit at speed, on level ground ---------------------------------------------------------------
    // Past the pit exit line a car leaving is free to go - on an oval that means round the first turn or two
    // BELOW the apron as hard as it can, and back on at the back straight. Down there the ground is level
    // (PlayerVehicleController only banks a car on the main road), so it carries far less speed than the
    // banked racing surface beside it. Pit road is planned off its own bends at flat-ground grip, and a car
    // running the apron on the main spline is capped to the same flat grip on the apron's tighter radius.

    [Header("Pit Exit")]
    [Tooltip("In a green race, past the pit exit line, run the rest of pit road as fast as its own bends allow on LEVEL ground, instead of holding the pit speed limit to the end of the lane.")]
    public bool pitExitAtGripLimit = true;
    [Tooltip("A car down on an oval's paved apron (leaving the pits, or dropping down to come in) is held to the corner speed the apron's tighter radius allows on level ground - the apron has no banking.")]
    public bool apronFlatGripCap = true;

    float[] _pitSpeedProfile;   // mph per pit sample: pit limit before the exit line, flat-ground grip after it

    // The speed plan along pit road, built from the drawn lane: the pit limit up to the exit line, then each
    // bend's level-ground corner speed, joined up by the car's acceleration and braking.
    void BuildPitSpeedProfile()
    {
        _pitSpeedProfile = null;
        if (_pitSamples == null || _pitSamples.Count < 3 || track == null || track.track == null || _pitLength <= 0f) return;

        int n = _pitSamples.Count;
        float limitMph = track.track.pitSpeedLimit > 0 ? track.track.pitSpeedLimit : 50f;
        float exitLine = track.track.PitExitLineDistance;
        float topMph = TopMph;

        var pts = new Vector2[n];
        for (int i = 0; i < n; i++) pts[i] = _pitSamples[i].position;
        var kappa = new float[n];
        for (int i = 0; i < n; i++)
        {
            int prev = StepDistinctOpen(pts, i, -1, 0.5f);
            int next = StepDistinctOpen(pts, i, +1, 0.5f);
            kappa[i] = prev == i || next == i ? 0f : MengerCurvature(pts[prev], pts[i], pts[next]);
        }
        var tmp = new float[n];
        for (int p = 0; p < 2; p++)
        {
            for (int i = 0; i < n; i++)
                tmp[i] = (kappa[Mathf.Max(0, i - 1)] + kappa[i] + kappa[Mathf.Min(n - 1, i + 1)]) / 3f;
            (tmp, kappa) = (kappa, tmp);
        }

        var v = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (_pitSamples[i].distance < exitLine) { v[i] = limitMph; continue; }
            // segIdx -1: no banking and no segment cap - pit road and the apron are level.
            v[i] = kappa[i] > 1e-4f ? Mathf.Min(topMph, ComputeTargetSpeedForCurvature(kappa[i], -1)) : topMph;
        }
        for (int i = 1; i < n; i++)
        {
            float d = _pitSamples[i].distance - _pitSamples[i - 1].distance;
            if (d <= 0f) continue;
            float vPrev = v[i - 1] * MphToMps;
            float vMax = Mathf.Sqrt(vPrev * vPrev + 2f * SampleAccel(v[i - 1]) * d) * MpsToMph;
            if (vMax < v[i]) v[i] = vMax;
        }
        for (int i = n - 2; i >= 0; i--)
        {
            float d = _pitSamples[i + 1].distance - _pitSamples[i].distance;
            if (d <= 0f) continue;
            float vNext = v[i + 1] * MphToMps;
            float vMax = Mathf.Sqrt(vNext * vNext + 2f * SampleDecel(v[i]) * d) * MpsToMph;
            if (vMax < v[i]) v[i] = vMax;
        }
        _pitSpeedProfile = v;
    }

    // StepDistinct for an open lane (pit road doesn't loop).
    static int StepDistinctOpen(Vector2[] pts, int from, int dir, float minDist)
    {
        int idx = from;
        for (int k = 0; k < 8; k++)
        {
            int cand = idx + dir;
            if (cand < 0 || cand >= pts.Length) return idx;
            idx = cand;
            if (Vector2.Distance(pts[from], pts[idx]) >= minDist) return idx;
        }
        return idx;
    }

    // The pit-road speed plan (mph) at a distance down the lane; the pit limit when there is no plan.
    public float PitSpeedPlanAt(float pitDistance)
    {
        float limitMph = track != null && track.track != null && track.track.pitSpeedLimit > 0 ? track.track.pitSpeedLimit : 50f;
        var v = _pitSpeedProfile;
        if (v == null || _pitSamples == null || v.Length != _pitSamples.Count) return limitMph;
        int lo = 0, hi = v.Length - 1;
        if (pitDistance <= _pitSamples[0].distance) return v[0];
        if (pitDistance >= _pitSamples[hi].distance) return v[hi];
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_pitSamples[mid].distance <= pitDistance) lo = mid; else hi = mid;
        }
        float span = _pitSamples[hi].distance - _pitSamples[lo].distance;
        float t = span > 0f ? (pitDistance - _pitSamples[lo].distance) / span : 0f;
        return Mathf.Lerp(v[lo], v[hi], t);
    }

    // The most a car down on the apron may carry here (mph), MaxValue when it is not down there. The bends ahead
    // are taken on the apron lane - inside the racing line, so a tighter radius - at level-ground grip, with the
    // braking from here to each. Only while the car really is off the banked road: that is where the physics
    // stops banking it (TrackBuilder.BankAt), and once it merges back up the race plan has it again.
    float ApronFlatCapMph()
    {
        if (!apronFlatGripCap || _onPit || !(_apronExitBlend > 0f || _apronEntryActive) || !ApronPitRoad) return float.MaxValue;
        EnsureApronGeometry();
        if (!_apronPaved || _curvatureProfile == null || _insideSideProfile == null || _mainSamples == null) return float.MaxValue;
        int n = _mainSamples.Count;
        if (n != _curvatureProfile.Length || n != _insideSideProfile.Length) return float.MaxValue;

        int i = SampleIndexAtOrBefore(_distance, n);
        if (Mathf.Abs(LateralOnTrack) <= _mainSamples[i].width * 0.5f + 0.5f) return float.MaxValue;   // on the banking

        float decel = SampleDecel(_currentMph) * 0.85f;
        if (decel <= 0.1f) return float.MaxValue;
        float scan = Mathf.Max(offLineScanMetres, (_currentMph * MphToMps) * (_currentMph * MphToMps) / (2f * decel));
        float here = _mainSamples[i].distance;
        float best = float.MaxValue;
        for (int k = 0; k < n; k++)
        {
            int j = (i + k) % n;
            float d = _mainSamples[j].distance - here;
            if (d < 0f) d += _mainLength;
            if (d > scan) break;
            float kappa = _curvatureProfile[j];
            if (kappa < 0.002f) continue;   // r > 500 m: no bend to speak of
            float lineLat = _lateralProfile != null && _lateralProfile.Length == n ? _lateralProfile[j] : 0f;
            float laneLat = Draftmaster.Tracks.OvalPitApron.LaneLateral(_apronInsideSide, _mainSamples[j].width, _apronPaved, apronCarOffset, apronEdgeInset);
            float inward = (laneLat - lineLat) * _insideSideProfile[j];
            float r = 1f / kappa;
            float rIn = Mathf.Max(r - Mathf.Max(0f, inward), r * 0.3f);
            float vMps = ComputeTargetSpeedForCurvature(1f / rIn, -1) * MphToMps;
            float allowed = Mathf.Sqrt(vMps * vMps + 2f * decel * d) * MpsToMph;
            if (allowed < best) best = allowed;
        }
        return best;
    }

    // A point `pitDistance` down pit road against the main track: the main distance beside it, its lateral off
    // the main centreline (+ right), and the apron lane's lateral there.
    bool PitLaneAgainstMain(float pitDistance, out float mainDistance, out float lateral, out float lane)
    {
        mainDistance = lateral = lane = 0f;
        if (_pitSamples == null || _pitSamples.Count < 2) return false;
        var ps = track.SamplePitAt(pitDistance, _pitSamples);
        Vector3 world = track.transform.TransformPoint(new Vector3(ps.position.x, ps.position.y, 0f));
        mainDistance = track.NearestCenterlineDistance(world);
        var ms = track.SampleAt(mainDistance, _mainSamples);
        lateral = Vector2.Dot(ps.position - ms.position, new Vector2(ms.tangent.y, -ms.tangent.x));
        lane = Draftmaster.Tracks.OvalPitApron.LaneLateral(_apronInsideSide, ms.width, _apronPaved, apronCarOffset, apronEdgeInset);
        return true;
    }

    // How far (m) a car on the apron still has to run before it joins pit road: to the cut-in where pit road
    // meets the apron, or to the entry node. Negative once past it. Only meaningful on an apron oval.
    float ApronGapToPitJoin()
    {
        EnsureApronGeometry();
        float join = track.track.PitEntryDistanceOnLap + Mathf.Max(0f, _apronCutInPastNode);
        float gap = Draftmaster.Tracks.OvalPitApron.Ahead(DistanceOnTrack, join, _mainLength);
        return gap > _mainLength * 0.5f ? gap - _mainLength : gap;
    }

    // The run-in on an apron oval. Hold race speed and move down to the apron lane at apronDropRate, starting
    // far enough out that the move is done (plus apronSettleM) before the braking for pit road begins; the speed
    // cap set by ApproachPitEntry only bites inside that braking distance. Pauses the move while a car is in
    // the way below.
    void ApproachOnApron(float gap, float pitMps, float decelMps2)
    {
        _pitApproachBlend = 0f;
        float lane = ApronLaneAt(DistanceOnTrack);
        float v = Mathf.Max(externalMotionController ? externalActualSpeedMps : speed, pitMps);
        float brakeM = Mathf.Max(0f, (v * v - pitMps * pitMps) / (2f * Mathf.Max(0.5f, decelMps2)));

        if (!_apronEntryActive)
        {
            float here = _hasPrevLateral ? _prevLateral - _mergeLatBias : lateralOffset;
            float moveM = v * Mathf.Abs(lane - here) / Mathf.Max(0.5f, apronDropRate);
            if (gap > brakeM + moveM + apronSettleM) return;
            _apronEntryActive = true;
            _apronEntryLat = here;
        }

        bool dropping = Mathf.Abs(lane - _apronEntryLat) > 0.01f;
        if (!dropping)
            _apronEntryLat = lane;   // down there: follow the lane as the road width changes
        else if (!ApronTraffic(_apronEntryLat, lane, 2f))
            _apronEntryLat = Mathf.MoveTowards(_apronEntryLat, lane, apronDropRate * Time.fixedDeltaTime);
    }

    // Off the pit lane onto the apron: hold the apron lane to the back straight, then merge back up.
    void BeginApronExit()
    {
        _apronExitBlend = 1f;
        _apronExitMerging = false;
        _apronExitWaited = 0f;
        _apronExitToGo = Draftmaster.Tracks.OvalPitApron.Ahead(DistanceOnTrack, _apronBackStraight, _mainLength);
    }

    void StepApronExit(float advanced)
    {
        if (_apronExitBlend <= 0f) return;
        if (!_apronExitMerging)
        {
            if (_apronExitToGo > 0f) { _apronExitToGo -= advanced; return; }
            _apronExitWaited += advanced;
            // Up onto the racing line only when nothing is coming through the lanes between here and there - or
            // when the wait has run on long enough that the straight is nearly gone.
            float target = LateralAt(_distance) + lateralOffset + tacticalLateralOffset;
            if (_apronExitWaited >= apronMergeMaxWaitM || !ApronTraffic(ApronLaneAt(_distance), target, apronMergeLookSec))
                _apronExitMerging = true;
            else return;
        }
        _apronExitBlend = Mathf.MoveTowards(_apronExitBlend, 0f, apronMergeBlendPerSec * Time.fixedDeltaTime);
    }

    // Moving across from lateral a (where I am) to b: is another car in that band (plus a car's width) alongside
    // me, or coming from behind fast enough to be there within lookSec? Cars in my own lane - nose to tail ahead
    // or behind - are not in the way of a sideways move.
    bool ApronTraffic(float a, float b, float lookSec)
    {
        const float halfCar = 1.2f;
        float lo = Mathf.Min(a, b) - halfCar, hi = Mathf.Max(a, b) + halfCar;
        float myD = CentreDistanceOnTrack;
        float myMps = externalMotionController ? externalActualSpeedMps : speed;
        float half = _mainLength * 0.5f;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var o = drivers[i];
            if (o == null || o == this || o.IsOnPit || !o.isActiveAndEnabled) continue;
            if (ApronCarInWay(o.CentreDistanceOnTrack - myD, o.LateralOnTrack, o.CurrentMph * MphToMps, myMps, half, a, lo, hi, lookSec))
                return true;
        }
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.gameObject == gameObject || !p.isActiveAndEnabled) continue;
            if (ApronCarInWay(p.TrackDistance - myD, p.TrackLateral, p.SpeedMps, myMps, half, a, lo, hi, lookSec))
                return true;
        }
        return false;
    }

    static bool ApronCarInWay(float rel, float lat, float theirMps, float myMps, float half, float myLat,
                              float lo, float hi, float lookSec)
    {
        if (lat < lo || lat > hi || Mathf.Abs(lat - myLat) < 1f) return false;
        if (half > 0f) { if (rel > half) rel -= 2f * half; else if (rel < -half) rel += 2f * half; }
        const float alongside = 5.5f;   // a car length and a bit, either way
        if (Mathf.Abs(rel) < alongside) return true;
        if (rel > 0f) return false;   // ahead and clear
        float closing = Mathf.Max(0f, theirMps - myMps);
        return -rel < alongside + closing * lookSec;
    }

    // Which side of the main centreline the pit lane is on, 30 m down it. Same right-hand convention as Place.
    float MeasurePitSide()
    {
        if (_pitSamples == null || _pitSamples.Count < 2 || _mainSamples == null) return 0f;
        const float probe = 30f;
        var pit = track.SamplePitAt(Mathf.Min(probe, _pitLength), _pitSamples);
        var main = track.SampleAt(track.track.PitEntryDistanceOnLap + probe, _mainSamples);
        Vector2 right = new Vector2(main.tangent.y, -main.tangent.x);
        float side = Vector2.Dot(pit.position - main.position, right);
        return Mathf.Abs(side) < 0.1f ? 0f : Mathf.Sign(side);
    }

    [Header("Cornering Feel")]
    [Tooltip("Generous temporal smoothing (0..0.97) applied to the racing-line lateral AND the turn-in yaw, so the car flows through corner entry/exit instead of the rear axle snapping at segment boundaries. Higher = smoother but slightly rounds/lags the authored line. Per-FixedUpdate Lerp weight on the OLD value.")]
    [Range(0f, 0.97f)] public float cornerSmoothing = 0.88f;
    [Tooltip("Fraction of speed bled off on a perfectly square contact (scaled by severity and how head-on the hit is). Keep small for the parade — a hard scrub on a nose-to-tail tap makes the field concertina into a pile-up.")]
    [Range(0f, 1f)] public float contactSpeedScrub = 0.12f;
    const float rearAxleToCenter = -2.4f;

    // The spline point _distance indexes sits this far AHEAD of the car's centre along its heading (Place builds
    // the centre as that point + forward * rearAxleToCenter, which is negative). DistanceOnTrack is that point.
    public static float PathPointAheadOfCentre => -rearAxleToCenter;

    // Where the car's CENTRE physically is along the main spline, including any contact shove still easing out —
    // what another car would actually run into. DistanceOnTrack is the path point ahead of the centre.
    public float CentreDistanceOnTrack
    {
        get
        {
            float d = _distance + _collisionLongitudinal - PathPointAheadOfCentre;
            return _mainLength > 0f ? ((d % _mainLength) + _mainLength) % _mainLength : d;
        }
    }

    // The same, measured along the pit lane. Only meaningful while IsOnPit.
    public float CentreDistanceOnPit => _distance + _collisionLongitudinal - PathPointAheadOfCentre;

    // Lateral (m) the pit-exit merge is still carrying and easing out — part of LateralOnTrack, but temporary.
    public float MergeLateralBias => _mergeLatBias;
    // Where the car would sit across the track with no tactical offset: its racing line plus any fixed lateral
    // offset (m, + right of travel). Before the road-bounds clamp, like the tactical offset laid on top of it.
    public float UntacticalLateral => lateralOffset + _lineLatSmoothed;

    // The braking this car's decel curve gives at a speed (mph/s), with the same pace stretch UpdateSpeedToward uses.
    // On a plated track the car itself runs the stock envelope (TrackConditions.AiParity), so its braking is
    // planned on the stock curve too.
    public float BrakingMphPerSecAt(float mph)
    {
        float stretch = TrackConditions.AiParity ? 1f : Mathf.Max(1f, paceMultiplier);
        return SampleDecel(mph / stretch) * stretch * MpsToMph;
    }

    // Leftmost / rightmost laterals the car may drive between here (the racing-line bounds Place clamps to).
    public bool GetLateralBounds(out float lo, out float hi)
    {
        lo = float.NegativeInfinity;
        hi = float.PositiveInfinity;
        if (_onPit || _leftBoundProfile == null || _rightBoundProfile == null) return false;
        BoundsAt(_distance, out lo, out hi);
        return true;
    }

    // The bounds aheadMetres up the road, with or without the run-off - where a car avoiding a wreck there could go.
    public bool GetBoundsAhead(float aheadMetres, bool runOff, out float lo, out float hi)
    {
        lo = float.NegativeInfinity;
        hi = float.PositiveInfinity;
        if (_onPit || _leftBoundProfile == null || _rightBoundProfile == null) return false;
        BoundsAt(_distance + aheadMetres, out lo, out hi, runOff);
        return true;
    }

    // The bounds here without the run-off (allowRunOff), so a car out on it can tell when it is back on the road.
    public bool GetRoadBounds(out float lo, out float hi)
    {
        lo = float.NegativeInfinity;
        hi = float.PositiveInfinity;
        if (_onPit || _leftBoundProfile == null || _rightBoundProfile == null) return false;
        BoundsAt(_distance, out lo, out hi, false);
        return true;
    }

    // World heading (deg, 0 = +X) of the main spline at a distance — lets a free car's yaw be read against the road.
    public float MainTangentWorldDeg(float distance)
    {
        if (track == null || _mainSamples == null || _mainSamples.Count < 2) return 0f;
        var s = track.SampleAt(distance, _mainSamples);
        Vector2 t = track.transform.TransformVector(new Vector3(s.tangent.x, s.tangent.y, 0f));
        return Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg;
    }

    float _pitExitMainPath = -1f;
    float _pitExitMainLateral;

    // Where the pit lane hands a car back to the main spline: the main-spline path-point distance and the lateral
    // (main frame) it arrives at. Computed once per build.
    public bool TryGetPitExit(out float mainPathDistance, out float mainLateral, out float pitPathDistance)
    {
        mainPathDistance = mainLateral = pitPathDistance = 0f;
        if (track == null || _pitSamples == null || _pitSamples.Count < 2 || _mainSamples == null
            || _mainSamples.Count < 2 || _pitLength <= 0f)
            return false;
        pitPathDistance = _pitLength * pitExitThreshold;
        if (_pitExitMainPath < 0f)
        {
            var ps = track.SamplePitAt(pitPathDistance, _pitSamples);
            Vector3 world = track.transform.TransformPoint(new Vector3(ps.position.x, ps.position.y, 0f));
            _pitExitMainPath = track.NearestCenterlineDistance(world);
            var ms = track.SampleAt(_pitExitMainPath, _mainSamples);
            Vector2 right = new Vector2(ms.tangent.y, -ms.tangent.x);
            _pitExitMainLateral = Vector2.Dot(ps.position - ms.position, right);
        }
        mainPathDistance = _pitExitMainPath;
        mainLateral = _pitExitMainLateral;
        return true;
    }

    // A world position (a free-driven car) projected onto the pit lane: centre distance along it and lateral off it.
    public bool ProjectOntoPit(Vector3 worldPos, out float distance, out float lateral)
    {
        distance = lateral = 0f;
        if (track == null || _pitSamples == null || _pitSamples.Count < 2) return false;
        distance = track.NearestPitDistance(worldPos);
        var s = track.SamplePitAt(distance, _pitSamples);
        Vector2 local = track.transform.InverseTransformPoint(worldPos);
        lateral = Vector2.Dot(local - s.position, new Vector2(s.tangent.y, -s.tangent.x));
        return true;
    }

    public float CurrentMph => _currentMph;
    /// What the speed profile wants here, before AI follow-caps. Lets behaviours ask "could I be going faster?"
    public float DesiredMph { get; private set; }
    // The speed (mph) the brain is asking for this step after every cap - tactics, formation, pit approach - but
    // before its own accel-curve ramp. A flat-out car (TrackConditions.AiFlatOut) drives to this, not to the ramp.
    public float TargetMph { get; private set; }
    public float DistanceOnTrack => _mainLength > 0f ? ((_distance % _mainLength) + _mainLength) % _mainLength : _distance;
    public float LateralOnTrack => _prevLateral;
    // Where the car itself is across the road (m, + right), driven by an external motion controller: projected each
    // step in ResyncToCar. The brain's plan (LateralOnTrack) leads it, and through a corner exit they part by metres.
    public float CarLateral => externalMotionController && _hasCarLateral && !_onPit ? _carLateral : _prevLateral;
    float _carLateral;
    bool _hasCarLateral;
    /// Sideways speed across the track (m/s, + = toward + lateral), from the last two steps.
    public float LateralRate { get; private set; }
    public float TrackLength => _mainLength;

    // IFormationMember — lets the formation lap line cars up in grid order (the safety car overrides its grid via
    // qualifyingPosition = FormationOrder.SafetyCarGrid so it leads).
    public int GridPosition => qualifyingPosition;
    float IFormationMember.TrackDistance => DistanceOnTrack;
    float IFormationMember.TrackLateral => LateralOnTrack;
    public float SpeedMph => _currentMph;
    public bool FormationActive => isActiveAndEnabled && _mainLength > 0f;

    // Commanded path state (consumed by BicycleDynamics).
    public Vector2 CommandedLocalPos { get; private set; }
    public float CommandedHeadingDeg { get; private set; }
    public float CommandedSpeedMps { get; private set; }
    public bool externalMotionController = false; // true when BicycleDynamics owns the transform
    [HideInInspector] public float externalActualSpeedMps; // actual car speed fed back by the input provider
    public int CurrentSegmentIndex() => _segmentStartDistance != null ? SegmentIndexAt(_distance) : -1;

    public enum CornerPhase { Straight, Approach, Entry, Apex, Exit, PostExit }
    public CornerPhase CurrentPhase { get; private set; }

    [Header("AI Inputs (driven by AIRacingBehaviour)")]
    [Tooltip("Additive lateral offset applied each frame. Used by AI for overtaking / side-repulsion.")]
    public float tacticalLateralOffset = 0f;
    [Tooltip("Hard speed cap (mph) layered on top of the speed profile. float.MaxValue = no cap.")]
    public float aiMaxSpeedMph = float.MaxValue;
    [Tooltip("Additive speed bonus (mph). Used for drafting/slipstream.")]
    public float aiSpeedBoostMph = 0f;

    [Header("Oval Lanes")]
    [Tooltip("On an oval (any track but a road course), the fixed lane this car drives (Draftmaster.Tracks.OvalLanes, Docs/Oval-Lanes.md). Its line becomes that lane outright - lineFactor, the trained line and tacticalLateralOffset all stand aside on the main track - and its corner speeds are planned off the lane's own curvature, so a car in the outside lane goes round on the outside. None = the normal racing line. Ignored on road courses. Changing it rebuilds the line on the next step.")]
    public Draftmaster.Tracks.OvalLanes.Lane ovalLane = Draftmaster.Tracks.OvalLanes.Lane.None;
    // True while the car's line IS its assigned oval lane: ovalLane is set and the track is an oval.
    public bool LaneFollowing { get; private set; }
    Draftmaster.Tracks.OvalLanes.Lane _builtLane;
    // A full lane-car build (BuildLateralProfile with ovalLane set) leaves every array but the driven line the same
    // whichever of the three lanes it was for, so a later switch between lanes only swaps the line (SwapLaneLine)
    // instead of rebuilding it all - a full build was ~80 ms, once per lane change per car, the race's recurring
    // hitch. These are what that build was laid with; a mismatch (Rebuild, conditions changed) builds in full.
    bool _laneSwapReady;
    bool _laneSwapTrainedInUse;
    object _laneSwapSamples;
    bool _laneSwapAiLanes, _laneSwapPack;
    // The tactical offset as it applies here: a car holding an oval lane on the main track takes none - except the
    // swerve round a stopped car (laneAvoidLateral), which a lane car takes on top of its lane.
    float TacticalLateral => LaneFollowing && !_onPit ? laneAvoidLateral : tacticalLateralOffset;

    // A lane car's swerve round a stopped car (AIRacingBehaviour), metres off its lane line. The lane owns the line,
    // so the brain's tactical offset is ignored while LaneFollowing - which left a car on an oval no way round a
    // wreck in its lane but to brake for it. Zero whenever the car is not holding a lane.
    [System.NonSerialized] public float laneAvoidLateral;
    // This step the brain is steering round a stopped car (AIRacingBehaviour). The lane train then leaves stopped
    // cars to the brain's own stop-short cap instead of braking for one the car is already steering out from behind.
    [System.NonSerialized] public bool avoidingStopped;

    [Tooltip("Under green, a car holding an oval lane runs single file behind the car ahead in its lane (OvalTrain, Draftmaster.Sim.TrainFollow): a time gap at its own speed, closed on a lift, held without an accordion. Off = it drives its lane at its own pace and only the racing brain's caps slow it.")]
    public bool trainFollowing = true;
    // This step's train (see trainFollowing): whether there is a car ahead in the lane, the most the gap to it
    // allows (m/s), whether closing on it is an emergency, and the gap itself and the gap wanted (m, centre to centre).
    public bool TrainActive { get; private set; }
    public float TrainCapMps { get; private set; } = float.MaxValue;
    public bool TrainEmergency { get; private set; }
    public float TrainGap { get; private set; } = float.MaxValue;
    public float TrainWantedGap { get; private set; }
    PlayerVehicleController _trainCar;
    SplineDriver _trainAhead;     // the car the train is following (null: none, or a free-driven human car)
    float _trainAheadMps;

    [Tooltip("Oval lane train: after a green flag (start or restart) a car runs flat out until it is up to speed, held off the car ahead only by TrainFollow's launchGap - the time gap is not asked for while the whole field is accelerating, which had every car lifting for the one ahead and the field stacking up behind the front row. Once up to speed the time gap it wants grows back from the gap it has at launchHeadwayGrowth. Off = the time-gap law from the green.")]
    public bool flatOutLaunch = true;
    [Tooltip("Flat-out launch: a car is up to speed at this share of its speed plan, or of the speed of a car ahead that is itself up to speed.")]
    [Range(0.8f, 1f)] public float launchUpToSpeedShare = 0.97f;
    [Tooltip("Flat-out launch: seconds after the green by which every car's launch is over, whatever its speed (and a car that joins later never launches).")]
    public float launchMaxSeconds = 30f;
    bool _trainWasGreen;
    bool _trainLaunching;
    float _trainHeadwayLimit = float.MaxValue;   // s: the most time gap the train wants, growing back after a launch
    [Tooltip("Flat-out launch: a car holds the speed it had at the green until the car directly ahead of it (in its lane, or in its path) is pulling away, then goes - the accordion of a real restart. The leader, with nobody ahead, goes on the green, and each row follows a beat after the one in front, so the rows (6-7 m apart at the green) open out as the speed builds. Off = the whole field floors it at the same instant: the rows never open, and when the front row eases off at the trioval everyone behind has to lift (Texas 2026-11-06: 55% of the field off the throttle 5 s after the green, 0.02 m nose to tail).")]
    public bool staggeredLaunch = true;
    [Tooltip("Staggered launch: seconds after the car ahead goes before this one does - a driver's reaction, and the time gap each row opens on the one behind it (6.4 m at ~50 mph pace grows by ~15 m by racing speed).")]
    public float launchReactSeconds = 0.25f;
    [Tooltip("Staggered launch, behind a free-driven human car: it counts as gone once it is this much (mph) quicker than the speed this car is holding.")]
    public float launchReactMph = 2f;
    [Tooltip("Staggered launch: a car ahead further than this (m, centre to centre) is not waited for.")]
    public float launchWaitGapM = 30f;
    [Tooltip("Staggered launch: the longest (s) any car holds after the green, whatever the car ahead does.")]
    public float launchMaxHoldSeconds = 2.5f;

    [Tooltip("Start lanes (oval lane AI): the pace lap runs two-wide on two LOW lines (OvalLanes.Start: a fixed height above the inside edge, well apart), and after the green each car keeps to its start lane - given the matching racing lane, low = inside, high = outside, with no lane changes - until it is up to speed (its flat-out launch is over), then eases onto its racing lane over startLaneReleaseSeconds. Before (Texas 2026-11-10) the green handed the pair straight to the racing lanes while the whole field was on the throttle together: the outside car was sent up toward the wall through the trioval and pairs closed on each other where the drawn lines converge. Off = the racing lanes from the green.")]
    public bool startLanes = true;
    [Tooltip("Start lanes: seconds to ease from the start lane onto the racing lane once up to speed (a smoothstep, so no sideways kick at either end).")]
    public float startLaneReleaseSeconds = 3f;
    [Tooltip("Start lanes: the longest (s) any car keeps to its start lane after the green, up to speed or not.")]
    public float startLaneMaxHoldSeconds = 20f;
    [Tooltip("Start lanes: a car in the low lane waits to ease off it while a car in the high lane within this (m, along the track) still holds - the low lane eases UP onto the inside racing lane, toward the car beside it.")]
    public float startLanePartnerMetres = 9f;
    // Which start lane FormationController has this car in on the pace lap: -1 low, +1 high, 0 none. Written every
    // formation step and latched as the green falls (StepStartLane).
    [System.NonSerialized] public int startLane;
    int _startSide;                 // the start lane being held (-1 low, +1 high, 0 none)
    float _startWeight;             // 1 holding, easing to 0 on release
    bool _startReleasing;
    float _startReleaseP, _startHeldSeconds;
    float[] _startLow, _startHigh;
    List<TrackBuilder.Sample> _startLanesFor;
    // The start lane this car is in (-1 low, +1 high) on the pace lap and after the green until it has eased off it.
    public int StartLaneSide => _startSide;
    // Still on its start lane after the green (not yet easing off it).
    public bool StartLaneHolding => _startSide != 0 && !_startReleasing;

    // Puts the car in a start lane (-1 low, +1 high, 0 none) for the pace lap - FormationController, every formation
    // step - or, in a sim that drops the green on a field already two-wide on the start lanes, as the green falls.
    // Taken now, so the lane director handing out lanes before this car's own step sees it.
    public void SetStartLane(int side)
    {
        side = System.Math.Sign(side);
        startLane = side;
        if (!startLanes || _onPit) side = 0;
        if (_startSide == side && (side == 0 || !_startReleasing)) return;
        if (RaceStart.IsGreen && _startHeldSeconds > 0f) return;   // after the green: only the next pace lap moves it
        _startSide = side;
        _startWeight = side != 0 ? 1f : 0f;
        _startReleasing = false;
        _startReleaseP = 0f;
        _startHeldSeconds = 0f;
    }

    // Lateral (m, + right of travel) of a start lane (-1 low, +1 high) at `distance`. False off an oval.
    public bool TryStartLaneLateralAt(int side, float distance, out float lateral)
    {
        lateral = 0f;
        if (side == 0 || track == null || _mainSamples == null || _mainSamples.Count < 3) return false;
        if (_startLanesFor != _mainSamples)
        {
            _startLanesFor = _mainSamples;
            if (!track.TryGetOvalStartLanes(_mainSamples, out _startLow, out _startHigh)) _startLow = _startHigh = null;
        }
        var line = side < 0 ? _startLow : _startHigh;
        if (line == null || line.Length != _mainSamples.Count) return false;
        lateral = SampleAlongLine(line, distance, 0f);
        return true;
    }

    // What holding the start lane adds on top of the lane line at `distance`.
    float StartLaneShiftAt(float distance)
    {
        if (_startSide == 0 || !(_startWeight > 0f) || !LaneFollowing || _onPit) return 0f;
        if (!TryStartLaneLateralAt(_startSide, distance, out float lat)) return 0f;
        return _startWeight * (lat - LaneLateralAt(ovalLane, distance));
    }

    // Start lanes: on the pace lap, follow the lane FormationController has put the car in; after the green hold it
    // until up to speed, then ease off it.
    void StepStartLane()
    {
        float dt = Time.fixedDeltaTime;
        if (!RaceStart.IsGreen)
        {
            _startSide = startLanes && !_onPit ? startLane : 0;
            _startWeight = _startSide != 0 ? 1f : 0f;
            _startReleasing = false;
            _startReleaseP = 0f;
            _startHeldSeconds = 0f;
            return;
        }
        startLane = 0;   // consumed: the next pace lap sets it again
        if (_startSide == 0) return;
        if (!startLanes || _onPit || usePitLane)
        {
            _startSide = 0;
            _startWeight = 0f;
            return;
        }
        _startHeldSeconds += dt;
        if (!_startReleasing)
        {
            // (Not on the green's first steps: the launch flags are only set as the train's step sees the green.)
            bool upToSpeed = _startHeldSeconds > 0.5f && LaneFollowing && !_launchHold && !_trainLaunching && SecondsSinceLaunch >= 0f;
            if (_startHeldSeconds > startLaneMaxHoldSeconds || upToSpeed && !(_startSide < 0 && HighLaneHeldBeside()))
                _startReleasing = true;
        }
        if (_startReleasing)
        {
            _startReleaseP = Mathf.MoveTowards(_startReleaseP, 1f, dt / Mathf.Max(0.1f, startLaneReleaseSeconds));
            float p = _startReleaseP;
            _startWeight = 1f - p * p * (3f - 2f * p);
            if (_startReleaseP >= 1f)
            {
                _startSide = 0;
                _startWeight = 0f;
            }
        }
    }

    // A car in the high start lane, still holding it, within startLanePartnerMetres along the track.
    bool HighLaneHeldBeside()
    {
        float len = _mainLength;
        if (!(len > 0f)) return false;
        float myD = CentreDistanceOnTrack;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var o = drivers[i];
            if (o == null || o == this || !o.isActiveAndEnabled || o._startSide <= 0 || !o.StartLaneHolding) continue;
            float along = Mathf.Abs(Mathf.Repeat(o.CentreDistanceOnTrack - myD + len * 0.5f, len) - len * 0.5f);
            if (along <= startLanePartnerMetres) return true;
        }
        return false;
    }
    bool _launchHold;          // holding the green speed until the car ahead goes (staggeredLaunch)
    // Whether this car is still holding its speed for the car ahead to go (staggeredLaunch).
    public bool LaunchHolding => _launchHold;
    // Seconds since this car stopped holding after the last green (it went); -1 while it holds. Counted in physics
    // steps, not off Time.time, so a sped-up game and the headless sims time it the same.
    public float SecondsSinceLaunch { get; private set; } = -1f;
    float _launchHeldSeconds;
    float _launchHoldMps;
    // Whether this car is still in its flat-out launch after the green (flatOutLaunch).
    public bool TrainLaunching => _trainLaunching;

    void StepTrain()
    {
        TrainActive = false;
        TrainEmergency = false;
        TrainCapMps = float.MaxValue;
        TrainGap = float.MaxValue;
        _trainAhead = null;
        // The launch starts as the green falls (a start or a restart) and lasts until the car is up to speed.
        bool green = RaceStart.IsGreen;
        if (green && !_trainWasGreen)
        {
            _trainLaunching = flatOutLaunch;
            _trainHeadwayLimit = float.MaxValue;
            // Lane AI only (OvalLaneDirector).
            _launchHold = flatOutLaunch && staggeredLaunch && OvalLaneDirector.Applies && OvalLaneDirector.Enabled && !_onPit;
            _launchHoldMps = OvalTrain.SpeedOf(this);
            SecondsSinceLaunch = _launchHold ? -1f : 0f;
            _launchHeldSeconds = 0f;
        }
        _trainWasGreen = green;
        if (!_launchHold && SecondsSinceLaunch >= 0f) SecondsSinceLaunch += Time.fixedDeltaTime;
        if (_launchHold)
        {
            _launchHeldSeconds += Time.fixedDeltaTime;
            // Holding the green speed until the car ahead goes: pace speed, no lift, no chase. Before (2026-11-06 to
            // -07) each car held until it reached where "the field leader" was at the green, a point worked out once
            // for the whole field; at Texas it came out 160 m up the road from the real leader, and the leader and
            // then every row behind it cruised there at 50 mph - the back of the field 13 s after the green.
            // The car ahead has gone once it stopped holding a reaction time ago (an AI car), or is pulling away (a
            // human's, which never holds). Rows can sit a few mph apart at the green, so a speed test alone let a
            // whole column go in one step and nothing opened.
            bool found = OvalTrain.TryFindAhead(this, out float holdGap, out float holdAhead, out _trainAhead);
            // Not on the green step itself: a car ahead that has not run its own step yet still shows the last green's
            // numbers. A car ahead that never launches (the pace car) is not waited for.
            bool firstStep = _launchHeldSeconds <= Time.fixedDeltaTime * 1.5f;
            bool aheadGone = !firstStep && (!found || holdGap > launchWaitGapM
                             || (_trainAhead != null
                                 ? !_trainAhead.LaunchHolding && (_trainAhead.SecondsSinceLaunch < 0f
                                                                 || _trainAhead.SecondsSinceLaunch >= launchReactSeconds)
                                 : holdAhead > _launchHoldMps + launchReactMph * MphToMps));
            if (!_trainLaunching || !green || !trainFollowing || _onPit || aheadGone
                || _launchHeldSeconds > launchMaxHoldSeconds)
            {
                _launchHold = false;
                SecondsSinceLaunch = 0f;
            }
            else
            {
                TrainActive = true;
                TrainCapMps = _launchHoldMps;
                {
                    // Never into the car ahead while holding (it is either holding too or already away).
                    var hc = Draftmaster.Sim.TrainFollow.Follow(OvalTrain.Spec, holdGap, OvalTrain.SpeedOf(this), holdAhead,
                                                                1.5f, float.MaxValue, true);
                    TrainCapMps = Mathf.Min(TrainCapMps, hc.speedMps);
                    TrainEmergency = hc.emergency;
                    TrainGap = holdGap;
                    TrainWantedGap = hc.wantedGap;
                }
                return;
            }
        }
        if (_trainLaunching && (!green || !flatOutLaunch || RaceStart.SecondsSinceGreen > launchMaxSeconds))
            _trainLaunching = false;
        if (!_trainLaunching && _trainHeadwayLimit < OvalTrain.Spec.headwaySeconds)
            _trainHeadwayLimit += OvalTrain.Spec.launchHeadwayGrowth * Time.fixedDeltaTime;
        if (!LaneFollowing || !trainFollowing || _onPit || !green) return;
        float v = OvalTrain.SpeedOf(this);
        float upShare = launchUpToSpeedShare;
        if (!OvalTrain.TryFindAhead(this, out float gap, out float aheadMps, out _trainAhead))
        {
            if (_trainLaunching && v >= upShare * DesiredMph * MphToMps) _trainLaunching = false;
            return;
        }
        _trainAheadMps = aheadMps;
        if (_trainLaunching && (v >= upShare * DesiredMph * MphToMps
                                || (_trainAhead != null && !_trainAhead._trainLaunching && v >= upShare * aheadMps)))
        {
            // Up to speed: the time gap wanted grows back from the gap there is, not all at once.
            _trainLaunching = false;
            _trainHeadwayLimit = Mathf.Max(OvalTrain.Spec.launchHeadwayMin,
                                           Mathf.Max(0f, gap - OvalTrain.Spec.standstillGap) / Mathf.Max(1f, v));
        }
        // What a lift sheds right now: the dynamic model's coast, half of it gone in a full tow.
        if (_trainCar == null) _trainCar = GetComponent<PlayerVehicleController>();
        float lift = _trainCar != null ? _trainCar.coastDecel * (1f - 0.5f * _trainCar.TowFactor) : 1.5f;
        var c = Draftmaster.Sim.TrainFollow.Follow(OvalTrain.Spec, gap, v, aheadMps, lift, _trainHeadwayLimit, _trainLaunching);
        TrainActive = true;
        TrainCapMps = c.speedMps;
        TrainEmergency = c.emergency;
        TrainGap = gap;
        TrainWantedGap = c.wantedGap;
    }

    [Tooltip("Under green, a car in an oval lane train may move to the other lane to pass the car it is following, when it has a run on it and the other lane has room (OvalPass, Draftmaster.Sim.LaneChange; Docs/Oval-Lanes.md stage 3). The move is a smooth blend between the two lane lines at a capped sideways speed and acceleration; it turns back if the room closes while the car is still mostly in its own lane. Off = it stays in its lane.")]
    public bool lanePassing = true;
    [Tooltip("With lanePassing on: single file a lane car runs the ideal racing line (OvalLanes.Lane.Ideal). When a car is alongside it, the one nearer the infield moves to the inside lane (the leftmost AI line) and the other to the outside lane (the rightmost); once clear, each moves back onto the ideal when it has room. A pass from the ideal pulls out to whichever side lane has room. Off = the lane a car is given is the lane it keeps, as stages 1-3 were measured.")]
    public bool idealWhenSingleFile = true;
    [Tooltip("Centre to centre along the track (m) inside which a car counts as alongside, so a car on the ideal moves to its side lane. A Cup car is 4.8 m long: 6.5 m is overlapping by a little under 2 m.")]
    public float alongsideEnterMetres = 6.5f;
    [Tooltip("...and beyond which a car in a side lane is clear of it and may go back to the ideal: 9 m is 4 m of daylight. The gap between the two is so a car drawing level does not flick back and forth.")]
    public float alongsideClearMetres = 9f;
    [Tooltip("A car out of line on a pass stays in its side lane until it is alongsideClearMetres clear ahead of the car it is passing, or this many seconds have gone (the pass has stalled), then goes back to the ideal when there is room.")]
    public float pairPassGiveUpSeconds = 30f;
    SplineDriver _pairPassing;   // the car a pass out of the ideal is on, held until we are clear of it
    float _pairPassTimer;
    // The lane change under way: ovalLane is already the lane being moved to (its line, speed plan and train), and
    // the car's line is the old lane's blended into it by _laneP (0 = on the old line, 1 = landed). _laneRate is in
    // progress per second.
    public bool LaneChanging { get; private set; }
    public float LaneChangeProgress => LaneChanging ? _laneP : 1f;
    public Draftmaster.Tracks.OvalLanes.Lane LaneChangeFrom => _laneFrom;
    public bool LaneChangeAborting => LaneChanging && _laneAborting;
    // The car this lane change set out to pass (null for a free-driven human car, or once it is over).
    public SplineDriver LaneChangePassing { get; private set; }
    // The smoothed run on the car ahead (m/s) the pass decision reads (Draftmaster.Sim.LaneChange.WantsToPass).
    public float PassAdvantage => _passAdvantage;
    // Running counts, for telemetry and the sim tests.
    public int LaneChangesStarted { get; private set; }
    public int LaneChangesCompleted { get; private set; }
    public int LaneChangesAborted { get; private set; }
    Draftmaster.Tracks.OvalLanes.Lane _laneFrom;
    float _laneP = 1f, _laneRate;
    bool _laneAborting;
    bool _laneSplit;     // this move is a split for a car alongside: never turned back
    float _laneInsideSide = -1f;
    float _laneCooldown;
    float _passAdvantage;
    float[] _insideLaneLine, _outsideLaneLine, _idealLaneLine;   // every lane's lateral per main sample (lane mode only)
    float[] _laneBuildInside, _laneBuildOutside;

    // Lateral (m) of an oval lane's line at `distance` along the main track. 0 when the car has no lanes.
    public float LaneLateralAt(Draftmaster.Tracks.OvalLanes.Lane lane, float distance)
    {
        var line = lane == Draftmaster.Tracks.OvalLanes.Lane.Outside ? _outsideLaneLine
                 : lane == Draftmaster.Tracks.OvalLanes.Lane.Ideal ? _idealLaneLine
                 : _insideLaneLine;
        return line != null && lane != Draftmaster.Tracks.OvalLanes.Lane.None ? SampleAlongLine(line, distance, 0f) : 0f;
    }

    // What a lane change adds on top of the line (ovalLane's lane) at `distance`: the rest of the way back to the old
    // lane, (old - new) x (1 - progress). Read ahead of the brain's point, the progress is projected at the move's
    // current rate, so the steering aims at where the blend will be when the car gets there.
    float LaneShiftAt(float distance) => LaneChangeShiftAt(distance) + StartLaneShiftAt(distance);

    float LaneChangeShiftAt(float distance)
    {
        if (!LaneChanging || _onPit) return 0f;
        float p = _laneP;
        if (_mainLength > 0f)
        {
            float ahead = Mathf.Repeat(distance - _distance + _mainLength * 0.5f, _mainLength) - _mainLength * 0.5f;
            float v = Mathf.Max(5f, OvalTrain.SpeedOf(this));
            p = Mathf.Clamp01(p + _laneRate * ahead / v);
        }
        return (LaneLateralAt(_laneFrom, distance) - LaneLateralAt(ovalLane, distance)) * (1f - p);
    }

    // The line the car is meant to be on at `distance` (m lateral): its lane, blended from the old one mid lane
    // change. What lane-following error is measured against.
    public float LaneTargetLateralAt(float distance) => LateralAt(distance) + LaneShiftAt(distance);

    // Runs first thing each step, before the line is rebuilt for a new ovalLane, so a change and its re-lay land on
    // the same step. Reads last step's train (StepTrain).
    void StepLaneChange()
    {
        float dt = Time.fixedDeltaTime;
        var spec = OvalPass.Spec;
        if (LaneChanging)
        {
            // A pit call or a yellow mid-move lets it finish (it is ~3 s); on pit road there is no lane to blend.
            if (_onPit || !LaneFollowing) { EndLaneChange(); return; }
            float d = CentreDistanceOnTrack;
            float spacing = Mathf.Abs(LaneLateralAt(ovalLane, d) - LaneLateralAt(_laneFrom, d));
            // The room closing while the car is still mostly in its own lane: turn back, the same smooth way.
            if (!_laneAborting && !_laneSplit && Draftmaster.Sim.LaneChange.CanAbort(spec, _laneP, _laneRate, spacing)
                && !OvalPass.TargetLaneClear(this, ovalLane, 0f, spec.abortShare))   // 0: a train gap ahead, not the pass
            {
                var back = _laneFrom;
                _laneFrom = ovalLane;
                ovalLane = back;
                _laneP = 1f - _laneP;
                _laneRate = -_laneRate;
                _laneAborting = true;
                LaneChangesAborted++;
            }
            if (Draftmaster.Sim.LaneChange.Step(spec, ref _laneP, ref _laneRate, spacing, dt))
            {
                if (!_laneAborting) LaneChangesCompleted++;
                EndLaneChange();
            }
            return;
        }

        _laneCooldown -= dt;
        // Nor while still on (or easing off) a start lane: the field stays two-wide in its start lanes until up to speed.
        if (!LaneFollowing || !lanePassing || _onPit || usePitLane || _pitApproachBlend > 0f || _apronEntryActive || _apronExitBlend > 0f
            || !RaceStart.IsGreen || _startSide != 0)
        {
            _passAdvantage = 0f;
            return;
        }
        // Side by side splits onto the inside / outside lanes; single file goes back to the ideal.
        if (idealWhenSingleFile && StepPairLanes()) return;
        if (!TrainActive)
        {
            _passAdvantage = 0f;
            return;
        }

        // The run on the car ahead: our own speed plan where it is now over its plan there (the pace edge, with any
        // tow boost), or the speed we are closing on it at (a run off the corner, a draft), whichever is bigger.
        float v = OvalTrain.SpeedOf(this);
        float advantage = v - _trainAheadMps;
        if (_trainAhead != null && _speedProfile != null)
        {
            float there = CentreDistanceOnTrack + TrainGap + PathPointAheadOfCentre;
            float ratio = Mathf.Max(0.1f, paceMultiplier) / _profilePace;
            float plan = Mathf.Min(ProfileAt(there) * ratio + aiSpeedBoostMph, GripLimitAt(there)) * MphToMps;
            advantage = Mathf.Max(advantage, plan - _trainAhead.DesiredMph * MphToMps);
        }
        _passAdvantage = Mathf.Lerp(_passAdvantage, advantage, Mathf.Clamp01(dt / Mathf.Max(dt, spec.advantageSeconds)));

        if (_laneCooldown > 0f || TrainEmergency) return;
        if (!Draftmaster.Sim.LaneChange.WantsToPass(spec, _passAdvantage, TrainGap, TrainWantedGap)) return;
        float here = CentreDistanceOnTrack;
        if (ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Ideal)
        {
            // From the ideal: out to a side lane, the one further from where the car being passed is (more room
            // between the doors on the way past), or the other if that one has no room.
            float aheadLat = _trainAhead != null ? _trainAhead.CarLateral : LaneLateralAt(ovalLane, here);
            var first = Mathf.Abs(LaneLateralAt(Draftmaster.Tracks.OvalLanes.Lane.Outside, here) - aheadLat)
                        >= Mathf.Abs(LaneLateralAt(Draftmaster.Tracks.OvalLanes.Lane.Inside, here) - aheadLat)
                ? Draftmaster.Tracks.OvalLanes.Lane.Outside : Draftmaster.Tracks.OvalLanes.Lane.Inside;
            var second = first == Draftmaster.Tracks.OvalLanes.Lane.Outside
                ? Draftmaster.Tracks.OvalLanes.Lane.Inside : Draftmaster.Tracks.OvalLanes.Lane.Outside;
            if (!TryStartPass(first, here, v)) TryStartPass(second, here, v);
            return;
        }
        TryStartPass(ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Inside
            ? Draftmaster.Tracks.OvalLanes.Lane.Outside : Draftmaster.Tracks.OvalLanes.Lane.Inside, here, v);
    }

    bool TryStartPass(Draftmaster.Tracks.OvalLanes.Lane target, float here, float v)
    {
        var spec = OvalPass.Spec;
        float laneSpacing = Mathf.Abs(LaneLateralAt(target, here) - LaneLateralAt(ovalLane, here));
        if (laneSpacing < 1f) return false;   // the road is too narrow for two lanes here
        if (!CornerAllowsLaneChange(v, Draftmaster.Sim.LaneChange.MoveDuration(spec, laneSpacing), spec.maxCornerAccel)) return false;
        if (!OvalPass.TargetLaneClear(this, target, TrainGap, 1f)) return false;
        BeginLaneMove(target, _trainAhead, false);
        return true;
    }

    void BeginLaneMove(Draftmaster.Tracks.OvalLanes.Lane target, SplineDriver passing, bool split)
    {
        _laneFrom = ovalLane;
        ovalLane = target;
        _laneP = 0f;
        _laneRate = 0f;
        _laneAborting = false;
        _laneSplit = split;
        if (passing != null) { _pairPassing = passing; _pairPassTimer = 0f; }
        else if (target == Draftmaster.Tracks.OvalLanes.Lane.Ideal) _pairPassing = null;
        LaneChanging = true;
        LaneChangePassing = passing;
        LaneChangesStarted++;
    }

    // Single file on the ideal, side by side on the inside / outside lanes (idealWhenSingleFile). True when it
    // started a move this step.
    bool StepPairLanes()
    {
        const Draftmaster.Tracks.OvalLanes.Lane Ideal = Draftmaster.Tracks.OvalLanes.Lane.Ideal,
                                                Inside = Draftmaster.Tracks.OvalLanes.Lane.Inside,
                                                Outside = Draftmaster.Tracks.OvalLanes.Lane.Outside;
        float window = ovalLane == Ideal ? alongsideEnterMetres : alongsideClearMetres;
        bool alongside = OvalPass.TryFindAlongside(this, window, out float otherLat, out var otherLane);
        float side = _laneInsideSide;
        float myIn = CarLateral * side, otherIn = otherLat * side;   // how far toward the infield

        if (alongside)
        {
            Draftmaster.Tracks.OvalLanes.Lane target;
            if (ovalLane == Ideal)
            {
                // Take the side lane the car beside us is not in (or heading for); if it is on the ideal too, the
                // one nearer the infield goes inside. Both cars read the same pair, so they always split.
                if (otherLane == Inside) target = Outside;
                else if (otherLane == Outside) target = Inside;
                else target = otherIn > myIn ? Outside : Inside;
            }
            else if (otherLane == ovalLane && (ovalLane == Inside ? otherIn > myIn : otherIn < myIn))
            {
                // Both in the same side lane: the one on the wrong side of the other moves over.
                target = ovalLane == Inside ? Outside : Inside;
            }
            else return false;   // in a side lane with a car alongside: hold it
            // A car alongside is the reason to move, so the move is made even mid-corner and never turned back.
            BeginLaneMove(target, null, true);
            return true;
        }

        if (ovalLane == Ideal) { _pairPassing = null; return false; }
        // Out of line on a pass: stay out until we are clear ahead of the car being passed (or the pass has stalled
        // for pairPassGiveUpSeconds, or it has dropped out of reach).
        if (_pairPassing != null)
        {
            _pairPassTimer += Time.fixedDeltaTime;
            float along = Mathf.Repeat(_pairPassing.CentreDistanceOnTrack - CentreDistanceOnTrack + _mainLength * 0.5f, _mainLength)
                          - _mainLength * 0.5f;
            bool still = _pairPassing.isActiveAndEnabled && !_pairPassing.IsOnPit
                         && along > -alongsideClearMetres && along < 80f && _pairPassTimer < pairPassGiveUpSeconds;
            if (still) return false;
            _pairPassing = null;
        }
        if (_laneCooldown > 0f || TrainEmergency) return false;
        // Single file again: back onto the ideal once it has room (the room a move under way keeps) and the corner
        // allows the sideways move.
        var spec = OvalPass.Spec;
        float here = CentreDistanceOnTrack;
        float spacing = Mathf.Abs(LaneLateralAt(Ideal, here) - LaneLateralAt(ovalLane, here));
        if (spacing > 0.05f)
        {
            float v = OvalTrain.SpeedOf(this);
            if (!CornerAllowsLaneChange(v, Draftmaster.Sim.LaneChange.MoveDuration(spec, spacing), spec.maxCornerAccel)) return false;
            if (!OvalPass.TargetLaneClear(this, Ideal, 0f, spec.abortShare)) return false;
        }
        BeginLaneMove(Ideal, null, false);
        return true;
    }

    // Lays the line, curvature and speed plan for ovalLane now if it has changed since they were last laid (otherwise
    // it happens at the start of the next physics step). OvalLaneDirector calls it to read the new lane's line.
    public void RelayLaneNow()
    {
        if (ovalLane == _builtLane || _mainSamples == null || _mainSamples.Count < 2) return;
        BuildLateralProfile();
        BuildCurvatureProfile();
        BuildSpeedProfile();
    }

    // Which side of travel the infield is (-1 left, +1 right), from the lanes themselves: the inside lane is the
    // one further toward it.
    public float LaneInsideSide => _laneInsideSide;

    void EndLaneChange()
    {
        LaneChanging = false;
        LaneChangePassing = null;
        _laneAborting = false;
        _laneP = 1f;
        _laneRate = 0f;
        _laneCooldown = OvalPass.Spec.cooldownSeconds;
        _passAdvantage = 0f;
    }

    // Whether the road over the next `seconds` at `speedMps` corners gently enough to move across on: the most
    // cornering load (curvature x speed squared) on the driven line there, under `maxAccel`.
    bool CornerAllowsLaneChange(float speedMps, float seconds, float maxAccel)
    {
        if (_curvatureProfile == null || _mainSamples == null || _mainLength <= 0f) return false;
        float reach = Mathf.Max(10f, speedMps * seconds);
        float kMax = maxAccel / Mathf.Max(1f, speedMps * speedMps);
        for (float a = 0f; a <= reach; a += 5f)
        {
            float k = SampleAlongLine(_curvatureProfile, _distance + a, 0f);
            if (k > kMax) return false;
        }
        return true;
    }

    // The run-in to pit road, set each tick by whoever has called the car in (ApproachPitEntry) and cleared once
    // it is on the lane. Kept apart from aiMaxSpeedMph / tacticalLateralOffset because the racing brain rewrites
    // both every tick until the car is on the lane — which is exactly the stretch this has to cover.
    float _pitApproachCapMph = float.MaxValue;
    float _pitApproachBlend;
    float _pitApproachLateral;
    float _pitSide;   // +1 / -1: which side of the track the pit lane peels off to. 0 = not measured yet.
    [Tooltip("Share of the grip limit this driver takes corners at (1 = right on the limit). Set per driver by AIDriverBinding from their skill, so the field differs in the corners and not just on the straights. Baked into the speed profile at Rebuild.")]
    [Range(0.5f, 1f)] public float cornerCommitment = 1f;
    [Tooltip("The driver's own grip out of the car (AIRatings.DriverGrip): scales the corner speeds planned here, the grip governor and the car's physics alike.")]
    public float driverGrip = 1f;
    [Tooltip("When > 0, GUARANTEES at least this braking rate (mph/sec) regardless of the (possibly weak) decel curve. Set per-frame by the formation/avoidance AI so an emergency slow can actually land. 0 = use the curve.")]
    [HideInInspector] public float aiMinDecelMphPerSec = 0f;
    [Tooltip("Default deceleration used when the vehicle's decel curve is unauthored, in m/s².")]
    public float fallbackDecel = 10f;
    [Tooltip("Default acceleration used when the vehicle's accel curve is unauthored, in m/s².")]
    public float fallbackAccel = 5f;
    [Tooltip("Default flat-corner speed used when the vehicle's cornering curve is unauthored, in mph.")]
    public float fallbackCornerMph = 110f;
    [Tooltip("Scales corner target speeds. Targets are computed from the driven line's real curvature and the same grip the physics uses, so 1.0 = the theoretical limit; keep slightly below 1 for margin, >1 commands past the grip ceiling (physics saturation caps what actually happens).")]
    [Range(0.6f, 2f)] public float cornerSpeedScale = 0.95f;
    [Tooltip("Corner speeds come from the line's curvature and the car's grip and banking alone. Off = a segment's maxSpeed caps them too - on the generated tracks that is a road-car estimate for the marker boards, 30-45 mph under what a Cup car holds on a banked oval.")]
    public bool cornerSpeedFromGrip = true;
    [Tooltip("Under green, a car on the dynamic model is driven to the speed the brain asks for with whatever throttle that takes, not along the brain's accel-curve ramp - the ramp held the throttle at 50-70% down every straight off the plate tracks. Plate tracks always drive this way (TrackConditions.AiFlatOut).")]
    public bool driveToTarget = true;
    // Whether this step's speed is the car's own, driven to TargetMph (see driveToTarget).
    public bool DrivesToTarget => externalMotionController && !_onPit && RaceStart.IsGreen && (driveToTarget || TrackConditions.AiFlatOut);

    const float MphToMps = 1f / 2.237f;
    const float MpsToMph = 2.237f;

    List<TrackBuilder.Sample> _mainSamples;
    List<TrackBuilder.Sample> _pitSamples;
    List<TrackInfoV2.RacingLineAnchor> _anchors;
    float[] _segmentTargetMph;
    float[] _segmentStartDistance;
    float[] _speedProfile;
    // The fastest the car can take each point of the line at all (mph): the usable-grip corner speed with no
    // cornerSpeedScale margin and no pace. Pace may bring a driver up to it, never past it. Straights hold
    // MaxValue. See BuildSpeedProfile.
    float[] _gripLimitProfile;
    float[] _curvatureProfile;  // |curvature| (1/m) of the DRIVEN line (centerline + smoothed lateral), per main sample
    float[] _insideSideProfile; // which way the inside of the bend is, per main sample: +1 right, -1 left
    float _bakedALatMaxMps2;    // lateral-accel ceiling (m/s²) the profile was baked against
    float[] _lateralProfile;
    float[] _lineLo, _lineHi;   // where the smoothed line may go: the road, or a player line's legal corridor
    float[] _roadLo, _roadHi;   // the road edge to edge (less RoadEdgeMargin)
    float[] _escapeLo, _escapeHi;   // the road plus the paved run-off a wreck can be avoided on
    float[] _leftBoundProfile;
    float[] _rightBoundProfile;
    float _mainLength;
    float _pitLength;
    float _distance;
    bool _onPit;
    float _prevLateral;
    bool _hasPrevLateral;
    float _currentMph;
    float _lineLatSmoothed;   // low-passed racing-line lateral, so the rear axle doesn't snap at segment boundaries
    bool _hasLineSmoothed;    // false until the first smoothed sample / after a lane change, so it seeds from raw
    float _pathYawSmoothed;   // low-passed turn-in yaw (deg), so the body doesn't snap pointing onto its path
    float _mergeLatBias; // lateral carried across the pit→main merge, eased out so the rejoin isn't a sideways pop
    float _mergeHeadingBias; // heading (deg) carried across the merge, eased out so the rejoin isn't a rotation snap

    EngineGearbox _gearbox; // optional: shapes acceleration (gear torque + shift drive-cut) and feeds engine audio

    void Awake()
    {
        var vl = GetComponent<VehicleLogic>();
        if (vl != null) vl.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        _gearbox = GetComponent<EngineGearbox>();
    }

    void OnEnable() { RaceField.Register(this); FormationOrder.Register(this); }
    void OnDisable() { RaceField.Unregister(this); FormationOrder.Unregister(this); }

    bool _startSeeded; // set by EngageFromCurrentPose so Start() doesn't clobber the seeded distance

    // Take over a free-driven car (e.g. the player's) without a teleport: rebuild the spline data, latch onto the
    // nearest point to the car's current world position, and continue at its current speed. Enable the component
    // first, then call this. Used by DriveModeController when handing the player's car to the AI.
    public void EngageFromCurrentPose(float startMph)
    {
        if (_mainSamples == null || _mainSamples.Count < 2) Rebuild();
        externalMotionController = false; // kinematic: this component owns the transform

        // Latch onto whichever lane the car is physically ON. Taking over a car that's in the pit lane and forcing
        // it onto the main racing line would drive it straight through the pit wall (the crash). Use an ABSOLUTE
        // test — within pitEngageLateralMax of the pit centerline — rather than "nearer spline wins": the pit lane
        // runs alongside the main straight, so the main line just across the wall is often geometrically closer than
        // the pit centerline, which made the comparison pick the track and crash. Same test as PlayerPitService.
        // RejoinSplineContinuous then continues along the chosen lane; the normal auto-exit carries a pit-lane car
        // back onto the track at the pit-exit node.
        bool onPit = false;
        if (track != null && _pitSamples != null && _pitSamples.Count >= 2 && _pitLength > 0f)
        {
            Vector2 local = track.transform.InverseTransformPoint(transform.position);
            Vector2 pitPos = track.SamplePitAt(track.NearestPitDistance(transform.position), _pitSamples).position;
            // A car parked on the box lane (outside the pit ribbon) still counts as on the pit.
            float engageMax = track.HasPitBoxLane ? Mathf.Max(pitEngageLateralMax, Mathf.Abs(track.PitBoxLaneOuterLateral) + 0.5f) : pitEngageLateralMax;
            onPit = Vector2.Distance(local, pitPos) < engageMax;
        }
        usePitLane = onPit;
        _onPit = onPit;
        _apronEntryActive = false;
        _apronExitBlend = 0f;
        RejoinSplineContinuous(transform.position);
        float cap = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        _currentMph = Mathf.Clamp(startMph, 0f, cap);
        speed = _currentMph * MphToMps;
        _startSeeded = true;
    }

    void Start()
    {
        // Re-resolve the gearbox: GridSpawner adds it AFTER this SplineDriver, so the Awake fetch missed it.
        // Start runs after the whole spawn loop, so it exists now. (Prefab-built cars already had it in Awake.)
        if (_gearbox == null) _gearbox = GetComponent<EngineGearbox>();

        if (_startSeeded) return; // already engaged via EngageFromCurrentPose; keep its seeded distance
        Rebuild();
        if (spawnInPit && _pitLength > 0f)
        {
            usePitLane = true;
            _onPit = true;
            _distance = ComputePitBoxDistance(qualifyingPosition);
        }
        else
        {
            _distance = startDistance;
            _onPit = usePitLane;
        }
        // Pit-box race start is a standing start. The speed field only seeds constant-speed test rigs.
        _currentMph = spawnInPit ? 0f : speed * MpsToMph;
        float capMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        _currentMph = Mathf.Clamp(_currentMph, 0f, capMph);
        Place();
    }

    float ComputePitBoxDistance(int idx)
    {
        float d = _pitLength - pitBoxExitGap - idx * pitBoxSpacing;
        return PitParkDistanceFor(d);
    }

    // Where the path point must stop for the car's BODY to sit centred on a box whose centre is `boxCentre` m down
    // the pit lane. Place() puts the centre PathPointAheadOfCentre behind the path point, so stopping the path
    // point on the box centre parked every AI car that far back in its box. PitLane.FitBoxes sets the ladder
    // back by the same amount, so this lands on the stop the fit was made for.
    public float PitParkDistanceFor(float boxCentre)
    {
        float stop = Mathf.Max(0f, boxCentre + PathPointAheadOfCentre);
        return _pitLength > 0f ? Mathf.Min(_pitLength, stop) : stop;
    }

    // Put the car straight into a box `boxCentre` m down the pit lane, `lateral` m off its centerline, stopped
    // and held there — a teleport, not a drive. The end of a race skipped to by the results screen (RaceDirector)
    // uses it for every car still out on its cool-down lap. Pinned at once rather than eased in (SettleIntoHold),
    // and the transform and body are written as well as the commanded pose, so a dynamic-AI car is re-seeded on
    // the spot by SplineInputDriver's held-still branch instead of being driven there from wherever it was.
    public void SnapToPitBox(float boxCentre, float lateral)
    {
        if (track == null) return;
        if (_mainSamples == null || _mainSamples.Count < 2 || _pitSamples == null) Rebuild();
        if (_pitSamples == null || _pitSamples.Count < 2 || _pitLength <= 0f) return;

        usePitLane = true;
        _onPit = true;
        _distance = PitParkDistanceFor(boxCentre);
        pitParkDistance = _distance;
        lateralOffset = lateral;
        tacticalLateralOffset = 0f;
        _collisionLateral = _collisionLongitudinal = 0f;
        _mergeLatBias = _mergeHeadingBias = 0f;
        _pitApproachBlend = 0f;
        _apronEntryActive = false;
        _apronExitBlend = 0f;
        _hasPrevLateral = false;
        speed = 0f;
        _currentMph = 0f;
        pitStopHold = true;
        parkedHold = true;
        _wasHeld = true;          // already held: SettleIntoHold must not ease it in from the old pose
        _settling = false;
        _droveSinceHold = false;

        Place();
        Vector3 wp = track.transform.TransformPoint(new Vector3(CommandedLocalPos.x, CommandedLocalPos.y, 0f));
        transform.position = new Vector3(wp.x, wp.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? CommandedHeadingDeg - 90f : CommandedHeadingDeg) + angleOffsetDeg);
        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
        }
        RacePositionTracker.NoteTeleport(transform);
    }

    // Place the car at startDistance immediately and write the transform, independent of Start() ordering.
    // The networked spawner calls this before NetworkObject.Spawn() so the car captures a valid on-track pose:
    // otherwise the AI input driver's first physics step (which runs before Start) seeds it at the origin.
    public void PlaceAtStartDistance()
    {
        if (track == null) return;
        if (_mainSamples == null || _mainSamples.Count < 2) Rebuild();

        if (spawnInPit && _pitLength > 0f)
        {
            usePitLane = true;
            _onPit = true;
            _distance = ComputePitBoxDistance(qualifyingPosition);
        }
        else
        {
            _distance = startDistance;
            _onPit = usePitLane;
        }
        _currentMph = spawnInPit ? 0f : speed * MpsToMph;
        Place(); // sets CommandedLocalPos/Heading/Speed; skips the transform under externalMotionController

        // Write the transform too (Place skips it when an external model owns motion), so the car physically
        // sits on its start spot and NetworkTransform replicates that pose to clients.
        Vector3 wp = track.transform.TransformPoint(new Vector3(CommandedLocalPos.x, CommandedLocalPos.y, 0f));
        transform.position = new Vector3(wp.x, wp.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? CommandedHeadingDeg - 90f : CommandedHeadingDeg) + angleOffsetDeg);
    }

    public void Rebuild()
    {
        if (track == null) return;
        _pitExitMainPath = -1f;
        _pitSide = 0f;
        _apronApplies = -1;
        _apronGeometryReady = false;
        _mainSamples = track.SampleCenterline();
        _mainLength = _mainSamples.Count > 0 ? _mainSamples[_mainSamples.Count - 1].distance : 0f;
        _pitSamples = track.SamplePitCenterline();
        _pitLength = _pitSamples.Count > 0 ? _pitSamples[_pitSamples.Count - 1].distance : 0f;
        _anchors = track.track != null ? track.track.BuildRacingLineAnchors() : null;
        PrecomputeSegmentSpeeds();
        // Lateral (the smoothed racing line) must exist before the speed profile: corner targets are computed
        // from the curvature of the line the car actually drives, not the raw centerline segments.
        BuildLateralProfile();
        BuildCurvatureProfile();
        BuildSpeedProfile();
        BuildPitSpeedProfile();
    }

    // Switches the driven line between the three oval lanes laid by the last full lane-car build. False when that
    // build no longer stands (see _laneSwapReady), and the caller builds in full.
    bool SwapLaneLine()
    {
        if (!_laneSwapReady || ovalLane == Draftmaster.Tracks.OvalLanes.Lane.None) return false;
        if (!ReferenceEquals(_laneSwapSamples, _mainSamples) || _laneSwapAiLanes != TrackConditions.AiLanes
            || _laneSwapPack != TrackConditions.AiPackRacing) return false;
        var lane = ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Outside ? _outsideLaneLine
                 : ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Inside ? _insideLaneLine
                 : _idealLaneLine;
        if (lane == null || _lateralProfile == null || lane.Length != _lateralProfile.Length) return false;
        _builtLane = ovalLane;
        LaneFollowing = true;
        TrainedLineInUse = ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Ideal && _laneSwapTrainedInUse;
        _lateralProfile = (float[])lane.Clone();
        return true;
    }

    void BuildLateralProfile()
    {
        if (SwapLaneLine()) return;
        _laneSwapReady = false;
        LaneFollowing = false;
        laneAvoidLateral = 0f;
        _builtLane = ovalLane;
        _insideLaneLine = _outsideLaneLine = _idealLaneLine = null;
        if (_mainSamples == null || _mainSamples.Count == 0 || track == null || track.track == null || _anchors == null)
        {
            _lateralProfile = null;
            _leftBoundProfile = null;
            _rightBoundProfile = null;
            _roadLo = _roadHi = _escapeLo = _escapeHi = null;
            return;
        }

        int n = _mainSamples.Count;
        // A car given an oval lane: its single-file line is the plain ideal (no per-driver lineFactor spread - the
        // lanes are where cars spread), and the side-by-side lanes come from the same place the gizmo draws them.
        bool laneCar = ovalLane != Draftmaster.Tracks.OvalLanes.Lane.None
                       && track.TryGetOvalLanes(_mainSamples, out _laneBuildInside, out _laneBuildOutside);
        float lf = laneCar ? 0f : lineFactor;
        _lateralProfile = new float[n];
        _leftBoundProfile = new float[n];
        _rightBoundProfile = new float[n];
        _roadLo = new float[n];
        _roadHi = new float[n];
        _escapeLo = new float[n];
        _escapeHi = new float[n];

        // A trained line, if this track has one, stands in for the authored ideal. It was found by driving
        // the lap a few hundred times and keeping whatever was quicker (RacingLineTrainer), so it already
        // sacrifices entry for exit the way a driver does. The length check is the guard: geometry gets
        // regenerated, and a line trained against an older shape of the road is worse than none.
        var trained = useTrainedLine && track.track != null
            ? Draftmaster.Tracks.TrainedRacingLines.For(track.track.name)
            : null;
        if (trained != null && !trained.MatchesLength(_mainLength)) trained = null;
        TrainedLineInUse = TrainedLineFound = trained != null;

        // Where the LINE may go. Normally the road (the bound profiles below). A line copied from the player's
        // lap carries the legal corridor it was driven in — road, kerb and tarmac run-off — and may use all of
        // it, as they did. The bound profiles themselves stay on the road: they are what lineFactor spread and
        // tactical moves read, and a car swerving onto the run-off to pass someone is not the idea.
        bool corridor = trained != null && trained.HasCorridor && lineUsesRunOff;
        bool runOff = trained != null && trained.HasCorridor;
        _lineLo = new float[n];
        _lineHi = new float[n];

        // Does the authored band pinch anywhere on this oval (ovalFullWidth)?
        bool ovalLanes = TrackConditions.AiLanes && !TrackConditions.AiPackRacing;
        BoundsPinched = false;
        if (ovalLanes)
        {
            for (int i = 0; i < n && !BoundsPinched; i++)
            {
                float d = _mainSamples[i].distance, w = _mainSamples[i].width;
                float band = Mathf.Abs(ClampToRoad(track.track.GetLateralAt(d, +1f, _anchors, _mainLength), w)
                                       - ClampToRoad(track.track.GetLateralAt(d, -1f, _anchors, _mainLength), w));
                float road = ClampToRoad(float.PositiveInfinity, w) - ClampToRoad(float.NegativeInfinity, w);
                if (!float.IsInfinity(road) && band < 0.4f * road) BoundsPinched = true;
            }
        }
        bool ovalWide = ovalLanes && ovalFullWidth && BoundsPinched;

        for (int i = 0; i < n; i++)
        {
            float d = _mainSamples[i].distance;
            _leftBoundProfile[i] = ClampToRoad(track.track.GetLateralAt(d, -1f, _anchors, _mainLength), _mainSamples[i].width);
            _rightBoundProfile[i] = ClampToRoad(track.track.GetLateralAt(d, +1f, _anchors, _mainLength), _mainSamples[i].width);
            if (ovalWide)
            {
                float edgeLo = ClampToRoad(float.NegativeInfinity, _mainSamples[i].width);
                float edgeHi = ClampToRoad(float.PositiveInfinity, _mainSamples[i].width);
                if (!float.IsInfinity(edgeLo) && !float.IsInfinity(edgeHi))
                {
                    bool leftLow = _leftBoundProfile[i] <= _rightBoundProfile[i];
                    _leftBoundProfile[i] = leftLow ? edgeLo : edgeHi;
                    _rightBoundProfile[i] = leftLow ? edgeHi : edgeLo;
                }
            }
            _lineLo[i] = Mathf.Min(_leftBoundProfile[i], _rightBoundProfile[i]);
            _lineHi[i] = Mathf.Max(_leftBoundProfile[i], _rightBoundProfile[i]);
            _roadLo[i] = Mathf.Min(_lineLo[i], ClampToRoad(float.NegativeInfinity, _mainSamples[i].width));
            _roadHi[i] = Mathf.Max(_lineHi[i], ClampToRoad(float.PositiveInfinity, _mainSamples[i].width));
            if (float.IsInfinity(_roadLo[i]) || float.IsInfinity(_roadHi[i]))
            {
                _roadLo[i] = _lineLo[i];   // no usable width data: the authored lines are all there is
                _roadHi[i] = _lineHi[i];
            }
            _escapeLo[i] = _roadLo[i];
            _escapeHi[i] = _roadHi[i];
            if (runOff)
            {
                // The corridor is where the player's car centre went; half a car in from it, and no further from
                // the road than runOffEscapeMetres - it was probed for surface, not for barriers.
                Vector2 c = trained.CorridorAt(d);
                _escapeLo[i] = Mathf.Min(_roadLo[i], Mathf.Max(c.x + 1f, _roadLo[i] - runOffEscapeMetres));
                _escapeHi[i] = Mathf.Max(_roadHi[i], Mathf.Min(c.y - 1f, _roadHi[i] + runOffEscapeMetres));
            }
            if (corridor)
            {
                Vector2 c = trained.CorridorAt(d);
                _lineLo[i] = Mathf.Min(_lineLo[i], c.x);
                _lineHi[i] = Mathf.Max(_lineHi[i], c.y);
            }

            if (TrackConditions.AiPackRacing)
            {
                // A superspeedway pack races in fixed grooves (AIRacingBehaviour's pack lanes), laid off the
                // centreline. The line under them has to be the centreline too: an out-in-out line swung 7 m across
                // Talladega into every turn, faster than a car's lane offset could follow, and dragged whole lines of
                // cars through the lanes beside them.
                _lateralProfile[i] = Mathf.Clamp(0f, _lineLo[i], _lineHi[i]);
            }
            else if (trained != null)
            {
                // Same blend TrackInfoV2 does between ideal and the outer lines, just off the trained ideal,
                // so per-driver lineFactor spread still spreads the field across the road.
                float ideal = Mathf.Clamp(trained.LateralAt(d), _lineLo[i], _lineHi[i]);
                _lateralProfile[i] = lf >= 0f
                    ? Mathf.Lerp(ideal, _rightBoundProfile[i], lf)
                    : Mathf.Lerp(ideal, _leftBoundProfile[i], -lf);
            }
            else
            {
                _lateralProfile[i] = ClampToRoad(track.track.GetLateralAt(d, lf, _anchors, _mainLength), _mainSamples[i].width);
            }
        }

        // On a road course with no trained line, or one copied from a player's lap: the ideal is the road's own
        // minimum-curvature line, edge to edge, rather than the authored arc (see optimiseLine). lineFactor still
        // spreads the field off it toward the authored leftmost / rightmost lines, as it does off a trained line.
        //
        // A player's lap is one lap, mistakes and all. Watkins Glen's (the only one, and the only line carrying a
        // run-off corridor) put the car's centre 2.8 m off the inside at the apexes on average and lapped 1:16.6 in
        // the calibration sim; the optimised line apexed tighter and lapped 1:12.3, clean (AILineDiagnostics,
        // 2026-10-31). The lines RacingLineTrainer settled on stay: they beat it at all eleven other road courses.
        // The lap's corridor still opens the run-off to a car driving round a wreck (runOff above).
        bool optimised = optimiseLine && !TrackConditions.AiPackRacing
                         && (roadCourseFullWidth && !TrackConditions.AiLanes && (trained == null || trained.HasCorridor)
                             || ovalLanes && (TrackConditions.AiOptimisedOvalLine || _mainLength >= optimiseOvalFromMetres)
                             || ovalWide && optimisePinchedOvals);
        if (optimised)
        {
            TrainedLineInUse = false;
            float[] ideal = OptimisedIdeal();
            for (int i = 0; i < n; i++)
            {
                _lineLo[i] = Mathf.Min(_lineLo[i], _roadLo[i]);
                _lineHi[i] = Mathf.Max(_lineHi[i], _roadHi[i]);
                _lateralProfile[i] = lf >= 0f
                    ? Mathf.Lerp(ideal[i], _rightBoundProfile[i], lf)
                    : Mathf.Lerp(ideal[i], _leftBoundProfile[i], -lf);
            }
        }

        // Min-curvature relaxation: each pass nudges every point toward the average of its neighbours, clamped to bounds.
        // Only a few passes on a trained line: it is already smooth (the trainer only ever moves it in raised-
        // cosine bumps), and a full relax would drag the late apexes back into the geometric arc it beat. But
        // not none. Where the road has been narrowed since the line was trained, the clamp above cuts the line
        // off square at the new edge, and a corner a couple of samples long is a 20 m radius in the curvature
        // profile — at Watkins Glen that put a phantom hairpin at 3,570 m, and the car braked for it mid-way
        // through the esses at 2 g, lost the rear and went off. Thirty passes (swept in AILapSimTests) round off a kink that
        // short and leaves a bump tens of metres wide as it was.
        //
        // Each point relaxes toward the straight line between its neighbours at ITS distance, not toward their
        // plain average. The samples aren't evenly spaced everywhere — at Watkins Glen the centreline runs 9 m past
        // the authored segments and that last stretch is sampled every 0.57 m instead of 2 m, with the closing
        // sample sat on top of the first — and a by-index average treats a 0.57 m step like a 2 m one. Where the
        // line was crossing the road there, that folded it into an 8 m "hairpin" on the run to the flag, and the
        // car braked to 31 mph for it every lap. With even spacing the two are identical.
        // An optimised line is already as smooth as the road allows; it only wants the same light rounding off.
        int relaxPasses = trained != null || optimised ? trainedLineSmoothingPasses : smoothingIterations;
        var tmp = new float[n];
        for (int p = 0; p < relaxPasses; p++)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (loop ? n - 1 : 0) : i - 1;
                int next = i == n - 1 ? (loop ? 0 : n - 1) : i + 1;
                float hPrev = SampleGap(prev, i), hNext = SampleGap(i, next);
                float avg = hPrev + hNext > 1e-4f
                    ? (_lateralProfile[prev] * hNext + _lateralProfile[next] * hPrev) / (hPrev + hNext)
                    : _lateralProfile[i];
                float relaxed = Mathf.Lerp(_lateralProfile[i], avg, smoothingRelaxation);
                tmp[i] = Mathf.Clamp(relaxed, _lineLo[i], _lineHi[i]);
            }
            (tmp, _lateralProfile) = (_lateralProfile, tmp);
        }

        // An assigned oval lane. Ideal (single file) keeps the line just laid; Inside / Outside (side by side)
        // replace it outright with that lane, which is not relaxed like a line - BuildFromLines already smoothed it,
        // and relaxing an offset line round a corner pulls it toward the inside. Either way the bounds open to the
        // road edges (the authored band can sit inside a lane) so nothing clamps the car off its lane, and all three
        // lanes are kept for the blends between them (LaneLateralAt).
        if (laneCar)
        {
            LaneFollowing = true;
            _laneSwapTrainedInUse = TrainedLineInUse;
            if (ovalLane != Draftmaster.Tracks.OvalLanes.Lane.Ideal) TrainedLineInUse = false;
            _insideLaneLine = _laneBuildInside;
            _outsideLaneLine = _laneBuildOutside;
            double towardInside = 0.0;
            for (int i = 0; i < n; i++) towardInside += _insideLaneLine[i] - _outsideLaneLine[i];
            _laneInsideSide = towardInside < 0.0 ? -1f : 1f;
            _idealLaneLine = (float[])_lateralProfile.Clone();
            var lane = ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Outside ? _outsideLaneLine
                     : ovalLane == Draftmaster.Tracks.OvalLanes.Lane.Inside ? _insideLaneLine
                     : _idealLaneLine;
            for (int i = 0; i < n; i++)
            {
                float w = _mainSamples[i].width;
                float edgeLo = ClampToRoad(float.NegativeInfinity, w), edgeHi = ClampToRoad(float.PositiveInfinity, w);
                if (float.IsInfinity(edgeLo) || float.IsInfinity(edgeHi)) { edgeLo = _lineLo[i]; edgeHi = _lineHi[i]; }
                float lo = Mathf.Min(edgeLo, Mathf.Min(_insideLaneLine[i], _outsideLaneLine[i]));
                float hi = Mathf.Max(edgeHi, Mathf.Max(_insideLaneLine[i], _outsideLaneLine[i]));
                lo = Mathf.Min(lo, _idealLaneLine[i]);
                hi = Mathf.Max(hi, _idealLaneLine[i]);
                bool leftLow = _leftBoundProfile[i] <= _rightBoundProfile[i];
                _leftBoundProfile[i] = leftLow ? lo : hi;
                _rightBoundProfile[i] = leftLow ? hi : lo;
                _lineLo[i] = _roadLo[i] = _escapeLo[i] = lo;
                _lineHi[i] = _roadHi[i] = _escapeHi[i] = hi;
                _lateralProfile[i] = lane[i];
            }
            _laneSwapReady = true;
            _laneSwapSamples = _mainSamples;
            _laneSwapAiLanes = TrackConditions.AiLanes;
            _laneSwapPack = TrackConditions.AiPackRacing;
        }
    }

    // The minimum-curvature line through the road (less RoadEdgeMargin) for this track, solved once and shared by
    // every car on it: it depends only on the geometry, and each car's own lineFactor is blended on afterwards.
    // Keyed on the sample count and lap length too, so a regenerated road is solved again.
    static readonly Dictionary<TrackInfoV2, (int n, float length, float[] ideal)> s_optimisedIdeal = new();

    float[] OptimisedIdeal()
    {
        int n = _mainSamples.Count;
        var info = track.track;
        if (s_optimisedIdeal.TryGetValue(info, out var hit) && hit.n == n && Mathf.Approximately(hit.length, _mainLength))
            return hit.ideal;

        // On an oval the solved line is rounded off in world space afterwards (below), which wants no zero-length
        // chord: a loop's closing sample sits on top of its first, so that one is left out and given the first's offset.
        bool oval = TrackConditions.AiLanes;
        int m = oval && loop && n > 3 && (_mainSamples[n - 1].position - _mainSamples[0].position).sqrMagnitude < 0.25f ? n - 1 : n;
        var centre = new Vector2[m];
        var right = new Vector2[m];
        var start = new float[m];
        var lo = new float[m];
        var hi = new float[m];
        for (int i = 0; i < m; i++)
        {
            var s = _mainSamples[i];
            centre[i] = s.position;
            right[i] = new Vector2(s.tangent.y, -s.tangent.x).normalized;
            start[i] = ClampToRoad(info.GetLateralAt(s.distance, 0f, _anchors, _mainLength), s.width);
            lo[i] = _roadLo[i];
            hi[i] = _roadHi[i];
        }
        var solved = Draftmaster.Sim.MinCurvatureLine.Solve(centre, right, lo, hi, start, loop);
        // The least-squares solve leaves a short tight hump where a short sharp bend meets a straight - Texas's
        // trioval kinks, 50 m of R143 on a 15.5 m road, came out R170-200 at the kink's end and the car lifted to
        // 146 there. On an oval, round it off in world space: each point eases toward the middle of its neighbours'
        // chord, which takes curvature out where it bunches and barely moves a long arc (Texas lap 31.9 -> 31.4 s in
        // the lap sim, T4 146 -> 155). Not on a road course: there the same pull would drag a hairpin's whole arc
        // onto the inside kerb.
        for (int pass = 0; oval && pass < OvalLineRoundingPasses; pass++)
        {
            var nxt = (float[])solved.Clone();
            for (int i = 0; i < m; i++)
            {
                int a = i > 0 ? i - 1 : (loop ? m - 1 : 0), b = i < m - 1 ? i + 1 : (loop ? 0 : m - 1);
                if (a == i || b == i) continue;
                Vector2 pa = centre[a] + right[a] * solved[a], pb = centre[b] + right[b] * solved[b];
                Vector2 mid = 0.5f * (pa + pb);
                float target = Vector2.Dot(mid - centre[i], right[i]);
                nxt[i] = Mathf.Clamp(Mathf.Lerp(solved[i], target, 0.5f), lo[i], hi[i]);
            }
            solved = nxt;
        }
        var ideal = new float[n];
        for (int i = 0; i < n; i++) ideal[i] = solved[i < m ? i : 0];
        s_optimisedIdeal[info] = (n, _mainLength, ideal);
        return ideal;
    }

    const int OvalLineRoundingPasses = 200;

    // Track distance from main sample a forward to main sample b, across the start/finish wrap on a loop.
    float SampleGap(int a, int b)
    {
        if (a == b) return 0f;
        float gap = _mainSamples[b].distance - _mainSamples[a].distance;
        if (gap < 0f) gap += _mainLength;
        return Mathf.Max(0f, gap);
    }

    // Room a car's centre keeps from the painted edge: half a car plus a little. The authored leftmost and
    // rightmost lines are anchored per segment and splined between anchors, and nothing ties them to the width
    // the road is actually built at — on a hand-traced track like Watkins Glen they run metres past the edge
    // through the esses, and a line (trained or not) clamped to them drove the car straight into the run-off.
    public const float RoadEdgeMargin = 1.4f;

    // A lateral offset (m, + right of travel) pulled back inside a road `roadWidth` wide. Shared with the
    // racing-line trainer so it trains inside the same corridor the car drives.
    public static float ClampToRoad(float lateral, float roadWidth)
    {
        float half = roadWidth * 0.5f - RoadEdgeMargin;
        if (half <= 0.25f) return lateral; // no usable width data (or a lane barely wider than a car): leave it
        return Mathf.Clamp(lateral, -half, half);
    }

    // Index of the last main-line sample at or before `distance` (0 when it is before them all), among the
    // first n. Binary search: these lookups run many times per car per physics step, and the linear walk they
    // replaced crossed every sample on the lap each time — the single biggest cost of a 44-car field under the
    // pace car on a phone (simpleperf, 2026-10-16). Sample distances only ever increase along the lap.
    int SampleIndexAtOrBefore(float distance, int n)
    {
        n = Mathf.Min(n, _mainSamples.Count);
        int lo = 0, hi = n - 1;
        if (n <= 0 || _mainSamples[0].distance > distance) return 0;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_mainSamples[mid].distance <= distance) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    void BoundsAt(float distance, out float lo, out float hi) => BoundsAt(distance, out lo, out hi, allowRunOff);

    void BoundsAt(float distance, out float lo, out float hi, bool runOff)
    {
        lo = -100f; hi = 100f;
        if (_leftBoundProfile == null || _rightBoundProfile == null || _mainSamples == null) return;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _leftBoundProfile.Length;
        int idxLo = SampleIndexAtOrBefore(distance, n);
        int idxHi = (idxLo + 1) % n;
        float dLo = _mainSamples[idxLo].distance;
        float dHi = _mainSamples[idxHi].distance;
        if (idxHi <= idxLo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        float left = Mathf.Lerp(_leftBoundProfile[idxLo], _leftBoundProfile[idxHi], t);
        float right = Mathf.Lerp(_rightBoundProfile[idxLo], _rightBoundProfile[idxHi], t);
        lo = Mathf.Min(left, right);
        hi = Mathf.Max(left, right);
        float[] wideLo = runOff ? _escapeLo : roadCourseFullWidth && !TrackConditions.AiLanes ? _roadLo : null;
        float[] wideHi = runOff ? _escapeHi : _roadHi;
        if (wideLo != null && wideHi != null && wideLo.Length == n && wideHi.Length == n)
        {
            lo = Mathf.Min(lo, Mathf.Lerp(wideLo[idxLo], wideLo[idxHi], t));
            hi = Mathf.Max(hi, Mathf.Lerp(wideHi[idxLo], wideHi[idxHi], t));
        }
    }

    float LateralAt(float distance)
    {
        if (_lateralProfile == null || _lateralProfile.Length == 0 || _mainSamples == null) return 0f;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _lateralProfile.Length;
        int lo = SampleIndexAtOrBefore(distance, n);
        int hi = (lo + 1) % n;
        float dLo = _mainSamples[lo].distance;
        float dHi = _mainSamples[hi].distance;
        if (hi <= lo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        return Mathf.Lerp(_lateralProfile[lo], _lateralProfile[hi], t);
    }

    // paceMultiplier folded into the current speed profile. Targets are baked at this pace so the
    // relaxation passes bake matching (longer) braking distances; the live command applies only the
    // residual ratio between rebuilds. Above 1 the whole longitudinal envelope stretches with it —
    // without that, the pace knob never reached the straights (accel tapers to zero at the authored
    // top speed and both brain and car hard-clamp there, so only corner speeds ever scaled).
    float _profilePace = 1f;
    float _nextProfileRebuildTime;
    float ProfileStretch => Mathf.Max(1f, _profilePace);

    void PrecomputeSegmentSpeeds()
    {
        if (track == null || track.track == null || track.track.segments == null)
        {
            _segmentTargetMph = null;
            _segmentStartDistance = null;
            return;
        }

        _profilePace = Mathf.Max(0.1f, paceMultiplier);
        var segs = track.track.segments;
        _segmentTargetMph = new float[segs.Length];
        _segmentStartDistance = new float[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            _segmentStartDistance[i] = cum;
            _segmentTargetMph[i] = ComputeTargetSpeedForSegment(segs[i]) * _profilePace;
            cum += segs[i].length;
        }

    }

    // Curvature (1/m) of the ACTUAL driven line — centerline + smoothed racing-line lateral — per main sample.
    // Segment radius alone lies about corner speed: the smoothed line opens the radius at the apex (that's the
    // point of a racing line) and the lateral swing through transitions adds curvature the segments never see.
    // Speed targets and braking distances are only physics-true against the line the car really drives.
    void BuildCurvatureProfile()
    {
        if (_mainSamples == null || _mainSamples.Count < 3) { _curvatureProfile = null; return; }
        int n = _mainSamples.Count;
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var s = _mainSamples[i];
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            float lat = (_lateralProfile != null && _lateralProfile.Length == n) ? _lateralProfile[i] : 0f;
            pts[i] = s.position + right * lat;
        }

        _curvatureProfile = new float[n];
        _insideSideProfile = new float[n];
        for (int i = 0; i < n; i++)
        {
            // Segment joins can emit near-coincident samples; step outward until the neighbours are far enough
            // apart for the three-point (Menger) estimate to be stable.
            int prev = StepDistinct(pts, i, -1, 0.5f);
            int next = StepDistinct(pts, i, +1, 0.5f);
            bool degenerate = prev == i || next == i || prev == next;
            _curvatureProfile[i] = degenerate ? 0f : MengerCurvature(pts[prev], pts[i], pts[next]);
            if (!degenerate)
            {
                Vector2 a = pts[i] - pts[prev], b = pts[next] - pts[i];
                float cross = a.x * b.y - a.y * b.x;   // > 0 bends left: the inside is to the left (-lateral)
                _insideSideProfile[i] = cross > 0f ? -1f : cross < 0f ? 1f : 0f;
            }
        }

        // Light box smoothing: keeps apex curvature honest while killing sample-to-sample jitter.
        var tmp = new float[n];
        for (int p = 0; p < 2; p++)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (loop ? n - 1 : 0) : i - 1;
                int next = i == n - 1 ? (loop ? 0 : n - 1) : i + 1;
                tmp[i] = (_curvatureProfile[prev] + _curvatureProfile[i] + _curvatureProfile[next]) / 3f;
            }
            (tmp, _curvatureProfile) = (_curvatureProfile, tmp);
        }
    }

    int StepDistinct(Vector2[] pts, int from, int dir, float minDist)
    {
        int n = pts.Length;
        int idx = from;
        for (int k = 0; k < 8; k++)
        {
            int cand = idx + dir;
            if (loop) cand = (cand + n) % n;
            else if (cand < 0 || cand >= n) return idx;
            idx = cand;
            if (Vector2.Distance(pts[from], pts[idx]) >= minDist) return idx;
        }
        return idx;
    }

    static float MengerCurvature(Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
        float denom = ab * bc * ca;
        if (denom < 1e-6f) return 0f;
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return 2f * Mathf.Abs(cross) / denom;
    }

    // Lateral-accel ceiling (m/s²) matching what the dynamic model can actually generate: maxLateralG × the same
    // grip multipliers PlayerVehicleController folds into its friction circle (global+AI conditions, tyre state).
    float GripLateralAccelMps2()
    {
        float gripMul = TrackConditions.AiEffective;
        var tireModel = GetComponent<TireModel>();
        if (tireModel != null) gripMul *= tireModel.OverallGrip;
        else { var tire = GetComponent<TireState>(); if (tire != null) gripMul *= tire.GripMultiplier; }
        float g = (vehicleInfo != null && vehicleInfo.maxLateralG > 0.01f) ? vehicleInfo.maxLateralG : 1.8f;
        return g * Mathf.Max(0.05f, gripMul) * 9.81f;
    }

    void BuildSpeedProfile()
    {
        if (_mainSamples == null || _mainSamples.Count == 0 || _segmentTargetMph == null)
        {
            _speedProfile = null;
            return;
        }

        int n = _mainSamples.Count;
        _speedProfile = new float[n];
        _gripLimitProfile = new float[n];
        _bakedALatMaxMps2 = GripLateralAccelMps2();
        bool lapTurnsLeft = track != null && track.track != null && track.track.LapTurnsLeft();
        bool hasLateral = _lateralProfile != null && _lateralProfile.Length == n;
        for (int i = 0; i < n; i++)
        {
            int segIdx = SegmentIndexAt(_mainSamples[i].distance);
            float kappa = _curvatureProfile != null ? _curvatureProfile[i] : 0f;
            // How far up the banking this car's line runs here (0 bottom edge, 1 top): the top groove is steeper
            // (BankedGrip.BankAcross), and the physics banks the car at its real lane, so the plan must too - an
            // outside-lane car plans the extra the high line holds, an inside one no more than the bottom holds.
            float lat = hasLateral ? _lateralProfile[i] : 0f;
            float across = Mathf.Clamp01((lapTurnsLeft ? lat : -lat) / Mathf.Max(_mainSamples[i].width, 0.1f) + 0.5f);
            // Curvature-true target where the line actually bends; the old segment target (top speed or the
            // authored cap) elsewhere and as the fallback when no curvature data exists.
            //
            // Pace scales the corner target but is capped at the grip limit. It used to scale corners
            // straight through: AIRacingBehaviour hands every car its driver pace x AiPaceMultiplier (1.2),
            // so a quick driver planned every corner ~25% over what the tyres could hold and left the grip
            // governor, which only looks 0.7 s ahead, to find out on turn-in. That is what filled Watkins
            // Glen's run-off in practice. Pace still buys straight-line speed and takes a driver from the
            // cornerSpeedScale margin right up to the limit — which is the difference between a good driver
            // and a slow one — but no amount of it makes the car corner faster than it physically can.
            if (kappa > 1e-4f && TrackConditions.AiFlatOut)
            {
                // Flat out (a plated superspeedway): the turns are taken at full throttle like the straights. The
                // plate keeps the car under what the banking holds; the live grip governor stays as the backstop.
                _gripLimitProfile[i] = float.MaxValue;
                _speedProfile[i] = TopMph * _profilePace;
            }
            else if (kappa > 1e-4f)
            {
                // cornerCommitment is how much of the grip a driver dares to use. Pace can't separate drivers in
                // the corners (it is capped at the grip limit, and a track's calibrated AI pace puts everyone
                // well above it), so without this every car took every corner at the identical speed and the
                // field only differed on the straights — it spread out and never passed.
                float limit = ComputeTargetSpeedForCurvature(kappa, segIdx, atLimit: true, acrossFrac: across) * Mathf.Clamp(cornerCommitment, 0.5f, 1f);
                _gripLimitProfile[i] = limit;
                _speedProfile[i] = Mathf.Min(ComputeTargetSpeedForCurvature(kappa, segIdx, acrossFrac: across) * _profilePace, limit);
            }
            else
            {
                _gripLimitProfile[i] = float.MaxValue;
                _speedProfile[i] = _segmentTargetMph[segIdx];
            }
        }

        // Two wrap-aware passes per direction so values settle across the loop seam.
        int passes = loop ? 2 : 1;
        for (int p = 0; p < passes; p++)
        {
            for (int i = 1; i < n; i++) ApplyAccelLimit(i, i - 1);
            if (loop) ApplyAccelLimit(0, n - 1);
        }
        for (int p = 0; p < passes; p++)
        {
            for (int i = n - 2; i >= 0; i--) ApplyBrakeLimit(i, i + 1);
            if (loop) ApplyBrakeLimit(n - 1, 0);
        }

        // The grip ceiling gets a braking zone of its own. It caps whatever is laid on top of the baked profile —
        // the live pace ratio and, above all, the tow (+12 mph at Watkins Glen) — but only where the car already
        // is. The profile's braking curve plus a flat boost is a braking curve 5 m/s too high: a car towed down
        // the back straight braked from there, met the corner's ceiling as a sudden 5 m/s step at turn-in, and
        // ran 6 m wide at the bus stop every lap of the race. Relaxed backward, the ceiling ends the tow where
        // braking has to start.
        for (int p = 0; p < passes; p++)
        {
            for (int i = n - 2; i >= 0; i--) ApplyGripLimitBraking(i, i + 1);
            if (loop) ApplyGripLimitBraking(n - 1, 0);
        }
    }

    void ApplyGripLimitBraking(int i, int next)
    {
        float vNextMph = _gripLimitProfile[next];
        if (vNextMph >= float.MaxValue) return;   // a straight ahead: nothing to brake for
        float d = _mainSamples[next].distance - _mainSamples[i].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vNext = vNextMph * MphToMps;
        // Sample the decel curve at roughly the speed the car brakes from here: its own profile speed.
        float vHere = Mathf.Min(_gripLimitProfile[i], Mathf.Max(_speedProfile[i], vNextMph));
        float decel = SampleDecel(vHere / ProfileStretch) * ProfileStretch;
        decel *= FrictionCircleHeadroom(i, vHere * MphToMps);
        float vMaxMph = Mathf.Sqrt(vNext * vNext + 2f * decel * d) * MpsToMph;
        if (vMaxMph < _gripLimitProfile[i]) _gripLimitProfile[i] = vMaxMph;
    }

    // Corner target from the driven line's real radius: v = √(r · a_lat). Same μ the physics friction circle
    // uses, so with cornerSpeedScale ≤ 1 the car is guaranteed lateral headroom instead of being commanded into
    // saturation (which is what strews understeering cars across the track when grip/speed tuning changes).
    // `atLimit`: the most the car can take this curvature at — no cornerSpeedScale margin.
    // `acrossFrac`: how far up the banking the line runs (0 bottom edge, 0.5 the middle - the published banking -
    // 1 the top), for the progressive banking under it (BankedGrip.BankAcross).
    float ComputeTargetSpeedForCurvature(float kappa, int segIdx, bool atLimit = false, float acrossFrac = 0.5f)
    {
        float topMph = TopMph;
        float radius = 1f / kappa;

        // What the car holds on this radius, measured (AIGrip, the skid pad): the share of the tyres' peak it keeps at the
        // speed it comes out at, then what the banking under this line adds (BankedGrip: the tyres thrown into the bank
        // and gravity's pull down it). Not the peak, and not the hand-drawn VehicleInfo.corneringSpeedCurve the plan
        // used to read - that sat ~9% under the car's real grip on every radius over 200 m, which put the AI 10-20 mph
        // under what it could hold through every fast oval turn.
        float flat = Mathf.Max(_bakedALatMaxMps2 * Mathf.Max(driverGrip, 0.05f), 0.1f);
        float bank = 0f;
        float capMph = topMph;
        var segs = (track != null && track.track != null) ? track.track.segments : null;
        bool haveSeg = segs != null && segIdx >= 0 && segIdx < segs.Length;
        if (haveSeg && Mathf.Abs(segs[segIdx].banking) > 0.01f)
            bank = Draftmaster.Sim.BankedGrip.BankAcross(segs[segIdx].banking, acrossFrac);
        float baseMph = AIGrip.BankedCornerSpeed(radius, flat, bank) * MpsToMph;
        if (haveSeg)
        {
            var seg = segs[segIdx];
            // The segment's maxSpeed is not a cap here. On the generated tracks it is OvalGeometry /
            // RoadCourseGeometry's rough hint for the marker boards, worked out at a road car's 1.05 g, and it held
            // every oval corner 30-45 mph under what the car's grip and banking hold (Charlotte 109 mph planned,
            // 150 held) - the AI ran 20-40% off a real lap while the player's car, which never reads it, didn't.
            if (seg.maxSpeed > 0 && !cornerSpeedFromGrip) capMph = Mathf.Min(capMph, seg.maxSpeed);
        }
        return Mathf.Clamp(baseMph * (atLimit ? 1f : cornerSpeedScale), 5f, capMph);
    }

    // Friction circle: longitudinal authority shrinks with the lateral load already spent at this point of the
    // line — a_long = a_max · √(1 − (a_lat/a_latMax)²). This is what pushes braking zones back up the straight
    // (brake BEFORE turn-in) instead of assuming full brake force mid-corner, where the real car's rear lets go.
    float FrictionCircleHeadroom(int idx, float vMps)
    {
        if (_curvatureProfile == null || idx >= _curvatureProfile.Length || _bakedALatMaxMps2 <= 0.01f) return 1f;
        float frac = vMps * vMps * _curvatureProfile[idx] / AIGrip.Usable(_bakedALatMaxMps2, vMps);
        // Floor keeps the relaxation passes progressing even where targets sit at the lateral limit.
        return Mathf.Max(0.15f, Mathf.Sqrt(Mathf.Clamp01(1f - frac * frac)));
    }

    void ApplyAccelLimit(int i, int prev)
    {
        float d = _mainSamples[i].distance - _mainSamples[prev].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vPrev = _speedProfile[prev] * MphToMps;
        // What the engine really pulls (driven to the plan, the car gets exactly this), never less than the old
        // pace-stretched curve. A car slower than its pace still plans its own lower pull.
        float a = externalMotionController
            ? EngineAccelMps2(_speedProfile[prev]) * Mathf.Min(1f, paceMultiplier)
            : SampleAccel(_speedProfile[prev] / ProfileStretch) * ProfileStretch;
        a *= FrictionCircleHeadroom(prev, vPrev);
        float vMaxMps = Mathf.Sqrt(vPrev * vPrev + 2f * a * d);
        float vMaxMph = vMaxMps * MpsToMph;
        if (vMaxMph < _speedProfile[i]) _speedProfile[i] = vMaxMph;
    }

    void ApplyBrakeLimit(int i, int next)
    {
        float d = _mainSamples[next].distance - _mainSamples[i].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vNext = _speedProfile[next] * MphToMps;
        float decel = SampleDecel(_speedProfile[i] / ProfileStretch) * ProfileStretch;
        decel *= FrictionCircleHeadroom(i, _speedProfile[i] * MphToMps);
        float vMaxMps = Mathf.Sqrt(vNext * vNext + 2f * decel * d);
        float vMaxMph = vMaxMps * MpsToMph;
        if (vMaxMph < _speedProfile[i]) _speedProfile[i] = vMaxMph;
    }

    float ProfileAt(float distance) => SampleAlongLine(_speedProfile, distance, 0f);

    // The braking the speed plan asks for over the next second (m/s²; 0 where it isn't slowing).
    public float PlannedDecelMps2()
    {
        if (_speedProfile == null || _speedProfile.Length == 0 || _onPit) return 0f;
        float v = Mathf.Max(_currentMph * MphToMps, 1f);
        return Mathf.Max(0f, (ProfileAt(_distance) - ProfileAt(_distance + v)) * MphToMps);
    }

    [Tooltip("A car off its racing line toward the inside of a bend coming up (two wide, or on the inside of a pass) takes the tighter radius at the speed it allows, and brakes for it in time. The profile is the line's - without this the inside car arrived at line speed on a smaller radius, ran wide and into the car beside it.")]
    public bool offLineCornerSpeed = true;
    [Tooltip("How far ahead (m) the off-line corner speed looks for a bend.")]
    public float offLineScanMetres = 220f;

    // The speed (mph) allowed here by the bends ahead at this car's distance off its line: each bend's line speed scaled
    // to the tighter radius of a car that far toward its inside (v ~ sqrt(r)), plus the braking from here to it.
    // MaxValue when the car is on its line or outside it.
    float OffLineCornerCapMph(float paceRatio)
    {
        if (_curvatureProfile == null || _insideSideProfile == null || _speedProfile == null || _mainSamples == null) return float.MaxValue;
        int n = _speedProfile.Length;
        if (n != _curvatureProfile.Length || n != _mainSamples.Count) return float.MaxValue;
        float off = LateralOnTrack - UntacticalLateral;   // + = right of the line
        if (Mathf.Abs(off) < 0.4f) return float.MaxValue;
        float decel = SampleDecel(_currentMph / ProfileStretch) * ProfileStretch * 0.85f;
        if (decel <= 0.1f) return float.MaxValue;
        int i = SampleIndexAtOrBefore(_distance, n);
        float here = _mainSamples[i].distance;
        float best = float.MaxValue;
        for (int k = 0; k < n; k++)
        {
            int j = (i + k) % n;
            float d = _mainSamples[j].distance - here;
            if (d < 0f) d += _mainLength;
            if (d > offLineScanMetres) break;
            float kappa = _curvatureProfile[j];
            if (kappa < 0.002f) continue;   // r > 500 m: no bend to speak of
            float inward = off * _insideSideProfile[j];
            if (inward <= 0.2f) continue;
            float r = 1f / kappa;
            float rIn = Mathf.Max(r - inward, r * 0.5f);
            float vMps = _speedProfile[j] * paceRatio * Mathf.Sqrt(rIn / r) * MphToMps;
            float allowed = Mathf.Sqrt(vMps * vMps + 2f * decel * d) * MpsToMph;
            if (allowed < best) best = allowed;
        }
        return best;
    }

    // The speed (mph) the car races at here on a clear road, with the profile's pace scaled by paceScale and
    // capped at the grip limit — what a car placed mid-lap should already be doing.
    public float RacePaceMphAt(float distance, float paceScale = 1f)
    {
        if (_speedProfile == null) return 0f;
        return Mathf.Min(ProfileAt(distance) * Mathf.Max(0.1f, paceScale), GripLimitAt(distance));
    }

    // The grip ceiling at this distance (mph); MaxValue on a straight or with no profile built.
    float GripLimitAt(float distance) => SampleAlongLine(_gripLimitProfile, distance, float.MaxValue);

    float SampleAlongLine(float[] profile, float distance, float empty)
    {
        if (profile == null || profile.Length == 0 || _mainSamples == null) return empty;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = profile.Length;
        int lo = SampleIndexAtOrBefore(distance, n);
        int hi = (lo + 1) % n;
        float dLo = _mainSamples[lo].distance;
        float dHi = _mainSamples[hi].distance;
        if (hi <= lo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        // A straight's MaxValue next to a corner's ceiling would lerp to nonsense: take the tighter end.
        if (profile[lo] >= float.MaxValue || profile[hi] >= float.MaxValue) return Mathf.Min(profile[lo], profile[hi]);
        return Mathf.Lerp(profile[lo], profile[hi], t);
    }

    float ComputeTargetSpeedForSegment(TrackInfoV2.TrackSegment seg)
    {
        float topMph = TopMph;
        if (seg.maxSpeed > 0) return Mathf.Min(topMph, seg.maxSpeed);
        if (seg.type == TrackInfoV2.SegmentType.Straight || Mathf.Approximately(seg.angle, 0f)) return topMph;

        float radius = seg.length / Mathf.Max(Mathf.Abs(seg.angle) * Mathf.Deg2Rad, 1e-4f);
        float baseMph;
        if (vehicleInfo != null && vehicleInfo.maxLateralG > 0.01f)
        {
            // What the car holds on this radius and bank (AIGrip, measured): the track conditions (incl. the AI bonus),
            // the driver's own grip and the tyres' wear.
            float gripMul = TrackConditions.AiEffective * Mathf.Max(driverGrip, 0.05f);
            var tireModel = GetComponent<TireModel>();
            if (tireModel != null) gripMul *= tireModel.OverallGrip;
            else { var tire = GetComponent<TireState>(); if (tire != null) gripMul *= tire.GripMultiplier; }
            float aLatMps2 = vehicleInfo.maxLateralG * Mathf.Max(0.05f, gripMul) * 9.81f;
            baseMph = AIGrip.BankedCornerSpeed(radius, aLatMps2, seg.banking) * MpsToMph;
        }
        else
        {
            baseMph = fallbackCornerMph;
        }
        return Mathf.Clamp(baseMph * cornerSpeedScale, 5f, topMph);
    }

    void UpdateCornerPhase()
    {
        if (track == null || track.track == null || track.track.segments == null || _segmentStartDistance == null)
        {
            CurrentPhase = CornerPhase.Straight;
            return;
        }
        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int idx = SegmentIndexAt(d);
        if (idx < 0 || idx >= segs.Length) { CurrentPhase = CornerPhase.Straight; return; }
        var seg = segs[idx];
        if (seg.type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(seg.angle) > 0.5f)
        {
            float into = d - _segmentStartDistance[idx];
            if (into < 0f) into += _mainLength;
            float t = seg.length > 0.01f ? into / seg.length : 0f;
            if (t < 0.3f) CurrentPhase = CornerPhase.Entry;
            else if (t < 0.6f) CurrentPhase = CornerPhase.Apex;
            else CurrentPhase = CornerPhase.Exit;
            return;
        }

        // Straight: classify by proximity of next turn / recent exit.
        float distToNextTurn = float.MaxValue;
        for (int k = 1; k <= segs.Length; k++)
        {
            int n = (idx + k) % segs.Length;
            if (segs[n].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[n].angle) > 0.5f)
            {
                float gap = _segmentStartDistance[n] - d;
                if (gap < 0f) gap += _mainLength;
                distToNextTurn = gap;
                break;
            }
        }
        float prevExitDist = float.MaxValue;
        for (int k = 1; k <= segs.Length; k++)
        {
            int p = (idx - k + segs.Length) % segs.Length;
            if (segs[p].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[p].angle) > 0.5f)
            {
                float endP = _segmentStartDistance[p] + segs[p].length;
                float gap = d - endP;
                if (gap < 0f) gap += _mainLength;
                prevExitDist = gap;
                break;
            }
        }
        if (distToNextTurn < 60f) CurrentPhase = CornerPhase.Approach;
        else if (prevExitDist < 30f) CurrentPhase = CornerPhase.PostExit;
        else CurrentPhase = CornerPhase.Straight;
    }

    public int NextTurnSign(float scanDistance)
    {
        if (track == null || track.track == null || track.track.segments == null) return 0;
        if (_segmentStartDistance == null || _mainLength <= 0f) return 0;
        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int curIdx = SegmentIndexAt(d);
        for (int k = 0; k < segs.Length; k++)
        {
            int idx = (curIdx + k) % segs.Length;
            float segStart = _segmentStartDistance[idx];
            float gap = segStart - d;
            if (gap < 0f) gap += _mainLength;
            if (gap > scanDistance) break;
            if (segs[idx].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[idx].angle) > 0.5f)
                return segs[idx].angle > 0f ? 1 : -1;
        }
        return 0;
    }

    void FixedUpdate()
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return;

        // Start lanes: the pace lap's low two-wide lines, held after the green until up to speed.
        StepStartLane();
        // Passing by lane change (lanePassing): may set a new ovalLane, re-laid just below on this same step.
        StepLaneChange();
        // A new oval lane assignment: re-lay the line, and the curvature and speed plan built on it.
        RelayLaneNow();

        // Pre-grid hold: sit parked in the pit box / at the pit exit until the formation lap begins.
        // Still Place() so the pose (and the commanded point feeding any dynamic model) tracks the box,
        // but advance nothing and command zero speed so dynamic-AI cars hold station too.
        if (IsHeldStill)
        {
            speed = 0f;
            _currentMph = 0f;
            Place();
            SettleIntoHold();
            // Pin the body to the box even under a dynamic motion controller. The controller's one-shot
            // seed can latch the wrong pose (its FixedUpdate may run before this Start places the car), and
            // a frozen car has no speed to recover. Writing the transform here guarantees it sits on its box;
            // a zero-velocity PlayerVehicleController preserves it, so they agree regardless of update order.
            if (externalMotionController && track != null)
            {
                Vector3 wp = track.transform.TransformPoint(new Vector3(CommandedLocalPos.x, CommandedLocalPos.y, 0f));
                transform.position = new Vector3(wp.x, wp.y, transform.position.z);
                transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? CommandedHeadingDeg - 90f : CommandedHeadingDeg) + angleOffsetDeg);
            }
            return;
        }
        _wasHeld = false;
        _droveSinceHold = true;

        // Called in on an apron oval: stay on the main spline, down on the apron, until pit road comes down to
        // meet it - committing at the node out on the centreline snapped the car's lane distance tens of metres
        // ahead of the car.
        bool holdForPitJoin = usePitLane && !_onPit && _apronEntryActive && ApronGapToPitJoin() > 0f;
        if (usePitLane != _onPit && !holdForPitJoin)
        {
            // Lane switch — if entering pit, reset tires (service stop). Out-of-pit doesn't touch wear.
            if (usePitLane)
            {
                var tire = GetComponent<TireState>();
                if (tire != null) tire.PitReset();
            }
            _onPit = usePitLane;
            ClearPitApproach();
            _apronExitBlend = 0f;
            // Land on the new lane at the point nearest the car's current position (no teleport to lane start),
            // carrying its current lateral as a bias that eases out — same continuous merge as the pit exit.
            RejoinSplineContinuous(transform.position);
        }

        float length = _onPit ? _pitLength : _mainLength;
        if (length <= 0f) return;

        if (vehicleInfo != null)
        {
            float targetMph;
            if (_onPit)
            {
                float pitLimit = track != null && track.track != null ? track.track.pitSpeedLimit : 50f;
                // In a green race the drawn lane's own plan: the limit to the exit line, then as fast as the
                // lane's bends allow on level ground. Otherwise (formation, pace car) the limit to the end.
                float laneMph = pitExitAtGripLimit && RaceStart.IsGreen ? PitSpeedPlanAt(_distance) : pitLimit;
                // aiMaxSpeedMph applies on the lane too: PitStopController's pit ACC queues a car behind
                // slower/stopped lane traffic through it (every other writer leaves it at/above the limit).
                targetMph = pitStopHold ? 0f : Mathf.Min(laneMph, aiMaxSpeedMph); // hold = a service stop at the box
            }
            else if (_speedProfile != null)
            {
                // The profile is baked at _profilePace; between (rate-limited) rebuilds the residual
                // ratio keeps the response live, so formation ramps and slider drags act immediately.
                float paceNow = Mathf.Max(0.1f, paceMultiplier);
                float ratio = paceNow / _profilePace;
                if ((ratio > 1.05f || ratio < 0.95f) && Time.time >= _nextProfileRebuildTime)
                {
                    _nextProfileRebuildTime = Time.time + 2f;
                    PrecomputeSegmentSpeeds();
                    BuildSpeedProfile();
                    ratio = 1f;
                }
                // The live pace ratio and a draft boost both scale on top of the baked profile, so both are
                // capped at the grip ceiling here too, or a pace change between rebuilds (or a tow into a
                // corner) would put the car back over it.
                targetMph = Mathf.Min(ProfileAt(_distance) * ratio + aiSpeedBoostMph, GripLimitAt(_distance));
                if (offLineCornerSpeed) targetMph = Mathf.Min(targetMph, OffLineCornerCapMph(ratio));
                targetMph = Mathf.Min(targetMph, ApronFlatCapMph());
                DesiredMph = targetMph;
                if (aiMaxSpeedMph < targetMph) targetMph = aiMaxSpeedMph;
                if (_pitApproachCapMph < targetMph) targetMph = _pitApproachCapMph;
                // An oval lane train: the dynamic model's input driver holds the gap itself (lift first, so it is
                // kept out of TargetMph, which it brakes to); a kinematic car just takes the cap.
                StepTrain();
                if (TrainActive && !externalMotionController && TrainCapMps * MpsToMph < targetMph)
                    targetMph = TrainCapMps * MpsToMph;
            }
            else
            {
                targetMph = 0f;
                DesiredMph = 0f;
            }
            TargetMph = targetMph;
            UpdateSpeedToward(targetMph);
            // Driven to TargetMph (driveToTarget, and flat out on the plate tracks), not along this ramp - so the
            // brain's speed IS the car's. Left to ramp on its own it ran 25 mph ahead of the car, and every closing
            // speed the racing brain judged a pack by was fiction.
            if (DrivesToTarget)
                _currentMph = externalActualSpeedMps * MpsToMph;
            speed = _currentMph * MphToMps;
        }

        UpdateCornerPhase();

        // Advance along the spline by the ACTUAL car speed when an external dynamic model drives it, so the
        // commanded path point stays with the car (a stalled/contacted car doesn't get left behind by its brain).
        // CommandedSpeedMps still carries the brain's DESIRED speed (set from `speed` in Place) for the provider.
        float advanceSpeed = externalMotionController ? externalActualSpeedMps : speed;
        _distance += advanceSpeed * Time.fixedDeltaTime;
        if (externalMotionController && !_onPit) ResyncToCar();

        // Hard-park target on the pit lane (safety car parking near the entrance). Deterministic: pin the car the
        // instant it reaches the target distance, independent of braking distance or whether vehicleInfo exists —
        // otherwise a kinematic pace car with no decel curve rolls to the pit-spline end and parks out by the exit.
        if (_onPit && pitParkDistance >= 0f && _distance >= pitParkDistance)
        {
            _distance = pitParkDistance;
            _currentMph = 0f;
            speed = 0f;
            pitStopHold = true;
            Place();
            return;
        }

        // On an apron oval in a race, leave pit road where it comes up level with the apron and run the apron
        // from there (not the formation lap: the field files out of the pits onto the pace car's train).
        bool apronExit = autoPitExit && _onPit && RaceStart.IsGreen && ApronPitRoad;
        if (apronExit) EnsureApronGeometry();
        apronExit = apronExit && _apronPitCutover >= 0f && _distance >= _apronPitCutover;

        if (autoPitExit && _onPit && _pitLength > 0f && (apronExit || _distance >= _pitLength * pitExitThreshold))
        {
            // Rejoin the main spline WITHOUT a positional jump. The authored pitExitDistance node need not line
            // up with the physical end of the pit lane, so teleporting there warps the car forward (and the
            // lateral discontinuity spikes the lean into a phantom 360). Instead continue from the main-spline
            // point nearest the car's current position, and carry its current lateral as a bias that eases out.
            _onPit = false;
            usePitLane = false;
            // Any pit-only lateral (the wall-side box lane a pit start parks on) must not leak onto the
            // track. Zeroed BEFORE the rejoin so the merge bias absorbs it and eases it out continuously.
            lateralOffset = 0f;
            length = _mainLength;
            // Set before the rejoin so the merge bias is measured against the apron lane, not the racing line.
            _apronExitBlend = apronExit ? 1f : 0f;
            RejoinSplineContinuous(transform.position);
            if (apronExit) BeginApronExit();
        }
        else if (!_onPit && _apronExitBlend > 0f)
            StepApronExit(advanceSpeed * Time.fixedDeltaTime);

        if (loop && !_onPit)
        {
            while (_distance >= length) _distance -= length;
            while (_distance < 0f) _distance += length;
        }
        else
        {
            _distance = Mathf.Clamp(_distance, 0f, length);
        }
        Place();
    }

    void UpdateSpeedToward(float targetMph)
    {
        // Pace above 1 stretches the longitudinal envelope — accel/decel curves sampled at the
        // pace-normalised speed and scaled up, top-speed ceiling raised to match. Sub-1 pace keeps
        // the stock envelope (formation laps just chase lower targets with normal urgency).
        float stretch = Mathf.Max(1f, paceMultiplier);
        float topMph = TopMph * stretch;
        if (_currentMph < targetMph)
        {
            float accel = SampleAccel(_currentMph / stretch) * stretch;
            // Gearbox shapes the live accel (gear torque curve + brief drive-cut on a shift). It averages ~1
            // so overall pace is preserved; the static speed profile is left untouched (built without it).
            if (_gearbox != null) accel *= _gearbox.AccelMultiplier;
            _currentMph += accel * MpsToMph * Time.fixedDeltaTime;
            if (_currentMph > targetMph) _currentMph = targetMph;
        }
        else if (_currentMph > targetMph)
        {
            float decelMphPerSec = SampleDecel(_currentMph / stretch) * stretch * MpsToMph;
            // Avoidance can demand a firmer brake than the authored decel curve (which may be too weak to stop a
            // closing car before contact). aiMinDecelMphPerSec is the floor on braking authority for that frame.
            if (aiMinDecelMphPerSec > 0f) decelMphPerSec = Mathf.Max(decelMphPerSec, aiMinDecelMphPerSec);
            _currentMph -= decelMphPerSec * Time.fixedDeltaTime;
            if (_currentMph < targetMph) _currentMph = targetMph;
        }
        // aiSpeedBoostMph is aero (slipstream) — it legitimately carries a car past its stock flat-out
        // speed, so the ceiling rises with it. Without this the boost is a dead letter on the straights.
        _currentMph = Mathf.Clamp(_currentMph, 0f, topMph + Mathf.Max(0f, aiSpeedBoostMph));
    }

    // The car's top speed (mph) at this track: the vehicle's, stretched by the package (TrackConditions.PackageSpeedScale).
    float TopMph => (vehicleInfo != null ? vehicleInfo.topSpeed : 200f) * Mathf.Max(0.1f, TrackConditions.PackageSpeedScale);

    // The engine's pull (m/s^2) at this speed as the physics gives this car (PlayerVehicleController): the accel curve
    // stretched by the AI's power, times the conditions' power (EffectivePower), that AI power and the package. The
    // speed plan's straights used the bare curve, a fifth under what the engine pulls, and driving to the plan held the
    // throttle at ~83% down every straight - the AI never once ran wide open.
    float EngineAccelMps2(float mph)
    {
        float aiPower = TrackConditions.AiPowerScale;
        return SampleAccel(mph / Mathf.Max(1f, aiPower)) * TrackConditions.EffectivePower * aiPower
               * Mathf.Max(0.1f, TrackConditions.PackageSpeedScale);
    }

    float SampleAccel(float mph)
    {
        if (vehicleInfo == null || vehicleInfo.accelerationCurve == null || vehicleInfo.accelerationCurve.length == 0)
            return fallbackAccel;
        return Mathf.Max(0f, vehicleInfo.accelerationCurve.Evaluate(mph));
    }

    float SampleDecel(float mph)
    {
        if (vehicleInfo == null || vehicleInfo.decelerationCurve == null || vehicleInfo.decelerationCurve.length == 0)
            return fallbackDecel;
        return Mathf.Max(0.1f, vehicleInfo.decelerationCurve.Evaluate(mph));
    }

    int SegmentIndexAt(float distance)
    {
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _segmentStartDistance.Length;
        int idx = 0;
        for (int i = 0; i < n; i++)
        {
            if (_segmentStartDistance[i] <= distance) idx = i;
            else break;
        }
        return idx;
    }

    // Lateral room (m) from the car's current line to each track edge. +lateral is right of travel, so
    // rightRoom is space to move right, leftRoom space to move left. Lets the AI pick the roomier passing side.
    public bool GetLateralRoom(out float leftRoom, out float rightRoom)
    {
        leftRoom = rightRoom = 0f;
        if (_leftBoundProfile == null || _rightBoundProfile == null || !_hasPrevLateral) return false;
        BoundsAt(_distance, out float lo, out float hi);
        float cur = _prevLateral;
        rightRoom = Mathf.Max(0f, hi - cur);
        leftRoom = Mathf.Max(0f, cur - lo);
        return true;
    }

    // Curvature (1/m) of the driven line `aheadMetres` up the road from the brain's point, and which side of travel
    // the inside of the bend is on (-1 left, +1 right, 0 straight). 0 on the pit lane or with no profile.
    public float LineCurvatureAhead(float aheadMetres, out float insideSide)
    {
        insideSide = 0f;
        if (_onPit || _curvatureProfile == null || _insideSideProfile == null || _mainSamples == null || _mainLength <= 0f)
            return 0f;
        int n = Mathf.Min(_curvatureProfile.Length, _insideSideProfile.Length);
        if (n == 0) return 0f;
        float d = ((_distance + aheadMetres) % _mainLength + _mainLength) % _mainLength;
        int i = SampleIndexAtOrBefore(d, n);
        insideSide = _insideSideProfile[i];
        return _curvatureProfile[i];
    }

    // Smallest turn radius (m) on the path within scanDistance ahead, including the current position.
    // float.MaxValue when the road ahead is straight. The AI input driver uses this to shorten its steering
    // lookahead on tight corners (aim short = turn in hard), keep it long on gradual ones (smooth), and to
    // cap its commanded speed to the live grip limit. Prefers the DRIVEN-line curvature profile (the radius
    // the car actually has to negotiate); falls back to raw segment radii when no profile exists.
    public float CurvatureRadiusAhead(float scanDistance)
    {
        if (!_onPit && _curvatureProfile != null && _mainSamples != null && _mainLength > 0f)
        {
            int n = _curvatureProfile.Length;
            float start = DistanceOnTrack;
            // Samples are distance-ordered: find the first at/after the car, then walk only the scan window.
            float wrapped = ((start % _mainLength) + _mainLength) % _mainLength;
            int i0 = SampleIndexAtOrBefore(wrapped, n);
            float maxK = 0f;
            for (int k = 0; k < n; k++)
            {
                int i = (i0 + k) % n;
                float ahead = _mainSamples[i].distance - start;
                if (ahead < 0f) ahead += _mainLength;
                if (k > 0 && ahead > scanDistance) break;
                if (_curvatureProfile[i] > maxK) maxK = _curvatureProfile[i];
            }
            return maxK > 1e-4f ? 1f / maxK : float.MaxValue;
        }

        if (track == null || track.track == null || track.track.segments == null) return float.MaxValue;
        if (_segmentStartDistance == null || _mainLength <= 0f) return float.MaxValue;

        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int curIdx = SegmentIndexAt(d);
        float minR = float.MaxValue;
        for (int k = 0; k < segs.Length; k++)
        {
            int idx = (curIdx + k) % segs.Length;
            if (k > 0)
            {
                float gap = _segmentStartDistance[idx] - d;
                if (gap < 0f) gap += _mainLength;
                if (gap > scanDistance) break;
            }
            var s = segs[idx];
            if (s.type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(s.angle) > 0.5f)
            {
                float r = s.length / Mathf.Max(Mathf.Abs(s.angle) * Mathf.Deg2Rad, 1e-4f);
                if (r < minR) minR = r;
            }
        }
        return minR;
    }

    // Land on the current lane (_onPit must already be set) at the point nearest worldNow — no teleport to the
    // lane start. Carries the car's current lateral as a bias that eases onto the lane line, and clears the lean
    // so the switch can't spike a phantom 360. Used by both the pit-entry and pit-exit transitions.
    void RejoinSplineContinuous(Vector3 worldNow)
    {
        if (track == null || _mainSamples == null || _mainSamples.Count < 2) { _distance = 0f; return; }
        Vector2 localNow = track.transform.InverseTransformPoint(worldNow);

        // worldNow is the car CENTRE, but _distance indexes the REAR-AXLE sample point — Place() builds the centre as
        // rearAxle + forward*rearAxleToCenter. Snapping _distance to the centre's nearest point makes the very next
        // Place() shove the centre back by |rearAxleToCenter| (~2.4m): the small backward jump seen on the pit-exit
        // merge. Resolve the rear-axle point from the arriving heading and snap _distance to THAT instead.
        Vector2 rearAxleLocal = localNow;
        if (_hasPrevLateral)
        {
            float hr = CommandedHeadingDeg * Mathf.Deg2Rad;
            rearAxleLocal = localNow - new Vector2(Mathf.Cos(hr), Mathf.Sin(hr)) * rearAxleToCenter;
        }
        Vector3 rearAxleWorld = track.transform.TransformPoint(new Vector3(rearAxleLocal.x, rearAxleLocal.y, 0f));

        TrackBuilder.Sample s;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            _distance = track.NearestPitDistance(rearAxleWorld);
            s = track.SamplePitAt(_distance, _pitSamples);
        }
        else
        {
            _distance = track.NearestCenterlineDistance(rearAxleWorld);
            s = track.SampleAt(_distance, _mainSamples);
        }

        float actualLat = Vector2.Dot(rearAxleLocal - s.position, s.normal);
        // The base lateral exactly as Place() will build it on the new lane, bounds clamp included. The line may
        // run outside the road bounds (a player-lap line uses its run-off corridor — Watkins Glen's sits 14 m
        // off the road at the pit rejoin); Place clamps it back, and a bias measured against the unclamped value
        // came out that much too big — every car rejoining at Watkins Glen landed 12 m right, past the barrier.
        float baseLat = lateralOffset + TacticalLateral + _collisionLateral;
        if (!_onPit)
        {
            baseLat += LateralAt(_distance) + LaneShiftAt(_distance);
            if (_leftBoundProfile != null && _rightBoundProfile != null)
            {
                BoundsAt(_distance, out float boundLo, out float boundHi);
                baseLat = Mathf.Clamp(baseLat, boundLo, boundHi);
            }
            // Leaving pit road onto the apron: Place holds the apron lane, so that is what the bias carries to.
            if (_apronExitBlend > 0f) baseLat = Mathf.Lerp(baseLat, ApronLaneAt(_distance), _apronExitBlend);
        }
        _mergeLatBias = Mathf.Clamp(actualLat - baseLat, -20f, 20f);

        // Heading bias: the new lane's tangent vs the facing the car arrives with. CommandedHeadingDeg is in the
        // same track-local frame as the sample tangent (both atan2 of a local-space direction), so the delta is
        // the kink at the join. Eased to 0 in Place() so the car turns smoothly onto the new tangent instead of
        // snapping. Only when we have a valid arriving pose (_hasPrevLateral); a cold engage has no facing to keep.
        if (_hasPrevLateral)
        {
            float newHeadingDeg = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;
            _mergeHeadingBias = Mathf.Clamp(Mathf.DeltaAngle(newHeadingDeg, CommandedHeadingDeg), -90f, 90f);
        }
        else _mergeHeadingBias = 0f;

        _hasPrevLateral = false;
        _hasLineSmoothed = false; // reseed the line low-pass from the new lane's raw offset (no stale drag)
    }

    // Pull the brain's track distance back onto where the car physically is.
    //
    // Advancing by the car's own speed only keeps the two together if the car drives the centreline. It
    // doesn't: the racing line cuts the inside of every corner (Watkins Glen's is 28 m a lap shorter than the
    // centreline, COTA's 147 m), so a brain counting the car's speed falls behind a little in every turn and
    // never catches up. A lap in, it is ten metres behind the car — braking points arrive late, the steering
    // aim point that should sit 20 m ahead sits 10 m ahead and the car weaves, and the gap maths the rest of the
    // field reads are wrong too. Dead-reckoning stays as the prediction; this corrects it against the car.
    void ResyncToCar()
    {
        if (track == null || _mainSamples == null || _mainSamples.Count < 2 || _mainLength <= 0f) return;
        Vector2 local = track.transform.InverseTransformPoint(transform.position);
        // _distance indexes the point PathPointAheadOfCentre in front of the car's centre.
        float guess = _distance + _collisionLongitudinal - PathPointAheadOfCentre;
        if (!ProjectOntoMainNear(local, guess, resyncWindow, out float centre)) return;
        var cs = track.SampleAt(centre, _mainSamples);
        _carLateral = Vector2.Dot(local - cs.position, new Vector2(cs.tangent.y, -cs.tangent.x));
        _hasCarLateral = true;
        float err = centre - guess;
        if (loop)
        {
            if (err > _mainLength * 0.5f) err -= _mainLength;
            else if (err < -_mainLength * 0.5f) err += _mainLength;
        }
        _distance += err;
        BrainLagMeters = err;
    }

    [Tooltip("Metres either side of the brain's own estimate searched when re-locating the car on the centreline each step. Keeps the search local, so a hairpin or a parallel stretch of road can never capture the car.")]
    public float resyncWindow = 40f;

    // How far the car was ahead (+) of the brain's dead-reckoned distance on the last step, before correction.
    public float BrainLagMeters { get; private set; }

    // Nearest point on the main centreline to `local`, searched only within `window` metres of `guess`.
    bool ProjectOntoMainNear(Vector2 local, float guess, float window, out float distance)
    {
        distance = guess;
        int n = _mainSamples.Count;
        if (n < 2 || _mainLength <= 0f) return false;
        float g = loop ? ((guess % _mainLength) + _mainLength) % _mainLength : Mathf.Clamp(guess, 0f, _mainLength);

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_mainSamples[mid].distance <= g) lo = mid; else hi = mid;
        }

        int segCount = n - 1; // segment i joins sample i to sample i+1
        float best = float.MaxValue;
        bool found = false;
        for (int dir = -1; dir <= 1; dir += 2)
        {
            for (int k = (dir < 0 ? 1 : 0); k < segCount; k++)
            {
                int i = lo + dir * k;
                if (loop) i = ((i % segCount) + segCount) % segCount;
                else if (i < 0 || i >= segCount) break;

                var a = _mainSamples[i];
                var b = _mainSamples[i + 1];
                float off = dir < 0 ? g - b.distance : a.distance - g;
                if (loop && off < -_mainLength * 0.5f) off += _mainLength;
                if (off > window) break;

                Vector2 ab = b.position - a.position;
                float denom = ab.sqrMagnitude;
                float t = denom > 1e-6f ? Mathf.Clamp01(Vector2.Dot(local - a.position, ab) / denom) : 0f;
                float dist2 = (a.position + ab * t - local).sqrMagnitude;
                if (dist2 < best)
                {
                    best = dist2;
                    distance = Mathf.Lerp(a.distance, b.distance, t);
                    found = true;
                }
            }
        }
        return found;
    }

    // Path point on the current spline a given distance AHEAD of the car, in track-local space. The AI input
    // driver steers toward this (pure-pursuit lookahead): aiming at the car's own position (CommandedLocalPos)
    // gives it nothing to track and makes it wander, so a lookahead point is what lets it hold the racing line.
    public Vector2 PathPointAhead(float aheadMeters)
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return CommandedLocalPos;

        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            float dp = Mathf.Clamp(_distance + aheadMeters, 0f, _pitLength);
            var sp = track.SamplePitAt(dp, _pitSamples);
            Vector2 rp = new Vector2(sp.tangent.y, -sp.tangent.x);
            return sp.position + rp * (lateralOffset + tacticalLateralOffset);
        }

        float d = _distance + aheadMeters;
        if (_mainLength > 0f) d = ((d % _mainLength) + _mainLength) % _mainLength;
        var s = track.SampleAt(d, _mainSamples);
        Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
        float lat = LateralAt(d) + LaneShiftAt(d) + lateralOffset + TacticalLateral;
        if (_leftBoundProfile != null && _rightBoundProfile != null)
        {
            BoundsAt(d, out float lo, out float hi);
            lat = Mathf.Clamp(lat, lo, hi);
        }
        // Pit road on an apron oval, as Place lays it: the dynamic AI steers at this point, not at Place's pose.
        if (_apronEntryActive)
        {
            float mps = Mathf.Max(1f, externalMotionController ? externalActualSpeedMps : speed);
            lat = Mathf.MoveTowards(_apronEntryLat, ApronLaneAt(d), apronDropRate * aheadMeters / mps);
        }
        else if (_apronExitBlend > 0f)
        {
            float b = _apronExitBlend * _apronExitBlend * (3f - 2f * _apronExitBlend);
            lat = Mathf.Lerp(lat, ApronLaneAt(d), b);
        }
        return s.position + right * lat;
    }

    bool _wasHeld;
    bool _droveSinceHold;
    bool _settling;
    Vector2 _settlePos;
    float _settleHeadingDeg;

    // A car that drives into its box and is then held (practice: pulled up, parkedHold) stops wherever its
    // brakes and physics left it — short of the spot, still half across the lane, nose angled in from the turn.
    // Pinning it straight onto the box pose was a visible jump. Instead the hold starts from the car's actual
    // pose and eases onto the spot. A car that has never driven (spawned into the hold) still pins at once, which
    // is what the seed-latch guard needs. Runs after Place(), so it overrides the commanded pose the dynamic
    // model is re-seeded from as well as a kinematic car's transform.
    void SettleIntoHold()
    {
        if (!_wasHeld)
        {
            _wasHeld = true;
            _settling = _droveSinceHold && track != null;
            _droveSinceHold = false;
            if (_settling)
            {
                _settlePos = track.transform.InverseTransformPoint(transform.position);
                _settleHeadingDeg = transform.eulerAngles.z - angleOffsetDeg + (spriteFacesUp ? 90f : 0f);
            }
        }
        if (!_settling) return;

        Vector2 target = CommandedLocalPos;
        float targetHeading = CommandedHeadingDeg;
        _settlePos = Vector2.MoveTowards(_settlePos, target, holdSettleSpeed * Time.fixedDeltaTime);
        _settleHeadingDeg = Mathf.MoveTowardsAngle(_settleHeadingDeg, targetHeading, holdSettleDegPerSec * Time.fixedDeltaTime);
        if ((_settlePos - target).sqrMagnitude < 1e-6f && Mathf.Abs(Mathf.DeltaAngle(_settleHeadingDeg, targetHeading)) < 0.01f)
            _settling = false;

        CommandedLocalPos = _settlePos;
        CommandedHeadingDeg = _settleHeadingDeg;
        if (externalMotionController) return;   // the hold branch writes the transform from the commanded pose
        Vector3 wp = track.transform.TransformPoint(new Vector3(_settlePos.x, _settlePos.y, 0f));
        transform.position = new Vector3(wp.x, wp.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? _settleHeadingDeg - 90f : _settleHeadingDeg) + angleOffsetDeg);
    }

    void Place()
    {
        // Decay collision offsets toward 0, but hold them one step after a fresh contact so cars actually separate.
        if (_contactHold) _contactHold = false;
        else
        {
            _collisionLateral = Mathf.MoveTowards(_collisionLateral, 0f, 6f * Time.fixedDeltaTime);
            _collisionLongitudinal = Mathf.MoveTowards(_collisionLongitudinal, 0f, 6f * Time.fixedDeltaTime);
        }
        // Ease the pit-merge lateral + heading biases out so the car drifts from its pit-exit line/facing onto the
        // racing line and tangent — a continuous turn-in instead of a sideways pop or a rotation snap.
        _mergeLatBias = Mathf.MoveTowards(_mergeLatBias, 0f, pitMergeEaseSpeed * Time.fixedDeltaTime);
        _mergeHeadingBias = Mathf.MoveTowards(_mergeHeadingBias, 0f, pitMergeHeadingEaseDegPerSec * Time.fixedDeltaTime);

        float placeDistance = _distance + _collisionLongitudinal;

        TrackBuilder.Sample sample;
        float length;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            sample = track.SamplePitAt(placeDistance, _pitSamples);
            length = _pitLength;
        }
        else
        {
            sample = track.SampleAt(placeDistance, _mainSamples);
            length = _mainLength;
        }

        // Racing-line lateral, temporally smoothed. The authored line steps between entry/apex/exit anchors, so the
        // raw value kinks at segment boundaries — the rear axle (built straight off this offset) snaps into and out
        // of turns. A generous low-pass rounds those kinks so the car flows through the corner. Reset on a lane
        // change (RejoinSplineContinuous clears _hasLineSmoothed) so it doesn't drag a stale value across the merge.
        float rawLineLateral = !_onPit ? LateralAt(placeDistance) + LaneShiftAt(placeDistance) : 0f;
        _lineLatSmoothed = _hasLineSmoothed ? Mathf.Lerp(rawLineLateral, _lineLatSmoothed, cornerSmoothing) : rawLineLateral;
        _hasLineSmoothed = true;
        float lineLateral = _lineLatSmoothed;
        float baseLateral = lateralOffset + TacticalLateral + lineLateral + _collisionLateral;
        if (!_onPit && _pitApproachBlend > 0f)
            baseLateral = Mathf.Lerp(baseLateral, _pitApproachLateral, _pitApproachBlend);
        if (!_onPit && _leftBoundProfile != null && _rightBoundProfile != null)
        {
            BoundsAt(placeDistance, out float boundLo, out float boundHi);
            // Where the line itself runs onto the run-off (lineUsesRunOff), the car may go as far as it does.
            float onLine = lateralOffset + lineLateral;
            baseLateral = Mathf.Clamp(baseLateral, Mathf.Min(boundLo, onLine), Mathf.Max(boundHi, onLine));
        }
        // Pit road on an apron oval: down on the apron for the run-in, and after the pit exit until the back
        // straight. Laid over the bounds clamp - the apron is below the road the bounds describe.
        if (!_onPit && _apronEntryActive)
            baseLateral = _apronEntryLat;
        else if (!_onPit && _apronExitBlend > 0f)
        {
            float b = _apronExitBlend * _apronExitBlend * (3f - 2f * _apronExitBlend);
            baseLateral = Mathf.Lerp(baseLateral, ApronLaneAt(placeDistance), b);
        }
        // The pit-merge bias carries the car from its REAL pit-exit position onto the racing line. It must NOT be
        // bounds-clamped, or the car snaps (warps) onto the track edge the instant it leaves the pit lane — the
        // pit lane sits laterally outside the track, so clamping deletes the offset. Added after the clamp and
        // eased to zero (pitMergeEaseSpeed) so the car slides smoothly onto the line instead of teleporting.
        float totalLateral = baseLateral + _mergeLatBias;
        Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
        if (track != null)
        {
            _lastWorldRight = ((Vector2)track.transform.TransformVector(new Vector3(right.x, right.y, 0f))).normalized;
            _lastWorldForward = ((Vector2)track.transform.TransformVector(new Vector3(sample.tangent.x, sample.tangent.y, 0f))).normalized;
        }
        else { _lastWorldRight = right; _lastWorldForward = sample.tangent; }
        Vector2 rearAxle = sample.position + right * totalLateral;
        float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg;
        float lateralRate = (_hasPrevLateral && Time.fixedDeltaTime > 0f) ? (totalLateral - _prevLateral) / Time.fixedDeltaTime : 0f;
        // Geometric turn-in: on an offset racing line the car's true direction of travel is the centreline tangent
        // rotated by atan2(lateral velocity, forward velocity). Through a chicane the offset swings hard, so this
        // yaw is large and MUST come from the real kinematics, or the car renders crabbed (pointing down the
        // centreline, not down its actual path). Speed-normalised so the angle is right at any pace; the floor
        // on forward speed guards against an atan2 blow-up at a standstill (where lateralRate is ~0 anyway).
        float forwardMps = Mathf.Max(Mathf.Abs(speed), 0.5f);
        float pathYawRaw = Mathf.Atan2(-lateralRate, forwardMps) * Mathf.Rad2Deg;
        // Smooth the turn-in yaw too (same generous filter), so the body doesn't snap as the car points onto its
        // path at corner entry/exit. Cold first frame takes the raw angle so there's no start-up swing.
        _pathYawSmoothed = _hasPrevLateral ? Mathf.Lerp(pathYawRaw, _pathYawSmoothed, cornerSmoothing) : pathYawRaw;
        // No cosmetic lean on top: a top-down car shows body roll as nothing at all, so extra yaw for "leaning
        // into the turn" only pointed the car off its own path — up to 5° on a weave, more than twice the real
        // turn-in, and it read as the car rocking side to side through every line change.
        LateralRate = lateralRate;
        _prevLateral = totalLateral;
        _hasPrevLateral = true;
        float carHeadingDeg = angleDeg + _pathYawSmoothed + _mergeHeadingBias;
        float carHeadingRad = carHeadingDeg * Mathf.Deg2Rad;
        Vector2 carForward = new Vector2(Mathf.Cos(carHeadingRad), Mathf.Sin(carHeadingRad));
        Vector2 finalPos = rearAxle + carForward * rearAxleToCenter;

        CommandedLocalPos = finalPos;
        CommandedHeadingDeg = carHeadingDeg;
        CommandedSpeedMps = speed;

        if (externalMotionController) return;

        Vector3 worldPos = track != null ? track.transform.TransformPoint(new Vector3(finalPos.x, finalPos.y, 0)) : new Vector3(finalPos.x, finalPos.y, 0);
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? carHeadingDeg - 90f : carHeadingDeg) + angleOffsetDeg);
    }
}
