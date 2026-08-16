using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Draftmaster.Chatter;

// Create and find the dialogue pools that feed the randomly-spawned crowd (paddock talkers, autograph fans,
// ambient barks). Assets live in Assets/Resources/Dialogue/ and are claimed by their `trackId` field.
public static class DialoguePoolMenu
{
    public const string Folder = "Assets/Resources/" + DialogueLibrary.ResourceFolder;

    [MenuItem("Draftmaster/NPCs/Dialogue Pool (Global)")]
    public static void OpenGlobal() => Select(EnsurePool(""));

    [MenuItem("Draftmaster/NPCs/Dialogue Pool (Selected Track)")]
    public static void OpenForSelectedTrack()
    {
        string id = TrackSelection.CurrentId;
        if (string.IsNullOrEmpty(id))
        {
            EditorUtility.DisplayDialog("Dialogue Pool",
                "No track selected. Draftmaster > Tracks > Select Track For Next Race first.", "OK");
            return;
        }
        Select(EnsurePool(id));
    }

    // Copy every compiled-in table into the global pool and switch it to Replace mode, so what the crowd
    // says stops being invisible: the house style becomes rows you can read, reword and delete, rather than
    // something you have to already know is in a .cs file. Content is identical the moment it runs.
    [MenuItem("Draftmaster/NPCs/Seed Global Dialogue Pool From Built-Ins")]
    public static void SeedGlobal()
    {
        var pool = EnsurePool("");
        if (pool.replaceBuiltIn &&
            !EditorUtility.DisplayDialog("Seed Dialogue Pool",
                "The global pool already replaces the built-ins. Overwrite everything in it with a fresh " +
                "copy of the compiled-in tables?", "Overwrite", "Cancel"))
            return;

        Undo.RecordObject(pool, "Seed Dialogue Pool");

        var barks = new List<DialoguePool.ChatterSet>();
        foreach (ChatterArea area in System.Enum.GetValues(typeof(ChatterArea)))
            foreach (ChatterMood mood in System.Enum.GetValues(typeof(ChatterMood)))
            {
                var lines = AmbientChatter.BuiltIn(area, mood);
                // BuiltIn falls back to Neutral for a mood with no table of its own; don't copy those twice.
                if (lines == null || lines.Length == 0) continue;
                if (mood != ChatterMood.Neutral && lines == AmbientChatter.BuiltIn(area, ChatterMood.Neutral)) continue;
                barks.Add(new DialoguePool.ChatterSet { area = area, mood = mood, lines = (string[])lines.Clone() });
            }
        pool.chatter = barks.ToArray();

        var convos = new List<DialoguePool.Conversation>();
        foreach (var lines in PaddockSpawner.BuiltInConversations)
            convos.Add(new DialoguePool.Conversation { kind = ConversationKind.PaddockCrew, lines = (string[])lines.Clone() });
        foreach (var lines in AutographFanSpawner.BuiltInConversations)
            convos.Add(new DialoguePool.Conversation { kind = ConversationKind.AutographFan, speakerName = "Fan", lines = (string[])lines.Clone() });
        pool.conversations = convos.ToArray();

        pool.speakerNames = (string[])PaddockSpawner.BuiltInNames.Clone();
        pool.replaceBuiltIn = true; // the tables now live here; don't say everything twice

        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();
        DialogueLibrary.Refresh();
        Select(pool);
        Debug.Log($"Dialogue: seeded the global pool with {pool.chatter.Length} bark set(s), " +
                  $"{pool.conversations.Length} conversation(s) and {pool.speakerNames.Length} name(s).");
    }

    // The pool asset for a track ("" = the global one), created on demand. A new track pool starts with one
    // empty entry of each kind so there's something to type into rather than an empty array to grow.
    public static DialoguePool EnsurePool(string trackId)
    {
        var existing = Find(trackId);
        if (existing != null) return existing;

        Directory.CreateDirectory(Folder);
        string file = string.IsNullOrEmpty(trackId) ? "Default" : trackId;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{file}.asset");

        var pool = ScriptableObject.CreateInstance<DialoguePool>();
        pool.trackId = trackId ?? "";
        pool.chatter = new[]
        {
            new DialoguePool.ChatterSet { area = ChatterArea.Paddock, mood = ChatterMood.Neutral, lines = new string[0] },
        };
        pool.conversations = new[]
        {
            new DialoguePool.Conversation { kind = ConversationKind.PaddockCrew, lines = new string[0] },
        };
        pool.speakerNames = new string[0];

        AssetDatabase.CreateAsset(pool, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Dialogue: created {path} ({(string.IsNullOrEmpty(trackId) ? "global" : trackId)}).");
        return pool;
    }

    // The pool claiming this track id, or null. Matches on the field, not the filename.
    public static DialoguePool Find(string trackId)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:DialoguePool"))
        {
            var pool = AssetDatabase.LoadAssetAtPath<DialoguePool>(AssetDatabase.GUIDToAssetPath(guid));
            if (pool == null) continue;
            if (string.IsNullOrEmpty(trackId) ? pool.IsGlobal
                                              : (!pool.IsGlobal && pool.AppliesTo(trackId))) return pool;
        }
        return null;
    }

    static void Select(DialoguePool pool)
    {
        if (pool == null) return;
        Selection.activeObject = pool;
        EditorGUIUtility.PingObject(pool);
        DialogueLibrary.Refresh();
    }
}

// Pools are cached per track at runtime, so an edit made while the game is running (or between play runs
// without a domain reload) would otherwise not be seen. Drop the cache whenever one is saved.
public class DialoguePoolPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
            if (path.EndsWith(".asset") && path.Contains("/" + DialogueLibrary.ResourceFolder + "/"))
            {
                DialogueLibrary.Refresh();
                return;
            }
    }
}
