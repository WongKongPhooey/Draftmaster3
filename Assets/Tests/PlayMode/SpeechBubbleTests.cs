using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Speech bubbles, with a camera actually looking at them.
//
// Two faults this covers, both seen in play: several bubbles up at once with no way to tell which line was
// yours, and a bubble hanging off the edge of the screen so the line could not be read. The first is the
// director's job, the second is placement — and placement can only be checked against a real camera and a
// real box, which is why it is here rather than in an EditMode test.
public class SpeechBubbleTests
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    Camera _cam;
    readonly System.Collections.Generic.List<GameObject> _junk = new();
    readonly System.Collections.Generic.List<Camera> _silenced = new();

    [SetUp]
    public void MakeACamera()
    {
        // The bubble places itself against Camera.main, so the scene's own cameras have to stand down or
        // the box is being clamped to a view this test is not measuring.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!cam.enabled) continue;
            cam.enabled = false;
            _silenced.Add(cam);
        }

        var go = new GameObject("TestCamera") { tag = "MainCamera" };
        _cam = go.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = 10f;
        _cam.transform.position = new Vector3(0f, 0f, -20f);
        _junk.Add(go);
    }

    [TearDown]
    public void CleanUp()
    {
        foreach (var go in _junk) if (go != null) Object.DestroyImmediate(go);
        _junk.Clear();

        foreach (var cam in _silenced) if (cam != null) cam.enabled = true;
        _silenced.Clear();
    }

    // A bubble on a speaker at the very edge of the view still has to be readable, so the box is pulled
    // back inside the frame rather than left hanging half off it.
    [UnityTest]
    public IEnumerator ABubbleNeverHangsOffTheEdgeOfTheScreen()
    {
        // Corners and edges — a speaker in any of them used to put their box outside the view.
        var corners = new[]
        {
            new Vector3(0.5f, 0.5f), new Vector3(0.02f, 0.5f), new Vector3(0.98f, 0.5f),
            new Vector3(0.5f, 0.98f), new Vector3(0.5f, 0.02f), new Vector3(0.98f, 0.98f),
        };

        foreach (var viewport in corners)
        {
            var actor = Speaker(viewport, "A line long enough to make a wide box out of, which is exactly " +
                                          "the sort that used to run off the side of the screen.");
            yield return null;
            yield return null;

            var box = FindBubble();
            Assert.IsNotNull(box, "No bubble was created.");

            Vector2 half = HalfSize(box);
            Vector3 centre = box.transform.position;

            var min = _cam.WorldToViewportPoint(centre - new Vector3(half.x, half.y, 0f));
            var max = _cam.WorldToViewportPoint(centre + new Vector3(half.x, half.y, 0f));

            Assert.GreaterOrEqual(min.x, -0.001f, $"Bubble runs off the left at viewport {viewport}.");
            Assert.GreaterOrEqual(min.y, -0.001f, $"Bubble runs off the bottom at viewport {viewport}.");
            Assert.LessOrEqual(max.x, 1.001f, $"Bubble runs off the right at viewport {viewport}.");
            Assert.LessOrEqual(max.y, 1.001f, $"Bubble runs off the top at viewport {viewport}.");

            Object.DestroyImmediate(box.gameObject);
            Object.DestroyImmediate(actor);
            _junk.Remove(actor);
            yield return null;
        }
    }

    // Two NPCs noticing the player at the same moment used to put two boxes on screen. Only one may be up.
    [UnityTest]
    public IEnumerator OnlyOneBubbleIsEverOnScreen()
    {
        var first = Speaker(new Vector3(0.35f, 0.5f), "First thing said.");
        var second = Speaker(new Vector3(0.65f, 0.5f), "Second thing said, over the top of the first.");
        yield return null;

        int visible = 0;
        foreach (var b in Object.FindObjectsByType(PlayModeScenes.GameType("SpeechBubble"),
                                                  FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (((Component)b).gameObject.activeInHierarchy) visible++;

        Assert.AreEqual(1, visible, "Two speakers put two bubbles on screen at once.");

        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
        _junk.Remove(first);
        _junk.Remove(second);
    }

    // ------------------------------------------------------------------ helpers

    // An actor at a viewport position, saying something at conversation priority.
    GameObject Speaker(Vector3 viewport, string line)
    {
        Vector3 world = _cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 20f));
        world.z = 0f;

        var actor = new GameObject("TestSpeaker");
        actor.transform.position = world;
        _junk.Add(actor);

        var bubbleType = PlayModeScenes.GameType("SpeechBubble");
        var attach = bubbleType.GetMethod("Attach", Any);
        var bubble = attach.Invoke(null, new object[] { actor.transform });

        var speak = bubbleType.GetMethod("Speak", Any);
        var priority = System.Enum.Parse(PlayModeScenes.GameType("Draftmaster.Sim.SpeechPriority"), "Conversation");
        speak.Invoke(bubble, new object[] { line, "TESTER", priority, null });
        return actor;
    }

    static Component FindBubble()
    {
        foreach (var b in Object.FindObjectsByType(PlayModeScenes.GameType("SpeechBubble"),
                                                  FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (((Component)b).gameObject.activeInHierarchy) return (Component)b;
        return null;
    }

    // The drawn box, measured off the bubble's own sizing rather than guessed at.
    static Vector2 HalfSize(Component bubble)
    {
        var field = bubble.GetType().GetField("_boxSize", Any);
        var size = (Vector2)field.GetValue(bubble);
        return size * 0.5f;
    }
}
