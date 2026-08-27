using UnityEngine;

// The in-race HUD from the Iron Oval design file's RACE HUD screen: position and speed as big Pixelify
// numerals with a hard shadow, the lap count in gold Silkscreen under them, and a tyre/draft meter plate
// on the right under the track map.
//
// It replaces three older readouts rather than sitting on top of them — the authored speedometer dial,
// the "P x / N" position panel and RaceDirector's own lap counter all stand down while this is running,
// so nothing is drawn twice. Turn `enabled` off and they come back.
//
// Everything is read live: RacePositionTracker for the order, LapTimingManager and RaceDirector for the
// lap, the player car's speed readout, and TireModel for wear. Draft is the closing-speed gauge the
// design shows in telemetry blue.
public class IronOvalRaceHUD : MonoBehaviour
{
    public static IronOvalRaceHUD Instance { get; private set; }

    [Tooltip("Draw the HUD. Off hands the screen back to the older readouts.")]
    public bool show = true;
    [Tooltip("Rebind to the player car / track this often (s).")]
    public float rebindSeconds = 1f;

    Transform _player;
    IVehicleSpeedReadout _speed;
    TireModel _tires;
    PlayerVehicleController _pvc;
    float _rebindTimer;
    float _shownMph;
    bool _standDownApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("IronOvalRaceHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<IronOvalRaceHUD>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        _rebindTimer -= Time.unscaledDeltaTime;
        if (_rebindTimer > 0f) return;
        _rebindTimer = Mathf.Max(0.25f, rebindSeconds);

        Rebind();
        if (show && _player != null) StandDownOlderReadouts();
    }

    void Rebind()
    {
        // The local player's car: a live PVC with no AI brain bolted on. Off the registry rather than a
        // scene search — on foot this rebind never latches, so it would otherwise scan the whole paddock
        // four times a second for the entire walk.
        if (_pvc == null || !_pvc.isActiveAndEnabled) _pvc = PlayerVehicleController.Human;

        _player = _pvc != null ? _pvc.transform : null;
        _speed = _pvc as IVehicleSpeedReadout;
        _tires = _pvc != null ? _pvc.GetComponent<TireModel>() : null;
    }

    // The older readouts covered the same three numbers in three different visual languages. This owns
    // them now, so they're switched off once rather than fought with every frame.
    void StandDownOlderReadouts()
    {
        if (_standDownApplied) return;
        _standDownApplied = true;

        var dial = FindFirstObjectByType<SpeedometerUI>();
        if (dial != null)
        {
            var canvas = dial.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
            else dial.gameObject.SetActive(false);
        }

        var tracker = RacePositionTracker.Instance;
        if (tracker != null) tracker.showHud = false;

        var director = RaceDirector.Instance;
        if (director != null) director.drawLapCounter = false;
    }

    void OnGUI()
    {
        if (!show || _player == null) return;
        if (RacePauseMenu.IsPaused || PhoneUI.IsOpen) return;

        DrawPositionBlock();
        DrawSpeedBlock();
        DrawMeters();
    }

    // ------------------------------------------------------------------ blocks

    void DrawPositionBlock()
    {
        var tracker = RacePositionTracker.Instance;
        int pos = tracker != null ? tracker.PlayerPosition : 0;
        int field = tracker != null ? tracker.FieldSize : 0;
        if (pos <= 0) return;

        float x = PixelGUI.Px(10f), y = PixelGUI.Px(8f);
        Shadowed(new Rect(x, y, PixelGUI.Px(120f), PixelGUI.Px(30f)), "P" + pos, PixelGUI.Display);

        var style = PixelGUI.Row;
        var prev = style.normal.textColor;
        style.normal.textColor = PixelGUI.TextDim;
        GUI.Label(new Rect(x + PixelGUI.Px(2f) + Measure(PixelGUI.Display, "P" + pos), y + PixelGUI.Px(14f),
                           PixelGUI.Px(60f), PixelGUI.DataLineH), "/" + Mathf.Max(field, pos), style);
        style.normal.textColor = prev;

        string lap = LapText();
        if (string.IsNullOrEmpty(lap)) return;
        var heading = PixelGUI.HeadingSmall;
        GUI.Label(new Rect(x + PixelGUI.Px(2f), y + PixelGUI.Px(32f), PixelGUI.Px(120f), PixelGUI.LineH),
                  lap, heading);
    }

    void DrawSpeedBlock()
    {
        if (_speed == null) return;

        float mph = _speed.SpeedMps * 2.237f;
        _shownMph = Mathf.Lerp(_shownMph, mph, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        string text = Mathf.RoundToInt(_shownMph).ToString();

        float x = PixelGUI.Px(10f), y = PixelGUI.Px(48f);
        Shadowed(new Rect(x, y, PixelGUI.Px(140f), PixelGUI.Px(30f)), text, PixelGUI.Display);

        var style = PixelGUI.Row;
        var prev = style.normal.textColor;
        style.normal.textColor = PixelGUI.TextDim;
        GUI.Label(new Rect(x + PixelGUI.Px(2f) + Measure(PixelGUI.Display, text), y + PixelGUI.Px(14f),
                           PixelGUI.Px(40f), PixelGUI.DataLineH), "MPH", style);
        style.normal.textColor = prev;
    }

    // Tyre life and draft, as the design's two cell rows: alarm red for the rubber, telemetry blue for
    // the tow. Sits on the right, under where the track map lives.
    void DrawMeters()
    {
        float w = PixelGUI.Px(92f);
        float h = PixelGUI.Px(12f) + 2f * (PixelGUI.LineH + PixelGUI.CellsHeight + PixelGUI.Px(4f));
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(96f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 6f);

        float cy = c.y;
        var label = PixelGUI.HeadingSmall;
        var prev = label.normal.textColor;

        label.normal.textColor = PixelGUI.Danger;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), "TIRE", label);
        cy += PixelGUI.LineH;
        float life = _tires != null ? 1f - Mathf.Clamp01(Mathf.Max(_tires.FrontWear, _tires.RearWear)) : 1f;
        PixelGUI.Cells(new Rect(c.x, cy, c.width, PixelGUI.CellsHeight), Mathf.RoundToInt(life * 10f), 10,
                       life > 0.66f ? PixelGUI.Confirm : life > 0.33f ? PixelGUI.Gold : PixelGUI.Danger);
        cy += PixelGUI.CellsHeight + PixelGUI.Px(4f);

        label.normal.textColor = PixelGUI.Info;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), "DRAFT", label);
        cy += PixelGUI.LineH;
        PixelGUI.Cells(new Rect(c.x, cy, c.width, PixelGUI.CellsHeight),
                       Mathf.RoundToInt(Draft01() * 10f), 10, PixelGUI.Info);

        label.normal.textColor = prev;
    }

    // ------------------------------------------------------------------ data

    string LapText()
    {
        var tracker = RacePositionTracker.Instance;
        if (tracker == null || _player == null) return "";
        int lap = tracker.LapOf(_player) + 1;

        var director = RaceDirector.Instance;
        int total = director != null ? director.raceLaps : 0;
        if (RaceWeekend.IsPracticeLike || total <= 0) return $"LAP {lap}";
        return $"LAP {Mathf.Min(lap, total)}/{total}";
    }

    // How much tow the player is getting, 0..1. Read off the car ahead's proximity in the running order
    // rather than a physics probe: the gauge is a driver aid, not telemetry.
    float Draft01()
    {
        var tracker = RacePositionTracker.Instance;
        if (tracker == null || _player == null) return 0f;

        var order = tracker.Order;
        RacePositionTracker.Entry me = null, ahead = null;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == null || order[i].tf != _player) continue;
            me = order[i];
            if (i > 0) ahead = order[i - 1];
            break;
        }
        if (me == null || ahead == null || ahead.tf == null) return 0f;

        float gap = Vector2.Distance(ahead.tf.position, _player.position);
        // Full bar within 6 m, nothing past 30 — roughly the span where a tow is worth having.
        return Mathf.Clamp01(Mathf.InverseLerp(30f, 6f, gap));
    }

    // ------------------------------------------------------------------ drawing helpers

    // The design's 3px hard shadow. Two labels, no blur — TMP's underlay and GUI's shadow both soften.
    static void Shadowed(Rect r, string text, GUIStyle style)
    {
        var prev = style.normal.textColor;
        style.normal.textColor = PixelGUI.Ink;
        GUI.Label(new Rect(r.x + PixelGUI.Px(2f), r.y + PixelGUI.Px(2f), r.width, r.height), text, style);
        style.normal.textColor = prev;
        GUI.Label(r, text, style);
    }

    static float Measure(GUIStyle style, string text) => style.CalcSize(new GUIContent(text)).x;
}
