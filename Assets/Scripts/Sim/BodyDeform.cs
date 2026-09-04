using UnityEngine;

namespace Draftmaster.Sim
{
    // How one body dents another.
    //
    // The model this replaces was a point and a radius: every vertex within `dentRadius` of the contact was
    // shoved the same way by `1 - dist/radius`. That is a cone — a circular crater centred on the contact,
    // the same shape whatever hit you, from whatever angle, on whatever panel. It reads as a small explosion
    // going off where the cars met, because geometrically that is what it is.
    //
    // Cars do not dent like that. A door struck by a wing folds along the wing. A nose buried in a wall goes
    // flat along the wall. The dent is a PRINT OF THE THING THAT MADE IT, and its width is the width of the
    // face that actually touched, not a number in the inspector.
    //
    // So a dent here is a press, not a blast. The striker's own body is advanced into the panel by `depth`
    // metres, and every vertex left inside it is moved back out along the contact normal. What that leaves
    // behind is the striker's outline: deepest where it is furthest in, tapering to nothing at its edges,
    // and exactly as wide as the contact. Nothing is radial, nothing is centred on a point.
    //
    // The depth is bought with closing speed rather than read off the real overlap, because there is no real
    // overlap to read: VehicleCollision ejects the two bodies every step (positionalSoftness = 1), so by the
    // time anything looks, the cars are touching and nothing more. Severity is the only honest measure of
    // how hard the panel was hit, so severity is what buys the intrusion.
    //
    // Pure maths, no MonoBehaviour, so the geometry is answerable in EditMode — which matters here because
    // the failure this replaces (a symmetrical stamp) is exactly the kind that looks fine in a still.
    public static class BodyDeform
    {
        public enum Shape
        {
            Box,    // another car: an oriented rectangle, and the dent takes its corner/flank profile
            Plane,  // a barrier: an infinite face, and the panel goes flat against it
        }

        // The thing doing the denting, in world space. It carries no depth — how far it is driven in is the
        // struck body's business, since that is where severity meets the bodywork's own stiffness.
        public struct Striker
        {
            public Shape shape;
            public Vector2 inward;   // unit, striker -> struck. The way the panel folds.
            public Vector2 centre;   // Box: its centre. Plane: any point on the face.
            public Vector2 right;    // unit, the box's own long axis in world. Unused by Plane.
            public Vector2 half;     // box half-extents in world metres, along `right` and its perpendicular.

            public static Striker Box(Vector2 centre, Vector2 right, Vector2 half, Vector2 inward)
            {
                return new Striker
                {
                    shape = Shape.Box,
                    centre = centre,
                    right = Unit(right, Vector2.right),
                    half = new Vector2(Mathf.Abs(half.x), Mathf.Abs(half.y)),
                    inward = Unit(inward, Vector2.up),
                };
            }

            public static Striker Plane(Vector2 pointOnFace, Vector2 inward)
            {
                return new Striker
                {
                    shape = Shape.Plane,
                    centre = pointOnFace,
                    right = Vector2.right,
                    half = Vector2.zero,
                    inward = Unit(inward, Vector2.up),
                };
            }

            // A small hammer: a kerb, a stone, a scrape, or an authored dent nobody has a body for. Still a
            // press rather than a blast — it leaves a flat-bottomed crease `width` across, not a cone. Its
            // face sits exactly on `pointOnFace` so the crease lands where it was aimed.
            public static Striker Point(Vector2 pointOnFace, Vector2 inward, float width)
            {
                Vector2 n = Unit(inward, Vector2.up);
                float w = Mathf.Max(1e-3f, width) * 0.5f;
                // Laid out with its long axis ACROSS the strike, so `right` is the face and the depth axis
                // runs back along -inward. The centre sits behind the face by its own half-depth, which puts
                // the face itself exactly on the point.
                return Box(pointOnFace - n * w, new Vector2(-n.y, n.x), new Vector2(w, w), n);
            }

            static Vector2 Unit(Vector2 v, Vector2 fallback)
            {
                return v.sqrMagnitude > 1e-10f ? v.normalized : fallback;
            }
        }

        // A contact is ONE event, and two deformable bodies have to split it between them.
        //
        // Give both of them the whole thing and each panel retreats by the full fold depth — away from the
        // other car, in opposite directions — so where the two of them were touching, or buried in each
        // other, a VOID opens up exactly `2 * depth - burial` wide. The cars come out dented but visibly not
        // in contact, which looks worse than no damage at all: two cars near each other with a hole between
        // them where the crash was. Halving each is not a fudge to close that hole, it is the double-count
        // being removed — the metal that disappears from the pair is the metal one impact folded, not two.
        //
        // The shares must therefore sum to 1, and where the split falls is a question of which body gives
        // way first. Mass stands in for that here: the lighter car folds more, which is the right direction
        // and is free, since a contact already has both masses to hand for its impulse. A wall is not a body
        // and does not give at all, so a car that hits one takes the whole contact — pass RigidPartner.
        //
        // WHY THE SHARES ALONE ARE NOT ENOUGH, and the rule that comes out of it. Both panels measure the
        // same intrusion, so the metal the pair give up between them is
        //
        //     foldA + foldB = intrusion * (shareA + shareB) = intrusion
        //
        // and their two surfaces land on the same plane if and only if that equals the distance the bodies
        // are ACTUALLY inside each other. Intrusion is real burial plus virtual `press`, so:
        //
        //     void between the panels = press
        //
        // exactly. Any press at all folds metal that nothing is occupying, and the pair come apart by that
        // much however the shares are set. So press is for contacts where the solver has already pushed the
        // bodies apart and there is no real overlap left to read (a race: the cars separate anyway, so the
        // gap is invisible and the speed has to buy the damage some other way). Where the bodies genuinely
        // stay inside each other — the title tableau, which holds them buried on purpose — press must be
        // ZERO and the burial does all the work, or a hole opens up down the middle of the crash.
        public const float RigidPartner = 1f;

        public static float Share(float myMass, float otherMass)
        {
            float mine = Mathf.Max(1f, myMass);
            float theirs = Mathf.Max(1f, otherMass);
            return theirs / (mine + theirs);
        }

        // How far `p` must travel along `striker.inward` to get back out of the striker, once the striker has
        // been driven a further `press` in. Zero if `p` was never inside it.
        //
        // This one function is the whole difference from the old crater. It is a slab ray-cast, not a
        // distance-to-a-point: the answer is large in the middle of the contacting face and falls to zero at
        // that face's edges, so the profile of the dent IS the profile of the striker.
        //
        // It is deliberately NOT clamped to `press`, and that matters. The answer is the TOTAL intrusion —
        // however far the two bodies are already inside each other, plus whatever `press` drives on top. Two
        // cars settled 26px into each other read 26px here before any press at all, which is exactly right:
        // the panel has to get out of the way of metal that is genuinely occupying its space. The caller
        // takes its SHARE of that (see Share) and its own maxDent caps the rest.
        public static float Intrusion(in Striker striker, Vector2 p, float press)
        {
            if (striker.shape == Shape.Plane)
            {
                // Distance from the face, positive on our side. Once the face is driven a further `press`
                // in, anything nearer than that has to come back out to meet it — and a point already
                // BEHIND the face (s negative, the panel buried in the wall) has that much further to come.
                float s = Vector2.Dot(p - striker.centre, striker.inward);
                return Mathf.Max(0f, press - s);
            }

            Vector2 c = striker.centre + striker.inward * press;
            Vector2 a = striker.right;
            Vector2 b = new Vector2(-a.y, a.x);

            Vector2 d = p - c;
            float la = Vector2.Dot(d, a), lb = Vector2.Dot(d, b);
            if (Mathf.Abs(la) >= striker.half.x || Mathf.Abs(lb) >= striker.half.y) return 0f;

            // Ray from p along inward: the shortest run that leaves both slabs.
            float na = Vector2.Dot(striker.inward, a), nb = Vector2.Dot(striker.inward, b);
            float t = float.PositiveInfinity;
            if (Mathf.Abs(na) > 1e-6f) t = Mathf.Min(t, (Mathf.Sign(na) * striker.half.x - la) / na);
            if (Mathf.Abs(nb) > 1e-6f) t = Mathf.Min(t, (Mathf.Sign(nb) * striker.half.y - lb) / nb);
            if (float.IsInfinity(t)) return 0f;

            return Mathf.Max(0f, t);
        }

        // Bodywork is one sheet, so the metal beside a fold comes with it. Without this the press leaves a
        // clean stamp of the striker with a hard edge, which is its own kind of wrong: panels crease and
        // buckle outside the contact rather than shearing along its outline.
        //
        // A few Laplacian passes over the DISPLACEMENT field, restricted to the vertices the press touched
        // plus one ring beyond, so old damage elsewhere on the car is never blurred away by a new hit.
        // `weight` is the rigid-core mask: a vertex that cannot bend cannot drag either.
        public static void Crumple(Vector3[] displacement, int vertsX, int vertsY, float[] weight,
                                   bool[] region, float spread, int iterations, ref Vector3[] scratch)
        {
            if (displacement == null || region == null) return;
            int n = vertsX * vertsY;
            if (n <= 0 || displacement.Length < n || region.Length < n) return;
            if (spread <= 0f || iterations <= 0) return;

            if (scratch == null || scratch.Length < n) scratch = new Vector3[n];

            for (int pass = 0; pass < iterations; pass++)
            {
                System.Array.Copy(displacement, scratch, n);

                for (int y = 0; y < vertsY; y++)
                {
                    for (int x = 0; x < vertsX; x++)
                    {
                        int i = y * vertsX + x;
                        if (!region[i]) continue;

                        float w = weight != null ? weight[i] : 1f;
                        if (w <= 0f) continue;

                        Vector3 sum = Vector3.zero;
                        int count = 0;
                        if (x > 0)          { sum += displacement[i - 1];      count++; }
                        if (x < vertsX - 1) { sum += displacement[i + 1];      count++; }
                        if (y > 0)          { sum += displacement[i - vertsX]; count++; }
                        if (y < vertsY - 1) { sum += displacement[i + vertsX]; count++; }
                        if (count == 0) continue;

                        scratch[i] = Vector3.Lerp(displacement[i], sum / count, Mathf.Clamp01(spread) * w);
                    }
                }

                System.Array.Copy(scratch, displacement, n);
            }
        }

        // Grow a touched set by one ring, so the crumple has somewhere to spread into.
        public static void Dilate(bool[] region, int vertsX, int vertsY, ref bool[] scratch)
        {
            if (region == null) return;
            int n = vertsX * vertsY;
            if (n <= 0 || region.Length < n) return;

            if (scratch == null || scratch.Length < n) scratch = new bool[n];
            System.Array.Copy(region, scratch, n);

            for (int y = 0; y < vertsY; y++)
            {
                for (int x = 0; x < vertsX; x++)
                {
                    int i = y * vertsX + x;
                    if (!scratch[i]) continue;
                    if (x > 0)          region[i - 1] = true;
                    if (x < vertsX - 1) region[i + 1] = true;
                    if (y > 0)          region[i - vertsX] = true;
                    if (y < vertsY - 1) region[i + vertsX] = true;
                }
            }
        }
    }
}
