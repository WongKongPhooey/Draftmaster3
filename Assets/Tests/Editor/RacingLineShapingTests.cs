using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// TrackInfoV2's ideal-line shaping (BuildRacingLineAnchors + GetLateralAt): a straight puts no point on the ideal
// line, corners closer than racingLineMergeGap run one arc apex to apex, and the ideal is the minimum-curvature line
// through its points, kept between the side lines. The side lines keep every authored point.
// TrackInfoV2 lives in Assembly-CSharp, which a test assembly cannot reference, so it is reached by reflection (as
// PitBoxFirstOffsetTests does).
public class RacingLineShapingTests
{
    static Type Info => Type.GetType("TrackInfoV2, Assembly-CSharp");

    ScriptableObject _info;
    object _anchors;   // built once per track; GetLateralAt rebuilds them when handed null
    Seg[] _segs;

    [TearDown]
    public void TearDown()
    {
        if (_info != null) Object.DestroyImmediate(_info);
        _info = null;
        _anchors = null;
        _segs = null;
    }

    struct Seg
    {
        public bool turn;
        public float length, angle, lead;
        public float entry, apex, exit;              // ideal
        public float lEntry, lApex, lExit;           // leftmost
        public float rEntry, rApex, rExit;           // rightmost
        public bool skipEntry, skipExit, skipApex;
        public float width;                          // 0 = the test road (40 m, wide enough not to clamp)

        public Seg Wide(float w) { var s = this; s.width = w; return s; }

        public Seg Sides(float l0, float l1, float l2, float r0, float r1, float r2)
        {
            var s = this;
            s.lEntry = l0; s.lApex = l1; s.lExit = l2; s.rEntry = r0; s.rApex = r1; s.rExit = r2;
            return s;
        }
    }

    static Seg Straight(float length, float idealApex = 0f, float left = -6f, float right = 6f, bool skipApex = false) =>
        new Seg { length = length, apex = idealApex, skipApex = skipApex }.Sides(left, left, left, right, right, right);

    static Seg Turn(float length, float angle, float lead, float entry, float apex, float exit,
                    bool skipEntry = false, bool skipExit = false, float left = -6f, float right = 6f) =>
        new Seg { turn = true, length = length, angle = angle, lead = lead, entry = entry, apex = apex, exit = exit,
                  skipEntry = skipEntry, skipExit = skipExit }.Sides(left, left, left, right, right, right);

    static void Set(object boxed, string field, object value)
    {
        var f = boxed.GetType().GetField(field);
        Assert.That(f, Is.Not.Null, $"field {field} missing on {boxed.GetType().Name}");
        f.SetValue(boxed, value);
    }

    void Build(params Seg[] segs) => Build(false, segs);

    void Build(bool keepInsideSideLines, params Seg[] segs)
    {
        Assert.That(Info, Is.Not.Null, "TrackInfoV2 not found in Assembly-CSharp");
        var segType = Info.GetNestedType("TrackSegment");
        var lineType = Info.GetNestedType("SegmentRacingLine");
        var kindType = Info.GetNestedType("SegmentType");

        _segs = segs;
        _info = ScriptableObject.CreateInstance(Info);
        var arr = Array.CreateInstance(segType, segs.Length);
        for (int i = 0; i < segs.Length; i++)
        {
            var s = segs[i];
            object line = Activator.CreateInstance(lineType);
            Set(line, "idealEntry", s.entry); Set(line, "idealApex", s.apex); Set(line, "idealExit", s.exit);
            Set(line, "leftEntry", s.lEntry); Set(line, "leftApex", s.lApex); Set(line, "leftExit", s.lExit);
            Set(line, "rightEntry", s.rEntry); Set(line, "rightApex", s.rApex); Set(line, "rightExit", s.rExit);
            Set(line, "skipEntry", s.skipEntry); Set(line, "skipExit", s.skipExit); Set(line, "skipApex", s.skipApex);

            object seg = Activator.CreateInstance(segType);
            Set(seg, "type", Enum.ToObject(kindType, s.turn ? 1 : 0));
            Set(seg, "length", s.length);
            Set(seg, "angle", s.angle);
            Set(seg, "leadIn", s.lead);
            Set(seg, "leadOut", s.lead);
            Set(seg, "width", s.width);
            Set(seg, "racingLine", line);
            arr.SetValue(seg, i);
        }
        Info.GetField("segments").SetValue(_info, arr);
        Info.GetField("closedLoop").SetValue(_info, true);
        Info.GetField("defaultWidth").SetValue(_info, 40f);
        Info.GetField("racingLineKeepIdealInsideSideLines").SetValue(_info, keepInsideSideLines);
        _anchors = Info.GetMethod("BuildRacingLineAnchors").Invoke(_info, null);
    }

    float Lateral(float distance, float lineFactor = 0f) =>
        (float)Info.GetMethod("GetLateralAt").Invoke(_info, new object[] { distance, lineFactor, _anchors, 0f });

    float LapLength() => (float)Info.GetMethod("TotalLength").Invoke(_info, null);

    // The road's curvature (1/m, positive left) at a distance round the lap, and the distance to the nearest
    // segment join.
    float RoadCurvature(float distance, out float toJoin)
    {
        float lap = LapLength();
        distance = ((distance % lap) + lap) % lap;
        float s = 0f;
        toJoin = float.MaxValue;
        float k = 0f;
        foreach (var seg in _segs)
        {
            toJoin = Mathf.Min(toJoin, Mathf.Abs(distance - s));
            if (distance >= s && distance < s + seg.length)
                k = seg.turn ? seg.angle * Mathf.Deg2Rad / seg.length : 0f;
            s += seg.length;
        }
        toJoin = Mathf.Min(toJoin, Mathf.Abs(distance - s));
        return k;
    }

    // A line's curvature in the world (1/m, positive left): the road's less the offset's second derivative.
    float LineCurvature(float distance, float step, out float toJoin, float lineFactor = 0f)
    {
        float n2 = (Lateral(distance - step, lineFactor) - 2f * Lateral(distance, lineFactor)
                    + Lateral(distance + step, lineFactor)) / (step * step);
        return RoadCurvature(distance, out toJoin) - n2;
    }

    // Distances of the points the ideal line is built from.
    float[] IdealPoints()
    {
        var list = (IList)_anchors;
        var result = new System.Collections.Generic.List<float>();
        foreach (var a in list)
        {
            var t = a.GetType();
            if ((bool)t.GetField("noIdeal").GetValue(a)) continue;
            result.Add((float)t.GetField("distance").GetValue(a));
        }
        return result.ToArray();
    }

    void AssertBetweenSideLines(float tolerance)
    {
        float lap = LapLength();
        for (float d = 0f; d < lap; d += 1f)
        {
            float v = Lateral(d), l = Lateral(d, -1f), r = Lateral(d, +1f);
            Assert.That(v, Is.InRange(Mathf.Min(l, r) - tolerance, Mathf.Max(l, r) + tolerance),
                        $"ideal {v:0.00} left the side lines [{l:0.00}, {r:0.00}] at {d} m");
        }
    }

    [Test]
    public void StraightApexNoLongerPullsTheIdealToTheCentre()
    {
        // Two corners exiting / entering wide (+5) round 400 m straights whose apex is the centre (0).
        Build(Turn(200f, 90f, 30f, 5f, -5f, 5f), Straight(400f, 0f, left: -4f),
              Turn(200f, 90f, 30f, 5f, -5f, 5f), Straight(400f, 0f, left: -4f));

        Assert.That(Lateral(400f), Is.GreaterThan(4f), "ideal should stay on the exit side mid-straight");
        Assert.That(Lateral(1000f), Is.GreaterThan(4f));
        // The side lines are built the same way: the straight's own point (-4) does not pull the leftmost line in.
        Assert.That(Lateral(400f, -1f), Is.EqualTo(-6f).Within(0.01f));
    }

    [Test]
    public void ShortStraightRunsOneArcApexToApex()
    {
        // S-bend: left-hander (apex left, -5), 60 m straight, right-hander (apex right, +5).
        Build(Turn(100f, 60f, 40f, 5f, -5f, 5f), Straight(60f),
              Turn(100f, -60f, 40f, -5f, 5f, -5f), Straight(500f));
        // A's apex at 50 m, B's at 210 m.

        foreach (float d in IdealPoints())
            Assert.That(d <= 50.01f || d >= 209.99f, $"ideal point at {d:0.0} m sits between the two apexes");

        Assert.That(Lateral(50f), Is.EqualTo(-5f).Within(0.05f));
        Assert.That(Lateral(210f), Is.EqualTo(5f).Within(0.05f));
    }

    [Test]
    public void LongStraightIsOneSweepAcross()
    {
        // Left-hander exiting +5, 600 m straight, right-hander entering -5. Straight runs 200..800 m; the exit point
        // is at 230 m and the entry at 770 m. Between them the line goes from turning left to turning right once -
        // one inflection, no weave.
        Build(Turn(200f, 90f, 30f, 5f, -5f, 5f), Straight(600f),
              Turn(200f, -90f, 30f, -5f, 5f, -5f), Straight(600f));

        int flips = 0;
        float prev = 0f;
        for (float d = 232f; d <= 770f; d += 4f)
        {
            float k = LineCurvature(d, 2f, out _);
            if (Mathf.Abs(k) < 1e-4f) continue;
            if (prev != 0f && Mathf.Sign(k) != Mathf.Sign(prev)) flips++;
            prev = k;
        }
        Assert.That(flips, Is.LessThanOrEqualTo(1), "the line weaved down the straight");
    }

    [Test]
    public void IdealStaysBetweenTheSideLines()
    {
        // Opted in (racingLineKeepIdealInsideSideLines). Texas with the kink points skipped: the kinks' inside lane
        // is drawn at the ideal's apex (-5), the start/finish straight's at 0.
        Build(true, Straight(215.6f, 5f, left: 0f, right: 6f), Turn(50f, 20f, 90f, 5f, -5f, 5f, skipEntry: true, left: -5f), Straight(160f),
              Turn(630f, 160f, 21.4f, 5f, -5f, 5f), Straight(460f, 5f),
              Turn(630f, 160f, 25.7f, 5f, -5f, 5f), Straight(160f),
              Turn(50f, 20f, 90f, 5f, -5f, 5f, skipExit: true, left: -5f));

        AssertBetweenSideLines(0.15f);
    }

    // Texas (FortWorth) as authored on 2026-11-04, real widths included.
    static Seg[] TexasNow() => new[]
    {
        Straight(215.6f, 0f, -3f, 2f).Wide(15.5f),
        Turn(50f, 20f, 90f, 5f, -5f, 5f, skipEntry: true).Sides(0f, -5f, 0f, 5f, 0f, 5f).Wide(15.5f),
        Straight(160f, 5f, 0f, 5f, skipApex: true).Wide(15.5f),
        Turn(630f, 160f, 21.4f, 5f, -8f, 8f).Sides(0f, -8f, 3f, 5f, 0f, 8f).Wide(33f),
        Straight(460f, 8f, 2f, 8f, skipApex: true).Wide(23f),
        Turn(630f, 160f, 25.7f, 6f, -5f, 3f).Sides(0f, -5f, -2f, 6f, 0f, 3f).Wide(23.5f),
        Straight(160f, 0f, -5f, 3f, skipApex: true).Sides(-2f, -5f, -5f, 3f, 3f, 3f).Wide(16f),
        Turn(50f, 20f, 90f, 5f, -5f, 5f, skipEntry: true, skipExit: true).Sides(0f, -5f, 0f, 5f, 0f, 5f).Wide(15.5f),
    };

    // The road at a distance on TexasNow: half-width less SplineDriver.RoadEdgeMargin (1.4 m), eased across each
    // segment towards the next one's width as TrackBuilder lays it.
    float TexasRoomAt(float distance)
    {
        float lap = LapLength();
        distance = ((distance % lap) + lap) % lap;
        float s = 0f;
        for (int i = 0; i < _segs.Length; i++)
        {
            var seg = _segs[i];
            if (distance < s + seg.length || i == _segs.Length - 1)
            {
                float next = _segs[(i + 1) % _segs.Length].width;
                return Mathf.Lerp(seg.width, next, Mathf.Clamp01((distance - s) / seg.length)) * 0.5f - 1.4f;
            }
            s += seg.length;
        }
        return 0f;
    }

    // The leftmost and rightmost lines are lanes: the outside one at least racingLineMinLaneGap (4 m) outside the
    // inside one all the way round. Built alike from sparse points they used to meet at Turn 3's exit and on the
    // front stretch.
    [Test]
    public void TexasLanesNeverMeet()
    {
        Build(TexasNow());
        float lap = LapLength();
        for (float d = 0f; d < lap; d += 1f)
        {
            float gap = Lateral(d, 1f) - Lateral(d, -1f);
            Assert.That(gap, Is.GreaterThanOrEqualTo(3.85f), $"lanes {gap:0.00} m apart at {d} m");
        }
    }

    // Every line stays on the road.
    [Test]
    public void TexasLinesStayOnTheRoad()
    {
        Build(TexasNow());
        float lap = LapLength();
        foreach (float f in new[] { -1f, 0f, 1f })
            for (float d = 0f; d < lap; d += 1f)
            {
                float v = Lateral(d, f), room = TexasRoomAt(d);
                Assert.That(Mathf.Abs(v), Is.LessThanOrEqualTo(room + 0.15f), $"line {f} at {v:0.00} is off a road {room:0.00} wide each side at {d} m");
            }
    }

    // The leftmost and rightmost lines are built like the ideal: one arc through the tri-oval, straight down the back
    // straight.
    [Test]
    public void TexasSideLinesArcAndRunStraightLikeTheIdeal()
    {
        Build(TexasNow());
        float lap = LapLength();
        foreach (float f in new[] { -1f, 1f })
        {
            float t3Exit = 2145.6f + 25.7f, t1Apex = lap + 240.6f;
            float minCurvature = float.MaxValue, minAt = 0f;
            for (float d = t3Exit; d <= t1Apex; d += 5f)
            {
                float k = LineCurvature(d, 2f, out float toJoin, f);
                if (toJoin < 3f) continue;
                if (k < minCurvature) { minCurvature = k; minAt = d % lap; }
            }
            // A lane is held off the apron by the straight join of its own points (TrackInfoV2.DrawnJoinAt), which has a
            // corner at each drawn point; at Turn 3's exit (inside lane drawn at -2 between -5s) that leaves a reverse
            // bend of about a kilometre's radius (-0.00096 1/m, 2026-11-04). Anything sharper is a weave.
            Assert.That(minCurvature, Is.GreaterThan(-0.0015f),
                        $"line {f} bends back right at {minAt:0} m ({minCurvature:0.00000} 1/m) - not one arc");

            float exit = 1055.6f + 21.4f, entry = 1515.6f - 25.7f;
            for (float d = exit + 3f; d <= entry - 3f; d += 5f)
                Assert.That(Mathf.Abs(LineCurvature(d, 2f, out _, f)), Is.LessThan(1e-4f), $"line {f} bends on the back straight at {d:0} m");
        }
    }

    // Turn 3 is 630 m round; its apex and exit points are 340 m apart, and the pure minimum-curvature line between
    // them cut up to 8 m onto the apron. racingLineKeepIdealOffApron (on by default) holds it to the inside line.
    [Test]
    public void TexasIdealStaysOffTheApron()
    {
        Build(TexasNow());
        float lap = LapLength();
        for (float d = 0f; d < lap; d += 1f)
        {
            float v = Lateral(d), inside = Lateral(d, -1f);
            Assert.That(v, Is.GreaterThanOrEqualTo(inside - 0.15f), $"ideal {v:0.00} is on the apron (inside line {inside:0.00}) at {d} m");
        }
    }

    // Texas's back straight (460 m, over racingLineFlatStraightMin) is driven straight: from Turn 2's exit point
    // (8 m, 21.4 m past the turn) to Turn 3's entry point (6 m, 25.7 m before it) the line is a straight line, and
    // Turn 2's arc does not carry out across it.
    [Test]
    public void TexasBackStraightIsDrivenStraight()
    {
        Build(TexasNow());
        float exit = 1055.6f + 21.4f, entry = 1515.6f - 25.7f;

        Assert.That(Lateral(exit), Is.EqualTo(8f).Within(0.01f));
        Assert.That(Lateral(entry), Is.EqualTo(6f).Within(0.01f));
        Assert.That(Lateral(0.5f * (exit + entry)), Is.EqualTo(7f).Within(0.01f), "not a straight line exit to entry");
        for (float d = exit + 3f; d <= entry - 3f; d += 5f)
            Assert.That(Mathf.Abs(LineCurvature(d, 2f, out _)), Is.LessThan(1e-4f), $"the line bends on the straight at {d:0} m");
        // Turn 2's arc settles onto the straight instead of swinging past it.
        for (float d = 1055.6f - 100f; d <= exit; d += 2f)
            Assert.That(Lateral(d), Is.LessThanOrEqualTo(8.3f), $"Turn 2's exit swings wide of the straight at {d:0} m");
    }

    // Texas as it stands (2026-11-04): Turn 3's exit, then Turn 4's apex and Turn 1's apex are the only ideal
    // points through the tri-oval. The line through them should be one long left-hand arc - turning left the whole
    // way in the world, never straightening into the road's straight-kink-straight or bending back right.
    [Test]
    public void TexasTriOvalIsOneLongArc()
    {
        Build(TexasNow());

        float lap = LapLength();
        float t3Exit = 2145.6f + 25.7f, t1Apex = lap + 240.6f;
        var log = new StringBuilder("Texas tri-oval ideal (distance, lateral, world curvature 1/m):\n");
        float minCurvature = float.MaxValue, minAt = 0f;
        for (float d = t3Exit; d <= t1Apex; d += 5f)
        {
            float k = LineCurvature(d, 2f, out float toJoin);
            log.AppendLine($"  {d % lap,7:0} {Lateral(d),6:0.00} {k,8:0.00000}");
            if (toJoin < 3f) continue;   // the finite difference straddles a curvature step at a segment join
            if (k < minCurvature) { minCurvature = k; minAt = d % lap; }
        }
        Debug.Log(log.ToString());
        Assert.That(minCurvature, Is.GreaterThan(-0.0005f),
                    $"the line bends back right at {minAt:0} m ({minCurvature:0.00000} 1/m) - not one arc");
    }
}
