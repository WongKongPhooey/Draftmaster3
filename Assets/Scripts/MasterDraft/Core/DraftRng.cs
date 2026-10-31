using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // The one source of randomness for Master Draft rules. A small splitmix64 generator rather than
    // System.Random so the sequence for a seed is fixed by this file alone — a pack opened on a phone, in the
    // editor and (later) in a Cloud Code script written to the same algorithm comes out the same.
    public sealed class DraftRng
    {
        ulong _state;

        public DraftRng(long seed) { _state = unchecked((ulong)seed) ^ 0x9E3779B97F4A7C15UL; }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // [0, max). max <= 0 returns 0.
        public int Next(int max)
        {
            if (max <= 1) return 0;
            return (int)(NextULong() % (ulong)max);
        }

        // [min, max).
        public int Range(int min, int max) => max <= min ? min : min + Next(max - min);

        // [0, 1).
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        // Index into weights by weight. All-zero weights pick index 0.
        public int Weighted(IReadOnlyList<int> weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return 0;
            int roll = Next(total);
            for (int i = 0; i < weights.Count; i++)
            {
                int w = Math.Max(0, weights[i]);
                if (roll < w) return i;
                roll -= w;
            }
            return weights.Count - 1;
        }

        public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // A stable seed from a string (a card name, a profile id) — FNV-1a, not string.GetHashCode, which
        // .NET randomises per process.
        public static long Hash(string text)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                if (text != null)
                    foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
                return (long)h;
            }
        }
    }
}
