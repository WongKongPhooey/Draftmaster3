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

    // The opening pace the tableau is authored around, read off the choreography rather than restated here.
    const float Entry = TitleCrash.DefaultEntrySpeed;

    // ------------------------------------------------------------------ the clock

    [Test]
    public void TheClockStartsAtZeroAndReachesExactlyOne()
    {
        Assert.AreEqual(0f, TitleCrash.Freeze(0f, Entry), 1e-5f);
        Assert.AreEqual(1f, TitleCrash.Freeze(1f, Entry), 1e-4f,
                        "The sequence has to be complete at the moment time stops, or the pile freezes half-built.");
    }

    [Test]
    public void TheClockOnlyEverRunsForwards()
    {
        float previous = -1f;
        for (int i = 0; i <= 200; i++)
        {
            float u = TitleCrash.Freeze(i / 200f, Entry);
            Assert.GreaterOrEqual(u, previous, "Choreography time went backwards — the cars would rewind.");
            previous = u;
        }
    }

    [Test]
    public void TimeComesToAStandstillRatherThanBeingCutOff()
    {
        // The last slice of wall clock should move the choreography almost not at all: that glide to nothing
        // IS the effect. A hard stop would read as a dropped frame.
        float lastStep = TitleCrash.Freeze(1f, Entry) - TitleCrash.Freeze(0.99f, Entry);
        float firstStep = TitleCrash.Freeze(0.01f, Entry) - TitleCrash.Freeze(0f, Entry);
        Assert.Less(lastStep, firstStep * 0.05f,
                    "Time should be crawling by the end, not still running at anything like its opening rate.");

        Assert.AreEqual(1f, TitleCrash.FreezeRate(0f, Entry), 1e-5f);
        Assert.AreEqual(0f, TitleCrash.FreezeRate(1f, Entry), 1e-5f,
                        "The particles ride this rate — anything above zero and the sparks keep burning after the freeze.");
    }

    [Test]
    public void TheEntryIsQuickAndTheSlowdownNeverLetsUp()
    {
        // What the shot needs is cars that arrive too fast to stop and then bleed that speed off the whole way
        // in — not a slow constant slide with a brake tacked on the end.
        const float step = 1f / 400f;

        float opening = TitleCrash.Freeze(step, Entry) / step;
        Assert.Greater(opening, 1.8f,
                       "The cars enter at barely more than the average pace — they slide in instead of being thrown in.");

        float previous = float.MaxValue;
        for (int i = 0; i < 400; i++)
        {
            float s = i / 400f;
            float rate = (TitleCrash.Freeze(s + step, Entry) - TitleCrash.Freeze(s, Entry)) / step;
            Assert.LessOrEqual(rate, previous + 1e-3f,
                               $"Choreography time speeds back up around s={s:0.00} — the slow-down has to be one continuous brake.");
            previous = rate;
        }

        Assert.Less(previous, 0.05f, "Time is still moving at the end — the pile wouldn't come to rest.");
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
    public void EveryCarHasLandedByTheTimeTheClockStops()
    {
        foreach (var plan in TitleCrash.Field())
        {
            Assert.Greater(plan.travel, 0f);
            Assert.LessOrEqual(plan.delay + plan.travel, 1f,
                               "A car is still in the air when time stops — it would freeze halfway across the screen.");
            Assert.AreEqual(1f, TitleCrash.Evaluate(plan, 1f).progress, 1e-4f);
        }
    }

    [Test]
    public void CarsSpinIntoPlaceRatherThanSwingingToIt()
    {
        foreach (var plan in TitleCrash.Field())
            Assert.Greater(Mathf.Abs(plan.startRotation - plan.endRotation), 180f,
                           "A car turns less than half a rotation on its way in — it should tumble, not lean over.");
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
    public void ThePileEndsUpLeaningOnItselfRatherThanParkedApart()
    {
        var poses = TitleCrash.Tableau(TitleCrash.Field(), 1f);

        int touching = 0;
        for (int a = 0; a < poses.Length; a++)
            for (int b = a + 1; b < poses.Length; b++)
                if (TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                       poses[b].position, poses[b].rotation, out _, out _))
                    touching++;

        Assert.GreaterOrEqual(touching, 2,
                              "Nothing in the frozen shot is touching anything else — that's four parked cars, not a crash.");
    }

    [Test]
    public void TheCarsConnectSeveralTimesWhileTheCrashIsPlaying()
    {
        var field = TitleCrash.Field();
        var contacts = new List<TitleCrash.Contact>();
        var open = new HashSet<int>();
        var hits = new List<(float u, int a, int b, float severity)>();

        for (int step = 0; step <= 400; step++)
        {
            float u = step / 400f;
            TitleCrash.Tableau(field, u, contacts);

            var now = new HashSet<int>();
            foreach (var hit in contacts)
            {
                int key = hit.a * field.Length + hit.b;
                now.Add(key);
                if (!open.Contains(key)) hits.Add((u, hit.a, hit.b, hit.severity));
            }
            open = now;
        }

        Assert.GreaterOrEqual(hits.Count, 4, "The pile barely connects — there's nothing to spark off.");

        foreach (var hit in hits)
        {
            Assert.Greater(hit.u, 0f, "A hit lands before the sequence has started — the cars are stacked off screen there.");
            Assert.That(hit.severity, Is.InRange(0.2f, 1f));
            Assert.AreNotEqual(hit.a, hit.b);
        }

        Assert.Greater(hits.Max(h => h.severity), 0.6f,
                       "Every contact is a love tap — the shot needs at least one proper hit in it.");
        Assert.Greater(hits.Count(h => h.a == TitleCrash.HeroIndex || h.b == TitleCrash.HeroIndex), 0,
                       "The hero car never touches anything, so the car you're meant to watch isn't in the crash.");
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
