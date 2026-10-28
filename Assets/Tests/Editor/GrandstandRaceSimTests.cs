using System;
using System.Collections.Generic;
using System.Reflection;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The race another championship runs while the player watches from the grandstand, with no play mode.
//
// GridSpawner.SpawnAmbientField builds that field on the cheap kinematic brain — SplineDriver writes the transform
// itself, no PlayerVehicleController — with AIRacingBehaviour and a driver's ratings on top, exactly as this does.
// The player's own (dynamic-model) field had PackRaceSimTests (removed 2026-10-25, in git history); this one says whether the watched field races:
// how many places change hands and how much of the time cars spend side by side, rather than circulating nose to
// tail in one line.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class GrandstandRaceSimTests
{
    GameObject _package;
    readonly List<GameObject> _cars = new List<GameObject>();

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

    [TearDown]
    public void Despawn()
    {
        foreach (var c in _cars)
        {
            if (c == null) continue;
            var s = c.GetComponent(Runtime("SplineDriver"));
            if (s != null) Call(s, "OnDisable");
            var r = c.GetComponent(Runtime("AIRacingBehaviour"));
            if (r != null) Call(r, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(c);
        }
        _cars.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    public class Result
    {
        public int passes;            // places gained after the first 20 s, summed over the field
        public float alongsideShare;  // car-time with another car door to door (different lane, within a length)
        public float meanMph;
        public float firstLapSeconds; // how long the leader took to reach the line for the first time
        public float startMph;        // the field's mean speed over its first two seconds
    }

    public class Start
    {
        public bool fromCorner = true;   // the field already racing off the last corner (false = the old grid)
        public float startMps = 22f;     // the old grid's rolling speed
        public float firstRow = 20f, spacing = 8f, stagger = 4f;   // RaceScene's GridSpawner values
        public bool watchedTuning = true;                          // AIRacingBehaviour.RaceAsWatchedField
        public Dictionary<string, object> racing;                  // AIRacingBehaviour field overrides on top
    }

    // Laid out the way GridSpawner.SpawnAmbientField lays out a watched race.
    Result Run(string trackId, int cars, float seconds, Start start)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"no track package for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var track = _package.GetComponentInChildren(Runtime("TrackBuilder"), true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { trackId });
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
        float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);

        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        var info = track.GetType().GetField("track").GetValue(track);
        float sf = (float)info.GetType().GetField("startFinishDistance").GetValue(info);

        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var spawner = Runtime("GridSpawner");
        UnityEngine.Random.InitState(11);
        var splines = new Component[cars];
        var racers = new Component[cars];
        var fromLine = new float[cars];
        float len = 0f, firstRow = Mathf.Abs(start.firstRow);
        for (int i = 0; i < cars; i++)
        {
            var go = new GameObject($"Car{i:D2}");
            _cars.Add(go);

            // A roster-like spread of ratings, shuffled against grid order so quick cars start behind slow ones.
            float q01 = Mathf.Lerp(0.4f, 1f, ((i * 5) % cars) / (float)(cars - 1));
            float cons01 = Mathf.Lerp(0.65f, 0.95f, ((i * 7) % cars) / (float)(cars - 1));
            float agg01 = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % cars) / (float)(cars - 1));

            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "externalMotionController", false);
            Set(spline, "loop", true);
            Set(spline, "spawnInPit", false);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", start.startMps);
            Set(spline, "lateralOffset", start.fromCorner ? 0f : ((i % 2 == 0) ? start.stagger * 0.5f : -start.stagger * 0.5f));
            Set(spline, "qualifyingPosition", i);
            var racing = go.AddComponent(racingType);

            const int statMax = 20;
            var ratings = AIRatings.ForEvent(Mathf.RoundToInt(q01 * statMax), Mathf.RoundToInt(q01 * statMax),
                                             Mathf.RoundToInt(cons01 * statMax), Mathf.RoundToInt(agg01 * statMax), statMax,
                                             UnityEngine.Random.value, UnityEngine.Random.value);
            Set(spline, "lineFactor", AIRatings.LineFactor(ratings.aggression01));
            Set(racing, "aggression01", ratings.aggression01);
            Set(racing, "consistency01", ratings.consistency01);
            float basePace = AIRatings.BasePace(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            Set(spline, "paceMultiplier", basePace);
            Set(spline, "cornerCommitment", AIRatings.CornerCommitment(ratings.strength01, ratings.consistency01, UnityEngine.Random.value));

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { basePace });
            if (start.watchedTuning) racingType.GetMethod("RaceAsWatchedField").Invoke(racing, new object[] { false });
            if (start.racing != null) foreach (var kv in start.racing) Set(racing, kv.Key, kv.Value);
            Call(spline, "Start");
            if (i == 0)
            {
                len = (float)splineType.GetProperty("TrackLength").GetValue(spline);
                if (start.fromCorner)
                {
                    float lead = (float)spawner.GetMethod("LastCornerLead", BindingFlags.Static | BindingFlags.Public)
                                              .Invoke(null, new object[] { track });
                    if (lead >= 0f) firstRow = Mathf.Max(firstRow, lead);
                }
            }
            fromLine[i] = -Draftmaster.Weekend.GrandstandRace.GridProgress(i, firstRow, start.spacing);
            Set(spline, "startDistance", Wrap(sf - fromLine[i], len));
            if (start.fromCorner)
                Set(spline, "speed", (float)splineType.GetMethod("RacePaceMphAt").Invoke(spline, new object[] { Wrap(sf - fromLine[i], len), aiPace }) / 2.237f);
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            if (start.fromCorner)
            {
                float column = (i % 2 == 0) ? start.stagger * 0.5f : -start.stagger * 0.5f;
                float untactical = (float)splineType.GetProperty("UntacticalLateral").GetValue(spline);
                racingType.GetMethod("TakeLateral").Invoke(racing, new object[] { column - untactical });
            }

            splines[i] = spline; racers[i] = racing;
        }
        Assert.Greater(len, 100f, "The AI brain never built its spline.");

        var distProp = splineType.GetProperty("DistanceOnTrack");
        var latProp = splineType.GetProperty("LateralOnTrack");
        var mphProp = splineType.GetProperty("CurrentMph");
        var racingStep = racingType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        var r = new Result { firstLapSeconds = -1f };
        float dt = Time.fixedDeltaTime;
        var lastD = new float[cars];
        var progress = new float[cars];   // metres past the start/finish line, from the grid
        for (int i = 0; i < cars; i++)
        {
            lastD[i] = (float)distProp.GetValue(splines[i]);
            progress[i] = -fromLine[i];
        }
        int[] prevOrder = null;
        int shapeSamples = 0, alongside = 0, mphSamples = 0;
        double mphSum = 0, startSum = 0;
        int startSamples = 0;
        int steps = Mathf.RoundToInt(seconds / dt);
        for (int step = 0; step < steps; step++)
        {
            float t = step * dt;
            SetGreenClock(raceStart, t);
            for (int i = 0; i < cars; i++)
            {
                racingStep.Invoke(racers[i], null);
                splineStep.Invoke(splines[i], null);
            }

            for (int i = 0; i < cars; i++)
            {
                float d = (float)distProp.GetValue(splines[i]);
                float moved = d - lastD[i];
                if (moved < -len * 0.5f) moved += len;
                progress[i] += moved;
                lastD[i] = d;
                if (r.firstLapSeconds < 0f && progress[i] >= 0f) r.firstLapSeconds = t;
            }

            if (Trace != null && step % 25 == 0 && t > 15f && Trace.Count < 120)
                for (int i = 0; i < cars; i++)
                    for (int j = 0; j < cars; j++)
                    {
                        float g = progress[j] - progress[i];
                        if (j == i || g <= 0f || g > 40f) continue;
                        Trace.Add($"t{t:0.0} car{i} behind car{j} gap {g:0.0} lat {(float)latProp.GetValue(splines[i]):0.0}/{(float)latProp.GetValue(splines[j]):0.0} " +
                                  $"mph {(float)mphProp.GetValue(splines[i]):0}/{(float)mphProp.GetValue(splines[j]):0} desired {Get(splines[i], "DesiredMph"):0.0}/{Get(splines[j], "DesiredMph"):0.0} " +
                                  $"cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(splines[i])):0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(splines[i]):0.0} " +
                                  $"phase {splineType.GetProperty("CurrentPhase").GetValue(splines[i])} cool {(float)racingType.GetField("_cooldownTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0}");
                        break;
                    }

            if (t < 2f)
            {
                for (int i = 0; i < cars; i++) startSum += (float)mphProp.GetValue(splines[i]);
                startSamples += cars;
            }

            if (step % 10 != 0) continue;
            var order = new int[cars];
            for (int i = 0; i < cars; i++) order[i] = i;
            Array.Sort(order, (a, b) => progress[b].CompareTo(progress[a]));
            // Places gained: a car moving from P5 to P3 has passed two cars.
            var pos = new int[cars];
            for (int p = 0; p < cars; p++) pos[order[p]] = p;
            if (t > 20f && prevOrder != null)
                for (int c = 0; c < cars; c++) r.passes += Mathf.Max(0, prevOrder[c] - pos[c]);
            prevOrder = pos;

            if (t < 10f) continue;
            for (int i = 0; i < cars; i++)
            {
                shapeSamples++;
                mphSum += (float)mphProp.GetValue(splines[i]); mphSamples++;
                float li = (float)latProp.GetValue(splines[i]);
                for (int j = 0; j < cars; j++)
                {
                    if (j == i) continue;
                    float g = Mathf.Abs(progress[j] - progress[i]);
                    float lat = Mathf.Abs((float)latProp.GetValue(splines[j]) - li);
                    if (g < 5.2f && lat > 1.5f) { alongside++; break; }
                }
            }
        }
        r.alongsideShare = shapeSamples > 0 ? alongside / (float)shapeSamples : 0f;
        r.meanMph = mphSamples > 0 ? (float)(mphSum / mphSamples) : 0f;
        r.startMph = startSamples > 0 ? (float)(startSum / startSamples) : 0f;
        return r;
    }

    // RaceStart's clock runs off Time in play; in edit mode the green has to be moved back by hand so
    // SecondsSinceGreen advances with the sim.
    static void SetGreenClock(Type raceStart, float t)
    {
        var f = raceStart.GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(f, "RaceStart.GreenTime is gone.");
        f.SetValue(null, Time.time - t);
    }

    List<string> Trace;
    static float Get(Component c, string prop) => (float)c.GetType().GetProperty(prop).GetValue(c);

    [Explicit("Diagnostic: chasers' state while close behind at Watkins Glen.")]
    [TestCase(false)] [TestCase(true)]
    public void TraceWatkinsGlen(bool tuned)
    {
        Trace = new List<string>();
        var r = Run("WatkinsGlen", 16, 60f, new Start { watchedTuning = tuned });
        Log("trace", "WatkinsGlen", r);
        Debug.Log(string.Join(Environment.NewLine, Trace));
    }

    static float Wrap(float d, float len) => len > 0f ? ((d % len) + len) % len : d;

    static void Log(string label, string track, Result r) =>
        Debug.Log($"[GrandstandRaceSim] {label} {track}: passes {r.passes}, alongside {r.alongsideShare:P0}, " +
                  $"mean {r.meanMph:0} mph (first 2 s {r.startMph:0}), leader at the line after {r.firstLapSeconds:0.0} s");

    // The complaint: watched from the stand at Watkins Glen, the field drove round in a single line. Stock
    // racecraft from the corner start managed 3 places in 100 s with cars side by side 3% of the time; the watched
    // field's racecraft ran 17 and 10%.
    [Test]
    public void TheWatchedField_Races_AtWatkinsGlen()
    {
        var r = Run("WatkinsGlen", 16, 120f, new Start());
        Log("watched", "WatkinsGlen", r);
        Assert.GreaterOrEqual(r.passes, 8, "the watched field hardly passed - single file again");
        Assert.GreaterOrEqual(r.alongsideShare, 0.06f, "the watched field is hardly ever side by side");
    }

    // ...without spoiling an oval, where it already raced (stock: 36 places, 45% side by side at Martinsville).
    [Test]
    public void TheWatchedField_StillRaces_AtMartinsville()
    {
        var r = Run("Martinsville", 16, 120f, new Start());
        Log("watched", "Martinsville", r);
        Assert.GreaterOrEqual(r.passes, 20);
        Assert.GreaterOrEqual(r.alongsideShare, 0.3f);
    }

    // No formation lap: the field appears off the last corner already at racing speed (the old grid rolled
    // away from the line at 49 mph), so the leader is at the line within seconds.
    [TestCase("WatkinsGlen")] [TestCase("Martinsville")]
    public void TheWatchedField_StartsAtRacePace_OffTheLastCorner(string track)
    {
        var r = Run(track, 16, 30f, new Start());
        Log("start", track, r);
        Assert.Greater(r.firstLapSeconds, 0f, "the leader never reached the line");
        Assert.Less(r.firstLapSeconds, 10f, "the leader took too long to reach the line - not starting off the last corner");
        Assert.GreaterOrEqual(r.startMph, r.meanMph * 0.75f, "the field started well below racing speed");
    }

    [Explicit("Diagnostic: the old two-by-two grid behind the line, the corner start, and the corner start raced as a watched field.")]
    [TestCase("WatkinsGlen")] [TestCase("Martinsville")] [TestCase("Daytona")] [TestCase("Charlotte")] [TestCase("Sonoma")]
    public void Compare(string track)
    {
        Log("old grid", track, Run(track, 16, 120f, new Start { fromCorner = false, watchedTuning = false }));
        Despawn();
        Log("corner, stock racecraft", track, Run(track, 16, 120f, new Start { watchedTuning = false }));
        Despawn();
        Log("corner, watched racecraft", track, Run(track, 16, 120f, new Start()));
    }

    [Explicit("Diagnostic: racecraft knobs for the watched field.")]
    [TestCase("WatkinsGlen")] [TestCase("Sonoma")] [TestCase("Charlotte")] [TestCase("Martinsville")]
    public void Sweep(string track)
    {
        Dictionary<string, object> D(params (string, object)[] kv)
        {
            var d = new Dictionary<string, object>();
            foreach (var (k, v) in kv) d[k] = v;
            return d;
        }
        var variants = new (string, Dictionary<string, object>)[]
        {
            ("stock", null),
            ("clear 2.4 step 5 hold 3 head .2", D(("corridorClearWidthNoLanes", 2.4f), ("overtakeLineOffset", 5f), ("commitHoldSeconds", 3f), ("followHeadwaySeconds", 0.2f))),
            ("clear 2.4 step 5 hold 3", D(("corridorClearWidthNoLanes", 2.4f), ("overtakeLineOffset", 5f), ("commitHoldSeconds", 3f))),
            ("clear 2.4 step 5 head .2", D(("corridorClearWidthNoLanes", 2.4f), ("overtakeLineOffset", 5f), ("followHeadwaySeconds", 0.2f))),
            ("clear 2.4 step 5 lat 2.4 hold 3 head .2", D(("corridorClearWidthNoLanes", 2.4f), ("overtakeLineOffset", 5f), ("maxLateralSpeed", 2.4f), ("commitHoldSeconds", 3f), ("followHeadwaySeconds", 0.2f))),
        };
        foreach (var (label, racing) in variants)
        {
            Log(label, track, Run(track, 16, 120f, new Start { watchedTuning = false, racing = racing }));
            Despawn();
        }
    }

    [Explicit("Diagnostic: where the watched race starts on every packaged track.")]
    [Test]
    public void LastCornerLeads()
    {
        var lines = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var track = prefab.GetComponentInChildren(Runtime("TrackBuilder"), true);
            if (track == null) continue;
            var info = track.GetType().GetField("track").GetValue(track);
            if (info == null) continue;
            var samples = (System.Collections.IList)track.GetType().GetMethod("SampleCenterline").Invoke(track, null);
            if (samples.Count == 0) continue;
            var last = samples[samples.Count - 1];
            float len = (float)last.GetType().GetField("distance").GetValue(last);
            float lead = (float)Runtime("GridSpawner").GetMethod("LastCornerLead").Invoke(null, new object[] { track });
            float sfd = (float)info.GetType().GetField("startFinishDistance").GetValue(info);
            var segs = (Array)info.GetType().GetField("segments").GetValue(info);
            var segText = new System.Text.StringBuilder();
            for (int k = 0; k < Mathf.Min(segs.Length, 6); k++)
            {
                var sg = segs.GetValue(k);
                segText.Append($" [{sg.GetType().GetField("type").GetValue(sg)} {(float)sg.GetType().GetField("length").GetValue(sg):0} m {(float)sg.GetType().GetField("angle").GetValue(sg):0}]");
            }
            var lastSeg = segs.GetValue(segs.Length - 1);
            segText.Append($" .. last [{lastSeg.GetType().GetField("type").GetValue(lastSeg)} {(float)lastSeg.GetType().GetField("length").GetValue(lastSeg):0} m {(float)lastSeg.GetType().GetField("angle").GetValue(lastSeg):0}]");
            lines.Add($"{prefab.name}: lap {len:0} m, line at {sfd:0}, last corner exit {lead:0} m short of the line;{segText}");
        }
        Debug.Log("[GrandstandRaceSim] " + string.Join(Environment.NewLine, lines));
    }

    // ---- The watched race on the dynamic model ----
    //
    // Since 3291479e a watched race is driven through the same PlayerVehicleController + SplineInputDriver pipeline as
    // the player's own field (GridSpawner.DriveWithDynamicModel). This lays it out the way SpawnAmbientField does - the
    // corner start, one rolling speed for the whole grid, the columns handed over by TakeLateral - and measures where
    // each car really is (its body, not the brain's point) as it passes Turn 1's apex: how far outside its own line,
    // how fast, and whether a pass was what put it there. No car-to-car contact: nothing steps Physics2D.

    public class ApexResult
    {
        public int lapOneCars, laterCars;
        public float lapOneOutside, laterOutside;   // mean metres outside the car's own line at the apex (+ = wide)
        public float lapOneMph, laterMph;
        public float lapOnePassing, laterPassing;   // share of cars with a pass on (commit timer running) at the apex
        public float widest;                        // the widest any car ran
        public int passes;                          // places gained after the first 20 s, summed over the field
        public readonly System.Text.StringBuilder detail = new System.Text.StringBuilder();
    }

    // Turn 1: the sharpest bend in the first 900 m past the line, and which side of the road is its inside (+1 = the
    // right-hand normal, SplineDriver's positive lateral).
    static void FindTurnOne(Component track, float sf, out float apexD, out float insideSign, out float lapLength)
    {
        var samples = (System.Collections.IList)track.GetType().GetMethod("SampleCenterline").Invoke(track, null);
        var st = samples[0].GetType();
        var fPos = st.GetField("position"); var fTan = st.GetField("tangent"); var fDist = st.GetField("distance");
        int n = samples.Count;
        lapLength = (float)fDist.GetValue(samples[n - 1]);
        apexD = sf; insideSign = 1f;
        float best = 0f;
        for (int i = 0; i < n; i++)
        {
            float d = (float)fDist.GetValue(samples[i]);
            float past = Wrap(d - sf, lapLength);
            if (past < 50f || past > 900f) continue;
            int a = i, b = i;
            while (a > 0 && d - (float)fDist.GetValue(samples[a]) < 15f) a--;
            while (b < n - 1 && (float)fDist.GetValue(samples[b]) - d < 15f) b++;
            Vector2 ta = (Vector2)fTan.GetValue(samples[a]), tb = (Vector2)fTan.GetValue(samples[b]);
            float cross = ta.x * tb.y - ta.y * tb.x;
            float span = (float)fDist.GetValue(samples[b]) - (float)fDist.GetValue(samples[a]);
            float k = span > 1f ? Mathf.Abs(cross) / span : 0f;
            if (k > best) { best = k; apexD = d; insideSign = cross < 0f ? 1f : -1f; }
        }
    }

    // watched: null = the player's field's racecraft, else RaceAsWatchedField(dynamicModel: watched).
    ApexResult RunDynamic(string trackId, int cars, float seconds, bool? watched, Dictionary<string, object> racingOverrides = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"no track package for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);

        var trackType = Runtime("TrackBuilder");
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { trackId });
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
        float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        var info = trackType.GetField("track").GetValue(track);
        float sf = (float)info.GetType().GetField("startFinishDistance").GetValue(info);
        FindTurnOne(track, sf, out float apexD, out float insideSign, out float lapLen);

        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");
        var start = new Start();
        UnityEngine.Random.InitState(11);
        var splines = new Component[cars];
        var racers = new Component[cars];
        var pvcs = new Component[cars];
        var inputs = new Component[cars];
        float firstRow = Mathf.Abs(start.firstRow);
        float lead = (float)Runtime("GridSpawner").GetMethod("LastCornerLead", BindingFlags.Static | BindingFlags.Public)
                                                 .Invoke(null, new object[] { track });
        if (lead >= 0f) firstRow = Mathf.Max(firstRow, lead);
        for (int i = 0; i < cars; i++)
        {
            var go = new GameObject($"Car{i:D2}");
            _cars.Add(go);
            float q01 = Mathf.Lerp(0.4f, 1f, ((i * 5) % cars) / (float)Mathf.Max(1, cars - 1));
            float cons01 = Mathf.Lerp(0.65f, 0.95f, ((i * 7) % cars) / (float)Mathf.Max(1, cars - 1));
            float agg01 = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % cars) / (float)Mathf.Max(1, cars - 1));

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
            Set(spline, "qualifyingPosition", i);
            var racing = go.AddComponent(racingType);
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

            const int statMax = 20;
            var ratings = AIRatings.ForEvent(Mathf.RoundToInt(q01 * statMax), Mathf.RoundToInt(q01 * statMax),
                                             Mathf.RoundToInt(cons01 * statMax), Mathf.RoundToInt(agg01 * statMax), statMax,
                                             UnityEngine.Random.value, UnityEngine.Random.value);
            Set(spline, "lineFactor", AIRatings.LineFactor(ratings.aggression01));
            Set(racing, "aggression01", ratings.aggression01);
            Set(racing, "consistency01", ratings.consistency01);
            float basePace = AIRatings.BasePace(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            Set(spline, "paceMultiplier", basePace);
            Set(spline, "cornerCommitment", AIRatings.CornerCommitment(ratings.strength01, ratings.consistency01, UnityEngine.Random.value));

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { basePace });
            if (watched.HasValue) racingType.GetMethod("RaceAsWatchedField").Invoke(racing, new object[] { watched.Value });
            if (racingOverrides != null) foreach (var kv in racingOverrides) Set(racing, kv.Key, kv.Value);
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");

            float fromLine = Draftmaster.Weekend.GrandstandRace.GridProgress(i, firstRow, start.spacing);
            Set(spline, "startDistance", Wrap(sf + fromLine, lapLen));
            splines[i] = spline; racers[i] = racing; pvcs[i] = pvc; inputs[i] = input;
        }

        // GridSpawner: the whole grid at the pace the slowest slot can carry, then the columns.
        float startMph = float.MaxValue;
        foreach (var s in splines)
        {
            float p = (float)splineType.GetMethod("RacePaceMphAt").Invoke(s, new object[] { (float)splineType.GetField("startDistance").GetValue(s), aiPace });
            if (p > 1f) startMph = Mathf.Min(startMph, p);
        }
        for (int i = 0; i < cars; i++)
        {
            Set(splines[i], "speed", startMph / 2.237f);
            splineType.GetMethod("PlaceAtStartDistance").Invoke(splines[i], null);
            Call(inputs[i], "OnEnable");
            float column = (i % 2 == 0) ? start.stagger * 0.5f : -start.stagger * 0.5f;
            float untactical = (float)splineType.GetProperty("UntacticalLateral").GetValue(splines[i]);
            racingType.GetMethod("TakeLateral").Invoke(racers[i], new object[] { column - untactical });
        }

        var distProp = splineType.GetProperty("DistanceOnTrack");
        var untacticalProp = splineType.GetProperty("UntacticalLateral");
        var tacticalField = splineType.GetField("tacticalLateralOffset");
        var speedProp = pvcType.GetProperty("SpeedMps");
        var commitField = racingType.GetField("_commitTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        var nearest = trackType.GetMethod("NearestCenterlineDistance");
        System.Reflection.MethodInfo sampleAt = null;
        foreach (var m in trackType.GetMethods())
            if (m.Name == "SampleAt" && m.GetParameters().Length == 2) { sampleAt = m; break; }
        var racingStep = racingType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        var r = new ApexResult();
        r.detail.AppendLine($"{trackId}: Turn 1 apex at {apexD:0} m (line at {sf:0}, lap {lapLen:0} m), inside {(insideSign > 0 ? "right" : "left")}");
        var lastD = new float[cars];
        var crossings = new int[cars];
        var progress = new float[cars];
        int[] prevOrder = null;
        for (int i = 0; i < cars; i++)
        {
            lastD[i] = (float)distProp.GetValue(splines[i]);
            progress[i] = Wrap(lastD[i] - sf + lapLen * 0.5f, lapLen) - lapLen * 0.5f;
        }
        float dt = Time.fixedDeltaTime;
        int steps = Mathf.RoundToInt(seconds / dt);
        for (int step = 0; step < steps; step++)
        {
            SetGreenClock(raceStart, step * dt);
            for (int i = 0; i < cars; i++)
            {
                racingStep.Invoke(racers[i], null);
                inputStep.Invoke(inputs[i], null);
                pvcStep.Invoke(pvcs[i], null);
                splineStep.Invoke(splines[i], null);
            }
            for (int i = 0; i < cars; i++)
            {
                float d = (float)distProp.GetValue(splines[i]);
                float before = Wrap(apexD - lastD[i], lapLen), moved = Wrap(d - lastD[i], lapLen);
                lastD[i] = d;
                if (moved > lapLen * 0.5f) moved -= lapLen;
                progress[i] += moved;
                if (moved <= 0f || before <= 0f || before > moved) continue;

                // Through the apex: where the body is, against this car's own line.
                Vector3 pos = splines[i].transform.position;
                float cd = (float)nearest.Invoke(track, new object[] { pos });
                var sample = sampleAt.Invoke(track, new object[] { cd, null });
                var smt = sample.GetType();
                Vector2 sp = (Vector2)smt.GetField("position").GetValue(sample);
                Vector2 tg = (Vector2)smt.GetField("tangent").GetValue(sample);
                Vector2 local = track.transform.InverseTransformPoint(pos);
                float carLat = Vector2.Dot(local - sp, new Vector2(tg.y, -tg.x));
                float lineLat = (float)untacticalProp.GetValue(splines[i]);
                float outside = -(carLat - lineLat) * insideSign;
                float mph = (float)speedProp.GetValue(pvcs[i]) * 2.237f;
                bool passing = (float)commitField.GetValue(racers[i]) > 0f;
                float tac = (float)tacticalField.GetValue(splines[i]);
                if (crossings[i] == 0)
                {
                    r.lapOneCars++; r.lapOneOutside += outside; r.lapOneMph += mph; r.lapOnePassing += passing ? 1 : 0;
                }
                else
                {
                    r.laterCars++; r.laterOutside += outside; r.laterMph += mph; r.laterPassing += passing ? 1 : 0;
                }
                r.widest = Mathf.Max(r.widest, outside);
                r.detail.AppendLine($"  t{step * dt:0.0} car{i:D2} lap{crossings[i]} carLat {carLat:0.0} line {lineLat:0.0} outside {outside:0.0} tac {tac:0.0} {mph:0} mph{(passing ? " PASSING" : "")}");
                crossings[i]++;
            }

            if (step % 10 != 0) continue;
            var order = new int[cars];
            for (int i = 0; i < cars; i++) order[i] = i;
            Array.Sort(order, (a, b) => progress[b].CompareTo(progress[a]));
            var place = new int[cars];
            for (int p = 0; p < cars; p++) place[order[p]] = p;
            if (step * dt > 20f && prevOrder != null)
                for (int c = 0; c < cars; c++) r.passes += Mathf.Max(0, prevOrder[c] - place[c]);
            prevOrder = place;
        }
        if (r.lapOneCars > 0) { r.lapOneOutside /= r.lapOneCars; r.lapOneMph /= r.lapOneCars; r.lapOnePassing /= r.lapOneCars; }
        if (r.laterCars > 0) { r.laterOutside /= r.laterCars; r.laterMph /= r.laterCars; r.laterPassing /= r.laterCars; }
        return r;
    }

    static void LogApex(string label, ApexResult r) =>
        Debug.Log($"[GrandstandRaceSim] T1 apex {label}: lap 1 {r.lapOneCars} cars {r.lapOneOutside:0.0} m wide {r.lapOneMph:0} mph passing {r.lapOnePassing:P0}; " +
                  $"later {r.laterCars} cars {r.laterOutside:0.0} m wide {r.laterMph:0} mph passing {r.laterPassing:P0}; widest {r.widest:0.0} m; passes {r.passes}{Environment.NewLine}{r.detail}");

    // The complaint: watching a race from the stand at Watkins Glen, the field all missed Turn 1's apex on a wide, slow
    // outside line. Nearly every car was on a pass, and the ones passing on the outside held the 5 m step all the way
    // round - up to 4.7 m wider than a car on its own (3.9 m outside its line; a car alone runs 0.9 m inside it).
    // A pass not alongside by the braking zone is given up now, and the queue takes the apex.
    [Test]
    public void TheDynamicWatchedField_TakesTurnOneApex_AtWatkinsGlen()
    {
        var r = RunDynamic("WatkinsGlen", 16, 100f, true);
        LogApex("watched", r);
        Assert.Greater(r.laterCars, 8, "too few cars came back round to Turn 1 to judge the line");
        Assert.Less(r.widest, 2f, "a car ran well wide of Turn 1's apex");
        Assert.Less(r.laterOutside, 0f, "the field is not taking Turn 1's apex");
    }

    [Explicit("Diagnostic: Watkins Glen Turn 1 on the dynamic model, watched-field racecraft against the player's field's.")]
    [Test]
    public void TurnOneApex_Dynamic_WatkinsGlen()
    {
        LogApex("solo", RunDynamic("WatkinsGlen", 1, 170f, null));
        Despawn();
        LogApex("player's field racecraft", RunDynamic("WatkinsGlen", 16, 170f, null));
        Despawn();
        LogApex("kinematic watched racecraft (before)", RunDynamic("WatkinsGlen", 16, 170f, false));
        Despawn();
        LogApex("dynamic watched racecraft", RunDynamic("WatkinsGlen", 16, 170f, true));
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }
}
