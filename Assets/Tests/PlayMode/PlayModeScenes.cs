using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

// Loading a scene from a play-mode test, without hanging the run.
//
// Unity's play-mode runner ("Code-based tests runner") is a plain scene GameObject marked DontSave rather
// than DontDestroyOnLoad, so a single-mode SceneManager.LoadScene deletes the object running the test
// coroutine. The run does not fail — it HANGS, in play mode, until somebody stops the editor by hand. Every
// fixture that changes scene goes through here, which moves the runner out of the way first.
public static class PlayModeScenes
{
    public const float Timeout = 30f;   // the race scene builds a road, a paddock and a database

    public static void Go(string sceneName)
    {
        KeepTheRunnerAlive();
        SceneManager.LoadScene(sceneName);
    }

    // The runner hides from FindObjectsByType (HideFlags.DontSave) and lives in the framework's own
    // InitTestScene rather than the active one, so ask the framework for it and scan every loaded scene as
    // a fallback.
    public static void KeepTheRunnerAlive()
    {
        var runner = ActiveRunner();
        Assert.IsNotNull(runner, "Could not find the play-mode test runner to protect from the scene load. " +
                                 "Loading a scene now would hang the run, so stopping here instead.");
        Object.DontDestroyOnLoad(runner);
    }

    static GameObject ActiveRunner()
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var type = System.Type.GetType("UnityEngine.TestTools.TestRunner.PlaymodeTestsController, UnityEngine.TestRunner");
        var active = type?.GetProperty("ActiveController", any)?.GetValue(null) as MonoBehaviour;
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

    public static IEnumerator WaitForScene(string name, string pressed = null)
    {
        float until = Time.realtimeSinceStartup + Timeout;
        while (SceneManager.GetActiveScene().name != name)
        {
            if (Time.realtimeSinceStartup > until)
            {
                string what = pressed == null ? $"Loading '{name}'" : $"Pressing {pressed}";
                Assert.Fail($"{what} did not reach '{name}' within {Timeout:0}s — " +
                            $"still in '{SceneManager.GetActiveScene().name}'.");
            }
            yield return null;
        }
        yield return null;   // one more, so the new scene has run its Awake/Start
    }

    // Wait for something the scene builds asynchronously — a track, a paddock, a venue.
    public static IEnumerator WaitFor(System.Func<bool> condition, string complaint, float seconds = Timeout)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > until) Assert.Fail($"{complaint} (waited {seconds:0}s).");
            yield return null;
        }
    }

    // The game's own types, by name. Runtime behaviour lives in Assembly-CSharp; the weekend's rules live
    // in their own assembly (Draftmaster.Weekend), and a test that only looked in one of them would report
    // a missing type as a missing feature.
    static readonly string[] GameAssemblies = { "Assembly-CSharp", "Draftmaster.Weekend", "Draftmaster.Sim" };

    public static System.Type GameType(string name)
    {
        foreach (string assembly in GameAssemblies)
        {
            var type = System.Type.GetType($"{name}, {assembly}");
            if (type != null) return type;
        }
        Assert.Fail($"{name} is in none of: {string.Join(", ", GameAssemblies)} — this test is out of date.");
        return null;
    }
}

