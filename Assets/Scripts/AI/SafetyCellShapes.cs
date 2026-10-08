using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The safety cell of each championship's car, as drawn in Draftmaster > Art > Safety Cell Editor.
//
// One box per series, in sprite fractions (x runs nose at 0 to tail at 1, y across the car from the bottom of
// the livery): inside it the shell is the roll cage and never folds; outside it is bodywork. `corner` rounds the
// box (2 = an ellipse, higher = squarer) and `soft` is the band outside it where the metal goes from rigid to
// fully foldable. SeriesSafetyCells.ShapeOf reads this; a series with no entry keeps the built-in shape.
//
// Lives in Resources so a build reads it too. The editor window is the only thing that writes it, and it bakes
// the series' mask PNG (Resources/Cars/SafetyCell_<series>.png) in the same save, so the two cannot disagree.
[CreateAssetMenu(menuName = "Draftmaster/Safety Cell Shapes", fileName = "SafetyCellShapes")]
public class SafetyCellShapes : ScriptableObject
{
    public const string ResourcePath = "Cars/SafetyCellShapes";
    public const string AssetPath = "Assets/Resources/Cars/SafetyCellShapes.asset";

    [System.Serializable]
    public class Entry
    {
        public RacingSeries series;
        [Tooltip("Middle of the cell, sprite fractions (x nose 0 -> tail 1, y across).")]
        public Vector2 centre = new Vector2(0.54f, 0.5f);
        [Tooltip("Half-size of the cell on each axis, sprite fractions.")]
        public Vector2 half = new Vector2(0.17f, 0.2f);
        [Tooltip("How square the box is: 2 = an ellipse, 8 = nearly a sharp rectangle.")]
        [Range(2f, 12f)] public float corner = 6f;
        [Tooltip("Band outside the cell (as a fraction of its size) where the metal goes from rigid to fully foldable.")]
        [Range(0.02f, 1f)] public float soft = 0.35f;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Find(RacingSeries series)
    {
        foreach (var e in entries)
            if (e != null && e.series == series) return e;
        return null;
    }

    static SafetyCellShapes _loaded;
    static bool _looked;

    public static SafetyCellShapes Load()
    {
        if (!_looked || _loaded == null)
        {
            _looked = true;
            _loaded = Resources.Load<SafetyCellShapes>(ResourcePath);
        }
        return _loaded;
    }

    // The editor writes the asset under us; the next lookup reads it again.
    public static void ForgetCache()
    {
        _loaded = null;
        _looked = false;
    }
}
