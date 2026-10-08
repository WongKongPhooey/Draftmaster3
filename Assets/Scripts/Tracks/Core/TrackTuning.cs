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
    //   DraftAero / PVC      — draftScale (through TrackConditions.DraftScale)
    //   AIRacingBehaviour    — draftFollowScale (through TrackConditions.AiFollowScale)
    //   tyre / fuel models   — tyreWearScale, fuelBurnScale
    //   AIRacingBehaviour    — lineSpread, cautionProneness
    //   GridSpawner          — gridColumns
    //   camera / PitLaneStart— racingZoom
    //   OvalGeometry         — roadWidth, pitSpeedLimitMph, turnShareOfLap when generating a layout
    public struct TrackTuningData
    {
        public TrackKind kind;

        public float draftScale;         // multiplier on the tow from the car ahead
        public float draftFollowScale;   // multiplier on how close the AI follows (NR2003's ai_drafting_distance)
        public bool raceInLanes;         // AI races in lanes (AIRacingBehaviour.useLanes): ovals, where packs run side by side

        // How the racing itself works here - the physics and the AI's temperament, per kind of track.
        public float plateMph;           // > 0: restrictor plate. Solo top speed for every car; the turns are given the grip
                                         // to take it (and a full tow on top) flat out. Superspeedways only.
        public bool flatOut;             // AI holds the throttle wide open all lap (no lift for the turns) - needs a plate
        public bool packRacing;          // AI races as a pack: nose to tail in lines, pull out only with a run, followers
                                         // go with their leader, nobody backs out of a move or shies from a car alongside
        public float pushScale;          // multiplier on the bump-draft push a line of cars gives the car at its head
        public float sideAwareness;      // 0-1: how hard the AI steers away from a car alongside (1 = road-course caution)
        public float laneSpacing;        // m between lane centres when racing in lanes (0 = the AI's default)
        public int packLanes;            // pack racing: how many fixed grooves, centred on the centreline (0 = fit the road)
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
                        // The whole race is the draft - but swept in the 40-car Daytona pack sim (PackRaceSimTests.
                        // DaytonaDraftSweep), a tow worth 1.65x surged cars into the one ahead and strung the field
                        // out, and following at 0.35x the headway ran them into each other. 1.3 / 0.6 packed it up
                        // tightest with the fewest contacts: ~16 m median gap, 60% within 20 m, a quarter 2-wide.
                        // A pack: the draft's top-speed gain is a few mph (a 13% slingshot pulled cars out of line
                        // every lap and the field never formed up), the push from a line behind you is what wins.
                        draftScale = 0.5f,
                        draftFollowScale = 0.6f,
                        raceInLanes = true,
                        plateMph = 190f,
                        flatOut = true,
                        packRacing = true,
                        pushScale = 1f,
                        sideAwareness = 0.15f,     // three wide is normal; you hold your lane and trust the others to
                        laneSpacing = 3.6f,        // 40 ft of road, three grooves: a car's width and a metre and a half
                        packLanes = 3,
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
                        draftFollowScale = 0.8f,
                        raceInLanes = true,
                        pushScale = 0.3f,
                        sideAwareness = 0.6f,      // two wide off the corners, give a little room
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
                        draftFollowScale = 1f,
                        raceInLanes = true,
                        sideAwareness = 0.45f,     // leaning on each other is part of it
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
                        // 1, not less: Watkins Glen's passing was tuned with the full Cup draft (it's how a car
                        // gets alongside into the bus stop), and halving it took the pack sim from 4 passes to 1.
                        draftScale = 1f,
                        draftFollowScale = 1f,
                        sideAwareness = 1f,        // braking zones and run-off: leave room
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
                        draftFollowScale = 1f,
                        raceInLanes = true,
                        sideAwareness = 0.7f,
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
                    t.draftScale = 0.6f;
                    t.plateMph = 194f;
                    t.packLanes = 4;           // room for four wide
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
                    // No plate, no pack: flat turns are braked for, and it races like an intermediate.
                    t.plateMph = 0f;
                    t.flatOut = false;
                    t.packRacing = false;
                    t.pushScale = 0.3f;
                    t.sideAwareness = 0.6f;
                    t.laneSpacing = 0f;
                    t.draftFollowScale = 0.8f;
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
