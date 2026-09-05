using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Crowd
{
    // Who arrives with whom, and where they stand once they get there.
    //
    // A paddock where every one of four hundred people walks their own errand reads as four hundred
    // strangers who have never met. Real ones turn up in twos and threes: a driver and his engineer, a
    // family with a programme between them, three mechanics stood round a tyre arguing about it. The
    // difference is not more people, it is that some of the people are obviously together.
    //
    // This module decides only the numbers -- how the headcount splits into company, and the offsets each
    // member stands at relative to the one leading them. It touches no GameObjects, so the shapes are
    // unit-testable; PaddockWalker is what walks anybody to them.

    // How much company the crowd keeps.
    [System.Serializable]
    public struct CrowdGroupTuning
    {
        [Tooltip("Share of the wandering crowd who turn up with company rather than on their own. 0 = the " +
                 "old crowd of individuals, 1 = nobody walks alone.")]
        [Range(0f, 1f)] public float groupedFraction;
        [Tooltip("Smallest group. Two is a pair; there is no such thing as a group of one.")]
        public int minSize;
        [Tooltip("Largest group. Past about four a huddle stops reading as a conversation and starts " +
                 "reading as a queue.")]
        public int maxSize;
        [Tooltip("Metres between neighbours in a standing huddle -- the gap between two people stood side " +
                 "by side in it. Close enough to be talking, far enough not to overlap.")]
        public float spacing;

        // Just under half the crowd in company. Enough that a walk down the paddock keeps passing pairs
        // and threes, not so much that the place looks like it arrived on coaches.
        public static CrowdGroupTuning Default => new CrowdGroupTuning
        {
            groupedFraction = 0.45f,
            minSize = 2,
            maxSize = 4,
            spacing = 0.85f,
        };
    }

    public static class CrowdGrouping
    {
        // Nothing sensible happens past this: a "group" that big is a crowd scene of its own, and the ring
        // it stands in gets wide enough to block an aisle.
        public const int MaxGroupSize = 8;

        // Inspector values are typed by hand, so order them before anything is derived from them.
        public static CrowdGroupTuning Sanitised(in CrowdGroupTuning tuning)
        {
            var t = tuning;
            t.groupedFraction = Mathf.Clamp01(t.groupedFraction);
            t.minSize = Mathf.Clamp(t.minSize, 2, MaxGroupSize);
            t.maxSize = Mathf.Clamp(t.maxSize, t.minSize, MaxGroupSize);
            t.spacing = Mathf.Max(0.1f, t.spacing);
            return t;
        }

        // A group size for a 0..1 roll, evenly spread across [minSize, maxSize].
        public static int SizeFor(float roll, in CrowdGroupTuning tuning)
        {
            var t = Sanitised(tuning);
            int span = t.maxSize - t.minSize + 1;
            int pick = t.minSize + Mathf.FloorToInt(Mathf.Clamp01(roll) * span);
            return Mathf.Clamp(pick, t.minSize, t.maxSize);
        }

        // How `population` walkers split up: a list of group sizes that sums to exactly the population,
        // with roughly `groupedFraction` of the people in a group of two or more and the rest walking on
        // their own (size 1).
        //
        // `roll` supplies 0..1 values -- UnityEngine.Random.value in play, a seeded generator in a test.
        // The order of the list carries no meaning: the caller drops each group at its own random spot in
        // the paddock, so groups first and singles after is the same paddock as any other ordering.
        public static List<int> Plan(int population, in CrowdGroupTuning tuning, System.Func<float> roll = null)
        {
            var sizes = new List<int>();
            if (population <= 0) return sizes;

            var t = Sanitised(tuning);
            if (roll == null) roll = () => Random.value;

            int wantGrouped = Mathf.RoundToInt(population * t.groupedFraction);
            int grouped = 0;
            int remaining = population;

            while (remaining > 0)
            {
                // A group needs enough people left to be one, and only until the share is met. Everybody
                // after that walks alone -- which is still most of the paddock.
                int size = 1;
                if (grouped < wantGrouped && remaining >= t.minSize)
                {
                    size = Mathf.Min(SizeFor(roll(), t), remaining);
                    grouped += size;
                }
                sizes.Add(size);
                remaining -= size;
            }
            return sizes;
        }

        // How many of `sizes` are actually company (two or more).
        public static int PeopleInCompany(IReadOnlyList<int> sizes)
        {
            if (sizes == null) return 0;
            int n = 0;
            for (int i = 0; i < sizes.Count; i++) if (sizes[i] >= 2) n += sizes[i];
            return n;
        }

        // ---------------------------------------------------------------- where they stand

        // Radius of the ring `count` people stand in to leave `spacing` between neighbours. Solved from
        // the chord, so a bigger group stands in a wider circle rather than a tighter one.
        public static float HuddleRadius(int count, float spacing)
        {
            if (count <= 1) return 0f;
            float s = Mathf.Max(0.01f, spacing);
            if (count == 2) return s * 0.5f;
            return s / (2f * Mathf.Sin(Mathf.PI / count));
        }

        // Where member `index` of a stopped group of `count` stands, in the group's own frame: +y is the
        // way the group was walking when it stopped, +x is its right.
        //
        // Slot 0 is the leader, at the front of the ring, so the rest of the group is behind them and the
        // leader turns round to face it. The exception is a pair, who stand shoulder to shoulder across
        // the line of travel instead: two people walking together walk side by side, and one directly
        // behind the other reads as a coincidence rather than as company.
        public static Vector2 HuddleSlot(int index, int count, float spacing)
        {
            if (count <= 1) return Vector2.zero;
            int i = ((index % count) + count) % count;
            float r = HuddleRadius(count, spacing);
            float offset = count == 2 ? Mathf.PI * 0.5f : 0f;
            float a = offset + i * (Mathf.PI * 2f) / count;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
        }

        // Where member `index` walks while the group is on the move, measured from the leader in the same
        // frame: the standing ring, drawn in a little across the line of travel and stretched out along
        // it. A moving group strings out and a stopped one closes up, and because one shape is the other
        // shape, halting is a shuffle of a few centimetres rather than everybody crossing the group to
        // find a new place.
        public static Vector2 WalkingSlot(int index, int count, float spacing)
        {
            if (count <= 1 || index <= 0) return Vector2.zero;
            Vector2 rel = HuddleSlot(index, count, spacing) - HuddleSlot(0, count, spacing);
            return new Vector2(rel.x * 0.85f, rel.y * 1.25f);
        }

        // A slot in the group's frame, turned into a world offset. `forward` is the direction the group is
        // facing (or last walked in); a zero one falls back to world up so a slot is never lost.
        public static Vector2 Rotate(Vector2 local, Vector2 forward)
        {
            Vector2 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector2.up;
            Vector2 right = new Vector2(f.y, -f.x);
            return right * local.x + f * local.y;
        }

        // The centre of the ring a group led from `leaderPosition` stands in. Derived rather than stored,
        // so it survives the leader being picked up and put down somewhere else in the paddock.
        public static Vector2 HuddleCentre(Vector2 leaderPosition, int count, float spacing, Vector2 forward)
        {
            if (count <= 1) return leaderPosition;
            return leaderPosition - Rotate(HuddleSlot(0, count, spacing), forward);
        }

        // Where member `index` of a group led from `leaderPosition` should be right now: in the pack while
        // it is moving, on the ring once it has stopped.
        public static Vector2 SlotWorld(int index, int count, float spacing, Vector2 leaderPosition,
                                        Vector2 forward, bool moving)
        {
            if (count <= 1 || index <= 0) return leaderPosition;
            if (moving) return leaderPosition + Rotate(WalkingSlot(index, count, spacing), forward);
            return HuddleCentre(leaderPosition, count, spacing, forward)
                   + Rotate(HuddleSlot(index, count, spacing), forward);
        }
    }
}
