using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // What a piece of an authored road course is allowed to do when the lap is solved.
    public enum RoadPieceKind
    {
        // A named corner. Its angle AND its length are held exactly as authored - this is the part of
        // the circuit that gives it its character, so the solver is not allowed to touch it.
        Corner = 0,

        // A connector between corners. Takes whatever gentle curvature is needed to make the lap's
        // total turning come to exactly one revolution, and its length may be adjusted to close the loop.
        // This is what the "straights" on a real road course actually are.
        Link = 1,

        // A straight that really is straight - a pit straight, a runway, an oval stretch. Never curves.
        // Its length may still be adjusted, and preferentially is: this is where a real circuit has slack.
        Straight = 2,
    }

    public struct RoadPiece
    {
        public RoadPieceKind kind;
        public string label;
        public float length;
        public float angle;      // degrees, +ve = left. Authored only on Corner pieces.
        public float banking;    // degrees. 0 for a flat road course; the oval sections of a roval,
                                 // and Pocono's three unequal corners, carry their real figures.

        public bool IsTurn => Mathf.Abs(angle) > 0.05f;
    }

    // An authored circuit, before it is solved.
    public class RoadCourseSpec
    {
        public string trackId = "NewCircuit";
        public string displayName = "New Circuit";
        public float lengthMiles = 2.5f;
        public float roadWidth = 12f;
        public bool clockwise = true;
        public float lineMargin = 1.2f;
        public int topSpeedMph = 180;
        public int pitSpeedLimitMph = 45;
        public int defaultLaps = 90;
        public bool pitLane = true;
        public List<RoadPiece> pieces = new List<RoadPiece>();
    }

    public struct RoadCheck
    {
        public float lapMetres;
        public float lapMiles;
        public float totalTurnDegrees;
        public float closureErrorMetres;
        public float linkResidualDegrees;
        public float tightestLinkRadius;
        public int namedCorners;

        public string Summary =>
            $"{lapMiles:0.###} mi ({lapMetres:0} m), {namedCorners} corners, turns sum " +
            $"{totalTurnDegrees:0.#}°, closure gap {closureErrorMetres:0.00} m, " +
            $"{linkResidualDegrees:0.#}° of sweep on the links (tightest link radius {tightestLinkRadius:0} m)";
    }

    // Solves a hand-authored road course into a lap that closes and measures its real length.
    //
    // THE PROBLEM. An oval is a formula; a road course is not. But authoring one corner by corner by hand
    // produces a lap that does not shut: guess twenty corner angles off a track map and they will sum to
    // something like 700 degrees, not the 360 that any simple closed loop must turn through, and the two
    // ends of the circuit will miss each other by hundreds of metres.
    //
    // THE INSIGHT. Both errors have somewhere honest to go, and it is the same place: the connecting
    // sections. The "straights" between the corners of a real road course are not straight — Road America's
    // Moraine Sweep and the run down to Canada Corner both bend, and it is exactly that gentle curvature
    // that lets a circuit with eight ninety-degree right-handers still come back to where it started. So:
    //
    //   1. HEADING. Named corners keep the angles they were authored with. Whatever is left over between
    //      their sum and one full revolution is shared across the links in proportion to their length, so
    //      a long sweep takes more of the bend than a short link and nothing turns into an accidental
    //      hairpin. That is `DistributeResidual`.
    //
    //   2. POSITION. Then the loop is shut by changing LENGTHS. This part is exact rather than iterative,
    //      which is worth spelling out: a piece's displacement is linear in its own length (an arc with a
    //      fixed angle just changes radius), and a length change never moves any heading downstream. So
    //      the closure gap is a linear function of the lengths and one weighted least-norm solve puts it
    //      at zero. Straights are weighted to move four times as readily as curved links, which keeps the
    //      correction where a real circuit has slack.
    //
    //   3. LENGTH. Finally every length is scaled by one factor to land the lap on its published distance.
    //      Uniform scaling cannot reopen the loop, because a closed shape stays closed when you scale it.
    //
    // Steps 1 and 2 interact — closing the loop changes the lengths the residual was shared out by — so
    // they alternate until the gap stops moving, which takes a handful of passes.
    //
    // WHAT YOU GET is a circuit with the right lap distance, the right number of corners in the right
    // order with the right relative severity, and the right width of road. What you do NOT get is a
    // survey: the exact position of every apex is authored by eye. WatkinsGlen is the counter-example
    // and the standard — it was measured off satellite imagery, and it is not generated by this at all.
    public static class RoadCourseGeometry
    {
        public const float MetresPerMile = 1609.344f;

        const float MinPieceLength = 40f;
        const float StraightMoveWeight = 4f;   // straights give up length four times as readily as links
        const float LinkMoveWeight = 1f;

        public static RoadPiece Corner(string label, float length, float angleDeg, float bankingDeg = 0f) =>
            new RoadPiece
            {
                kind = RoadPieceKind.Corner, label = label, length = length,
                angle = angleDeg, banking = bankingDeg,
            };

        public static RoadPiece Link(float length) => new RoadPiece
        { kind = RoadPieceKind.Link, label = "Link", length = length };

        public static RoadPiece Straight(string label, float length) => new RoadPiece
        { kind = RoadPieceKind.Straight, label = label, length = length };

        // ------------------------------------------------------------ the solve

        public static List<RoadPiece> Solve(RoadCourseSpec spec)
        {
            var pieces = new List<RoadPiece>();
            if (spec == null || spec.pieces == null || spec.pieces.Count == 0) return pieces;
            pieces.AddRange(spec.pieces);

            float target = spec.clockwise ? -360f : 360f;
            DistributeResidual(pieces, target);

            for (int pass = 0; pass < 60; pass++)
            {
                ClosePosition(pieces);
                Walk(pieces, out var end, out _);
                if (end.magnitude < 0.01f) break;
                DistributeResidual(pieces, target);
            }

            NormaliseLapLength(pieces, Mathf.Max(400f, spec.lengthMiles * MetresPerMile));
            return pieces;
        }

        // Share the leftover turning across the links, weighted by length.
        static void DistributeResidual(List<RoadPiece> pieces, float targetDegrees)
        {
            float named = 0f, linkLength = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].kind == RoadPieceKind.Corner) named += pieces[i].angle;
                else if (pieces[i].kind == RoadPieceKind.Link) linkLength += pieces[i].length;
            }
            if (linkLength <= 0.01f) return;

            float residual = targetDegrees - named;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].kind != RoadPieceKind.Link) continue;
                var p = pieces[i];
                p.angle = residual * (p.length / linkLength);
                pieces[i] = p;
            }
        }

        // Shut the loop by adjusting lengths. One weighted least-norm solve; see the class comment for
        // why this is exact rather than a search.
        static void ClosePosition(List<RoadPiece> pieces)
        {
            var units = new List<Vector2>();
            var weights = new List<float>();
            var indices = new List<int>();

            float heading = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                float w = MoveWeight(p.kind) * p.length;
                if (w > 1e-6f && p.length > 1e-6f)
                {
                    Vector2 local = LocalOffset(p.length, p.angle);
                    units.Add(Rotate(local, heading) / p.length);
                    weights.Add(w);
                    indices.Add(i);
                }
                heading += p.angle * Mathf.Deg2Rad;
            }
            if (units.Count == 0) return;

            Walk(pieces, out var gap, out _);

            // Normal equations of the 2xN weighted system.
            float a = 0f, b = 0f, c = 0f;
            for (int k = 0; k < units.Count; k++)
            {
                a += weights[k] * units[k].x * units[k].x;
                b += weights[k] * units[k].x * units[k].y;
                c += weights[k] * units[k].y * units[k].y;
            }
            float det = a * c - b * b;
            if (Mathf.Abs(det) < 1e-9f) return;

            float tx = -gap.x, ty = -gap.y;
            float m0 = (c * tx - b * ty) / det;
            float m1 = (-b * tx + a * ty) / det;

            for (int k = 0; k < units.Count; k++)
            {
                int i = indices[k];
                var p = pieces[i];
                p.length = Mathf.Max(MinPieceLength, p.length + weights[k] * (units[k].x * m0 + units[k].y * m1));
                pieces[i] = p;
            }
        }

        static float MoveWeight(RoadPieceKind kind) => kind switch
        {
            RoadPieceKind.Straight => StraightMoveWeight,
            RoadPieceKind.Link => LinkMoveWeight,
            _ => 0f,   // named corners are never resized
        };

        // Scale every length to hit the published lap distance. Angles untouched, so the shape - and its
        // closure - survives.
        static void NormaliseLapLength(List<RoadPiece> pieces, float targetMetres)
        {
            float total = 0f;
            for (int i = 0; i < pieces.Count; i++) total += pieces[i].length;
            if (total <= 1f) return;

            float scale = targetMetres / total;
            if (Mathf.Abs(scale - 1f) < 1e-6f) return;

            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                p.length *= scale;
                pieces[i] = p;
            }
        }

        // ------------------------------------------------------------ geometry helpers

        // Where one piece ends, in its own start frame (heading 0 = +x). Linear in length, which is the
        // property the closure solve is built on.
        static Vector2 LocalOffset(float length, float angleDeg)
        {
            if (Mathf.Abs(angleDeg) < 1e-6f) return new Vector2(length, 0f);
            float a = angleDeg * Mathf.Deg2Rad;
            float r = length / Mathf.Abs(a);
            float sign = angleDeg >= 0f ? 1f : -1f;
            return new Vector2(r * Mathf.Sin(Mathf.Abs(a)), sign * r * (1f - Mathf.Cos(a)));
        }

        static Vector2 Rotate(Vector2 v, float headingRad)
        {
            float c = Mathf.Cos(headingRad), s = Mathf.Sin(headingRad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static void Walk(IList<RoadPiece> pieces, out Vector2 end, out float headingDeg)
        {
            Vector2 pos = Vector2.zero;
            float heading = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                pos += Rotate(LocalOffset(pieces[i].length, pieces[i].angle), heading);
                heading += pieces[i].angle * Mathf.Deg2Rad;
            }
            end = pos;
            headingDeg = heading * Mathf.Rad2Deg;
        }

        public static RoadCheck Validate(IList<RoadPiece> pieces)
        {
            var check = new RoadCheck { tightestLinkRadius = float.PositiveInfinity };
            if (pieces == null || pieces.Count == 0) return check;

            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                check.lapMetres += p.length;
                check.totalTurnDegrees += p.angle;
                if (p.kind == RoadPieceKind.Corner) check.namedCorners++;
                if (p.kind == RoadPieceKind.Link)
                {
                    check.linkResidualDegrees += p.angle;
                    if (Mathf.Abs(p.angle) > 0.2f)
                        check.tightestLinkRadius = Mathf.Min(check.tightestLinkRadius,
                                                             p.length / (Mathf.Abs(p.angle) * Mathf.Deg2Rad));
                }
            }

            check.lapMiles = check.lapMetres / MetresPerMile;
            Walk(pieces, out var end, out _);
            check.closureErrorMetres = end.magnitude;
            return check;
        }

        // ------------------------------------------------------------ racing line

        // Wide in, tight apex, wide out — mirrored by which way the corner goes. Positive offsets are to
        // the RIGHT of the direction of travel, matching TrackInfoV2.SegmentRacingLine.
        public static LineOffsets LineFor(RoadPiece piece, float roadWidth, float lineMargin)
        {
            float edge = Mathf.Max(0.5f, roadWidth * 0.5f - lineMargin);
            var line = new LineOffsets
            {
                leftEntry = -edge, leftApex = -edge, leftExit = -edge,
                rightEntry = edge, rightApex = edge, rightExit = edge,
            };

            if (!piece.IsTurn) return line;

            // A left-hander is taken from the right side of the road; a right-hander from the left.
            float outside = piece.angle > 0f ? edge * 0.85f : -edge * 0.85f;
            float inside = piece.angle > 0f ? -edge * 0.7f : edge * 0.7f;

            line.idealEntry = outside;
            line.idealApex = inside;
            line.idealExit = outside * 0.75f;
            return line;
        }

        // Corner speed from the geometry, same model the oval generator uses, with road-course grip and no
        // banking. A link with 600 m of radius comes out flat-out, which is correct — it is a sweep.
        public static int CornerSpeedMph(RoadPiece piece, int topSpeedMph)
        {
            if (!piece.IsTurn) return topSpeedMph;
            float angleRad = Mathf.Abs(piece.angle) * Mathf.Deg2Rad;
            if (angleRad < 1e-4f) return topSpeedMph;

            float radius = piece.length / angleRad;
            const float grip = 1.05f;
            float bank = Mathf.Tan(Mathf.Clamp(piece.banking, 0f, 40f) * Mathf.Deg2Rad);
            float vMs = Mathf.Sqrt(9.81f * radius * (grip + bank));
            return Mathf.Clamp(Mathf.RoundToInt(vMs * 2.237f), 30, topSpeedMph);
        }

        // Pit road, alongside whichever piece opens the lap — the pit straight. Same shape as the oval's:
        // taper away, run parallel, taper back.
        public static List<RoadPiece> BuildPitLane(RoadCourseSpec spec, float pitStraightMetres)
        {
            var lane = new List<RoadPiece>();
            if (spec == null || !spec.pitLane) return lane;

            float laneLength = Mathf.Max(80f, pitStraightMetres * 0.72f);
            float taper = Mathf.Clamp(pitStraightMetres * 0.1f, 25f, 70f);
            float diverge = spec.clockwise ? 5f : -5f;   // away from the infield

            lane.Add(new RoadPiece { kind = RoadPieceKind.Corner, label = "Pit Entry", length = taper, angle = diverge });
            lane.Add(new RoadPiece { kind = RoadPieceKind.Straight, label = "Pit Road", length = laneLength });
            lane.Add(new RoadPiece { kind = RoadPieceKind.Corner, label = "Pit Exit", length = taper, angle = -diverge });
            return lane;
        }

        public static float PitWidth(RoadCourseSpec spec) => Mathf.Max(9f, spec.roadWidth * 0.75f);

        public static float PitExitLineDistance(RoadCourseSpec spec, float pitStraightMetres)
        {
            var lane = BuildPitLane(spec, pitStraightMetres);
            if (lane.Count < 2) return 0f;
            return lane[0].length + lane[1].length * 0.9f;
        }
    }
}
