using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The guard that stops the race scene's package-stripper eating somebody's authoring session.
//
// The stripper exists for a good reason: a track package left in RaceScene.unity pins every race to that one
// track, because TrackSceneLoader adopts a road it finds in the scene and ignores the selection. So saving the
// scene destroys any package instance in it.
//
// The problem was that it destroyed them SILENTLY. Place a weekend marker against the package instance in the
// race scene — which is the natural thing to do, and what the marker gizmo invites, since it draws its handles
// on whatever is selected — and the position lives only as a prefab-instance override. Save the scene and the
// instance is gone, the override with it, and there is nothing in the log, nothing to undo, and nothing on
// disk: the package file is byte-identical to what was committed days ago. That happened, and it cost a
// night's marker placement.
//
// So the rule is now: never destroy an instance carrying edits the package has not got. Keeping it has a
// visible cost — the scene holds a road again — but that state is already loudly detected (there is a test for
// it and a menu item to clear it), whereas deleted authoring is gone for good.
public class RaceSceneSplitterGuardTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/WatkinsGlen.prefab";

    Scene _scene;
    GameObject _instance;

    [SetUp]
    public void SetUp()
    {
        // A preview scene, so nothing here can touch the scenes open in the editor.
        _scene = EditorSceneManager.NewPreviewScene();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath);
        Assert.IsNotNull(asset, $"No track package at {PackagePath} to test the guard against.");

        _instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
        Assert.IsNotNull(_instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void AnUntouchedInstanceIsSafeToStrip()
    {
        // The common case, and the one that has to keep working: a package dropped in to look at, unchanged.
        // A guard that fired on this would mean never stripping anything, and the race scene would go back to
        // being pinned to whichever track was previewed last.
        Assert.IsNull(UnappliedEdits(_instance),
                      "A freshly instantiated package reads as carrying edits, so nothing would ever be " +
                      "stripped and the scene would keep a road in it.");
    }

    [Test]
    public void MovingTheWholeInstanceIsStillSafeToStrip()
    {
        // The splitter positions the instance itself when it drops one into the scene, so the root transform
        // being overridden is the normal state of every preview rather than authoring worth protecting.
        _instance.transform.position = new Vector3(12f, -34f, 0f);

        Assert.IsNull(UnappliedEdits(_instance),
                      "Moving the instance root counts as unapplied work, so a previewed package could never " +
                      "be stripped again.");
    }

    [Test]
    public void AMarkerMovedInsideTheInstanceIsNotSafeToStrip()
    {
        // The exact thing that was lost: a weekend marker dragged to a new position against the scene
        // instance rather than inside the prefab stage.
        var marker = FindChild(_instance, "Grandstand_Marker");
        Assert.IsNotNull(marker, "The package has no Grandstand_Marker to move.");

        Vector3 was = marker.transform.localPosition;
        marker.transform.localPosition = was + new Vector3(0f, -14.6f, 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(marker.transform);

        string edits = UnappliedEdits(_instance);
        Assert.IsNotNull(edits, "A marker moved inside the instance reads as safe to throw away — which is " +
                                "the bug this guard exists to prevent.");
        StringAssert.Contains("Grandstand_Marker", edits,
                              "The report doesn't name what would have been lost, so it tells the author " +
                              "nothing they can act on.");
        StringAssert.Contains("moved", edits);
    }

    [Test]
    public void TheRoadRebuildingItselfIsNotMistakenForAuthoring()
    {
        // TrackBuilder is [ExecuteAlways] and regenerates the road from the spline every time the object is
        // enabled — edge lines, brake markers, pit lane, runoff, barriers, decorations. A package nobody has
        // touched therefore reports eight added GameObjects before anyone has done anything.
        //
        // Counting those as work would jam the guard permanently shut, and the race scene would keep a road
        // in it forever. So the generated half is expected to be there and expected to be ignored.
        var generated = new[] { "LeftEdgeLine", "RightEdgeLine", "BrakeMarkers", "PitLane" };
        int found = 0;
        foreach (string name in generated) if (FindChild(_instance, name) != null) found++;

        Assert.Greater(found, 0, "None of the generated road is present, so this test is no longer proving " +
                                 "anything about ignoring it.");
        Assert.IsNull(UnappliedEdits(_instance),
                      "The generated road reads as unapplied authoring, so no package could ever be stripped.");
    }

    [Test]
    public void AComponentAddedToSomethingInThePackageIsNotSafeToStrip()
    {
        var marker = FindChild(_instance, "Grandstand_Marker");
        Assert.IsNotNull(marker);

        marker.AddComponent<BoxCollider2D>();

        string edits = UnappliedEdits(_instance);
        Assert.IsNotNull(edits, "A component wired onto an object that IS in the package reads as safe to throw away.");
        StringAssert.Contains("BoxCollider2D", edits);
    }

    [Test]
    public void AnObjectThatIsNotAPrefabInstanceIsNeverSafeToStrip()
    {
        // An unpacked package is the worst case of all: there is no asset behind it, so everything in it
        // exists only in this scene and destroying it is unrecoverable.
        PrefabUtility.UnpackPrefabInstance(_instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        string edits = UnappliedEdits(_instance);
        Assert.IsNotNull(edits, "An unpacked package reads as safe to destroy, and nothing in it exists " +
                                "anywhere else on disk.");
        StringAssert.Contains("not a prefab instance", edits);
    }

    [Test]
    public void TheReportStaysShortEnoughToRead()
    {
        // A wall of a hundred lines in the console is the same as no message at all. Most of a track package
        // is generated and therefore invisible to the guard, so the cap is exercised by lowering it rather
        // than by manufacturing hundreds of edits that could not happen.
        int moved = 0;
        foreach (var t in _instance.GetComponentsInChildren<Transform>(true))
        {
            if (t == _instance.transform) continue;
            t.localPosition += new Vector3(0.5f, 0f, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            moved++;
        }
        Assert.GreaterOrEqual(moved, 3, "Nothing in the package could be moved, so the cap isn't being tested.");

        string all = UnappliedEdits(_instance, 99);
        Assert.IsNotNull(all);
        int lines = all.Split('\n').Length;
        Assert.GreaterOrEqual(lines, 3, "Fewer edits were reported than were made.");

        string capped = UnappliedEdits(_instance, 2);
        Assert.IsNotNull(capped);
        Assert.LessOrEqual(capped.Split('\n').Length, 3,
                           "The override report isn't capped, so a badly-edited instance buries its own warning.");
        StringAssert.Contains("more", capped, "The report doesn't say how much it left out.");
    }

    // RaceSceneSplitter lives in Assembly-CSharp-Editor, which this test assembly cannot reference — the
    // same reason every other suite here reaches the runtime through reflection.
    static string UnappliedEdits(GameObject instance, int limit = 10)
    {
        var type = System.AppDomain.CurrentDomain.GetAssemblies()
                         .Select(a => a.GetType("RaceSceneSplitter", false))
                         .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "RaceSceneSplitter is gone, and with it the guard on destroying authoring work.");

        var method = type.GetMethod("UnappliedEdits", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "RaceSceneSplitter has no UnappliedEdits any more — nothing is guarding the " +
                                 "strip, so a package instance carrying edits would be destroyed silently.");

        return (string)method.Invoke(null, new object[] { instance, limit });
    }

    static GameObject FindChild(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
