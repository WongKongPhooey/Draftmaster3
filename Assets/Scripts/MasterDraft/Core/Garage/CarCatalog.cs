using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // One collectible car: a Draftmaster 2 carset (series prefix, e.g. "cup22") and a number in it. Generated
    // into Resources/MasterDraft/Cars.json by Tools/masterdraft_export.py from Assets/Scripts/DriverNames.cs;
    // edit the C# source (or the script) and re-run it rather than hand-editing the JSON.
    [Serializable]
    public class CarDef
    {
        public string id;            // "{carset}_{number}", e.g. "cup22_26"
        public string carset;        // series prefix, "cup22"
        public int number;
        public string driver;        // DM2 short name, "Ky.Busch"
        public string team;          // three-letter code, "JGR"
        public string manufacturer;  // "CHV" / "FRD" / "TYT" / "HON" / "PNT" / "DDG" / "OLD"
        public int rarity;           // stars, 1-4
        public string type;          // one of DriverTypes.All
        public string livery;        // Resources sprite name, "{carset}livery{number}"

        public override string ToString() => $"{id} #{number} {driver} ({team} {manufacturer}, {rarity}*, {type})";
    }

    // A carset as listed in the catalogue: id (prefix), display name ("Cup '22") and how many cars it holds.
    [Serializable]
    public class CarsetDef
    {
        public string id;
        public string name;
        public int cars;
    }

    // The file as JsonUtility sees it: public fields, an object at the root.
    [Serializable]
    public class CarCatalogFile
    {
        public int version;
        public string source;
        public List<CarsetDef> carsets = new List<CarsetDef>();
        public List<CarDef> cars = new List<CarDef>();
    }

    // DM2's driver types (DriverNames.shortenedType). A car's type is one of these, spelled exactly so.
    public static class DriverTypes
    {
        public const string Rookie = "Rookie", Strategist = "Strategist", Closer = "Closer", Intimidator = "Intimidator",
            Blocker = "Blocker", Pusher = "Pusher", Dominator = "Dominator", Legend = "Legend";

        public static readonly string[] All = { Rookie, Strategist, Closer, Intimidator, Blocker, Pusher, Dominator, Legend };

        public static bool IsValid(string type) => Array.IndexOf(All, type) >= 0;
    }

    // Every car Master Draft can award, with the lookups series restrictions, challenge prizes and the garage
    // need. Engine-free: the runtime reads the TextAsset at ResourcePath and hands the text to FromJson, which
    // parses through ProfileJson (JsonUtility in game, System.Text.Json in headless tests).
    public class CarCatalog
    {
        public const string ResourcePath = "MasterDraft/Cars";   // Resources.Load<TextAsset>(ResourcePath)
        public const int MinRarity = 1, MaxRarity = 4;

        static readonly IReadOnlyList<CarDef> None = Array.Empty<CarDef>();

        readonly List<CarDef> cars = new List<CarDef>();
        readonly List<CarsetDef> carsets = new List<CarsetDef>();
        readonly Dictionary<string, CarDef> byId = new Dictionary<string, CarDef>(StringComparer.Ordinal);
        readonly Dictionary<string, List<CarDef>> byCarset = new Dictionary<string, List<CarDef>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<CarDef>> byTeam = new Dictionary<string, List<CarDef>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<CarDef>> byManufacturer = new Dictionary<string, List<CarDef>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<CarDef>> byType = new Dictionary<string, List<CarDef>>(StringComparer.OrdinalIgnoreCase);
        readonly List<CarDef>[] byRarity = new List<CarDef>[MaxRarity + 1];

        public int Version { get; }
        public IReadOnlyList<CarDef> Cars => cars;
        public IReadOnlyList<CarsetDef> Carsets => carsets;
        public int Count => cars.Count;

        // Rows with no id or no carset are dropped; a repeated id keeps its first row. Rarity is clamped to 1-4.
        public CarCatalog(CarCatalogFile file)
        {
            for (int r = 0; r <= MaxRarity; r++) byRarity[r] = new List<CarDef>();
            if (file == null) return;
            Version = file.version;
            if (file.carsets != null)
                foreach (var s in file.carsets)
                    if (s != null && !string.IsNullOrEmpty(s.id)) carsets.Add(s);
            if (file.cars == null) return;
            foreach (var c in file.cars)
            {
                if (c == null || string.IsNullOrEmpty(c.id) || string.IsNullOrEmpty(c.carset)) continue;
                if (byId.ContainsKey(c.id)) continue;
                c.rarity = Math.Max(MinRarity, Math.Min(MaxRarity, c.rarity));
                cars.Add(c);
                byId[c.id] = c;
                Add(byCarset, c.carset, c);
                Add(byTeam, c.team, c);
                Add(byManufacturer, c.manufacturer, c);
                Add(byType, c.type, c);
                byRarity[c.rarity].Add(c);
            }
        }

        // Null for empty or unreadable text. A missing serializer throws (ProfileJson.Reader), so a setup bug
        // is never mistaken for an empty catalogue.
        public static CarCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var read = ProfileJson.Reader;
            CarCatalogFile file;
            try { file = read(json, typeof(CarCatalogFile)) as CarCatalogFile; }
            catch (Exception) { return null; }
            return file == null ? null : new CarCatalog(file);
        }

        public static string IdFor(string carset, int number) => carset + "_" + number;

        public CarDef Get(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;
        public CarDef Get(string carset, int number) => Get(IdFor(carset, number));
        public bool Contains(string id) => Get(id) != null;

        public CarsetDef GetCarset(string id)
        {
            foreach (var s in carsets)
                if (string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public IReadOnlyList<CarDef> InCarset(string carset) => Find(byCarset, carset);
        public IReadOnlyList<CarDef> ByTeam(string team) => Find(byTeam, team);
        public IReadOnlyList<CarDef> ByManufacturer(string manufacturer) => Find(byManufacturer, manufacturer);
        public IReadOnlyList<CarDef> ByType(string driverType) => Find(byType, driverType);
        public IReadOnlyList<CarDef> ByRarity(int rarity) => rarity >= MinRarity && rarity <= MaxRarity ? byRarity[rarity] : None;

        // Every car of at least this many stars (DM2's "min rarity" restriction).
        public List<CarDef> AtLeastRarity(int rarity)
        {
            var list = new List<CarDef>();
            foreach (var c in cars) if (c.rarity >= rarity) list.Add(c);
            return list;
        }

        // Every car driven by this DM2 driver name, across carsets (a challenge prize names a driver: "Johnson").
        public List<CarDef> ByDriver(string driver)
        {
            var list = new List<CarDef>();
            foreach (var c in cars)
                if (string.Equals(c.driver, driver, StringComparison.OrdinalIgnoreCase)) list.Add(c);
            return list;
        }

        public IEnumerable<string> Teams => byTeam.Keys;
        public IEnumerable<string> Manufacturers => byManufacturer.Keys;

        static void Add(Dictionary<string, List<CarDef>> index, string key, CarDef c)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<CarDef>();
            list.Add(c);
        }

        static IReadOnlyList<CarDef> Find(Dictionary<string, List<CarDef>> index, string key) =>
            key != null && index.TryGetValue(key, out var list) ? list : None;
    }
}
