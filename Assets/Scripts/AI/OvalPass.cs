using Draftmaster.Sim;
using Draftmaster.Tracks;
using UnityEngine;

// Stage 3 of the oval AI rework (Docs/Oval-Lanes.md): whether the other lane has room for a car to move into it and
// pass. The rules are Draftmaster.Sim.LaneChange; this walks the field for them. SplineDriver asks it before starting
// a lane change and every step while one is under way. Road courses never get here: LaneFollowing is false off an
// oval.
public static class OvalPass
{
    // The rules every lane car passes by. Static so a test or a tuning pass can change the whole field at once.
    public static LaneChange.Spec Spec = LaneChange.Spec.Default;

    // Whether `target` lane is clear for `self` to move into: every car in it (or physically on its line, or itself
    // mid lane change) far enough ahead and behind. share = 1 to start a move, LaneChange.Spec.abortShare while one
    // is under way. gapToCarAhead = the gap to the car being passed (m, centre to centre).
    public static bool TargetLaneClear(SplineDriver self, OvalLanes.Lane target, float gapToCarAhead, float share)
    {
        if (self == null || self.TrackLength <= 0f) return false;
        float len = self.TrackLength;
        float myD = self.CentreDistanceOnTrack;
        float myV = OvalTrain.SpeedOf(self);
        float targetLat = self.LaneLateralAt(target, myD);
        var train = OvalTrain.Spec;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - len) > 0.5f) continue;
            bool inTarget = d.LaneFollowing && (d.ovalLane == target || d.LaneChanging)
                            || Mathf.Abs(d.CarLateral - targetLat) < OvalTrain.CorridorWidth;
            if (!inTarget) continue;
            float along = Along(d.CentreDistanceOnTrack - myD, len);
            if (!LaneChange.Clears(Spec, train, along, OvalTrain.SpeedOf(d), myV, gapToCarAhead, share)) return false;
        }

        // A free-driven human car (absent from RaceField) on the target lane's line.
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack != self.track) continue;
            if (Mathf.Abs(p.TrackLateral - targetLat) >= OvalTrain.CorridorWidth) continue;
            float along = Along(p.TrackDistance - myD, len);
            if (!LaneChange.Clears(Spec, train, along, p.SpeedMph / 2.237f, myV, gapToCarAhead, share)) return false;
        }
        return true;
    }

    // A signed gap along the lap wrapped into (-len / 2, len / 2]: + ahead, - behind.
    static float Along(float g, float len)
    {
        g %= len;
        if (g > len * 0.5f) g -= len;
        else if (g <= -len * 0.5f) g += len;
        return g;
    }
}
