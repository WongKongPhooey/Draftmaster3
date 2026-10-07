using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sim;
using UnityEngine;

[RequireComponent(typeof(SplineDriver))]
public class AIDriverBinding : MonoBehaviour
{
    public Driver driver;
    public VehicleInfo vehicleInfo;

    // Share of the grip limit the lowest-rated driver corners at (the best take all of it).
    public const float MinCornerCommitment = 0.93f;

    SplineDriver _spline;

    // This event's rolls, per driver id (NR2003: the band is rolled once when the event loads and kept for it).
    // Practice, qualifying and the race at one track all re-spawn the field, and must not re-roll it — a driver
    // who is off the pace on Friday is off it on Sunday. Cleared when the track changes.
    static string _eventTrackId;
    static readonly Dictionary<int, Vector2> _eventRolls = new();

    public static void ResetEventRolls() { _eventTrackId = null; _eventRolls.Clear(); }

    static Vector2 EventRolls(int driverId, string trackId)
    {
        if (_eventTrackId != trackId) { _eventTrackId = trackId; _eventRolls.Clear(); }
        if (!_eventRolls.TryGetValue(driverId, out var rolls))
        {
            rolls = new Vector2(Random.value, Random.value);
            _eventRolls[driverId] = rolls;
        }
        return rolls;
    }

    // The driver's 0-20 aptitude for the kind of track being raced.
    public static int TrackAptitude(Driver d, TrackType type)
    {
        switch (type)
        {
            case TrackType.ShortTrack:    return d.ShortTracks;
            case TrackType.Superspeedway: return d.Superspeedways;
            case TrackType.RoadCourse:    return d.RoadCourses;
            case TrackType.DirtCourse:    return d.DirtCourses;
            default:                      return d.Speedways;
        }
    }

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
    }

    public void Apply()
    {
        if (_spline == null) return;

        if (vehicleInfo != null) _spline.vehicleInfo = vehicleInfo;

        var racing = GetComponent<AIRacingBehaviour>();
        if (racing == null) racing = gameObject.AddComponent<AIRacingBehaviour>();

        if (GetComponent<TireState>() == null) gameObject.AddComponent<TireState>();

        if (driver != null)
        {
            // NR2003-style event ratings: strength blends raw speed with this track type's aptitude and is rolled
            // inside a band whose width is the driver's consistency; aggression gets a small roll of its own.
            string trackId = TrackSelection.CurrentId;
            var rolls = EventRolls(driver.Id, trackId);
            var ratings = AIRatings.ForEvent(driver.Qualifying, TrackAptitude(driver, TrackSelection.CurrentType),
                                             driver.Consistency, driver.Aggression, Driver.StatMax, rolls.x, rolls.y);

            float aggression01 = ratings.aggression01;
            // Everyone runs the ideal line; aggression only nudges them fractionally off it (-0.05 = a touch
            // inside, +0.08 = a touch outside). Kept tight so the field visibly follows the ideal line.
            _spline.lineFactor = Mathf.Lerp(-0.05f, 0.08f, aggression01);
            racing.aggression01 = aggression01;

            float strength01 = ratings.strength01;
            float consistency01 = ratings.consistency01;
            racing.consistency01 = consistency01;

            float pace = Mathf.Lerp(0.93f, 1.04f, strength01);
            float jitter = Random.Range(1f - (1f - consistency01) * 0.04f, 1f);
            float basePace = pace * jitter;
            _spline.paceMultiplier = basePace;
            racing.SetBasePace(basePace);

            // How close to the grip limit they corner. Pace can't do this: it is capped at the limit, and every
            // car sits above it, so without it the whole field cornered identically, strung out ~5 lengths
            // apart and never passed. The best drivers use all of it; the weakest rated give up ~4%, a second
            // or two a lap at Watkins Glen. Consistency adds a little spread on top so equal-rated drivers differ.
            float commitJitter = Random.Range(-(1f - consistency01) * 0.012f, (1f - consistency01) * 0.004f);
            _spline.cornerCommitment = Mathf.Clamp(Mathf.Lerp(MinCornerCommitment, 1f, strength01) + commitJitter,
                                                   MinCornerCommitment - 0.01f, 1f);

            gameObject.name = $"AI_{driver.LastName}_{driver.Id}";
        }

        _spline.Rebuild();
    }
}
