using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// Which safety cell a car is built around, by the championship it races in.
//
// A cell is a greyscale mask painted over the car sprite — black where the shell is a tub and cannot fold,
// white where it is bodywork — so the shape is authored rather than described by two numbers. A truck's cab
// sits forward of a stock car's tub and is squarer; a rectangle centred on the sprite is the wrong shape for
// it at any size, which is why this is a picture and not a width and a height.
//
// The masks live at Resources/Cars/SafetyCell_<series>.png and are built by
// Draftmaster > Art > Build Safety Cell Masks. A series with no mask on disk falls back to VehicleDamage's
// own rectangle, so nothing breaks in a project that has never run the builder.
public static class SeriesSafetyCells
{
    public const string Folder = "Cars";

    // Resources path for one series' mask, without the extension — the same string the builder writes to.
    public static string ResourcePath(RacingSeries series) => $"{Folder}/SafetyCell_{FileCode(series)}";

    public static string AssetPath(RacingSeries series) =>
        $"Assets/Resources/{Folder}/SafetyCell_{FileCode(series)}.png";

    // Lower-case and spelled out, because these are filenames a person types and reads in the Project
    // window — not the timing tower's three-letter code.
    public static string FileCode(RacingSeries series) => series switch
    {
        RacingSeries.Cup => "cup",
        RacingSeries.National => "national",
        _ => "trucks",
    };

    // The shape each series is built with, in sprite fractions. `centre` is where the tub sits on the car
    // (x runs nose at 0 to tail at 1, y across the car), `half` is its half-size on each axis, `corner` is
    // how square the box is (2 = an ellipse, higher = squarer), and `soft` is the band outside it where the
    // metal goes from rigid to fully foldable.
    public struct Cell
    {
        public Vector2 centre;
        public Vector2 half;
        public float corner;
        public float soft;
    }

    public static Cell ShapeOf(RacingSeries series) => series switch
    {
        // A stock car's tub: the driver sits behind the middle of the wheelbase, so the cell does too.
        RacingSeries.Cup => new Cell
        {
            centre = new Vector2(0.54f, 0.5f),
            half = new Vector2(0.17f, 0.20f),
            corner = 3.2f,
            soft = 0.45f,
        },

        // The national car is the older chassis: the same idea, a little tighter, and it gives up its
        // bodywork more readily.
        RacingSeries.National => new Cell
        {
            centre = new Vector2(0.55f, 0.5f),
            half = new Vector2(0.155f, 0.185f),
            corner = 3.0f,
            soft = 0.5f,
        },

        // A truck is a cab in front of a bed. What has to survive is the cab, and the cab is forward of
        // centre, squarer, and as wide as the body — so the tail can fold right up to it.
        _ => new Cell
        {
            centre = new Vector2(0.40f, 0.5f),
            half = new Vector2(0.15f, 0.23f),
            corner = 4.5f,
            soft = 0.38f,
        },
    };

    // How rigid the shell is at this point of the sprite: 0 inside the cell, 1 out on the bodywork.
    //
    // The same function the mask is baked from, so a fallback drawn from it and the painted texture agree
    // about where the tub is.
    public static float DeformAt(in Cell cell, float fx, float fy)
    {
        float hx = Mathf.Max(1e-4f, cell.half.x);
        float hy = Mathf.Max(1e-4f, cell.half.y);
        float p = Mathf.Max(2f, cell.corner);

        // Superellipse: e is 1 on the edge of the cell, 0 at its middle, and grows outward from there.
        float ex = Mathf.Abs(fx - cell.centre.x) / hx;
        float ey = Mathf.Abs(fy - cell.centre.y) / hy;
        float e = Mathf.Pow(Mathf.Pow(ex, p) + Mathf.Pow(ey, p), 1f / p);

        return Mathf.Clamp01((e - 1f) / Mathf.Max(0.01f, cell.soft));
    }

    static readonly Dictionary<RacingSeries, Texture2D> _cache = new();
    static readonly HashSet<RacingSeries> _missingReported = new();

    // The painted mask for a series, or null if there isn't one on disk (or it was imported without
    // Read/Write, which VehicleDamage warns about and works around).
    public static Texture2D MaskFor(RacingSeries series)
    {
        if (_cache.TryGetValue(series, out var cached) && cached != null) return cached;

        var tex = Resources.Load<Texture2D>(ResourcePath(series));
        if (tex == null)
        {
            if (_missingReported.Add(series))
                Debug.Log($"SeriesSafetyCells: no mask at Resources/{ResourcePath(series)} — " +
                          $"{SeriesCatalog.Nickname(series)} cars fall back to the rectangular cell. " +
                          "Draftmaster > Art > Build Safety Cell Masks paints one.");
            return null;
        }

        _cache[series] = tex;
        return tex;
    }

    // Editor tools rebuild the textures under us; drop what we are holding so the next car picks up the
    // new paint rather than the copy loaded before the rebuild.
    public static void ForgetCache()
    {
        _cache.Clear();
        _missingReported.Clear();
    }
}
