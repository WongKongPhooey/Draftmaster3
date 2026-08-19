using System;
using System.Collections.Generic;
using UnityEngine;

// The log behind the phone's Notes app: what the player agreed to, and who asked.
//
// A side quest in this game is a conversation, not a menu entry — somebody in the paddock asks for
// something and you say yes. That's easy to forget an hour later, so every accepted quest writes a note
// here with the person's name and what they wanted, and completing it marks the note done rather than
// deleting it. Persisted in PlayerPrefs as JSON, like SponsorBook.
public static class PhoneNotes
{
    const string Key = "phone.notes";
    const string ReadKey = "phone.notes.read";   // count of notes the player has seen, for the tile badge

    [Serializable]
    public class Note
    {
        public string id;            // unique; "quest.<questId>" for quest notes
        public string title;         // "Lucky charm"
        public string from;          // "Marla Boyd" — who asked
        public string body;          // what they wanted, in their terms
        public string stamp;         // "Weekend 3 · Practice"
        public string questId;       // empty for a plain note
        public bool resolved;
    }

    [Serializable]
    class Book { public List<Note> notes = new(); }

    static Book _cache;

    static Book Data
    {
        get
        {
            if (_cache != null) return _cache;
            var raw = PlayerPrefs.GetString(Key, "");
            _cache = string.IsNullOrEmpty(raw) ? new Book() : (JsonUtility.FromJson<Book>(raw) ?? new Book());
            return _cache;
        }
    }

    static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    public static IReadOnlyList<Note> All => Data.notes;

    // Notes written since the player last opened the app.
    public static int Unread => Mathf.Max(0, Data.notes.Count - PlayerPrefs.GetInt(ReadKey, 0));
    public static void MarkAllRead() => PlayerPrefs.SetInt(ReadKey, Data.notes.Count);

    // Writing the same id twice updates the note instead of duplicating it — an NPC re-offering a quest
    // shouldn't fill the log.
    public static void Record(string id, string title, string from, string body, string questId = "")
    {
        if (string.IsNullOrEmpty(id)) return;

        var note = Find(id);
        if (note == null)
        {
            note = new Note { id = id };
            Data.notes.Add(note);
        }
        note.title = title;
        note.from = from;
        note.body = body;
        note.questId = questId;
        note.stamp = Stamp();
        Save();
    }

    public static void Resolve(string id)
    {
        var note = Find(id);
        if (note == null || note.resolved) return;
        note.resolved = true;
        Save();
    }

    // Quest hooks. QuestGiverNPC knows the person; QuestManager knows when it's finished.
    public static void RecordQuest(QuestInfo quest, string from)
    {
        if (quest == null) return;
        Record("quest." + quest.id, string.IsNullOrEmpty(quest.title) ? quest.id : quest.title,
               from, quest.description, quest.id);
    }

    public static void ResolveQuest(QuestInfo quest)
    {
        if (quest != null) Resolve("quest." + quest.id);
    }

    public static void Clear()
    {
        _cache = new Book();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey(ReadKey);
    }

    static Note Find(string id)
    {
        for (int i = 0; i < Data.notes.Count; i++)
            if (Data.notes[i].id == id) return Data.notes[i];
        return null;
    }

    static string Stamp()
    {
        string session = RaceWeekend.IsQualifying ? "Qualifying"
                       : RaceWeekend.IsPractice ? "Practice"
                       : "Race day";
        int weekend = RaceWeekend.WeekendId + 1;
        return $"Weekend {weekend} · {session}";
    }
}
