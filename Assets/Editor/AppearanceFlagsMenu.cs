using UnityEditor;
using UnityEngine;
using Draftmaster.Progression;

// Testing aid for AppearanceConditions: any beat set to once-per-weekend / once-ever writes a
// PlayerPrefs flag when it plays, which makes it impossible to see again from the editor. This wipes
// every such flag so the next Play Mode run replays them all.
public static class AppearanceFlagsMenu
{
    [MenuItem("Draftmaster/NPCs/Clear Appearance Flags")]
    public static void Clear()
    {
        AppearanceConditions.ClearAllSeen();
        Debug.Log("Cleared NPC appearance flags — once-per-weekend / once-ever beats will play again.");
    }

    // The career-path answer is a one-off per save (CareerPathNPC only asks once), so testing the beat needs
    // a way to un-answer it. Also takes the starting stats it paid out back off the ledger.
    [MenuItem("Draftmaster/NPCs/Clear Career Path Choice")]
    public static void ClearCareerPath()
    {
        var was = CareerPath.Current;
        CareerPath.Reset();
        Debug.Log($"Cleared career path (was {CareerPath.DisplayName(was)}) and refunded its starting stats — " +
                  "the paddock veteran will ask again.");
    }
}
