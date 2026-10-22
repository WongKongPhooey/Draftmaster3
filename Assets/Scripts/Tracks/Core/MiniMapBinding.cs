using UnityEngine;

namespace Draftmaster.Tracks
{
    // When the in-car track map has to redraw. TrackMiniMap polls this once a second with the road it baked
    // and the road the race is on now (TrackBuilder and its TrackInfoV2, passed as plain Objects so the rule
    // can be tested from the EditMode suite, which cannot see the game assembly).
    //
    // The map used to bake the first TrackBuilder a scene search found and only look again once that builder
    // had been destroyed, which is how every track came to show Watkins Glen.
    public static class MiniMapBinding
    {
        // Whether the baked map no longer shows `current`: a different road, the same road on different
        // geometry, or nothing baked yet for a road that has geometry to draw. A road that has gone (Unity
        // compares a destroyed object equal to null) with nothing in its place is not a rebake — there is
        // nothing to draw.
        public static bool NeedsRebake(Object baked, Object bakedTrack, Object current, Object currentTrack,
                                       bool haveMap)
        {
            if (current != baked) return true;
            if (current == null) return false;
            return currentTrack != bakedTrack || (!haveMap && currentTrack != null);
        }
    }
}
