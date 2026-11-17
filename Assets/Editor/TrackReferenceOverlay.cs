using System.IO;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Unity end of Tools/track_reference.py: puts the venue's real aerial (USGS NAIP) under or over its track
// package, already lined up, so a track can be checked or hand-reshaped against the real thing without
// calibrating a photo by hand (what TexasOverlay.unity needed).
//
// Run `python -I Tools/track_reference.py <TrackId>` first: it fetches the aerial, fits the game's centreline
// onto the real one and writes Tools/TrackReference/<id>/cache/alignment.json - the aerial's four corners in
// package-local metres. This reads those corners back (TrackReferenceAlignment) and places a
// TrackReferenceImage sprite there.
//
// The overlay goes in a NEW, UNSAVED scene with the package dropped in at the origin - never in RaceScene,
// whose save hook folds whatever is on a track instance into the package (RaceSceneSplitter). The image is
// loaded from the git-ignored cache at run time and is not an asset, so saving the overlay scene keeps the
// placement but not the picture; re-run the menu instead.
public static class TrackReferenceOverlay
{
    const string AerialName = "ReferenceAerial";

    static string Folder(string id) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "TrackReference", id));

    [System.Serializable]
    class Corner { public float x, y; }

    [System.Serializable]
    class Alignment
    {
        public string trackId, image, fittedTo;
        public int width, height;
        public float scale, rmsM;
        public Corner topLeft, topRight, bottomRight, bottomLeft;
    }

    [MenuItem("Draftmaster/Tracks/Reference/Open Aerial Overlay For Selected Track", priority = 450)]
    static void OpenForSelected()
    {
        string id = TrackSelection.CurrentId;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!Build(scene, id, out string why))
        {
            EditorUtility.DisplayDialog("Track reference", why, "OK");
            return;
        }
        var aerial = GameObject.Find(AerialName);
        Selection.activeGameObject = aerial;
        var view = SceneView.lastActiveSceneView;
        if (view != null && aerial != null)
        {
            view.in2DMode = true;
            view.Frame(aerial.GetComponent<SpriteRenderer>().bounds, false);
        }
    }

    [MenuItem("Draftmaster/Tracks/Reference/Toggle Aerial Over Or Under The Track", priority = 451)]
    static void ToggleOverUnder()
    {
        var img = Object.FindFirstObjectByType<TrackReferenceImage>();
        if (img == null) { Debug.LogWarning("Track reference: no reference aerial in the open scene."); return; }
        bool over = img.sortingOrder < 0;
        Undo.RecordObjects(new Object[] { img, img.transform, img.GetComponent<SpriteRenderer>() }, "Toggle aerial");
        SetLayer(img, over);
    }

    [MenuItem("Draftmaster/Tracks/Reference/Open Report For Selected Track", priority = 452)]
    static void OpenReport()
    {
        string folder = Folder(TrackSelection.CurrentId);
        string report = Path.Combine(folder, "report.md");
        if (!File.Exists(report))
        {
            EditorUtility.DisplayDialog("Track reference",
                $"No report for {TrackSelection.CurrentId} yet. Run:\n\npython -I Tools/track_reference.py {TrackSelection.CurrentId}", "OK");
            return;
        }
        EditorUtility.OpenWithDefaultApp(report);
        string overview = Path.Combine(folder, "cache", "overview_small.jpg");
        if (File.Exists(overview)) EditorUtility.OpenWithDefaultApp(overview);
    }

    /// <summary>Drop track <paramref name="id"/>'s package and its aligned aerial into <paramref name="scene"/>.</summary>
    public static bool Build(Scene scene, string id, out string why)
    {
        why = null;
        string folder = Folder(id);
        string alignPath = Path.Combine(folder, "cache", "alignment.json");
        if (!File.Exists(alignPath))
        {
            why = $"No aligned aerial for {id}. Run:\n\npython -I Tools/track_reference.py {id}\n\n" +
                  "(US venues only - NAIP imagery - and only where OSM gives a real centreline to fit to.)";
            return false;
        }
        var a = JsonUtility.FromJson<Alignment>(File.ReadAllText(alignPath));
        var placement = TrackReferenceAlignment.FromCorners(V(a.topLeft), V(a.topRight), V(a.bottomRight), V(a.bottomLeft),
                                                            a.width, a.height);
        if (placement.mirrored) { why = $"{alignPath} describes a mirrored image; re-run the report."; return false; }
        string imagePath = Path.Combine(folder, "cache", a.image);
        if (!File.Exists(imagePath)) { why = $"Missing {imagePath}; re-run the report."; return false; }

        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = id + " aerial", hideFlags = HideFlags.DontSave };
        if (!tex.LoadImage(File.ReadAllBytes(imagePath))) { why = $"Could not read {imagePath}."; return false; }
        tex.filterMode = FilterMode.Bilinear;
        if (tex.width != a.width || tex.height != a.height)
        {
            why = $"{a.image} is {tex.width}x{tex.height}, the alignment expects {a.width}x{a.height}; re-run the report.";
            Object.DestroyImmediate(tex);
            return false;
        }

        var prefab = TrackCatalog.Package(id);
        if (prefab != null)
        {
            var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            pkg.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        else Debug.LogWarning($"Track reference: {id} has no package; showing the aerial alone.");

        // One sprite unit = one pixel; the transform scales a pixel to metresPerPixel. Root object, so its
        // world position is package-local metres (the package sits at the origin).
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.DontSave;
        sprite.name = tex.name;
        var go = new GameObject(AerialName);
        SceneManager.MoveGameObjectToScene(go, scene);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        var img = go.AddComponent<TrackReferenceImage>();
        img.image = sprite;
        img.metresPerPixel = placement.metresPerPixel;
        img.rotationDeg = placement.rotationDeg;
        img.worldOffset = placement.centre;
        go.transform.localScale = new Vector3(placement.metresPerPixel, placement.metresPerPixel, 1f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, placement.rotationDeg);
        SetLayer(img, over: true);

        Debug.Log($"Track reference: {id} aerial placed ({placement.metresPerPixel:0.###} m/px, " +
                  $"{placement.rotationDeg:0.##} deg, fitted to {a.fittedTo}, game line within {a.rmsM:0.#} m RMS). " +
                  "Draftmaster > Tracks > Reference > Toggle Aerial Over Or Under The Track to swap layers.");
        return true;
    }

    // Over: translucent on top, for lining things up (how Josh fitted Texas). Under: opaque between the grass
    // (z 0.1, sorting -30) and the road (z 0, sorting 0).
    static void SetLayer(TrackReferenceImage img, bool over)
    {
        var sr = img.GetComponent<SpriteRenderer>();
        img.sortingOrder = over ? 100 : -20;
        img.opacity = over ? 0.35f : 1f;
        sr.sortingOrder = img.sortingOrder;
        sr.color = new Color(1f, 1f, 1f, img.opacity);
        var p = img.worldOffset;
        img.transform.position = new Vector3(p.x, p.y, over ? -1f : 0.05f);
    }

    static Vector2 V(Corner c) => c == null ? Vector2.zero : new Vector2(c.x, c.y);
}
