using System;
using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // Finds how strong the AI has to be to lap at the player's pace.
    //
    // "Strength" is one number, k, that the calibrator multiplies into the AI's grip and power together — the
    // two things that decide how fast a car goes through a corner and down a straight. A lap is driven at some
    // k, its time comes back, and the search picks the next k. Lap time falls as k rises, so this is a root
    // find on lapTime(k) - target:
    //
    //   1. Bracket: start at 1 (the AI as it is today) and step by 20% until the target sits between a lap that
    //      was too slow and one that was fast enough.
    //   2. Close in: regula falsi between those two (it lands in two or three laps on a curve this smooth). If
    //      the same end moves twice running, the next guess halves the bracket instead (Illinois), so a bent
    //      curve can't stall it.
    //
    // A lap the car didn't finish cleanly — it left the road or spun — counts as TOO FAST, whatever its time. A
    // car can post a quick lap by leaning on grip it can't hold every lap, and a difficulty that only matches
    // the player by crashing is not the difficulty wanted. So the answer is always a CLEAN lap.
    //
    // Pure and deterministic: the editor window drives it with the headless lap sim, the tests with a formula.
    public sealed class AIPaceSearch
    {
        public readonly struct Trial
        {
            public readonly float k, lapSeconds;
            public readonly bool clean;
            public Trial(float k, float lapSeconds, bool clean) { this.k = k; this.lapSeconds = lapSeconds; this.clean = clean; }
        }

        public float TargetSeconds { get; }
        public float ToleranceSeconds { get; }
        public float MinK { get; }
        public float MaxK { get; }
        public int MaxTrials { get; }

        readonly List<Trial> _trials = new List<Trial>();
        public IReadOnlyList<Trial> Trials => _trials;

        // The bracket: the strongest k still slower than the target, and the weakest k that was at/under it or
        // not clean. NaN until a trial lands on that side.
        float _slowK = float.NaN, _slowLap;
        float _fastK = float.NaN, _fastLap;
        bool _fastIsClean;
        int _lastSide, _sameSideRuns;   // +1 the fast end moved, -1 the slow end

        public AIPaceSearch(float targetSeconds, float toleranceSeconds = 0.2f, float minK = 0.5f, float maxK = 2.5f,
                            int maxTrials = 14)
        {
            if (targetSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(targetSeconds));
            TargetSeconds = targetSeconds;
            ToleranceSeconds = Math.Max(0.01f, toleranceSeconds);
            MinK = minK;
            MaxK = maxK;
            MaxTrials = Math.Max(1, maxTrials);
        }

        // Finished: matched within tolerance, ran out of trials, or hit a limit it can't get past.
        public bool Done { get; private set; }

        // Why it stopped, in words for the window.
        public string Outcome { get; private set; } = "";

        // Whether the answer actually matches the target (within tolerance), rather than being the closest the
        // search could get.
        public bool Matched { get; private set; }

        // The answer: the clean trial whose lap is closest to the target. k is NaN if no clean lap was driven.
        public Trial Best
        {
            get
            {
                Trial best = new Trial(float.NaN, float.NaN, false);
                float bestErr = float.PositiveInfinity;
                foreach (var t in _trials)
                {
                    if (!t.clean) continue;
                    float err = Math.Abs(t.lapSeconds - TargetSeconds);
                    if (err < bestErr) { bestErr = err; best = t; }
                }
                return best;
            }
        }

        // The k to drive next. Only meaningful while !Done.
        public float Next()
        {
            if (_trials.Count == 0) return Clamp(1f);
            if (float.IsNaN(_fastK)) return Clamp(_slowK * 1.2f);   // everything so far too slow: step up
            if (float.IsNaN(_slowK)) return Clamp(_fastK / 1.2f);   // everything so far too fast: step down

            // A dirty fast end has no time worth interpolating, and a stalled end wants a halving.
            if (!_fastIsClean || _sameSideRuns >= 2) return (_slowK + _fastK) * 0.5f;

            float slowErr = _slowLap - TargetSeconds;   // > 0: too slow
            float fastErr = _fastLap - TargetSeconds;   // <= 0
            float k = _slowK + (_fastK - _slowK) * slowErr / (slowErr - fastErr);
            // Strictly inside the bracket, so a flat stretch can't send it back to an end it already drove.
            float margin = (_fastK - _slowK) * 0.05f;
            return Math.Max(_slowK + margin, Math.Min(_fastK - margin, k));
        }

        public void Report(float k, float lapSeconds, bool clean)
        {
            if (Done) return;
            _trials.Add(new Trial(k, lapSeconds, clean));

            if (clean && Math.Abs(lapSeconds - TargetSeconds) <= ToleranceSeconds)
            {
                Done = Matched = true;
                Outcome = $"Matched: the AI lapped {lapSeconds:0.00}s against your {TargetSeconds:0.00}s.";
                return;
            }

            bool fast = !clean || lapSeconds <= TargetSeconds;
            int side = fast ? 1 : -1;
            _sameSideRuns = side == _lastSide ? _sameSideRuns + 1 : 1;
            _lastSide = side;
            if (fast)
            {
                if (float.IsNaN(_fastK) || k < _fastK) { _fastK = k; _fastLap = lapSeconds; _fastIsClean = clean; }
            }
            else if (float.IsNaN(_slowK) || k > _slowK) { _slowK = k; _slowLap = lapSeconds; }

            if (float.IsNaN(_fastK) && k >= MaxK)
                Stop($"Even at the strongest setting allowed (x{MaxK:0.00}) the AI laps {lapSeconds:0.00}s, slower " +
                     "than your lap. Grip and power alone can't close this gap.");
            else if (float.IsNaN(_slowK) && k <= MinK)
                Stop($"Even at the weakest setting allowed (x{MinK:0.00}) the AI is at or under your lap.");
            else if (!float.IsNaN(_slowK) && !float.IsNaN(_fastK) && _fastK - _slowK < 0.002f)
                Stop(_fastIsClean
                    ? $"Converged; closest clean lap {Best.lapSeconds:0.00}s."
                    : $"The AI leaves the road before it gets down to {TargetSeconds:0.00}s. Its fastest clean lap is " +
                      $"{Best.lapSeconds:0.00}s — as quick as it can drive this track without crashing.");
            else if (_trials.Count >= MaxTrials)
                Stop($"Stopped after {MaxTrials} laps; closest clean lap {Best.lapSeconds:0.00}s.");
        }

        void Stop(string why)
        {
            Done = true;
            Outcome = why;
        }

        float Clamp(float k) => Math.Max(MinK, Math.Min(MaxK, k));
    }
}
