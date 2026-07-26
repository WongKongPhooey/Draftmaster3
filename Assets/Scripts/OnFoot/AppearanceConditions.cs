using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Progression;

// Authorable "should this NPC (or cutscene, or scene beat) show up right now?" rule block.
//
// Drop one on any spawner as a serialized field and check IsMet() before building the thing:
//
//     if (rvNpcAppearance.IsMet()) BuildRvDoorCutscene(...);
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

    [Header("Where")]
    [Tooltip("Scene names this may appear in (e.g. 'WatkinsGlen'). Empty = any scene. Case-insensitive.")]
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

    const string SeenPrefix = "npc.seen.";
    const string RegistryKey = "npc.seen.keys"; // CSV of every key ever written, so a debug menu can clear them

    // OncePerPlaySession lives here only — statics die on domain reload, which is exactly the scope.
    static readonly HashSet<string> _playSessionSeen = new HashSet<string>();

    // Everything ANDed. Rolls `chance`, so call once and cache.
    public bool IsMet()
    {
        if (!enabled) return false;
        if (!SessionAllowed()) return false;
        if (!Matches(scenes, SceneManager.GetActiveScene().name)) return false;
        if (!SeriesAllowed()) return false;
        if (!StatAllowed()) return false;
        if (!CareerPath.Allows(careerPaths)) return false;
        if (!QuestAllowed()) return false;
        if (!string.IsNullOrEmpty(requiredItemId) && !PlayerInventory.Has(requiredItemId)) return false;
        if (AlreadySeen()) return false;
        if (ExtraGate != null && !ExtraGate()) return false;
        if (chance < 1f && Random.value > chance) return false;
        return true;
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
        if (repeat == Repeat.EveryTime) return false;
        string key = ScopedKey();
        if (key == null) return false;
        if (repeat == Repeat.OncePerPlaySession) return _playSessionSeen.Contains(key);
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    bool SessionAllowed()
    {
        if (RaceWeekend.IsPractice) return inPractice;
        if (RaceWeekend.IsQualifying) return inQualifying;
        return inRace; // multiplayer and plain race sessions both land here
    }

    bool SeriesAllowed()
    {
        if (series == null || series.Length == 0) return true;
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
            case Repeat.OncePerTrack:          return $"{SeenPrefix}{saveKey}.{SceneManager.GetActiveScene().name}";
            default:                           return SeenPrefix + saveKey;
        }
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
