using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Pit boxes belong on the paddock side of pit road, away from the racing surface: cars drive the open lane
// between the pit wall and the boxes. TrackBuilder.pitBoxLaneOnRight picks the side per package.
//
// Pinned because it went wrong silently: pitBoxLaneOnLeft (default = right) was renamed pitBoxLaneOnRight
// (default = left) with no FormerlySerializedAs, so every package fell to the left. Watkins Glen was re-ticked
// and committed, then an uncommitted re-save of the package dropped it again, putting its boxes and crews on the
// track side of the lane. Checked on the completed tracks only (Daytona, Texas, Watkins Glen).
//
// TrackBuilder is in Assembly-CSharp, which this assembly can't reference, so it is reached by reflection.
public class PitBoxSideTests
{
    static readonly Type Builder = Type.GetType("TrackBuilder, Assembly-CSharp", throwOnError: true);

    [TestCase("WatkinsGlen")]
    [TestCase("Daytona")]
    [TestCase("FortWorth")]
    public void TheBoxesAreOnTheSideAwayFromTheTrack(string trackId)
    {
        string path = $"Assets/Resources/TrackPackages/{trackId}.prefab";
        Assume.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, $"No package for {trackId}.");

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var tb = root.GetComponentInChildren(Builder, true);
            Assert.IsNotNull(tb, $"{trackId}'s package has no TrackBuilder.");
            if (!(bool)Builder.GetProperty("HasPitBoxLane").GetValue(tb)) Assert.Ignore($"{trackId} has no box lane.");

            float side = (float)Builder.GetProperty("PitBoxSide").GetValue(tb);
            var pit = (IList)Builder.GetMethod("SamplePitCenterline").Invoke(tb, null);
            var road = (IList)Builder.GetMethod("SampleCenterline").Invoke(tb, null);
            Assert.Greater(pit.Count, 2, $"{trackId}: no pit lane samples.");
            Assert.Greater(road.Count, 2, $"{trackId}: no track samples.");

            // Midway down pit road (the straight the boxes are on), which side of the pit lane is the track?
            object mid = pit[pit.Count / 2];
            var sampleType = mid.GetType();
            Vector2 Pos(object s) => (Vector2)sampleType.GetField("position").GetValue(s);
            Vector2 p = Pos(mid);
            Vector2 n = (Vector2)sampleType.GetField("normal").GetValue(mid);

            Vector2 nearest = Pos(road[0]);
            foreach (var s in road)
                if ((Pos(s) - p).sqrMagnitude < (nearest - p).sqrMagnitude) nearest = Pos(s);
            float trackSide = Mathf.Sign(Vector2.Dot(nearest - p, n));

            Assert.AreNotEqual(trackSide, side,
                $"{trackId}'s pit boxes are on the track side of pit road (PitBoxSide {side}, racing surface " +
                $"on {trackSide}). Flip TrackBuilder.pitBoxLaneOnRight in its package.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
