using UnityEngine;
using UnityEngine.SceneManagement;

// Paints the player's car in the livery of the driver picked on the SINGLE RACE screen.
//
// SingleRaceUI saves the pick as `career.carnumber`, but RaceScene's PlayerCar is authored in #89's paint
// (the demo's own car) and nothing ever repainted it — so whoever was chosen, the player went out in the 89.
// Every other reader of "which car is the player" (GridSpawner taking that livery out of the AI pool, the
// team-switch label, the timing tower, sponsor decals) reads the number off the paintwork via CarIdentity,
// so swapping the sprite here is the whole fix: it runs on sceneLoaded, which is after every Awake and before
// any Start, so they all see the chosen number.
//
// Single race only. The career keeps the scene's car; the saved number means something else there.
public static class PlayerCarLivery
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameSession.IsSingleRace) return;

        int number = PlayerPrefs.GetInt(PlayerDriver.NumberKey, 0);
        if (number <= 0) return;

        var car = CarIdentity.FindPlayerCar();
        if (car == null || car.scene != scene) return;

        Apply(car, number);
    }

    // Repaint `car` as `number` in the carset it is already wearing ("cup26livery89" -> "cup26livery24").
    // Returns false and leaves the car alone when it is already that number, has no livery to read a carset
    // from, or the carset has no paint for that number.
    public static bool Apply(GameObject car, int number)
    {
        if (car == null || number < 0) return false;

        var damage = car.GetComponentInChildren<VehicleDamage>();
        var sr = car.GetComponentInChildren<SpriteRenderer>();
        Sprite current = damage != null && damage.sourceSprite != null ? damage.sourceSprite
                       : (sr != null ? sr.sprite : null);
        if (current == null) return false;
        if (CarIdentity.NumberFromSpriteName(current.name) == number) return false;

        string name = LiveryName(current.name, number);
        if (name == null) return false;

        var livery = Resources.Load<Sprite>(name);
        if (livery == null)
        {
            Debug.LogWarning($"PlayerCarLivery: no livery '{name}' in Resources — the player keeps {current.name}.");
            return false;
        }

        if (damage != null && damage.sourceSprite != null)
        {
            damage.sourceSprite = livery;
            damage.material = null;      // rebuilt from the new texture
            damage.Build();
        }
        else sr.sprite = livery;
        return true;
    }

    // "cup26livery89", 24 -> "cup26livery24". Null when the sprite name isn't a carset livery.
    public static string LiveryName(string spriteName, int number)
    {
        if (string.IsNullOrEmpty(spriteName) || number < 0) return null;
        const string token = "livery";
        int at = spriteName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase);
        if (at <= 0) return null;
        return spriteName.Substring(0, at) + token + number;
    }
}
