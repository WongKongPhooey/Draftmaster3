using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The car-vs-car impulse: a hit through both centres is the plain two-body answer, and a hit off to one side
// spends part of itself turning the cars, so it shoves them apart less.
public class CarContactTests
{
    const float Mass = 1500f;
    const float Inertia = 2940f;   // a 2.8 m wheelbase, 50/50 car: m·a·b

    [Test]
    public void AHitThroughBothCentresIsThePlainTwoBodyImpulse()
    {
        Vector2 n = Vector2.right;
        float j = CarContact.NormalImpulse(5f, 0.1f, n, Mass, Inertia, new Vector2(2.4f, 0f),
                                           Mass, Inertia, new Vector2(-2.4f, 0f));
        float plain = 1.1f * 5f / (2f / Mass);
        Assert.AreEqual(plain, j, plain * 1e-4f);
    }

    [Test]
    public void ARearCornerHitShovesLessThanItWould()
    {
        // B's rear corner struck sideways: 2.2 m behind its centre, the normal across the car.
        Vector2 n = Vector2.up;
        float centred = CarContact.NormalImpulse(5f, 0.1f, n, Mass, Inertia, Vector2.zero,
                                                 Mass, Inertia, Vector2.zero);
        float corner = CarContact.NormalImpulse(5f, 0.1f, n, Mass, Inertia, Vector2.zero,
                                                Mass, Inertia, new Vector2(-2.2f, -0.95f));
        Assert.Less(corner, centred * 0.6f, "a corner hit should spend much of itself turning the car");
        Assert.Greater(corner, 0f);
    }

    [Test]
    public void ACarThatCantBeSpunTakesTheMassOnlyImpulse()
    {
        Vector2 n = Vector2.up;
        float j = CarContact.NormalImpulse(5f, 0f, n, Mass, float.PositiveInfinity, new Vector2(-2.2f, 0f),
                                           Mass, float.PositiveInfinity, new Vector2(2.2f, 0f));
        Assert.AreEqual(5f / (2f / Mass), j, 0.01f);
    }

    [Test]
    public void SeparatingCarsExchangeNothing()
    {
        Assert.AreEqual(0f, CarContact.NormalImpulse(-1f, 0.1f, Vector2.right, Mass, Inertia, Vector2.zero,
                                                     Mass, Inertia, Vector2.zero));
    }
}
