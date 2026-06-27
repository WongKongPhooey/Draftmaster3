using UnityEngine;

// Services the human player's car when they stop in the pit lane: hold roughly still on pit road for a few
// seconds and the crew fits fresh tyres (and straightens the bodywork). Re-arms once the car leaves the pit.
public class PlayerPitService : MonoBehaviour
{
    public TrackBuilder track;
    public PlayerVehicleController playerCar;
    [Tooltip("Seconds stationary on pit road before the stop completes.")]
    public float serviceSeconds = 3.5f;
    [Tooltip("Distance (m) from the pit centerline still counted as 'in the pit lane'.")]
    public float pitLateralMax = 6f;
    [Tooltip("Speed (m/s) below which the car counts as stopped for service.")]
    public float stopSpeedMps = 3f;
    public bool repairDamage = true;
    public bool refuel = true;

    TireModel _tires;
    VehicleDamage _bodywork;
    FuelTank _fuel;
    float _timer;
    bool _serviced;
    bool _crewWorking;
    float _msgTimer;

    void Start() { if (track == null) track = FindFirstObjectByType<TrackBuilder>(); }

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

        if (onPit && slow && needs && !_serviced && RaceStart.IsGreen)
        {
            if (!_crewWorking)
            {
                PitCrewRegistry.Nearest(playerCar.transform.position)?.BeginService(playerCar.transform);
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
        PitCrewRegistry.Nearest(worldPos)?.EndService();
        _crewWorking = false;
    }

    bool OnPitLane(Vector3 worldPos)
    {
        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) return false;
        float d = track.NearestPitDistance(worldPos);
        var s = track.SamplePitAt(d, pit);
        Vector2 local = track.transform.InverseTransformPoint(worldPos);
        return Vector2.Distance(local, s.position) < pitLateralMax;
    }

    PlayerVehicleController FindPlayer()
    {
        var all = Object.FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].GetComponent<SplineInputDriver>() == null) return all[i];
        return null;
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (_timer > 0f && !_serviced)
        {
            style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, 50f), $"PITTING…  {Mathf.Max(0f, serviceSeconds - _timer):0.0}s", style);
        }
        else if (_msgTimer > 0f)
        {
            style.normal.textColor = new Color(0.3f, 1f, 0.4f, Mathf.Clamp01(_msgTimer));
            GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, 50f), "FRESH TYRES", style);
        }
    }
}
