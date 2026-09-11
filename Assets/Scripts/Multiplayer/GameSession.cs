// Tracks how the current track scene was entered, so shared scene code (player car, GridSpawner)
// can branch between the local single-player path and the networked paths.
// Set by the menu before the race scene loads; defaults to SinglePlayer so launching a track
// scene directly (in editor or via the old flow) behaves exactly as before.
//
// Four modes, because "is there a network session", "is the career layer running" and "can the player
// get out of the car" are different questions and the codebase asks all three:
//
//   SinglePlayer — solo career. No network.
//   SingleRace   — one race, picked off SINGLE RACE or EXHIBITION. No network and no career either: no
//                  paddock, no weekend, no money, no on-foot. The player starts in the car and that is
//                  the whole event.
//   Multiplayer  — the competitive lobby race. A network session INSTEAD of the career: the paddock,
//                  the weekend clock, the race director and the session flow are all switched off.
//   CoopCareer   — a second player dropped into the host's career. A network session INSIDE the career:
//                  everything single player runs stays running, and the guest rides the host's weekend.
//
// The old two-value predicates keep their exact meaning so the competitive mode is untouched; career
// code that used to ask IsSinglePlayer asks CareerActive instead.
public static class GameSession
{
    public enum Mode { SinglePlayer, Multiplayer, CoopCareer, SingleRace }

    public static Mode CurrentMode = Mode.SinglePlayer;

    // Literally solo — no network session of any kind. True of a single race as well as a solo career.
    public static bool IsSinglePlayer => !IsNetworked;

    // The competitive lobby race. Deliberately NOT true in co-op: every gate reading this switches the
    // career layer off, which is the opposite of what co-op wants.
    public static bool IsMultiplayer => CurrentMode == Mode.Multiplayer;

    public static bool IsCoop => CurrentMode == Mode.CoopCareer;

    // One race on its own, with nothing of the career around it.
    public static bool IsSingleRace => CurrentMode == Mode.SingleRace;

    // Some network session is up (either flavour) — for transport/NetworkManager-level questions.
    public static bool IsNetworked => CurrentMode == Mode.Multiplayer || CurrentMode == Mode.CoopCareer;

    // The weekend, the paddock, the ledger and the session flow are live. This is what career code
    // should gate on: true solo, true in co-op, false in the competitive race and in a single race.
    public static bool CareerActive => CurrentMode == Mode.SinglePlayer || CurrentMode == Mode.CoopCareer;

    // Whether the player ever leaves the car in this mode. The walk to the car, the paddock cast, the
    // crowd, the motorhome lot and the pit-wall avatar all hang off this. A single race is driven from
    // the green flag to the chequer and never puts a body on the ground; the career and the lobby race
    // both do (the lobby race spawns straight onto the grid, but its own code decides that).
    public static bool OnFootAllowed => CurrentMode != Mode.SingleRace;
}
