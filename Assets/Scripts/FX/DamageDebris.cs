using UnityEngine;
using Draftmaster.Sim;

// Bits falling off a damaged car. Every time the bodywork dents (VehicleDamage.Dented) a few small pieces
// — flakes of the car's paint, dark splinters of fibreglass and plastic — drop off it where the metal moved,
// and are left lying on the track as the car drives on. The panel is knocked loose by the hit, so it keeps
// shaking the odd bit off for a few seconds afterwards while the car is moving, dying away as it settles.
//
// Not the same thing as ImpactParticles' debris, which is a shower thrown OUT of the contact by the force of
// the hit. These leave the car at the car's own speed and fall: a touch bigger as they come off (nearer the
// camera), shrinking as they drop to the asphalt, then sliding to rest and fading.
//
// Self-installed by PlayerVehicleController (its `damageDebris` toggle) on player and AI cars alike, emitted
// manually like DamageDragSparks so every velocity stays in the top-down plane. The particle system is only
// built the first time a car is dented, so the field of clean cars never pays for one.
[RequireComponent(typeof(PlayerVehicleController))]
public class DamageDebris : MonoBehaviour
{
    [Header("Bits Knocked Off")]
    [Tooltip("Pieces per unit of DentAmount gained. A hit that grows the denting by 0.05 sheds a tenth of this.")]
    public float piecesPerDent = 260f;
    [Tooltip("Most pieces one dent knocks off at once.")]
    public int maxBurst = 18;

    [Header("Loose Panel Trickle")]
    [Tooltip("How loose a dent leaves the panel, per unit of DentAmount gained (looseness caps at 1).")]
    public float loosenPerDent = 8f;
    [Tooltip("Seconds for the panel to settle to half as loose.")]
    public float settleHalfLife = 1.2f;
    [Tooltip("Pieces per second a fully loose panel sheds at full speed.")]
    public float maxTrickleRate = 14f;
    [Tooltip("Speed (m/s) below which a loose panel doesn't shake bits off.")]
    public float minSpeed = 3f;
    [Tooltip("Speed (m/s) at which the trickle reaches its full rate.")]
    public float fullSpeed = 35f;

    [Header("Pieces")]
    [Tooltip("Share of the pieces in the car's own paint; the rest are dark fibreglass, plastic and metal.")]
    [Range(0f, 1f)] public float paintShare = 0.55f;
    public Color shardColorA = new Color(0.62f, 0.62f, 0.64f);
    public Color shardColorB = new Color(0.16f, 0.16f, 0.18f);
    [Tooltip("Fraction of the car's velocity a piece leaves with. Under 1, so it falls behind the car.")]
    [Range(0f, 1f)] public float inheritMin = 0.7f;
    [Range(0f, 1f)] public float inheritMax = 0.95f;
    [Tooltip("Random sideways scatter (m/s) as a piece comes away.")]
    public float scatterSpeed = 2.5f;
    [Tooltip("Spread (m) around the dent the pieces come away from.")]
    public float spawnRadius = 0.35f;
    public float lifetime = 1.6f;
    public float sizeMin = 0.05f;
    public float sizeMax = 0.12f;
    [Tooltip("How much bigger a piece looks as it leaves the car than once it has dropped onto the track.")]
    public float fallScale = 1.5f;
    [Tooltip("Sorting order. The car body is 5 — the pieces come off its top surface, so they start above it.")]
    public int sortingOrder = 6;
    public int maxParticles = 300;

    PlayerVehicleController _car;
    VehicleDamage _bodywork;
    ParticleSystem _ps;
    Vector2 _lastPos, _vel;
    bool _havePos;
    float _burstCarry, _trickleAccum, _loose, _nextBind;
    // Where the last dent landed, in the car's own frame so the trickle follows the car.
    Vector3 _looseLocal;
    Color _paintA = Color.white, _paintB = Color.grey;

    void Start()
    {
        _car = GetComponent<PlayerVehicleController>();
        Bind();
    }

    // The bodywork can be added after this (spawners build it onto the sprite), so look again now and then.
    void Bind()
    {
        _nextBind = Time.time + 1f;
        _bodywork = GetComponentInChildren<VehicleDamage>();
        if (_bodywork != null) _bodywork.Dented += OnDented;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 pos = transform.position;
        _vel = _havePos && dt > 0f ? (pos - _lastPos) / dt : Vector2.zero;
        _lastPos = pos;
        _havePos = true;

        if (_bodywork == null && Time.time >= _nextBind) Bind();
        if (_loose <= 0f) return;
        // A repair straightens the panels and bolts everything back on.
        if (_bodywork == null || _bodywork.DentAmount <= 0.001f) { _loose = 0f; _trickleAccum = 0f; return; }

        float rate = BodyworkDebris.TrickleRate(_loose, _car != null ? _car.SpeedMps : _vel.magnitude,
                                            minSpeed, fullSpeed, maxTrickleRate);
        _loose = BodyworkDebris.Settle(_loose, dt, settleHalfLife);
        if (_loose < 0.01f) _loose = 0f;
        if (rate <= 0f) { _trickleAccum = 0f; return; }

        _trickleAccum += rate * dt;
        int count = Mathf.FloorToInt(_trickleAccum);
        if (count <= 0) return;
        _trickleAccum -= count;
        Shed(transform.TransformPoint(_looseLocal), count);
    }

    void OnDented(Vector2 worldPoint, float dentGained)
    {
        _looseLocal = transform.InverseTransformPoint(new Vector3(worldPoint.x, worldPoint.y, transform.position.z));
        _loose = BodyworkDebris.Loosen(_loose, dentGained, loosenPerDent);

        int count = BodyworkDebris.Pieces(ref _burstCarry, dentGained, piecesPerDent, maxBurst);
        if (count <= 0) return;
        // The paint a car is wearing can change mid-race (a team switch), so read it at the hit.
        CarColours.For(gameObject, out _paintA, out _paintB);
        Shed(worldPoint, count);
    }

    void Shed(Vector2 at, int count)
    {
        if (_ps == null) _ps = MakeSystem();
        float z = transform.position.z;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            Vector2 v = _vel * Random.Range(inheritMin, inheritMax) + Random.insideUnitCircle * scatterSpeed;
            Vector2 from = at + Random.insideUnitCircle * spawnRadius;

            p.position = new Vector3(from.x, from.y, z);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = Random.value < paintShare
                ? (Random.value < 0.7f ? _paintA : _paintB)
                : Color.Lerp(shardColorA, shardColorB, Random.value);
            p.startSize = Random.Range(sizeMin, sizeMax);
            p.startLifetime = lifetime * Random.Range(0.7f, 1.2f);
            _ps.Emit(p, 1);
        }
    }

    ParticleSystem MakeSystem()
    {
        var go = new GameObject($"DamageDebris ({name})");
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // left on the track, not carried by the car
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.gravityModifier = 0f; // top-down: the fall is told by the shrink, not by motion
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        // Once a piece lands it scrapes along the asphalt and stops.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.08f;
        limit.limit = new ParticleSystem.MinMaxCurve(0f);

        // Shrinks from fallScale to its ground size over the first fifth of its life — dropping away from
        // the camera onto the track.
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        float k = Mathf.Max(1f, fallScale);
        var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 1f / k), new Keyframe(1f, 1f / k));
        size.size = new ParticleSystem.MinMaxCurve(k, curve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = ParticleFX.DefaultMaterial();
        rend.sortingOrder = sortingOrder;
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    void OnDestroy()
    {
        if (_bodywork != null) _bodywork.Dented -= OnDented;
        if (_ps != null) Destroy(_ps.gameObject);
    }
}
