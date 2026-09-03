using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// The title screen's hero art: four cars coming down from above the top edge across the empty half of the
// screen, and time easing to a dead stop so the moment stays there. Two of them are racing in company and
// never touch; the other two are a corner being settled, one having put its nose in the back of the other
// and turned it.
//
// The hit is scripted rather than solved. It used to be four cars thrown at the same spot with
// TitleCrash.Settle pushing the overlaps apart every frame, which is a physics solver running at speed on
// four bodies and looked like one — jitter, and sparks on frames where nothing had really happened. Now the
// choreography guarantees the only two cars that ever come near each other are the crash pair, and the one
// impact fires on a time rather than on an overlap. Settle still runs underneath as a backstop and has
// nothing left to push.
//
// This is the runtime half of TitleCrash — that holds the choreography (who goes where, when, and how the
// clock decelerates), this builds the cars out of it and paints them. Nothing here is authored in the
// scene except the component itself: the cars are real VehicleDamage bodywork built from carset liveries,
// so the dents are the game's own damage model rather than a drawing of one.
//
// Sorting: everything this makes sits below 0, which is the title canvas's order, so the scrim, the
// wordmark and the menu always draw over the crash however far the cars slide.
//
// Time: driven entirely off Time.unscaledDeltaTime and a local clock. Time.timeScale is deliberately never
// touched — it survives a scene load, and a title screen that freezes the race you load out of it would be
// a very quiet bug.
[DisallowMultipleComponent]
public class TitleCrashScene : MonoBehaviour
{
    [Header("Cast")]
    [Tooltip("Carset the liveries come from, e.g. cup26 -> Resources/cup26liveryN.")]
    public string carsetPrefix = "cup26";
    [Tooltip("Numbers tried, in order, to dress the cars the player isn't driving. The first ones that " +
             "resolve to a livery sprite get used.")]
    public int[] fillerCarNumbers = { 24, 11, 5, 22, 9, 19, 12, 48, 3, 2, 20, 1 };
    [Tooltip("Number the hero car wears when there's no career save to read one from.")]
    public int fallbackHeroNumber = 8;

    [Header("Timing")]
    [Tooltip("Seconds of empty screen before the first car arrives.")]
    public float startDelay = 0.35f;
    [Tooltip("Seconds from the first car entering frame to the tableau standing still.")]
    public float duration = 3f;
    [Tooltip("How much faster than the run's average pace the cars enter, before time decelerates to a " +
             "stop. 2 is a flat slow-down the whole way; higher makes the entry a burst and the last few " +
             "pixels a longer settle.")]
    [Range(1f, 4f)] public float entrySpeed = TitleCrash.DefaultEntrySpeed;

    [Header("Damage")]
    [Tooltip("Dents the two cars in the accident arrive already carrying. The racing pair stay clean.")]
    [Range(0, 12)] public int dentsPerCar = 3;
    [Tooltip("Severity of that pre-existing damage, 0..1.")]
    [Range(0f, 1f)] public float preDamageSeverity = 0.75f;
    [Tooltip("Seed for the pre-damage, so the same screen comes up the same way every boot.")]
    public int seed = 1987;

    [Header("Bodywork")]
    [Tooltip("Deformable mesh resolution across the car. Higher = smoother crumple, at this size it's cheap.")]
    public int gridX = 18;
    public int gridY = 12;
    public float dentRadius = 0.9f;
    public float dentStrength = 0.5f;
    public float maxDent = 0.8f;

    [Header("Particles")]
    public Color sparkColorA = new Color(1f, 0.95f, 0.65f);
    public Color sparkColorB = new Color(1f, 0.55f, 0.12f);
    public Color smokeColorA = new Color(0.42f, 0.44f, 0.48f, 0.55f);
    public Color smokeColorB = new Color(0.12f, 0.13f, 0.16f, 0.5f);
    [Tooltip("Sparks thrown by the hardest contact. Softer ones scale down from here.")]
    public int sparksPerImpact = 44;
    [Tooltip("Smoke puffs coughed out by the hardest contact.")]
    public int smokePerImpact = 9;
    [Tooltip("Puffs the burning pile gives off across the rest of the sequence.")]
    public int plumePuffs = 26;

    [Header("Render")]
    [Tooltip("Draw order of the rear-most car. Everything here has to stay below the title canvas (0) or " +
             "the crash covers the wordmark.")]
    public int baseSortingOrder = -24;
    [Tooltip("Z the tableau sits at. Behind the canvas plane, in front of the camera's clear colour.")]
    public float depthZ = 0f;

    [Header("Wiring")]
    [Tooltip("Canvas the reference layout is measured against. Left empty, the title menu's own canvas is used.")]
    public Canvas layoutCanvas;

    class Car
    {
        public Transform t;
        public VehicleDamage damage;
        public TitleCrash.CarPlan plan;
    }

    // Indexed by plan, with a hole where a livery failed to load, so an impact always dents the car the
    // choreography named rather than whichever one happened to fill that slot.
    Car[] _cars = System.Array.Empty<Car>();
    ParticleSystem _sparks, _smoke;

    RectTransform _canvasRt;
    Camera _camera;
    float _unit;                 // world units per reference pixel
    bool _built;
    int _tries;

    float _elapsed;              // wall clock since the first car was due, seconds
    float _u;                    // choreography time, 0..1
    float _plumeCarry;           // fractional puff owed to the plume, carried between frames
    float _plumeSpent;           // puffs the plume has issued so far

    // Working state. Poses are rebuilt from the choreography every frame and settled against each other, so
    // these are buffers rather than memory: the tableau stays a pure function of the clock.
    TitleCrash.CarPlan[] _plans = System.Array.Empty<TitleCrash.CarPlan>();
    TitleCrash.CarPose[] _poses = System.Array.Empty<TitleCrash.CarPose>();
    readonly List<TitleCrash.Contact> _contacts = new List<TitleCrash.Contact>();

    // The scripted hit, and whether it has gone off yet. One flag per impact is all the memory the shot
    // needs now that a bang is keyed to a moment rather than to two boxes still being inside each other.
    TitleCrash.ImpactPlan[] _impacts = System.Array.Empty<TitleCrash.ImpactPlan>();
    bool[] _impactsFired = System.Array.Empty<bool>();

    float _plumeFrom = -1f;      // choreography time of the first contact; < 0 until something is hit
    Vector2 _plumeAt;            // where the plume rises from: the middle of what has been hit so far
    int _plumeHits;

    void Update()
    {
        if (!_built)
        {
            if (!TryBuild()) return;
            _built = true;
        }

        _elapsed += Time.unscaledDeltaTime;
        float s = duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);
        _u = TitleCrash.Freeze(s, entrySpeed);

        PoseCars();
        Collide();
        Smoulder();

        // The particle systems run on the same decelerating clock, so the sparks hang in the air mid-streak
        // instead of burning out while the cars stand still.
        float rate = TitleCrash.FreezeRate(s, entrySpeed);
        SetSimulationSpeed(_sparks, rate);
        SetSimulationSpeed(_smoke, rate);
    }

    // ------------------------------------------------------------------ build

    bool TryBuild()
    {
        // A title screen opened straight out of the editor can be a frame or two ahead of the canvas's first
        // layout pass, and a canvas with no rect yet would scale every car to nothing.
        if (!ResolveLayout())
        {
            if (++_tries < 120) return false;
            Debug.LogWarning("TitleCrashScene: no canvas or camera to measure the layout against.", this);
            enabled = false;
            return false;
        }

        var plans = TitleCrash.Field();
        _plans = plans;
        _poses = new TitleCrash.CarPose[plans.Length];
        _impacts = TitleCrash.Impacts();
        _impactsFired = new bool[_impacts.Length];
        _plumeAt = TitleCrash.PileCentrePx;

        var liveries = PickLiveries(plans.Length);
        var rng = new System.Random(seed);

        _cars = new Car[plans.Length];
        int built = 0;
        for (int i = 0; i < plans.Length; i++)
        {
            var sprite = liveries[i];
            if (sprite == null) continue;

            _cars[i] = BuildCar(i, plans[i], sprite);
            // Only the two cars in the accident wear any. The racing pair are meant to read as clean cars
            // going by at speed — which is what makes the wrecked pair beside them read as wrecked.
            if (TitleCrash.IsInTheCrash(i)) ApplyPreDamage(_cars[i], rng);
            built++;
        }

        if (built == 0)
        {
            Debug.LogWarning($"TitleCrashScene: no '{carsetPrefix}' liveries in Resources — no crash to show.", this);
            enabled = false;
            return false;
        }

        _sparks = BuildSparks();
        _smoke = BuildSmoke();

        // Pose once before the first frame is drawn, or every car flashes at the origin for a frame.
        _elapsed = -Mathf.Max(0f, startDelay);
        PoseCars();
        return true;
    }

    bool ResolveLayout()
    {
        if (layoutCanvas == null)
        {
            var menu = FindFirstObjectByType<TitleScreenUI>();
            if (menu != null) layoutCanvas = menu.GetComponent<Canvas>();
        }

        if (layoutCanvas != null)
        {
            var rt = layoutCanvas.transform as RectTransform;
            if (rt != null && rt.rect.width > 1f)
            {
                float unit = (rt.rect.width / TitleCrash.CanvasWidth) * rt.lossyScale.x;
                if (unit > 0f) { _canvasRt = rt; _unit = unit; return true; }
            }

            // The canvas is there but hasn't laid out yet. Wait for it rather than framing off the camera,
            // which would put the tableau on screen without lining it up with the copy column.
            if (_tries < 10) return false;
        }

        // No canvas at all: fall back to framing the orthographic camera directly, which gets the tableau on
        // screen even in a scene that has no Iron Oval layout in it.
        _canvasRt = null;
        _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (_camera == null || !_camera.orthographic) return false;
        _unit = (_camera.orthographicSize * 2f) / TitleCrash.CanvasHeight;
        return _unit > 0f;
    }

    // Reference-canvas pixel (origin bottom-left) -> world point on the tableau's plane.
    Vector3 PxToWorld(Vector2 px)
    {
        if (_canvasRt != null)
        {
            var r = _canvasRt.rect;
            var local = new Vector3(r.xMin + (px.x / TitleCrash.CanvasWidth) * r.width,
                                    r.yMin + (px.y / TitleCrash.CanvasHeight) * r.height, 0f);
            var world = _canvasRt.TransformPoint(local);
            return new Vector3(world.x, world.y, depthZ);
        }

        var centre = _camera != null ? _camera.transform.position : Vector3.zero;
        float halfW = _camera != null ? _camera.orthographicSize * _camera.aspect : TitleCrash.CanvasWidth * _unit * 0.5f;
        float halfH = _camera != null ? _camera.orthographicSize : TitleCrash.CanvasHeight * _unit * 0.5f;
        return new Vector3(centre.x - halfW + (px.x / TitleCrash.CanvasWidth) * halfW * 2f,
                           centre.y - halfH + (px.y / TitleCrash.CanvasHeight) * halfH * 2f, depthZ);
    }

    // The paint. The hero wears the player's number once there's a career behind it — same number the
    // garage and the timing tower resolve a driver from — and the rest of the pile takes whatever else the
    // carset has, skipping the hero's own number so the shot never contains two of the same car.
    Sprite[] PickLiveries(int count)
    {
        var chosen = new Sprite[count];
        int heroNumber = HeroCarNumber();

        var hero = Livery(heroNumber);
        if (hero == null) { heroNumber = fallbackHeroNumber; hero = Livery(heroNumber); }
        chosen[Mathf.Clamp(TitleCrash.HeroIndex, 0, count - 1)] = hero;

        int at = 0;
        for (int i = 0; i < count; i++)
        {
            if (chosen[i] != null) continue;
            while (at < (fillerCarNumbers?.Length ?? 0))
            {
                int number = fillerCarNumbers[at++];
                if (number == heroNumber) continue;
                var sprite = Livery(number);
                if (sprite != null) { chosen[i] = sprite; break; }
            }
        }

        // Nothing resolved for the hero slot either: let it borrow a filler rather than leave a hole in the
        // middle of the shot.
        if (chosen[TitleCrash.HeroIndex] == null)
            for (int i = 0; i < count; i++)
                if (chosen[i] != null) { chosen[TitleCrash.HeroIndex] = chosen[i]; break; }

        return chosen;
    }

    Sprite Livery(int number)
    {
        if (number < 0 || string.IsNullOrEmpty(carsetPrefix)) return null;
        return Resources.Load<Sprite>($"{carsetPrefix}livery{number}");
    }

    // Has the player got a team yet? A career is a saved number or a saved name — either one means there is
    // a car of theirs to put in the shot, and PlayerDriver already knows how to turn that into a number.
    int HeroCarNumber()
    {
        bool hasCareer = PlayerPrefs.GetInt(PlayerDriver.NumberKey, 0) > 0
                         || PlayerDriver.CareerName.Length > 0;
        return hasCareer ? PlayerDriver.CarNumber : fallbackHeroNumber;
    }

    Car BuildCar(int index, TitleCrash.CarPlan plan, Sprite livery)
    {
        var go = new GameObject($"CrashCar_{index}_{livery.name}");
        go.transform.SetParent(transform, false);

        var damage = go.AddComponent<VehicleDamage>();
        damage.sourceSprite = livery;
        damage.gridX = Mathf.Clamp(gridX, 2, 32);
        damage.gridY = Mathf.Clamp(gridY, 2, 32);
        damage.dentRadius = dentRadius;
        damage.dentStrength = dentStrength;
        damage.maxDent = maxDent;
        damage.sortingOrder = baseSortingOrder + plan.depth * 2;
        damage.Build();

        // The plans are drawn in reference pixels; the sprite is 5 world units of car. Scale bridges the two
        // so the tableau keeps its proportions whatever the screen is.
        float spriteLength = Mathf.Max(0.001f, livery.bounds.size.x);
        float scale = (TitleCrash.CarLengthPx * _unit) / spriteLength;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        return new Car { t = go.transform, damage = damage, plan = plan };
    }

    // Bodywork the car turned up with. Hits are struck on the outline and pushed inward, which is the same
    // call a barrier makes during a race — so what the title screen shows is genuinely the damage model.
    void ApplyPreDamage(Car car, System.Random rng)
    {
        if (dentsPerCar <= 0 || car.damage.sourceSprite == null) return;
        Vector3 extents = car.damage.sourceSprite.bounds.extents;

        for (int i = 0; i < dentsPerCar; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            var edge = new Vector3(Mathf.Cos(angle) * extents.x, Mathf.Sin(angle) * extents.y, 0f);
            float severity = preDamageSeverity * (0.6f + 0.4f * (float)rng.NextDouble());
            Dent(car, car.t.TransformPoint(edge), severity);
        }
    }

    // Dent the car at whatever point of its bodywork is nearest `worldPoint`, pushing toward its centre.
    // Clamping onto the body first means a flash struck between two cars still lands on both of them
    // rather than falling outside the dent radius and doing nothing.
    void Dent(Car car, Vector3 worldPoint, float severity)
    {
        if (car.damage.sourceSprite == null) return;
        Vector3 extents = car.damage.sourceSprite.bounds.extents;

        Vector3 local = car.t.InverseTransformPoint(worldPoint);
        local.x = Mathf.Clamp(local.x, -extents.x, extents.x);
        local.y = Mathf.Clamp(local.y, -extents.y, extents.y);

        Vector3 inward = new Vector3(-local.x, -local.y, 0f);
        if (inward.sqrMagnitude < 1e-6f) inward = Vector3.down;

        car.damage.OnImpact(car.t.TransformPoint(local), car.t.TransformVector(inward), severity);
    }

    // ------------------------------------------------------------------ playback

    // Every car posed from the choreography, then the whole field settled against itself so no two bodies
    // are inside each other, then painted. The settle is what turns the pile from an overlap into a crash.
    void PoseCars()
    {
        if (_poses.Length != _plans.Length) _poses = new TitleCrash.CarPose[_plans.Length];

        for (int i = 0; i < _plans.Length; i++) _poses[i] = TitleCrash.Evaluate(_plans[i], _u);
        TitleCrash.Settle(_plans, _poses, _u, _contacts);

        for (int i = 0; i < _cars.Length && i < _poses.Length; i++)
        {
            if (_cars[i] == null) continue;
            _cars[i].t.position = PxToWorld(_poses[i].position);
            _cars[i].t.rotation = Quaternion.Euler(0f, 0f, _poses[i].rotation);
        }
    }

    // The hit, once, when the choreography says so.
    //
    // This used to fire off whatever TitleCrash.Settle reported, which meant the shot's damage was decided
    // by a solver: it dented both cars at the midpoint between their centres, so which panel got hit was
    // whatever the geometry happened to hand over, and any frame where the resolver failed to fully separate
    // four bodies banged again. One authored impact instead — the striker takes it across the nose, the car
    // it turned takes it across the tail, and it happens on exactly one frame because it is keyed to a time
    // rather than to an overlap.
    void Collide()
    {
        for (int i = 0; i < _impacts.Length; i++)
        {
            if (_impactsFired[i] || _u < _impacts[i].atU) continue;
            _impactsFired[i] = true;

            var hit = _impacts[i];
            Vector3 at = PxToWorld(hit.pointPx);

            // Front of the one that did it, back of the one it did it to. Both ends are worked across a
            // spread of points so the panel folds rather than picking up a single dimple.
            DentPanel(hit.striker, front: true, severity: hit.severity);
            DentPanel(hit.struck, front: false, severity: hit.severity);

            // Sparks fly off the line the two cars are pushing along, which is the way a real scrape throws
            // them; the smoke just puffs up out of the same point.
            Vector2 spray = hit.normal.sqrMagnitude > 1e-6f ? hit.normal.normalized : Vector2.up;
            Burst(_sparks, at, spray, 150f, Mathf.RoundToInt(sparksPerImpact * hit.severity),
                  120f, 430f, sparkColorA, sparkColorB, 2.5f, 4.5f, 0.55f);
            Burst(_smoke, at, spray, 180f, Mathf.RoundToInt(smokePerImpact * hit.severity),
                  14f, 60f, smokeColorA, smokeColorB, 26f, 62f, 4f);

            if (_plumeFrom < 0f) { _plumeFrom = _u; _plumeAt = hit.pointPx; _plumeHits = 1; }
            else { _plumeHits++; _plumeAt += (hit.pointPx - _plumeAt) / _plumeHits; }
        }
    }

    // Cave in one end of a car: several dents spread across the panel, each struck on the outline and pushed
    // inward, which is the same call a barrier makes during a race.
    void DentPanel(int index, bool front, float severity)
    {
        if (index < 0 || index >= _cars.Length || _cars[index] == null) return;

        var car = _cars[index];
        if (car.damage.sourceSprite == null) return;
        Vector3 extents = car.damage.sourceSprite.bounds.extents;

        foreach (var local in TitleCrash.Panel(front))
        {
            Vector3 on = new Vector3(local.x * extents.x, local.y * extents.y, 0f);
            Dent(car, car.t.TransformPoint(on), severity);
        }
    }

    // The pile keeps smoking after the first proper contact, so there's a plume hanging over the cars by the
    // time everything stops. Puffs are spent against choreography time, not wall clock, so the smoke thins
    // out and stops as the clock does.
    void Smoulder()
    {
        if (_plumeFrom < 0f || _smoke == null) return;

        // Puffs are spent against choreography time, so the whole `plumePuffs` budget is issued across the
        // plume's window however many frames that takes — and issues none once the clock has stopped.
        float span = Mathf.Max(0.01f, 1f - _plumeFrom);
        float want = plumePuffs * Mathf.Clamp01((_u - _plumeFrom) / span);
        _plumeCarry += Mathf.Max(0f, want - _plumeSpent);
        _plumeSpent = want;

        while (_plumeCarry >= 1f)
        {
            _plumeCarry -= 1f;
            Vector2 jitter = Random.insideUnitCircle * 34f;
            Vector3 at = PxToWorld(_plumeAt + jitter);
            Burst(_smoke, at, Vector2.up, 60f, 1, 8f, 34f, smokeColorA, smokeColorB, 30f, 74f, 5f);
        }
    }

    // ------------------------------------------------------------------ particles

    ParticleSystem BuildSparks()
    {
        var ps = MakeSystem("CrashSparks", baseSortingOrder + 8);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.03f;
        renderer.lengthScale = 2.5f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.14f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.5f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        Fade(ps, 0.35f);
        return ps;
    }

    ParticleSystem BuildSmoke()
    {
        var ps = MakeSystem("CrashSmoke", baseSortingOrder + 7);
        var main = ps.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.05f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.4f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        // Smoke swells as it rises; the frozen frame wants a bloom over the pile, not a scatter of dots.
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.6f));

        Fade(ps, 0.15f);
        return ps;
    }

    ParticleSystem MakeSystem(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;          // every particle's velocity is supplied per-emit
        main.gravityModifier = 0f;     // top-down view
        main.maxParticles = 600;
        main.playOnAwake = false;
        // The whole screen runs on its own clock: unscaled so a stray Time.timeScale can't touch it, and
        // simulationSpeed is what actually brings the effects to a halt with the cars.
        main.useUnscaledTime = true;

        var emission = ps.emission;
        emission.enabled = false;      // emission is entirely manual, via Emit(EmitParams)

        var shape = ps.shape;
        shape.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = ParticleFX.DefaultMaterial();
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        ps.Play();
        return ps;
    }

    static void Fade(ParticleSystem ps, float holdFraction)
    {
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdFraction), new GradientAlphaKey(0f, 1f) });
        colour.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    static void SetSimulationSpeed(ParticleSystem ps, float speed)
    {
        if (ps == null) return;
        var main = ps.main;
        main.simulationSpeed = Mathf.Max(0f, speed);
    }

    // Speeds and sizes come in as reference pixels (per second, and across) so a burst reads the same on any
    // screen; `_unit` is the only place they turn into world units.
    void Burst(ParticleSystem ps, Vector3 at, Vector2 direction, float spreadDeg, int count,
               float speedMinPx, float speedMaxPx, Color colorA, Color colorB,
               float sizeMinPx, float sizeMaxPx, float lifetime)
    {
        if (ps == null || count <= 0) return;

        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        float baseAngle = Mathf.Atan2(direction.y, direction.x);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
            float speed = Random.Range(speedMinPx, speedMaxPx) * _unit;

            emit.position = at;
            emit.velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            emit.startColor = Color.Lerp(colorA, colorB, Random.value);
            emit.startSize = Random.Range(sizeMinPx, sizeMaxPx) * _unit;
            emit.startLifetime = lifetime * Random.Range(0.75f, 1.2f);
            ps.Emit(emit, 1);
        }
    }
}
