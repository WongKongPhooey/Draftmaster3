using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Re-opening the setup sheet mid-session (back in the car after a tow, in practice or qualifying) must not be
// a free pit stop: only what the driver changed goes on the car. SetupAdjustment, read by
// CarSetup.ApplyChangesTo.
public class CarSetupAdjustTests
{
    [Test]
    public void UntouchedFuelSliderKeepsWhatIsLeft()
    {
        Assert.IsFalse(SetupAdjustment.ChangesFuel(12f, 12f));
        Assert.IsFalse(SetupAdjustment.ChangesFuel(12f, 12.2f), "Float noise is not a moved slider.");
    }

    [Test]
    public void MovedFuelSliderFillsToTheNewLoad()
    {
        Assert.IsTrue(SetupAdjustment.ChangesFuel(12f, 13f));
        Assert.IsTrue(SetupAdjustment.ChangesFuel(12f, 4f), "Draining counts as a change too.");
    }

    // The setup sheet sets grille tape on a slider (0-100% in 5% steps, 50% by default); the old soft/hard tyre
    // choice is gone.
    [Test]
    public void SetupPanelHasATapeSliderAndNoTyreChoice()
    {
        const string panelPrefab = "Assets/Resources/UI/CarSetupPanel.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelPrefab);
        Assert.IsNotNull(prefab, $"{panelPrefab} is missing.");

        var slider = prefab.transform.Find("Panel/Tape/Slider")?.GetComponent<Slider>();
        Assert.IsNotNull(slider, "CarSetupPanel has no Panel/Tape/Slider.");
        Assert.IsNotNull(prefab.transform.Find("Panel/Tape/Value")?.GetComponent<Text>(), "CarSetupPanel has no Panel/Tape/Value.");
        Assert.IsNotNull(prefab.transform.Find("Panel/Tape/Note")?.GetComponent<Text>(), "CarSetupPanel has no Panel/Tape/Note.");
        Assert.IsNull(prefab.transform.Find("Panel/Tyres"), "The soft/hard tyre row is still on the setup panel.");

        var setupType = System.Type.GetType("CarSetup, Assembly-CSharp");
        Assert.IsNotNull(setupType, "CarSetup is missing.");
        var tape = setupType.GetField("tape");
        Assert.IsNotNull(tape, "CarSetup has no tape field.");
        Assert.AreEqual(GrilleTape.Default, (float)tape.GetValue(System.Activator.CreateInstance(setupType)), 1e-6f,
                        "A new setup should open half taped.");
        Assert.IsNull(setupType.GetField("compound"), "CarSetup still carries a tyre compound.");
    }
}
