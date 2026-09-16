using System;
using System.Collections.Generic;
using UnityEngine;

// The log behind the phone's MESSAGES app: texts from people the driver works with, one thread per person.
//
// Notes are what the player agreed to; messages are people getting hold of them. The first is the crew
// chief asking where they have got to on the way to the Friday briefing (ChiefCheckInBeat) — the thing that
// teaches the phone key. Persisted in PlayerPrefs as JSON, like PhoneNotes, so a text survives the scene
// reload between sessions.
public static class PhoneMessages
{
    const string Key = "phone.messages";

    [Serializable]
    public class Message
    {
        public string id;            // unique within the thread; resending the same id is a no-op
        public string text;
        public string stamp;         // "FRI 7:52 AM"
        public bool fromPlayer;
    }

    [Serializable]
    public class Thread
    {
        public string id;            // "crew.chief"
        public string contact;       // "Dale Mason"
        public string role;          // "Crew chief"
        public List<Message> messages = new();
        public int read;             // how many of `messages` the player has seen
        public long order;           // bumped by every new message; newest thread first

        public int Unread => Mathf.Max(0, messages.Count - read);
        public Message Last => messages.Count > 0 ? messages[messages.Count - 1] : null;
    }

    [Serializable]
    class Book
    {
        public List<Thread> threads = new();
        public long clock;
    }

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

    // A message has landed. The beat that sent it rings the phone; this only keeps the record.
    public static event Action<Thread> Received;

    // Threads, most recent first.
    public static IReadOnlyList<Thread> Threads
    {
        get
        {
            var list = Data.threads;
            list.Sort((a, b) => b.order.CompareTo(a.order));
            return list;
        }
    }

    public static int Unread
    {
        get
        {
            int n = 0;
            var list = Data.threads;
            for (int i = 0; i < list.Count; i++) n += list[i].Unread;
            return n;
        }
    }

    public static Thread Find(string threadId)
    {
        if (string.IsNullOrEmpty(threadId)) return null;
        var list = Data.threads;
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == threadId) return list[i];
        return null;
    }

    // Somebody texts the player. Returns false when that message id is already in the thread, so a beat that
    // runs twice does not send twice. The contact's name is refreshed on every message: a career that hires
    // a new crew chief keeps the thread and shows who is on the other end now.
    public static bool Receive(string threadId, string contact, string role, string messageId, string text)
    {
        if (string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(text)) return false;

        var thread = Find(threadId);
        if (thread == null)
        {
            thread = new Thread { id = threadId };
            Data.threads.Add(thread);
        }
        else if (!string.IsNullOrEmpty(messageId))
        {
            for (int i = 0; i < thread.messages.Count; i++)
                if (thread.messages[i].id == messageId) return false;
        }

        if (!string.IsNullOrEmpty(contact)) thread.contact = contact;
        if (!string.IsNullOrEmpty(role)) thread.role = role;
        thread.messages.Add(new Message { id = messageId ?? "", text = text, stamp = Stamp() });
        thread.order = ++Data.clock;
        Save();

        Received?.Invoke(thread);
        return true;
    }

    public static void MarkRead(string threadId)
    {
        var thread = Find(threadId);
        if (thread == null || thread.read == thread.messages.Count) return;
        thread.read = thread.messages.Count;
        Save();
    }

    public static void Clear()
    {
        _cache = new Book();
        PlayerPrefs.DeleteKey(Key);
    }

    // The weekend clock, as a phone prints it. Outside a weekend there is no clock to read, so no stamp.
    static string Stamp()
    {
        var sheet = Draftmaster.Weekend.WeekendLedger.Timetable;
        if (sheet == null) return "";
        return Draftmaster.Weekend.WeekendSlots.DayShort(Draftmaster.Weekend.WeekendLedger.CurrentSlot) + " " +
               Draftmaster.Weekend.WeekendSlots.ClockAmPm(Draftmaster.Weekend.WeekendLedger.ClockMinute);
    }
}
