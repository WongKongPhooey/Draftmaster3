using System.Collections.Generic;
using System.IO;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// The AI's line is no longer asserted, it is trained: RacingLineTrainer drives a lap over and over and keeps
// whatever came out quicker. That only works if the lap it drives is honest, so these tests pin the physics
// first (a constant-radius circle has an answer you can do on paper), then the optimiser's contract — never
// slower than the seed, never off the road, never a line the car could not steer, and always the same answer
// twice, because a line that changed every time it was regenerated would be unreviewable.
//
// Everything here is pure maths in Draftmaster.Tracks. The bridge that turns a TrackInfoV2 into a Course
// lives in the editor assembly (Assembly-CSharp cannot be referenced from an asmdef), so the last test
// checks its OUTPUT instead: the JSON files on disk.
public class RacingLineTrainerTests
{
    const float Mps2Mph = 2.237f;

    // --- Course fixtures ---------------------------------------------------------------------------------

    struct Piece
    {
        public bool turn;
        public float length;
        public float angleDeg;   // positive = left
        public Piece(bool turn, float length, float angleDeg) { this.turn = turn; this.length = length; this.angleDeg = angleDeg; }
    }

    // Walk a list of straights and arcs, emitting a sample every `step` metres, with a corridor `width` wide
    // centred on the centreline. Same shape of data TrackBuilder hands the real trainer.
    static RacingLineTrainer.Course Circuit(IList<Piece> pieces, float width, float step = 2f)
    {
        var centre = new List<Vector2>();
        var right = new List<Vector2>();

        Vector2 at = Vector2.zero;
        float heading = 0f;

        foreach (var p in pieces)
        {
            for (float t = 0f; t < p.length - 1e-3f; t += step)
            {
                float frac = t / p.length;
                float h = heading + (p.turn ? p.angleDeg * frac : 0f);
                Vector2 pos = at + Advance(p, t, heading);
                float r = h * Mathf.Deg2Rad;
                centre.Add(pos);
                right.Add(new Vector2(Mathf.Sin(r), -Mathf.Cos(r)));
            }
            at += Advance(p, p.length, heading);
            heading += p.turn ? p.angleDeg : 0f;
        }

        int n = centre.Count;
        var course = new RacingLineTrainer.Course
        {
            centre = centre.ToArray(),
            right = right.ToArray(),
            minLateral = new float[n],
            maxLateral = new float[n],
            loop = true
        };
        for (int i = 0; i < n; i++)
        {
            course.minLateral[i] = -width * 0.5f;
            course.maxLateral[i] = width * 0.5f;
        }
        return course;
    }

    // Where you end up `t` metres into a piece, starting from `heading`.
    static Vector2 Advance(Piece p, float t, float headingDeg)
    {
        float h = headingDeg * Mathf.Deg2Rad;
        if (!p.turn || Mathf.Abs(p.angleDeg) < 1e-4f)
            return new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * t;

        float a = p.angleDeg * Mathf.Deg2Rad * (t / p.length);
        float radius = p.length / (p.angleDeg * Mathf.Deg2Rad);
        return new Vector2(radius * (Mathf.Sin(h + a) - Mathf.Sin(h)),
                           -radius * (Mathf.Cos(h + a) - Mathf.Cos(h)));
    }

    static RacingLineTrainer.Course Circle(float radius, float width, float step = 2f)
        => Circuit(new[] { new Piece(true, 2f * Mathf.PI * radius, 360f) }, width, step);

    // Two long straights joined by two hairpins — the shape where a racing line is worth the most time,
    // because there is a straight on the far side of every corner to be dragged onto.
    static RacingLineTrainer.Course Hairpins(float width = 14f)
        => Circuit(new[]
        {
            new Piece(false, 300f, 0f),
            new Piece(true, Mathf.PI * 40f, 180f),
            new Piece(false, 300f, 0f),
            new Piece(true, Mathf.PI * 40f, 180f),
        }, width);

    static float[] Zeros(RacingLineTrainer.Course c) => new float[c.Count];

    static RacingLineTrainer.CarLimits Car()
        => RacingLineTrainer.CarLimits.Simple(lateralAccelMps2: 15f, topSpeedMps: 80f, accel: 6f, decel: 12f);

    // --- The lap simulation ------------------------------------------------------------------------------

    [Test]
    public void Circle_SettlesAtTheGripLimitedSpeed()
    {
        var course = Circle(radius: 100f, width: 12f);
        var limits = Car();
        var tel = RacingLineTrainer.Analyse(course, Zeros(course), limits);

        float expected = Mathf.Sqrt(limits.lateralAccelMps2 * 100f);   // v = sqrt(r a) = 38.7 m/s
        for (int i = 0; i < tel.speedMps.Length; i++)
            Assert.That(tel.speedMps[i], Is.EqualTo(expected).Within(expected * 0.03f),
                $"sample {i} should sit on the cornering limit of a constant-radius circle");
    }

    [Test]
    public void Circle_LapTimeIsCircumferenceOverSpeed()
    {
        var course = Circle(radius: 100f, width: 12f);
        var limits = Car();
        float t = RacingLineTrainer.LapTime(course, Zeros(course), limits);

        float expected = 2f * Mathf.PI * 100f / Mathf.Sqrt(limits.lateralAccelMps2 * 100f);
        Assert.That(t, Is.EqualTo(expected).Within(expected * 0.03f));
    }

    [Test]
    public void Braking_StartsBeforeTheCornerNotInIt()
    {
        var course = Hairpins();
        var limits = Car();
        var tel = RacingLineTrainer.Analyse(course, Zeros(course), limits);

        // The hairpin cannot be taken above sqrt(r*a) at its 40m radius; the straight before it must already
        // be down off top speed by the time the road starts to bend.
        float hairpinV = Mathf.Sqrt(limits.lateralAccelMps2 * 40f);
        int firstTurnSample = Mathf.RoundToInt(300f / 2f);
        Assert.That(tel.speedMps[firstTurnSample], Is.LessThan(hairpinV * 1.15f),
            "car should arrive at the hairpin already slowed, not braking inside it");
        Assert.That(tel.speedMps[firstTurnSample / 2], Is.GreaterThan(hairpinV * 1.5f),
            "the middle of the straight should still be flat out");
    }

    [Test]
    public void SpeedCap_IsHonoured()
    {
        var course = Circuit(new[] { new Piece(false, 400f, 0f), new Piece(true, 200f, 360f) }, 12f);
        course.speedCapMps = new float[course.Count];
        for (int i = 0; i < course.Count; i++) course.speedCapMps[i] = 30f;

        var tel = RacingLineTrainer.Analyse(course, Zeros(course), Car());
        for (int i = 0; i < tel.speedMps.Length; i++)
            Assert.That(tel.speedMps[i], Is.LessThanOrEqualTo(30.01f), $"sample {i} broke the authored cap");
    }

    // TrackBuilder closes a lap by walking back to the start point, so its last sample sits on top of its
    // first. A zero-length step must still tie the two ends together: if it does not, the two halves of the
    // lap stop constraining each other's speed and the car gets to change pace across start/finish for free.
    [Test]
    public void CoincidentSeamSample_DoesNotLeakSpeed()
    {
        var clean = Hairpins();
        float cleanTime = RacingLineTrainer.LapTime(clean, Zeros(clean), Car());

        int n = clean.Count;
        var doubled = new RacingLineTrainer.Course
        {
            centre = new Vector2[n + 1],
            right = new Vector2[n + 1],
            minLateral = new float[n + 1],
            maxLateral = new float[n + 1],
            loop = true
        };
        for (int i = 0; i < n; i++)
        {
            doubled.centre[i] = clean.centre[i];
            doubled.right[i] = clean.right[i];
            doubled.minLateral[i] = clean.minLateral[i];
            doubled.maxLateral[i] = clean.maxLateral[i];
        }
        doubled.centre[n] = clean.centre[0];      // the seam sample, repeated
        doubled.right[n] = clean.right[0];
        doubled.minLateral[n] = clean.minLateral[0];
        doubled.maxLateral[n] = clean.maxLateral[0];

        float doubledTime = RacingLineTrainer.LapTime(doubled, Zeros(doubled), Car());
        Assert.That(doubledTime, Is.EqualTo(cleanTime).Within(cleanTime * 0.01f),
            "a repeated seam sample must not open a hole in the speed limits");
    }

    // --- Training ----------------------------------------------------------------------------------------

    [Test]
    public void Training_FindsRealTimeOnACircuitWithCorners()
    {
        var course = Hairpins();
        var limits = Car();
        var report = RacingLineTrainer.Train(course, Zeros(course), limits,
            RacingLineTrainer.Settings.For(851f));

        Assert.That(report.lapsSimulated, Is.GreaterThan(100), "should have driven the lap many times");
        Assert.That(report.improvements, Is.GreaterThan(0), "should have kept at least one improvement");
        Assert.That(report.trainedLapTime, Is.LessThan(report.seedLapTime * 0.98f),
            $"expected >2% off the centreline lap; got {report.seedLapTime:F2}s -> {report.trainedLapTime:F2}s");
    }

    [Test]
    public void Training_UsesTheRoadThroughTheCorners()
    {
        var course = Hairpins();
        var report = RacingLineTrainer.Train(course, Zeros(course), Car(), RacingLineTrainer.Settings.For(851f));

        float widest = 0f;
        for (int i = 0; i < report.lateral.Length; i++) widest = Mathf.Max(widest, Mathf.Abs(report.lateral[i]));
        Assert.That(widest, Is.GreaterThan(2f),
            "a trained line that never leaves the centreline has not learned anything");
    }

    [Test]
    public void Training_StaysInsideTheCorridor()
    {
        var course = Hairpins(width: 14f);
        var report = RacingLineTrainer.Train(course, Zeros(course), Car(), RacingLineTrainer.Settings.For(851f));

        for (int i = 0; i < report.lateral.Length; i++)
            Assert.That(Mathf.Abs(report.lateral[i]), Is.LessThanOrEqualTo(7f),
                $"sample {i} at {report.lateral[i]:F2}m is off a 14m road");
    }

    [Test]
    public void Training_LeavesALineTheCarCouldSteer()
    {
        var course = Hairpins();
        var report = RacingLineTrainer.Train(course, Zeros(course), Car(), RacingLineTrainer.Settings.For(851f));

        // Samples are 2m apart; a metre of lateral movement per metre travelled would be a 45-degree kink.
        // The trainer only ever moves the line in raised-cosine bumps, so nothing should come close.
        int n = report.lateral.Length;
        for (int i = 0; i < n; i++)
        {
            float step = Mathf.Abs(report.lateral[(i + 1) % n] - report.lateral[i]) / 2f;
            Assert.That(step, Is.LessThan(1f), $"kink at sample {i}: {step:F3} m of lateral per metre travelled");
        }
    }

    [Test]
    public void Training_NeverReturnsSomethingSlowerThanTheSeed()
    {
        // A circle has no line to find — every point is already at the limit. The optimiser must recognise
        // that and hand the seed back untouched rather than wandering off it.
        var course = Circle(radius: 100f, width: 12f);
        var limits = Car();
        var report = RacingLineTrainer.Train(course, Zeros(course), limits, RacingLineTrainer.Settings.For(628f));

        Assert.That(report.trainedLapTime, Is.LessThanOrEqualTo(report.seedLapTime + 1e-3f));
        Assert.That(RacingLineTrainer.LapTime(course, report.lateral, limits),
            Is.LessThanOrEqualTo(report.seedLapTime + 1e-3f));
    }

    [Test]
    public void Training_IsDeterministic()
    {
        var settings = RacingLineTrainer.Settings.For(851f);
        var a = RacingLineTrainer.Train(Hairpins(), Zeros(Hairpins()), Car(), settings);
        var b = RacingLineTrainer.Train(Hairpins(), Zeros(Hairpins()), Car(), settings);

        Assert.That(b.trainedLapTime, Is.EqualTo(a.trainedLapTime).Within(1e-4f));
        Assert.That(b.lateral.Length, Is.EqualTo(a.lateral.Length));
        for (int i = 0; i < a.lateral.Length; i++)
            Assert.That(b.lateral[i], Is.EqualTo(a.lateral[i]).Within(1e-4f), $"sample {i} differs between runs");
    }

    [Test]
    public void Training_RejectsAMismatchedSeed()
    {
        var course = Hairpins();
        var report = RacingLineTrainer.Train(course, new float[4], Car(), RacingLineTrainer.Settings.Default);
        Assert.That(report.trainedLapTime, Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void Settings_ScaleTheirSpanWithTheLap()
    {
        var shortTrack = RacingLineTrainer.Settings.For(858f);     // Bristol
        var superSpeedway = RacingLineTrainer.Settings.For(4281f); // Talladega
        Assert.That(shortTrack.startHalfWidth, Is.LessThan(superSpeedway.startHalfWidth));
        Assert.That(shortTrack.endHalfWidth, Is.LessThan(superSpeedway.endHalfWidth));
    }

    [Test]
    public void Settings_WorkCoarseToFine()
    {
        var s = RacingLineTrainer.Settings.For(3200f);
        Assert.That(s.HalfWidthAt(0f), Is.GreaterThan(s.HalfWidthAt(0.5f)));
        Assert.That(s.HalfWidthAt(0.5f), Is.GreaterThan(s.HalfWidthAt(1f)));
        Assert.That(s.HalfWidthAt(1f), Is.EqualTo(s.endHalfWidth).Within(0.01f));
        Assert.That(s.AmplitudeAt(0f), Is.GreaterThan(s.AmplitudeAt(1f)));
    }

    // --- Storage ----------------------------------------------------------------------------------------

    [Test]
    public void StoredLine_ResamplesAndReadsBack()
    {
        // A ramp from 0 to 10m over a 100m lap, sampled unevenly, must read back as the same ramp.
        var distance = new float[] { 0f, 7f, 23f, 51f, 80f, 100f };
        var lateral = new float[] { 0f, 0.7f, 2.3f, 5.1f, 8.0f, 10f };
        var line = new TrainedRacingLine
        {
            trackLength = 100f,
            spacing = 2f,
            lateral = TrainedRacingLine.Resample(distance, lateral, 100f, 2f, out int count)
        };

        Assert.That(count, Is.EqualTo(50));
        Assert.That(line.LateralAt(0f), Is.EqualTo(0f).Within(0.01f));
        Assert.That(line.LateralAt(40f), Is.EqualTo(4f).Within(0.05f));
        Assert.That(line.LateralAt(80f), Is.EqualTo(8f).Within(0.05f));
        Assert.That(line.LateralAt(140f), Is.EqualTo(line.LateralAt(40f)).Within(0.01f), "should wrap");
    }

    [Test]
    public void StoredLine_RoundTripsThroughItsOwnJson()
    {
        var line = new TrainedRacingLine
        {
            trackId = "Testville",
            vehicle = "Cup24",
            trainedUtc = "2026-09-08 01:23:45",
            trackLength = 1234.5f,
            spacing = 3f,
            seedLapTime = 31.25f,
            trainedLapTime = 30.5f,
            lateralAccelMps2 = 33.9f,
            drivenLength = 1220f,
            lapsSimulated = 2048,
            lateral = new[] { 0f, -1.234f, 4.5678f, -0.0004f, 3f }
        };
        line.RoundForStorage();

        var back = JsonUtility.FromJson<TrainedRacingLine>(line.ToCompactJson());
        Assert.That(back.trackId, Is.EqualTo("Testville"));
        Assert.That(back.vehicle, Is.EqualTo("Cup24"));
        Assert.That(back.trainedUtc, Is.EqualTo("2026-09-08 01:23:45"));
        Assert.That(back.lapsSimulated, Is.EqualTo(2048));
        Assert.That(back.trackLength, Is.EqualTo(1234.5f).Within(0.01f));
        Assert.That(back.seedLapTime, Is.EqualTo(31.25f).Within(0.001f));
        Assert.That(back.lateral.Length, Is.EqualTo(5));
        for (int i = 0; i < 5; i++)
            Assert.That(back.lateral[i], Is.EqualTo(line.lateral[i]).Within(0.001f), $"sample {i}");
    }

    [Test]
    public void StoredLine_RejectsGeometryThatHasMoved()
    {
        var line = new TrainedRacingLine { trackLength = 1000f, spacing = 3f, lateral = new float[334] };
        Assert.IsTrue(line.MatchesLength(1000f));
        Assert.IsTrue(line.MatchesLength(1005f), "a few metres of resampling slop is fine");
        Assert.IsFalse(line.MatchesLength(1200f), "a track that has been regenerated must reject its old line");
        Assert.IsFalse(new TrainedRacingLine().MatchesLength(1000f), "an empty line is never usable");
    }

    // Whatever the editor tool has written must be loadable, in date, and an improvement. This is the only
    // check on the TrackInfoV2 -> Course bridge that an asmdef-scoped test can make, and it is the one that
    // matters: a file the game will read.
    [Test]
    public void EveryTrainedLineOnDiskIsUsable()
    {
        string folder = Path.Combine(Application.dataPath, "Resources/RacingLines");
        if (!Directory.Exists(folder)) Assert.Pass("no trained lines written yet");

        var files = Directory.GetFiles(folder, "*.json");
        if (files.Length == 0) Assert.Pass("no trained lines written yet");

        foreach (var file in files)
        {
            string id = Path.GetFileNameWithoutExtension(file);
            var line = JsonUtility.FromJson<TrainedRacingLine>(File.ReadAllText(file));
            Assert.IsNotNull(line, $"{id}: unparseable");
            Assert.That(line.version, Is.LessThanOrEqualTo(TrainedRacingLine.CurrentVersion), $"{id}: from a newer format");
            Assert.That(line.trackId, Is.EqualTo(id), $"{id}: file name and trackId disagree");
            Assert.That(line.trackLength, Is.GreaterThan(100f), $"{id}: implausible lap length");
            Assert.That(line.spacing, Is.GreaterThan(0.1f), $"{id}: no sample spacing");
            Assert.That(line.lateral, Is.Not.Null.And.Length.GreaterThan(3), $"{id}: no line stored");
            Assert.That(line.lateral.Length, Is.EqualTo(Mathf.Max(4, Mathf.CeilToInt(line.trackLength / line.spacing))),
                $"{id}: sample count does not cover the lap");
            Assert.That(line.trainedLapTime, Is.GreaterThan(1f), $"{id}: implausible lap time");
            Assert.That(line.trainedLapTime, Is.LessThanOrEqualTo(line.seedLapTime + 1e-3f),
                $"{id}: trained line is slower than the seed it started from");

            float widest = 0f, worstStep = 0f;
            for (int i = 0; i < line.lateral.Length; i++)
            {
                widest = Mathf.Max(widest, Mathf.Abs(line.lateral[i]));
                float step = Mathf.Abs(line.lateral[(i + 1) % line.lateral.Length] - line.lateral[i]) / line.spacing;
                worstStep = Mathf.Max(worstStep, step);
            }
            Assert.That(widest, Is.LessThan(40f), $"{id}: {widest:F1}m off the centreline is not a road");
            Assert.That(worstStep, Is.LessThan(1f), $"{id}: kink of {worstStep:F2} m lateral per metre travelled");
        }
    }
}
