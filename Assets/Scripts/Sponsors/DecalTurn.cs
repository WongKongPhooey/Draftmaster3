using UnityEngine;

namespace Draftmaster.Sponsors
{
    // How a decal is turned before it is painted onto a panel, as it LOOKS in an image viewer (clockwise is
    // clockwise on screen). One small.png serves the trunk and both rear quarters, so the trunk turns it a
    // quarter to read across the decklid and the far-side quarter turns it a half so its lettering faces
    // the other way to the near side.
    public enum DecalTurn
    {
        None = 0,
        Clockwise = 1,
        Half = 2,
        CounterClockwise = 3,
    }

    public static class DecalTurns
    {
        public static bool SwapsAxes(DecalTurn turn) => turn == DecalTurn.Clockwise || turn == DecalTurn.CounterClockwise;

        // Footprint of a (w,h) decal once turned.
        public static Vector2Int Size(DecalTurn turn, int w, int h) =>
            SwapsAxes(turn) ? new Vector2Int(h, w) : new Vector2Int(w, h);

        // Turned copy of a texture's pixels (Unity order: rows bottom-up). Returns the source array untouched
        // for DecalTurn.None.
        public static Color32[] Rotate(Color32[] src, int w, int h, DecalTurn turn, out int outW, out int outH)
        {
            Vector2Int size = Size(turn, w, h);
            outW = size.x;
            outH = size.y;
            if (turn == DecalTurn.None) return src;

            var dst = new Color32[src.Length];
            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    // Inverse map: which source pixel lands at (x, y). Rows run bottom-up, so a turn that is
                    // clockwise on screen is clockwise here too.
                    int sx, sy;
                    switch (turn)
                    {
                        case DecalTurn.Clockwise:        sx = w - 1 - y; sy = x;         break;
                        case DecalTurn.CounterClockwise: sx = y;         sy = h - 1 - x; break;
                        default:                         sx = w - 1 - x; sy = h - 1 - y; break;   // Half
                    }
                    dst[y * outW + x] = src[sy * w + sx];
                }
            }
            return dst;
        }
    }
}
