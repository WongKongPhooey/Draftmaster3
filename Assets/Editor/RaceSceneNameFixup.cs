using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot cleanup for the WatkinsGlen -> RaceScene split.
//
// The multiplayer entry points were authored while WatkinsGlen was still the race scene, so their
// `raceSceneName` fields are serialised to "WatkinsGlen" in the prefab and in DemoMenu — changing the
// C# default alone would not move them. This retargets the serialised values and drops WatkinsGlen
// from the build (the scene file stays on disk as the authored reference).
public static class RaceSceneNameFixup
{
    const string OldName = "WatkinsGlen";
    const string NewName = "RaceScene";
    const string WatkinsScenePath = "Assets/Scenes/WatkinsGlen.unity";

    [MenuItem("Draftmaster/Tracks/Retarget Multiplayer To RaceScene")]
    public static void Run()
    {
        var log = new List<string>();

        // --- prefabs
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = Retarget(root, path, log);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // --- scenes
        foreach (var scenePath in AssetDatabase.FindAssets("t:Scene")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => p.StartsWith("Assets/")))
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                changed |= Retarget(root, scenePath, log);
            if (changed) EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        // --- build settings
        var scenes = EditorBuildSettings.scenes.ToArray();
        foreach (var s in scenes)
        {
            if (s.path != WatkinsScenePath) continue;
            if (!s.enabled) { log.Add("build settings: WatkinsGlen already unticked"); break; }
            s.enabled = false;
            log.Add("build settings: WatkinsGlen unticked (entry kept, scene file untouched)");
        }
        EditorBuildSettings.scenes = scenes;

        AssetDatabase.SaveAssets();
        Debug.Log("RaceSceneNameFixup:\n" + (log.Count == 0 ? "  nothing to change" : string.Join("\n", log)));
    }

    static bool Retarget(GameObject root, string owner, List<string> log)
    {
        bool changed = false;
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (type != "MultiplayerMenuUI" && type != "NetworkLauncher") continue;

            var so = new SerializedObject(mb);
            var prop = so.FindProperty("raceSceneName");
            if (prop == null || prop.stringValue != OldName) continue;

            prop.stringValue = NewName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mb);
            changed = true;
            log.Add($"  {owner}: {type}.raceSceneName {OldName} -> {NewName}");
        }
        return changed;
    }
}
