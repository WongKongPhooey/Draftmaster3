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
// lap, the player car's speed readout, and TireModel for wear. Draft is the car's own slipstream tow
// (PlayerVehicleController.TowFactor), shown in the design's telemetry blue.
public class IronOvalRaceHUD : MonoBehaviour
{
    public static IronOvalRaceHUD Instance { get; private set; }

    [Tooltip("Draw the HUD. Off hands the screen back to the older readouts.")]
    public bool show = true;
    [Tooltip("Rebind to the player car / track this often (s).")]
    public float rebindSeconds = 1f;

    [Header("Caution flag")]
    [Tooltip("Side (design px) of the yellow flag box drawn in the top-right corner.")]
    public float cautionFlagSize = 28f;
    [Tooltip("Placeholder flag colour — swap for the flag sprite when it exists.")]
    public Color cautionFlagColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("Blink period (s) of the caution flag.")]
    public float cautionBlinkSeconds = 0.6f;

    [Header("Draft meter")]
    [Tooltip("How fast (1/s) the draft bar eases toward the car's tow. Higher is twitchier.")]
    public float draftEaseRate = 4f;

    Transform _player;
    IVehicleSpeedReadout _speed;
    TireModel _tires;
    PlayerVehicleController _pvc;
    float _rebindTimer;
    float _shownMph;
    float _shownDraft;
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
        // The tracker and director are re-told every rebind: GridSpawner creates the director after the
        // player car exists, and this HUD outlives scene loads, so a one-shot stand-down missed both and
        // left RaceDirector's lap box drawn under the position numerals.
        var tracker = RacePositionTracker.Instance;
        if (tracker != null) tracker.showHud = false;

        var director = RaceDirector.Instance;
        if (director != null) director.drawLapCounter = false;

        if (_standDownApplied) return;
        _standDownApplied = true;

        var dial = FindFirstObjectByType<SpeedometerUI>();
        if (dial != null)
        {
            var canvas = dial.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
            else dial.gameObject.SetActive(false);
        }
    }

    // True while this HUD is drawing its own lap count, so RaceDirector can hold its box back even in the
    // frames before the next rebind reaches it.
    public static bool DrawsLapCount =>
        Instance != null && Instance.isActiveAndEnabled && Instance.show && Instance._player != null;

    void OnGUI()
    {
        if (!show || _player == null) return;
        if (RacePauseMenu.IsPaused || PhoneUI.IsOpen) return;

        DrawPositionBlock();
        DrawSpeedBlock();
        DrawMeters();
        DrawCautionFlag();
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
        // Sized from what goes in it rather than from a round number: a ten-cell row is CellsWidth(10)
        // across and the plate's frame and margin take Px(10) off each side, so a Px(92) plate was always
        // narrower than the rows drawn inside it.
        float inset = PixelGUI.Px(4f) + PixelGUI.Px(6f);
        float w = PixelGUI.CellsWidth(10) + inset * 2f;
        float h = inset * 2f + 2f * PixelGUI.LineH + 2f * PixelGUI.CellsHeight + PixelGUI.Px(6f);
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(96f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 6f);

        float cy = c.y;
        var label = PixelGUI.HeadingSmall;
        var prev = label.normal.textColor;
        // The rows are the authored ten-cell width or whatever the plate has room for, whichever is
        // smaller — never simply the content box, which would stretch a row past its own boxes the day
        // the plate is sized off anything but the row.
        float rowW = Mathf.Min(c.width, PixelGUI.CellsWidth(10));

        label.normal.textColor = PixelGUI.Danger;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), "Tire", label);
        cy += PixelGUI.LineH;
        float life = _tires != null ? 1f - Mathf.Clamp01(Mathf.Max(_tires.FrontWear, _tires.RearWear)) : 1f;
        PixelGUI.Cells(new Rect(c.x, cy, rowW, PixelGUI.CellsHeight), Mathf.RoundToInt(life * 10f), 10,
                       life > 0.66f ? PixelGUI.Confirm : life > 0.33f ? PixelGUI.Gold : PixelGUI.Danger);
        cy += PixelGUI.CellsHeight + PixelGUI.Px(4f);

        label.normal.textColor = PixelGUI.Info;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), "Draft", label);
        cy += PixelGUI.LineH;
        PixelGUI.Cells(new Rect(c.x, cy, rowW, PixelGUI.CellsHeight),
                       Mathf.RoundToInt(Draft01() * 10f), 10, PixelGUI.Info);

        label.normal.textColor = prev;
    }

    // Yellow flag, top right: a stopped car, or one that just hit the wall, within CautionWatch's look-ahead. Placeholder
    // art — a plain yellow box on the ink shadow the rest of the HUD uses — until the flag sprite exists.
    // It blinks, because a static square in the corner reads as part of the furniture rather than as a
    // warning that just came out.
    void DrawCautionFlag()
    {
        var watch = CautionWatch.Instance;
        // Up for a stopped car ahead, and for the whole of a full-course caution (FullCourseCaution).
        if (!RaceStart.IsCaution && (watch == null || !watch.CautionAhead)) return;

        float size = PixelGUI.Px(cautionFlagSize);
        float x = Screen.width - size - PixelGUI.Px(8f);
        float y = PixelGUI.Px(8f);

        float b = PixelGUI.Px(1f);
        PixelGUI.Fill(new Rect(x - b, y - b, size + b * 2f, size + b * 2f), PixelGUI.Ink);

        var c = cautionFlagColor;
        // Two-thirds on, one-third off: an unmistakable blink that still leaves the flag up most of the time.
        if (Mathf.Repeat(Time.unscaledTime, cautionBlinkSeconds) > cautionBlinkSeconds * 0.66f) c.a *= 0.25f;
        PixelGUI.Fill(new Rect(x, y, size, size), c);
    }

    // ------------------------------------------------------------------ data

    string LapText()
    {
        var tracker = RacePositionTracker.Instance;
        if (tracker == null || _player == null) return "";
        // A car that starts short of the line is on lap -1 until it crosses it (RaceLaps.FirstLap).
        int lap = Mathf.Max(1, tracker.LapOf(_player) + 1);

        var director = RaceDirector.Instance;
        int total = director != null ? director.raceLaps : 0;
        if (RaceWeekend.IsPracticeLike || total <= 0) return $"Lap {lap}";
        return $"Lap {Mathf.Min(lap, total)}/{total}";
    }

    // How much tow the player is getting, 0..1 — the same TowFactor the car's physics runs on. That is
    // already gated on speed (nothing below VehicleInfo.draftingMinSpeed less DraftAero.SpeedRampMph, full
    // from draftingMinSpeed up), needs the car ahead to be in our lane, and ignores pitted cars. The old
    // gauge read straight-line distance to the next car in the running order at any speed, so trundling
    // down the pit lane past parked cars made it pump up and down. Eased so a single physics step's
    // wobble doesn't flick a cell on and off.
    float Draft01()
    {
        float tow = _pvc != null && _pvc.isActiveAndEnabled ? Mathf.Clamp01(_pvc.TowFactor) : 0f;
        // OnGUI runs more than once a frame; ease on the repaint pass only so the rate holds.
        if (Event.current.type == EventType.Repaint)
            _shownDraft = Mathf.Lerp(_shownDraft, tow, 1f - Mathf.Exp(-draftEaseRate * Time.unscaledDeltaTime));
        return _shownDraft;
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
