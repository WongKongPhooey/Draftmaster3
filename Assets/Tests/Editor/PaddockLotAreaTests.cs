using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the authored paddock footprints — the rectangles a track package draws for the
// motorhome lot and the team garages (PaddockLotArea), which the two lots pack themselves into instead
// of growing off the player's RV by number.
//
// The whole point of the component is that the block can be judged in the editor, so the maths the gizmo
// draws with is the maths the lot builds with. That is what these measure: everything lands inside the
// rectangle it was drawn in, the block is centred in it, a field too big for the box is squeezed along
// its lines rather than stacked out the back, and the player's RV keeps the place it stands in.
//
// Assembly-CSharp can't be referenced by an asmdef, so the types are reached by reflection the same way
// PopupGarageTests reaches the rigs.
public class PaddockLotAreaTests
{
    static readonly System.Type AreaType = System.Type.GetType("PaddockLotArea, Assembly-CSharp");
    static readonly System.Type KindType = System.Type.GetType("PaddockLotKind, Assembly-CSharp");
    static readonly System.Type LotType = System.Type.GetType("DriverMotorhomeLot, Assembly-CSharp");

    const float RvWidth = 3.95f;
    const float RvLength = 9.93f;

    // A rectangle of the given size, at `position`, turned `degrees` about z.
    static Component Area(GameObject go, float width, float depth, string kind, float degrees = 0f, Vector3? position = null)
    {
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, depth);

        var area = go.AddComponent(AreaType);
        AreaType.GetField("kind").SetValue(area, System.Enum.Parse(KindType, kind));
        go.transform.SetPositionAndRotation(position ?? Vector3.zero, Quaternion.Euler(0f, 0f, degrees));
        return area;
    }

    static void SetFloat(object o, string name, float v) => o.GetType().GetField(name).SetValue(o, v);

    // PaddockLotArea.Solve, with its three out parameters read back off the argument array.
    static bool Solve(object area, int count, float across, float depth, float z,
                      out object line, out int rows, out bool tight)
    {
        var m = AreaType.GetMethod("Solve", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(m, "PaddockLotArea.Solve is gone.");
        var args = new object[] { count, across, depth, z, null, null, null };
        bool ok = (bool)m.Invoke(area, args);
        line = args[4];
        rows = (int)args[5];
        tight = (bool)args[6];
        return ok;
    }

    static float LineFloat(object line, string name) => (float)line.GetType().GetField(name).GetValue(line);
    static int LineInt(object line, string name) => (int)line.GetType().GetField(name).GetValue(line);
    static Vector3 LineVector(object line, string name) => (Vector3)line.GetType().GetField(name).GetValue(line);
    static Quaternion LineRotation(object line) => (Quaternion)line.GetType().GetField("rotation").GetValue(line);

    static Vector3 PlaceAt(object line, int index)
        => (Vector3)line.GetType().GetMethod("PlaceAt").Invoke(line, new object[] { index });

    // Where a place sits in the rectangle's own frame: x across a line, y across the stack.
    static Vector2 Local(Component area, Vector3 world)
    {
        Vector3 l = area.transform.InverseTransformPoint(new Vector3(world.x, world.y, area.transform.position.z));
        return new Vector2(l.x, l.y);
    }

    // The rule the whole component exists for: what the gizmo drew inside the box is where the rigs go.
    [Test]
    public void EveryRigLandsInsideTheRectangle()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 90f, 80f, "Motorhomes");
            Assert.IsTrue(Solve(area, 43, RvWidth, RvLength, -0.5f, out object line, out int rows, out bool tight));
            Assert.IsFalse(tight, "43 motorhomes fit a 90x80 box at the default spacing; they should not have been squeezed.");

            for (int i = 0; i < 43; i++)
            {
                Vector2 p = Local(area, PlaceAt(line, i));
                Assert.LessOrEqual(Mathf.Abs(p.x) + RvWidth * 0.5f, 45f + 0.01f, $"rig {i} hangs out of the side of the box");
                Assert.LessOrEqual(Mathf.Abs(p.y) + RvLength * 0.5f, 40f + 0.01f, $"rig {i} hangs out of the end of the box");
            }
            Assert.AreEqual(43, Mathf.Min(43, rows * LineInt(line, "perRow")), "the box has to hold every rig it was asked for");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // A block packed into one corner would make the rectangle a hint rather than a footprint.
    [Test]
    public void TheBlockIsCentredInTheRectangle()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 90f, 80f, "Motorhomes", 0f, new Vector3(-300f, 40f, 0f));
            Solve(area, 40, RvWidth, RvLength, -0.5f, out object line, out int rows, out _);

            int perRow = LineInt(line, "perRow");
            Vector3 last = PlaceAt(line, perRow * rows - 1);
            Vector3 first = PlaceAt(line, 0);
            Vector2 mid = Local(area, (first + last) * 0.5f);

            Assert.AreEqual(0f, mid.x, 0.01f, "the block is not centred across the box");
            Assert.AreEqual(0f, mid.y, 0.01f, "the block is not centred along the box");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Rotating the object rotates the whole block — lines, stacking and the bodies themselves — so the
    // lot can be laid along a paddock that isn't axis-aligned.
    [Test]
    public void TheLinesRunAlongTheBoxsOwnAxes()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 90f, 80f, "Motorhomes", 37f);
            Solve(area, 20, RvWidth, RvLength, -0.5f, out object line, out _, out _);

            Quaternion rot = go.transform.rotation;
            Assert.AreEqual(0f, Vector3.Angle(LineVector(line, "axis"), rot * Vector3.right), 0.01f,
                            "lines should run along the box's local +X");
            Assert.AreEqual(0f, Vector3.Angle(LineVector(line, "front"), rot * Vector3.up), 0.01f,
                            "lines should stack along the box's local +Y");
            Assert.AreEqual(0f, Quaternion.Angle(LineRotation(line), rot), 0.01f,
                            "the bodies should stand in the box's frame");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // The z the lot draws at is the lot's business, not the box's — the box is authored on the ground plane.
    [Test]
    public void ThePlacesTakeTheirZFromTheLotNotTheBox()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 90f, 80f, "Motorhomes", 0f, new Vector3(10f, 10f, 0f));
            Solve(area, 12, RvWidth, RvLength, -0.5f, out object line, out _, out _);
            for (int i = 0; i < 12; i++)
                Assert.AreEqual(-0.5f, PlaceAt(line, i).z, 0.001f, "a rig was placed off the lot's z plane");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Too small a box is an authoring mistake, and the answer is to say so loudly while still parking the
    // whole field inside the footprint — a block that quietly grew out the back would be the bug the
    // rectangle was added to stop.
    [Test]
    public void AFieldTooBigForTheBoxIsSqueezedAlongItsLinesAndFlagged()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 60f, 30f, "Motorhomes");
            Solve(area, 43, RvWidth, RvLength, -0.5f, out object line, out int rows, out bool tight);

            Assert.IsTrue(tight, "43 motorhomes cannot fit a 60x30 box; the squeeze should have been reported.");
            Assert.LessOrEqual(rows * LineFloat(line, "rowPitch"), 30f + RvLength,
                               "the block stacked past the back of the box instead of squeezing along its lines");

            for (int i = 0; i < 43; i++)
            {
                Vector2 p = Local(area, PlaceAt(line, i));
                Assert.LessOrEqual(Mathf.Abs(p.x), 30f + 0.01f, $"rig {i} was pushed out of the side of the box");
                Assert.LessOrEqual(Mathf.Abs(p.y), 15f + 0.01f, $"rig {i} was pushed out of the end of the box");
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    // The player's motorhome is scene-placed and never moves. Standing inside the lot it has to hold the
    // place it is already in, or the field parks a second rig on top of it.
    [Test]
    public void ThePlayersRvHoldsThePlaceItStandsIn()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 90f, 80f, "Motorhomes");
            Solve(area, 43, RvWidth, RvLength, -0.5f, out object line, out int rows, out _);

            int places = rows * LineInt(line, "perRow");
            Vector3 target = PlaceAt(line, 7) + new Vector3(0.4f, -0.3f, 0f);   // stood a shade off its mark

            var nearest = AreaType.GetMethod("NearestPlace", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(nearest, "PaddockLotArea.NearestPlace is gone.");
            int place = (int)nearest.Invoke(null, new object[] { line, places, target });

            Assert.AreEqual(7, place, "the player's RV was given a different place from the one it is parked in");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Containment decides whether the player's rig is part of the lot at all, and it is asked while the
    // scene is still assembling itself — so it is local-space maths rather than a physics query.
    [Test]
    public void ContainmentFollowsTheBoxWhenItIsTurned()
    {
        var go = new GameObject("MotorhomesLotArea");
        try
        {
            var area = Area(go, 40f, 10f, "Motorhomes", 90f);
            var contains = AreaType.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance);

            // Turned a quarter turn, the box is 10 wide and 40 tall in world terms.
            Assert.IsTrue((bool)contains.Invoke(area, new object[] { new Vector3(0f, 18f, 0f) }), "inside the turned box");
            Assert.IsFalse((bool)contains.Invoke(area, new object[] { new Vector3(18f, 0f, 0f) }), "outside the turned box");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // The garages are the block most likely to be drawn somewhere awkward — they go furthest from the pit
    // lane — so the end-to-end path is measured too: an authored box and the real PopupGarageLot.Build.
    [Test]
    public void TheGaragesParkInsideTheirOwnAuthoredBox()
    {
        var areaGo = new GameObject("GaragesLotArea");
        var lotGo = new GameObject("MotorhomeLot");
        GameObject garageGo = null;
        try
        {
            var area = Area(areaGo, 160f, 90f, "Garages", 0f, new Vector3(-300f, 200f, 0f));
            SetFloat(area, "gap", 2.5f);
            SetFloat(area, "rowGap", 7f);

            var lot = MotorhomeLotWithSlots(lotGo, 12);
            var garages = LotType.Assembly.GetType("PopupGarageLot")
                                 .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                                 .Invoke(null, new object[] { lot }) as Component;
            Assert.IsNotNull(garages, "PopupGarageLot.Create built nothing.");
            garageGo = garages.gameObject;

            var rigs = (System.Collections.IEnumerable)garages.GetType().GetProperty("Rigs").GetValue(garages);
            int seen = 0;
            foreach (Component rig in rigs)
            {
                Vector2 p = Local(area, rig.transform.position);
                Assert.LessOrEqual(Mathf.Abs(p.x), 80f + 0.01f, "a garage parked outside the side of its box");
                Assert.LessOrEqual(Mathf.Abs(p.y), 45f + 0.01f, "a garage parked outside the end of its box");
                seen++;
            }
            Assert.AreEqual(12, seen, "one garage per entry");
        }
        finally
        {
            if (garageGo != null) Object.DestroyImmediate(garageGo);
            Object.DestroyImmediate(lotGo);
            Object.DestroyImmediate(areaGo);
        }
    }

    // A motorhome lot with a roster in it and no line of its own — the garages have to stand up from the
    // authored box alone, without the block in front of them having been laid out.
    static Component MotorhomeLotWithSlots(GameObject go, int entries)
    {
        var lot = go.AddComponent(LotType);
        var slotType = LotType.GetNestedType("Slot");
        var slots = (System.Collections.IList)LotType.GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance)
                                                     .GetValue(lot);
        for (int i = 0; i < entries; i++)
        {
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("carNumber").SetValue(slot, 20 + i);
            slotType.GetField("shortName").SetValue(slot, "Driver" + i);
            slotType.GetField("fullName").SetValue(slot, "Test Driver" + i);
            slotType.GetField("teamName").SetValue(slot, "Test Motorsports");
            slots.Add(slot);
        }
        return lot;
    }
}
