using System;
using System.Globalization;
using UnityEngine;

// When the career was last written down, in real-world time.
//
// There is no save button in this game and no save file: progress is PlayerPrefs, written by whichever
// subsystem owns it at the moment it changes (see CareerReset for the full spread of it). So "the last save
// point" is not a thing on disk — it is the last moment the player did something the career kept, and the
// only way to know when that was is to say so at the points where it happens.
//
// Stamp() marks one. It is called where progress actually moves rather than on a timer: choosing a track,
// starting or settling one of the player's sessions, opening a fresh weekend, and leaving a race for the
// title screen. The title screen reads it back under CONTINUE, so the row says where you were and when, the
// way a save slot would.
//
// Wiped with everything else by CareerReset.ClearAll, which is correct: a career that has just been thrown
// away has no save point, and the first thing the restart does is make a new one.
public static class CareerSave
{
    // Ticks as a string. PlayerPrefs has no 64-bit integer, and a float would round the answer off to the
    // nearest few minutes — which is fine for a date and wrong the moment anything wants the time.
    const string Key = "career.savedat";

    // The moment progress was last kept, in the player's own time zone. Null when there has never been one:
    // a fresh install, or a career just reset.
    public static DateTime? At
    {
        get
        {
            string saved = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(saved)) return null;
            if (!long.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                return null;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return null;

            return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
        }
    }

    public static bool Exists => At.HasValue;

    // Stored UTC so a player who travels, or whose machine changes zone, does not have their save jump
    // hours; read back local, because the date under CONTINUE is a real-world date the player recognises.
    public static void Stamp()
    {
        PlayerPrefs.SetString(Key, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
