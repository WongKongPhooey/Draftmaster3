using UnityEngine;

namespace Draftmaster.Weekend
{
    // Whether the team liaison is waiting outside the motorhome this time, and what she says when she is.
    //
    // She used to be there every time the driver stepped out, reading the same four lines, which turned the
    // team's front door into a speed bump. Now she turns up now and then: always the first time (she is how a
    // new driver learns the weekend exists, and the opening's chief briefing is booked off her), never twice
    // running, and otherwise on a roll. When she stays away the weekend books its own next thing, exactly as
    // it does at a track whose cast has no liaison — see WeekendBriefing's `giverComing`.
    //
    // The rule and the line bank are pure; PlacedNPC asks ShouldVisit/Record when she would stand up and
    // PlacedNPCDefaults.LiaisonLines fills Lines in from the sheet.
    public static class LiaisonVisit
    {
        // Odds of her being there on any visit that isn't forced or ruled out.
        public const float Chance = 0.35f;

        const string MetKey = "liaison.met";            // she has had her say at least once this save
        const string LastKey = "liaison.last";          // 1 = she was there at the last chance, 0 = she wasn't
        const string VariantKey = "liaison.variant";    // the last line set she used, so she never repeats it

        // Should she be there?
        //
        //   metBefore      she has spoken to this driver before
        //   cameLastTime   she was there the previous time the driver could have met her
        //   roll           0..1, uniform
        public static bool ShouldVisit(bool metBefore, bool cameLastTime, float roll)
        {
            if (!metBefore) return true;
            if (cameLastTime) return false;
            return roll < Chance;
        }

        // The live decision, against the save. Call once per chance to meet her and act on the answer.
        //
        // A "yes" is not written down here — only Met() does that, when she actually speaks. Standing her up
        // is not meeting her: a scene that loads and reloads, or a load the driver never walks out of the
        // motorhome in, used to spend the certain first meeting and then rule her out the next time as
        // "twice running", so a fresh save could start without her.
        public static bool Roll()
        {
            bool visit = ShouldVisit(PlayerPrefs.GetInt(MetKey, 0) == 1, PlayerPrefs.GetInt(LastKey, 0) == 1,
                                     Random.value);
            if (!visit)
            {
                PlayerPrefs.SetInt(LastKey, 0);
                PlayerPrefs.Save();
            }
            return visit;
        }

        // She has walked up and had her say. This is what makes the next chance a "twice running" one.
        public static void Met()
        {
            PlayerPrefs.SetInt(MetKey, 1);
            PlayerPrefs.SetInt(LastKey, 1);
            PlayerPrefs.Save();
        }

        // Back to a driver who has never met her: the next chance is a certain one.
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(MetKey);
            PlayerPrefs.DeleteKey(LastKey);
            PlayerPrefs.DeleteKey(VariantKey);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ what she says

        // Something to be at. {0} = "<Day>, <time>" now, {1} = WeekendSchedulePlan.Describe of the booking.
        // The name tokens are filled when the line is spoken, which is why these are format strings with
        // positional holes and not interpolated ones.
        static readonly string[][] Booked =
        {
            new[]
            {
                "There you are, {playerfirst}. {0}, and you're already wanted.",
                "You're down for {1}.",
                "Do I have time for a coffee first? #player",
                "Only if you walk as fast as you drive. Follow the marker and don't be late, they don't like delays.",
            },
            new[]
            {
                "{playerfirst}! Caught you. {0} — have you looked at the sheet?",
                "Not yet. Should I be worried? #player",
                "Only if you miss {1}.",
                "The marker's up. Off you go.",
            },
            new[]
            {
                "Don't go wandering off, {playerfirst}. {0}, and the team has plans for you.",
                "Plans? #player",
                "You're down for {1}. Try to look like you wanted to be there.",
            },
            new[]
            {
                "Morning, afternoon, whatever it is — {0}, {playerfirst}, and you're on the clock.",
                "What have I got? #player",
                "It's {1}. I've put it on your marker so you can't say nobody told you.",
                "You're a lifesaver. #player",
                "I'm a clipboard with legs. Go.",
            },
            new[]
            {
                "Quick one, {playerfirst}, I've got three other people to chase. {0}.",
                "Go on. #player",
                "You're down for {1}. Be early, be polite, and smile if there's a camera.",
            },
            new[]
            {
                "Ah, good, you're up. {0}, {playerfirst} — {chieffirst} asked me to make sure you'd seen the sheet.",
                "Am I running errands again? #player",
                "Not errands. It's {1}, and it's on the marker.",
            },
        };

        // Nothing left to be at today.
        static readonly string[][] Clear =
        {
            new[]
            {
                "{playerfirst}. You're clear for now — nothing on the sheet until later.",
                "I'll come and find you when you're needed. #player",
                "That's the idea. Enjoy the quiet, it doesn't last.",
            },
            new[]
            {
                "Nothing for you right now, {playerfirst}. Honestly, I'm as surprised as you are.",
                "So I can just... wander? #player",
                "Wander. Sign things if anybody asks. I'll shout when the sheet changes.",
            },
            new[]
            {
                "You've got a gap, {playerfirst}. Don't get used to it.",
                "Any tips for spending it? #player",
                "Eat something. Drink some water. Stay where the phone works.",
            },
        };

        public static int BookedVariants => Booked.Length;
        public static int ClearVariants => Clear.Length;

        // One line set, filled in. `variant` wraps, so any int is safe.
        //
        //   clock     "Friday, 9:30 AM" — when she is saying it
        //   booking   WeekendSchedulePlan.Describe of what is next, or null for nothing left today
        public static string[] Lines(int variant, string clock, string booking)
        {
            var bank = booking == null ? Clear : Booked;
            var set = bank[((variant % bank.Length) + bank.Length) % bank.Length];
            var lines = new string[set.Length];
            for (int i = 0; i < set.Length; i++)
                lines[i] = set[i].Replace("{0}", clock ?? "").Replace("{1}", booking ?? "");
            return lines;
        }

        // A line set that isn't the one she used last time, against the save.
        public static string[] PickLines(string clock, string booking)
        {
            int count = booking == null ? Clear.Length : Booked.Length;
            int last = PlayerPrefs.GetInt(VariantKey, -1);
            int pick = NextVariant(count, last, Random.Range(0, count));
            PlayerPrefs.SetInt(VariantKey, pick);
            PlayerPrefs.Save();
            return Lines(pick, clock, booking);
        }

        // `roll` in [0, count). Steps off `last` so the same set never comes up twice running.
        public static int NextVariant(int count, int last, int roll)
        {
            if (count <= 1) return 0;
            int pick = ((roll % count) + count) % count;
            return pick == last ? (pick + 1) % count : pick;
        }
    }
}
