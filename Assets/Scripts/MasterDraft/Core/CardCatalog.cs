using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // A driver as the card factory needs it — filled by the runtime from Draftmaster.Data.Driver rows
    // (DatabaseManager, seeded by DummyDrivers.Build). Kept as a plain copy so Core never touches sqlite.
    [Serializable]
    public class DriverSource
    {
        public int id;
        public string firstName, lastName;
        public int carNumber;
        public string team, manufacturer;
        public int currentAbility;                                            // 0-100
        public int shortTracks, speedways, superspeedways, roadCourses, dirtCourses;  // 0-20
        public int qualifying, consistency, aggression, awareness, tyreManagement;    // 0-20
    }

    // A crew chief as the factory needs it — from Staff rows with StaffRole.CrewChief.
    [Serializable]
    public class CrewChiefSource
    {
        public string name;
        public int rating;   // 0-100
    }

    // Builds card definitions out of the game's own data, and holds the pools packs draw from.
    public sealed class CardCatalog
    {
        public readonly List<CardDefinition> All = new();
        readonly Dictionary<(CardType, Rarity), List<CardDefinition>> _pools = new();
        readonly Dictionary<string, CardDefinition> _byId = new();

        // drivers / crewChiefs may be null or empty: generated stand-ins fill in, so a catalogue always has
        // every card type and pack guarantees can always be met.
        public CardCatalog(IEnumerable<DriverSource> drivers, IEnumerable<CrewChiefSource> crewChiefs = null)
        {
            int driverCount = 0;
            if (drivers != null)
                foreach (var d in drivers)
                    if (d != null) { Add(CardFactory.Driver(d)); driverCount++; }
            if (driverCount == 0)
                foreach (var d in CardFactory.GeneratedDrivers()) Add(CardFactory.Driver(d));

            foreach (var c in CardFactory.Cars()) Add(c);

            int chiefCount = 0;
            if (crewChiefs != null)
                foreach (var c in crewChiefs)
                    if (c != null && !string.IsNullOrWhiteSpace(c.name))
                    {
                        var def = CardFactory.CrewChief(c);
                        if (_byId.ContainsKey(def.id)) continue;   // two teams' chiefs can share a seeded name
                        Add(def);
                        chiefCount++;
                    }
            if (chiefCount == 0)
                foreach (var c in CardFactory.GeneratedCrewChiefs()) Add(CardFactory.CrewChief(c));

            foreach (var spec in ActionCatalog.All) Add(ActionCatalog.Definition(spec));
        }

        void Add(CardDefinition def)
        {
            if (_byId.ContainsKey(def.id)) return;
            _byId[def.id] = def;
            All.Add(def);
            // Unplayable actions are catalogued but never drop.
            if (def.type == CardType.Action && !ActionCatalog.IsPlayable(def.effect)) return;
            var key = (def.type, def.rarity);
            if (!_pools.TryGetValue(key, out var pool)) _pools[key] = pool = new List<CardDefinition>();
            pool.Add(def);
        }

        public CardDefinition Find(string id) => id != null && _byId.TryGetValue(id, out var d) ? d : null;

        public int Count(CardType type, Rarity rarity) =>
            _pools.TryGetValue((type, rarity), out var p) ? p.Count : 0;

        public bool HasAny(CardType type)
        {
            for (var r = Rarity.Common; r <= Rarity.Legendary; r++) if (Count(type, r) > 0) return true;
            return false;
        }

        // A random droppable card of this type, at this rarity if the pool has one; otherwise the nearest
        // rarity below, then above. Returns a fresh copy, never the catalogue's own object.
        public CardDefinition Pick(CardType type, Rarity rarity, DraftRng rng)
        {
            for (int step = 0; step <= 3; step++)
            {
                foreach (int r in new[] { (int)rarity - step, (int)rarity + step })
                {
                    if (r < 0 || r > 3) continue;
                    if (_pools.TryGetValue((type, (Rarity)r), out var pool) && pool.Count > 0)
                        return rng.Pick(pool).Clone();
                }
            }
            return null;
        }
    }

    public static class CardFactory
    {
        // OVR bands shared by every card type. The Cup field sits roughly 70-96.
        public const int LegendaryFrom = 90;
        public const int EpicFrom = 82;
        public const int RareFrom = 72;

        public static Rarity RarityFor(int overall) =>
            overall >= LegendaryFrom ? Rarity.Legendary
            : overall >= EpicFrom ? Rarity.Epic
            : overall >= RareFrom ? Rarity.Rare
            : Rarity.Common;

        public static CardDefinition Driver(DriverSource d)
        {
            int ovr = Clamp(d.currentAbility, 1, 99);
            string name = $"{d.firstName} {d.lastName}".Trim();
            if (name.Length == 0) name = d.carNumber > 0 ? $"Driver #{d.carNumber}" : "Unknown Driver";
            return new CardDefinition
            {
                id = "DRV-" + d.id,
                type = CardType.Driver,
                rarity = RarityFor(ovr),
                name = name,
                overall = ovr,
                artKey = "driver_" + d.id,
                carNumber = d.carNumber,
                team = d.team ?? "",
                manufacturer = d.manufacturer ?? "",
                shortTracks = Clamp(d.shortTracks, 0, 20),
                speedways = Clamp(d.speedways, 0, 20),
                superspeedways = Clamp(d.superspeedways, 0, 20),
                roadCourses = Clamp(d.roadCourses, 0, 20),
                dirtCourses = Clamp(d.dirtCourses, 0, 20),
                qualifying = Clamp(d.qualifying, 0, 20),
                consistency = Clamp(d.consistency, 0, 20),
                aggression = Clamp(d.aggression, 0, 20),
                awareness = Clamp(d.awareness, 0, 20),
                tyreManagement = Clamp(d.tyreManagement, 0, 20),
            };
        }

        // Each make leans one way: Chevrolet power, Ford draft, Toyota handling. Four trims per make, one per
        // rarity.
        static readonly (string code, string model, int power, int handling, int draft)[] Makes =
        {
            ("CHV", "Chevrolet Camaro", +4, -1, -1),
            ("FRD", "Ford Mustang", -1, -1, +4),
            ("TYT", "Toyota Camry", -1, +4, -1),
        };

        static readonly (string trim, Rarity rarity, int baseStat, int reliability)[] Trims =
        {
            ("Club", Rarity.Common, 64, 70),
            ("Pro", Rarity.Rare, 74, 78),
            ("Elite", Rarity.Epic, 83, 85),
            ("Champion", Rarity.Legendary, 91, 92),
        };

        public static IEnumerable<CardDefinition> Cars()
        {
            foreach (var m in Makes)
                for (int t = 0; t < Trims.Length; t++)
                {
                    var trim = Trims[t];
                    int power = Clamp(trim.baseStat + m.power, 0, 99);
                    int handling = Clamp(trim.baseStat + m.handling, 0, 99);
                    int draft = Clamp(trim.baseStat + m.draft, 0, 99);
                    yield return new CardDefinition
                    {
                        id = $"CAR-{m.code}-{t}",
                        type = CardType.Car,
                        rarity = trim.rarity,
                        name = $"{m.model} {trim.trim}",
                        manufacturer = m.code,
                        power = power,
                        handling = handling,
                        draft = draft,
                        reliability = trim.reliability,
                        overall = (power + handling + draft + trim.reliability + 2) / 4,
                        artKey = $"car_{m.code}_{t}",
                    };
                }
        }

        public static CardDefinition CrewChief(CrewChiefSource c)
        {
            int rating = Clamp(c.rating, 1, 99);
            long h = DraftRng.Hash(c.name);
            var rng = new DraftRng(h);
            int pit = Clamp(rating + rng.Range(-6, 7), 1, 99);
            int strat = Clamp(rating + rng.Range(-6, 7), 1, 99);
            int setup = Clamp(rating + rng.Range(-6, 7), 1, 99);
            int ovr = (pit + strat + setup + 1) / 3;
            string slug = c.name.Trim().ToLowerInvariant().Replace(' ', '-');
            return new CardDefinition
            {
                id = "CC-" + slug,
                type = CardType.CrewChief,
                rarity = RarityFor(ovr),
                name = c.name.Trim(),
                overall = ovr,
                pitSpeed = pit,
                strategy = strat,
                setup = setup,
                artKey = "chief_" + slug,
            };
        }

        static readonly string[] ChiefNames =
        {
            "Rudy Fairbanks", "Lou Pettis", "Dot Calloway", "Earl Maddox", "Vic Stanton", "June Harlan",
            "Bo Tillman", "Nate Ruffin", "Gus Ostrander", "Ida Klein", "Walt Brennan", "Skip Lacey",
            "Hal Pruitt", "Mo Delgado", "Abe Corwin", "Tess Whitlock",
        };

        // Stand-in chiefs when the Staff table has none: ratings spread evenly from 60 to 94.
        public static IEnumerable<CrewChiefSource> GeneratedCrewChiefs()
        {
            for (int i = 0; i < ChiefNames.Length; i++)
                yield return new CrewChiefSource { name = ChiefNames[i], rating = 60 + i * 34 / (ChiefNames.Length - 1) };
        }

        static readonly string[] DriverFirst = { "Cody", "Rex", "Dana", "Tyler", "Jesse", "Lane", "Brock", "Kim", "Reid", "Shane", "Mack", "Toni" };
        static readonly string[] DriverLast = { "Waller", "Hobbs", "Garrity", "Pace", "Lowry", "Struthers", "Kemp", "Ochoa", "Varney", "Delacroix", "Bishop", "Yates" };
        static readonly string[] MakeCodes = { "CHV", "FRD", "TYT" };

        // Stand-in drivers when no roster is available (database not ready, or tests). Twelve drivers, one
        // speciality each, ratings 62-94 so every rarity has someone.
        public static IEnumerable<DriverSource> GeneratedDrivers()
        {
            for (int i = 0; i < 12; i++)
            {
                int ovr = 62 + i * 32 / 11;
                int b = ovr / 6;   // ~10-15 baseline aptitude
                var d = new DriverSource
                {
                    id = -(i + 1),
                    firstName = DriverFirst[i],
                    lastName = DriverLast[i],
                    carNumber = 70 + i,
                    team = "Independent",
                    manufacturer = MakeCodes[i % 3],
                    currentAbility = ovr,
                    shortTracks = b, speedways = b, superspeedways = b, roadCourses = b, dirtCourses = b - 2,
                    qualifying = b, consistency = b, aggression = 8 + i % 9, awareness = b, tyreManagement = b,
                };
                switch (i % 5)
                {
                    case 0: d.shortTracks = Clamp(b + 5, 0, 20); break;
                    case 1: d.speedways = Clamp(b + 5, 0, 20); break;
                    case 2: d.superspeedways = Clamp(b + 5, 0, 20); break;
                    case 3: d.roadCourses = Clamp(b + 5, 0, 20); break;
                    default: d.dirtCourses = Clamp(b + 5, 0, 20); break;
                }
                yield return d;
            }
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
