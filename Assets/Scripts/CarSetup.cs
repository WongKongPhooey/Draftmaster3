using UnityEngine;

// The three choices the driver makes before rolling out: rubber, fuel load, and handling balance.
// Authored by the pre-race setup panel (CarSetupPanelUI), applied to the car's own components — there is no
// separate "setup" system to keep in sync, each field just writes the thing that already models it:
//   compound → TireModel.SetCompound (grip / wear / warm-up)
//   fuel     → FuelTank.fuelLitres   (mass, and the engine cuts when dry)
//   balance  → PlayerVehicleController.understeerBias (front/rear grip split)
// Persisted to PlayerPrefs so the next session opens on what the driver last ran.
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

    public float UndersteerBias => NeutralBias + balance * BalanceSwing;

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

    public CarSetup Clone() => new CarSetup { compound = compound, fuelLitres = fuelLitres, balance = balance };

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
        if (pvc != null) pvc.understeerBias = UndersteerBias;
    }

    const string KeyCompound = "setup.compound";
    const string KeyFuel = "setup.fuel";
    const string KeyBalance = "setup.balance";

    public void Save()
    {
        PlayerPrefs.SetInt(KeyCompound, (int)compound);
        PlayerPrefs.SetFloat(KeyFuel, fuelLitres);
        PlayerPrefs.SetFloat(KeyBalance, balance);
        PlayerPrefs.Save();
    }

    public static CarSetup Load() => new CarSetup
    {
        compound = (TireModel.Compound)PlayerPrefs.GetInt(KeyCompound, (int)TireModel.Compound.Soft),
        fuelLitres = Mathf.Clamp(PlayerPrefs.GetFloat(KeyFuel, 12f), MinFuel, MaxFuel),
        balance = Mathf.Clamp(PlayerPrefs.GetFloat(KeyBalance, 0f), -1f, 1f),
    };
}
