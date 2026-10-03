using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The qualifying grid outlives a quit.
//
// Qualifying is Saturday and the race is Sunday, with a title screen possibly in between. The grid used to be
// a static, so a player who quit after qualifying came back through CAREER to a shuffled grid. These pin that
// it is read back off disk, and that it never leaks into a different weekend.
//
// A relaunch is stood in for by pointing the weekend id somewhere else and back: RaceWeekend re-reads the
// grid whenever the weekend it holds is not the current one, which is exactly the cold-start path.
//
// RaceWeekend lives in Assembly-CSharp, which an asmdef cannot reference, so it is reached by reflection.
public class QualifyingGridSaveTests
{
    const string IdKey = "raceweekend.id";
    const string GridKey = "raceweekend.grid";

    static readonly Type WeekendType = Type.GetType("RaceWeekend, Assembly-CSharp");
    static readonly Type EntryType = Type.GetType("RaceWeekend+GridEntry, Assembly-CSharp");

    static PropertyInfo GridProp =>
        WeekendType.GetProperty("GridOrder", BindingFlags.Public | BindingFlags.Static);

    bool _hadId, _hadGrid;
    int _id;
    string _grid;

    [SetUp]
    public void Save()
    {
        Assert.IsNotNull(WeekendType, "RaceWeekend is missing from Assembly-CSharp.");
        Assert.IsNotNull(EntryType, "RaceWeekend.GridEntry is missing from Assembly-CSharp.");
        Assert.IsNotNull(GridProp, "RaceWeekend.GridOrder is no longer a static property.");

        _hadId = PlayerPrefs.HasKey(IdKey);
        _id = PlayerPrefs.GetInt(IdKey, 0);
        _hadGrid = PlayerPrefs.HasKey(GridKey);
        _grid = PlayerPrefs.GetString(GridKey, "");
    }

    [TearDown]
    public void Restore()
    {
        if (_hadId) PlayerPrefs.SetInt(IdKey, _id); else PlayerPrefs.DeleteKey(IdKey);
        if (_hadGrid) PlayerPrefs.SetString(GridKey, _grid); else PlayerPrefs.DeleteKey(GridKey);
        PlayerPrefs.Save();
        if (GridProp != null) SwitchWeekend(_id);   // drop whatever the tests left in memory
    }

    static IList Grid
    {
        get => (IList)GridProp.GetValue(null);
        set => GridProp.SetValue(null, value);
    }

    static void SwitchWeekend(int id)
    {
        PlayerPrefs.SetInt(IdKey, id);
        _ = Grid;   // the read that notices the weekend changed
    }

    static object Entry(string name, int number, bool isPlayer, float bestLap)
    {
        var e = Activator.CreateInstance(EntryType);
        EntryType.GetField("driverName").SetValue(e, name);
        EntryType.GetField("carNumber").SetValue(e, number);
        EntryType.GetField("isPlayer").SetValue(e, isPlayer);
        EntryType.GetField("bestLap").SetValue(e, bestLap);
        return e;
    }

    static T Field<T>(object entry, string name) => (T)EntryType.GetField(name).GetValue(entry);

    static IList SampleGrid()
    {
        var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(EntryType));
        list.Add(Entry("Pole Sitter", 24, false, 52.31f));
        list.Add(Entry("Ace Emerson", 89, true, 52.40f));
        list.Add(Entry("No Time", 7, false, -1f));
        return list;
    }

    [Test]
    public void TheGridComesBackAfterARelaunch()
    {
        SwitchWeekend(9001);
        Grid = SampleGrid();

        SwitchWeekend(9002);
        SwitchWeekend(9001);

        var grid = Grid;
        Assert.IsNotNull(grid, "The qualifying grid was not read back off disk.");
        Assert.AreEqual(3, grid.Count);
        Assert.AreEqual("Pole Sitter", Field<string>(grid[0], "driverName"));
        Assert.AreEqual(89, Field<int>(grid[1], "carNumber"));
        Assert.IsTrue(Field<bool>(grid[1], "isPlayer"), "The player's slot has to survive, or they start from the back.");
        Assert.AreEqual(-1f, Field<float>(grid[2], "bestLap"), "A no-time car has to stay a no-time car.");
    }

    [Test]
    public void AGridNeverCarriesIntoAnotherWeekend()
    {
        SwitchWeekend(9001);
        Grid = SampleGrid();

        SwitchWeekend(9002);
        Assert.IsNull(Grid, "Last weekend's qualifying set this weekend's grid.");
    }

    [Test]
    public void ClearingTheGridClearsItOnDisk()
    {
        SwitchWeekend(9001);
        Grid = SampleGrid();
        Grid = null;

        SwitchWeekend(9002);
        SwitchWeekend(9001);
        Assert.IsNull(Grid, "A cleared grid came back after a relaunch.");
        Assert.IsFalse(PlayerPrefs.HasKey(GridKey));
    }
}
