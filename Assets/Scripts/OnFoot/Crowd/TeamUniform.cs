using UnityEngine;

namespace Draftmaster.Crowd
{
    // A team's two colours, mapped onto the paper-doll layers somebody actually wears them on.
    //
    // A pit crew is not dressed at random: everyone over the wall is in the same firesuit, painted like the
    // car, so a glance down pit road says whose stop you are watching. That single rule — which layer takes
    // the car's primary and which takes its secondary — lives here rather than in the people wearing it, so
    // the crew (and anything else put in team kit later) all read the same.
    //
    // Layers not named here — the body, the hair, the boots — keep whatever the outfit rolled: a uniform
    // changes what somebody is wearing, not who they are.
    public static class TeamUniform
    {
        public const string Top = "Top";
        public const string Bottoms = "Bottoms";
        public const string Hat = "Hat";

        // The colour a layer takes in team kit, or false when that layer is not part of the uniform.
        public static bool TryColour(string category, Color primary, Color secondary, out Color tint)
        {
            if (Is(category, Top) || Is(category, Hat)) { tint = primary; return true; }
            if (Is(category, Bottoms)) { tint = secondary; return true; }

            tint = Color.white;
            return false;
        }

        static bool Is(string category, string named) =>
            !string.IsNullOrEmpty(category) &&
            string.Equals(category, named, System.StringComparison.OrdinalIgnoreCase);
    }
}
