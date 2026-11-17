using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// The aerial placement read back from Tools/track_reference.py's alignment.json corners.
public class TrackReferenceAlignmentTests
{
    static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    // Corners the way the Python side writes them: pixel (0,0) top-left, rows running down.
    static Vector2[] Corners(Vector2 centre, float deg, float mpp, int w, int h)
    {
        float hw = w * mpp * 0.5f, hh = h * mpp * 0.5f;
        return new[]
        {
            centre + Rot(new Vector2(-hw, hh), deg), centre + Rot(new Vector2(hw, hh), deg),
            centre + Rot(new Vector2(hw, -hh), deg), centre + Rot(new Vector2(-hw, -hh), deg),
        };
    }

    [Test]
    public void CornersComeBackAsTheirPlacement()
    {
        var c = Corners(new Vector2(-143.8f, -265.9f), 37.5f, 0.6f, 3600, 2950);
        var p = TrackReferenceAlignment.FromCorners(c[0], c[1], c[2], c[3], 3600, 2950);
        Assert.That(p.centre.x, Is.EqualTo(-143.8f).Within(0.01f));
        Assert.That(p.centre.y, Is.EqualTo(-265.9f).Within(0.01f));
        Assert.That(p.rotationDeg, Is.EqualTo(37.5f).Within(0.01f));
        Assert.That(p.metresPerPixel, Is.EqualTo(0.6f).Within(1e-4f));
        Assert.IsFalse(p.mirrored);
    }

    [Test]
    public void PixelCornersLandOnTheCorners()
    {
        var c = Corners(new Vector2(10f, 20f), -120f, 1.25f, 800, 600);
        var p = TrackReferenceAlignment.FromCorners(c[0], c[1], c[2], c[3], 800, 600);
        var tl = TrackReferenceAlignment.PixelToLocal(p, 0, 0, 800, 600);
        var br = TrackReferenceAlignment.PixelToLocal(p, 800, 600, 800, 600);
        Assert.That(Vector2.Distance(tl, c[0]), Is.LessThan(0.01f));
        Assert.That(Vector2.Distance(br, c[2]), Is.LessThan(0.01f));
    }

    [Test]
    public void AFlippedImageIsCaught()
    {
        var c = Corners(Vector2.zero, 0f, 1f, 100, 100);
        // Top and bottom swapped: a mirror image, which no rotation of the real aerial can produce.
        var p = TrackReferenceAlignment.FromCorners(c[3], c[2], c[1], c[0], 100, 100);
        Assert.IsTrue(p.mirrored);
    }
}
