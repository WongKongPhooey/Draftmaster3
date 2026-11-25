using UnityEngine;
using RevDialScale = Draftmaster.Sim.RevDialScale;

// The in-race HUD from the Iron Oval design file's RACE HUD screen: position ("POS 7 /40") and lap ("LAP 12 /200")
// stacked top left in big numerals with a hard shadow and a dim suffix, a rev counter scaled to the car's rev range
// bottom right (with road speed under the hub), and a tyre/draft/fuel meter plate on the right under the track map.
//
// It replaces three older readouts rather than sitting on top of them — the authored speedometer dial,
// the "P x / N" position panel and RaceDirector's own lap counter all stand down while this is running,
// so nothing is drawn twice. Turn `enabled` off and they come back.
//
// Everything is read live: RacePositionTracker for the order, LapTimingManager and RaceDirector for the
// lap, the player car's speed readout, TireModel for wear and FuelTank for the tank. Draft is the car's own
// slipstream tow (PlayerVehicleController.TowFactor), shown in the design's telemetry blue.
public class IronOvalRaceHUD : MonoBehaviour
{
    public static IronOvalRaceHUD Instance { get; private set; }

    [Tooltip("Draw the HUD. Off hands the screen back to the older readouts.")]
    public bool show = true;
    [Tooltip("Rebind to the player car / track this often (s).")]
    public float rebindSeconds = 1f;

    [Header("Meters")]
    [Tooltip("Desktop only: the tyre/draft/fuel/temp plate is drawn one kit step smaller than the rest of the HUD " +
             "(3x -> 2x at 1080p, 4x -> 3x at 1440p), never below this many screen pixels per kit pixel. A phone " +
             "keeps the full size, which is what reads at arm's length; on a monitor it filled the right side.")]
    public int desktopMeterMinScale = 2;

    // Where the meter plate and the speed readout ended up on screen the last time they were drawn (screen
    // pixels; the plate's bottom edge from the top, the readout's top edge from the top). TrackMiniMap fits
    // itself between the two. 0 = not drawn.
    public static float MetersBottom { get; private set; }
    public static float SpeedTop { get; private set; }

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
    SplineDriver _spline;
    TireModel _tires;
    FuelTank _fuel;
    EngineGearbox _gearbox;
    PlayerVehicleController _pvc;
    float _rebindTimer;
    float _shownMph;
    float _shownRpm;
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
        // The local player's car, driven by them or by Auto for them (PlayerVehicleController.Local). Human alone
        // dropped the car the moment Auto's AI input driver went on it: once the safety car switched its controller
        // off (formation lap, a caution) the HUD let go and never found it again, so POS, LAP and MPH were gone for
        // the rest of the Auto run. Off the registry rather than a scene search — on foot this rebind never latches,
        // so it would otherwise scan the whole paddock four times a second for the entire walk.
        if (_pvc == null || !_pvc.isActiveAndEnabled) _pvc = PlayerVehicleController.Local;

        _player = _pvc != null ? _pvc.transform : null;
        _spline = _pvc != null ? _pvc.GetComponent<SplineDriver>() : null;
        _tires = _pvc != null ? _pvc.GetComponent<TireModel>() : null;
        _fuel = _pvc != null ? _pvc.GetComponent<FuelTank>() : null;
        _gearbox = _pvc != null ? _pvc.GetComponent<EngineGearbox>() : null;
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
        if (!show || _player == null) { MetersBottom = SpeedTop = 0f; return; }
        if (RacePauseMenu.IsPaused || PhoneUI.IsOpen) return;

        DrawPositionBlock();
        DrawLapBlock();
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

        float numeralX = PixelGUI.Px(10f) + LabelColumnW;
        SmallLabel(PixelGUI.Px(10f), PixelGUI.Px(8f), "POS");
        NumeralWithSuffix(numeralX, PixelGUI.Px(8f), pos.ToString(), "/" + Mathf.Max(field, pos));
    }

    // The lap, in the position block's own layout directly under it: small "LAP", the lap in the big numerals,
    // "/200" dim beside it. Practice and open-ended sessions have no total, so just the lap.
    void DrawLapBlock()
    {
        int lap = CurrentLap(out int total);
        if (lap <= 0) return;
        float numeralX = PixelGUI.Px(10f) + LabelColumnW;
        SmallLabel(PixelGUI.Px(10f), PixelGUI.Px(44f), "LAP");
        NumeralWithSuffix(numeralX, PixelGUI.Px(44f), lap.ToString(), total > 0 ? "/" + total : null);
    }

    // POS and LAP share one column, as wide as the wider of the two plus a gap, so the two numbers start at the
    // same x and read as a column.
    static float LabelColumnW =>
        Mathf.Max(Measure(PixelGUI.HeadingSmall, "POS"), Measure(PixelGUI.HeadingSmall, "LAP")) + PixelGUI.Px(3f);

    // A small Silkscreen caption beside a numeral, its cap tops level with the numeral's. Drawn at the numeral's
    // own rect y: measured off a 1080p frame, the big monogram digits' tops sit Px(4) into their rect and
    // Silkscreen's caps Px(3.7) into theirs, so the same y puts them within a screen pixel of each other.
    static void SmallLabel(float x, float y, string text)
    {
        var style = PixelGUI.HeadingSmall;
        var prev = style.normal.textColor;
        style.normal.textColor = PixelGUI.TextDim;
        GUI.Label(new Rect(x, y, Measure(style, text) + PixelGUI.Px(2f), PixelGUI.LineH), text, style);
        style.normal.textColor = prev;
    }

    // ------------------------------------------------------------------ rev counter

    // A rev counter in the bottom-right corner, drawn in code rather than from a sprite so its numbering fits the
    // car: Draftmaster.Sim.RevDialScale turns the car's own VehicleInfo rev range (maxRpm, shiftUpRpm) into
    // numerals in thousands, a round top end and a red band, and the face is rasterised from that whenever the
    // range changes — a Cup car reads 0-10, a low-revving engine 0-3.5 in halves. The face and needle are drawn
    // into textures at the kit's own resolution (104 px landscape, 128 upright) and blown up by PixelGUI's whole
    // number scale with point filtering, so every edge lands on a screen pixel; the numerals are the kit's
    // Silkscreen face, sharp at any scale. Road speed stays as the readout under the hub (pit-lane limits are
    // in mph). A car with no gearbox falls back to the plain numeral speed readout.
    static readonly Color DialInk = new Color32(0x05, 0x06, 0x0A, 0xFF);        // hard shadow
    static readonly Color DialReadoutInk = new Color32(0x12, 0x14, 0x1C, 0xFF); // readout on the cream face
    static readonly Color32 DialBezel = new Color32(0x1E, 0x22, 0x30, 0xFF);
    static readonly Color32 DialRim = new Color32(0x8A, 0x90, 0xA0, 0xFF);
    static readonly Color32 DialFace = new Color32(0xF0, 0xE6, 0xCE, 0xFF);
    static readonly Color32 DialTick = new Color32(0x12, 0x14, 0x1C, 0xFF);
    static readonly Color32 DialRed = new Color32(0xD8, 0x34, 0x2B, 0xFF);
    static readonly Color32 DialNeedle = new Color32(0xE8, 0x45, 0x2C, 0xFF);
    static readonly Color32 DialHub = new Color32(0x1E, 0x22, 0x30, 0xFF);
    static readonly Color32 DialClear = new Color32(0, 0, 0, 0);

    // How far (640x360 UI px) the corner buttons — headset, TV, Auto, team swaps — step left of where they were
    // authored (92 px in from the right) so they clear the 104 px dial at 12 px in, with a 6 px gap. Added at
    // placement rather than to the serialized corners, which RaceScene's saved values would override.
    public const float DialCornerClearance = 32f;

    static Font _readoutFont;
    static GUIStyle _readoutStyle, _numeralStyle;
    static int _readoutPt, _numeralPt;

    // Face: rebuilt when the dial size or the car's rev range changes. Needle: rebuilt when its whole-degree
    // angle changes, on a clear canvas the same size as the face.
    static Texture2D _faceTex, _needleTex;
    static Color32[] _needlePixels;
    static int _faceKit;
    static RevDialScale _faceScale;
    static int _needleDeg = int.MinValue;

    // Speed and revs in the bottom-right corner, standing on top of the touch pedals on a phone as the headset does.
    void DrawSpeedBlock()
    {
        if (_pvc == null) return;

        float mph = SpeedMps() * 2.237f;
        _shownMph = Mathf.Lerp(_shownMph, mph, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

        var info = _gearbox != null ? (_gearbox.vehicleInfo != null ? _gearbox.vehicleInfo : _pvc.vehicleInfo) : null;
        if (info == null)
        {
            // No engine to read: the plain numeral readout, so speed never disappears.
            string text = Mathf.RoundToInt(_shownMph).ToString();
            float w = Measure(PixelGUI.Display, text) + PixelGUI.Px(2f) + Measure(PixelGUI.Row, "MPH");
            float ty = Screen.height - PixelGUI.Px(8f) - BlockH - TouchDriveControls.PedalsTopFromBottom;
            SpeedTop = ty;
            NumeralWithSuffix(Screen.width - PixelGUI.Px(8f) - w, ty, text, "MPH");
            return;
        }

        _shownRpm = Mathf.Lerp(_shownRpm, _gearbox.Rpm, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));

        bool upright = Screen.height > Screen.width;
        int kit = upright ? 128 : 104;
        var scale = RevDialScale.For(info.maxRpm, info.shiftUpRpm);
        EnsureFace(kit, scale);

        float side = PixelGUI.Px(kit);
        float x = Screen.width - PixelGUI.Px(upright ? 16f : 12f) - side;
        float y = Screen.height - PixelGUI.Px(upright ? 12f : 10f) - side - TouchDriveControls.PedalsTopFromBottom;
        // Whole screen pixels, so a kit pixel never straddles two.
        x = Mathf.Round(x); y = Mathf.Round(y);
        SpeedTop = y;

        var rect = new Rect(x, y, side, side);
        var prevColour = GUI.color;

        // 3 px ink drop shadow: the face's own silhouette in ink, offset down-right.
        GUI.color = DialInk;
        GUI.DrawTexture(new Rect(x + PixelGUI.Px(3f), y + PixelGUI.Px(3f), side, side), _faceTex);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, _faceTex);

        DrawNumerals(rect, kit, scale);
        DrawReadout(rect, kit, Mathf.RoundToInt(_shownMph).ToString());

        EnsureNeedle(kit, Mathf.RoundToInt(scale.Degrees(_shownRpm)));
        GUI.DrawTexture(rect, _needleTex);

        GUI.color = prevColour;
    }

    // Dial geometry in kit px, all from the 104 px design: bezel 3, rim 1, red band 4 wide set 5 in, ticks
    // hanging off the inside of the band, numerals on a ring inside the ticks.
    static float DialK(int kit) => kit / 104f;
    static float RimOuter(int kit) => kit * 0.5f;
    static float TickOuter(int kit) => RimOuter(kit) - 5f * DialK(kit);
    static float NumeralRing(int kit) => TickOuter(kit) - 14f * DialK(kit);

    static void EnsureFace(int kit, RevDialScale scale)
    {
        if (_faceTex != null && _faceKit == kit && SameScale(_faceScale, scale)) return;
        _faceKit = kit;
        _faceScale = scale;
        if (_faceTex == null || _faceTex.width != kit)
        {
            if (_faceTex != null) Destroy(_faceTex);
            _faceTex = NewDialTexture(kit, "RevCounterFace");
        }

        float k = DialK(kit);
        float c = kit * 0.5f;
        float rOut = RimOuter(kit);
        float rBezel = rOut - 3f * k;
        float rRim = rBezel - 1f * k;
        float tickOut = TickOuter(kit);
        float bandIn = tickOut - 4f * k;
        float redFromDeg = scale.Degrees(scale.RedFromRpm);
        float redToDeg = scale.Degrees(scale.TopRpm);

        var px = new Color32[kit * kit];
        for (int row = 0; row < kit; row++)
        {
            float dy = c - (kit - 1 - row + 0.5f);   // up positive; texture row 0 is the bottom
            for (int col = 0; col < kit; col++)
            {
                float dx = col + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 col32;
                if (r > rOut) col32 = DialClear;
                else if (r > rBezel) col32 = DialBezel;
                else if (r > rRim) col32 = DialRim;
                else
                {
                    col32 = DialFace;
                    float deg = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // clockwise from up
                    if (r >= bandIn && r <= tickOut && deg >= redFromDeg && deg <= redToDeg) col32 = DialRed;
                }
                px[row * kit + col] = col32;
            }
        }

        // Ticks, stamped as pixel lines rather than tested per pixel: a thin ray sampled at pixel centres breaks
        // into dots on the diagonals. Every minor step; majors twice as long and two pixels wide.
        int tickCount = Mathf.RoundToInt(scale.TopRpm / scale.MinorStep) + 1;
        for (int i = 0; i < tickCount; i++)
        {
            float rpm = i * scale.MinorStep;
            bool major = Mathf.Abs(rpm / scale.MajorStep - Mathf.Round(rpm / scale.MajorStep)) < 1e-3f;
            float a = scale.Degrees(rpm) * Mathf.Deg2Rad;
            float s = Mathf.Sin(a), co = Mathf.Cos(a);
            float rIn = tickOut - (major ? 8f : 4f) * k;
            int width = Mathf.Max(1, Mathf.RoundToInt((major ? 2f : 1f) * k));
            for (float along = rIn; along <= tickOut - 0.25f; along += 0.25f)
                for (int wv = 0; wv < width; wv++)
                {
                    float off = wv - (width - 1) * 0.5f;
                    // Point in texture space (y up): along the ray, nudged sideways for width.
                    float fx = c + along * s + off * co;
                    float fy = c + along * co - off * s;
                    int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                    if (ix < 0 || iy < 0 || ix >= kit || iy >= kit) continue;
                    px[iy * kit + ix] = DialTick;
                }
        }
        _faceTex.SetPixels32(px);
        _faceTex.Apply(false);
    }

    static void EnsureNeedle(int kit, int deg)
    {
        if (_needleTex != null && _needleTex.width == kit && _needleDeg == deg) return;
        _needleDeg = deg;
        if (_needleTex == null || _needleTex.width != kit)
        {
            if (_needleTex != null) Destroy(_needleTex);
            _needleTex = NewDialTexture(kit, "RevCounterNeedle");
            _needlePixels = new Color32[kit * kit];
        }

        float k = DialK(kit);
        float c = kit * 0.5f;
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), co = Mathf.Cos(a);
        float tip = TickOuter(kit) - 1f * k;
        float tail = -7f * k;
        float halfW = 1.4f * k;
        float hub = 5f * k;
        float hubDot = 1.6f * k;

        for (int row = 0; row < kit; row++)
        {
            float dy = c - (kit - 1 - row + 0.5f);
            for (int col = 0; col < kit; col++)
            {
                float dx = col + 0.5f - c;
                float r2 = dx * dx + dy * dy;
                Color32 col32 = DialClear;
                float along = dx * s + dy * co;
                float perp = Mathf.Abs(dx * co - dy * s);
                // Tapers from full width at the hub to one pixel at the tip.
                float w = Mathf.Lerp(halfW, 0.75f, Mathf.Clamp01(along / tip));
                if (along >= tail && along <= tip && perp <= w) col32 = DialNeedle;
                if (r2 <= hub * hub) col32 = DialHub;
                if (r2 <= hubDot * hubDot) col32 = DialRim;
                _needlePixels[row * kit + col] = col32;
            }
        }
        _needleTex.SetPixels32(_needlePixels);
        _needleTex.Apply(false);
    }

    static bool SameScale(RevDialScale a, RevDialScale b) =>
        a.TopRpm == b.TopRpm && a.MajorStep == b.MajorStep && a.MinorStep == b.MinorStep && a.RedFromRpm == b.RedFromRpm;

    static Texture2D NewDialTexture(int kit, string name) =>
        new Texture2D(kit, kit, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };

    // The numerals, generated from the scale each frame: thousands on a ring inside the ticks, red from the
    // redline up, and "x1000" above the hub so the units are never a guess.
    static void DrawNumerals(Rect dial, int kit, RevDialScale scale)
    {
        int pt = PixelGUI.HeadingSmall.fontSize;
        if (_numeralStyle == null || _numeralPt != pt)
        {
            _numeralPt = pt;
            _numeralStyle = new GUIStyle(PixelGUI.HeadingSmall)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                richText = false,
            };
        }

        float px = PixelGUI.Scale;
        float c = kit * 0.5f;
        float ring = NumeralRing(kit);
        float h = PixelGUI.LineH;
        float w = PixelGUI.Px(24f);
        for (int i = 0; i < scale.MajorCount; i++)
        {
            float rpm = i * scale.MajorStep;
            float a = scale.Degrees(rpm) * Mathf.Deg2Rad;
            // Centre on a whole kit pixel, so every numeral sits on the same pixel grid as the face.
            float kx = Mathf.Round(c + ring * Mathf.Sin(a));
            float ky = Mathf.Round(c - ring * Mathf.Cos(a));
            _numeralStyle.normal.textColor = rpm >= scale.RedFromRpm - 1f ? (Color)DialRed : DialReadoutInk;
            GUI.Label(new Rect(dial.x + kx * px - w * 0.5f, dial.y + ky * px - h * 0.5f, w, h),
                      scale.Label(rpm), _numeralStyle);
        }

        _numeralStyle.normal.textColor = new Color(DialReadoutInk.r, DialReadoutInk.g, DialReadoutInk.b, 0.6f);
        float capY = Mathf.Round(kit * 0.36f);
        GUI.Label(new Rect(dial.x, dial.y + capY * px - h * 0.5f, dial.width, h), "x1000", _numeralStyle);
    }

    static void DrawReadout(Rect dial, float dialKit, string text)
    {
        if (_readoutFont == null) _readoutFont = Resources.Load<Font>("Fonts/PixelifySans-Bold");
        // Pixelify Sans is drawn on a 20 px cell: anything else resamples it soft. Nearest whole cell to the
        // design's size, never less than one.
        int pt = Mathf.Max(1, Mathf.RoundToInt(14f * dialKit / 100f * PixelGUI.Scale / 20f)) * 20;
        if (_readoutStyle == null || _readoutPt != pt)
        {
            _readoutPt = pt;
            _readoutStyle = new GUIStyle
            {
                font = _readoutFont != null ? _readoutFont : PixelGUI.Display.font,
                fontSize = pt,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
            };
            _readoutStyle.normal.textColor = DialReadoutInk;
        }
        float h = pt * 1.4f;
        GUI.Label(new Rect(dial.x, dial.y + dial.height * 0.74f - h * 0.5f, dial.width, h), text, _readoutStyle);

        // "MPH" small under the readout, in the numerals' face.
        if (_numeralStyle == null) return;
        _numeralStyle.normal.textColor = new Color(DialReadoutInk.r, DialReadoutInk.g, DialReadoutInk.b, 0.6f);
        float lh = PixelGUI.LineH;
        float my = Mathf.Round(dialKit * 0.88f) * PixelGUI.Scale;
        GUI.Label(new Rect(dial.x, dial.y + my - lh * 0.5f, dial.width, lh), "MPH", _numeralStyle);
    }

    // Height of a numeral block: the big numerals' 30 px rect, with the dim suffix sitting low beside them.
    static float BlockH => PixelGUI.Px(14f) + PixelGUI.DataLineH;

    // The HUD's one readout layout: big shadowed numerals, then a dim suffix sitting low beside them.
    static void NumeralWithSuffix(float x, float y, string numeral, string suffix)
    {
        float bigW = Measure(PixelGUI.Display, numeral);
        Shadowed(new Rect(x, y, bigW + PixelGUI.Px(4f), PixelGUI.Px(30f)), numeral, PixelGUI.Display);
        if (string.IsNullOrEmpty(suffix)) return;

        var style = PixelGUI.Row;
        var prev = style.normal.textColor;
        style.normal.textColor = PixelGUI.TextDim;
        GUI.Label(new Rect(x + PixelGUI.Px(2f) + bigW, y + PixelGUI.Px(14f),
                           Measure(style, suffix) + PixelGUI.Px(4f), PixelGUI.DataLineH), suffix, style);
        style.normal.textColor = prev;
    }

    // Tyre life, draft, fuel and engine temperature, as the design's cell rows: alarm red for the rubber, telemetry blue for
    // the tow, gold for the tank. Sits on the right, under where the track map lives.
    void DrawMeters()
    {
        // Sized from what goes in it rather than from a round number: a ten-cell row is CellsWidth(10)
        // across and the plate's frame and margin take Px(10) off each side, so a Px(92) plate was always
        // narrower than the rows drawn inside it.
        float inset = PixelGUI.Px(4f) + PixelGUI.Px(6f);
        float w = PixelGUI.CellsWidth(10) + inset * 2f;
        // Four label + row sections, a Px(4) gap between each, and the same Px(2) of slack at the foot.
        float h = inset * 2f + 4f * PixelGUI.LineH + 4f * PixelGUI.CellsHeight + PixelGUI.Px(14f);
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(96f);

        // Smaller on a monitor, pinned at its top-right corner so it stays tucked under the caution flag.
        float k = MeterScale();
        var savedMatrix = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), new Vector2(x + w, y));
        MetersBottom = y + h * k;

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
        cy += PixelGUI.CellsHeight + PixelGUI.Px(4f);

        // Fuel left in the tank. A car without a FuelTank reads full, as a car without a TireModel reads
        // fresh rubber.
        label.normal.textColor = PixelGUI.Gold;
        // The litres beside the word: a practice load (CarSetup, 1-20 L) is only a cell or two of the 100 L tank,
        // and the number is what visibly counts down.
        string fuelCaption = _fuel != null ? $"Fuel {Mathf.CeilToInt(_fuel.fuelLitres)} L" : "Fuel";
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), fuelCaption, label);
        cy += PixelGUI.LineH;
        float fuel = _fuel != null ? _fuel.Fraction : 1f;
        // A cell stays lit while any of its tenth is left, so the last one goes out when the tank is dry.
        int fuelCells = Mathf.Clamp(Mathf.CeilToInt(fuel * 10f - 1e-3f), 0, 10);
        // Alarm red: the last fifth of a race tank; in practice and qualifying, where a load is a few litres of
        // the 100 L tank from the start, the last 3 L.
        bool fuelLow = _fuel != null && !_fuel.Planned ? _fuel.fuelLitres <= 3f : fuel <= 0.2f;
        PixelGUI.Cells(new Rect(c.x, cy, rowW, PixelGUI.CellsHeight), fuelCells, 10,
                       fuelLow ? PixelGUI.Danger : PixelGUI.Gold);
        cy += PixelGUI.CellsHeight + PixelGUI.Px(4f);

        // Engine temperature (EngineTemp): three bars idling in the box (already warm), climbing with how hard the
        // engine works, full at MaxC. Amber once it runs hot - back off the bumper or find clean air - and alarm red
        // when it is losing power. Pinned full it flashes: the throttle is now counting down to a blown engine
        // (EngineTemp.BlowSeconds). Blown, the row says so.
        bool live = _pvc != null && _pvc.isActiveAndEnabled;
        float tempC = live ? _pvc.EngineTempC : Draftmaster.Sim.EngineTemp.NormalC;
        bool blown = _pvc != null && _pvc.EngineBlown;
        bool pinned = !blown && tempC >= Draftmaster.Sim.EngineTemp.MaxC - 0.01f;
        var tempColour = blown || tempC >= Draftmaster.Sim.EngineTemp.OverheatC ? PixelGUI.Danger
                       : tempC >= Draftmaster.Sim.EngineTemp.HotC ? PixelGUI.Gold : PixelGUI.Confirm;
        bool flashOff = pinned && Mathf.Repeat(Time.unscaledTime, 0.4f) >= 0.2f;
        label.normal.textColor = tempColour;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), blown ? "Blown" : "Temp", label);
        cy += PixelGUI.LineH;
        int tempCells = blown ? 10 : Mathf.RoundToInt(Draftmaster.Sim.EngineTemp.Gauge01(tempC) * 10f);
        PixelGUI.Cells(new Rect(c.x, cy, rowW, PixelGUI.CellsHeight), flashOff ? 0 : tempCells, 10, tempColour);

        label.normal.textColor = prev;
        GUI.matrix = savedMatrix;
    }

    // Fraction of the HUD's size the meter plate is drawn at: 1 on a phone; on a desktop one whole kit step
    // down, so the pixel type lands on a whole screen-pixel scale (2/3 of 3x is exactly 2x) and stays crisp.
    float MeterScale()
    {
        if (PixelGUI.Handheld) return 1f;
        int full = PixelGUI.Scale;
        int small = Mathf.Max(desktopMeterMinScale, full - 1);
        return small >= full ? 1f : (float)small / full;
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

    // The player's lap, 1-based, and the race distance (0 in practice or when there isn't one). 0 = no lap.
    int CurrentLap(out int total)
    {
        total = 0;
        var tracker = RacePositionTracker.Instance;
        if (tracker == null || _player == null) return 0;
        // A car that starts short of the line is on lap -1 until it crosses it (RaceLaps.FirstLap).
        int lap = Mathf.Max(1, tracker.LapOf(_player) + 1);

        var director = RaceDirector.Instance;
        int laps = director != null ? director.raceLaps : 0;
        if (RaceWeekend.IsPracticeLike || laps <= 0) return lap;
        total = laps;
        return Mathf.Min(lap, total);
    }

    // The car's speed from whatever is moving it. On Auto behind the safety car FormationController switches the
    // controller off and the SplineDriver writes the transform, and a switched-off controller's speed stays frozen
    // at the moment it stopped — so the spline's own speed is read then (as DrivingCameraFeel does).
    float SpeedMps()
    {
        if (_pvc.isActiveAndEnabled) return _pvc.SpeedMps;
        if (_spline != null && _spline.isActiveAndEnabled && !_spline.externalMotionController) return _spline.SpeedMps;
        return _pvc.SpeedMps;
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
