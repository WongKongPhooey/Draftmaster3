using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The two pieces of game data that moved out of code and binary prefabs into text, so they can be written in
// parallel (by hand or by overnight agents) and checked without Unity:
//
//   - the travel map's shops and junkyards, Resources/Travel/Stops/<Region>.json, read by TravelGraph;
//   - paddock sidecars, Resources/Paddocks/<TrackId>.json, applied to a track package by PaddockSidecar.
//
// Their Python validators (Tools/validate_travel_stops.py, Tools/paddock_sidecar.py) check the files; these
// check the game reads them the way the validators assume. Assembly-CSharp types are reached by reflection,
// as in PaddockLotAreaTests.
public class TravelStopsAndPaddockSidecarTests
{
    static readonly Type Graph = Type.GetType("TravelGraph, Assembly-CSharp");
    static readonly Type StopsFile = Type.GetType("TravelGraph+StopsFile, Assembly-CSharp");
    static readonly Type Parts = Type.GetType("PartCatalog, Assembly-CSharp");
    static readonly Type Sidecar = Type.GetType("PaddockSidecar, Assembly-CSharp");
    static readonly Type SidecarFile = Type.GetType("PaddockSidecar+File, Assembly-CSharp");
    static readonly Type Package = Type.GetType("TrackPackage, Assembly-CSharp");
    static readonly Type Marker = Type.GetType("WeekendMarker, Assembly-CSharp");
    static readonly Type LotArea = Type.GetType("PaddockLotArea, Assembly-CSharp");

    static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

    [Test]
    public void EveryStopInTheDataSplitsARoadOnTheMap()
    {
        Assert.IsNotNull(Graph);
        Assert.IsNotNull(StopsFile);
        var files = Resources.LoadAll<TextAsset>((string)Graph.GetField("StopsFolder").GetValue(null));
        Assert.GreaterOrEqual(files.Length, 1, "no stop files");

        var get = Graph.GetMethod("Get");
        var neighbors = Graph.GetMethod("Neighbors");
        int count = 0;
        foreach (var f in files)
        {
            var data = JsonUtility.FromJson(f.text, StopsFile);
            foreach (var stop in (Array)Field(data, "stops"))
            {
                string id = (string)Field(stop, "id");
                var node = get.Invoke(null, new object[] { id });
                Assert.IsNotNull(node, $"{f.name}: '{id}' is not on the map");
                Assert.AreEqual((string)Field(stop, "type"), Field(node, "locationType").ToString(), id);
                var roads = (IReadOnlyList<string>)neighbors.Invoke(null, new object[] { id });
                Assert.AreEqual(2, roads.Count, $"'{id}' should sit on one road, joined to both its ends");
                count++;
            }
        }
        Assert.GreaterOrEqual(count, 20, "the 20 stops that used to be in code");
    }

    [Test]
    public void EveryShopStocksRealParts()
    {
        var get = Parts.GetMethod("Get");
        foreach (var node in (System.Collections.IEnumerable)Graph.GetProperty("Nodes").GetValue(null))
        {
            if (!(Field(node, "shopStock") is string[] stock)) continue;
            foreach (var part in stock)
                Assert.IsNotNull(get.Invoke(null, new object[] { part }), $"{Field(node, "id")} sells unknown part '{part}'");
        }
    }

    [Test]
    public void SidecarFillsGapsInPackageLocalSpaceAndNeverOverridesThePackage()
    {
        var root = new GameObject("Track_Test");
        try
        {
            // A package that isn't at the origin: sidecar numbers are package-local.
            root.transform.position = new Vector3(100f, 50f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var package = root.AddComponent(Package);
            Package.GetField("trackId").SetValue(package, "Test");
            new GameObject("Grandstand_Marker").transform.SetParent(root.transform, false);   // authored

            const string json = "{\"trackId\":\"Test\",\"markers\":[" +
                "{\"name\":\"Grandstand_Marker\",\"x\":1,\"y\":1}," +
                "{\"name\":\"PitBox_Marker\",\"x\":10,\"y\":0,\"rotation\":30,\"width\":4,\"height\":3,\"seat\":{\"x\":12,\"y\":2}}," +
                "{\"name\":\"SigningFence_Marker\",\"x\":30,\"y\":5}," +
                "{\"name\":\"Nonsense_Marker\",\"x\":0,\"y\":0}]," +
                "\"lots\":[{\"kind\":\"Motorhomes\",\"x\":0,\"y\":-40,\"rotation\":15,\"width\":120,\"height\":50,\"gap\":3,\"rowGap\":7}," +
                "{\"kind\":\"Motorhomes\",\"x\":0,\"y\":80,\"width\":50,\"height\":50}]}";
            var data = JsonUtility.FromJson(json, SidecarFile);
            var apply = Sidecar.GetMethod("Apply", new[] { Package, SidecarFile });

            int added = (int)apply.Invoke(null, new[] { package, data });
            Assert.AreEqual(3, added, "PitBox + SigningFence markers + one Motorhomes lot (Grandstand is authored, Nonsense names no venue, " +
                                      "and only the first lot of a kind counts)");

            var pit = root.transform.Find("PaddockSidecar/PitBox_Marker");
            Assert.IsNotNull(pit);
            Vector3 local = root.transform.InverseTransformPoint(pit.position);
            Assert.AreEqual(10f, local.x, 1e-3f);
            Assert.AreEqual(0f, local.y, 1e-3f);
            Assert.AreEqual(30f, (Quaternion.Inverse(root.transform.rotation) * pit.rotation).eulerAngles.z, 1e-2f);
            Assert.AreEqual(new Vector2(4f, 3f), pit.GetComponent<BoxCollider2D>().size);
            Assert.IsTrue(pit.GetComponent<BoxCollider2D>().isTrigger);
            var marker = pit.GetComponent(Marker);
            Assert.IsNotNull(marker);
            Assert.AreEqual("PitBox", Marker.GetField("venue").GetValue(marker).ToString());
            var seat = (Transform)Marker.GetField("teleportTo").GetValue(marker);
            Assert.IsNotNull(seat);
            Vector3 seatLocal = root.transform.InverseTransformPoint(seat.position);
            Assert.AreEqual(12f, seatLocal.x, 1e-3f);
            Assert.AreEqual(2f, seatLocal.y, 1e-3f);

            // No seat in the JSON = no teleport — JsonUtility hands back a Point anyway, which must not count.
            var fence = root.transform.Find("PaddockSidecar/SigningFence_Marker");
            Assert.IsNotNull(fence);
            Assert.IsNull(Marker.GetField("teleportTo").GetValue(fence.GetComponent(Marker)) as Transform);
            Assert.IsNull(fence.Find("Seat"));

            var lot = root.transform.Find("PaddockSidecar/PaddockLotArea_Motorhomes");
            Assert.IsNotNull(lot);
            Assert.AreEqual(-40f, root.transform.InverseTransformPoint(lot.position).y, 1e-3f);
            Assert.AreEqual(new Vector2(120f, 50f), lot.GetComponent<BoxCollider2D>().size);
            var area = lot.GetComponent(LotArea);
            Assert.AreEqual(3f, (float)LotArea.GetField("gap").GetValue(area), 1e-4f);
            Assert.AreEqual(7f, (float)LotArea.GetField("rowGap").GetValue(area), 1e-4f);
            Assert.IsTrue(lot.gameObject.activeSelf);

            Assert.AreEqual(0, (int)apply.Invoke(null, new[] { package, data }), "a second apply adds nothing");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void VariantIsStableForAVisitAndEveryVariantGetsUsed()
    {
        var pick = Sidecar.GetMethod("PickVariant");
        int a = (int)pick.Invoke(null, new object[] { "Dover", 4, 7 });
        Assert.AreEqual(a, (int)pick.Invoke(null, new object[] { "Dover", 4, 7 }), "same track, same week, same layout");
        var seen = new HashSet<int>();
        for (int week = 0; week < 40; week++) seen.Add((int)pick.Invoke(null, new object[] { "Dover", 4, week }));
        Assert.AreEqual(4, seen.Count, "over a season every layout comes round");
        Assert.AreEqual(-1, (int)pick.Invoke(null, new object[] { "Dover", 0, 3 }));
    }

    [Test]
    public void EveryGeneratedSidecarHasTheFullPaddockInEveryVariant()
    {
        var files = Resources.LoadAll<TextAsset>((string)Sidecar.GetField("Folder").GetValue(null));
        foreach (var f in files)
        {
            var data = JsonUtility.FromJson(f.text, SidecarFile);
            var variants = (Array)Field(data, "variants");
            if (variants == null) continue;
            foreach (var v in variants)
            {
                var names = new HashSet<string>();
                foreach (var m in (Array)Field(v, "markers")) names.Add((string)Field(m, "name"));
                foreach (var want in new[] { "SponsorSuite_Marker", "SigningFence_Marker", "MeetingRoom_Marker",
                                             "IntroStage_Marker", "Grandstand_Marker" })
                    Assert.IsTrue(names.Contains(want), $"{f.name}: a variant has no {want}");
                var kinds = new HashSet<string>();
                foreach (var l in (Array)Field(v, "lots")) kinds.Add((string)Field(l, "kind"));
                Assert.IsTrue(kinds.Contains("Motorhomes") && kinds.Contains("Garages"), $"{f.name}: a variant is missing a lot");
            }
        }
    }

    [Test]
    public void ALoadedVariantReadsBackTheSameInPackageSpace()
    {
        var frame = new GameObject("Frame");
        var holder = new GameObject("Holder");
        try
        {
            frame.transform.position = new Vector3(-30f, 12f, 0f);
            frame.transform.rotation = Quaternion.Euler(0f, 0f, 40f);
            const string json = "{\"markers\":[" +
                "{\"name\":\"Grandstand_Marker\",\"x\":5,\"y\":6,\"rotation\":20,\"width\":10,\"height\":8,\"seat\":{\"x\":50,\"y\":-60},\"view\":{\"x\":30,\"y\":-20}}," +
                "{\"name\":\"SponsorSuite_Marker\",\"x\":-15,\"y\":40,\"width\":14,\"height\":12}]," +
                "\"lots\":[{\"kind\":\"Garages\",\"x\":100,\"y\":-20,\"rotation\":95,\"width\":150,\"height\":45,\"gap\":2.5,\"rowGap\":7}]}";
            var variantType = Type.GetType("PaddockSidecar+Variant, Assembly-CSharp");
            var v = JsonUtility.FromJson(json, variantType);
            var buildMarker = Sidecar.GetMethod("BuildMarker");
            var buildLot = Sidecar.GetMethod("BuildLot");
            foreach (var m in (Array)Field(v, "markers")) buildMarker.Invoke(null, new[] { frame.transform, holder.transform, m });
            foreach (var l in (Array)Field(v, "lots")) buildLot.Invoke(null, new[] { frame.transform, holder.transform, l });

            var back = Sidecar.GetMethod("ReadLayout").Invoke(null, new object[] { frame.transform, holder.transform });
            var markers = (Array)Field(back, "markers");
            Assert.AreEqual(2, markers.Length);
            var g = markers.GetValue(0);
            Assert.AreEqual("Grandstand_Marker", Field(g, "name"));
            Assert.AreEqual(5f, (float)Field(g, "x"), 0.06f);
            Assert.AreEqual(6f, (float)Field(g, "y"), 0.06f);
            Assert.AreEqual(20f, (float)Field(g, "rotation"), 0.06f);
            Assert.AreEqual(10f, (float)Field(g, "width"), 0.06f);
            Assert.AreEqual(50f, (float)Field(Field(g, "seat"), "x"), 0.06f);
            Assert.AreEqual(-20f, (float)Field(Field(g, "view"), "y"), 0.06f);
            var has = Sidecar.GetMethod("Has");
            Assert.IsFalse((bool)has.Invoke(null, new[] { Field(markers.GetValue(1), "seat") }), "no seat stays no seat");
            var lot = ((Array)Field(back, "lots")).GetValue(0);
            Assert.AreEqual("Garages", Field(lot, "kind"));
            Assert.AreEqual(100f, (float)Field(lot, "x"), 0.06f);
            Assert.AreEqual(95f, (float)Field(lot, "rotation"), 0.06f);
            Assert.AreEqual(45f, (float)Field(lot, "height"), 0.06f);
            Assert.AreEqual(2.5f, (float)Field(lot, "gap"), 0.06f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(frame);
            UnityEngine.Object.DestroyImmediate(holder);
        }
    }
}
