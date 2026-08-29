using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // Mirror of Draftmaster.Data.TrackType, value for value. Duplicated because that enum lives in
    // Assembly-CSharp alongside the SQLite models, which an assembly definition cannot reference — the same
    // split the fight system uses. TrackProfile casts between the two.
    public enum TrackKind
    {
        ShortTrack = 0,
        Speedway = 1,
        Superspeedway = 2,
        RoadCourse = 3,
        DirtCourse = 4,
    }

    // Lateral racing-line offsets for one segment, in metres. Positive = right of the direction of travel,
    // matching TrackInfoV2.SegmentRacingLine.
    public struct LineOffsets
    {
        public float idealEntry, idealApex, idealExit;
        public float leftEntry, leftApex, leftExit;
        public float rightEntry, rightApex, rightExit;
    }

    // One piece of a generated layout, in the same terms TrackInfoV2 stores: a length along the centreline,
    // a heading change, banking, and the racing line through it.
    public struct OvalSegment
    {
        public string label;
        public bool isTurn;
        public float length;
        public float angle;        // degrees; positive = left
        public float banking;
        public float leadIn, leadOut;
        public int maxSpeedMph;
        public float width;        // 0 = inherit the track default
        public LineOffsets line;
    }

    // What kind of oval to lay down. Every field is a plain number so a designer can nudge one and rebuild.
    [System.Serializable]
    public class OvalSpec
    {
        public string trackId = "NewOval";
        public string displayName = "New Oval";
        public float lengthMiles = 1.5f;

        [Tooltip("Corners in the lap. 4 = a conventional oval (two per end), 2 = a paperclip with one 180 at each end.")]
        public int corners = 4;
        public float turnBanking = 12f;
        public float straightBanking = 0f;
        [Tooltip("Fraction of the lap spent cornering. A bullring is nearly half corner; a flat 2.5-miler much less.")]
        [Range(0.15f, 0.7f)] public float turnShareOfLap = 0.42f;
        [Tooltip("Straight split. 0.5 = front and back equal; higher = the longer front stretch most ovals have.")]
        [Range(0.3f, 0.7f)] public float frontStraightShare = 0.5f;
        [Tooltip("Short connecting chute between the two corners at each end (m). 0 = one continuous sweep.")]
        public float chuteMetres = 0f;
        [Tooltip("Tri-oval dog-leg on the front stretch (deg). 0 = a straight front stretch. Daytona is about 6.")]
        public float frontKinkDegrees = 0f;

        public float roadWidth = 16f;
        [Tooltip("How far inside the edge the outermost AI line runs (m).")]
        public float lineMargin = 1.5f;
        public int topSpeedMph = 190;

        public bool pitLane = true;
        public int pitSpeedLimitMph = 45;
        [Range(0.3f, 0.95f)] public float pitLengthShare = 0.75f;

        public int defaultLaps = 200;
        [Tooltip("Left-hand (counter-clockwise), like every oval in the series. Off mirrors the layout.")]
        public bool leftHanded = true;
    }

    // The shape of a generated lap, reported back for sanity checks and the editor log line.
    public struct OvalCheck
    {
        public float lapMetres;
        public float lapMiles;
        public float totalTurnDegrees;
        public float closureErrorMetres;
        public bool hasPitLane;

        public string Summary =>
            $"{lapMiles:0.###} mi ({lapMetres:0} m), turns sum {totalTurnDegrees:0.#}°, " +
            $"closure gap {closureErrorMetres:0.0} m, pit lane {(hasPitLane ? "yes" : "no")}";
    }

    // Solves an oval from its lap length.
    //
    // Most of a 35-round calendar is ovals, and an oval is a solved shape: two or four corners, two
    // straights, and a racing line that runs wide-in, tight-apex, wide-out every time. Hand-authoring
    // twenty-five of those segment by segment produces layouts that don't close and eats a week. This
    // builds one that does — the corner angles sum to exactly 360 degrees, the straights are solved so the
    // lap comes out the requested length, and the racing line and corner speeds come out with it.
    //
    // It is a starting point, not a finished track: real ovals have unequal radii, progressive banking and
    // asymmetric straights. Generate, then tune in the inspector with TrackBuilder's gizmo on.
    public static class OvalGeometry
    {
        public const float MetresPerMile = 1609.344f;

        // Type-appropriate starting numbers. Shared with TrackTuning so a change to what a superspeedway
        // "is" shows up in both the sim tuning and every layout generated afterwards.
        public static OvalSpec Preset(TrackKind kind, string trackId, string displayName,
                                      float lengthMiles, float banking, int laps)
        {
            var tuning = TrackTuning.For(kind);
            var spec = new OvalSpec
            {
                trackId = trackId,
                displayName = displayName,
                lengthMiles = lengthMiles,
                turnBanking = banking,
                turnShareOfLap = tuning.turnShareOfLap,
                roadWidth = tuning.roadWidth,
                pitSpeedLimitMph = tuning.pitSpeedLimitMph,
                defaultLaps = laps,
            };

            switch (kind)
            {
                case TrackKind.Superspeedway:
                    spec.corners = 4;
                    spec.frontStraightShare = 0.56f;
                    spec.frontKinkDegrees = 6f;      // tri-oval
                    spec.topSpeedMph = 200;
                    break;

                case TrackKind.ShortTrack:
                    spec.corners = 2;                // a bullring is a paperclip
                    spec.topSpeedMph = 125;
                    break;

                case TrackKind.DirtCourse:
                    spec.corners = 4;
                    spec.topSpeedMph = 110;
                    break;

                default:
                    spec.corners = 4;
                    spec.frontStraightShare = 0.52f;
                    spec.topSpeedMph = 180;
                    break;
            }

            return ApplyTrackShape(trackId, spec);
        }

        // Layer the venue's real published dimensions over the type defaults.
        //
        // This used to be a hand-written switch of shape notes, and the width came from the track TYPE —
        // every superspeedway 18 m, every short track 13 m. That is wrong in a way you feel from the
        // driver's seat: Michigan is 73 feet wide and Dover is 40, and both are "speedways". TrackDimensions
        // holds the published figure for each venue, so a track that is in that table gets its own numbers
        // and one that is not still falls back to a sensible layout for its type.
        public static OvalSpec ApplyTrackShape(string trackId, OvalSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(trackId)) return spec;
            if (!TrackDimensions.TryGet(trackId, out var dim)) return spec;

            spec.displayName = dim.displayName;
            spec.lengthMiles = dim.lapMiles;
            spec.roadWidth = dim.widthMetres;
            spec.turnBanking = dim.turnBankingDeg;
            spec.straightBanking = dim.straightBankingDeg;
            spec.pitSpeedLimitMph = dim.pitSpeedLimitMph;
            if (dim.cupLaps > 0) spec.defaultLaps = dim.cupLaps;
            if (dim.corners >= 2) spec.corners = dim.corners;
            if (dim.turnShareOfLap > 0.01f) spec.turnShareOfLap = dim.turnShareOfLap;
            spec.frontKinkDegrees = dim.frontKinkDeg;

            // The AI lines are pinned a fixed distance in from the wall, so on a 40 ft track the default
            // 1.5 m margin leaves almost nothing between them. Scale it with the road instead.
            spec.lineMargin = Mathf.Clamp(dim.widthMetres * 0.1f, 0.8f, 2.2f);

            return spec;
        }

        // ------------------------------------------------------------ the solve

        // Solve a layout that closes AND measures the right length.
        //
        // Those two constrain each other hard, and the constraint is worth stating plainly because it is
        // not obvious: two straights joined by two semicircular ends can only close if the straights are
        // the SAME LENGTH. No amount of skewing the corners or varying their radius rescues an oval whose
        // front stretch is authored longer than its back — the loop just doesn't shut (this was measured,
        // not assumed: a 2.5-mile oval with a 56/44 split leaves a 254 m gap, and no corner geometry closes it).
        //
        // What makes a real front stretch longer is therefore not an authored split — it's the SHAPE.
        // Daytona's tri-oval bows out toward the grandstand, so the front stretch covers more tarmac than
        // the straight-line distance it spans. So the dog-leg is the input here, and the straight split is
        // the OUTPUT: given the kink, the back stretch is solved so the lap closes exactly, which lands the
        // front stretch naturally longer by however much the bow is worth.
        //
        // Lengths are then scaled uniformly to hit the target lap distance, which leaves the closure alone
        // because the whole shape scales together.
        public static List<OvalSegment> Build(OvalSpec spec)
        {
            if (spec == null) return new List<OvalSegment>();

            float back = SolveBackStretch(spec);
            var segments = Layout(spec, back);
            NormaliseLapLength(segments, Mathf.Max(200f, spec.lengthMiles * MetresPerMile));
            return segments;
        }

        // Back-stretch length that shuts the loop, by bisection on the closure gap. Straight-line search
        // rather than anything clever: the gap is monotonic either side of the solution and this runs a few
        // dozen times at author time, never in a race.
        static float SolveBackStretch(OvalSpec spec)
        {
            float nominal = NominalStraightLength(spec);
            float lo = nominal * 0.5f, hi = nominal * 1.6f;

            float Gap(float back) => Validate(Layout(spec, back)).closureErrorMetres;

            for (int i = 0; i < 80 && hi - lo > 1e-3f; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Gap(mid - 0.5f) < Gap(mid + 0.5f)) hi = mid;
                else lo = mid;
            }
            return (lo + hi) * 0.5f;
        }

        // Half of what's left over once the corners have taken their share — the starting guess for both
        // straights, and exactly right for a track with no dog-leg.
        static float NominalStraightLength(OvalSpec spec)
        {
            int corners = Mathf.Max(2, spec.corners);
            float lap = Mathf.Max(200f, spec.lengthMiles * MetresPerMile);
            float turnTotal = Mathf.Max(60f, lap * Mathf.Clamp(spec.turnShareOfLap, 0.15f, 0.7f));
            int chuteCount = corners > 2 ? corners - 2 : 0;
            float chuteTotal = spec.chuteMetres > 0.5f ? spec.chuteMetres * chuteCount : 0f;
            return Mathf.Max(20f, (lap - turnTotal - chuteTotal) * 0.5f);
        }

        // Scale every length so the lap measures exactly what was asked for. Angles are untouched, so the
        // shape — and its closure — is preserved.
        static void NormaliseLapLength(List<OvalSegment> segments, float targetMetres)
        {
            float total = 0f;
            for (int i = 0; i < segments.Count; i++) total += segments[i].length;
            if (total <= 1f) return;

            float scale = targetMetres / total;
            if (Mathf.Abs(scale - 1f) < 1e-5f) return;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                seg.length *= scale;
                seg.leadIn *= scale;
                seg.leadOut *= scale;
                if (seg.isTurn && Mathf.Abs(seg.angle) > 1f)
                    seg.maxSpeedMph = CornerSpeedMph(seg.length, seg.angle, seg.banking);
                segments[i] = seg;
            }
        }

        // One pass of the layout with a given back-stretch length. The front stretch always runs at its
        // nominal length; the solver moves the back one until the loop shuts.
        static List<OvalSegment> Layout(OvalSpec spec, float backLength)
        {
            var segments = new List<OvalSegment>();
            if (spec == null) return segments;

            int corners = Mathf.Max(2, spec.corners);
            float lap = Mathf.Max(200f, spec.lengthMiles * MetresPerMile);
            float turnShare = Mathf.Clamp(spec.turnShareOfLap, 0.15f, 0.7f);
            float sign = spec.leftHanded ? 1f : -1f;

            // Corners: equal arcs summing to a full lap of heading change, so the heading closes by
            // construction and only the position has to be solved for.
            float turnTotal = Mathf.Max(60f, lap * turnShare);
            float turnLength = turnTotal / corners;
            float turnAngle = 360f / corners * sign;

            float frontLength = NominalStraightLength(spec);
            float outer = Mathf.Max(0.5f, spec.roadWidth * 0.5f - spec.lineMargin);

            AddFrontStretch(segments, spec, frontLength, outer, sign);

            int cornersPerEnd = Mathf.Max(1, corners / 2);
            int cornersFarEnd = corners - cornersPerEnd;
            int turnNumber = 1;

            for (int c = 0; c < cornersPerEnd; c++)
            {
                segments.Add(Turn($"Turn {turnNumber++}", turnLength, turnAngle, spec, outer, sign));
                if (spec.chuteMetres > 0.5f && c < cornersPerEnd - 1)
                    segments.Add(Straight("Chute", spec.chuteMetres, spec, -outer * 0.6f * sign));
            }

            segments.Add(Straight("Back Stretch", Mathf.Max(20f, backLength), spec, 0f));

            for (int c = 0; c < cornersFarEnd; c++)
            {
                segments.Add(Turn($"Turn {turnNumber++}", turnLength, turnAngle, spec, outer, sign));
                if (spec.chuteMetres > 0.5f && c < cornersFarEnd - 1)
                    segments.Add(Straight("Chute", spec.chuteMetres, spec, -outer * 0.6f * sign));
            }

            return segments;
        }

        // Pit road: a lane parallel to the front stretch, angled in off the last corner and angled back out
        // at the end of the front stretch — what every oval's pit road looks like from above.
        public static List<OvalSegment> BuildPitLane(OvalSpec spec, float frontStretchMetres)
        {
            var lane = new List<OvalSegment>();
            if (spec == null || !spec.pitLane) return lane;

            float sign = spec.leftHanded ? 1f : -1f;
            float laneLength = Mathf.Max(60f, frontStretchMetres * Mathf.Clamp(spec.pitLengthShare, 0.3f, 0.95f));
            float taper = Mathf.Clamp(frontStretchMetres * 0.08f, 25f, 90f);
            float width = PitWidth(spec);
            float diverge = 5f * -sign;   // away from the infield, so pit road never crosses the racing surface

            lane.Add(new OvalSegment
            {
                label = "Pit Entry", isTurn = true, length = taper, angle = diverge,
                maxSpeedMph = spec.pitSpeedLimitMph, width = width,
            });
            lane.Add(new OvalSegment
            {
                label = "Pit Road", isTurn = false, length = laneLength,
                maxSpeedMph = spec.pitSpeedLimitMph, width = width,
            });
            lane.Add(new OvalSegment
            {
                label = "Pit Exit", isTurn = true, length = taper, angle = -diverge,
                maxSpeedMph = spec.pitSpeedLimitMph, width = width,
            });
            return lane;
        }

        // Pit road is its own width where the venue publishes one (Indianapolis' pit road is narrow
        // relative to its 50 ft straights), otherwise a fraction of the racing surface.
        public static float PitWidth(OvalSpec spec)
        {
            if (spec != null && TrackDimensions.TryGet(spec.trackId, out var dim) && dim.pitWidthMetres > 0.5f)
                return dim.pitWidthMetres;
            return Mathf.Max(9f, spec.roadWidth * 0.6f);
        }

        // Where the limiter releases: just before the merge back onto the track.
        public static float PitExitLineDistance(OvalSpec spec, float frontStretchMetres)
        {
            var lane = BuildPitLane(spec, frontStretchMetres);
            if (lane.Count < 2) return 0f;
            return lane[0].length + lane[1].length * 0.9f;
        }

        // Length of the front stretch a solve produced — the pit lane is sized against it.
        public static float FrontStretchLength(OvalSpec spec)
        {
            float total = 0f;
            var segments = Build(spec);
            for (int i = 0; i < segments.Count; i++)
            {
                string label = segments[i].label ?? "";
                if (label.StartsWith("Front Stretch") || label == "Tri-Oval") total += segments[i].length;
            }
            return total;
        }

        // Index of the last segment making up the front stretch (0 for a plain straight, 2 for a tri-oval).
        public static int FrontStretchLastIndex(IList<OvalSegment> segments)
        {
            int last = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                string label = segments[i].label ?? "";
                if (label.StartsWith("Front Stretch") || label == "Tri-Oval") last = i;
                else if (i > 0) break;
            }
            return last;
        }

        // ------------------------------------------------------------ pieces

        // The front stretch. Without a dog-leg it's simply a straight.
        //
        // With one it's the tri-oval: two legs angled away from the back stretch, joined at a bend that
        // points at the grandstand. Authored as taper-out, leg, double-back bend, leg, taper-in, so the
        // heading finishes exactly where it started while the tarmac covers more ground than the distance
        // it spans — which is precisely what makes a real front stretch longer than the back.
        static void AddFrontStretch(List<OvalSegment> segments, OvalSpec spec, float frontLength,
                                    float outer, float sign)
        {
            float kink = Mathf.Abs(spec.frontKinkDegrees);
            if (kink < 0.5f)
            {
                segments.Add(Straight("Front Stretch", frontLength, spec, 0f));
                return;
            }

            float taper = frontLength * 0.08f;
            float leg = Mathf.Max(10f, (frontLength - taper * 3f) * 0.5f);

            segments.Add(Turn("Front Stretch (in)", taper, -kink * sign, spec, outer * 0.5f, sign, shallow: true));
            segments.Add(Straight("Front Stretch A", leg, spec, 0f));
            segments.Add(Turn("Tri-Oval", taper, 2f * kink * sign, spec, outer * 0.8f, sign, shallow: true));
            segments.Add(Straight("Front Stretch B", leg, spec, 0f));
            segments.Add(Turn("Front Stretch (out)", taper, -kink * sign, spec, outer * 0.5f, sign, shallow: true));
        }

        // A corner with the classic oval line: enter wide, apex tight, exit wide, with the outermost AI
        // lines pinned near the edges so the field can run two and three abreast.
        static OvalSegment Turn(string label, float length, float angle, OvalSpec spec,
                                float outer, float sign, bool shallow = false)
        {
            float lead = Mathf.Clamp(length * 0.35f, 15f, 120f);
            float edge = Mathf.Max(0.5f, spec.roadWidth * 0.5f - spec.lineMargin);
            float apexOffset = -outer * 0.65f * sign;   // inside of the corner
            float wideOffset = outer * 0.8f * sign;     // out by the wall

            return new OvalSegment
            {
                label = label,
                isTurn = true,
                length = length,
                angle = angle,
                banking = shallow ? spec.straightBanking : spec.turnBanking,
                leadIn = lead,
                leadOut = lead,
                maxSpeedMph = shallow ? spec.topSpeedMph : CornerSpeedMph(length, angle, spec.turnBanking),
                line = new LineOffsets
                {
                    idealEntry = wideOffset,
                    idealApex = apexOffset,
                    idealExit = wideOffset * 0.75f,
                    leftEntry = -edge, leftApex = -edge, leftExit = -edge,
                    rightEntry = edge, rightApex = edge, rightExit = edge,
                },
            };
        }

        static OvalSegment Straight(string label, float length, OvalSpec spec, float apexOffset)
        {
            float edge = Mathf.Max(0.5f, spec.roadWidth * 0.5f - spec.lineMargin);
            return new OvalSegment
            {
                label = label,
                isTurn = false,
                length = length,
                angle = 0f,
                banking = spec.straightBanking,
                maxSpeedMph = spec.topSpeedMph,
                line = new LineOffsets
                {
                    idealApex = apexOffset,
                    leftApex = -edge,
                    rightApex = edge,
                },
            };
        }

        // Rough corner speed from the geometry: v = sqrt(g * r * (grip + tan(bank))). Only a hint for the
        // AI's braking lookahead and the marker boards, but it stops a banked 2.5-miler and a flat bullring
        // claiming the same corner speed.
        public static int CornerSpeedMph(float arcLength, float angleDeg, float bankingDeg)
        {
            float angleRad = Mathf.Abs(angleDeg) * Mathf.Deg2Rad;
            if (angleRad < 1e-4f) return 200;
            float radius = arcLength / angleRad;
            const float grip = 1.05f;                    // slick tyre on abrasive asphalt
            float bank = Mathf.Tan(Mathf.Clamp(bankingDeg, 0f, 40f) * Mathf.Deg2Rad);
            float vMs = Mathf.Sqrt(9.81f * radius * (grip + bank));
            return Mathf.Clamp(Mathf.RoundToInt(vMs * 2.237f), 30, 230);
        }

        // ------------------------------------------------------------ checks

        // Walk the solved segments and report the lap: right length, heading closed, ends where it started.
        public static OvalCheck Validate(IList<OvalSegment> segments, bool hasPitLane = false)
        {
            var check = new OvalCheck { hasPitLane = hasPitLane };
            if (segments == null || segments.Count == 0) return check;

            Vector2 pos = Vector2.zero;
            float heading = 0f;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                check.lapMetres += seg.length;
                check.totalTurnDegrees += seg.angle;
                Advance(seg, ref pos, ref heading);
            }

            check.lapMiles = check.lapMetres / MetresPerMile;
            check.closureErrorMetres = pos.magnitude;   // started at the origin
            return check;
        }

        // Same integration TrackInfoV2 does when it samples its authored spline, so a check here means the
        // same thing the built mesh will show.
        static void Advance(OvalSegment seg, ref Vector2 pos, ref float headingDeg)
        {
            if (!seg.isTurn || Mathf.Approximately(seg.angle, 0f))
            {
                float rad = headingDeg * Mathf.Deg2Rad;
                pos += new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * seg.length;
                return;
            }

            float angleRad = seg.angle * Mathf.Deg2Rad;
            float radius = seg.length / Mathf.Abs(angleRad);
            Vector2 forward = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
            Vector2 toCentre = seg.angle >= 0f ? new Vector2(-forward.y, forward.x) : new Vector2(forward.y, -forward.x);
            Vector2 centre = pos + toCentre * radius;
            Vector2 radial = pos - centre;
            float startAngle = Mathf.Atan2(radial.y, radial.x);
            float endAngle = startAngle + angleRad;
            pos = centre + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius;
            headingDeg += seg.angle;
        }
    }
}
