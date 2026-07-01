using UnityEngine;

// Kinematic collider + post-move overlap resolution. Mobile-friendly: no dynamic rigidbody solver,
// just per-frame depenetration against barriers (EdgeCollider2D) and other vehicles.
// Runs after car controllers (execution order 300) so it corrects the final position each step.
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(Rigidbody2D))]
public class VehicleCollision : MonoBehaviour
{
    [Tooltip("Half-extents (m) of the car's box collider. x = half-width, y = half-length.")]
    public Vector2 halfExtents = new Vector2(1.0f, 2.4f);
    [Tooltip("Layers treated as collidable (barriers + other vehicles).")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Max colliders considered per step.")]
    public int maxContacts = 8;

    [Header("Damage")]
    [Tooltip("Closing speed (m/s) below which a contact does NO bodywork damage. Stops slow nudges from squashing a car.")]
    public float damageMinSpeed = 3f;
    [Tooltip("Closing speed (m/s) that produces a full-severity dent. Lower = harder-hitting damage at speed.")]
    public float damageSpeedFull = 18f;
    [Tooltip("Bounciness of car-vs-car impacts. 0 = cars shove/stick (max inertia transfer), 0.3 = lively rebound.")]
    [Range(0f, 0.6f)] public float vehicleRestitution = 0.1f;
    [Tooltip("Fraction of penetration corrected per step. 1 = fully ejected each step (no pass-through). Below ~0.9 a fast car can sink through a thin barrier before the push balances. Bounce/flex feel lives in the responder's restitution instead.")]
    [Range(0.1f, 1f)] public float positionalSoftness = 1f;
    [Tooltip("Log overlap diagnostics each second.")]
    public bool debugLog = false;

    BoxCollider2D _box;
    Rigidbody2D _rb;
    ICollisionResponder _responder;
    IDamageable _damage;
    readonly Collider2D[] _hits = new Collider2D[16];

    Vector2 _prevPos;
    Vector2 _vel;                 // world velocity from position delta (kinematic body)
    public Vector2 Velocity => _vel;

    // Fired when this car contacts a static barrier, with the closing speed (m/s). Lap timing
    // listens to invalidate the current lap on a wall hit.
    public event System.Action<float> BarrierHit;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;

        _box = GetComponent<BoxCollider2D>();
        if (_box == null) _box = gameObject.AddComponent<BoxCollider2D>();
        // Car forward = local +X. halfExtents.x = half-width, halfExtents.y = half-length → length maps to X.
        _box.size = new Vector2(halfExtents.y * 2f, halfExtents.x * 2f);

        _responder = PickResponder();
        _damage = GetComponentInChildren<IDamageable>();
        _prevPos = transform.position;
    }

    ICollisionResponder PickResponder()
    {
        var all = GetComponents<MonoBehaviour>();
        ICollisionResponder enabledNonSpline = null; // a free controller that actually owns the transform (e.g. PlayerVehicleController)
        ICollisionResponder enabledAny = null;       // any enabled responder (SplineDriver on AI cars)
        ICollisionResponder fallback = null;          // disabled responder, last resort
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is ICollisionResponder r)
            {
                if (all[i].enabled)
                {
                    enabledAny ??= r;
                    // SplineDriver's ApplyContact only nudges a spline-relative offset — useless for a free-driven
                    // car. When a non-spline controller is present and enabled, it owns depenetration.
                    if (!(all[i] is SplineDriver)) enabledNonSpline ??= r;
                }
                else fallback ??= r;
            }
        }
        return enabledNonSpline ?? enabledAny ?? fallback;
    }

    // Call after setting halfExtents at runtime (e.g. from GridSpawner) so the collider resizes.
    public void ApplyExtents()
    {
        if (_box == null) _box = GetComponent<BoxCollider2D>();
        if (_box == null) _box = gameObject.AddComponent<BoxCollider2D>();
        _box.size = new Vector2(halfExtents.y * 2f, halfExtents.x * 2f);
    }

    void FixedUpdate()
    {
        if (_box == null) return;

        // Track world velocity from the kinematic body's position delta (used for impact-speed damage).
        float dt = Time.fixedDeltaTime;
        Vector2 nowPos = transform.position;
        _vel = dt > 0f ? (nowPos - _prevPos) / dt : Vector2.zero;
        _prevPos = nowPos;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = collisionMask, useTriggers = false };
        int count = _box.Overlap(filter, _hits);

        if (debugLog && Time.frameCount % 50 == 0)
        {
            var sb = new System.Text.StringBuilder($"[VehicleCollision] {name}: overlaps={count} ");
            for (int j = 0; j < count; j++)
            {
                var o = _hits[j];
                if (o == null) continue;
                var dd = _box.Distance(o);
                sb.Append($"| {o.name} carPos={(Vector2)transform.position} ptOnCar={dd.pointA} ptOnBarrier={dd.pointB} dist={dd.distance:F2} n={dd.normal} ");
            }
            Debug.Log(sb.ToString(), this);
        }

        if (count <= 0) return;

        // Re-resolve each contact frame: the active controller (PlayerVehicleController) may be enabled AFTER our Awake,
        // so a responder cached at Awake goes stale (was picking the dormant SplineDriver, which can't move a free car).
        _responder = PickResponder();

        int processed = 0;
        for (int i = 0; i < count && processed < maxContacts; i++)
        {
            var other = _hits[i];
            if (other == null || other.attachedRigidbody == _rb) continue;

            ColliderDistance2D d = _box.Distance(other);
            if (!d.isOverlapped) continue;

            // Soft push: only correct a fraction per step so the car flexes into the barrier then eases out.
            // (Depenetration stays penetration-based — that's positional correctness, not damage.)
            Vector2 pushWorld = -d.normal * Mathf.Abs(d.distance) * positionalSoftness;

            // Damage scales with CLOSING SPEED, not penetration depth, so a slow sustained nudge does nothing
            // while a real impact dents hard. d.normal points along the contact; relVel·normal = approach rate.
            var otherResponder = other.GetComponent<ICollisionResponder>();
            var otherVC = other.GetComponent<VehicleCollision>();
            Vector2 otherVel = otherVC != null ? otherVC.Velocity : Vector2.zero;
            float closingSpeed = Mathf.Max(0f, Vector2.Dot(_vel - otherVel, d.normal));
            float severity = Mathf.Clamp01(closingSpeed / Mathf.Max(damageSpeedFull, 0.1f));

            // Dent bodywork at the contact point — only for genuine impacts above the threshold.
            if (closingSpeed >= damageMinSpeed)
                _damage?.OnImpact(d.pointB, -d.normal, severity);

            if (otherResponder != null)
            {
                // Vehicle-vehicle. Positional depenetration is split by inverse mass (heavier car barely moves),
                // and a proper 2-body impulse along the contact normal transfers momentum: a fast car shunts a
                // stationary one forward and sheds its own speed, instead of both bouncing off like walls.
                float mA = _responder != null ? _responder.Mass : 1500f;
                float mB = otherResponder.Mass;
                float total = Mathf.Max(mA + mB, 1f);

                Vector2 n = d.normal; // points from this car (A) toward the other (B)
                float vn = Vector2.Dot(_vel - otherVel, n); // >0 = closing along the normal
                Vector2 dvA = Vector2.zero, dvB = Vector2.zero;
                if (vn > 0f)
                {
                    float invSum = (1f / mA) + (1f / mB);
                    float j = (1f + vehicleRestitution) * vn / Mathf.Max(invSum, 1e-4f);
                    dvA = -(j / mA) * n; // A slows / rebounds
                    dvB = (j / mB) * n;  // B is shunted forward
                }

                _responder?.ApplyCarImpact(pushWorld * (mB / total), d.pointA, dvA, severity);
                otherResponder.ApplyCarImpact(-pushWorld * (mA / total), d.pointB, dvB, severity);
            }
            else
            {
                // Barrier (static): full correction on us. d.pointA is the contact on our body → lever arm for spin.
                _responder?.ApplyContact(pushWorld, d.pointA, severity);
                BarrierHit?.Invoke(closingSpeed);
            }
            processed++;
        }
    }

    void OnValidate()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.size = new Vector2(halfExtents.y * 2f, halfExtents.x * 2f);
    }
}
