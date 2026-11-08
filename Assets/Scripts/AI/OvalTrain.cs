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

    // Below this (m/s, 20 mph) a car ahead is stopped - a wreck, a spin - not a car to run in a train behind. It is
    // followed only if it is physically in our path (lane identity means nothing once it has spun out of its lane),
    // and not at all while the brain is steering round it (SplineDriver.avoidingStopped): braking in the train for
    // a car already being driven round was the lift that piled the field up behind every wreck.
    public const float StoppedMps = 20f * MphToMps;

    // The nearest car ahead in this car's lane (or physically in its path), within half a lap: the centre-to-centre
    // gap along the track and its speed. False when there is none.
    public static bool TryFindAhead(SplineDriver self, out float gap, out float aheadMps) =>
        TryFindAhead(self, out gap, out aheadMps, out _);

    // The same, and the car it is (null for a free-driven human car).
    public static bool TryFindAhead(SplineDriver self, out float gap, out float aheadMps, out SplineDriver ahead)
    {
        ahead = null;
        gap = float.MaxValue;
        aheadMps = 0f;
        if (self == null || self.TrackLength <= 0f) return false;
        float len = self.TrackLength;
        float myD = self.CentreDistanceOnTrack;
        float myLat = self.CarLateral;
        bool found = false;

        var watch = CautionWatch.Instance;   // a car in the middle of a crash counts as stopped
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - len) > 0.5f) continue;
            bool stopped = SpeedOf(d) < StoppedMps || watch != null && watch.IsWrecking(d.transform);
            if (stopped && self.avoidingStopped) continue;
            bool inLane = d.LaneFollowing && d.ovalLane == self.ovalLane && !stopped
                          || Mathf.Abs(d.CarLateral - myLat) < CorridorWidth;
            float g = Ahead(d.CentreDistanceOnTrack - myD, len);
            if (g <= 0f || g >= gap) continue;
            // A car in another lane whose line runs into ours just past it: the ideal line meets a side lane at
            // every entry, apex and exit (it is drawn on the side lines there), so a car on the ideal is in the
            // path of one in that side lane a few car lengths before their lines touch - followed from now.
            if (!inLane && (stopped || !(g < ConvergeLookMetres && LinesMeet(self, self.ovalLane, d, d.CentreDistanceOnTrack,
                                                                 d.CentreDistanceOnTrack + ConvergeAheadMetres))))
                continue;
            gap = g;
            aheadMps = SpeedOf(d);
            ahead = d;
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
            ahead = null;
            found = true;
        }
        return found;
    }

    // Lines (or a car and a line) this close, centre to centre, put two 2 m wide cars within half a metre of
    // touching. The side lanes are at least 3.2 m apart, so only the ideal line ever comes this near a side lane.
    public const float LineClearance = 2.4f;
    // How far ahead (m) a car in another lane is checked for its line running into ours, and how far past it the
    // two lines are compared.
    const float ConvergeLookMetres = 60f;
    const float ConvergeAheadMetres = 50f;
    const float LineStepMetres = 5f;

    // Whether `other` (holding a lane, not mid lane change) runs on a line that comes within LineClearance of
    // `myLane`'s line anywhere from `from` to `to` (m along the lap). Every lane car on a track lays the same three
    // lines, so both are read off `self`.
    public static bool LinesMeet(SplineDriver self, Draftmaster.Tracks.OvalLanes.Lane myLane, SplineDriver other,
                                 float from, float to)
    {
        const Draftmaster.Tracks.OvalLanes.Lane None = Draftmaster.Tracks.OvalLanes.Lane.None;
        if (self == null || other == null || !self.LaneFollowing || !other.LaneFollowing || other.LaneChanging) return false;
        var theirs = other.ovalLane;
        if (myLane == None || theirs == None) return false;
        if (theirs == myLane) return true;
        for (float x = from; ; x += LineStepMetres)
        {
            if (x > to) x = to;
            if (Mathf.Abs(self.LaneLateralAt(myLane, x) - self.LaneLateralAt(theirs, x)) < LineClearance) return true;
            if (x >= to) return false;
        }
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
