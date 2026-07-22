using UnityEngine;

// On-screen running order from RacePositionTracker: P, car number, driver, gap to leader. Shows a compact
// top-N plus the player's own row by default; hold Tab for the full field.
public class LeaderboardUI : MonoBehaviour
{
    [Tooltip("Rows shown in the compact view.")]
    public int compactRows = 12;
    [Tooltip("Hold this key to expand to the full field.")]
    public KeyCode expandKey = KeyCode.Tab;
    [Tooltip("Press to show/hide the board. Persists across sessions.")]
    public KeyCode toggleKey = KeyCode.F2;
    public float rowHeight = 22f;
    public float width = 250f;
    public Vector2 origin = new Vector2(16f, 70f);

    GUIStyle _row, _head;
    static Texture2D _tex;
    DriveModeController _drive;
    bool _driveSearched;
    bool _visible = true;

    const string PrefKey = "hud.leaderboard";

    void Awake() => _visible = PlayerPrefs.GetInt(PrefKey, 1) == 1;

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        _visible = !_visible;
        PlayerPrefs.SetInt(PrefKey, _visible ? 1 : 0);
    }

    void EnsureStyles()
    {
        if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
        if (_row == null)
            _row = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 0, 0), normal = { textColor = Color.white } };
        if (_head == null)
            _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 0, 0), normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
    }

    void OnGUI()
    {
        if (!_visible) return;
        var t = RacePositionTracker.Instance;
        if (t == null || t.Order.Count == 0) return;
        EnsureStyles();

        // In broadcast mode the rows become camera buttons: click a driver to cut to their car.
        if (_drive == null && !_driveSearched) { _drive = FindFirstObjectByType<DriveModeController>(); _driveSearched = true; }
        bool broadcast = _drive != null && !_drive.IsDriving;
        Transform featured = broadcast ? _drive.FeaturedTransform : null;

        bool expanded = Input.GetKey(expandKey);
        var order = t.Order;
        int n = order.Count;
        int show = expanded ? n : Mathf.Min(compactRows, n);

        float x = origin.x, y = origin.y;
        float h = (show + 1) * rowHeight + 8f;
        Fill(new Rect(x - 4f, y - 4f, width + 8f, h + 8f), new Color(0f, 0f, 0f, 0.55f));

        string header = broadcast ? "ORDER — click = camera" : (expanded ? $"ORDER — {n} cars" : "ORDER (Tab = full)");
        GUI.Label(new Rect(x, y, width, rowHeight), header, _head);
        y += rowHeight;

        bool playerShown = false;
        for (int i = 0; i < show; i++)
        {
            var e = order[i];
            DrawRow(x, y, e, broadcast, featured);
            if (e.isPlayer) playerShown = true;
            y += rowHeight;
        }

        // Ensure the player's row is visible even when outside the compact window.
        if (!expanded && !playerShown && t.PlayerPosition > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (!order[i].isPlayer) continue;
                y += 4f;
                DrawRow(x, y, order[i], broadcast, featured);
                break;
            }
        }
    }

    void DrawRow(float x, float y, RacePositionTracker.Entry e, bool broadcast, Transform featured)
    {
        var rowRect = new Rect(x - 4f, y, width + 8f, rowHeight);
        if (e.isPlayer) Fill(rowRect, new Color(0.2f, 0.45f, 1f, 0.35f));
        else if (broadcast && featured != null && e.tf == featured) Fill(rowRect, new Color(1f, 0.3f, 0.2f, 0.35f));
        // Invisible button under the label so the whole row is clickable without changing its look.
        if (broadcast && e.tf != null && GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
            _drive.FeatureCar(e.tf);
        string num = e.carNumber > 0 ? $"#{e.carNumber}" : "";
        string gap = e.position == 1 ? "Leader" : $"+{e.gapToLeaderSec:0.0}s";
        GUI.Label(new Rect(x, y, width, rowHeight), $"{e.position,2}  {num,-4} {Trim(e.name, 12),-12} {gap}", e.isPlayer ? _head : _row);
    }

    static string Trim(string s, int len) => string.IsNullOrEmpty(s) ? "" : (s.Length <= len ? s : s.Substring(0, len));

    static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _tex);
        GUI.color = prev;
    }
}
