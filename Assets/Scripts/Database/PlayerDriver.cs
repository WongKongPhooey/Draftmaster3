using Draftmaster.Data;
using UnityEngine;

// Who the player is when there is no race running.
//
// On track the answer comes off the paintwork — TeamSwitchController reads the car's number and
// RosterLookup turns it into the driver who really races it. In a menu there is no car to read, so the
// number is persisted instead (`career.carnumber`) and resolved the same way. Same rule, same row: the
// garage shows the driver the player's number belongs to, not a second made-up person.
//
// A career name (`career.drivername`) overrides the *name* only, exactly as TeamSwitchController.
// EnsurePlayerLabel does: the player takes the seat, keeps their own name over it.
public static class PlayerDriver
{
    public const string NameKey = "career.drivername";
    public const string NumberKey = "career.carnumber";

    // The same name, kept in halves as well as whole.
    //
    // `career.drivername` stays the one thing every reader looks at — the garage sheet, the timing screen,
    // the dialogue tokens — because splitting a full name is a guess and re-joining two halves is not. The
    // two keys below are only there so the options screen can hand back exactly what was typed into its two
    // boxes: "Van Der Berg" is a surname, and a save that only remembers "Josh Van Der Berg" cannot know
    // that. They are a cache of the split, never the truth.
    public const string FirstNameKey = "career.driverfirst";
    public const string LastNameKey = "career.driverlast";

    // The longest either half may be. The name is drawn in bitmap faces into fixed columns (the garage
    // plate, the timing tower), and a name that overruns is cut mid-word rather than wrapped.
    public const int MaxNameHalfLength = 16;

    // The number the demo car wears when a save has never picked one.
    public const int DefaultCarNumber = 8;

    // The persisted number, falling back to the car actually in the scene when there is one (so this
    // answers the same thing during a race as it does in the garage).
    public static int CarNumber
    {
        get
        {
            int saved = PlayerPrefs.GetInt(NumberKey, 0);
            if (saved > 0) return saved;

            int onTrack = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());
            return onTrack > 0 ? onTrack : DefaultCarNumber;
        }
    }

    // The player's career name, or "" when they've never been given one ("You" is the placeholder, not
    // a name, so it doesn't count as set).
    public static string CareerName
    {
        get
        {
            string name = PlayerPrefs.GetString(NameKey, "");
            return string.IsNullOrWhiteSpace(name) || name == TeamSwitchController.kPlaceholderName ? "" : name.Trim();
        }
    }

    // The player's first and last name, "" when they have never been named.
    public static string FirstName { get { SplitCareerName(out var first, out _); return first; } }
    public static string LastName { get { SplitCareerName(out _, out var last); return last; } }

    // How CareerName divides in two. The cached halves are used only when they still add back up to the
    // full name — SINGLE RACE writes a chosen driver's name straight into NameKey and knows nothing about
    // the halves, so anything else would show the previous player's first name against the new full one.
    public static void SplitCareerName(out string first, out string last)
    {
        string full = CareerName;
        string cachedFirst = PlayerPrefs.GetString(FirstNameKey, "").Trim();
        string cachedLast = PlayerPrefs.GetString(LastNameKey, "").Trim();

        if (Join(cachedFirst, cachedLast) == full && full.Length > 0)
        {
            first = cachedFirst;
            last = cachedLast;
            return;
        }

        SplitFullName(full, out first, out last);
    }

    // "Kyle Larson" -> "Kyle" + "Larson"; "Ricky Stenhouse Jr" -> "Ricky" + "Stenhouse Jr". The first word
    // is the first name and everything after it is the surname, which is the only split a single string
    // supports — the halves above exist so a name the player typed does not have to survive this.
    public static void SplitFullName(string full, out string first, out string last)
    {
        first = last = "";
        if (string.IsNullOrWhiteSpace(full)) return;

        string trimmed = full.Trim();
        int space = trimmed.IndexOf(' ');
        if (space < 0) { first = trimmed; return; }

        first = trimmed.Substring(0, space);
        last = trimmed.Substring(space + 1).Trim();
    }

    // Name the player. Writes the halves AND the full name, so every existing reader — which all read the
    // full name — sees the change without knowing this screen exists. Clearing both halves un-names them
    // rather than leaving an empty string behind that reads as a name nobody typed.
    public static void SetCareerName(string first, string last)
    {
        first = CleanNameHalf(first);
        last = CleanNameHalf(last);
        string full = Join(first, last);

        PlayerPrefs.SetString(FirstNameKey, first);
        PlayerPrefs.SetString(LastNameKey, last);
        if (full.Length > 0) PlayerPrefs.SetString(NameKey, full);
        else PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.Save();
    }

    // Trim, collapse runs of whitespace, and cut to the column width. Nothing here rejects a name: a
    // player who wants to be called "X" is called "X".
    public static string CleanNameHalf(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var sb = new System.Text.StringBuilder(value.Length);
        bool lastWasSpace = true;                       // leading whitespace is dropped
        foreach (char c in value.Trim())
        {
            bool space = char.IsWhiteSpace(c);
            if (space && lastWasSpace) continue;
            sb.Append(space ? ' ' : c);
            lastWasSpace = space;
        }

        string cleaned = sb.ToString().Trim();
        return cleaned.Length > MaxNameHalfLength ? cleaned.Substring(0, MaxNameHalfLength).Trim() : cleaned;
    }

    static string Join(string first, string last) => ((first ?? "") + " " + (last ?? "")).Trim();

    // The Drivers row behind the player's ride. Prefers the number (the roster pins one driver per
    // number, and RosterLookup answers from the code roster when the database hasn't opened yet — which
    // is every menu scene in the editor), then falls back to matching a career name against the table.
    public static Driver Row()
    {
        var byNumber = RosterLookup.ByCarNumber(CarNumber);
        if (byNumber != null) return byNumber;

        string career = CareerName;
        if (career.Length == 0) return null;

        var dbm = DatabaseManager.Instance;
        if (dbm == null || !dbm.IsReady) return null;

        try
        {
            foreach (var d in dbm.Connection.Table<Driver>())
            {
                if (d == null) continue;
                string full = ((d.FirstName ?? "") + " " + (d.LastName ?? "")).Trim();
                if (string.Equals(full, career, System.StringComparison.OrdinalIgnoreCase)) return d;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PlayerDriver: driver lookup failed — {e.Message}");
        }
        return null;
    }

    // What to call the player: their career name if they have one, otherwise the name of the driver whose
    // ride they're in, and only then the placeholder.
    public static string DisplayName(Driver row)
    {
        string career = CareerName;
        if (career.Length > 0) return career;

        if (row != null)
        {
            string full = ((row.FirstName ?? "") + " " + (row.LastName ?? "")).Trim();
            if (full.Length > 0) return full;
        }
        return TeamSwitchController.kPlaceholderName;
    }

    // Team names as they're said out loud, which is also all that fits a 158px column: "Hendrick
    // Motorsports" is "Hendrick", "Legacy Motor Club" is "Legacy". Anything still too long after the
    // suffix comes off is cut rather than allowed to wrap (the bitmap faces have no ellipsis glyph).
    const int kMaxTeamChars = 22;

    static readonly string[] kTeamSuffixes =
    {
        " Factory Team", " Racing Team", " Motorsports", " Motorsport", " Motor Club", " Racing",
    };

    public static string ShortTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName)) return "";
        string name = teamName.Trim();

        foreach (var suffix in kTeamSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - suffix.Length).TrimEnd();
                break;
            }
        }

        return name.Length > kMaxTeamChars ? name.Substring(0, kMaxTeamChars).TrimEnd() : name;
    }
}
