using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Progression;
using Draftmaster.Weekend;

// Authorable "should this NPC (or cutscene, or scene beat) show up right now?" rule block.
//
// Drop one on any spawner as a serialized field and check IsMet() before building the thing:
//
//     if (appear.IsMet()) BuildTheThing(...);
//
// PlacedNPC carries one of these per editor-placed NPC — that's the main way they're authored now.
//
// Every clause is opt-in: an empty/default block means "always". Clauses are ANDed. Repeat policy is
// the one that needs a save key — everything else reads live state (weekend session, scene, series,
// career stats, quest state, inventory).
//
// IsMet() rolls the dice for `chance`, so call it once at spawn time and cache the answer rather than
// polling it every frame. Once the beat has actually PLAYED, call MarkSeen() — spawning it isn't the
// same as the player seeing it, so an NPC the player walked past can still be there next time.
[System.Serializable]
public class AppearanceConditions
{
    // How often the beat is allowed to happen. Anything other than EveryTime needs a saveKey.
    public enum Repeat
    {
        EveryTime,            // no memory at all
        OncePerPlaySession,   // once per Play Mode run / app launch (runtime only, never persisted)
        OncePerWeekendSession,// once per practice / qualifying / race session of a weekend
        OncePerWeekend,       // once per race weekend (RaceWeekend.WeekendId)
        OncePerTrack,         // once for each track, forever
        OnceEver,             // once per save, forever
    }

    // Optional quest gate. NotCompleted covers "still relevant"; Started covers Active|ReadyToTurnIn.
    public enum QuestRequirement { Ignore, NotStarted, Active, ReadyToTurnIn, Completed, NotCompleted, Started }

    [Tooltip("Master switch. Off = this NPC/beat never appears.")]
    public bool enabled = true;

    [Header("How often")]
    [Tooltip("How often the beat may play. Anything but EveryTime needs a Save Key.")]
    public Repeat repeat = Repeat.EveryTime;
    [Tooltip("Stable id for the repeat memory, e.g. 'rv.door.intro'. Never change it once players have progress. Ignored by EveryTime.")]
    public string saveKey = "";

    [Header("Which session")]
    public bool inPractice = true;
    public bool inQualifying = true;
    public bool inRace = true;

    [Header("Which half-day")]
    // A race weekend is six half-days, and most of the paddock is only there for some of them: the fan
    // fence is Saturday morning, the sponsor's people fly in Sunday, the truck team is gone by Saturday
    // night. All six on (the default) means "any time this weekend" and nothing changes.
    //
    // This is the axis the NPC Director previews on — pick the day and the half, and the scene shows who
    // is there. Keep it in step with ScheduledLines below, which is the same six half-days deciding what
    // somebody SAYS rather than whether they are there at all.
    public bool fridayAM = true;
    public bool fridayPM = true;
    public bool saturdayAM = true;
    public bool saturdayPM = true;
    public bool sundayAM = true;
    public bool sundayPM = true;

    [Header("Where")]
    [Tooltip("Track ids this may appear at (e.g. 'Daytona'). Empty = any track. Case-insensitive. This is the " +
             "one to use — every track now runs in the same scene, so the scene name no longer says where you are.")]
    public string[] tracks;
    [Tooltip("Scene names this may appear in (e.g. 'RaceScene', 'TeamGarage'). Empty = any scene. Case-insensitive. " +
             "A track id still matches here, for beats authored before tracks and scenes were separated.")]
    public string[] scenes;
    [Tooltip("Series this may appear in, matched against PlayerPrefs CurrentSeriesIndex / CurrentSeries / CurrentSeriesName. Empty = any series. Case-insensitive.")]
    public string[] series;

    [Header("Career progress")]
    [Tooltip("PlayerStatsLedger key that must be in range, e.g. 'starts', 'wins', 'starts.chevrolet'. Empty = no stat check.")]
    public string statKey = "";
    [Tooltip("Minimum value of statKey (inclusive).")]
    public int statMin = 0;
    [Tooltip("Maximum value of statKey (inclusive). Leave at int.MaxValue for no upper bound.")]
    public int statMax = int.MaxValue;

    [Header("Career path")]
    [Tooltip("Career paths this beat is for — the answer the player gave the paddock veteran at the start of their career (CareerPathNPC). Empty = any path, including a save that was never asked. This is how an NPC only offers an opportunity to, say, a would-be team owner.")]
    public CareerPath.Path[] careerPaths;

    [Header("Quest / inventory")]
    [Tooltip("Quest id this depends on. Empty = no quest check.")]
    public string questId = "";
    [Tooltip("What state questId must be in.")]
    public QuestRequirement questRequirement = QuestRequirement.Ignore;
    [Tooltip("Inventory item the player must be carrying. Empty = no item check.")]
    public string requiredItemId = "";

    [Header("Chance")]
    [Tooltip("Probability the beat appears when everything else passes. 1 = always. Rolled once, inside IsMet().")]
    [Range(0f, 1f)] public float chance = 1f;

    // Code-side extra clause, for conditions no inspector field covers ("only if the player parked in
    // the pit box", "only if it's raining"). Set by the spawner after deserialization. Null = ignored.
    [System.NonSerialized] public System.Func<bool> ExtraGate;

    // ------------------------------------------------------------------ editor preview
    //
    // The NPC Director asks "who would be here in qualifying, at Martinsville, in the Cup series?" without
    // entering Play Mode. Set Preview and every clause answers against that hypothetical instead of live
    // state; null (the shipping case) reads the real session/track/series as always. Repeat memory and the
    // chance roll are skipped by default too — a preview is about the AUTHORED rule, not about what this
    // particular save has already seen.
    public class PreviewContext
    {
        public RaceWeekend.Session session = RaceWeekend.Session.Practice;
        public WeekendSlot slot = WeekendSlot.FridayAM;
        public string trackId = "";
        public string series = "";
        public bool ignoreSeen = true;
        public bool ignoreChance = true;
    }

    // Editor-only in practice: nothing at runtime ever assigns it. Static so a gizmo, an inspector and the
    // Director window all answer identically without threading a context through every call.
    [System.NonSerialized] public static PreviewContext Preview;

    const string SeenPrefix = "npc.seen.";
    const string RegistryKey = "npc.seen.keys"; // CSV of every key ever written, so a debug menu can clear them

    // OncePerPlaySession lives here only — statics die on domain reload, which is exactly the scope.
    static readonly HashSet<string> _playSessionSeen = new HashSet<string>();

    // Everything ANDed. Rolls `chance`, so call once and cache.
    public bool IsMet() => FirstUnmet() == null;

    // Same test as IsMet(), but says WHICH clause said no — null means "appears". The Director window and
    // the PlacedNPC inspector print this, so an NPC that fails to turn up explains itself instead of
    // needing a clause-by-clause read of the inspector.
    public string FirstUnmet()
    {
        if (!enabled) return "disabled";
        if (!SessionAllowed()) return $"not in {CurrentSession}";
        if (!SlotAllowed()) return $"not on {WeekendSlots.Label(CurrentSlot).ToLowerInvariant()}";
        if (!Matches(tracks, CurrentTrackId)) return $"track is {Blank(CurrentTrackId)}, needs {Join(tracks)}";
        if (!PlaceAllowed()) return $"scene is {SceneManager.GetActiveScene().name}, needs {Join(scenes)}";
        if (!SeriesAllowed()) return $"series not one of {Join(series)}";
        if (!StatAllowed()) return $"stat '{statKey}' is {PlayerStatsLedger.Get(statKey)}, needs {statMin}..{(statMax == int.MaxValue ? "∞" : statMax.ToString())}";
        if (!CareerPath.Allows(careerPaths)) return "career path doesn't match";
        if (!QuestAllowed()) return $"quest '{questId}' not {questRequirement}";
        if (!string.IsNullOrEmpty(requiredItemId) && !PlayerInventory.Has(requiredItemId)) return $"missing item '{requiredItemId}'";
        if (AlreadySeen()) return $"already seen ({repeat})";
        if (ExtraGate != null && !ExtraGate()) return "code gate says no";
        if (chance < 1f && !ChanceAllowed()) return $"lost the {chance:P0} roll";
        return null;
    }

    // One-line human summary of what this block is gated on, for list rows and gizmo labels.
    // "always" when nothing is set.
    public string Summarise()
    {
        var bits = new List<string>();
        if (!enabled) bits.Add("DISABLED");
        if (!(inPractice && inQualifying && inRace))
        {
            var s = new List<string>();
            if (inPractice) s.Add("P");
            if (inQualifying) s.Add("Q");
            if (inRace) s.Add("R");
            bits.Add(s.Count == 0 ? "no session" : string.Join("/", s));
        }
        if (!(fridayAM && fridayPM && saturdayAM && saturdayPM && sundayAM && sundayPM))
        {
            var half = new List<string>();
            if (fridayAM) half.Add("FRI AM");
            if (fridayPM) half.Add("FRI PM");
            if (saturdayAM) half.Add("SAT AM");
            if (saturdayPM) half.Add("SAT PM");
            if (sundayAM) half.Add("SUN AM");
            if (sundayPM) half.Add("SUN PM");
            bits.Add(half.Count == 0 ? "no half-day" : string.Join("/", half));
        }
        if (tracks != null && tracks.Length > 0) bits.Add(Join(tracks));
        if (scenes != null && scenes.Length > 0) bits.Add("scene " + Join(scenes));
        if (series != null && series.Length > 0) bits.Add("series " + Join(series));
        if (!string.IsNullOrEmpty(statKey))
            bits.Add($"{statKey} {statMin}..{(statMax == int.MaxValue ? "∞" : statMax.ToString())}");
        if (careerPaths != null && careerPaths.Length > 0) bits.Add("path " + string.Join("/", careerPaths));
        if (!string.IsNullOrEmpty(questId) && questRequirement != QuestRequirement.Ignore)
            bits.Add($"quest {questId} {questRequirement}");
        if (!string.IsNullOrEmpty(requiredItemId)) bits.Add("holds " + requiredItemId);
        if (repeat != Repeat.EveryTime) bits.Add(repeat.ToString());
        if (chance < 1f) bits.Add($"{chance:P0} chance");
        return bits.Count == 0 ? "always" : string.Join(" · ", bits);
    }

    static string Blank(string s) => string.IsNullOrEmpty(s) ? "(none)" : s;
    static string Join(string[] a) => a == null || a.Length == 0 ? "(any)" : string.Join("/", a);

    bool ChanceAllowed() => (Preview != null && Preview.ignoreChance) || Random.value <= chance;

    // Which session the rules are being read against — the previewed one in the editor, the live one in game.
    public static RaceWeekend.Session CurrentSession
    {
        get
        {
            if (Preview != null) return Preview.session;
            if (RaceWeekend.IsPractice) return RaceWeekend.Session.Practice;
            if (RaceWeekend.IsQualifying) return RaceWeekend.Session.Qualifying;
            return RaceWeekend.Session.Race;
        }
    }

    // The beat actually played. Writes the repeat memory for the current scope.
    public void MarkSeen()
    {
        if (repeat == Repeat.EveryTime) return;
        string key = ScopedKey();
        if (key == null) return;

        if (repeat == Repeat.OncePerPlaySession) { _playSessionSeen.Add(key); return; }

        PlayerPrefs.SetInt(key, 1);
        RegisterKey(key);
        PlayerPrefs.Save();
    }

    // Wipe this block's memory for the current scope, so the beat can play again. For debug/testing and
    // for beats a quest wants to re-arm.
    public void Forget()
    {
        string key = ScopedKey();
        if (key == null) return;
        _playSessionSeen.Remove(key);
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    public bool AlreadySeen()
    {
        if (Preview != null && Preview.ignoreSeen) return false;
        if (repeat == Repeat.EveryTime) return false;
        string key = ScopedKey();
        if (key == null) return false;
        if (repeat == Repeat.OncePerPlaySession) return _playSessionSeen.Contains(key);
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    // Which half-day the weekend is on. The Director previews one; at runtime it is wherever the ledger's
    // clock has got to.
    public static WeekendSlot CurrentSlot => Preview != null ? Preview.slot : WeekendLedger.CurrentSlot;

    bool SlotAllowed() => CurrentSlot switch
    {
        WeekendSlot.FridayAM   => fridayAM,
        WeekendSlot.FridayPM   => fridayPM,
        WeekendSlot.SaturdayAM => saturdayAM,
        WeekendSlot.SaturdayPM => saturdayPM,
        WeekendSlot.SundayAM   => sundayAM,
        _                      => sundayPM,
    };

    bool SessionAllowed()
    {
        switch (CurrentSession)
        {
            case RaceWeekend.Session.Practice:    return inPractice;
            case RaceWeekend.Session.Qualifying:  return inQualifying;
            default:                              return inRace; // multiplayer and plain race both land here
        }
    }

    bool SeriesAllowed()
    {
        if (series == null || series.Length == 0) return true;
        if (Preview != null) return Matches(series, Preview.series);
        return Matches(series, PlayerPrefs.GetString("CurrentSeriesIndex", ""))
            || Matches(series, PlayerPrefs.GetString("CurrentSeriesName", ""))
            || Matches(series, PlayerPrefs.GetInt("CurrentSeries", -1).ToString());
    }

    bool StatAllowed()
    {
        if (string.IsNullOrEmpty(statKey)) return true;
        int v = PlayerStatsLedger.Get(statKey);
        return v >= statMin && v <= statMax;
    }

    bool QuestAllowed()
    {
        if (questRequirement == QuestRequirement.Ignore || string.IsNullOrEmpty(questId)) return true;

        QuestInfo quest = null;
        foreach (var q in QuestManager.All)
            if (q != null && q.id == questId) { quest = q; break; }
        if (quest == null)
        {
            Debug.LogWarning($"AppearanceConditions: quest id '{questId}' doesn't exist — treating the gate as unmet.");
            return false;
        }

        var state = QuestManager.GetState(quest);
        switch (questRequirement)
        {
            case QuestRequirement.NotStarted:    return state == QuestManager.State.NotStarted;
            case QuestRequirement.Active:        return state == QuestManager.State.Active;
            case QuestRequirement.ReadyToTurnIn: return state == QuestManager.State.ReadyToTurnIn;
            case QuestRequirement.Completed:     return state == QuestManager.State.Completed;
            case QuestRequirement.NotCompleted:  return state != QuestManager.State.Completed;
            case QuestRequirement.Started:       return state == QuestManager.State.Active
                                                     || state == QuestManager.State.ReadyToTurnIn;
        }
        return true;
    }

    // The PlayerPrefs key for the current repeat scope, or null when the block can't remember anything.
    string ScopedKey()
    {
        if (repeat == Repeat.EveryTime) return null;
        if (string.IsNullOrEmpty(saveKey))
        {
            Debug.LogWarning($"AppearanceConditions: repeat is {repeat} but Save Key is empty — the beat will repeat every time.");
            return null;
        }

        switch (repeat)
        {
            case Repeat.OncePerPlaySession:    return saveKey;
            case Repeat.OncePerWeekendSession: return $"{SeenPrefix}{saveKey}.w{RaceWeekend.WeekendId}.{RaceWeekend.Current}";
            case Repeat.OncePerWeekend:        return $"{SeenPrefix}{saveKey}.w{RaceWeekend.WeekendId}";
            // Keyed on the track, not the scene: since the multi-track split every round runs in the same
            // RaceScene, so a scene-name key would make "once per track" mean "once, ever".
            case Repeat.OncePerTrack:          return $"{SeenPrefix}{saveKey}.{CurrentTrackId}";
            default:                           return SeenPrefix + saveKey;
        }
    }

    // Which track the player is at. The loaded package is the truth (it's what's actually built around
    // them); TrackSelection is the fallback for a scene that loads no package at all.
    public static string CurrentTrackId
    {
        get
        {
            if (Preview != null) return Preview.trackId;
            var active = TrackPackage.Active;
            if (active != null && !string.IsNullOrEmpty(active.trackId)) return active.trackId;
            return TrackSelection.CurrentId;
        }
    }

    // A beat authored before the split may name its track in `scenes` — "WatkinsGlen" meant both the track
    // and the scene back then. Accept either so those keep working.
    bool PlaceAllowed()
    {
        if (scenes == null || scenes.Length == 0) return true;
        return Matches(scenes, SceneManager.GetActiveScene().name) || Matches(scenes, CurrentTrackId);
    }

    static bool Matches(string[] allowed, string value)
    {
        if (allowed == null || allowed.Length == 0) return true; // no filter = anything passes
        if (string.IsNullOrEmpty(value)) return false;
        foreach (var a in allowed)
            if (!string.IsNullOrEmpty(a) && string.Equals(a.Trim(), value.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // PlayerPrefs can't be enumerated, so keep our own list of written keys for ClearAllSeen().
    static void RegisterKey(string key)
    {
        var raw = PlayerPrefs.GetString(RegistryKey, "");
        foreach (var k in raw.Split(','))
            if (k == key) return;
        PlayerPrefs.SetString(RegistryKey, raw.Length == 0 ? key : raw + "," + key);
    }

    // Debug/testing: forget every beat that has ever been marked seen (see Draftmaster > NPCs menu).
    public static void ClearAllSeen()
    {
        foreach (var k in PlayerPrefs.GetString(RegistryKey, "").Split(','))
            if (!string.IsNullOrWhiteSpace(k)) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.DeleteKey(RegistryKey);
        PlayerPrefs.Save();
        _playSessionSeen.Clear();
    }
}
