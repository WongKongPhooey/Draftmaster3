using System.Text;

namespace Draftmaster.Sponsors
{
    // Naming rules shared by the catalogue, the art loader and the editor tools, kept here (rather than in
    // SponsorCatalog, which needs the database) so both sides of the assembly split agree on them.
    public static class SponsorKeys
    {
        // Each brand ships three images, keyed off the brand name so adding a sponsor needs no schema change
        // ("Voltage Energy" -> "voltage-energy"):
        //   Resources/Sponsors/Full/<key>.png   boards, billboards, sponsor + team UI — any size
        //   Resources/Sponsors/Hood/<key>.png   the hood decal, drawn at the hood panel's size
        //   Resources/Sponsors/Small/<key>.png  the trunk AND both rear quarters (turned per slot by the layout)
        // Sponsors/Car/ is the original one-decal-per-brand folder, still read as a fallback.
        public const string FullArtFolder = "Sponsors/Full/";
        public const string HoodArtFolder = "Sponsors/Hood/";
        public const string SmallArtFolder = "Sponsors/Small/";
        public const string CarArtFolder = "Sponsors/Car/";

        // Resources paths to try, best first, for the decal a slot wears.
        public static string[] DecalArtPaths(SponsorSlot slot, string logoKey) => new[]
        {
            (slot == SponsorSlot.Hood ? HoodArtFolder : SmallArtFolder) + logoKey,
            CarArtFolder + logoKey,
        };

        // Resources paths to try, best first, for the full-size brand image.
        public static string[] FullArtPaths(string logoKey) => new[]
        {
            FullArtFolder + logoKey,
            HoodArtFolder + logoKey,
            CarArtFolder + logoKey,
        };

        public static string LogoKey(string sponsorName)
        {
            if (string.IsNullOrEmpty(sponsorName)) return "sponsor";
            var sb = new StringBuilder(sponsorName.Length);
            bool lastDash = false;
            foreach (char c in sponsorName)
            {
                if (c == '&')
                {
                    // "Aces & Spades" -> "aces-and-spades", the way the art file is named.
                    if (!lastDash && sb.Length > 0) sb.Append('-');
                    sb.Append("and");
                    lastDash = false;
                }
                else if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); lastDash = false; }
                else if (!lastDash && sb.Length > 0) { sb.Append('-'); lastDash = true; }
            }
            string key = sb.ToString().TrimEnd('-');
            return key.Length == 0 ? "sponsor" : key;
        }

        public static string CarArtPath(string sponsorName) => CarArtFolder + LogoKey(sponsorName);
    }
}
