using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Draws the Texas (FortWorth) package over the calibrated aerial to Temp/texas_layout.png, plus a text dump of the
// geometry, walls, stands and paddock to Temp/texas_layout.txt. A check that needs no play mode and no focused
// scene view: road, pit road, walls, stands, paved areas, buildings and the paddock are filled in their own
// colours over the photo, so anything standing in the wrong place shows at a glance.
//
// The aerial is Assets/RefImages/texas-aerial.png (Josh's top-down capture, NOT committed - Google imagery) with
// the calibration from Assets/RefImages/TexasOverlay.unity: 0.8414 m per pixel, rotated 0.24 degrees, centred at
// (-143.82, -265.85). Without the image the layout is drawn on black.
public static class TexasLayoutReport
{
    public const string TrackId = "FortWorth";
    public const string AerialPath = "Assets/RefImages/texas-aerial.png";
    public const float AerialMetresPerPixel = 0.8414f;
    public const float AerialRotationDeg = 0.24f;
    public static readonly Vector2 AerialCentre = new Vector2(-143.82f, -265.85f);

    const string PngPath = "Temp/texas_layout.png";
    const string TxtPath = "Temp/texas_layout.txt";
    const float MetresPerOutPixel = 1f;

    // Aerial pixel (x right, y DOWN from the top-left, as an image viewer reports it) to world metres.
    public static Vector2 AerialToWorld(Vector2 px, int width, int height)
    {
        var local = new Vector2((px.x - width * 0.5f) * AerialMetresPerPixel, (height * 0.5f - px.y) * AerialMetresPerPixel);
        return AerialCentre + (Vector2)(Quaternion.Euler(0f, 0f, AerialRotationDeg) * local);
    }

    [MenuItem("Draftmaster/Tracks/Texas/Report Layout Over Aerial (Temp png)")]
    public static void Report()
    {
        string path = $"Assets/Resources/TrackPackages/{TrackId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string summary = Render(contents);
            Debug.Log($"[TexasLayoutReport] {summary}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    public static string Render(GameObject contents)
    {
        var package = contents.GetComponent<TrackPackage>();
        var builder = package != null ? package.Builder : contents.GetComponentInChildren<TrackBuilder>(true);
        if (builder == null || builder.track == null) return "no TrackBuilder/geometry in the package";
        var track = builder.track;
        var samples = builder.SampleCenterline();
        var pit = builder.SamplePitCenterline();

        // Canvas: the road's bounds plus a margin wide enough for the stands and the car parks round it.
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        foreach (var s in samples) { min = Vector2.Min(min, s.position); max = Vector2.Max(max, s.position); }
        min -= Vector2.one * 160f; max += Vector2.one * 160f;
        int w = Mathf.CeilToInt((max.x - min.x) / MetresPerOutPixel);
        int h = Mathf.CeilToInt((max.y - min.y) / MetresPerOutPixel);
        var px = new Color32[w * h];

        // The photo, dimmed, as the backdrop.
        var aerial = new Texture2D(2, 2);
        bool haveAerial = File.Exists(AerialPath) && aerial.LoadImage(File.ReadAllBytes(AerialPath));
        if (haveAerial)
        {
            var inv = Quaternion.Euler(0f, 0f, -AerialRotationDeg);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 world = new Vector2(min.x + (x + 0.5f) * MetresPerOutPixel, min.y + (y + 0.5f) * MetresPerOutPixel);
                Vector2 local = inv * (world - AerialCentre);
                float ax = local.x / AerialMetresPerPixel + aerial.width * 0.5f;
                float ayDown = aerial.height * 0.5f - local.y / AerialMetresPerPixel;
                if (ax < 0 || ayDown < 0 || ax >= aerial.width || ayDown >= aerial.height) continue;
                Color c = aerial.GetPixel((int)ax, aerial.height - 1 - (int)ayDown);
                px[y * w + x] = (Color32)(c * 0.75f);
            }
        }
        Object.DestroyImmediate(aerial);

        void Blend(int x, int y, Color32 c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = Color32.Lerp(px[y * w + x], c, a);
        }
        Vector2Int ToPx(Vector2 world) => new Vector2Int(
            Mathf.FloorToInt((world.x - min.x) / MetresPerOutPixel), Mathf.FloorToInt((world.y - min.y) / MetresPerOutPixel));
        void Tri(Vector2 a, Vector2 b, Vector2 c, Color32 col, float alpha)
        {
            Vector2 pa = (a - min) / MetresPerOutPixel, pb = (b - min) / MetresPerOutPixel, pc = (c - min) / MetresPerOutPixel;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
            float area = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = ((pb.x - p.x) * (pc.y - p.y) - (pc.x - p.x) * (pb.y - p.y)) / area;
                float w1 = ((pc.x - p.x) * (pa.y - p.y) - (pa.x - p.x) * (pc.y - p.y)) / area;
                float w2 = 1f - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                Blend(x, y, col, alpha);
            }
        }
        void Line(Vector2 a, Vector2 b, Color32 col)
        {
            float len = Vector2.Distance(a, b) / MetresPerOutPixel;
            int n = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
            for (int i = 0; i <= n; i++)
            {
                var p = ToPx(Vector2.Lerp(a, b, i / (float)n));
                Blend(p.x, p.y, col, 1f);
            }
        }
        void Dot(Vector2 at, Color32 col, int r)
        {
            var c = ToPx(at);
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) Blend(c.x + dx, c.y + dy, col, 1f);
        }
        void Mesh(MeshFilter mf, Color32 col, float alpha)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;
            var v = mesh.vertices;
            var t = mesh.triangles;
            var world = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) world[i] = mf.transform.TransformPoint(v[i]);
            for (int i = 0; i + 2 < t.Length; i += 3) Tri(world[t[i]], world[t[i + 1]], world[t[i + 2]], col, alpha);
        }

        var sb = new StringBuilder();
        var road = new Color32(230, 230, 230, 255);
        var pitCol = new Color32(255, 220, 40, 255);
        var wall = new Color32(255, 40, 40, 255);
        var stand = new Color32(230, 60, 230, 255);
        var tarmac = new Color32(60, 60, 70, 255);
        var building = new Color32(255, 150, 40, 255);
        var screen = new Color32(40, 255, 255, 255);
        var suites = new Color32(255, 255, 255, 255);
        var deck = new Color32(150, 150, 120, 255);

        // Every generated or placed mesh except the scattered scenery, coloured by what it is.
        foreach (var mf in contents.GetComponentsInChildren<MeshFilter>(true))
        {
            string p = PathOf(mf.transform);
            if (p.Contains("/Scenery/")) continue;
            if (p.Contains("Barrier")) Mesh(mf, wall, 0.95f);
            else if (p.Contains("Suites")) Mesh(mf, suites, 0.6f);
            else if (p.Contains("Grandstand")) Mesh(mf, stand, 0.55f);
            else if (p.Contains("BigHoss")) Mesh(mf, screen, 0.9f);
            else if (p.Contains("Deck")) Mesh(mf, deck, 0.6f);
            else if (p.Contains("Building") || p.Contains("Tower") || p.Contains("Garage")) Mesh(mf, building, 0.7f);
            else if (p.Contains("Runoff") || p.Contains("Tarmac") || p.Contains("Paving") || p.Contains("HollerLoop")) Mesh(mf, tarmac, 0.5f);
        }
        // The racing surface and pit road from the spline, so they show even when the builder made no mesh.
        for (int i = 0; i + 1 < samples.Count; i++) Quad(samples[i], samples[i + 1], road, 0.35f);
        if (samples.Count > 2 && track.closedLoop) Quad(samples[samples.Count - 1], samples[0], road, 0.35f);
        for (int i = 0; i + 1 < pit.Count; i++) Quad(pit[i], pit[i + 1], pitCol, 0.45f);
        void Quad(TrackBuilder.Sample a, TrackBuilder.Sample b, Color32 col, float alpha)
        {
            Vector2 al = a.position - a.normal * a.width * 0.5f, ar = a.position + a.normal * a.width * 0.5f;
            Vector2 bl = b.position - b.normal * b.width * 0.5f, br = b.position + b.normal * b.width * 0.5f;
            Tri(al, ar, br, col, alpha); Tri(al, br, bl, col, alpha);
        }

        // Paddock: walkable boundary in cyan, lot boxes in blue, markers and spawns as green dots.
        foreach (var b in contents.GetComponentsInChildren<PaddockBoundary>(true))
        {
            var poly = b.GetComponent<PolygonCollider2D>();
            if (poly == null) continue;
            var pts = poly.points;
            for (int i = 0; i < pts.Length; i++)
                Line(b.transform.TransformPoint(pts[i] + poly.offset), b.transform.TransformPoint(pts[(i + 1) % pts.Length] + poly.offset), new Color32(0, 255, 255, 255));
            sb.AppendLine($"boundary {PathOf(b.transform)} {pts.Length} points, bounds {b.WorldBounds.min:F0}..{b.WorldBounds.max:F0}");
        }
        foreach (var area in contents.GetComponentsInChildren<PaddockLotArea>(true))
        {
            area.GetRect(out Vector3 c, out Quaternion rot, out float aw, out float ad);
            Vector2 ax = rot * Vector3.right * aw * 0.5f, ay = rot * Vector3.up * ad * 0.5f;
            Vector2 cc = c;
            Line(cc - ax - ay, cc + ax - ay, new Color32(60, 120, 255, 255));
            Line(cc + ax - ay, cc + ax + ay, new Color32(60, 120, 255, 255));
            Line(cc + ax + ay, cc - ax + ay, new Color32(60, 120, 255, 255));
            Line(cc - ax + ay, cc - ax - ay, new Color32(60, 120, 255, 255));
            sb.AppendLine($"lot {area.name} {area.kind} centre {cc:F0} {aw:F0}x{ad:F0} rot {rot.eulerAngles.z:F0}");
        }
        var paddockRoot = package != null && package.paddockRoot != null ? package.paddockRoot : contents.transform.Find("Paddock");
        if (paddockRoot != null)
            foreach (Transform t in paddockRoot)
            {
                if (t.GetComponent<PaddockLotArea>() || t.GetComponent<PaddockBoundary>()) continue;
                Dot(t.position, new Color32(40, 255, 80, 255), 3);
                sb.AppendLine($"paddock {t.name} at {(Vector2)t.position:F1} rot {t.eulerAngles.z:F0}");
            }

        // Pit boxes along the box lane, as small white ticks, and the start/finish line in green.
        float pitLen = track.PitLaneLength;
        int boxes = builder.HasPitBoxLane ? builder.ResolvePitBoxCount() : 0;
        if (boxes > 0 && pit.Count > 1)
        {
            float from = builder.PitBoxLaneFrom(pitLen), to = builder.PitBoxLaneTo(pitLen);
            for (int i = 0; i <= boxes; i++)
            {
                var s = builder.SamplePitAt(Mathf.Lerp(from, to, i / (float)boxes), pit);
                Vector2 a = s.position + s.normal * builder.PitBoxLaneInnerLateral;
                Vector2 b = s.position + s.normal * builder.PitBoxLaneOuterLateral;
                Line(a, b, new Color32(255, 255, 255, 255));
            }
            sb.AppendLine($"pit boxes {boxes} from {from:F0} to {to:F0} m of {pitLen:F0} m pit road, side {(builder.PitBoxSide > 0f ? "right" : "left")} of pit travel");
        }
        var sf = builder.SampleAt(track.startFinishDistance, samples);
        Line(sf.position - sf.normal * sf.width * 0.5f, sf.position + sf.normal * sf.width * 0.5f, new Color32(0, 255, 0, 255));
        Dot(samples[0].position, new Color32(0, 255, 0, 255), 2);

        // Text: geometry by segment, walls, stands, paved areas.
        float d = 0f;
        sb.AppendLine($"lap {track.TotalLength():F1} m, start/finish at {track.startFinishDistance:F1} m, width {track.defaultWidth}");
        for (int i = 0; i < track.segments.Length; i++)
        {
            var seg = track.segments[i];
            var a = builder.SampleAt(d, samples);
            sb.AppendLine($"seg {i} '{seg.label}' {seg.type} len {seg.length:F1} angle {seg.angle:F1} bank {seg.banking:F1} width {seg.width:F1} from {d:F0} m at {a.position:F0} heading {Mathf.Atan2(a.tangent.y, a.tangent.x) * Mathf.Rad2Deg:F0}");
            d += seg.length;
        }
        if (track.hasPitLane)
        {
            sb.AppendLine($"pit road {pitLen:F0} m, entry at lap {track.pitEntryDistance:F0}, exit at lap {track.pitExitDistance:F0}, width {track.pitDefaultWidth}");
            for (int i = 0; i < track.pitSegments.Length; i++)
                sb.AppendLine($"  pit seg {i} {track.pitSegments[i].type} len {track.pitSegments[i].length:F1} angle {track.pitSegments[i].angle:F1}");
            if (pit.Count > 0) sb.AppendLine($"  pit start {pit[0].position:F0} end {pit[pit.Count - 1].position:F0}");
        }
        var envBuilder = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
        if (envBuilder != null && envBuilder.environment != null)
        {
            var env = envBuilder.environment;
            sb.AppendLine($"walls inner(right)={env.innerSideBarrier} off {env.innerEdgeOffset} outer(left)={env.outerSideBarrier} off {env.outerEdgeOffset}");
            foreach (var g in env.barrierGaps ?? new TrackEnvironment.BarrierGap[0])
                sb.AppendLine($"  gap '{g.label}' {g.side} seg {g.segmentIndex} {g.startDistance:F0}..{g.endDistance:F0}");
            foreach (var m in env.manualSections ?? new TrackEnvironment.ManualBarrierSection[0])
                sb.AppendLine($"  manual '{m.label}' {m.side} {m.manualPoints?.Length ?? 0} points");
            foreach (var r in env.runoffAreas ?? new TrackEnvironment.RunoffArea[0])
                sb.AppendLine($"  runoff '{r.label}' {r.surface} {r.points?.Length ?? 0} points");
            foreach (var s in env.strips ?? new TrackEnvironment.Strip[0])
                sb.AppendLine($"  strip '{s.label}' segs {s.startSegmentIndex}..{s.endSegmentIndex} off {s.lateralOffset} width {s.width}");
        }
        foreach (var g in contents.GetComponentsInChildren<Grandstand>(true))
            sb.AppendLine($"stand {g.name} at {(Vector2)g.transform.position:F0} len {g.length:F0} depth {g.depth:F0} bend {g.bendDegrees:F1} rot {g.transform.eulerAngles.z:F0}");

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        Directory.CreateDirectory("Temp");
        File.WriteAllBytes(PngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        sb.Insert(0, $"canvas {w}x{h} px at {MetresPerOutPixel} m/px, world {min:F0}..{max:F0} (+y up = image up), aerial {(haveAerial ? "drawn" : "MISSING")}\n");
        File.WriteAllText(TxtPath, sb.ToString());
        return $"wrote {PngPath} ({w}x{h}) and {TxtPath}";
    }

    // The package as the game draws it: instantiated into a throwaway preview scene (nothing opened or saved) and
    // shot from straight above with an orthographic camera - the whole venue, then closer views of the front
    // stretch, turn 1/2 and the back stretch. Temp/texas_preview_*.png.
    [MenuItem("Draftmaster/Tracks/Texas/Capture Rendered Preview (Temp png)")]
    public static void CapturePreview()
    {
        Debug.Log("[TexasLayoutReport] " + Capture(new[]
        {
            ("whole", new Vector2(-110f, -255f), 760f),
            ("frontstretch", new Vector2(-110f, -60f), 260f),
            ("turn1", new Vector2(-560f, -230f), 300f),
            ("backstretch", new Vector2(-130f, -540f), 200f),
            ("paddock", new Vector2(-60f, -160f), 160f),
        }));
    }

    public static string Capture((string name, Vector2 centre, float viewMetres)[] shots)
    {
        var prefab = Resources.Load<GameObject>("TrackPackages/" + TrackId);
        if (prefab == null) return $"No package Resources/TrackPackages/{TrackId}.";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var written = new List<string>();
        try
        {
            var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            foreach (var env in pkg.GetComponentsInChildren<TrackEnvironmentBuilder>(true)) env.Build();

            var camGo = new GameObject("PreviewCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.GetComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.magenta;   // anything the package doesn't cover shows up loudly
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 500f;
            const int w = 1600, h = 1000;
            foreach (var shot in shots)
            {
                cam.orthographicSize = shot.viewMetres * 0.5f;
                camGo.transform.position = new Vector3(shot.centre.x, shot.centre.y, -100f);
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
                    string path = $"Temp/texas_preview_{shot.name}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    written.Add(path);
                }
                finally
                {
                    cam.targetTexture = null;
                    RenderTexture.active = prev;
                    Object.DestroyImmediate(rt);
                }
            }
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        return "captured " + string.Join(", ", written);
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
