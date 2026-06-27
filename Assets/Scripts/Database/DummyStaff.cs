using System.Collections.Generic;

namespace Draftmaster.Data
{
    // Per-team staff seed. A roster is generated for each existing Team, with ratings anchored to the team's
    // CarRating so stronger teams field a stronger crew. Seeded after Teams exist (see DatabaseManager).
    public static class DummyStaff
    {
        static readonly string[] First =
        {
            "Mike","Dale","Ron","Greg","Tony","Pat","Sam","Bobby","Carl","Jeff",
            "Lonnie","Chad","Cole","Wes","Hank","Roy","Gil","Dean","Marty","Ray"
        };
        static readonly string[] Last =
        {
            "Hutchins","Barker","Doyle","Frost","Quinn","Vance","Mercer","Ables","Sutter","Pryor",
            "Knox","Reed","Calder","Mason","Webb","Dorn","Hale","Ramsey","Fox","Whitt"
        };

        // The roles every team carries. PitCrew appears multiple times (the over-the-wall crew).
        static readonly StaffRole[] Roster =
        {
            StaffRole.CrewChief,
            StaffRole.RaceEngineer,
            StaffRole.Strategist,
            StaffRole.Spotter,
            StaffRole.Mechanic,
            StaffRole.PitCrew,
            StaffRole.PitCrew,
        };

        // Deterministic name + rating per (team, slot) so a reseed reproduces the same roster.
        public static List<Staff> ForTeam(Team team)
        {
            var list = new List<Staff>(Roster.Length);
            for (int i = 0; i < Roster.Length; i++)
            {
                int seed = team.Id * 31 + i * 7;
                string name = First[seed % First.Length] + " " + Last[(seed / 3) % Last.Length];

                // Centre ratings on the team's car rating, with a small per-slot spread; clamp 35-99.
                int spread = ((seed % 13) - 6);          // -6..+6
                int rating = Mathf.Clamp(team.CarRating + spread, 35, 99);

                list.Add(new Staff
                {
                    TeamId = team.Id,
                    Name = name,
                    Role = Roster[i],
                    Rating = rating,
                    Salary = 20000 + rating * 1500,
                    Morale = 70
                });
            }
            return list;
        }

        // Tiny local clamp so this file needs no UnityEngine dependency.
        static class Mathf
        {
            public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
