using System;
using UnityEngine;

// StatsManager is the friendly, gameplay-facing API for player stats. It sits on
// top of DatabaseManager so the rest of the game never touches SQL directly, and
// it gives every stat a single well-known key. Driving systems, side-quests and
// dialogue all flow through here when they hand out stat points.
//
// "Stats" here are RPG-style attributes the player grows over a career:
//   Endurance  - earned by racking up distance behind the wheel
//   Pace       - earned by hitting high speeds
//   Drafting   - earned by spending time in the slipstream
//   Charisma   - earned by talking to NPCs and finishing their side-quests
//   Reputation - earned by completing side-quests
public static class StatsManager
{
    // Canonical stat keys. Use these constants everywhere rather than raw
    // strings so a typo can't silently create a brand new stat row.
    public const string Endurance  = "Endurance";
    public const string Pace       = "Pace";
    public const string Drafting   = "Drafting";
    public const string Charisma   = "Charisma";
    public const string Reputation = "Reputation";

    // Raised whenever a stat changes so UI (a stat screen, a toast) can react.
    // Args: stat name, the amount that was added, the new running total.
    public static event Action<string, int, int> OnStatChanged;

    // Adds points to a stat and persists the result. Returns the new total.
    public static int GainStat(string statName, int amount){
        if(string.IsNullOrEmpty(statName) || amount == 0){
            return GetStat(statName);
        }
        int newTotal = DatabaseManager.AddStat(statName, amount);
        Debug.Log("[Stats] " + statName + " +" + amount + " (now " + newTotal + ")");
        OnStatChanged?.Invoke(statName, amount, newTotal);
        return newTotal;
    }

    public static int GetStat(string statName){
        if(string.IsNullOrEmpty(statName)){
            return 0;
        }
        return DatabaseManager.GetStat(statName);
    }

    public static void SetStat(string statName, int value){
        if(string.IsNullOrEmpty(statName)){
            return;
        }
        DatabaseManager.SetStat(statName, value);
        OnStatChanged?.Invoke(statName, 0, value);
    }
}
