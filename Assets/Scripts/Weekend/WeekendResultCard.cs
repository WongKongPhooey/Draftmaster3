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

    // The skip summary rather than one activity's result: what skipping ahead walked past, listed above
    // the meters it moved, with no jump back to the sheet afterwards —
    // the skip has just taken the player back to their motorhome, and that is where the card leaves them.
    bool _summary;

    // A practice or qualifying session the player drove. Its card closes back onto the circuit rather than
    // onto the sheet: what comes next, and skipping ahead to it, is the phone's SCHEDULE app now.
    bool _drove;
    List<(string label, string value, Color colour)> _skippedRows;

    // Past this many, the skipped list folds its tail into one "+N MORE" row so the card stays on screen.
    const int MaxSkippedRows = 8;

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
    //
    // Not on a phone or tablet, though. The step down is about a card taking over a big monitor; a handset's
    // 1080 lines are a few centimetres tall, and a step below the kit there leaves the headline and the lines
    // that moved too small to read at arm's length. There the card is drawn at the kit's own scale.
    static int CardScale => PixelGUI.Handheld
        ? Mathf.Max(1, PixelGUI.Scale)
        : Mathf.Max(1, PixelGUI.Scale - 1);

    // A pixel measurement at the card's scale rather than the kit's.
    static float Px(float baseline) => baseline * CardScale;

    // The card as the Draftmaster3_UI_Unity design draws it (Layout/DaySummary_PC / DaySummary_Mobile,
    // 2026-11-11), in its kit pixels at the card's scale: a navy body 300 wide (330 on a phone) inside a 3px
    // gold frame, on a hard drop shadow 6px down and right. Across the top a 28px band, a shade lighter, with
    // a 3px dark rule along its foot and the title in cream monogram 10px in. Under it the headline, a 1px
    // rule, the rows on a 17px pitch (sand label left, coloured value right), and the red confirm plate: 32
    // tall in a 3px dark outline on a 4px shadow, with the confirm key's cap in front of the caption. Every
    // word on it is monogram Bold 16; only the keycap's letter is Silkscreen.
    const float CardW = 300f, CardWHandheld = 330f;
    const float FrameW = 3f, ShadowOffset = 6f;
    const float BandH = 28f, BandRuleH = 3f, Inset = 10f;
    const float HeadlineTop = 9f, HeadlineGap = 8f, RuleGap = 7f;
    const float RowH = 16f, RowPitch = 17f, RowsToButton = 11f;
    const float ButtonH = 32f, ButtonOutline = 3f, ButtonShadow = 4f, Bottom = 10f;
    const float KeyW = 14f, KeyH = 10f, KeyTop = 11f, KeyShadowDrop = 2f, KeyGap = 8f;

    static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
    static readonly Color ButtonRed = Hex(0xc4452fff);   // the design's main confirm plate

    static GUIStyle _title, _body, _row, _caption, _key;
    static int _stylesAtKitScale = -1, _stylesAtCardScale = -1;

    // The kit's monogram and Silkscreen faces with their type stepped down to the card's scale. Kit sizes are
    // always a whole multiple of PixelGUI.Scale, so the division is exact and the faces stay on their cells.
    // Overflow rather than clip: clipped IMGUI text drops any line that does not fit its rect whole, and the
    // design's 16px rows are exactly one monogram cell tall.
    static void EnsureStyles()
    {
        int kit = Mathf.Max(1, PixelGUI.Scale), card = CardScale;
        if (_title != null && _stylesAtKitScale == kit && _stylesAtCardScale == card) return;
        _stylesAtKitScale = kit;
        _stylesAtCardScale = card;

        GUIStyle Down(GUIStyle from, Color colour, TextAnchor anchor, bool wrap)
        {
            var st = new GUIStyle(from)
            {
                fontSize = Mathf.Max(8, from.fontSize * card / kit),
                alignment = anchor, wordWrap = wrap, clipping = TextClipping.Overflow, richText = false,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            st.normal.textColor = colour;
            st.hover.textColor = colour;
            return st;
        }

        _title = Down(PixelGUI.Data, DriveScoutStyle.Text, TextAnchor.MiddleLeft, false);
        _body = Down(PixelGUI.Data, DriveScoutStyle.Text, TextAnchor.UpperLeft, true);
        _row = Down(PixelGUI.Data, DriveScoutStyle.Label, TextAnchor.MiddleLeft, false);
        _caption = Down(PixelGUI.Data, DriveScoutStyle.Text, TextAnchor.MiddleLeft, false);
        _key = Down(PixelGUI.HeadingSmall, DriveScoutStyle.PickerPlate, TextAnchor.MiddleCenter, false);
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
        card._drove = a != null && a.IsOnTrack;
    }

    // What a skip ahead off the sheet cost. Modal, like a result read at the sheet, but dismissed into the
    // world rather than back onto the schedule.
    public static void ShowSkipSummary(WeekendSkip.Report report)
    {
        if (report == null) return;

        string to = report.endedWeekend ? "the end of the weekend" : WeekendSlots.Label(report.to).ToLowerInvariant();
        string headline = "Gave up the rest of " + WeekendSlots.Label(report.from).ToLowerInvariant() +
                          " and went back to the motorhome. Now " + to + ".";
        if (!report.SkippedAnything) headline += " Nothing on the sheet was missed.";

        var rows = new List<(string, string, Color)>();
        for (int i = 0; i < report.skipped.Count; i++)
        {
            if (i == MaxSkippedRows - 1 && report.skipped.Count > MaxSkippedRows)
            {
                rows.Add(("+" + (report.skipped.Count - i) + " More", "Skipped", DriveScoutStyle.Meta));
                break;
            }
            var a = report.skipped[i];
            string title = a.title.Length > 26 ? a.title.Substring(0, 26) : a.title;
            rows.Add((title, a.mandatory ? "No-Show" : "Skipped",
                      a.mandatory ? DriveScoutStyle.Bad : DriveScoutStyle.Meta));
        }

        var outcome = report.cost;
        outcome.headline = headline;
        Show(new WeekendActivity { title = "Skipped " + WeekendSlots.ShortLabel(report.from) }, outcome);
        Instance._summary = true;
        Instance._skippedRows = rows;
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
        // The phone is on-foot only, so the hint names it for when they are out of the car; pressing it from
        // the car does nothing, which is no worse than not pressing it.
        if (_drove)
            ControlHints.Show("weekend.phoneSchedule", WeekendScripts.PhoneKeyName(), InputGlyphs.PhonePad,
                              "What's next, or skip ahead: SCHEDULE on your phone", seconds: 8f, once: false,
                              onPress: () => PhoneUI.Open("schedule"), icon: PixelGUI.ActionIcon.Phone);
        else if (!wasInWorld && !_summary) WeekendScheduleUI.Open();
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
        float w = Mathf.Min(Px(PixelGUI.Handheld ? CardWHandheld : CardW),
                            Screen.width - Px(16f) - Px(ShadowOffset));
        float contentW = w - Px(Inset) * 2f;
        int rowCount = Mathf.Max(lines.Count, 1);

        // Measured, not budgeted: the same arithmetic the draw below does, in the same order, so a long
        // headline or a skip that walked past eight things pushes the button down rather than under the rows.
        float headlineH = string.IsNullOrEmpty(_outcome.headline)
            ? 0f
            : Mathf.Ceil(_body.CalcHeight(new GUIContent(_outcome.headline), contentW)) + Px(HeadlineGap);
        float h = Px(BandH)
                  + Px(HeadlineTop) + headlineH                     // what the weekend will remember it by
                  + Px(1f) + Px(RuleGap)                            // the rule under it
                  + Px(RowPitch) * (rowCount - 1) + Px(RowH)        // one line per meter that moved
                  + Px(RowsToButton) + Px(ButtonH) + Px(Bottom);    // and the way out
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        // In the world it sits low, out of the way of whoever is stood in front of you; as a modal it sits
        // where a modal sits.
        float y = _inWorld
            ? Mathf.Round(Screen.height - h - Px(ShadowOffset) - Px(18f))
            : Mathf.Round((Screen.height - h) * 0.4f);
        var card = new Rect(x, y, w, h);

        float frame = Px(FrameW), shadow = Px(ShadowOffset);
        PixelGUI.Fill(new Rect(card.x + shadow, card.y + shadow, card.width, card.height), DriveScoutStyle.HeaderRule);
        PixelGUI.Fill(new Rect(card.x - frame, card.y - frame, card.width + frame * 2f, card.height + frame * 2f),
                      DriveScoutStyle.Gold);
        PixelGUI.Fill(card, DriveScoutStyle.Panel);

        // Title band, with its dark rule along the foot.
        float bandInner = Px(BandH - BandRuleH);
        PixelGUI.Fill(new Rect(card.x, card.y, card.width, bandInner), DriveScoutStyle.HeaderBand);
        PixelGUI.Fill(new Rect(card.x, card.y + bandInner, card.width, Px(BandRuleH)), DriveScoutStyle.HeaderRule);
        GUI.Label(new Rect(card.x + Px(Inset), card.y, contentW, bandInner), _activity.title, _title);

        float cx = card.x + Px(Inset);
        float cy = card.y + Px(BandH) + Px(HeadlineTop);

        if (headlineH > 0f)
        {
            GUI.Label(new Rect(cx, cy, contentW, headlineH), _outcome.headline, _body);
            cy += headlineH;
        }

        PixelGUI.Fill(new Rect(cx, cy, contentW, Px(1f)), DriveScoutStyle.Edge);
        cy += Px(1f) + Px(RuleGap);

        if (lines.Count == 0)
        {
            _row.normal.textColor = DriveScoutStyle.Meta;
            GUI.Label(new Rect(cx, cy, contentW, Px(RowH)), "Nothing moved.", _row);
        }
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var (label, value, colour) = lines[i];
                var r = new Rect(cx, cy + Px(RowPitch) * i, contentW, Px(RowH));
                _row.normal.textColor = DriveScoutStyle.Label;
                _row.alignment = TextAnchor.MiddleLeft;
                GUI.Label(r, label, _row);
                _row.normal.textColor = colour;
                _row.alignment = TextAnchor.MiddleRight;
                GUI.Label(r, value, _row);
            }
            _row.alignment = TextAnchor.MiddleLeft;
        }

        var button = new Rect(cx, card.yMax - Px(Bottom) - Px(ButtonH), contentW, Px(ButtonH));
        if (ConfirmButton(button, _inWorld || _summary || _drove ? "Got It" : "Back to the Schedule") ||
            (Time.unscaledTime - _openedAt > 0.4f && ConfirmPressed()))
            Dismiss();
    }

    // The design's red plate: a 3px dark outline round it on a 4px hard shadow, and the caption in cream
    // monogram behind a keycap naming the confirm key - E on a keyboard, the pad's own button with a pad in
    // hand, and no cap at all on a phone, where the plate is tapped. Cap and caption are centred as one group.
    // Held, the face drops 2px, the kit's pressed state.
    static bool ConfirmButton(Rect r, string caption)
    {
        float outline = Px(ButtonOutline), drop = Px(ButtonShadow);
        PixelGUI.Fill(new Rect(r.x + drop, r.y + drop, r.width, r.height), DriveScoutStyle.HeaderRule);
        PixelGUI.Fill(new Rect(r.x - outline, r.y - outline, r.width + outline * 2f, r.height + outline * 2f),
                      DriveScoutStyle.HeaderRule);
        PixelGUI.Fill(r, ButtonRed);

        bool clicked = TouchTaps.Button(r, GUIContent.none, GUIStyle.none);
        bool held = Event.current != null && GUIUtility.hotControl != 0 && r.Contains(Event.current.mousePosition);
        float dy = held ? Px(2f) : 0f;

        bool phone = InputGlyphs.UsingTouch || (PixelGUI.Handheld && !InputGlyphs.UsingGamepad);
        string key = phone ? null : InputGlyphs.Confirm;
        float captionW = Mathf.Ceil(_caption.CalcSize(new GUIContent(caption)).x);
        float keyW = 0f;
        if (!string.IsNullOrEmpty(key))
            keyW = key == "E" ? Px(KeyW) : Mathf.Ceil(_key.CalcSize(new GUIContent(key)).x / Px(1f)) * Px(1f) + Px(8f);
        float groupW = captionW + (keyW > 0f ? keyW + Px(KeyGap) : 0f);
        float gx = Mathf.Round(r.x + (r.width - groupW) * 0.5f);

        if (keyW > 0f)
        {
            var cap = new Rect(gx, r.y + Px(KeyTop) + dy, keyW, Px(KeyH));
            PixelGUI.Fill(new Rect(cap.x, cap.y + Px(KeyShadowDrop), cap.width, cap.height), DriveScoutStyle.KeyShadow);
            PixelGUI.Fill(cap, DriveScoutStyle.Text);
            GUI.Label(cap, key, _key);
            gx += keyW + Px(KeyGap);
        }
        GUI.Label(new Rect(gx, r.y + dy, captionW + Px(2f), r.height), caption, _caption);
        return clicked;
    }

    static bool ConfirmPressed()
    {
        if (PadInput.WasPressed(Draftmaster.Controls.PadBindings.Confirm)) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        return kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
    }

    List<(string label, string value, Color colour)> Deltas()
    {
        var lines = new List<(string, string, Color)>();
        var o = _outcome;

        if (_skippedRows != null) lines.AddRange(_skippedRows);

        if (o.money != 0)
            lines.Add((o.money > 0 ? "Earned" : "Cost",
                       (o.money > 0 ? "+" : "-") + PlayerWallet.Format(Mathf.Abs(o.money)),
                       o.money > 0 ? DriveScoutStyle.Gold : DriveScoutStyle.Bad));

        Add(lines, "Fan Appeal", o.fanAppeal, 0.05f);
        Add(lines, "Sponsor Mood", o.sponsorMood, 0.5f);
        Add(lines, "Team Morale", o.teamMorale, 0.5f);
        Add(lines, "Press", o.mediaStanding, 0.5f);
        if (o.setupGain > 0.001f)
            lines.Add(("Setup", "+" + Mathf.RoundToInt(o.setupGain * 100f) + "%", PixelGUI.Info));
        if (!string.IsNullOrEmpty(o.rivalName) && Mathf.Abs(o.rivalDelta) > 0.5f)
            lines.Add((o.rivalName,
                       (o.rivalDelta > 0f ? "+" : "") + Mathf.RoundToInt(o.rivalDelta),
                       o.rivalDelta > 0f ? DriveScoutStyle.Good : DriveScoutStyle.Bad));

        return lines;
    }

    static void Add(List<(string, string, Color)> into, string label, float value, float epsilon)
    {
        if (Mathf.Abs(value) < epsilon) return;
        string text = (value > 0f ? "+" : "") + (Mathf.Abs(value) < 10f ? value.ToString("0.0") : Mathf.RoundToInt(value).ToString());
        into.Add((label, text, value > 0f ? DriveScoutStyle.Good : DriveScoutStyle.Bad));
    }
}
