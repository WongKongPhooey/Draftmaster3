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
    [Tooltip("Log overlap diagnostics each second.")]
    public bool debugLog = false;

    BoxCollider2D _box;
    Rigidbody2D _rb;
    ICollisionResponder _responder;
    readonly Collider2D[] _hits = new Collider2D[16];

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
    }

    ICollisionResponder PickResponder()
    {
        var all = GetComponents<MonoBehaviour>();
        ICollisionResponder fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is ICollisionResponder r)
            {
                if (all[i].enabled) return r; // prefer the active controller
                fallback ??= r;
            }
        }
        return fallback;
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

        int processed = 0;
        for (int i = 0; i < count && processed < maxContacts; i++)
        {
            var other = _hits[i];
            if (other == null || other.attachedRigidbody == _rb) continue;

            ColliderDistance2D d = _box.Distance(other);
            if (!d.isOverlapped) continue;

            Vector2 mtv = d.normal * d.distance; // distance negative when overlapped; normal points from other to us
            // d.normal points from collider B (other) toward A (this) when overlapped; push us out fully.
            Vector2 pushWorld = -d.normal * Mathf.Abs(d.distance);

            float severity = Mathf.Clamp01(Mathf.Abs(d.distance) / Mathf.Max(halfExtents.x, 0.1f));

            var otherResponder = other.GetComponent<ICollisionResponder>();
            if (otherResponder != null)
            {
                // Vehicle-vehicle: split correction.
                _responder?.ApplyContact(pushWorld * 0.5f, severity);
                otherResponder.ApplyContact(-pushWorld * 0.5f, severity);
            }
            else
            {
                // Barrier (static): full correction on us.
                _responder?.ApplyContact(pushWorld, severity);
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
