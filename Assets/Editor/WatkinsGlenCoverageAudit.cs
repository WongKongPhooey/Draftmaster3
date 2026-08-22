using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Answers one question before WatkinsGlen.unity is deleted: is everything in it also in
// RaceScene.unity or Resources/TrackPackages/WatkinsGlen.prefab?
//
// The split moved the track half into a package and left the manager half in RaceScene, but nothing ever
// verified the two halves add up — the splitter picked track roots by component/name, so anything outside
// that set could have been dropped. This walks all three, keys every GameObject by name + component
// signature, and reports what the scene has that the union does not.
//
// Read-only: opens scenes additively, closes without saving, never writes to the project.
public static class WatkinsGlenCoverageAudit
{
    const string ScenePath = "Assets/Scenes/WatkinsGlen.unity";
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";
    const string PackagePath = "Assets/Resources/TrackPackages/WatkinsGlen.prefab";

    [MenuItem("Draftmaster/Tracks/Audit WatkinsGlen Scene Coverage")]
    public static void Run()
    {
        var scene = Inventory(ScenePath, out var sceneRoots);
        var race = Inventory(RaceScenePath, out var raceRoots);
        var package = PrefabInventory(PackagePath);

        var union = new Dictionary<string, int>(race);
        foreach (var kv in package)
            union[kv.Key] = union.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;

        var missing = new List<string>();
        foreach (var kv in scene.OrderBy(k => k.Key))
        {
            union.TryGetValue(kv.Key, out int have);
            if (have < kv.Value) missing.Add($"  {kv.Key}   scene x{kv.Value}, union x{have}");
        }

        var lines = new List<string>
        {
            $"WatkinsGlen coverage audit",
            $"  scene      {ScenePath}: {scene.Values.Sum()} objects, {sceneRoots} roots",
            $"  race scene {RaceScenePath}: {race.Values.Sum()} objects, {raceRoots} roots",
            $"  package    {PackagePath}: {package.Values.Sum()} objects",
            "",
            missing.Count == 0
                ? "COVERED: every object in the scene has a match in RaceScene + package."
                : $"NOT COVERED: {missing.Count} object signatures short (name | components):"
        };
        lines.AddRange(missing);

        string report = string.Join("\n", lines);
        Debug.Log(report);
        Directory.CreateDirectory("Docs/Reports");
        File.WriteAllText("Docs/Reports/WatkinsGlenCoverage.txt", report);
    }

    // name + sorted component types — enough to spot a lost object or a stripped component, and stable
    // across the reparenting the split did (paths would not be).
    static Dictionary<string, int> Inventory(string scenePath, out int rootCount)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        rootCount = roots.Length;
        var counts = new Dictionary<string, int>();
        foreach (var root in roots) Walk(root, counts);
        EditorSceneManager.CloseScene(scene, true);
        return counts;
    }

    static Dictionary<string, int> PrefabInventory(string path)
    {
        var counts = new Dictionary<string, int>();
        var root = PrefabUtility.LoadPrefabContents(path);
        // Skip the wrapper the splitter added; its children are the scene's original roots.
        foreach (Transform child in root.transform) Walk(child.gameObject, counts);
        PrefabUtility.UnloadPrefabContents(root);
        return counts;
    }

    static void Walk(GameObject go, Dictionary<string, int> counts)
    {
        string key = Key(go);
        counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        foreach (Transform child in go.transform) Walk(child.gameObject, counts);
    }

    static string Key(GameObject go)
    {
        var types = go.GetComponents<Component>()
            .Where(c => c != null)
            .Select(c => c.GetType().Name)
            .OrderBy(s => s);
        return $"{go.name} | {string.Join(",", types)}";
    }
}
