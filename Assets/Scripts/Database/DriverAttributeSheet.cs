using System;
using Draftmaster.Data;

// The Drivers table read as a character sheet: every attribute the database rates a driver on, in the
// order the garage draws them, with the label and the scale each one uses.
//
// One list, read from both ends — IronOvalGarageBuilder lays the rows out at edit time, GarageScreenUI
// fills them in at run time — so a row can never end up labelled one thing and filled with another. Add
// a stat column to Driver, add it to the right group here, and both ends pick it up on the next rebuild.
//
// `All` is the row order the garage builds in: the two ability ratings full width across the top, then
// the left column (track types, then standing) and the right column (craft). Nothing else may reorder it.
//
// Labels are sized for the 8px Silkscreen the garage's narrow blocks use — about 6.3px a character with
// the kit's tracking — so 14 characters is the most the sheet's 90px label box takes without wrapping.
public static class DriverAttributeSheet
{
    public readonly struct Attribute
    {
        public readonly string Label;
        public readonly Func<Driver, int> Read;
        // The top of this attribute's scale: 20 for a skill, 100 for an overall ability rating.
        public readonly int Max;

        public Attribute(string label, Func<Driver, int> read, int max)
        {
            Label = label;
            Read = read;
            Max = max;
        }
    }

    // Overall ability, 0-100. Where the driver is now and how good they can still get.
    public static readonly Attribute[] Ability =
    {
        new Attribute("ABILITY",   d => d.CurrentAbility,   Driver.AbilityMax),
        new Attribute("POTENTIAL", d => d.PotentialAbility, Driver.AbilityMax),
    };

    // Track-type aptitudes, 0-20.
    public static readonly Attribute[] TrackTypes =
    {
        new Attribute("SHORT TRACKS",  d => d.ShortTracks,     Driver.StatMax),
        new Attribute("SPEEDWAYS",     d => d.Speedways,       Driver.StatMax),
        new Attribute("SUPERSPEEDWAY", d => d.Superspeedways,  Driver.StatMax),
        new Attribute("ROAD COURSES",  d => d.RoadCourses,     Driver.StatMax),
        new Attribute("DIRT COURSES",  d => d.DirtCourses,     Driver.StatMax),
        new Attribute("OPEN WHEEL",    d => d.OpenWheel,       Driver.StatMax),
    };

    // Craft, 0-20 — how the driver races once the flag drops.
    public static readonly Attribute[] Craft =
    {
        new Attribute("QUALIFYING",   d => d.Qualifying,     Driver.StatMax),
        new Attribute("CONSISTENCY",  d => d.Consistency,    Driver.StatMax),
        new Attribute("AGGRESSION",   d => d.Aggression,     Driver.StatMax),
        new Attribute("AWARENESS",    d => d.Awareness,      Driver.StatMax),
        new Attribute("ADAPTABILITY", d => d.Adaptability,   Driver.StatMax),
        new Attribute("TYRE MGMT",    d => d.TyreManagement, Driver.StatMax),
        new Attribute("FUEL MGMT",    d => d.FuelManagement, Driver.StatMax),
    };

    // Commercial standing, 0-20 — what the name is worth off the track.
    public static readonly Attribute[] Standing =
    {
        new Attribute("SPONSOR APPEAL", d => d.SponsorAppeal, Driver.StatMax),
        new Attribute("FAN SUPPORT",    d => d.FanSupport,    Driver.StatMax),
        new Attribute("PRESTIGE",       d => d.Prestige,      Driver.StatMax),
    };

    // Every attribute in garage row order. Built by concatenation rather than written out again, so the
    // groups above stay the only place a stat is declared.
    public static readonly Attribute[] All = Concat(Ability, TrackTypes, Standing, Craft);

    static Attribute[] Concat(params Attribute[][] groups)
    {
        int count = 0;
        foreach (var g in groups) count += g.Length;

        var all = new Attribute[count];
        int at = 0;
        foreach (var g in groups)
        {
            Array.Copy(g, 0, all, at, g.Length);
            at += g.Length;
        }
        return all;
    }
}
