using System;

namespace Draftmaster.MasterDraft
{
    // Everything the race scene needs to run one challenge chapter: which car (a garage car, or the one the chapter
    // hands over), which track and how far, how strong the field is and the class step. Worked out from the profile
    // by ChallengeRace.Plan; MasterDraftRaceLauncher stages it and MasterDraftRaceHost applies it — the same path a
    // championship round takes (SeriesRacePlan).
    [Serializable]
    public class ChallengeRacePlan
    {
        public string eventId;
        public string eventName;
        public string chapterId;
        public string chapterName;
        public int week;              // the cycle week it was started in
        public string carId;          // CarDef.id of the car raced (may be outside the catalogue for a handed-over car)
        public bool providedCar;      // the chapter handed the car over (moment.playerLivery); not the player's to keep
        public int number;
        public string driver;         // the name the player races as; "" = keep the player's own
        public string livery;         // Resources sprite, "{carset}livery{number}"
        public int carClass;          // the owned car's class (0 for a handed-over car the player doesn't own)
        public string trackId;        // TrackCatalog id
        public int laps;              // laps the race scene runs (Challenges.LapsToRun)
        public int raceLaps;          // the historic race's full length (Challenges.RaceLaps)
        public int startingLap;       // the lap the moment joins on (1 = from the green)
        public int aiLevel;
        public float aiStrength;      // SeriesRace.AiStrength(aiLevel)
        public float powerScale;      // PlayerVehicleController.cardPowerScale
        public float gripScale;       // PlayerVehicleController.cardGripScale

        // The moment parts RaceScene can't stage yet (see ChallengeRace): the race runs from a green flag instead.
        public bool momentStartSkipped;   // the moment joins after lap 1
        public bool momentGridSkipped;    // the moment sets a field order

        public override string ToString() =>
            $"{chapterId} at {trackId}, {laps} laps (from lap {startingLap} of {raceLaps}), #{number} class {carClass}, ai {aiLevel} (x{aiStrength:0.###})";
    }

    // A challenge chapter raced in RaceScene: the plan that launches it, the cycle week the screen shows, and the
    // scoring of what came back.
    //
    //  - The week: DM2 advanced its 13-week cycle one week per 7 real days (GameData). Here the cycle runs off the
    //    real calendar for everyone alike — the week turns at 00:00 UTC each Monday — so "this week's events" are
    //    the same for every player (online-ready) and don't depend on a profile's age.
    //  - Field strength, the class step and the car's paint are SeriesRace's (same AI level table, same
    //    CarProgression scales).
    //  - MOMENTS. A moment replays the end of a real race: join on lap N of M with the field in a given order.
    //    RaceScene can do neither today — every race starts from lap 1 behind a formation lap, and a one-off race's
    //    grid is drawn by GridSpawner (RaceWeekend.GridOrder is ignored outside a career weekend, and its cars are
    //    the game's drivers, not the moment's DM2 carset). So a moment runs as a normal race over the laps the
    //    moment has left (LapsToRun: lap N to M), and the grid is the usual one.
    //    TODO(moments): when RaceScene can start mid-race, stage startingLap (lap counter from N, field strung out
    //    as it was) and, when GridSpawner takes a one-off grid, moment.fieldOrder (Challenges.GridCars /
    //    PlayerGridSlot already decode it); then port the moment's win conditions (DM2 MomentsCriteria, `name`)
    //    and pass momentComplete to RecordResult. Until then a moment is completed by winning.
    public static class ChallengeRace
    {
        // Monday 2026-01-05 00:00 UTC is week 1 of the cycle.
        public static readonly DateTime CycleEpoch = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        // The cycle week (1..cycleWeeks) a moment in time falls in.
        public static int WeekAt(DateTime utc, int cycleWeeks = Challenges.DefaultCycleWeeks)
        {
            if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
            long days = (long)Math.Floor((utc - CycleEpoch).TotalDays);
            long weeks = days >= 0 ? days / 7 : -((-days + 6) / 7);
            long w = weeks % Math.Max(1, cycleWeeks);
            if (w < 0) w += Math.Max(1, cycleWeeks);
            return (int)w + 1;
        }

        // When the cycle next turns over to a new week, after utc.
        public static DateTime NextWeekStarts(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
            long days = (long)Math.Floor((utc - CycleEpoch).TotalDays);
            long weeks = days >= 0 ? days / 7 : -((-days + 6) / 7);
            return CycleEpoch.AddDays((weeks + 1) * 7);
        }

        // Whether the chapter is a moment the race scene can only approximate, and how, for the screen.
        public static string MomentNote(ChallengeChapter chapter)
        {
            if (chapter == null) return "";
            int start = Challenges.StartingLap(chapter);
            bool late = start > 1;
            bool grid = chapter.moment != null && chapter.moment.HasFieldOrder;
            if (!late && !grid) return "";
            int laps = Challenges.LapsToRun(chapter);
            if (late)
                return $"Moment: lap {start} of {Challenges.RaceLaps(chapter)}. Runs as a {laps}-lap race from the green"
                       + (grid ? ", usual grid." : ".");
            return "Moment: its grid order isn't staged yet; usual grid.";
        }

        // The chapter, ready to launch in carId (ignored when the chapter hands a car over); null (and why) when it
        // can't run: not live this week, locked behind the previous chapter, or the car / level don't qualify.
        public static ChallengeRacePlan Plan(MasterDraftProfile profile, ChallengeCatalog challenges, string chapterId,
            string carId, CarCatalog cars, int week, out string error)
        {
            error = null;
            if (profile == null) { error = "Your profile isn't loaded."; return null; }
            var chapter = challenges?.GetChapter(chapterId);
            if (chapter == null) { error = "That challenge doesn't exist."; return null; }
            var ev = challenges.EventOf(chapter);
            if (ev == null) { error = "That challenge's event doesn't exist."; return null; }

            CarDef car = null;
            OwnedCar owned = null;
            string raceCarId;
            if (chapter.ProvidesCar)
            {
                raceCarId = Challenges.ProvidedCarId(chapter);
                if (raceCarId == null) { error = $"The car this challenge hands over ({chapter.moment.playerLivery}) can't be read."; return null; }
                car = cars?.Get(raceCarId);
                owned = profile.Car(raceCarId);
            }
            else
            {
                car = cars?.Get(carId);
                owned = profile.Car(carId);
                if (car == null || owned == null) { error = "Pick a car from your garage."; return null; }
                raceCarId = car.id;
            }

            var entry = Challenges.CanEnter(ev, chapter, car, profile.garage, profile.level, week, profile.challengeRecords,
                challenges.CycleWeeks);
            if (!entry.Eligible)
            {
                error = entry.Summary.Split('\n')[0];
                return null;
            }

            int carClass = owned == null ? CarProgression.MinClass : CarProgression.ClampClass(owned.carClass);
            int start = Challenges.StartingLap(chapter);
            var plan = new ChallengeRacePlan
            {
                eventId = ev.id,
                eventName = ev.name,
                chapterId = chapter.id,
                chapterName = chapter.name,
                week = Challenges.CycleWeek(week, challenges.CycleWeeks),
                carId = raceCarId,
                providedCar = chapter.ProvidesCar,
                carClass = carClass,
                trackId = chapter.track,
                laps = Challenges.LapsToRun(chapter),
                raceLaps = Challenges.RaceLaps(chapter),
                startingLap = start,
                aiLevel = chapter.aiLevel,
                aiStrength = SeriesRace.AiStrength(chapter.aiLevel),
                powerScale = CarProgression.PowerScale(carClass),
                gripScale = CarProgression.GripScale(carClass),
                momentStartSkipped = start > 1,
                momentGridSkipped = chapter.moment != null && chapter.moment.HasFieldOrder,
            };
            if (chapter.ProvidesCar)
            {
                plan.livery = chapter.moment.playerLivery;
                int at = plan.livery.LastIndexOf("livery", StringComparison.Ordinal);
                int.TryParse(plan.livery.Substring(at + "livery".Length), out plan.number);
                plan.driver = car?.driver ?? "";
            }
            else
            {
                plan.number = car.number;
                plan.driver = car.driver ?? "";
                plan.livery = string.IsNullOrEmpty(car.livery) ? car.carset + "livery" + car.number : car.livery;
            }
            return plan;
        }

        // Score a finished chapter race and pay it (RaceRewards.Challenge): the record, the money and XP, and the
        // prize when it's due. position 1-based, 0 = not classified. Moments are completed by winning until their
        // conditions are ported (see the class comment).
        public static RaceRewardSummary RecordResult(MasterDraftProfile profile, ChallengeCatalog challenges,
            ChallengeRacePlan plan, int position, CarCatalog cars)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var chapter = challenges?.GetChapter(plan.chapterId)
                ?? throw new InvalidOperationException($"no challenge chapter {plan.chapterId}");
            return RaceRewards.Challenge(profile, challenges.EventOf(chapter), chapter, plan.carId, position,
                momentComplete: false, cars);
        }

        // The car a chapter's entry picker starts on: the profile's selected car if it can enter, else the first
        // garage car that can; null if none can (or the chapter hands its own car over).
        public static OwnedCar DefaultEntry(ChallengeChapter chapter, MasterDraftProfile profile, CarCatalog cars)
        {
            if (chapter == null || chapter.ProvidesCar || profile == null || cars == null) return null;
            var eligible = Restrictions.EligibleGarage(chapter.restriction, cars, profile.garage, profile.level);
            if (eligible.Count == 0) return null;
            foreach (var c in eligible) if (c.id == profile.selectedCar) return c;
            return eligible[0];
        }
    }
}
