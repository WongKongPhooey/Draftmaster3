namespace Draftmaster.Weekend
{
    // A tiny deterministic PRNG. The timetable, the press questions and the simulated results for the other
    // two series all have to come out the same every time they are rebuilt, because they ARE rebuilt: the
    // weekend reloads the race scene between practice, qualifying and the race, and the schedule screen is
    // reconstructed from the weekend id each time it opens. UnityEngine.Random is a shared global sequence
    // and would hand back a different Friday every reload.
    //
    // xorshift32 — fast, no allocation, and good enough for picking a reporter and jittering a lap time.
    public struct WeekendRandom
    {
        uint _state;

        public WeekendRandom(int seed)
        {
            unchecked { _state = (uint)seed * 2654435761u + 0x9E3779B9u; }
            if (_state == 0) _state = 0x1234567u;
        }

        // Mix several ints into one seed so callers can key a stream to (weekend, series, session) without
        // colliding with (weekend, series, other session).
        public static WeekendRandom For(int a, int b = 0, int c = 0, int d = 0)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a; h = h * 31 + b; h = h * 31 + c; h = h * 31 + d;
                return new WeekendRandom(h);
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        // 0..1 exclusive of 1.
        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        // [min, max) for ints, [min, max] behaviour matching UnityEngine.Random.Range(int,int) exclusivity.
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public float Range(float min, float max) => min + (max - min) * Value;

        public bool Chance(float probability01) => Value < probability01;

        public T Pick<T>(T[] items)
        {
            if (items == null || items.Length == 0) return default;
            return items[Range(0, items.Length)];
        }

        // Fisher-Yates, in place.
        public void Shuffle<T>(System.Collections.Generic.IList<T> items)
        {
            if (items == null) return;
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
