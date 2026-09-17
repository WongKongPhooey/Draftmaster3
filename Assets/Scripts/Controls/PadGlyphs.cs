using System.Collections.Generic;

namespace Draftmaster.Controls
{
    // A button on a pad, named by where it sits rather than by what is printed on it. The south face button is
    // "A" on an Xbox pad and "CROSS" on a PlayStation one; the code that reads it and the prompt that names it
    // both want the same answer whichever pad is plugged in, so they agree on the position and the label is
    // worked out at draw time.
    public enum PadButton
    {
        None,
        South, East, West, North,
        LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
        Start, Select,
        DpadUp, DpadDown, DpadLeft, DpadRight, Dpad,
    }

    // The two naming schemes a prompt can be drawn in. Anything that isn't recognisably a PlayStation pad gets
    // Xbox naming: that is what an unbranded PC pad almost always reports.
    public enum PadFamily { Xbox, PlayStation }

    // What a pad button is called, and which icon draws it. Pure, so the names and the parser are testable
    // without a device — the runtime half (which pad is in use, the sprites themselves) is InputGlyphs.
    public static class PadGlyphs
    {
        // Where the button icons live under Resources. One 16x16 Kenney tile per file, same as key_e.
        public const string IconFolder = "UI/Prompts/";

        public static string Name(PadButton b, PadFamily f)
        {
            bool ps = f == PadFamily.PlayStation;
            switch (b)
            {
                case PadButton.South:         return ps ? "CROSS" : "A";
                case PadButton.East:          return ps ? "CIRCLE" : "B";
                case PadButton.West:          return ps ? "SQUARE" : "X";
                case PadButton.North:         return ps ? "TRIANGLE" : "Y";
                case PadButton.LeftShoulder:  return ps ? "L1" : "LB";
                case PadButton.RightShoulder: return ps ? "R1" : "RB";
                case PadButton.LeftTrigger:   return ps ? "L2" : "LT";
                case PadButton.RightTrigger:  return ps ? "R2" : "RT";
                case PadButton.Start:         return ps ? "OPTIONS" : "MENU";
                case PadButton.Select:        return ps ? "CREATE" : "VIEW";
                case PadButton.DpadUp:        return "D-PAD UP";
                case PadButton.DpadDown:      return "D-PAD DOWN";
                case PadButton.DpadLeft:      return "D-PAD LEFT";
                case PadButton.DpadRight:     return "D-PAD RIGHT";
                case PadButton.Dpad:          return "D-PAD";
                default:                      return "";
            }
        }

        // Several buttons named as one label: "RT / LT".
        public static string Name(IList<PadButton> buttons, PadFamily f)
        {
            if (buttons == null || buttons.Count == 0) return "";
            if (buttons.Count == 1) return Name(buttons[0], f);
            var parts = new string[buttons.Count];
            for (int i = 0; i < buttons.Count; i++) parts[i] = Name(buttons[i], f);
            return string.Join(" / ", parts);
        }

        // Resources path of the icon for a button, without extension. Null for None.
        public static string IconResource(PadButton b, PadFamily f)
        {
            string token = Token(b);
            if (token == null) return null;
            return IconFolder + "pad_" + (f == PadFamily.PlayStation ? "ps" : "xbox") + "_" + token;
        }

        static string Token(PadButton b)
        {
            switch (b)
            {
                case PadButton.South:         return "south";
                case PadButton.East:          return "east";
                case PadButton.West:          return "west";
                case PadButton.North:         return "north";
                case PadButton.LeftShoulder:  return "lb";
                case PadButton.RightShoulder: return "rb";
                case PadButton.LeftTrigger:   return "lt";
                case PadButton.RightTrigger:  return "rt";
                case PadButton.Start:         return "start";
                case PadButton.Select:        return "select";
                case PadButton.DpadUp:        return "dpad_up";
                case PadButton.DpadDown:      return "dpad_down";
                case PadButton.DpadLeft:      return "dpad_left";
                case PadButton.DpadRight:     return "dpad_right";
                case PadButton.Dpad:          return "dpad";
                default:                      return null;
            }
        }

        // Every name a button goes by, either family's. Xbox's "X" is the WEST button — the PlayStation cross
        // is only ever spelled out — so a label written in Xbox naming parses the same whatever is plugged in.
        static readonly Dictionary<string, PadButton> Aliases = new Dictionary<string, PadButton>
        {
            { "A", PadButton.South },          { "CROSS", PadButton.South },
            { "B", PadButton.East },           { "CIRCLE", PadButton.East },
            { "X", PadButton.West },           { "SQUARE", PadButton.West },
            { "Y", PadButton.North },          { "TRIANGLE", PadButton.North },
            { "LB", PadButton.LeftShoulder },  { "L1", PadButton.LeftShoulder },
            { "RB", PadButton.RightShoulder }, { "R1", PadButton.RightShoulder },
            { "LT", PadButton.LeftTrigger },   { "L2", PadButton.LeftTrigger },
            { "RT", PadButton.RightTrigger },  { "R2", PadButton.RightTrigger },
            { "MENU", PadButton.Start },       { "START", PadButton.Start },   { "OPTIONS", PadButton.Start },
            { "VIEW", PadButton.Select },      { "SELECT", PadButton.Select },
            { "CREATE", PadButton.Select },    { "SHARE", PadButton.Select },
            { "D-PAD", PadButton.Dpad },       { "DPAD", PadButton.Dpad },
            { "D-PAD UP", PadButton.DpadUp },       { "DPAD UP", PadButton.DpadUp },
            { "D-PAD DOWN", PadButton.DpadDown },   { "DPAD DOWN", PadButton.DpadDown },
            { "D-PAD LEFT", PadButton.DpadLeft },   { "DPAD LEFT", PadButton.DpadLeft },
            { "D-PAD RIGHT", PadButton.DpadRight }, { "DPAD RIGHT", PadButton.DpadRight },
        };

        // Reads a pad label back into buttons: "LB" -> [LeftShoulder], "RT / LT" -> [RightTrigger, LeftTrigger].
        // Only call it on a label already known to be a pad's — "A" is also a key. False (and nothing added)
        // unless every part of the label is a button, so a free-text label falls back to being drawn as text.
        public static bool TryParse(string label, List<PadButton> into)
        {
            if (string.IsNullOrEmpty(label) || into == null) return false;
            var parts = label.Split('/', '+');
            int start = into.Count;
            for (int i = 0; i < parts.Length; i++)
            {
                string key = parts[i].Trim().ToUpperInvariant();
                if (key.Length == 0 || !Aliases.TryGetValue(key, out var b))
                {
                    into.RemoveRange(start, into.Count - start);
                    return false;
                }
                into.Add(b);
            }
            return into.Count > start;
        }
    }
}
