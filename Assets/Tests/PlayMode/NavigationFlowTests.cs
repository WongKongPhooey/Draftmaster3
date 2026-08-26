using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// The demo, walked end to end with the game actually running.
//
// SceneNavigationTests reads the map; this drives it. Every route the player has out of a scene is taken
// here the way the player takes it — the menu row is confirmed, the laptop is interacted with, the door is
// opened, the pause menu quits — and the test then waits for the scene it should have landed in. That
// catches the failures a static read cannot see: a screen that loads but throws on the way up, a button
// whose listener was never attached, a scene that comes up with no camera or no player in it.
//
// Any Debug.LogError raised while a test runs fails that test on its own, so "the scene came up clean" is
// asserted just by loading it.
//
// This assembly cannot reference Assembly-CSharp, so the game's own types are reached by name through
// reflection. Everything the player's own save owns — the selected track, the garage's return scene — is
// snapshotted and put back, so running the suite never costs anyone their weekend.
public class NavigationFlowTests
{
    const string Title = "TitleScreen";
    const string Race = "RaceScene";
    const string Garage = "GarageScreen";
    const string Factory = "TeamGarage";

    const float SceneTimeout = 30f;      // the race scene builds a road and waits on the database

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly string[] SavedPrefs = { "track.current", "garage.return", "raceweekend.id" };
    readonly Dictionary<string, string> _prefs = new();

    [OneTimeSetUp]
    public void BorrowTheSave()
    {
        foreach (string key in SavedPrefs) _prefs[key] = PlayerPrefs.GetString(key, null);
        KeepTheTestRunnerAlive();
    }

    // Load a scene the way the game does, having first got the test runner out of the way.
    //
    // The play-mode runner is a plain GameObject sitting in whatever scene the run started in, marked
    // DontSave rather than DontDestroyOnLoad. A single-mode SceneManager.LoadScene therefore deletes the
    // object running these coroutines, and the run does not fail, it HANGS: the coroutine is gone, so
    // nothing ever reports a result and the editor sits in play mode until someone stops it. A suite whose
    // whole job is changing scene has to move the runner somewhere the scene change cannot reach, before
    // every load rather than once, because the framework rebuilds it between fixtures.
    static void Go(string sceneName)
    {
        KeepTheTestRunnerAlive();
        SceneManager.LoadScene(sceneName);
    }

    // Finding it is its own small problem: the runner marks itself HideFlags.DontSave, which keeps it out
    // of FindObjectsByType, and it lives in the framework's own InitTestScene rather than the active one,
    // which keeps it out of a scan of the scene under test. The framework does hold a static handle on it,
    // so ask that; the scan across every loaded scene is the belt to that pair of braces.
    static void KeepTheTestRunnerAlive()
    {
        var runner = ActiveRunner();
        Assert.IsNotNull(runner, "Could not find the play-mode test runner to protect from the scene load. " +
                                 "Loading a scene now would hang the run, so stopping here instead.");
        Object.DontDestroyOnLoad(runner);
    }

    static GameObject ActiveRunner()
    {
        var type = System.Type.GetType("UnityEngine.TestTools.TestRunner.PlaymodeTestsController, UnityEngine.TestRunner");
        var active = type?.GetProperty("ActiveController", Any)?.GetValue(null) as MonoBehaviour;
        if (active != null) return active.transform.root.gameObject;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.Contains("tests runner")) return root;
        }
        return null;
    }

    [OneTimeTearDown]
    public void GiveItBack()
    {
        foreach (var pair in _prefs)
        {
            if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
            else PlayerPrefs.SetString(pair.Key, pair.Value);
        }
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ the scenes themselves

    // Load everything the build ships, one at a time, and let it run for a moment. A scene that throws on
    // the way up fails here through the log, before any of the routing tests get near it.
    [UnityTest]
    public IEnumerator EveryBuildSceneComesUpClean()
    {
        foreach (string name in EnabledBuildScenes())
        {
            Go(name);
            yield return WaitForScene(name);
            for (int i = 0; i < 5; i++) yield return null;   // let Start and the first Updates run

            Assert.IsNotEmpty(SceneManager.GetActiveScene().GetRootGameObjects(),
                              $"'{name}' came up empty.");
            Assert.IsNotNull(Object.FindFirstObjectByType<Camera>(),
                             $"'{name}' has no camera — the player would be looking at nothing.");
        }
    }

    // A button with no listener is the project's signature failure: the builders that generate these
    // screens call onClick.AddListener at edit time, which is never serialised, so the button draws
    // perfectly and does nothing. Checked after Start, because the binders that do survive attach their
    // listeners there.
    [UnityTest]
    public IEnumerator EveryButtonDoesSomethingOnceTheSceneHasStarted()
    {
        var dead = new List<string>();
        foreach (string name in EnabledBuildScenes())
        {
            Go(name);
            yield return WaitForScene(name);
            for (int i = 0; i < 5; i++) yield return null;

            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (buttons.Length > 0)
                Assert.IsNotNull(EventSystem.current,
                                 $"'{name}' draws {buttons.Length} button(s) and has no EventSystem — none of them " +
                                 "can be clicked.");

            foreach (var button in buttons)
            {
                int calls = ListenerCount(button.onClick);
                if (calls == 0) dead.Add($"{name}: {PathOf(button.transform)}");
            }
        }

        Assert.IsEmpty(dead, "Buttons that draw but call nothing:\n  " + string.Join("\n  ", dead));
    }

    // ------------------------------------------------------------------ the title menu

    // Every row that loads a scene lands in that scene. Confirm() is what the Enter key calls, so this is
    // the row being pressed, not a re-implementation of what pressing it ought to do.
    [UnityTest]
    public IEnumerator EveryTitleRowThatLoadsASceneGetsThere()
    {
        foreach (var row in TitleRows())
        {
            if (row.command != "LoadScene") continue;

            Go(Title);
            yield return WaitForScene(Title);
            yield return null;

            Press(row.index);
            yield return WaitForScene(row.scene, $"the '{row.label}' row");
        }
    }

    // NEW SEASON is the demo's front door: it should end with a road under a car, not just a scene change.
    [UnityTest]
    public IEnumerator NewSeasonPutsAPlayerOnATrack()
    {
        Go(Title);
        yield return WaitForScene(Title);
        yield return null;

        var row = RowWithLabel("NEW SEASON");
        Press(row.index);
        yield return WaitForScene(Race, "NEW SEASON");

        yield return WaitForObject("TrackBuilder", "the race scene never built a road");
        yield return WaitForObject("SplineInputDriver", "the race scene put no cars on the track");
    }

    // And back out of it: Esc > QUIT TO TITLE is the only exit a race scene has.
    [UnityTest]
    public IEnumerator QuitToTitleLeavesTheRace()
    {
        Go(Race);
        yield return WaitForScene(Race);
        for (int i = 0; i < 5; i++) yield return null;

        var menu = Object.FindFirstObjectByType(TypeNamed("RacePauseMenu")) as MonoBehaviour;
        Assert.IsNotNull(menu, "No RacePauseMenu installed itself in the race scene — Esc does nothing there.");

        var quit = menu.GetType().GetMethod("QuitToTitle", Any);
        Assert.IsNotNull(quit, "RacePauseMenu.QuitToTitle is gone; the race scene has no way back to the front.");
        quit.Invoke(menu, null);

        yield return WaitForScene(Title, "QUIT TO TITLE");
    }

    // Leaving a scene with a weekend panel up must not carry its frozen clock into the next one.
    //
    // The schedule screen zeroes Time.timeScale while it is open and lives on a DontDestroyOnLoad object,
    // so before WeekendDirector.OnSceneLoaded cleared it, quitting to the title with the timetable up left
    // the whole game frozen — menus still responded (Update runs at timeScale 0) but the next race loaded
    // with nothing moving in it, which reads as "the game is broken" rather than "a menu was open".
    [UnityTest]
    public IEnumerator QuittingWithTheTimetableUpDoesNotFreezeTheNextScene()
    {
        Go(Race);
        yield return WaitForScene(Race);
        for (int i = 0; i < 5; i++) yield return null;

        TypeNamed("WeekendScheduleUI").GetMethod("Open", Any).Invoke(null, null);
        yield return null;
        Assert.AreEqual(0f, Time.timeScale, "The schedule screen no longer freezes the race behind it.");

        Go(Title);
        yield return WaitForScene(Title);
        yield return null;

        Assert.AreEqual(1f, Time.timeScale,
                        "Left the race with the timetable up and the clock is still stopped — everything loaded " +
                        "from here would come up frozen.");
        Assert.IsFalse((bool)TypeNamed("WeekendScheduleUI").GetProperty("IsOpen", Any).GetValue(null),
                       "The schedule screen followed the player out of the race scene.");
    }

    // ------------------------------------------------------------------ the garage round trip

    // The car sheet is a thing in the world: a laptop opens it and BACK returns to the room the laptop was
    // standing in. Both halves are walked here, from the factory, which is where the title menu sends you.
    [UnityTest]
    public IEnumerator TheFactoryLaptopOpensTheGarageAndBackComesHome()
    {
        Go(Factory);
        yield return WaitForScene(Factory);
        yield return null;

        Interact("LaptopInteractable", "the factory has no laptop to open the car sheet on");
        yield return WaitForScene(Garage, "the factory laptop");

        for (int i = 0; i < 5; i++) yield return null;
        Call("GarageScreenUI", "Back", "the garage screen has no BACK");
        yield return WaitForScene(Factory, "BACK out of the garage");
    }

    // RACE on the sheet is the other way out of it.
    [UnityTest]
    public IEnumerator RaceOnTheGarageSheetGoesRacing()
    {
        Go(Garage);
        yield return WaitForScene(Garage);
        for (int i = 0; i < 5; i++) yield return null;

        Call("GarageScreenUI", "Race", "the garage screen has no RACE");
        yield return WaitForScene(Race, "RACE off the garage sheet");
    }

    // Opened cold — no laptop, nothing remembered — BACK still has to lead somewhere, or the sheet is a
    // room with the door bricked up.
    [UnityTest]
    public IEnumerator BackOutOfAColdGarageReachesTheTitle()
    {
        PlayerPrefs.DeleteKey("garage.return");
        PlayerPrefs.Save();

        Go(Garage);
        yield return WaitForScene(Garage);
        for (int i = 0; i < 5; i++) yield return null;

        Call("GarageScreenUI", "Back", "the garage screen has no BACK");
        yield return WaitForScene(Title, "BACK out of a cold-opened garage");
    }

    // The factory is otherwise a room with no exit — nothing in it changes scene and the pause menu does
    // not arm itself there, so the door is the whole way out.
    [UnityTest]
    public IEnumerator TheFactoryDoorLeadsBackToTheTitle()
    {
        Go(Factory);
        yield return WaitForScene(Factory);
        yield return null;

        Interact("SceneDoorInteractable", "the factory has no door out");
        yield return WaitForScene(Title, "the factory door");
    }

    // ------------------------------------------------------------------ driving the game

    struct MenuRow
    {
        public int index;
        public string label, command, scene;
    }

    static MonoBehaviour TitleMenu()
    {
        var menu = Object.FindFirstObjectByType(TypeNamed("TitleScreenUI")) as MonoBehaviour;
        Assert.IsNotNull(menu, "The title screen has no TitleScreenUI — there is no menu to press.");
        return menu;
    }

    // The rows as the menu holds them, read once off the title screen so the row tests can be driven from
    // the same list the game reads.
    static List<MenuRow> _rows;

    [UnitySetUp]
    public IEnumerator ReadTheMenu()
    {
        if (_rows != null) yield break;

        Go(Title);
        yield return WaitForScene(Title);
        yield return null;

        _rows = new List<MenuRow>();
        var menu = TitleMenu();
        var rows = menu.GetType().GetField("rows").GetValue(menu) as IList;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var type = row.GetType();
            _rows.Add(new MenuRow
            {
                index = i,
                label = type.GetField("label").GetValue(row) as string,
                command = type.GetField("command").GetValue(row).ToString(),
                scene = type.GetField("sceneName").GetValue(row) as string,
            });
        }
    }

    static List<MenuRow> TitleRows() => _rows;

    static MenuRow RowWithLabel(string label)
    {
        foreach (var row in _rows)
            if (row.label == label) return row;
        Assert.Fail($"The title screen has no '{label}' row.");
        return default;
    }

    // Select a row and confirm it — what the arrow keys and Enter do, minus the keyboard.
    static void Press(int index)
    {
        var menu = TitleMenu();
        var type = menu.GetType();
        type.GetField("_index", Any).SetValue(menu, index);
        var confirm = type.GetMethod("Confirm", Any);
        Assert.IsNotNull(confirm, "TitleScreenUI.Confirm is gone; nothing presses the menu.");
        confirm.Invoke(menu, null);
    }

    // Walk up to a thing in the world and press the action button on it.
    static void Interact(string typeName, string complaint)
    {
        var thing = Object.FindFirstObjectByType(TypeNamed(typeName)) as MonoBehaviour;
        Assert.IsNotNull(thing, complaint + $" (no {typeName} in {SceneManager.GetActiveScene().name}).");

        var interact = thing.GetType().GetMethod("Interact", Any);
        Assert.IsNotNull(interact, $"{typeName}.Interact is gone; walking up to it would do nothing.");
        interact.Invoke(thing, null);
    }

    static void Call(string typeName, string method, string complaint)
    {
        var screen = Object.FindFirstObjectByType(TypeNamed(typeName)) as MonoBehaviour;
        Assert.IsNotNull(screen, $"No {typeName} in {SceneManager.GetActiveScene().name}.");

        var call = screen.GetType().GetMethod(method, Any);
        Assert.IsNotNull(call, complaint + $" ({typeName}.{method} is gone).");
        call.Invoke(screen, null);
    }

    // ------------------------------------------------------------------ waiting

    static IEnumerator WaitForScene(string name, string pressed = null)
    {
        float until = Time.realtimeSinceStartup + SceneTimeout;
        while (SceneManager.GetActiveScene().name != name)
        {
            if (Time.realtimeSinceStartup > until)
            {
                string what = pressed == null ? $"Loading '{name}'" : $"Pressing {pressed}";
                Assert.Fail($"{what} did not reach '{name}' within {SceneTimeout:0}s — " +
                            $"still in '{SceneManager.GetActiveScene().name}'.");
            }
            yield return null;
        }
        yield return null;   // one more, so the new scene has run its Awake/Start
    }

    static IEnumerator WaitForObject(string typeName, string complaint)
    {
        var type = TypeNamed(typeName);
        float until = Time.realtimeSinceStartup + SceneTimeout;
        while (Object.FindFirstObjectByType(type) == null)
        {
            if (Time.realtimeSinceStartup > until) Assert.Fail($"{complaint} (no {typeName} after {SceneTimeout:0}s).");
            yield return null;
        }
    }

    // ------------------------------------------------------------------ odds and ends

    static System.Type TypeNamed(string name)
    {
        var type = System.Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is gone from Assembly-CSharp; this test is out of date.");
        return type;
    }

    // The build list as the running game sees it, minus the scene the test framework injects into it for
    // the duration of the run — InitTestScene<guid> is the runner's own empty stage, not a screen anyone
    // navigates to, and it has neither camera nor content to check.
    static IEnumerable<string> EnabledBuildScenes()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("InitTestScene")) continue;
            yield return name;
        }
    }

    // Persistent listeners are the ones wired in the inspector; runtime ones are added in code, and only
    // UnityEvent's own bookkeeping knows about those. If that bookkeeping ever moves, say so rather than
    // passing an unchecked test forever.
    static int ListenerCount(UnityEngine.Events.UnityEventBase evt)
    {
        int persistent = evt.GetPersistentEventCount();
        if (persistent > 0) return persistent;

        var calls = typeof(UnityEngine.Events.UnityEventBase)
                    .GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(evt);
        var runtime = calls?.GetType()
                      .GetField("m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(calls)
                      as System.Collections.ICollection;

        if (runtime == null)
            Assert.Inconclusive("UnityEvent no longer keeps its runtime listeners in m_Calls.m_RuntimeCalls — " +
                                "this check cannot see code-wired buttons any more.");
        return runtime.Count;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}

