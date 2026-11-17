using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// The restart queue under a full-course caution (rules: Draftmaster.Sim.CautionOrder). FullCourseCaution takes it from
// the running order the moment the caution comes out, and it changes only three ways while the caution runs:
//
//   * a car that stops on the track, for any length of time, is dropped from it;
//   * once that car is back up to speed (rejoinMph, 30) it joins the back of the queue;
//   * a car that pits loses its place, and takes whatever place it is in on the road when it leaves pit road.
//
// The order is what the position readout and the classification show for as long as the caution lasts
// (RacePositionTracker sorts by it), and the field drives into it: a car that stopped and rejoined at the back, with
// anybody behind it who is due ahead of it, slows and moves down to the inside to be passed (FormationController,
// PaceLapAssist for the player), and the cars due ahead go by it on the outside. Nobody else passes under the caution.
public static class CautionLineup
{
    static readonly CautionOrder _order = new();
    static readonly HashSet<Transform> _yielding = new();
    static readonly List<int> _scratch = new();
    static readonly Dictionary<Transform, PlayerVehicleController> _pvcOf = new();

    // A caution's restart queue is frozen and in force.
    public static bool Active { get; private set; }

    // The queue from the tracker's running order now; `stopped` = cars already stopped on the track, dropped from it at
    // once. A car is stopped under `stoppedMph` and rejoins (at the back) at `rejoinMph`.
    public static void Begin(RacePositionTracker rt, IEnumerable<Transform> stopped, float stoppedMph, float rejoinMph)
    {
        _scratch.Clear();
        if (rt != null)
        {
            var order = rt.Order;
            for (int i = 0; i < order.Count; i++)
                if (order[i] != null && order[i].tf != null) _scratch.Add(order[i].tf.GetInstanceID());
        }
        _order.Freeze(_scratch);
        _order.StopMps = Mathf.Max(0f, stoppedMph) * CautionOrder.MphToMps;
        _order.RejoinMps = Mathf.Max(_order.StopMps, rejoinMph * CautionOrder.MphToMps);
        _yielding.Clear();
        _pvcOf.Clear();
        if (stopped != null)
            foreach (var t in stopped)
                if (t != null) _order.Drop(t.GetInstanceID());
        Active = true;
    }

    public static void End()
    {
        Active = false;
        _yielding.Clear();
        _pvcOf.Clear();
        _order.Freeze(null);
    }

    // Once a step under the caution: every car's speed, pit road and race distance into the queue (CautionOrder.Observe)
    // - stopped cars out, cars back up to speed onto the back, cars off pit road in where they are.
    public static void Step(RacePositionTracker rt)
    {
        if (!Active || rt == null) return;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;
            bool onPit = e.spline != null && e.spline.enabled ? e.spline.IsOnPit
                       : rt.track != null && rt.track.IsOnPitSurface(e.tf.position);
            _order.Observe(e.tf.GetInstanceID(), onPit, SpeedOf(e), e.progress);
        }
        _yielding.RemoveWhere(t => t == null);
    }

    // The car's real speed, step by step: the dynamic model's when it drives the car, else the brain's. The tracker's
    // own figure is a per-frame difference of positions, which reads 0 on any frame a kinematic car was not moved in -
    // and one zero would drop the car from the queue.
    static float SpeedOf(RacePositionTracker.Entry e)
    {
        if (!_pvcOf.TryGetValue(e.tf, out var pvc)) _pvcOf[e.tf] = pvc = e.tf.GetComponent<PlayerVehicleController>();
        if (pvc != null && pvc.enabled) return pvc.SpeedMps;
        if (e.spline != null && e.spline.isActiveAndEnabled) return OvalTrain.SpeedOf(e.spline);
        return e.speedMps;
    }

    // Stopped under this caution and not since placed by a pit exit: dropped, or at the back of the queue it rejoined.
    public static bool IsCaught(Transform t) => Active && t != null && _order.IsCaught(t.GetInstanceID());

    // Sort key for the restart order, lowest first (CautionOrder.Key).
    public static long Key(Transform t) => t != null ? _order.Key(t.GetInstanceID()) : long.MaxValue;

    // Whether `t` is due behind `other` in the restart queue.
    public static bool DueBehind(Transform t, Transform other) =>
        Active && t != null && other != null && _order.Behind(t.GetInstanceID(), other.GetInstanceID());

    // A caught car moving aside to be passed (set by whoever drives it).
    public static void SetYielding(Transform t, bool yielding)
    {
        if (t == null) return;
        if (yielding && Active) _yielding.Add(t); else _yielding.Remove(t);
    }

    public static bool IsYielding(Transform t) => Active && t != null && _yielding.Contains(t);

    // A caught car should be letting a car by: something due ahead of it in the queue is on the track within
    // `behindM` behind it (or alongside it). Pit road does not count - those cars come out wherever they come out.
    public static bool ShouldYield(Transform self, float selfDistance, float lapLength, TrackBuilder road, float behindM)
    {
        if (!Active || self == null || lapLength <= 0f || !IsCaught(self)) return false;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d.transform == self || !d.isActiveAndEnabled || d.IsOnPit) continue;
            if (System.Math.Abs(d.TrackLength - lapLength) > 0.5f) continue;
            if (d.qualifyingPosition == FormationOrder.SafetyCarGrid) continue;
            if (!DueBehind(self, d.transform)) continue;
            float behind = PackAvoidance.SignedGap(selfDistance, d.CentreDistanceOnTrack, lapLength);
            if (behind > -6f && behind < behindM) return true;
        }
        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.transform == self) continue;
            if (road != null && p.ObstacleTrack != road) continue;
            if (!DueBehind(self, p.transform)) continue;
            float behind = PackAvoidance.SignedGap(selfDistance, p.TrackDistance, lapLength);
            if (behind > -6f && behind < behindM) return true;
        }
        return false;
    }
}
