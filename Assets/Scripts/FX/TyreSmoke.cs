using UnityEngine;

// Rear-tyre smoke while the wheels are spinning — donuts and hot launches. Puffs leave each rear tyre at a
// rate set by PlayerVehicleController.Wheelspin, swell as they drift, and thin out; a held donut leaves a
// ring of it hanging where the tail has been.
//
// Self-installed by PlayerVehicleController (its `tyreSmoke` toggle), same as TyreSurfaceParticles, and
// emitted manually for the same reason: every velocity stays in the XY plane of the top-down view. The
// particle system is only built the first time the tyres light up, so AI cars — which never spin their
// wheels — never pay for one.
[RequireComponent(typeof(PlayerVehicleController))]
public class TyreSmoke : MonoBehaviour
{
    [Tooltip("Smoke colour. Alpha is the density of a fresh puff.")]
    public Color smokeColor = new Color(0.86f, 0.86f, 0.84f, 0.55f);
    [Tooltip("Puffs per second per tyre at full wheelspin.")]
    public float maxRate = 45f;
    [Tooltip("Wheelspin (0..1) below which nothing is emitted — a trickle of spin shouldn't fog the car.")]
    [Range(0f, 1f)] public float minWheelspin = 0.08f;
    [Tooltip("Seconds a puff lives.")]
    public float lifetime = 1.4f;
    [Tooltip("Puff size when it leaves the tyre, and how many times bigger it grows before fading (m).")]
    public float sizeMin = 0.5f;
    public float sizeMax = 0.9f;
    public float growth = 3f;
    [Tooltip("Drift speed range (m/s). Puffs roll off the tyre backwards and sideways, then slow to a hang.")]
    public float driftMin = 0.4f;
    public float driftMax = 2.2f;
    [Tooltip("Lateral distance between the two rear tyres (m).")]
    public float tyreTrackWidth = 1.6f;
    [Tooltip("Sorting order. The car body is 5 — smoke sits over it, as it would from above.")]
    public int sortingOrder = 6;
    public int maxParticles = 350;

    PlayerVehicleController _car;
    ParticleSystem _ps;
    float _spawnAccum;
    bool _leftTyre;

    void Start() => _car = GetComponent<PlayerVehicleController>();

    ParticleSystem MakeSystem()
    {
        var go = new GameObject($"TyreSmoke ({name})");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // smoke hangs where it was laid
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.08f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.3f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(growth, AnimationCurve.EaseInOut(0f, 1f / Mathf.Max(growth, 1f), 1f, 1f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = ParticleFX.DefaultMaterial();
        rend.sortingOrder = sortingOrder;
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    void FixedUpdate()
    {
        if (_car == null) return;
        float spin = _car.Wheelspin;
        if (spin < minWheelspin) { _spawnAccum = 0f; return; }
        if (_ps == null) _ps = MakeSystem();

        float hr = _car.HeadingDeg * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(hr), Mathf.Sin(hr));
        Vector2 right = new Vector2(fwd.y, -fwd.x);
        Vector2 rear = _car.RearAxleWorld;
        float z = transform.position.z;

        _spawnAccum += maxRate * spin * 2f * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(_spawnAccum);
        if (count <= 0) return;
        _spawnAccum -= count;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            _leftTyre = !_leftTyre;
            float side = _leftTyre ? 1f : -1f;
            Vector2 tyre = rear + right * (tyreTrackWidth * 0.5f) * side;

            // Thrown back off the spinning tread and out past the tyre wall.
            Vector2 dir = (-fwd + right * side * Random.Range(0.2f, 0.9f)).normalized;
            Vector2 v = dir * Random.Range(driftMin, driftMax) * Mathf.Lerp(0.6f, 1f, spin);

            p.position = new Vector3(tyre.x, tyre.y, z);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = smokeColor * new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, spin));
            p.startSize = Random.Range(sizeMin, sizeMax);
            _ps.Emit(p, 1);
        }
    }

    void OnDestroy()
    {
        if (_ps != null) Destroy(_ps.gameObject);
    }
}
