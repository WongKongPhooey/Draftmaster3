using System.Collections.Generic;
using System.IO;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Checks the assets that are actually ON DISK, not the solver that made them.
//
// TrackDimensionsTests proves the geometry maths is right. That is a different claim from "the 37 files in
// Resources/Tracks are right": an asset can be stale, half-written, generated before a fix landed, or
// silently skipped by a batch build that still reported success. That last one really happened — the first
// run of Build All wrapped itself in AssetDatabase.StartAssetEditing(), which pauses importing, so every
// package failed to find the layout written a line earlier and 37 tracks came out with no package at all
// while the summary said "built 37". These tests are what catches that.
//
// WHY SerializedObject RATHER THAN THE TYPE ITSELF: TrackInfoV2, TrackBuilder and TrackPackage live in
// Assembly-CSharp, and an assembly definition cannot reference the predefined assemblies — the same wall
// the Draftmaster.Sim split ran into. So the assets are read through their serialised fields instead.
// That turns out to be the better test: the lap length and closure below are recomputed from the stored
// segment list by an independent walk, so this is checking the DATA, not re-running the code that wrote it.
public class BuiltTrackAssetTests
{
    const string GeometryDir = "Assets/Resources/Tracks";
    const string PackageDir = "Assets/Resources/TrackPackages";

    static SerializedObject Load(string id)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{GeometryDir}/{id}.asset");
        return asset == null ? null : new SerializedObject(asset);
    }

    // Walk the stored segments the way TrackInfoV2 does, but with this file's own arithmetic.
    static void Measure(SerializedObject track, out float lapMetres, out float turnDegrees, out Vector2 end)
    {
        lapMetres = 0f;
        turnDegrees = 0f;
        Vector2 pos = Vector2.zero;
        float heading = track.FindProperty("startHeading").floatValue * Mathf.Deg2Rad;

        var segments = track.FindProperty("segments");
        for (int i = 0; i < segments.arraySize; i++)
        {
            var seg = segments.GetArrayElementAtIndex(i);
            float length = seg.FindPropertyRelative("length").floatValue;
            float angle = seg.FindPropertyRelative("angle").floatValue;

            lapMetres += length;
            turnDegrees += angle;

            Vector2 local;
            if (Mathf.Abs(angle) < 1e-6f)
            {
                local = new Vector2(length, 0f);
            }
            else
            {
                float a = angle * Mathf.Deg2Rad;
                float r = length / Mathf.Abs(a);
                float sign = angle >= 0f ? 1f : -1f;
                local = new Vector2(r * Mathf.Sin(Mathf.Abs(a)), sign * r * (1f - Mathf.Cos(a)));
            }

            float c = Mathf.Cos(heading), s = Mathf.Sin(heading);
            pos += new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
            heading += angle * Mathf.Deg2Rad;
        }

        var start = track.FindProperty("startPosition").vector2Value;
        end = pos + start;
    }

    [Test]
    public void EveryVenueHasABuiltLayoutAndPackage()
    {
        var missingGeometry = new List<string>();
        var missingPackage = new List<string>();

        foreach (var dim in TrackDimensions.All)
        {
            if (!File.Exists($"{GeometryDir}/{dim.id}.asset")) missingGeometry.Add(dim.id);
            if (!File.Exists($"{PackageDir}/{dim.id}.prefab")) missingPackage.Add(dim.id);
        }

        Assert.IsEmpty(missingGeometry,
                       "No layout asset — run Draftmaster > Tracks > Build All Calendar Tracks: "
                       + string.Join(", ", missingGeometry));
        Assert.IsEmpty(missingPackage,
                       "No package prefab — run Draftmaster > Tracks > Build All Calendar Tracks: "
                       + string.Join(", ", missingPackage));
    }

    // The headline claim of the whole exercise: the road you drive is the length the venue publishes.
    [Test]
    public void EveryBuiltTrackMeasuresItsPublishedLength()
    {
        foreach (var dim in TrackDimensions.All)
        {
            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            Measure(track, out float metres, out _, out _);
            float miles = metres / OvalGeometry.MetresPerMile;

            // Watkins Glen is hand-measured off satellite imagery rather than generated, so it is allowed
            // to disagree with the published round number — a real measurement beats a marketing figure.
            float tolerance = dim.id == RoadCourseLayouts.HandAuthored ? 0.06f : 0.01f;
            Assert.AreEqual(dim.lapMiles, miles, tolerance,
                            $"{dim.id}: built lap is {miles:0.###} mi, published {dim.lapMiles:0.###} mi.");
        }
    }

    // The bug this whole table exists to fix: width used to come from the track TYPE.
    [Test]
    public void EveryBuiltTrackIsItsPublishedWidth()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.id == RoadCourseLayouts.HandAuthored) continue;   // measured, not published

            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            float width = track.FindProperty("defaultWidth").floatValue;
            Assert.AreEqual(dim.widthMetres, width, 0.05f,
                            $"{dim.id}: built {width:0.0} m wide, published {dim.widthMetres:0.0} m.");
        }
    }

    // A lap that does not shut leaves a kink in the road at the start/finish line, and cars drive into it.
    [Test]
    public void EveryBuiltTrackClosesBackOntoItsStartLine()
    {
        foreach (var dim in TrackDimensions.All)
        {
            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            Measure(track, out _, out float turns, out var end);
            var start = track.FindProperty("startPosition").vector2Value;
            float gap = Vector2.Distance(end, start);

            // Watkins Glen is hand-authored and carries whatever seam its author accepted.
            float tolerance = dim.id == RoadCourseLayouts.HandAuthored ? 60f
                                                                      : Mathf.Max(5f, dim.LapMetres * 0.01f);
            Assert.Less(gap, tolerance, $"{dim.id}: lap misses its own start by {gap:0.0} m.");
            Assert.AreEqual(360f, Mathf.Abs(turns), 1f,
                            $"{dim.id}: segments turn through {turns:0.#}°, not one revolution.");
        }
    }

    // PaddockSpawner frames its crowd alongside the longest STRAIGHT in the pit lane, 30 m deep. That
    // rectangle is what decides how many of a 400-strong paddock are inside the CrowdDirector's 25 m
    // reduced radius at once, and therefore what the crowd costs per frame — so the venue with the
    // shortest pit road is the one that has to stay inside the budget, not the roomy ovals.
    //
    // Both pit-lane builders emit exactly one "Pit Road" straight between two tapers
    // (OvalGeometry.BuildPitLane / RoadCourseGeometry.BuildPitLane) with a floor of 60 m, and Watkins
    // Glen's is hand-authored, so this reads the assets rather than trusting either.
    [Test]
    public void ThePaddockCrowdFitsItsFrameBudgetAtEveryVenue()
    {
        const float paddockDepth = 30f;          // PaddockSpawner.paddockDepth
        const float perAwakeMs = 0.0045f;        // CrowdBenchmarkTests: cost of one awake NPC
        const float frameMs = 1000f / 60f;
        var tuning = Draftmaster.Crowd.CrowdTuning.Default;
        int crowd = Draftmaster.Crowd.CrowdPolicy.ComfortableMaxPopulation;

        string tightest = null;
        float tightestLen = float.MaxValue, worstCost = 0f;

        foreach (var dim in TrackDimensions.All)
        {
            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            var pit = track.FindProperty("pitSegments");
            float longestStraight = 0f;
            for (int i = 0; i < pit.arraySize; i++)
            {
                var seg = pit.GetArrayElementAtIndex(i);
                if (seg.FindPropertyRelative("type").enumValueIndex != 0) continue;   // 0 = Straight
                longestStraight = Mathf.Max(longestStraight, seg.FindPropertyRelative("length").floatValue);
            }

            Assert.Greater(longestStraight, 40f,
                           $"{dim.id}: pit lane has no straight worth putting a paddock beside.");

            float awake = Draftmaster.Crowd.CrowdPolicy.ExpectedAwakeCount(
                crowd, longestStraight, paddockDepth, tuning);
            float cost = awake * perAwakeMs;

            Assert.Less(cost, frameMs / 4f,
                        $"{dim.id}: a {crowd}-strong paddock over {longestStraight:0} x {paddockDepth:0} m " +
                        $"leaves {awake:0} awake = {cost:0.00} ms/frame.");

            if (longestStraight < tightestLen) { tightestLen = longestStraight; tightest = dim.id; }
            worstCost = Mathf.Max(worstCost, cost);
        }

        TestContext.WriteLine($"Tightest paddock: {tightest} at {tightestLen:0} x {paddockDepth:0} m; " +
                              $"worst case {worstCost:0.00} ms/frame for {crowd} NPCs.");
    }

    [Test]
    public void EveryBuiltTrackHasAPitLaneAndARaceDistance()
    {
        foreach (var dim in TrackDimensions.All)
        {
            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            Assert.Greater(track.FindProperty("segments").arraySize, 2, $"{dim.id}: too few segments for a lap.");
            Assert.IsTrue(track.FindProperty("hasPitLane").boolValue, $"{dim.id}: no pit lane — nowhere to stop.");
            Assert.Greater(track.FindProperty("pitSegments").arraySize, 0, $"{dim.id}: pit lane flagged but empty.");
            Assert.Greater(track.FindProperty("pitSpeedLimit").intValue, 0, $"{dim.id}: no pit speed limit.");
            Assert.Greater(track.FindProperty("trackLaps").intValue, 0, $"{dim.id}: no race distance.");
        }
    }

    // Corner speed hints, which SplineDriver reads to know how early to brake.
    //
    // ZERO IS LEGAL and means "no authored cap - work it out from the radius and the car's cornering
    // curve": every read site guards with `seg.maxSpeed > 0` (SplineDriver.ComputeTargetSpeedForSegment
    // falls through to a radius calculation). Watkins Glen, authored by hand, leaves several corners at 0
    // on purpose. What would actually break a race is a hint that is present but absurd, so that is what
    // this checks.
    [Test]
    public void CornerSpeedHintsAreEitherAbsentOrSane()
    {
        foreach (var dim in TrackDimensions.All)
        {
            var track = Load(dim.id);
            if (track == null) continue;

            var segments = track.FindProperty("segments");
            for (int i = 0; i < segments.arraySize; i++)
            {
                var seg = segments.GetArrayElementAtIndex(i);
                if (Mathf.Abs(seg.FindPropertyRelative("angle").floatValue) < 0.05f) continue;

                int speed = seg.FindPropertyRelative("maxSpeed").intValue;
                if (speed == 0) continue;   // derive it from the geometry

                string label = seg.FindPropertyRelative("label").stringValue;
                Assert.Greater(speed, 20, $"{dim.id}/{label}: a {speed} mph cap would stop the field dead.");
                Assert.LessOrEqual(speed, 235, $"{dim.id}/{label}: {speed} mph through a corner.");
            }
        }
    }

    // The generated tracks, unlike the hand-authored one, should always supply the hint - both factories
    // compute it. A generated corner arriving at 0 means the speed model was skipped.
    [Test]
    public void EveryGeneratedCornerCarriesASpeedHint()
    {
        foreach (var dim in TrackDimensions.All)
        {
            if (dim.id == RoadCourseLayouts.HandAuthored) continue;

            var track = Load(dim.id);
            Assert.IsNotNull(track, $"{dim.id}: no built asset.");

            var segments = track.FindProperty("segments");
            for (int i = 0; i < segments.arraySize; i++)
            {
                var seg = segments.GetArrayElementAtIndex(i);
                if (Mathf.Abs(seg.FindPropertyRelative("angle").floatValue) < 0.05f) continue;

                string label = seg.FindPropertyRelative("label").stringValue;
                Assert.Greater(seg.FindPropertyRelative("maxSpeed").intValue, 20,
                               $"{dim.id}/{label}: generated corner with no speed hint.");
            }
        }
    }

    // The package is what TrackSceneLoader drops into the shared race scene. A package whose TrackBuilder
    // lost its geometry reference loads a race with no road in it.
    [Test]
    public void EveryPackageIsWiredToItsOwnGeometry()
    {
        foreach (var dim in TrackDimensions.All)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackageDir}/{dim.id}.prefab");
            Assert.IsNotNull(prefab, $"{dim.id}: no package prefab.");

            var geometry = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{GeometryDir}/{dim.id}.asset");
            Assert.IsNotNull(geometry, $"{dim.id}: no geometry asset for the package to point at.");

            bool wired = false;
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                var track = so.FindProperty("track");
                if (track != null && track.propertyType == SerializedPropertyType.ObjectReference &&
                    track.objectReferenceValue == geometry)
                {
                    wired = true;
                    break;
                }
            }

            Assert.IsTrue(wired, $"{dim.id}: nothing in the package prefab points at {dim.id}.asset — " +
                                 "the race would load with no road in it.");
        }
    }
}
