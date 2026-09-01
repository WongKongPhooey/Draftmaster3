using UnityEngine;

// Services the human player's car when they stop in their pit box: a glowing rectangle marks the
// player's reserved box while they're in the pit lane, and the stop only begins once the car has
// stopped FULLY inside it. Hold still for a few seconds and the crew fits fresh tyres (and
// straightens the bodywork). Re-arms once the car leaves the pit. If the session gave the player no
// reserved box (PitLane.PlayerBox = -1, e.g. multiplayer), falls back to the old anywhere-in-the-lane
// behaviour.
public class PlayerPitService : MonoBehaviour
{
    public TrackBuilder track;
    public PlayerVehicleController playerCar;
    [Tooltip("Seconds stationary on pit road before the stop completes.")]
    public float serviceSeconds = 3.5f;
    [Tooltip("Distance (m) from the pit centerline still counted as 'in the pit lane'.")]
    public float pitLateralMax = 6f;
    [Tooltip("Speed (m/s) below which the car counts as stopped for service.")]
    public float stopSpeedMps = 1.5f;
    [Tooltip("Tell the crew the player is coming once the car is this close (m) to their box, so the sign man is out with the board down before it arrives — the board is what marks the stop.")]
    public float signCallM = 35f;

    public bool repairDamage = true;
    public bool refuel = true;

    TireModel _tires;
    VehicleDamage _bodywork;
    FuelTank _fuel;
    PlayerPitBoxMarker _marker;
    PitCrewBox _activeCrew;
    float _timer;
    bool _serviced;
    bool _crewWorking;
    float _msgTimer;
    bool _showBoxHint;

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        _marker = PlayerPitBoxMarker.Ensure();
    }

    void Update()
    {
        if (playerCar == null) playerCar = FindPlayer();
        if (playerCar == null || track == null) return;
        if (_tires == null) _tires = playerCar.GetComponent<TireModel>();
        if (_bodywork == null) _bodywork = playerCar.GetComponentInChildren<VehicleDamage>();
        if (_fuel == null) _fuel = playerCar.GetComponent<FuelTank>();

        bool onPit = OnPitLane(playerCar.transform.position);
        bool slow = playerCar.SpeedMps < stopSpeedMps;
        float wear = _tires != null ? 0.5f * (_tires.FrontWear + _tires.RearWear) : 0f;
        bool needs = wear > 0.05f
                     || (repairDamage && _bodywork != null && _bodywork.DamageLevel > 0.05f)
                     || (refuel && _fuel != null && _fuel.Fraction < 0.999f);

        // Glowing box marker: shown while the player drives the pit lane. The stop only arms once the
        // car is stopped fully inside it. No reserved box this session → the whole lane counts.
        if (_marker != null)
        {
            _marker.UpdateFor(track);
            // Only while there is a session to pit out of. Between sessions the player's car is parked in
            // its box, which lit the glow permanently and read as an invitation to get in it.
            _marker.SetVisible(onPit && RaceWeekend.SessionLive);
        }
        bool hasBox = _marker != null && _marker.IsBuilt;
        // Strict: when the pit-box system is live, ONLY the player's own box services them — stopping in
        // another car's box does nothing. The lane-wide fallback exists solely for sessions that have no
        // pit boxes at all (PitLane never configured, e.g. legacy/multiplayer scenes).
        bool inBox = hasBox ? _marker.CarFullyInside(playerCar.transform, CarHalfExtents())
                            : !PitLane.Configured;
        _showBoxHint = hasBox && onPit && slow && needs && !inBox && !_serviced;

        // Coming down the lane with a stop to make: call the crew so their sign man is out and the board is
        // down over the box before the car gets there. Only ever the player's OWN box — a sign man waving at
        // every box driven past would be worse than none. SignalApproach ignores the repeats.
        if (hasBox && onPit && needs && !_serviced && RaceStart.IsGreen)
        {
            var crew = PitCrewRegistry.ForBox(PitLane.PlayerBox);
            if (crew != null &&
                (crew.transform.position - playerCar.transform.position).sqrMagnitude < signCallM * signCallM)
                crew.SignalApproach(playerCar.transform);
        }

        if (onPit && inBox && slow && needs && !_serviced && RaceStart.IsGreen)
        {
            if (!_crewWorking)
            {
                // The car is in its own box, so use its own crew; Nearest is the no-reserved-box fallback.
                _activeCrew = hasBox ? PitCrewRegistry.ForBox(PitLane.PlayerBox) : null;
                if (_activeCrew == null) _activeCrew = PitCrewRegistry.Nearest(playerCar.transform.position);
                _activeCrew?.BeginService(playerCar.transform);
                var hist = playerCar.GetComponent<PitHistory>();
                if (hist == null) hist = playerCar.gameObject.AddComponent<PitHistory>();
                hist.Record(RacePositionTracker.Instance != null ? RacePositionTracker.Instance.LapOf(playerCar.transform) : 0);
                _crewWorking = true;
            }
            _timer += Time.deltaTime;
            if (_timer >= serviceSeconds)
            {
                playerCar.PitResetTyres();
                if (repairDamage && _bodywork != null) _bodywork.RepairFull();
                if (refuel && _fuel != null) _fuel.FillFull();
                _serviced = true;
                _msgTimer = 3f;
                EndCrew(playerCar.transform.position);
            }
        }
        else
        {
            _timer = 0f;
            if (_crewWorking) EndCrew(playerCar.transform.position);
            if (!onPit) _serviced = false; // re-arm after leaving the pit
        }

        if (_msgTimer > 0f) _msgTimer -= Time.deltaTime;
    }

    void EndCrew(Vector3 worldPos)
    {
        if (_activeCrew == null) _activeCrew = PitCrewRegistry.Nearest(worldPos);
        _activeCrew?.EndService();
        _activeCrew = null;
        _crewWorking = false;
    }

    // Car footprint for the fully-inside-the-box test (VehicleCollision convention: x = half-width,
    // y = half-length).
    Vector2 CarHalfExtents()
    {
        var vc = playerCar != null ? playerCar.GetComponent<VehicleCollision>() : null;
        return vc != null ? vc.halfExtents : new Vector2(1.0f, 2.4f);
    }

    bool OnPitLane(Vector3 worldPos)
    {
        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) return false;
        float d = track.NearestPitDistance(worldPos);
        var s = track.SamplePitAt(d, pit);
        Vector2 local = track.transform.InverseTransformPoint(worldPos);
        // Cover the box lane too — the player's box sits on it, outside the pit ribbon proper.
        float max = track.HasPitBoxLane ? Mathf.Max(pitLateralMax, track.PitBoxLaneOuterLateral + 0.5f) : pitLateralMax;
        return Vector2.Distance(local, s.position) < max;
    }

    // Registry read, not a scene search. This is asked every frame until it answers, and it answers with
    // nothing for the whole time the player is walking around the paddock.
    PlayerVehicleController FindPlayer() => PlayerVehicleController.Human;

    void OnGUI()
    {
        // Pit-stop callouts, centred over the car: a framed strip rather than floating text, so a message
        // that lands over a light piece of pit lane is still readable.
        if (_timer > 0f && !_serviced)
            Callout($"PITTING…  {Mathf.Max(0f, serviceSeconds - _timer):0.0}s", PixelGUI.Gold, 1f);
        else if (_msgTimer > 0f)
            Callout("FRESH TYRES", PixelGUI.Confirm, Mathf.Clamp01(_msgTimer));
        else if (_showBoxHint)
            Callout("STOP FULLY INSIDE YOUR PIT BOX", PixelGUI.Gold, 1f);
    }

    static void Callout(string text, Color colour, float alpha)
    {
        float w = PixelGUI.Px(240f), h = PixelGUI.Px(26f);
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round(Screen.height * 0.28f);

        var prevGui = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        PixelGUI.Panel(new Rect(x, y, w, h));

        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(colour.r, colour.g, colour.b, alpha);
        GUI.Label(new Rect(x, y, w, h), text, style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
        GUI.color = prevGui;
    }
}
