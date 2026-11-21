using UnityEngine;

namespace Draftmaster.Sim
{
    // Where each wheel bolts onto its axle, and whether the bodywork still covers it.
    //
    // A car's shell is a deformable mesh (VehicleDamage) folded in by every hit. Underneath it, at four fixed points
    // on the chassis, sit the hubs — the places a wheel joins the axle. They do not move with the panels: they are
    // part of the frame. While the bodywork outline still wraps a hub, the corner is bent but the wheel is on. Once a
    // hit has folded the outline in PAST a hub, the corner has been crushed back through the suspension and the wheel
    // is gone — a car in that state is not driving anywhere, however low its overall damage figure reads.
    //
    // Hubs are authored in sprite fractions, the same frame the safety cell is: x runs along the car from the nose
    // (0) to the tail (1) — the liveries lie nose at sprite x=0 — and y across it, door to door. The defaults are read
    // off the 64x32 liveries (12.8 px/m, so a 5 m x 2.5 m car): the tyres poke out of the body at pixels 5-15 (front)
    // and 43-53 (rear) along its length. A wheel is about 0.3 m wide (TyreWidth), and the hub - where it joins the
    // axle - is inboard of all of it: a full wheel width in from the side of the body. So a dent has to fold the side
    // in by more than a whole wheel before the axle is exposed; a pack rub that only creases the panel over the tyre
    // leaves it on. (The first cut sat the hubs 0.2 m in, under the tyre, and a 3 m/s nudge at Daytona took a wheel
    // off five seconds after the green, 2026-11-21.)
    //
    // Pure geometry, so it can be pinned in EditMode and headless.
    public static class WheelHubs
    {
        // Front-left, front-right, rear-left, rear-right — in sprite fractions (see the header). Which of y 0 and 1 is
        // "left" depends on how the car is turned; the names are only for reading logs.
        public const float TyreWidth = 0.3f;     // metres
        public const float BodyWidth = 2.5f;     // metres, the 32 px liveries at 12.8 px/m
        const float Inset = TyreWidth / BodyWidth;

        public static readonly Vector2[] Default =
        {
            new Vector2(0.17f, Inset),
            new Vector2(0.17f, 1f - Inset),
            new Vector2(0.76f, Inset),
            new Vector2(0.76f, 1f - Inset),
        };

        public static string Name(int index) => index switch
        {
            0 => "front-left",
            1 => "front-right",
            2 => "rear-left",
            3 => "rear-right",
            _ => $"wheel {index}",
        };

        // A hub's position in the bodywork mesh's local frame, for a mesh `size` across built centred on the origin
        // (VehicleDamage lays its grid out from -size/2 to +size/2).
        public static Vector2 LocalPoint(Vector2 fraction, Vector2 size) =>
            new Vector2(-size.x * 0.5f + size.x * fraction.x, -size.y * 0.5f + size.y * fraction.y);

        // Whether `point` is still inside the outline of the deformed bodywork: the outer ring of a `vx` x `vy` vertex
        // grid laid out row by row (index = y * vx + x), as VehicleDamage builds it. Nonzero winding, so a panel folded
        // over itself does not flip the answer the way an even-odd count would.
        public static bool Covered(Vector2 point, Vector3[] grid, int vx, int vy)
        {
            if (grid == null || vx < 2 || vy < 2 || grid.Length < vx * vy) return true;

            int winding = 0;
            int n = RingLength(vx, vy);
            Vector2 a = RingVertex(grid, vx, vy, n - 1);
            for (int k = 0; k < n; k++)
            {
                Vector2 b = RingVertex(grid, vx, vy, k);
                if (a.y <= point.y)
                {
                    if (b.y > point.y && Cross(a, b, point) > 0f) winding++;
                }
                else if (b.y <= point.y && Cross(a, b, point) < 0f) winding--;
                a = b;
            }
            return winding != 0;
        }

        // Exposed = no longer covered by the bodywork: the wheel on this hub has come off.
        public static bool Exposed(Vector2 point, Vector3[] grid, int vx, int vy) => !Covered(point, grid, vx, vy);

        // The outer ring, walked once round: along row 0, up the last column, back along the top row, down column 0.
        static int RingLength(int vx, int vy) => 2 * (vx - 1) + 2 * (vy - 1);

        static Vector2 RingVertex(Vector3[] grid, int vx, int vy, int k)
        {
            int w = vx - 1, h = vy - 1;
            if (k < w) return grid[k];                                   // row 0, x = 0 .. w-1
            k -= w;
            if (k < h) return grid[k * vx + w];                          // column w, y = 0 .. h-1
            k -= h;
            if (k < w) return grid[h * vx + (w - k)];                    // row h, x = w .. 1
            k -= w;
            return grid[(h - k) * vx];                                   // column 0, y = h .. 1
        }

        // > 0 when `p` is left of the directed edge a -> b.
        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);
    }
}
