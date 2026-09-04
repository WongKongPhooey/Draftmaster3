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
    public void EveryCarDrivesInNoseFirstRatherThanBackwards()
    {
        // The liveries are drawn nose-left, so a sprite at angle r is a car pointing along r + 180. Miss that
        // and the whole field reverses down the screen at speed, which is precisely what it used to do.
        foreach (var plan in TitleCrash.Field())
        {
            Vector2 travel = (plan.endPos - plan.startPos).normalized;
            Assert.Greater(Vector2.Dot(Nose(plan.startRotation), travel), 0.9f,
                           "A car enters pointing somewhere other than where it's going — it's driving in backwards.");
        }
    }

    [Test]
    public void OnlyTheCarThatGotTurnedEndsUpPointingOffItsLineOfTravel()
    {
        var field = TitleCrash.Field();
        for (int i = 0; i < field.Length; i++)
        {
            Vector2 travel = (field[i].endPos - field[i].startPos).normalized;
            float alignment = Vector2.Dot(Nose(field[i].endRotation), travel);

            if (i == TitleCrash.TurnedIndex)
                Assert.Less(alignment, 0.75f,
                            "The car that's meant to have been turned finishes pointing straight down the road — nothing happened to it.");
            else
                Assert.Greater(alignment, 0.9f,
                               $"Car {i} finishes sideways; only the turned car is supposed to be out of shape.");
        }

        Assert.Greater(Mathf.Abs(field[TitleCrash.TurnedIndex].endRotation -
                                 field[TitleCrash.TurnedIndex].startRotation), 30f,
                       "The turned car barely rotates on its way in — the hit reads as a nudge.");
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
                    Assert.IsFalse(through && depth > 1f,
                                   $"Cars {a} and {b} are {depth:0.0}px inside each other at u={u:0.00} — " +
                                   "they're drawn through each other rather than crashing.");
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
    public void TheOneAuthoredHitIsTheHeroTurningTheCarAheadInSlowMotion()
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
                       "The hit fires during the slam — the sparks would be over before anyone could see them.");
        Assert.Less(hit.atU, 1f - Beat.Share * 0.25f,
                    "The hit fires so late there's no crawl left to watch the sparks travel through.");

        // And it has to land where the two cars actually are at that moment: on the striker's nose, aimed at
        // the car it is turning.
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
