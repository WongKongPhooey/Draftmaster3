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

    // TargetLaneClear's look along the lines: cars this far behind / ahead (m) are checked for a line that meets the
    // target lane's, over the gap between us plus the ground the move covers (at least the minimum metres).
    const float MeetBehindMetres = 40f;
    const float MeetAheadMetres = 100f;
    const float MeetMinReachMetres = 20f;

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
        // How far the move itself carries the car (m along): once it has landed, a line meeting ours further on is
        // the train's to handle (OvalTrain.TryFindAhead follows a car whose line runs into ours).
        float spacing = Mathf.Abs(targetLat - self.LaneLateralAt(self.ovalLane, myD));
        float reach = Mathf.Max(MeetMinReachMetres, myV * LaneChange.MoveDuration(Spec, spacing));

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - len) > 0.5f) continue;
            bool inTarget = d.LaneFollowing && (d.ovalLane == target || d.LaneChanging)
                            || Mathf.Abs(d.CarLateral - targetLat) < OvalTrain.CorridorWidth;
            float along = Along(d.CentreDistanceOnTrack - myD, len);
            // A car in another lane whose line the target line runs into over the stretch the move covers (and the
            // car with it): onto the ideal where it is about to meet a side lane is onto that lane's car.
            if (!inTarget && along > -MeetBehindMetres && along < MeetAheadMetres)
                inTarget = OvalTrain.LinesMeet(self, target, d, myD + Mathf.Min(0f, along), myD + Mathf.Max(0f, along) + reach);
            if (!inTarget) continue;
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

    // The car nearest alongside `self`: centre to centre within `window` metres along the track, on the main track,
    // anywhere across it. Its lateral, and the lane it holds or is moving to (None for a car with no lane, such as
    // a free-driven human car). False when nobody is alongside.
    public static bool TryFindAlongside(SplineDriver self, float window, out float otherLateral, out OvalLanes.Lane otherLane)
    {
        otherLateral = 0f;
        otherLane = OvalLanes.Lane.None;
        if (self == null || self.TrackLength <= 0f) return false;
        float len = self.TrackLength;
        float myD = self.CentreDistanceOnTrack;
        float best = window;
        bool found = false;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - len) > 0.5f) continue;
            float along = Mathf.Abs(Along(d.CentreDistanceOnTrack - myD, len));
            if (along >= best) continue;
            best = along;
            otherLateral = d.CarLateral;
            otherLane = d.LaneFollowing ? d.ovalLane : OvalLanes.Lane.None;
            found = true;
        }

        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack != self.track) continue;
            float along = Mathf.Abs(Along(p.TrackDistance - myD, len));
            if (along >= best) continue;
            best = along;
            otherLateral = p.TrackLateral;
            otherLane = OvalLanes.Lane.None;
            found = true;
        }
        return found;
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
