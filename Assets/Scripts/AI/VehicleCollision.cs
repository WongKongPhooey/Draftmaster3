using UnityEngine;

// Kinematic collider + post-move overlap resolution. Mobile-friendly: no dynamic rigidbody solver,
// just per-frame depenetration against barriers (EdgeCollider2D) and other vehicles.
// Runs after car controllers (execution order 300) so it corrects the final position each step.
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(Rigidbody2D))]
public class VehicleCollision : MonoBehaviour
{
    [Tooltip("Half-extents (m) of the car's box collider. x = half-width, y = half-length. Overridden by the " +
             "bodywork's own size when fitToBodywork is on and the car has one.")]
    public Vector2 halfExtents = new Vector2(1.25f, 2.5f);
    [Tooltip("Size the box to the car's bodywork (VehicleDamage's sprite), so cars touch where the paint does. " +
             "The old fixed 4.8 x 2.0 m box sat 0.25 m inside each side of the 5 x 2.5 m art, so two cars side by " +
             "side were half a metre into each other before they made contact.")]
    public bool fitToBodywork = true;
    [Tooltip("Layers treated as collidable (barriers + other vehicles).")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Max colliders considered per step.")]
    public int maxContacts = 8;

    [Header("Damage")]
    [Tooltip("No bodywork damage before the green flag. The field jostles on the way to the line and the AI are not clean enough about it yet, so a race could begin with half the grid already bent. Contact still pushes cars around — this is the metal, not the physics.")]
    public bool noDamageBeforeGreen = true;
    [Tooltip("Closing speed (m/s) below which a contact does NO bodywork damage. Stops slow nudges from squashing a car.")]
    public float damageMinSpeed = 3f;
    [Tooltip("Closing speed (m/s) that produces a full-severity dent. Lower = harder-hitting damage at speed.")]
    public float damageSpeedFull = 18f;
    [Tooltip("Bounciness of car-vs-car impacts. 0 = cars shove/stick (max inertia transfer), 0.3 = lively rebound.")]
    [Range(0f, 0.6f)] public float vehicleRestitution = 0.1f;
    [Tooltip("Fraction of penetration corrected per step. 1 = fully ejected each step (no pass-through). Below ~0.9 a fast car can sink through a thin barrier before the push balances. Bounce/flex feel lives in the responder's restitution instead.")]
    [Range(0.1f, 1f)] public float positionalSoftness = 1f;
    [Tooltip("Shorten the collider as the nose and tail fold back, so the car is touched where its crushed " +
             "metal is rather than where the undamaged bodywork used to be. Off leaves a gap in front of a " +
             "crumpled bonnet on every contact it makes afterwards.")]
    public bool colliderFollowsCrush = true;
    [Tooltip("The shortest the collider can get, as a fraction of its undamaged length — the safety cell.")]
    [Range(0.3f, 1f)] public float minCrushedLengthFrac = 0.6f;
    [Tooltip("Log overlap diagnostics each second.")]
    public bool debugLog = false;

    BoxCollider2D _box;
    Rigidbody2D _rb;
    ICollisionResponder _responder;
    IDamageable _damage;
    VehicleDamage _bodywork;
    Vector2 _baseOffset;
    // The box's offset before any crush shifted it. DuctTapePatches keeps tape inside the undamaged box.
    public Vector2 BaseOffset => _baseOffset;
    readonly Collider2D[] _hits = new Collider2D[16];

    Vector2 _prevPos;
    Vector2 _vel;                 // world velocity from position delta (kinematic body)
    public Vector2 Velocity => _vel;

    // Fired when this car contacts a static barrier, with the closing speed (m/s). Lap timing
    // listens to invalidate the current lap on a wall hit.
    public event System.Action<float> BarrierHit;
    // The same, for every car at once: (car, closing speed m/s). CautionWatch listens to put a yellow out the
    // moment a car up the road finds the wall, without having to subscribe car by car.
    public static event System.Action<Transform, float> AnyBarrierHit;

    // One resolved contact, in world space. Fired for barriers AND cars every step the overlap persists,
    // so listeners that shouldn't repeat (bursts, sounds) must rate-limit themselves. ImpactParticles reads this.
    public struct ContactEvent
    {
        public Vector2 point;       // contact point on OUR body
        public Vector2 normal;      // points from us toward the obstacle
        public float closingSpeed;  // m/s along the normal — how hard we drove into it
        public float scrapeSpeed;   // m/s tangent to the contact — how fast we're sliding along it
        public float severity;      // 0..1, closingSpeed normalised by damageSpeedFull
        public bool otherIsCar;     // false = static barrier
    }
    public event System.Action<ContactEvent> Contacted;

    // Everything before the green flag: parked on the grid, and the lap behind the pace car. Bodywork does
    // not fold and nobody takes it personally — an AI field that shuffles into itself on the way round would
    // otherwise start the race dented and already feuding. A caution is the race, not before it: damage counts.
    bool DamageSuppressed => noDamageBeforeGreen && !RaceStart.IsRunning;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;

        _box = GetComponent<BoxCollider2D>();
        if (_box == null) _box = gameObject.AddComponent<BoxCollider2D>();
        _baseOffset = _box.offset;

        _responder = PickResponder();
        _damage = GetComponentInChildren<IDamageable>();
        _bodywork = _damage as VehicleDamage;

        FitToBodywork();
        // Car forward = local +X. halfExtents.x = half-width, halfExtents.y = half-length → length maps to X.
        _box.size = new Vector2(halfExtents.y * 2f, halfExtents.x * 2f);
        _prevPos = transform.position;
    }

    void OnEnable()  { if (_bodywork != null) _bodywork.BodyChanged += ApplyBoxShape; PerfLog.CarsActive++; _hasSafePos = false; }
    void OnDisable() { if (_bodywork != null) _bodywork.BodyChanged -= ApplyBoxShape; PerfLog.CarsActive--; }

    // The box is the undamaged car less however far each end has been folded back. See BodyDeform.CrushedSpan.
    void ApplyBoxShape()
    {
        if (_box == null) return;
        FitToBodywork();
        float length = halfExtents.y * 2f;
        float front = 0f, rear = 0f;
        if (colliderFollowsCrush && _bodywork != null)
        {
            _bodywork.EndCrush(transform.right, out front, out rear);
            // EndCrush answers in world metres; the box is sized in this transform's local units.
            float toLocal = 1f / Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));
            front *= toLocal;
            rear *= toLocal;
        }
        float span = Draftmaster.Sim.BodyDeform.CrushedSpan(length, front, rear, minCrushedLengthFrac, out float shift);
        _box.size = new Vector2(span, halfExtents.x * 2f);
        _box.offset = _baseOffset + new Vector2(shift, 0f);
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

    // The box is the bodywork's own footprint: the sprite the dents are folded into, in this transform's frame
    // (the livery lies along the car, so sprite x is the length). Everything that reads halfExtents - grid
    // spacing, pit boxes, retirement parking - then sees the car's real size too.
    void FitToBodywork()
    {
        if (!fitToBodywork) return;
        if (_bodywork == null) _bodywork = GetComponentInChildren<VehicleDamage>();
        if (_bodywork == null || _bodywork.sourceSprite == null) return;
        Vector2 size = _bodywork.sourceSprite.bounds.size;
        Vector3 bodyScale = _bodywork.transform.lossyScale, ownScale = transform.lossyScale;
        float sx = Mathf.Abs(bodyScale.x) / Mathf.Max(1e-4f, Mathf.Abs(ownScale.x));
        float sy = Mathf.Abs(bodyScale.y) / Mathf.Max(1e-4f, Mathf.Abs(ownScale.y));
        halfExtents = new Vector2(size.y * sy * 0.5f, size.x * sx * 0.5f);
    }

    // Call after setting halfExtents at runtime (e.g. from GridSpawner) so the collider resizes.
    public void ApplyExtents()
    {
        if (_box == null) _box = GetComponent<BoxCollider2D>();
        if (_box == null) _box = gameObject.AddComponent<BoxCollider2D>();
        ApplyBoxShape();
    }

    void FixedUpdate()
    {
        if (_box == null) return;

        // Track world velocity from the kinematic body's position delta (used for impact-speed damage).
        float dt = Time.fixedDeltaTime;
        Vector2 nowPos = transform.position;
        Vector2 lastPlaced = _prevPos;
        _vel = dt > 0f ? (nowPos - _prevPos) / dt : Vector2.zero;
        _prevPos = nowPos;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = collisionMask, useTriggers = false };
        _filter = filter;
        _jumped = Jumped(nowPos, lastPlaced);
        StopCrossing(filter);
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

        if (count <= 0) { NoteSafePosition(); return; }

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

            var otherResponder = other.GetComponent<ICollisionResponder>();

            // People are not walls. Anything carrying its own rigidbody but no vehicle responder — the
            // on-foot player, paddock NPCs, pit crew — would otherwise fall through to the barrier branch
            // below and shove 1.5 tonnes of parked car aside at walking pace. Skipping it leaves the pair
            // to Unity's own solver, which pushes the person out of this kinematic body and never the
            // other way round: a parked car is immovable on foot, and a moving one runs you over.
            // Barriers and scenery (static colliders, no rigidbody) still push the car as before.
            if (otherResponder == null && other.attachedRigidbody != null &&
                other.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;

            // Soft push: only correct a fraction per step so the car flexes into the barrier then eases out.
            // (Depenetration stays penetration-based — that's positional correctness, not damage.)
            Vector2 pushWorld = -d.normal * Mathf.Abs(d.distance) * positionalSoftness;

            // Damage scales with CLOSING SPEED, not penetration depth, so a slow sustained nudge does nothing
            // while a real impact dents hard. d.normal points along the contact; relVel·normal = approach rate.
            var otherVC = other.GetComponent<VehicleCollision>();
            Vector2 otherVel = otherVC != null ? otherVC.Velocity : Vector2.zero;
            float closingSpeed = Mathf.Max(0f, Vector2.Dot(_vel - otherVel, d.normal));
            float severity = Mathf.Clamp01(closingSpeed / Mathf.Max(damageSpeedFull, 0.1f));

            // Fold the bodywork around whatever hit it — only for genuine impacts above the threshold.
            // The striker is handed over as a BODY, not as a point: a car presses its own oriented box into
            // our panels and leaves a dent shaped like its nose or its flank, and a wall presses a flat face
            // and leaves the panel flat against it. See BodyDeform for why a point and a radius was wrong.
            //
            // And only OUR share of it. Both cars in a contact run this same line against each other, so
            // handing each of them the full severity folds one impact's worth of metal twice — both panels
            // retreat from the contact and leave a void between two cars that are supposed to be touching.
            // A barrier is not a body and gives nothing, so a car that hits one takes the whole thing.
            if (closingSpeed >= damageMinSpeed && _damage != null && !DamageSuppressed)
            {
                // Split by mass AND by how soft each car is where they meet, so a nose buried in a door
                // takes more of the overlap than the door does while the pair still fold only what is there.
                float share = Draftmaster.Sim.BodyDeform.RigidPartner;
                if (otherResponder != null)
                {
                    var otherBody = other.GetComponentInChildren<VehicleDamage>();
                    float myC = _bodywork != null ? _bodywork.ComplianceAt(d.pointA, severity) : 1f;
                    float theirC = otherBody != null ? otherBody.ComplianceAt(d.pointB, severity) : 1f;
                    share = Draftmaster.Sim.BodyDeform.Share(_responder != null ? _responder.Mass : 1500f, myC,
                                                             otherResponder.Mass, theirC);
                }
                PerfLog.Impacts++;
                if (PerfLog.Enabled)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    _damage.OnImpact(StrikerFor(other, d.pointB, -d.normal), severity, share);
                    PerfLog.AddDamageTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
                }
                else _damage.OnImpact(StrikerFor(other, d.pointB, -d.normal), severity, share);
            }

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
                    // With each car's turning in the denominator: an off-centre hit spends itself partly on
                    // spinning the cars and shoves them apart less (see CarContact). Head-on bumps are unchanged.
                    // The other car's inertia from the controller its VehicleCollision picked: GetComponent can
                    // answer with the dormant SplineDriver on a dynamic AI car, which can't be spun.
                    float iA = _responder != null ? _responder.YawInertia : float.PositiveInfinity;
                    var spunB = otherVC != null && otherVC._responder != null ? otherVC._responder : otherResponder;
                    float j = Draftmaster.Sim.CarContact.NormalImpulse(
                        vn, vehicleRestitution, n,
                        mA, iA, d.pointA - (Vector2)transform.position,
                        mB, spunB.YawInertia, d.pointB - (Vector2)other.transform.position);
                    dvA = -(j / mA) * n; // A slows / rebounds
                    dvB = (j / mB) * n;  // B is shunted forward
                }

                _responder?.ApplyCarImpact(pushWorld * (mB / total), d.pointA, dvA, severity);
                otherResponder.ApplyCarImpact(-pushWorld * (mA / total), d.pointB, dvB, severity);

                // Feed the rivalry system: genuine impacts (above the damage threshold) sour the two
                // drivers' relationship. frontHit = the contact point lies ahead of us along our direction
                // of travel, i.e. we drove into them — used to attribute blame. Both cars report the same
                // impact; DriverRelationships dedupes per pair.
                if (closingSpeed >= damageMinSpeed && otherVC != null && !DamageSuppressed)
                {
                    bool frontHit = _vel.sqrMagnitude > 1f
                        && Vector2.Dot(d.pointA - (Vector2)transform.position, _vel.normalized) > 0f;
                    DriverRelationships.ReportContact(gameObject, otherVC.gameObject, severity, frontHit);
                }
            }
            else
            {
                // Barrier (static): full correction on us. d.pointA is the contact on our body → lever arm for spin.
                pushWorld = SameSidePush(other, pushWorld);
                _responder?.ApplyContact(pushWorld, d.pointA, severity);
                BarrierHit?.Invoke(closingSpeed);
                AnyBarrierHit?.Invoke(transform, closingSpeed);
            }

            // Broadcast the resolved contact for FX (sparks, bodywork debris). Scrape speed is the
            // relative motion ALONG the contact — a car grinding down the wall has almost no closing
            // speed but plenty of scrape, and that's what should be throwing sparks.
            if (Contacted != null)
            {
                Vector2 rel = _vel - otherVel;
                Vector2 scrape = rel - Vector2.Dot(rel, d.normal) * d.normal;
                Contacted.Invoke(new ContactEvent
                {
                    point = d.pointA,
                    normal = d.normal,
                    closingSpeed = closingSpeed,
                    scrapeSpeed = scrape.magnitude,
                    severity = severity,
                    otherIsCar = otherResponder != null,
                });
            }
            processed++;
        }
        NoteSafePosition();
    }

    // Where this car sat at the end of its last collision pass: resolved, so on the side of every wall it was
    // meant to be on. A wall is pushed out of back toward it.
    Vector2 _safePos;
    bool _hasSafePos;
    [Tooltip("A car further than this (m) from where it was last step was moved there (a re-seed, a tow), not " +
             "driven, so where it was says nothing about which side of a wall it belongs on.")]
    public float wallSideJumpLimit = 8f;

    ContactFilter2D _filter;
    bool _jumped;
    readonly Collider2D[] _pointHits = new Collider2D[8];

    // A car's centre still inside a wall (an overlap the push couldn't clear this step) is not somewhere it is safe
    // to measure from: a line from inside a collider doesn't hit it on the way out (queriesStartInColliders is off),
    // so the next step's crossing test would miss the car carrying on out of the back. Keep the last clear spot.
    void NoteSafePosition()
    {
        Vector2 pos = transform.position;
        if (_hasSafePos && !_jumped && CentreInScenery(pos)) return;
        _safePos = pos;
        _hasSafePos = true;
    }

    bool CentreInScenery(Vector2 p)
    {
        int n = Physics2D.OverlapPoint(p, _filter, _pointHits);
        for (int i = 0; i < n; i++)
            if (IsScenery(_pointHits[i])) return true;
        return false;
    }

    // Moved there, not driven there: a re-seed, a tow, a car parked in its box. Then where it was says nothing about
    // which side of a wall it belongs on. Judged against BOTH where this pass left the car (a free car drives on
    // from there) and where its controller put it last step (a car placed on the spline is put on its path again
    // whatever we did to it, so a placed car held at a wall its path runs behind is a long way from the wall face
    // but only a step from where it was placed last). Only a car far from both was moved.
    bool Jumped(Vector2 pos, Vector2 lastPlaced)
    {
        if (!_hasSafePos) return true;
        float limit2 = wallSideJumpLimit * wallSideJumpLimit;
        return (pos - _safePos).sqrMagnitude > limit2 && (pos - lastPlaced).sqrMagnitude > limit2;
    }

    // Walls are thin (TrackEnvironmentBuilder: 1 m), and Unity's overlap answer pushes a car out by the shortest
    // way. A car shoved deep into one in a step (pinned in a pile-up with cars hammering it from the inside) got
    // its centre past the middle of the wall, and the shortest way out was then the back: out into the crowd. And
    // a long curved wall is hundreds of convex slivers, whose seams can answer with a push ALONG the wall that
    // lets the car keep sinking. A car only ever reaches a wall from one side, so it leaves by that side: out of
    // the face nearest where it was last step, far enough for every corner of the box to clear it.
    Vector2 SameSidePush(Collider2D wall, Vector2 push)
    {
        if (!_hasSafePos || _jumped) return push;
        Vector2 surface = wall.ClosestPoint(_safePos);
        Vector2 side = _safePos - surface;
        if (side.sqrMagnitude < 1e-6f) return push;  // it was inside the wall last step: no side to go back to
        side.Normalize();
        Vector2 facePush = side * Mathf.Max(0f, -Clearance(surface, side));
        if (facePush.sqrMagnitude > 1e-8f) return facePush;
        // Touching it somewhere the face through that point doesn't see (an end cap): the overlap's own answer,
        // as long as it doesn't take the car through.
        return Vector2.Dot(push, side) >= 0f ? push : Vector2.zero;
    }

    // How far the box's deepest corner is in front of the face through `facePoint` facing `n` (negative = behind it).
    float Clearance(Vector2 facePoint, Vector2 n)
    {
        Vector2 c = _box.offset, h = _box.size * 0.5f;
        float least = float.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            Vector2 local = c + new Vector2((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y);
            Vector2 world = transform.TransformPoint(local);
            least = Mathf.Min(least, Vector2.Dot(world - facePoint, n));
        }
        return least;
    }

    // Solid scenery: a wall, a building. Not a car (they carry a rigidbody and a responder), not a person.
    static bool IsScenery(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        var rb = c.attachedRigidbody;
        if (rb != null && rb.bodyType != RigidbodyType2D.Static) return false;
        return c.GetComponent<ICollisionResponder>() == null;
    }

    readonly RaycastHit2D[] _crossHits = new RaycastHit2D[16];

    // The backstop. However a car got there in one step (shoved by a pile-up, a spike in a frame), its centre never
    // goes through a wall: if the path from where it was last step to where it is now crosses solid scenery, it is
    // put back in front of the face it crossed, every corner clear, and its speed into the wall taken away.
    void StopCrossing(ContactFilter2D filter)
    {
        if (!_hasSafePos || _jumped) return;
        Vector2 pos = transform.position;
        if ((pos - _safePos).sqrMagnitude < 1e-8f) return;
        int n = Physics2D.Linecast(_safePos, pos, filter, _crossHits);
        for (int i = 0; i < n; i++)
        {
            var hit = _crossHits[i];
            if (hit.fraction <= 0f || !IsScenery(hit.collider)) continue;  // started inside it: nothing crossed
            Vector2 mtv = hit.normal * (Mathf.Max(0f, -Clearance(hit.point, hit.normal)) + 0.02f);
            _responder = PickResponder();
            if (_responder != null) _responder.ApplyContact(mtv, hit.point, 0f);
            else transform.position += new Vector3(mtv.x, mtv.y, 0f);
            return;
        }
    }

    // The body that hit us, in world space, for the deformation model to press into our panels.
    //
    // A car is a BoxCollider2D, so it goes over as the oriented rectangle it actually is: hit side-on by a
    // flank and the dent is a long shallow crease down the door; hit by a corner and it is a narrow gouge.
    // Anything else — barriers, scenery, an EdgeCollider2D wall — is an infinite face through the contact
    // point, which is what puts a car's nose flat against a wall instead of putting a hole in it.
    static Draftmaster.Sim.BodyDeform.Striker StrikerFor(Collider2D other, Vector2 contactPoint, Vector2 inward)
    {
        var box = other as BoxCollider2D;
        if (box == null) return Draftmaster.Sim.BodyDeform.Striker.Plane(contactPoint, inward);

        Transform t = other.transform;
        Vector3 scale = t.lossyScale;
        Vector2 centre = t.TransformPoint(box.offset);
        Vector2 half = new Vector2(box.size.x * 0.5f * Mathf.Abs(scale.x),
                                   box.size.y * 0.5f * Mathf.Abs(scale.y));
        return Draftmaster.Sim.BodyDeform.Striker.Box(centre, t.right, half, inward);
    }

    void OnValidate()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.size = new Vector2(halfExtents.y * 2f, halfExtents.x * 2f);
    }
}
