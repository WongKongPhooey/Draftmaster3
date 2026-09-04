using System.Collections.Generic;
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
    public void TheRunIsHalfASecondOfSlamAndHalfASecondOfCrawl()
    {
        // The shape of the whole thing, in the terms it was asked for: a very fast entry that brakes to
        // almost nothing inside half a second, then half a second of super slow motion, then a pause.
        Assert.AreEqual(0.5f, Beat.slamSeconds, 0.05f);
        Assert.AreEqual(0.5f, Beat.crawlSeconds, 0.05f);
        Assert.AreEqual(1f, Beat.RunSeconds, 0.1f,
                        "The whole sequence should be over in about a second.");

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

    [Test]
    public void EveryCarEntersFromAboveTheTopEdgeHeadingDown()
    {
        foreach (var plan in TitleCrash.Field())
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

    [Test]
    public void NoCarCrossesTheCopyColumnOnItsWayIn()
    {
        // Two things move a car off the straight line between its poses: the bow across its travel (arcPx)
        // and being shoved by whatever it runs into. So this walks the settled tableau — what is actually
        // drawn — rather than the choreography on its own.
        var field = TitleCrash.Field();
        for (int step = 0; step <= 200; step++)
        {
            var poses = TitleCrash.Tableau(field, step / 200f);
            for (int i = 0; i < poses.Length; i++)
            {
                float halfHeight = TitleCrash.HalfSpan(poses[i].rotation, horizontal: false);
                if (poses[i].position.y - halfHeight > TitleCrash.CanvasHeight) continue;   // still off the top

                float halfWidth = TitleCrash.HalfSpan(poses[i].rotation, horizontal: true);
                Assert.GreaterOrEqual(poses[i].position.x - halfWidth, TitleCrash.ColumnRightPx - 0.01f,
                                      "A car crosses the copy column mid-crash — it would wipe across the wordmark.");
            }
        }
    }

    [Test]
    public void EveryCarLandsInTheEmptyHalfOfTheScreen()
    {
        // The authored resting pose is where a car is aimed; where it ends up is that pose after the pile has
        // been pushed apart, so the shot is measured on the settled tableau.
        var field = TitleCrash.Field();
        var poses = TitleCrash.Tableau(field, 1f);

        for (int i = 0; i < field.Length; i++)
        {
            Assert.AreEqual(field[i].endRotation, poses[i].rotation, 0.01f);
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
            Assert.Less(Vector2.Distance(poses[i].position, field[i].endPos), 60f,
                        "A car is shoved a long way off the pose it was aimed at — the pile isn't the shot any more.");
        }
    }

    [Test]
    public void EveryCarLandsExactlyAsTheClockStopsAndNotBefore()
    {
        // Landing early isn't just untidy: the clock spends its whole last half-second creeping through the
        // last few percent of u, so a car that finished at u = 0.85 would stand perfectly still through the
        // entire slow-motion beat — the one part of the shot anybody can actually watch.
        foreach (var plan in TitleCrash.Field())
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
        var field = TitleCrash.Field();
        for (int i = 0; i < field.Length; i++)
        {
            Vector2 travel = (field[i].endPos - field[i].startPos).normalized;
            float alignment = Vector2.Dot(Nose(field[i].startRotation), travel);

            if (i == TitleCrash.TurnedIndex)
                Assert.Less(Mathf.Abs(alignment), 0.6f,
                            "The car that's supposed to slide in sideways enters pointing down the road — " +
                            "there's no flank presented to hit, so the crash isn't a T-bone.");
            else
                Assert.Greater(alignment, 0.9f,
                               $"Car {i} enters pointing somewhere other than where it's going — it's driving in backwards.");
        }
    }

    [Test]
    public void OnlyTheSlidingCarEndsUpBroadsideToItsLineOfTravel()
    {
        var field = TitleCrash.Field();
        for (int i = 0; i < field.Length; i++)
        {
            Vector2 travel = (field[i].endPos - field[i].startPos).normalized;
            float alignment = Vector2.Dot(Nose(field[i].endRotation), travel);

            if (i == TitleCrash.TurnedIndex)
                // Not merely "off its line" — square across it, or the hero arrives at a corner rather than
                // a door and the two dents come out the same shape as each other.
                Assert.Less(Mathf.Abs(alignment), 0.35f,
                            "The car being T-boned isn't broadside when it's hit — it's a glancing blow, " +
                            "and the whole point of the shot is that the two panels deform differently.");
            else
                Assert.Greater(alignment, 0.9f,
                               $"Car {i} finishes sideways; only the sliding car is supposed to be out of shape.");
        }

        Assert.Greater(Mathf.Abs(field[TitleCrash.TurnedIndex].endRotation -
                                 field[TitleCrash.TurnedIndex].startRotation), 30f,
                       "The sliding car barely rotates on its way in — it reads as parked at an angle rather " +
                       "than as a car that has lost it and is still coming round.");
    }

    // Which way a car is actually pointing, given the sprite angle it's drawn at.
    static Vector2 Nose(float spriteRotationDeg)
    {
        float rad = (spriteRotationDeg + 180f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    // How much daylight there is between two car bodies, in reference px. Separating-axis: the widest gap on
    // any of the four body axes, which is 0 or less once they are touching.
    static float Gap(TitleCrash.CarPose a, TitleCrash.CarPose b)
    {
        Vector2 between = b.position - a.position;
        float widest = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            float rad = (i < 2 ? a.rotation : b.rotation) * Mathf.Deg2Rad;
            var axis = (i % 2 == 0)
                ? new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))
                : new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

            float reach = Mathf.Abs(Vector2.Dot(between, axis));
            widest = Mathf.Max(widest, reach - Reach(a.rotation, axis) - Reach(b.rotation, axis));
        }
        return widest;
    }

    // Half the body's extent projected onto an axis: the same projection TitleCrash.Overlap works in.
    static float Reach(float rotationDeg, Vector2 axis)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        var along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        var across = new Vector2(-along.y, along.x);
        return TitleCrash.CarLengthPx * 0.5f * Mathf.Abs(Vector2.Dot(along, axis))
             + TitleCrash.CarWidthPx * 0.5f * Mathf.Abs(Vector2.Dot(across, axis));
    }

    [Test]
    public void TheHeroIsTheFrontMostCarAndNoCarIsAnyBiggerThanAnother()
    {
        var field = TitleCrash.Field();
        Assert.That(TitleCrash.HeroIndex, Is.InRange(0, field.Length - 1));

        var hero = field[TitleCrash.HeroIndex];
        for (int i = 0; i < field.Length; i++)
        {
            if (i == TitleCrash.HeroIndex) continue;
            Assert.Greater(hero.depth, field[i].depth, "The player's car has to draw in front of the pile.");
        }

        // Size is a property of the shot, not of a car: four of the same machine, so nothing in the pile can
        // read as a toy parked next to a truck.
        Assert.Greater(TitleCrash.CarLengthPx, 0f);
        Assert.AreEqual(TitleCrash.CarLengthPx * 0.5f, TitleCrash.CarWidthPx, 1e-4f,
                        "The body has to keep the 64x32 livery's proportions.");
    }

    // ------------------------------------------------------------------ the collisions

    [Test]
    public void TheSettledPileNeverHasOneCarInsideAnother()
    {
        var field = TitleCrash.Field();
        for (int step = 0; step <= 200; step++)
        {
            float u = step / 200f;
            var poses = TitleCrash.Tableau(field, u);

            for (int a = 0; a < poses.Length; a++)
            {
                for (int b = a + 1; b < poses.Length; b++)
                {
                    if (poses[a].progress <= 0f || poses[b].progress <= 0f) continue;
                    if (OffTheTop(poses[a]) || OffTheTop(poses[b])) continue;

                    bool through = TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                                      poses[b].position, poses[b].rotation,
                                                      out _, out float depth);

                    // The crash pair are allowed to bury into each other, and have to be: opaque liveries
                    // held at a hard zero draw as two rectangles meeting along a line, which reads as two
                    // cars parked together rather than one buried in the other. Everybody else still gets
                    // no allowance at all.
                    float allowed = TitleCrash.IsInTheCrash(a) && TitleCrash.IsInTheCrash(b)
                        ? TitleCrash.Bite(u) + 1f
                        : 1f;

                    Assert.IsFalse(through && depth > allowed,
                                   $"Cars {a} and {b} are {depth:0.0}px inside each other at u={u:0.00} " +
                                   $"(allowed {allowed:0.0}) — they're drawn through each other rather than crashing.");
                }
            }
        }
    }

    [Test]
    public void TheCrashPairFinishOnTopOfEachOtherAndTheRacingPairKeepStation()
    {
        // This is a tableau, not a pile-up (see Field): the only two cars that ever come near each other are
        // the crash pair, and even they close up rather than interpenetrate. So "is it a crash" is measured
        // as how much closer the crash pair get than anybody else, not as an overlap count.
        var field = TitleCrash.Field();
        var poses = TitleCrash.Tableau(field, 1f);

        // Measured between the BODIES, not the centres: two cars side by side in adjacent lanes have their
        // centres closer together than two cars nose-to-tail, so centre distance says nothing about whether
        // anything is touching.
        float crashPair = Gap(poses[TitleCrash.TurnerIndex], poses[TitleCrash.TurnedIndex]);
        Assert.Less(crashPair, 1f,
                    "The two cars in the accident finish apart from each other — nothing happened between them.");

        for (int a = 0; a < poses.Length; a++)
            for (int b = a + 1; b < poses.Length; b++)
                if (!(TitleCrash.IsInTheCrash(a) && TitleCrash.IsInTheCrash(b)))
                    Assert.Greater(Gap(poses[a], poses[b]), 5f,
                                   $"Cars {a} and {b} are on top of each other, and only the crash pair should be.");

        // The racing pair are running in company, and two cars that keep station cannot touch.
        for (int step = 0; step <= 200; step++)
        {
            var walk = TitleCrash.Tableau(field, step / 200f);
            Assert.IsFalse(TitleCrash.Overlap(walk[0].position, walk[0].rotation,
                                              walk[1].position, walk[1].rotation, out _, out _),
                           "The two cars that are only racing ran into each other — they're the clean half of the shot.");
        }
    }

    [Test]
    public void TheOneAuthoredHitIsTheHeroTBoningTheSliderInSlowMotion()
    {
        var impacts = TitleCrash.Impacts();
        Assert.AreEqual(1, impacts.Length,
                        "The shot is built around one hit; more than that and the damage is a solver's opinion again.");

        var hit = impacts[0];
        Assert.AreEqual(TitleCrash.HeroIndex, hit.striker,
                        "The player's car isn't the one doing it, so the car you're meant to watch isn't in the crash.");
        Assert.AreEqual(TitleCrash.TurnedIndex, hit.struck);
        Assert.Greater(hit.severity, 0.6f, "The one hit in the shot is a love tap.");
        Assert.That(hit.normal.magnitude, Is.EqualTo(1f).Within(0.05f),
                    "The push direction has to be a unit vector — the sparks spray along it.");

        // It has to land inside the slow-motion beat, or it goes off while time is still a blur.
        Assert.Greater(hit.atU, 1f - Beat.Share,
                       "The hit fires during the slam — the crush would be over before anyone could see it.");
        Assert.Less(hit.atU, 1f - Beat.Share * 0.25f,
                    "The hit fires so late there's no crawl left to watch the bodywork fold through.");

        // And it has to land where the two cars actually are at that moment: on the striker's nose, aimed at
        // the car it is about to fold up.
        var poses = TitleCrash.Tableau(TitleCrash.Field(), hit.atU);
        Vector2 nose = poses[hit.striker].position
                       + Nose(poses[hit.striker].rotation) * (TitleCrash.CarLengthPx * 0.5f);

        Assert.Less(Vector2.Distance(hit.pointPx, nose), 25f,
                    "The flash goes off somewhere other than the front of the car that did it.");
        Assert.Less(Gap(poses[hit.striker], poses[hit.struck]), 5f,
                    "The two cars aren't together when the hit fires — the sparks would go off in mid-air.");
        Assert.Greater(Vector2.Dot(hit.normal.normalized,
                                   (poses[hit.struck].position - poses[hit.striker].position).normalized), 0f,
                       "The push points away from the car being hit — the sparks would spray backwards.");
        Assert.GreaterOrEqual(hit.pointPx.x, TitleCrash.ColumnRightPx,
                              "The hit is over the copy column, so the flash would go off on the wordmark.");
    }

    [Test]
    public void TheHitLandsSquareInTheSlidersDoorRatherThanOnACorner()
    {
        // This is what makes the shot worth staging: one contact where the two panels either side of it have
        // to deform completely differently. A narrow nose driven into a long flat flank gouges deep and
        // local; that same flank pressed back into the nose creases it right across. Let the hero arrive at
        // an angle, or on a corner, and both dents come out the same shape — which is exactly the failure
        // the old crater model had, reintroduced through the choreography instead of through the maths.
        var hit = TitleCrash.Impacts()[0];
        var poses = TitleCrash.Tableau(TitleCrash.Field(), hit.atU);

        Vector2 heading = Nose(poses[hit.striker].rotation);
        float rad = poses[hit.struck].rotation * Mathf.Deg2Rad;
        Vector2 flank = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));   // the struck car's long axis

        Assert.Less(Mathf.Abs(Vector2.Dot(heading, flank)), 0.35f,
                    "The hero arrives more than 20 degrees off square to the car it's hitting — that's a " +
                    "sideswipe down the side, not a T-bone into the door.");

        // Where along the struck car the nose lands: 0 is the middle of the door, ±half a car is a corner.
        Vector2 noseAt = poses[hit.striker].position + heading * (TitleCrash.CarLengthPx * 0.5f);
        float along = Vector2.Dot(noseAt - poses[hit.struck].position, flank);

        Assert.Less(Mathf.Abs(along), TitleCrash.CarLengthPx * 0.25f,
                    $"The nose lands {along:0}px along the struck car — it's clipping a corner rather than " +
                    "going into the middle of the flank, so the deep local gouge never happens.");
    }

    [Test]
    public void TheCrushDevelopsAcrossTheWholeSlowMotionBeatRatherThanInOneFrame()
    {
        // A dent that appears fully formed between two frames is a decal. The bodywork model can be pressed
        // deeper a slice at a time, so the fold is spent across the crawl and the last thing the tableau does
        // before it freezes is give way.
        var hit = TitleCrash.Impacts()[0];

        Assert.AreEqual(1f, hit.throughU, 1e-4f,
                        "The crush finishes before the clock does, so the shot has a dead beat on the end of it.");
        Assert.Greater(hit.throughU - hit.atU, Beat.Share * 0.5f,
                       "The fold is spent over a blink — it may as well be the single-frame stamp it replaced.");

        Assert.AreEqual(0f, TitleCrash.Crush(hit, hit.atU), 1e-4f, "The bodywork is already dented on contact.");
        Assert.AreEqual(1f, TitleCrash.Crush(hit, hit.throughU), 1e-4f, "The fold never reaches full depth.");
        Assert.AreEqual(0f, TitleCrash.Crush(hit, hit.atU - 0.05f), 1e-4f, "The car dents before it is hit.");
        Assert.AreEqual(1f, TitleCrash.Crush(hit, 2f), 1e-4f, "The crush runs past the end of the clock.");

        // Monotonic, and front-loaded: metal collapses first and resists after, so half the depth is gone
        // well before half the time is.
        float previous = -1f;
        for (int step = 0; step <= 100; step++)
        {
            float u = Mathf.Lerp(hit.atU, hit.throughU, step / 100f);
            float crush = TitleCrash.Crush(hit, u);
            Assert.GreaterOrEqual(crush, previous, "The fold un-dents partway through the crush.");
            previous = crush;
        }
        Assert.Greater(TitleCrash.Crush(hit, Mathf.Lerp(hit.atU, hit.throughU, 0.5f)), 0.6f,
                       "The crush is linear or back-loaded — a real one collapses hard and then resists.");
    }

    [Test]
    public void TheHeroKeepsDrivingIntoTheSliderRightUpToTheFreeze()
    {
        // The crush is only worth spending over half a second if the two cars are still closing through it.
        // The hero is authored to finish well INSIDE the other car — past the bite allowance, so Settle has
        // something left to push with, and what the screen shows is the struck car being shoved down the road
        // rather than two bodies parked together while a dent quietly deepens.
        var field = TitleCrash.Field();
        var hit = TitleCrash.Impacts()[0];

        var atContact = TitleCrash.Tableau(field, hit.atU);
        var atFreeze = TitleCrash.Tableau(field, 1f);

        Assert.Less(Gap(atContact[hit.striker], atContact[hit.struck]), 5f, "They aren't touching at contact.");
        Assert.Less(Gap(atFreeze[hit.striker], atFreeze[hit.struck]), 1f, "They came apart before the freeze.");

        float shove = Vector2.Distance(atContact[hit.struck].position, atFreeze[hit.struck].position);
        Assert.Greater(shove, 15f,
                       $"The struck car only moves {shove:0}px between the hit and the freeze — nothing is " +
                       "being shunted, so the crush plays out over two parked cars.");

        // Raw choreography, before Settle gets anywhere near it: the hero really is still coming, and by more
        // than the bite allowance, or there would be nothing left over to shove with.
        var heroEnd = TitleCrash.Evaluate(field[hit.striker], 1f);
        var struckEnd = TitleCrash.Evaluate(field[hit.struck], 1f);
        Assert.IsTrue(TitleCrash.Overlap(heroEnd.position, heroEnd.rotation,
                                         struckEnd.position, struckEnd.rotation, out _, out float drive),
                      "The hero's authored finish stops short of the car it hit, so there's no shunt to resolve.");
        Assert.Greater(drive, TitleCrash.MaxBitePx,
                       "The hero drives in by less than the bite allowance, so Settle never pushes and the " +
                       "struck car is never shunted.");
    }

    [Test]
    public void TheTwoCarsInTheCrashActuallyBuryIntoEachOther()
    {
        // The complaint this exists to catch: at a hard zero separation, two opaque liveries draw as two
        // rectangles meeting along a line. Nothing about that reads as contact — it reads as two cars parked
        // very close together. Cars in a wreck share space, because the metal between them has folded and the
        // sprite outline is no longer where the bodywork is.
        var field = TitleCrash.Field();
        var hit = TitleCrash.Impacts()[0];

        Assert.AreEqual(0f, TitleCrash.Bite(hit.atU), 1e-4f, "The cars are already inside each other on contact.");
        Assert.AreEqual(0f, TitleCrash.Bite(hit.atU - 0.1f), 1e-4f, "They bury into each other before the hit.");
        Assert.AreEqual(TitleCrash.MaxBitePx, TitleCrash.Bite(1f), 1e-3f, "The burial never reaches full depth.");
        Assert.Greater(TitleCrash.MaxBitePx, TitleCrash.CarLengthPx * 0.1f,
                       "The allowance is too small to see — the contact will still draw as a seam.");
        Assert.Less(TitleCrash.MaxBitePx, TitleCrash.CarLengthPx * 0.3f,
                    "The cars swallow each other rather than crumpling.");

        // And it has to actually happen in the settled tableau, deepening as the fold does.
        var atFreeze = TitleCrash.Tableau(field, 1f);
        Assert.IsTrue(TitleCrash.Overlap(atFreeze[hit.striker].position, atFreeze[hit.striker].rotation,
                                         atFreeze[hit.struck].position, atFreeze[hit.struck].rotation,
                                         out _, out float buried),
                      "The crash pair finish merely touching — the hit doesn't read as a hit.");
        Assert.Greater(buried, TitleCrash.MaxBitePx * 0.75f,
                       $"The hero only ends up {buried:0.0}px into the car it hit, well short of the {TitleCrash.MaxBitePx:0}px " +
                       "allowance — Settle is pushing them apart again.");

        // Nobody else gets the allowance: the clean half of the shot stays clean.
        for (int a = 0; a < atFreeze.Length; a++)
            for (int b = a + 1; b < atFreeze.Length; b++)
                if (!(TitleCrash.IsInTheCrash(a) && TitleCrash.IsInTheCrash(b)))
                    Assert.IsFalse(TitleCrash.Overlap(atFreeze[a].position, atFreeze[a].rotation,
                                                      atFreeze[b].position, atFreeze[b].rotation, out _, out _),
                                   $"Cars {a} and {b} are inside each other, and only the crash pair may be.");
    }

    [Test]
    public void TheSliderIsThereToBeCaughtRatherThanToKeepUp()
    {
        // Everything in the field lands at u = 1, so how hard one car arrives at another is decided entirely
        // by how much further it had to come. The slider is slow on purpose: it is the difference between the
        // two speeds that is the severity of the hit, and a slider running away from the hero would turn the
        // T-bone into a tap however hard the impact plan claims it is.
        var field = TitleCrash.Field();
        float sliderRun = (field[TitleCrash.TurnedIndex].endPos - field[TitleCrash.TurnedIndex].startPos).magnitude;
        float heroRun = (field[TitleCrash.TurnerIndex].endPos - field[TitleCrash.TurnerIndex].startPos).magnitude;

        Assert.Greater(heroRun / sliderRun, 2f,
                       $"The hero covers {heroRun:0}px to the slider's {sliderRun:0} — it barely catches it, " +
                       "so there's no closing speed in the hit.");

        Assert.AreEqual(1f, TitleCrash.Impacts()[0].severity, 1e-4f,
                        "The one hit in a shot with no other damage in it isn't at full severity.");
    }

    [Test]
    public void ContactsAreReportedWhereTheCarsActuallyMeet()
    {
        var field = TitleCrash.Field();
        var contacts = new List<TitleCrash.Contact>();

        for (int step = 0; step <= 200; step++)
        {
            float u = step / 200f;
            var poses = TitleCrash.Tableau(field, u, contacts);

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

    // A car that hasn't dropped into frame yet: it's allowed to be sitting on top of the rest of the field
    // up there, because none of them are in the crash until they arrive.
    static bool OffTheTop(TitleCrash.CarPose pose)
    {
        return pose.position.y - TitleCrash.HalfSpan(pose.rotation, horizontal: false) > TitleCrash.CanvasHeight;
    }

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
