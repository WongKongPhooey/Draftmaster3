using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scatters misc scenery — fans' motorhomes, spectators, and greenery once it's drawn — over the empty grass
// round a track, into the package's Environment/Scenery root.
//
// The layout is SceneryLayout's (pure, unit tested). This file's job is measuring the package for it: the
// road and pit lane off the TrackBuilder, the ground off the ground plane, and everything already in the
// package — stands, paddock, the player's RV, placed NPCs, venue markers, any hand-placed prop — as ground
// to keep off. What comes out are SceneryPiece objects: small saved recipes that draw themselves on enable.
//
// Like TrackDressingFactory, it only ever replaces what it made: a re-scatter clears the unlocked pieces
// under Environment/Scenery and nothing else.
public static class TrackSceneryGenerator
{
    const string PackageDir = "Assets/Resources/TrackPackages";
    const string PaletteAssetPath = "Assets/Resources/Tracks/SceneryPalette.asset";
    public const string SceneryName = "Scenery";

    const float SpriteZ = -0.2f;      // towards the camera from the grass (+0.1) and the road (0)
    const float HugeRenderer = 250_000f;   // m^2 — a renderer this big is ground or road, not a prop

    // ---------------------------------------------------------------- menu

    [MenuItem("Draftmaster/Tracks/Scenery/Scatter Scenery (Open or Selected Package)")]
    static void ScatterSelectedMenu() => Report(RunOnOpenOrSelected(clear: false));

    [MenuItem("Draftmaster/Tracks/Scenery/Clear Scenery (Open or Selected Package)")]
    static void ClearSelectedMenu() => Report(RunOnOpenOrSelected(clear: true));

    [MenuItem("Draftmaster/Tracks/Scenery/Scatter Scenery On Every Package")]
    static void ScatterAllMenu()
    {
        var lines = new List<string>();
        foreach (var row in TrackCatalog.All)
            if (File.Exists($"{PackageDir}/{row.Name}.prefab")) lines.Add(ScatterPackage(row.Name));
        Report(lines.Count == 0 ? "Scenery: no packages built yet." : string.Join("\n", lines));
    }

    [MenuItem("Draftmaster/Tracks/Scenery/Create Default Scenery Palette")]
    static void CreatePaletteMenu()
    {
        var palette = EnsureDefaultPalette();
        Selection.activeObject = palette;
        Report($"Scenery palette at {AssetDatabase.GetAssetPath(palette)} ({palette.entries.Length} entries).");
    }

    // A package open on a prefab stage (Edit In Context, or opened directly) is scattered in place and left
    // for you to save; otherwise the package TrackSelection names is loaded, scattered and saved.
    static string RunOnOpenOrSelected(bool clear)
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var stagePackage = stage != null ? stage.prefabContentsRoot.GetComponent<TrackPackage>() : null;
        if (stagePackage != null)
        {
            string text = clear ? Clear(stagePackage.gameObject) : Scatter(stagePackage.gameObject);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            return text + " (open stage — save it to keep)";
        }
        return clear ? ClearPackage(TrackSelection.CurrentId) : ScatterPackage(TrackSelection.CurrentId);
    }

    public static string ScatterPackage(string trackId) => EditPackage(trackId, Scatter);
    public static string ClearPackage(string trackId) => EditPackage(trackId, Clear);

    static string EditPackage(string trackId, System.Func<GameObject, string> edit)
    {
        string path = $"{PackageDir}/{trackId}.prefab";
        if (!File.Exists(path)) return $"{trackId}: no package at {path}.";

        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string text = edit(contents);
            // The pieces drew themselves on load; their art is DontSave, but strip it anyway so nothing
            // generated can ever be baked into the asset.
            foreach (var piece in contents.GetComponentsInChildren<SceneryPiece>(true)) piece.ClearArt();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return text;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------------------------------------------------------------- the scatter

    public static string Clear(GameObject packageRoot)
    {
        var scenery = FindScenery(packageRoot);
        if (scenery == null) return $"{Id(packageRoot)}: no scenery to clear.";
        int removed = ClearUnlocked(scenery.transform);
        return $"{Id(packageRoot)}: cleared {removed} scenery pieces (locked ones kept).";
    }

    public static string Scatter(GameObject packageRoot)
    {
        string id = Id(packageRoot);
        var package = packageRoot.GetComponent<TrackPackage>();
        var builder = package != null ? package.Builder : packageRoot.GetComponentInChildren<TrackBuilder>(true);
        if (builder == null || builder.track == null) return $"{id}: no TrackBuilder/geometry — nothing to measure.";

        var scenery = EnsureScenery(packageRoot, package);
        var palette = scenery.Palette;
        if (palette == null) palette = EnsureDefaultPalette();

        // Entries that can actually draw something, and the layout's view of them.
        var usable = new List<int>();
        for (int i = 0; i < palette.entries.Length; i++)
            if (palette.entries[i] != null && palette.entries[i].enabled && palette.entries[i].HasArt) usable.Add(i);
        var skipped = palette.entries.Where((e, i) => e != null && e.enabled && !e.HasArt).Select(e => e.name).ToList();

        int removed = ClearUnlocked(scenery.transform);

        var root = packageRoot.transform;
        var req = new SceneryRequest
        {
            seed = scenery.seed,
            roadClearance = scenery.roadClearance,
            campScale = palette.campScale,
            campCoverage = palette.campCoverage,
            maxPieces = scenery.maxPieces - scenery.GetComponentsInChildren<SceneryPiece>(true).Length,
            lapLength = builder.track.TotalLength(),
        };
        req.kinds = usable.Select(i => ToKind(palette.entries[i], scenery.density)).ToArray();
        for (int k = 0; k < usable.Count; k++)
        {
            string follower = palette.entries[usable[k]].followers;
            req.kinds[k].followerKind = usable.FindIndex(i => palette.entries[i].name == follower && !string.IsNullOrEmpty(follower));
        }

        Measure(req, root, builder, scenery);
        var result = SceneryLayout.Solve(req);

        var lines = new List<string>();
        foreach (var p in result.placements)
        {
            var entry = palette.entries[usable[p.kind]];
            Build(scenery.transform, root, entry, p);
        }
        for (int k = 0; k < usable.Count; k++)
            lines.Add($"{result.placed[k]} {palette.entries[usable[k]].name}" +
                      (result.wanted[k] > 0 ? $" (wanted {result.wanted[k]})" : ""));

        string text = $"{id}: scattered {result.placements.Count} pieces — {string.Join(", ", lines)}";
        if (removed > 0) text += $"; replaced {removed}";
        if (skipped.Count > 0) text += $"; no art yet for {string.Join(", ", skipped)}";
        return text + ".";
    }

    static SceneryKind ToKind(SceneryPalette.Entry e, float density)
    {
        Vector2 fp = e.Footprint;
        return new SceneryKind
        {
            perKm = e.perKm * Mathf.Max(0f, density),
            perHectare = e.perHectare * Mathf.Max(0f, density),
            length = fp.x,
            width = fp.y,
            minSetback = e.minSetback,
            maxSetback = Mathf.Max(e.minSetback, e.maxSetback),
            spacing = e.spacing,
            campsOnly = e.campsOnly,
            facing = e.facing,
            angleJitter = e.angleJitter,
            turnChance = e.turnChance,
            followerKind = -1,
            followersMin = e.followersMin,
            followersMax = e.followersMax,
            followerReach = e.followerReach,
        };
    }

    // ---------------------------------------------------------------- measuring the package
    //
    // Everything is worked in the package root's own 2D space, so a package scattered on a stage that sits
    // somewhere in the race scene lays out the same as one loaded at the origin.

    static void Measure(SceneryRequest req, Transform root, TrackBuilder builder, TrackScenery scenery)
    {
        Vector2 ToRoot(Transform from, Vector2 p) => root.InverseTransformPoint(from.TransformPoint(new Vector3(p.x, p.y, 0f)));
        Vector2 DirToRoot(Transform from, Vector2 d) => root.InverseTransformDirection(from.TransformDirection(new Vector3(d.x, d.y, 0f)));

        var main = builder.SampleCenterline();
        foreach (var s in main)
            req.road.Add(new SceneryEdge(ToRoot(builder.transform, s.position), DirToRoot(builder.transform, s.tangent).normalized, s.width * 0.5f));

        // The pit lane, box lane included, is kept clear rather than measured from — scenery should line the
        // racing surface, not crowd the pit wall.
        float boxLane = builder.HasPitBoxLane ? builder.pitBoxLaneWidth : 0f;
        foreach (var s in builder.SamplePitCenterline())
            req.keepClear.Add(new SceneryEdge(ToRoot(builder.transform, s.position), Vector2.zero,
                                              s.width * 0.5f + boxLane + scenery.pitClearance));

        req.area = GroundArea(root, builder, req, scenery.groundInset);

        var sceneryRoot = scenery.transform;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || t.IsChildOf(sceneryRoot) || UnderGenerator(t)) continue;

            // Hand-drawn run-off — tarmac aprons, gravel traps — is in the track's local space. Grass areas
            // are just grass, and fair game.
            var env = t.GetComponent<TrackEnvironmentBuilder>();
            if (env != null && env.environment != null && env.environment.runoffAreas != null)
            {
                var space = env.track != null ? env.track.transform : builder.transform;
                foreach (var area in env.environment.runoffAreas)
                {
                    if (area.points == null || area.points.Length < 3 || area.surface == TrackEnvironment.SurfaceType.Grass) continue;
                    req.blockedPolygons.Add(new SceneryPolygon(area.points.Select(q => ToRoot(space, q)).ToArray(), scenery.propClearance));
                }
                continue;
            }

            // Free-placed roads (service roads, the Watkins paddock road): kept clear like the pit lane.
            var extra = t.GetComponent<ExtraTrackSpline>();
            if (extra != null)
            {
                foreach (var s in extra.SampleLocal())
                    req.keepClear.Add(new SceneryEdge(ToRoot(t, s.position), Vector2.zero, s.width * 0.5f + scenery.roadClearance));
                continue;
            }

            var stand = t.GetComponent<Grandstand>();
            if (stand != null) { req.blockedRects.Add(Pad(OrientedBounds(root, t, stand.length, stand.depth), scenery.propClearance + 4f)); continue; }

            if (t.GetComponent<PaddockBoundary>() != null || t.GetComponent<PaddockLotArea>() != null)
            {
                var r = ColliderBounds(root, t);
                if (r.HasValue) req.blockedRects.Add(Pad(r.Value, scenery.boundaryClearance));
            }

            // The player's RV and the start markers: the drivers' motorhome lot and the team garages are laid
            // out from here at play time, so the clearance is sized for the lot, not for the rig.
            if (t.GetComponent<PlayerSpawnPoint>() != null || t.GetComponent<RVExterior>() != null ||
                t.name == "RV" || t.name == "SpawnPoint_RV")
                req.blockedCircles.Add(new SceneryCircle(ToRoot(t, Vector2.zero), scenery.paddockClearance));
            else if (t.GetComponent<PlacedNPC>() != null || t.name.EndsWith("_Marker"))
                req.blockedCircles.Add(new SceneryCircle(ToRoot(t, Vector2.zero), scenery.markerClearance));

            foreach (var r in t.GetComponents<Renderer>())
            {
                var b = RendererBounds(root, r);
                if (b.HasValue && b.Value.width * b.Value.height < HugeRenderer)
                    req.blockedRects.Add(Pad(b.Value, scenery.propClearance));
            }

            foreach (var c in t.GetComponents<Collider2D>())
            {
                if (c.isTrigger) continue;
                var b = ColliderBounds(root, t);
                if (b.HasValue && b.Value.width * b.Value.height < HugeRenderer)
                    req.blockedRects.Add(Pad(b.Value, scenery.propClearance));
                break;
            }
        }

        // Locked pieces stay, and are planted round.
        foreach (var piece in sceneryRoot.GetComponentsInChildren<SceneryPiece>(true))
        {
            if (!piece.locked) continue;
            var p = ToRoot(piece.transform, Vector2.zero);
            float r = Mathf.Max(1f, piece.size.magnitude * 0.5f);
            req.blockedCircles.Add(new SceneryCircle(p, r + 1f));
        }
    }

    // The ground plane's footprint if there is one (generated TrackGround, or Watkins' bare "Ground" quad),
    // else the road's bounding box with the same margin TrackGround uses.
    static Rect GroundArea(Transform root, TrackBuilder builder, SceneryRequest req, float inset)
    {
        Rect? ground = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<TrackGround>() == null && t.name != "Ground") continue;
            var r = t.GetComponent<Renderer>();
            var b = r != null ? RendererBounds(root, r) : null;
            if (b.HasValue) ground = ground.HasValue ? Union(ground.Value, b.Value) : b.Value;
        }

        if (!ground.HasValue)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var e in req.road)
            {
                minX = Mathf.Min(minX, e.position.x - e.halfWidth); maxX = Mathf.Max(maxX, e.position.x + e.halfWidth);
                minY = Mathf.Min(minY, e.position.y - e.halfWidth); maxY = Mathf.Max(maxY, e.position.y + e.halfWidth);
            }
            const float margin = 150f;
            ground = Rect.MinMaxRect(minX - margin, minY - margin, maxX + margin, maxY + margin);
        }

        var g = ground.Value;
        return Rect.MinMaxRect(g.xMin + inset, g.yMin + inset, g.xMax - inset, g.yMax - inset);
    }

    // Under one of the [ExecuteAlways] builders: their output is the road, its walls and its ground, all of
    // which the layout already knows about from the spline. (A Grandstand's own transform is not "under" it.)
    static bool UnderGenerator(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
            if (p.GetComponent<TrackBuilder>() != null || p.GetComponent<TrackGround>() != null ||
                p.GetComponent<TrackEnvironmentBuilder>() != null || p.GetComponent<Grandstand>() != null ||
                p.GetComponent<ExtraTrackSpline>() != null || p.GetComponent<TrackOverpass>() != null)
                return true;
        return t.GetComponent<TrackBuilder>() != null || t.GetComponent<TrackGround>() != null ||
               t.GetComponent<TrackOverpass>() != null;
    }

    static Rect? RendererBounds(Transform root, Renderer r)
    {
        var b = r.bounds;
        if (b.size.sqrMagnitude < 1e-6f) return null;
        return BoxToRoot(root, b.center, b.extents);
    }

    // Collider2D.bounds needs a physics scene that has stepped, which a prefab loaded for editing doesn't
    // have — so the shapes are read off their own fields.
    static Rect? ColliderBounds(Transform root, Transform t)
    {
        var pts = new List<Vector2>();
        foreach (var c in t.GetComponents<Collider2D>())
        {
            switch (c)
            {
                case BoxCollider2D box:
                {
                    Vector2 h = box.size * 0.5f;
                    foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                        pts.Add(box.offset + Vector2.Scale(h, s));
                    break;
                }
                case CircleCollider2D circle:
                {
                    float r = circle.radius;
                    pts.Add(circle.offset + new Vector2(-r, -r)); pts.Add(circle.offset + new Vector2(r, r));
                    pts.Add(circle.offset + new Vector2(-r, r)); pts.Add(circle.offset + new Vector2(r, -r));
                    break;
                }
                case PolygonCollider2D poly:
                    foreach (var p in poly.points) pts.Add(poly.offset + p);
                    break;
            }
        }
        if (pts.Count == 0) return null;
        return PointsToRoot(root, t, pts);
    }

    // A rectangle `length` x `depth` centred on t, in t's local XY.
    static Rect OrientedBounds(Transform root, Transform t, float length, float depth)
    {
        float hx = length * 0.5f, hy = depth * 0.5f;
        return PointsToRoot(root, t, new List<Vector2>
        {
            new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy),
        });
    }

    static Rect PointsToRoot(Transform root, Transform t, List<Vector2> local)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in local)
        {
            Vector2 q = root.InverseTransformPoint(t.TransformPoint(new Vector3(p.x, p.y, 0f)));
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    static Rect BoxToRoot(Transform root, Vector3 centre, Vector3 extents)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            var w = centre + new Vector3((i & 1) == 0 ? -extents.x : extents.x, (i & 2) == 0 ? -extents.y : extents.y, 0f);
            Vector2 q = root.InverseTransformPoint(w);
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    static Rect Pad(Rect r, float pad) => Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
    static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    // ---------------------------------------------------------------- building pieces

    static void Build(Transform sceneryRoot, Transform packageRoot, SceneryPalette.Entry entry, SceneryPlacement p)
    {
        var rng = new System.Random(p.seed);
        GameObject go;
        if (entry.look == SceneryPalette.Look.Prefab)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, sceneryRoot);
            go.name = $"{entry.prefab.name}";
        }
        else
        {
            go = new GameObject(entry.name);
            go.transform.SetParent(sceneryRoot, false);
        }

        float z = entry.look == SceneryPalette.Look.Spectator ? SpriteZ - 0.05f : SpriteZ;
        go.transform.position = packageRoot.TransformPoint(new Vector3(p.position.x, p.position.y, z));
        go.transform.rotation = packageRoot.rotation * Quaternion.Euler(0f, 0f, p.angle);

        var piece = go.GetComponent<SceneryPiece>();
        if (piece == null) piece = go.AddComponent<SceneryPiece>();
        piece.kind = entry.name;
        piece.look = entry.look;
        piece.sortingOrder = entry.sortingOrder;
        piece.size = entry.look == SceneryPalette.Look.Spectator ? Vector2.zero : entry.size;
        piece.outfitSeed = rng.Next();

        if (entry.look == SceneryPalette.Look.Sprites)
        {
            var sprites = entry.sprites.Where(s => s != null).ToArray();
            piece.sprite = sprites[rng.Next(sprites.Length)];
            piece.tint = entry.pastelTint ? Color.HSVToRGB((float)rng.NextDouble(), 0.22f, 0.95f) : Color.white;
        }
        piece.Rebuild();
    }

    // ---------------------------------------------------------------- roots and palette

    static TrackScenery FindScenery(GameObject packageRoot) => packageRoot.GetComponentInChildren<TrackScenery>(true);

    static TrackScenery EnsureScenery(GameObject packageRoot, TrackPackage package)
    {
        var found = FindScenery(packageRoot);
        if (found != null) return found;

        Transform environment = package != null && package.environmentRoot != null
            ? package.environmentRoot
            : packageRoot.transform.Find("Environment");
        if (environment == null)
        {
            environment = new GameObject("Environment").transform;
            environment.SetParent(packageRoot.transform, false);
            if (package != null) package.environmentRoot = environment;
        }

        var go = new GameObject(SceneryName);
        go.transform.SetParent(environment, false);
        return go.AddComponent<TrackScenery>();
    }

    static int ClearUnlocked(Transform sceneryRoot)
    {
        int removed = 0;
        for (int i = sceneryRoot.childCount - 1; i >= 0; i--)
        {
            var child = sceneryRoot.GetChild(i);
            var piece = child.GetComponent<SceneryPiece>();
            if (piece == null || piece.locked) continue;
            Object.DestroyImmediate(child.gameObject);
            removed++;
        }
        return removed;
    }

    // The shared palette, created with the defaults if it isn't there yet. The motorhome art is the
    // paddock lot's own three rigs (Resources/Environment), so the fans' campers match the drivers'.
    public static SceneryPalette EnsureDefaultPalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<SceneryPalette>(PaletteAssetPath);
        if (palette != null) return palette;

        palette = ScriptableObject.CreateInstance<SceneryPalette>();
        palette.entries = DefaultEntries();
        Directory.CreateDirectory(Path.GetDirectoryName(PaletteAssetPath));
        AssetDatabase.CreateAsset(palette, PaletteAssetPath);
        AssetDatabase.SaveAssets();
        return palette;
    }

    public static SceneryPalette.Entry[] DefaultEntries()
    {
        var motorhomes = new[] { "motorhome", "motorhome2", "motorhome3" }
            .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Environment/{n}.png"))
            .Where(s => s != null).ToArray();

        // Order matters: kinds are placed in palette order against one piece cap, so the fence-line crowd goes
        // first and can't be starved by a superspeedway's acres of camping.
        return new[]
        {
            new SceneryPalette.Entry
            {
                name = "Spectators", look = SceneryPalette.Look.Spectator,
                perKm = 12f, minSetback = 14f, maxSetback = 40f, spacing = 0.4f,
                facing = SceneryFacing.FaceTrack, angleJitter = 25f,
                followers = "Spectators", followersMin = 1, followersMax = 4, followerReach = 2.5f,
                sortingOrder = 4,
            },
            new SceneryPalette.Entry
            {
                name = "Fan Motorhomes", look = SceneryPalette.Look.Sprites, sprites = motorhomes,
                size = new Vector2(10f, 3.75f),        // the drivers' lot's own rig size
                pastelTint = true,
                perKm = 0f, perHectare = 18f, minSetback = 30f, maxSetback = 130f, spacing = 2.5f, campsOnly = true,
                facing = SceneryFacing.AlongTrack, angleJitter = 6f, turnChance = 0.4f,
                followers = "Spectators", followersMin = 0, followersMax = 3, followerReach = 3f,
                sortingOrder = 2,
            },
            new SceneryPalette.Entry
            {
                name = "Greenery", look = SceneryPalette.Look.Sprites, sprites = new Sprite[0],
                perKm = 0f, perHectare = 4f, minSetback = 20f, maxSetback = 140f, spacing = 1f,
                facing = SceneryFacing.Fixed,
                sortingOrder = 3,
            },
        };
    }

    static string Id(GameObject packageRoot)
    {
        var package = packageRoot.GetComponent<TrackPackage>();
        return package != null && !string.IsNullOrEmpty(package.trackId) ? package.trackId : packageRoot.name;
    }

    static void Report(string text)
    {
        Debug.Log($"TrackScenery:\n{text}");
        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "track-tools.txt"), text);
        }
        catch (IOException) { }
    }
}

// Scatter / clear buttons where the settings are, so a tweak to the seed or a clearance is one click from
// seeing what it does — on the package open in Edit In Context, or the prefab opened on its own.
[CustomEditor(typeof(TrackScenery))]
public class TrackSceneryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var scenery = (TrackScenery)target;
        var package = scenery.GetComponentInParent<TrackPackage>();
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(package == null || EditorUtility.IsPersistent(scenery)))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scatter")) Run(scenery, package, clear: false);
            if (GUILayout.Button("Clear")) Run(scenery, package, clear: true);
            EditorGUILayout.EndHorizontal();
        }
        if (package == null) EditorGUILayout.HelpBox("Not inside a track package.", MessageType.Info);
        else if (EditorUtility.IsPersistent(scenery))
            EditorGUILayout.HelpBox("Open the package (Edit Selected Package In Context) to scatter.", MessageType.Info);

        int pieces = scenery.GetComponentsInChildren<SceneryPiece>(true).Length;
        EditorGUILayout.LabelField($"{pieces} pieces");
    }

    static void Run(TrackScenery scenery, TrackPackage package, bool clear)
    {
        string text = clear ? TrackSceneryGenerator.Clear(package.gameObject) : TrackSceneryGenerator.Scatter(package.gameObject);
        EditorSceneManager.MarkSceneDirty(scenery.gameObject.scene);
        Debug.Log($"TrackScenery:\n{text}");
    }
}
