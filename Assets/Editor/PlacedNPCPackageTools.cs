using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Moving an NPC into the track that owns them.
//
// The rule for where a marker lives is simple — an NPC who exists at every track goes in the shared race
// scene, an NPC who is part of ONE track goes in that track's package — but acting on it by hand meant
// copying a component between a scene and a prefab stage and hoping every field came with it. These items
// do it properly: the component is copied wholesale (CopySerialized), so the tuned anchor, trigger ring,
// dialogue and appearance conditions all survive.
//
//   Move Selected NPC Into Track Package    the marker you have selected → Paddock/NPCs of the selected track
//
// It writes into the package prefab asset, so the change travels with the track.
public static class PlacedNPCPackageTools
{
    const string NpcRoot = "Paddock/NPCs";

    [MenuItem("Draftmaster/NPCs/Move Selected NPC Into Track Package")]
    public static void MoveSelectedIntoPackage()
    {
        var markers = new List<PlacedNPC>();
        foreach (var go in Selection.gameObjects)
        {
            if (go == null) continue;
            var npc = go.GetComponent<PlacedNPC>();
            if (npc != null) markers.Add(npc);
        }

        if (markers.Count == 0)
        {
            EditorUtility.DisplayDialog("Move NPC",
                "Select one or more PlacedNPC markers in the scene first.", "OK");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("Move NPC",
                "This moves a marker OUT of the open scene and INTO a package. Close the prefab stage and " +
                "select the marker in the scene instead.", "OK");
            return;
        }

        string id = TrackSelection.CurrentId;
        string path = PackagePath(id);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Move NPC",
                $"No package at {path}.\n\nPick the track first: Draftmaster > Tracks > Select Track For Next Race.",
                "OK");
            return;
        }

        string names = string.Join(", ", markers.ConvertAll(m => m.name));
        if (!EditorUtility.DisplayDialog("Move NPC into track package",
                $"Move {names} into {id}?\n\nThey will only ever appear at {id}, and the marker leaves the " +
                "scene it's in now.", $"Move into {id}", "Cancel"))
            return;

        // Edit the prefab through its contents root, which is the only way to write into a prefab asset
        // without instantiating it into the open scene.
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var parent = FindOrCreate(root.transform, NpcRoot);
            foreach (var marker in markers)
            {
                var copy = new GameObject(marker.name);
                copy.transform.SetParent(parent, false);
                copy.transform.localPosition = Vector3.zero;

                var added = copy.AddComponent<PlacedNPC>();
                EditorUtility.CopySerialized(marker, added);

                // A marker inside a package is placed, not derived: the package IS the track, so "Here"
                // is meaningful and the geometry anchors are only needed by the shared scene's cast.
                // Anything already anchored to geometry keeps that anchor — it still resolves in place.
                if (added.anchor == PlacedNPC.Anchor.Here)
                    copy.transform.position = marker.transform.position;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var marker in markers)
            Undo.DestroyObjectImmediate(marker.gameObject);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"NPCs: moved {names} into {path}. Save the scene to commit their removal from it.");
    }

    static string PackagePath(string id) => $"Assets/Resources/{TrackCatalog.PackageFolder}/{id}.prefab";

    // Walk (and build) a "Paddock/NPCs" style path under a root.
    static Transform FindOrCreate(Transform root, string path)
    {
        var current = root;
        foreach (var part in path.Split('/'))
        {
            var next = current.Find(part);
            if (next == null)
            {
                var go = new GameObject(part);
                go.transform.SetParent(current, false);
                next = go.transform;
            }
            current = next;
        }
        return current;
    }
}
