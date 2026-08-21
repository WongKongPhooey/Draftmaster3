using UnityEngine;

// Contact FX. Listens to VehicleCollision.Contacted and throws two kinds of particle at the contact point:
//
//   Sparks  — bright, short-lived, fast. Fire on any real contact AND while scraping along a barrier
//             (grinding down the wall has almost no closing speed but should still light up).
//   Debris  — bodywork shed by the car: darker, bigger, tumbling, slower. Only on genuine impacts, so a
//             gentle nudge doesn't shower the track with panels.
//
// Both burst OUT of the obstacle (along -normal) in a cone, and carry a share of the car's own motion so
// the shower trails the way the car was going. Self-installed by PlayerVehicleController (`impactDebris`)
// on player and AI cars alike. Emitters live at the scene root so a scaled car can't distort them.
public class ImpactParticles : MonoBehaviour
{
    [Header("Trigger Thresholds")]
    [Tooltip("Closing speed (m/s) below which a contact makes no sparks. Stops parked-car jostling from sparking.")]
    public float sparkMinClosingSpeed = 1.5f;
    [Tooltip("Scrape speed (m/s) along a wall needed to strike sparks with no real impact behind it.")]
    public float scrapeMinSpeed = 5f;
    [Tooltip("Severity (0..1) needed before the car sheds bodywork debris.")]
    [Range(0f, 1f)] public float debrisMinSeverity = 0.12f;
    [Tooltip("Seconds between spark bursts. Low = a near-continuous stream while scraping.")]
    public float sparkInterval = 0.04f;
    [Tooltip("Seconds between debris bursts, so a sustained crunch sheds panels in bursts rather than a fire hose.")]
    public float debrisInterval = 0.2f;
    [Tooltip("Spark output from car-vs-car contact, relative to hitting a barrier.")]
    [Range(0f, 1f)] public float carContactSparkScale = 0.6f;

    [Header("Sparks")]
    public int sparkCountMax = 14;
    public Color sparkColorA = new Color(1f, 0.95f, 0.65f);
    public Color sparkColorB = new Color(1f, 0.55f, 0.12f);
    public float sparkSpeedMin = 6f;
    public float sparkSpeedMax = 20f;
    public float sparkLifetime = 0.28f;
    public float sparkSize = 0.10f;
    [Tooltip("Spark cone half-angle (deg) around the rebound direction.")]
    public float sparkSpreadDeg = 45f;

    [Header("Debris")]
    public int debrisCountMax = 12;
    [Tooltip("Bodywork shards — leave grey for generic panels, or tint toward the car's livery.")]
    public Color debrisColorA = new Color(0.72f, 0.72f, 0.74f);
    public Color debrisColorB = new Color(0.28f, 0.28f, 0.30f);
    public float debrisSpeedMin = 2.5f;
    public float debrisSpeedMax = 9f;
    public float debrisLifetime = 1.1f;
    public float debrisSizeMin = 0.12f;
    public float debrisSizeMax = 0.34f;
    [Tooltip("Debris cone half-angle (deg) around the rebound direction.")]
    public float debrisSpreadDeg = 70f;
    [Tooltip("Fraction of the car's own velocity the shower inherits, so debris trails behind the car.")]
    [Range(0f, 1f)] public float inheritVelocity = 0.35f;

    // The tuned Spark/Debris numbers above describe the HARDEST contact (intensity/severity 1). Everything
    // below that is scaled down by these three, so a slow nudge coughs out a few short-lived flecks instead
    // of the same metre-wide shower a 60 m/s shunt produces. Leave all three at 1 for the old flat response.
    [Header("Speed Response")]
    [Tooltip("Ejection-speed multiplier at the weakest contact that still shows anything. Scales how FAR particles are thrown.")]
    [Range(0.05f, 1f)] public float weakSpeedScale = 0.25f;
    [Tooltip("Lifetime multiplier at the weakest contact. Combines with the speed scale, so reach falls off faster than speed alone.")]
    [Range(0.1f, 1f)] public float weakLifetimeScale = 0.45f;
    [Tooltip("Particle-size multiplier at the weakest contact, so a light scuff throws smaller flecks.")]
    [Range(0.1f, 1f)] public float weakSizeScale = 0.6f;
    [Tooltip("Shapes the ramp from weakest to hardest contact. >1 keeps low-speed contacts subdued for longer.")]
    [Range(0.25f, 4f)] public float responseExponent = 1.35f;

    [Header("Render")]
    [Tooltip("Sorting order. Sparks and debris sit above the car body (5) — they're in front of it.")]
    public int sortingOrder = 7;
    public Material particleMaterial;

    VehicleCollision _collision;
    ParticleSystem _sparks, _debris;
    float _nextSpark, _nextDebris;

    void Start()
    {
        _collision = GetComponent<VehicleCollision>();
        if (_collision == null)
        {
            // No collider on this car — nothing will ever report a contact.
            enabled = false;
            return;
        }

        _sparks = MakeSystem("ImpactSparks", sparkLifetime, sparkSize, sparkSize, false);
        _debris = MakeSystem("ImpactDebris", debrisLifetime, debrisSizeMin, debrisSizeMax, true);
        _collision.Contacted += OnContact;
    }

    void OnDisable()
    {
        if (_collision != null) _collision.Contacted -= OnContact;
    }

    void OnEnable()
    {
        // Re-subscribe after a disable (Start already subscribed on first enable).
        if (_collision != null) { _collision.Contacted -= OnContact; _collision.Contacted += OnContact; }
    }

    ParticleSystem MakeSystem(string name, float lifetime, float sizeMin, float sizeMax, bool tumble)
    {
        var go = new GameObject(name);
        RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles); // stays off the car, but out of the scene root
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // the shower stays at the impact, not on the car
        main.startLifetime = lifetime;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = 0f;      // every particle's velocity is supplied per-emit
        main.gravityModifier = 0f; // top-down view
        main.maxParticles = 400;
        main.playOnAwake = false;

        var em = ps.emission;
        em.enabled = false;        // emission is entirely manual, via Emit(EmitParams)

        var shape = ps.shape;
        shape.enabled = false;     // position is supplied per-emit too

        // Air drag: sparks die back almost instantly, debris skitters a bit further.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = tumble ? 0.06f : 0.14f;
        limit.limit = new ParticleSystem.MinMaxCurve(tumble ? 1.5f : 0.5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, tumble ? 0.6f : 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        // Sparks shrink to nothing (they burn out); debris keeps most of its size (it's a solid lump).
        size.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, tumble ? 0.75f : 0.1f));

        if (tumble)
        {
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        }

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = tumble ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
        if (!tumble)
        {
            // Sparks stretch along their own velocity — reads as a streak rather than a dot.
            rend.velocityScale = 0.04f;
            rend.lengthScale = 2.5f;
        }
        rend.material = particleMaterial != null ? particleMaterial : ParticleFX.DefaultMaterial();
        rend.sortingOrder = sortingOrder;
        rend.alignment = ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    void OnContact(VehicleCollision.ContactEvent c)
    {
        if (_sparks == null) return;
        float now = Time.time;

        // Rebound direction: out of whatever we hit, i.e. back toward us.
        Vector2 outward = c.normal.sqrMagnitude > 1e-6f ? -c.normal.normalized : Vector2.up;
        float z = transform.position.z;
        Vector2 carVel = _collision != null ? _collision.Velocity : Vector2.zero;

        // Intensity blends a head-on hit with a wall scrape, so both a heavy shunt and a long grind spark.
        float hit = Mathf.Clamp01((c.closingSpeed - sparkMinClosingSpeed) / 8f);
        float scrape = Mathf.Clamp01((c.scrapeSpeed - scrapeMinSpeed) / 15f);
        float intensity = Mathf.Clamp01(Mathf.Max(hit, scrape * 0.8f));
        if (c.otherIsCar) intensity *= carContactSparkScale;

        if (intensity > 0.01f && now >= _nextSpark)
        {
            _nextSpark = now + sparkInterval;
            int count = Mathf.Max(1, Mathf.RoundToInt(sparkCountMax * intensity));
            // Sparks fly out of the contact but are dragged along by the scrape — a car grinding the wall
            // leaves its sparks behind it, not in a neat fan.
            Vector2 bias = carVel * (inheritVelocity * 0.5f);
            Response(intensity, out float spd, out float life, out float sz);
            Burst(_sparks, c.point, z, outward, bias, count, sparkSpreadDeg,
                sparkSpeedMin * spd, sparkSpeedMax * spd, sparkLifetime * life,
                sparkColorA, sparkColorB, sparkSize * sz, sparkSize * sz);
        }

        if (c.severity >= debrisMinSeverity && now >= _nextDebris)
        {
            _nextDebris = now + debrisInterval;
            float sev = Mathf.Clamp01(c.severity);
            int count = Mathf.Max(1, Mathf.RoundToInt(debrisCountMax * sev));
            Response(sev, out float spd, out float life, out float sz);
            Burst(_debris, c.point, z, outward, carVel * inheritVelocity, count, debrisSpreadDeg,
                debrisSpeedMin * spd, debrisSpeedMax * spd, debrisLifetime * life,
                debrisColorA, debrisColorB, debrisSizeMin * sz, debrisSizeMax * sz);
        }
    }

    // Map a 0..1 contact strength onto the three multipliers that make the burst read as harder or softer.
    // Both ends of each speed range move together — scaling only the top end (what this used to do) left the
    // slowest particles as fast as they were in a full shunt, so every contact threw its shower the same
    // distance no matter how gently the car touched. Speed sets throw velocity and lifetime sets how long
    // they keep it, so reach scales roughly with the product of the two.
    void Response(float t, out float speedScale, out float lifetimeScale, out float sizeScale)
    {
        float k = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.01f, responseExponent));
        speedScale = Mathf.Lerp(weakSpeedScale, 1f, k);
        lifetimeScale = Mathf.Lerp(weakLifetimeScale, 1f, k);
        sizeScale = Mathf.Lerp(weakSizeScale, 1f, k);
    }

    // Emit `count` particles from `point`, fanned within `spreadDeg` of `dir`, plus an inherited velocity bias.
    // startLifetime is supplied per burst (not read from the system's main module) so a soft contact's
    // particles die back early instead of coasting out to the full-impact distance.
    static void Burst(ParticleSystem ps, Vector2 point, float z, Vector2 dir, Vector2 bias, int count,
                      float spreadDeg, float speedMin, float speedMax, float lifetime, Color colA, Color colB,
                      float sizeMin, float sizeMax)
    {
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        float baseAngle = Mathf.Atan2(dir.y, dir.x);
        for (int i = 0; i < count; i++)
        {
            float ang = baseAngle + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
            float spd = Random.Range(speedMin, speedMax);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd + bias;

            p.position = new Vector3(point.x, point.y, z);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startColor = Color.Lerp(colA, colB, Random.value);
            p.startSize = Random.Range(sizeMin, sizeMax);
            p.startLifetime = Mathf.Max(0.02f, lifetime * Random.Range(0.8f, 1.15f));
            ps.Emit(p, 1);
        }
    }

    void OnDestroy()
    {
        if (_sparks != null) Destroy(_sparks.gameObject);
        if (_debris != null) Destroy(_debris.gameObject);
    }
}
