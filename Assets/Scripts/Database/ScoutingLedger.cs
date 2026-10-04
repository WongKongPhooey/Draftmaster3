using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Weekend;
using UnityEngine;

// The save's record of which rival stats the player has uncovered — the runtime half of DriverScouting.
//
// One PlayerPrefs int per driver (a bitmask over DriverAttributeSheet.All), keyed by the driver's name, so it
// goes with CareerReset like the rest of the career. The player's own row is always fully known.
//
// Written by WeekendDirector when a session the player drove or watched is finished; read by the RV laptop
// (GarageScreenUI), which draws an unscouted stat as "??".
public static class ScoutingLedger
{
    const string Prefix = "scout.";

    static string[] _labels;
    public static string[] Labels
    {
        get
        {
            if (_labels != null) return _labels;
            var all = DriverAttributeSheet.All;
            _labels = new string[all.Length];
            for (int i = 0; i < all.Length; i++) _labels[i] = all[i].Label;
            return _labels;
        }
    }

    public static int Total => Labels.Length;

    static string Key(Driver d) =>
        Prefix + ((d.FirstName ?? "") + " " + (d.LastName ?? "")).Trim().ToUpperInvariant();

    public static int Mask(Driver d) => d == null ? 0 : PlayerPrefs.GetInt(Key(d), 0);

    // Is this stat of this driver's on show? The player always knows their own.
    public static bool IsKnown(Driver d, int attributeIndex, bool isPlayer) =>
        isPlayer || DriverScouting.Known(Mask(d), attributeIndex);

    public static int KnownCount(Driver d, bool isPlayer) =>
        isPlayer ? Total : DriverScouting.KnownCount(Mask(d), Total);

    // A session has just been driven or watched: uncover what it shows of everyone in that field. Returns the
    // number of stats newly uncovered across the field (0 for anything that is not a session).
    public static int RecordSession(ActivityKind kind, RacingSeries series)
    {
        int per = DriverScouting.RevealsFor(kind);
        if (per <= 0) return 0;

        var row = SeriesRow(series);
        if (row == null) return 0;

        bool mine = series == SeriesCatalog.PlayerSeries;
        int playerNumber = PlayerDriver.CarNumber;
        string aptitude = AptitudeLabel(TrackSelection.CurrentType);

        int uncovered = 0;
        foreach (var d in SeriesRoster.Drivers(row))
        {
            if (d == null || (mine && d.CarNumber == playerNumber)) continue;
            string key = Key(d);
            int before = PlayerPrefs.GetInt(key, 0);
            int after = DriverScouting.Reveal(before, Labels, kind, aptitude, per, DriverScouting.Seed(key));
            if (after == before) continue;
            PlayerPrefs.SetInt(key, after);
            uncovered += DriverScouting.KnownCount(after, Total) - DriverScouting.KnownCount(before, Total);
        }
        if (uncovered > 0) PlayerPrefs.Save();
        return uncovered;
    }

    // The laptop's series row for one of the weekend's three championships (matched by short code).
    public static Draftmaster.Data.Series SeriesRow(RacingSeries series)
    {
        string code = SeriesCatalog.ShortCode(series);
        foreach (var s in SeriesRoster.AllSeries())
            if (s != null && s.ShortName == code) return s;
        return null;
    }

    // The sheet's aptitude label for the kind of track the weekend is at.
    static string AptitudeLabel(TrackType type) => type switch
    {
        TrackType.Superspeedway => "SUPERSPEEDWAY",
        TrackType.Speedway => "SPEEDWAYS",
        TrackType.ShortTrack => "SHORT TRACKS",
        TrackType.RoadCourse => "ROAD COURSES",
        TrackType.DirtCourse => "DIRT COURSES",
        _ => null,
    };
}
