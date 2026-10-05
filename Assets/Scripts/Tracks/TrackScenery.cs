using UnityEngine;

// The root of a track package's scattered scenery (Environment/Scenery) and the settings it was scattered
// with. The scatter itself is editor-only — Draftmaster > Tracks > Scenery, or the buttons on this
// component's inspector — and writes SceneryPiece children under this object.
//
// Re-scattering replaces every piece under here except the ones ticked `locked`, and never touches anything
// outside it: the stands, the paddock and whatever was hand-placed in the package are measured and planted
// around, not moved.
public class TrackScenery : MonoBehaviour
{
    [Tooltip("What to scatter. Empty = the shared Resources/Tracks/SceneryPalette.")]
    public SceneryPalette palette;
    [Tooltip("Same seed, same layout. Change it for a different arrangement of the same amount of scenery.")]
    public int seed = 1;
    [Tooltip("Scale every entry's per-km count at this track. 0 = bare, 1 = as the palette says.")]
    [Min(0f)] public float density = 1f;

    [Header("Keep clear (metres)")]
    [Tooltip("Nothing nearer the racing surface's edge than this — the walls, the catch fence and the run-off.")]
    [Min(0f)] public float roadClearance = 12f;
    [Tooltip("Round the pit lane and its box lane.")]
    [Min(0f)] public float pitClearance = 30f;
    [Tooltip("Round the player's RV and spawn points. The drivers' motorhome lot and the team garages are laid " +
             "out from the RV at play time, so this has to cover where they will be, not where anything is now.")]
    [Min(0f)] public float paddockClearance = 220f;
    [Tooltip("Round each paddock boundary and lot area.")]
    [Min(0f)] public float boundaryClearance = 40f;
    [Tooltip("Round placed NPCs and weekend venue markers.")]
    [Min(0f)] public float markerClearance = 25f;
    [Tooltip("Round any other hand-placed object in the package, from its drawn bounds.")]
    [Min(0f)] public float propClearance = 4f;
    [Tooltip("In from the edge of the ground plane, so nothing hangs off the world.")]
    [Min(0f)] public float groundInset = 8f;

    [Header("Limits")]
    [Tooltip("Hard cap on pieces at this track, groups included.")]
    [Min(0)] public int maxPieces = 1200;

    public SceneryPalette Palette => palette != null ? palette : SceneryPalette.LoadDefault();
}
