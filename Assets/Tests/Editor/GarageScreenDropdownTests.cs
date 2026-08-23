using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The garage sheet's top bar browses the world: a series picker, and the field entered in that series.
// Both halves are data — the roster lookup answers who races where, and the pickers themselves are built
// into a generated scene — so neither can be checked by reading a script, and the sheet is a menu scene
// that only shows what it is bound to once it is in play mode.
//
// So the two things that would break silently are pinned here instead:
//   * SeriesRoster answers with a real championship list and a real field even with no database open,
//     which is the state every menu scene in the editor is in.
//   * The built scene actually carries the two dropdowns, wired to the sheet, laid out inside the header
//     band, and each one a TMP_Dropdown whose template is complete — an incomplete template only throws
//     when the list is clicked open, which is the one thing no EditMode test can press.
//
// Nothing here names GarageScreenUI's type: this assembly cannot reference Assembly-CSharp, so the
// component is read through SerializedObject the way the inspector reads it, and SeriesRoster is called
// by reflection.
public class GarageScreenDropdownTests
{
    const string GarageScenePath = "Assets/Scenes/GarageScreen.unity";

    Scene _garage;

    // Additive: the editor keeps whatever scene it had open while these run.
    [SetUp]
    public void OpenGarage() => _garage = EditorSceneManager.OpenScene(GarageScenePath, OpenSceneMode.Additive);

    [TearDown]
    public void CloseGarage()
    {
        if (_garage.IsValid() && _garage.isLoaded) EditorSceneManager.CloseScene(_garage, true);
    }

    // ---------------------------------------------------------------- the roster behind the pickers

    [Test]
    public void SeriesRosterListsEveryChampionshipWithoutADatabase()
    {
        var series = AllSeries();
        Assert.Greater(series.Count, 1, "The series picker would have nothing to cycle through.");

        foreach (var row in series)
            Assert.IsNotEmpty(Label(row), "A series with no label draws as an empty row in the dropdown.");
    }

    [Test]
    public void TheSeededFieldBacksTheTopStockCarSeries()
    {
        var series = AllSeries();
        object seeded = null;
        foreach (var row in series)
            if ((bool)Call("IsSeededField", row)) { seeded = row; break; }

        Assert.IsNotNull(seeded, "No top-tier stock-car series, so the seeded Cup field belongs to nobody.");

        var field = Drivers(seeded);
        Assert.Greater(field.Count, 20, "The top stock-car series should be entered by the seeded roster.");

        int previous = int.MinValue;
        foreach (var driver in field)
        {
            int number = (int)driver.GetType().GetProperty("CarNumber").GetValue(driver);
            Assert.Greater(number, 0, "A driver with no car number can't be picked out of a field.");
            Assert.GreaterOrEqual(number, previous, "The field should read in car-number order.");
            previous = number;

            StringAssert.StartsWith("#" + number, Label(driver),
                                    "A driver row should lead with the number, the way a timing tower does.");
        }
    }

    [Test]
    public void ASeriesNobodyIsEnteredInComesBackEmptyRatherThanBorrowed()
    {
        var series = AllSeries();
        foreach (var row in series)
        {
            if ((bool)Call("IsSeededField", row)) continue;
            Assert.IsEmpty(Drivers(row),
                           $"'{Label(row)}' has no entry list yet, so it must not show another series' drivers.");
        }
    }

    // ---------------------------------------------------------------- the pickers in the built scene

    [Test]
    public void TheSheetIsWiredToBothPickers()
    {
        var sheet = Sheet();
        Assert.IsNotNull(Picker(sheet, "seriesDropdown"), "The sheet has no series picker to read.");
        Assert.IsNotNull(Picker(sheet, "driverDropdown"), "The sheet has no driver picker to read.");
    }

    [Test]
    public void BothPickersSitInTheHeaderBandWithoutOverlapping()
    {
        var sheet = Sheet();
        var series = Picker(sheet, "seriesDropdown");
        var driver = Picker(sheet, "driverDropdown");

        var band = Band(series);
        Assert.IsNotNull(band, "The pickers belong in the header band, not loose on the canvas.");
        Assert.AreSame(band, Band(driver), "Both pickers should be in the same band.");

        var seriesRect = (RectTransform)series.transform;
        var driverRect = (RectTransform)driver.transform;

        foreach (var rect in new[] { seriesRect, driverRect })
        {
            Assert.LessOrEqual(rect.sizeDelta.y, band.sizeDelta.y,
                               "A picker taller than the band would hang over the columns below it.");
            Assert.GreaterOrEqual(rect.anchoredPosition.x, 0f, "A picker has been pushed off the left of the band.");
            Assert.LessOrEqual(rect.anchoredPosition.x + rect.sizeDelta.x, PixelUITheme_ReferenceWidth,
                               "A picker runs off the right of a 640-wide screen.");
        }

        float seriesRight = seriesRect.anchoredPosition.x + seriesRect.sizeDelta.x;
        Assert.LessOrEqual(seriesRight, driverRect.anchoredPosition.x,
                           "The two pickers overlap, so one can't be clicked.");
    }

    [Test]
    public void EveryPickerTemplateIsCompleteAndPutAway()
    {
        var sheet = Sheet();
        foreach (var name in new[] { "seriesDropdown", "driverDropdown" })
        {
            var picker = Picker(sheet, name);
            Assert.IsNotNull(picker.template, $"{name} has no template, so opening the list throws.");
            Assert.IsNotNull(picker.captionText, $"{name} has no caption, so the selection never shows.");
            Assert.IsNotNull(picker.itemText, $"{name} has no item label, so its rows draw blank.");

            Assert.IsFalse(picker.template.gameObject.activeSelf,
                           $"{name}'s template is left showing, so the list is drawn over the sheet.");

            // The parentage TMP_Dropdown checks when the list opens.
            Assert.IsTrue(picker.itemText.transform.IsChildOf(picker.template),
                          $"{name}'s item label is outside its template.");
            Assert.IsFalse(picker.captionText.transform.IsChildOf(picker.template),
                           $"{name}'s caption is inside the template, so the closed control shows nothing.");

            var toggle = picker.template.GetComponentInChildren<Toggle>(true);
            Assert.IsNotNull(toggle, $"{name}'s template has no item toggle — TMP_Dropdown rejects it on open.");

            var scroll = picker.template.GetComponent<ScrollRect>();
            Assert.IsNotNull(scroll, $"{name}'s template has no scroll rect, so a long field can't be reached.");
            Assert.IsNotNull(scroll.content, $"{name}'s scroll rect has no content.");
            Assert.IsNotNull(scroll.viewport, $"{name}'s scroll rect has no viewport.");
        }
    }

    // The builder fills the pickers once so the saved scene reads as a real series and a real field.
    [Test]
    public void TheBuiltSceneShipsWithBothListsFilled()
    {
        var sheet = Sheet();
        Assert.Greater(Picker(sheet, "seriesDropdown").options.Count, 1,
                       "The saved scene's series picker is empty; it was never filled at build time.");
        Assert.Greater(Picker(sheet, "driverDropdown").options.Count, 20,
                       "The saved scene's driver picker doesn't hold a full field.");
    }

    // ---------------------------------------------------------------- plumbing

    const float PixelUITheme_ReferenceWidth = 640f;

    SerializedObject Sheet()
    {
        foreach (var root in _garage.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == "GarageScreenUI")
                    return new SerializedObject(behaviour);

        Assert.Fail($"{GarageScenePath} has no GarageScreenUI on it.");
        return null;
    }

    static TMP_Dropdown Picker(SerializedObject sheet, string field)
    {
        var property = sheet.FindProperty(field);
        Assert.IsNotNull(property, $"GarageScreenUI has no '{field}' field any more.");
        return property.objectReferenceValue as TMP_Dropdown;
    }

    static RectTransform Band(TMP_Dropdown picker)
    {
        for (var t = picker.transform.parent; t != null; t = t.parent)
            if (t.name == "HeaderBand") return t as RectTransform;
        return null;
    }

    // SeriesRoster lives in Assembly-CSharp, which a test assembly can't reference — so it is called the
    // same way the inspector reads a component it has no type for.
    static readonly System.Type Roster = System.Type.GetType("SeriesRoster, Assembly-CSharp");

    static object Call(string method, params object[] args)
    {
        Assert.IsNotNull(Roster, "SeriesRoster is missing from Assembly-CSharp.");
        var found = Roster.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(found, $"SeriesRoster has no {method}().");
        return found.Invoke(null, args);
    }

    static List<object> AllSeries() => Rows(Call("AllSeries"));
    static List<object> Drivers(object series) => Rows(Call("Drivers", series));

    static string Label(object row)
    {
        foreach (var method in Roster.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "Label") continue;
            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(row))
                return (string)method.Invoke(null, new[] { row });
        }
        Assert.Fail("SeriesRoster has no Label() for " + row.GetType().Name);
        return null;
    }

    static List<object> Rows(object list)
    {
        var rows = new List<object>();
        foreach (var row in (IEnumerable)list) rows.Add(row);
        return rows;
    }
}
