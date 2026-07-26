using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Fans;

namespace Draftmaster.Progression
{
    // The player's answer to "what do you want to be when you grow up?", asked once at the start of a
    // career (see CareerPathNPC). The answer is the career's opening premise: it seeds the player's
    // starting career stats and it is the gate other NPCs read to decide which opportunities they offer
    // ("a crew chief only pitches a wrench job to someone who set out to be a mechanic").
    //
    // PlayerPrefs-backed, following the project's persistence convention (PlayerStatsLedger, FanAppeal,
    // track records). Kept dependency-free and in its own assembly so the stat maths is EditMode-testable
    // (Assembly-CSharp can't be referenced by test assemblies) — same reason FanAppeal lives in one.
    public static class CareerPath
    {
        // Persisted as an int, so the numbers are a save format: never renumber an existing member.
        public enum Path
        {
            Unchosen = 0,
            PitCrew = 1,     // "I want to be on a championship winning pit crew"
            Driver = 2,      // "I want to be a championship winning driver"
            TeamOwner = 3,   // "I want to own my own race team"
            Scout = 4,       // "I want to scout the world's best young drivers"
        }

        const string PathKey = "career.path";
        const string AppliedKey = "career.path.applied";
        // PlayerStatsLedger's key prefix. The grants below are written as ordinary ledger counters so
        // quests (StatThreshold) and AppearanceConditions (statKey) can read them with no extra plumbing.
        const string StatPrefix = "stat.";

        // The five career attributes a starting choice moves. Ledger keys, so they're quest-able.
        public const string StatDriving = "career.driving";
        public const string StatPitCraft = "career.pitcraft";
        public const string StatEngineering = "career.engineering";
        public const string StatBusiness = "career.business";
        public const string StatScouting = "career.scouting";

        public static readonly string[] StatKeys =
        {
            StatDriving, StatPitCraft, StatEngineering, StatBusiness, StatScouting,
        };

        // One career attribute and how much the chosen path starts you with.
        public struct StatGrant
        {
            public string key;
            public int value;
            public StatGrant(string key, int value) { this.key = key; this.value = value; }
        }

        // Every path hands out the same total, spread differently — the choice is a shape, not a power level.
        public const int StartingStatBudget = 17;

        // Raised when a path is chosen for the first time. Runtime only (statics reset on domain reload).
        public static event System.Action<Path> Chosen;

        public static Path Current => (Path)PlayerPrefs.GetInt(PathKey, (int)Path.Unchosen);
        public static bool HasChosen => Current != Path.Unchosen;
        // True once the starting stats have actually been paid out, so they can never land twice.
        public static bool StatsApplied => PlayerPrefs.GetInt(AppliedKey, 0) != 0;

        // Commit a choice: persists the path and, the first time only, pays out the starting stats and
        // the fan-appeal nudge. Returns false for a no-op (Unchosen, or a path already chosen).
        public static bool Choose(Path path)
        {
            if (path == Path.Unchosen) return false;
            if (HasChosen) return false;   // a career opens once; re-answering is not a thing

            PlayerPrefs.SetInt(PathKey, (int)path);
            ApplyStartingStats(path);
            PlayerPrefs.Save();

            Chosen?.Invoke(path);
            return true;
        }

        // Starting career stats for a path. Pure — no state read or written — so it can be unit-tested
        // and shown in UI ("this is what you'd start with") before anything is committed.
        public static StatGrant[] StartingStats(Path path)
        {
            switch (path)
            {
                // Over the wall: fastest hands in the lane, knows the car, has never been paid to drive one.
                case Path.PitCrew:
                    return new[]
                    {
                        new StatGrant(StatPitCraft, 8),
                        new StatGrant(StatEngineering, 5),
                        new StatGrant(StatDriving, 2),
                        new StatGrant(StatBusiness, 1),
                        new StatGrant(StatScouting, 1),
                    };

                // Behind the wheel: everything went into seat time.
                case Path.Driver:
                    return new[]
                    {
                        new StatGrant(StatDriving, 9),
                        new StatGrant(StatPitCraft, 2),
                        new StatGrant(StatEngineering, 2),
                        new StatGrant(StatBusiness, 2),
                        new StatGrant(StatScouting, 2),
                    };

                // Running the show: sponsor decks and payroll before setup sheets.
                case Path.TeamOwner:
                    return new[]
                    {
                        new StatGrant(StatBusiness, 8),
                        new StatGrant(StatEngineering, 3),
                        new StatGrant(StatPitCraft, 2),
                        new StatGrant(StatDriving, 2),
                        new StatGrant(StatScouting, 2),
                    };

                // Finding the next one: an eye for talent and the contacts to move it.
                case Path.Scout:
                    return new[]
                    {
                        new StatGrant(StatScouting, 9),
                        new StatGrant(StatBusiness, 3),
                        new StatGrant(StatDriving, 2),
                        new StatGrant(StatEngineering, 2),
                        new StatGrant(StatPitCraft, 1),
                    };

                default:
                    return new StatGrant[0];
            }
        }

        // How the choice colours the player's starting profile with the fans: the kid who wanted to drive
        // starts with a name, the one who wanted to work over the wall doesn't.
        public static float StartingFanAppealBonus(Path path)
        {
            switch (path)
            {
                case Path.Driver: return 10f;
                case Path.TeamOwner: return 2f;
                case Path.Scout: return -2f;
                case Path.PitCrew: return -4f;
                default: return 0f;
            }
        }

        // The player's own words, as offered by the NPC asking the question. Used for the dialogue option
        // list and for echoing the answer back in the player's speech bubble.
        public static string Ambition(Path path)
        {
            switch (path)
            {
                case Path.PitCrew: return "I want to be on a championship winning pit crew";
                case Path.Driver: return "I want to be a championship winning driver";
                case Path.TeamOwner: return "I want to own my own race team";
                case Path.Scout: return "I want to scout the world's best young drivers";
                default: return "I haven't decided yet";
            }
        }

        // Short label for HUD/menus ("Crew", "Driver", ...).
        public static string DisplayName(Path path)
        {
            switch (path)
            {
                case Path.PitCrew: return "Pit Crew";
                case Path.Driver: return "Driver";
                case Path.TeamOwner: return "Team Owner";
                case Path.Scout: return "Talent Scout";
                default: return "Undecided";
            }
        }

        // The four answers, in the order they're offered. Excludes Unchosen.
        public static Path[] Choices => new[] { Path.PitCrew, Path.Driver, Path.TeamOwner, Path.Scout };

        // Gate helper for content that only exists for certain paths (AppearanceConditions.careerPaths,
        // and anything else that wants "only offer this to a would-be team owner"). Empty list = any path,
        // including a save that has never been asked.
        public static bool Allows(IList<Path> allowed)
        {
            if (allowed == null || allowed.Count == 0) return true;
            var current = Current;
            for (int i = 0; i < allowed.Count; i++)
                if (allowed[i] == current) return true;
            return false;
        }

        // Debug/testing: forget the answer AND the stats it paid out, so the opening question can be asked
        // again from a clean slate (see Draftmaster > NPCs > Clear Career Path Choice).
        public static void Reset()
        {
            var path = Current;
            if (StatsApplied)
            {
                foreach (var grant in StartingStats(path))
                    AddStat(grant.key, -grant.value);
                FanAppeal.Add(-StartingFanAppealBonus(path));
            }
            PlayerPrefs.DeleteKey(PathKey);
            PlayerPrefs.DeleteKey(AppliedKey);
            PlayerPrefs.Save();
        }

        // Current value of a career attribute (same store PlayerStatsLedger reads).
        public static int Stat(string key) => PlayerPrefs.GetInt(StatPrefix + key, 0);

        static void ApplyStartingStats(Path path)
        {
            if (StatsApplied) return;
            foreach (var grant in StartingStats(path))
                AddStat(grant.key, grant.value);
            FanAppeal.Add(StartingFanAppealBonus(path));
            PlayerPrefs.SetInt(AppliedKey, 1);
        }

        static void AddStat(string key, int by)
        {
            if (string.IsNullOrEmpty(key) || by == 0) return;
            PlayerPrefs.SetInt(StatPrefix + key, Stat(key) + by);
        }
    }
}
