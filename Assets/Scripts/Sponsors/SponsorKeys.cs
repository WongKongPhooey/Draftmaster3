using System.Text;

namespace Draftmaster.Sponsors
{
    // Naming rules shared by the catalogue, the art loader and the editor tools, kept here (rather than in
    // SponsorCatalog, which needs the database) so both sides of the assembly split agree on them.
    public static class SponsorKeys
    {
        // Decal art lives at Resources/<CarArtFolder><key>.png, keyed off the brand name so adding a
        // sponsor needs no schema change: "Voltage Energy" -> "voltage-energy".
        public const string CarArtFolder = "Sponsors/Car/";

        public static string LogoKey(string sponsorName)
        {
            if (string.IsNullOrEmpty(sponsorName)) return "sponsor";
            var sb = new StringBuilder(sponsorName.Length);
            bool lastDash = false;
            foreach (char c in sponsorName)
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); lastDash = false; }
                else if (!lastDash && sb.Length > 0) { sb.Append('-'); lastDash = true; }
            }
            string key = sb.ToString().TrimEnd('-');
            return key.Length == 0 ? "sponsor" : key;
        }

        public static string CarArtPath(string sponsorName) => CarArtFolder + LogoKey(sponsorName);
    }
}
