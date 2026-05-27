using System.Collections.Generic;

public static class RaceField
{
    static readonly List<SplineDriver> _drivers = new();
    public static IReadOnlyList<SplineDriver> Drivers => _drivers;

    public static void Register(SplineDriver d)
    {
        if (d == null) return;
        if (!_drivers.Contains(d)) _drivers.Add(d);
    }

    public static void Unregister(SplineDriver d)
    {
        if (d == null) return;
        _drivers.Remove(d);
    }

    public static bool TryGetAhead(SplineDriver self, float maxRange, out SplineDriver ahead, out float gap)
    {
        ahead = null;
        gap = float.MaxValue;
        if (self == null || self.TrackLength <= 0f) return false;
        float selfDist = self.DistanceOnTrack;
        float trackLen = self.TrackLength;
        for (int i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            if (d == self || d == null) continue;
            if (System.Math.Abs(d.TrackLength - trackLen) > 0.5f) continue;
            float g = d.DistanceOnTrack - selfDist;
            if (g <= 0f) g += trackLen;
            if (g > 0f && g <= maxRange && g < gap) { gap = g; ahead = d; }
        }
        return ahead != null;
    }

    public static bool TryGetBehind(SplineDriver self, float maxRange, out SplineDriver behind, out float gap)
    {
        behind = null;
        gap = float.MaxValue;
        if (self == null || self.TrackLength <= 0f) return false;
        float selfDist = self.DistanceOnTrack;
        float trackLen = self.TrackLength;
        for (int i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            if (d == self || d == null) continue;
            if (System.Math.Abs(d.TrackLength - trackLen) > 0.5f) continue;
            float g = selfDist - d.DistanceOnTrack;
            if (g <= 0f) g += trackLen;
            if (g > 0f && g <= maxRange && g < gap) { gap = g; behind = d; }
        }
        return behind != null;
    }
}
