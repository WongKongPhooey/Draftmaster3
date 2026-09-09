using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Why the co-op guest is handed the host's paddock row instead of solving one of its own.
//
// DriverMotorhomeLot.ComputeLine turns "an anchor, a direction and N rigs" into the line that hands out
// every parking place in the lot. Everything else in the paddock is measured off the block that line
// produces: the team garages continue it, the walkable pocket is cut round it, the player's own motorhome
// is driven into a place in it, and WeekendVenueSites lays the drivers' room, the hospitality awning, the
// fan fence and the intro stage out in the first clear ground past the end of it.
//
// The two rules below are the whole argument for CoopPaddockMirror:
//
//   * the same line gives the same places, so shipping the line is enough to make two machines agree;
//   * N is not a detail — one rig more or fewer re-shapes the row, so two peers that each counted their
//     own field would disagree about where every one of those places is.
//
// A guest used to skip the lot entirely, which is the extreme case of the second rule: nothing parked, so
// the venue cluster was laid out from the middle of an empty paddock and four rooms' worth of walls ended
// up tens of metres from where the host had them.
//
// As with CoopRowStateTests, nothing here names a type from Assembly-CSharp: this assembly cannot
// reference the predefined assemblies, so the lot is reached by reflection.
public class CoopPaddockLayoutTests
{
    static MethodInfo _computeLine;
    static MethodInfo _placeAt;

    // The numbers the lot ships with, so this reads as a real paddock rather than as unit-test soup.
    const float RvWidth = 3.95f;
    const float RvLength = 9.93f;
    const float LineGap = 2f;
    const float RowGap = 4f;
    const int Rows = 2;
    const float Z = -0.5f;

    [OneTimeSetUp]
    public void FindMethods()
    {
        var lot = AppDomain.CurrentDomain.GetAssemblies()
                           .Select(a => a.GetType("DriverMotorhomeLot", false))
                           .FirstOrDefault(t => t != null);
        Assert.IsNotNull(lot, "No DriverMotorhomeLot type — the motorhome lot has moved or been renamed.");

        _computeLine = lot.GetMethod("ComputeLine", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(_computeLine, "DriverMotorhomeLot has no static ComputeLine — the row's layout is " +
                                       "no longer a pure function, so it can no longer be handed to a peer.");

        var layout = lot.GetNestedType("LineLayout", BindingFlags.Public);
        Assert.IsNotNull(layout, "DriverMotorhomeLot has no public LineLayout — there is nothing for the " +
                                 "co-op mirror to send.");

        _placeAt = layout.GetMethod("PlaceAt", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(_placeAt, "LineLayout has no PlaceAt — parking places are no longer derived from " +
                                   "the line alone.");
    }

    // One lot, laid out for `total` rigs, with the player holding place `playerIndex`.
    static object Line(int total, int playerIndex = 0, float rowGap = RowGap)
        => _computeLine.Invoke(null, new object[]
        {
            new Vector3(120f, -40f, Z),          // anchor: the player's own RV
            Quaternion.Euler(0f, 0f, 25f),       // parked at an angle, so nothing here is axis-aligned by luck
            Vector2.right,                       // the line runs to the right of the anchor
            RvWidth, RvLength, LineGap, rowGap,
            Rows, total, playerIndex, Z,
            true,
        });

    static Vector3 PlaceAt(object line, int index)
        => (Vector3)_placeAt.Invoke(line, new object[] { index });

    // Shipping the line is enough: it is a pure function of its arguments, so a peer given the same line
    // hands out the same places without needing the roster that produced it.
    [Test]
    public void TheSameLineGivesTheSamePlaces()
    {
        var a = Line(40);
        var b = Line(40);

        for (int i = 0; i < 40; i++)
            Assert.AreEqual(PlaceAt(a, i), PlaceAt(b, i),
                            $"Place {i} moved between two identical layouts — the row is not reproducible, " +
                            "so sending it to the guest cannot make the two paddocks agree.");
    }

    // And why it has to be shipped rather than re-solved. The field size decides how long a line is before
    // it wraps, so a peer that counted one rig more than the host puts most of the lot somewhere else — and
    // with it the garages, the walkable pocket and every weekend venue laid out past the end of the block.
    [Test]
    public void OneRigMoreReshapesTheRow()
    {
        var host = Line(40);
        var guest = Line(41);

        float worst = 0f;
        for (int i = 0; i < 40; i++)
            worst = Mathf.Max(worst, Vector3.Distance(PlaceAt(host, i), PlaceAt(guest, i)));

        Assert.Greater(worst, 5f,
                       "A row built for 41 rigs parks them where a row built for 40 does. If that is now " +
                       "true the co-op mirror could send the count alone — but check it is not the test " +
                       "that has stopped exercising the wrap.");
    }

    // The far end of the block is what the weekend venues are measured from, so the two peers have to agree
    // about it to within nothing at all — not to within a rig.
    [Test]
    public void TheEndOfTheBlockMovesWithTheFieldSize()
    {
        Vector3 hostEnd = PlaceAt(Line(40), 39);
        Vector3 guestEnd = PlaceAt(Line(41), 39);

        Assert.AreNotEqual(hostEnd, guestEnd,
                           "The last place in the row is the same whether 40 or 41 rigs are parked. The " +
                           "venue cluster starts just past it, so if that ever became true the drivers' " +
                           "room would survive a miscounted field — it does not today.");
    }

    // The player's own motorhome is driven into the place the row gives it. Both peers therefore have to be
    // told which place that is, or one of them parks the shared RV somewhere the other does not.
    [Test]
    public void ThePlayersPlaceMovesTheWholeRow()
    {
        var atHead = Line(40, playerIndex: 0);
        var midRow = Line(40, playerIndex: 7);

        Assert.AreNotEqual(PlaceAt(atHead, 0), PlaceAt(midRow, 0),
                           "Which place the player's rig holds no longer slides the lot. It is sent with " +
                           "the layout precisely because it does.");
    }
}
