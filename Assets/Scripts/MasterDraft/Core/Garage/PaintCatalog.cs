using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // ---- Paints.json --------------------------------------------------------------------------------------------
    // The Garage's Cars tab: paint schemes to collect, each named after its (fictional) primary sponsor. The master
    // copy, edited by hand (Assets/Resources/MasterDraft/Paints.json). The first set is every Cup '26 blank paint in
    // Resources (cup26livery9blank.png, cup26livery21blankalt1.png...): a blank is the car's scheme with the real
    // sponsor art taken off. Rarity is the DM2 star rating of the car the paint came from — the same number in
    // DM2's newest Cup carset that has it (cup24, else cup23, cup22, cup20); rarityFrom records which. Public fields
    // for JsonUtility.
    [Serializable]
    public class PaintDef
    {
        public string id;          // "cup26_9", an alternate "cup26_21_alt1"
        public string carset;      // "cup26"
        public int number;         // the number it was painted for
        public int alt;            // 0 = the car's main scheme, 1+ = its alternate
        public string livery;      // Resources sprite, "cup26livery9blank"
        public string sponsor;     // the primary sponsor it's named after; "" until it's been named
        public int rarity;         // 1-4 stars (Common, Rare, Epic, Legend)
        public string rarityFrom;  // the DM2 car the rarity came from, "cup24_9"; "" = none had the number (Common)

        public bool Named => !string.IsNullOrEmpty(sponsor);

        // The card's name: the sponsor, else what it is until it's named ("No. 9 paint", "No. 21 alt paint").
        public string DisplayName => Named ? sponsor : $"No. {number}{(alt > 0 ? " alt" : "")} paint";

        public override string ToString() => $"{id} ({DisplayName}, {rarity}*)";
    }

    [Serializable]
    public class PaintsFile
    {
        public int version;
        public string source;
        public List<PaintDef> paints = new List<PaintDef>();
    }

    public class PaintCatalog
    {
        public const string ResourcePath = "MasterDraft/Paints";   // Resources.Load<TextAsset>(ResourcePath)

        readonly List<PaintDef> paints = new List<PaintDef>();
        readonly Dictionary<string, PaintDef> byId = new Dictionary<string, PaintDef>(StringComparer.Ordinal);

        public int Version { get; }
        public IReadOnlyList<PaintDef> Paints => paints;
        public int Count => paints.Count;

        // Rows with no id or livery are dropped; a repeated id keeps its first row. Rarity is clamped to 1-4.
        public PaintCatalog(PaintsFile file)
        {
            if (file?.paints == null) return;
            Version = file.version;
            foreach (var p in file.paints)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || string.IsNullOrEmpty(p.livery) || byId.ContainsKey(p.id)) continue;
                p.rarity = Math.Max(CarCatalog.MinRarity, Math.Min(CarCatalog.MaxRarity, p.rarity));
                p.sponsor ??= "";
                p.rarityFrom ??= "";
                paints.Add(p);
                byId[p.id] = p;
            }
        }

        public static PaintCatalog FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (ProfileJson.Read == null) throw new InvalidOperationException("ProfileJson.Read isn't installed");
            return new PaintCatalog((PaintsFile)ProfileJson.Read(json, typeof(PaintsFile)));
        }

        public PaintDef Get(string id) => id != null && byId.TryGetValue(id, out var p) ? p : null;
        public bool Contains(string id) => Get(id) != null;
    }

    // The Garage's tabs (MasterDraftUI.Collection.cs). Builds holds one build — the highest rated driver, car and
    // crew chief card in the collection (TeamRules.BestBuild); Cars is the paint collection; Drivers is every
    // driver card in the collection; Crew Chiefs is every crew chief card (it replaced a Teams tab, 2026-11-21);
    // Numbers is still to come.
    public enum GarageTab
    {
        Builds = 0,
        Cars = 1,
        Drivers = 2,
        CrewChiefs = 3,
        Numbers = 4,
    }

    // The Cars tab's rules: which paints show and in what order, and how many are collected. Engine-free.
    public static class PaintCollection
    {
        static readonly string[] TabNames = { "Builds", "Cars", "Drivers", "Crew Chiefs", "Numbers" };

        public static string TabName(GarageTab tab) => TabNames[Math.Max(0, Math.Min(TabNames.Length - 1, (int)tab))];

        public static int TabCount => TabNames.Length;

        // Paints in the catalogue the player holds: the header's "n / N collected".
        public static int Collected(PaintCatalog paints, IList<string> owned)
        {
            if (paints == null || owned == null) return 0;
            int n = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in owned)
                if (id != null && paints.Contains(id) && seen.Add(id)) n++;
            return n;
        }

        public static bool Owns(IList<string> owned, string id) => owned != null && id != null && owned.Contains(id);

        // Every paint passing the rarity filter (0 = all), owned first. Rarity and Class sort: rarest first, then
        // number; Number sort: catalogue order. (Paints have no class: Class sorts like Rarity.)
        public static List<PaintDef> List(PaintCatalog paints, IList<string> owned, int rarity, CollectionSort sort)
        {
            var have = new List<PaintDef>();
            var locked = new List<PaintDef>();
            if (paints == null) return have;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < paints.Paints.Count; i++) index[paints.Paints[i].id] = i;
            foreach (var p in paints.Paints)
            {
                if (rarity > 0 && p.rarity != rarity) continue;
                (Owns(owned, p.id) ? have : locked).Add(p);
            }
            Comparison<PaintDef> order = sort == CollectionSort.Number
                ? (a, b) => index[a.id].CompareTo(index[b.id])
                : (a, b) =>
                {
                    int r = b.rarity.CompareTo(a.rarity);
                    if (r != 0) return r;
                    int n = a.number.CompareTo(b.number);
                    return n != 0 ? n : a.alt.CompareTo(b.alt);
                };
            have.Sort(order);
            locked.Sort(order);
            have.AddRange(locked);
            return have;
        }

        // Adds a paint to the collection; false if the catalogue hasn't got it or it's already held.
        public static bool Award(IList<string> owned, PaintCatalog paints, string id)
        {
            if (owned == null || paints == null || !paints.Contains(id) || owned.Contains(id)) return false;
            owned.Add(id);
            return true;
        }
    }
}
