using UnityEngine;

// Rear-tyre surface spray. While a car runs on a loose surface the rear tyres throw a plume of
// surface-coloured particles backwards — green/earth clods on grass, pale stones on gravel. Rate scales
// with speed and with how sideways the car is (a broadside slide roosts far more than a straight-line
// excursion). Tarmac runoff is dry and hard, so nothing is thrown there.
//
// Self-installed by PlayerVehicleController (its `surfaceSpray` toggle) on every car — player and AI —
// and reads the surface the controller already resolved this step, so there's no second SurfaceField query.
//
// Particles are emitted manually rather than by a shape module: a cone sprays in 3D, which in this
// top-down 2D view would drift clods along the camera's depth axis. Every velocity here stays in XY.
// The emitter lives at the scene root so a scaled car can't distort it.
[RequireComponent(typeof(PlayerVehicleController))]
public class TyreSurfaceParticles : MonoBehaviour
{
    [Header("Surface Colours")]
    [Tooltip("Torn turf — the green half of a grass roost.")]
    public Color grassColorA = new Color(0.25f, 0.37f, 0.13f);
    [Tooltip("Soil under the turf — the brown half of a grass roost.")]
    public Color grassColorB = new Color(0.29f, 0.21f, 0.12f);
    [Tooltip("Light stones in a gravel trap.")]
    public Color gravelColorA = new Color(0.70f, 0.65f, 0.55f);
    [Tooltip("Dark stones / dust in a gravel trap.")]
    public Color gravelColorB = new Color(0.44f, 0.40f, 0.34f);

    [Header("Emission")]
    [Tooltip("Particles per second per tyre at full spray.")]
    public float maxRate = 70f;
    [Tooltip("Speed (m/s) at which emission reaches maxRate.")]
    public float fullRateSpeed = 22f;
    [Tooltip("Below this speed (m/s) the tyres stop throwing anything.")]
    public float minSpeed = 1.5f;
    [Tooltip("Extra emission when fully sideways. 1 = no slide bonus, 2 = a broadside slide doubles the roost.")]
    [Range(1f, 4f)] public float slideBoost = 2f;
    [Tooltip("Body-slip angle (deg) counted as a full slide for the boost above.")]
    public float fullSlideDeg = 35f;
    [Tooltip("Lateral distance between the two tyre plumes (m).")]
    public float tyreTrackWidth = 1.6f;

    [Header("Particle Look")]
    [Tooltip("Seconds a clod lives before fading out.")]
    public float lifetime = 0.6f;
    [Tooltip("Throw speed range (m/s). Clods leave the tyre backwards at these speeds; the top of the range is only reached at fullRateSpeed.")]
    public float throwSpeedMin = 3f;
    public float throwSpeedMax = 10f;
    [Tooltip("Spray cone half-angle (deg) around the backwards throw direction.")]
    public float spreadDeg = 30f;
    public float sizeMin = 0.10f;
    public float sizeMax = 0.32f;
    [Tooltip("Sorting order. Tyre trails are 1 and the car body is 5 — sit between them.")]
    public int sortingOrder = 3;
    [Tooltip("Optional particle material. An unlit sprite material is created if left empty.")]
    public Material particleMaterial;
    [Tooltip("Particle budget for this car's spray.")]
    public int maxParticles = 400;

    PlayerVehicleController _car;
    ParticleSystem _ps;
    float _spawnAccum;   // fractional particles carried between steps so low rates still emit smoothly
    bool _leftTyre;      // alternates so the two tyres share the emission evenly

    void Start()
    {
        _car = GetComponent<PlayerVehicleController>();
        _ps = MakeSystem();
    }

    ParticleSystem MakeSystem()
    {
        var go = new GameObject($"TyreSpray ({name})");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles); // world-space spray, filed under the Particles bucket
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // clods stay where they land, not glued to the car
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = 0f;      // velocity is supplied per particle, in-plane
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f; // top-down view: "up" is off-screen, so gravity would look wrong
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission;
        em.enabled = false;        // emission is entirely manual, via Emit(EmitParams)
        var shape = ps.shape;
        shape.enabled = false;     // position is supplied per emit too

        // Clods lose speed to the air and settle instead of sailing off across the track.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.6f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = particleMaterial != null ? particleMaterial : ParticleFX.DefaultMaterial();
        rend.sortingOrder = sortingOrder;
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    void FixedUpdate()
    {
        if (_car == null || _ps == null) return;

        float speed = _car.SpeedMps;
        if (!_car.OnLooseSurface || speed <= minSpeed) { _spawnAccum = 0f; return; }

        // Rear tyres in world metres: back from the car centre along its heading, then out to each side.
        float hr = _car.HeadingDeg * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(hr), Mathf.Sin(hr));
        Vector2 right = new Vector2(fwd.y, -fwd.x);
        Vector2 rear = _car.RearAxleWorld;
        float z = transform.position.z;

        // Clods are flung opposite the car's actual TRAVEL, not its nose — a broadside car roosts sideways,
        // and that's what sells the slide.
        float slipRad = _car.SlipAngleDeg * Mathf.Deg2Rad;
        Vector2 travel = new Vector2(
            fwd.x * Mathf.Cos(slipRad) - fwd.y * Mathf.Sin(slipRad),
            fwd.x * Mathf.Sin(slipRad) + fwd.y * Mathf.Cos(slipRad));
        float baseAngle = Mathf.Atan2(-travel.y, -travel.x);

        float speedFactor = Mathf.Clamp01(speed / Mathf.Max(fullRateSpeed, 0.1f));
        float slide = Mathf.Clamp01(Mathf.Abs(_car.SlipAngleDeg) / Mathf.Max(fullSlideDeg, 1f));
        float ratePerTyre = maxRate * speedFactor * Mathf.Lerp(1f, slideBoost, slide);

        // Both tyres' worth of particles this step, alternating sides. The accumulator keeps fractional
        // rates smooth instead of quantising them to whole particles per FixedUpdate.
        _spawnAccum += ratePerTyre * 2f * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(_spawnAccum);
        if (count <= 0) return;
        _spawnAccum -= count;

        Color colA, colB;
        SurfaceColors(_car.CurrentSurface, out colA, out colB);
        float topThrow = Mathf.Lerp(throwSpeedMin, throwSpeedMax, speedFactor);

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            _leftTyre = !_leftTyre;
            Vector2 tyre = rear + right * (tyreTrackWidth * 0.5f) * (_leftTyre ? 1f : -1f);

            float ang = baseAngle + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
            float spd = Random.Range(throwSpeedMin, topThrow);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;

            p.position = new Vector3(tyre.x, tyre.y, z);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = Color.Lerp(colA, colB, Random.value);
            p.startSize = Random.Range(sizeMin, sizeMax);
            _ps.Emit(p, 1);
        }
    }

    void SurfaceColors(TrackEnvironment.SurfaceType surface, out Color a, out Color b)
    {
        switch (surface)
        {
            case TrackEnvironment.SurfaceType.Gravel: a = gravelColorA; b = gravelColorB; break;
            default: a = grassColorA; b = grassColorB; break; // Grass, and unclassified off-track
        }
    }

    void OnDestroy()
    {
        if (_ps != null) Destroy(_ps.gameObject);
    }
}
