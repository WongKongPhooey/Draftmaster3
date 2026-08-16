using UnityEditor;
using UnityEngine;

// Editor housekeeping for the paddock fight system. FightTestRivals seeds a couple of rivalries on a fresh
// save so the "square up" dialogue option is reachable in a test session; these items undo that without
// touching feuds the player actually earned on track.
public static class FightMenu
{
    [MenuItem("Draftmaster/Fights/Clear Seeded Test Rivalries")]
    public static void ClearSeeded()
    {
        string raw = PlayerPrefs.GetString(FightTestRivals.SeededKey, "");
        if (string.IsNullOrEmpty(raw))
        {
            Debug.Log("Fights: nothing seeded — no test rivalries to clear.");
            return;
        }

        string playerName = DriverRelationships.PlayerName;
        int cleared = 0;
        foreach (var name in raw.Split('\n'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            float current = DriverRelationships.Get(playerName, name);
            if (!Mathf.Approximately(current, 0f)) DriverRelationships.Modify(playerName, name, -current);
            cleared++;
        }

        PlayerPrefs.DeleteKey(FightTestRivals.SeededKey);
        PlayerPrefs.Save();
        Debug.Log($"Fights: cleared {cleared} seeded rivalry/rivalries back to neutral.");
    }

    [MenuItem("Draftmaster/Fights/Reset ALL Driver Relationships")]
    public static void ResetAll()
    {
        if (!EditorUtility.DisplayDialog(
                "Reset all driver relationships?",
                "This wipes every stored rivalry and alliance for this save, including ones earned on track. " +
                "It cannot be undone.",
                "Reset", "Cancel"))
            return;

        DriverRelationships.ResetAll();
        PlayerPrefs.DeleteKey(FightTestRivals.SeededKey);
        PlayerPrefs.Save();
        Debug.Log("Fights: all driver relationships reset to neutral.");
    }
}
