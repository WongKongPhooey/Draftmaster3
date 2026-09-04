using System.Collections.Generic;
using UnityEngine;

// On-screen running order: P, car number, driver, gap to leader. Shows a compact top-N plus the player's
// own row by default; hold Tab for the full field.
//
// In a practice or qualifying session the rows come from LapTimingManager instead and are ranked by best
// lap, with the lap time in the right column: half the field is sat in its pit box at any moment, so
// track position — and any gap computed from it — is meaningless there. Best lap is what decides the
// session, and it's the same order PracticeDirector captures as the race grid.
//
// Drawn with the Iron Oval kit (PixelGUI): a framed plate, a Silkscreen header and VT323 rows, whose fixed
// advance is what keeps the four columns lined up without any tab work. Layout is in UI pixels scaled by
// PixelGUI.Scale, so the board is the same size relative to the screen at 1080p and 4K.
public class LeaderboardUI : MonoBehaviour
{
    [Tooltip("Rows shown in the compact view.")]
    public int compactRows = 12;
    [Tooltip("Hold this key to expand to the full field.")]
    public KeyCode expandKey = KeyCode.Tab;
    [Tooltip("Press to show/hide the board. Persists across sessions.")]
    public KeyCode toggleKey = KeyCode.F2;
    [Tooltip("Row height in UI pixels, before PixelGUI.Scale.")]
    public float rowHeight = 16f;
    [Tooltip("Board width in UI pixels, before PixelGUI.Scale.")]
    public float width = 148f;
    [Tooltip("Extra width in practice/qualifying, where the right column is a lap time rather than a gap.")]
    public float lapTimeExtraWidth = 26f;
    [Tooltip("Top-left corner in UI pixels, before PixelGUI.Scale.")]
    public Vector2 origin = new Vector2(8f, 36f);

    DriveModeController _drive;
    bool _driveSearched;
    bool _visible = true;

    // One drawn line, built either from the running order or from the timing rows.
    struct Row
    {
        public int position;
        public int carNumber;
        public string name;
        public string right;      // gap to leader, or best lap in a practice-like session
        public bool isPlayer;
        public Transform tf;
    }

    readonly List<Row> _rows = new();
    readonly List<LapTimingManager.CarTimes> _timing = new();

    const string PrefKey = "hud.leaderboard";

    void Awake() => _visible = PlayerPrefs.GetInt(PrefKey, 1) == 1;

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        _visible = !_visible;
        PlayerPrefs.SetInt(PrefKey, _visible ? 1 : 0);
    }

    void OnGUI()
    {
        if (!_visible) return;

        // The timing tower belongs to the player's own session. Another championship's cars are on track
        // for a good part of the weekend, and their running order is not something to draw over somebody
        // walking across the paddock.
        if (!RaceWeekend.SessionLive) return;

        bool byLap = RaceWeekend.IsPracticeLike && BuildFromLapTimes();
        if (!byLap && !BuildFromRunningOrder()) return;

        // In broadcast mode the rows become camera buttons: click a driver to cut to their car.
        if (_drive == null && !_driveSearched) { _drive = FindFirstObjectByType<DriveModeController>(); _driveSearched = true; }
        bool broadcast = _drive != null && !_drive.IsDriving;
        Transform featured = broadcast ? _drive.FeaturedTransform : null;

        bool expanded = Input.GetKey(expandKey);
        int n = _rows.Count;
        int show = expanded ? n : Mathf.Min(compactRows, n);

        float pad = PixelGUI.Px(6f);
        // Never tighter than a line of the face actually needs, whatever the inspector says.
        float row = Mathf.Max(PixelGUI.Px(rowHeight), PixelGUI.DataLineH);
        float w = PixelGUI.Px(width + (byLap ? lapTimeExtraWidth : 0f));
        float x = PixelGUI.Px(origin.x), y = PixelGUI.Px(origin.y);

        int playerRow = PlayerRowIndex();
        bool playerOutsideWindow = !expanded && playerRow >= show;
        float h = (show + 1) * row + pad * 2f + (playerOutsideWindow ? row + pad : 0f);
        PixelGUI.Panel(new Rect(x, y, w + pad * 2f, h));

        float rx = x + pad, ry = y + pad;
        GUI.Label(new Rect(rx, ry, w, row), Header(broadcast, byLap, expanded, n), PixelGUI.HeadingSmall);
        ry += row;

        for (int i = 0; i < show; i++)
        {
            DrawRow(rx, ry, w, row, _rows[i], broadcast, featured);
            ry += row;
        }

        // Keep the player's row on screen even when they are running outside the compact window.
        if (playerOutsideWindow)
        {
            PixelGUI.Rule(rx, ry + PixelGUI.Px(2f), w);
            DrawRow(rx, ry + pad, w, row, _rows[playerRow], broadcast, featured);
        }
    }

    string Header(bool broadcast, bool byLap, bool expanded, int n)
    {
        if (broadcast) return "ORDER · CLICK = CAMERA";
        if (byLap)
        {
            string session = RaceWeekend.IsQualifying ? "QUALIFYING" : "PRACTICE";
            return expanded ? $"{session} · {n} CARS" : $"{session} · BEST LAP";
        }
        return expanded ? $"ORDER · {n} CARS" : "ORDER · TAB = FULL";
    }

    // Practice/qualifying: rank on best lap, show the lap itself. False when timing isn't up yet, so the
    // caller falls back to the running order rather than drawing an empty plate.
    bool BuildFromLapTimes()
    {
        var lt = LapTimingManager.Instance;
        if (lt == null) return false;
        lt.RankByBest(_timing);
        if (_timing.Count == 0) return false;

        _rows.Clear();
        for (int i = 0; i < _timing.Count; i++)
        {
            var c = _timing[i];
            _rows.Add(new Row
            {
                position = i + 1,
                carNumber = c.carNumber,
                name = c.name,
                right = LapTimingManager.Format(c.bestLap),
                isPlayer = c.isPlayer,
                tf = c.tf,
            });
        }
        return true;
    }

    bool BuildFromRunningOrder()
    {
        var t = RacePositionTracker.Instance;
        if (t == null || t.Order.Count == 0) return false;

        _rows.Clear();
        var order = t.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            _rows.Add(new Row
            {
                position = e.position,
                carNumber = e.carNumber,
                name = e.name,
                right = e.position == 1 ? "LEADER" : $"+{e.gapToLeaderSec:0.0}",
                isPlayer = e.isPlayer,
                tf = e.tf,
            });
        }
        return true;
    }

    int PlayerRowIndex()
    {
        for (int i = 0; i < _rows.Count; i++) if (_rows[i].isPlayer) return i;
        return int.MaxValue;
    }

    void DrawRow(float x, float y, float w, float h, Row e, bool broadcast, Transform featured)
    {
        var rowRect = new Rect(x, y, w, h);
        // The kit keeps red for alarm, so the player's own row is marked with the accent and the camera's
        // current car with telemetry blue — both as a low-alpha band rather than a recoloured label.
        if (e.isPlayer) PixelGUI.Fill(rowRect, new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, 0.22f));
        else if (broadcast && featured != null && e.tf == featured)
            PixelGUI.Fill(rowRect, new Color(PixelGUI.Info.r, PixelGUI.Info.g, PixelGUI.Info.b, 0.30f));

        // Invisible button under the label so the whole row is clickable without changing its look.
        if (broadcast && e.tf != null && GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
            _drive.FeatureCar(e.tf);

        string num = e.carNumber > 0 ? $"#{e.carNumber}" : "";
        GUI.Label(new Rect(x + PixelGUI.Px(2f), y, w, h),
                  $"{e.position,2} {num,-4}{Trim(e.name, 11),-12}{e.right}",
                  e.isPlayer ? PixelGUI.Data : PixelGUI.Row);
    }

    static string Trim(string s, int len) => string.IsNullOrEmpty(s) ? "" : (s.Length <= len ? s : s.Substring(0, len));
}
