using System;
using System.Collections.Generic;

namespace Draftmaster.Data
{
    // Builds "new-gen" rookie drivers who enter the world each season. A rookie debuts young with a LOW current
    // ability but a HIGH ceiling (PotentialAbility) — DriverProgression then grows them toward that ceiling over
    // their early seasons. Stats are shaped by a random archetype so intakes have character, not flat noise.
    public static class NewGenGenerator
    {
        // A rookie's raw talent shape. Weights below bias the 0-20 skill stats; ability ceiling is separate.
        enum Archetype
        {
            Charger,     // aggressive short-track banger
            Smooth,      // consistent tyre/fuel saver
            RoadRacer,   // road-course / open-wheel convert
            Ovalist,     // classic speedway/superspeedway runner
            Qualifier,   // one-lap specialist, raw pace
            AllRounder   // no glaring weakness
        }

        // Age band a rookie can debut at.
        const int MinDebutAge = 17;
        const int MaxDebutAge = 24;

        // Generate one rookie for the given season.
        public static Driver Rookie(int season, Random rng)
        {
            var (first, last) = DriverNames.Pick(rng);
            var arch = (Archetype)rng.Next(Enum.GetValues(typeof(Archetype)).Length);

            var d = new Driver
            {
                FirstName = first,
                LastName = last,
                Nickname = DriverNames.PickNickname(rng),
                Age = rng.Next(MinDebutAge, MaxDebutAge + 1),
                DebutSeason = season,
                PeakAge = rng.Next(27, 34),          // tops out late-20s / early-30s
                Retired = false
            };

            ApplyArchetype(d, arch, rng);

            // Ceiling: most rookies 60-85, with the occasional generational talent (86-96) and a few journeymen (52-60).
            int roll = rng.Next(100);
            int potential =
                roll < 8 ? rng.Next(86, 97) :    // ~8% future stars
                roll < 25 ? rng.Next(52, 61) :   // ~17% capped journeymen
                rng.Next(61, 86);                // the rest, solid pros
            d.PotentialAbility = potential;

            // Debut ability sits well below the ceiling — the gap is what they grow into.
            int gap = rng.Next(18, 34);
            d.CurrentAbility = Clamp(potential - gap, 30, potential - 6);

            // Scale the raw skill spread to sit roughly around the current ability so a green rookie also has green stats.
            ScaleStatsToAbility(d);
            return d;
        }

        // Build a full intake of rookies for a season.
        public static List<Driver> Intake(int season, int count, Random rng)
        {
            var list = new List<Driver>(count);
            for (int i = 0; i < count; i++) list.Add(Rookie(season, rng));
            return list;
        }

        // --- internals ---

        // Base each 0-20 stat then bump the archetype's signature stats. Values here are the driver's *innate shape*;
        // ScaleStatsToAbility then compresses them toward the (low) rookie ability.
        static void ApplyArchetype(Driver d, Archetype arch, Random rng)
        {
            // Baseline mediocre everywhere.
            d.ShortTracks = Base(rng); d.Speedways = Base(rng); d.Superspeedways = Base(rng);
            d.RoadCourses = Base(rng); d.DirtCourses = Base(rng); d.OpenWheel = Base(rng);
            d.FuelManagement = Base(rng); d.TyreManagement = Base(rng); d.Qualifying = Base(rng);
            d.Consistency = Base(rng); d.Aggression = Base(rng); d.Awareness = Base(rng); d.Adaptability = Base(rng);

            // Commercial stats start low for unknowns; stars build these over a career.
            d.SponsorAppeal = rng.Next(4, 11);
            d.FanSupport = rng.Next(3, 10);
            d.Prestige = rng.Next(2, 7);

            switch (arch)
            {
                case Archetype.Charger:
                    d.ShortTracks = Bump(d.ShortTracks, rng); d.Aggression = Bump(d.Aggression, rng, 3); d.DirtCourses = Bump(d.DirtCourses, rng);
                    break;
                case Archetype.Smooth:
                    d.TyreManagement = Bump(d.TyreManagement, rng, 3); d.FuelManagement = Bump(d.FuelManagement, rng, 3); d.Consistency = Bump(d.Consistency, rng);
                    break;
                case Archetype.RoadRacer:
                    d.RoadCourses = Bump(d.RoadCourses, rng, 3); d.OpenWheel = Bump(d.OpenWheel, rng, 3); d.Adaptability = Bump(d.Adaptability, rng);
                    break;
                case Archetype.Ovalist:
                    d.Speedways = Bump(d.Speedways, rng, 3); d.Superspeedways = Bump(d.Superspeedways, rng, 3); d.Awareness = Bump(d.Awareness, rng);
                    break;
                case Archetype.Qualifier:
                    d.Qualifying = Bump(d.Qualifying, rng, 4); d.Aggression = Bump(d.Aggression, rng);
                    break;
                case Archetype.AllRounder:
                    d.Consistency = Bump(d.Consistency, rng); d.Adaptability = Bump(d.Adaptability, rng); d.Awareness = Bump(d.Awareness, rng);
                    break;
            }
        }

        static int Base(Random rng) => rng.Next(6, 13);           // 6-12
        static int Bump(int stat, Random rng, int extra = 2) => Clamp(stat + rng.Next(2, 4) + extra, 0, Driver.StatMax);

        // Nudge every performance stat toward the driver's CurrentAbility so a low-rated rookie reads as low across the
        // board (keeping the archetype's relative shape). Uses the same helper the aging system leans on.
        static void ScaleStatsToAbility(Driver d) => DriverProgression.PullPerformanceStatsToward(d, d.CurrentAbility);

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
