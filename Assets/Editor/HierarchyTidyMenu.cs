using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Edit-time version of the runtime hierarchy tidier (see Assets/Scripts/RuntimeHierarchy.cs).
//
// The runtime organiser only files objects during play; this menu applies the same grouping to the scene
// you are authoring, so an authored scene can be checked in already tidy. It is undoable and it never
// opens a dialog, so it is safe to drive from MCP.
public static class HierarchyTidyMenu
{
    [MenuItem("Draftmaster/Scene/Tidy Hierarchy Into Groups", false, 40)]
    public static void Tidy()
    {
        int moved = 0;
        var scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) scenes.Add(s);
        }

        var roots = new List<GameObject>();
        foreach (var scene in scenes)
        {
            roots.Clear();
            scene.GetRootGameObjects(roots);

            foreach (var go in roots)
            {
                if (go == null || go.hideFlags != HideFlags.None) continue;
                if (go.GetComponent<HierarchyIgnore>() != null) continue;
                if (IsGroupBucket(go)) continue;

                var group = RuntimeHierarchy.Classify(go);
                var parent = FindOrCreateBucket(RuntimeHierarchy.NameOf(group), scene);
                if (parent == null || parent.gameObject == go) continue;

                Undo.SetTransformParent(go.transform, parent, "Tidy Hierarchy");
                moved++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[HierarchyTidy] Filed {moved} root object(s) into groups across {scenes.Count} scene(s). Save the scene to keep it.");
    }

    [MenuItem("Draftmaster/Scene/Flatten Hierarchy Groups", false, 41)]
    public static void Flatten()
    {
        int moved = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = new List<GameObject>();
            scene.GetRootGameObjects(roots);

            foreach (var go in roots)
            {
                if (!IsGroupBucket(go)) continue;
                for (int c = go.transform.childCount - 1; c >= 0; c--)
                {
                    Undo.SetTransformParent(go.transform.GetChild(c), null, "Flatten Hierarchy");
                    moved++;
                }
                Undo.DestroyObjectImmediate(go);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[HierarchyTidy] Returned {moved} object(s) to the scene root.");
    }

    // A bucket is a root named after a group that carries nothing but its Transform.
    static bool IsGroupBucket(GameObject go)
    {
        if (go == null || go.transform.parent != null) return false;
        if (go.GetComponents<Component>().Length != 1) return false;

        foreach (HierarchyGroup g in System.Enum.GetValues(typeof(HierarchyGroup)))
            if (go.name == RuntimeHierarchy.NameOf(g)) return true;
        return false;
    }

    static Transform FindOrCreateBucket(string name, Scene scene)
    {
        var roots = new List<GameObject>();
        scene.GetRootGameObjects(roots);
        foreach (var go in roots)
            if (go.name == name && IsGroupBucket(go)) return go.transform;

        var bucket = new GameObject(name);
        SceneManager.MoveGameObjectToScene(bucket, scene);
        bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        bucket.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(bucket, "Tidy Hierarchy");
        return bucket.transform;
    }
}
