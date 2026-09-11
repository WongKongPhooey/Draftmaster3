using UnityEngine;
using UnityEngine.UI;

// Hard on/off blink for the cursor and the advance caret. Steps, never a fade — a pixel cursor that
// cross-dissolves reads as a bug.
//
// IN ITS OWN FILE ON PURPOSE. It used to live inside IronOvalUI.cs with the rest of the kit, and a
// MonoBehaviour whose file is not named after it never gets a MonoScript of its own. Unity would let you
// put one in a scene and then write it out with no field data behind it — which is how the title menu
// ended up with five solid arrows and two flashing ones, and how TitleScreen.unity ended up with four
// half-written components that crashed the first Windows build before it drew a frame. The editor shrugs
// those off; a player build reads the scene with no type tree to fall back on, walks off the end of the
// record and declares the whole file corrupt.
//
// Callers still add it in code (IronOvalUI.Cursor, the dialogue caret, TitleScreenUI.InstallCursorBlinks),
// which stays the safe habit for a component that costs nothing to build at load.
public class IronOvalBlink : MonoBehaviour
{
    [Tooltip("Seconds on, then the same off. The sheet asks for 0.45s for the selection cursor.")]
    public float interval = 0.45f;

    Graphic _graphic;
    float _t;

    void Awake() { _graphic = GetComponent<Graphic>(); }

    // A cursor that is shown starts its beat over, visible. Without this it picks up whatever phase the
    // last row left behind, so moving the selection can land the arrow on the dark half of the blink and
    // the row reads as unselected for a moment.
    void OnEnable() { _t = 0f; if (_graphic != null) _graphic.enabled = true; }

    void OnDisable() { if (_graphic != null) _graphic.enabled = true; }

    void Update()
    {
        if (_graphic == null || interval <= 0f) return;
        _t += Time.unscaledDeltaTime;
        if (_t < interval) return;
        _t -= interval;
        _graphic.enabled = !_graphic.enabled;
    }
}
