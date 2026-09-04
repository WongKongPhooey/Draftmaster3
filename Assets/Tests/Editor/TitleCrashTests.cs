using System.Collections.Generic;
using System.Linq;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The title screen's crash tableau can only be *watched* in Play Mode, but everything that makes it either
// work or embarrassing is arithmetic: whether the cars clear the copy column, whether they enter from off
// the screen, whether the clock actually reaches a standstill, and whether the shot is still assembled by
// the time it does. All of that is in TitleCrash, so all of it is checked here.
//
// The scene half is read the way TitleScreenWiringTests reads it — through SerializedObject and type names —
// because this assembly can't reference Assembly-CSharp, where the runtime component lives.
public class TitleCrashTests
{
    const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";

    // The clock the tableau is authored around, read off the choreography rather than restated here.
    static readonly TitleCrash.Tempo Beat = TitleCrash.Tempo.Default;

    // ------------------------------------------------------------------ the clock

    [Test]
    public void TheClockStartsAtZeroAndReachesExactlyOne()
    {
        Assert.AreEqual(0f, Beat.Clock(0f), 1e-5f);
        Assert.AreEqual(1f, Beat.Clock(Beat.RunSeconds), 1e-4f,
                        "The sequence has to be complete at the moment time stops, or the pile freezes half-built.");
        Assert.AreEqual(1f, Beat.Clock(Beat.RunSeconds * 4f), 1e-4f,
                        "The clock runs on past the end — the tableau has to hold, not carry on.");
    }

    [Test]
    public void TheClockOnlyEverRunsForwards()
    {
        float previous = -1f;
        for (int i = 0; i <= 400; i++)
        {
            float u = Beat.Clock(Beat.RunSeconds * i / 400f);
            Assert.GreaterOrEqual(u, previous, "Choreography time went backwards — the cars would rewind.");
            previous = u;
        }
    }

    [Test]
    public void TheRunIsAHalfSecondSlamAndThenAMuchLongerCrawl()
    {
        // The shape of the whole thing: a very fast entry that brakes to almost nothing inside half a second,
        // then a long beat of super slow motion, then a pause. The crawl is the half of the shot there is
        // anything to watch in — the entry is a blur on purpose — so it gets most of the running time.
        Assert.AreEqual(0.5f, Beat.slamSeconds, 0.05f);
        Assert.AreEqual(1.5f, Beat.crawlSeconds, 0.05f);
        Assert.AreEqual(2f, Beat.RunSeconds, 0.1f,
                        "The whole sequence should be over in about two seconds.");
        Assert.Greater(Beat.crawlSeconds, Beat.slamSeconds * 2f,
                       "The slow-motion beat isn't much longer than the slam — the crush and the sparks are " +
                       "the thing worth looking at and they all happen in it.");

        // The slam does nearly all the work, which is the only way the crawl can be a crawl.
        Assert.Greater(Beat.Clock(Beat.slamSeconds), 0.85f,
                       "The cars still have a long way to go when the slow-motion beat starts — it would be a slide, not a crawl.");
        Assert.Less(Beat.Clock(Beat.slamSeconds), 0.99f,
                    "Nothing at all is left for the slow-motion beat — it would be half a second of a still image.");
    }

    [Test]
    public void TheEntryIsViolentAndTheBrakeNeverLetsUp()
    {
        // What the shot needs is cars thrown in far too fast to follow, shedding that speed from the first
        // frame — not a slow constant slide with a brake tacked on the end.
        Assert.Greater(Beat.EntryRate, 20f * Beat.CrawlRate,
                       "The cars enter at nothing like a multiple of the crawl — the slow-motion beat wouldn't read as slow.");
        Assert.Greater(Beat.EntryRate, 4f,
                       "The whole sequence would take longer than a quarter second even at the opening pace — that isn't a slam.");

        // Half of everything is done in the first fifth of a second.
        Assert.Greater(Beat.Clock(0.1f), 0.5f, "The entry isn't fast enough to read as a car being thrown in.");

        float previous = float.MaxValue;
        for (int i = 0; i <= 400; i++)
        {
            float t = Beat.RunSeconds * i / 400f;
            float rate = Beat.Rate(t);
            Assert.LessOrEqual(rate, previous + 1e-3f,
                               $"Choreography time speeds back up around t={t:0.000}s — the slow-down has to be one continuous brake.");
            previous = rate;
        }
    }

    [Test]
    public void TheSlamHandsOverToTheCrawlWithoutAKink()
    {
        // The two beats are joined at the rate, so the seam is invisible: whatever speed the slam decays to
        // is exactly the speed the crawl carries on at.
        Assert.AreEqual(Beat.CrawlRate, Beat.Rate(Beat.slamSeconds - 1e-4f), Beat.CrawlRate * 0.05f,
                        "The slam doesn't decay onto the crawl's speed — there'd be a visible step at the handover.");
        Assert.AreEqual(1f - Beat.Share, Beat.Clock(Beat.slamSeconds), 1e-3f);
    }

    [Test]
    public void TimeCrawlsThroughTheSlowBeatAndThenStopsDead()
    {
        float crawl = Beat.Rate(Beat.slamSeconds + Beat.crawlSeconds * 0.5f);
        Assert.Greater(crawl, 0f, "Nothing moves during the slow-motion beat — it's a freeze, not a crawl.");
        Assert.Less(crawl, Beat.EntryRate * 0.05f,
                    "The 'slow motion' beat is still running at a serious fraction of the entry pace.");

        Assert.AreEqual(0f, Beat.Rate(Beat.RunSeconds), 1e-5f,
                        "The particles ride this rate — anything above zero and the sparks keep burning after the pause.");
        Assert.AreEqual(0f, Beat.Rate(Beat.RunSeconds * 2f), 1e-5f);
    }

    [Test]
    public void ATempoWithNoCrawlStillFinishesTheSequence()
    {
        // The crawl is tunable down to nothing in the inspector; a shot that then never reached u = 1 would
        // freeze half-built.
        var noCrawl = new TitleCrash.Tempo
        {
            slamSeconds = 0.5f, crawlSeconds = 0f, crawlShare = 0.06f, slamDecay = 3f,
        };
        Assert.AreEqual(1f, noCrawl.Clock(noCrawl.RunSeconds), 1e-4f);
        Assert.AreEqual(0f, noCrawl.Rate(noCrawl.RunSeconds), 1e-5f);
    }

    // ------------------------------------------------------------------ the tableau

    // The shot is composed from a seed now, so these are swept rather than asserted against one arrangement.
    // Every constraint the hand-solved shot was built to satisfy has to hold for EVERY shot the composer will
    // ever produce, and the only way to know that is to make a lot of them and look.
    const int Seeds = 120;

    // Composed once and shared. A shot costs real work to put together — the composer draws, then walks the
    // whole tableau to see whether the draw holds up, and throws away the ones that don't — so recomposing
    // the same hundred-odd seeds for every test in the file turns a second of work into several minutes of it.
    static Shot[] _shots;

    static Shot[] Shots()
    {
        if (_shots != null) return _shots;

        _shots = new Shot[Seeds];
        for (int seed = 1; seed <= Seeds; seed++) _shots[seed - 1] = TitleCrashComposer.Compose(seed);
        return _shots;
    }

    // Not an assertion so much as a report: what the composer objected to, tallied over a lot of draws. A
    // composer that rejects nearly everything still produces a title screen — the fallback one, every time —
    // so this is what says which range is too tight to be worth drawing from.
    [Test]
    [Explicit("Diagnostic: reports why draws are being rejected rather than asserting anything.")]
    public void WhyDrawsGetRejected()
    {
        var tally = new Dictionary<string, int>();
        int sound = 0;

        for (int seed = 1; seed <= 400; seed++)
        {
            var shot = TitleCrashComposer.Draw(new System.Random(seed));
            if (TitleCrashComposer.IsSound(shot, out string why)) { sound++; continue; }
            tally.TryGetValue(why, out int n);
            tally[why] = n + 1;
        }

        var report = tally.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Value}x {kv.Key}");
        UnityEngine.Debug.Log($"{sound}/400 first draws sound. Rejections: {string.Join(" | ", report)}");
    }

    [Test]
    public void EverySeedComposesAShotThatHoldsUp()
    {
        // IsSound is the composer's own gate, so this is really asking whether it ever ships a draw it should
        // have thrown away — and whether Compose can be trusted to terminate with something usable at all.
        var shots = Shots();
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var shot = shots[seed - 1];
            Assert.IsTrue(TitleCrashComposer.IsSound(shot), $"Seed {seed} composed a shot that isn't sound.");
            Assert.AreEqual(4, shot.CarCount, $"Seed {seed} didn't put four cars on the screen.");
        }

        // A seed has to be a shot: same number in, same crash out, or nothing above is reproducible and a
        // pinned seed in the inspector would pin nothing.
        for (int seed = 1; seed <= 12; seed++)
            Assert.IsTrue(Same(shots[seed - 1], TitleCrashComposer.Compose(seed)),
                          $"Seed {seed} composes a different shot each time it is asked.");
    }

    [Test]
    public void TheRandomiserActuallyRandomisesRatherThanFallingBackEveryTime()
    {
        // A composer that rejects nearly everything would still pass every constraint above — by handing back
        // the solved shot every time. What that would look like on screen is the thing this replaced.
        var solved = TitleCrashComposer.Solved();
        int fellBack = 0;
        var crashSizes = new HashSet<int>();
        var places = new HashSet<int>();
        var bites = new HashSet<int>();

        foreach (var shot in Shots())
        {
            if (Same(shot, solved)) fellBack++;
            crashSizes.Add(shot.CrashCount);
            places.Add(Mathf.RoundToInt(shot.cars[SliderOf(shot)].endPos.x / 12f));
            bites.Add(Mathf.RoundToInt(shot.bitePx / 3f));
        }

        Assert.Less(fellBack, Seeds / 5,
                    $"{fellBack} of {Seeds} seeds fell back to the solved shot — the composer is rejecting so " +
                    "much that most boots would show the same crash anyway.");

        Assert.IsTrue(crashSizes.Contains(2) && crashSizes.Contains(3) && crashSizes.Contains(4),
                      $"Only {string.Join("/", crashSizes)} cars ever end up in the accident — it is supposed " +
                      "to be sometimes two, sometimes three, sometimes four.");
        Assert.Greater(places.Count, 3, "The crash always happens in the same place across the slot.");
        Assert.Greater(bites.Count, 3, "Every crash is exactly as hard as every other one.");
    }

    // The first car in the shot that had already lost it. A four-car accident has two of them, lying end to
    // end; everything below that only needs one takes the first.
    static int SliderOf(Shot shot)
    {
        for (int i = 0; i < shot.CarCount; i++) if (shot.IsSlider(i)) return i;
        return 0;
    }

    static bool Same(Shot a, Shot b)
    {
        if (a.CarCount != b.CarCount) return false;
        for (int i = 0; i < a.CarCount; i++)
            if (Vector2.Distance(a.cars[i].endPos, b.cars[i].endPos) > 0.01f) return false;
        return true;
    }

    [Test]
    public void EveryCarEntersFromAboveTheTopEdgeHeadingDown()
    {
        foreach (var shot in Shots())
        {
            foreach (var plan in shot.cars)
            {
                var start = TitleCrash.Evaluate(plan, 0f);
                Assert.AreEqual(plan.startPos.x, start.position.x, 0.01f);
                Assert.AreEqual(plan.startPos.y, start.position.y, 0.01f);

                float halfHeight = TitleCrash.HalfSpan(plan.startRotation, horizontal: false);
                Assert.Greater(start.position.y - halfHeight, TitleCrash.CanvasHeight,
                               "A car is already on screen when the sequence opens — it should fly in, not appear.");

                Assert.Less(plan.endPos.y, plan.startPos.y - 100f,
                            "A car doesn't run down the screen — the whole field should be driving in from the top.");
                Assert.Greater(Mathf.Abs(plan.endPos.y - plan.startPos.y),
                               Mathf.Abs(plan.endPos.x - plan.startPos.x),
                               "A car travels further sideways than it does down — that reads as a slide-in, not a drop-in.");
            }
        }
    }

    [Test]
    public void NoCarCrossesTheCopyColumnOnItsWayIn()
    {
        // Two things move a car off the straight line between its poses: the bow across its travel (arcPx)
        // and being shoved by whatever it runs into. So this walks the settled tableau — what is actually
        // drawn — rather than the choreography on its own.
        foreach (var shot in Shots())
        {
            for (int step = 0; step <= 120; step++)
            {
                var poses = TitleCrash.Tableau(shot, step / 120f);
                for (int i = 0; i < poses.Length; i++)
                {
                    if (TitleCrash.OffTheTop(poses[i])) continue;

                    float halfWidth = TitleCrash.HalfSpan(poses[i].rotation, horizontal: true);
                    Assert.GreaterOrEqual(poses[i].position.x - halfWidth, TitleCrash.ColumnRightPx - 0.01f,
                                          "A car crosses the copy column mid-crash — it would wipe across the wordmark.");
                }
            }
        }
    }

    [Test]
    public void EveryCarLandsInTheEmptyHalfOfTheScreen()
    {
        // The authored resting pose is where a car is aimed; where it ends up is that pose after the pile has
        // been pushed apart, so the shot is measured on the settled tableau.
        foreach (var shot in Shots())
        {
            var poses = TitleCrash.Tableau(shot, 1f);

            for (int i = 0; i < shot.CarCount; i++)
            {
                Assert.AreEqual(shot.cars[i].endRotation, poses[i].rotation, 0.01f);
                Assert.AreEqual(1f, poses[i].progress, 1e-4f);

                float halfWidth = TitleCrash.HalfSpan(poses[i].rotation, horizontal: true);
                float halfHeight = TitleCrash.HalfSpan(poses[i].rotation, horizontal: false);

                Assert.GreaterOrEqual(poses[i].position.x - halfWidth, TitleCrash.ColumnRightPx - 0.01f,
                                      "A car finishes over the copy column — it would sit on the wordmark or the menu.");
                Assert.LessOrEqual(poses[i].position.x + halfWidth, TitleCrash.CanvasWidth + 60f,
                                   "A car finishes so far off the right edge that most of it isn't in the shot.");
                Assert.GreaterOrEqual(poses[i].position.y - halfHeight, -0.01f);
                Assert.LessOrEqual(poses[i].position.y + halfHeight, TitleCrash.CanvasHeight + 0.01f);

                // Settling is a nudge that stops the bodies sharing space, not a re-choreography.
                Assert.Less(Vector2.Distance(poses[i].position, shot.cars[i].endPos), 60f,
                            "A car is shoved a long way off the pose it was aimed at — the pile isn't the shot any more.");
            }
        }
    }

    [Test]
    public void EveryCarLandsExactlyAsTheClockStopsAndNotBefore()
    {
        // Landing early isn't just untidy: the clock spends its whole last beat creeping through the last few
        // percent of u, so a car that finished at u = 0.85 would stand perfectly still through the entire
        // slow-motion beat — the one part of the shot anybody can actually watch.
        foreach (var shot in Shots())
        {
            foreach (var plan in shot.cars)
            {
                Assert.Greater(plan.travel, 0f);
                Assert.LessOrEqual(plan.delay + plan.travel, 1f,
                                   "A car is still in the air when time stops — it would freeze halfway across the screen.");
                Assert.AreEqual(1f, plan.delay + plan.travel, 1e-3f,
                                "A car parks before the clock does, so it's a still image through the slow-motion beat.");
                Assert.AreEqual(1f, TitleCrash.Evaluate(plan, 1f).progress, 1e-4f);
                Assert.Less(TitleCrash.Evaluate(plan, 1f - Beat.Share).progress, 1f,
                            "A car has already stopped when the crawl starts — nothing of it is left to watch in slow motion.");
            }
        }
    }

    [Test]
    public void EveryCarDrivesInNoseFirstExceptTheOneThatIsAlreadySliding()
    {
        // The liveries are drawn nose-left, so a sprite at angle r is a car pointing along r + 180. Miss that
        // and the whole field reverses down the screen at speed, which is precisely what it used to do.
        //
        // The exception is the car that gets T-boned, and it is the exception on purpose: it lost it before
        // the shot opened and slides in ACROSS its own line of travel. That is the only reason there is a
        // flank pointing up the road for anything to hit — a field where every car is nose-first has no
        // T-bone in it, only a rear-ender.
        foreach (var shot in Shots())
        {
            for (int i = 0; i < shot.CarCount; i++)
            {
                Vector2 travel = (shot.cars[i].endPos - shot.cars[i].startPos).normalized;
                float alignment = Vector2.Dot(Nose(shot.cars[i].startRotation), travel);

                if (shot.IsSlider(i))
                    Assert.Less(Mathf.Abs(alignment), 0.6f,
                                "The car that's supposed to slide in sideways enters pointing down the road — " +
                                "there's no flank presented to hit, so the crash isn't a T-bone.");
                else
                    Assert.Greater(alignment, 0.9f,
                                   $"Car {i} enters pointing somewhere other than where it's going — it's driving in backwards.");
            }
        }
    }

    [Test]
    public void OnlyTheSlidingCarEndsUpBroadsideToItsLineOfTravel()
    {
        foreach (var shot in Shots())
        {
            for (int i = 0; i < shot.CarCount; i++)
            {
                Vector2 travel = (shot.cars[i].endPos - shot.cars[i].startPos).normalized;
                float alignment = Vector2.Dot(Nose(shot.cars[i].endRotation), travel);

                if (shot.IsSlider(i))
                {
                    // Not merely "off its line" — square across it, or the strikers arrive at a corner rather
                    // than a door and every dent comes out the same shape as every other.
                    Assert.Less(Mathf.Abs(alignment), 0.35f,
                                "The car being T-boned isn't broadside when it's hit — it's a glancing blow, " +
                                "and the whole point of the shot is that the two panels deform differently.");
                    Assert.Greater(Mathf.Abs(shot.cars[i].endRotation - shot.cars[i].startRotation), 30f,
                                   "The sliding car barely rotates on its way in — it reads as parked at an " +
                                   "angle rather than as a car that has lost it and is still coming round.");
                }
                else
                {
                    Assert.Greater(alignment, 0.9f,
                                   $"Car {i} finishes sideways; only the sliding car is supposed to be out of shape.");
                }
            }
        }
    }

    // Which way a car is actually pointing, given the sprite angle it's drawn at.
    static Vector2 Nose(float spriteRotationDeg)
    {
        float rad = (spriteRotationDeg + 180f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    static float Gap(TitleCrash.CarPose a, TitleCrash.CarPose b) => TitleCrash.Gap(a, b);

    [Test]
    public void TheHeroIsTheFrontMostCarAndIsAlwaysInTheAccident()
    {
        foreach (var shot in Shots())
        {
            Assert.That(shot.heroIndex, Is.InRange(0, shot.CarCount - 1));
            Assert.IsTrue(shot.IsInTheCrash(shot.heroIndex),
                          "The player's car is watching the accident rather than being in it.");

            var hero = shot.cars[shot.heroIndex];
            for (int i = 0; i < shot.CarCount; i++)
                if (i != shot.heroIndex)
                    Assert.Greater(hero.depth, shot.cars[i].depth, "The player's car has to draw in front of the pile.");
        }

        // Size is a property of the shot, not of a car: four of the same machine, so nothing in the pile can
        // read as a toy parked next to a truck.
        Assert.Greater(TitleCrash.CarLengthPx, 0f);
        Assert.AreEqual(TitleCrash.CarLengthPx * 0.5f, TitleCrash.CarWidthPx, 1e-4f,
                        "The body has to keep the 64x32 livery's proportions.");
    }

    // ------------------------------------------------------------------ the collisions

    [Test]
    public void OnlyTheCarsInTheAccidentEverTouchAndOnlyByTheirOwnAllowance()
    {
        foreach (var shot in Shots())
        {
            for (int step = 0; step <= 120; step++)
            {
                float u = step / 120f;
                var poses = TitleCrash.Tableau(shot, u);

                for (int a = 0; a < poses.Length; a++)
                {
                    for (int b = a + 1; b < poses.Length; b++)
                    {
                        if (poses[a].progress <= 0f || poses[b].progress <= 0f) continue;
                        if (TitleCrash.OffTheTop(poses[a]) || TitleCrash.OffTheTop(poses[b])) continue;

                        bool through = TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                                          poses[b].position, poses[b].rotation,
                                                          out _, out float depth);

                        // Two cars an impact joins are allowed to bury into each other, and have to be: held
                        // at a hard zero, opaque liveries draw as two rectangles meeting along a line, which
                        // reads as two cars parked together rather than one buried in the other. Anybody
                        // else — cars merely racing past, or two strikers that happen to be near each other —
                        // still gets no allowance at all.
                        float allowed = shot.AllowedBite(a, b, u) + 1f;

                        Assert.IsFalse(through && depth > allowed,
                                       $"Cars {a} and {b} are {depth:0.0}px inside each other at u={u:0.00} " +
                                       $"(allowed {allowed:0.0}) — drawn through each other rather than crashing.");
                    }
                }
            }
        }
    }

    [Test]
    public void TheAccidentFinishesTogetherAndEverybodyElseKeepsWellClear()
    {
        foreach (var shot in Shots())
        {
            var poses = TitleCrash.Tableau(shot, 1f);

            // Measured between the BODIES, not the centres: two cars side by side in adjacent lanes have their
            // centres closer together than two cars nose-to-tail, so centre distance says nothing about whether
            // anything is touching.
            foreach (var hit in shot.impacts)
                Assert.Less(Gap(poses[hit.striker], poses[hit.struck]), 1f,
                            "Two cars in the accident finish apart from each other — nothing happened between them.");

            for (int a = 0; a < poses.Length; a++)
                for (int b = a + 1; b < poses.Length; b++)
                    if (shot.AllowedBite(a, b, 1f) <= 0f)
                        Assert.Greater(Gap(poses[a], poses[b]), 5f,
                                       $"Cars {a} and {b} are on top of each other, and only cars an impact " +
                                       "joins are supposed to be.");
        }
    }

    [Test]
    public void EveryHitIsATBoneSquareIntoTheSlidersDoorInSlowMotion()
    {
        // This is what makes the shot worth staging: one contact where the two panels either side of it have
        // to deform completely differently. A narrow nose driven into a long flat flank gouges deep and
        // local; that same flank pressed back into the nose creases it right across. Let a striker arrive at
        // an angle, or on a corner, and both dents come out the same shape — which is exactly the failure the
        // old crater model had, reintroduced through the choreography instead of through the maths.
        foreach (var shot in Shots())
        {
            int sliders = 0;
            for (int i = 0; i < shot.CarCount; i++) if (shot.IsSlider(i)) sliders++;

            Assert.GreaterOrEqual(sliders, 1, "Nothing in the accident had lost it, so there is no flank to hit.");
            Assert.AreEqual(shot.CrashCount - sliders, shot.impacts.Length,
                            "Every car in the accident that had not already lost it should be hitting one that had.");

            foreach (var hit in shot.impacts)
            {
                Assert.IsTrue(shot.IsSlider(hit.struck),
                              "Something is being hit other than a car that had already lost it.");
                Assert.IsTrue(shot.IsInTheCrash(hit.striker));
                Assert.Greater(hit.severity, 0.6f, "A hit in the shot is a love tap.");
                Assert.That(hit.normal.magnitude, Is.EqualTo(1f).Within(0.05f),
                            "The push direction has to be a unit vector — the sparks spray along it.");

                // Inside the slow-motion beat, or it goes off while time is still a blur.
                Assert.Greater(hit.atU, 1f - Beat.Share,
                               "A hit fires during the slam — the crush would be over before anyone could see it.");
                Assert.Less(hit.atU, 1f - Beat.Share * 0.25f,
                            "A hit fires so late there's no crawl left to watch the bodywork fold through.");

                var poses = TitleCrash.Tableau(shot, hit.atU);
                Assert.Less(Gap(poses[hit.striker], poses[hit.struck]), 5f,
                            "The two cars aren't together when the hit fires — the sparks would go off in mid-air.");

                Vector2 heading = Nose(poses[hit.striker].rotation);
                float rad = poses[hit.struck].rotation * Mathf.Deg2Rad;
                Vector2 flank = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));   // the struck car's long axis

                Assert.Less(Mathf.Abs(Vector2.Dot(heading, flank)), 0.35f,
                            "A striker arrives more than 20 degrees off square to the car it's hitting — " +
                            "that's a sideswipe down the side, not a T-bone into the door.");

                // Where along the struck car the nose lands: 0 is the middle of the door, ±half a car a corner.
                Vector2 noseAt = poses[hit.striker].position + heading * (TitleCrash.CarLengthPx * 0.5f);
                float along = Vector2.Dot(noseAt - poses[hit.struck].position, flank);
                // On the body rather than off the end of it. A car arriving on its own goes into the door;
                // when two arrive abreast the outer one lands on a quarter panel, which is where a second
                // car piling into the same flank SHOULD land — so the limit is the end of the car, not the
                // middle of the door.
                Assert.Less(Mathf.Abs(along), TitleCrash.CarLengthPx * 0.4f,
                            $"The nose lands {along:0}px along the struck car — it's clipping a corner rather " +
                            "than going into the flank, so the deep local gouge never happens.");

                Assert.Greater(Vector2.Dot(hit.normal.normalized,
                                           (poses[hit.struck].position - poses[hit.striker].position).normalized), 0f,
                               "The push points away from the car being hit — the sparks would spray backwards.");
            }
        }
    }

    [Test]
    public void TheFlashFollowsTheCrashInsteadOfMarkingWhereItStarted()
    {
        // The cars are still closing all the way to the freeze, so a flash fired once at the point they first
        // touched ends up hanging in clear air a good way behind them. The contact point is found off the
        // poses every frame instead, and has to stay ON the two cars the whole time it is being used.
        foreach (var shot in Shots())
        {
            foreach (var hit in shot.impacts)
            {
                Vector2 atContact = TitleCrash.ContactPointPx(shot, hit, TitleCrash.Tableau(shot, hit.atU), hit.atU);
                Vector2 atFreeze = TitleCrash.ContactPointPx(shot, hit, TitleCrash.Tableau(shot, 1f), 1f);

                Assert.Greater(Vector2.Distance(atContact, atFreeze), 8f,
                               "The contact point doesn't move between the hit and the freeze, but the cars do — " +
                               "so the sparks are being left behind by the crash that threw them.");

                // Wherever it is, it has to be on BOTH cars: that is what makes it the contact rather than a
                // point that happens to drift along near it. A few pixels of slack, because at the moment of
                // contact the two bodies are only just touching and any single point can at best be on the
                // boundary of both.
                for (int step = 0; step <= 24; step++)
                {
                    float u = Mathf.Lerp(hit.atU, 1f, step / 24f);
                    var poses = TitleCrash.Tableau(shot, u);
                    Vector2 where = TitleCrash.ContactPointPx(shot, hit, poses, u);

                    Assert.GreaterOrEqual(where.x, TitleCrash.ColumnRightPx,
                                          $"The flash crosses the copy column at u={u:0.000}.");
                    foreach (int car in new[] { hit.striker, hit.struck })
                        Assert.Less(Inside(poses[car], where), 8f,
                                    $"At u={u:0.000} the contact point is outside car {car} — the sparks are " +
                                    "coming off somewhere the two of them aren't touching.");
                }
            }
        }
    }

    // How far outside a car's body a point is, in reference px. Zero or less means it is within the bodywork.
    static float Inside(TitleCrash.CarPose pose, Vector2 point)
    {
        float rad = pose.rotation * Mathf.Deg2Rad;
        Vector2 along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 across = new Vector2(-along.y, along.x);
        Vector2 d = point - pose.position;

        return Mathf.Max(Mathf.Abs(Vector2.Dot(d, along)) - TitleCrash.CarLengthPx * 0.5f,
                         Mathf.Abs(Vector2.Dot(d, across)) - TitleCrash.CarWidthPx * 0.5f);
    }

    [Test]
    public void TheCrushDevelopsAcrossTheWholeSlowMotionBeatRatherThanInOneFrame()
    {
        // A dent that appears fully formed between two frames is a decal. The bodywork model can be pressed
        // deeper a slice at a time, so the fold is spent across the crawl and the last thing the tableau does
        // before it freezes is give way.
        foreach (var shot in Shots())
        {
            foreach (var hit in shot.impacts)
            {
                Assert.AreEqual(1f, hit.throughU, 1e-4f,
                                "The crush finishes before the clock does, so the shot has a dead beat on the end of it.");
                Assert.Greater(hit.throughU - hit.atU, Beat.Share * 0.2f,
                               "The fold is spent over a blink — it may as well be the single-frame stamp it replaced.");

                Assert.AreEqual(0f, TitleCrash.Crush(hit, hit.atU), 1e-4f, "The bodywork is already dented on contact.");
                Assert.AreEqual(1f, TitleCrash.Crush(hit, hit.throughU), 1e-4f, "The fold never reaches full depth.");
                Assert.AreEqual(0f, TitleCrash.Crush(hit, hit.atU - 0.05f), 1e-4f, "The car dents before it is hit.");
                Assert.AreEqual(1f, TitleCrash.Crush(hit, 2f), 1e-4f, "The crush runs past the end of the clock.");

                // Monotonic, and front-loaded: metal collapses first and resists after, so half the depth is
                // gone well before half the time is.
                float previous = -1f;
                for (int step = 0; step <= 40; step++)
                {
                    float crush = TitleCrash.Crush(hit, Mathf.Lerp(hit.atU, hit.throughU, step / 40f));
                    Assert.GreaterOrEqual(crush, previous, "The fold un-dents partway through the crush.");
                    previous = crush;
                }
                Assert.Greater(TitleCrash.Crush(hit, Mathf.Lerp(hit.atU, hit.throughU, 0.5f)), 0.6f,
                               "The crush is linear or back-loaded — a real one collapses hard and then resists.");
            }
        }
    }

    [Test]
    public void TheStrikersKeepDrivingInAndActuallyBuryThemselves()
    {
        // The complaint this exists to catch: at a hard zero separation, two opaque liveries draw as two
        // rectangles meeting along a line. Nothing about that reads as contact — it reads as two cars parked
        // very close together. Cars in a wreck share space, because the metal between them has folded and the
        // sprite outline is no longer where the bodywork is.
        //
        // And the burial has to be earned rather than allowed: a striker authored to finish exactly on the
        // allowance would sink in and stop, with nothing left over for Settle to shunt the struck car with.
        foreach (var shot in Shots())
        {
            Assert.Greater(shot.bitePx, TitleCrash.CarLengthPx * 0.1f,
                           "The allowance is too small to see — the contact will still draw as a seam.");
            Assert.Less(shot.bitePx, TitleCrash.CarLengthPx * 0.3f,
                        "The cars swallow each other rather than crumpling.");

            var atFreeze = TitleCrash.Tableau(shot, 1f);

            foreach (var hit in shot.impacts)
            {
                Assert.AreEqual(0f, shot.AllowedBite(hit.striker, hit.struck, hit.atU), 1e-3f,
                                "The cars are already inside each other on contact.");
                Assert.AreEqual(shot.bitePx, shot.AllowedBite(hit.striker, hit.struck, 1f), 1e-3f,
                                "The burial never reaches full depth.");

                Assert.IsTrue(TitleCrash.Overlap(atFreeze[hit.striker].position, atFreeze[hit.striker].rotation,
                                                 atFreeze[hit.struck].position, atFreeze[hit.struck].rotation,
                                                 out _, out float buried),
                              "A pair in the accident finish merely touching — the hit doesn't read as a hit.");
                // Not the whole allowance, because a slider with two cars in it is being pressed from two
                // directions at once and Settle cannot satisfy both of them fully — it has to put the car
                // somewhere, and where it puts it is a compromise between its two impacts. Well over half
                // the allowance is still a nose visibly buried in a door.
                Assert.Greater(buried, shot.bitePx * 0.6f,
                               $"A striker only ends up {buried:0.0}px into the car it hit, well short of the " +
                               $"{shot.bitePx:0}px allowance — Settle is pushing them apart again.");

                // Raw choreography, before Settle gets anywhere near it: the striker really is still coming,
                // and by more than the allowance, or there would be nothing left over to shove with.
                var strikerEnd = TitleCrash.Evaluate(shot.cars[hit.striker], 1f);
                var struckEnd = TitleCrash.Evaluate(shot.cars[hit.struck], 1f);
                Assert.IsTrue(TitleCrash.Overlap(strikerEnd.position, strikerEnd.rotation,
                                                 struckEnd.position, struckEnd.rotation, out _, out float drive),
                              "A striker's authored finish stops short of the car it hit, so there's no shunt to resolve.");
                Assert.Greater(drive, shot.bitePx,
                               "A striker drives in by less than the bite allowance, so Settle never pushes and " +
                               "the struck car is never shunted.");
            }
        }
    }

    [Test]
    public void TheSliderIsAlwaysThereToBeCaughtRatherThanToKeepUp()
    {
        // Everything in the field lands at u = 1, so how hard one car arrives at another is decided entirely
        // by how much further it had to come. The slider is slow on purpose: it is the difference between the
        // two speeds that is the severity of the hit, and a slider running away from the strikers would turn
        // every T-bone into a tap however hard the impact plan claims it is.
        foreach (var shot in Shots())
        {
            foreach (var hit in shot.impacts)
            {
                float sliderRun = (shot.cars[hit.struck].endPos - shot.cars[hit.struck].startPos).magnitude;
                float strikerRun = (shot.cars[hit.striker].endPos - shot.cars[hit.striker].startPos).magnitude;
                Assert.Greater(strikerRun / sliderRun, 1.6f,
                               $"A striker covers {strikerRun:0}px to the slider's {sliderRun:0} — it barely " +
                               "catches it, so there's no closing speed in the hit.");
            }
        }
    }

    [Test]
    public void ContactsAreReportedWhereTheCarsActuallyMeet()
    {
        var contacts = new List<TitleCrash.Contact>();

        foreach (var shot in Shots())
        {
            for (int step = 0; step <= 60; step++)
            {
                var poses = TitleCrash.Tableau(shot, step / 60f, contacts);

                foreach (var hit in contacts)
                {
                    Assert.Greater(hit.depthPx, 0f, "A contact was reported between cars that weren't touching.");
                    Assert.That(hit.normal.magnitude, Is.EqualTo(1f).Within(1e-3f),
                                "The push direction has to be a unit vector — the separation is scaled by it.");

                    // The flash goes where the bodies met, so it has to be between the two of them and on screen.
                    float span = Vector2.Distance(poses[hit.a].position, poses[hit.b].position) * 0.5f + 1f;
                    Assert.LessOrEqual(Vector2.Distance(hit.pointPx, poses[hit.a].position), span + TitleCrash.CarLengthPx);
                    Assert.GreaterOrEqual(hit.pointPx.x, TitleCrash.ColumnRightPx - TitleCrash.CarLengthPx,
                                          "A hit is reported over the copy column.");
                }
            }
        }
    }

    // ------------------------------------------------------------------ the hits

    [Test]
    public void TheSmokeHangsOverTheArtSlotBeforeAnythingHasBeenHit()
    {
        // Once the pile connects the plume moves to what was actually hit; this is only where it sits until
        // then, so it still has to be inside the shot.
        Assert.GreaterOrEqual(TitleCrash.PileCentrePx.x, TitleCrash.ColumnRightPx);
        Assert.Less(TitleCrash.PileCentrePx.x, TitleCrash.CanvasWidth);
        Assert.That(TitleCrash.PileCentrePx.y, Is.InRange(0f, TitleCrash.CanvasHeight));
    }

    // ------------------------------------------------------------------ the scene

    [Test]
    public void TheSceneIsNotQuietlyOverridingTheAuthoredTempoOrBodywork()
    {
        // TitleCrashScene's tuning fields are public, so the scene carries its OWN serialized copy of every
        // one of them — and that copy is whatever the default happened to be on the day the component was
        // added. Change a default in code afterwards and the title screen carries on using the old value,
        // silently, with no error and nothing to notice. That has already happened once (maxDent was left
        // behind at a stale value while the code default moved), so it is checked rather than remembered.
        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
        try
        {
            var crash = Component(scene, "TitleCrash", "TitleCrashScene");
            Assert.IsNotNull(crash, "The title screen has no TitleCrashScene.");
            var so = new SerializedObject(crash);

            foreach (var (field, expected) in new[]
                     {
                         ("slamSeconds", Beat.slamSeconds),
                         ("crawlSeconds", Beat.crawlSeconds),
                         ("crawlShare", Beat.crawlShare),
                         ("slamDecay", Beat.slamDecay),
                     })
            {
                Assert.AreEqual(expected, so.FindProperty(field).floatValue, 1e-4f,
                                $"TitleScreen.unity has {field} = {so.FindProperty(field).floatValue} baked " +
                                $"into it, but the choreography is authored around {expected}. The scene wins, " +
                                "so the shot on screen is not the one the tests are checking.");
            }
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void TheTitleSceneCarriesTheCrashAndHasPutThePlaceholderArtAway()
    {
        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
        try
        {
            var crash = Component(scene, "TitleCrash", "TitleCrashScene");
            Assert.IsNotNull(crash, "The title screen has no TitleCrashScene — the art half is empty again.");

            var so = new SerializedObject(crash);
            Assert.IsNotNull(so.FindProperty("layoutCanvas").objectReferenceValue,
                             "TitleCrashScene has no layout canvas, so the tableau can't line up with the column.");
            Assert.IsNotEmpty(so.FindProperty("carsetPrefix").stringValue,
                              "Without a carset there are no liveries to build cars out of.");
            Assert.Less(so.FindProperty("baseSortingOrder").intValue, 0,
                        "The crash has to sort below the title canvas or it draws over the wordmark.");

            var art = Find(scene, "TitleArt");
            Assert.IsNotNull(art, "The art slot itself should stay in the scene.");
            var hatch = art.GetComponent<UnityEngine.UI.Image>();
            Assert.IsFalse(hatch != null && hatch.enabled,
                           "The placeholder hatch is still drawing — it would cover the crash.");

            var note = Find(scene, "ArtNote");
            Assert.IsFalse(note != null && note.activeSelf,
                           "The '[ title art ]' note is still on top of the art that replaced it.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ------------------------------------------------------------------ helpers

    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }

    static Component Component(Scene scene, string objectName, string typeName)
    {
        var go = Find(scene, objectName);
        if (go == null) return null;
        foreach (var component in go.GetComponents<Component>())
            if (component != null && component.GetType().Name == typeName) return component;
        return null;
    }
}
