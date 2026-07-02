using System;
using System.Linq;
using SQLite;

namespace Draftmaster.Data
{
    // Offseason turnover for the driver world. Call Run() once between seasons:
    //   1. every active driver ages a year (DriverProgression.Advance) — young ones improve, old ones fade,
    //   2. drivers who decide to hang it up are retired (kept in the table for history),
    //   3. a fresh new-gen intake (NewGenGenerator) tops the active pool back up to TargetPoolSize.
    // Persists all changes to the Drivers table.
    public static class SeasonRollover
    {
        // How many *active* (non-retired) drivers to keep in the world. Sized above total series seats so there's a
        // free-agent bench and a development ladder below the top series.
        public const int TargetPoolSize = 260;

        public struct Report
        {
            public int NewSeason;
            public int Aged;
            public int Retired;
            public int Debuted;
        }

        // Advance the whole driver world from (newSeason-1) into newSeason. Pass an rng for reproducible intakes/aging;
        // omit for a fresh seeded one.
        public static Report Run(SQLiteConnection db, int newSeason, Random rng = null)
        {
            rng ??= new Random();
            var report = new Report { NewSeason = newSeason };

            var active = db.Table<Driver>().Where(d => !d.Retired).ToList();

            db.RunInTransaction(() =>
            {
                foreach (var d in active)
                {
                    DriverProgression.Advance(d, rng);
                    report.Aged++;

                    if (DriverProgression.ShouldRetire(d, rng))
                    {
                        d.Retired = true;
                        d.RetiredSeason = newSeason;
                        report.Retired++;
                    }
                    db.Update(d);
                }

                int remaining = active.Count - report.Retired;
                int needed = Math.Max(0, TargetPoolSize - remaining);
                if (needed > 0)
                {
                    db.InsertAll(NewGenGenerator.Intake(newSeason, needed, rng));
                    report.Debuted = needed;
                }
            });

            return report;
        }

        // Convenience for the currently-loaded save: rolls the world forward and bumps the Career/Series calendars.
        public static Report RunForActiveCareer(SQLiteConnection db, Random rng = null)
        {
            var career = db.Table<Career>().FirstOrDefault(c => c.Active);
            int newSeason = (career?.Season ?? 2025) + 1;

            var report = Run(db, newSeason, rng);

            if (career != null)
            {
                career.Season = newSeason;
                db.Update(career);
            }
            foreach (var s in db.Table<Series>().ToList())
            {
                s.CurrentSeason = newSeason;
                db.Update(s);
            }
            return report;
        }
    }
}
