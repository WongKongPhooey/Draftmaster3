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

    // Deterministic variant: if preferredName is set and an enabled marker with that exact
    // GameObject name exists, always return it (weight is ignored — the pin is explicit).
    // Otherwise falls back to the weighted-random Pick(). Lets a scene fix the on-foot start at a
    // named spot (e.g. the player's motorhome) without affecting scenes that lack that marker.
    public static PlayerSpawnPoint Pick(string preferredName)
    {
        if (!string.IsNullOrEmpty(preferredName))
        {
            var all = FindObjectsByType<PlayerSpawnPoint>();
            for (int i = 0; i < all.Length; i++)
                if (all[i].isActiveAndEnabled && all[i].gameObject.name == preferredName)
                    return all[i];
        }
        return Pick();
    }

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

        // Into whatever is open — the scene, or the track package on a prefab stage — and parented so the
        // stage will actually save it. See PaddockAuthoringStage.
        PaddockAuthoringStage.Place(go, cmd);

        go.AddComponent<PlayerSpawnPoint>();

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Player Spawn Point");
        UnityEditor.Selection.activeGameObject = go;
        UnityEditor.EditorGUIUtility.PingObject(go);
    }
#endif
}
