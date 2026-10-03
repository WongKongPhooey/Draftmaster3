using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // The rules of a race watched from the stand.
    //
    // Sitting down for another championship's race starts one: the whole field on a grid behind the line, the
    // green as the screen comes up, and the chequered flag about five minutes later. This is the scoring half —
    // who has done how many laps, who is leading, when the flag is out and who has taken it — kept pure so it
    // is testable without a scene. GrandstandRaceDirector feeds it the distance each car has driven.
    //
    // Distances are measured from the start/finish line: a car on the grid starts at a negative progress (how
    // far behind the line it is parked), crossing the line for the first time is the start rather than a lap,
    // and a lap is completed every time progress passes another whole lap length.
    //
    // The finish is the real one. When the race clock runs out the flag is shown to the leader, who takes it
    // the next time they cross the line; everybody after that finishes the next time THEY cross it, however
    // many laps down they are. The order is laps completed, then the order they took the flag in, then — for
    // anybody still out there — how far round they are.
    public sealed class GrandstandRace
    {
        // Green to chequered flag. "About five minutes": the flag comes out at five, and the leader still has
        // to finish the lap they are on.
        public const float RaceSeconds = 300f;

        // How long after the winner the rest of the field is given to take the flag before the race is called.
        // A car stuck somewhere must not keep the result open for ever.
        public const float FinishGraceSeconds = 60f;

        readonly float _lap;
        readonly float _raceSeconds;
        readonly float[] _progress;
        readonly int[] _laps;
        readonly float[] _lastCross;    // race time this car last crossed the line on a completed lap
        readonly bool[] _finished;
        readonly float[] _finishTime;
        readonly List<float> _firstCross = new();   // [k] = race time the leader completed lap k + 1

        float _elapsed;
        int _flagLap = -1;              // lap count the leader takes the flag on; -1 = flag not out yet
        float _winnerAt = -1f;
        int _finishedCount;

        public GrandstandRace(float lapLength, IList<float> startProgress, float raceSeconds = RaceSeconds)
        {
            _lap = Mathf.Max(1f, lapLength);
            _raceSeconds = Mathf.Max(0f, raceSeconds);
            int n = startProgress != null ? startProgress.Count : 0;
            _progress = new float[n];
            _laps = new int[n];
            _lastCross = new float[n];
            _finished = new bool[n];
            _finishTime = new float[n];
            for (int i = 0; i < n; i++)
            {
                _progress[i] = startProgress[i];
                _laps[i] = LapsAt(_progress[i]);
                _lastCross[i] = -1f;
            }
        }

        public int Count => _progress.Length;
        public float LapLength => _lap;
        public float Elapsed => _elapsed;
        public float RaceLengthSeconds => _raceSeconds;
        public float SecondsLeft => Mathf.Max(0f, _raceSeconds - _elapsed);

        // The flag has been shown: the leader is on the last lap.
        public bool FlagOut => _flagLap >= 0;

        // Somebody has won it.
        public bool WinnerIn => _winnerAt >= 0f;

        // Everybody has taken the flag, or the grace for the stragglers has run out.
        public bool Over => Count == 0
                         || _finishedCount >= Count
                         || (WinnerIn && _elapsed - _winnerAt >= FinishGraceSeconds);

        public bool Finished(int car) => _finished[car];
        public int Laps(int car) => _laps[car];
        public float Progress(int car) => _progress[car];

        // The most laps anybody has completed — the leader's lap count.
        public int LeaderLaps
        {
            get
            {
                int best = 0;
                for (int i = 0; i < _laps.Length; i++) best = Mathf.Max(best, _laps[i]);
                return best;
            }
        }

        // The lap the leader is on, counting the first as lap 1.
        public int CurrentLap => LeaderLaps + 1;

        // Race clock. Shows the flag the moment it runs out.
        public void Tick(float dt)
        {
            if (dt > 0f) _elapsed += dt;
            if (_flagLap < 0 && _elapsed >= _raceSeconds) _flagLap = LeaderLaps + 1;
        }

        // A car drove `metres` further round. Negative is allowed (a car nudged backwards) and never un-counts
        // a lap: only crossings going forward are scored.
        public void Advance(int car, float metres)
        {
            if (car < 0 || car >= Count) return;
            _progress[car] += metres;

            int now = LapsAt(_progress[car]);
            while (_laps[car] < now)
            {
                _laps[car]++;
                Crossed(car, _laps[car]);
            }
        }

        void Crossed(int car, int lap)
        {
            _lastCross[car] = _elapsed;
            if (_firstCross.Count < lap) _firstCross.Add(_elapsed);

            if (_finished[car] || _flagLap < 0) return;

            // The leader takes it on the flag lap; once somebody has, the next crossing is everybody's last.
            if (WinnerIn || lap >= _flagLap)
            {
                _finished[car] = true;
                _finishTime[car] = _elapsed;
                _finishedCount++;
                if (!WinnerIn) _winnerAt = _elapsed;
            }
        }

        int LapsAt(float progress) => progress <= 0f ? 0 : Mathf.FloorToInt(progress / _lap);

        // Running order (or the result, once it is in): car indices, leader first.
        public void Classify(List<int> into)
        {
            into.Clear();
            for (int i = 0; i < Count; i++) into.Add(i);
            into.Sort(Compare);
        }

        int Compare(int a, int b)
        {
            // Laps first: a car a lap down is behind, however it got there.
            int byLaps = ScoredLaps(b).CompareTo(ScoredLaps(a));
            if (byLaps != 0) return byLaps;

            // Both have taken the flag on the same lap count: whoever took it first.
            if (_finished[a] && _finished[b])
            {
                int byTime = _finishTime[a].CompareTo(_finishTime[b]);
                return byTime != 0 ? byTime : a.CompareTo(b);
            }

            // Otherwise by where they are on the road. A finished car is scored at the line it crossed.
            int byDist = ScoredProgress(b).CompareTo(ScoredProgress(a));
            return byDist != 0 ? byDist : a.CompareTo(b);
        }

        // A car that has taken the flag is frozen where it took it; the cool-down lap is not racing.
        int ScoredLaps(int car) => _laps[car];
        float ScoredProgress(int car) => _finished[car] ? _laps[car] * _lap : _progress[car];

        // How far behind `leader` this car is: whole laps down, or else seconds at the line. Seconds are the
        // difference between the two cars crossing the last line they have both crossed, which is what a
        // timing screen shows; -1 seconds = nothing to measure yet (the opening lap).
        public void GapTo(int leader, int car, out int lapsDown, out float seconds)
        {
            seconds = -1f;
            lapsDown = 0;
            if (car == leader) return;

            float behind = ScoredProgress(leader) - ScoredProgress(car);
            lapsDown = _finished[leader] && _finished[car]
                ? Mathf.Max(0, _laps[leader] - _laps[car])
                : Mathf.Max(0, Mathf.FloorToInt(behind / _lap));
            if (lapsDown > 0) return;

            int k = _laps[car];
            if (k >= 1 && k <= _firstCross.Count && _lastCross[car] >= 0f)
                seconds = Mathf.Max(0f, _lastCross[car] - _firstCross[k - 1]);
        }

        // The gap as the timing screen prints it.
        public string GapText(int leader, int car)
        {
            if (car == leader) return _finished[car] ? "WINNER" : "LAP " + (_laps[car] + 1);
            GapTo(leader, car, out int down, out float s);
            if (down > 0) return "+" + down + (down == 1 ? " LAP" : " LAPS");
            return s < 0f ? "-" : "+" + s.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Where each car starts: two by two behind the line, `firstRow` metres back for the pole sitter and
        // `spacing` metres between one car and the next (the rows alternate sides, so a car is 2x spacing
        // behind the one directly in front of it).
        public static float GridProgress(int slot, float firstRow, float spacing) =>
            -(Mathf.Abs(firstRow) + Mathf.Max(0, slot) * Mathf.Max(0f, spacing));
    }
}
