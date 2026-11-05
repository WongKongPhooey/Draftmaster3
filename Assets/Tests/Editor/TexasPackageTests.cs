using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Texas (FortWorth) dressed after the real Texas Motor Speedway (TexasPackageAuthoring), and the flat dressing
// shapes it is drawn with (TrackDressingShape).
//
// The package checks read what the Author menu left in the prefab: the one main grandstand is on the front
// stretch and nothing stands on the back stretch any more; nothing placed - stands, buildings, paving, the infield
// road course - reaches the racing surface; the pit boxes run along the straight of pit road, on its infield side.
//
// Assembly-CSharp cannot be referenced from an asmdef, so its types are reached by reflection, as in
// GrandstandBendTests and OvalLaneTests.
public class TexasPackageTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/FortWorth.prefab";

    readonly List<GameObject> _spawned = new();
    GameObject _contents;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
        _contents = null;
    }

    static Type Runtime(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from Assembly-CSharp.");
        return null;
    }

    // ------------------------------------------------------------------ TrackDressingShape

    Component Shape(string kind, Vector2[] points, float pathWidth = 8f, bool closed = false)
    {
        var type = Runtime("TrackDressingShape");
        var go = new GameObject("Shape");
        _spawned.Add(go);
        var shape = go.AddComponent(type);
        type.GetField("kind").SetValue(shape, Enum.Parse(type.GetField("kind").FieldType, kind));
        type.GetField("points").SetValue(shape, points);
        type.GetField("pathWidth").SetValue(shape, pathWidth);
        type.GetField("closedPath").SetValue(shape, closed);
        type.GetMethod("Build").Invoke(shape, null);
        return shape;
    }

    static bool Covers(Component shape, Vector2 at, float pad = 0f) =>
        (bool)shape.GetType().GetMethod("Covers").Invoke(shape, new object[] { at, pad });

    // Area of one winding of a submesh (the triangles are emitted both ways round, so half the total).
    static float FilledArea(Mesh mesh, int submesh)
    {
        var v = mesh.vertices;
        var t = mesh.GetTriangles(submesh);
        float area = 0f;
        for (int i = 0; i + 2 < t.Length; i += 3)
            area += Mathf.Abs((v[t[i + 1]].x - v[t[i]].x) * (v[t[i + 2]].y - v[t[i]].y) -
                              (v[t[i + 2]].x - v[t[i]].x) * (v[t[i + 1]].y - v[t[i]].y)) * 0.5f;
        return area * 0.5f;
    }

    [Test]
    public void APolygonFillsExactlyItsOutline_EvenConcaveAndClockwise()
    {
        // An L, wound clockwise: 20 x 20 less a 10 x 10 corner = 300 m2.
        var l = new[] { V(0, 0), V(0, 20), V(10, 20), V(10, 10), V(20, 10), V(20, 0) };
        var shape = Shape("Polygon", l);
        var mesh = shape.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(mesh);
        Assert.AreEqual(1, mesh.subMeshCount, "no shadow or border asked for: one submesh");
        Assert.AreEqual(300f, FilledArea(mesh, 0), 0.01f);

        Assert.IsTrue(Covers(shape, V(5, 15)));
        Assert.IsTrue(Covers(shape, V(15, 5)));
        Assert.IsFalse(Covers(shape, V(15, 15)), "the cut-out corner is not covered");
        Assert.IsTrue(Covers(shape, V(15, 12), 2.5f), "within the pad of an edge counts");
    }

    [Test]
    public void AShadowGoesUnderTheFillAndABorderOverIt()
    {
        var type = Runtime("TrackDressingShape");
        var shape = Shape("Polygon", new[] { V(0, 0), V(10, 0), V(10, 4), V(0, 4) });
        type.GetField("shadowOffset").SetValue(shape, new Vector2(3f, 6f));
        type.GetField("borderWidth").SetValue(shape, 1f);
        type.GetMethod("Build").Invoke(shape, null);
        var mesh = shape.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(3, mesh.subMeshCount, "shadow, fill, border");
        Assert.AreEqual(40f, FilledArea(mesh, 1), 0.01f, "the fill is still just the outline");
        Assert.AreEqual(3, shape.GetComponent<MeshRenderer>().sharedMaterials.Length);
    }

    [Test]
    public void APathIsItsWidthAllTheWayAlong_AndCoversOnlyThat()
    {
        var shape = Shape("Path", new[] { V(0, 0), V(100, 0), V(100, 50) }, pathWidth: 9f);
        var bounds = shape.GetComponent<MeshFilter>().sharedMesh.bounds;
        Assert.AreEqual(-4.5f, bounds.min.x, 0.01f);
        Assert.AreEqual(104.5f, bounds.max.x, 0.01f);
        Assert.AreEqual(54.5f, bounds.max.y, 0.01f);
        Assert.IsTrue(Covers(shape, V(50, 4)));
        Assert.IsFalse(Covers(shape, V(50, 5)));
        Assert.IsTrue(Covers(shape, V(104, 25)));
        Assert.IsFalse(Covers(shape, V(50, 25)), "an open path does not cover the corner it turns round");
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    // ------------------------------------------------------------------ the FortWorth package

    Component LoadBuilder(out List<(Vector2 pos, Vector2 normal, float width)> centreline, out ScriptableObject track)
    {
        _contents = PrefabUtility.LoadPrefabContents(PackagePath);
        var builderType = Runtime("TrackBuilder");
        var builder = _contents.GetComponentInChildren(builderType, true);
        Assert.IsNotNull(builder, "FortWorth package has no TrackBuilder");
        track = (ScriptableObject)builderType.GetField("track").GetValue(builder);
        Assert.IsNotNull(track, "FortWorth TrackBuilder has no geometry");
        centreline = new List<(Vector2, Vector2, float)>();
        var samples = (IEnumerable)builderType.GetMethod("SampleCenterline").Invoke(builder, null);
        foreach (var s in samples)
        {
            var st = s.GetType();
            Vector2 p = (Vector2)st.GetField("position").GetValue(s);
            Vector2 n = (Vector2)st.GetField("normal").GetValue(s);
            float w = (float)st.GetField("width").GetValue(s);
            var t = ((Component)builder).transform;
            centreline.Add((t.TransformPoint(p), ((Vector2)t.TransformDirection(n)).normalized, w));
        }
        return builder;
    }

    // How far a world point is outside the edge of the racing surface (negative = on it).
    static float BeyondEdge(List<(Vector2 pos, Vector2 normal, float width)> centreline, Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var s in centreline)
        {
            float d = Vector2.Distance(s.pos, p) - s.width * 0.5f;
            if (d < best) best = d;
        }
        return best;
    }

    static Vector2 Centroid(List<(Vector2 pos, Vector2 normal, float width)> centreline)
    {
        Vector2 c = Vector2.zero;
        foreach (var s in centreline) c += s.pos;
        return c / centreline.Count;
    }

    [Test]
    public void TheMainGrandstandIsOnTheFrontStretch_AndTheBackStretchHasNone()
    {
        LoadBuilder(out var centreline, out _);
        var stands = _contents.transform.Find("Environment/Grandstands");
        Assert.IsNotNull(stands, "no Grandstands root - run Draftmaster > Tracks > Texas > Author Texas");
        var standType = Runtime("Grandstand");
        var pieces = stands.GetComponentsInChildren(standType, true);
        Assert.GreaterOrEqual(pieces.Length, 6, "one long stand, in pieces, round the front stretch");

        // The start/finish line is on the front stretch; the back stretch is the far side of the infield, across
        // the line through the middle of the circuit parallel to the front stretch.
        Vector2 line = centreline[0].pos;
        Vector2 middle = Centroid(centreline);
        Vector2 toFront = centreline[0].normal;
        if (Vector2.Dot(toFront, line - middle) < 0f) toFront = -toFront;
        foreach (var piece in pieces)
        {
            Assert.IsTrue(piece.name.StartsWith("Grandstand_Main_"), $"{piece.name}: only the main stand at Texas");
            Vector2 at = piece.transform.position;
            Assert.Greater(Vector2.Dot(at - middle, toFront), 0f,
                           $"{piece.name} at {at} is on the back half of the circuit - Texas has no back-stretch seats");
            Assert.Greater(BeyondEdge(centreline, at), 0f, $"{piece.name} at {at} sits on the racing surface");
        }
    }

    [Test]
    public void NothingPlacedReachesTheRacingSurface()
    {
        LoadBuilder(out var centreline, out _);
        var dressing = _contents.transform.Find("Environment/TexasDressing");
        Assert.IsNotNull(dressing, "no TexasDressing root - run Draftmaster > Tracks > Texas > Author Texas");
        var shapeType = Runtime("TrackDressingShape");
        var shapes = dressing.GetComponentsInChildren(shapeType, true);
        Assert.Greater(shapes.Length, 10);
        var names = new HashSet<string>();
        foreach (var shape in shapes)
        {
            names.Add(shape.name);
            var pts = (Vector2[])shapeType.GetField("points").GetValue(shape);
            bool path = shapeType.GetField("kind").GetValue(shape).ToString() == "Path";
            float half = path ? (float)shapeType.GetField("pathWidth").GetValue(shape) * 0.5f : 0f;
            foreach (var p in pts)
            {
                Vector2 world = shape.transform.TransformPoint(p);
                Assert.Greater(BeyondEdge(centreline, world) - half, 2.5f,
                               $"{shape.name}: {world} is within 2.5 m of the racing surface");
            }
        }
        foreach (var landmark in new[] { "MainGrandstandSuites", "SpeedwayClub", "LoneStarTower", "BigHossTV", "BackstretchDeck", "InfieldPaving", "HollerLoop" })
            Assert.IsTrue(names.Contains(landmark), $"{landmark} is missing");
    }

    [Test]
    public void PitBoxesRunAlongTheStraightOfPitRoad_OnItsInfieldSide()
    {
        var builder = LoadBuilder(out var centreline, out var track);
        var t = builder.GetType();
        var so = new SerializedObject(track);
        var pitSegs = so.FindProperty("pitSegments");
        Assert.AreEqual(3, pitSegs.arraySize, "entry arc, straight, exit arc");
        float entry = pitSegs.GetArrayElementAtIndex(0).FindPropertyRelative("length").floatValue;
        float straight = pitSegs.GetArrayElementAtIndex(1).FindPropertyRelative("length").floatValue;
        float exit = pitSegs.GetArrayElementAtIndex(2).FindPropertyRelative("length").floatValue;
        float pitLen = entry + straight + exit;

        float from = (float)t.GetMethod("PitBoxLaneFrom").Invoke(builder, new object[] { pitLen });
        float to = (float)t.GetMethod("PitBoxLaneTo").Invoke(builder, new object[] { pitLen });
        Assert.GreaterOrEqual(from, entry, "a box on the entry arc");
        Assert.LessOrEqual(to, entry + straight, "a box on the exit arc");
        Assert.Greater(to - from, straight * 0.8f, "the boxes should fill most of the straight");

        // The box side, at the middle of the boxes, points into the infield.
        var sample = t.GetMethod("SamplePitAt").Invoke(builder, new object[] { (from + to) * 0.5f, null });
        Vector2 pos = ((Component)builder).transform.TransformPoint((Vector2)sample.GetType().GetField("position").GetValue(sample));
        Vector2 normal = ((Component)builder).transform.TransformDirection((Vector2)sample.GetType().GetField("normal").GetValue(sample));
        float side = (float)t.GetProperty("PitBoxSide").GetValue(builder);
        Assert.Greater(Vector2.Dot(normal * side, Centroid(centreline) - pos), 0f, "the boxes are on the infield side of pit road");
    }
}
