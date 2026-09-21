namespace Draftmaster.Controls
{
    // Every keyboard shortcut in the game that matters to a player, and the pad button that does the same job.
    //
    // A pad has a dozen buttons against a keyboard's hundred, so buttons are reused by context: in the car the
    // d-pad flips the race panels (F1/F2/F4/F6), on foot it is the weekend's (Q, F10). The constants are what
    // the reading code uses and the table is what the tests check — two shortcuts on one button in the same
    // context have to say why they never both answer, or the table fails.
    //
    // Dev panels (F5 dossier, F7 telemetry, F8 formation, F9 tuner, F12 co-op debug, the F6 sponsor board and
    // UI showcase) stay keyboard-only on purpose: they are for whoever is building the game, and a pad has no
    // buttons to spare for them.
    public static class PadBindings
    {
        public enum Context
        {
            Anywhere,   // the whole time the player is playing, in the car or out of it
            Driving,    // sat in the car (no walking body)
            OnFoot,     // walking the paddock
            Seated,     // in a grandstand seat — still on foot
            Fight,      // a paddock scrap — still on foot
            Menu,       // a modal screen is up and owns the pad
        }

        // ---- anywhere
        public const PadButton Pause = PadButton.Start;              // Esc
        public const PadButton CrewChief = PadButton.West;           // C
        public const PadButton LeaderboardExpand = PadButton.RightShoulder;   // hold Tab

        // ---- driving
        public const PadButton Throttle = PadButton.RightTrigger;    // W
        public const PadButton Brake = PadButton.LeftTrigger;        // S
        public const PadButton PitLimiter = PadButton.North;         // L
        public const PadButton Tow = PadButton.North;                // P, stranded on track
        public const PadButton DriveBroadcast = PadButton.East;      // V
        public const PadButton TeamBox = PadButton.LeftShoulder;     // F3
        public const PadButton Leaderboard = PadButton.DpadUp;       // F2
        public const PadButton LapTiming = PadButton.DpadDown;       // F1
        public const PadButton Tyres = PadButton.DpadLeft;           // F6
        public const PadButton Rivalries = PadButton.DpadRight;      // F4

        // ---- on foot
        public const PadButton Interact = PadButton.South;           // E
        public const PadButton Run = PadButton.LeftShoulder;         // Left Shift
        public const PadButton Phone = PadButton.Select;             // P
        public const PadButton TravelThere = PadButton.North;        // T
        public const PadButton RecallObjective = PadButton.DpadUp;   // Q
        public const PadButton WeekendSheet = PadButton.DpadDown;    // F10
        public const PadButton WatchPrevCar = PadButton.DpadLeft;    // , (crew chief only)
        public const PadButton WatchNextCar = PadButton.DpadRight;   // . (crew chief only)

        // ---- grandstand seat
        public const PadButton LeaveSeat = PadButton.North;          // E
        public const PadButton LiveTiming = PadButton.West;          // F11

        // ---- paddock fight
        public const PadButton Shove = PadButton.West;               // Space
        public const PadButton LeftHook = PadButton.LeftShoulder;    // J
        public const PadButton RightHook = PadButton.RightShoulder;  // K

        // ---- menus
        public const PadButton Confirm = PadButton.South;            // Enter / E
        public const PadButton Back = PadButton.East;                // Esc / Backspace

        public readonly struct Shortcut
        {
            public readonly string action;
            public readonly string keyboard;
            public readonly PadButton pad;
            public readonly Context context;
            // Other actions on the same button in an overlapping context, each of which has been checked to
            // never answer at the same moment as this one.
            public readonly string[] sharesWith;

            public Shortcut(string action, string keyboard, PadButton pad, Context context, params string[] sharesWith)
            {
                this.action = action;
                this.keyboard = keyboard;
                this.pad = pad;
                this.context = context;
                this.sharesWith = sharesWith ?? new string[0];
            }
        }

        public static readonly Shortcut[] All =
        {
            new Shortcut("Pause", "ESC", Pause, Context.Anywhere),
            // Only offered during the player's own session; the stand only ever shows somebody else's, and a
            // fight stands the crew chief toggle down while it lasts.
            new Shortcut("Crew chief", "C", CrewChief, Context.Anywhere, "Live timing", "Shove"),
            // Held rather than pressed, and ignored for the length of a fight.
            new Shortcut("Full field (hold)", "TAB", LeaderboardExpand, Context.Anywhere, "Right hook"),

            new Shortcut("Throttle", "W", Throttle, Context.Driving),
            new Shortcut("Brake", "S", Brake, Context.Driving),
            // The limiter only arms inside the pit lane, and a tow is never offered there.
            new Shortcut("Pit limiter", "L", PitLimiter, Context.Driving, "Tow"),
            new Shortcut("Tow", "P", Tow, Context.Driving, "Pit limiter"),
            new Shortcut("Drive / broadcast", "V", DriveBroadcast, Context.Driving),
            new Shortcut("Team box", "F3", TeamBox, Context.Driving),
            new Shortcut("Leaderboard", "F2", Leaderboard, Context.Driving),
            new Shortcut("Lap timing", "F1", LapTiming, Context.Driving),
            new Shortcut("Tyres", "F6", Tyres, Context.Driving),
            new Shortcut("Rivalries", "F4", Rivalries, Context.Driving),

            new Shortcut("Interact", "E", Interact, Context.OnFoot),
            // Fights hold the player still, so the hook never has a run to clash with.
            new Shortcut("Run", "LEFT SHIFT", Run, Context.OnFoot, "Left hook"),
            new Shortcut("Phone", "P", Phone, Context.OnFoot),
            // Travel stands down while the player is sat in a stand.
            new Shortcut("Travel there", "T", TravelThere, Context.OnFoot, "Leave seat"),
            new Shortcut("Show objective", "Q", RecallObjective, Context.OnFoot),
            new Shortcut("Weekend sheet", "F10", WeekendSheet, Context.OnFoot),
            // Only read while the player is the crew chief on the pit wall.
            new Shortcut("Watch previous car", ",", WatchPrevCar, Context.OnFoot),
            new Shortcut("Watch next car", ".", WatchNextCar, Context.OnFoot),

            new Shortcut("Leave seat", "E", LeaveSeat, Context.Seated, "Travel there"),
            new Shortcut("Live timing", "F11", LiveTiming, Context.Seated, "Crew chief", "Shove"),

            new Shortcut("Shove", "SPACE", Shove, Context.Fight, "Crew chief", "Live timing"),
            new Shortcut("Left hook", "J", LeftHook, Context.Fight, "Run"),
            new Shortcut("Right hook", "K", RightHook, Context.Fight, "Full field (hold)"),

            new Shortcut("Confirm", "ENTER", Confirm, Context.Menu),
            new Shortcut("Back", "ESC", Back, Context.Menu),
        };

        // Can the two contexts be live at the same moment? A seat and a fight are both on foot; a menu is modal
        // and has the pad to itself.
        public static bool Overlap(Context a, Context b)
        {
            if (a == Context.Menu || b == Context.Menu) return a == b;
            if (a == b || a == Context.Anywhere || b == Context.Anywhere) return true;
            return Walking(a) && Walking(b);
        }

        static bool Walking(Context c) => c == Context.OnFoot || c == Context.Seated || c == Context.Fight;
    }
}
