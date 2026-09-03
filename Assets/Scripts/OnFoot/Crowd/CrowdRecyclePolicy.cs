using UnityEngine;

namespace Draftmaster.Crowd
{
    // Rules for recycling background crowd members around the player.
    //
    // A paddock is a long, thin strip -- a few hundred metres of pit straight by thirty deep -- and a
    // crowd spread evenly over it is mostly somewhere the player is not. Walk to one end and the other
    // end's two hundred people are doing nothing for the scene except existing.
    //
    // So the filler is treated as a pool rather than as a cast. Anyone who drifts further than
    // `despawnRadius` from the on-foot player is taken off the board and put back just outside the
    // camera frame, with a fresh outfit, so the same headcount is always packed into the part of the
    // paddock the player is actually standing in. The player never sees it happen: the despawn is a
    // hundred metres away and the respawn is off screen by construction.
    //
    // The cap (`targetNearPlayer`) is what keeps it from turning into a crush. Once that many crowd
    // members are already inside the despawn radius, anyone else who wanders off is simply left where
    // they are -- frozen, out of the way, costing nothing -- and comes back into the mix later, when
    // the player has walked on and the cluster has room again.

    // A paddock-shaped rectangle in world XY: a centre, two unit axes, and a half-extent on each.
    // Defined here rather than taken from PaddockSpawner so the policy stays pure and testable.
    [System.Serializable]
    public struct CrowdRect
    {
        [Tooltip("Centre of the rectangle, world XY.")]
        public Vector2 center;
        [Tooltip("Unit axis the length runs along.")]
        public Vector2 along;
        [Tooltip("Unit axis the depth runs along, perpendicular to `along`.")]
        public Vector2 outward;
        [Tooltip("Half the rectangle's length, metres.")]
        public float halfLength;
        [Tooltip("Half the rectangle's depth, metres.")]
        public float halfDepth;

        public CrowdRect(Vector2 center, Vector2 along, Vector2 outward, float halfLength, float halfDepth)
        {
            this.center = center;
            this.along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            this.outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector2.up;
            this.halfLength = halfLength;
            this.halfDepth = halfDepth;
        }

        public bool IsValid =>
            halfLength > 0.01f && halfDepth > 0.01f &&
            along.sqrMagnitude > 1e-6f && outward.sqrMagnitude > 1e-6f;

        // `insetFraction` pulls the test in from every edge, so a respawned NPC does not materialise
        // straddling the paddock's edge.
        public bool Contains(Vector2 point, float insetFraction = 0f)
        {
            if (!IsValid) return false;
            Vector2 d = point - center;
            float keep = 1f - Mathf.Clamp01(insetFraction);
            return Mathf.Abs(Vector2.Dot(d, along)) <= halfLength * keep &&
                   Mathf.Abs(Vector2.Dot(d, outward)) <= halfDepth * keep;
        }
    }

    // Distances (metres) and budgets for the recycler.
    [System.Serializable]
    public struct CrowdRecycleTuning
    {
        [Tooltip("Off: the crowd stays where it was spawned and spreads out across the whole paddock.")]
        public bool enabled;
        [Tooltip("Past this distance from the on-foot player a filler NPC is taken out of the paddock and put back near them.")]
        public float despawnRadius;
        [Tooltip("Nearest a recycled NPC may be put back. Raised at runtime to the on-foot camera's corner distance, so nobody ever pops in on screen.")]
        public float respawnMinRadius;
        [Tooltip("Furthest a recycled NPC may be put back. The band between the two radii is sampled evenly by area, so the crowd lands spread through it rather than ringed on the inner edge.")]
        public float respawnMaxRadius;
        [Tooltip("How many filler NPCs are allowed inside despawnRadius at once. Anyone who drifts off while the cluster is full is left where they are instead of being brought back. 0 = no cap, so eventually the whole crowd ends up around the player.")]
        public int targetNearPlayer;
        [Tooltip("Ceiling on recycles per frame. Each one rebuilds an outfit, so this spreads the cost of a big migration over several frames instead of spiking it on one.")]
        public int recyclesPerFrame;
        [Tooltip("Candidate positions tried before giving up on one NPC. Sampling is rejected against the paddock rectangle and any PaddockBoundary, so a player stood at the end of the paddock needs a few tries.")]
        public int samplesPerRecycle;
        [Tooltip("Fraction of the paddock's half-extents kept clear of the edges when picking a respawn point.")]
        public float edgeInset;
        [Tooltip("Metres of slack added beyond the camera's corner before an NPC may be put back, so nothing appears in the corner of the frame or in the first step of a pan.")]
        public float cameraMargin;

        // Sized off the on-foot camera (3.5 orthographic, ~7.1m to the corner at 16:9) and the paddock
        // PaddockSpawner builds: a few hundred metres of pit straight by thirty deep.
        //
        // targetNearPlayer has to sit ABOVE the headcount that would naturally be nearby, or the cap
        // fires before the recycler has added anybody. A full house of 400 over a 400m x 30m paddock puts
        // about 200 inside a 100m radius already, so 280 tops that up by half again in the middle of the
        // paddock and nearly triples it at the ends — where the illusion is worth the most — while
        // leaving a hundred-odd frozen out in the paddock as headroom.
        public static CrowdRecycleTuning Default => new CrowdRecycleTuning
        {
            enabled = true,
            despawnRadius = 100f,
            respawnMinRadius = 14f,
            respawnMaxRadius = 45f,
            targetNearPlayer = 280,
            recyclesPerFrame = 2,
            samplesPerRecycle = 12,
            edgeInset = 0.08f,
            cameraMargin = 3f,
        };
    }

    public static class CrowdRecyclePolicy
    {
        // Whether this NPC should be picked up and moved. Distance is the only thing that qualifies it;
        // the cluster headcount is what decides whether there is anywhere to put it.
        public static bool ShouldRecycle(bool playerOnFoot, float distanceToPlayer, int nearPlayerCount,
                                         in CrowdRecycleTuning tuning)
        {
            if (!tuning.enabled) return false;
            // Nobody is looking, so nothing needs clustering -- and with the player in a car the camera
            // is wide enough that a respawn could land in frame.
            if (!playerOnFoot) return false;
            if (distanceToPlayer <= Mathf.Max(0f, tuning.despawnRadius)) return false;
            if (tuning.targetNearPlayer > 0 && nearPlayerCount >= tuning.targetNearPlayer) return false;
            return true;
        }

        // Distance from the centre of an orthographic frame to its far corner, plus a margin. Anything
        // beyond this is off screen whichever direction it lies in.
        public static float OutOfShotRadius(float orthographicSize, float aspect, float margin)
        {
            float halfHeight = Mathf.Max(0f, orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            return Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth) + Mathf.Max(0f, margin);
        }

        // The authored band, widened so its inner edge clears the camera frame and capped so a respawn
        // never lands beyond the radius that would immediately send it back out again.
        public static CrowdRecycleTuning ClampedToCamera(in CrowdRecycleTuning tuning,
                                                        float orthographicSize, float aspect)
        {
            var t = tuning;
            float outOfShot = OutOfShotRadius(orthographicSize, aspect, t.cameraMargin);
            if (t.respawnMinRadius < outOfShot) t.respawnMinRadius = outOfShot;
            return Sanitised(t);
        }

        // Order the radii and keep them inside the despawn radius, so no combination of inspector
        // values can produce a band that respawns NPCs straight back out again.
        public static CrowdRecycleTuning Sanitised(in CrowdRecycleTuning tuning)
        {
            var t = tuning;
            t.despawnRadius = Mathf.Max(0f, t.despawnRadius);
            t.respawnMinRadius = Mathf.Clamp(t.respawnMinRadius, 0f, t.despawnRadius);
            t.respawnMaxRadius = Mathf.Min(t.respawnMaxRadius, t.despawnRadius);
            if (t.respawnMaxRadius < t.respawnMinRadius) t.respawnMaxRadius = t.respawnMinRadius;
            return t;
        }

        // Radius for a 0..1 roll, distributed evenly by AREA across the band rather than evenly by
        // radius. Lerping the radius straight would pile the crowd against the inner edge of the ring,
        // which is exactly the wall of people this is meant to avoid.
        public static float RadiusFor(float radius01, in CrowdRecycleTuning tuning)
        {
            var t = Sanitised(tuning);
            float min = t.respawnMinRadius, max = t.respawnMaxRadius;
            return Mathf.Sqrt(Mathf.Lerp(min * min, max * max, Mathf.Clamp01(radius01)));
        }

        // One candidate respawn point, from two 0..1 rolls. False when the point falls outside the
        // paddock -- the caller rolls again. Deterministic, so the tests can walk the whole band.
        public static bool TryCandidate(Vector2 player, in CrowdRect area, in CrowdRecycleTuning tuning,
                                        float angle01, float radius01, out Vector2 point)
        {
            float r = RadiusFor(radius01, tuning);
            float a = Mathf.Clamp01(angle01) * Mathf.PI * 2f;
            point = player + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            return area.Contains(point, tuning.edgeInset);
        }
    }
}
