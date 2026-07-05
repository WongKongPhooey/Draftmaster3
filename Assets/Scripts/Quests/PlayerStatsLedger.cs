using UnityEngine;

// Persistent career counters: "starts", "wins", "top10s", later "starts.chevrolet" etc. PlayerPrefs-backed,
// following the project's persistence convention. Quest StatThreshold objectives read these keys, so any
// new counter added here is immediately quest-able without touching the quest system.
public static class PlayerStatsLedger
{
    const string Prefix = "stat.";

    public static int Get(string key) => PlayerPrefs.GetInt(Prefix + key, 0);

    public static void Increment(string key, int by = 1)
    {
        PlayerPrefs.SetInt(Prefix + key, Get(key) + by);
        PlayerPrefs.Save();
        QuestManager.ReevaluateStatObjectives();
    }
}
