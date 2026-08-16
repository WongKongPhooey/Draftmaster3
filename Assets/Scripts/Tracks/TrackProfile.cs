using Draftmaster.Data;
using Draftmaster.Tracks;

// Game-side view of the per-track-type tuning table.
//
// The numbers themselves live in Draftmaster.Tracks.TrackTuning, which is its own assembly so they can be
// unit tested; this is the seam that speaks the game's own types (Draftmaster.Data.TrackType, the track
// catalogue) so no caller has to know about that split.
//
//     var profile = TrackProfile.Current;        // whatever we're racing at
//     draftForce *= profile.draftScale;
public static class TrackProfile
{
    // Draftmaster.Data.TrackType and Draftmaster.Tracks.TrackKind are declared with identical values, so
    // this is a straight cast — kept in one place in case either list ever grows.
    public static TrackKind KindOf(TrackType type) => (TrackKind)(int)type;

    public static TrackTuningData For(TrackType type) => TrackTuning.For(KindOf(type));

    public static TrackTuningData ForTrack(string trackId)
        => TrackTuning.ForTrack(trackId, KindOf(TrackCatalog.TypeOf(trackId)));

    // The profile for the track currently selected for racing.
    public static TrackTuningData Current => ForTrack(TrackSelection.CurrentId);
}
