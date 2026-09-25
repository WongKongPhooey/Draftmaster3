using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

// On-screen walking controls for a phone or tablet: a floating stick under the left thumb, and a tap
// anywhere else that the paddock reads as "talk to them".
//
// The sibling of TouchDriveControls, and put together the same way — the rules are TouchWalkLayout /
// TouchWalkState in Draftmaster.Controls where the EditMode tests can reach them, and this reads the
// fingers and draws. What differs is when it is up and what a stray finger means.
//
// The stick is up only while it is the way the player is walking: on a touch device, on foot, nothing modal
// over the paddock, not frozen by a cutscene, and the pad not the device they touched last.
//
// Taps outlive the stick. A conversation counts as modal (PadInput.ModalOpen), so gating taps on the stick
// being up would leave a phone player unable to advance a line they had just started — the stick goes away
// and the only thing that moves the dialogue on is another tap. So taps keep being read through a
// conversation, and stand down only for the screens that draw their own buttons: the pause menu, the phone,
// the weekend sheet, a result card, a dialogue CHOICE.
//
// OnFootController reads Move while Active, and asks TookTap() for the tap.
[DefaultExecutionOrder(-900)]
public class TouchWalkControls : MonoBehaviour
{
    static TouchWalkControls _instance;
    static readonly TouchWalkState _state = new TouchWalkState();
    static TouchWalkLayout _layout;

    readonly List<TouchPoint> _touches = new List<TouchPoint>();
    GUIStyle _label;
    GUIStyle _labelFrom;

    static bool _tapPending;
    static Vector2 _tapAt;      // Unity screen space, y up — ready for a camera to unproject

    // Kenney's On-screen Controls (CC0, licence copied in beside the art): the socket is the disc with four
    // direction marks on it, so the control says which way it goes without a caption, and the cap is the
    // plain disc. Copied into Resources rather than referenced, because this component wires itself up and
    // has no inspector to hold a sprite.
    //
    // The WHITE variant of the pack deliberately: the art is drawn in the kit's own colours by tinting it
    // through GUI.color, and tinting the dark variant only ever gets you a darker version of the dark
    // variant. Not the pixel prompts' pack — these are smooth vector-derived discs and are imported
    // bilinear, because a circle scaled with point filtering shows every step of its edge.
    const string BaseResource = "UI/Touch/stick-base";
    const string KnobResource = "UI/Touch/stick-knob";
    static Texture2D _baseArt, _knobArt;
    static bool _artLoaded;

    // Both null when the art is missing — the stick then falls back to the drawn blocks below, which is
    // plain but is still a control the player can find.
    static void LoadArt()
    {
        if (_artLoaded) return;
        _artLoaded = true;
        _baseArt = Resources.Load<Texture2D>(BaseResource);
        _knobArt = Resources.Load<Texture2D>(KnobResource);
        if (_baseArt == null || _knobArt == null)
            Debug.LogWarning($"TouchWalkControls: no art at Resources/{BaseResource} + {KnobResource} — the stick draws as plain blocks.");
    }

    // The stick is up and the player's body should read it.
    public static bool Active { get; private set; }

    public static Vector2 Move => Active ? new Vector2(_state.MoveX, _state.MoveY) : Vector2.zero;

    // Double-tap the stick to flip walk/run: thumb down-up-down, and the second landing is already running.
    // The thumb on the glass is the "push" — a floating stick has no tilt until the thumb moves, and a tap
    // is exactly a thumb that came down and went away again. A stick-zone touch is always the stick's, never
    // a talk-tap, so the two never argue over a finger. Kept across the stick being put away (a
    // conversation, a menu): the gait is the player's choice, not the screen's.
    static readonly StickDoubleTap _gait = new StickDoubleTap();
    public static bool Running => _gait.Running;

    // A tap is waiting, and taking it clears it. One caller gets each tap: the body that acts on it.
    public static bool TookTap(out Vector2 screenPoint)
    {
        screenPoint = _tapAt;
        if (!_tapPending) return false;
        _tapPending = false;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("TouchWalkControls");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TouchWalkControls>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        useGUILayout = false;
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        Active = false;
        _tapPending = false;
        _state.Reset();
    }

    // On foot with a thumb, and the pad not in the player's hands.
    static bool OnFootWithThumbs =>
        TouchDriveControls.TouchPlatform && !InputGlyphs.UsingGamepad && PadInput.OnFoot;

    // A screen with its own buttons on it owns every finger that lands. Deliberately NOT PadInput.ModalOpen,
    // which counts a conversation — see the note at the top.
    static bool ScreenOwnsTouches =>
        RacePauseMenu.IsPaused || PhoneUI.IsOpen || WeekendScheduleUI.IsOpen ||
        WeekendModal.AnyOpen || DialogueChoiceUI.IsOpen;

    static bool StickShows
    {
        get
        {
            if (!OnFootWithThumbs || ScreenOwnsTouches) return false;
            if (NPCInteractable.AnyConversationActive) return false;   // planted until the talk is over
            var body = OnFootController.Current;
            return body != null && !body.MovementLocked && !body.RemotePuppet;
        }
    }

    void Update()
    {
        if (!OnFootWithThumbs || ScreenOwnsTouches)
        {
            Stand();
            return;
        }

        _layout = CurrentLayout();
        ReadTouches();
        _state.Update(_touches, _layout, Time.unscaledTime);

        // The stick only drives a body that is free to walk; the tap is read either way, so a conversation
        // can be advanced and a cutscene can be tapped through wherever it hands control back.
        Active = StickShows;

        // Until the opening has taught running there is nothing to toggle, and a double tap made early must
        // not be waiting to turn into a run the moment the lock lifts.
        if (PitLaneStart.RunLocked) _gait.Reset();
        else if (Active) _gait.Update(_state.Walking ? 1f : 0f, Time.unscaledTime);
        else _gait.Interrupt();

        if (_state.Tapped)
        {
            _tapPending = true;
            _tapAt = new Vector2(_state.TapX, UnityEngine.Device.Screen.height - _state.TapY);
        }
    }

    void Stand()
    {
        _gait.Interrupt();
        if (!Active && !_tapPending) return;
        Active = false;
        _tapPending = false;
        _state.Reset();
    }

    static TouchWalkLayout CurrentLayout()
    {
        float w = UnityEngine.Device.Screen.width, h = UnityEngine.Device.Screen.height;
        // Screen.safeArea has its origin bottom-left; the layout is in IMGUI's top-left space.
        var sa = UnityEngine.Device.Screen.safeArea;
        if (sa.width <= 0f || sa.height <= 0f) sa = new Rect(0f, 0f, w, h);
        var safe = new TouchRect(sa.x, h - sa.yMax, sa.width, sa.height);
        return new TouchWalkLayout(safe, TouchLayout.UnitFor(w, h));
    }

    void ReadTouches()
    {
        _touches.Clear();
        var screen = Touchscreen.current;
        if (screen == null) return;

        float h = UnityEngine.Device.Screen.height;
        var touches = screen.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];
            if (!t.isInProgress) continue;
            var p = t.position.ReadValue();
            _touches.Add(new TouchPoint(t.touchId.ReadValue(), p.x, h - p.y));
        }
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
        if (!Active || Event.current.type != EventType.Repaint) return;
        GUI.depth = -10;

        float u = _layout.unit;
        float ring = TouchWalkLayout.RingSize * u;
        float knob = TouchWalkLayout.KnobSize * u;

        // Held: the ring sits where the thumb came down and the knob follows it. Idle: it waits in the
        // corner at half strength, showing where a thumb goes without covering the paddock.
        float cx, cy, kx, ky;
        if (_state.Walking)
        {
            cx = _state.CentreX; cy = _state.CentreY;
            kx = _state.KnobX; ky = _state.KnobY;
        }
        else
        {
            cx = _layout.stickRest.centerX; cy = _layout.stickRest.centerY;
            kx = cx; ky = cy;
        }

        float alpha = _state.Walking ? 1f : 0.55f;
        var ringRect = new Rect(cx - ring * 0.5f, cy - ring * 0.5f, ring, ring);
        var knobRect = new Rect(kx - knob * 0.5f, ky - knob * 0.5f, knob, knob);

        LoadArt();
        if (_baseArt != null && _knobArt != null)
        {
            // The socket sits well back so the paddock reads through it; the cap is the kit's gold, the
            // same colour the menus put under the player's own selection.
            GUI.color = Fade(PixelGUI.Text, 0.35f * alpha);
            GUI.DrawTexture(ringRect, _baseArt);
            GUI.color = Fade(PixelGUI.Gold, alpha);
            GUI.DrawTexture(knobRect, _knobArt);
            GUI.color = Color.white;
        }
        else
        {
            PixelGUI.Fill(ringRect, Fade(PixelGUI.PlateDeep, 0.45f * alpha));
            PixelGUI.Frame(ringRect, Fade(PixelGUI.Text, 0.45f * alpha));
            PixelGUI.Fill(knobRect, Fade(PixelGUI.Gold, 0.9f * alpha));
            PixelGUI.Frame(knobRect, Fade(PixelGUI.Ink, 0.8f * alpha));
        }

        // Always up, held or not: the second half of a double tap is a held thumb, and the gait it just
        // flipped to should read the moment it flips.
        Label(new Rect(ringRect.x, ringRect.y - PixelGUI.LineH, ringRect.width, PixelGUI.LineH),
                  _gait.Running ? "RUN" : "WALK", 0.7f);
    }

    void Label(Rect r, string text, float alpha)
    {
        var from = PixelGUI.Label;
        if (_label == null || _labelFrom != from)
        {
            _label = new GUIStyle(from) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            _labelFrom = from;
        }
        _label.normal.textColor = Fade(PixelGUI.Text, alpha);
        GUI.Label(r, text, _label);
    }

    static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);
}
