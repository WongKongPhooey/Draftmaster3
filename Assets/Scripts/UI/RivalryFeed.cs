using System.Collections.Generic;
using UnityEngine;

// On-screen feed for the driver-relationship system: contact toasts, standing changes ("X and Y are
// now RIVALS"), and payback declarations. Also holds a toggleable standings panel (default F4) listing
// the player's relationships. Created on demand by DriverRelationships.Ensure() calls — scene-local,
// so it dies with the race scene and never leaks into menus.
public class RivalryFeed : MonoBehaviour
{
    public static RivalryFeed Instance { get; private set; }

    [Tooltip("Seconds a toast stays readable before fading out.")]
    public float toastSeconds = 4.5f;
    [Tooltip("Max toasts shown at once.")]
    public int maxToasts = 5;
    [Tooltip("Show contact toasts only when the player is involved (standing changes and paybacks always show).")]
    public bool playerContactsOnly = true;
    [Tooltip("Key toggling the relationship standings panel.")]
    public KeyCode standingsKey = KeyCode.F4;

    struct Toast
    {
        public string text;
        public Color color;
        public float bornAt;
    }

    readonly List<Toast> _toasts = new();
    bool _showStandings;
    GUIStyle _toastStyle, _headStyle, _rowStyle;

    public static void Ensure()
    {
        if (Instance != null) return;
        // Only bootstrap inside a race scene (same gate QuestHUD uses for gameplay scenes).
        if (RacePositionTracker.Instance == null) return;
        var go = new GameObject("RivalryFeed");
        Instance = go.AddComponent<RivalryFeed>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        DriverRelationships.ContactReported += OnContact;
        DriverRelationships.Changed += OnChanged;
        DriverRelationships.PaybackDeclared += OnPayback;
    }

    void OnDisable()
    {
        DriverRelationships.ContactReported -= OnContact;
        DriverRelationships.Changed -= OnChanged;
        DriverRelationships.PaybackDeclared -= OnPayback;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(standingsKey)) _showStandings = !_showStandings;
        for (int i = _toasts.Count - 1; i >= 0; i--)
            if (Time.time - _toasts[i].bornAt > toastSeconds) _toasts.RemoveAt(i);
    }

    void OnContact(string striker, string victim, float severity)
    {
        bool playerInvolved = DriverRelationships.IsPlayerName(striker) || DriverRelationships.IsPlayerName(victim);
        if (playerContactsOnly && !playerInvolved) return;
        string verb = severity > 0.55f ? "slams into" : "trades paint with";
        Push($"{striker} {verb} {victim}", playerInvolved ? new Color(1f, 0.8f, 0.4f) : new Color(0.85f, 0.85f, 0.85f));
    }

    void OnChanged(string a, string b, float value, float delta)
    {
        // Announce only threshold crossings, not every nudge.
        float prev = value - delta;
        var was = DriverRelationships.StandingOf(prev);
        var now = DriverRelationships.StandingOf(value);
        if (was == now) return;

        switch (now)
        {
            case DriverRelationships.Standing.Furious:
                Push($"{a} is FURIOUS with {b}!", new Color(1f, 0.3f, 0.25f));
                break;
            case DriverRelationships.Standing.Rival:
                if (was == DriverRelationships.Standing.Furious)
                    Push($"{a} and {b} are cooling off", new Color(0.7f, 0.8f, 1f));
                else
                    Push($"{a} and {b} are now RIVALS", new Color(1f, 0.55f, 0.3f));
                break;
            case DriverRelationships.Standing.Ally:
                Push($"{a} and {b} are working together", new Color(0.45f, 1f, 0.55f));
                break;
            case DriverRelationships.Standing.Neutral:
                break;
        }
    }

    void OnPayback(string attacker, string target)
    {
        Push($"{attacker} wants PAYBACK on {target}!", new Color(1f, 0.25f, 0.2f));
    }

    void Push(string text, Color color)
    {
        _toasts.Add(new Toast { text = text, color = color, bornAt = Time.time });
        while (_toasts.Count > maxToasts) _toasts.RemoveAt(0);
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToasts();
        if (_showStandings) DrawStandings();
    }

    void DrawToasts()
    {
        float w = 420f;
        float x = (Screen.width - w) * 0.5f;
        float y = 96f;
        for (int i = 0; i < _toasts.Count; i++)
        {
            var t = _toasts[i];
            float age = Time.time - t.bornAt;
            float alpha = Mathf.Clamp01((toastSeconds - age) / 1.2f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
            GUI.DrawTexture(new Rect(x, y, w, 24f), Texture2D.whiteTexture);
            GUI.color = new Color(t.color.r, t.color.g, t.color.b, alpha);
            GUI.Label(new Rect(x, y, w, 24f), t.text, _toastStyle);
            y += 27f;
        }
        GUI.color = Color.white;
    }

    // The player's relationships, worst first. F4 toggles.
    void DrawStandings()
    {
        var rows = new List<(string other, float value)>();
        var rt = RacePositionTracker.Instance;
        string player = (rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You").ToLowerInvariant();
        foreach (var (a, b, value) in DriverRelationships.AllPairs())
        {
            if (a == player) rows.Add((b, value));
            else if (b == player) rows.Add((a, value));
        }
        rows.Sort((p, q) => p.value.CompareTo(q.value));

        float w = 300f;
        float h = 56f + Mathf.Max(rows.Count, 1) * 20f;
        float x = Screen.width - w - 16f;
        float y = 110f;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 22f), "RELATIONSHIPS", _headStyle);
        float ry = y + 32f;
        if (rows.Count == 0)
        {
            GUI.Label(new Rect(x + 10f, ry, w - 20f, 20f), "No history yet — race clean or don't.", _rowStyle);
            return;
        }
        foreach (var (other, value) in rows)
        {
            var standing = DriverRelationships.StandingOf(value);
            Color c = standing switch
            {
                DriverRelationships.Standing.Furious => new Color(1f, 0.3f, 0.25f),
                DriverRelationships.Standing.Rival => new Color(1f, 0.6f, 0.3f),
                DriverRelationships.Standing.Ally => new Color(0.45f, 1f, 0.55f),
                _ => Color.white,
            };
            _rowStyle.normal.textColor = c;
            GUI.Label(new Rect(x + 10f, ry, w - 20f, 20f), $"{Cap(other),-16} {value,5:F0}  {standing}", _rowStyle);
            ry += 20f;
        }
        _rowStyle.normal.textColor = Color.white;
    }

    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    void EnsureStyles()
    {
        if (_toastStyle != null) return;
        _toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _headStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        _rowStyle.normal.textColor = Color.white;
    }
}
