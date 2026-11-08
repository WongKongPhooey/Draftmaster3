using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // The restart order under a full-course caution. The field is frozen where it ran the moment the caution came out
    // (scored then, not where the cars are when they get back to the safety car), with two changes made to it:
    //
    //   * a car that pits under the caution comes out behind every car that stayed out, in the order it leaves pit road;
    //   * a car caught up in the crash - spun, in the wall, stopped, or wrecking in the seconds after the flag - goes to
    //     the tail of the queue, behind everybody, whatever else it does.
    //
    // Cars the snapshot never saw (joined after it) go after the cars that stayed out, ordered by pit-exit then
    // snapshot as best known. Ids are opaque ints (instance ids at runtime), so this stays testable without a scene.
    public sealed class CautionOrder
    {
        readonly Dictionary<int, int> _snapshot = new();   // id -> running position at the flag (0 = leader)
        readonly HashSet<int> _caught = new();
        readonly Dictionary<int, int> _pitExit = new();    // id -> order it came off pit road (0 = first)
        readonly HashSet<int> _onPit = new();
        int _nextExit;

        public int Count => _snapshot.Count;
        public bool Contains(int id) => _snapshot.ContainsKey(id);
        public bool IsCaught(int id) => _caught.Contains(id);
        public bool HasPitted(int id) => _onPit.Contains(id) || _pitExit.ContainsKey(id);

        // The running order at the flag, leader first.
        public void Freeze(IReadOnlyList<int> runningOrder)
        {
            _snapshot.Clear();
            _caught.Clear();
            _pitExit.Clear();
            _onPit.Clear();
            _nextExit = 0;
            if (runningOrder == null) return;
            for (int i = 0; i < runningOrder.Count; i++)
                if (!_snapshot.ContainsKey(runningOrder[i])) _snapshot[runningOrder[i]] = i;
        }

        // A car caught up in the crash: to the tail. Stays caught until the next Freeze.
        public void MarkCaught(int id) => _caught.Add(id);

        // Where a car is right now - on pit road or not. A car seen on pit road under the caution has pitted; it is
        // given its place behind the cars that stayed out when it leaves pit road.
        public void Observe(int id, bool onPitRoad)
        {
            if (onPitRoad) { _onPit.Add(id); return; }
            if (_onPit.Remove(id) && !_pitExit.ContainsKey(id)) _pitExit[id] = _nextExit++;
        }

        // Sort key, lowest first. Group (stayed out / pitted or unknown / caught) dominates; inside a group, snapshot
        // order - or pit-exit order for the cars that pitted (still on pit road = after every car already out).
        public long Key(int id)
        {
            bool known = _snapshot.TryGetValue(id, out int snap);
            if (!known) snap = 9999;
            if (_caught.Contains(id)) return 2L * 100_000_000L + snap;
            if (_onPit.Contains(id)) return 1L * 100_000_000L + 50_000_000L + snap;
            if (_pitExit.TryGetValue(id, out int exit)) return 1L * 100_000_000L + exit * 10_000L + snap;
            if (!known) return 1L * 100_000_000L + 60_000_000L;
            return snap;
        }

        // Whether `id` ranks behind `other` in the restart order.
        public bool Behind(int id, int other) => Key(id) > Key(other);
    }
}
