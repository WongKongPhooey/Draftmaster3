using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Can you get everywhere from the front of the game, and back out again?
//
// The demo is a handful of scenes joined by strings — a door names the scene it opens, a menu row names
// the scene it loads, a laptop names one in code. None of that is checked by the compiler: a renamed
// scene, a scene dropped from the build settings or a typo in an inspector field all fail the same silent
// way, with a button that draws fine and does nothing. TitleScreenWiringTests checks the title menu's own
// rows; this walks the whole map instead — every enabled build scene, every exit out of it, and whether
// the graph those make is connected.
//
// Read in edit mode, so it runs in seconds and needs no play session. The other half — that pressing the
// thing actually moves you — is NavigationFlowTests in Assets/Tests/PlayMode.
//
// Nothing here names a game type: this assembly can't reference Assembly-CSharp, so components are found
// by type name and read through SerializedObject, the way the inspector reads them.
public class SceneNavigationTests
{
    const string TitleScene = "TitleScreen";

    // Scenes that ship in the build list with nothing routing to them. Each entry is a job someone still
    // owes, not a rule — DemoMenu is the multiplayer lobby, orphaned when the title screen became the boot
    // scene, and it either gets a row on that menu or comes out of the build settings. Listing it here
    // makes the test fail in both directions: a NEW scene going unreachable fails, and wiring DemoMenu up
    // fails too, until the name is deleted from this list.
    static readonly string[] KnownOrphans = { "DemoMenu" };

    // The map: scene name -> the scenes it can reach. Built once, from the scenes themselves.
    static Dictionary<string, HashSet<string>> _exits;
    static List<string> _enabled;                 // enabled build scene names, in build order
    static Dictionary<string, string> _paths;     // scene name -> asset path

    [OneTimeSetUp]
    public void MapTheGame()
    {
        _enabled = new List<string>();
        _paths = new Dictionary<string, string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            _enabled.Add(name);
            _paths[name] = scene.path;
        }

        _exits = new Dictionary<string, HashSet<string>>();
        foreach (string name in _enabled) _exits[name] = ExitsOf(name);
    }

    // A scene in the build list that will not open is the loudest possible failure and the easiest to
    // miss: the row that loads it draws enabled, and the game dies on the way in. Missing scripts are
    // checked at the same time, because a component whose class was renamed away takes its wiring with it.
    [Test]
    public void EveryEnabledBuildSceneOpens()
    {
        foreach (string name in _enabled)
        {
            var scene = EditorSceneManager.OpenScene(_paths[name], OpenSceneMode.Additive);
            try
            {
                Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"'{name}' is in the build settings but would not open.");
                Assert.Greater(scene.rootCount, 0, $"'{name}' opened empty — nothing would be there to see.");

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        Assert.AreEqual(0, missing,
                            $"'{name}' has {missing} missing script(s) on '{PathOf(t)}' — whatever that object did, " +
                            "it no longer does.");
                    }
            }
            finally { Close(scene); }
        }
    }

    // Every scene named by a component — a door's sceneName, a screen's titleSceneName, a menu row's
    // sceneName — has to be in the build settings. A name that is not loads nothing at runtime.
    [Test]
    public void EverySceneAnObjectNamesIsInTheBuildSettings()
    {
        foreach (string from in _enabled)
            foreach (var exit in NamedExits(from))
                Assert.Contains(exit.scene, _enabled,
                    $"'{from}' has {exit.owner}.{exit.field} = '{exit.scene}', which is not an enabled build scene — " +
                    "that route leads nowhere.");
    }

    // The whole point of a menu: from the boot scene you can get to everything the build ships. A scene in
    // the build list that nothing routes to is either an orphan that should come out of the list, or a
    // screen someone forgot to hang a door on.
    [Test]
    public void EveryEnabledBuildSceneIsReachableFromTheTitle()
    {
        var reached = ReachableFromTitle();
        var stranded = new List<string>();
        foreach (string name in _enabled)
            if (!reached.Contains(name)) stranded.Add(name);

        var newlyStranded = new List<string>(stranded);
        foreach (string known in KnownOrphans) newlyStranded.Remove(known);
        Assert.IsEmpty(newlyStranded,
            $"No route from {TitleScene} to: {string.Join(", ", newlyStranded)}. Either wire a way in, or take them " +
            "out of the build settings so they stop shipping.");

        foreach (string known in KnownOrphans)
            Assert.Contains(known, stranded,
                $"'{known}' is reachable now — take it out of SceneNavigationTests.KnownOrphans so the next orphan " +
                "is still caught.");
    }

    // ...and back out. A scene you can walk into and not leave is a soft lock — the player quits the game
    // to escape it. Race scenes count the pause menu, which installs itself rather than sitting in the
    // scene; on-foot scenes need a door or a laptop.
    [Test]
    public void NoSceneYouCanReachIsADeadEnd()
    {
        foreach (string name in ReachableFromTitle())
            Assert.IsNotEmpty(_exits[name],
                $"'{name}' has no way out: nothing in it names another scene and no self-installing screen covers " +
                "it. Once the player is in there, the only exit is quitting.");
    }

    // ...and the way out leads home. Not necessarily in one hop, but from anywhere the player can stand,
    // some sequence of doors has to end at the front of the game.
    [Test]
    public void TheTitleIsReachableFromEveryScene()
    {
        foreach (string from in ReachableFromTitle())
        {
            if (from == TitleScene) continue;
            Assert.IsTrue(Reaches(from, TitleScene), $"There is no route from '{from}' back to the title screen.");
        }
    }

    // ------------------------------------------------------------------ the map

    struct Exit
    {
        public string scene, owner, field;
    }

    static HashSet<string> ReachableFromTitle()
    {
        var seen = new HashSet<string> { TitleScene };
        var queue = new Queue<string>();
        queue.Enqueue(TitleScene);
        while (queue.Count > 0)
        {
            if (!_exits.TryGetValue(queue.Dequeue(), out var next)) continue;
            foreach (string to in next)
                if (_exits.ContainsKey(to) && seen.Add(to)) queue.Enqueue(to);
        }
        return seen;
    }

    static bool Reaches(string from, string target)
    {
        var seen = new HashSet<string> { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            if (!_exits.TryGetValue(queue.Dequeue(), out var next)) continue;
            foreach (string to in next)
            {
                if (to == target) return true;
                if (_exits.ContainsKey(to) && seen.Add(to)) queue.Enqueue(to);
            }
        }
        return false;
    }

    static HashSet<string> ExitsOf(string name)
    {
        var exits = new HashSet<string>();
        foreach (var exit in NamedExits(name))
            if (_paths.ContainsKey(exit.scene)) exits.Add(exit.scene);
        foreach (string exit in CodeExits(name))
            if (exit != null && _paths.ContainsKey(exit)) exits.Add(exit);
        exits.Remove(name);   // reloading yourself is not a way out
        return exits;
    }

    // Scenes named by a serialized string field. Any field called sceneName or ...SceneName counts, which
    // is the convention every screen and door in the project follows — nested ones included, so a menu
    // row's sceneName inside a list is found too.
    static List<Exit> NamedExits(string name)
    {
        var found = new List<Exit>();
        var scene = EditorSceneManager.OpenScene(_paths[name], OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    var it = new SerializedObject(behaviour).GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.String) continue;
                        if (!IsSceneField(it.name) || string.IsNullOrEmpty(it.stringValue)) continue;
                        found.Add(new Exit { scene = it.stringValue, owner = behaviour.GetType().Name, field = it.name });
                    }
                }
        }
        finally { Close(scene); }
        return found;
    }

    static bool IsSceneField(string field) =>
        field == "sceneName" || field.EndsWith("SceneName") || field == "backTo";

    // Exits no object in the scene declares, because the thing that owns them installs itself at runtime.
    // Both are read off the real types rather than spelled out here, so renaming the field or the constant
    // fails this rather than quietly dropping an edge off the map.
    static List<string> CodeExits(string name)
    {
        var scene = EditorSceneManager.OpenScene(_paths[name], OpenSceneMode.Additive);
        bool isRace, hasLaptop;
        try
        {
            // The same question TrackSceneLoader asks: does this scene run a race?
            isRace = Has(scene, "GridSpawner") || Has(scene, "PitLaneStart") || Has(scene, "TrackBuilder");
            hasLaptop = Has(scene, "LaptopInteractable");
        }
        finally { Close(scene); }

        var exits = new List<string>();
        // RacePauseMenu bootstraps into every scene and arms itself where there is a track: Esc > QUIT TO TITLE.
        if (isRace) exits.Add(FieldDefault("RacePauseMenu", "titleSceneName"));
        // A laptop opens the car sheet, and the scene it opens is a constant on the loader.
        if (hasLaptop) exits.Add(Const("GarageScreenLoader", "SceneName"));
        return exits;
    }

    // The value a MonoBehaviour's field starts with, read off a throwaway instance. None of these types
    // run in edit mode (no ExecuteAlways), so adding one costs nothing but the GC.
    static string FieldDefault(string typeName, string fieldName)
    {
        var type = Type(typeName);
        var host = new GameObject(typeName + "UnderTest") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var component = host.AddComponent(type);
            var field = type.GetField(fieldName);
            Assert.IsNotNull(field, $"{typeName}.{fieldName} is gone; the navigation map is out of date.");
            return field.GetValue(component) as string;
        }
        finally { Object.DestroyImmediate(host); }
    }

    static string Const(string typeName, string fieldName)
    {
        var field = Type(typeName).GetField(fieldName);
        Assert.IsNotNull(field, $"{typeName}.{fieldName} is gone; the navigation map is out of date.");
        return field.GetValue(null) as string;
    }

    static System.Type Type(string typeName)
    {
        var type = System.Type.GetType(typeName + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{typeName} is gone from Assembly-CSharp; the navigation map is out of date.");
        return type;
    }

    static bool Has(Scene scene, string typeName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == typeName) return true;
        return false;
    }

    static void Close(Scene scene)
    {
        if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
