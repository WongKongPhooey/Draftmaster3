using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// What a track-package instance in the open scene is carrying that its package on disk has not got — and
// the one operation that gets it out of the scene without losing it.
//
// RaceSceneSplitter already works the first half out: it is the guard that decides whether saving the race
// scene may strip a package instance or has to keep it, because a road left in the shared scene pins every
// race to that one track. But it reports through Debug.LogError, and the console is both unreadable over
// MCP in this project and wiped by the next domain reload — so the one time it matters ("the save kept the
// package; WHAT is on it?") the answer has already gone. The report writes it to Logs/track-overrides.txt
// instead, which survives a reload.
public static class TrackPackageOverrideReport
{
    [MenuItem("Draftmaster/Tracks/Report Package Overrides In Scene")]
    public static void Report()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Scene: {scene.path} (dirty={scene.isDirty})");

        var packages = Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        text.AppendLine($"Track package instances: {packages.Length}");

        foreach (var package in packages)
        {
            if (package == null) continue;
            var root = package.gameObject;
            text.AppendLine();
            text.AppendLine($"== {root.name} (trackId '{package.trackId}')");
            text.AppendLine($"   prefab instance: {PrefabUtility.IsPartOfPrefabInstance(root)}");
            text.AppendLine($"   asset: {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)}");

            // The same list the save-time guard acts on, with no cap — the point of a report is to see all
            // of it.
            string unapplied = RaceSceneSplitter.UnappliedEdits(root, int.MaxValue);
            text.AppendLine(unapplied == null
                ? "   carrying nothing the package has not got — a save would strip it."
                : "   carrying, and a save will KEEP it for:\n" + unapplied);

            // Everything Unity itself counts as an override, generated output included, for the cases where
            // the guard's filter is the thing in question.
            int changed = PrefabUtility.GetObjectOverrides(root, false).Count;
            int added = PrefabUtility.GetAddedComponents(root).Count;
            int removed = PrefabUtility.GetRemovedComponents(root).Count;
            int addedObjects = PrefabUtility.GetAddedGameObjects(root).Count;
            text.AppendLine($"   raw Unity counts: {changed} changed, {added} added component(s), " +
                            $"{removed} removed component(s), {addedObjects} added object(s)");
        }

        Write(text.ToString());
        Debug.Log($"Track package overrides written to Logs/track-overrides.txt.\n{text}");
    }

    // Send the edits a scene instance is carrying back into its package, so the scene can go back to
    // holding no road.
    //
    // The guard tells the author to do this by hand — "right-click > Prefab > Apply All" — and that is the
    // wrong tool for this asset. TrackBuilder is [ExecuteAlways] and regenerates the road from the spline
    // on every enable, so an untouched instance already reports twenty-odd added children full of road,
    // kerb, runoff and barrier meshes; Apply All bakes that lot into the package, and the next enable
    // generates another set beside it. The asset grows every time and the scene is no better off.
    //
    // So this applies exactly what the guard counts as authoring — a property changed on something that IS
    // in the package, a component added to one, a component pulled off one — and leaves generated output in
    // the instance, to be thrown away with it. Afterwards the instance carries nothing, so saving the race
    // scene strips it and the scene is free to build whichever track is selected again.
    [MenuItem("Draftmaster/Tracks/Apply Package Edits Back To Package")]
    public static void ApplyEdits()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var text = new System.Text.StringBuilder();
        int applied = 0, packages = 0;

        foreach (var package in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (package == null) continue;
            var root = package.gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(root)) continue;

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (string.IsNullOrEmpty(assetPath)) continue;

            packages++;
            text.AppendLine($"== {root.name} -> {assetPath}");

            // The root's own transform is the splitter placing the instance, and mesh components are
            // builder output. Same two exclusions the guard makes.
            foreach (var over in PrefabUtility.GetObjectOverrides(root, includeDefaultOverrides: false))
            {
                var target = over?.instanceObject;
                if (target == null) continue;
                if (target == (Object)root.transform || target == (Object)root) continue;
                if (target is MeshFilter || target is MeshRenderer) continue;

                var component = target as Component;
                if (component == null || !PrefabUtility.IsPartOfPrefabInstance(component.gameObject)) continue;

                string what = $"{target.GetType().Name} on {component.name}" +
                              (target is Transform t ? $" at {t.localPosition}" : "");
                PrefabUtility.ApplyObjectOverride(target, assetPath, InteractionMode.AutomatedAction);
                applied++;
                text.AppendLine($"   applied: {what}");
            }

            foreach (var added in PrefabUtility.GetAddedComponents(root))
            {
                var component = added?.instanceComponent;
                if (component == null) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(component.gameObject)) continue;
                string what = $"{component.GetType().Name} on {component.name}";
                added.Apply(assetPath, InteractionMode.AutomatedAction);
                applied++;
                text.AppendLine($"   applied added component: {what}");
            }

            foreach (var gone in PrefabUtility.GetRemovedComponents(root))
            {
                if (gone?.assetComponent == null) continue;
                string what = gone.assetComponent.GetType().Name;
                gone.Apply(assetPath, InteractionMode.AutomatedAction);
                applied++;
                text.AppendLine($"   applied removal: {what}");
            }

            string left = RaceSceneSplitter.UnappliedEdits(root, int.MaxValue);
            text.AppendLine(left == null
                ? "   instance now carries nothing — saving the scene will strip it."
                : "   STILL carrying:\n" + left);
        }

        AssetDatabase.SaveAssets();
        if (applied > 0) EditorSceneManager.MarkSceneDirty(scene);

        string summary = packages == 0
            ? "Apply: no track package instances in the scene."
            : $"Apply: pushed {applied} edit(s) from {packages} instance(s) back into their package(s).";
        Write(summary + "\n" + $"Scene: {scene.path}\n" + text);
        Debug.Log(summary + "\n" + text);
    }

    static void Write(string text)
    {
        string path = Path.Combine(Application.dataPath, "..", "Logs", "track-overrides.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }
}
