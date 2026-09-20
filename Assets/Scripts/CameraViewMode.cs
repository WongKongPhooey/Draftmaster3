using UnityEngine;

// Which camera the player watches the race from. Two of them:
//
//   Fixed — the default, and what the game has always done: the view hangs over the car square with the
//           world, so north is up all race and the car turns underneath it.
//   Swing — a chase camera. The view rolls until the car's nose points up the screen, so it sits behind the
//           car through a corner, and it is sprung rather than bolted on, arriving a beat late with a little
//           swing past the mark (see Draftmaster.Sim.CameraSwing).
//
// Held here rather than on the camera because the choice outlives any one scene's camera: it is a player
// setting, saved to PlayerPrefs, read by DrivingCameraFeel wherever the race happens to be. Switching is
// safe mid-race — the spring eases from wherever the view currently is, in both directions.
public static class CameraViewMode
{
    public enum Mode { Fixed = 0, Swing = 1 }

    const string PrefKey = "CameraMode";

    static bool _loaded;
    static Mode _mode;

    public static Mode Current
    {
        get
        {
            if (!_loaded)
            {
                _mode = PlayerPrefs.GetInt(PrefKey, (int)Mode.Fixed) == (int)Mode.Swing ? Mode.Swing : Mode.Fixed;
                _loaded = true;
            }
            return _mode;
        }
        set
        {
            if (_loaded && _mode == value) return;
            _mode = value;
            _loaded = true;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    // The same choice as a toggle, for the pause menu's row.
    public static bool Swinging
    {
        get => Current == Mode.Swing;
        set => Current = value ? Mode.Swing : Mode.Fixed;
    }

    public static string Label => Current == Mode.Swing ? "SWING" : "FIXED";
}
