using System.Collections.Generic;

namespace Draftmaster.Garage
{
    // Placeholder team / car state backing the garage role stations. In-memory stubs for now — later these
    // read from the SQLite DB / save game (see [[db-schema-migration]]). Kept static so every station reads
    // the same shared team snapshot while the scene is scaffolding.
    public static class TeamGarageData
    {
        // ---- Chassis (Fabricator) ----
        public static string CurrentChassisName = "Gen-3 #22";
        public static int CurrentChassisCondition = 88;   // 0-100, higher = fresher
        public static string NewBuildName = "Gen-4 Prototype";
        public static int NewBuildProgress = 42;          // 0-100 fabrication progress
        public static int NewBuildEtaRaces = 4;

        // ---- Engine (Engine Mechanic) ----
        public static string EngineSpec = "V8 358ci — Spec B";
        public static int EngineWear = 27;                // 0-100, higher = more worn
        public static int EngineDevelopment = 61;         // 0-100 development level
        public static int EnginePeakHp = 742;
        public static int RacesSinceRebuild = 3;

        // ---- Sponsorship (Sponsorship Manager) ----
        public class Sponsor
        {
            public string Name;
            public int PerRace;            // $ paid per race
            public string Demand;          // performance clause they expect
            public bool Signed;            // on the car now, vs a prospect to land
        }

        public static readonly List<Sponsor> Sponsors = new()
        {
            new Sponsor { Name = "Duffety Oil",    PerRace = 45000, Demand = "Finish top 15",  Signed = true },
            new Sponsor { Name = "Apex Tools",     PerRace = 28000, Demand = "Lead 1+ lap",    Signed = true },
            new Sponsor { Name = "Redline Energy", PerRace = 60000, Demand = "Top 10 average", Signed = false },
            new Sponsor { Name = "Coastline Bank", PerRace = 90000, Demand = "Top 5 finish",   Signed = false },
        };
    }
}
