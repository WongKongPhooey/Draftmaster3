using UnityEngine;
using UnityEngine.InputSystem;
using Draftmaster.Controls;

// The winner's few minutes with the circuit to themselves.
//
// END RACE normally puts the race away in one move: every car into its box, the driver stood beside theirs
// (PitLaneStart.EndRaceInBox). A race the player WON stops short of that. The rest of the field is still parked,
// but the player's car is put on the start/finish line with the controls in their hands — donuts, a burnout on the
// line, a slow lap back round — and they can get out wherever they stop and walk about (the winner's fans find them
// there; WinnerFanCrowd). While the car is being driven, the tow's prompt slot offers the way out: same key, same
// button, same sticky hint, reading "Head back to the pits". Press it and the screen goes dark and the race is put
// away exactly as END RACE would have put it away.
//
// The prompt is only offered from the driving seat, for the reason the tow is: its key (P) is the phone's on foot.
// A driver out of the car gets back in to head in — the car keeps its "Your car" marker the whole time.
//
// Added by RaceDirector.EndRace to its own GameObject, so it dies with the race scene.
public class WinnerVictoryLap : MonoBehaviour
{
    static WinnerVictoryLap _instance;

    // The winner has the circuit. OnFootController lets the walker off the paddock boundary while this is true (they
    // climbed out on the racing surface, which no boundary is drawn round), StrandedTow stands down so the two never
    // offer the same key at once, and PitLaneStart lets the driver out anywhere rather than only in their box.
    public static bool Active => _instance != null && _instance.isActiveAndEnabled && !_instance._done;

    const string HintId = "victorylap";

    PitLaneStart _pits;
    bool _offering;
    bool _done;

    // Put the winner's car on the line and hand it to them. False when this scene cannot do it (no on-foot body,
    // no start/finish, no box to come back to) — END RACE then puts the race away as it always did.
    public static bool Begin(GameObject host, PitLaneStart pits)
    {
        if (host == null || pits == null || !pits.StartVictoryLap()) return false;

        var lap = host.GetComponent<WinnerVictoryLap>();
        if (lap == null) lap = host.AddComponent<WinnerVictoryLap>();
        lap._pits = pits;
        lap._done = false;
        lap.enabled = true;
        _instance = lap;
        return true;
    }

    void OnDestroy()
    {
        Offer(false);
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (_done) return;
        if (_pits == null) { Finish(); return; }

        bool show = _pits.IsDriving && !RacePauseMenu.IsPaused && !ScreenFade.Busy;
        Offer(show);
        if (!show) return;

        var kb = Keyboard.current;
        var towKey = StrandedTow.Instance != null ? StrandedTow.Instance.towKey : Key.P;
        if ((kb != null && towKey != Key.None && kb[towKey].wasPressedThisFrame) ||
            PadInput.PressedDriving(PadBindings.Tow)) HeadBack();
    }

    // The tow's own hint, worded for a car that is not broken: urgent so it is not stuck behind whatever else is
    // up, and a button as well for a touch screen.
    void Offer(bool show)
    {
        if (show == _offering) return;
        _offering = show;

        if (show)
        {
            var towKey = StrandedTow.Instance != null ? StrandedTow.Instance.towKey : Key.P;
            ControlHints.ShowSticky(HintId, towKey.ToString().ToUpperInvariant(), InputGlyphs.Pad(PadBindings.Tow),
                                    "Head back to the pits", urgent: true, onPress: HeadBack,
                                    icon: PixelGUI.ActionIcon.Tow);
        }
        else
        {
            ControlHints.Hide(HintId);
        }
    }

    // Dark, put away, light — the tow's cut, hiding the same half-lap jump.
    void HeadBack()
    {
        if (_done || ScreenFade.Busy) return;
        Offer(false);
        ScreenFade.Cut(() =>
        {
            Finish();
            if (_pits != null) _pits.EndRaceInBox();
        }, outSeconds: 0.5f, holdSeconds: 0.7f);
    }

    void Finish()
    {
        _done = true;
        Offer(false);
        enabled = false;
    }
}
