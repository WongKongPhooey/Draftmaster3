using UnityEngine;

namespace Draftmaster.Tracks
{
    /// <summary>Where a reference aerial sits in a track package's own metres.</summary>
    public struct ReferencePlacement
    {
        public Vector2 centre;          // the image centre, package-local metres
        public float rotationDeg;       // anticlockwise, image x axis against package x
        public float metresPerPixel;
        public bool mirrored;           // corners describe a flipped image: the alignment is broken, do not use it
    }

    /// <summary>
    /// Reads the alignment Tools/track_reference.py writes (cache/alignment.json): the four corners of the
    /// aerial, in package-local metres, after fitting the game's centreline onto the real one. The corners
    /// carry everything - position, rotation and scale - so the Python side's maths never has to be
    /// repeated here, only read back as a sprite placement.
    /// </summary>
    public static class TrackReferenceAlignment
    {
        public static ReferencePlacement FromCorners(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight,
                                                     Vector2 bottomLeft, int width, int height)
        {
            Vector2 across = ((topRight - topLeft) + (bottomRight - bottomLeft)) * 0.5f;
            Vector2 down = ((bottomLeft - topLeft) + (bottomRight - topRight)) * 0.5f;
            float mx = width > 0 ? across.magnitude / width : 0f;
            float my = height > 0 ? down.magnitude / height : 0f;
            // Image x runs right and image rows run down; in an unflipped placement "down" is x turned
            // clockwise, so the cross product of across and down is negative.
            float cross = across.x * down.y - across.y * down.x;
            return new ReferencePlacement
            {
                centre = (topLeft + topRight + bottomRight + bottomLeft) * 0.25f,
                rotationDeg = Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg,
                metresPerPixel = (mx + my) * 0.5f,
                mirrored = cross > 0f,
            };
        }

        /// <summary>Package-local metres of image pixel (u, v), v counted down from the top row.</summary>
        public static Vector2 PixelToLocal(ReferencePlacement p, float u, float v, int width, int height)
        {
            float r = p.rotationDeg * Mathf.Deg2Rad;
            float x = (u - width * 0.5f) * p.metresPerPixel;
            float y = (height * 0.5f - v) * p.metresPerPixel;
            return p.centre + new Vector2(x * Mathf.Cos(r) - y * Mathf.Sin(r), x * Mathf.Sin(r) + y * Mathf.Cos(r));
        }
    }
}
