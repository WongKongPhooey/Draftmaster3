using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // Which of the three stock-car championships visit a venue. A bitmask because most tracks host two or
    // three of them across the same three days, which is the whole premise of the race weekend.
    [System.Flags]
    public enum SeriesVisits
    {
        None = 0,
        Trucks = 1 << 0,
        National = 1 << 1,   // the second-tier stock-car championship
        Cup = 1 << 2,
        All = Trucks | National | Cup,
    }

    // How confident the numbers below are. Lap length, width and banking are published figures for every
    // paved oval; what a generator CANNOT know is the exact corner-by-corner shape, so that is called out
    // separately rather than silently implied.
    public enum DimensionConfidence
    {
        Measured = 0,     // taken off satellite imagery for this project (Watkins Glen)
        Published = 1,    // the venue's own published spec - length, width, banking
        Estimated = 2,    // no published width; inferred from era, type and comparable venues
    }

    // Real-world dimensions of every venue on the Cup / National / Truck calendars.
    //
    // WHY THIS EXISTS: the layout generator used to take its road width from the track TYPE - every
    // superspeedway 18 m, every short track 13 m. That is wrong in a way you can feel from the driver's
    // seat: Michigan is 73 feet wide and Dover is 40, and they are both "speedways". Bristol and
    // Martinsville are both half-mile bullrings and are not the same width either. So width is a
    // per-track number now, published where a published number exists.
    //
    // WHAT IS EXACT: lap length, racing-surface width, turn banking and straight banking are the venues'
    // own published figures, converted from feet. Those are the numbers a driver feels.
    //
    // WHAT IS NOT: the corner-by-corner SHAPE. corners / turnShareOfLap / frontKinkDeg are shaping hints
    // for OvalGeometry, which solves equal-radius corners - real ovals have unequal radii and progressive
    // banking. A generated layout drives the right distance at the right speed in the right width of road;
    // it is not a survey. Tighten one the way WatkinsGlen was done: measure it on satellite imagery and
    // author the segments by hand.
    //
    // Widths are authored in FEET, because that is how American ovals publish them, and converted once.
    public struct TrackDimensionRow
    {
        public string id;
        public string displayName;
        public string country;
        public TrackKind kind;

        public float lapMiles;
        public float widthMetres;          // racing surface, wall to wall (or edge line to edge line)
        public float pitWidthMetres;       // 0 = derive from the road width

        public float turnBankingDeg;
        public float straightBankingDeg;

        public int corners;                // 4 = conventional oval, 2 = paperclip, 3 = Pocono's triangle
        public float turnShareOfLap;       // fraction of the lap spent turning
        public float frontKinkDeg;         // tri-oval / quad-oval dog-leg on the front stretch; 0 = straight
        public int cornerCount;            // road courses: numbered corners on the circuit map

        public int pitSpeedLimitMph;
        public int cupLaps;                // scheduled distance for the top series
        public SeriesVisits series;
        public DimensionConfidence confidence;
        public string note;

        public float LapMetres => lapMiles * 1609.344f;
    }

    public static class TrackDimensions
    {
        public const float MetresPerFoot = 0.3048f;
        public static float Feet(float ft) => ft * MetresPerFoot;

        static Dictionary<string, TrackDimensionRow> _byId;

        public static IReadOnlyList<TrackDimensionRow> All => Rows;
        public static bool Has(string id) => !string.IsNullOrEmpty(id) && Index.ContainsKey(id);

        public static bool TryGet(string id, out TrackDimensionRow row)
        {
            row = default;
            return !string.IsNullOrEmpty(id) && Index.TryGetValue(id, out row);
        }

        static Dictionary<string, TrackDimensionRow> Index
        {
            get
            {
                if (_byId != null) return _byId;
                _byId = new Dictionary<string, TrackDimensionRow>(Rows.Count);
                for (int i = 0; i < Rows.Count; i++) _byId[Rows[i].id] = Rows[i];
                return _byId;
            }
        }

        // Every venue a given championship (or set of them) visits.
        public static IEnumerable<TrackDimensionRow> Visited(SeriesVisits series)
        {
            for (int i = 0; i < Rows.Count; i++)
                if ((Rows[i].series & series) != 0) yield return Rows[i];
        }

        public static bool IsRoadCourse(string id) =>
            TryGet(id, out var row) && row.kind == TrackKind.RoadCourse;

        // ------------------------------------------------------------------ constructors

        static TrackDimensionRow Oval(
            string id, string display, TrackKind kind, float miles, float widthFeet,
            float turnBank, float straightBank, int corners, float turnShare, float kink,
            int pitMph, int cupLaps, SeriesVisits series,
            DimensionConfidence confidence = DimensionConfidence.Published, string note = null,
            float pitWidthFeet = 0f, string country = "USA")
        {
            return new TrackDimensionRow
            {
                id = id, displayName = display, country = country, kind = kind,
                lapMiles = miles,
                widthMetres = Feet(widthFeet),
                pitWidthMetres = pitWidthFeet > 0f ? Feet(pitWidthFeet) : 0f,
                turnBankingDeg = turnBank, straightBankingDeg = straightBank,
                corners = corners, turnShareOfLap = turnShare, frontKinkDeg = kink,
                pitSpeedLimitMph = pitMph, cupLaps = cupLaps, series = series,
                confidence = confidence, note = note,
            };
        }

        static TrackDimensionRow Road(
            string id, string display, float miles, float widthMetres, int cornerCount,
            int pitMph, int cupLaps, SeriesVisits series,
            DimensionConfidence confidence, string note, string country = "USA")
        {
            return new TrackDimensionRow
            {
                id = id, displayName = display, country = country, kind = TrackKind.RoadCourse,
                lapMiles = miles,
                widthMetres = widthMetres,
                turnBankingDeg = 0f, straightBankingDeg = 0f,
                corners = 0, turnShareOfLap = 0.4f, frontKinkDeg = 0f,
                cornerCount = cornerCount,
                pitSpeedLimitMph = pitMph, cupLaps = cupLaps, series = series,
                confidence = confidence, note = note,
            };
        }

        // ------------------------------------------------------------------ the table

        public static readonly List<TrackDimensionRow> Rows = new List<TrackDimensionRow>
        {
            // ---------------------------------------------------------- superspeedways
            // Daytona and Talladega are the two the draft model was built for. Talladega is both longer
            // and eight feet wider, which is why its packs are bigger - that difference reaches the mesh now.
            Oval("Daytona", "Daytona International Speedway", TrackKind.Superspeedway,
                 2.5f, 40f, 31f, 3f, 4, 0.47f, 6f, 55, 200, SeriesVisits.All,
                 note: "Tri-oval banked 18 deg, back stretch 3 deg. 40 ft wide throughout."),

            Oval("Talladega", "Talladega Superspeedway", TrackKind.Superspeedway,
                 2.66f, 48f, 33f, 2f, 4, 0.5f, 5f, 55, 188, SeriesVisits.All,
                 note: "Widest oval on the calendars at 48 ft - the reason three-wide is the default here."),

            Oval("Indianapolis", "Indianapolis Motor Speedway", TrackKind.Superspeedway,
                 2.5f, 50f, 9.2f, 0f, 4, 0.28f, 0f, 55, 160, SeriesVisits.Cup | SeriesVisits.National,
                 note: "Four distinct corners of 9 deg 12 min, no banking on the straights. 50 ft on the "
                     + "straights widening to 60 ft through the turns. Flat and narrow for its length.",
                 pitWidthFeet: 40f),

            // ---------------------------------------------------------- intermediates
            Oval("Charlotte", "Charlotte Motor Speedway", TrackKind.Speedway,
                 1.5f, 60f, 24f, 5f, 4, 0.42f, 5f, 45, 400, SeriesVisits.All,
                 note: "Quad-oval: the front stretch bows toward the grandstand rather than running straight."),

            Oval("Atlanta", "Atlanta Motor Speedway", TrackKind.Speedway,
                 1.54f, 40f, 28f, 5f, 4, 0.45f, 5f, 45, 260, SeriesVisits.All,
                 note: "Reconfigured in 2022 to 28 deg and narrowed to 40 ft, which is what turned it into "
                     + "a pack-racing track despite the 1.54-mile length."),

            Oval("LasVegas", "Las Vegas Motor Speedway", TrackKind.Speedway,
                 1.5f, 60f, 20f, 9f, 4, 0.42f, 4f, 45, 267, SeriesVisits.All,
                 note: "Progressive 20 deg turns, 9 deg front stretch, 3 deg back stretch."),

            Oval("Kansas", "Kansas Speedway", TrackKind.Speedway,
                 1.5f, 55f, 18f, 10.4f, 4, 0.42f, 4f, 45, 267, SeriesVisits.All,
                 note: "Variable banking of 17-20 deg through the corners; 10.4 deg front stretch."),

            Oval("Miami", "Homestead-Miami Speedway", TrackKind.Speedway,
                 1.5f, 55f, 19f, 4f, 4, 0.42f, 3f, 45, 267, SeriesVisits.All,
                 note: "Progressive 18-20 deg. The wide corner exit is why the top groove works here."),

            Oval("FortWorth", "Texas Motor Speedway", TrackKind.Speedway,
                 1.5f, 60f, 20f, 5f, 4, 0.42f, 5f, 45, 334, SeriesVisits.All,
                 note: "Turns 1-2 and 3-4 both 20 deg since the 2017 repave; 5 deg on both straights."),

            Oval("Michigan", "Michigan International Speedway", TrackKind.Speedway,
                 2.0f, 73f, 18f, 12f, 4, 0.38f, 4f, 45, 200, SeriesVisits.Cup | SeriesVisits.Trucks,
                 note: "73 ft - the widest racing surface on any of the three calendars."),

            Oval("Darlington", "Darlington Raceway", TrackKind.Speedway,
                 1.366f, 55f, 25f, 3f, 4, 0.45f, 0f, 45, 367, SeriesVisits.All,
                 note: "Egg-shaped: turns 1-2 are 25 deg and tighter than the 23 deg turns 3-4. The "
                     + "generator lays down equal corners, so this is a prime candidate for hand-shaping."),

            Oval("Dover", "Dover Motor Speedway", TrackKind.Speedway,
                 1.0f, 40f, 24f, 9f, 4, 0.5f, 0f, 45, 400, SeriesVisits.All,
                 note: "Concrete. 24 deg of banking in only 40 ft of width - the Monster Mile is steep and "
                     + "narrow, not wide and fast."),

            Oval("Nashville", "Nashville Superspeedway", TrackKind.Speedway,
                 1.33f, 50f, 14f, 9f, 4, 0.42f, 0f, 45, 300, SeriesVisits.All,
                 note: "Concrete, 14 deg turns, 9 deg on the front stretch."),

            Oval("Gateway", "World Wide Technology Raceway", TrackKind.Speedway,
                 1.25f, 50f, 11f, 4f, 4, 0.4f, 0f, 45, 240, SeriesVisits.All,
                 note: "Egg-shaped 1.25-miler: turns 1-2 are 11 deg, turns 3-4 only 9 deg."),

            Oval("LongPond", "Pocono Raceway", TrackKind.Speedway,
                 2.5f, 50f, 10f, 0f, 3, 0.3f, 0f, 45, 160, SeriesVisits.Cup | SeriesVisits.Trucks,
                 note: "The triangle: turn 1 is 14 deg, the Tunnel Turn 8 deg, turn 3 only 6 deg. The "
                     + "three-corner shape is generated; the unequal banking between them is not."),

            // ---------------------------------------------------------- short tracks
            Oval("Bristol", "Bristol Motor Speedway", TrackKind.ShortTrack,
                 0.533f, 40f, 28f, 8f, 2, 0.55f, 0f, 35, 500, SeriesVisits.All,
                 note: "Concrete bullring with variable 26-30 deg banking. 40 ft wide with that much "
                     + "banking is exactly why it races the way it does."),

            Oval("Martinsville", "Martinsville Speedway", TrackKind.ShortTrack,
                 0.526f, 40f, 12f, 0f, 2, 0.42f, 0f, 35, 500, SeriesVisits.All,
                 note: "The paperclip: two 800 ft straights, flat, with 12 deg on the ends. Brakes, not banking."),

            Oval("Richmond", "Richmond Raceway", TrackKind.ShortTrack,
                 0.75f, 60f, 14f, 8f, 4, 0.45f, 0f, 35, 400, SeriesVisits.All,
                 note: "60 ft of width on a three-quarter mile - the widest short track on the calendars, "
                     + "and the reason it races like a small intermediate."),

            Oval("Phoenix", "Phoenix Raceway", TrackKind.ShortTrack,
                 1.022f, 52f, 11f, 3f, 4, 0.36f, 9f, 35, 312, SeriesVisits.All,
                 note: "Turns 1-2 are 10-11 deg, turns 3-4 only 8-9. The back-straight dog-leg is authored "
                     + "here as the front kink, which is what makes the lap asymmetric."),

            Oval("NewHampshire", "New Hampshire Motor Speedway", TrackKind.ShortTrack,
                 1.058f, 55f, 7f, 2f, 4, 0.44f, 0f, 35, 301, SeriesVisits.All,
                 note: "Flat one-mile oval with 2-7 deg of variable banking."),

            Oval("Iowa", "Iowa Speedway", TrackKind.ShortTrack,
                 0.875f, 60f, 14f, 10f, 4, 0.45f, 0f, 35, 350, SeriesVisits.All,
                 note: "Variable 12-14 deg with a 10 deg front stretch. Wide for its length."),

            Oval("NorthWilkesboro", "North Wilkesboro Speedway", TrackKind.ShortTrack,
                 0.625f, 45f, 14f, 3f, 2, 0.45f, 0f, 35, 400, SeriesVisits.All,
                 note: "The famous feature is the grade - the front stretch runs uphill and the back "
                     + "downhill. The spline is flat; elevation is not modelled anywhere in this project."),

            Oval("Rockingham", "Rockingham Speedway", TrackKind.ShortTrack,
                 0.94f, 55f, 23f, 8f, 4, 0.46f, 0f, 40, 300, SeriesVisits.National | SeriesVisits.Trucks,
                 note: "The Rock: turns 1-2 are 22 deg, turns 3-4 25 deg. Abrasive - hardest on tyres anywhere."),

            Oval("IRP", "Lucas Oil Indianapolis Raceway Park", TrackKind.ShortTrack,
                 0.686f, 55f, 12f, 0f, 4, 0.44f, 0f, 35, 200, SeriesVisits.Trucks,
                 note: "Nearly flat .686-mile oval; the Truck series' traditional Indiana night race."),

            Oval("Milwaukee", "Milwaukee Mile", TrackKind.ShortTrack,
                 1.015f, 50f, 9.25f, 2.5f, 4, 0.4f, 0f, 35, 250, SeriesVisits.Trucks,
                 note: "Oldest operating racetrack in the world, and almost completely flat."),

            Oval("BowmanGray", "Bowman Gray Stadium", TrackKind.ShortTrack,
                 0.25f, 40f, 2f, 0f, 2, 0.5f, 0f, 30, 200, SeriesVisits.Cup,
                 confidence: DimensionConfidence.Estimated,
                 note: "Flat quarter-mile inside a football stadium. The width is estimated - the venue "
                     + "does not publish one. Hosts the preseason exhibition."),

            // ---------------------------------------------------------- road and street
            // Road courses are NOT solved by OvalGeometry - see RoadCourseLayouts for the authored corner
            // sequences. What lives here is the lap length, the surface width and the corner count.
            Road("WatkinsGlen", "Watkins Glen International", 2.45f, 12.2f, 11, 45, 90,
                 SeriesVisits.Cup | SeriesVisits.National, DimensionConfidence.Measured,
                 "Measured off satellite imagery for this project - the reference road course. Its asset "
                 + "is hand-built and is never regenerated."),

            Road("Sonoma", "Sonoma Raceway", 2.52f, 12f, 12, 45, 110,
                 SeriesVisits.Cup | SeriesVisits.Trucks, DimensionConfidence.Published,
                 "The NASCAR configuration, running the Carousel."),

            Road("COTA", "Circuit of the Americas", 3.41f, 15f, 20, 45, 68,
                 SeriesVisits.All, DimensionConfidence.Published,
                 "Grade 1 circuit built to a 15 m minimum width; 133 ft of climb into turn 1, which the "
                 + "flat spline does not model."),

            Road("CharlotteRoval", "Charlotte Motor Speedway Roval", 2.32f, 12.5f, 17, 45, 109,
                 SeriesVisits.Cup | SeriesVisits.National, DimensionConfidence.Published,
                 "Infield road course joined to the oval's front and back stretches. The oval sections "
                 + "carry the oval's 24 deg banking; the infield is flat."),

            Road("MidOhio", "Mid-Ohio Sports Car Course", 2.258f, 12f, 13, 45, 75,
                 SeriesVisits.National, DimensionConfidence.Published,
                 "Run with the chicane, as the stock cars do."),

            Road("Portland", "Portland International Raceway", 1.967f, 12f, 12, 45, 75,
                 SeriesVisits.National, DimensionConfidence.Published,
                 "Flat former airfield perimeter; the Festival Curves chicane opens the lap."),

            Road("RoadAmerica", "Road America", 4.048f, 15f, 14, 45, 62,
                 SeriesVisits.National, DimensionConfidence.Published,
                 "Longest circuit on any of the three calendars at just over four miles."),

            Road("LimeRock", "Lime Rock Park", 1.5f, 11f, 7, 40, 100,
                 SeriesVisits.Trucks, DimensionConfidence.Published,
                 "Short, fast and narrow - seven corners in a mile and a half."),

            Road("IndyRoad", "Indianapolis Motor Speedway Road Course", 2.439f, 15f, 14, 45, 82,
                 SeriesVisits.National, DimensionConfidence.Published,
                 "The infield road course run clockwise, using part of the oval's front stretch."),

            Road("Chicago", "Chicago Street Course", 2.2f, 11f, 12, 45, 75,
                 SeriesVisits.Cup | SeriesVisits.National, DimensionConfidence.Published,
                 "Grant Park street circuit. Narrow, walled, no run-off."),

            Road("SanDiego", "Naval Base Coronado Street Circuit", 3.0f, 12f, 14, 45, 100,
                 SeriesVisits.Cup, DimensionConfidence.Estimated,
                 "Street circuit on a naval air station. Lap length and corner count are approximate - "
                 + "this one wants measuring once a definitive map exists."),

            Road("MexicoCity", "Autodromo Hermanos Rodriguez", 2.674f, 15f, 15, 45, 100,
                 SeriesVisits.Cup | SeriesVisits.National, DimensionConfidence.Published,
                 "Run at 7,300 ft, which the sim does not model.", "Mexico"),
        };
    }
}
