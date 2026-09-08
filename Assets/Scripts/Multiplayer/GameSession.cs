// Tracks how the current track scene was entered, so shared scene code (player car, GridSpawner)
// can branch between the local single-player path and the networked paths.
// Set by the menu before the race scene loads; defaults to SinglePlayer so launching a track
// scene directly (in editor or via the old flow) behaves exactly as before.
//
// Three modes, because "is there a network session" and "is the career layer running" are different
// questions and the codebase asks both:
//
//   SinglePlayer — solo career. No network.
//   Multiplayer  — the competitive lobby race. A network session INSTEAD of the career: the paddock,
//                  the weekend clock, the race director and the session flow are all switched off.
//   CoopCareer   — a second player dropped into the host's career. A network session INSIDE the career:
//                  everything single player runs stays running, and the guest rides the host's weekend.
//
// The old two-value predicates keep their exact meaning so the competitive mode is untouched; career
// code that used to ask IsSinglePlayer asks CareerActive instead.
public static class GameSession
{
    public enum Mode { SinglePlayer, Multiplayer, CoopCareer }

    public static Mode CurrentMode = Mode.SinglePlayer;

    // Literally solo — no network session of any kind.
    public static bool IsSinglePlayer => CurrentMode == Mode.SinglePlayer;

    // The competitive lobby race. Deliberately NOT true in co-op: every gate reading this switches the
    // career layer off, which is the opposite of what co-op wants.
    public static bool IsMultiplayer => CurrentMode == Mode.Multiplayer;

    public static bool IsCoop => CurrentMode == Mode.CoopCareer;

    // Some network session is up (either flavour) — for transport/NetworkManager-level questions.
    public static bool IsNetworked => CurrentMode != Mode.SinglePlayer;

    // The weekend, the paddock, the ledger and the session flow are live. This is what career code
    // should gate on: true solo, true in co-op, false in the competitive race.
    public static bool CareerActive => CurrentMode != Mode.Multiplayer;
}
