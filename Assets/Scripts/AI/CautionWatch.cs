using UnityEngine;

// Watches the track just ahead of the player for a car that has stopped — a spin that never got going
// again, a wreck, anything parked on the racing surface — and raises a caution flag for the HUD.
//
// The field is read off RacePositionTracker rather than RaceField, for the same reason the tracker
// itself does it: on a client the AI are network puppets with their brains disabled, so they are not in
// RaceField, but the tracker still has them with a live progress and speed. Cars in the pit lane are
// skipped — a car sitting on its jacks is stationary and is not a hazard on the track.
//
// Distance is measured along the centerline, not as the crow flies: a car stopped on the far side of a
// short oval is metres away in world space and most of a lap away on the road.
public class CautionWatch : MonoBehaviour
{
    public static CautionWatch Instance { get; private set; }

    [Tooltip("Look this far (m) up the road from the player for a stopped car.")]
    public float lookAheadMetres = 100f;
    [Tooltip("At or below this speed (mph) a car counts as stopped.")]
    public float stoppedMph = 8f;
    [Tooltip("A car must be that slow for this long (s) before it raises a flag — a car being passed at the exit of a slow hairpin is not an incident.")]
    public float stoppedForSeconds = 0.75f;
    [Tooltip("Once raised, hold the flag at least this long (s), so it doesn't strobe as the player drives past the stopped car.")]
    public float holdSeconds = 1.5f;

    // True while there is a stopped car within lookAheadMetres up the road.
    public bool CautionAhead { get; private set; }
    // Distance (m) along the track to the nearest stopped car ahead; 0 when nothing is flagged.
    public float DistanceToIncident { get; private set; }

    readonly System.Collections.Generic.Dictionary<Transform, float> _slowSince = new();
    float _holdUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("CautionWatch");
        DontDestroyOnLoad(go);
        go.AddComponent<CautionWatch>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        var tracker = RacePositionTracker.Instance;
        float len = tracker != null ? tracker.TrackLength : 0f;
        if (tracker == null || len <= 0f) { Clear(); return; }

        RacePositionTracker.Entry me = null;
        var order = tracker.Order;
        for (int i = 0; i < order.Count; i++) if (order[i] != null && order[i].isPlayer) { me = order[i]; break; }
        if (me == null || me.tf == null) { Clear(); return; }

        float stoppedMps = stoppedMph / 2.237f;
        float myDist = Mathf.Repeat(me.progress, len);
        float nearest = float.MaxValue;
        float now = Time.time;

        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null || e == me) continue;

            if (e.speedMps > stoppedMps) { _slowSince.Remove(e.tf); continue; }
            if (InPits(tracker, e)) { _slowSince.Remove(e.tf); continue; }

            if (!_slowSince.TryGetValue(e.tf, out float since)) { _slowSince[e.tf] = now; since = now; }
            if (now - since < stoppedForSeconds) continue;

            // Gap up the road, wrapped — a car just over the start/finish line is ahead of a player who
            // has not reached it yet, not a lap away.
            float gap = Mathf.Repeat(Mathf.Repeat(e.progress, len) - myDist, len);
            if (gap > 0f && gap <= lookAheadMetres && gap < nearest) nearest = gap;
        }

        PruneDead();

        if (nearest < float.MaxValue)
        {
            CautionAhead = true;
            DistanceToIncident = nearest;
            _holdUntil = now + holdSeconds;
        }
        else if (now >= _holdUntil)
        {
            CautionAhead = false;
            DistanceToIncident = 0f;
        }
    }

    static bool InPits(RacePositionTracker tracker, RacePositionTracker.Entry e)
    {
        if (e.spline != null && e.spline.enabled) return e.spline.IsOnPit;
        return tracker.track != null && tracker.track.IsOnPitSurface(e.tf.position);
    }

    void Clear()
    {
        CautionAhead = false;
        DistanceToIncident = 0f;
        if (_slowSince.Count > 0) _slowSince.Clear();
    }

    // Destroyed transforms compare == null but still hash as keys.
    void PruneDead()
    {
        if (_slowSince.Count == 0) return;
        System.Collections.Generic.List<Transform> dead = null;
        foreach (var kv in _slowSince)
            if (kv.Key == null) (dead ??= new System.Collections.Generic.List<Transform>()).Add(kv.Key);
        if (dead == null) return;
        for (int i = 0; i < dead.Count; i++) _slowSince.Remove(dead[i]);
    }
}
