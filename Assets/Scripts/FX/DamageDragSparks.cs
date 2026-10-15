using UnityEngine;
using Draftmaster.Sim;

// A car knocked about badly enough drags its bodywork on the track. Once the denting on VehicleDamage passes
// `startDent` the lowest-hanging metal (the most folded vertex) scrapes the asphalt and leaves a trail of
// sparks behind the car, growing denser as the car gets more battered and as it goes faster. The bodywork
// bounces as it drags, so the stream comes and goes rather than pouring out like a hose.
//
// Self-installed by PlayerVehicleController (its `damageDragSparks` toggle) on player and AI cars alike, and
// emitted manually like TyreSmoke so every velocity stays in the top-down plane. The particle system is only
// built the first time a car is dented enough to drag, so the field of clean cars never pays for one. A
// repair (pit stop, the crew between runs) straightens the panels, DentAmount drops and the sparks stop.
[RequireComponent(typeof(PlayerVehicleController))]
public class DamageDragSparks : MonoBehaviour
{
    [Header("When It Drags")]
    [Tooltip("Denting (VehicleDamage.DentAmount, 0..1) at which the bodywork first touches the ground.")]
    [Range(0f, 1f)] public float startDent = 0.10f;
    [Tooltip("Denting at which the drag is as heavy as it gets.")]
    [Range(0f, 1f)] public float fullDent = 0.30f;
    [Tooltip("Speed (m/s) below which dragging metal doesn't spark.")]
    public float minSpeed = 4f;
    [Tooltip("Speed (m/s) at which the spark stream reaches its full rate.")]
    public float fullSpeed = 45f;
    [Tooltip("Off the asphalt (grass, gravel) the metal ploughs dirt rather than striking sparks.")]
    public bool onlyOnTrack = true;

    [Header("Sparks")]
    [Tooltip("Sparks per second at full drag and full speed.")]
    public float maxRate = 160f;
    [Tooltip("How much the bodywork bounces: 0 = a steady stream, 1 = it lifts clear of the ground at times.")]
    [Range(0f, 1f)] public float bounce = 0.6f;
    [Tooltip("How fast the bounce comes and goes (Hz).")]
    public float bounceFrequency = 3f;
    public Color sparkColorA = new Color(1f, 0.95f, 0.65f);
    public Color sparkColorB = new Color(1f, 0.55f, 0.12f);
    [Tooltip("Speed (m/s) the sparks are kicked back off the asphalt, relative to the car.")]
    public float kickSpeedMin = 3f;
    public float kickSpeedMax = 11f;
    [Tooltip("Spark fan half-angle (deg) around straight back.")]
    public float spreadDeg = 22f;
    [Tooltip("Fraction of the car's velocity a spark keeps. Under 1, so they fall behind the car.")]
    [Range(0f, 1f)] public float inheritVelocity = 0.55f;
    public float lifetime = 0.22f;
    public float sizeMin = 0.06f;
    public float sizeMax = 0.11f;
    [Tooltip("Sorting order. The car body is 5 — the scrape is underneath the car, but the sparks fly out past it.")]
    public int sortingOrder = 6;
    public int maxParticles = 400;

    PlayerVehicleController _car;
    VehicleDamage _bodywork;
    ParticleSystem _ps;
    Vector2 _lastPos;
    bool _havePos;
    float _spawnAccum;
    float _noiseSeed;

    void Start()
    {
        _car = GetComponent<PlayerVehicleController>();
        _bodywork = GetComponentInChildren<VehicleDamage>();
        _noiseSeed = Random.value * 100f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 pos = transform.position;
        Vector2 vel = _havePos && dt > 0f ? (pos - _lastPos) / dt : Vector2.zero;
        _lastPos = pos;
        _havePos = true;

        if (_car == null || _bodywork == null) { _spawnAccum = 0f; return; }
        if (onlyOnTrack && _car.OnLooseSurface) { _spawnAccum = 0f; return; }

        float intensity = DamageDrag.Intensity(_bodywork.DentAmount, startDent, fullDent);
        float speed = _car.SpeedMps;
        float rate = DamageDrag.Rate(intensity, speed, minSpeed, fullSpeed, maxRate);
        if (rate <= 0f) { _spawnAccum = 0f; return; }

        // The panel bounces on the asphalt: noise in ~0..1, and at full bounce the low part of its swing
        // lifts the metal clear and the stream stops for a moment.
        float n = Mathf.PerlinNoise(_noiseSeed, Time.time * bounceFrequency);
        rate *= Mathf.Clamp01(1f + bounce * (2f * n - 1f) * 2f);
        if (rate <= 0f) return;

        _spawnAccum += rate * dt;
        int count = Mathf.FloorToInt(_spawnAccum);
        if (count <= 0) return;
        _spawnAccum -= count;
        if (_ps == null) _ps = MakeSystem();

        // Straight back along the way the car is travelling (its nose if it is barely moving sideways).
        float hr = _car.HeadingDeg * Mathf.Deg2Rad;
        Vector2 travel = vel.sqrMagnitude > 1f ? vel.normalized : new Vector2(Mathf.Cos(hr), Mathf.Sin(hr));
        float backAngle = Mathf.Atan2(-travel.y, -travel.x);
        Vector2 point = _bodywork.DragPointWorld;
        float z = transform.position.z;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            float ang = backAngle + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
            Vector2 v = vel * inheritVelocity
                      + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(kickSpeedMin, kickSpeedMax);
            // Spread the sparks over the step the car just covered so a fast car lays a line, not dots.
            Vector2 at = point - vel * (dt * Random.value) + Random.insideUnitCircle * 0.15f;

            p.position = new Vector3(at.x, at.y, z);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = Color.Lerp(sparkColorA, sparkColorB, Random.value);
            p.startSize = Random.Range(sizeMin, sizeMax);
            p.startLifetime = lifetime * Random.Range(0.6f, 1.2f);
            _ps.Emit(p, 1);
        }
    }

    ParticleSystem MakeSystem()
    {
        var go = new GameObject($"DamageDragSparks ({name})");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.gravityModifier = 0f;
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        // Sparks bleed their speed fast — they skitter along the track and burn out.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch; // a streak, same as the impact sparks
        rend.velocityScale = 0.04f;
        rend.lengthScale = 2.5f;
        rend.material = ParticleFX.DefaultMaterial();
        rend.sortingOrder = sortingOrder;
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    void OnDestroy()
    {
        if (_ps != null) Destroy(_ps.gameObject);
    }
}
