using UnityEditor;
using UnityEngine;

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
}
