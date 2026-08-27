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

    // Carset liveries are 64x32, so a car is drawn half as wide as it is long.
    const float WidthOverLength = 0.5f;

    // ------------------------------------------------------------------ the clock

    [Test]
    public void TheClockStartsAtZeroAndReachesExactlyOne()
    {
        Assert.AreEqual(0f, TitleCrash.Freeze(0f, 0.45f), 1e-5f);
        Assert.AreEqual(1f, TitleCrash.Freeze(1f, 0.45f), 1e-4f,
                        "The sequence has to be complete at the moment time stops, or the pile freezes half-built.");
    }

    [Test]
    public void TheClockOnlyEverRunsForwards()
    {
        float previous = -1f;
        for (int i = 0; i <= 200; i++)
        {
            float u = TitleCrash.Freeze(i / 200f, 0.45f);
            Assert.GreaterOrEqual(u, previous, "Choreography time went backwards — the cars would rewind.");
            previous = u;
        }
    }

    [Test]
    public void TimeComesToAStandstillRatherThanBeingCutOff()
    {
        // The last slice of wall clock should move the choreography almost not at all: that glide to nothing
        // IS the effect. A hard stop would read as a dropped frame.
        float lastStep = TitleCrash.Freeze(1f, 0.45f) - TitleCrash.Freeze(0.99f, 0.45f);
        float firstStep = TitleCrash.Freeze(0.01f, 0.45f) - TitleCrash.Freeze(0f, 0.45f);
        Assert.Less(lastStep, firstStep * 0.05f,
                    "Time should be crawling by the end, not still running at anything like its opening rate.");

        Assert.AreEqual(1f, TitleCrash.FreezeRate(0f, 0.45f), 1e-5f);
        Assert.AreEqual(0f, TitleCrash.FreezeRate(1f, 0.45f), 1e-5f,
                        "The particles ride this rate — anything above zero and the sparks keep burning after the freeze.");
    }

    // ------------------------------------------------------------------ the tableau

    [Test]
    public void EveryCarEntersFromOffTheRightEdge()
    {
        foreach (var plan in TitleCrash.Field())
        {
            var start = TitleCrash.Evaluate(plan, 0f);
            Assert.AreEqual(plan.startPos.x, start.position.x, 0.01f);

            float halfWidth = HalfSpan(plan.lengthPx, plan.startRotation, horizontal: true);
            Assert.Greater(start.position.x - halfWidth, TitleCrash.CanvasWidth,
                           "A car is already on screen when the sequence opens — it should fly in, not appear.");
        }
    }

    [Test]
    public void EveryCarLandsInTheEmptyHalfOfTheScreen()
    {
        foreach (var plan in TitleCrash.Field())
        {
            var end = TitleCrash.Evaluate(plan, 1f);
            Assert.AreEqual(plan.endPos.x, end.position.x, 0.01f);
            Assert.AreEqual(plan.endPos.y, end.position.y, 0.01f);
            Assert.AreEqual(plan.endRotation, end.rotation, 0.01f);

            float halfWidth = HalfSpan(plan.lengthPx, plan.endRotation, horizontal: true);
            float halfHeight = HalfSpan(plan.lengthPx, plan.endRotation, horizontal: false);

            Assert.GreaterOrEqual(end.position.x - halfWidth, TitleCrash.ColumnRightPx,
                                  "A car finishes over the copy column — it would sit on the wordmark or the menu.");
            Assert.LessOrEqual(end.position.x + halfWidth, TitleCrash.CanvasWidth + 60f,
                               "A car finishes so far off the right edge that most of it isn't in the shot.");
            Assert.GreaterOrEqual(end.position.y - halfHeight, 0f);
            Assert.LessOrEqual(end.position.y + halfHeight, TitleCrash.CanvasHeight);
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
    public void TheHeroIsTheBiggestAndFrontMostCar()
    {
        var field = TitleCrash.Field();
        Assert.That(TitleCrash.HeroIndex, Is.InRange(0, field.Length - 1));

        var hero = field[TitleCrash.HeroIndex];
        for (int i = 0; i < field.Length; i++)
        {
            if (i == TitleCrash.HeroIndex) continue;
            Assert.Greater(hero.depth, field[i].depth, "The player's car has to draw in front of the pile.");
            Assert.Greater(hero.lengthPx, field[i].lengthPx, "The player's car has to be the one you look at.");
        }
    }

    // ------------------------------------------------------------------ the hits

    [Test]
    public void ImpactsLandInOrderOnRealCarsInsideTheShot()
    {
        var field = TitleCrash.Field();
        var impacts = TitleCrash.Impacts();
        Assert.IsNotEmpty(impacts);

        float previous = -1f;
        foreach (var hit in impacts)
        {
            Assert.GreaterOrEqual(hit.at, previous, "Impacts are consumed in order — an out-of-order one never fires.");
            previous = hit.at;

            Assert.That(hit.at, Is.InRange(0f, 1f));
            Assert.That(hit.car, Is.InRange(0, field.Length - 1));
            Assert.Greater(hit.severity, 0f);
            Assert.LessOrEqual(hit.severity, 1f);
            Assert.Greater(hit.spray.sqrMagnitude, 1e-6f, "A hit with no spray direction fans its sparks nowhere.");

            Assert.GreaterOrEqual(hit.pointPx.x, TitleCrash.ColumnRightPx);
            Assert.LessOrEqual(hit.pointPx.x, TitleCrash.CanvasWidth);
            Assert.That(hit.pointPx.y, Is.InRange(0f, TitleCrash.CanvasHeight));

            // A hit only dents its car if the car has arrived: OnImpact is aimed at where the car is now.
            var plan = field[hit.car];
            Assert.Greater(TitleCrash.Evaluate(plan, hit.at).progress, 0.5f,
                           "A car is hit before it is anywhere near the pile.");
        }
    }

    [Test]
    public void TheSmokeStartsWithTheFirstContactAndOverThePile()
    {
        Assert.AreEqual(TitleCrash.Impacts()[0].at, TitleCrash.PlumeStartsAt, 1e-4f,
                        "The pile should start smoking when it first connects, not before or long after.");
        Assert.GreaterOrEqual(TitleCrash.PlumeCentrePx.x, TitleCrash.ColumnRightPx);
        Assert.Less(TitleCrash.PlumeCentrePx.x, TitleCrash.CanvasWidth);
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

    // Half the extent of a rotated car along one screen axis, in reference pixels.
    static float HalfSpan(float lengthPx, float rotationDeg, bool horizontal)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        float along = lengthPx * 0.5f;
        float across = lengthPx * WidthOverLength * 0.5f;
        return horizontal
            ? along * Mathf.Abs(Mathf.Cos(rad)) + across * Mathf.Abs(Mathf.Sin(rad))
            : along * Mathf.Abs(Mathf.Sin(rad)) + across * Mathf.Abs(Mathf.Cos(rad));
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
