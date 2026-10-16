using System;

namespace Draftmaster.Audio
{
    // The parts of the mix OPTIONS > SOUND sets a level for. Every voice in the game belongs to exactly one.
    public enum SoundChannel
    {
        Master,     // everything, on the listener
        Engines,    // your car, the field, the cars past the title
        Effects,    // the world close by: footsteps, the alarm clock, the title's crunches
        Crowd,      // the grandstands and the paddock murmur
        Interface,  // the phone in your pocket
    }

    // The rules behind the sound levels, with nothing in them that needs a running engine: what each level
    // is called, how it is stored, how a press moves it and how it is drawn. SoundVolumes (runtime) holds
    // the saved values and hands them to the voices.
    //
    // A level is a whole percent in steps of ten, stored as an int so CareerReset can keep it across a wipe
    // the same way it keeps every other setting.
    public static class SoundMix
    {
        public const int StepPercent = 10;
        public const int MaxPercent = 100;
        public const int DefaultPercent = 100;

        public static readonly SoundChannel[] All =
        {
            SoundChannel.Master, SoundChannel.Engines, SoundChannel.Effects, SoundChannel.Crowd, SoundChannel.Interface,
        };

        public static string PrefKey(SoundChannel channel) => "Sound." + channel;

        public static int Clamp(int percent) => Math.Max(0, Math.Min(MaxPercent, percent));

        // A left/right press: one step either way, held at the ends. Turning a level up past full should not
        // drop it to silence.
        public static int Step(int percent, int dir)
        {
            int snapped = Snap(percent);
            return Clamp(snapped + Math.Sign(dir) * StepPercent);
        }

        // A tap or ENTER, which only ever goes forward: one step up, and from full round to silence, so a
        // phone (which has no left and right) can still reach every level.
        public static int Cycle(int percent)
        {
            int snapped = Snap(percent);
            return snapped >= MaxPercent ? 0 : Clamp(snapped + StepPercent);
        }

        // The nearest step, so a hand-edited pref of 35 still moves in tens.
        public static int Snap(int percent)
        {
            int c = Clamp(percent);
            return (int)Math.Round(c / (double)StepPercent, MidpointRounding.AwayFromZero) * StepPercent;
        }

        public static float Scale(int percent) => Clamp(percent) / (float)MaxPercent;

        // The value column: a ten-cell bar and the number, OFF at silence.
        public static string Format(int percent)
        {
            int c = Clamp(percent);
            int cells = MaxPercent / StepPercent;
            int lit = (int)Math.Round(c / (double)StepPercent, MidpointRounding.AwayFromZero);
            string bar = "[" + new string('#', lit) + new string('-', cells - lit) + "]";
            return bar + "  " + (c == 0 ? "OFF" : c + "%");
        }

        public static string Label(SoundChannel channel) => channel switch
        {
            SoundChannel.Master => "MASTER",
            SoundChannel.Engines => "ENGINES",
            SoundChannel.Effects => "EFFECTS",
            SoundChannel.Crowd => "CROWD",
            SoundChannel.Interface => "PHONE",
            _ => channel.ToString().ToUpperInvariant(),
        };

        public static string Describe(SoundChannel channel) => channel switch
        {
            SoundChannel.Master => "EVERYTHING AT ONCE. THE OTHER LEVELS SIT UNDER THIS ONE.",
            SoundChannel.Engines => "YOUR CAR, THE REST OF THE FIELD, GEAR CHANGES.",
            SoundChannel.Effects => "THE WORLD CLOSE BY: FOOTSTEPS, THE ALARM CLOCK, THE TITLE CRUNCH.",
            SoundChannel.Crowd => "THE GRANDSTANDS AND THE PADDOCK MURMUR.",
            SoundChannel.Interface => "TEXTS AND ALERTS FROM THE PHONE IN YOUR POCKET.",
            _ => "",
        };
    }
}
