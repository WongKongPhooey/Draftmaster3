using UnityEngine;

// Editor-placed marker for where the on-foot player may appear at scene load. Drop any number of
// these into a race scene (GameObject > Draftmaster > Player Spawn Point); PitLaneStart picks one
// at random, weighted, and spawns the player there instead of the procedural pit-lane midpoint.
// No markers in the scene = the old pit-lane spawn is used, so existing scenes are unaffected.
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Relative chance of being picked. 2 = twice as likely as a weight-1 marker. 0 = never picked (parked without deleting).")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Optional editor-only label drawn next to the gizmo, e.g. \"paddock gate\".")]
    public string label;

    // Weighted-random pick over the enabled markers in the scene. Null if none are usable.
    public static PlayerSpawnPoint Pick()
    {
        var all = FindObjectsByType<PlayerSpawnPoint>();
        float total = 0f;
        for (int i = 0; i < all.Length; i++)
            if (all[i].isActiveAndEnabled && all[i].weight > 0f) total += all[i].weight;
        if (total <= 0f) return null;

        float roll = Random.value * total;
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (!p.isActiveAndEnabled || p.weight <= 0f) continue;
            roll -= p.weight;
            if (roll <= 0f) return p;
        }
        return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        bool usable = isActiveAndEnabled && weight > 0f;
        Gizmos.color = usable ? new Color(0.2f, 0.9f, 1f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        // A person-sized ring with a head dot — reads as "someone stands here" from the top-down view.
        Vector3 p = transform.position;
        Gizmos.DrawWireSphere(p, 0.6f);
        Gizmos.DrawSphere(p + new Vector3(0f, 0.25f, 0f), 0.12f);

        UnityEditor.Handles.color = Gizmos.color;
        string text = string.IsNullOrEmpty(label) ? "Player Spawn" : $"Player Spawn — {label}";
        if (weight > 0f && !Mathf.Approximately(weight, 1f)) text += $"  (w={weight:0.##})";
        UnityEditor.Handles.Label(p + new Vector3(0.8f, 0.8f, 0f), text);
    }

    [UnityEditor.MenuItem("GameObject/Draftmaster/Player Spawn Point", false, 10)]
    static void CreateMarker(UnityEditor.MenuCommand cmd)
    {
        var go = new GameObject("PlayerSpawnPoint");
        go.AddComponent<PlayerSpawnPoint>();
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

        // Drop it where the scene view is looking so it lands on-screen, not at the origin.
        var view = UnityEditor.SceneView.lastActiveSceneView;
        if (go.transform.parent == null && view != null)
        {
            Vector3 pivot = view.pivot;
            go.transform.position = new Vector3(pivot.x, pivot.y, 0f);
        }

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Player Spawn Point");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
