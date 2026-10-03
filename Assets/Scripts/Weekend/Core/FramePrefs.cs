using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // PlayerPrefs reads, remembered for the rest of the frame.
    //
    // On Android every PlayerPrefs read is a JNI call into SharedPreferences, and the career state this game
    // keeps in prefs (is a session live, which weekend, which series, has the day been handed over) is asked
    // from Update and OnGUI all over the HUD — several times a frame each. Profiled on device (2026-10-03) that
    // was 4% of the main thread on its own, on a frame with about 3 ms to spare.
    //
    // So the hot getters read through here: the first ask in a frame goes to PlayerPrefs, the rest are free.
    // Anything written through the owning setter calls Invalidate, so a write is seen by the very next read;
    // a write that bypasses the setter (CareerMirror, a prefs stash being restored) is seen a frame later.
    //
    // Outside Play Mode it is a straight read: edit-mode tests write a key and read it back without a frame
    // ever passing, and must see what they wrote.
    public static class FramePrefs
    {
        static readonly Dictionary<(string, int), int> _ints = new();
        static int _frame = -1;

        public static int GetInt(string key, int fallback)
        {
            if (!Application.isPlaying) return PlayerPrefs.GetInt(key, fallback);

            int frame = Time.frameCount;
            if (frame != _frame) { _ints.Clear(); _frame = frame; }

            if (_ints.TryGetValue((key, fallback), out int v)) return v;
            v = PlayerPrefs.GetInt(key, fallback);
            _ints[(key, fallback)] = v;
            return v;
        }

        // Forget everything read this frame. Call after writing a key that is read through here.
        public static void Invalidate() => _frame = -1;
    }
}
