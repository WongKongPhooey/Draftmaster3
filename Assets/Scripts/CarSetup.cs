using Draftmaster.Data;
using Draftmaster.Sim;
using UnityEngine;

// The choices the driver makes before rolling out: rubber, fuel load, handling balance and steering ratio.
// Authored by the pre-race setup panel (CarSetupPanelUI), applied to the car's own components — there is no
// separate "setup" system to keep in sync, each field just writes the thing that already models it:
//   compound → TireModel.SetCompound (grip / wear / warm-up)
//   fuel     → FuelTank.fuelLitres   (mass, and the engine cuts when dry)
//   balance  → PlayerVehicleController.understeerBias (front/rear grip split)
//   steering → PlayerVehicleController.steeringRatio  (1:8 = the car as it always steered; see SteeringRatio)
// Persisted to PlayerPrefs so the next session opens on what the driver last ran. In a career race the car
// runs the race plan instead (SaveAsRacePlan / ForRace), set on race morning in the motorhome.
[System.Serializable]
public class CarSetup
{
    public const float MinFuel = 1f;
    public const float MaxFuel = 20f;

    // Balance maps onto the car's understeer bias. Neutral matches PlayerVehicleController's own default
    // (mildly stable), and the swing stays well inside its [-0.3, 0.3] range so no setting is undriveable.
    public const float NeutralBias = 0.05f;
    public const float BalanceSwing = 0.15f;

    public TireModel.Compound compound = TireModel.Compound.Soft;
    [Range(MinFuel, MaxFuel)] public float fuelLitres = 12f;
    [Tooltip("-1 = full oversteer (loose, rotates), 0 = as the team left it, +1 = full understeer (stable, pushes).")]
    [Range(-1f, 1f)] public float balance = 0f;

    [Tooltip("Steering ratio 1:N. 8 = baseline; higher = slower rack, less sensitive. Defaults per track: ovals 1:12, road courses 1:8.")]
    [Range(SteeringRatio.Min, SteeringRatio.Max)] public float steeringRatio = SteeringRatio.Baseline;

    public float UndersteerBias => NeutralBias + balance * BalanceSwing;

    public string SteeringRatioLabel => SteeringRatio.Label(steeringRatio);

    // Human-readable balance for the panel label.
    public string BalanceLabel
    {
        get
        {
            if (balance <= -0.66f) return "Loose";
            if (balance <= -0.2f) return "Slight oversteer";
            if (balance < 0.2f) return "Neutral";
            if (balance < 0.66f) return "Slight understeer";
            return "Tight";
        }
    }

    public CarSetup Clone() => new CarSetup
    {
        compound = compound, fuelLitres = fuelLitres, balance = balance, steeringRatio = steeringRatio,
    };

    // Push the setup onto a car. Adds a FuelTank if the car hasn't got one (only the crew-chief HUD did that
    // before, so a car that never opened it had no tank to fill).
    public void ApplyTo(GameObject car)
    {
        if (car == null) return;

        var tires = car.GetComponent<TireModel>();
        if (tires == null) tires = car.AddComponent<TireModel>();
        tires.SetCompound(compound);

        var tank = car.GetComponent<FuelTank>();
        if (tank == null) tank = car.AddComponent<FuelTank>();
        tank.fuelLitres = Mathf.Clamp(fuelLitres, 0f, tank.capacityLitres);

        var pvc = car.GetComponent<PlayerVehicleController>();
        if (pvc != null)
        {
            pvc.understeerBias = UndersteerBias;
            pvc.steeringRatio = SteeringRatio.Clamp(steeringRatio);
        }
    }

    // Push only what the driver changed against `before` (what the panel opened on) onto a car that has been
    // run on. The car sat in its box after a tow is as worn and as light as it came in, and re-applying the
    // whole setup would hand it a fresh set and a full load for crashing. Rules in SetupAdjustment.
    public void ApplyChangesTo(GameObject car, CarSetup before)
    {
        if (car == null) return;
        if (before == null) { ApplyTo(car); return; }

        if (SetupAdjustment.FitsNewTyres((int)before.compound, (int)compound))
        {
            var tires = car.GetComponent<TireModel>();
            if (tires == null) tires = car.AddComponent<TireModel>();
            tires.SetCompound(compound);
        }

        if (SetupAdjustment.ChangesFuel(before.fuelLitres, fuelLitres))
        {
            var tank = car.GetComponent<FuelTank>();
            if (tank == null) tank = car.AddComponent<FuelTank>();
            tank.fuelLitres = Mathf.Clamp(fuelLitres, 0f, tank.capacityLitres);
        }

        var pvc = car.GetComponent<PlayerVehicleController>();
        if (pvc != null)
        {
            pvc.understeerBias = UndersteerBias;
            pvc.steeringRatio = SteeringRatio.Clamp(steeringRatio);
        }
    }

    const string KeyCompound = "setup.compound";
    const string KeyFuel = "setup.fuel";
    const string KeyBalance = "setup.balance";
    // The steering ratio is remembered per kind of track, not once: what suits Daytona's long banked turns
    // is too slow for the Glen's esses, and the next oval should open on the last oval's choice.
    const string KeySteeringOval = "setup.steering.oval";
    const string KeySteeringRoad = "setup.steering.road";

    // The track the next drive is on — the one the race scene builds.
    static bool CurrentIsRoadCourse => TrackSelection.CurrentType == TrackType.RoadCourse;

    static string SteeringKey(bool roadCourse) => roadCourse ? KeySteeringRoad : KeySteeringOval;

    static float LoadSteering(string key, bool roadCourse) =>
        SteeringRatio.Clamp(PlayerPrefs.GetFloat(key, SteeringRatio.DefaultFor(roadCourse)));

    public void Save()
    {
        PlayerPrefs.SetInt(KeyCompound, (int)compound);
        PlayerPrefs.SetFloat(KeyFuel, fuelLitres);
        PlayerPrefs.SetFloat(KeyBalance, balance);
        PlayerPrefs.SetFloat(SteeringKey(CurrentIsRoadCourse), SteeringRatio.Clamp(steeringRatio));
        PlayerPrefs.Save();
    }

    public static CarSetup Load()
    {
        bool road = CurrentIsRoadCourse;
        return new CarSetup
        {
            compound = (TireModel.Compound)PlayerPrefs.GetInt(KeyCompound, (int)TireModel.Compound.Soft),
            fuelLitres = Mathf.Clamp(PlayerPrefs.GetFloat(KeyFuel, 12f), MinFuel, MaxFuel),
            balance = Mathf.Clamp(PlayerPrefs.GetFloat(KeyBalance, 0f), -1f, 1f),
            steeringRatio = LoadSteering(SteeringKey(road), road),
        };
    }

    // ---- the race plan
    //
    // Agreed on race morning in the motorhome (the RacePlan booking) and kept apart from the pit-box setup
    // above, so a session driven after it (a truck driver's Friday qualifying) cannot quietly change the race
    // car. Stamped with the weekend it was made for: last week's plan is not this week's.
    const string KeyPlanWeekend = "raceplan.weekend";
    const string KeyPlanCompound = "raceplan.compound";
    const string KeyPlanFuel = "raceplan.fuel";
    const string KeyPlanBalance = "raceplan.balance";
    const string KeyPlanSteering = "raceplan.steering";

    public void SaveAsRacePlan(int weekendId)
    {
        PlayerPrefs.SetInt(KeyPlanWeekend, weekendId);
        PlayerPrefs.SetInt(KeyPlanCompound, (int)compound);
        PlayerPrefs.SetFloat(KeyPlanFuel, fuelLitres);
        PlayerPrefs.SetFloat(KeyPlanBalance, balance);
        PlayerPrefs.SetFloat(KeyPlanSteering, SteeringRatio.Clamp(steeringRatio));
        PlayerPrefs.Save();
    }

    // The race plan made for this weekend, or null when nobody sat down to make one.
    public static CarSetup LoadRacePlan(int weekendId)
    {
        if (!PlayerPrefs.HasKey(KeyPlanWeekend) || PlayerPrefs.GetInt(KeyPlanWeekend) != weekendId) return null;
        return new CarSetup
        {
            compound = (TireModel.Compound)PlayerPrefs.GetInt(KeyPlanCompound, (int)TireModel.Compound.Soft),
            fuelLitres = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPlanFuel, 12f), MinFuel, MaxFuel),
            balance = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPlanBalance, 0f), -1f, 1f),
            // A plan saved before the ratio existed opens on the track's default.
            steeringRatio = LoadSteering(KeyPlanSteering, CurrentIsRoadCourse),
        };
    }

    // What the car runs in the race: the plan when there is one, otherwise whatever the crew left on it
    // from the last session the driver set up — the crew made the plan without them.
    public static CarSetup ForRace(int weekendId) => LoadRacePlan(weekendId) ?? Load();

    public static void ClearRacePlan()
    {
        PlayerPrefs.DeleteKey(KeyPlanWeekend);
        PlayerPrefs.DeleteKey(KeyPlanCompound);
        PlayerPrefs.DeleteKey(KeyPlanFuel);
        PlayerPrefs.DeleteKey(KeyPlanBalance);
        PlayerPrefs.DeleteKey(KeyPlanSteering);
        PlayerPrefs.Save();
    }
}
