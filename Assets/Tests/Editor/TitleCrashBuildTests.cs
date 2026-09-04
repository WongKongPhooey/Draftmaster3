using System.Linq;
using System.Reflection;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// TitleCrashTests checks the choreography — the arithmetic of where the cars go and how the clock stops.
// This checks the thing the arithmetic is fed into: it actually stands the tableau up, drives it from the
// first frame to the freeze, and looks at what came out. Cars built, liveries loaded, bodywork dented,
// draw order below the title canvas, pile sitting in the empty half of the screen, particles frozen with
// everything else.
//
// It runs in a preview scene so nothing here can dirty or overwrite the scenes open in the editor, and it
// reaches TitleCrashScene by reflection because this assembly can't reference Assembly-CSharp. The clock is
// driven by writing `_elapsed` before each Update rather than waiting on wall time, so the sequence plays
// out deterministically in EditMode where nothing ticks by itself.
public class TitleCrashBuildTests
{
    // The reference canvas is 640x360 at 100px/unit, matching PixelUITheme — so one reference pixel is
    // 0.01 world units, which is what a WorldSpace canvas of that size at this scale gives us.
    const float CanvasScale = 0.01f;

    // A number the carset definitely has a livery for, used to stand in for a career save.
    const int CareerNumber = 11;

    Scene _scene;
    GameObject _root;
    Canvas _canvas;

    bool _hadSavedNumber;
    int _savedNumber;

    [SetUp]
    public void SetUp()
    {
        _hadSavedNumber = PlayerPrefs.HasKey(PlayerDriverNumberKey);
        _savedNumber = PlayerPrefs.GetInt(PlayerDriverNumberKey, 0);

        // Start every test with no career, whatever ran before this one. The hero's number decides which
        // livery is loaded, the livery decides the sprite, and the sprite decides the size of the mesh every
        // measurement below is taken against — so a sibling suite leaving a career number behind quietly
        // changes the cast of this one. Each test that cares sets the number it wants.
        PlayerPrefs.DeleteKey(PlayerDriverNumberKey);

        // Dent depth is scaled by a global damage slider (TrackConditions.DamageMultiplier), so a suite that
        // turns damage down and does not put it back would quietly shrink every measurement below. Reached
        // by reflection for the same reason as everything else here: TrackConditions is in Assembly-CSharp,
        // which this assembly cannot reference.
        FindRuntimeType("TrackConditions")
            ?.GetField("DamageMultiplier", BindingFlags.Static | BindingFlags.Public)
            ?.SetValue(null, 1f);

        _scene = EditorSceneManager.NewPreviewScene();

        var canvasGo = new GameObject("TestCanvas", typeof(Canvas));
        SceneManager.MoveGameObjectToScene(canvasGo, _scene);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        // WorldSpace rather than ScreenSpaceCamera on purpose: the rect is then the size we set instead of
        // whatever the editor's game view happens to be, so the measurements below are the same on any
        // machine. TitleCrashScene reads rect + lossyScale either way, which is the path being tested.
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = new Vector2(TitleCrash.CanvasWidth, TitleCrash.CanvasHeight);
        rt.localScale = Vector3.one * CanvasScale;
    }

    [TearDown]
    public void TearDown()
    {
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);

        if (_hadSavedNumber) PlayerPrefs.SetInt(PlayerDriverNumberKey, _savedNumber);
        else PlayerPrefs.DeleteKey(PlayerDriverNumberKey);
    }

    // ------------------------------------------------------------------ the cars exist

    [Test]
    public void TheWholeFieldIsBuiltWithRealBodyworkMeshes()
    {
        var crash = Play(out var component);
        var cars = Cars(crash);

        Assert.AreEqual(TitleCrash.Field().Length, cars.Length,
                        "The tableau didn't build every car — a livery failed to load, or the layout wasn't measured.");

        int gridX = Field<int>(component, "gridX");
        int gridY = Field<int>(component, "gridY");

        foreach (var car in cars)
        {
            var mesh = car.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, $"{car.name} has no bodywork mesh — VehicleDamage.Build() never ran.");
            Assert.AreEqual((gridX + 1) * (gridY + 1), mesh.vertexCount,
                            $"{car.name}'s mesh isn't the deformable grid the component asked for.");

            var mr = car.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr.sharedMaterial, $"{car.name} has no material — it would draw as magenta or not at all.");
            Assert.Greater(car.transform.localScale.x, 0f, $"{car.name} was scaled to nothing.");
        }
    }

    [Test]
    public void EveryCarDrawsBelowTheTitleCanvasWithTheHeroInFront()
    {
        var cars = Cars(Play(out _));
        var orders = cars.Select(c => c.GetComponent<MeshRenderer>().sortingOrder).ToArray();

        foreach (var order in orders)
            Assert.Less(order, 0, "A car sorts at or above the title canvas — it would draw over the wordmark.");

        Assert.AreEqual(orders.Max(), orders[TitleCrash.HeroIndex],
                        "The player's car isn't the front-most thing in the pile.");
        Assert.AreEqual(orders.Length, orders.Distinct().Count(),
                        "Two cars share a sorting order, so which one is in front is undefined.");
    }

    // ------------------------------------------------------------------ the damage model is on show

    [Test]
    public void CarsArriveAlreadyDentedAndThePileAddsMoreOnImpact()
    {
        // One Update builds the field and poses it at u = 0: pre-damage is on, but the scripted impact
        // isn't until TitleCrash.ImpactU, so this is the bodywork the cars turned up with.
        var crash = Play(out var component, steps: 0);
        var cars = Cars(crash);

        Assert.Greater(cars.Sum(Deformation), 0.05f,
                       "The cars are showroom-fresh — the pre-damage never reached the mesh.");

        // The bodywork as the cars turned up, kept vertex by vertex so the crash can be compared against it
        // directly. Two easier measurements both lie here:
        //
        //   "the deepest dent got deeper" — a dent is a vector ADDED to whatever a vertex already carried,
        //   so a second hit from another angle can leave it nearer the flat grid than it was.
        //   "more vertices are bent"      — the pile connects where the cars overlap, which is inside the
        //   area the pre-damage already covered, so the count can be saturated before the crash starts.
        //
        // What is unambiguous is whether the crash MOVED any metal, so that is what is asserted.
        var onArrival = cars.Select(Vertices).ToArray();

        Drive(component, from: 0f, to: 1f, steps: 240);

        int moved = 0;
        for (int i = 0; i < cars.Length; i++) moved += VerticesMovedSince(onArrival[i], cars[i]);

        Assert.Greater(moved, 0,
                       "Not one vertex on any car moved across the whole crash — the contacts are denting nothing.");
    }

    [Test]
    public void DamageStaysWithinTheBodyworkRatherThanTearingTheCarApart()
    {
        var crash = Play(out var component);
        float maxDent = Field<float>(component, "maxDent");

        foreach (var car in Cars(crash))
            Assert.LessOrEqual(Deformation(car), maxDent + 1e-3f,
                               $"{car.name} has a vertex pulled further than maxDent — the mesh is torn, not dented.");
    }

    // ------------------------------------------------------------------ the shot

    [Test]
    public void TheFrozenPileFillsTheEmptyHalfOfTheScreen()
    {
        var cars = Cars(Play(out _));

        var pile = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        foreach (var car in cars)
        {
            var b = car.GetComponent<MeshRenderer>().bounds;
            Vector2 min = ToCanvasPx(new Vector3(b.min.x, b.min.y, 0f));
            Vector2 max = ToCanvasPx(new Vector3(b.max.x, b.max.y, 0f));

            Assert.GreaterOrEqual(min.x, TitleCrash.ColumnRightPx - 1f,
                                  $"{car.name} is drawn over the copy column, not in the art half.");
            Assert.LessOrEqual(min.y, TitleCrash.CanvasHeight);
            Assert.GreaterOrEqual(max.y, 0f, $"{car.name} finished off the bottom of the screen.");

            pile = Rect.MinMaxRect(Mathf.Min(pile.xMin, min.x), Mathf.Min(pile.yMin, min.y),
                                   Mathf.Max(pile.xMax, max.x), Mathf.Max(pile.yMax, max.y));
        }

        // The point of the tableau is to fill the slot the hatch used to occupy, so it has to be a pile
        // across the space rather than four cars stacked in one corner of it.
        float slotWidth = TitleCrash.CanvasWidth - TitleCrash.ColumnRightPx;
        Assert.Greater(pile.width, slotWidth * 0.6f,
                       "The cars are bunched into a fraction of the art slot — most of it is still empty.");
        Assert.Greater(pile.height, TitleCrash.CanvasHeight * 0.45f,
                       "The pile is a thin band; it doesn't fill the height of the slot.");
    }

    [Test]
    public void EveryCarIsBuiltTheSameSize()
    {
        // Liveries come out of the carset at whatever pixel size they were drawn at, so "same size" has to be
        // measured on the drawn body — sprite width times the scale the tableau gave it — rather than trusted.
        var cars = Cars(Play(out _));
        float first = DrawnLength(cars[0]);

        Assert.Greater(first, 0f);
        for (int i = 1; i < cars.Length; i++)
            Assert.AreEqual(first, DrawnLength(cars[i]), first * 0.01f,
                            $"{cars[i].name} is drawn a different size to the rest of the field.");
    }

    [Test]
    public void TheFrozenPileIsCarsLeaningOnEachOtherRatherThanDrawnThroughEachOther()
    {
        var cars = Cars(Play(out _));

        var field = TitleCrash.Field();
        var poses = TitleCrash.Tableau(field, 1f);

        // What the maths settled has to be what the transforms got, or the overlap was resolved on paper only.
        for (int i = 0; i < cars.Length && i < poses.Length; i++)
        {
            Vector2 drawn = ToCanvasPx(cars[i].transform.position);
            Assert.Less(Vector2.Distance(drawn, poses[i].position), 2f,
                        $"{cars[i].name} isn't standing where the settled pile put it.");
        }

        for (int a = 0; a < poses.Length; a++)
            for (int b = a + 1; b < poses.Length; b++)
                if (TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                       poses[b].position, poses[b].rotation, out _, out float depth))
                    Assert.Less(depth, 1f, $"Cars {a} and {b} are still inside each other in the frozen shot.");
    }

    // ------------------------------------------------------------------ the player's car

    [Test]
    public void ThePlayersOwnCarIsTheHeroOnceTheyHaveACareer()
    {
        PlayerPrefs.SetInt(PlayerDriverNumberKey, CareerNumber);

        var cars = Cars(Play(out _));
        StringAssert.EndsWith($"livery{CareerNumber}", cars[TitleCrash.HeroIndex].name,
                              "A career save is set but the hero car isn't wearing the player's number.");

        for (int i = 0; i < cars.Length; i++)
            if (i != TitleCrash.HeroIndex)
                StringAssert.DoesNotEndWith($"livery{CareerNumber}", cars[i].name,
                                            "The player's number turns up twice in the same shot.");
    }

    [Test]
    public void WithNoCareerTheHeroFallsBackToTheDemoCar()
    {
        PlayerPrefs.DeleteKey(PlayerDriverNumberKey);

        var cars = Cars(Play(out var component));
        int fallback = Field<int>(component, "fallbackHeroNumber");

        StringAssert.EndsWith($"livery{fallback}", cars[TitleCrash.HeroIndex].name,
                              "With no save behind it the hero slot should still be dressed, not left empty.");
    }

    // ------------------------------------------------------------------ the particles

    [Test]
    public void SmokeAndSparksAreThereAndFreezeWithEverythingElse()
    {
        var crash = Play(out _);

        foreach (var name in new[] { "CrashSparks", "CrashSmoke" })
        {
            var ps = crash.transform.Find(name)?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, $"There is no {name} — the crash has no {(name.EndsWith("Smoke") ? "smoke" : "sparks")}.");

            var main = ps.main;
            Assert.IsTrue(main.useUnscaledTime,
                          $"{name} rides Time.timeScale, so anything that touches it would drag the title screen with it.");
            Assert.AreEqual(0f, main.simulationSpeed, 1e-4f,
                            $"{name} is still running after the freeze — the sparks would burn out over a still pile.");
            Assert.IsFalse(ps.emission.enabled,
                           $"{name} emits on its own as well as on cue; the bursts would be doubled up.");
            Assert.Greater(ps.particleCount, 0, $"{name} never emitted anything.");
        }
    }

    // ------------------------------------------------------------------ harness

    // PlayerDriver lives in Assembly-CSharp; the key it persists the career number under does not change,
    // and TitleCrashScene reads the same one. (PlayerDriver.NumberKey.)
    const string PlayerDriverNumberKey = "career.carnumber";

    // Stands the tableau up and runs it to the freeze, returning the object it built everything under.
    GameObject Play(out Component component, int steps = 240)
    {
        var type = FindRuntimeType("TitleCrashScene");
        Assert.IsNotNull(type, "TitleCrashScene isn't compiled — the title screen has no crash to show.");

        _root = new GameObject("TitleCrash");
        SceneManager.MoveGameObjectToScene(_root, _scene);
        component = _root.AddComponent(type);

        type.GetField("layoutCanvas").SetValue(component, _canvas);
        type.GetField("startDelay").SetValue(component, 0f);

        Step(component);                                   // builds, then poses at u = 0
        if (steps > 0) Drive(component, 0f, 1f, steps);
        return _root;
    }

    // Walks the sequence by writing the component's own clock, so it plays out the same way every run
    // instead of depending on how long the editor took between Update calls. `from`/`to` are seconds, and
    // the default tempo's whole run is one of them (half a second of slam, half a second of crawl).
    void Drive(Component component, float from, float to, int steps)
    {
        var elapsed = component.GetType().GetField("_elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 1; i <= steps; i++)
        {
            elapsed.SetValue(component, Mathf.Lerp(from, to, i / (float)steps));
            Step(component);
        }

        // One last frame past the end, so anything that only settles at s = 1 has settled.
        elapsed.SetValue(component, to * 2f);
        Step(component);
    }

    // Reads a tuning field off the component rather than restating its value here, so a test measures the
    // tableau against what it was actually told to build.
    static T Field<T>(Component component, string name)
    {
        var field = component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, $"TitleCrashScene has no '{name}' field any more.");
        return (T)field.GetValue(component);
    }

    static void Step(Component component)
    {
        component.GetType()
                 .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                 .Invoke(component, null);
    }

    // The cars the tableau built, in choreography order (the index is baked into the object's name).
    static GameObject[] Cars(GameObject crash)
    {
        return crash.transform.Cast<Transform>()
                    .Where(t => t.name.StartsWith("CrashCar_"))
                    .OrderBy(t => int.Parse(t.name.Split('_')[1]))
                    .Select(t => t.gameObject)
                    .ToArray();
    }

    // The length of the car as drawn: the livery's own width, scaled by whatever the tableau set on it.
    static float DrawnLength(GameObject car)
    {
        var damage = car.GetComponent("VehicleDamage");
        var sprite = (Sprite)damage.GetType().GetField("sourceSprite").GetValue(damage);
        return sprite.bounds.size.x * car.transform.lossyScale.x;
    }

    // A copy of a car's bodywork as it stands right now. Copied rather than referenced: the damage model
    // writes back into the same mesh, so holding the array would be holding the live one and every
    // comparison against it would be a comparison with itself.
    static Vector3[] Vertices(GameObject car) => (Vector3[])car.GetComponent<MeshFilter>().sharedMesh.vertices.Clone();

    // How many of a car's vertices have moved since that snapshot was taken.
    static int VerticesMovedSince(Vector3[] before, GameObject car)
    {
        var now = car.GetComponent<MeshFilter>().sharedMesh.vertices;
        int moved = 0;
        for (int i = 0; i < now.Length && i < before.Length; i++)
            if (Vector3.Distance(now[i], before[i]) > 1e-5f) moved++;
        return moved;
    }

    // How far the worst-dented vertex has been pushed off the flat grid the mesh was built as, in local
    // units. Zero means the car is untouched.
    static float Deformation(GameObject car)
    {
        var damage = car.GetComponent("VehicleDamage");
        var sprite = (Sprite)damage.GetType().GetField("sourceSprite").GetValue(damage);
        int gridX = (int)damage.GetType().GetField("gridX").GetValue(damage);
        int gridY = (int)damage.GetType().GetField("gridY").GetValue(damage);

        Vector2 size = sprite.bounds.size;
        Vector2 min = -size * 0.5f;
        var verts = car.GetComponent<MeshFilter>().sharedMesh.vertices;

        float worst = 0f;
        for (int y = 0; y <= gridY; y++)
        {
            for (int x = 0; x <= gridX; x++)
            {
                var flat = new Vector3(min.x + size.x * (x / (float)gridX),
                                       min.y + size.y * (y / (float)gridY), 0f);
                worst = Mathf.Max(worst, Vector3.Distance(verts[y * (gridX + 1) + x], flat));
            }
        }
        return worst;
    }

    // World point -> reference-canvas pixel, the inverse of TitleCrashScene.PxToWorld.
    Vector2 ToCanvasPx(Vector3 world)
    {
        var rt = (RectTransform)_canvas.transform;
        var local = rt.InverseTransformPoint(world);
        var r = rt.rect;
        return new Vector2((local.x - r.xMin) / r.width * TitleCrash.CanvasWidth,
                           (local.y - r.yMin) / r.height * TitleCrash.CanvasHeight);
    }

    static System.Type FindRuntimeType(string name)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
                     .Select(a => a.GetType(name, false))
                     .FirstOrDefault(t => t != null);
    }
}
