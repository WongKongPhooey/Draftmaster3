using System.Collections.Generic;
using System.IO;
using Draftmaster.Data;
using UnityEditor;
using UnityEngine;

// Dresses a track package from its own geometry: ground, walls, grandstands, paddock, player start.
//
// The point is that none of this is per-track creative work — it is all derivable from the spline. Watkins
// Glen has each piece placed by hand, which is right for the reference track and wrong as a process: doing
// it thirty-five times is the repetition the package structure exists to kill. So the rule here is that a
// generated track arrives drivable and walkable, and hand-authoring is an improvement on top rather than a
// prerequisite.
//
// What comes out is REAL GameObjects saved in the package prefab, not runtime magic — open the prefab and
// move, delete or add to any of it. Re-dressing only replaces the pieces it owns (and only with overwrite
// on), so hand edits inside them are the one thing that doesn't survive.
public static class TrackDressingFactory
{
    const string PackageDir = "Assets/Resources/TrackPackages";
    const string GeometryDir = "Assets/Resources/Tracks";

    // Named so a re-dress can find what it made last time.
    const string GroundName = "Ground";
    const string EnvironmentName = "TrackEnvironment";
    const string GrandstandsName = "Grandstands";
    const string BoundaryName = "PaddockBoundary";
    const string RvName = "RV";
    const string SpawnName = "SpawnPoint_Paddock";

    // ---- placement numbers. Everything else is measured off the track. ----
    const float MinStraightForStands = 150f;   // shorter than this and a stand looks pasted on
    const float StandLength = 110f;
    const float StandDepth = 14f;
    const float StandGap = 14f;                // between adjacent stands on the same straight
    const float StandSetback = 12f;            // from the road edge to the front row
    const float PaddockSetback = 45f;          // from the pit lane to the middle of the paddock
    const float PaddockLength = 260f;
    const float PaddockDepth = 80f;

    // ---------------------------------------------------------------- entry points

    [MenuItem("Draftmaster/Tracks/Dress Selected Package")]
    public static void DressSelected() => Report(Dress(TrackSelection.CurrentId, overwrite: true));

    [MenuItem("Draftmaster/Tracks/Dress All Undressed Packages")]
    public static void DressAll()
    {
        var lines = new List<string>();
        foreach (var row in TrackCatalog.All)
        {
            if (!File.Exists($"{PackageDir}/{row.Name}.prefab")) continue;
            lines.Add(Dress(row.Name, overwrite: false));
        }
        Report(lines.Count == 0 ? "Dress: no packages built yet." : string.Join("\n", lines));
    }

    // Dress one package. overwrite:false leaves any piece that already exists alone, so this is safe to run
    // over a package a designer has already been into.
    public static string Dress(string trackId, bool overwrite)
    {
        string path = $"{PackageDir}/{trackId}.prefab";
        if (!File.Exists(path)) return $"{trackId}: no package at {path}.";

        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var package = contents.GetComponent<TrackPackage>() ?? contents.AddComponent<TrackPackage>();
            if (string.IsNullOrEmpty(package.trackId)) package.trackId = trackId;

            var builder = package.Builder;
            if (builder == null || builder.track == null)
                return $"{trackId}: package has no TrackBuilder/geometry — nothing to measure.";

            builder.Build();
            var samples = builder.SampleCenterline();
            if (samples.Count < 2) return $"{trackId}: geometry samples to nothing.";

            var environmentRoot = EnsureChild(contents.transform, "Environment");
            var paddockRoot = EnsureChild(contents.transform, "Paddock");
            // The home for NPCs that belong to THIS track — a local promoter, a track-specific quest giver.
            // Left empty: the paddock regulars (crew, fans, drivers, the career-path veteran) are spawned
            // from the pit-lane geometry by the shared scene and don't want a copy per track.
            EnsureChild(paddockRoot, "NPCs");
            package.environmentRoot = environmentRoot;
            package.paddockRoot = paddockRoot;

            Vector2 centroid = Centroid(samples);
            var done = new List<string>();
            var kept = new List<string>();

            if (Wanted<TrackGround>(contents, environmentRoot, GroundName, overwrite, kept, "ground")
                && BuildGround(environmentRoot, builder)) done.Add("ground");
            if (Wanted<TrackEnvironmentBuilder>(contents, environmentRoot, EnvironmentName, overwrite, kept, "walls")
                && BuildBarriers(environmentRoot, builder, trackId, overwrite)) done.Add("walls");
            if (Wanted<Grandstand>(contents, environmentRoot, GrandstandsName, overwrite, kept, "grandstands"))
            {
                int stands = BuildGrandstands(environmentRoot, builder, samples, centroid);
                if (stands > 0) done.Add($"{stands} grandstands");
            }
            if (Wanted<PaddockBoundary>(contents, paddockRoot, BoundaryName, overwrite, kept, "paddock")
                && BuildPaddock(paddockRoot, builder, samples, centroid)) done.Add("paddock + RV");

            if (kept.Count > 0) done.Add($"kept hand-authored {string.Join(", ", kept)}");

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return done.Count == 0
                ? $"{trackId}: already dressed (nothing added)."
                : $"{trackId}: added {string.Join(", ", done)}.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------------------------------------------------------------- pieces

    static bool BuildGround(Transform root, TrackBuilder builder)
    {
        var go = new GameObject(GroundName);
        go.transform.SetParent(root, false);
        var ground = go.AddComponent<TrackGround>();
        ground.track = builder;
        ground.material = FindMaterial("Grass", "Grassy", "Green");
        ground.Build();
        return true;
    }

    // Walls come from the existing TrackEnvironmentBuilder rather than a second barrier system — it already
    // walks the spline per segment and cuts gaps. All that's missing for a generated track is the asset that
    // drives it, so write one: walls both sides, hugging the road, opened up where the pit lane branches off
    // and rejoins (otherwise the pit entry is a wall).
    static bool BuildBarriers(Transform root, TrackBuilder builder, string trackId, bool overwrite)
    {
        string assetPath = $"{GeometryDir}/{trackId}Environment.asset";
        var env = AssetDatabase.LoadAssetAtPath<TrackEnvironment>(assetPath);
        bool fresh = env == null;
        if (fresh) env = ScriptableObject.CreateInstance<TrackEnvironment>();

        if (fresh || overwrite)
        {
            env.generateBarriers = true;
            env.innerEdgeOffset = 1f;
            env.outerEdgeOffset = 1f;
            env.barrierWidth = 1f;
            env.barrierColliders = true;
            env.barrierSortingOrder = 2;
            env.barrierMaterial = FindMaterial("Concrete", "WallStripe", "White");
            env.grassMaterial = FindMaterial("Grass", "Grassy");
            env.barrierGaps = PitGaps(builder);
        }

        if (fresh)
        {
            Directory.CreateDirectory(GeometryDir);
            AssetDatabase.CreateAsset(env, assetPath);
        }
        EditorUtility.SetDirty(env);
        AssetDatabase.SaveAssets();

        var go = new GameObject(EnvironmentName);
        go.transform.SetParent(root, false);
        var envBuilder = go.AddComponent<TrackEnvironmentBuilder>();
        envBuilder.track = builder;
        envBuilder.environment = env;
        envBuilder.Build();
        return true;
    }

    // Openings in the wall where the pit lane leaves and rejoins the track. Which enum side that is depends
    // on which way the pit road sits from the racing surface, so measure it rather than assume: the builder's
    // "Inner" is simply the right of travel, which is the outside wall on an anticlockwise oval.
    static TrackEnvironment.BarrierGap[] PitGaps(TrackBuilder builder)
    {
        var track = builder.track;
        if (track == null || !track.hasPitLane) return new TrackEnvironment.BarrierGap[0];

        var pit = builder.SamplePitCenterline();
        if (pit.Count < 2) return new TrackEnvironment.BarrierGap[0];

        var entrySample = builder.SampleAt(track.pitEntryDistance);
        float side = Vector2.Dot(pit[0].position - entrySample.position, entrySample.normal);
        var pitSide = side >= 0f
            ? TrackEnvironment.BarrierSide.Inner    // pit road sits to the right of travel
            : TrackEnvironment.BarrierSide.Outer;

        var gaps = new List<TrackEnvironment.BarrierGap>();
        AddGap(gaps, track, pitSide, track.pitEntryDistance, "pit entry");
        AddGap(gaps, track, pitSide, track.pitExitDistance, "pit exit");
        return gaps.ToArray();
    }

    // Barrier gaps are per segment with distances local to that segment, so a window that straddles a segment
    // boundary has to be cut into one gap per segment it touches.
    static void AddGap(List<TrackEnvironment.BarrierGap> gaps, TrackInfoV2 track,
                       TrackEnvironment.BarrierSide side, float lapDistance, string label)
    {
        const float halfWindow = 35f;
        var segs = track.segments;
        if (segs == null || segs.Length == 0) return;

        float from = lapDistance - halfWindow;
        float to = lapDistance + halfWindow;

        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            float segStart = cum, segEnd = cum + segs[i].length;
            cum = segEnd;

            float a = Mathf.Max(from, segStart), b = Mathf.Min(to, segEnd);
            if (b - a < 1f) continue;

            gaps.Add(new TrackEnvironment.BarrierGap
            {
                label = label,
                side = side,
                segmentIndex = i,
                startDistance = a - segStart,
                endDistance = b - segStart,
            });
        }
    }

    // Stands along every straight long enough to carry one, on the side facing away from the middle of the
    // circuit — that's where a real crowd sits, and on an oval it keeps them out of the infield.
    static int BuildGrandstands(Transform root, TrackBuilder builder, List<TrackBuilder.Sample> samples,
                                Vector2 centroid)
    {
        var track = builder.track;
        if (track == null || track.segments == null) return 0;

        var crowd = FindTexture("crowd-phoenix", "lacrowd1", "crowd");
        var standsRoot = new GameObject(GrandstandsName);
        standsRoot.transform.SetParent(root, false);

        int built = 0;
        float cum = 0f;
        for (int i = 0; i < track.segments.Length; i++)
        {
            var seg = track.segments[i];
            float segStart = cum;
            cum += seg.length;
            if (seg.type != TrackInfoV2.SegmentType.Straight || seg.length < MinStraightForStands) continue;

            // Leave the ends of the straight clear so stands don't run into the corner.
            float usable = seg.length - 40f;
            int count = Mathf.Max(1, Mathf.FloorToInt((usable + StandGap) / (StandLength + StandGap)));
            float block = count * StandLength + (count - 1) * StandGap;
            float cursor = segStart + 20f + (usable - block) * 0.5f;

            for (int k = 0; k < count; k++)
            {
                float centre = cursor + StandLength * 0.5f;
                cursor += StandLength + StandGap;
                BuildStand(standsRoot.transform, builder, samples, centroid, centre, crowd, $"Grandstand_{i}_{k}");
                built++;
            }
        }

        if (built == 0) Object.DestroyImmediate(standsRoot);
        return built;
    }

    static void BuildStand(Transform root, TrackBuilder builder, List<TrackBuilder.Sample> samples,
                           Vector2 centroid, float distance, Texture2D crowd, string name)
    {
        var s = builder.SampleAt(distance, samples);

        // +1 when the sample's normal points away from the middle of the circuit.
        float outward = Vector2.Dot(s.position - centroid, s.normal) >= 0f ? 1f : -1f;
        float setback = s.width * 0.5f + StandSetback + StandDepth * 0.5f;
        Vector2 pos = s.position + s.normal * outward * setback;

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

        // Local +X runs along the track, local +Y is the stand's depth. Unrotated, +Y is the LEFT of travel
        // (normal is the right), so a stand on the normal side is turned through 180 to face back at the road.
        float tangentAngle = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, tangentAngle + (outward > 0f ? 180f : 0f));

        var stand = go.AddComponent<Grandstand>();
        stand.crowdTexture = crowd;
        stand.length = StandLength;
        stand.depth = StandDepth;
        stand.flipFacing = outward > 0f;
        stand.Build();
    }

    // A walkable pocket beside the pit lane with the player's motorhome in it, so a generated track opens the
    // same way Watkins does (on foot, at the RV) instead of dumping the player onto a blank apron.
    static bool BuildPaddock(Transform root, TrackBuilder builder, List<TrackBuilder.Sample> samples,
                             Vector2 centroid)
    {
        // The RV and the fallback marker are siblings of the boundary, so a re-dress has to clear them too.
        var oldRv = root.Find(RvName);
        if (oldRv != null) Object.DestroyImmediate(oldRv.gameObject);
        var oldSpawn = root.Find(SpawnName);
        if (oldSpawn != null) Object.DestroyImmediate(oldSpawn.gameObject);

        // Sit the paddock behind the middle of the pit lane where there is one, otherwise behind the
        // start/finish straight.
        var pit = builder.SamplePitCenterline();
        Vector2 anchor, tangent, normal;
        if (pit.Count >= 2)
        {
            var mid = pit[pit.Count / 2];
            anchor = mid.position; tangent = mid.tangent; normal = mid.normal;
        }
        else
        {
            var mid = builder.SampleAt(0f, samples);
            anchor = mid.position; tangent = mid.tangent; normal = mid.normal;
        }

        // Away from the racing surface: the side of the pit road that faces the middle of the circuit.
        float inward = Vector2.Dot(anchor - centroid, normal) >= 0f ? -1f : 1f;
        Vector2 centre = anchor + normal * inward * PaddockSetback;
        float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

        var go = new GameObject(BoundaryName);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        var poly = go.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;
        float hx = PaddockLength * 0.5f, hy = PaddockDepth * 0.5f;
        poly.points = new[]
        {
            new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy)
        };
        go.AddComponent<PaddockBoundary>();

        // The RV carries the SpawnPoint_RV marker PitLaneStart looks for by name, so placing it is also what
        // decides where the player wakes up.
        var rvPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/RV.prefab");
        if (rvPrefab != null)
        {
            var rv = (GameObject)PrefabUtility.InstantiatePrefab(rvPrefab, root);
            rv.name = RvName;
            // A quarter of the way along the pocket, on the far side from the pit road.
            Vector3 local = new Vector3(-hx * 0.5f, hy * 0.45f * (inward > 0f ? 1f : -1f), 0f);
            rv.transform.localPosition = go.transform.localPosition + go.transform.localRotation * local;
            rv.transform.localRotation = go.transform.localRotation;
        }

        // A fallback start, for when the RV prefab is missing or the player is put outside it.
        var spawn = new GameObject(SpawnName);
        spawn.transform.SetParent(root, false);
        spawn.transform.localPosition = go.transform.localPosition;
        spawn.AddComponent<PlayerSpawnPoint>().weight = 0.5f;
        return true;
    }

    // ---------------------------------------------------------------- helpers

    // Should this piece be generated?
    //
    // Three cases, and the third is the one that matters: WatkinsGlen's package was lifted out of a scene
    // where every piece was placed by hand, and those pieces sit at the package root rather than under the
    // Environment/Paddock roots this factory owns. Generating over them would double up the ground plane and
    // scatter a second set of stands through a track someone has already dressed. So anything found outside
    // the owned child is left alone and reported, whatever `overwrite` says — overwrite means "replace what I
    // generated", never "replace what you made".
    static bool Wanted<T>(GameObject contents, Transform ownerRoot, string ownedName, bool overwrite,
                          List<string> kept, string label) where T : Component
    {
        var owned = ownerRoot.Find(ownedName);

        foreach (var component in contents.GetComponentsInChildren<T>(true))
        {
            if (IsOurs(component.transform, owned)) continue;
            kept.Add(label);
            return false;
        }

        // Hand-made pieces don't always carry the component that identifies a generated one — Watkins' ground
        // is a bare MeshFilter quad, not a TrackGround — so the name counts as a claim too.
        foreach (var child in contents.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != ownedName || IsOurs(child, owned)) continue;
            kept.Add(label);
            return false;
        }

        if (owned == null) return true;             // nothing there at all
        if (!overwrite) return false;               // ours, but leave it as it is

        Object.DestroyImmediate(owned.gameObject);
        return true;
    }

    static bool IsOurs(Transform t, Transform owned) =>
        owned != null && (t == owned || t.IsChildOf(owned));

    static Transform EnsureChild(Transform parent, string name)
    {
        var found = parent.Find(name);
        if (found != null) return found;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Vector2 Centroid(List<TrackBuilder.Sample> samples)
    {
        Vector2 sum = Vector2.zero;
        foreach (var s in samples) sum += s.position;
        return sum / Mathf.Max(1, samples.Count);
    }

    static Material FindMaterial(params string[] names) => FindAsset<Material>(names);
    static Texture2D FindTexture(params string[] names) => FindAsset<Texture2D>(names);

    // Exact name first, then the first fuzzy hit — so "Grass" doesn't silently resolve to "grasspalms".
    static T FindAsset<T>(params string[] names) where T : Object
    {
        foreach (string name in names)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            T fuzzy = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;
                if (asset.name == name) return asset;
                if (fuzzy == null) fuzzy = asset;
            }
            if (fuzzy != null) return fuzzy;
        }
        return null;
    }

    static void Report(string text)
    {
        Debug.Log($"TrackDressing:\n{text}");
        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "track-tools.txt"), text);
        }
        catch (IOException) { }
    }
}
