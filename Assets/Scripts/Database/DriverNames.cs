using System;

namespace Draftmaster.Data
{
    // Name banks for procedurally generated new-gen drivers. Kept separate from the tiny DummyStaff pools so the
    // driver world has enough variety to run many seasons without obvious repeats.
    public static class DriverNames
    {
        static readonly string[] First =
        {
            "Cody","Tyler","Mason","Ben","Nico","Drew","Sam","Lance","Cooper","Hector",
            "Reggie","Will","Owen","Joaquin","Jesse","Rusty","Hank","Dale","Buck","Earl",
            "Chase","Brody","Kaden","Levi","Ryder","Cole","Wyatt","Trey","Blake","Gage",
            "Dominic","Xavier","Elias","Silas","Jaxon","Colton","Hudson","Bryce","Tanner","Landon",
            "Diego","Mateo","Rafa","Enzo","Luca","Andre","Kai","Zane","Rhett","Cash",
            "Jax","Beau","Knox","Ford","Ace","Dash","Sonny","Vince","Marco","Devon"
        };

        static readonly string[] Last =
        {
            "Roberts","Jensen","Whitlow","Marsh","Briggs","Calloway","Pike","Holloway","Reyes","McGraw",
            "Vance","Atherton","Schaeffer","Castillo","Carlisle","Ortega","Lange","Aldana","Boone","Petrov",
            "Sullivan","Barrett","Hayes","Foster","Riggs","Doyle","Mercer","Sutton","Prescott","Kessler",
            "Navarro","Delgado","Salazar","Cabrera","Quintero","Vega","Moreno","Solano","Ibarra","Fuentes",
            "Larsson","Novak","Volkov","Weiss","Brandt","Kohler","Renner","Falk","Sorensen","Halloran",
            "Ashby","Cromwell","Fairbanks","Winslow","Thatcher","Beckett","Ellsworth","Radford","Sterling","Whitaker"
        };

        // Optional flavour handles; most new-gens debut without one and earn it later.
        static readonly string[] Nicknames =
        {
            "Rook","Flash","The Kid","Ace","Ghost","Turbo","Rocket","Hotshoe","The Blade","Slingshot",
            "Maverick","Cannon","Comet","Bullet","Nitro","The Prospect","Freewheel","Sidewinder","Torque","Redline"
        };

        // Pull a first/last pair. rng lets a caller reproduce a season's intake deterministically.
        public static (string first, string last) Pick(Random rng)
        {
            return (First[rng.Next(First.Length)], Last[rng.Next(Last.Length)]);
        }

        // ~1-in-4 rookies debut with a nickname; the rest get "" (empty).
        public static string PickNickname(Random rng)
        {
            return rng.Next(4) == 0 ? Nicknames[rng.Next(Nicknames.Length)] : "";
        }
    }
}
