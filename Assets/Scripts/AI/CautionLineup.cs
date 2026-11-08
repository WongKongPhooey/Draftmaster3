using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// The restart queue under a full-course caution (rules: Draftmaster.Sim.CautionOrder). FullCourseCaution freezes it the
// moment the caution comes out: the running order then is the order the field restarts in, except that a car that pits
// comes out behind the cars that stayed out, and a car caught up in the crash goes to the tail.
//
// Caught up in the crash = CautionWatch saw it wrecking (spun, sideways, in the wall, stopped on the track) in the
// seconds before the flag, or it wrecks in the first few seconds after it - a crash is still happening when the flag
// comes out.
//
// The order is what the position readout and the classification show for as long as the caution lasts
// (RacePositionTracker sorts by it), and the field drives into it: a caught car with anybody behind it who is due
// ahead of it slows and moves down to the inside to be passed (FormationController, PaceLapAssist for the player),
// and the cars due ahead go by it on the outside. Nobody else passes under the caution.
public static class CautionLineup
{
    static readonly CautionOrder _order = new();
    static readonly HashSet<Transform> _yielding = new();
    static readonly List<int> _scratch = new();
    static float _declaredAt = -1f;
    static float _caughtGrace;

    // A caution's restart queue is frozen and in force.
    public static bool Active { get; private set; }

    public static void Begin(RacePositionTracker rt, IEnumerable<Transform> caught, float now, float caughtGraceSeconds)
    {
        _scratch.Clear();
        if (rt != null)
        {
            var order = rt.Order;
            for (int i = 0; i < order.Count; i++)
                if (order[i] != null && order[i].tf != null) _scratch.Add(order[i].tf.GetInstanceID());
        }
        _order.Freeze(_scratch);
        _yielding.Clear();
        if (caught != null)
            foreach (var t in caught)
                if (t != null) _order.MarkCaught(t.GetInstanceID());
        _declaredAt = now;
        _caughtGrace = Mathf.Max(0f, caughtGraceSeconds);
        Active = true;
    }

    public static void End()
    {
        Active = false;
        _yielding.Clear();
        _order.Freeze(null);
        _declaredAt = -1f;
    }

    // Once a step under the caution: cars still wrecking in the grace after the flag join the tail, and pit road is
    // watched for the cars that pit (they restart behind the cars that stayed out).
    public static void Step(RacePositionTracker rt, float now)
    {
        if (!Active || rt == null) return;
        var watch = CautionWatch.Instance;
        bool grace = now - _declaredAt <= _caughtGrace;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;
            int id = e.tf.GetInstanceID();
            if (grace && watch != null && watch.IsWrecking(e.tf)) _order.MarkCaught(id);
            bool onPit = e.spline != null && e.spline.enabled ? e.spline.IsOnPit
                       : rt.track != null && rt.track.IsOnPitSurface(e.tf.position);
            _order.Observe(id, onPit);
        }
        _yielding.RemoveWhere(t => t == null);
    }

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
