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
