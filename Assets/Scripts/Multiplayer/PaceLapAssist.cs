using System.Collections.Generic;
using UnityEngine;

// Owner-only pace-lap helper for the LOCAL player car. During the formation lap the player drives freely but is
// speed-capped so they can't overtake the car directly ahead, and an on-screen prompt tells them which car to
// line up behind — warning them back into position if they drop out of place.
//
// Works on every peer using WORLD-SPACE car positions: the AI brains run only on the host, so on a client the
// other cars are NetworkTransform puppets with no spline/RaceField entry. We therefore detect "the car ahead"
// from transforms (a forward cone) rather than the spline, and estimate each car's speed from its motion.
// Added at runtime by NetworkedCarBindings for the owning client; it governs ONLY during the Formation phase
// (NetworkedCarBindings owns the PreGrid hold and the Green release).
public class PaceLapAssist : MonoBehaviour
{
    [HideInInspector] public PlayerVehicleController pvc;

    [Header("Car-ahead detection")]
    [Tooltip("Forward cone half-angle (deg) within which a car counts as 'ahead' of us.")]
    public float coneHalfAngleDeg = 55f;
    [Tooltip("Range (m) ahead scanned for the car to hold behind.")]
    public float scanRange = 70f;
    [Tooltip("Lateral half-width (m) — a car further off our line than this isn't treated as the one ahead.")]
    public float laneHalfWidth = 7f;

    [Header("No-overtake")]
    [Tooltip("Gap (m) at/under which we cap to the car ahead's speed so we can't overtake.")]
    public float holdGap = 13f;
    [Tooltip("Gap (m) at/under which we back off BELOW the car ahead's speed so we don't tap it.")]
    public float hardGap = 6f;
    [Tooltip("If our locked leader is more than this far ahead (m), prompt the player to close up.")]
    public float catchUpGap = 30f;

    const float MphToMps = 1f / 2.237f;

    Transform _leader;          // the car we locked onto to line up behind (sticky once acquired)
    int _leaderNumber = -1;
    bool _leaderIsSafety;
    SafetyCar _safetyCar;

    readonly Dictionary<int, Vector2> _prevPos = new();
    readonly Dictionary<int, float> _speedMps = new();

    string _promptText;
    Color _promptColor;
    bool _showPrompt;

    void OnDisable()
    {
        // Don't leave the car governed if this component is torn down mid-formation.
        if (pvc != null && !RaceStart.IsGreen) pvc.speedGovernorMps = Mathf.Infinity;
        _showPrompt = false;
    }

    void Update()
    {
        if (pvc == null) return;

        if (RaceStart.Current != RaceStart.Phase.Formation)
        {
            // Not the formation lap — drop our hold + prompt. NetworkedCarBindings owns PreGrid/Green governing.
            _leader = null; _showPrompt = false;
            _prevPos.Clear(); _speedMps.Clear();
            // Defensive: once green, make sure our last no-overtake cap is lifted so the player is never stranded
            // capped if nothing else releases the governor.
            if (RaceStart.IsGreen) pvc.speedGovernorMps = Mathf.Infinity;
            return;
        }

        // Single-player / host: every car (incl. the safety car) is a spline car in RaceField with a real
        // along-track distance. Pick the leader by TRACK ORDER — the car immediately ahead — instead of a
        // world-space cone that can lock onto a car that's spatially ahead round a bend but behind in the order.
        if (RaceField.Drivers.Count > 0 && pvc.ObstacleTrack != null)
        {
            UpdateSpline();
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector2 myPos = transform.position;
        float hd = pvc.HeadingDeg * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(hd), Mathf.Sin(hd));
        Vector2 right = new Vector2(fwd.y, -fwd.x);
        float cosCone = Mathf.Cos(coneHalfAngleDeg * Mathf.Deg2Rad);

        Transform nearestAhead = null;
        float nearestFwd = float.MaxValue;

        // Update a car's tracked speed (from motion) and, if it sits ahead in our cone+lane+range, contend to be
        // the nearest car ahead.
        void Consider(Transform t)
        {
            if (t == null || t == transform) return;
            int id = t.GetInstanceID();
            Vector2 cur = t.position;
            float spd = _prevPos.TryGetValue(id, out var prev) ? Vector2.Distance(prev, cur) / dt : 0f;
            _prevPos[id] = cur;
            _speedMps[id] = Mathf.Lerp(_speedMps.TryGetValue(id, out var ps) ? ps : spd, spd, 0.3f);

            Vector2 to = cur - myPos;
            float fwdDist = Vector2.Dot(to, fwd);
            if (fwdDist <= 0.5f || fwdDist > scanRange) return;
            if (Mathf.Abs(Vector2.Dot(to, right)) > laneHalfWidth) return;
            if (to.sqrMagnitude > 1e-4f && Vector2.Dot(to.normalized, fwd) < cosCone) return;
            if (fwdDist < nearestFwd) { nearestFwd = fwdDist; nearestAhead = t; }
        }

        // Multiplayer: the other cars are NetworkTransform puppets (no spline/RaceField entry on a client).
        var ai = NetworkedAICar.All;
        for (int i = 0; i < ai.Count; i++) if (ai[i] != null) Consider(ai[i].transform);
        var players = NetworkedCarBindings.Players;
        for (int i = 0; i < players.Count; i++) if (players[i] != null) Consider(players[i].transform);
        // Single-player (and the host): the AI field are spline cars in RaceField. These lists are empty in the
        // other mode, so scanning both is safe and lets this one helper drive the pace lap in either mode.
        var field = RaceField.Drivers;
        for (int i = 0; i < field.Count; i++) if (field[i] != null) Consider(field[i].transform);
        if (_safetyCar == null) _safetyCar = FindFirstObjectByType<SafetyCar>();
        if (_safetyCar != null) Consider(_safetyCar.transform);

        // No-overtake speed cap: match (or back off under) the car ahead once we're close; otherwise free.
        float gov = Mathf.Infinity;
        if (nearestAhead != null)
        {
            float aheadSpd = _speedMps.TryGetValue(nearestAhead.GetInstanceID(), out var s) ? s : 0f;
            if (nearestFwd < hardGap) gov = aheadSpd * 0.8f;
            else if (nearestFwd < holdGap) gov = aheadSpd;
        }
        pvc.speedGovernorMps = gov;

        // Lock the leader (car to line up behind) — first car ahead we see, then keep it unless it disappears.
        if ((_leader == null || !_leader.gameObject.activeInHierarchy) && nearestAhead != null)
        {
            _leader = nearestAhead;
            _leaderIsSafety = _safetyCar != null && nearestAhead == _safetyCar.transform;
            _leaderNumber = _leaderIsSafety ? -1 : CarNumberOf(nearestAhead);
        }

        UpdatePrompt(myPos, fwd);
    }

    // Spline-based pace hold. The leader is the car directly ahead of the player in GRID ORDER (so the prompt
    // matches the reserved-slot formation); if the player has no grid slot it falls back to the nearest car ahead.
    // Speed-capped to that car (no overtaking) with a prompt to line up behind it.
    void UpdateSpline()
    {
        var field = RaceField.Drivers;
        float trackLen = 0f;
        for (int i = 0; i < field.Count; i++)
            if (field[i] != null && field[i].TrackLength > trackLen) trackLen = field[i].TrackLength;
        if (trackLen <= 0f) { _showPrompt = false; pvc.speedGovernorMps = Mathf.Infinity; return; }

        float playerDist = pvc.TrackDistance;
        Transform leaderT = null;
        bool leaderSafety = false;
        float leaderMph = 0f;
        float gap = float.MaxValue; // signed in grid-order mode (+ ahead, - behind); positive-only in fallback

        // Preferred: the member directly ahead of the player in grid order.
        var member = pvc.GridPosition >= 0 ? FormationOrder.MemberAhead(pvc.GridPosition) : null;
        var sdMember = member as SplineDriver;
        if (sdMember != null)
        {
            float g = sdMember.DistanceOnTrack - playerDist;
            if (g > trackLen * 0.5f) g -= trackLen;
            else if (g < -trackLen * 0.5f) g += trackLen;
            gap = g;
            leaderT = sdMember.transform;
            leaderSafety = sdMember.GetComponent<SafetyCar>() != null;
            leaderMph = sdMember.CurrentMph;
        }
        else
        {
            // Fallback: nearest car ahead in track order.
            SplineDriver leader = null;
            float best = float.MaxValue;
            for (int i = 0; i < field.Count; i++)
            {
                var d = field[i];
                if (d == null) continue;
                float g = d.DistanceOnTrack - playerDist;
                if (g <= 0f) g += trackLen;
                if (g > 0f && g < best) { best = g; leader = d; }
            }
            if (leader != null)
            {
                gap = best;
                leaderT = leader.transform;
                leaderSafety = leader.GetComponent<SafetyCar>() != null;
                leaderMph = leader.CurrentMph;
            }
        }

        float gov = Mathf.Infinity;
        if (leaderT != null)
        {
            float aheadMps = leaderMph * MphToMps;
            if (gap <= hardGap) gov = aheadMps * 0.8f;   // at/under buffer (incl. nosing ahead of our slot) — back off
            else if (gap < holdGap) gov = aheadMps;      // close — match it, no overtaking
            _leader = leaderT;
            _leaderIsSafety = leaderSafety;
            _leaderNumber = leaderSafety ? -1 : CarNumberOf(leaderT);
        }
        pvc.speedGovernorMps = gov;

        if (leaderT == null) { _showPrompt = false; return; }
        string who = _leaderIsSafety ? "the safety car" : $"car #{_leaderNumber}";
        _showPrompt = true;
        if (gap < -2f) { _promptText = $"OUT OF POSITION — drop in behind {who}"; _promptColor = new Color(1f, 0.3f, 0.2f); }
        else if (gap > catchUpGap) { _promptText = $"Close up behind {who}"; _promptColor = new Color(1f, 0.85f, 0.2f); }
        else { _promptText = $"Hold station behind {who}"; _promptColor = new Color(0.55f, 1f, 0.55f); }
    }

    void UpdatePrompt(Vector2 myPos, Vector2 fwd)
    {
        if (_leader == null) { _showPrompt = false; return; }
        float fwdDist = Vector2.Dot((Vector2)_leader.position - myPos, fwd);
        string who = _leaderIsSafety ? "the safety car" : $"car #{_leaderNumber}";
        _showPrompt = true;
        if (fwdDist < -2f) { _promptText = $"OUT OF POSITION — drop in behind {who}"; _promptColor = new Color(1f, 0.3f, 0.2f); }
        else if (fwdDist > catchUpGap) { _promptText = $"Close up behind {who}"; _promptColor = new Color(1f, 0.85f, 0.2f); }
        else { _promptText = $"Hold station behind {who}"; _promptColor = new Color(0.55f, 1f, 0.55f); }
    }

    static int CarNumberOf(Transform t)
    {
        var label = t.GetComponent<DriverLabel>();
        if (label != null) return label.carNumber;
        var ai = t.GetComponent<NetworkedAICar>();
        if (ai != null) return ai.CarNumber.Value;
        var pl = t.GetComponent<NetworkedCarBindings>();
        if (pl != null) return pl.Number;
        return 0;
    }

    void OnGUI()
    {
        if (!_showPrompt) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = _promptColor;
        GUI.Label(new Rect(0, Screen.height * 0.08f, Screen.width, 40f), _promptText, style);
    }
}
