using UnityEngine;

// A blown engine (PlayerVehicleController.EngineBlown, from Draftmaster.Sim.EngineTemp): the moment it lets go, a
// bang of debris and oil thrown out of the engine bay with a flash and a ball of smoke; after that, thick dark
// smoke pours out of the front of the car for as long as the engine is dead, laid down behind it as it coasts so
// it leaves a trail, and rising off it in a column once it has stopped.
//
// Self-installed by PlayerVehicleController (with the engine temperature) on player and AI cars alike, emitted
// manually like TyreSmoke and DamageDebris so every velocity stays in the top-down plane. Nothing is built until
// an engine actually blows, so a field of healthy engines never pays for it.
[RequireComponent(typeof(PlayerVehicleController))]
public class EngineBlowFX : MonoBehaviour
{
    [Header("Where")]
    [Tooltip("Engine bay, metres ahead of the car's centre.")]
    public float engineAhead = 1.5f;

    [Header("The bang")]
    public int debrisPieces = 40;
    public int flashPuffs = 10;
    public int smokeBurst = 26;
    [Tooltip("Speed (m/s) pieces are thrown out at, on top of the car's own.")]
    public float debrisSpeedMin = 3f;
    public float debrisSpeedMax = 11f;
    public Color debrisColorA = new Color(0.12f, 0.12f, 0.13f);
    public Color debrisColorB = new Color(0.55f, 0.55f, 0.58f);
    public Color oilColor = new Color(0.05f, 0.04f, 0.03f, 0.9f);
    public Color flashColor = new Color(1f, 0.62f, 0.2f, 0.95f);

    [Header("The trail")]
    [Tooltip("Smoke puffs per second while the car is still moving fast, and while it is stopped.")]
    public float trailRateMoving = 40f;
    public float trailRateStopped = 9f;
    public Color smokeColor = new Color(0.16f, 0.16f, 0.17f, 0.75f);
    public float smokeLifetime = 2.6f;
    public float smokeSizeMin = 0.7f;
    public float smokeSizeMax = 1.3f;
    public float smokeGrowth = 3.5f;

    [Tooltip("Sorting order. The car body is 5 - all of this comes up out of it, so it sits over it.")]
    public int sortingOrder = 7;

    PlayerVehicleController _car;
    ParticleSystem _smoke, _debris;
    float _trailAccum;

    void Start()
    {
        _car = GetComponent<PlayerVehicleController>();
        if (_car != null) _car.EngineBlew += OnBlew;
    }

    void OnDestroy()
    {
        if (_car != null) _car.EngineBlew -= OnBlew;
        if (_smoke != null) Destroy(_smoke.gameObject);
        if (_debris != null) Destroy(_debris.gameObject);
    }

    void Engine(out Vector2 at, out Vector2 fwd, out Vector2 right, out Vector2 carVel)
    {
        float hr = _car.HeadingDeg * Mathf.Deg2Rad;
        fwd = new Vector2(Mathf.Cos(hr), Mathf.Sin(hr));
        right = new Vector2(fwd.y, -fwd.x);
        at = (Vector2)transform.position + fwd * engineAhead;
        carVel = fwd * _car.SpeedMps;
    }

    void OnBlew()
    {
        if (_smoke == null) _smoke = MakeSmoke();
        if (_debris == null) _debris = MakeDebris();
        Engine(out Vector2 at, out Vector2 fwd, out Vector2 right, out Vector2 carVel);
        float z = transform.position.z;

        // Metal, and oil, thrown out of the bay: out to the sides and down the road with the car.
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < debrisPieces; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector2 dir = (right * side * Random.Range(0.3f, 1f) + fwd * Random.Range(-0.6f, 0.8f)).normalized;
            Vector2 v = carVel * Random.Range(0.55f, 0.9f) + dir * Random.Range(debrisSpeedMin, debrisSpeedMax);
            bool oil = Random.value < 0.3f;
            p.position = new Vector3(at.x, at.y, z) + (Vector3)(Random.insideUnitCircle * 0.4f);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = oil ? oilColor : Color.Lerp(debrisColorA, debrisColorB, Random.value);
            p.startSize = oil ? Random.Range(0.12f, 0.3f) : Random.Range(0.06f, 0.16f);
            p.startLifetime = Random.Range(2.5f, 4f);
            p.rotation = Random.Range(0f, 360f);
            _debris.Emit(p, 1);
        }

        // The flash: a few hot puffs that burn out fast.
        for (int i = 0; i < flashPuffs; i++)
        {
            Vector2 v = carVel * 0.8f + Random.insideUnitCircle * 3f;
            p.position = new Vector3(at.x, at.y, z) + (Vector3)(Random.insideUnitCircle * 0.5f);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = flashColor;
            p.startSize = Random.Range(0.8f, 1.5f);
            p.startLifetime = Random.Range(0.25f, 0.45f);
            _smoke.Emit(p, 1);
        }

        // And the ball of smoke.
        for (int i = 0; i < smokeBurst; i++) EmitSmoke(at, carVel * 0.7f, z, Random.Range(2f, 5f), 1.4f);
    }

    void FixedUpdate()
    {
        if (_car == null || !_car.EngineBlown || _smoke == null) { _trailAccum = 0f; return; }

        Engine(out Vector2 at, out _, out _, out Vector2 carVel);
        float speed = _car.SpeedMps;
        float rate = Mathf.Lerp(trailRateStopped, trailRateMoving, Mathf.Clamp01(speed / 20f));
        _trailAccum += rate * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(_trailAccum);
        if (count <= 0) return;
        _trailAccum -= count;
        // Left behind the moving car (most of its velocity shed at once), so it draws a trail down the road.
        for (int i = 0; i < count; i++) EmitSmoke(at, carVel * 0.15f, transform.position.z, Random.Range(0.3f, 1.4f), 1f);
    }

    void EmitSmoke(Vector2 at, Vector2 baseVel, float z, float drift, float sizeScale)
    {
        Vector2 v = baseVel + Random.insideUnitCircle.normalized * drift;
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        p.position = new Vector3(at.x, at.y, z) + (Vector3)(Random.insideUnitCircle * 0.35f);
        p.velocity = new Vector3(v.x, v.y, 0f);
        p.startColor = smokeColor;
        p.startSize = Random.Range(smokeSizeMin, smokeSizeMax) * sizeScale;
        p.startLifetime = Random.Range(smokeLifetime * 0.7f, smokeLifetime);
        _smoke.Emit(p, 1);
    }

    ParticleSystem MakeSmoke()
    {
        var ps = NewSystem($"EngineSmoke ({name})", 900);
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.1f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.4f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(smokeGrowth,
            AnimationCurve.EaseInOut(0f, 1f / Mathf.Max(smokeGrowth, 1f), 1f, 1f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder + 1;
        return ps;
    }

    ParticleSystem MakeDebris()
    {
        var ps = NewSystem($"EngineDebris ({name})", 200);
        // Pieces skid to rest on the track and lie there a moment before fading.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.06f;
        limit.limit = new ParticleSystem.MinMaxCurve(0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
        return ps;
    }

    static ParticleSystem NewSystem(string name, int maxParticles)
    {
        var go = new GameObject(name);
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // it stays where it was laid
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = ParticleFX.DefaultMaterial();
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }
}
