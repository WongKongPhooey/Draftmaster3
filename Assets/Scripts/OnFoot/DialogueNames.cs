using System;
using System.Linq;
using Draftmaster.Chatter;
using Draftmaster.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

// Who the paddock is talking about: the player, and the player's crew chief.
//
// Draftmaster.Chatter.SpeakerIdentity owns the token syntax and is deliberately pure. This is the other
// half — the part that knows about PlayerPrefs, the roster and the SQLite Staff table — and it installs
// itself into that class's two hooks as the game starts. Anything that speaks a line (NPCInteractable,
// the ambient crowd, a weekend venue host) therefore gets real names without knowing where they came from.
//
// The player is whoever PlayerDriver says they are, which is the same answer the garage sheet gives.
//
// The crew chief had no single answer before this: DummyStaff seeds one per team, but the Staff table is
// keyed on the fictional Teams rows while a driver's TeamName is the real-world one, so the two do not
// reliably meet. The order below is therefore "an explicit answer, then a database answer, then a stable
// invented one" — and the invented one is derived from the team name, so the same career gets the same
// crew chief every session without anything having to be saved.
public static class DialogueNames
{
    // Set this and it wins outright. Here for a career system that hires and fires crew chiefs; nothing
    // writes it today.
    public const string CrewChiefKey = "career.crewchief";

    // What a marker or a venue cast entry calls the chief before we know their name. Matched
    // case-insensitively, so the weekend's shouty "CREW CHIEF" resolves as well as the paddock's.
    static readonly string[] GenericChiefLabels = { "Crew Chief", "Chief", "The Crew Chief" };

    static string _player, _chief;
    static bool _resolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SpeakerIdentity.PlayerNameProvider = () => PlayerName;
        SpeakerIdentity.CrewChiefNameProvider = () => CrewChiefName;

        // The cast is rebuilt per scene and a career name can be set between them, so start each scene by
        // asking again rather than carrying an answer across a load.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Refresh();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // Forget both names. Call after a career name is chosen or a crew chief changes hands.
    public static void Refresh()
    {
        _resolved = false;
        _player = _chief = null;
    }

    // The player's full name, "" when nothing in the save or the roster names them. Resolved once per scene
    // — a roster scan is far too much work to do per spoken line — and retried while it comes back empty,
    // because the position tracker and the database both turn up a few frames after the scene does.
    public static string PlayerName
    {
        get { Resolve(); return _player; }
    }

    // The player's crew chief, full name. Never empty in practice: the last fallback invents one.
    public static string CrewChiefName
    {
        get { Resolve(); return _chief; }
    }

    // What to actually put over a speaker's head, given the name a marker or a cast list was authored with.
    // Tokens are filled, and a generic "Crew Chief" label becomes whoever that is this career — which is the
    // whole point of the exercise: you talk to Ron Doyle, not to a job title. An authored label that was
    // shouted (the weekend venues are all upper case) keeps its voice.
    public static string ResolveSpeaker(string authored)
    {
        if (string.IsNullOrWhiteSpace(authored)) return authored;

        string filled = SpeakerIdentity.Fill(authored);
        if (!IsGenericChiefLabel(filled)) return filled;

        string chief = CrewChiefName;
        if (string.IsNullOrEmpty(chief)) return filled;
        return IsShouted(authored) ? chief.ToUpperInvariant() : chief;
    }

    public static bool IsGenericChiefLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return false;
        string trimmed = label.Trim();
        foreach (var generic in GenericChiefLabels)
            if (string.Equals(trimmed, generic, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // All-caps with at least one letter in it — the house style for a weekend venue host.
    static bool IsShouted(string label)
    {
        bool sawLetter = false;
        foreach (char c in label)
        {
            if (!char.IsLetter(c)) continue;
            sawLetter = true;
            if (char.IsLower(c)) return false;
        }
        return sawLetter;
    }

    // ------------------------------------------------------------------ resolution

    static void Resolve()
    {
        if (_resolved && !string.IsNullOrEmpty(_player) && !string.IsNullOrEmpty(_chief)) return;

        _player = ResolvePlayerName();
        _chief = ResolveCrewChiefName();
        _resolved = true;
    }

    static string ResolvePlayerName()
    {
        // During a race the tracker holds the identity the timing tower is using, which is the one the
        // player answers to even after a mid-race car swap.
        var tracker = RacePositionTracker.Instance;
        if (tracker != null && IsRealName(tracker.playerName)) return tracker.playerName.Trim();

        try
        {
            string name = PlayerDriver.DisplayName(PlayerDriver.Row());
            return IsRealName(name) ? name.Trim() : "";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DialogueNames: could not resolve the player's name — {e.Message}");
            return "";
        }
    }

    static bool IsRealName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim() != TeamSwitchController.kPlaceholderName;

    static string ResolveCrewChiefName()
    {
        string saved = PlayerPrefs.GetString(CrewChiefKey, "");
        if (!string.IsNullOrWhiteSpace(saved)) return saved.Trim();

        string team = PlayerTeamName();

        string fromTable = ChiefFromStaffTable(team);
        if (!string.IsNullOrEmpty(fromTable)) return fromTable;

        // Nothing in the database matches this team, which is the ordinary case: invent one, but invent the
        // same one forever by seeding off the team's name.
        return DummyStaff.NameFor(StableSeed(team));
    }

    static string PlayerTeamName()
    {
        try
        {
            var row = PlayerDriver.Row();
            return row != null && !string.IsNullOrWhiteSpace(row.TeamName) ? row.TeamName.Trim() : "";
        }
        catch { return ""; }
    }

    // The crew chief of the Teams row whose name matches, when the database is open and it does.
    static string ChiefFromStaffTable(string teamName)
    {
        if (string.IsNullOrEmpty(teamName)) return "";

        var dbm = DatabaseManager.Instance;
        if (dbm == null || !dbm.IsReady) return "";

        try
        {
            string lowered = teamName.ToLowerInvariant();
            var team = dbm.Connection.Table<Team>().FirstOrDefault(t => t.Name.ToLower() == lowered);
            if (team == null) return "";

            var chief = dbm.Connection.Table<Staff>()
                           .FirstOrDefault(s => s.TeamId == team.Id && s.Role == StaffRole.CrewChief && s.Active);
            return chief != null && !string.IsNullOrWhiteSpace(chief.Name) ? chief.Name.Trim() : "";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DialogueNames: crew chief lookup failed — {e.Message}");
            return "";
        }
    }

    // FNV-1a, so a team name always lands on the same crew chief and two similar names do not land on
    // neighbouring ones.
    static int StableSeed(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (text != null)
                foreach (char c in text) { hash ^= c; hash *= 16777619u; }
            return (int)(hash & 0x7fffffff);
        }
    }
}
