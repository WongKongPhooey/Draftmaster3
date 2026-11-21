using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // The team a player takes racing. Everything is a CardInstance uid from the profile's collection.
    [Serializable]
    public class MasterDraftTeam
    {
        public string activeDriver = "";
        public List<string> bench = new();
        public string car = "";
        public string crewChief = "";

        public IEnumerable<string> Drivers()
        {
            if (!string.IsNullOrEmpty(activeDriver)) yield return activeDriver;
            foreach (var b in bench) yield return b;
        }

        public bool Uses(string uid) =>
            !string.IsNullOrEmpty(uid)
            && (uid == activeDriver || uid == car || uid == crewChief || bench.Contains(uid));

        // Drops a uid from every slot (used when a card leaves the collection in a trade).
        public void Remove(string uid)
        {
            if (activeDriver == uid) activeDriver = "";
            if (car == uid) car = "";
            if (crewChief == uid) crewChief = "";
            bench.RemoveAll(x => x == uid);
        }
    }

    // A build's three cards: the highest rated of each type in the collection (TeamRules.BestBuild), or one of the
    // player's builds read off the collection (BuildRules.Resolve). A slot is null while it has no card.
    public sealed class CardBuild
    {
        public CardInstance driver, car, crewChief;

        public bool Complete => driver != null && car != null && crewChief != null;

        // Same weighting as the team's OVR (TeamRules.Rating); 0 until every slot is filled.
        public int Rating => Complete ? TeamRules.Rating(driver.def, car.def, crewChief.def) : 0;
    }

    public static class TeamRules
    {
        public const int MaxDrivers = 5;          // active + bench
        public const int MaxBench = MaxDrivers - 1;

        // Every reason the team can't race, in plain words. Empty = legal.
        public static List<string> Validate(MasterDraftTeam team, MasterDraftProfile profile)
        {
            var problems = new List<string>();
            if (team == null) { problems.Add("No team set up."); return problems; }

            var seen = new HashSet<string>();
            void CheckSlot(string uid, CardType type, string slot)
            {
                if (string.IsNullOrEmpty(uid)) { problems.Add($"Pick a {slot}."); return; }
                var card = profile?.Find(uid);
                if (card == null) { problems.Add($"Your {slot} is not in your collection."); return; }
                if (card.def.type != type) problems.Add($"{card.def.name} is not a {CardText.TypeName(type)} card.");
                if (!seen.Add(uid)) problems.Add($"{card.def.name} is used twice.");
            }

            CheckSlot(team.activeDriver, CardType.Driver, "driver");
            if (team.bench.Count > MaxBench)
                problems.Add($"Bench holds {MaxBench} drivers at most ({team.bench.Count} picked).");
            foreach (var b in team.bench) CheckSlot(b, CardType.Driver, "bench driver");
            CheckSlot(team.car, CardType.Car, "car");
            CheckSlot(team.crewChief, CardType.CrewChief, "crew chief");

            return problems;
        }

        public static bool IsValid(MasterDraftTeam team, MasterDraftProfile profile) => Validate(team, profile).Count == 0;

        // Team OVR: the driver counts for half, the car and the crew chief a quarter each. 0 when a slot is
        // empty.
        public static int Rating(MasterDraftTeam team, MasterDraftProfile profile)
        {
            var d = profile.Find(team.activeDriver);
            var c = profile.Find(team.car);
            var cc = profile.Find(team.crewChief);
            if (d == null || c == null || cc == null) return 0;
            return Rating(d.def, c.def, cc.def);
        }

        public static int Rating(CardDefinition driver, CardDefinition car, CardDefinition chief) =>
            (int)Math.Round(driver.overall * 0.5 + car.overall * 0.25 + chief.overall * 0.25);

        // The highest rated card of each type in the collection (ties: the earlier uid). Rebuilt from the
        // collection every time, so it follows every pack and trade.
        public static CardBuild BestBuild(MasterDraftProfile profile)
        {
            var build = new CardBuild();
            if (profile?.cards == null) return build;
            build.driver = BestOf(profile, CardType.Driver);
            build.car = BestOf(profile, CardType.Car);
            build.crewChief = BestOf(profile, CardType.CrewChief);
            return build;
        }

        public static CardInstance BestOf(MasterDraftProfile profile, CardType type)
        {
            var list = Best(profile, type);
            return list.Count > 0 ? list[0] : null;
        }

        // The team driver with the best aptitude for this kind of track; ties go to the higher OVR. Null when
        // the team has no drivers.
        public static CardInstance BestDriverFor(TrackKind kind, IEnumerable<CardInstance> drivers)
        {
            CardInstance best = null;
            foreach (var d in drivers)
            {
                if (d?.def == null || d.def.type != CardType.Driver) continue;
                if (best == null) { best = d; continue; }
                int a = d.def.Aptitude(kind), b = best.def.Aptitude(kind);
                if (a > b || (a == b && d.def.overall > best.def.overall)) best = d;
            }
            return best;
        }

        public static List<CardInstance> TeamDrivers(MasterDraftTeam team, MasterDraftProfile profile)
        {
            var list = new List<CardInstance>();
            foreach (var uid in team.Drivers())
            {
                var c = profile.Find(uid);
                if (c != null && c.def.type == CardType.Driver) list.Add(c);
            }
            return list;
        }

        // Fills a team from the collection: best drivers, best car, best chief. Used after the Starter Pack so a new player can race at once, and
        // by My Team's AUTO button. Never produces an illegal team out of a collection that allows a legal
        // one.
        public static MasterDraftTeam AutoBuild(MasterDraftProfile profile)
        {
            var team = new MasterDraftTeam();
            var drivers = Best(profile, CardType.Driver);
            if (drivers.Count > 0) team.activeDriver = drivers[0].uid;
            for (int i = 1; i < drivers.Count && team.bench.Count < MaxBench; i++) team.bench.Add(drivers[i].uid);

            var cars = Best(profile, CardType.Car);
            if (cars.Count > 0) team.car = cars[0].uid;
            var chiefs = Best(profile, CardType.CrewChief);
            if (chiefs.Count > 0) team.crewChief = chiefs[0].uid;

            return team;
        }

        static List<CardInstance> Best(MasterDraftProfile profile, CardType type)
        {
            var list = new List<CardInstance>();
            foreach (var c in profile.cards) if (c?.def != null && c.def.type == type) list.Add(c);
            list.Sort((a, b) => b.def.overall != a.def.overall
                ? b.def.overall.CompareTo(a.def.overall)
                : string.CompareOrdinal(a.uid, b.uid));
            return list;
        }
    }
}
