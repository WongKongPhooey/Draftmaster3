#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Draftmaster > Tracks > Capture Barrier Preview (Watkins Glen)
//
// Looks at a track's barriers without opening anything for editing: the package is instantiated into a
// throwaway preview scene, its environment is built, an orthographic camera is pointed at one barrier piece,
// and the frame is written to Temp/barrier_preview.png. Nothing is saved and no prefab stage is opened.
public static class TrackBarrierPreview
{
    [MenuItem("Draftmaster/Tracks/Capture Barrier Preview (Watkins Glen)", priority = 400)]
    public static void CaptureMenu()
    {
        // An auto piece each side, and a hand-drawn one (whose points can run either way round).
        Debug.Log(Capture("WatkinsGlen", "Barrier_Outer_", 14f, "Temp/barrier_preview.png"));
        Debug.Log(Capture("WatkinsGlen", "Barrier_Inner_", 14f, "Temp/barrier_preview_inner.png"));
        Debug.Log(Capture("WatkinsGlen", "_Manual_", 14f, "Temp/barrier_preview_manual.png"));
    }

    // The camera centres on the first barrier piece whose name contains `piece`.
    // `viewMetres` is the height of the frame.
    public static string Capture(string trackId, string piece, float viewMetres, string outPath)
    {
        var prefab = Resources.Load<GameObject>("TrackPackages/" + trackId);
        if (prefab == null) return $"No package Resources/TrackPackages/{trackId}.";

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var env = pkg.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (env == null) return $"{trackId} has no TrackEnvironmentBuilder.";
            env.Build();

            Transform target = null;
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains(piece) && t.GetComponentInChildren<Renderer>() != null) { target = t; break; }
            if (target == null) return $"No barrier piece '{piece}' in {trackId}.";

            // A point ON the wall — the middle vertex of its first strip. A piece can run for hundreds of metres
            // round a bend, so the middle of its bounds may be nowhere near it.
            var mf = target.GetComponentInChildren<MeshFilter>();
            var verts = mf.sharedMesh.vertices;
            var bounds = new Bounds(mf.transform.TransformPoint(verts[verts.Length / 2]), Vector3.zero);

            var camGo = new GameObject("PreviewCamera", typeof(Camera));
            SceneManager_Move(camGo, scene);
            var cam = camGo.GetComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = viewMetres * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.magenta;   // anything not covered by the package shows up loudly
            camGo.transform.position = new Vector3(bounds.center.x, bounds.center.y, -50f);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prev;
                Object.DestroyImmediate(rt);
            }
            return $"Captured {Path.GetFullPath(outPath)} at {bounds.center}";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
        => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
}
#endif
