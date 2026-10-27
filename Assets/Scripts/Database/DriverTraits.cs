using System.Collections.Generic;
using Draftmaster.Weekend;

// What a driver is known for, beyond their numbers: the TRAITS block on the garage laptop.
//
// Authored per driver by name — the same name key the per-series careers use (SeriesCareers2025.Key), so
// accents and dots don't matter. A driver nobody has written traits for has none, and the sheet says so.
public enum DriverTrait { Icon, FanFavourite, Aggressive }

public static class DriverTraits
{
    static readonly Dictionary<string, DriverTrait[]> ByDriver = new()
    {
        { SeriesCareers2025.Key("Ace Emerson"), new[] { DriverTrait.Icon, DriverTrait.FanFavourite, DriverTrait.Aggressive } },
    };

    static readonly DriverTrait[] None = new DriverTrait[0];

    public static IReadOnlyList<DriverTrait> For(string driverName) =>
        !string.IsNullOrEmpty(driverName) && ByDriver.TryGetValue(SeriesCareers2025.Key(driverName), out var traits)
            ? traits : None;

    public static string Label(DriverTrait trait) => trait switch
    {
        DriverTrait.Icon => "Icon",
        DriverTrait.FanFavourite => "Fan Favourite",
        DriverTrait.Aggressive => "Aggressive",
        _ => System.Text.RegularExpressions.Regex.Replace(trait.ToString(), "(?<=[a-z])(?=[A-Z])", " "),
    };
}
