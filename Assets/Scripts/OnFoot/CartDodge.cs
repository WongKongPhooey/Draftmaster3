using System.Collections.Generic;
using UnityEngine;

// Getting out of the way of the golf cart.
//
// The paddock is full of people who walk a fixed route and never look up, and the cart now moves at twice
// walking pace. Driving through a crowd used to be driving THROUGH it — the walkers are kinematic bodies
// that shove the player around and cannot be shoved back, so a cart at speed either stopped dead on
// somebody's shoulder or slid through them. Neither reads as a paddock.
//
// So anyone the cart is about to run over jumps sideways: perpendicular to the way the cart is going,
// toward whichever side of its path they are already nearer, which is the shortest way out of it and the
// way a person actually steps. They hold there while the cart goes by, then carry on — a walker simply
// picks its route up from where it is now stood, and anybody posted somewhere (a marshal at a gate, a fan
// at the fence) walks back to their spot, so the cart cannot slowly sweep the paddock's cast into a corner.
//
// The component is added on the fly by the cart, to whoever needs it, and left on them afterwards: the
// people near the one drivable cart in the paddock are the same handful over and over, and adding a
// component every time somebody is nearly flattened is a lot of garbage for no gain.
public class CartDodge : MonoBehaviour
{
    [Tooltip("How fast they scramble clear, units/sec. Faster than a walk — it is a jump, not a stroll.")]
    public float dodgeSpeed = 3.4f;
    [Tooltip("How far out of the cart's path they go, metres.")]
    public float dodgeDistance = 1.8f;
    [Tooltip("Seconds they stand clear after arriving before going back to what they were doing. Topped " +
             "up for as long as the cart keeps threatening them, so nobody steps back into its path.")]
    public float holdSeconds = 1.2f;
    [Tooltip("Walk-cycle frames per second while scrambling.")]
    public float frameRate = 10f;
    [Tooltip("Rotation added to the movement angle so the sprite's drawn facing lines up, as PaddockWalker's.")]
    public float spriteFacingOffsetDeg = 90f;
    [Tooltip("Radius used when checking the way out is clear of bodywork.")]
    public float obstacleRadius = 0.45f;

    enum State { Idle, Diving, Waiting, Returning }
    State _state = State.Idle;

    Vector2 _target;
    Vector2 _home;
    bool _hasHome;
    float _hold;
    float _frameTimer;
    int _frame;

    Rigidbody2D _rb;
    NPCLayeredAppearance _appearance;
    PaddockWalker _walker;
    bool _walkerWasEnabled;

    public bool Dodging => _state != State.Idle;

    // --- the rule ---------------------------------------------------------------------------------

    // Is this person in the cart's way? True when they are within `clearance` of the stretch of ground the
    // cart will cover in the next `lookahead` seconds — the line from where it is to where it is about to
    // be, not just the point it is at, or a cart at eight metres a second would arrive before anybody had
    // been told to move.
    public static bool ShouldDodge(Vector2 cartPos, Vector2 cartVelocity, Vector2 personPos,
                                   float clearance, float lookahead)
    {
        Vector2 ahead = cartVelocity * Mathf.Max(0f, lookahead);
        float len = ahead.magnitude;

        // Standing still (or crawling): only the cart's own footprint counts.
        if (len < 1e-4f) return (personPos - cartPos).sqrMagnitude <= clearance * clearance;

        Vector2 dir = ahead / len;
        float along = Mathf.Clamp(Vector2.Dot(personPos - cartPos, dir), 0f, len);
        Vector2 nearest = cartPos + dir * along;
        return (personPos - nearest).sqrMagnitude <= clearance * clearance;
    }

    // Which way to jump: square across the cart's path, on the side they are already nearer. Somebody dead
    // centre in front of it is sent left, arbitrarily but consistently — a coin flip there produces two
    // people stood beside each other diving opposite ways.
    public static Vector2 EscapeDirection(Vector2 cartPos, Vector2 cartHeading, Vector2 personPos)
    {
        Vector2 h = cartHeading.sqrMagnitude > 1e-6f ? cartHeading.normalized : Vector2.up;
        Vector2 left = new Vector2(-h.y, h.x);
        float side = Vector2.Dot(personPos - cartPos, left);
        if (Mathf.Abs(side) < 1e-3f) return left;
        return side > 0f ? left : -left;
    }

    // --- the sweep --------------------------------------------------------------------------------

    static readonly List<Collider2D> _hits = new();
    static ContactFilter2D _filter;
    static bool _filterReady;

    // Tell everybody in front of the cart to move. Called by GolfCart every fixed step it is being driven.
    // `exclude` is the rider's own body — the person sat in the cart is not somebody it is about to run over.
    public static int ScatterFrom(Vector2 cartPos, Vector2 cartVelocity, Transform exclude,
                                  float clearance = 1.0f, float lookahead = 0.45f)
    {
        if (!_filterReady)
        {
            _filter = new ContactFilter2D();
            _filter.NoFilter();
            _filter.useTriggers = true;
            _filterReady = true;
        }

        // One circle big enough to hold the whole stretch the cart is about to cover, centred halfway along.
        Vector2 ahead = cartVelocity * Mathf.Max(0f, lookahead);
        Vector2 centre = cartPos + ahead * 0.5f;
        float radius = ahead.magnitude * 0.5f + clearance + 0.75f;   // a body's width of slack

        _hits.Clear();
        Physics2D.OverlapCircle(centre, radius, _filter, _hits);

        int told = 0;
        for (int i = 0; i < _hits.Count; i++)
        {
            var col = _hits[i];
            if (col == null) continue;

            Transform t = col.transform;
            if (exclude != null && (t == exclude || t.IsChildOf(exclude))) continue;

            // People only. The same trio OnFootController's bump handling treats as a person, so scenery,
            // cars and trigger volumes are ignored.
            var walker = t.GetComponentInParent<PaddockWalker>();
            var talker = t.GetComponentInParent<NPCInteractable>();
            var look = t.GetComponentInParent<NPCLayeredAppearance>();
            if (walker == null && talker == null && look == null) continue;

            // The cart itself is an NPCInteractable, and so is anything else parked about the place.
            if (talker is GolfCart) continue;
            // Mid-conversation: leave them be. The player is in the cart, so this is two NPCs talking to
            // each other, and the pair sliding apart mid-line looks worse than a near miss.
            if (talker != null && talker.IsTalking) continue;

            Transform person = walker != null ? walker.transform
                             : look != null ? look.transform
                             : talker.transform;

            if (!ShouldDodge(cartPos, cartVelocity, person.position, clearance, lookahead)) continue;

            var dodge = person.GetComponent<CartDodge>();
            if (dodge == null) dodge = person.gameObject.AddComponent<CartDodge>();
            dodge.Scatter(cartPos, cartVelocity);
            told++;
        }

        return told;
    }

    // --- the dive ---------------------------------------------------------------------------------

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _appearance = GetComponent<NPCLayeredAppearance>();
        _walker = GetComponent<PaddockWalker>();
    }

    // Jump clear of a cart at `cartPos` travelling at `cartVelocity`. Safe to call every frame the cart is
    // still bearing down: it tops the hold up, and only re-aims once the jump itself is finished.
    public void Scatter(Vector2 cartPos, Vector2 cartVelocity)
    {
        _hold = holdSeconds;

        if (_state == State.Diving) return;                  // already going; let them finish the jump

        Vector2 here = transform.position;
        Vector2 dir = EscapeDirection(cartPos, cartVelocity, here);
        Vector2 target = here + dir * dodgeDistance;

        // Never dive through a fence or into the side of a motorhome. A blocked escape is shortened to
        // wherever they can actually reach, and if that is nowhere they simply stand and take it.
        if (PaddockBoundary.AnyActive) target = PaddockBoundary.Constrain(target);
        if (!PaddockObstacles.TryStep(here, target, obstacleRadius, out Vector2 stepped)) return;
        target = stepped;
        if ((target - here).sqrMagnitude < 0.01f) return;

        if (_state == State.Idle)
        {
            // Posted somewhere: remember it, so they can go back afterwards. A walker has no post — its
            // route is wherever it happens to be — so it is handed back on the spot instead.
            _home = here;
            _hasHome = _walker == null;
            TakeOverWalker();
        }

        _target = target;
        _state = State.Diving;
    }

    void Update() => Tick(Time.deltaTime);

    // One frame of the dodge, `dt` seconds long. Split out of Update so a test can run the jump to its end.
    public void Tick(float dt)
    {
        switch (_state)
        {
            case State.Idle:
                return;

            case State.Diving:
                _hold = Mathf.Max(0f, _hold - dt);
                if (StepToward(_target, dt)) _state = State.Waiting;
                return;

            case State.Waiting:
                _hold -= dt;
                if (_hold > 0f) return;
                if (_hasHome) { _state = State.Returning; return; }
                Release();
                return;

            case State.Returning:
                // Threatened again on the way back: Scatter() puts them into Diving from wherever they are.
                if (StepToward(_home, dt)) Release();
                return;
        }
    }

    // One step toward a point. True once they are there, or once there is plainly no way to get there.
    bool StepToward(Vector2 target, float dt)
    {
        Vector2 here = transform.position;
        Vector2 to = target - here;
        float gap = to.magnitude;
        if (gap <= 0.05f) { Park(); return true; }

        Vector2 dir = to / gap;
        Vector2 next = here + dir * Mathf.Min(dodgeSpeed * dt, gap);

        if (PaddockBoundary.AnyActive) next = PaddockBoundary.Constrain(next);
        if (!PaddockObstacles.TryStep(here, next, obstacleRadius, out Vector2 stepped)) { Park(); return true; }
        next = stepped;

        Vector2 moved = next - here;
        if (moved.sqrMagnitude <= 1e-8f) { Park(); return true; }   // wedged: nowhere left to go

        // A body the crowd director has frozen is out of the simulation, and MovePosition on it does nothing
        // at all — so the jump never lands and the walk cycle runs on the spot forever. This component is
        // bolted on after CrowdActor collected what to freeze, so it keeps running regardless: move the pose
        // directly instead, body and transform together.
        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic && _rb.simulated) _rb.MovePosition(next);
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if (_rb != null) _rb.position = next;
        }

        OnFootController.ApplyFacing(transform, _rb, moved.normalized, spriteFacingOffsetDeg);
        Animate(dt);
        return false;
    }

    void Animate(float dt)
    {
        if (_appearance == null) return;
        _frameTimer += dt;
        float step = 1f / Mathf.Max(1f, frameRate);
        while (_frameTimer >= step)
        {
            _frameTimer -= step;
            _frame++;
            _appearance.SetFrame(_frame);
        }
    }

    void Park()
    {
        _frame = 0;
        _frameTimer = 0f;
        if (_appearance != null) _appearance.SetFrame(0);
    }

    // Stop the walker thinking while we move it, remembering whether it was thinking at all — the crowd
    // director disables walkers the player is nowhere near, and handing one back enabled would quietly
    // wake up somebody it had put to sleep.
    void TakeOverWalker()
    {
        if (_walker == null) return;
        _walkerWasEnabled = _walker.enabled;
        _walker.enabled = false;
    }

    void Release()
    {
        _state = State.Idle;
        _hold = 0f;
        Park();

        if (_walker != null)
        {
            // The route it was following started where it used to be stood. Put it down here instead, and
            // give it a fresh one, or it walks straight back across the path it just jumped out of.
            _walker.PlaceAt(transform.position);
            _walker.OnRecycled();

            // Only back on if the crowd director would have it on right now. It may have frozen this NPC
            // while the jump was in progress — the walker was already off, so it had nothing to switch — and
            // a walker woken into a frozen body steps against a body out of the simulation and walks on the
            // spot. The director switches it back on itself when the player comes close again.
            bool on = _walkerWasEnabled;
            var crowd = GetComponent<CrowdActor>();
            if (on && crowd != null) on = Draftmaster.Crowd.CrowdPolicy.RunsAt(crowd.Lod, false);
            _walker.enabled = on;
        }
    }

    void OnDisable()
    {
        // Frozen or recycled mid-jump: hand the walker back rather than leaving it switched off for good.
        if (_state != State.Idle) Release();
    }
}
