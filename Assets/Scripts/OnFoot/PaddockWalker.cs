using System.Collections.Generic;
using UnityEngine;

// A non-talking paddock NPC that wanders a generated looping path inside the paddock rectangle.
// Rotates to face its walking direction and cycles the recoloured walk frames from NPCAppearance
// (the art isn't directional, so facing is done by rotating the transform — same convention the
// player's OnFootController uses). Configured by PaddockSpawner with the paddock bounds.
public class PaddockWalker : MonoBehaviour
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

    // Paddock rectangle, world space. Set via Configure.
    Vector3 _center, _along, _outward;
    float _halfLen, _halfDepth;

    Rigidbody2D _rb;
    NPCLayeredAppearance _appearance;
    readonly List<Vector3> _path = new();
    int _idx;
    float _pauseTimer;
    float _frameTimer;
    int _frame;

    // along/outward are the rectangle's unit axes; halfLen spans along, halfDepth spans outward.
    public void Configure(Vector3 center, Vector3 along, Vector3 outward, float halfLen, float halfDepth)
    {
        _center = center; _along = along; _outward = outward;
        _halfLen = halfLen; _halfDepth = halfDepth;
        GeneratePath();
        _idx = 0;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _appearance = GetComponent<NPCLayeredAppearance>();
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
        // Inset a touch so walkers don't clip the paddock edge.
        float l = Random.Range(-_halfLen * 0.92f, _halfLen * 0.92f);
        float d = Random.Range(-_halfDepth * 0.92f, _halfDepth * 0.92f);
        return _center + _along * l + _outward * d;
    }

    void Update()
    {
        if (_path.Count == 0) return;

        Vector3 pos = transform.position;
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
            _idx++;
            if (_idx >= _path.Count) { _idx = 0; if (Random.value < 0.5f) GeneratePath(); }
            _pauseTimer = Random.Range(0f, maxPauseSeconds);
            Idle();
            return;
        }

        Vector2 dir = toTarget.normalized;
        Vector3 step = (Vector3)(dir * speed * Time.deltaTime);
        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.MovePosition((Vector2)pos + (Vector2)step);
        else transform.position = pos + step;

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
