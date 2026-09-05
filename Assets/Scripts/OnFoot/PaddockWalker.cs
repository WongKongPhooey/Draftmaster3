using System.Collections.Generic;
using Draftmaster.Crowd;
using UnityEngine;

// A non-talking paddock NPC that wanders a generated looping path inside the paddock rectangle.
// Rotates to face its walking direction and cycles the recoloured walk frames from NPCAppearance
// (the art isn't directional, so facing is done by rotating the transform — same convention the
// player's OnFootController uses). Configured by PaddockSpawner with the paddock bounds.
//
// Some of them walk in company. A walker with `groupLeader` set stops wandering and instead keeps the
// slot that leader holds for it — strung out behind them in a loose pack while they walk, closed up into
// a ring facing inward the moment they stop, which is what a pair or a three stood talking looks like
// from above. The leader wanders exactly as before, but dawdles longer at each waypoint and waits for
// anyone who falls behind. See Draftmaster.Crowd.CrowdGrouping for the shapes and PaddockSpawner for who
// ends up with whom.
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

    [Header("Company")]
    [Tooltip("The walker this one is keeping company with, set by whoever spawned the group. Null — the " +
             "usual case — means this walker is on its own and wanders the paddock as it always did.")]
    public PaddockWalker groupLeader;
    [Tooltip("This walker's place in its group: 0 is the leader, 1..n the people with them. Picks which " +
             "slot of the walking pack, and of the standing huddle, they head for.")]
    public int groupSlot;
    [Tooltip("Metres between neighbours in this group's standing huddle.")]
    public float groupSpacing = 0.85f;
    [Tooltip("How close (m) to their slot counts as being in place. Not zero, or a follower twitches on " +
             "the spot for ever chasing a target that moves with the leader.")]
    public float slotTolerance = 0.22f;
    [Tooltip("Top speed a follower closing on its slot may use, as a multiple of its walk speed. Eased in " +
             "with the gap, so somebody held up by a passing player catches their group back up instead " +
             "of trailing them for the rest of the weekend.")]
    public float catchUpFactor = 1.5f;
    [Tooltip("How far (m) somebody may fall behind before the leader stops and waits for them.")]
    public float groupLeash = 4f;
    [Tooltip("Seconds a follower may spend stuck out beyond the leash — walled off behind a motorhome, " +
             "say — before it gives up and goes back to wandering alone. Nothing visible happens; it is " +
             "what stops one blocked follower freezing their whole group in place.")]
    public float groupGiveUpSeconds = 8f;
    [Tooltip("Extra seconds of dwell added to a waypoint pause while this walker has company, so a group " +
             "stands about talking rather than marching the length of the paddock in formation.")]
    public float groupChatSeconds = 7f;

    // Paddock rectangle, world space. Set via Configure.
    Vector3 _center, _along, _outward;
    float _halfLen, _halfDepth;

    // The people keeping this walker company. Only ever populated on a leader.
    readonly List<PaddockWalker> _followers = new();
    Vector2 _heading = Vector2.up;  // last direction actually walked — the frame the group's slots sit in
    bool _moving;                   // walking this frame, as opposed to stood still
    float _adriftTimer;             // how long this follower has been stranded outside the leash

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

    // ---------------------------------------------------------------- company

    public int GroupSize => _followers.Count + 1;
    public bool HasCompany => _followers.Count > 0;
    public bool IsFollower => groupLeader != null;
    // The direction this walker last actually walked in. Slots are measured in this frame, so it is held
    // steady while they are stood still — otherwise a huddle would spin every time somebody looked round.
    public Vector2 Heading => _heading;
    public bool IsMoving => _moving;

    // Take somebody into this walker's company. Slots are handed out in order and slot 0 is this walker,
    // so the leader stands at the front of the ring and the rest fill in behind.
    public void AddFollower(PaddockWalker follower)
    {
        if (follower == null || follower == this || _followers.Contains(follower)) return;
        _followers.Add(follower);
        follower.groupLeader = this;
        follower.groupSpacing = groupSpacing;
        Renumber();
    }

    public void RemoveFollower(PaddockWalker follower)
    {
        if (follower != null && _followers.Remove(follower)) Renumber();
    }

    void Renumber()
    {
        for (int i = 0; i < _followers.Count; i++)
            if (_followers[i] != null) _followers[i].groupSlot = i + 1;
    }

    // Peel off and go back to wandering alone. The leader carries on with whoever is left.
    public void LeaveGroup()
    {
        if (groupLeader != null) groupLeader.RemoveFollower(this);
        groupLeader = null;
        groupSlot = 0;
        _adriftTimer = 0f;
        GeneratePath();
        _idx = 0;
    }

    // Where follower `slot` of this walker's group should be stood right now: in the pack while the group
    // is walking, on the ring once it has stopped.
    public Vector2 SlotWorld(int slot) =>
        CrowdGrouping.SlotWorld(slot, GroupSize, groupSpacing, transform.position, _heading, _moving);

    // The middle of the ring this walker's group stands in — what a member who has arrived turns to face.
    public Vector2 GroupCentre() =>
        CrowdGrouping.HuddleCentre(transform.position, GroupSize, groupSpacing, _heading);

    // Point this walker somewhere immediately, skipping the turn rate, and take the group's slot frame
    // with it. Used at spawn so a paddock full of groups is not a paddock all facing the same way.
    public void FaceInstantly(Vector2 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        _heading = dir.normalized;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.rotation = ang;
    }

    // Moved bodily by whoever owns this walker — its leader having been recycled across the paddock.
    // Prefer the crowd director's own move, which rerolls the outfit and resets everything hanging off the
    // NPC; fall back to shifting the body when there is no CrowdActor on it.
    public void PlaceAt(Vector2 point)
    {
        var actor = GetComponent<CrowdActor>();
        if (actor != null) { actor.RecycleTo(point); return; }

        transform.position = new Vector3(point.x, point.y, transform.position.z);
        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.position = point;
        OnRecycled();
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
        _adriftTimer = 0f;
        Idle();

        // Company is picked up as one and put back down as one. Leaving the followers behind would send
        // two or three people marching the length of the paddock after a leader who is now a hundred
        // metres away — with the leader stood waiting on the leash for the whole trip.
        GatherCompany();
    }

    // Put every follower down in its slot around wherever this walker is now stood.
    void GatherCompany()
    {
        for (int i = 0; i < _followers.Count; i++)
        {
            var f = _followers[i];
            if (f == null) continue;
            Vector2 slot = SlotWorld(f.groupSlot);
            if (avoidObstacles && PaddockObstacles.IsBlocked(slot, obstacleRadius))
                slot = PaddockObstacles.PushOut(slot, obstacleRadius);
            f.PlaceAt(slot);
        }
    }

    // True while somebody in this walker's company is further off than the leash allows. A follower the
    // crowd director has frozen does not count: nothing is moving out there, so waiting on one would leave
    // a leader stood still in an empty corner of the paddock for as long as the player stayed away.
    bool AnyoneAdrift()
    {
        float leash = Mathf.Max(0.5f, groupLeash);
        Vector3 pos = transform.position;
        for (int i = 0; i < _followers.Count; i++)
        {
            var f = _followers[i];
            if (f == null || !f.isActiveAndEnabled) continue;
            if (((Vector2)(f.transform.position - pos)).sqrMagnitude > leash * leash) return true;
        }
        return false;
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

        // Somebody with company dawdles. A group that only ever pauses as long as a lone walker does
        // reads as a squad on the march; the standing-about is the half of it that looks like people.
        float dwell = maxPauseSeconds + (HasCompany ? Mathf.Max(0f, groupChatSeconds) : 0f);
        _pauseTimer = Random.Range(0f, dwell);
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
                        if (groupLeader == null)
                        {
                            GeneratePath();     // the old route started from inside the bodywork
                            _idx = 0;
                        }
                    }
                }
            }
        }

        // Keeping somebody company: head for the slot they are holding rather than wandering off on an
        // errand of our own.
        if (groupLeader != null) { FollowLeader(pos); return; }

        if (_path.Count == 0) { Idle(); return; }

        // Nobody is left behind. A group only reads as a group if it arrives together, so the leader
        // stands and waits — looking back at them, which is what somebody waiting actually does.
        if (HasCompany && AnyoneAdrift())
        {
            Idle();
            Face(GroupCentre() - (Vector2)pos);
            return;
        }

        Vector3 target = _path[_idx];
        target.z = pos.z; // stay in the NPC's own sorting plane

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            Idle();
            // Stopped with company: turn into the huddle. The followers close up onto the ring around it
            // on their own, and what the player walks past is three people talking rather than three
            // people who happen to have halted near each other.
            if (HasCompany) Face(GroupCentre() - (Vector2)pos);
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

        _heading = dir;
        _moving = true;
        Face(dir);
        Animate();
    }

    // Keep the slot the leader is holding: strung out behind them in the pack while they walk, closed up
    // onto the ring once they stop. Everything else a walker honours still applies here — the paddock
    // boundary, the bodywork, being bumped — the only difference is where they are headed.
    void FollowLeader(Vector3 pos)
    {
        var leader = groupLeader;
        // Gone for good (destroyed by something else in the scene): there is no group left to keep, so
        // go back to wandering rather than standing on the spot waiting for nobody.
        if (leader == null) { LeaveGroup(); return; }
        // Still there but not thinking — frozen by the crowd director, or suspended while it plays a part
        // in something else. Its slots are where they were, so stand in ours and wait it out.
        if (!leader.isActiveAndEnabled) { Idle(); return; }

        // Stranded outside the leash for long enough that they are plainly never getting back — walled
        // off by a rig that was assembled between them, most likely. Peel off and wander alone rather
        // than leaving the leader stood waiting on the far side of it for the rest of the session.
        float leash = Mathf.Max(0.5f, leader.groupLeash);
        if (((Vector2)(leader.transform.position - pos)).sqrMagnitude > leash * leash)
        {
            _adriftTimer += Time.deltaTime;
            if (_adriftTimer > Mathf.Max(1f, groupGiveUpSeconds)) { LeaveGroup(); return; }
        }
        else _adriftTimer = 0f;

        Vector2 slot = leader.SlotWorld(groupSlot);
        Vector2 toSlot = slot - (Vector2)pos;
        float gap = toSlot.magnitude;

        if (gap <= Mathf.Max(0.02f, slotTolerance))
        {
            // Arrived. Stand and look into the middle of the group, which is the whole point of a huddle.
            Idle();
            Face(leader.GroupCentre() - (Vector2)pos);
            return;
        }

        // Eased rather than switched: the slot itself moves at the leader's pace, so a follower on the
        // same flat speed can never close a gap, and one that snaps to a catch-up speed at a threshold
        // pulses between the two. Scaling with the gap settles them a few centimetres adrift and holds.
        float lead = Mathf.Max(0f, gap - slotTolerance) / Mathf.Max(0.1f, groupSpacing);
        float sp = Mathf.Max(0.01f, speed) * (1f + Mathf.Clamp01(lead) * (Mathf.Max(1f, catchUpFactor) - 1f));

        Vector2 dir = toSlot / gap;
        Vector3 newPos = pos + (Vector3)(dir * Mathf.Min(sp * Time.deltaTime, gap));

        if (PaddockBoundary.AnyActive)
        {
            Vector2 c = PaddockBoundary.Constrain(newPos);
            if ((Vector2)newPos != c) newPos = new Vector3(c.x, c.y, newPos.z);
        }

        if (avoidObstacles)
        {
            if (!PaddockObstacles.TryStep(pos, newPos, obstacleRadius, out Vector2 stepped)) { Idle(); return; }
            if (stepped != (Vector2)newPos)
            {
                newPos = new Vector3(stepped.x, stepped.y, newPos.z);
                Vector2 slid = (Vector2)(newPos - pos);
                if (slid.sqrMagnitude > 1e-8f) dir = slid.normalized;   // face the way they're actually going
            }
        }

        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic) _rb.MovePosition(newPos);
        else transform.position = newPos;

        _heading = dir;
        _moving = true;
        Face(dir);
        Animate();
    }

    // Hold a standing pose: keep the current facing, park on the first frame.
    void Idle()
    {
        _moving = false;
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
