using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Chatter;

// Resolves what the randomly-spawned crowd says at the track the player is actually at.
//
// Every spawner used to carry its own hard-coded table, which meant the paddock said the same eight things
// at all thirty-five circuits and nothing could be edited without recompiling. Those tables are still there
// as the house style; this layers authored `DialoguePool` assets on top of them:
//
//     Resources/Dialogue/Default.asset     trackId ""          applies everywhere
//     Resources/Dialogue/Daytona.asset     trackId "Daytona"   applies at Daytona, on top of the above
//
// Any pool with `replaceBuiltIn` drops the compiled-in tables, leaving only what's authored.
//
// The lookup is cached per track and re-resolved when the track changes, so a spawner can call it per NPC.
public static class DialogueLibrary
{
    public const string ResourceFolder = "Dialogue";

    static readonly List<DialoguePool> _pools = new List<DialoguePool>();
    static string _loadedFor;
    static bool _loaded;

    // Install the chatter hook as the game starts. AmbientChatter stays a pure, testable class; this is the
    // one place that knows about Resources and about which track we're at.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        AmbientChatter.Provider = (area, mood) => Chatter(area, mood);
    }

    // Forget the cache — call after editing pool assets, or when the track changes under us.
    public static void Refresh()
    {
        _loaded = false;
        _loadedFor = null;
        _pools.Clear();
    }

    // Pools that apply right now: the global one plus this track's, in that order.
    public static IReadOnlyList<DialoguePool> Active
    {
        get
        {
            string track = AppearanceConditions.CurrentTrackId;
            if (_loaded && _loadedFor == track) return _pools;

            _pools.Clear();
            _loadedFor = track;
            _loaded = true;

            // LoadAll rather than by filename: a pool is claimed by its trackId field, so renaming the
            // asset can't quietly detach it from its track.
            var all = Resources.LoadAll<DialoguePool>(ResourceFolder);
            foreach (var p in all) if (p != null && p.IsGlobal) _pools.Add(p);
            foreach (var p in all) if (p != null && !p.IsGlobal && p.AppliesTo(track)) _pools.Add(p);
            return _pools;
        }
    }

    static bool ReplacesBuiltIn()
    {
        foreach (var p in Active) if (p.replaceBuiltIn) return true;
        return false;
    }

    // ---------------------------------------------------------------- barks

    // Ambient one-liners for an area/mood, authored lines first. Falls back to the built-in table, and to
    // the area's Neutral pool when a mood has nothing authored or compiled in.
    public static string[] Chatter(ChatterArea area, ChatterMood mood)
    {
        var lines = new List<string>();
        foreach (var pool in Active)
        {
            if (pool.chatter == null) continue;
            foreach (var set in pool.chatter)
                if (set != null && set.area == area && set.mood == mood && set.lines != null)
                    foreach (var l in set.lines) if (!string.IsNullOrWhiteSpace(l)) lines.Add(l);
        }

        if (!ReplacesBuiltIn())
        {
            var builtIn = AmbientChatter.BuiltIn(area, mood);
            if (builtIn != null) lines.AddRange(builtIn);
        }

        // A mood nobody wrote for still has to say something: fall back to the area's neutral voice.
        if (lines.Count == 0 && mood != ChatterMood.Neutral) return Chatter(area, ChatterMood.Neutral);
        return lines.ToArray();
    }

    // ---------------------------------------------------------------- conversations

    // Multi-line conversations for one kind of random speaker, authored ones first, then the caller's
    // built-in table (unless a pool replaces it). Callers pass their own compiled table so the house style
    // lives next to the spawner that uses it.
    public static string[][] Conversations(ConversationKind kind, string[][] builtIn)
    {
        var list = new List<string[]>();
        foreach (var pool in Active)
        {
            if (pool.conversations == null) continue;
            foreach (var c in pool.conversations)
                if (c != null && c.kind == kind && c.lines != null && c.lines.Length > 0) list.Add(c.lines);
        }

        if (!ReplacesBuiltIn() && builtIn != null) list.AddRange(builtIn);
        if (list.Count == 0 && builtIn != null) return builtIn; // never leave a speaker mute
        return list.ToArray();
    }

    // The speaker name that goes with a conversation: the one authored on it when there is one, otherwise
    // one of `fallbackNames`.
    //
    // `usePoolNames` is off for speakers whose name is part of what they are — a fan is "Fan", and drawing
    // from the shared crew-name list would put "Tyre Tech" over a kid at the fence.
    public static string SpeakerNameFor(ConversationKind kind, string[] lines, string[] fallbackNames,
                                        int index, bool usePoolNames = true)
    {
        foreach (var pool in Active)
        {
            if (pool.conversations == null) continue;
            foreach (var c in pool.conversations)
                if (c != null && c.kind == kind && c.lines == lines && !string.IsNullOrEmpty(c.speakerName))
                    return c.speakerName;
        }

        var names = usePoolNames ? SpeakerNames(fallbackNames) : fallbackNames;
        if (names == null || names.Length == 0) return "Crew Member";
        return names[Mathf.Abs(index) % names.Length];
    }

    // Names random talkers can be given: authored first, then the caller's built-in list.
    public static string[] SpeakerNames(string[] builtIn)
    {
        var names = new List<string>();
        foreach (var pool in Active)
            if (pool.speakerNames != null)
                foreach (var n in pool.speakerNames) if (!string.IsNullOrWhiteSpace(n)) names.Add(n);

        if (!ReplacesBuiltIn() && builtIn != null) names.AddRange(builtIn);
        if (names.Count == 0 && builtIn != null) return builtIn;
        return names.ToArray();
    }
}
