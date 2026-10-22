using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The steering ratio on the car setup panel: 1:6 to 1:16, with 1:8 meaning "how the car always steered".
// Ovals open on a slower rack than road courses.
public class SteeringRatioTests
{
    const string PanelPrefab = "Assets/Resources/UI/CarSetupPanel.prefab";

    [Test]
    public void BaselineLeavesSteeringUnchanged()
    {
        Assert.AreEqual(8f, SteeringRatio.Baseline);
        Assert.AreEqual(1f, SteeringRatio.Gain(SteeringRatio.Baseline), 1e-6f);
    }

    [Test]
    public void SlowerRackMeansLessSteering()
    {
        Assert.AreEqual(0.5f, SteeringRatio.Gain(16f), 1e-6f);
        Assert.AreEqual(8f / 6f, SteeringRatio.Gain(6f), 1e-6f);
        Assert.Less(SteeringRatio.Gain(12f), SteeringRatio.Gain(8f));
    }

    [Test]
    public void RatioIsClampedToSixToSixteen()
    {
        Assert.AreEqual(6f, SteeringRatio.Clamp(2f));
        Assert.AreEqual(16f, SteeringRatio.Clamp(40f));
        Assert.AreEqual(0.5f, SteeringRatio.Gain(100f), 1e-6f);
        Assert.AreEqual("1:16", SteeringRatio.Label(30f));
        Assert.AreEqual("1:8", SteeringRatio.Label(8f));
    }

    [Test]
    public void OvalsDefaultSlowerThanRoadCourses()
    {
        Assert.AreEqual(SteeringRatio.Baseline, SteeringRatio.DefaultFor(roadCourse: true));
        Assert.Greater(SteeringRatio.DefaultFor(roadCourse: false), SteeringRatio.DefaultFor(roadCourse: true));
    }

    [Test]
    public void SetupPanelHasASteeringSlider()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab);
        Assert.IsNotNull(prefab, $"{PanelPrefab} is missing.");

        var slider = prefab.transform.Find("Panel/Steering/Slider")?.GetComponent<Slider>();
        Assert.IsNotNull(slider, "CarSetupPanel has no Panel/Steering/Slider.");
        Assert.AreEqual(SteeringRatio.Min, slider.minValue);
        Assert.AreEqual(SteeringRatio.Max, slider.maxValue);
        Assert.IsTrue(slider.wholeNumbers);
        Assert.IsNotNull(prefab.transform.Find("Panel/Steering/Value")?.GetComponent<Text>(),
                         "CarSetupPanel has no Panel/Steering/Value label.");
    }

    // The steering row shipped in 0.7.6 with a localScale of 0 — present, wired, and invisible on every
    // screen. Nothing in the panel has any business being scaled away.
    [Test]
    public void NothingInTheSetupPanelIsScaledToNothing()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab);
        Assert.IsNotNull(prefab, $"{PanelPrefab} is missing.");

        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            // The root is an overlay Canvas: its scale is driven at runtime and saved as zero.
            if (t == prefab.transform) continue;
            var s = t.localScale;
            Assert.IsFalse(Mathf.Approximately(s.x, 0f) || Mathf.Approximately(s.y, 0f),
                           $"{AnimationUtility.CalculateTransformPath(t, prefab.transform)} has localScale {s}.");
        }
    }
}
