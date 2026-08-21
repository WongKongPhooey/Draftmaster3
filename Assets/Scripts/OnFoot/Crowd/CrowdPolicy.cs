using UnityEngine;

namespace Draftmaster.Crowd
{
    // How much of a crowd NPC is running this frame.
    //
    // The paddock is meant to look busy, and "busy" is a headcount — but a headcount is also the thing
    // that costs. The way out is that almost none of the crowd is doing anything the player can perceive
    // at any given moment: the on-foot camera is a 3.5m orthographic size (roughly 12m x 7m of world), the
    // ambient chatter notice radius is 6.5m and the interact range is 2.2m. An NPC forty metres away is
    // off-screen, inaudible and unreachable, so the only thing it owes the scene is its silhouette.
    public enum CrowdLod
    {
        // Everything on: wandering, ambient one-liners, conversation, physics.
        Full = 0,
        // Still walking (so nothing visibly pops into motion as the player approaches) but silent and
        // not talkable — both of those need the player within a few metres anyway.
        Reduced = 1,
        // Standing still with every behaviour and the physics body switched off. Renderers are left
        // alone, so the NPC is still drawn exactly where it was — it just stops thinking.
        Frozen = 2,
    }

    // Distances (metres) and per-frame work budget for the crowd director.
    [System.Serializable]
    public struct CrowdTuning
    {
        [Tooltip("Within this distance of the on-foot player an NPC runs everything.")]
        public float fullRadius;
        [Tooltip("Between fullRadius and this, an NPC keeps walking but stops talking. Beyond it, it freezes.")]
        public float reducedRadius;
        [Tooltip("Slack (m) added to a radius before an NPC is allowed to drop to a cheaper level, so one stood on a boundary doesn't flicker between the two.")]
        public float hysteresis;
        [Tooltip("How many NPCs the director re-evaluates per frame. The rest keep their current level, so director cost stays flat however big the crowd gets.")]
        public int evaluationsPerFrame;

        // Sized off the on-foot camera and the interaction ranges, with room to spare on both.
        public static CrowdTuning Default => new CrowdTuning
        {
            fullRadius = 12f,
            reducedRadius = 25f,
            hysteresis = 2f,
            evaluationsPerFrame = 8,
        };
    }

    // Pure decision logic for the crowd. No Unity objects touched, so it is unit-testable and cheap
    // enough to call for every NPC that comes up in the rota.
    public static class CrowdPolicy
    {
        // The level an NPC at `distanceToPlayer` should be running at. When the player is not on foot
        // (in the car, in a menu, mid-cutscene with no body in the scene) nobody in the crowd can be
        // seen up close or spoken to, so the whole crowd freezes regardless of where it is stood.
        public static CrowdLod Evaluate(bool playerOnFoot, float distanceToPlayer, in CrowdTuning tuning)
        {
            if (!playerOnFoot) return CrowdLod.Frozen;
            if (distanceToPlayer <= Mathf.Max(0f, tuning.fullRadius)) return CrowdLod.Full;
            if (distanceToPlayer <= Mathf.Max(0f, tuning.reducedRadius)) return CrowdLod.Reduced;
            return CrowdLod.Frozen;
        }

        // As Evaluate, but an NPC only gives up a level once it is `hysteresis` metres clear of the
        // radius that granted it. Promotion is immediate — being late to wake up is a visible bug,
        // being late to go to sleep is just a few wasted frames.
        public static CrowdLod EvaluateWithHysteresis(CrowdLod current, bool playerOnFoot,
                                                      float distanceToPlayer, in CrowdTuning tuning)
        {
            // Losing the player is not a distance change, so hysteresis doesn't apply to it.
            if (!playerOnFoot) return CrowdLod.Frozen;

            float slack = Mathf.Max(0f, tuning.hysteresis);
            var plain = Evaluate(true, distanceToPlayer, tuning);
            if (plain <= current) return plain;   // promoting (or unchanged): take it straight away

            // Demoting: re-run the test against the widened radii the NPC currently qualifies under.
            var widened = tuning;
            widened.fullRadius += slack;
            widened.reducedRadius += slack;
            return Evaluate(true, distanceToPlayer, widened);
        }

        // Spacing between the crowd members re-evaluated on any one frame. Every NPC is still visited,
        // just on a rota: with 200 NPCs and a budget of 8 the stride is 25, so each is re-evaluated
        // every 25 frames (~0.4s at 60fps) — far quicker than anyone can cross a 13m LOD band.
        public static int StrideFor(int population, int evaluationsPerFrame)
        {
            if (population <= 0) return 1;
            int budget = Mathf.Max(1, evaluationsPerFrame);
            return Mathf.Max(1, Mathf.CeilToInt(population / (float)budget));
        }

        // Whether the NPC at `index` falls in this frame's slice of the rota.
        public static bool TicksThisFrame(int index, int frame, int stride)
        {
            int s = Mathf.Max(1, stride);
            return ((index - frame) % s + s) % s == 0;
        }

        // Worst-case seconds before an NPC is re-evaluated, for a given crowd size and frame rate.
        // Used by the tests to assert the rota still reacts fast enough at large populations.
        public static float WorstCaseLatencySeconds(int population, int evaluationsPerFrame, float fps)
        {
            if (fps <= 0f) return float.PositiveInfinity;
            return StrideFor(population, evaluationsPerFrame) / fps;
        }

        // ---------------------------------------------------------------- what a level costs

        // Whether a behaviour runs at this level. `proximityOnly` marks the ones that need the player
        // within a couple of metres to do anything at all — ambient one-liners and conversations —
        // which are switched off a level earlier than the rest.
        public static bool RunsAt(CrowdLod lod, bool proximityOnly) =>
            proximityOnly ? lod == CrowdLod.Full : lod != CrowdLod.Frozen;

        // Whether the NPC takes part in the 2D simulation at this level. A frozen NPC is scenery: it is
        // out of the broadphase and nothing collides with it.
        public static bool PhysicsRunsAt(CrowdLod lod) => lod != CrowdLod.Frozen;

        // Whether the walk cycle should be parked on the standing pose.
        public static bool StandsStillAt(CrowdLod lod) => lod == CrowdLod.Frozen;

        // ---------------------------------------------------------------- sizing a paddock

        // Roughly how many of `population` NPCs, spread evenly over a paddock `lengthM` x `depthM`, fall
        // inside the reducedRadius of a player somewhere in it — i.e. how many are actually running.
        //
        // This is the number that costs. The total population only costs at scene load and in memory:
        // everyone beyond the radius is frozen, and while the player is driving that is everyone.
        // The player is assumed to be in the middle of the paddock (the worst case for a shallow strip,
        // since both sides of the depth are then occupied).
        public static float ExpectedAwakeCount(int population, float lengthM, float depthM,
                                               in CrowdTuning tuning)
        {
            float area = Mathf.Max(0.01f, lengthM) * Mathf.Max(0.01f, depthM);
            if (population <= 0 || area <= 0f) return 0f;

            float r = Mathf.Max(0f, tuning.reducedRadius);
            // The disc of radius r, clipped to the paddock strip. Approximated by the rectangle the disc
            // spans in each axis, capped by the paddock itself — close enough to size a crowd, and it
            // errs high, which is the safe direction.
            float spanAlong = Mathf.Min(2f * r, lengthM);
            float spanDeep = Mathf.Min(2f * r, depthM);
            float awakeArea = spanAlong * spanDeep;

            return population * Mathf.Clamp01(awakeArea / area);
        }
    }
}
