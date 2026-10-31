using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // Every action card, its rules text, and the numbers the race applies. The race effects read their
    // durations and magnitudes from here so tuning is one file.
    //
    // `Implemented` is the gate between "in the catalogue" and "in the game": an action whose effect the
    // race can't apply yet stays listed (so the collection screen can tease it) but never drops from a
    // pack and is refused by Play.
    public sealed class ActionSpec
    {
        public ActionEffect Effect;
        public string Name;
        public Rarity Rarity;
        public string Rules;
        public bool Implemented;
        public float Seconds;     // how long a timed effect lasts (0 = instant)
        public float Magnitude;   // effect strength; meaning per effect (see rules text)
        public int Overall;       // card OVR shown on the face — a rough power ranking, nothing more
    }

    public static class ActionCatalog
    {
        public static readonly IReadOnlyList<ActionSpec> All = new[]
        {
            new ActionSpec { Effect = ActionEffect.PushToPass, Name = "Push To Pass", Rarity = Rarity.Common,
                             Rules = "+6% engine power for 8 seconds.", Implemented = true, Seconds = 8f, Magnitude = 0.06f, Overall = 62 },
            new ActionSpec { Effect = ActionEffect.FuelSaver, Name = "Fuel Saver", Rarity = Rarity.Common,
                             Rules = "Burn 40% less fuel for 60 seconds.", Implemented = true, Seconds = 60f, Magnitude = 0.4f, Overall = 58 },
            new ActionSpec { Effect = ActionEffect.SpottersEye, Name = "Spotter's Eye", Rarity = Rarity.Common,
                             Rules = "Your spotter calls every car alongside for one lap.", Implemented = false, Seconds = 0f, Magnitude = 0f, Overall = 55 },
            new ActionSpec { Effect = ActionEffect.Slipstream, Name = "Slipstream", Rarity = Rarity.Rare,
                             Rules = "Double draft strength for 12 seconds.", Implemented = true, Seconds = 12f, Magnitude = 2f, Overall = 72 },
            new ActionSpec { Effect = ActionEffect.FreshRubber, Name = "Fresh Rubber", Rarity = Rarity.Rare,
                             Rules = "Restore half your tyre wear.", Implemented = true, Seconds = 0f, Magnitude = 0.5f, Overall = 74 },
            new ActionSpec { Effect = ActionEffect.QuickRepair, Name = "Quick Repair", Rarity = Rarity.Epic,
                             Rules = "Fix 40% of your bodywork damage.", Implemented = true, Seconds = 0f, Magnitude = 0.4f, Overall = 82 },
            new ActionSpec { Effect = ActionEffect.PerfectPitStop, Name = "Perfect Pit Stop", Rarity = Rarity.Epic,
                             Rules = "Your next pit stop is serviced in half the time.", Implemented = true, Seconds = 0f, Magnitude = 0.5f, Overall = 85 },
            new ActionSpec { Effect = ActionEffect.RocketRestart, Name = "Rocket Restart", Rarity = Rarity.Legendary,
                             Rules = "+15% power for 5 seconds on the next green flag.", Implemented = false, Seconds = 5f, Magnitude = 0.15f, Overall = 92 },
        };

        public static ActionSpec Get(ActionEffect effect)
        {
            foreach (var s in All) if (s.Effect == effect) return s;
            return null;
        }

        public static bool IsPlayable(ActionEffect effect) => Get(effect)?.Implemented ?? false;

        public static string DefinitionId(ActionEffect effect) => "ACT-" + effect;

        public static CardDefinition Definition(ActionSpec spec) => new CardDefinition
        {
            id = DefinitionId(spec.Effect),
            type = CardType.Action,
            rarity = spec.Rarity,
            name = spec.Name,
            overall = spec.Overall,
            effect = spec.Effect,
            rules = spec.Implemented ? spec.Rules : spec.Rules + " (Coming soon)",
            artKey = "action_" + spec.Effect,
        };
    }
}
