namespace Draftmaster.Weekend
{
    // The naming convention for objective markers: which object name means which venue.
    //
    // Pure string work, and in the core assembly on purpose. The runtime half (WeekendMarker, which owns the
    // perimeter and the teleport) is a MonoBehaviour in Assembly-CSharp and cannot be reached from a test —
    // but "does PitBox_Marker mean the pit box, and does the default name for every venue resolve back to
    // it" is exactly the sort of thing that should fail the build rather than be discovered in the paddock.
    //
    // The convention: an object whose name ends `_Marker` is a marker. What comes before the suffix is
    // matched against the venues, ignoring case, spaces and underscores — Pitbox_Marker, pit_box_Marker and
    // PitBox_Marker are the same request, because a convention that fails on capitalisation is one people
    // stop using. A name that matches no venue is still a marker, just one a plan file has to ask for by
    // name (`"markerLocation": "Podium_Marker"`).
    public static class WeekendMarkerNames
    {
        public const string Suffix = "_Marker";

        public static bool IsMarkerName(string objectName) =>
            !string.IsNullOrEmpty(objectName) &&
            objectName.EndsWith(Suffix, System.StringComparison.OrdinalIgnoreCase);

        // The name to give an object so it becomes a given venue's marker, and the name a plan file falls
        // back to when it does not override the marker location.
        public static string DefaultNameFor(WeekendVenue venue) => venue + Suffix;

        public static WeekendVenue VenueFromName(string objectName)
        {
            if (!IsMarkerName(objectName)) return WeekendVenue.None;

            string key = Simplify(objectName.Substring(0, objectName.Length - Suffix.Length));
            switch (key)
            {
                case "pitbox":
                case "box":              return WeekendVenue.PitBox;

                case "motorhome":
                case "rv":               return WeekendVenue.Motorhome;

                case "driversroom":
                case "meetingroom":
                case "meeting":          return WeekendVenue.MeetingRoom;

                case "signingfence":
                case "signing":
                case "fanfence":
                case "fence":
                case "autographs":       return WeekendVenue.SigningFence;

                case "sponsorsuite":
                case "winnerscircle":
                case "winnercircle":
                case "hospitality":
                case "sponsor":
                case "suite":
                case "photoshoot":
                case "photo":            return WeekendVenue.SponsorSuite;

                case "introstage":
                case "stage":
                case "intros":           return WeekendVenue.IntroStage;

                case "grandstand":
                case "stand":
                case "spectate":         return WeekendVenue.Grandstand;

                default:                 return WeekendVenue.None;
            }
        }

        // Names are compared on their letters and digits alone, which is what makes the matching forgiving.
        // Used for object names and for a plan file's markerLocation, so the two always agree.
        public static string Simplify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var chars = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                if (char.IsLetterOrDigit(c)) chars.Append(char.ToLowerInvariant(c));
            return chars.ToString();
        }

        public static bool SameName(string a, string b) => Simplify(a) == Simplify(b);
    }
}
