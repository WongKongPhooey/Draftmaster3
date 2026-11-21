using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

// Skip the pace lap. A formation lap at Daytona or Pocono is two-plus minutes of following the safety car, and with
// the AI driving the player's car (Auto) there is nothing for the player to do in it. While Auto is on, a prompt
// along the bottom of the screen offers to skip it: pressing it fast-forwards the game (Time.timeScale) until the
// safety car starts to peel off and the field closes up into rows for the start (FormationDirector.FieldClosingUp),
// then drops back to normal speed for the run to the green.
//
// Fast-forward rather than a teleport on purpose: the whole field is driven by the dynamic car model, part of it is
// still filing out of the pit lane, and every pace-lap system (safety car lead-off, the train, the close-up rows)
// carries state built up over the lap. Running the same simulation faster arrives at the close-up exactly as
// watching it would have; moving forty cars round the track by hand would not.
//
// The same skip covers the laps behind the safety car under a full-course caution (FullCourseCaution), offered once
// the safety car has picked up the leader (SafetyCar.PickingUp off) and the player has crossed the start/finish line
// since the caution came out. It fast-forwards through the pits-open lap (on Auto the AI makes the pit call) and
// stops in the same place: the field closing up for the restart, pit road shut.
//
// Press again to stop skipping. Taking the wheel back (Auto off) stops it too. Single player only: a co-op guest
// cannot be fast-forwarded by the host's clock. Added by FormationDirector at Start, so no scene wiring.
public class FormationSkip : MonoBehaviour
{
    [Tooltip("Game speed while skipping. The physics step is unchanged, so this is how many fixed steps run per " +
             "rendered frame's worth of time; a machine that can't keep up just skips more slowly (Time.maximumDeltaTime " +
             "caps the steps per frame).")]
    public float skipTimeScale = 8f;
    [Tooltip("Keyboard shortcut. The pad button is PadBindings.SkipPaceLap.")]
    public Key skipKey = Key.N;

    // True while the game is being fast-forwarded through the pace lap or a caution. AIOnlyRaceWatch reads this so
    // its own game speed doesn't fight the skip.
    public static bool Skipping { get; private set; }

    bool _skipping;
    bool _pressed;          // the prompt was clicked or tapped in OnGUI; acted on in Update
    bool _restorePending;   // the skip ended while the game was paused; put the clock back once it runs again
    readonly List<Sprite> _icons = new List<Sprite>();

    // The player's lap count (RacePositionTracker) when the current caution came out; -1 outside a caution, or
    // while the player's car isn't known yet. A higher count since = they have crossed the line under this caution.
    int _cautionStartLap = -1;
    bool _inCaution;

    // Is the skip on offer? On the formation lap, before the field starts closing up; or under a caution once the
    // safety car has the leader and the player has crossed the line since it came out, until the field closes up
    // for the restart. Either way only with the AI driving the player's car and the player sat in it rather than
    // out on foot.
    public static bool Offered(bool multiplayer, RaceStart.Phase phase, bool fieldClosingUp, bool auto, bool onFoot,
                               bool leaderCaught, bool playerCrossedLine)
    {
        if (multiplayer || fieldClosingUp || !auto || onFoot) return false;
        if (phase == RaceStart.Phase.Formation) return true;
        return phase == RaceStart.Phase.Caution && leaderCaught && playerCrossedLine;
    }

    bool OfferedNow()
    {
        var drive = DriveModeController.Current;
        bool auto = drive != null && !drive.IsDriving && !drive.HandBackPending;
        var director = FormationDirector.Instance;
        bool closingUp = director != null && director.FieldClosingUp;
        var phase = RaceStart.Current;
        bool leaderCaught = false, crossed = false;
        if (phase == RaceStart.Phase.Caution)
        {
            var sc = director != null ? director.SafetyCarInstance : null;
            leaderCaught = sc != null && sc.CautionLap && !sc.PickingUp;
            // The restart close-up is the one with pit road shut (the safety car only closes up on the pass it pits
            // on, but a stray close-up blip with pit road still open must not cut the skip short).
            closingUp = closingUp && !FullCourseCaution.PitRoadOpen;
            var rt = RacePositionTracker.Instance;
            int lap = rt != null ? rt.PlayerLap : -1;
            crossed = _cautionStartLap >= 0 && lap > _cautionStartLap;
        }
        return Offered(GameSession.IsMultiplayer, phase, closingUp, auto, PadInput.OnFoot, leaderCaught, crossed);
    }

    // Notes the player's lap as a caution comes out (or as soon as their car is known in one), and forgets it after.
    void TrackCautionLap()
    {
        if (!RaceStart.IsCaution)
        {
            _inCaution = false;
            _cautionStartLap = -1;
            return;
        }
        if (!_inCaution) { _inCaution = true; _cautionStartLap = -1; }
        if (_cautionStartLap < 0)
        {
            var rt = RacePositionTracker.Instance;
            _cautionStartLap = rt != null ? rt.PlayerLap : -1;
        }
    }

    void Update()
    {
        TrackCautionLap();
        bool offered = OfferedNow();

        bool asked = _pressed;
        _pressed = false;
        if (offered && !PadInput.ModalOpen)
        {
            var kb = Keyboard.current;
            if (kb != null && skipKey != Key.None && kb[skipKey].wasPressedThisFrame) asked = true;
            if (PadInput.PressedDriving(PadBindings.SkipPaceLap)) asked = true;
        }
        if (asked && offered) SetSkipping(!_skipping);
        if (_skipping && !offered) SetSkipping(false);   // closing up, green, or the player took the wheel

        // Paused (timeScale 0): leave the clock alone. The pause menu puts back whatever it found.
        if (Time.timeScale > 0f)
        {
            if (_skipping && !Mathf.Approximately(Time.timeScale, skipTimeScale)) Time.timeScale = skipTimeScale;
            if (_restorePending)
            {
                _restorePending = false;
                if (!_skipping && Mathf.Approximately(Time.timeScale, skipTimeScale)) Time.timeScale = 1f;
            }
        }
    }

    void SetSkipping(bool on)
    {
        if (_skipping == on) return;
        _skipping = on;
        Skipping = on;
        if (on)
        {
            if (Time.timeScale > 0f) Time.timeScale = skipTimeScale;
            return;
        }
        if (Time.timeScale > 0f) Time.timeScale = 1f;
        else _restorePending = true;
    }

    void OnDisable()
    {
        if (_skipping)
        {
            _skipping = false;
            Skipping = false;
            Time.timeScale = 1f;   // a scene change mid-skip must not hand the next scene a fast clock
        }
    }

    void OnGUI()
    {
        if (!OfferedNow() || PadInput.ModalOpen || RacePauseMenu.IsPaused) return;

        string what = RaceStart.IsCaution ? "the caution" : "the pace lap";
        string text = _skipping ? $"Skipping {what} · back to normal speed" : $"Skip {what}";
        string key = skipKey != Key.None ? skipKey.ToString() : null;
        _icons.Clear();
        if (InputGlyphs.UsingGamepad)
        {
            var icon = InputGlyphs.Icon(PadBindings.SkipPaceLap);
            if (icon != null) _icons.Add(icon);
            else key = InputGlyphs.PadName(PadBindings.SkipPaceLap);
        }
        if (PixelGUI.PromptButton(key, text, _icons, PixelGUI.ActionIcon.Next)) _pressed = true;
    }
}
