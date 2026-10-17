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
// PackRaceSimTests covers the player's own (dynamic-model) field; this one says whether the watched field races:
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
            if (start.watchedTuning) Call(racing, "RaceAsWatchedField");
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
