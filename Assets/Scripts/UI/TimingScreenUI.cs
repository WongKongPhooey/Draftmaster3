using System.Collections.Generic;
using UnityEngine;

// Full-field timing screen, opened from the crew-chief "Timing" button and on F11 in a grandstand. Rows come
// from LapTimingManager, ranked by best lap (cars with no time yet sort to the bottom, by laps run).
//
// Three columns: position, driver, best lap. It used to be seven (car number, laps, last lap and gap as
// well), laid out by padding one monospaced string — sixty-odd characters of the data face in a panel a
// third that wide, in rows shorter than the glyphs, so it spilled out of the frame on every side. Every
// size here is now measured off the face it is drawn in, the panel is exactly as wide as its widest line,
// and a field too long for the screen shows the top of the order plus the player's own row.
public class TimingScreenUI : MonoBehaviour
{
    public static TimingScreenUI Instance { get; private set; }

    public bool visible;

    [Tooltip("Overrides the session line. Empty = read off the player's own session (RaceWeekend). Set by " +
             "anything watching a session that is not the player's — a grandstand puts the championship " +
             "and the session it is timing here.")]
    public string sessionLabel = "";

    [Tooltip("Short status opposite the session line — the session clock, or what it finished as.")]
    public string statusLine = "";

    // Longest name drawn before it is cut. "B. Kowalczykowski" is the kind of thing a generated roster makes.
    const int NameChars = 16;
    const string Title = "LIVE TIMING";

    readonly List<LapTimingManager.CarTimes> _sorted = new();
    readonly List<int> _shown = new();

    public static TimingScreenUI Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("TimingScreenUI");
            Instance = go.AddComponent<TimingScreenUI>();
        }
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Toggle() { visible = !visible; }
    public void Hide() { visible = false; }

    void OnGUI()
    {
        if (!visible) return;
        var lt = LapTimingManager.Instance;
        if (lt == null) return;

        lt.RankByBest(_sorted);

        var head = PixelGUI.Heading;
        var dim = PixelGUI.LabelDim;
        var data = PixelGUI.Data;

        float headH = LineH(head);
        float dimH = LineH(dim);
        float rowH = LineH(data);
        float gap = PixelGUI.Px(8f);

        // Columns as wide as the widest thing that goes in them, header included.
        float posW = Mathf.Max(Width(data, "P" + Mathf.Max(10, _sorted.Count)), Width(dim, "POS"));
        float nameW = Mathf.Max(Width(data, new string('M', NameChars)), Width(dim, "DRIVER"));
        float bestW = Mathf.Max(Width(data, LapTimingManager.Format(-1f)), Width(dim, "BEST"));
        float tableW = posW + gap + nameW + gap + bestW;

        string session = !string.IsNullOrEmpty(sessionLabel)
            ? sessionLabel
            : RaceWeekend.IsQualifying ? "QUALIFYING" : (RaceWeekend.IsPractice ? "PRACTICE" : "RACE");
        float sessionW = Width(dim, session) + (string.IsNullOrEmpty(statusLine) ? 0f : gap + Width(dim, statusLine));

        float innerW = Mathf.Max(tableW, Mathf.Max(Width(head, Title), sessionW));

        const float PanelMargin = 8f;   // PanelContent's margin below
        float inset = PixelGUI.Px(4f) + PixelGUI.Px(PanelMargin);
        float edge = PixelGUI.Px(12f);
        float top = PixelGUI.Px(30f);   // clear of the HUD strip along the top

        float w = Mathf.Min(innerW + inset * 2f, Screen.width - edge * 2f);
        float chrome = inset * 2f + headH + dimH + dimH + PixelGUI.Px(4f);   // title, session, headers, rule

        // As many rows as the screen has room for.
        int fit = Mathf.Max(1, Mathf.FloorToInt((Screen.height - top - edge - chrome) / rowH));
        PickRows(fit);

        float h = chrome + _shown.Count * rowH;
        float x = edge;
        float y = Mathf.Max(top, Mathf.Round((Screen.height - h) * 0.35f));
        var box = new Rect(x, y, w, h);

        PixelGUI.Panel(box, focused: true);
        var c0 = PixelGUI.PanelContent(box, PanelMargin);
        float cx = c0.x, cy = c0.y, cw = c0.width;

        GUI.Label(new Rect(cx, cy, cw, headH), Title, head);
        cy += headH;

        // The session, and the clock opposite it — a compressed hour in a grandstand needs somewhere to say
        // how much of it is left.
        Draw(new Rect(cx, cy, cw, dimH), session, dim, null, TextAnchor.MiddleLeft);
        if (!string.IsNullOrEmpty(statusLine))
            Draw(new Rect(cx, cy, cw, dimH), statusLine, dim, null, TextAnchor.MiddleRight);
        cy += dimH;

        float nameX = cx + posW + gap;
        Draw(new Rect(cx, cy, posW, dimH), "POS", dim, null, TextAnchor.MiddleLeft);
        Draw(new Rect(nameX, cy, nameW, dimH), "DRIVER", dim, null, TextAnchor.MiddleLeft);
        Draw(new Rect(cx, cy, cw, dimH), "BEST", dim, null, TextAnchor.MiddleRight);
        cy += dimH;
        PixelGUI.Rule(cx, cy + PixelGUI.Px(1f), cw);
        cy += PixelGUI.Px(4f);

        for (int k = 0; k < _shown.Count; k++)
        {
            int i = _shown[k];
            var c = _sorted[i];

            // Purple-for-fastest is a broadcast idiom this palette has no room for, so the session's best
            // lap takes the one accent and the player's own row is the only other marked line.
            Color colour = i == 0 && c.bestLap > 0f ? PixelGUI.Gold
                         : c.isPlayer ? PixelGUI.Confirm
                         : PixelGUI.Text;

            string name = string.IsNullOrEmpty(c.name) ? "?"
                        : c.name.Length > NameChars ? c.name.Substring(0, NameChars) : c.name;

            Draw(new Rect(cx, cy, posW, rowH), "P" + (i + 1), data, colour, TextAnchor.MiddleLeft);
            Draw(new Rect(nameX, cy, nameW, rowH), name, data, colour, TextAnchor.MiddleLeft);
            Draw(new Rect(cx, cy, cw, rowH), LapTimingManager.Format(c.bestLap), data, colour, TextAnchor.MiddleRight);
            cy += rowH;
        }
    }

    // Which rows to draw: everybody if they fit, otherwise the front of the order with the player's own line
    // taking the last slot when they would have been cut off.
    void PickRows(int fit)
    {
        _shown.Clear();
        int n = _sorted.Count;
        if (n <= fit)
        {
            for (int i = 0; i < n; i++) _shown.Add(i);
            return;
        }

        int player = -1;
        for (int i = 0; i < n; i++)
            if (_sorted[i].isPlayer) { player = i; break; }

        bool playerCut = player >= fit;
        int front = playerCut ? fit - 1 : fit;
        for (int i = 0; i < front; i++) _shown.Add(i);
        if (playerCut) _shown.Add(player);
    }

    // One line of a face, leading included.
    static float LineH(GUIStyle s) => s.fontSize + PixelGUI.Px(3f);

    static float Width(GUIStyle s, string text) =>
        string.IsNullOrEmpty(text) ? 0f : Mathf.Ceil(s.CalcSize(new GUIContent(text)).x);

    static void Draw(Rect r, string text, GUIStyle style, Color? colour, TextAnchor align)
    {
        var wasColour = style.normal.textColor;
        var wasAlign = style.alignment;
        var wasWrap = style.wordWrap;
        if (colour.HasValue) style.normal.textColor = colour.Value;
        style.alignment = align;
        style.wordWrap = false;
        GUI.Label(r, text, style);
        style.normal.textColor = wasColour;
        style.alignment = wasAlign;
        style.wordWrap = wasWrap;
    }
}
