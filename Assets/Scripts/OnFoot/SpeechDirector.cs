using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// One screen, one voice.
//
// Every speech bubble in the game asks here before it appears. Whatever is already being said either keeps
// the screen, gets cut off, or is queued behind — the rules are in Draftmaster.Sim.SpeechQueue, and this is
// the part that knows about actual bubbles.
//
// Without it, the paddock talks over itself: a wandering NPC barks while the crew chief is mid-sentence, a
// walk-up cutscene starts under an autograph fan's line, and the player is left reading three boxes at once
// with no idea which one is theirs.
public static class SpeechDirector
{
    class Waiting
    {
        public SpeechBubble bubble;
        public string text, speaker;
        public SpeechPriority priority;
        public object owner;
        public float queuedAt;
    }

    static SpeechBubble _current;
    static SpeechPriority _currentPriority;
    static object _currentOwner;
    static readonly List<Waiting> _queue = new();

    // How long a queued line still counts as worth saying. Past this it is about a moment that has gone.
    const float StaleAfterSeconds = 6f;

    public static bool AnyoneSpeaking => _current != null;
    public static SpeechBubble Current => _current;

    // Ask to say something. True = say it now; false = it was queued or dropped, and the caller should
    // carry on as if it had not spoken (ambient chatter re-offers a line a second later anyway).
    // `owner` is what makes a conversation one voice rather than two: a two-hander alternates between the
    // NPC's bubble and the player's, and the reply must never be made to queue behind the line it is
    // answering. Both bubbles are handed the same owner, so the exchange is treated as one speaker.
    public static bool Request(SpeechBubble bubble, string text, string speaker, SpeechPriority priority,
                               object owner = null)
    {
        if (bubble == null) return false;

        Forget(bubble);   // a speaker asking again replaces its own place in the queue
        bool sameSpeaker = _current == bubble || (owner != null && ReferenceEquals(owner, _currentOwner));
        var verdict = SpeechQueue.Judge(_current != null ? _currentPriority : (SpeechPriority?)null,
                                        priority, sameSpeaker);

        switch (verdict)
        {
            case SpeechVerdict.Speak:
                if (_current != null && _current != bubble) _current.HideNow();
                _current = bubble;
                _currentPriority = priority;
                _currentOwner = owner;
                return true;

            case SpeechVerdict.Queue:
                SpeechQueue.Enqueue(_queue, new Waiting
                {
                    bubble = bubble, text = text, speaker = speaker, owner = owner,
                    priority = priority, queuedAt = Time.unscaledTime,
                });
                return false;

            default:
                return false;
        }
    }

    // A bubble has finished with the screen. The next thing waiting gets it.
    public static void Release(SpeechBubble bubble)
    {
        Forget(bubble);
        if (_current != bubble) return;

        _current = null;
        _currentOwner = null;
        PumpQueue();
    }

    static void PumpQueue()
    {
        while (_queue.Count > 0)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);

            if (next.bubble == null) continue;
            if (Time.unscaledTime - next.queuedAt > StaleAfterSeconds) continue;

            _current = next.bubble;
            _currentPriority = next.priority;
            _currentOwner = next.owner;
            next.bubble.SpeakNow(next.text, next.speaker);
            return;
        }
    }

    static void Forget(SpeechBubble bubble)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
            if (_queue[i].bubble == null || _queue[i].bubble == bubble) _queue.RemoveAt(i);
    }

    // Scene changes take every actor with them; statics survive, so clear the floor rather than leaving the
    // next scene believing a destroyed NPC still has the screen.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad()
    {
        _current = null;
        _currentOwner = null;
        _queue.Clear();
    }
}
