using UnityEngine;

// The player's car build: which part is installed in each slot (PlayerPrefs), and the outfitter that
// layers those parts onto a VehicleInfo at spawn. VehicleInfo assets are SHARED — the whole AI field
// reads the same asset — so the player's upgrades apply to a runtime CLONE, never the asset itself.
//
// Applied by PlayerVehicleController.Start on the human-driven car only. Known limit: mid-race team
// switching moves the human into a car whose VehicleInfo was already resolved stock — parts belong to
// YOUR car, not to you.
public static class PlayerCarBuild
{
    const string Prefix = "car.part."; // + slot name -> installed part id

    public static string InstalledId(PartSlot slot) => PlayerPrefs.GetString(Prefix + slot, "");
    public static PartDef Installed(PartSlot slot) => PartCatalog.Get(InstalledId(slot));

    public static void Install(PartDef part)
    {
        if (part == null) return;
        PlayerPrefs.SetString(Prefix + part.slot, part.id);
        PlayerPrefs.Save();
    }

    public static bool HasAnyPart()
    {
        foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            if (Installed(slot) != null) return true;
        return false;
    }

    // Base VehicleInfo + installed parts -> outfitted runtime clone (or the base itself when stock).
    // Curve values AND tangents scale together so the accel curve keeps its shape, just taller.
    public static VehicleInfo Outfit(VehicleInfo baseInfo)
    {
        if (baseInfo == null || !HasAnyPart()) return baseInfo;

        float topAdd = 0f, accelScale = 1f, gripAdd = 0f, wearScale = 1f;
        foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
        {
            var p = Installed(slot);
            if (p == null) continue;
            topAdd += p.topSpeedAdd;
            accelScale *= p.accelScale;
            gripAdd += p.gripAdd;
            wearScale *= p.wearScale;
        }

        var outfitted = Object.Instantiate(baseInfo);
        outfitted.name = baseInfo.name + " (Outfitted)";
        outfitted.topSpeed = Mathf.Max(60, baseInfo.topSpeed + Mathf.RoundToInt(topAdd));
        outfitted.maxLateralG = Mathf.Max(0.5f, baseInfo.maxLateralG + gripAdd);
        outfitted.tireWearRate = baseInfo.tireWearRate * wearScale;

        if (!Mathf.Approximately(accelScale, 1f) && baseInfo.accelerationCurve != null
            && baseInfo.accelerationCurve.length > 0)
        {
            var keys = baseInfo.accelerationCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= accelScale;
                keys[i].inTangent *= accelScale;
                keys[i].outTangent *= accelScale;
            }
            outfitted.accelerationCurve = new AnimationCurve(keys);
        }

        return outfitted;
    }

    // "Engine: R7 Race Engine" lines for shop panels / garage readouts.
    public static string DescribeSlot(PartSlot slot)
    {
        var p = Installed(slot);
        return $"{slot}: {(p != null ? p.name : "Stock")}";
    }
}
