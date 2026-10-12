using System.Collections.Generic;
using UnityEngine;

// The VIP grid walk's pass holders: the signing queue turned round.
//
// At the fence the player is walked along a line of fans (SigningQueue). On race morning the player stays with
// the car at their own garage and the people come to them: the crowd is gathered a few metres off when the
// walk starts, and the pit-box host calls them over one at a time — walk up, stop in front of the driver,
// say their piece, and when the next one is called the last one walks back off the way they came.
//
// Built by WeekendVenueHost when a GridWalk booking starts and dismissed when it settles or is walked out of.
// It cleans itself up if the host goes away underneath it (scene change, the crew chief swapped to the pit
// box), so a race-morning crowd can never be left stood in the paddock all afternoon.
public class GridWalkCrowd : MonoBehaviour
{
    const float GatherDistance = 7f;     // how far off the pass holders wait, metres
    const float StandOff = 0.9f;         // how close the one being signed for stops to the driver
    const float SpreadAcross = 0.7f;     // the waiting group's spacing, across and in depth
    const float FacingOffsetDeg = 90f;   // walk art faces -Y
    const float Fps = 8f;
    const float LeaveSeconds = 6f;

    class Walker
    {
        public Transform body;
        public Rigidbody2D rb;
        public NPCLayeredAppearance doll;
        public Vector3 target;
        public bool moving;
        public bool leaving;
        public float frameTimer;
        public int frame;
    }

    readonly List<Walker> _fans = new();
    Behaviour _owner;
    Vector3 _driver;
    Vector3 _towardCrowd;
    Vector3 _home;
    float _speed = 2.2f;
    int _called = -1;
    bool _dismissed;

    // Gather `count` pass holders a few metres from the driver, on the side `towardCrowd` points to.
    public static GridWalkCrowd Gather(Behaviour owner, Vector3 driver, Vector3 towardCrowd, int count, int seed)
    {
        var go = new GameObject("GridWalkCrowd");
        var crowd = go.AddComponent<GridWalkCrowd>();
        crowd._owner = owner;
        crowd._driver = driver;
        towardCrowd.z = 0f;
        crowd._towardCrowd = towardCrowd.sqrMagnitude > 0.01f ? towardCrowd.normalized : Vector3.down;
        crowd._home = driver + crowd._towardCrowd * GatherDistance;
        crowd._home.z = PaddockPerson.GroundZ;
        go.transform.position = crowd._home;

        var walker = OnFootController.Current;
        if (walker != null) crowd._speed = Mathf.Max(1f, walker.moveSpeed * 0.8f);

        Vector3 across = new Vector3(-crowd._towardCrowd.y, crowd._towardCrowd.x, 0f);
        const int perRow = 4;
        for (int i = 0; i < count; i++)
        {
            float col = i % perRow - (perRow - 1) * 0.5f;
            float row = i / perRow;
            // A loose bunch, not a parade-ground block: nudge each one off the grid a little.
            float jitter = ((i * 37) % 7 - 3) * 0.06f;
            Vector3 at = crowd._home + across * (col * SpreadAcross + jitter) + crowd._towardCrowd * (row * SpreadAcross);
            at.z = PaddockPerson.GroundZ;

            var body = PaddockPerson.Spawn(go.transform, at, $"VipGuest_{i}", seed + i);
            var w = new Walker
            {
                body = body.transform,
                rb = body.GetComponent<Rigidbody2D>(),
                doll = body.GetComponentInChildren<NPCLayeredAppearance>(),
            };
            OnFootController.ApplyFacing(w.body, w.rb, driver - at, FacingOffsetDeg);
            crowd._fans.Add(w);
        }
        return crowd;
    }

    public int Count => _fans.Count;

    public Transform Fan(int place) => place >= 0 && place < _fans.Count ? _fans[place].body : null;

    // The one called over has reached the driver.
    public bool Arrived => _called < 0 || _called >= _fans.Count || !_fans[_called].moving;

    // Send the last one back off and bring `place` over to stand in front of the driver.
    public void Call(int place)
    {
        if (_called >= 0 && _called < _fans.Count) Leave(_fans[_called]);
        _called = place;
        if (place < 0 || place >= _fans.Count) return;

        var w = _fans[place];
        w.target = _driver + _towardCrowd * StandOff;
        w.target.z = PaddockPerson.GroundZ;
        w.moving = true;
    }

    // The walk is over: everybody still here heads back to the gate, and the crowd tidies itself away.
    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        foreach (var w in _fans) Leave(w);
        Destroy(gameObject, LeaveSeconds);
    }

    void Leave(Walker w)
    {
        if (w.body == null || w.leaving) return;
        w.leaving = true;
        w.target = w.body.position + _towardCrowd * (GatherDistance + 4f);
        w.moving = true;
    }

    void Update()
    {
        // The host went away with the walk still on: nobody is coming to sign, so the pass holders go too.
        if (!_dismissed && (_owner == null || !_owner.isActiveAndEnabled)) { Dismiss(); }

        foreach (var w in _fans)
        {
            if (w.body == null || !w.moving) continue;

            Vector3 pos = w.body.position;
            Vector2 to = (Vector2)(w.target - pos);
            float step = _speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                Place(w, new Vector3(w.target.x, w.target.y, pos.z));
                w.moving = false;
                FightMotion.IdleFrame(w.doll, ref w.frameTimer, ref w.frame);
                if (w.leaving) w.body.gameObject.SetActive(false);
                else OnFootController.ApplyFacing(w.body, w.rb, _driver - w.body.position, FacingOffsetDeg);
                continue;
            }

            Vector2 dir = to.normalized;
            Place(w, pos + (Vector3)(dir * step));
            OnFootController.ApplyFacing(w.body, w.rb, dir, FacingOffsetDeg);
            FightMotion.StepFrames(w.doll, ref w.frameTimer, ref w.frame, Fps);
        }
    }

    static void Place(Walker w, Vector3 at)
    {
        if (w.rb != null) w.rb.position = at;
        w.body.position = at;
    }
}
