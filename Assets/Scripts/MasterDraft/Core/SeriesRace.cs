using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // Everything the race scene needs to run one championship round: which car (number, driver, livery, class),
    // which track and how far, how strong the field is and the class performance step. Worked out from the
    // profile by SeriesRace.Plan; MasterDraftRaceLauncher stages it and MasterDraftRaceHost applies it.
    [Serializable]
    public class SeriesRacePlan
    {
        public string seriesId;
        public string seriesName;
        public string carId;         // CarDef.id, the championship's locked car
        public string carset;
        public int number;
        public string driver;        // DM2 short name, the name the player races as
        public string livery;        // Resources sprite, "{carset}livery{number}"
        public int carClass;         // the owned car's class when the round started
        public string trackId;       // TrackCatalog id
        public int round;            // 1-based round about to run
        public int rounds;
        public int laps;
        public int aiLevel;          // DM2 AI level 0-15
        public float aiStrength;     // multiplies the field's grip and pace (in place of OPTIONS > AI STRENGTH)
        public float powerScale;     // PlayerVehicleController.cardPowerScale
        public float gripScale;      // PlayerVehicleController.cardGripScale

        public override string ToString() =>
            $"{seriesId} round {round}/{rounds} at {trackId}, {laps} laps, #{number} class {carClass}, ai {aiLevel} (x{aiStrength:0.###})";
    }

    // A championship round raced in RaceScene: the plan that launches it and the scoring of what came back.
    //
    //  - AI level -> field strength: DM2's AI levels 0-15 become a multiplier on the AI's grip and pace (the same
    //    two numbers OPTIONS > AI STRENGTH scales, AIDifficulty), in place of that setting for the race. Level 7 is
    //    roughly the game as calibrated; Rookies at level 0 are about 10% softer, the top level about 12% harder.
    //    The table below is the one place to tune it.
    //  - Laps: SeriesRules.NextRaceLaps (the round's base laps x the AI level's / the player's multiplier).
    //  - Class step: CarProgression.PowerScale / GripScale of the owned car's class.
    //  - Results: the race field is the game's own drivers (GridSpawner), not the DM2 carset the championship
    //    scores. FinishingOrder maps one onto the other: the player's car takes the player's place; every other
    //    classified car takes the championship car with its number, or the next car of the carset that has none.
    //    So the player's championship finish is their race finish, and the rest of the table moves with the race.
    public static class SeriesRace
    {
        // Field strength by AI level (index = level). 1.0 = the AI as calibrated for each track.
        static readonly float[] AiStrengthTable =
        {
            0.900f, 0.915f, 0.930f, 0.945f, 0.960f, 0.975f, 0.990f, 1.005f,
            1.020f, 1.035f, 1.050f, 1.065f, 1.080f, 1.095f, 1.110f, 1.125f,
        };

        public static float AiStrength(int aiLevel) =>
            AiStrengthTable[Math.Max(0, Math.Min(AiStrengthTable.Length - 1, aiLevel))];

        // The next round of the championship in progress for seriesId, ready to launch; null (and why) if it can't run.
        // series (optional) supplies the display name.
        public static SeriesRacePlan Plan(MasterDraftProfile profile, string seriesId, CarCatalog cars, out string error,
            SeriesDef series = null)
        {
            error = null;
            if (profile == null) { error = "Your profile isn't loaded."; return null; }
            var progress = profile.Championship(seriesId);
            if (progress == null) { error = "Start the championship first."; return null; }
            if (progress.IsComplete) { error = "This championship is over."; return null; }
            var round = SeriesRules.NextRound(progress);
            if (round == null || string.IsNullOrEmpty(round.track)) { error = "This round has no track."; return null; }
            var car = cars?.Get(progress.carId);
            if (car == null) { error = $"The championship's car ({progress.carId}) isn't in the catalogue."; return null; }
            var owned = profile.Car(progress.carId);
            if (owned == null) { error = $"You no longer own the #{car.number} {car.driver}."; return null; }

            int carClass = CarProgression.ClampClass(owned.carClass);
            return new SeriesRacePlan
            {
                seriesId = progress.seriesId,
                seriesName = series != null && series.id == progress.seriesId ? series.name : progress.seriesId,
                carId = car.id,
                carset = car.carset,
                number = car.number,
                driver = car.driver ?? "",
                livery = string.IsNullOrEmpty(car.livery) ? car.carset + "livery" + car.number : car.livery,
                carClass = carClass,
                trackId = round.track,
                round = progress.round + 1,
                rounds = progress.Length,
                laps = SeriesRules.NextRaceLaps(progress),
                aiLevel = progress.aiLevel,
                aiStrength = AiStrength(progress.aiLevel),
                powerScale = CarProgression.PowerScale(carClass),
                gripScale = CarProgression.GripScale(carClass),
            };
        }

        // The race's classification as championship car ids, winner first (SeriesRules.RecordRace's input).
        // raceNumbers = every classified car's number in finishing order, the player's included (its number there is
        // ignored); playerPosition = the player's 1-based place, 0 if not classified. A race car takes the
        // championship car with its number if that car is still free, otherwise the first free car of the table;
        // once the table runs out, the cars still unplaced are left out.
        public static List<string> FinishingOrder(ChampionshipProgress progress, IList<int> raceNumbers, int playerPosition)
        {
            var order = new List<string>();
            if (progress == null || progress.table == null) return order;
            int n = raceNumbers == null ? 0 : raceNumbers.Count;
            int playerSlot = playerPosition >= 1 ? playerPosition - 1 : -1;
            int slots = Math.Max(n, playerSlot + 1);
            var placed = new string[slots];
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(progress.carId)) used.Add(progress.carId);
            if (playerSlot >= 0) placed[playerSlot] = progress.carId;

            // Same number first, so a car the field shares with the carset keeps its own line in the table.
            for (int i = 0; i < n; i++)
            {
                if (i == playerSlot) continue;
                foreach (var e in progress.table)
                    if (e != null && e.number == raceNumbers[i] && !used.Contains(e.carId))
                    {
                        placed[i] = e.carId;
                        used.Add(e.carId);
                        break;
                    }
            }

            // Then everyone else, in finishing order, from the free cars in table order.
            int next = 0;
            for (int i = 0; i < slots; i++)
            {
                if (placed[i] != null || i == playerSlot) continue;
                while (next < progress.table.Count && (progress.table[next] == null || used.Contains(progress.table[next].carId))) next++;
                if (next >= progress.table.Count) break;
                placed[i] = progress.table[next].carId;
                used.Add(placed[i]);
            }

            foreach (var id in placed) if (id != null) order.Add(id);
            return order;
        }

        // Score a finished round and pay it (RaceRewards.ChampionshipRound): the table, XP, and after the last round
        // the championship's money, gears and prize car. raceNumbers / playerPosition as FinishingOrder; the race's
        // field size scales the gears. series may be null if the data no longer has it. Throws if no championship
        // is in progress for seriesId.
        public static RaceRewardSummary RecordResult(MasterDraftProfile profile, SeriesDef series, string seriesId,
            IList<int> raceNumbers, int playerPosition, CarCatalog cars)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            seriesId = series?.id ?? seriesId;
            var progress = profile.Championship(seriesId)
                ?? throw new InvalidOperationException($"no championship in progress for {seriesId}");
            var order = FinishingOrder(progress, raceNumbers, playerPosition);
            int field = raceNumbers == null ? 0 : Math.Max(raceNumbers.Count, playerPosition);
            return RaceRewards.ChampionshipRound(profile, series, seriesId, order, cars, field);
        }

        // The car a series' entry picker starts on: the profile's selected car if it can enter, else the first
        // garage car that can; null if none can.
        public static OwnedCar DefaultEntry(SeriesDef series, MasterDraftProfile profile, CarCatalog cars)
        {
            if (series == null || profile == null || cars == null) return null;
            var eligible = Restrictions.EligibleGarage(series.restriction, cars, profile.garage, profile.level);
            if (eligible.Count == 0) return null;
            foreach (var c in eligible) if (c.id == profile.selectedCar) return c;
            return eligible[0];
        }
    }
}
