using UnityEngine;

// Reads a car's number off the livery it's painted in. Carset sprites are named
// "<carset>livery<number>" (optionally with a "blank"/"alt" suffix), so the paintwork is the single
// source of truth for which car this is — and therefore which driver races it.
public static class CarIdentity
{
    // "cup26livery8" -> 8, "cup26livery21alt1" -> 21. Returns -1 when the name isn't a livery.
    public static int NumberFromSpriteName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return -1;
        const string token = "livery";
        int at = spriteName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase);
        if (at < 0) return -1;

        int i = at + token.Length;
        int value = 0, digits = 0;
        while (i < spriteName.Length && spriteName[i] >= '0' && spriteName[i] <= '9')
        {
            value = value * 10 + (spriteName[i] - '0');
            digits++;
            i++;
        }
        return digits > 0 ? value : -1;
    }

    // The car number worn by this GameObject, from whichever renderer holds its paint. The dynamic model
    // replaces the SpriteRenderer with a deformable VehicleDamage mesh, so check that first.
    public static int NumberOf(GameObject car)
    {
        if (car == null) return -1;

        var damage = car.GetComponentInChildren<VehicleDamage>();
        if (damage != null && damage.sourceSprite != null)
        {
            int n = NumberFromSpriteName(damage.sourceSprite.name);
            if (n >= 0) return n;
        }

        var sr = car.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            int n = NumberFromSpriteName(sr.sprite.name);
            if (n >= 0) return n;
        }

        return -1;
    }

    // The human's car: the one running the dynamic model without an AI input driver bolted on.
    // Call before the AI field spawns and it's the only candidate in the scene. Inactive objects are
    // included because the controller is disabled whenever the player is parked or on foot.
    public const string PlayerCarName = "PlayerCar";

    public static GameObject FindPlayerCar()
    {
        var all = Object.FindObjectsByType<PlayerVehicleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].GetComponent<SplineInputDriver>() == null) return all[i].gameObject;
        return GameObject.Find(PlayerCarName);
    }
}
