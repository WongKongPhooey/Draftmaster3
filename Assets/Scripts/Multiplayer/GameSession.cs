// Tracks how the current track scene was entered, so shared scene code (player car, GridSpawner)
// can branch between the local single-player path and the networked multiplayer path.
// Set by the menu before the race scene loads; defaults to SinglePlayer so launching a track
// scene directly (in editor or via the old flow) behaves exactly as before.
public static class GameSession
{
    public enum Mode { SinglePlayer, Multiplayer }

    public static Mode CurrentMode = Mode.SinglePlayer;

    public static bool IsMultiplayer => CurrentMode == Mode.Multiplayer;
    public static bool IsSinglePlayer => CurrentMode == Mode.SinglePlayer;
}
