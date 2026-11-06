using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The two oval lanes (Docs/Oval-Lanes.md): the pure geometry, and one dynamic AI car alone in each lane at Texas
// (FortWorth) holding it lap after lap. The sim builds the car the way AILapSimTests does (SplineDriver brain,
// PlayerVehicleController physics, SplineInputDriver between them) and steps the three by hand - no play mode.
public class OvalLaneTests
{
    // ---------------------------------------------------------------- geometry

    [Test]
    public void LanesSitInTheBottomGrooveAndACarWidthAboveIt()
    {
        var spec = OvalLanes.Default;
        float width = 60f * TrackDimensions.MetresPerFoot;   // Texas: 60 ft
        float inside = OvalLanes.Lateral(OvalLanes.Lane.Inside, width, -1f, spec);
        float outside = OvalLanes.Lateral(OvalLanes.Lane.Outside, width, -1f, spec);
        // Infield on the left (anticlockwise): lanes are negative lateral, the inside lane further left.
        Assert.AreEqual(-(width * 0.5f - spec.insideEdgeMargin), inside, 1e-4f);
        Assert.AreEqual(spec.laneSpacing, outside - inside, 1e-4f);
        Assert.Greater(width * 0.5f + outside, spec.outsideEdgeMargin, "The outside lane must stay clear of the outside edge.");
        // Infield on the right mirrors it.
        Assert.AreEqual(-inside, OvalLanes.Lateral(OvalLanes.Lane.Inside, width, +1f, spec), 1e-4f);
    }

    [Test]
    public void ANarrowRoadSqueezesTheOutsideLaneNotTheInside()
    {
        var spec = OvalLanes.Default;
        float width = spec.insideEdgeMargin + spec.outsideEdgeMargin + 1f;   // room for 1 m of spacing only
        float inside = OvalLanes.HeightAboveInsideEdge(OvalLanes.Lane.Inside, width, spec);
        float outside = OvalLanes.HeightAboveInsideEdge(OvalLanes.Lane.Outside, width, spec);
        Assert.AreEqual(spec.insideEdgeMargin, inside, 1e-4f);
        Assert.AreEqual(width - spec.outsideEdgeMargin, outside, 1e-4f);
        // Too narrow for even one lane: both on the centreline.
        Assert.AreEqual(1.5f, OvalLanes.HeightAboveInsideEdge(OvalLanes.Lane.Outside, 3f, spec), 1e-4f);
    }

    [Test]
    public void TheInfieldSideFollowsTheWayTheLapTurns()
    {
        var ccw = new Vector2[72];
        for (int i = 0; i < ccw.Length; i++)
        {
            float a = i * 5f * Mathf.Deg2Rad + Mathf.PI * 0.5f;   // tangent of an anticlockwise circle
            ccw[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        Assert.AreEqual(-1f, OvalLanes.InsideSide(ccw));
        Array.Reverse(ccw);
        for (int i = 0; i < ccw.Length; i++) ccw[i] = -ccw[i];
        Assert.AreEqual(+1f, OvalLanes.InsideSide(ccw));
    }

    [Test]
    public void OnlyOvalsHaveLanes()
    {
        Assert.IsTrue(OvalLanes.AppliesTo("FortWorth"));
        Assert.IsTrue(OvalLanes.AppliesTo("Daytona"));
        Assert.IsTrue(OvalLanes.AppliesTo("Martinsville"));
        Assert.IsFalse(OvalLanes.AppliesTo("WatkinsGlen"));
        Assert.IsFalse(OvalLanes.AppliesTo("NoSuchTrack"));
    }

    [Test]
    public void SmoothingTurnsAWidthStepIntoADrift()
    {
        int n = 100;
        var d = new float[n];
        var w = new float[n];
        for (int i = 0; i < n; i++) { d[i] = i * 2f; w[i] = i < 50 ? 18f : 20f; }
        var s = OvalLanes.Smooth(d, w, 0f, 10f);
        Assert.AreEqual(18f, s[10], 1e-4f);
        Assert.AreEqual(20f, s[90], 1e-4f);
        for (int i = 1; i < n; i++) Assert.Less(Mathf.Abs(s[i] - s[i - 1]), 0.25f, $"step at {i}");
    }

    // ---------------------------------------------------------------- the AI in its lane

    const string TrackId = "FortWorth";
    // Metres from the racing surface's edge to an oval's walls as TrackDressingFactory stands them.
    const float OutsideWallOffset = 1f, InfieldWallOffset = 6f;
    const float CarHalfWidth = 1f;
    const int TimedLaps = 6;

    GameObject _package;
    readonly List<GameObject> _cars = new List<GameObject>();

    [TearDown]
    public void Despawn()
    {
        foreach (var c in _cars) if (c != null) UnityEngine.Object.DestroyImmediate(c);
        _cars.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
    }

    static Type Runtime(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from Assembly-CSharp.");
        return null;
    }

    void SpawnTrack(string trackId)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"track package missing for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
    }

    [Test]
    public void ARoadCourseIgnoresAnAssignedLane()
    {
        SpawnTrack("WatkinsGlen");
        var splineType = Runtime("SplineDriver");
        var go = new GameObject("LaneProbe"); _cars.Add(go);
        var spline = go.AddComponent(splineType);
        Set(spline, "track", _package.GetComponentInChildren(Runtime("TrackBuilder"), true));
        Set(spline, "vehicleInfo", Resources.Load("Vehicles/Cup24"));
        SetLane(spline, "Inside");
        splineType.GetMethod("Rebuild").Invoke(spline, null);
        Assert.IsFalse((bool)splineType.GetProperty("LaneFollowing").GetValue(spline), "Lanes are for ovals only.");
    }

    // A stadium oval starting mid front straight: 300 m straights, 200 m-radius turns. The front stretch weight is 1
    // round the line, 0 in both turns and down the back straight, and eases in and out.
    [Test]
    public void TheFrontStretchIsTheRoadThroughTheLineBetweenTheTurns()
    {
        const float straight = 300f, radius = 200f, step = 2f;
        var d = new List<float>();
        var t = new List<Vector2>();
        float turn = Mathf.PI * radius, lap = 2f * straight + 2f * turn;
        for (float x = 0f; x <= lap + 1e-3f; x += step)
        {
            // Laid out from the line: half the front straight, turn 1-2, the back straight, turn 3-4, the other half.
            float u = x, heading;
            if (u < straight * 0.5f) heading = 0f;
            else if ((u -= straight * 0.5f) < turn) heading = u / radius;
            else if ((u -= turn) < straight) heading = Mathf.PI;
            else if ((u -= straight) < turn) heading = Mathf.PI + u / radius;
            else heading = 2f * Mathf.PI;
            d.Add(x);
            t.Add(new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)));
        }
        var dist = d.ToArray();
        var w = OvalLanes.FrontStretchWeight(dist, t.ToArray(), dist[dist.Length - 1]);
        float At(float x) => w[Mathf.Clamp(Mathf.RoundToInt(Mathf.Repeat(x, lap) / step), 0, w.Length - 1)];
        Assert.AreEqual(1f, At(0f), 1e-3f, "on the line");
        Assert.AreEqual(1f, At(-40f), 1e-3f, "just before the line");
        Assert.AreEqual(0f, At(straight * 0.5f + turn * 0.5f), 1e-3f, "mid turn 1-2");
        Assert.AreEqual(0f, At(straight * 0.5f + turn + straight * 0.5f), 1e-3f, "the back straight");
        Assert.AreEqual(0f, At(-straight * 0.5f - turn * 0.5f), 1e-3f, "mid turn 3-4");
        for (int i = 1; i < w.Length; i++) Assert.Less(Mathf.Abs(w[i] - w[i - 1]), 0.1f, $"step at {dist[i]} m");
    }

    // Lanes from the AI lines open out and move up the banking (OvalLanes.Spec extraSpacing / outwardShift, more on the
    // front stretch). Two lines 2 m apart in the middle of a 20 m road: the gap becomes laneSpacing + the spread, and
    // the middle of the pair rises by the shift; at the outside edge the pair is held off it, still apart.
    [Test]
    public void LanesFromTheAILinesOpenOutAndMoveUp()
    {
        var spec = OvalLanes.Default;
        Assert.Greater(spec.extraSpacing, 0f);
        Assert.Greater(spec.frontStretchOutwardShift, 0f);
        const int n = 40;
        var d = new float[n]; var w = new float[n]; var a = new float[n]; var b = new float[n];
        var front = new float[n];
        for (int i = 0; i < n; i++) { d[i] = i * 2f; w[i] = 20f; a[i] = -1f; b[i] = 1f; front[i] = i < n / 2 ? 0f : 1f; }
        // Infield on the left (insideSide -1): height above the inside edge = half + lateral.
        OvalLanes.BuildFromLines(d, w, a, b, -1f, 0f, spec, out var inside, out var outside, 0f, front);
        float hIn0 = 10f + inside[2], hOut0 = 10f + outside[2];
        Assert.AreEqual(spec.laneSpacing + spec.extraSpacing, hOut0 - hIn0, 1e-3f, "spread, off the front stretch");
        Assert.AreEqual(10f + spec.outwardShift, (hIn0 + hOut0) * 0.5f, 1e-3f, "shift, off the front stretch");
        float hIn1 = 10f + inside[n - 2], hOut1 = 10f + outside[n - 2];
        Assert.AreEqual(spec.laneSpacing + spec.extraSpacing + spec.frontStretchExtraSpacing, hOut1 - hIn1, 1e-3f);
        Assert.AreEqual(10f + spec.outwardShift + spec.frontStretchOutwardShift, (hIn1 + hOut1) * 0.5f, 1e-3f);
        // Lines already up against the outside edge: held outsideEdgeMargin off it (less the front-stretch relief
        // there), the gap kept.
        for (int i = 0; i < n; i++) { a[i] = 6f; b[i] = 9f; }
        OvalLanes.BuildFromLines(d, w, a, b, -1f, 0f, spec, out inside, out outside, 0f, front);
        Assert.AreEqual(20f - spec.outsideEdgeMargin, 10f + outside[2], 1e-3f, "off the front stretch");
        Assert.AreEqual(20f - (spec.outsideEdgeMargin - spec.frontStretchOutsideEdgeRelief), 10f + outside[n - 2], 1e-3f);
        Assert.GreaterOrEqual(outside[n - 2] - inside[n - 2], spec.laneSpacing + spec.extraSpacing - 1e-3f);
    }

    public class LaneResult
    {
        public readonly List<float> lapTimes = new List<float>();
        public double errSum; public int errCount;
        public float errMax, errMaxAt;
        public double cornerErrSum; public int cornerErrCount;
        public float minOutsideWall = float.MaxValue, minInfieldWall = float.MaxValue;
        public int wallHits, spins, offs;
        public float maxSlip;
        public readonly StringBuilder incidents = new StringBuilder();
        // Signed error (+ = toward the infield) by 50 m of track, over the timed laps.
        public readonly double[] binSum = new double[200];
        public readonly int[] binCount = new int[200];
        public float Mean => errCount > 0 ? (float)(errSum / errCount) : 0f;
        public float CornerMean => cornerErrCount > 0 ? (float)(cornerErrSum / cornerErrCount) : 0f;

        public string Report(string title) =>
            $"{title}: laps {string.Join(", ", lapTimes.ConvertAll(t => t.ToString("0.00")))} s | lateral error vs lane " +
            $"mean {Mean:0.000} m (corners {CornerMean:0.000}), max {errMax:0.000} m at {errMaxAt:0} m | " +
            $"closest to outside wall {minOutsideWall:0.00} m, infield wall {minInfieldWall:0.00} m | " +
            $"wall hits {wallHits}, spins {spins}, offs {offs}, max slip {maxSlip:0.0}°{incidents}{Bins()}";

        string Bins()
        {
            var sb = new StringBuilder("\n  signed error toward the infield by 50 m:");
            for (int b = 0; b < binSum.Length; b++)
                if (binCount[b] > 0) sb.Append($" {b * 50}:{binSum[b] / binCount[b]:+0.00;-0.00}");
            return sb.ToString();
        }
    }

    [Test]
    public void AnAICarHoldsItsLaneAtTexas([Values("Inside", "Outside")] string lane)
    {
        var calib = Runtime("AIPaceCalibration").GetMethod("ApplyFor");
        LaneResult r;
        try
        {
            calib.Invoke(null, new object[] { TrackId });
            float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);
            SpawnTrack(TrackId);
            r = DriveLane(lane, 1.04f * aiPace);   // the quickest driver in the field
        }
        finally { calib.Invoke(null, new object[] { "" }); }

        string report = r.Report($"[OvalLanes] Texas {lane} lane");
        Debug.Log(report);
        Assert.AreEqual(TimedLaps, r.lapTimes.Count, "The car didn't finish its laps. " + report);
        Assert.Less(r.Mean, 0.3f, "Mean lateral error too big. " + report);
        Assert.Less(r.errMax, 1f, "Max lateral error too big. " + report);
        Assert.AreEqual(0, r.wallHits, report);
        Assert.AreEqual(0, r.spins, report);
        Assert.AreEqual(0, r.offs, report);
    }

    [Test]
    [Explicit("Diagnostic: the centreline and the lanes either side of Texas's start/finish seam.")]
    public void SeamGeometry()
    {
        SpawnTrack(TrackId);
        var splineType = Runtime("SplineDriver");
        var go = new GameObject("SeamProbe"); _cars.Add(go);
        var spline = go.AddComponent(splineType);
        Set(spline, "track", _package.GetComponentInChildren(Runtime("TrackBuilder"), true));
        Set(spline, "vehicleInfo", Resources.Load("Vehicles/Cup24"));
        SetLane(spline, "Inside");
        splineType.GetMethod("Rebuild").Invoke(spline, null);
        var samples = (System.Collections.IList)splineType.GetField("_mainSamples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        var lat = (float[])splineType.GetField("_lateralProfile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        var kappa = (float[])splineType.GetField("_curvatureProfile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        var prof = (float[])splineType.GetField("_speedProfile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        var st = samples[0].GetType();
        int n = samples.Count;
        float len = (float)st.GetField("distance").GetValue(samples[n - 1]);
        var sb = new StringBuilder($"[OvalLanes] seam: {n} samples, length {len:0.00}");
        var info = Resources.Load("Tracks/" + TrackId);
        var it = info.GetType();
        sb.Append($"\n  start {it.GetField("startPosition").GetValue(info)} heading {it.GetField("startHeading").GetValue(info)} " +
                  $"startFinishDistance {it.GetField("startFinishDistance").GetValue(info)} pit entry seg {it.GetField("pitEntrySegmentIndex").GetValue(info)} " +
                  $"+{it.GetField("pitEntryOffset").GetValue(info)} = {it.GetField("pitEntryDistance").GetValue(info)}, exit seg {it.GetField("pitExitSegmentIndex").GetValue(info)} " +
                  $"+{it.GetField("pitExitOffset").GetValue(info)} = {it.GetField("pitExitDistance").GetValue(info)}, pit exit line {it.GetField("pitExitLineDistance").GetValue(info)}");
        var segs = (Array)it.GetField("segments").GetValue(info);
        for (int k = 0; k < segs.Length; k++)
        {
            var sg = segs.GetValue(k);
            var sgt = sg.GetType();
            sb.Append($"\n  seg{k} {sgt.GetField("label").GetValue(sg)} {sgt.GetField("type").GetValue(sg)} len {sgt.GetField("length").GetValue(sg)} " +
                      $"ang {sgt.GetField("angle").GetValue(sg)} bank {sgt.GetField("banking").GetValue(sg)} w {sgt.GetField("width").GetValue(sg)}");
        }
        for (int i = 0; i < n; i++)
        {
            float d = (float)st.GetField("distance").GetValue(samples[i]);
            if (d > 40f && d < len - 40f && i % 25 != 0) continue;
            Vector2 p = (Vector2)st.GetField("position").GetValue(samples[i]);
            Vector2 t = (Vector2)st.GetField("tangent").GetValue(samples[i]);
            float w = (float)st.GetField("width").GetValue(samples[i]);
            sb.Append($"\n  i{i} d{d,8:0.00} pos({p.x:0.00},{p.y:0.00}) hdg{Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg,8:0.00} w{w:0.00} lane{lat[i],7:0.00} R{(kappa[i] > 1e-5f ? 1f / kappa[i] : 99999f),7:0} v{prof[i] / 2.237f,6:0.0}");
        }
        Debug.Log(sb.ToString());
    }

    LaneResult DriveLane(string lane, float pace)
    {
        var trackType = Runtime("TrackBuilder");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        var go = new GameObject("LaneCar");
        _cars.Add(go);
        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var spline = go.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "cornerSpeedScale", 0.95f);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 180f);
        Set(spline, "speed", 45f);
        Set(spline, "startDistance", 0f);
        Set(spline, "externalMotionController", true);
        Set(spline, "paceMultiplier", pace);
        SetLane(spline, lane);

        var pvc = go.AddComponent(pvcType);
        Set(pvc, "vehicleInfo", vehicleInfo);
        Set(pvc, "track", track);
        Set(pvc, "spriteFacesUp", false);
        Set(pvc, "angleOffsetDeg", 180f);
        Set(pvc, "grassTrails", false);
        Set(pvc, "enableWheelspin", false);
        Set(pvc, "externalInput", true);
        Set(pvc, "damageImpairsHandling", false);
        Set(pvc, "surfaceSpray", false);
        Set(pvc, "impactDebris", false);
        Set(pvc, "corneringStiffness", 15f);   // GridSpawner.aiCorneringStiffness

        var input = go.AddComponent(inputType);

        Call(spline, "Awake");
        Call(input, "Awake");
        Call(input, "OnEnable");
        Call(pvc, "Start");
        Call(spline, "Start");

        Assert.IsTrue((bool)splineType.GetProperty("LaneFollowing").GetValue(spline), "The car isn't following its lane.");

        // The centreline, for the road's half width and the infield side at the car.
        var samples = (System.Collections.IList)splineType.GetField("_mainSamples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        int n = samples.Count;
        var sDist = new float[n];
        var sHalf = new float[n];
        var sTan = new Vector2[n];
        var sampleType = samples[0].GetType();
        for (int i = 0; i < n; i++)
        {
            sDist[i] = (float)sampleType.GetField("distance").GetValue(samples[i]);
            sHalf[i] = (float)sampleType.GetField("width").GetValue(samples[i]) * 0.5f;
            sTan[i] = (Vector2)sampleType.GetField("tangent").GetValue(samples[i]);
        }
        float insideSide = OvalLanes.InsideSide(sTan);

        var trackLength = splineType.GetProperty("TrackLength");
        var distanceOnTrack = splineType.GetProperty("DistanceOnTrack");
        var centreDistance = splineType.GetProperty("CentreDistanceOnTrack");
        var carLateral = splineType.GetProperty("CarLateral");
        var lateralAt = splineType.GetMethod("LateralAt", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        var radiusAhead = splineType.GetMethod("CurvatureRadiusAhead");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var slipDeg = pvcType.GetProperty("SlipAngleDeg");
        var recovering = inputType.GetProperty("IsRecovering");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);
        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        float length = (float)trackLength.GetValue(spline);
        Assert.Greater(length, 100f, "The AI brain never built its spline.");

        var r = new LaneResult();
        float dt = Time.fixedDeltaTime;
        float lapClock = 0f, lastDistance = 0f;
        int lap = -1;                         // the first crossing starts lap 0 - the run-up lap is not timed
        bool wasOn = true, wasRecovering = false, wasOnWall = false;
        int maxSteps = Mathf.RoundToInt((TimedLaps + 1.5f) * 120f / dt);

        for (int step = 0; step < maxSteps; step++)
        {
            inputStep.Invoke(input, null);
            pvcStep.Invoke(pvc, null);
            splineStep.Invoke(spline, null);
            lapClock += dt;

            float d = (float)distanceOnTrack.GetValue(spline);
            if (d < lastDistance - length * 0.5f)
            {
                if (lap >= 0) r.lapTimes.Add(lapClock);
                lap++;
                lapClock = 0f;
                if (lap >= TimedLaps) break;
            }
            lastDistance = d;
            if (lap < 0) continue;

            // Where the car's centre is against the lane, at the car's centre.
            float cd = (float)centreDistance.GetValue(spline);
            cd = Mathf.Repeat(cd, length);
            float laneLat = (float)lateralAt.Invoke(spline, new object[] { cd });
            float lat = (float)carLateral.GetValue(spline);
            float err = Mathf.Abs(lat - laneLat);
            r.errSum += err; r.errCount++;
            if (err > r.errMax) { r.errMax = err; r.errMaxAt = cd; }
            int bin = Mathf.Clamp((int)(cd / 50f), 0, r.binSum.Length - 1);
            r.binSum[bin] += (lat - laneLat) * insideSide;
            r.binCount[bin]++;
            if ((float)radiusAhead.Invoke(spline, new object[] { 2f }) < 1500f) { r.cornerErrSum += err; r.cornerErrCount++; }

            // The walls: outside wall OutsideWallOffset past the edge, infield wall InfieldWallOffset past the other.
            float half = sHalf[Index(sDist, cd)];
            float towardInfield = lat * insideSide;   // + toward the infield
            float infieldGap = half + InfieldWallOffset - (towardInfield + CarHalfWidth);
            float outsideGap = half + OutsideWallOffset - (-towardInfield + CarHalfWidth);
            r.minInfieldWall = Mathf.Min(r.minInfieldWall, infieldGap);
            r.minOutsideWall = Mathf.Min(r.minOutsideWall, outsideGap);
            bool onWall = infieldGap <= 0f || outsideGap <= 0f;
            if (onWall && !wasOnWall) { r.wallHits++; r.incidents.Append($"\n  lap {lap}: wall at {cd:0} m"); }
            wasOnWall = onWall;

            float slip = (float)slipDeg.GetValue(pvc);
            r.maxSlip = Mathf.Max(r.maxSlip, Mathf.Abs(slip));
            bool on = (bool)onLegal.Invoke(null, new object[] { track, go.transform.position, 1f });
            bool rec = (bool)recovering.GetValue(input);
            if (!on && wasOn) { r.offs++; r.incidents.Append($"\n  lap {lap}: off at {cd:0} m, {(float)speedMps.GetValue(pvc):0.0} m/s"); }
            if (rec && !wasRecovering) { r.spins++; r.incidents.Append($"\n  lap {lap}: spin at {cd:0} m, slip {slip:0.0}°"); }
            wasOn = on;
            wasRecovering = rec;
        }
        return r;
    }

    // ---------------------------------------------------------------- stage 2: two trains

    [Test]
    public void TheTrainGapGrowsWithSpeedAndIsHeldOnTheCarAhead()
    {
        var s = Draftmaster.Sim.TrainFollow.Spec.Default;
        float wanted = Draftmaster.Sim.TrainFollow.WantedGap(s, 75f);
        Assert.AreEqual(s.standstillGap + s.headwaySeconds * 75f, wanted, 1e-4f);
        // At the wanted gap, at the car ahead's speed: its speed exactly, nothing to do.
        var at = Draftmaster.Sim.TrainFollow.Follow(s, wanted, 75f, 75f, 1.5f);
        Assert.AreEqual(75f, at.speedMps, 1e-4f);
        Assert.IsFalse(at.emergency);
        // Too close: asks for less than the car ahead is doing.
        Assert.Less(Draftmaster.Sim.TrainFollow.Follow(s, wanted - 4f, 75f, 75f, 1.5f).speedMps, 75f);
    }

    [Test]
    public void ATowMakesTheApproachBackOffEarlier()
    {
        var s = Draftmaster.Sim.TrainFollow.Spec.Default;
        float wanted = Draftmaster.Sim.TrainFollow.WantedGap(s, 75f);
        // Well back: the approach (what a lift can shed by the wanted gap) sets the speed, not the linear term.
        float clean = Draftmaster.Sim.TrainFollow.Follow(s, wanted + 40f, 75f, 70f, 1.5f).speedMps;
        float towed = Draftmaster.Sim.TrainFollow.Follow(s, wanted + 40f, 75f, 70f, 0.75f).speedMps;
        Assert.AreEqual(70f + Mathf.Sqrt(2f * 1.5f * s.liftShare * 40f), clean, 1e-3f);
        Assert.Less(towed, clean, "In a tow a lift sheds less, so the car must close more slowly.");
    }

    [Test]
    public void ClosingFastOnAStoppedCarIsAnEmergency()
    {
        var s = Draftmaster.Sim.TrainFollow.Spec.Default;
        Assert.IsTrue(Draftmaster.Sim.TrainFollow.Follow(s, 100f, 70f, 0f, 1.5f).emergency);
        Assert.IsTrue(Draftmaster.Sim.TrainFollow.Follow(s, s.hardGap - 0.5f, 60f, 59f, 1.5f).emergency);
        Assert.IsFalse(Draftmaster.Sim.TrainFollow.Follow(s, 30f, 61f, 60f, 1.5f).emergency);
        Assert.AreEqual(0f, Draftmaster.Sim.TrainFollow.Follow(s, s.standstillGap - 3f, 5f, 0f, 1.5f).speedMps, 1e-4f);
    }

    const int CarsPerLane = 9;
    const int TrainTimedLaps = 20;          // after one lap to form up
    const float RowGap = 30f;               // m between rows on the rolling start, ~the gap the train holds
    const float CarHalfLength = 2.4f;       // GridSpawner.collisionHalfExtents
    const int PulseLap = 10;                // the timed lap on which each lane's leader lifts
    const float PulsePace = 0.94f, PulseSeconds = 2f;
    const float PulseWatchSeconds = 20f;

    public class TrainResult
    {
        public int laps;
        public readonly List<float>[] leaderLaps = { new List<float>(), new List<float>() };
        public int contacts, wallHits, spins, offs;
        public float minBumper = float.MaxValue;    // m, nose to tail, same lane
        public float minDoor = float.MaxValue;      // m, door to door, cars overlapping along the track
        public double errSum; public int errCount;
        public float errMax, errMaxAt;
        public float maxSlip;
        public int emergencySteps, trainBrakeSteps, trainBrakeFirmSteps;
        public float maxTrainBrake, maxTrainBrakeOutsidePulse;
        public double trainBrakeSum;
        public int followerBrakeSteps, followerSteps;   // any brake over 0.3, from anything, on a follower
        public int leaderBrakeSteps, leaderSteps;       // the same on the two leaders, who have nobody to follow
        public float pulseStart = -1f;
        // Per train position (1 = the car behind the leader), both lanes pooled.
        public readonly double[] gapSum = new double[CarsPerLane], gapErrSum = new double[CarsPerLane], gapErrSq = new double[CarsPerLane];
        public readonly double[] hwSum = new double[CarsPerLane], hwSq = new double[CarsPerLane];
        public readonly int[] gapN = new int[CarsPerLane];
        public readonly float[] gapMin = Fill(float.MaxValue), gapMax = Fill(0f);
        public readonly float[] pulsePeak = new float[CarsPerLane];      // biggest |gap error| after the pulse
        public readonly float[] pulseMinGap = Fill(float.MaxValue);
        public readonly StringBuilder incidents = new StringBuilder();
        public float Mean => errCount > 0 ? (float)(errSum / errCount) : 0f;

        static float[] Fill(float v) { var a = new float[CarsPerLane]; for (int i = 0; i < a.Length; i++) a[i] = v; return a; }

        public float GapMean(int k) => gapN[k] > 0 ? (float)(gapSum[k] / gapN[k]) : 0f;
        public float GapErrMean(int k) => gapN[k] > 0 ? (float)(gapErrSum[k] / gapN[k]) : 0f;
        public float GapErrStd(int k) => gapN[k] > 0 ? Std(gapErrSum[k], gapErrSq[k], gapN[k]) : 0f;
        public float HeadwayMean(int k) => gapN[k] > 0 ? (float)(hwSum[k] / gapN[k]) : 0f;
        public float HeadwayStd(int k) => gapN[k] > 0 ? Std(hwSum[k], hwSq[k], gapN[k]) : 0f;
        static float Std(double sum, double sq, int n) { double m = sum / n; return (float)Math.Sqrt(Math.Max(0.0, sq / n - m * m)); }

        public string Report()
        {
            var sb = new StringBuilder("[OvalLanes] Texas, two trains of " + CarsPerLane + ": " + laps + " timed laps");
            sb.Append($"\n  leader laps inside {Laps(0)} | outside {Laps(1)}");
            sb.Append($"\n  car contacts {contacts}, wall hits {wallHits}, spins {spins}, offs {offs}; closest nose to tail {minBumper:0.00} m, door to door {minDoor:0.00} m");
            sb.Append($"\n  lateral error vs lane: mean {Mean:0.000} m, max {errMax:0.000} m at {errMaxAt:0} m; max slip {maxSlip:0.0}°");
            sb.Append($"\n  braking: train emergencies {emergencySteps} steps; train brake on {(followerSteps > 0 ? (float)trainBrakeSteps / followerSteps : 0f):P1} of follower time, mean {(trainBrakeSteps > 0 ? trainBrakeSum / trainBrakeSteps : 0):0.000} when on, over 20% {(followerSteps > 0 ? (float)trainBrakeFirmSteps / followerSteps : 0f):P2}, max {maxTrainBrake:0.00} ({maxTrainBrakeOutsidePulse:0.00} away from the pulse); followers over 30% brake from anything {(followerSteps > 0 ? (float)followerBrakeSteps / followerSteps : 0f):P1} of the time, leaders {(leaderSteps > 0 ? (float)leaderBrakeSteps / leaderSteps : 0f):P1}");
            sb.Append("\n  gap to the car ahead by train position (m centre to centre: mean / min / max | error vs wanted mean ± sd | headway s mean ± sd | after the pulse: peak |error|, min gap):");
            for (int k = 1; k < CarsPerLane; k++)
                sb.Append($"\n    P{k}: {GapMean(k):0.0} / {gapMin[k]:0.0} / {gapMax[k]:0.0} | {GapErrMean(k):+0.00;-0.00} ± {GapErrStd(k):0.00} | {HeadwayMean(k):0.000} ± {HeadwayStd(k):0.000} | {pulsePeak[k]:0.00}, {pulseMinGap[k]:0.0}");
            sb.Append(incidents);
            return sb.ToString();
        }

        string Laps(int lane) => string.Join(", ", leaderLaps[lane].ConvertAll(t => t.ToString("0.00")));
    }

    // Two trains at Texas, one per lane, 9 cars each: every car holds its lane and follows the car ahead in it. Each
    // lane is ordered slowest first, so every follower is quicker than the car ahead and the whole line has to close
    // up and hold the gap. On timed lap 10 each lane's leader lifts (6% off its pace for 2 s) and the response is
    // watched down the line. Built like GrandstandRaceSimTests.RunDynamic: SplineDriver + AIRacingBehaviour +
    // PlayerVehicleController + SplineInputDriver, stepped by hand. Contacts are geometric (car boxes in track space:
    // 4.8 m x 2.0 m) - nothing steps Physics2D. Mistakes are off: this is the train, not the driver.
    [Test]
    public void TwoTrainsHoldTheirLanesAndGapsAtTexas()
    {
        var calib = Runtime("AIPaceCalibration").GetMethod("ApplyFor");
        var raceStart = Runtime("RaceStart");
        TrainResult r;
        try
        {
            calib.Invoke(null, new object[] { TrackId });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
            raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
            SpawnTrack(TrackId);
            r = DriveTrains(raceStart);
        }
        finally
        {
            calib.Invoke(null, new object[] { "" });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        }

        string report = r.Report();
        Debug.Log(report);
        Assert.AreEqual(TrainTimedLaps, r.laps, "The field didn't finish its laps. " + report);
        Assert.AreEqual(0, r.contacts, "Cars touched. " + report);
        Assert.AreEqual(0, r.wallHits, report);
        Assert.AreEqual(0, r.spins, report);
        Assert.AreEqual(0, r.offs, report);
        Assert.Less(r.Mean, 0.3f, "Mean lateral error too big. " + report);
        Assert.Less(r.errMax, 1f, "Max lateral error too big. " + report);
        Assert.AreEqual(0, r.emergencySteps, "A follower had to stand on the brakes. " + report);
        // No accordion: a gap error passed down the line must not grow - the last car's response to the leader's
        // lift no bigger than the first follower's, give or take a metre.
        Assert.LessOrEqual(r.pulsePeak[CarsPerLane - 1], r.pulsePeak[1] + 1f, "The leader's lift grew down the train. " + report);
    }

    TrainResult DriveTrains(Type raceStart)
    {
        var trackType = Runtime("TrackBuilder");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);

        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");
        int cars = CarsPerLane * 2;

        // The drivers: a spread of strength as GridSpawner rolls it, then each lane ordered slowest at the front.
        UnityEngine.Random.InitState(23);
        var paces = new float[cars];
        var commit = new float[cars];
        for (int i = 0; i < cars; i++)
        {
            float q01 = Mathf.Lerp(0.4f, 1f, ((i * 5) % cars) / (float)(cars - 1));
            float cons01 = Mathf.Lerp(0.65f, 0.95f, ((i * 7) % cars) / (float)(cars - 1));
            float agg01 = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % cars) / (float)(cars - 1));
            const int statMax = 20;
            var ratings = Draftmaster.Sim.AIRatings.ForEvent(Mathf.RoundToInt(q01 * statMax), Mathf.RoundToInt(q01 * statMax),
                Mathf.RoundToInt(cons01 * statMax), Mathf.RoundToInt(agg01 * statMax), statMax,
                UnityEngine.Random.value, UnityEngine.Random.value);
            paces[i] = Draftmaster.Sim.AIRatings.BasePace(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            commit[i] = Draftmaster.Sim.AIRatings.CornerCommitment(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
        }
        var byPace = new int[cars];
        for (int i = 0; i < cars; i++) byPace[i] = i;
        Array.Sort(byPace, (a, b) => paces[a].CompareTo(paces[b]));

        // trains[lane][position]: car index. Cars alternate lanes in pace order, so the two lanes are matched.
        var trains = new int[2][] { new int[CarsPerLane], new int[CarsPerLane] };
        var splines = new Component[cars];
        var racers = new Component[cars];
        var pvcs = new Component[cars];
        var inputs = new Component[cars];
        var basePace = new float[cars];
        var lane = new int[cars];
        var pos = new int[cars];
        float length = 0f;
        for (int k = 0; k < cars; k++)
        {
            int src = byPace[k];
            int ln = k % 2, p = k / 2;
            int i = k;
            trains[ln][p] = i; lane[i] = ln; pos[i] = p;
            basePace[i] = paces[src];

            var go = new GameObject($"Train{(ln == 0 ? "In" : "Out")}{p}");
            _cars.Add(go);
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "externalMotionController", true);
            Set(spline, "loop", true);
            Set(spline, "spawnInPit", false);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "lateralOffset", 0f);
            Set(spline, "speed", 60f);
            Set(spline, "paceMultiplier", basePace[i] * aiPace);
            Set(spline, "cornerCommitment", commit[src]);
            SetLane(spline, ln == 0 ? "Inside" : "Outside");
            Set(spline, "lanePassing", false);   // the trains alone: passing is OvalPassingTests
            var racing = go.AddComponent(racingType);
            Set(racing, "mistakeProbabilityPerSecond", 0f);
            var pvc = go.AddComponent(pvcType);
            Set(pvc, "vehicleInfo", vehicleInfo);
            Set(pvc, "track", track);
            Set(pvc, "spriteFacesUp", false);
            Set(pvc, "angleOffsetDeg", 180f);
            Set(pvc, "grassTrails", false);
            Set(pvc, "enableWheelspin", false);
            Set(pvc, "externalInput", true);
            Set(pvc, "damageImpairsHandling", false);
            Set(pvc, "surfaceSpray", false);
            Set(pvc, "impactDebris", false);
            Set(pvc, "corneringStiffness", 15f);   // GridSpawner.aiCorneringStiffness
            var input = go.AddComponent(inputType);

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { basePace[i] });
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");
            length = (float)splineType.GetProperty("TrackLength").GetValue(spline);
            Assert.Greater(length, 100f, "The AI brain never built its spline.");
            Set(spline, "startDistance", Mathf.Repeat(-p * RowGap, length));
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            Call(input, "OnEnable");
            Assert.IsTrue((bool)splineType.GetProperty("LaneFollowing").GetValue(spline), "A car isn't following its lane.");
            splines[i] = spline; racers[i] = racing; pvcs[i] = pvc; inputs[i] = input;
        }

        // The centreline: half width and the infield side, for the walls.
        var samples = (System.Collections.IList)splineType.GetField("_mainSamples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(splines[0]);
        int n = samples.Count;
        var sDist = new float[n];
        var sHalf = new float[n];
        var sTan = new Vector2[n];
        var sampleType = samples[0].GetType();
        for (int i = 0; i < n; i++)
        {
            sDist[i] = (float)sampleType.GetField("distance").GetValue(samples[i]);
            sHalf[i] = (float)sampleType.GetField("width").GetValue(samples[i]) * 0.5f;
            sTan[i] = (Vector2)sampleType.GetField("tangent").GetValue(samples[i]);
        }
        float insideSide = OvalLanes.InsideSide(sTan);

        var steps = new Action[cars * 4];
        for (int i = 0; i < cars; i++)
        {
            steps[i * 4 + 0] = Step(racers[i]);
            steps[i * 4 + 1] = Step(inputs[i]);
            steps[i * 4 + 2] = Step(pvcs[i]);
            steps[i * 4 + 3] = Step(splines[i]);
        }
        var centreDistance = splineType.GetProperty("CentreDistanceOnTrack");
        var distanceOnTrack = splineType.GetProperty("DistanceOnTrack");
        var carLateral = splineType.GetProperty("CarLateral");
        var trainEmergency = splineType.GetProperty("TrainEmergency");
        var lateralAt = splineType.GetMethod("LateralAt", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        var speedMps = pvcType.GetProperty("SpeedMps");
        var slipDeg = pvcType.GetProperty("SlipAngleDeg");
        var recovering = inputType.GetProperty("IsRecovering");
        var lastBrake = inputType.GetProperty("LastBrake");
        var lastTrainBrake = inputType.GetProperty("LastTrainBrake");
        var setBasePace = racingType.GetMethod("SetBasePace");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);
        var spec = (Draftmaster.Sim.TrainFollow.Spec)Runtime("OvalTrain").GetField("Spec").GetValue(null);

        var r = new TrainResult();
        float dt = Time.fixedDeltaTime;
        var cd = new float[cars];
        var lat = new float[cars];
        var v = new float[cars];
        var wasOn = new bool[cars];
        var wasRec = new bool[cars];
        var wasWall = new bool[cars];
        var touching = new bool[cars, cars];
        for (int i = 0; i < cars; i++) wasOn[i] = true;
        var leaderClock = new float[2];
        var leaderLast = new float[2];
        var crossings = new int[2];
        for (int ln = 0; ln < 2; ln++) leaderLast[ln] = (float)distanceOnTrack.GetValue(splines[trains[ln][0]]);
        int lap = -1;                 // the inside leader's first crossing starts the timed laps
        float clock = 0f, pulseEnd = -1f;
        int maxSteps = Mathf.RoundToInt((TrainTimedLaps + 2f) * 40f / dt);

        for (int step = 0; step < maxSteps; step++)
        {
            SetGreen(raceStart, step * dt);
            for (int s = 0; s < steps.Length; s++) steps[s]();
            clock += dt;

            // Lap counting off each lane's leader.
            bool newLap = false;
            for (int ln = 0; ln < 2; ln++)
            {
                leaderClock[ln] += dt;
                float d = (float)distanceOnTrack.GetValue(splines[trains[ln][0]]);
                if (d < leaderLast[ln] - length * 0.5f)
                {
                    crossings[ln]++;
                    if (crossings[ln] > 1) r.leaderLaps[ln].Add(leaderClock[ln]);
                    if (ln == 0) { lap++; newLap = true; }
                    leaderClock[ln] = 0f;
                }
                leaderLast[ln] = d;
            }
            if (lap >= TrainTimedLaps) break;
            if (lap < 0) continue;

            // The pulse: each lane's leader lifts off its pace for a moment.
            if (newLap && lap == PulseLap - 1 && r.pulseStart < 0f)
            {
                r.pulseStart = clock;
                pulseEnd = clock + PulseSeconds;
                for (int ln = 0; ln < 2; ln++) { int c = trains[ln][0]; setBasePace.Invoke(racers[c], new object[] { basePace[c] * PulsePace }); }
            }
            if (pulseEnd > 0f && clock >= pulseEnd)
            {
                pulseEnd = -1f;
                for (int ln = 0; ln < 2; ln++) { int c = trains[ln][0]; setBasePace.Invoke(racers[c], new object[] { basePace[c] }); }
            }
            bool watchingPulse = r.pulseStart >= 0f && clock - r.pulseStart <= PulseWatchSeconds;

            for (int i = 0; i < cars; i++)
            {
                cd[i] = Mathf.Repeat((float)centreDistance.GetValue(splines[i]), length);
                lat[i] = (float)carLateral.GetValue(splines[i]);
                v[i] = (float)speedMps.GetValue(pvcs[i]);

                float laneLat = (float)lateralAt.Invoke(splines[i], new object[] { cd[i] });
                float err = Mathf.Abs(lat[i] - laneLat);
                r.errSum += err; r.errCount++;
                if (err > r.errMax) { r.errMax = err; r.errMaxAt = cd[i]; }

                float half = sHalf[Index(sDist, cd[i])];
                float towardInfield = lat[i] * insideSide;
                bool onWall = half + InfieldWallOffset - (towardInfield + CarHalfWidth) <= 0f
                              || half + OutsideWallOffset - (-towardInfield + CarHalfWidth) <= 0f;
                if (onWall && !wasWall[i]) { r.wallHits++; r.incidents.Append($"\n  lap {lap}: car {Name(lane[i], pos[i])} wall at {cd[i]:0} m"); }
                wasWall[i] = onWall;

                float slip = (float)slipDeg.GetValue(pvcs[i]);
                r.maxSlip = Mathf.Max(r.maxSlip, Mathf.Abs(slip));
                bool on = (bool)onLegal.Invoke(null, new object[] { track, splines[i].transform.position, 1f });
                bool rec = (bool)recovering.GetValue(inputs[i]);
                if (!on && wasOn[i]) { r.offs++; r.incidents.Append($"\n  lap {lap}: car {Name(lane[i], pos[i])} off at {cd[i]:0} m"); }
                if (rec && !wasRec[i]) { r.spins++; r.incidents.Append($"\n  lap {lap}: car {Name(lane[i], pos[i])} spin at {cd[i]:0} m, slip {slip:0.0}°"); }
                wasOn[i] = on; wasRec[i] = rec;

                if ((bool)trainEmergency.GetValue(splines[i])) r.emergencySteps++;
                float tb = (float)lastTrainBrake.GetValue(inputs[i]);
                if (tb > 0f) { r.trainBrakeSteps++; r.trainBrakeSum += tb; }
                if (tb > 0.2f) r.trainBrakeFirmSteps++;
                r.maxTrainBrake = Mathf.Max(r.maxTrainBrake, tb);
                if (!watchingPulse) r.maxTrainBrakeOutsidePulse = Mathf.Max(r.maxTrainBrakeOutsidePulse, tb);
                bool braking = (float)lastBrake.GetValue(inputs[i]) > 0.3f;
                if (pos[i] > 0) { r.followerSteps++; if (braking) r.followerBrakeSteps++; }
                else { r.leaderSteps++; if (braking) r.leaderBrakeSteps++; }
            }

            // Contacts: every pair of car boxes in track space.
            for (int a = 0; a < cars; a++)
                for (int b = a + 1; b < cars; b++)
                {
                    float along = Mathf.Abs(Mathf.DeltaAngle(cd[a] / length * 360f, cd[b] / length * 360f)) / 360f * length;
                    float across = Mathf.Abs(lat[a] - lat[b]);
                    if (lane[a] == lane[b]) r.minBumper = Mathf.Min(r.minBumper, along - 2f * CarHalfLength);
                    else if (along < 2f * CarHalfLength) r.minDoor = Mathf.Min(r.minDoor, across - 2f * CarHalfWidth);
                    bool touch = along < 2f * CarHalfLength && across < 2f * CarHalfWidth;
                    if (touch && !touching[a, b])
                    {
                        r.contacts++;
                        r.incidents.Append($"\n  lap {lap}: contact {Name(lane[a], pos[a])} / {Name(lane[b], pos[b])} at {cd[a]:0} m ({along:0.00} m along, {across:0.00} m across, {v[a]:0.0} / {v[b]:0.0} m/s)");
                    }
                    touching[a, b] = touch;
                }

            // The gap to the car ahead in the lane, by train position.
            for (int ln = 0; ln < 2; ln++)
                for (int p = 1; p < CarsPerLane; p++)
                {
                    int me = trains[ln][p], ahead = trains[ln][p - 1];
                    float gap = Mathf.Repeat(cd[ahead] - cd[me], length);
                    float error = gap - Draftmaster.Sim.TrainFollow.WantedGap(spec, v[me]);
                    float hw = gap / Mathf.Max(v[me], 1f);
                    r.gapSum[p] += gap; r.gapErrSum[p] += error; r.gapErrSq[p] += (double)error * error;
                    r.hwSum[p] += hw; r.hwSq[p] += (double)hw * hw; r.gapN[p]++;
                    r.gapMin[p] = Mathf.Min(r.gapMin[p], gap);
                    r.gapMax[p] = Mathf.Max(r.gapMax[p], gap);
                    if (watchingPulse)
                    {
                        r.pulsePeak[p] = Mathf.Max(r.pulsePeak[p], Mathf.Abs(error));
                        r.pulseMinGap[p] = Mathf.Min(r.pulseMinGap[p], gap);
                    }
                }
        }
        r.laps = r.leaderLaps[0].Count;

        // Leave RaceField as we found it.
        var unregister = Runtime("RaceField").GetMethod("Unregister");
        foreach (var s in splines) unregister.Invoke(null, new object[] { s });
        return r;
    }

    static string Name(int lane, int pos) => (lane == 0 ? "In" : "Out") + pos;

    static Action Step(Component c)
    {
        var m = c.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(m, $"{c.GetType().Name}.FixedUpdate is gone.");
        return (Action)Delegate.CreateDelegate(typeof(Action), c, m);
    }

    static void SetGreen(Type raceStart, float t)
    {
        var f = raceStart.GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(f, "RaceStart.GreenTime is gone.");
        f.SetValue(null, Time.time - t);
    }

    static int Index(float[] distance, float d)
    {
        int lo = 0, hi = distance.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (distance[mid] <= d) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    static void SetLane(object spline, string lane)
    {
        var f = spline.GetType().GetField("ovalLane");
        Assert.IsNotNull(f, "SplineDriver.ovalLane is gone.");
        f.SetValue(spline, Enum.Parse(f.FieldType, lane));
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }
}
