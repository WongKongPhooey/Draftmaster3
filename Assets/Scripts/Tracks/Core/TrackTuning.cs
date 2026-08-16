namespace Draftmaster.Tracks
{
    // How a track TYPE should feel, in one table.
    //
    // A 2.5-mile superspeedway and a half-mile bullring share every line of code in this project — the same
    // spline driver, the same draft model, the same tyre model — and are completely different races. What
    // separates them is a handful of numbers: what the draft is worth, how fast tyres go off, how wide the
    // AI will run, how tight the camera sits. Rather than let those get hard-coded per scene (which does not
    // survive 35 rounds), they live here.
    //
    // Consumers pull from this; it never reaches into them. Intended pickup points:
    //   DraftAero            — draftScale
    //   tyre / fuel models   — tyreWearScale, fuelBurnScale
    //   AIRacingBehaviour    — lineSpread, cautionProneness
    //   GridSpawner          — gridColumns
    //   camera / PitLaneStart— racingZoom
    //   OvalGeometry         — roadWidth, pitSpeedLimitMph, turnShareOfLap when generating a layout
    public struct TrackTuningData
    {
        public TrackKind kind;

        public float draftScale;         // multiplier on the tow from the car ahead
        public float tyreWearScale;      // multiplier on wear rate
        public float fuelBurnScale;      // multiplier on burn rate

        public float lineSpread;         // 0-1: how far off the ideal line the AI will race
        public float cautionProneness;   // 0-1: rough likelihood of contact-driven cautions

        public float roadWidth;          // m, used when generating a layout
        public int pitSpeedLimitMph;
        public float turnShareOfLap;     // fraction of the lap spent cornering

        public int gridColumns;
        public float racingZoom;         // orthographic size while racing
    }

    public static class TrackTuning
    {
        public static TrackTuningData For(TrackKind kind)
        {
            switch (kind)
            {
                case TrackKind.Superspeedway:
                    return new TrackTuningData
                    {
                        kind = kind,
                        draftScale = 1.65f,        // the whole race is the draft
                        tyreWearScale = 0.7f,
                        fuelBurnScale = 1.15f,
                        lineSpread = 1f,           // three wide as standard
                        cautionProneness = 0.8f,   // the big one
                        roadWidth = 18f,
                        pitSpeedLimitMph = 55,
                        turnShareOfLap = 0.46f,
                        gridColumns = 2,
                        racingZoom = 26f,
                    };

                case TrackKind.Speedway:
                    return new TrackTuningData
                    {
                        kind = kind,
                        draftScale = 1.15f,
                        tyreWearScale = 1f,
                        fuelBurnScale = 1f,
                        lineSpread = 0.75f,
                        cautionProneness = 0.45f,
                        roadWidth = 16f,
                        pitSpeedLimitMph = 45,
                        turnShareOfLap = 0.42f,
                        gridColumns = 2,
                        racingZoom = 22f,
                    };

                case TrackKind.ShortTrack:
                    return new TrackTuningData
                    {
                        kind = kind,
                        draftScale = 0.7f,
                        tyreWearScale = 1.5f,      // brake, turn, throttle, repeat
                        fuelBurnScale = 0.85f,
                        lineSpread = 0.5f,         // barely room for two
                        cautionProneness = 0.9f,   // beating and banging
                        roadWidth = 13f,
                        pitSpeedLimitMph = 35,
                        turnShareOfLap = 0.5f,     // a bullring is nearly all corner
                        gridColumns = 2,
                        racingZoom = 16f,
                    };

                case TrackKind.RoadCourse:
                    return new TrackTuningData
                    {
                        kind = kind,
                        draftScale = 0.5f,
                        tyreWearScale = 1.25f,
                        fuelBurnScale = 1f,
                        lineSpread = 0.55f,
                        cautionProneness = 0.35f,
                        roadWidth = 12f,
                        pitSpeedLimitMph = 45,
                        turnShareOfLap = 0.4f,
                        gridColumns = 2,
                        racingZoom = 20f,
                    };

                case TrackKind.DirtCourse:
                    return new TrackTuningData
                    {
                        kind = kind,
                        draftScale = 0.6f,
                        tyreWearScale = 1.35f,
                        fuelBurnScale = 0.85f,
                        lineSpread = 0.85f,        // everyone runs their own line in the slop
                        cautionProneness = 0.85f,
                        roadWidth = 15f,
                        pitSpeedLimitMph = 35,
                        turnShareOfLap = 0.5f,
                        gridColumns = 2,
                        racingZoom = 17f,
                    };

                default:
                    goto case TrackKind.Speedway;
            }
        }

        // A specific track: its type's defaults, then any hand-tuned exception. Keep the exception list
        // short and reasoned — anything true of a whole class of track belongs in the defaults above.
        public static TrackTuningData ForTrack(string trackId, TrackKind kind)
        {
            var t = For(kind);
            if (string.IsNullOrEmpty(trackId)) return t;

            switch (trackId)
            {
                case "Talladega":       // wider and faster than Daytona: bigger pack, bigger tow
                    t.draftScale = 1.8f;
                    t.cautionProneness = 0.85f;
                    break;

                case "Bristol":         // concrete bullring, 28 degrees of banking — hardest on tyres anywhere
                    t.tyreWearScale = 1.8f;
                    t.racingZoom = 14f;
                    break;

                case "Martinsville":    // flat paperclip: brakes, not banking. No speed in the corner at all
                    t.tyreWearScale = 1.35f;
                    t.lineSpread = 0.45f;
                    break;

                case "Indianapolis":    // 2.5 miles but flat and narrow — nothing like Daytona despite the type
                    t.draftScale = 1.15f;
                    t.lineSpread = 0.6f;
                    t.turnShareOfLap = 0.3f;
                    break;

                case "Darlington":      // egg-shaped, one groove, wall-scraping
                    t.lineSpread = 0.45f;
                    t.tyreWearScale = 1.6f;
                    break;
            }
            return t;
        }
    }
}
