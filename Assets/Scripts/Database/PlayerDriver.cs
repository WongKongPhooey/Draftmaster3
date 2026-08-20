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
