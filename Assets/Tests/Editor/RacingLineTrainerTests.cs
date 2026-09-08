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

    // The whole point of a second session: hand the trainer the line it found last time and it goes out and
    // finds more. It must never come back with less — practice cannot make the AI slower — and over a few
    // sessions it has to actually beat what one session alone managed, or "train it again" is a no-op button.
    [Test]
    public void Practice_KeepsFindingTimeSessionAfterSession()
    {
        var course = Hairpins();
        var limits = Car();

        var first = RacingLineTrainer.Train(course, Zeros(course), limits, RacingLineTrainer.Settings.For(851f, 0));
        float best = first.trainedLapTime;
        float[] line = first.lateral;

        for (int session = 1; session <= 3; session++)
        {
            var next = RacingLineTrainer.Train(course, line, limits, RacingLineTrainer.Settings.For(851f, session));
            Assert.That(next.trainedLapTime, Is.LessThanOrEqualTo(best + 1e-4f),
                $"session {session} came back slower than the line it started from");
            best = next.trainedLapTime;
            line = next.lateral;
        }

        Assert.That(best, Is.LessThan(first.trainedLapTime),
            $"three more sessions found nothing at all ({first.trainedLapTime:F3}s -> {best:F3}s)");
        Assert.That(best, Is.LessThan(first.seedLapTime * 0.97f), "cumulative gain over the seed should be real");
    }

    // A later session skips the coarse end of the anneal — the corner shapes were found on session one, and
    // spending session five rediscovering them is spending it on nothing.
    [Test]
    public void Practice_LaterSessionsStartFinerButNeverGoAllTheWayToPolishing()
    {
        Assert.That(RacingLineTrainer.Settings.For(3200f, 0).StartT01, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(RacingLineTrainer.Settings.For(3200f, 2).StartT01,
            Is.GreaterThan(RacingLineTrainer.Settings.For(3200f, 1).StartT01));
        Assert.That(RacingLineTrainer.Settings.For(3200f, 40).StartT01, Is.LessThanOrEqualTo(0.6f),
            "however many sessions it has had, a line still gets a mid-scale sweep");
    }

    [Test]
    public void Practice_IsStillDeterministicPerSession()
    {
        var settings = RacingLineTrainer.Settings.For(851f, 3);
        var a = RacingLineTrainer.Train(Hairpins(), Zeros(Hairpins()), Car(), settings);
        var b = RacingLineTrainer.Train(Hairpins(), Zeros(Hairpins()), Car(), settings);

        Assert.That(b.trainedLapTime, Is.EqualTo(a.trainedLapTime).Within(1e-4f));
        for (int i = 0; i < a.lateral.Length; i++)
            Assert.That(b.lateral[i], Is.EqualTo(a.lateral[i]).Within(1e-4f), $"sample {i} differs between runs");
    }

    // Where the apex sits is the difference between a geometric arc and a driver's line, and a symmetric
    // bump cannot move one. A hairpin trained properly should not come out symmetric about its own middle.
    [Test]
    public void Training_MovesTheApexRatherThanRoundingTheCornerOffEvenly()
    {
        var course = Hairpins();
        var report = RacingLineTrainer.Train(course, Zeros(course), Car(), RacingLineTrainer.Settings.For(851f));

        // The first hairpin runs from 300m to 300m + pi*40m, at 2m per sample.
        int start = Mathf.RoundToInt(300f / 2f);
        int end = start + Mathf.RoundToInt(Mathf.PI * 40f / 2f);

        // A geometric arc is a mirror image about the middle of the corner. Anything a driver would call a
        // line is not: entry is given away to straighten the exit, so the two halves do not match.
        float asymmetry = 0f;
        int pairs = 0;
        for (int k = 0; k <= (end - start) / 2; k++, pairs++)
            asymmetry += Mathf.Abs(report.lateral[start + k] - report.lateral[end - k]);
        asymmetry /= Mathf.Max(1, pairs);

        // Reported for interest: the corner is a left-hander, so the inside is negative lateral.
        int apex = start;
        for (int i = start; i < end; i++) if (report.lateral[i] < report.lateral[apex]) apex = i;
        float through = (apex - start) / (float)(end - start);

        Assert.That(asymmetry, Is.GreaterThan(0.25f),
            $"trained hairpin is symmetric to within {asymmetry:F2}m — that is still a geometric arc " +
            $"(apex {through:P0} through the corner)");
    }

    // The line the game drives is the line as STORED — resampled onto an even grid and read back with a lerp.
    // That round trip is not free: it smooths the apexes, and curvature is a second derivative, so it feels
    // the smoothing far more than the positions do. The tool therefore measures the line it is about to
    // write, and picks between the trained line and the one the session started from on that basis.
    [Test]
    public void Storage_PickFasterNeverHandsBackTheSlowerOfTheTwo()
    {
        var course = Hairpins();
        var limits = Car();
        var report = RacingLineTrainer.Train(course, Zeros(course), limits, RacingLineTrainer.Settings.For(851f));

        var centreline = Zeros(course);
        Assert.That(RacingLineTrainer.PickFaster(course, limits, report.lateral, centreline),
            Is.SameAs(report.lateral), "the trained line is the quicker one here");
        Assert.That(RacingLineTrainer.PickFaster(course, limits, centreline, report.lateral),
            Is.SameAs(report.lateral), "...whichever order it is asked in");
        Assert.That(RacingLineTrainer.PickFaster(course, limits, null, centreline), Is.SameAs(centreline));
        Assert.That(RacingLineTrainer.PickFaster(course, limits, centreline, null), Is.SameAs(centreline));
    }

    // On a track sampled at about the storage spacing — which is every generated track — the round trip is
    // near enough free, and this pins that. It is deliberately NOT a test that the round trip is free at any
    // spacing: it is not, and no interpolant makes it so. Resampling error is a ripple one grid step long,
    // and the three-point curvature estimate works over a window shorter than that step, so it reads the
    // ripple as corners. Sample a track four times finer than the grid and the same lap comes back seconds
    // slower off a few centimetres of difference. That is why the editor tool measures the line it is about
    // to write rather than the line it drove. See Docs/Tracks.md, "What the storage grid costs".
    [Test]
    public void Storage_RoundTripIsNearlyFreeWhenTheGridMatchesTheSampling()
    {
        var course = Hairpins();                  // sampled every 2m, like a generated track
        var limits = Car();
        var report = RacingLineTrainer.Train(course, Zeros(course), limits, RacingLineTrainer.Settings.For(851f));

        // Real arc length along the centreline, the way TrackBuilder hands it over. Assuming an even step
        // instead drifts at every segment join, and reading a line back at a distance it was not written at
        // is a step in the lateral profile — which the curvature estimate then reads as a corner.
        const float spacing = 2f;
        var distance = new float[course.Count + 1];
        var lateral = new float[course.Count + 1];
        float cum = 0f;
        for (int i = 0; i < course.Count; i++)
        {
            if (i > 0) cum += Vector2.Distance(course.centre[i - 1], course.centre[i]);
            distance[i] = cum;
            lateral[i] = report.lateral[i];
        }
        float loop = cum + Vector2.Distance(course.centre[course.Count - 1], course.centre[0]);
        distance[course.Count] = loop;
        lateral[course.Count] = report.lateral[0];

        var stored = new TrainedRacingLine
        {
            trackLength = loop,
            spacing = spacing,
            lateral = TrainedRacingLine.Resample(distance, lateral, loop, spacing, out _)
        };
        stored.RoundForStorage();

        var readBack = new float[course.Count];
        for (int i = 0; i < course.Count; i++) readBack[i] = stored.LateralAt(distance[i]);
        RacingLineTrainer.ClampToCorridor(course, readBack, 0f);

        float asStored = RacingLineTrainer.LapTime(course, readBack, limits);
        float found = report.seedLapTime - report.trainedLapTime;
        float kept = report.seedLapTime - asStored;

        float worst = 0f;
        for (int i = 0; i < course.Count; i++)
            worst = Mathf.Max(worst, Mathf.Abs(readBack[i] - report.lateral[i]));

        Assert.That(worst, Is.LessThan(0.05f), "the stored line should be the trained line, to a few cm");
        Assert.That(kept, Is.GreaterThan(found * 0.85f),
            $"the storage round trip gave back {(found - kept):F3}s of the {found:F3}s training found " +
            $"(seed {report.seedLapTime:F3}s, trained {report.trainedLapTime:F3}s, as stored {asStored:F3}s, " +
            $"worst lateral difference {worst:F4}m over {course.Count} samples / {stored.lateral.Length} nodes)");
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
            trainerVersion = TrainedRacingLine.CurrentTrainerVersion,
            trackLength = 1234.5f,
            spacing = 3f,
            baselineLapTime = 32.75f,
            seedLapTime = 31.25f,
            trainedLapTime = 30.5f,
            lateralAccelMps2 = 33.9f,
            drivenLength = 1220f,
            lapsSimulated = 2048,
            totalLapsSimulated = 9001,
            refinePasses = 4,
            lateral = new[] { 0f, -1.234f, 4.5678f, -0.0004f, 3f }
        };
        line.RoundForStorage();

        var back = JsonUtility.FromJson<TrainedRacingLine>(line.ToCompactJson());
        Assert.That(back.trackId, Is.EqualTo("Testville"));
        Assert.That(back.vehicle, Is.EqualTo("Cup24"));
        Assert.That(back.trainedUtc, Is.EqualTo("2026-09-08 01:23:45"));
        Assert.That(back.lapsSimulated, Is.EqualTo(2048));
        Assert.That(back.totalLapsSimulated, Is.EqualTo(9001));
        Assert.That(back.refinePasses, Is.EqualTo(4));
        Assert.That(back.trainerVersion, Is.EqualTo(TrainedRacingLine.CurrentTrainerVersion));
        Assert.That(back.baselineLapTime, Is.EqualTo(32.75f).Within(0.001f));
        // The gain the report quotes is measured from the AUTHORED line, not from whatever the last session
        // happened to start on — otherwise every refine would appear to have thrown away the earlier work.
        Assert.That(back.GainSeconds, Is.EqualTo(2.25f).Within(0.001f));
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

    // A line found by an older, weaker optimiser is still perfectly drivable — the game keeps using it — it
    // just wants another session. That is a different question from "does it fit the road", so the batch
    // tool asks it separately.
    [Test]
    public void StoredLine_KnowsWhenItsTrainerIsOutOfDate()
    {
        var line = new TrainedRacingLine { trackLength = 1000f, spacing = 3f, lateral = new float[334] };

        line.trainerVersion = 0;
        Assert.IsTrue(line.MatchesLength(1000f), "an older trainer's line is still usable in game");
        Assert.IsFalse(line.IsCurrent(1000f), "...but the batch tool must know to run it again");

        line.trainerVersion = TrainedRacingLine.CurrentTrainerVersion;
        Assert.IsTrue(line.IsCurrent(1000f));
        Assert.IsFalse(line.IsCurrent(1200f), "up to date is no use if the road moved");
    }

    // Old files predate baselineLapTime; they must not start reporting a gain of "everything".
    [Test]
    public void StoredLine_FallsBackToItsSeedWhenThereIsNoBaseline()
    {
        var line = new TrainedRacingLine { seedLapTime = 40f, trainedLapTime = 38f };
        Assert.That(line.Baseline, Is.EqualTo(40f).Within(1e-3f));
        Assert.That(line.GainSeconds, Is.EqualTo(2f).Within(1e-3f));
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
            Assert.That(line.trainedLapTime, Is.LessThanOrEqualTo(line.Baseline + 1e-3f),
                $"{id}: practice has left the AI slower than the authored line they used to drive");
            Assert.That(line.refinePasses, Is.GreaterThanOrEqualTo(0), $"{id}: negative session count");

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
