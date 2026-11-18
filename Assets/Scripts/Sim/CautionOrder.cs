using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // The restart order under a full-course caution: a queue, taken from the running order the moment the caution comes
    // out (scored then, not where the cars are when they get back to the safety car), then changed in exactly three ways:
    //
    //   * a car that stops - any speed under StopMps, for any length of time, out on the track - is dropped from the
    //     queue; it holds no place at all while it is stopped and crawling;
    //   * once it is back up to RejoinMps it joins the BACK of the queue (two cars getting going again join in the order
    //     they reach that speed);
    //   * a car that pits loses its place in the queue the moment it is on pit road, and when it leaves pit road it
    //     simply takes whatever place it is in on the road: in front of the first car in the queue it is ahead of by
    //     race distance (laps and all), behind every car it is not. A stop on pit road is a pit stop, not a stop.
    //
    // Cars not holding a place sort after every car that does: on pit road first, then cars the queue never saw, then
    // stopped cars. Ids are opaque ints (instance ids at runtime), so this stays testable without a scene.
    public sealed class CautionOrder
    {
        public const float MphToMps = 1f / 2.237f;
        public const float DefaultStopMph = 3f;
        public const float DefaultRejoinMph = 30f;

        // Under this (m/s) out on the track = stopped: dropped from the queue.
        public float StopMps = DefaultStopMph * MphToMps;
        // A dropped car back up to this (m/s) joins the back of the queue.
        public float RejoinMps = DefaultRejoinMph * MphToMps;

        readonly List<int> _queue = new();                  // the restart order, leader first
        readonly Dictionary<int, int> _snapshot = new();    // id -> running position at the flag (0 = leader)
        readonly Dictionary<int, double> _distance = new(); // id -> race distance last observed
        readonly HashSet<int> _dropped = new();             // stopped on the track, out of the queue
        readonly HashSet<int> _onPit = new();               // on pit road, out of the queue
        readonly HashSet<int> _pitted = new();              // has been on pit road under this caution
        readonly HashSet<int> _caught = new();              // stopped and not since placed by a pit exit

        public int Count => _snapshot.Count;
        public IReadOnlyList<int> Queue => _queue;
        public bool Contains(int id) => _snapshot.ContainsKey(id);
        public bool IsQueued(int id) => _queue.Contains(id);
        public bool IsDropped(int id) => _dropped.Contains(id);
        public bool IsOnPit(int id) => _onPit.Contains(id);
        public bool HasPitted(int id) => _pitted.Contains(id);
        // How many cars are on pit road right now (out of the queue until they leave it).
        public int OnPitCount => _onPit.Count;

        // Stopped under this caution: dropped, or back at the tail it rejoined at. A pit exit clears it - that car has
        // its place by where it is on the road, so nobody is due to pass it.
        public bool IsCaught(int id) => _caught.Contains(id);

        // The running order at the flag, leader first: the queue.
        public void Freeze(IReadOnlyList<int> runningOrder)
        {
            _queue.Clear();
            _snapshot.Clear();
            _distance.Clear();
            _dropped.Clear();
            _onPit.Clear();
            _pitted.Clear();
            _caught.Clear();
            if (runningOrder == null) return;
            for (int i = 0; i < runningOrder.Count; i++)
            {
                int id = runningOrder[i];
                if (_snapshot.ContainsKey(id)) continue;
                _snapshot[id] = i;
                _queue.Add(id);
            }
        }

        // A car known to be stopped on the track: out of the queue until it is back up to RejoinMps.
        public void Drop(int id)
        {
            _queue.Remove(id);
            _onPit.Remove(id);
            _dropped.Add(id);
            _caught.Add(id);
        }

        // Where a car is right now: on pit road or not, how fast, and how far into the race (laps x lap length + distance
        // into the lap - any measure that grows with the race, the same for every car). Once per car per step.
        public void Observe(int id, bool onPitRoad, float speedMps, double raceDistance)
        {
            _distance[id] = raceDistance;

            if (onPitRoad)
            {
                // Pitting: out of the queue. A stop in the pit box is a pit stop, not a stop.
                _queue.Remove(id);
                _dropped.Remove(id);
                _onPit.Add(id);
                _pitted.Add(id);
                return;
            }

            if (_onPit.Remove(id))
            {
                // Off pit road: into the queue wherever it is on the road.
                _caught.Remove(id);
                Insert(id, raceDistance);
                if (speedMps < StopMps) Drop(id);
                return;
            }

            if (_dropped.Contains(id))
            {
                if (speedMps < RejoinMps) return;
                _dropped.Remove(id);
                _queue.Add(id);   // back up to speed: the back of the queue
                return;
            }

            bool queued = _queue.Contains(id);
            if (speedMps < StopMps) { Drop(id); return; }
            if (!queued && speedMps >= RejoinMps) _queue.Add(id);   // a car the flag never saw, up to speed: the back
        }

        // In front of the first car in the queue it is ahead of on the road (race distance), else at the back.
        void Insert(int id, double raceDistance)
        {
            _queue.Remove(id);
            for (int i = 0; i < _queue.Count; i++)
            {
                if (!_distance.TryGetValue(_queue[i], out double d)) continue;
                if (raceDistance > d) { _queue.Insert(i, id); return; }
            }
            _queue.Add(id);
        }

        // Sort key, lowest first: the place in the queue; then cars on pit road, cars never seen, stopped cars - each of
        // those groups in the order the flag found them.
        public long Key(int id)
        {
            int at = _queue.IndexOf(id);
            if (at >= 0) return at;
            bool known = _snapshot.TryGetValue(id, out int snap);
            if (!known) snap = 9999;
            if (_dropped.Contains(id)) return 2L * 100_000_000L + snap;
            if (_onPit.Contains(id)) return 1L * 100_000_000L + 50_000_000L + snap;
            return 1L * 100_000_000L + 60_000_000L + snap;
        }

        // Whether `id` ranks behind `other` in the restart order.
        public bool Behind(int id, int other) => Key(id) > Key(other);
    }

    // Two-wide for the restart. Under a caution the field runs single file in restart order (CautionOrder); once pit road
    // has shut for good ("green next time by") and every car has left it, the order is set for the restart, and each car
    // is given a place in a two-wide line-up: slot 0 the leader, slots 2k and 2k+1 a row, even slots one column and odd
    // the other - as grid slots are on the formation lap (FormationLanes.Column). The slots are latched, not re-counted
    // from the queue every step: a car dropping out of the queue mid-field would otherwise move every car behind it
    // into the other column at once. A car that leaves the queue afterwards (stops on the track) loses its slot and its
    // row partner runs on alone; a car that joins it afterwards (back up to speed, or off pit road) takes the next slot
    // at the tail.
    public sealed class RestartSlots
    {
        readonly Dictionary<int, int> _slot = new();
        readonly HashSet<int> _inQueue = new();
        readonly List<int> _gone = new();
        int _next;

        // The line-up has been set: the field is lining up two-wide.
        public bool Latched { get; private set; }

        // When to double up: pit road has shut for the rest of the caution and nobody is still on it (a car coming off it
        // takes a place by where it is on the road, so the order is not settled until the last one is out) - or, whatever
        // else, once the field is closing up for the green.
        public static bool ShouldLatch(bool greenSignalled, int carsOnPitRoad, bool closingUp) =>
            closingUp || (greenSignalled && carsOnPitRoad <= 0);

        public void Clear()
        {
            _slot.Clear();
            _next = 0;
            Latched = false;
        }

        // Set the line-up from the restart queue (leader first).
        public void Latch(IReadOnlyList<int> queue)
        {
            Clear();
            Latched = true;
            if (queue == null) return;
            for (int i = 0; i < queue.Count; i++)
                if (!_slot.ContainsKey(queue[i])) _slot[queue[i]] = _next++;
        }

        // Once latched, keep the line-up in step with the queue: a car no longer in it gives up its slot, a car new to it
        // takes the next one at the tail (in queue order).
        public void Update(IReadOnlyList<int> queue)
        {
            if (!Latched) return;
            _inQueue.Clear();
            if (queue != null) for (int i = 0; i < queue.Count; i++) _inQueue.Add(queue[i]);
            _gone.Clear();
            foreach (var kv in _slot) if (!_inQueue.Contains(kv.Key)) _gone.Add(kv.Key);
            for (int i = 0; i < _gone.Count; i++) _slot.Remove(_gone[i]);
            if (queue == null) return;
            for (int i = 0; i < queue.Count; i++)
                if (!_slot.ContainsKey(queue[i])) _slot[queue[i]] = _next++;
        }

        // This car's slot in the two-wide line-up; -1 before the line-up is set, or for a car without one.
        public int SlotOf(int id) => Latched && _slot.TryGetValue(id, out int s) ? s : -1;
    }
}
