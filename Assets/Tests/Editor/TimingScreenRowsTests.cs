using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The F11 / crew-chief timing screen is three columns sized to fit, and never taller than the screen: a field
// with more cars than rows shows the front of the order, and the player's own line takes the last row when
// they would otherwise have been cut off. TimingScreenUI is Assembly-CSharp, so it is reached by reflection.
public class TimingScreenRowsTests
{
    static readonly System.Type ScreenType = System.Type.GetType("TimingScreenUI, Assembly-CSharp");
    static readonly System.Type CarType = System.Type.GetType("LapTimingManager+CarTimes, Assembly-CSharp");
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Rank a field of `cars` with the player at `playerAt` (-1 = no player), ask for `fit` rows, and return
    // the order indices it chose.
    static List<int> Pick(int cars, int playerAt, int fit)
    {
        Assert.IsNotNull(ScreenType, "TimingScreenUI not found in Assembly-CSharp.");
        Assert.IsNotNull(CarType, "LapTimingManager.CarTimes not found in Assembly-CSharp.");

        var go = new GameObject("TimingScreenUI (test)");
        try
        {
            var screen = go.AddComponent(ScreenType);
            var sorted = (IList)ScreenType.GetField("_sorted", Any).GetValue(screen);
            for (int i = 0; i < cars; i++)
            {
                var car = System.Activator.CreateInstance(CarType);
                CarType.GetField("isPlayer").SetValue(car, i == playerAt);
                sorted.Add(car);
            }

            ScreenType.GetMethod("PickRows", Any).Invoke(screen, new object[] { fit });
            return new List<int>((List<int>)ScreenType.GetField("_shown", Any).GetValue(screen));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AFieldThatFits_IsShownWhole()
    {
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Pick(cars: 6, playerAt: 3, fit: 10));
    }

    [Test]
    public void ALongField_ShowsTheFrontOfTheOrder()
    {
        var shown = Pick(cars: 40, playerAt: -1, fit: 12);
        Assert.AreEqual(12, shown.Count, "More rows than the screen has room for.");
        CollectionAssert.AreEqual(System.Linq.Enumerable.Range(0, 12), shown);
    }

    [Test]
    public void APlayerOutsideTheCut_TakesTheLastRow()
    {
        var shown = Pick(cars: 40, playerAt: 25, fit: 10);
        Assert.AreEqual(10, shown.Count);
        Assert.AreEqual(25, shown[9], "The player's own line should replace the last row when they are off the bottom.");
        CollectionAssert.AreEqual(System.Linq.Enumerable.Range(0, 9), shown.GetRange(0, 9));
    }

    [Test]
    public void APlayerInsideTheCut_IsNotShownTwice()
    {
        var shown = Pick(cars: 40, playerAt: 4, fit: 10);
        CollectionAssert.AreEqual(System.Linq.Enumerable.Range(0, 10), shown);
    }
}
