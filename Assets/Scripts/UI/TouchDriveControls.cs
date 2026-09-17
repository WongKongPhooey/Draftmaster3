using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

// On-screen steering and pedals for a phone or tablet: a steering strip under the left thumb, brake and
// throttle under the right, and a pause button at the top of the screen (a phone has no Esc, and its back
// gesture is easy to miss mid-race).
//
// Up only while it is the way the player is driving: on a touch device, sat in their own car, nothing modal
// open, and the pad not the device they touched last. Picking up a pad puts the controls away and prompts
// switch to its buttons (InputGlyphs); touching the screen brings them back. PlayerVehicleController reads
// Steer / Throttle / Brake while Active.
//
// Rules — who each finger belongs to and what it is pressing — are TouchDriveState in Draftmaster.Controls,
// where the EditMode tests read them. This reads the fingers and draws.
//
// To try it without a phone, open the Device Simulator (Window > General > Device Simulator) and play: the
// simulator reports a mobile platform and turns mouse clicks into touches.
[DefaultExecutionOrder(-900)]
public class TouchDriveControls : MonoBehaviour
{
    static TouchDriveControls _instance;
    static readonly TouchDriveState _state = new TouchDriveState();
    static TouchLayout _layout;

    readonly List<TouchPoint> _touches = new List<TouchPoint>();
    GUIStyle _label;
    GUIStyle _labelFrom;

    // The controls are up and the player's car should read them.
    public static bool Active { get; private set; }

    public static float Steer => Active ? _state.Steer : 0f;
    public static float Throttle => Active ? _state.Throttle : 0f;
    public static float Brake => Active ? _state.Brake : 0f;

    // How far up from the bottom of the screen the pedals reach, in screen pixels; 0 while the controls are
    // put away. The crew chief's headset button shares that corner and stands on top of the pedals while
    // they show.
    public static float PedalsTopFromBottom =>
        Active ? UnityEngine.Device.Screen.height - (_layout.brake.y - TouchLayout.Slop * _layout.unit) : 0f;

    // A device the player drives with their thumbs: a phone or tablet, or the editor's Device Simulator
    // pretending to be one.
    public static bool TouchPlatform =>
        UnityEngine.Device.Application.isMobilePlatform && Touchscreen.current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("TouchDriveControls");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TouchDriveControls>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        useGUILayout = false;   // every control is placed by hand, and this runs on every platform
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        Active = false;
        _state.Reset();
    }

    static bool ShouldShow =>
        TouchPlatform &&
        !InputGlyphs.UsingGamepad &&
        !PadInput.OnFoot &&
        !PadInput.ModalOpen &&
        PlayerVehicleController.Human != null;

    void Update()
    {
        if (!ShouldShow)
        {
            if (Active)
            {
                Active = false;
                _state.Reset();
            }
            return;
        }

        _layout = CurrentLayout();
        ReadTouches();
        _state.Update(_touches, _layout);
        Active = true;

        if (_state.PauseTapped) RacePauseMenu.TogglePause();
    }

    static TouchLayout CurrentLayout()
    {
        float w = UnityEngine.Device.Screen.width, h = UnityEngine.Device.Screen.height;
        // Screen.safeArea has its origin bottom-left; the layout is in IMGUI's top-left space.
        var sa = UnityEngine.Device.Screen.safeArea;
        if (sa.width <= 0f || sa.height <= 0f) sa = new Rect(0f, 0f, w, h);
        var safe = new TouchRect(sa.x, h - sa.yMax, sa.width, sa.height);
        return new TouchLayout(safe, TouchLayout.UnitFor(w, h));
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
        GUI.depth = -10;   // over the race HUD's panels

        DrawSteering();
        DrawPedal(_layout.brake, "BRAKE", _state.Brake > 0f, PixelGUI.Danger);
        DrawPedal(_layout.throttle, "GAS", _state.Throttle > 0f, PixelGUI.Confirm);
        DrawPause();
    }

    void DrawSteering()
    {
        float u = _layout.unit;
        var rest = _layout.steerRest;
        float travel = _layout.steerTravel;
        float knob = TouchLayout.KnobSize * u;

        // Held: the strip sits where the thumb came down and the knob follows it. Idle: it waits in the
        // corner, showing where to put a thumb.
        float cx, cy;
        if (_state.Steering) { cx = _state.SteerCentreX; cy = _state.SteerCentreY; }
        else { cx = rest.centerX; cy = rest.centerY; }

        var strip = new Rect(cx - travel - knob * 0.5f, cy - knob * 0.5f, travel * 2f + knob, knob);
        PixelGUI.Fill(strip, Fade(PixelGUI.PlateDeep, 0.55f));
        PixelGUI.Frame(strip, Fade(PixelGUI.Text, 0.45f));

        // The centre notch, so a player can see how far off straight they are holding it.
        PixelGUI.Fill(new Rect(cx - u * 0.5f, strip.y + 2f * u, u, strip.height - 4f * u), Fade(PixelGUI.TextDim, 0.6f));

        Label(new Rect(strip.x, strip.y, knob, knob), "<", 0.7f);
        Label(new Rect(strip.xMax - knob, strip.y, knob, knob), ">", 0.7f);

        float kx = cx + _state.Steer * travel - knob * 0.5f;
        var k = new Rect(kx, strip.y, knob, knob);
        PixelGUI.Fill(k, Fade(PixelGUI.Gold, _state.Steering ? 0.9f : 0.5f));
        PixelGUI.Frame(k, Fade(PixelGUI.Ink, 0.8f));

        if (!_state.Steering)
            Label(new Rect(strip.x, strip.y - PixelGUI.LineH, strip.width, PixelGUI.LineH), "STEER", 0.7f);
    }

    void DrawPedal(TouchRect r, string text, bool pressed, Color tint)
    {
        var rect = ToRect(r);
        PixelGUI.Fill(rect, pressed ? Fade(tint, 0.75f) : Fade(PixelGUI.PlateDeep, 0.45f));
        PixelGUI.Frame(rect, pressed ? Fade(PixelGUI.Text, 0.9f) : Fade(tint, 0.7f));
        Label(rect, text, pressed ? 1f : 0.8f);
    }

    void DrawPause()
    {
        var rect = ToRect(_layout.pause);
        PixelGUI.Fill(rect, Fade(PixelGUI.PlateDeep, 0.55f));
        PixelGUI.Frame(rect, Fade(PixelGUI.Text, 0.5f));

        // Two bars, drawn rather than typed: the pixel faces don't all carry a pause glyph.
        float u = _layout.unit;
        float barW = 3f * u, barH = rect.height - 10f * u;
        float y = rect.y + 5f * u;
        PixelGUI.Fill(new Rect(rect.center.x - barW - 1.5f * u, y, barW, barH), Fade(PixelGUI.Text, 0.85f));
        PixelGUI.Fill(new Rect(rect.center.x + 1.5f * u, y, barW, barH), Fade(PixelGUI.Text, 0.85f));
    }

    void Label(Rect r, string text, float alpha)
    {
        // A centred copy of the kit's label face, remade whenever the kit rebuilds its styles (scale change).
        var from = PixelGUI.Label;
        if (_label == null || _labelFrom != from)
        {
            _label = new GUIStyle(from) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            _labelFrom = from;
        }
        _label.normal.textColor = Fade(PixelGUI.Text, alpha);
        GUI.Label(r, text, _label);
    }

    static Rect ToRect(TouchRect r) => new Rect(r.x, r.y, r.width, r.height);

    static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);
}
