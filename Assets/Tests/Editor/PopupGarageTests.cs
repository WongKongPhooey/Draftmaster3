using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the team garages — the popup rigs PopupGarageLot parks behind the drivers'
// motorhomes, with a canopy off the side, the car under it and a masked meeting room behind the door.
//
// None of this can be checked by looking at a scene: the lot is generated at play time from the live
// roster, and the room behind each door is generated again the first time somebody walks up to it. So
// these tests build a rig the way the lot does and measure what came out — in particular that the
// doorway the player walks through on the OUTSIDE is the same doorway the room leaves open on the
// INSIDE, which is the one thing that silently breaks if the frame maths drifts.
//
// The garage scripts live in Assembly-CSharp, which an asmdef can't reference, so they're reached by
// reflection — the same way TitleScreenWiringTests builds an RV interior.
public class PopupGarageTests
{
    static readonly System.Type RigType = System.Type.GetType("PopupGarageRig, Assembly-CSharp");
    static readonly System.Type InteriorType = System.Type.GetType("PopupGarageInterior, Assembly-CSharp");
    static readonly System.Type LotType = System.Type.GetType("DriverMotorhomeLot, Assembly-CSharp");

    // What the lot hands each rig. Kept here rather than read off the component so a silent change to a
    // default shows up as a failing test rather than as a paddock that quietly moved.
    const float BodyWidth = 3.95f;
    const float BodyLength = 9.93f;
    const float CanopyWidth = 6.5f;
    const float DoorWidth = 1.6f;
    const float DoorAlong = 3.1f;
    const float LineGap = 2.5f;

    static object Field(object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, $"{o.GetType().Name}.{name} is gone.");
        return f.GetValue(o);
    }

    static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, $"{o.GetType().Name}.{name} is gone.");
        f.SetValue(o, value);
    }

    static object Prop(object o, string name)
    {
        var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(p, $"{o.GetType().Name}.{name} is gone.");
        return p.GetValue(o);
    }

    static object Call(object o, string name, params object[] args)
    {
        var m = o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(m, $"{o.GetType().Name}.{name}() is gone.");
        return m.Invoke(o, args);
    }

    // A rig configured the way PopupGarageLot configures one, parked at `position` facing `rotation`.
    static Component Rig(GameObject go, int canopySide, bool carAtHome, bool teamSign = false)
    {
        Assert.IsNotNull(RigType, "PopupGarageRig is missing from Assembly-CSharp.");
        var rig = go.AddComponent(RigType);

        SetField(rig, "carNumber", 20);
        SetField(rig, "teamName", "Test Motorsports");
        SetField(rig, "carset", "");            // no livery for this number: the block fallback, deterministically
        SetField(rig, "canopySide", canopySide);
        SetField(rig, "carAtHome", carAtHome);
        SetField(rig, "showTeamName", teamSign);
        SetField(rig, "bodyWidth", BodyWidth);
        SetField(rig, "bodyLength", BodyLength);
        SetField(rig, "canopyWidth", CanopyWidth);
        SetField(rig, "doorWidth", DoorWidth);
        SetField(rig, "doorAlong", DoorAlong);

        Call(rig, "Assemble");
        return rig;
    }

    static Component Room(GameObject host, Component rig)
    {
        Assert.IsNotNull(InteriorType, "PopupGarageInterior is missing from Assembly-CSharp.");
        var room = host.AddComponent(InteriorType);
        Call(room, "Initialize", rig);
        Call(room, "BuildNow");
        return room;
    }

    static bool RectHolds(Vector2 point, Vector2 centre, Vector2 size, float shrink)
    {
        Vector2 half = size * 0.5f - new Vector2(shrink, shrink);
        return Mathf.Abs(point.x - centre.x) < half.x && Mathf.Abs(point.y - centre.y) < half.y;
    }

    // ---------------------------------------------------------------- the rig

    [Test]
    public void TheCanopyAndTheCarSitOnTheDoorSideOfTheRig()
    {
        foreach (int side in new[] { 1, -1 })
        {
            var go = new GameObject("Garage");
            try
            {
                var rig = Rig(go, side, carAtHome: true);

                var canopy = go.transform.Find("Canopy");
                Assert.IsNotNull(canopy, $"canopySide {side}: the rig has no canopy at all.");
                Assert.AreEqual(side * (BodyWidth + CanopyWidth) * 0.5f, canopy.localPosition.x, 0.001f,
                                $"canopySide {side}: the canopy is not pitched off the body's side.");

                var car = (Transform)Prop(rig, "ParkedCar");
                Assert.IsNotNull(car, $"canopySide {side}: nothing is parked under the canopy.");

                // The car has to be UNDER the canopy, not beside it — that is the whole idea.
                float alongCanopy = Mathf.Abs(car.localPosition.y - canopy.localPosition.y);
                Assert.AreEqual(canopy.localPosition.x, car.localPosition.x, 0.001f,
                                $"canopySide {side}: the parked car is not centred under the canopy.");
                Assert.Less(alongCanopy, (float)Field(rig, "canopyLength") * 0.5f,
                            $"canopySide {side}: the parked car has slid out of the end of the canopy.");

                // And the door is on the same side, so the way in is under the awning past the car.
                var doorLocal = (Vector2)Prop(rig, "DoorLocalPosition");
                Assert.AreEqual(side * BodyWidth * 0.5f, doorLocal.x, 0.001f,
                                $"canopySide {side}: the door is not on the canopy side of the body.");
                Assert.Greater(doorLocal.y, (float)Field(rig, "carAlong") + 2f,
                               $"canopySide {side}: the door is level with the parked car, so walking in means walking through it.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    [Test]
    public void TheCanopyStandsEmptyWhenTheCarIsOutOnTrack()
    {
        var go = new GameObject("Garage");
        try
        {
            var rig = Rig(go, 1, carAtHome: false);

            Assert.IsNull((Transform)Prop(rig, "ParkedCar"),
                          "a car that is out on track (or sat in its pit box) is parked under its canopy as well.");
            Assert.IsNotNull(go.transform.Find("Canopy"), "the canopy should still be pitched with the car away.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TheShellIsSolidExceptForTheDoorway()
    {
        var go = new GameObject("Garage");
        try
        {
            var rig = Rig(go, 1, carAtHome: true);
            var door = (Vector2)Prop(rig, "DoorLocalPosition");

            var boxes = go.GetComponentsInChildren<BoxCollider2D>();
            Assert.Greater(boxes.Length, 3, "the rig has no shell to walk around.");

            // Measured in the middle of the wall's thickness rather than on the body's outer edge: a point
            // sat exactly on the edge misses every wall on the X axis alone, which would pass whatever the
            // doorway did.
            float inward = -Mathf.Sign(door.x) * (float)Field(rig, "wallThickness") * 0.5f;
            var through = new Vector2(door.x + inward, door.y);

            foreach (var box in boxes)
                Assert.IsFalse(RectHolds(through, box.transform.localPosition, box.size, 0.001f),
                               $"'{box.name}' is across the doorway — there is no way into the garage.");

            // Control: the same edge, away from the door, is solid — otherwise the test above would pass
            // on a rig with no walls at all.
            var blocked = new Vector2(door.x + inward, -DoorAlong);
            bool covered = false;
            foreach (var box in boxes)
                covered |= RectHolds(blocked, box.transform.localPosition, box.size, 0.001f);
            Assert.IsTrue(covered, "the door side of the body is open along its whole length, not just at the door.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TheTeamNameIsLetteredOnTheCanopy()
    {
        var go = new GameObject("Garage");
        try
        {
            Rig(go, 1, carAtHome: true, teamSign: true);
            var sign = go.GetComponentInChildren<TextMesh>(true);
            Assert.IsNotNull(sign, "the canopy carries no team name, so a row of garages says nothing about whose it is.");
            StringAssert.Contains("Test", sign.text);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---------------------------------------------------------------- the room

    // The one that matters. The shell's notch and the room's wall gap are worked out in different frames
    // — the rig's local one and the room's door-facing one — so a sign error anywhere leaves a garage you
    // can walk into and then find yourself standing in a wall. Run against a rig that is both rotated and
    // flipped, because an unrotated +X rig hides every sign mistake there is.
    [Test]
    public void TheRoomsDoorwayLinesUpWithTheDoorInTheShell()
    {
        var go = new GameObject("Garage");
        var host = new GameObject("GarageInterior");
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(31.5f, -12.25f, -0.5f), Quaternion.Euler(0f, 0f, 37f));
            var rig = Rig(go, -1, carAtHome: true);
            var room = Room(host, rig);

            var interior = host.transform.Find("InsideView/Interior");
            Assert.IsNotNull(interior, "the room was never generated.");
            Assert.AreEqual(0f, host.transform.position.z, 0.0001f,
                            "the room must sit in the ground plane — its quads are authored in world z.");

            var left = interior.Find("WallFrontL");
            var right = interior.Find("WallFrontR");
            Assert.IsNotNull(left, "the front wall has no left-hand segment.");
            Assert.IsNotNull(right, "the front wall has no right-hand segment.");

            float gapMin = left.localPosition.x + left.GetComponent<BoxCollider2D>().size.x * 0.5f;
            float gapMax = right.localPosition.x - right.GetComponent<BoxCollider2D>().size.x * 0.5f;
            Assert.AreEqual(DoorWidth, gapMax - gapMin, 0.01f, "the gap in the front wall is not the doorway's width.");

            // Where the room says the doorway is, in the world.
            float doorLine = left.localPosition.y;
            Vector3 roomDoor = interior.TransformPoint(new Vector3((gapMin + gapMax) * 0.5f, doorLine, 0f));

            // Where the shell says it is: the notch, pushed out by the lip the room adds so a player
            // stood in the doorway is already counted as inside.
            var shellDoor = (Vector3)Prop(rig, "DoorWorldPosition");
            var doorDir = (Vector2)Prop(rig, "DoorWorldDirection");
            Vector3 expected = shellDoor + (Vector3)(doorDir * (float)Field(room, "doorLip"));

            Assert.AreEqual(expected.x, roomDoor.x, 0.02f, "the room's doorway is not where the shell's notch is (x).");
            Assert.AreEqual(expected.y, roomDoor.y, 0.02f, "the room's doorway is not where the shell's notch is (y).");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(go);
        }
    }

    // Walking up to the shell has to be enough to be counted as inside: the enter line is roomFront minus
    // the hysteresis, and if the lip ever grows past the dead band that line lands outside the bodywork
    // and the mask flickers on while the player is still stood in the paddock.
    [Test]
    public void TheEnterLineSitsInsideTheBodywork()
    {
        var go = new GameObject("Garage");
        var host = new GameObject("GarageInterior");
        try
        {
            var rig = Rig(go, 1, carAtHome: true);
            var room = Room(host, rig);

            float lip = (float)Field(room, "doorLip");
            float hysteresis = (float)Field(room, "hysteresis");
            Assert.Less(lip, hysteresis,
                        "the door lip is bigger than the dead band, so the garage view switches on before the player reaches the doorway.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TheRoomIsMaskedAndHiddenUntilSomebodyWalksIn()
    {
        var go = new GameObject("Garage");
        var host = new GameObject("GarageInterior");
        try
        {
            var rig = Rig(go, 1, carAtHome: true);
            var room = Room(host, rig);

            var view = host.transform.Find("InsideView");
            Assert.IsNotNull(view, "the room has no InsideView to toggle.");
            Assert.IsFalse(view.gameObject.activeSelf,
                           "the garage interior is showing before anybody has walked through the door.");
            Assert.IsFalse((bool)Prop(room, "IsInside"), "the room thinks the player is already in it.");

            var mask = view.Find("BlackMask");
            Assert.IsNotNull(mask, "there is no blackout quad, so walking in would show the paddock through the walls.");
            Assert.Less(mask.localPosition.z, -1.5f, "the blackout quad is not in front of the world it has to hide.");
            Assert.IsNotNull(mask.GetComponent<MeshRenderer>(), "the blackout has to be opaque geometry to occlude anything.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TheRoomHasSomewhereForTheTeamToSitDown()
    {
        var go = new GameObject("Garage");
        var host = new GameObject("GarageInterior");
        try
        {
            var rig = Rig(go, 1, carAtHome: true);
            Room(host, rig);

            var interior = host.transform.Find("InsideView/Interior");
            Assert.IsNotNull(interior.Find("MeetingTable"), "there is no table to hold a team meeting at.");
            Assert.IsNotNull(interior.Find("SetupBoard"), "there is no setup board on the wall.");

            int chairs = 0;
            foreach (Transform child in interior)
                if (child.name.StartsWith("Chair")) chairs++;
            Assert.GreaterOrEqual(chairs, 6, "a team debrief needs more than a couple of seats.");

            // The table has to be away from the door, or walking in puts you in somebody's lap.
            var table = interior.Find("MeetingTable");
            var doormat = interior.Find("Doormat");
            Assert.IsNotNull(doormat, "there is no doorway in the room.");
            Assert.Greater(Mathf.Abs(table.localPosition.x - doormat.localPosition.x), 2f,
                           "the meeting table is right in the doorway.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(go);
        }
    }

    // ---------------------------------------------------------------- the lot

    // A motorhome lot that has already laid its row out, with `entries` slots in it — the state
    // PopupGarageLot is handed at play time, stood up here without a database, a track or a field.
    // Slot 0 is the player and carries a live car; the rest have none, which is the between-sessions
    // paddock (no cars are spawned outside a session).
    static Component MotorhomeLot(GameObject go, int entries, GameObject playerCar, out object layout)
    {
        var lot = go.AddComponent(LotType);

        var slotType = LotType.GetNestedType("Slot");
        Assert.IsNotNull(slotType, "DriverMotorhomeLot.Slot is gone.");
        var slotsField = LotType.GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(slotsField, "DriverMotorhomeLot no longer keeps its slots in _slots.");
        var slots = (System.Collections.IList)slotsField.GetValue(lot);

        for (int i = 0; i < entries; i++)
        {
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("carNumber").SetValue(slot, 20 + i);
            slotType.GetField("shortName").SetValue(slot, "Driver" + i);
            slotType.GetField("fullName").SetValue(slot, "Test Driver" + i);
            slotType.GetField("teamName").SetValue(slot, "Test Motorsports");
            slotType.GetField("isPlayer").SetValue(slot, i == 0);
            if (i == 0) slotType.GetField("car").SetValue(slot, playerCar);
            slots.Add(slot);
        }

        // The line the motorhomes parked on, the same call the lot makes.
        var compute = LotType.GetMethod("ComputeLine", BindingFlags.Public | BindingFlags.Static);
        layout = compute.Invoke(null, new object[]
        {
            Vector3.zero, Quaternion.identity, Vector2.right,
            BodyWidth, BodyLength, 2f, 4f, 2, entries, 0, -0.5f, true,
        });

        LotType.GetProperty("Line").GetSetMethod(true).Invoke(lot, new[] { layout });
        LotType.GetProperty("LineRows").GetSetMethod(true).Invoke(lot, new object[] { 2 });
        LotType.GetProperty("HasLine").GetSetMethod(true).Invoke(lot, new object[] { true });
        return lot;
    }

    static float LayoutFloat(object layout, string name) => (float)layout.GetType().GetField(name).GetValue(layout);
    static Vector3 LayoutVector(object layout, string name) => (Vector3)layout.GetType().GetField(name).GetValue(layout);

    // Where the block lands is the whole reason it reads as one paddock walked through in order rather
    // than as garages dropped on top of the motorhomes. Nothing about this can be seen in a scene — the
    // lot is generated at play time — so the placement is measured here.
    [Test]
    public void TheGaragesParkBeyondTheMotorhomes()
    {
        var lotGo = new GameObject("MotorhomeLot");
        var car = new GameObject("PlayerCar");
        GameObject garageGo = null;
        try
        {
            var lot = MotorhomeLot(lotGo, 6, car, out object layout);
            var garages = LotType.Assembly.GetType("PopupGarageLot")
                                 .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                                 .Invoke(null, new object[] { lot }) as Component;
            Assert.IsNotNull(garages, "PopupGarageLot.Create built nothing.");
            garageGo = garages.gameObject;

            Vector3 origin = LayoutVector(layout, "origin");
            Vector3 front = LayoutVector(layout, "front");
            float lastMotorhomeRow = LayoutFloat(layout, "rowPitch") * 1f;     // LineRows = 2, so row index 1
            float clearOf = lastMotorhomeRow + LayoutFloat(layout, "depth") * 0.5f;

            int rigs = 0;
            foreach (var rig in garageGo.GetComponentsInChildren(RigType))
            {
                rigs++;
                float along = Vector3.Dot(rig.transform.position - origin, front);
                Assert.Greater(along, clearOf,
                               $"'{rig.name}' is parked in among the motorhomes rather than behind them.");
            }
            Assert.AreEqual(6, rigs, "every entry in the paddock should have a garage.");

            var interiors = garageGo.transform.Find("GarageInteriors");
            Assert.IsNotNull(interiors, "no rooms were stood up behind the garage doors.");
            Assert.AreEqual(6, interiors.childCount, "every garage should have a room behind its door.");
        }
        finally
        {
            if (garageGo != null) Object.DestroyImmediate(garageGo);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(lotGo);
        }
    }

    // The rule the whole feature turns on: the car is at its garage whenever it is not somewhere else.
    // A driver with a live car in the world is out on track or sat in their pit box, so their canopy is
    // empty; between sessions there are no cars at all and the lot has its bodywork at home.
    [Test]
    public void OnlyTheCarsThatArentOutSitUnderTheirCanopies()
    {
        var lotGo = new GameObject("MotorhomeLot");
        var car = new GameObject("PlayerCar");
        GameObject garageGo = null;
        try
        {
            var lot = MotorhomeLot(lotGo, 4, car, out _);
            var garages = LotType.Assembly.GetType("PopupGarageLot")
                                 .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                                 .Invoke(null, new object[] { lot }) as Component;
            garageGo = garages.gameObject;

            int home = 0, away = 0;
            foreach (var rig in garageGo.GetComponentsInChildren(RigType))
            {
                bool atHome = (bool)Field(rig, "carAtHome");
                Assert.AreEqual(atHome, Prop(rig, "ParkedCar") != null,
                                $"'{rig.name}' says its car is {(atHome ? "home" : "out")} and has done the opposite.");
                if (atHome) home++; else away++;
            }

            Assert.AreEqual(1, away, "the one driver whose car is in the world should have an empty canopy.");
            Assert.AreEqual(3, home, "the drivers with no car out should have theirs parked at the garage.");
        }
        finally
        {
            if (garageGo != null) Object.DestroyImmediate(garageGo);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(lotGo);
        }
    }

    // The garages are spaced by the motorhome lot's own line maths, with the canopy folded into the
    // width. If that ever stops being true, every canopy is pitched over the next rig's bodywork.
    [Test]
    public void AGarageLineLeavesRoomForTheCanopyBetweenRigs()
    {
        Assert.IsNotNull(LotType, "DriverMotorhomeLot is missing from Assembly-CSharp.");
        var compute = LotType.GetMethod("ComputeLine", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(compute, "DriverMotorhomeLot.ComputeLine is gone; the garage lot lays its rows out with it.");

        object layout = compute.Invoke(null, new object[]
        {
            Vector3.zero, Quaternion.identity, Vector2.right,
            BodyWidth + CanopyWidth, BodyLength, LineGap, 7f,
            4, 40, 0, -0.5f, true,
        });

        float pitch = (float)layout.GetType().GetField("pitch").GetValue(layout);
        Assert.GreaterOrEqual(pitch, BodyWidth + CanopyWidth + LineGap - 0.001f,
                              "consecutive garages are closer together than a body plus its canopy — the awnings overlap the next rig.");

        // And the places really are that far apart, so the spacing isn't just a number on the struct.
        var placeAt = layout.GetType().GetMethod("PlaceAt");
        var first = (Vector3)placeAt.Invoke(layout, new object[] { 0 });
        var second = (Vector3)placeAt.Invoke(layout, new object[] { 1 });
        Assert.AreEqual(pitch, Vector3.Distance(first, second), 0.001f, "the line's places don't match its own pitch.");
    }
}
