using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The wheel hubs: four fixed points on the chassis where the wheels join their axles. While the folded bodywork
// outline still wraps a hub the wheel is on; once a hit has folded the outline in past it, the wheel is off.
// Engine-free (runs headless too) — the VehicleDamage end of it is VehicleDamageWheelTests.
public class WheelHubsTests
{
    // The grid VehicleDamage builds for a 64x32 livery at 12.8 px/m: 5 m x 2.5 m, 11 x 17 vertices.
    const int VX = 11, VY = 17;
    static readonly Vector2 Size = new Vector2(5f, 2.5f);

    static Vector3[] Body()
    {
        var grid = new Vector3[VX * VY];
        for (int y = 0; y < VY; y++)
        for (int x = 0; x < VX; x++)
            grid[y * VX + x] = new Vector3(-Size.x * 0.5f + Size.x * x / (VX - 1f),
                                           -Size.y * 0.5f + Size.y * y / (VY - 1f), 0f);
        return grid;
    }

    // Press the whole y=0 side in by `depth`, flat, as a wall does: every vertex nearer that side than the new face
    // ends up on it.
    static void FlattenSide(Vector3[] grid, float depth)
    {
        float face = -Size.y * 0.5f + depth;
        for (int i = 0; i < grid.Length; i++)
            if (grid[i].y < face) grid[i].y = face;
    }

    // Push the nose (sprite x = 0, local -x) back by `depth`, flat.
    static void FlattenNose(Vector3[] grid, float depth)
    {
        float face = -Size.x * 0.5f + depth;
        for (int i = 0; i < grid.Length; i++)
            if (grid[i].x < face) grid[i].x = face;
    }

    static Vector2 Hub(int i) => WheelHubs.LocalPoint(WheelHubs.Default[i], Size);

    [Test]
    public void AnUndamagedBodyCoversEveryHub()
    {
        var grid = Body();
        for (int i = 0; i < WheelHubs.Default.Length; i++)
            Assert.IsTrue(WheelHubs.Covered(Hub(i), grid, VX, VY), $"{WheelHubs.Name(i)} hub shows on a car as built.");
    }

    [Test]
    public void HubsSitInsideTheCarNearEachCorner()
    {
        Assert.AreEqual(4, WheelHubs.Default.Length);
        foreach (var h in WheelHubs.Default)
        {
            Assert.That(h.x, Is.InRange(0.1f, 0.9f));
            // Under the tyre, a little inboard of its middle: hidden by the body, well short of the tub.
            float fromSide = Mathf.Min(h.y, 1f - h.y) * Size.y;
            Assert.That(fromSide, Is.InRange(0.1f, 0.4f), "hub should sit under the tyre, near the side of the car");
        }
        Assert.Less(WheelHubs.Default[0].x, 0.5f, "front hubs are toward the nose (sprite x 0)");
        Assert.Greater(WheelHubs.Default[2].x, 0.5f, "rear hubs are toward the tail");
    }

    [Test]
    public void ASideFoldedShortOfTheHubLeavesTheWheelsOn()
    {
        var grid = Body();
        float hubDepth = WheelHubs.Default[0].y * Size.y;
        FlattenSide(grid, hubDepth * 0.8f);
        for (int i = 0; i < 4; i++)
            Assert.IsTrue(WheelHubs.Covered(Hub(i), grid, VX, VY), $"{WheelHubs.Name(i)} came off a dented door.");
    }

    [Test]
    public void ASideFoldedPastTheHubsTakesThatSidesWheelsOnly()
    {
        var grid = Body();
        float hubDepth = WheelHubs.Default[0].y * Size.y;
        FlattenSide(grid, hubDepth + 0.05f);
        Assert.IsTrue(WheelHubs.Exposed(Hub(0), grid, VX, VY), "front hub on the crushed side still covered");
        Assert.IsTrue(WheelHubs.Exposed(Hub(2), grid, VX, VY), "rear hub on the crushed side still covered");
        Assert.IsTrue(WheelHubs.Covered(Hub(1), grid, VX, VY), "the far side's front wheel came off too");
        Assert.IsTrue(WheelHubs.Covered(Hub(3), grid, VX, VY), "the far side's rear wheel came off too");
    }

    [Test]
    public void ANoseCrushedBackPastTheFrontAxleTakesTheFrontWheels()
    {
        var grid = Body();
        float axle = WheelHubs.Default[0].x * Size.x;
        FlattenNose(grid, axle * 0.7f);
        Assert.IsTrue(WheelHubs.Covered(Hub(0), grid, VX, VY), "a folded bonnet short of the axle took a wheel");

        FlattenNose(grid, axle + 0.05f);
        Assert.IsTrue(WheelHubs.Exposed(Hub(0), grid, VX, VY));
        Assert.IsTrue(WheelHubs.Exposed(Hub(1), grid, VX, VY));
        Assert.IsTrue(WheelHubs.Covered(Hub(2), grid, VX, VY), "a nose hit took a rear wheel");
        Assert.IsTrue(WheelHubs.Covered(Hub(3), grid, VX, VY), "a nose hit took a rear wheel");
    }

    [Test]
    public void ALocalDentOverTheWheelArchExposesOnlyThatHub()
    {
        // A narrow press over the front-left arch only, the rest of the side untouched.
        var grid = Body();
        Vector2 hub = Hub(0);
        float face = hub.y + 0.05f;
        for (int i = 0; i < grid.Length; i++)
            if (Mathf.Abs(grid[i].x - hub.x) < 0.6f && grid[i].y < face) grid[i].y = face;
        Assert.IsTrue(WheelHubs.Exposed(hub, grid, VX, VY));
        Assert.IsTrue(WheelHubs.Covered(Hub(2), grid, VX, VY));
    }

    [Test]
    public void AMissingOrTooSmallGridNeverReportsAWheelOff()
    {
        Assert.IsTrue(WheelHubs.Covered(Vector2.zero, null, VX, VY));
        Assert.IsTrue(WheelHubs.Covered(Vector2.zero, new Vector3[3], VX, VY));
    }
}
