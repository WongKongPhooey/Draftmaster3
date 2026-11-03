using Draftmaster.Sim;
using UnityEngine;

// Stage 2 of the oval AI rework (Docs/Oval-Lanes.md): a car holding an oval lane runs single file behind the car
// ahead of it in that lane, holding the gap by Draftmaster.Sim.TrainFollow. SplineDriver asks for the car ahead
// each step (only while it is LaneFollowing, under green, on the main track); SplineInputDriver turns the speed
// it allows into a lift first and a gentle brake only if it must. Road courses never get here: LaneFollowing is
// false off an oval.
public static class OvalTrain
{
    // The gap law every lane car follows (TrainFollow.Spec.Default). Static so a test or a tuning pass can change
    // the whole field at once.
    public static TrainFollow.Spec Spec = TrainFollow.Spec.Default;

    // Centre to centre across the track (m) inside which a car is in our path whatever lane it was given: two
    // 2 m wide cars closer than this would touch. The lanes are 3.2 m apart, so a car in the other lane never is.
    public const float CorridorWidth = 2f;

    const float MphToMps = 1f / 2.237f;

    // The nearest car ahead in this car's lane (or physically in its path), within half a lap: the centre-to-centre
    // gap along the track and its speed. False when there is none.
    public static bool TryFindAhead(SplineDriver self, out float gap, out float aheadMps)
    {
        gap = float.MaxValue;
        aheadMps = 0f;
        if (self == null || self.TrackLength <= 0f) return false;
        float len = self.TrackLength;
        float myD = self.CentreDistanceOnTrack;
        float myLat = self.CarLateral;
        bool found = false;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - len) > 0.5f) continue;
            bool inLane = d.LaneFollowing && d.ovalLane == self.ovalLane
                          || Mathf.Abs(d.CarLateral - myLat) < CorridorWidth;
            if (!inLane) continue;
            float g = Ahead(d.CentreDistanceOnTrack - myD, len);
            if (g <= 0f || g >= gap) continue;
            gap = g;
            aheadMps = SpeedOf(d);
            found = true;
        }

        // A free-driven human car (absent from RaceField): followed if it is in our path.
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack != self.track) continue;
            if (Mathf.Abs(p.TrackLateral - myLat) >= CorridorWidth) continue;
            float g = Ahead(p.TrackDistance - myD, len);
            if (g <= 0f || g >= gap) continue;
            gap = g;
            aheadMps = p.SpeedMph * MphToMps;
            found = true;
        }
        return found;
    }

    // The car's real speed: the dynamic model's when one drives it, else the brain's.
    public static float SpeedOf(SplineDriver d) =>
        d.externalMotionController ? d.externalActualSpeedMps : d.CurrentMph * MphToMps;

    // A forward gap wrapped into (0, len / 2]; anything behind or more than half a lap on is not ahead (-1).
    static float Ahead(float g, float len)
    {
        g %= len;
        if (g < 0f) g += len;
        return g > 0f && g <= len * 0.5f ? g : -1f;
    }
}
