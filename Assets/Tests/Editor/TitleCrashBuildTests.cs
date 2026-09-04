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
    public void CarsArriveUndamagedAndEveryDentComesFromTheOneHit()
    {
        // The cars used to turn up carrying a scatter of authored pre-damage, which meant the dents on screen
        // were part simulation and part decoration and you could not tell which was which. They arrive clean
        // now: everything the frozen tableau shows has to have been folded in by the contact itself.
        //
        // One Update builds the field and poses it at u = 0, long before TitleCrash.ImpactU, so this is the
        // bodywork the cars turned up with.
        var crash = Play(out var component, steps: 0);
        var cars = Cars(crash);

        foreach (var car in cars)
            Assert.Less(Deformation(car), 1e-3f,
                        $"{car.name} is already dented before anything has hit it — the shot is showing " +
                        "damage the simulation didn't do.");

        var onArrival = cars.Select(Vertices).ToArray();

        Drive(component, from: 0f, to: 1f, steps: 240);

        // The crash pair have to come out of it visibly folded, and the racing pair have to come out of it
        // untouched — that contrast is what makes the wrecked pair read as wrecked.
        for (int i = 0; i < cars.Length; i++)
        {
            bool inTheCrash = TitleCrash.IsInTheCrash(i);
            int moved = VerticesMovedSince(onArrival[i], cars[i]);

            if (inTheCrash)
            {
                Assert.Greater(moved, 0, $"{cars[i].name} is in the accident and not one vertex on it moved.");
                Assert.Greater(Deformation(cars[i]), 0.05f,
                               $"{cars[i].name} came through the T-bone with barely a mark on it.");
            }
            else
            {
                Assert.AreEqual(0, moved, $"{cars[i].name} is only racing past, and something dented it.");
            }
        }
    }

    [Test]
    public void TheCrashCarsFoldOutOfARealOverlapAndNeverOutOfAVirtualPress()
    {
        // The rule that keeps the crash welded together, checked where it is actually set.
        //
        // dentStrength drives the striker a further `dentStrength * severity` into the panel ON TOP of
        // wherever the two bodies already are. In a race that is the only source of fold, because the
        // collision solver ejects the cars every step and leaves no real overlap to read. Here the opposite
        // holds: Settle keeps this pair MaxBitePx inside each other on purpose, so there is a real intrusion
        // for both of them to fold out of — and a press on top of it would open a void exactly as wide as
        // the press, whatever the shares are set to (see BodyDeformTests).
        var cars = Cars(Play(out _));

        foreach (int i in new[] { TitleCrash.TurnerIndex, TitleCrash.TurnedIndex })
        {
            var damage = cars[i].GetComponent(FindRuntimeType("VehicleDamage"));
            Assert.IsNotNull(damage, $"{cars[i].name} has no VehicleDamage to read a fold depth off.");

            Assert.AreEqual(0f, Field<float>(damage, "dentStrength"), 1e-5f,
                            $"{cars[i].name} carries a virtual press. The two cars are already buried in " +
                            "each other, so this folds metal nothing is occupying and opens a hole down the " +
                            "middle of the crash exactly this wide.");
        }
    }

    [Test]
    public void TheDeformedPanelsActuallyMeetWithNoVoidBetweenThem()
    {
        // The one that measures the thing you can see. Everything else about the crush is arithmetic on
        // depths and burials; this drives the whole shot and then looks at where the two BODYWORK MESHES
        // actually ended up, because a void down the middle of the crash is a fact about vertices, not
        // about the numbers that were supposed to place them.
        var crash = Play(out var component);
        Drive(component, from: 0f, to: 1f, steps: 240);

        var cars = Cars(crash);
        var hit = TitleCrash.Impacts()[0];

        Vector2 n = hit.normal.normalized;              // striker -> struck
        Vector2 across = new Vector2(-n.y, n.x);

        // How far each car's metal reaches along the contact normal, measured only across the width of the
        // contact — otherwise the hero's tail and the slider's far end decide the answer instead of the
        // panels that touched.
        float heroReach = float.MinValue;               // furthest the striker's metal gets toward the struck car
        float sliderReach = float.MaxValue;             // nearest the struck car's metal comes back toward it

        foreach (var v in MeshPointsPx(cars[hit.striker]))
            if (Mathf.Abs(Vector2.Dot(v - hit.pointPx, across)) < 30f)
                heroReach = Mathf.Max(heroReach, Vector2.Dot(v, n));

        foreach (var v in MeshPointsPx(cars[hit.struck]))
            if (Mathf.Abs(Vector2.Dot(v - hit.pointPx, across)) < 30f)
                sliderReach = Mathf.Min(sliderReach, Vector2.Dot(v, n));

        float gap = sliderReach - heroReach;
        Assert.Less(gap, 2f,
                    $"There is a {gap:0.0}px void between the two cars' bodywork at the freeze. They both " +
                    $"folded away from the contact and left a hole where the crash is supposed to be. " +
                    $"(burial {TitleCrash.MaxBitePx:0}px, so each panel may give up at most half of it.)");
        Assert.Greater(gap, -TitleCrash.MaxBitePx - 4f,
                    $"The two cars' bodywork is {-gap:0.0}px through each other — past the burial, so the " +
                    "metal is drawn overlapping rather than crushed together.");
    }

    // A car's bodywork vertices, in reference-canvas pixels: the mesh as it currently stands, through the
    // car's transform and back out into the space the choreography is authored in.
    Vector2[] MeshPointsPx(GameObject car)
    {
        var mesh = car.GetComponent<MeshFilter>().sharedMesh;
        return mesh.vertices.Select(v => ToCanvasPx(car.transform.TransformPoint(v))).ToArray();
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
    public void TheFrozenPileIsTwoCarsBuriedInEachOtherAndNobodyElseTouching()
    {
        var cars = Cars(Play(out _));

        var field = TitleCrash.Field();
        var poses = TitleCrash.Tableau(field, 1f);

        // What the maths settled has to be what the transforms got, or the pile was resolved on paper only.
        for (int i = 0; i < cars.Length && i < poses.Length; i++)
        {
            Vector2 drawn = ToCanvasPx(cars[i].transform.position);
            Assert.Less(Vector2.Distance(drawn, poses[i].position), 2f,
                        $"{cars[i].name} isn't standing where the settled pile put it.");
        }

        for (int a = 0; a < poses.Length; a++)
        {
            for (int b = a + 1; b < poses.Length; b++)
            {
                bool crashPair = TitleCrash.IsInTheCrash(a) && TitleCrash.IsInTheCrash(b);
                bool through = TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                                  poses[b].position, poses[b].rotation, out _, out float depth);

                if (crashPair)
                {
                    // These two are SUPPOSED to be inside each other by the freeze. Held at a hard zero they
                    // draw as two opaque rectangles meeting along a line, which reads as two cars parked
                    // together rather than one buried in the other.
                    Assert.IsTrue(through, "The crash pair finish merely touching — the hit doesn't read as a hit.");
                    Assert.Greater(depth, TitleCrash.MaxBitePx * 0.75f,
                                   $"They're only {depth:0.0}px into each other, well short of the " +
                                   $"{TitleCrash.MaxBitePx:0}px allowance.");
                    Assert.Less(depth, TitleCrash.MaxBitePx + 1f,
                                $"They're {depth:0.0}px into each other — past the allowance, so they're " +
                                "drawn through each other rather than crashing.");
                }
                else
                {
                    Assert.IsFalse(through && depth > 1f,
                                   $"Cars {a} and {b} are inside each other, and only the crash pair may be.");
                }
            }
        }
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
        Assert.IsNotNull(field, $"{component.GetType().Name} has no '{name}' field any more.");
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
