using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The painted pit-box lines go UNDER the cars parked between them.
//
// The lines are a mesh a hair in front of the box strip's tarmac in z. They used to be built with an opaque
// unlit material, and an opaque quad writes depth before the transparent car sprites are drawn — so every
// car parked in its box had white bars painted across it whatever the sorting orders said. Built on a sprite
// shader instead, the lines draw in the transparent queue where the sorting order decides, and that order sits
// below the cars'.
//
// Assembly-CSharp cannot be referenced from an asmdef, so TrackBuilder and GridSpawner are reached by
// reflection and the packages are read through SerializedObject, as in BuiltTrackAssetTests.
public class PitBoxLineLayeringTests
{
    const string PackageDir = "Assets/Resources/TrackPackages";
    const int TransparentQueue = 3000;

    static readonly System.Type BuilderType = System.Type.GetType("TrackBuilder, Assembly-CSharp");
    static readonly System.Type SpawnerType = System.Type.GetType("GridSpawner, Assembly-CSharp");

    static int DefaultCarSortingOrder()
    {
        var go = new GameObject("GridSpawner_Defaults");
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var spawner = go.AddComponent(SpawnerType);
            return (int)SpawnerType.GetField("carSortingOrder").GetValue(spawner);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void BuiltLineMaterialDrawsInTheTransparentQueue()
    {
        Assert.NotNull(BuilderType, "TrackBuilder is missing from Assembly-CSharp.");

        // Inactive host, so TrackBuilder's [ExecuteAlways] OnEnable doesn't try to build a road with no track.
        var go = new GameObject("TrackBuilder_Test");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.SetActive(false);
        Material mat = null;
        try
        {
            var builder = go.AddComponent(BuilderType);
            var build = BuilderType.GetMethod("BuildBoxLineMaterial",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(build, "TrackBuilder.BuildBoxLineMaterial has gone.");
            mat = (Material)build.Invoke(builder, null);
            Assert.GreaterOrEqual(mat.renderQueue, TransparentQueue,
                $"box lines built on '{mat.shader.name}' (queue {mat.renderQueue}) — an opaque line writes depth " +
                "in front of the tarmac and paints over the car sprites parked in the boxes.");
        }
        finally
        {
            if (mat != null) Object.DestroyImmediate(mat);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EveryPackageSortsItsBoxLinesUnderTheCars()
    {
        Assert.NotNull(BuilderType, "TrackBuilder is missing from Assembly-CSharp.");
        Assert.NotNull(SpawnerType, "GridSpawner is missing from Assembly-CSharp.");
        int carOrder = DefaultCarSortingOrder();

        foreach (var dim in TrackDimensions.All)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackageDir}/{dim.id}.prefab");
            Assert.IsNotNull(prefab, $"{dim.id}: no package prefab.");

            foreach (var builder in prefab.GetComponentsInChildren(BuilderType, true))
            {
                var so = new SerializedObject(builder);
                Assert.Less(so.FindProperty("pitBoxLineSortingOrder").intValue, carOrder,
                    $"{dim.id}: box lines sort at or above the cars ({carOrder}).");

                var custom = so.FindProperty("pitBoxLineMaterial").objectReferenceValue as Material;
                if (custom != null)
                    Assert.GreaterOrEqual(custom.renderQueue, TransparentQueue,
                        $"{dim.id}: box line material '{custom.name}' is opaque and will paint over the cars.");
            }
        }
    }
}
