using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // The three stock-car championships that share a race weekend. A real NASCAR venue runs all three over
    // the same three days: the trucks go Friday, the second-tier cars Saturday, the top series Sunday. The
    // player is entered in exactly one of them and can watch the other two.
    //
    // Deliberately a small enum rather than the SQLite Series table: the timetable has to build in menu
    // scenes and on the title screen, where DatabaseManager has never been opened (it lives in the race
    // scene). SeriesCatalog.ShortCode lines each one up with the row in the Series table for anything that
    // does have the database open.
    public enum RacingSeries
    {
        Trucks = 0,     // "Truck Series"          — TRK, Friday night
        National = 1,   // "National Stock Series" — NSS, Saturday afternoon
        Cup = 2,        // "Premier Cup Series"    — CUP, Sunday afternoon
    }

    public static class SeriesCatalog
    {
        // Ladder order, bottom rung first — the order the player climbs and the order the weekend runs in.
        public static readonly RacingSeries[] All = { RacingSeries.Trucks, RacingSeries.National, RacingSeries.Cup };

        const string PlayerSeriesKey = "weekend.series";

        // Which championship the player is entered in. Everything else at the venue is somebody else's race.
        public static RacingSeries PlayerSeries
        {
            get
            {
                int v = PlayerPrefs.GetInt(PlayerSeriesKey, (int)RacingSeries.Trucks);
                return (RacingSeries)Mathf.Clamp(v, 0, 2);
            }
            set
            {
                PlayerPrefs.SetInt(PlayerSeriesKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        // The two championships the player is only ever a spectator at this weekend.
        public static List<RacingSeries> Others(RacingSeries mine)
        {
            var list = new List<RacingSeries>(2);
            foreach (var s in All) if (s != mine) list.Add(s);
            return list;
        }

        public static string Name(RacingSeries s) => s switch
        {
            RacingSeries.Cup => "Premier Cup Series",
            RacingSeries.National => "National Stock Series",
            _ => "Truck Series",
        };

        // Timing-tower label, and the ShortName on the matching Series table row.
        public static string ShortCode(RacingSeries s) => s switch
        {
            RacingSeries.Cup => "CUP",
            RacingSeries.National => "NSS",
            _ => "TRK",
        };

        // What the paddock actually calls it.
        public static string Nickname(RacingSeries s) => s switch
        {
            RacingSeries.Cup => "Cup",
            RacingSeries.National => "National",
            _ => "Trucks",
        };

        public static int FieldSize(RacingSeries s) => s switch
        {
            RacingSeries.Cup => 38,
            RacingSeries.National => 32,
            _ => 36,
        };

        // Scheduled race distance in laps, before the track's own lap length is taken into account. Used by
        // the spectate sim for its lap counter and by the race director when the player is the one racing.
        public static int RaceLaps(RacingSeries s) => s switch
        {
            RacingSeries.Cup => 267,
            RacingSeries.National => 163,
            _ => 134,
        };

        // How long the race blocks the player's calendar for, in minutes.
        public static int RaceMinutes(RacingSeries s) => s switch
        {
            RacingSeries.Cup => 190,
            RacingSeries.National => 130,
            _ => 105,
        };

        // Base purse scale — the same figures the Series table is seeded with (DummySeries).
        public static int PurseScale(RacingSeries s) => s switch
        {
            RacingSeries.Cup => 500000,
            RacingSeries.National => 50000,
            _ => 120000,
        };

        // How much the world cares. Drives press-conference tone, appearance fees and crowd size.
        public static int Prestige(RacingSeries s) => s switch
        {
            RacingSeries.Cup => 100,
            RacingSeries.National => 50,
            _ => 65,
        };
    }
}
