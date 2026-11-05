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
        public float draftRangeScale;    // multiplier on how far back the tow reaches (0 = the car's own range)
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
        public float alongsideGap;       // m centre to centre the AI leave a car alongside (0 = the AI's default)
        public int packLanes;            // pack racing: how many fixed grooves, centred on the centreline (0 = fit the road)
        public float tyreWearScale;      // multiplier on wear rate
        public float tyreGripLossScale;  // multiplier on how much grip a worn tyre loses (0 = full loss, as everywhere)
        public float fuelBurnScale;      // multiplier on burn rate
        public float dirtyAirLoss;       // share of grip a full tow's wake costs at speed (Draftmaster.Sim.DirtyAir; 0 = none)

        // The car package - NASCAR runs a different car on ovals than on road courses, and so does this. Both 0 = 1.
        public float packageGrip;        // multiplier on every car's tyre grip (TrackConditions.PackageGripScale)
        public float packageYawInertia;  // multiplier on the player car's yaw inertia (TrackConditions.PackageYawInertiaScale)

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
        // The oval package. With the road-course grip (Cup24's 2.0 g x the 1.44 baked conditions) every oval was
        // taken at or near the top speed: Texas's 3-4 held 213 mph on the centreline and the 1.5-milers barely
        // lifted, so the car felt glued down and slow rather than heavy. A centreline lap model (Cup24's accel/brake
        // curves, BankedGrip, the generated turn radii and banking) against real Cup laps put the grip at:
        //   intermediates x0.8  - Texas 29.4 s (real ~28.4), Charlotte 31.5 (30.0), Atlanta 30.9 (30.6), Dover 21.8
        //                          (22.9), Homestead 31.8 (32.0), Nashville 30.0 (29.5), Indianapolis 49.8 (49.5);
        //                          the racing line's wider radius takes a second or so off each. Was 28-29 s at
        //                          Texas/Charlotte with corners at 170-200 mph - flat or nearly.
        //   short tracks x0.7   - Phoenix 24.8 (27.0), Richmond 20.7 (22.6), New Hampshire 26.4 (29.3), Martinsville
        //                          18.9 (19.8); still quick, but from 15-25% under a real lap at x1.
        // The grip goes to everyone through TrackConditions.Effective / AiEffective, so the AI's speed plan, the
        // racing-line overlay and the physics all move together. A plated superspeedway keeps x1: RestrictorPlate
        // already solves its turns' grip to be taken flat out at the plate speed, and the packs are tuned on that.
        public const float OvalPackageGrip = 0.8f;
        public const float ShortTrackPackageGrip = 0.7f;
        // A stock car on an oval should feel like 3,400 lb: slower to take a set into the turn, slower to snap back.
        // Player's car only - the AI's steering is tuned on the road-course car and an oval is long steady arcs, so
        // this is feel, not pace.
        public const float OvalPackageYawInertia = 1.3f;

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
                        // How far back the tow reaches. At the car's own 35 m a car that slipped that far back had no
                        // tow anywhere - nothing to come back for - and the pack strung out into trains; NR2003's tow
                        // fades out at 1.5 s, over 100 m at plate speed. Swept in the 40-car Daytona sim
                        // (PackRaceSimTests.SuperspeedwayDraftRangeConfirm, three rosters): 35 m kept 44% of the field
                        // in the lead pack, 3-wide 51% in 10 s rows, ~5 lane changes per car per lap; 90 m kept 92%,
                        // 3-wide 80% in 30 s rows, 1.3 lane changes, half the hard contacts; 120 m blew up a roster.
                        draftRangeScale = 2.6f,
                        draftFollowScale = 0.6f,
                        raceInLanes = true,
                        plateMph = 190f,
                        flatOut = true,
                        packRacing = true,
                        pushScale = 1f,
                        packageGrip = 1f,          // the plate solves the turns' grip (RestrictorPlate)
                        packageYawInertia = OvalPackageYawInertia,
                        sideAwareness = 0.15f,     // three wide is normal; you hold your lane and trust the others to
                        laneSpacing = 3.6f,        // 40 ft of road, three grooves: a car's width and a metre and a half
                        packLanes = 3,
                        tyreWearScale = 0.7f,
                        // Flat out with grip to spare in the turns, old tyres cost little here: a full stint's wear
                        // cost 2.3 s a lap alone at Daytona at the road-course loss (PackRaceSimTests.TyreFalloff).
                        tyreGripLossScale = 0.3f,
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
                        packageGrip = OvalPackageGrip,
                        packageYawInertia = OvalPackageYawInertia,
                        // Aero push (NR2003's wake cuts the follower's downforce with the same table as its drag):
                        // OFF for now. At 0.06 (~6% grip right behind a car at 170 mph) the pack sim
                        // (PackRaceSimTests.DirtyAirOvals, Charlotte + Atlanta, two rosters) lost a quarter of its
                        // passes (39 -> 28.5 a run) with a few more side contacts (19 -> 25, HARD 0 -> 2): the AI
                        // has nothing that looks for clean air in another lane, which is how NR2003's lateral field
                        // turns the same wake into passing. Turn on with that, and play-test the player's car.
                        dirtyAirLoss = 0f,
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
                        alongsideGap = 3.4f,       // braking side by side into a tight turn: at 2.6 m the boxes touched
                        packageGrip = ShortTrackPackageGrip,
                        packageYawInertia = OvalPackageYawInertia,
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
                        // The road-course package is the car every handling number was tuned on (Watkins Glen's AI
                        // calibration, the trained lines, the player's dialled-in balance). Leave it at 1.
                        packageGrip = 1f,
                        packageYawInertia = 1f,
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
                    // The banking carries real speed: at the short-track grip the lap model ran 16.2 s (real ~15.1),
                    // at the intermediate grip 15.3.
                    t.packageGrip = OvalPackageGrip;
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
                    t.packageGrip = OvalPackageGrip;   // unplated, so the intermediate package (49.8 s, real ~49.5)
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
