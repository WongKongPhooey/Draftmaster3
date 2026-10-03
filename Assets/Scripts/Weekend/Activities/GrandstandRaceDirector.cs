using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The race in front of the grandstand.
//
// Sitting down to watch another championship's race starts one (GridSpawner lines the whole field up on the
// grid and puts this in charge of it): green as soon as the player is in the seat, chequered flag about five
// minutes later. This is the runtime half — it reads how far every car has driven off its spline each frame
// and hands that to GrandstandRace, which owns the laps, the flag and the order. GrandstandVisit reads the
// state for its prompt and its clock; the timing screen reads the running order.
//
// It lives on its own object rather than under the field, because the result outlasts the cars: when the
// last of them has taken the flag the stand hands the circuit back and the field comes in, and the timing
// screen still has the result to show. The visit ends it when the player gets up.
public class GrandstandRaceDirector : MonoBehaviour
{
    public static GrandstandRaceDirector Current { get; private set; }

    GrandstandRace _race;
    readonly List<SplineDriver> _cars = new();
    readonly List<string> _names = new();
    readonly List<float> _prev = new();
    readonly List<bool> _hasPrev = new();
    readonly List<int> _order = new();
    float _born;

    public GrandstandRace Race => _race;
    public IReadOnlyList<int> Order => _order;
    public string NameOf(int car) => car >= 0 && car < _names.Count ? _names[car] : "";

    // Who won it, once somebody has.
    public string Winner => _race != null && _race.WinnerIn && _order.Count > 0 ? NameOf(_order[0]) : "";

    // A new race, replacing whatever the last one was. `startProgress[i]` is where car i sits relative to
    // the start/finish line (negative = behind it on the grid).
    public static GrandstandRaceDirector Begin(IList<SplineDriver> cars, IList<string> names,
                                               IList<float> startProgress, float lapLength)
    {
        EndCurrent();

        var go = new GameObject("GrandstandRace");
        var d = go.AddComponent<GrandstandRaceDirector>();
        d._born = Time.unscaledTime;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            d._cars.Add(car);
            d._names.Add(names != null && i < names.Count ? names[i] : "");
            bool on = car != null && car.TrackLength > 0f;
            d._prev.Add(on ? car.DistanceOnTrack : 0f);
            d._hasPrev.Add(on);
        }
        d._race = new GrandstandRace(lapLength, startProgress);
        d._race.Classify(d._order);
        Current = d;
        return d;
    }

    public static void EndCurrent()
    {
        if (Current == null) return;
        var old = Current;
        Current = null;
        Destroy(old.gameObject);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        // Nobody in the stand any more (the visit closed before the field even got out): there is no one to
        // run this for.
        if (!GrandstandVisit.Watching && Time.unscaledTime - _born > 2f) { EndCurrent(); return; }
        if (_race == null) return;

        // Scaled time, like the cars: a pause menu stops the race clock and the field together.
        float dt = Time.deltaTime;
        _race.Tick(dt);

        for (int i = 0; i < _cars.Count; i++)
        {
            var car = _cars[i];
            if (car == null || !car.isActiveAndEnabled) continue;   // cleared once the race is in

            float len = car.TrackLength;
            // Off the main lap (a car shoved into the pit lane): its distance is not on the circuit, so take
            // it up again from wherever it rejoins rather than scoring the jump.
            if (len <= 0f || car.IsOnPit) { _hasPrev[i] = false; continue; }

            float now = car.DistanceOnTrack;
            if (_hasPrev[i])
            {
                float moved = now - _prev[i];
                if (moved < -len * 0.5f) moved += len;      // across the end of the spline going forward
                else if (moved > len * 0.5f) moved -= len;  // ... or backward
                _race.Advance(i, moved);
            }
            _prev[i] = now;
            _hasPrev[i] = true;
        }

        _race.Classify(_order);
    }

    // The line under the session name on the timing screen.
    public string StatusLine()
    {
        if (_race == null) return "";
        if (_race.Over) return "RESULT";
        if (_race.WinnerIn) return "CHEQUERED FLAG";
        if (_race.FlagOut) return "FINAL LAP";
        int left = Mathf.CeilToInt(_race.SecondsLeft);
        return $"LAP {_race.CurrentLap} · {left / 60}:{left % 60:00}";
    }
}
