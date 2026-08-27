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

        var lines = Deltas();
        float w = Mathf.Min(PixelGUI.Px(340f), Screen.width - PixelGUI.Px(16f));

        // Measured, not budgeted. The meter rows are set in the data face — 32px at the current 2x skin —
        // while the old height allowed Px(12), 24px, for each of them and a flat Px(74) for everything
        // above; a card with three or more meters on it ran its last rows out through the GOT IT button.
        // What follows is the same arithmetic the draw below does, in the same order.
        float pad = PixelGUI.Px(4f) + PixelGUI.Px(8f);   // what PanelContent(outer, 8f) takes off each side
        float contentW = w - pad * 2f;
        float bandH = PixelGUI.Px(18f);
        float rowH = PixelGUI.Data.fontSize + PixelGUI.Px(3f);
        float buttonH = PixelGUI.Px(18f);
        float headlineH = string.IsNullOrEmpty(_outcome.headline)
            ? 0f
            : PixelGUI.Body.CalcHeight(new GUIContent(_outcome.headline), contentW) + PixelGUI.Px(5f);

        float h = pad * 2f
                  + bandH + PixelGUI.Px(6f)                    // title band
                  + headlineH                                  // what the weekend will remember it by
                  + PixelGUI.Px(4f)                            // the rule under it
                  + Mathf.Max(lines.Count, 1) * rowH           // one line per meter that moved
                  + PixelGUI.Px(6f) + buttonH;                 // and the way out
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        // In the world it sits low, out of the way of whoever is stood in front of you; as a modal it sits
        // where a modal sits.
        float y = _inWorld
            ? Mathf.Round(Screen.height - h - PixelGUI.Px(18f))
            : Mathf.Round((Screen.height - h) * 0.4f);
        var outer = new Rect(x, y, w, h);

        PixelGUI.Panel(outer, focused: true);
        var c = PixelGUI.PanelContent(outer, 8f);

        PixelGUI.Fill(new Rect(c.x, c.y, c.width, bandH), PixelGUI.PlateLight);
        GUI.Label(new Rect(c.x + PixelGUI.Px(4f), c.y + PixelGUI.Px(4f), c.width, PixelGUI.Px(12f)),
                  _activity.title, PixelGUI.Heading);

        var meta = PixelGUI.DataDim;
        var prevAlign = meta.alignment;
        meta.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(c.x, c.y, c.width - PixelGUI.Px(4f), bandH), Grade(_outcome.score), meta);
        meta.alignment = prevAlign;

        float cy = c.y + bandH + PixelGUI.Px(6f);

        if (!string.IsNullOrEmpty(_outcome.headline))
        {
            var body = new GUIContent(_outcome.headline);
            float bh = PixelGUI.Body.CalcHeight(body, c.width);
            GUI.Label(new Rect(c.x, cy, c.width, bh), body, PixelGUI.Body);
            cy += bh + PixelGUI.Px(5f);
        }

        PixelGUI.Rule(c.x, cy, c.width);
        cy += PixelGUI.Px(4f);

        if (lines.Count == 0)
        {
            GUI.Label(new Rect(c.x, cy, c.width, rowH), "Nothing moved.", PixelGUI.DataDim);
            cy += rowH;
        }
        else
        {
            foreach (var (label, value, colour) in lines)
            {
                GUI.Label(new Rect(c.x, cy, c.width, rowH), label, PixelGUI.DataDim);
                var s = PixelGUI.Data;
                var pc = s.normal.textColor;
                var pa = s.alignment;
                s.normal.textColor = colour;
                s.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(c.x, cy, c.width, rowH), value, s);
                s.normal.textColor = pc;
                s.alignment = pa;
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
