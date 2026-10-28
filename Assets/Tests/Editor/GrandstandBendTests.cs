using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// A grandstand bent into a gentle arc, so a run of them follows a curved front stretch (Daytona's tri-oval)
// instead of being a row of straight blocks set at angles to the road.
//
// What can be checked without a play mode: a straight stand is the same quad it always was; a bent one is an
// arc of the stated length that turns the stated amount with its ends swung to the side the sign says; the
// solid block round it covers the whole arc; Covers follows the arc rather than the box round it; and the
// texture offset starts the crowd where the stand before it left off.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the type is reached by reflection, as in
// GrandstandKeepOutTests.
public class GrandstandBendTests
{
    static readonly System.Type StandType = System.Type.GetType("Grandstand, Assembly-CSharp");

    const float StandLength = 108f;
    const float StandDepth = 14f;

    readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp() => Assert.NotNull(StandType, "Grandstand is missing from Assembly-CSharp.");

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
        _spawned.Clear();
    }

    Component Stand(float bendDegrees, float textureOffset = 0f)
    {
        var go = new GameObject("Grandstand_Bend");
        _spawned.Add(go);
        var stand = go.AddComponent(StandType);
        Set(stand, "length", StandLength);
        Set(stand, "depth", StandDepth);
        Set(stand, "bendDegrees", bendDegrees);
        Set(stand, "textureOffset", textureOffset);
        StandType.GetMethod("Build").Invoke(stand, null);
        return stand;
    }

    static void Set(Component c, string field, object value) =>
        StandType.GetField(field, BindingFlags.Public | BindingFlags.Instance).SetValue(c, value);

    static Mesh MeshOf(Component stand) => stand.GetComponent<MeshFilter>().sharedMesh;

    static bool Covers(Component stand, Vector2 local, float pad = 0f) =>
        (bool)StandType.GetMethod("Covers").Invoke(stand, new object[] { local, pad });

    static BoxCollider2D Structure(Component stand) =>
        (BoxCollider2D)StandType.GetMethod("BuildStructure").Invoke(stand, null);

    // The middle of the stand at each cross-section of the mesh: a bent mesh is laid as pairs of vertices
    // (front row, back row) round the arc.
    static List<Vector2> MiddleLine(Mesh mesh)
    {
        var v = mesh.vertices;
        var line = new List<Vector2>();
        for (int i = 0; i + 1 < v.Length; i += 2) line.Add((v[i] + v[i + 1]) * 0.5f);
        return line;
    }

    [Test]
    public void StraightStandIsTheSameQuad()
    {
        var stand = Stand(0f);
        var v = MeshOf(stand).vertices;
        Assert.AreEqual(4, v.Length, "A straight stand is one quad.");
        foreach (var p in v)
        {
            Assert.AreEqual(StandLength * 0.5f, Mathf.Abs(p.x), 0.001f);
            Assert.AreEqual(StandDepth * 0.5f, Mathf.Abs(p.y), 0.001f);
        }
        var box = Structure(stand);
        Assert.AreEqual(new Vector2(StandLength, StandDepth), box.size, "The straight stand's block changed size.");
        Assert.AreEqual(Vector2.zero, box.offset, "The straight stand's block moved off its centre.");
    }

    [TestCase(10f)]
    [TestCase(-10f)]
    public void BentStandIsAnArcOfItsLength(float bend)
    {
        var line = MiddleLine(MeshOf(Stand(bend)));
        Assert.Greater(line.Count, 2, "A bent stand should be more than one quad.");

        float along = 0f;
        for (int i = 1; i < line.Count; i++) along += Vector2.Distance(line[i - 1], line[i]);
        Assert.AreEqual(StandLength, along, StandLength * 0.002f, "The stand's length is not measured round the bend.");

        // A circular arc: chord 2 sin(θ/2) / k, both ends swung the sagitta to the side the sign says.
        float theta = bend * Mathf.Deg2Rad;
        float k = theta / StandLength;
        Vector2 a = line[0], b = line[line.Count - 1];
        Assert.AreEqual(2f * Mathf.Sin(theta * 0.5f) / k, Vector2.Distance(a, b), 0.01f, "Chord is wrong for the bend.");
        float sagitta = (1f - Mathf.Cos(theta * 0.5f)) / k;
        Assert.AreEqual(sagitta, a.y, 0.01f, "The -X end is not swung to the bend's side.");
        Assert.AreEqual(sagitta, b.y, 0.01f, "The +X end is not swung to the bend's side.");
        Assert.AreEqual(Vector2.zero, line[line.Count / 2], "The transform is not at the middle of the arc.");

        // The ends turn through the bend between them.
        var v = MeshOf(Stand(bend)).vertices;
        Vector2 startAcross = v[1] - v[0], endAcross = v[v.Length - 1] - v[v.Length - 2];
        Assert.AreEqual(bend, Vector2.SignedAngle(startAcross, endAcross), 0.05f, "The stand does not turn by its bend.");
    }

    [Test]
    public void BentStandsBlockCoversTheWholeArc()
    {
        var stand = Stand(12f);
        var box = Structure(stand);
        var r = new Rect(box.offset - box.size * 0.5f, box.size);
        foreach (Vector2 p in MeshOf(stand).vertices)
            Assert.IsTrue(r.xMin - 0.001f <= p.x && p.x <= r.xMax + 0.001f && r.yMin - 0.001f <= p.y && p.y <= r.yMax + 0.001f,
                $"Vertex {p} sticks out of the block {r}.");
    }

    [Test]
    public void CoversFollowsTheArcNotItsBox()
    {
        var stand = Stand(10f);
        var line = MiddleLine(MeshOf(stand));
        Assert.IsTrue(Covers(stand, line[0], 0.01f), "The -X end of the stand is not on it.");
        Assert.IsTrue(Covers(stand, line[line.Count - 1], 0.01f), "The +X end of the stand is not on it.");
        Assert.IsTrue(Covers(stand, new Vector2(0f, -StandDepth * 0.5f + 0.2f)), "The front row at the middle is not on it.");

        // Behind the middle of the back row: inside the box round the arc (whose ends swing toward +Y) but off
        // the seating.
        Assert.IsFalse(Covers(stand, new Vector2(0f, StandDepth * 0.5f + 1.5f)), "Ground behind the middle reads as stand.");
        Assert.IsTrue(Covers(stand, new Vector2(0f, StandDepth * 0.5f + 1.5f), 2f), "Pad does not grow the footprint.");
    }

    [Test]
    public void TextureOffsetCarriesTheCrowdOn()
    {
        // No texture: a repeat is metresPerRepeat (12 m) wide.
        var stand = Stand(6f, textureOffset: 30f);
        var uv = MeshOf(stand).uv;
        Assert.AreEqual(30f / 12f, uv[0].x, 0.001f, "The crowd does not start where the stand before left off.");
        Assert.AreEqual((30f + StandLength) / 12f, uv[uv.Length - 1].x, 0.001f, "The crowd does not run the stand's length.");
    }
}
