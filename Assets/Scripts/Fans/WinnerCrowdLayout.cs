using UnityEngine;

namespace Draftmaster.Fans
{
    // Where the race winner's fans stand round them: the plain math behind WinnerFanCrowd, kept here so it is
    // EditMode-testable without a scene.
    //
    // A winner standing still is packed in on every side — rows of people round them, the front row close
    // enough to talk to (inside NPCInteractable's 2.2 m interact range). A winner on the move has the crowd
    // trailing after them in a fan-shaped pack, the front rank at their shoulders, nobody in the way ahead.
    public static class WinnerCrowdLayout
    {
        // Front-row distance from the winner (m). Under the 2.2 m interact range, so whoever is nearest can be
        // talked to without the player stepping in.
        public const float InnerRadius = 1.5f;
        // Each row behind the first stands this much further out (m).
        public const float RowGap = 0.8f;
        // People in the front row; each row behind fits RowGrowth more, the way a crowd fans out.
        public const int FrontRow = 7;
        public const int RowGrowth = 4;
        // Half-width (degrees) of the trailing pack behind a winner on the move.
        public const float TrailHalfAngle = 75f;

        // Which row fan `index` stands in, and its place along that row.
        public static void RowOf(int index, out int row, out int place, out int rowSize)
        {
            row = 0;
            int start = 0;
            int size = FrontRow;
            index = Mathf.Max(0, index);
            while (index >= start + size)
            {
                start += size;
                row++;
                size = FrontRow + row * RowGrowth;
            }
            place = index - start;
            rowSize = size;
        }

        // How many of `count` fans stand in `row` (the last row is usually short).
        static int InRow(int row, int count)
        {
            int start = 0;
            for (int r = 0; r < row; r++) start += FrontRow + r * RowGrowth;
            return Mathf.Clamp(count - start, 0, FrontRow + row * RowGrowth);
        }

        // Where fan `index` of `count` wants to be. `heading` is the way the winner is walking (any length; zero
        // or `moving` false = standing still).
        public static Vector2 Slot(int index, int count, Vector2 winner, Vector2 heading, bool moving)
        {
            count = Mathf.Max(1, count);
            RowOf(index, out int row, out int place, out _);
            int n = Mathf.Max(1, InRow(row, count));
            float radius = InnerRadius + row * RowGap;

            if (moving && heading.sqrMagnitude > 1e-6f)
            {
                // A pack behind: spread across the arc centred on straight back, staggered row to row.
                Vector2 back = -heading.normalized;
                float t = n == 1 ? 0.5f : (place + (row % 2 == 0 ? 0f : 0.5f)) / (n - (row % 2 == 0 ? 1f : 0f));
                float ang = Mathf.Lerp(-TrailHalfAngle, TrailHalfAngle, Mathf.Clamp01(t));
                return winner + Rotate(back, ang) * radius;
            }

            // A ring all the way round, each row turned half a place on from the one in front so the people at
            // the back look between the heads.
            float step = 360f / n;
            float a = (place + (row % 2 == 0 ? 0f : 0.5f)) * step + 90f;
            return winner + Rotate(Vector2.right, a) * radius;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
