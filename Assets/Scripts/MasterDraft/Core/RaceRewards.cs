using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // What one race (or a championship's last round) paid. The results screen shows it line by line.
    public class RaceRewardSummary
    {
        public RaceRewardKind kind;
        public int position;              // the player's 1-based finish in this race (0 = not classified)
        public int money;                 // coins paid
        public int xp;                    // XP earned
        public int levelBefore, levelAfter;
        public string carId;              // the car raced
        public int gears;                 // gears landed on the car raced (0 if it isn't owned)
        public List<PrizeOutcome> prizes = new List<PrizeOutcome>();   // car / gears / alt paint prizes paid
        public int coins;                 // balance after

        // Championships
        public RoundResult round;         // what the round did to the table
        public bool championshipOver;
        public int championshipPosition;  // final place in the standings (championshipOver only)
        public int rounds;                // the championship's length (championshipOver only)

        // Challenges
        public ChallengeResult challenge;

        public int LevelsGained => levelAfter - levelBefore;
    }

    public enum RaceRewardKind
    {
        Race = 0,               // a one-off race off a series (DM2's default reward path)
        ChampionshipRound = 1,  // a round mid-championship: XP only, the purse waits for the end (DM2 rerouted
                                // mid-season rounds to the championship hub, past the rewards screen)
        ChampionshipFinal = 2,  // the last round: XP for it plus the championship's rewards
        Challenge = 3,
    }

    // Draftmaster 2's race rewards, ported from RaceResultsUI (XP and level, git show
    // 2217821d^:Assets/Scripts/UI/RaceResultsUI.cs), RaceRewardsUI (money, gears and prize cars) and PrizeMoney:
    //
    //  - Money by finishing position: 10,000 for a win, 7,500 for 2nd ... 100 for 43rd and beyond (PrizeMoney). A
    //    challenge pays a flat 1,000 when completed, nothing otherwise. A championship pays its final standings
    //    position's money times the number of rounds, at the end; its rounds pay nothing on their own.
    //  - XP: round(((90 / place) + 10) x (1 + aiLevel / 10)), integer division on the place — 110 for a win at AI 1,
    //    every race (championship rounds and challenges too). Each level takes XpToNextLevel(level) (100 at level 1
    //    ... 16,000 at 45); XP over the line carries into the next level. Level 46 is the top (DM2's table ends at 45).
    //  - Gears to the car raced: the race's top gears are floor(aiLevel / 3) + 2, clamped 3..7, minus one for every
    //    four places back, scaled down for a field under 40 (never below 24 counted). A championship multiplies its
    //    final-position gears by the rounds, with a floor of half the rounds for seasons of 8+. Challenges pay no gears
    //    (DM2 gave events their prize instead). Gears only land on a car the player owns.
    //  - Prizes: a one-off race draws a car's parts from the series' prize pool for a top 3, otherwise with a chance of
    //    gears / 8 (each gear a raffle ticket). A finished championship pays ceil(rounds / 2) x ceil(aiLevel / 5) /
    //    ceil(place / 2) gears of a prize-pool car (DM2's rewardRatio; Master Draft guarantees the champion at least
    //    one). Pool cars at class 6 are skipped unless that's all there is. A car not yet owned arrives holding those
    //    gears (CarProgression.AwardGears). Challenge prizes are Challenges.ClaimPrize.
    //
    // Level-up rewards (DM2's GameData.levelUpRewards: pooled gears and transfers) aren't ported: Master Draft has no
    // gear pool. Every number is in the tables below.
    public static class RaceRewards
    {
        public const int MaxLevel = 46;
        public const int DefaultField = 40;   // DM2 assumed a 40-car field when it didn't know
        public const int MinCountedField = 24;  // "Min of 60%, as not to scale the rewards too low"
        public const int ChallengeMoney = 1000;
        public const int LongSeasonRounds = 8;  // championships this long get the generous gears formula

        // PrizeMoney.cashAmount, by finishing position - 1. 43rd and beyond get the last entry.
        static readonly int[] PrizeMoneyTable =
        {
            10000, 7500, 6000, 4750, 4250, 4000, 3800, 3600, 3500, 3400,
            3300, 3200, 3100, 3000, 2900, 2800, 2700, 2600, 2500, 2400,
            2300, 2200, 2100, 2000, 1900, 1800, 1700, 1600, 1500, 1400,
            1300, 1200, 1100, 1000, 900, 800, 700, 600, 500, 400,
            300, 200, 100,
        };

        // RaceResultsUI.levelUpExp, by level - 1 (levels 1..45).
        static readonly int[] LevelXpTable =
        {
            100, 125, 150, 175, 200, 225, 250, 275, 300, 350,
            400, 450, 500, 550, 650, 750, 850, 950, 1050, 1250,
            1450, 1650, 1850, 2050, 2300, 2550, 2800, 3050, 3300, 3600,
            3900, 4200, 4500, 4800, 5300, 5800, 6500, 7200, 8000, 9000,
            10000, 11200, 12500, 14000, 16000,
        };

        // --- Tables --------------------------------------------------------------------------------------------

        // Money for a 1-based finishing position; 0 or less pays the last entry.
        public static int PrizeMoney(int position)
        {
            if (position < 1 || position > PrizeMoneyTable.Length) return PrizeMoneyTable[PrizeMoneyTable.Length - 1];
            return PrizeMoneyTable[position - 1];
        }

        // XP needed to leave this level; int.MaxValue at the top (XP still banks there).
        public static int XpToNextLevel(int level) =>
            level >= 1 && level <= LevelXpTable.Length ? LevelXpTable[level - 1] : int.MaxValue;

        // XP for a 1-based finish against this AI level. Unclassified (0) counts as DefaultField.
        public static int RaceXp(int position, int aiLevel)
        {
            int place = position >= 1 ? position : DefaultField;
            double xp = (90 / place + 10) * (1.0 + Math.Max(0, aiLevel) / 10.0);
            return (int)Math.Round(xp, MidpointRounding.ToEven);   // Mathf.Round rounds half to even too
        }

        // A race's top gears (the winner's, before field scaling): floor(aiLevel / 3) + 2, clamped 3..7.
        public static int MaxRaceGears(int aiLevel) => Math.Max(3, Math.Min(7, Math.Max(0, aiLevel) / 3 + 2));

        // The field the gears are scaled to: DefaultField when unknown (<= 0), never below MinCountedField.
        public static int CountedField(int fieldSize) => fieldSize <= 0 ? DefaultField : Math.Max(MinCountedField, fieldSize);

        // Gears for a one-off race finish (RaceRewardsUI's default path). 0 or less when the finish is too far back.
        public static int RaceGears(int position, int aiLevel, int fieldSize)
        {
            int p0 = (position >= 1 ? position : CountedField(fieldSize)) - 1;
            int g = MaxRaceGears(aiLevel) - p0 / 4;
            return Math.Max(0, ScaleToField(g, fieldSize));
        }

        // Gears for a championship's final standings position over its rounds (RaceRewardsUI with rewardMultiplier =
        // the season's length).
        public static int ChampionshipGears(int position, int aiLevel, int fieldSize, int rounds)
        {
            rounds = Math.Max(1, rounds);
            int p0 = (position >= 1 ? position : CountedField(fieldSize)) - 1;
            if (rounds >= LongSeasonRounds)
            {
                int g = ScaleToField(MaxRaceGears(aiLevel) - (p0 + 1) / 4, fieldSize);
                int minimum = (rounds + 1) / 2;
                return g >= 1 ? Math.Max(minimum, g * rounds) : minimum;
            }
            int short_ = ScaleToField(MaxRaceGears(aiLevel) - p0 / 4, fieldSize);
            return short_ > 0 ? short_ * rounds : 0;
        }

        public static int ChampionshipMoney(int position, int rounds) => PrizeMoney(position) * Math.Max(1, rounds);

        // Gears of a prize-pool car a finished championship pays (DM2's rewardRatio); 0 = no prize car. The champion
        // always gets at least one, where DM2's low-AI series paid nothing.
        public static int ChampionshipPrizeGears(int position, int aiLevel, int rounds)
        {
            int place = Math.Max(1, position);
            int ratio = (rounds + 1) / 2 * ((Math.Max(0, aiLevel) + 4) / 5) / ((place + 1) / 2);
            if (position == 1) ratio = Math.Max(1, ratio);
            return Math.Max(0, ratio);
        }

        static int ScaleToField(int gears, int fieldSize) => (int)Math.Floor(gears / 40.0 * CountedField(fieldSize));

        // --- Level -------------------------------------------------------------------------------------------

        // Bank XP and level up as many times as it covers. Returns the levels gained.
        public static int AddXp(MasterDraftProfile profile, int xp)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.level < MasterDraftProfile.FirstLevel) profile.level = MasterDraftProfile.FirstLevel;
            profile.xp = (int)Math.Min(int.MaxValue, (long)Math.Max(0, profile.xp) + Math.Max(0, xp));
            int gained = 0;
            while (profile.level < MaxLevel && profile.xp >= XpToNextLevel(profile.level))
            {
                profile.xp -= XpToNextLevel(profile.level);
                profile.level++;
                gained++;
            }
            return gained;
        }

        // 0-1 bar toward the next level (1 at the top).
        public static float LevelProgress(MasterDraftProfile profile)
        {
            if (profile == null) return 0f;
            if (profile.level >= MaxLevel) return 1f;
            return Math.Min(1f, Math.Max(0, profile.xp) / (float)XpToNextLevel(profile.level));
        }

        // --- Paying races ------------------------------------------------------------------------------------

        // A one-off race in a garage car. prize (optional) = the series' prize pool for the raffle; cars is needed
        // for it. position 1-based, 0 = not classified (paid as last of the field).
        public static RaceRewardSummary Race(MasterDraftProfile profile, string carId, int position, int aiLevel,
            int fieldSize, PrizeFilter prize = null, CarCatalog cars = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var s = Begin(profile, RaceRewardKind.Race, carId, position);
            int place = position >= 1 ? position : CountedField(fieldSize);
            s.money = PrizeMoney(place);
            s.xp = RaceXp(position, aiLevel);
            int gears = RaceGears(position, aiLevel, fieldSize);
            s.gears = GiveGears(profile, carId, gears);

            // The raffle: top 3 always, otherwise a gears-in-8 chance. Seeded off the profile and race count so a
            // reveal can be replayed.
            if (gears > 0 && prize != null && !prize.IsNone && cars != null)
            {
                var rng = new DraftRng(Challenges.PrizeSeed(profile.Seed(), "race", profile.racesRun + 1));
                if (place <= 3 || rng.NextDouble() * 8.0 <= gears)
                {
                    var o = PayPoolGears(prize, cars, profile, 1, rng);
                    if (o != null) s.prizes.Add(o);
                }
            }
            return Finish(profile, s, position);
        }

        // A championship round. Records it (SeriesRules.RecordRace) and pays its XP; after the last round, also the
        // championship's money, gears and prize car, and folds it into the series record (the progress is then
        // dropped from the profile). series may be null if the data no longer has it (no prize car then).
        // finishingOrder = car ids winner first. fieldSize 0 = the championship's table.
        public static RaceRewardSummary ChampionshipRound(MasterDraftProfile profile, SeriesDef series, string seriesId,
            IList<string> finishingOrder, CarCatalog cars, int fieldSize = 0)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            seriesId = series?.id ?? seriesId;
            var progress = profile.Championship(seriesId)
                ?? throw new InvalidOperationException($"no championship in progress for {seriesId}");
            if (fieldSize <= 0) fieldSize = progress.table.Count;

            var round = SeriesRules.RecordRace(progress, finishingOrder);
            var s = Begin(profile, round.championshipOver ? RaceRewardKind.ChampionshipFinal : RaceRewardKind.ChampionshipRound,
                progress.carId, round.playerFinish);
            s.round = round;
            s.xp = RaceXp(round.playerFinish, progress.aiLevel);

            if (round.championshipOver && SeriesRules.CollectRewards(progress))
            {
                s.championshipOver = true;
                s.rounds = progress.Length;
                int final = SeriesRules.Position(progress);
                s.championshipPosition = final;
                s.money = ChampionshipMoney(final, progress.Length);
                s.gears = GiveGears(profile, progress.carId,
                    ChampionshipGears(final, progress.aiLevel, fieldSize, progress.Length));

                int prizeGears = ChampionshipPrizeGears(final, progress.aiLevel, progress.Length);
                if (prizeGears > 0 && series?.prize != null && !series.prize.IsNone && cars != null)
                {
                    var record = profile.SeriesRecord(seriesId);
                    int done = record == null ? 0 : record.championships;
                    var rng = new DraftRng(Challenges.PrizeSeed(profile.Seed(), seriesId, done + 1));
                    var o = PayPoolGears(series.prize, cars, profile, prizeGears, rng);
                    if (o != null) s.prizes.Add(o);
                }
                SeriesRules.RecordChampionship(profile.seriesRecords, progress);
                profile.championships.Remove(progress);
            }
            return Finish(profile, s, round.playerFinish);
        }

        // A challenge chapter. Records it (Challenges.RecordResult), pays ChallengeMoney when completed, the XP, and
        // the chapter's prize when one is due (Challenges.ClaimPrize). carId = the car raced (a garage car, or the one
        // the chapter handed over — it gets no gears either way).
        public static RaceRewardSummary Challenge(MasterDraftProfile profile, ChallengeEvent ev, ChallengeChapter chapter,
            string carId, int position, bool momentComplete, CarCatalog cars)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (chapter == null) throw new ArgumentNullException(nameof(chapter));
            var s = Begin(profile, RaceRewardKind.Challenge, carId, position);
            s.challenge = Challenges.RecordResult(profile.challengeRecords, ev, chapter, position, momentComplete);
            s.money = s.challenge.completed ? ChallengeMoney : 0;
            s.xp = RaceXp(position, chapter.aiLevel);
            if (s.challenge.prizeDue)
            {
                var o = Challenges.ClaimPrize(ev, chapter, profile.challengeRecords, cars, profile.garage, profile.altPaints,
                    profile.Seed());
                if (o.PaysSomething) s.prizes.Add(o);
            }
            return Finish(profile, s, position);
        }

        // --- Internals ---------------------------------------------------------------------------------------

        static RaceRewardSummary Begin(MasterDraftProfile profile, RaceRewardKind kind, string carId, int position) =>
            new RaceRewardSummary { kind = kind, carId = carId, position = Math.Max(0, position), levelBefore = profile.level };

        static RaceRewardSummary Finish(MasterDraftProfile profile, RaceRewardSummary s, int position)
        {
            profile.coins = (int)Math.Min(int.MaxValue, (long)profile.coins + s.money);
            AddXp(profile, s.xp);
            profile.racesRun++;
            if (position == 1) profile.wins++;
            s.levelAfter = profile.level;
            s.coins = profile.coins;
            return s;
        }

        // Gears onto an owned car; 0 (and nothing changes) if the car isn't in the garage — a race reward never
        // hands over a car the player only borrowed.
        static int GiveGears(MasterDraftProfile profile, string carId, int gears)
        {
            if (gears <= 0) return 0;
            var car = profile.Car(carId);
            if (car == null) return 0;
            CarProgression.AddGears(car, gears);
            return gears;
        }

        // Gears for a car drawn from a prize pool (skipping class-6 cars unless that's all there is); a car not owned
        // arrives holding them. Null when the pool is empty.
        static PrizeOutcome PayPoolGears(PrizeFilter filter, CarCatalog cars, MasterDraftProfile profile, int gears, DraftRng rng)
        {
            var pool = new List<CarDef>();
            foreach (var c in cars.Cars) if (filter.Matches(c)) pool.Add(c);
            if (pool.Count == 0) return null;
            var open = pool.FindAll(c => !CarProgression.IsMaxClass(profile.Car(c.id)));
            if (open.Count == 0) open = pool;
            var car = rng.Pick(open);
            var result = CarProgression.AwardGears(profile.garage, car.id, gears);
            return new PrizeOutcome
            {
                kind = ChallengePrizeKind.Category,
                carId = car.id,
                gears = gears,
                newCar = result == CarAwardResult.NewCar,
            };
        }
    }
}
