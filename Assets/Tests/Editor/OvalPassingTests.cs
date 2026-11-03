using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Draftmaster.Sim;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Stage 3 of the oval AI rework (Docs/Oval-Lanes.md): passing by lane change. The pure rules
// (Draftmaster.Sim.LaneChange), then a mixed-pace field at Texas (FortWorth) racing two lanes for 20 laps with
// passing on, built and stepped by hand like OvalLaneTests' trains - no play mode.
public class OvalPassingTests
{
    // ---------------------------------------------------------------- the rules

    [Test]
    public void AMoveLandsExactlyOnTheNewLineWithinItsLimits()
    {
        var s = LaneChange.Spec.Default;
        const float spacing = 3.2f, dt = 0.02f;
        float p = 0f, rate = 0f, lat = 0f, latRate = 0f, maxRate = 0f, maxAccel = 0f, t = 0f;
        bool landed = false;
        for (int i = 0; i < 1000 && !landed; i++)
        {
            landed = LaneChange.Step(s, ref p, ref rate, spacing, dt);
            float newLat = p * spacing, newRate = (newLat - lat) / dt;
            maxRate = Mathf.Max(maxRate, Mathf.Abs(newRate));
            if (!landed) maxAccel = Mathf.Max(maxAccel, Mathf.Abs(newRate - latRate) / dt);
            lat = newLat; latRate = newRate; t += dt;
        }
        Assert.IsTrue(landed, "The move never landed.");
        Assert.AreEqual(1f, p, 0f, "It must land exactly on the new line.");
        Assert.LessOrEqual(maxRate, s.maxLateralSpeed + 1e-3f);
        Assert.LessOrEqual(maxAccel, s.lateralAccel + 0.05f);
        Assert.AreEqual(LaneChange.MoveDuration(s, spacing), t, 0.25f);   // the estimate the corner gate looks ahead by
    }

    [Test]
    public void AnAbortTurnsBackSmoothlyAndNeverPastHalfway()
    {
        var s = LaneChange.Spec.Default;
        const float spacing = 3.2f, dt = 0.02f;
        float p = 0f, rate = 0f;
        // Under way until it may no longer abort, then one step earlier: abort there.
        float pAbort = 0f, rAbort = 0f;
        for (int i = 0; i < 500; i++)
        {
            float p0 = p, r0 = rate;
            LaneChange.Step(s, ref p, ref rate, spacing, dt);
            if (!LaneChange.CanAbort(s, p, rate, spacing)) { pAbort = p0; rAbort = r0; break; }
        }
        Assert.Greater(pAbort, 0.05f);
        // Turned back: the far lane is now the old one.
        p = 1f - pAbort; rate = -rAbort;
        float minP = p, lat = (1f - p) * spacing, latRate = rAbort * spacing, maxAccel = 0f;
        bool landed = false;
        for (int i = 0; i < 1000 && !landed; i++)
        {
            landed = LaneChange.Step(s, ref p, ref rate, spacing, dt);
            minP = Mathf.Min(minP, p);
            float newLat = (1f - p) * spacing, newRate = (newLat - lat) / dt;
            if (!landed) maxAccel = Mathf.Max(maxAccel, Mathf.Abs(newRate - latRate) / dt);
            lat = newLat; latRate = newRate;
        }
        Assert.IsTrue(landed);
        Assert.Greater(minP, 0.5f, "Turning back took the car past the middle of the road.");
        Assert.LessOrEqual(maxAccel, s.lateralAccel + 0.05f, "The turn back was a swerve.");
    }

    [Test]
    public void TheOtherLaneMustHaveRoomAheadAndBehind()
    {
        var s = LaneChange.Spec.Default;
        var train = TrainFollow.Spec.Default;
        const float v = 75f, gapAhead = 26f;
        float ahead = LaneChange.RoomAhead(s, train, v, gapAhead);
        Assert.AreEqual(TrainFollow.WantedGap(train, v) + s.aheadMargin, ahead, 1e-3f);
        Assert.IsFalse(LaneChange.Clears(s, train, 0f, v, v, gapAhead, 1f), "A car alongside blocks the move.");
        Assert.IsFalse(LaneChange.Clears(s, train, ahead - 1f, v, v, gapAhead, 1f));
        Assert.IsTrue(LaneChange.Clears(s, train, ahead + 1f, v, v, gapAhead, 1f));
        // Behind: a train gap at an equal speed, more for a car closing.
        float behind = TrainFollow.WantedGap(train, v);
        Assert.IsTrue(LaneChange.Clears(s, train, -(behind + 1f), v, v, gapAhead, 1f));
        Assert.IsFalse(LaneChange.Clears(s, train, -(behind + 1f), v + 2f, v, gapAhead, 1f), "Cut off a closing car.");
        // Under way, the room may shrink to abortShare before it turns back.
        Assert.IsTrue(LaneChange.Clears(s, train, -(behind * 0.7f), v, v, gapAhead, s.abortShare));
        // It only wants out with a run, and only while held up.
        Assert.IsTrue(LaneChange.WantsToPass(s, 1f, 28f, 29f));
        Assert.IsFalse(LaneChange.WantsToPass(s, 0.3f, 28f, 29f));
        Assert.IsFalse(LaneChange.WantsToPass(s, 1f, 60f, 29f));
    }

    // ---------------------------------------------------------------- the field at Texas

    const string TrackId = "FortWorth";
    const float OutsideWallOffset = 1f, InfieldWallOffset = 6f;   // as TrackDressingFactory stands an oval's walls
    const float CarHalfWidth = 1f, CarHalfLength = 2.4f;
    const int CarsPerLane = 9;
    const int TimedLaps = 20;               // after one lap to form up
    const float RowGap = 70f;               // m between cars in a lane at the start; the outside lane half a row back
    const float PassWindowSeconds = 60f;    // a lane change has this long to get the car past the one it followed

    GameObject _package;
    readonly List<GameObject> _cars = new List<GameObject>();

    [TearDown]
    public void Despawn()
    {
        foreach (var c in _cars) if (c != null) UnityEngine.Object.DestroyImmediate(c);
        _cars.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
    }

    public class PassResult
    {
        public int laps;
        public readonly List<float> leaderLaps = new List<float>();
        public int started, completed, aborted;
        public int passes, noPass;          // lane changes that got the car past the car it followed, and that didn't
        public int overtakes;               // any change of running order between two cars
        public int contacts, wallHits, spins, offs, emergencySteps;
        public float minDoor = float.MaxValue, minBumper = float.MaxValue;
        public double errInSum, errOutSum; public int errInN, errOutN;
        public float errInMax, errOutMax, errOutMaxAt;
        public float maxLatRateIn, maxLatRateOut, maxSlip;
        public float minRearGapAtStart = float.MaxValue;     // m: closest car behind in the target lane when a move began
        public int[] changesByCar;
        public readonly StringBuilder incidents = new StringBuilder();
        public float ErrIn => errInN > 0 ? (float)(errInSum / errInN) : 0f;
        public float ErrOut => errOutN > 0 ? (float)(errOutSum / errOutN) : 0f;

        public string Report()
        {
            var sb = new StringBuilder($"[OvalPassing] Texas, {CarsPerLane * 2} cars of mixed pace in two lanes, passing on: {laps} timed laps");
            sb.Append($"\n  leader laps {string.Join(", ", leaderLaps.ConvertAll(t => t.ToString("0.00")))}");
            sb.Append($"\n  lane changes started {started}, completed {completed}, aborted {aborted}; passes made by a lane change {passes}, lane changes that didn't get by in {PassWindowSeconds:0} s {noPass}; running-order changes {overtakes}");
            sb.Append($"\n  car contacts {contacts}, wall hits {wallHits}, spins {spins}, offs {offs}, train emergencies {emergencySteps} steps; closest door to door {minDoor:0.00} m, nose to tail {minBumper:0.00} m; closest car behind in the other lane as a move began {minRearGapAtStart:0.0} m");
            sb.Append($"\n  lane error outside lane changes: mean {ErrOut:0.000} m, max {errOutMax:0.000} m at {errOutMaxAt:0} m");
            sb.Append($"\n  lane error during lane changes (vs the blended line): mean {ErrIn:0.000} m, max {errInMax:0.000} m; fastest sideways {maxLatRateIn:0.00} m/s (outside lane changes {maxLatRateOut:0.00} m/s); max slip {maxSlip:0.0}°");
            if (changesByCar != null) sb.Append($"\n  lane changes by car (start order): {string.Join(" ", Array.ConvertAll(changesByCar, c => c.ToString()))}");
            sb.Append(incidents);
            return sb.ToString();
        }
    }

    // 18 cars of mixed pace (driver ratings rolled as GridSpawner rolls them, in no particular order), 9 to a lane,
    // 70 m apart with the outside lane half a row back, a lap to form up then 20 timed laps under green with
    // passing on. Contacts are geometric (car boxes 4.8 x 2.0 m in track space); walls, spins and offs as in
    // OvalLaneTests. Lane error is the car centre against the line it is meant to be on: its lane, or mid lane
    // change the blend between the two.
    [Test]
    public void AMixedFieldPassesByChangingLanesAtTexas()
    {
        var calib = Runtime("AIPaceCalibration").GetMethod("ApplyFor");
        var raceStart = Runtime("RaceStart");
        PassResult r;
        try
        {
            calib.Invoke(null, new object[] { TrackId });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
            raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
            SpawnTrack(TrackId);
            r = DriveField(raceStart);
        }
        finally
        {
            calib.Invoke(null, new object[] { "" });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        }

        string report = r.Report();
        Debug.Log(report);
        Assert.AreEqual(TimedLaps, r.laps, "The field didn't finish its laps. " + report);
        Assert.AreEqual(0, r.contacts, "Cars touched. " + report);
        Assert.AreEqual(0, r.wallHits, report);
        Assert.AreEqual(0, r.spins, report);
        Assert.AreEqual(0, r.offs, report);
        Assert.Greater(r.passes, 0, "Nobody passed. " + report);
        Assert.Less(r.ErrOut, 0.3f, "Mean lane error too big. " + report);
        Assert.Less(r.errOutMax, 1f, "Max lane error too big. " + report);
        Assert.Less(r.errInMax, 1f, "A lane change ran off its line. " + report);
        Assert.LessOrEqual(r.maxLatRateIn, LaneChange.Spec.Default.maxLateralSpeed + 1f, "A lane change was a swerve. " + report);
    }

    PassResult DriveField(Type raceStart)
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

        // The drivers, rolled as OvalLaneTests' trains roll them, but started in a mixed order.
        UnityEngine.Random.InitState(23);
        var paces = new float[cars];
        var commit = new float[cars];
        for (int i = 0; i < cars; i++)
        {
            float q01 = Mathf.Lerp(0.4f, 1f, ((i * 5) % cars) / (float)(cars - 1));
            float cons01 = Mathf.Lerp(0.65f, 0.95f, ((i * 7) % cars) / (float)(cars - 1));
            float agg01 = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % cars) / (float)(cars - 1));
            const int statMax = 20;
            var ratings = AIRatings.ForEvent(Mathf.RoundToInt(q01 * statMax), Mathf.RoundToInt(q01 * statMax),
                Mathf.RoundToInt(cons01 * statMax), Mathf.RoundToInt(agg01 * statMax), statMax,
                UnityEngine.Random.value, UnityEngine.Random.value);
            paces[i] = AIRatings.BasePace(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            commit[i] = AIRatings.CornerCommitment(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
        }

        var splines = new Component[cars];
        var racers = new Component[cars];
        var pvcs = new Component[cars];
        var inputs = new Component[cars];
        float length = 0f;
        var paceLine = new StringBuilder("\n  start order (lane, pace):");
        for (int i = 0; i < cars; i++)
        {
            int ln = i % 2, row = i / 2;
            var go = new GameObject($"Pass{i}");
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
            Set(spline, "paceMultiplier", paces[i] * aiPace);
            Set(spline, "cornerCommitment", commit[i]);
            SetLane(spline, ln == 0 ? "Inside" : "Outside");
            Set(spline, "lanePassing", true);
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
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { paces[i] });
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");
            length = (float)splineType.GetProperty("TrackLength").GetValue(spline);
            Assert.Greater(length, 100f, "The AI brain never built its spline.");
            Set(spline, "startDistance", Mathf.Repeat(-row * RowGap - ln * RowGap * 0.5f, length));
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            Call(input, "OnEnable");
            Assert.IsTrue((bool)splineType.GetProperty("LaneFollowing").GetValue(spline), "A car isn't following its lane.");
            splines[i] = spline; racers[i] = racing; pvcs[i] = pvc; inputs[i] = input;
            paceLine.Append($" {(ln == 0 ? "I" : "O")}{paces[i]:0.000}");
        }

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
        var carLateral = splineType.GetProperty("CarLateral");
        var trainEmergency = splineType.GetProperty("TrainEmergency");
        var laneChanging = splineType.GetProperty("LaneChanging");
        var started = splineType.GetProperty("LaneChangesStarted");
        var completed = splineType.GetProperty("LaneChangesCompleted");
        var aborted = splineType.GetProperty("LaneChangesAborted");
        var ovalLane = splineType.GetField("ovalLane");
        var passing = splineType.GetProperty("LaneChangePassing");
        var aborting = splineType.GetProperty("LaneChangeAborting");
        var targetLat = splineType.GetMethod("LaneTargetLateralAt");
        var laneLatAt = splineType.GetMethod("LaneLateralAt");
        object insideLane = Enum.Parse(ovalLane.FieldType, "Inside");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var slipDeg = pvcType.GetProperty("SlipAngleDeg");
        var recovering = inputType.GetProperty("IsRecovering");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);

        var r = new PassResult { changesByCar = new int[cars] };
        float dt = Time.fixedDeltaTime;
        var cd = new float[cars];
        var lat = new float[cars];
        var prevLat = new float[cars];
        var sideRate = new float[cars];
        var v = new float[cars];
        var total = new double[cars];     // distance run, unwrapped
        var lastCd = new float[cars];
        var lane = new int[cars];
        var changing = new bool[cars];
        var wasChanging = new bool[cars];
        var startedSeen = new int[cars];
        var baseCompleted = new int[cars];
        var baseAborted = new int[cars];
        var wasOn = new bool[cars];
        var wasRec = new bool[cars];
        var wasWall = new bool[cars];
        var touching = new bool[cars, cars];
        var aheadOf = new bool[cars, cars];
        // A lane change waiting to get its car past the car it was following: car followed, clock it started.
        var pendingTarget = new int[cars];
        var pendingSince = new float[cars];
        for (int i = 0; i < cars; i++)
        {
            wasOn[i] = true;
            pendingTarget[i] = -1;
            lastCd[i] = Mathf.Repeat((float)centreDistance.GetValue(splines[i]), length);
            total[i] = lastCd[i] > length * 0.5f ? lastCd[i] - length : lastCd[i];
        }
        for (int a = 0; a < cars; a++)
            for (int b = 0; b < cars; b++) aheadOf[a, b] = total[a] > total[b];
        var index = new Dictionary<object, int>();
        for (int i = 0; i < cars; i++) index[splines[i]] = i;

        int lap = -1, lead = 0;
        float lapClock = 0f, clock = 0f;
        double leadMark = 0;
        int maxSteps = Mathf.RoundToInt((TimedLaps + 2f) * 40f / dt);
        for (int step = 0; step < maxSteps; step++)
        {
            SetGreen(raceStart, step * dt);
            for (int s = 0; s < steps.Length; s++) steps[s]();
            clock += dt;
            lapClock += dt;

            for (int i = 0; i < cars; i++)
            {
                cd[i] = Mathf.Repeat((float)centreDistance.GetValue(splines[i]), length);
                float delta = Mathf.Repeat(cd[i] - lastCd[i] + length * 0.5f, length) - length * 0.5f;
                total[i] += delta;
                lastCd[i] = cd[i];
                lat[i] = (float)carLateral.GetValue(splines[i]);
                v[i] = (float)speedMps.GetValue(pvcs[i]);
                lane[i] = (int)ovalLane.GetValue(splines[i]);
                wasChanging[i] = changing[i];
                changing[i] = (bool)laneChanging.GetValue(splines[i]);
                // Sideways speed against the lanes themselves (the inside lane's line): across the road the lanes
                // drift metres as Texas's width changes, and that is not the car moving between them.
                float rel = lat[i] - (float)laneLatAt.Invoke(splines[i], new object[] { insideLane, cd[i] });
                sideRate[i] = step > 0 ? Mathf.Abs(rel - prevLat[i]) / dt : 0f;
                prevLat[i] = rel;
            }

            // Laps: the race leader's distance run.
            for (int i = 0; i < cars; i++) if (total[i] > total[lead]) lead = i;
            if (total[lead] - leadMark >= length)
            {
                leadMark += length;
                lap++;
                if (lap > 0) r.leaderLaps.Add(lapClock);
                lapClock = 0f;
            }
            if (lap >= TimedLaps) break;
            if (lap < 0)
            {
                // Forming up: the counters are read from the first timed lap.
                for (int i = 0; i < cars; i++)
                {
                    startedSeen[i] = (int)started.GetValue(splines[i]);
                    baseCompleted[i] = (int)completed.GetValue(splines[i]);
                    baseAborted[i] = (int)aborted.GetValue(splines[i]);
                }
                continue;
            }

            for (int i = 0; i < cars; i++)
            {
                // A lane change starting: what it is passing, and how close the car behind in the new lane is.
                int st = (int)started.GetValue(splines[i]);
                if (st > startedSeen[i])
                {
                    startedSeen[i] = st;
                    r.changesByCar[i]++;
                    var ahead = passing.GetValue(splines[i]);
                    if (pendingTarget[i] >= 0) r.noPass++;
                    pendingTarget[i] = ahead != null && index.TryGetValue(ahead, out int k) ? k : -1;
                    pendingSince[i] = clock;
                    for (int j = 0; j < cars; j++)
                    {
                        if (j == i || lane[j] != lane[i]) continue;
                        float behind = Mathf.Repeat(cd[i] - cd[j], length);
                        if (behind < length * 0.5f) r.minRearGapAtStart = Mathf.Min(r.minRearGapAtStart, behind);
                    }
                }
                if (pendingTarget[i] >= 0 && (bool)aborting.GetValue(splines[i])) pendingTarget[i] = -1;   // counted as aborted
                if (pendingTarget[i] >= 0)
                {
                    if (total[i] > total[pendingTarget[i]] + 2f * CarHalfLength) { r.passes++; pendingTarget[i] = -1; }
                    else if (clock - pendingSince[i] > PassWindowSeconds) { r.noPass++; pendingTarget[i] = -1; }
                }

                float want = (float)targetLat.Invoke(splines[i], new object[] { cd[i] });
                float err = Mathf.Abs(lat[i] - want);
                float latRate = sideRate[i];
                if (changing[i] && wasChanging[i])
                {
                    r.errInSum += err; r.errInN++;
                    r.errInMax = Mathf.Max(r.errInMax, err);
                    r.maxLatRateIn = Mathf.Max(r.maxLatRateIn, latRate);
                }
                else
                {
                    r.errOutSum += err; r.errOutN++;
                    if (!wasChanging[i]) r.maxLatRateOut = Mathf.Max(r.maxLatRateOut, latRate);
                    if (err > r.errOutMax) { r.errOutMax = err; r.errOutMaxAt = cd[i]; }
                }

                float half = sHalf[Index(sDist, cd[i])];
                float towardInfield = lat[i] * insideSide;
                bool onWall = half + InfieldWallOffset - (towardInfield + CarHalfWidth) <= 0f
                              || half + OutsideWallOffset - (-towardInfield + CarHalfWidth) <= 0f;
                if (onWall && !wasWall[i]) { r.wallHits++; r.incidents.Append($"\n  lap {lap}: car {i} wall at {cd[i]:0} m"); }
                wasWall[i] = onWall;
                float slip = (float)slipDeg.GetValue(pvcs[i]);
                r.maxSlip = Mathf.Max(r.maxSlip, Mathf.Abs(slip));
                bool on = (bool)onLegal.Invoke(null, new object[] { track, splines[i].transform.position, 1f });
                bool rec = (bool)recovering.GetValue(inputs[i]);
                if (!on && wasOn[i]) { r.offs++; r.incidents.Append($"\n  lap {lap}: car {i} off at {cd[i]:0} m"); }
                if (rec && !wasRec[i]) { r.spins++; r.incidents.Append($"\n  lap {lap}: car {i} spin at {cd[i]:0} m, slip {slip:0.0}°"); }
                wasOn[i] = on; wasRec[i] = rec;
                if ((bool)trainEmergency.GetValue(splines[i])) r.emergencySteps++;
            }

            for (int a = 0; a < cars; a++)
                for (int b = a + 1; b < cars; b++)
                {
                    float along = Mathf.Abs(Mathf.Repeat(cd[a] - cd[b] + length * 0.5f, length) - length * 0.5f);
                    float across = Mathf.Abs(lat[a] - lat[b]);
                    if (lane[a] == lane[b] && !changing[a] && !changing[b]) r.minBumper = Mathf.Min(r.minBumper, along - 2f * CarHalfLength);
                    else if (along < 2f * CarHalfLength) r.minDoor = Mathf.Min(r.minDoor, across - 2f * CarHalfWidth);
                    bool touch = along < 2f * CarHalfLength && across < 2f * CarHalfWidth;
                    if (touch && !touching[a, b])
                    {
                        r.contacts++;
                        r.incidents.Append($"\n  lap {lap}: contact {a} / {b} at {cd[a]:0} m ({along:0.00} m along, {across:0.00} m across, {v[a]:0.0} / {v[b]:0.0} m/s, changing {changing[a]} / {changing[b]})");
                    }
                    touching[a, b] = touch;
                    bool ab = total[a] > total[b];
                    if (ab != aheadOf[a, b] && Math.Abs(total[a] - total[b]) < 50.0) r.overtakes++;
                    aheadOf[a, b] = ab;
                }
        }
        r.laps = r.leaderLaps.Count;
        for (int i = 0; i < cars; i++)
        {
            r.started += r.changesByCar[i];
            r.completed += (int)completed.GetValue(splines[i]) - baseCompleted[i];
            r.aborted += (int)aborted.GetValue(splines[i]) - baseAborted[i];
        }
        r.incidents.Append(paceLine);

        var unregister = Runtime("RaceField").GetMethod("Unregister");
        foreach (var s in splines) unregister.Invoke(null, new object[] { s });
        return r;
    }

    // The gap closing during a move. A quick car (A) runs up behind a slow one (B) in the inside lane and pulls out to
    // pass; the moment it is under way a third car (C, kinematic, outside lane) is dropped 14 m behind it in the lane it
    // is moving into, at its speed - closer than the room a move under way keeps (abortShare of a train gap). A must turn
    // back to the inside lane the same smooth way, not swerve, and nothing may touch.
    [Test]
    public void ALaneChangeTurnsBackWhenTheGapCloses()
    {
        var calib = Runtime("AIPaceCalibration").GetMethod("ApplyFor");
        var raceStart = Runtime("RaceStart");
        try
        {
            calib.Invoke(null, new object[] { TrackId });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
            raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
            SpawnTrack(TrackId);
            RunAbort(raceStart);
        }
        finally
        {
            calib.Invoke(null, new object[] { "" });
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        }
    }

    void RunAbort(Type raceStart)
    {
        var trackType = Runtime("TrackBuilder");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);
        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        Component MakeSpline(string name, string lane, float pace, bool dynamic, bool passing)
        {
            var go = new GameObject(name);
            _cars.Add(go);
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "externalMotionController", dynamic);
            Set(spline, "loop", true);
            Set(spline, "spawnInPit", false);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", 60f);
            Set(spline, "paceMultiplier", pace * aiPace);
            SetLane(spline, lane);
            Set(spline, "lanePassing", passing);
            return spline;
        }

        var steps = new List<Action>();
        Component[] MakeDynamic(string name, string lane, float pace, float start)
        {
            var spline = MakeSpline(name, lane, pace, true, true);
            var go = spline.gameObject;
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
            Set(pvc, "corneringStiffness", 15f);
            var input = go.AddComponent(inputType);
            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { pace });
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");
            Set(spline, "startDistance", start);
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            Call(input, "OnEnable");
            steps.Add(Step(racing)); steps.Add(Step(input)); steps.Add(Step(pvc)); steps.Add(Step(spline));
            return new[] { spline, pvc, input };
        }

        var a = MakeDynamic("A", "Inside", 1.04f, 0f);
        var b = MakeDynamic("B", "Inside", 0.95f, 40f);
        var c = MakeSpline("C", "Outside", 1f, false, false);
        Call(c, "Awake");
        Call(c, "OnEnable");
        Call(c, "Start");
        float length = (float)splineType.GetProperty("TrackLength").GetValue(c);
        Set(c, "startDistance", length * 0.5f);
        splineType.GetMethod("PlaceAtStartDistance").Invoke(c, null);
        steps.Add(Step(c));

        var centre = splineType.GetProperty("CentreDistanceOnTrack");
        var pathD = splineType.GetProperty("DistanceOnTrack");
        var carLat = splineType.GetProperty("CarLateral");
        var changingP = splineType.GetProperty("LaneChanging");
        var progressP = splineType.GetProperty("LaneChangeProgress");
        var abortedP = splineType.GetProperty("LaneChangesAborted");
        var completedP = splineType.GetProperty("LaneChangesCompleted");
        var ovalLane = splineType.GetField("ovalLane");
        var targetLat = splineType.GetMethod("LaneTargetLateralAt");
        var laneLatAt = splineType.GetMethod("LaneLateralAt");
        object inside = Enum.Parse(ovalLane.FieldType, "Inside"), outside = Enum.Parse(ovalLane.FieldType, "Outside");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var recovering = inputType.GetProperty("IsRecovering");
        var cars = new[] { a[0], b[0], c };

        float dt = Time.fixedDeltaTime;
        bool dropped = false, landedBack = false;
        int contacts = 0, spins = 0;
        float maxOut = 0f, maxErr = 0f, maxRate = 0f, prevRel = 0f, droppedAt = -1f, backAt = -1f;
        var touching = new bool[3, 3];
        var sb = new StringBuilder("[OvalPassing] abort:");
        int maxSteps = Mathf.RoundToInt(80f / dt);
        for (int step = 0; step < maxSteps && !landedBack; step++)
        {
            SetGreen(raceStart, step * dt);
            foreach (var s in steps) s();
            float t = step * dt;
            bool changing = (bool)changingP.GetValue(a[0]);
            float aCd = Mathf.Repeat((float)centre.GetValue(a[0]), length);
            float aLat = (float)carLat.GetValue(a[0]);
            float va = (float)speedMps.GetValue(a[1]);
            if (!dropped && changing && (float)progressP.GetValue(a[0]) > 0.05f)
            {
                // C into the lane A is moving into, 14 m behind it centre to centre, at its speed.
                Set(c, "speed", va);
                Set(c, "startDistance", Mathf.Repeat((float)pathD.GetValue(a[0]) - 14f, length));
                splineType.GetMethod("PlaceAtStartDistance").Invoke(c, null);
                dropped = true;
                droppedAt = t;
                sb.Append($" A pulled out at {aCd:0} m, {t:0.0} s, {va:0.0} m/s; C dropped 14 m behind.");
            }
            if (dropped)
            {
                float rel = aLat - (float)laneLatAt.Invoke(a[0], new object[] { inside, aCd });
                float spacing = Mathf.Abs((float)laneLatAt.Invoke(a[0], new object[] { outside, aCd }) - (float)laneLatAt.Invoke(a[0], new object[] { inside, aCd }));
                maxOut = Mathf.Max(maxOut, Mathf.Abs(rel) / Mathf.Max(0.5f, spacing));
                if (t > droppedAt + dt) maxRate = Mathf.Max(maxRate, Mathf.Abs(rel - prevRel) / dt);
                prevRel = rel;
                maxErr = Mathf.Max(maxErr, Mathf.Abs(aLat - (float)targetLat.Invoke(a[0], new object[] { aCd })));
                if (!changing && backAt < 0f) backAt = t;
                if (backAt >= 0f && t > backAt + 2f) landedBack = true;   // two seconds settled on the lane after
            }
            if ((bool)recovering.GetValue(a[2])) spins++;
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    float di = Mathf.Repeat((float)centre.GetValue(cars[i]), length), dj = Mathf.Repeat((float)centre.GetValue(cars[j]), length);
                    float along = Mathf.Abs(Mathf.Repeat(di - dj + length * 0.5f, length) - length * 0.5f);
                    float across = Mathf.Abs((float)carLat.GetValue(cars[i]) - (float)carLat.GetValue(cars[j]));
                    bool touch = along < 2f * CarHalfLength && across < 2f * CarHalfWidth;
                    if (touch && !touching[i, j]) contacts++;
                    touching[i, j] = touch;
                }
        }
        int aborted = (int)abortedP.GetValue(a[0]), completed = (int)completedP.GetValue(a[0]);
        sb.Append($" aborted {aborted}, completed {completed}; back on the inside lane after {backAt - droppedAt:0.00} s; furthest across {maxOut:P0} of the way;" +
                  $" fastest sideways {maxRate:0.00} m/s; max error vs the blended line {maxErr:0.000} m; contacts {contacts}, spins {spins}; final lane {ovalLane.GetValue(a[0])}");
        Debug.Log(sb.ToString());
        var unregister = Runtime("RaceField").GetMethod("Unregister");
        foreach (var s in cars) unregister.Invoke(null, new object[] { s });

        Assert.IsTrue(dropped, "A never pulled out to pass B. " + sb);
        Assert.AreEqual(1, aborted, sb.ToString());
        Assert.AreEqual(0, completed, "A finished the move into the path of C. " + sb);
        Assert.AreEqual(inside, ovalLane.GetValue(a[0]), sb.ToString());
        Assert.IsTrue(landedBack, "A never settled back on its lane. " + sb);
        Assert.Less(maxOut, 0.5f, "Turning back took A past the middle. " + sb);
        Assert.Less(maxErr, 1f, sb.ToString());
        Assert.AreEqual(0, contacts, sb.ToString());
        Assert.AreEqual(0, spins, sb.ToString());
    }

    // ---------------------------------------------------------------- helpers (as OvalLaneTests)

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
