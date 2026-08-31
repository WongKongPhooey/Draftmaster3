using System.Collections.Generic;
using UnityEngine;

// Every car's two colours, in one asset: Resources/Cars/CarColours.
//
// A car's paint is a livery sprite, and until now nothing in the game could ask "what colour is the 24
// car?" — which anything built AROUND a car needs: the pit box stand over its box, a timing tower row, a
// team banner, a mini-map dot. So the answer lives here, once, and everything reads it.
//
// Rows are matched most specific first:
//
//   1. carset + car number   — this exact paint scheme ("cup26" #24)
//   2. team name             — everything that team runs, whatever it is driving
//   3. carset, number < 0    — a default for a whole carset
//   4. the asset's fallback  — white with a grey trim
//
// The table is SEEDED FROM THE ART rather than typed: Draftmaster > Cars > Build Car Colours From Liveries
// reads each livery sprite and picks its two colours (LiveryPalette). Tick `handAuthored` on a row and the
// seeder leaves it alone, which is how you correct the handful it gets wrong without losing the edit the
// next time somebody repaints the field.
[CreateAssetMenu(fileName = "CarColours", menuName = "Draftmaster/Car Colours")]
public class CarColours : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Carset prefix the livery belongs to, e.g. 'cup26'. Empty = any carset.")]
        public string carset = "";
        [Tooltip("Car number this paint is on. Negative = the whole carset's default.")]
        public int carNumber = -1;
        [Tooltip("Team this belongs to, matched when no carset+number row exists. Empty = not a team row.")]
        public string teamName = "";

        public Color primary = Color.white;
        public Color secondary = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Corrected by hand — the livery seeder will not overwrite this row.")]
        public bool handAuthored = false;
    }

    [Tooltip("Used when nothing matches: an unpainted car.")]
    public Color fallbackPrimary = Color.white;
    public Color fallbackSecondary = new Color(0.55f, 0.55f, 0.6f);

    public List<Entry> entries = new();

    public const string ResourcePath = "Cars/CarColours";

    static CarColours _instance;
    static bool _looked;

    // The asset, or null when the project has none yet. Null is not an error: everything falls back to
    // white, which reads as "nobody has said what colour this car is" rather than as a bug.
    public static CarColours Instance
    {
        get
        {
            if (_instance != null) return _instance;
            if (_looked) return null;
            _looked = true;
            _instance = Resources.Load<CarColours>(ResourcePath);
            return _instance;
        }
    }

    // Editor tools that create or rewrite the asset call this so the next read picks it up.
    public static void Forget() { _instance = null; _looked = false; }

    // The two colours for a car. Always answers — the fallback is a colour too.
    public static void For(string carset, int carNumber, string teamName, out Color primary, out Color secondary)
    {
        var table = Instance;
        if (table == null)
        {
            primary = Color.white;
            secondary = new Color(0.55f, 0.55f, 0.6f);
            return;
        }

        var hit = table.Find(carset, carNumber, teamName);
        primary = hit != null ? hit.primary : table.fallbackPrimary;
        secondary = hit != null ? hit.secondary : table.fallbackSecondary;
    }

    // Convenience for anything holding a car: the label carries carset, number and team.
    public static void For(DriverLabel label, out Color primary, out Color secondary)
    {
        if (label == null) For("", -1, "", out primary, out secondary);
        else For(label.carset, label.carNumber, label.teamName, out primary, out secondary);
    }

    public Entry Find(string carset, int carNumber, string teamName)
    {
        Entry carsetDefault = null, teamRow = null;

        foreach (var e in entries)
        {
            if (e == null) continue;

            bool carsetMatches = string.IsNullOrEmpty(e.carset) ||
                                 string.Equals(e.carset, carset, System.StringComparison.OrdinalIgnoreCase);

            if (carsetMatches && e.carNumber >= 0 && e.carNumber == carNumber) return e;   // exact paint

            if (teamRow == null && !string.IsNullOrEmpty(e.teamName) && !string.IsNullOrEmpty(teamName) &&
                string.Equals(e.teamName, teamName, System.StringComparison.OrdinalIgnoreCase))
                teamRow = e;

            if (carsetDefault == null && e.carNumber < 0 && string.IsNullOrEmpty(e.teamName) && carsetMatches &&
                !string.IsNullOrEmpty(e.carset))
                carsetDefault = e;
        }

        return teamRow ?? carsetDefault;
    }

    // Used by the seeder: the row for exactly this paint, created if it is not there yet.
    public Entry EntryFor(string carset, int carNumber)
    {
        foreach (var e in entries)
            if (e != null && e.carNumber == carNumber &&
                string.Equals(e.carset, carset, System.StringComparison.OrdinalIgnoreCase))
                return e;

        var added = new Entry { carset = carset, carNumber = carNumber };
        entries.Add(added);
        return added;
    }
}
