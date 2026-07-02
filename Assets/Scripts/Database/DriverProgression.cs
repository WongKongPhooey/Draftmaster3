using System;

namespace Draftmaster.Data
{
    // The aging / development model. One call to Advance() per driver per offseason ages them a year and moves their
    // CurrentAbility along a growth→peak→decline curve toward PotentialAbility, then drags their skill stats to match.
    //
    //   young  (age < PeakAge):  CurrentAbility climbs toward PotentialAbility, fastest when youngest & furthest away.
    //   peak   (age ~ PeakAge):  plateau.
    //   old    (age > PeakAge):  CurrentAbility declines, faster each year past peak.
    //
    // Racecraft (fuel/tyre/consistency/awareness/adaptability) keeps creeping UP with experience even in decline —
    // that's why grizzled veterans stay dangerous — while raw pace (track aptitudes, qualifying) tracks the ability
    // curve and aggression mellows with age.
    public static class DriverProgression
    {
        // Raw-pace stats: rise and fall with the ability curve.
        static readonly Func<Driver, int>[] PerfGet =
        {
            d => d.ShortTracks, d => d.Speedways, d => d.Superspeedways,
            d => d.RoadCourses, d => d.DirtCourses, d => d.OpenWheel, d => d.Qualifying
        };
        static readonly Action<Driver, int>[] PerfSet =
        {
            (d, v) => d.ShortTracks = v, (d, v) => d.Speedways = v, (d, v) => d.Superspeedways = v,
            (d, v) => d.RoadCourses = v, (d, v) => d.DirtCourses = v, (d, v) => d.OpenWheel = v, (d, v) => d.Qualifying = v
        };

        // Advance one driver by a single season. Mutates the driver in place. Returns the change in CurrentAbility
        // (positive = improved, negative = declined) so callers can flag breakout seasons / fading veterans.
        public static int Advance(Driver d, Random rng)
        {
            EnsureDefaults(d, rng);

            int before = d.CurrentAbility;
            d.Age += 1;

            if (d.Age <= d.PeakAge) GrowToward(d, rng);
            else Decline(d, rng);

            // Drag the raw-pace stats to sit around the new ability (gradual: at most 2 points a year).
            PullPerformanceStatsToward(d, d.CurrentAbility, maxStep: 2);
            AgeCraftAndCommercial(d, rng);

            return d.CurrentAbility - before;
        }

        // Growth: close a fraction of the gap to potential, biggest when young. Asymptotically approaches the ceiling.
        static void GrowToward(Driver d, Random rng)
        {
            int gap = d.PotentialAbility - d.CurrentAbility;
            if (gap <= 0) return;

            float frac =
                d.Age <= 20 ? 0.40f :
                d.Age <= 24 ? 0.28f :
                0.16f;

            int delta = (int)Math.Ceiling(gap * frac) + rng.Next(-1, 2); // ±1 jitter
            d.CurrentAbility = Clamp(d.CurrentAbility + delta, 25, d.PotentialAbility);
        }

        // Decline: ramps up the further past peak. A per-driver "longevity" roll softens it for the lucky ones.
        static void Decline(Driver d, Random rng)
        {
            int yearsPast = d.Age - d.PeakAge;
            float drop = 1f + (yearsPast - 1) * 0.6f + rng.Next(0, 2);
            if (rng.Next(3) == 0) drop *= 0.5f;                 // ~1/3 of years are gentle
            d.CurrentAbility = Clamp(d.CurrentAbility - (int)Math.Round(drop), 20, d.PotentialAbility);
        }

        // Uniformly shift every raw-pace stat toward the average implied by targetAbility, preserving the driver's
        // relative shape (their strong tracks stay strong). maxStep caps the yearly move; NewGenGenerator passes a big
        // step to snap a fresh rookie's stats down to their (low) starting ability in one go.
        public static void PullPerformanceStatsToward(Driver d, int targetAbility, int maxStep = 99)
        {
            float targetAvg = 4f + targetAbility * 0.15f;       // ability 30→8.5, 50→11.5, 100→19

            float sum = 0f;
            for (int i = 0; i < PerfGet.Length; i++) sum += PerfGet[i](d);
            float currentAvg = sum / PerfGet.Length;

            int shift = (int)Math.Round(targetAvg - currentAvg);
            shift = Clamp(shift, -maxStep, maxStep);
            if (shift == 0) return;

            for (int i = 0; i < PerfGet.Length; i++)
                PerfSet[i](d, Clamp(PerfGet[i](d) + shift, 0, Driver.StatMax));
        }

        // Experience-linked stats. Racecraft creeps up with seasons (slowing with age); aggression mellows past peak;
        // commercial standing accrues, faster for the genuinely able.
        static void AgeCraftAndCommercial(Driver d, Random rng)
        {
            // Racecraft — better every year until the mind slows very late in a career.
            if (d.Age < 42)
            {
                if (Roll(rng, d.Age < 32 ? 60 : 40)) d.FuelManagement = Up(d.FuelManagement);
                if (Roll(rng, d.Age < 32 ? 60 : 40)) d.TyreManagement = Up(d.TyreManagement);
                if (Roll(rng, 50)) d.Consistency = Up(d.Consistency);
                if (Roll(rng, 50)) d.Awareness = Up(d.Awareness);
                if (Roll(rng, d.Age < 30 ? 45 : 25)) d.Adaptability = Up(d.Adaptability);
            }

            // Aggression cools with age once past peak.
            if (d.Age > d.PeakAge && rng.Next(2) == 0)
                d.Aggression = Clamp(d.Aggression - 1, 0, Driver.StatMax);

            // Commercial capital: rises while relevant, faster for high-ability names. (Race results feed this too
            // once the sim is wired in — this is the passive drift.)
            int comChance = 25 + d.CurrentAbility / 3;
            if (rng.Next(100) < comChance) d.SponsorAppeal = Clamp(d.SponsorAppeal + 1, 0, Driver.StatMax);
            if (rng.Next(100) < comChance) d.FanSupport = Clamp(d.FanSupport + 1, 0, Driver.StatMax);
            if (rng.Next(100) < comChance / 2) d.Prestige = Clamp(d.Prestige + 1, 0, Driver.StatMax);
        }

        static bool Roll(Random rng, int chancePct) => rng.Next(100) < chancePct;
        static int Up(int stat) => Clamp(stat + 1, 0, Driver.StatMax);

        // Retirement decision for the offseason. Probability climbs with age and spikes if the driver has faded badly.
        // Returns true if the driver should hang it up this offseason.
        public static bool ShouldRetire(Driver d, Random rng)
        {
            if (d.Retired) return true;

            int pct;
            if (d.Age < 34) pct = 0;
            else if (d.Age < 38) pct = 8;
            else if (d.Age < 41) pct = 22;
            else if (d.Age < 44) pct = 45;
            else if (d.Age < 47) pct = 70;
            else pct = 100;                          // hard cap — nobody races past 47 here

            // A washed-up driver (well below their own former ceiling, and past it) is more likely to walk away.
            if (d.Age > d.PeakAge && d.CurrentAbility < 35) pct += 25;

            return rng.Next(100) < pct;
        }

        // Give seed / imported drivers sensible lifecycle defaults on first touch.
        static void EnsureDefaults(Driver d, Random rng)
        {
            if (d.PeakAge <= 0) d.PeakAge = rng.Next(27, 34);
            if (d.PotentialAbility <= 0) d.PotentialAbility = Math.Max(d.CurrentAbility, 50);
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
