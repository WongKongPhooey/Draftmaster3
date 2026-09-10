using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// What that hour did. Shown after every completed activity: the headline the weekend will remember it by,
// then one line per meter that actually moved, then back to the sheet.
//
// Only non-zero deltas are listed. A press conference that bought press standing at the cost of the
// sponsor's afternoon shows exactly those two lines and nothing else, so the trade is legible instead of
// buried in five rows of zeroes.
public class WeekendResultCard : MonoBehaviour
{
    public static WeekendResultCard Instance { get; private set; }
    public static bool IsOpen => Instance != null;

    // How long an in-world card sits there before it takes itself away. Long enough to read the headline
    // and the two lines that moved, short enough that the player is not stood in the paddock waiting for a
    // panel to finish having its say.
    const float InWorldSeconds = 7f;

    WeekendActivity _activity;
    WeekendOutcome _outcome;
    float _openedAt;

    // An obligation done out in the paddock reports back without stopping the world: no scrim, no frozen
    // clock, no jump back to the sheet. The player is stood in front of the person they just talked to, and
    // the weekend carries on around them.
    bool _inWorld;

    // This card's own entry on the modal stack, and whether it has already handed over. Both are tracked
    // per card rather than inferred from Instance: Destroy() is deferred to the end of the frame, so a card
    // is still alive — and still drawing — after the next one has taken Instance off it.
    bool _pushed;
    bool _dismissed;

    // The card is drawn one integer step below the kit's display scale.
    //
    // PixelGUI.Scale steps in whole numbers off the screen height, so a card authored against the 2x skin
    // of a 720p window becomes half again as big the moment the window is 1080p: a 340-unit card is 680px
    // there and 1020px here, over half the width of the screen, with the headline set in 48px type. That
    // is the right size for a menu the player is sat reading and much too big for a notice that appears
    // over someone's shoulder while they are stood in the paddock.
    //
    // Whole steps only, for the same reason PixelGUI floors its own scale: a pixel face resampled to a
    // fraction of its cell loses its stems.
    static int CardScale => Mathf.Max(1, PixelGUI.Scale - 1);

    // A pixel measurement at the card's scale rather than the kit's.
    static float Px(float baseline) => baseline * CardScale;

    static GUIStyle _heading, _body, _data, _dataDim;
    static int _stylesAtKitScale = -1, _stylesAtCardScale = -1;

    // The kit's own styles with their type stepped down to match. Cloned rather than rebuilt from the
    // theme, so the face, colour, alignment and wrapping all stay whatever the kit says they are and only
    // the size moves. Kit sizes are always a whole multiple of PixelGUI.Scale, so the division is exact.
    static void EnsureStyles()
    {
        int kit = Mathf.Max(1, PixelGUI.Scale), card = CardScale;
        if (_heading != null && _stylesAtKitScale == kit && _stylesAtCardScale == card) return;
        _stylesAtKitScale = kit;
        _stylesAtCardScale = card;

        GUIStyle Down(GUIStyle from) =>
            new GUIStyle(from) { fontSize = Mathf.Max(8, from.fontSize * card / kit) };

        _heading = Down(PixelGUI.Heading);
        // The activity title is one line in a band, not a paragraph: centred in the band rather than
        // hung from its top, and never wrapped. The kit's heading is set up for a section header with
        // room under it, and left as it came the title sat high in the band with its descenders cut.
        _heading.alignment = TextAnchor.MiddleLeft;
        _heading.wordWrap = false;

        _body = Down(PixelGUI.Body);
        _data = Down(PixelGUI.Data);
        _dataDim = Down(PixelGUI.DataDim);
    }

    public static void Show(WeekendActivity a, WeekendOutcome o, bool inWorld = false)
    {
        if (Instance != null) Destroy(Instance.gameObject);

        var go = new GameObject("WeekendResultCard");
        DontDestroyOnLoad(go);
        var card = go.AddComponent<WeekendResultCard>();
        card._activity = a;
        card._outcome = o;
        card._inWorld = inWorld;
    }

    void OnEnable()
    {
        Instance = this;
        _openedAt = Time.unscaledTime;
        if (!_inWorld) { WeekendModal.Push(); _pushed = true; }
    }

    // Pops what this card pushed whatever else has happened to Instance in between. Guarding the pop on
    // "am I still the Instance" leaked a modal depth every time one card replaced another — the outgoing
    // card never popped, so the counter never came back to zero and the world stayed frozen behind the
    // panel that did pop. The depth counter in WeekendModal is what keeps the overlap honest; this only
    // has to be sure its own push is matched exactly once.
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_pushed) { WeekendModal.Pop(); _pushed = false; }
    }

    void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;

        bool wasInWorld = _inWorld;

        // Stand down as the Instance here rather than waiting for OnDestroy at the end of the frame: the
        // schedule refuses to open over a result card, so handing over while still holding Instance meant
        // the sheet silently never came back.
        if (Instance == this) Instance = null;
        Destroy(gameObject);
        if (!wasInWorld) WeekendScheduleUI.Open();
    }

    void OnGUI()
    {
        // Destroyed but not yet collected: draw nothing, and above all do not dismiss a second time.
        if (_dismissed) return;
        if (_activity == null) { Dismiss(); return; }

        if (_inWorld && Time.unscaledTime - _openedAt > InWorldSeconds) { Dismiss(); return; }
        if (!_inWorld) PixelGUI.Scrim(0.9f);

        EnsureStyles();

        var lines = Deltas();
        float w = Mathf.Min(Px(340f), Screen.width - Px(16f));

        // Measured, not budgeted. The meter rows are set in the data face, while the old height allowed a
        // flat Px(12) for each of them and a flat Px(74) for everything above; a card with three or more
        // meters on it ran its last rows out through the GOT IT button. What follows is the same
        // arithmetic the draw below does, in the same order.
        //
        // The frame is the one part still measured at the kit's scale: PixelGUI.Panel 9-slices it from the
        // kit's own upscaled sprite whatever size the card inside is, so the inset that clears it has to be
        // asked for in those pixels. Everything inboard of it is the card's.
        float pad = PixelGUI.Px(4f) + Px(8f);
        float contentW = w - pad * 2f;
        // The band is whatever the title needs, so a theme on a bigger cell opens it up instead of
        // trimming the type inside it.
        float bandH = Mathf.Max(Px(18f), _heading.fontSize + Px(6f));
        float rowH = _data.fontSize + Px(3f);
        float buttonH = Px(18f);
        float headlineH = string.IsNullOrEmpty(_outcome.headline)
            ? 0f
            : _body.CalcHeight(new GUIContent(_outcome.headline), contentW) + Px(5f);

        float h = pad * 2f
                  + bandH + Px(6f)                             // title band
                  + headlineH                                  // what the weekend will remember it by
                  + Px(4f)                                     // the rule under it
                  + Mathf.Max(lines.Count, 1) * rowH           // one line per meter that moved
                  + Px(6f) + buttonH;                          // and the way out
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        // In the world it sits low, out of the way of whoever is stood in front of you; as a modal it sits
        // where a modal sits.
        float y = _inWorld
            ? Mathf.Round(Screen.height - h - Px(18f))
            : Mathf.Round((Screen.height - h) * 0.4f);
        var outer = new Rect(x, y, w, h);

        PixelGUI.Panel(outer, focused: true);
        var c = new Rect(x + pad, y + pad, contentW, h - pad * 2f);

        PixelGUI.Fill(new Rect(c.x, c.y, c.width, bandH), PixelGUI.PlateLight);
        GUI.Label(new Rect(c.x + Px(4f), c.y, c.width - Px(8f), bandH), _activity.title, _heading);

        var meta = _dataDim;
        var prevAlign = meta.alignment;
        meta.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(c.x, c.y, c.width - Px(4f), bandH), Grade(_outcome.score), meta);
        meta.alignment = prevAlign;

        float cy = c.y + bandH + Px(6f);

        if (!string.IsNullOrEmpty(_outcome.headline))
        {
            var body = new GUIContent(_outcome.headline);
            float bh = _body.CalcHeight(body, c.width);
            GUI.Label(new Rect(c.x, cy, c.width, bh), body, _body);
            cy += bh + Px(5f);
        }

        PixelGUI.Fill(new Rect(c.x, cy, c.width, Px(1f)), PixelGUI.PlateLight);
        cy += Px(4f);

        if (lines.Count == 0)
        {
            GUI.Label(new Rect(c.x, cy, c.width, rowH), "Nothing moved.", _dataDim);
            cy += rowH;
        }
        else
        {
            foreach (var (label, value, colour) in lines)
            {
                GUI.Label(new Rect(c.x, cy, c.width, rowH), label, _dataDim);
                var prevColour = _data.normal.textColor;
                var prevAlignment = _data.alignment;
                _data.normal.textColor = colour;
                _data.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(c.x, cy, c.width, rowH), value, _data);
                _data.normal.textColor = prevColour;
                _data.alignment = prevAlignment;
                cy += rowH;
            }
        }

        if (PixelGUI.Button(new Rect(c.x, c.yMax - buttonH, c.width, buttonH),
                            _inWorld ? "GOT IT" : "BACK TO THE SCHEDULE") ||
            (Time.unscaledTime - _openedAt > 0.4f && ConfirmPressed()))
            Dismiss();
    }

    static bool ConfirmPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        return kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
    }

    List<(string label, string value, Color colour)> Deltas()
    {
        var lines = new List<(string, string, Color)>();
        var o = _outcome;

        if (o.money != 0)
            lines.Add((o.money > 0 ? "EARNED" : "COST",
                       (o.money > 0 ? "+" : "-") + PlayerWallet.Format(Mathf.Abs(o.money)),
                       o.money > 0 ? PixelGUI.Gold : PixelGUI.Danger));

        Add(lines, "FAN APPEAL", o.fanAppeal, 0.05f);
        Add(lines, "SPONSOR MOOD", o.sponsorMood, 0.5f);
        Add(lines, "TEAM MORALE", o.teamMorale, 0.5f);
        Add(lines, "PRESS", o.mediaStanding, 0.5f);
        if (o.setupGain > 0.001f)
            lines.Add(("SETUP", "+" + Mathf.RoundToInt(o.setupGain * 100f) + "%", PixelGUI.Info));
        if (!string.IsNullOrEmpty(o.rivalName) && Mathf.Abs(o.rivalDelta) > 0.5f)
            lines.Add((o.rivalName.ToUpperInvariant(),
                       (o.rivalDelta > 0f ? "+" : "") + Mathf.RoundToInt(o.rivalDelta),
                       o.rivalDelta > 0f ? PixelGUI.Confirm : PixelGUI.Danger));

        return lines;
    }

    static void Add(List<(string, string, Color)> into, string label, float value, float epsilon)
    {
        if (Mathf.Abs(value) < epsilon) return;
        string text = (value > 0f ? "+" : "") + (Mathf.Abs(value) < 10f ? value.ToString("0.0") : Mathf.RoundToInt(value).ToString());
        into.Add((label, text, value > 0f ? PixelGUI.Confirm : PixelGUI.Danger));
    }

    static string Grade(float score01) => score01 switch
    {
        >= 0.9f => "NAILED IT",
        >= 0.75f => "WELL DONE",
        >= 0.5f => "FINE",
        >= 0.25f => "SCRAPPY",
        _ => "ROUGH",
    };
}
