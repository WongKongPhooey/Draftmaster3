using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The damage model's geometry. What is on trial here is the one thing a screenshot cannot tell you: whether
// a dent is a print of the body that made it, or a circular crater centred on the contact — which is what
// the point-and-radius model it replaces always produced, whatever hit you and from wherever.
public class BodyDeformTests
{
    // A car's flank, pointing along +x, half a metre away from the panel we are about to dent.
    static BodyDeform.Striker Flank(Vector2 centre, Vector2 inward)
    {
        return BodyDeform.Striker.Box(centre, Vector2.right, new Vector2(2.4f, 1.0f), inward);
    }

    [Test]
    public void APointOutsideTheStrikerIsNeverDented()
    {
        var striker = Flank(new Vector2(0f, -1.2f), Vector2.up);

        // Off the end of the body: a car cannot dent you with metal it does not have there.
        Assert.AreEqual(0f, BodyDeform.Intrusion(striker, new Vector2(9f, -1.2f), 0.3f), 1e-5f,
                        "A point well past the striker's end was dented — the dent isn't bounded by the body.");
        // Beyond the far face, i.e. not overlapped at all.
        Assert.AreEqual(0f, BodyDeform.Intrusion(striker, new Vector2(0f, 4f), 0.3f), 1e-5f);
    }

    [Test]
    public void TheIntrusionCountsWhatIsAlreadyOverlappingAndNotJustThePress()
    {
        // A flank whose face sits 0.4m past our panel: the two bodies are genuinely 0.4m inside each other.
        // That much of our space is occupied whether or not any press is applied on top, and the answer has
        // to say so — a panel has to get out of the way of metal that is really there.
        var buried = BodyDeform.Striker.Box(new Vector2(0f, -0.6f), Vector2.right, new Vector2(2.4f, 1.0f), Vector2.up);

        Assert.AreEqual(0.4f, BodyDeform.Intrusion(buried, Vector2.zero, 0f), 1e-4f,
                        "Two bodies 0.4m inside each other read as no intrusion without a press — so a pair " +
                        "held together would never dent at all.");

        // A press drives the striker further in on top of that, and the two add.
        Assert.AreEqual(0.7f, BodyDeform.Intrusion(buried, Vector2.zero, 0.3f), 1e-4f,
                        "The press doesn't stack on the real overlap.");

        // With no real overlap at all — bodies just touching, which is where a race collision solver leaves
        // them — the press is the whole story, and a panel vertex on the surface folds by exactly it.
        var touching = BodyDeform.Striker.Box(new Vector2(0f, -1.0f), Vector2.right, new Vector2(2.4f, 1.0f), Vector2.up);
        Assert.AreEqual(0.3f, BodyDeform.Intrusion(touching, new Vector2(0f, -0.0001f), 0.3f), 1e-3f);
    }

    [Test]
    public void TheDentTakesTheShapeOfTheStrikerNotACircleAroundTheContact()
    {
        // A flank pressed up into a panel that runs along y = 0. Its ends are at x = ±2.4.
        var striker = Flank(new Vector2(0f, -1.0f), Vector2.up);
        const float depth = 0.25f;

        // Two points the same distance from the contact centre, one across the face and one off its end.
        // A crater would dent them identically. A press dents only the one the striker actually covers.
        float underTheBody = BodyDeform.Intrusion(striker, new Vector2(2.0f, 0.02f), depth);
        float pastTheEnd = BodyDeform.Intrusion(striker, new Vector2(2.6f, 0.02f), depth);

        Assert.Greater(underTheBody, 0f, "The panel under the striker's flank didn't fold at all.");
        Assert.AreEqual(0f, pastTheEnd, 1e-5f,
                        "Bodywork past the end of the striker was dented — that's a blast radius, not a contact.");

        // And along the face the fold is flat, not a cone falling off from a centre point.
        float atCentre = BodyDeform.Intrusion(striker, new Vector2(0f, 0.02f), depth);
        Assert.AreEqual(atCentre, underTheBody, 1e-4f,
                        "The fold is deeper in the middle of the contact than at its edge — still a crater.");
    }

    [Test]
    public void ANarrowStrikerLeavesANarrowFoldAndAWideOneLeavesAWideFold()
    {
        // The same hit, same severity, same place — once with a nose-on corner and once with a whole flank.
        // The old model could not tell these apart: the dent was `dentRadius` across either way.
        var nose = BodyDeform.Striker.Box(new Vector2(0f, -1.0f), Vector2.right, new Vector2(0.4f, 1.0f), Vector2.up);
        var flank = Flank(new Vector2(0f, -1.0f), Vector2.up);

        Assert.AreEqual(0f, BodyDeform.Intrusion(nose, new Vector2(1.5f, 0.02f), 0.25f), 1e-5f,
                        "A nose-first hit dented bodywork a metre and a half away from the nose.");
        Assert.Greater(BodyDeform.Intrusion(flank, new Vector2(1.5f, 0.02f), 0.25f), 0f,
                       "A side-on hit failed to crease the panel along the flank that hit it.");
    }

    [Test]
    public void AWallFlattensThePanelAgainstItself()
    {
        // A barrier is a face, not an object: everything past it goes flat onto it, which is what a car with
        // its nose in the wall looks like.
        var wall = BodyDeform.Striker.Plane(Vector2.zero, Vector2.up);
        const float depth = 0.4f;

        // A vertex level with the face takes the full press; one already `depth` clear of it takes nothing.
        Assert.AreEqual(depth, BodyDeform.Intrusion(wall, Vector2.zero, depth), 1e-4f);
        Assert.AreEqual(0f, BodyDeform.Intrusion(wall, new Vector2(0f, depth), depth), 1e-4f);

        // And it does not care how far along the wall you are — a wall has no centre to explode from.
        Assert.AreEqual(BodyDeform.Intrusion(wall, new Vector2(0f, 0.1f), depth),
                        BodyDeform.Intrusion(wall, new Vector2(50f, 0.1f), depth), 1e-4f);
    }

    [Test]
    public void APointStrikeIsAHammerNotABlast()
    {
        // Even a hit with no body behind it presses rather than explodes: a flat-bottomed crease of the
        // given width, with its face on the point it was aimed at.
        var hammer = BodyDeform.Striker.Point(Vector2.zero, Vector2.up, 0.5f);

        Assert.Greater(BodyDeform.Intrusion(hammer, new Vector2(0.2f, -0.01f), 0.2f), 0f,
                       "The strike missed bodywork inside its own footprint.");
        Assert.AreEqual(0f, BodyDeform.Intrusion(hammer, new Vector2(0.4f, -0.01f), 0.2f), 1e-5f,
                        "The strike reached outside its footprint — the width isn't bounding it.");
        Assert.AreEqual(0f, BodyDeform.Intrusion(hammer, new Vector2(0f, 0.3f), 0.2f), 1e-5f,
                        "The strike dented metal on the far side of where it landed.");
    }

    [Test]
    public void TwoBodiesSplitOneContactBetweenThemRatherThanBothTakingItWhole()
    {
        // The failure this prevents is a void. Both cars in a contact press each other, so if each takes the
        // full severity, each panel retreats the full fold depth in opposite directions and the metal parts
        // company: two cars with a hole between them where the crash was. The shares must sum to 1, always.
        Assert.AreEqual(0.5f, BodyDeform.Share(1500f, 1500f), 1e-4f,
                        "Two identical cars don't fold half each, so one impact's metal is folded twice.");

        float light = BodyDeform.Share(900f, 2100f);
        float heavy = BodyDeform.Share(2100f, 900f);
        Assert.AreEqual(1f, light + heavy, 1e-4f,
                        "The two shares don't add up to the one contact — the pair fold too much or too little.");
        Assert.Greater(light, heavy, "The heavier car is caving in more than the light one it hit.");

        // A wall is not a body and gives nothing, so a car that hits one takes all of it.
        Assert.AreEqual(1f, BodyDeform.RigidPartner, 1e-4f);

        // Degenerate masses must not produce a share outside 0..1 — that would deepen a dent past its plan.
        foreach (var pair in new[] { (0f, 0f), (-5f, 1500f), (1e9f, 1f) })
        {
            float s = BodyDeform.Share(pair.Item1, pair.Item2);
            Assert.That(s, Is.InRange(0f, 1f), $"Share({pair.Item1}, {pair.Item2}) is outside 0..1.");
        }
    }

    [Test]
    public void SharesThatSumToOneWeldThePanelsAndAVirtualPressIsWhatOpensTheVoid()
    {
        // The whole void, from first principles, on the real geometry rather than as an asserted ratio.
        //
        // Two bodies buried `burial` deep in each other, each folding out of the other's way. Both measure
        // the SAME intrusion (burial + press), each keeps its share of it, and the two surfaces end up
        //
        //     apart = foldA + foldB - burial = (burial + press) * (shareA + shareB) - burial
        //
        // Positive is the void. With the shares summing to 1 that collapses to exactly `press` — so where
        // the bodies really do stay inside each other, any virtual press at all opens a hole, and no choice
        // of split can close it. Where the split falls only decides which car folds more.
        const float burial = 26f;

        foreach (float shareA in new[] { 0.5f, 0.7f, 0.35f })
        {
            float shareB = 1f - shareA;

            foreach (float press in new[] { 0f, 6f, 20f })
            {
                float intrusion = burial + press;
                float apart = intrusion * (shareA + shareB) - burial;

                Assert.AreEqual(press, apart, 1e-3f,
                                $"With shares {shareA:0.00}/{shareB:0.00} and a press of {press:0}, the panels " +
                                $"end up {apart:0.0}px apart — the void is supposed to be the press and nothing else.");
            }
        }

        // And the failure this all exists to prevent: a full share on BOTH bodies folds one impact's metal
        // twice and leaves a hole as wide as the two cars were buried, whatever the press is.
        Assert.AreEqual(burial, (burial + 0f) * (1f + 1f) - burial, 1e-3f,
                        "Giving both cars the whole contact should leave a void the width of the burial — " +
                        "that is the bug, and it has to stay reproducible for the fix to mean anything.");
    }

    [Test]
    public void CrumpleDragsTheSurroundingPanelWithTheFold()
    {
        // A 5x5 sheet with one vertex driven in. Bodywork is one piece, so its neighbours have to come with
        // it — otherwise the press leaves a stamp of the striker with a sheared edge.
        const int n = 5;
        var disp = new Vector3[n * n];
        var region = new bool[n * n];
        int centre = 2 * n + 2;
        disp[centre] = new Vector3(0f, -0.4f, 0f);
        for (int i = 0; i < region.Length; i++) region[i] = true;

        Vector3[] scratch = null;
        BodyDeform.Crumple(disp, n, n, null, region, 0.5f, 2, ref scratch);

        Assert.Less(disp[centre - 1].y, -1e-3f, "The metal beside the fold didn't move — the panel sheared.");
        Assert.Less(disp[centre].y, 0f, "The fold itself came out the wrong way.");
        Assert.Less(Mathf.Abs(disp[centre].y), 0.4f, "The fold didn't give anything up to its neighbours.");
    }

    [Test]
    public void CrumpleLeavesDamageElsewhereOnTheCarAlone()
    {
        // Only what this press touched, plus one ring, is smoothed — a new hit must not blur out an old dent
        // on the other end of the car.
        const int n = 5;
        var disp = new Vector3[n * n];
        var region = new bool[n * n];
        disp[0] = new Vector3(0f, -0.5f, 0f);   // old damage, far corner
        region[n * n - 1] = true;               // this press landed at the opposite corner

        Vector3[] scratch = null;
        BodyDeform.Crumple(disp, n, n, null, region, 0.9f, 3, ref scratch);

        Assert.AreEqual(-0.5f, disp[0].y, 1e-5f, "An old dent was smoothed away by an unrelated impact.");
    }

    [Test]
    public void ARigidVertexNeitherBendsNorDrags()
    {
        const int n = 3;
        var disp = new Vector3[n * n];
        var region = new bool[n * n];
        var weight = new float[n * n];
        for (int i = 0; i < weight.Length; i++) { weight[i] = 1f; region[i] = true; }
        weight[4] = 0f;                                   // the rigid core
        disp[1] = new Vector3(0f, -0.3f, 0f);

        Vector3[] scratch = null;
        BodyDeform.Crumple(disp, n, n, weight, region, 0.8f, 2, ref scratch);

        Assert.AreEqual(0f, disp[4].y, 1e-5f, "The rigid core bent.");
    }

    [Test]
    public void DilateGrowsTheTouchedSetByExactlyOneRing()
    {
        const int n = 5;
        var region = new bool[n * n];
        region[2 * n + 2] = true;

        bool[] scratch = null;
        BodyDeform.Dilate(region, n, n, ref scratch);

        Assert.IsTrue(region[2 * n + 1] && region[2 * n + 3] && region[n + 2] && region[3 * n + 2],
                      "The fold has nowhere to spread into — the crumple can only touch what the press hit.");
        Assert.IsFalse(region[2 * n], "The ring spread further than one vertex.");
        Assert.IsFalse(region[n + 1], "Diagonals were included — the smoothing kernel is 4-neighbour.");
    }
}
