using UnityEngine;

// The Draftmaster 2 control scheme, on or off (OPTIONS > AI Drivers > Controls). On, the player's car drives itself on
// Auto whenever they are racing it and left / right only pick the lane (Draftmaster.Controls.LaneSelect,
// DriveModeController). Kept across runs in PlayerPrefs; default off.
public static class LegacyLaneControls
{
    const string PrefKey = "LegacyLaneControls";
    static int _enabled = -1;   // cached pref: -1 unread

    public static bool Enabled
    {
        get
        {
            if (_enabled < 0) _enabled = PlayerPrefs.GetInt(PrefKey, 0);
            return _enabled == 1;
        }
        set
        {
            _enabled = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefKey, _enabled);
        }
    }
}
