using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // The authored road and street circuits, corner by corner.
    //
    // HOW TO READ A LAYOUT. Each circuit is a list in lap order:
    //
    //   C("Turn 1", 80, -95)   a named corner: 80 m of arc turning 95 degrees RIGHT (negative = right).
    //   S("Back Straight", 700) a real straight — a pit straight, a runway, an oval stretch.
    //   L(220)                  a connector, which takes gentle curvature when the lap is solved.
    //
    // Corner angles and arc lengths are authored by eye off the circuit maps, at their real severity and
    // in the real order — a hairpin is authored as a hairpin and a kink as a kink. The lengths then get
    // scaled as one so the lap comes out at its published distance, and the connectors take up the
    // slack in both heading and position (RoadCourseGeometry explains the solve).
    //
    // WHAT THIS IS AND IS NOT. The lap distance, the road width, the corner count and the sequence and
    // relative severity of the corners are right. The exact position of each apex is not surveyed — this
    // is a circuit that drives like the real one, not a scan of it. WatkinsGlen is the standard to beat
    // and is deliberately absent from this file: it was measured off satellite imagery by hand, and
    // BuildAll skips it so it can never be overwritten by a generated approximation.
    //
    // TO IMPROVE ONE: open the map, correct the angles and arc lengths in place, and rebuild. The solver
    // will re-close the lap for you, so a partial correction is always safe to commit.
    public static class RoadCourseLayouts
    {
        static RoadPiece C(string label, float length, float angle, float banking = 0f) =>
            RoadCourseGeometry.Corner(label, length, angle, banking);
        static RoadPiece S(string label, float length) => RoadCourseGeometry.Straight(label, length);
        static RoadPiece L(float length) => RoadCourseGeometry.Link(length);

        // Watkins Glen is hand-measured; nothing here may generate over it.
        public const string HandAuthored = "WatkinsGlen";

        public static bool Has(string trackId) =>
            !string.IsNullOrEmpty(trackId) && trackId != HandAuthored && Layouts.ContainsKey(trackId);

        public static IEnumerable<string> Ids => Layouts.Keys;

        // Build the full spec for a circuit: its authored pieces, plus the published dimensions.
        public static RoadCourseSpec Spec(string trackId)
        {
            if (!Has(trackId)) return null;
            if (!TrackDimensions.TryGet(trackId, out var dim)) return null;

            return new RoadCourseSpec
            {
                trackId = trackId,
                displayName = dim.displayName,
                lengthMiles = dim.lapMiles,
                roadWidth = dim.widthMetres,
                clockwise = Clockwise.Contains(trackId),
                pitSpeedLimitMph = dim.pitSpeedLimitMph,
                defaultLaps = dim.cupLaps,
                topSpeedMph = 180,
                pitLane = true,
                pieces = new List<RoadPiece>(Layouts[trackId]),
            };
        }

        // Which way round the lap runs. Everything on these calendars is clockwise except COTA and the
        // Roval, which inherit the oval's counter-clockwise direction.
        static readonly HashSet<string> Clockwise = new HashSet<string>
        {
            "Sonoma", "MidOhio", "Portland", "RoadAmerica", "LimeRock",
            "IndyRoad", "Chicago", "MexicoCity", "SanDiego",
        };

        static readonly Dictionary<string, RoadPiece[]> Layouts = new Dictionary<string, RoadPiece[]>
        {
            // ---------------------------------------------------------------- Pocono
            // Not a road course - an oval that no oval formula fits. Pocono is a TRIANGLE with three
            // straights of different lengths and three corners of different radius AND different banking
            // (14, 8 and 6 degrees), which is exactly the thing OvalGeometry cannot express: its solver
            // assumes two straights joined by two matched ends, and a triangle leaves it 1.5 km short of
            // closing. Authored here instead, where each corner can carry its own numbers.
            //
            // Counter-clockwise, so the angles are positive and sum to exactly 360 on their own - there
            // are no connectors here to take up a residual.
            ["LongPond"] = new[]
            {
                S("Front Stretch", 1140),
                C("Turn 1", 345, 110, 14f),
                S("Long Pond Straight", 900),
                C("Turn 2 Tunnel Turn", 482, 115, 8f),
                S("North Straight", 450),
                C("Turn 3", 707, 135, 6f),
            },

            // ---------------------------------------------------------------- Sonoma
            // Clockwise, 12 turns. The lap is defined by the Carousel — a near-180 that eats a quarter of
            // the circuit on its own — and by turns 2 and 3A, the only meaningful lefts.
            ["Sonoma"] = new[]
            {
                S("Front Straight", 330),
                C("Turn 1", 70, -90),
                L(150),
                C("Turn 2", 60, 70),
                L(90),
                C("Turn 3", 60, -80),
                C("Turn 3A", 50, 55),
                L(210),
                C("Turn 4", 70, -95),
                L(130),
                C("Turn 4A", 55, 40),
                L(120),
                C("Carousel", 240, -180),
                L(150),
                C("Turn 7", 75, -90),
                S("Back Straight", 300),
                C("Turn 8", 70, -85),
                L(140),
                C("Turn 9", 60, 55),
                L(260),
                C("Turn 10", 80, -105),
                L(120),
                C("Turn 11", 65, -85),
                L(330),
            },

            // ---------------------------------------------------------------- Circuit of the Americas
            // Counter-clockwise, 20 turns. Three set pieces: the uphill turn 1 hairpin, the turn 3-6 esses,
            // and the 1.1 km back straight into the turn 12 heavy braking zone.
            ["COTA"] = new[]
            {
                S("Front Straight", 420),
                C("Turn 1", 80, 120),
                L(130),
                C("Turn 2", 60, 40),
                C("Turn 3", 65, -60),
                C("Turn 4", 65, 60),
                C("Turn 5", 60, -60),
                C("Turn 6", 60, 60),
                C("Turn 7", 60, -70),
                L(70),
                C("Turn 8", 70, 85),
                C("Turn 9", 55, -60),
                L(90),
                C("Turn 10", 60, 45),
                C("Turn 11", 90, 150),
                S("Back Straight", 1150),
                C("Turn 12", 85, 95),
                L(140),
                C("Turn 13", 70, -80),
                L(200),
                C("Turn 14", 65, 60),
                C("Turn 15", 70, -95),
                L(60),
                C("Turn 16", 90, 85),
                C("Turn 17", 90, 85),
                C("Turn 18", 85, 80),
                L(110),
                C("Turn 19", 70, -85),
                C("Turn 20", 75, 95),
                L(260),
            },

            // ---------------------------------------------------------------- Charlotte Roval
            // Counter-clockwise. The four oval corners already supply a full revolution between them, so
            // the infield section has to net out to nothing: it loops away from the oval and comes back.
            // Authored that way deliberately — it is the constraint that makes a roval a roval.
            ["CharlotteRoval"] = new[]
            {
                S("Front Stretch", 300),
                C("Oval Turn 1", 190, 90, 24f),
                C("Oval Turn 2", 190, 90, 24f),
                S("Back Stretch", 330),
                C("Turn 3", 70, -80),
                L(90),
                C("Turn 4", 60, 85),
                L(120),
                C("Turn 5", 55, -75),
                L(80),
                C("Turn 6", 60, 90),
                L(150),
                C("Turn 7", 65, -85),
                C("Turn 8", 60, 80),
                L(110),
                C("Chicane In", 45, 85),
                C("Chicane Out", 45, -85),
                L(140),
                C("Turn 11", 70, 90),
                L(100),
                C("Turn 12", 60, -80),
                L(130),
                C("Turn 13", 65, -50),
                C("Turn 14", 60, 25),
                C("Oval Turn 3", 185, 90, 24f),
                C("Oval Turn 4", 185, 90, 24f),
                S("Front Stretch (in)", 280),
            },

            // ---------------------------------------------------------------- Mid-Ohio
            // Clockwise, 13 corners. The Keyhole is the signature: a 170-degree right onto the back
            // straight. The chicane before the final turn is the one the stock cars use.
            ["MidOhio"] = new[]
            {
                S("Front Straight", 430),
                C("Turn 1", 75, -85),
                L(160),
                C("Turn 2 Keyhole", 110, -170),
                S("Back Straight", 340),
                C("Turn 4", 60, 50),
                L(200),
                C("Turn 5", 70, -80),
                L(120),
                C("Turn 6", 60, 45),
                C("Turn 7", 60, -70),
                L(150),
                C("Turn 8 Carousel", 130, -120),
                L(180),
                C("Turn 9", 55, 45),
                C("Turn 10", 60, -65),
                L(140),
                C("Turn 11 Chicane In", 40, 60),
                C("Turn 12 Chicane Out", 40, -60),
                L(190),
                C("Turn 13", 70, -85),
                L(300),
            },

            // ---------------------------------------------------------------- Portland
            // Clockwise, 12 turns, dead flat — a former airfield perimeter. The Festival Curves chicane
            // opens the lap straight off the long front straight, which is where the first-lap carnage is.
            ["Portland"] = new[]
            {
                S("Front Straight", 600),
                C("Festival Curves In", 45, -55),
                C("Festival Curves Out", 45, 55),
                C("Turn 3", 60, -80),
                L(280),
                C("Turn 4", 70, -95),
                L(200),
                C("Turn 5", 60, -70),
                L(150),
                C("Turn 6", 55, 45),
                S("Back Straight", 220),
                C("Turn 7", 75, -100),
                L(180),
                C("Turn 8", 60, -60),
                L(140),
                C("Turn 9", 55, 40),
                L(160),
                C("Turn 10", 70, -90),
                C("Turn 11", 60, -70),
                L(120),
                C("Turn 12", 65, -80),
                L(240),
            },

            // ---------------------------------------------------------------- Road America
            // Clockwise, 14 turns, four miles. Three long straights, the Carousel, and the flat-out Kink —
            // and the reason its connectors carry so much sweep is that its "straights" genuinely bend.
            ["RoadAmerica"] = new[]
            {
                S("Front Straight", 700),
                C("Turn 1", 80, -90),
                L(180),
                C("Turn 2", 70, -45),
                S("Moraine Sweep", 560),
                C("Turn 3", 90, -100),
                S("Long Straight", 700),
                C("Turn 4", 60, -35),
                L(160),
                C("Turn 5", 85, -95),
                L(280),
                C("Turn 6 Hurry Downs", 70, 60),
                L(200),
                C("Turn 7 Hurry Downs", 65, -70),
                L(320),
                C("Turn 8 Carousel", 200, -150),
                L(420),
                C("Turn 9", 60, 30),
                L(200),
                C("Turn 10", 70, -55),
                L(180),
                C("Turn 11 Kink", 90, -35),
                S("Kettle Bottom", 430),
                C("Turn 12 Canada Corner", 95, -105),
                L(360),
                C("Turn 13", 70, 55),
                L(200),
                C("Turn 14", 80, -90),
                L(500),
            },

            // ---------------------------------------------------------------- Lime Rock Park
            // Clockwise, 7 turns in a mile and a half — the shortest road course any of the three
            // championships visit, and the narrowest at 11 m.
            ["LimeRock"] = new[]
            {
                S("Front Straight", 430),
                C("Big Bend", 130, -140),
                L(180),
                C("Left Hander", 70, 60),
                L(150),
                C("No Name Straight Bend", 80, -80),
                L(200),
                C("Uphill", 90, -95),
                L(160),
                C("West Bend", 85, -90),
                L(140),
                C("Diving Turn", 90, -95),
                L(210),
                C("Onramp", 70, -50),
                L(240),
            },

            // ---------------------------------------------------------------- Indianapolis road course
            // Clockwise, 14 turns, using part of the oval's front stretch and the Hulman straight.
            ["IndyRoad"] = new[]
            {
                S("Front Stretch", 560),
                C("Turn 1", 80, -95),
                L(200),
                C("Turn 2", 65, -75),
                C("Turn 3", 60, 60),
                L(150),
                C("Turn 4", 70, -85),
                L(120),
                C("Turn 5", 60, 50),
                L(180),
                C("Turn 6", 75, -90),
                S("Hulman Straight", 420),
                C("Turn 7", 85, -100),
                L(240),
                C("Turn 8", 60, 45),
                C("Turn 9", 60, -60),
                L(160),
                C("Turn 10", 70, -80),
                L(300),
                C("Turn 11", 65, -70),
                L(140),
                C("Turn 12", 70, -85),
                L(180),
                C("Turn 13", 90, 60),
                C("Turn 14", 150, -90),
                L(340),
            },

            // ---------------------------------------------------------------- Chicago street course
            // Clockwise, 12 turns on Grant Park's closed public roads. Square, walled, no run-off — the
            // corners are literal street junctions, which is why they are all near ninety degrees.
            ["Chicago"] = new[]
            {
                S("Columbus Drive", 520),
                C("Turn 1", 45, -90),
                S("Balbo Drive", 300),
                C("Turn 2", 45, -90),
                L(220),
                C("Turn 3", 50, -80),
                L(180),
                C("Turn 4", 45, 60),
                L(240),
                C("Turn 5", 45, -90),
                S("DuSable Lake Shore", 420),
                C("Turn 6", 50, -85),
                L(160),
                C("Turn 7", 45, 55),
                L(200),
                C("Turn 8", 45, -85),
                L(300),
                C("Turn 9", 45, -90),
                L(180),
                C("Turn 10", 45, 50),
                L(150),
                C("Turn 11", 50, -85),
                L(260),
                C("Turn 12", 45, -80),
                L(300),
            },

            // ---------------------------------------------------------------- Autodromo Hermanos Rodriguez
            // Clockwise, 15 turns. The 1.15 km main straight and the Peraltada complex through the
            // baseball stadium are the two things anyone remembers about the lap.
            ["MexicoCity"] = new[]
            {
                S("Main Straight", 1150),
                C("Turn 1", 70, -90),
                C("Turn 2", 60, 55),
                C("Turn 3", 65, -85),
                L(220),
                C("Turn 4", 75, -95),
                L(300),
                C("Turn 5", 60, -60),
                C("Turn 6", 60, 50),
                L(180),
                C("Turn 7", 70, -85),
                S("Esses Approach", 260),
                C("Turn 8", 65, -75),
                L(150),
                C("Turn 9", 60, 45),
                L(200),
                C("Turn 10", 70, -90),
                L(160),
                C("Turn 11", 60, -70),
                L(240),
                C("Turn 12", 70, -60),
                L(140),
                C("Peraltada In", 110, -95),
                C("Foro Sol", 90, 70),
                C("Peraltada Out", 130, -110),
                L(380),
            },

            // ---------------------------------------------------------------- Naval Base Coronado
            // Clockwise, 14 turns. The most speculative layout in this file — a street circuit on an
            // active naval air station whose map was not public when this was authored. Long runway
            // straights joined by square junctions is the right SHAPE for the venue, and the lap length
            // is approximate. Replace it wholesale when a real map exists.
            ["SanDiego"] = new[]
            {
                S("Runway Straight", 800),
                C("Turn 1", 55, -90),
                S("Taxiway", 420),
                C("Turn 2", 55, -85),
                L(300),
                C("Turn 3", 50, 55),
                L(360),
                C("Turn 4", 60, -95),
                L(280),
                C("Turn 5", 50, -70),
                L(240),
                C("Turn 6", 55, 50),
                L(320),
                C("Turn 7", 60, -90),
                S("Back Runway", 400),
                C("Turn 8", 55, -80),
                L(220),
                C("Turn 9", 50, 45),
                L(260),
                C("Turn 10", 55, -85),
                L(300),
                C("Turn 11", 50, -60),
                L(180),
                C("Turn 12", 55, -75),
                L(340),
                C("Turn 13", 60, -90),
                L(260),
                C("Turn 14", 55, -55),
                L(420),
            },
        };
    }
}
