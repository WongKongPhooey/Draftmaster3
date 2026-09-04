using System.Collections.Generic;
using UnityEngine;

// A non-talking paddock NPC that wanders a generated looping path inside the paddock rectangle.
// Rotates to face its walking direction and cycles the recoloured walk frames from NPCAppearance
// (the art isn't directional, so facing is done by rotating the transform — same convention the
// player's OnFootController uses). Configured by PaddockSpawner with the paddock bounds.
public class PaddockWalker : MonoBehaviour, ICrowdRecyclable
{
    [Tooltip("Walk speed in units/sec.")]
    public float speed = 1.2f;
    [Tooltip("How close (m) to a waypoint counts as reached.")]
    public float arriveRadius = 0.6f;
    [Tooltip("Waypoints generated per wander loop.")]
    public int waypointCount = 6;
    [Tooltip("Seconds paused on reaching a waypoint (0 = keep moving).")]
    public float maxPauseSeconds = 1.5f;
    [Tooltip("How fast the body rotates to face the walking direction (deg/sec).")]
    public float turnRate = 540f;
    [Tooltip("Rotation added to the movement angle so the sprite's drawn facing lines up. The walk art faces -Y, so +90.")]
    public float spriteFacingOffsetDeg = 90f;
    [Tooltip("Walk-cycle playback rate (frames/sec) while moving.")]
    public float frameRate = 8f;
    [Tooltip("Seconds this walker stands still after the player walks into it, looking at whoever bumped " +
             "them before carrying on. Also what stops the pair grinding against each other: a kinematic " +
             "walker that keeps stepping into a dynamic player can never be pushed out of the way.")]
    public float bumpPauseSeconds = 1.4f;
    [Tooltip("Walk round solid paddock scenery — motorhomes, popup garages, haulers — instead of straight " +
             "through it. Off restores the old behaviour of ignoring everything but the paddock boundary.")]
    public bool avoidObstacles = true;
    [Tooltip("How wide a berth this walker gives solid scenery (m) — roughly their own footprint. Their " +
             "centre stops this far from a motorhome's side or a garage wall.")]
    public float obstacleRadius = 0.45f;
    [Tooltip("Conversation this walker owns. While it's running the walker stands still and turns to face whoever stopped it — otherwise it would wander off mid-sentence, dragging its speech bubble along. Auto-found on the same object if left null.")]
    public NPCInteractable conversation;
    [Tooltip("Ambient one-liners this walker mutters at a passing player. Handled the same as a conversation: stand still and look at them while speaking. Auto-found on the same object if left null.")]
    public NPCAmbientChatter chatter;

    // Paddock rectangle, world space. Set via Configure.
    Vector3 _center, _along, _outward;
    float _halfLen, _halfDepth;

    Rigidbody2D _rb;
    NPCLayeredAppearance _appearance;
    readonly List<Vector3> _path = new();
    int _idx;
    float _pauseTimer;
    Transform _bumpedBy;           // whoever last walked into us, for as long as _bumpTimer runs
    float _bumpTimer;
    float _frameTimer;
    int _frame;
    float _escapeTimer;            // throttles the "am I standing inside a motorhome?" check

    // along/outward are the rectangle's unit axes; halfLen spans along, halfDepth spans outward.
    public void Configure(Vector3 center, Vector3 along, Vector3 outward, float halfLen, float halfDepth)
    {
        _center = center; _along = along; _outward = outward;
        _halfLen = halfLen; _halfDepth = halfDepth;
        GeneratePath();
        _idx = 0;
    }

    // The CrowdActor has just picked this walker up and put it down somewhere else in the paddock. The
    // old route led back to wherever it came from, which is now a long walk away and off the far side of
    // the recycle radius, so throw it away and pick a new one from here.
    public void OnRecycled()
    {
        GeneratePath();
        _idx = 0;
        _pauseTimer = 0f;
        _bumpTimer = 0f;
        _bumpedBy = null;
        _escapeTimer = 0f;   // they may have been put back down on top of a motorhome; check straight away
        Idle();
    }

    // Somebody has walked into us. Stand still and look at them for a moment, then carry on.
    //
    // This is the same courtesy the walker already extends to anyone who talks to it, and it is also what
    // unsticks the pair. The walker is a KINEMATIC body stepping along a fixed path with MovePosition, and
    // the player is a dynamic one: a kinematic body shoves a dynamic body and is never shoved back, so a
    // walker that keeps marching into the player pins them and neither can get past. Stopping hands the
    // ground back — and the player's own contact slide (OnFootController) does the rest.
    public void Bumped(Transform by)
    {
        _bumpedBy = by;
        _bumpTimer = Mathf.Max(_bumpTimer, bumpPauseSeconds);
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _appearance = GetComponent<NPCLayeredAppearance>();
        if (conversation == null) conversation = GetComponent<NPCInteractable>();
        if (chatter == null) chatter = GetComponent<NPCAmbientChatter>();
    }

    void GeneratePath()
    {
        _path.Clear();
        int n = Mathf.Max(2, waypointCount);
        for (int i = 0; i < n; i++)
            _path.Add(RandomPointInRect());
    }

    Vector3 RandomPointInRect()
    {
        // Inset a touch so walkers don't clip the paddock edge. When a PaddockBoundary is authored,
        // reject-sample so waypoints land inside it (clamping instead would pile them on the edge).
        //
        // Solid scenery is rejected the same way, and with a metre of margin: a waypoint sitting inside a
        // motorhome can never be reached now that the walls are honoured, so the walker would spend its
        // whole life pressed against the same panel. Better to aim somewhere it can actually stand.
        Vector3 p = _center;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float l = Random.Range(-_halfLen * 0.92f, _halfLen * 0.92f);
            float d = Random.Range(-_halfDepth * 0.92f, _halfDepth * 0.92f);
            p = _center + _along * l + _outward * d;
            if (!PaddockBoundary.IsInside(p)) continue;
            if (avoidObstacles && PaddockObstacles.IsBlocked(p, obstacleRadius + 1f)) continue;
            return p;
        }
        Vector2 c = PaddockBoundary.Constrain(p);
        return new Vector3(c.x, c.y, p.z);
    }

    // Give up on the current waypoint and head for the next, pausing a beat. Used both on arriving and on
    // finding the way there closed — a boundary edge or a wall of bodywork.
    void NextWaypoint()
    {
        _idx++;
        if (_idx >= _path.Count) { _idx = 0; if (Random.value < 0.5f) GeneratePath(); }
        _pauseTimer = Random.Range(0f, maxPauseSeconds);
    }

    void Update()
    {
        // Stop and listen for as long as someone is talking to us.
        if (conversation != null && conversation.IsTalking)
        {
            Idle();
            if (conversation.Interactor != null)
                Face((Vector2)(conversation.Interactor.position - transform.position));
            return;
        }

        // Same treatment for an unprompted mutter: stop, look at whoever we're talking to, then walk on.
        if (chatter != null && chatter.IsSpeaking)
        {
            Idle();
            if (chatter.Listener != null)
                Face((Vector2)(chatter.Listener.position - transform.position));
            return;
        }

        // Bumped: stand still, look at them, and let the moment pass before walking on.
        if (_bumpTimer > 0f)
        {
            _bumpTimer -= Time.deltaTime;
            Idle();
            if (_bumpedBy != null) Face((Vector2)(_bumpedBy.position - transform.position));
            return;
        }

        if (_path.Count == 0) return;

        Vector3 pos = transform.position;

        // Standing inside something solid: put down on top of a motorhome by the crowd director, or a rig
        // assembled around them after they arrived. Walk out before doing anything else — otherwise the
        // step below finds every direction blocked and they are sealed in for good. Checked a couple of
        // times a second rather than every frame, because for everybody who is not stuck it costs nothing
        // and there can be hundreds of them.
        if (avoidObstacles)
        {
            _escapeTimer -= Time.deltaTime;
            if (_escapeTimer <= 0f)
            {
                _escapeTimer = 0.5f;
                if (PaddockObstacles.IsBlocked(pos, obstacleRadius))
                {
                    Vector2 freed = PaddockObstacles.PushOut(pos, obstacleRadius);
                    if (freed != (Vector2)pos)
                    {
                        pos = new Vector3(freed.x, freed.y, pos.z);
                        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.position = freed;
                        transform.position = pos;
                        GeneratePath();     // the old route started from inside the bodywork
                        _idx = 0;
                    }
                }
            }
        }

        Vector3 target = _path[_idx];
        target.z = pos.z; // stay in the NPC's own sorting plane

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            Idle();
            return;
        }

        Vector2 toTarget = (Vector2)(target - pos);
        if (toTarget.magnitude <= arriveRadius)
        {
            // Reached: advance, occasionally regenerate the loop so the route varies over time.
            NextWaypoint();
            Idle();
            return;
        }

        Vector2 dir = toTarget.normalized;
        Vector3 step = (Vector3)(dir * speed * Time.deltaTime);
        Vector3 newPos = pos + step;

        // Never step outside an authored PaddockBoundary. A clamped step means the waypoint is
        // unreachable through the polygon — skip to the next one rather than grinding on the edge.
        if (PaddockBoundary.AnyActive)
        {
            Vector2 c = PaddockBoundary.Constrain(newPos);
            if ((Vector2)newPos != c)
            {
                newPos = new Vector3(c.x, c.y, newPos.z);
                NextWaypoint();
            }
        }

        // Round the bodywork rather than through it. The paddock's motorhomes, popup garages and haulers
        // are plain static colliders, which stop the dynamic player on their own but do nothing at all to
        // a kinematic body moved with MovePosition — so this walker has to steer itself. A blocked step
        // becomes a slide along the panel; a step with nowhere to go at all means the waypoint is on the
        // wrong side of a wall, so give up on it and pick the next.
        if (avoidObstacles)
        {
            if (!PaddockObstacles.TryStep(pos, newPos, obstacleRadius, out Vector2 stepped))
            {
                NextWaypoint();
                Idle();
                return;
            }

            if (stepped != (Vector2)newPos)
            {
                newPos = new Vector3(stepped.x, stepped.y, newPos.z);
                Vector2 slid = (Vector2)(newPos - pos);
                if (slid.sqrMagnitude > 1e-8f) dir = slid.normalized;   // face the way they're actually going
            }
        }

        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.MovePosition(newPos);
        else transform.position = newPos;

        Face(dir);
        Animate();
    }

    // Hold a standing pose: keep the current facing, park on the first frame.
    void Idle()
    {
        _frame = 0;
        _frameTimer = 0f;
        _appearance?.SetFrame(0);
    }

    void Face(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic)
            _rb.MoveRotation(Mathf.MoveTowardsAngle(_rb.rotation, ang, turnRate * Time.deltaTime));
        else
        {
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, ang, turnRate * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    void Animate()
    {
        if (_appearance == null || _appearance.FrameCount == 0) return;
        _frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(0.01f, frameRate);
        while (_frameTimer >= step)
        {
            _frameTimer -= step;
            _frame++;
            _appearance.SetFrame(_frame);
        }
    }
}
