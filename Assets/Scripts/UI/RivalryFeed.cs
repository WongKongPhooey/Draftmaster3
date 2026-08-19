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
        Push($"{striker} {verb} {victim}", playerInvolved ? PixelGUI.Gold : PixelGUI.TextDim);
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
                Push($"{a} is FURIOUS with {b}!", PixelGUI.Danger);
                break;
            case DriverRelationships.Standing.Rival:
                if (was == DriverRelationships.Standing.Furious)
                    Push($"{a} and {b} are cooling off", PixelGUI.Info);
                else
                    Push($"{a} and {b} are now RIVALS", PixelGUI.Gold);
                break;
            case DriverRelationships.Standing.Ally:
                Push($"{a} and {b} are working together", PixelGUI.Confirm);
                break;
            case DriverRelationships.Standing.Neutral:
                break;
        }
    }

    void OnPayback(string attacker, string target)
    {
        Push($"{attacker} wants PAYBACK on {target}!", PixelGUI.Danger);
    }

    void Push(string text, Color color)
    {
        _toasts.Add(new Toast { text = text, color = color, bornAt = Time.time });
        while (_toasts.Count > maxToasts) _toasts.RemoveAt(0);
    }

    void OnGUI()
    {
        DrawToasts();
        if (_showStandings) DrawStandings();
    }

    // Toasts: one framed strip each, fading out as a whole. The kit forbids fading UI sprites, but these
    // are transient notices rather than furniture — a toast that vanished in one frame would read as a
    // glitch, so the fade stays and everything permanent on screen keeps its hard edges.
    void DrawToasts()
    {
        float w = PixelGUI.Px(260f), h = PixelGUI.Px(18f);
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = PixelGUI.Px(48f);
        for (int i = 0; i < _toasts.Count; i++)
        {
            var t = _toasts[i];
            float age = Time.time - t.bornAt;
            float alpha = Mathf.Clamp01((toastSeconds - age) / 1.2f);

            var prevGui = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            PixelGUI.Panel(new Rect(x, y, w, h + PixelGUI.Px(8f)));

            var style = PixelGUI.Data;
            var prevAlign = style.alignment;
            var prevColour = style.normal.textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(t.color.r, t.color.g, t.color.b, alpha);
            GUI.Label(new Rect(x, y + PixelGUI.Px(4f), w, h), t.text, style);
            style.alignment = prevAlign;
            style.normal.textColor = prevColour;
            GUI.color = prevGui;

            y += h + PixelGUI.Px(12f);
        }
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

        float row = PixelGUI.Px(14f);
        float w = PixelGUI.Px(168f);
        float h = PixelGUI.Px(28f) + Mathf.Max(rows.Count, 1) * row;
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(56f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 6f);

        GUI.Label(new Rect(c.x, c.y, c.width, PixelGUI.Px(10f)), "RELATIONSHIPS", PixelGUI.HeadingSmall);
        float ry = c.y + PixelGUI.Px(14f);
        if (rows.Count == 0)
        {
            GUI.Label(new Rect(c.x, ry, c.width, row), "NO HISTORY YET", PixelGUI.Row);
            return;
        }

        var style2 = PixelGUI.Data;
        var prev = style2.normal.textColor;
        foreach (var (other, value) in rows)
        {
            var standing = DriverRelationships.StandingOf(value);
            // Alarm red only for a driver actually out to get you; the merely annoyed get the accent.
            style2.normal.textColor = standing switch
            {
                DriverRelationships.Standing.Furious => PixelGUI.Danger,
                DriverRelationships.Standing.Rival => PixelGUI.Gold,
                DriverRelationships.Standing.Ally => PixelGUI.Confirm,
                _ => PixelGUI.Text,
            };
            GUI.Label(new Rect(c.x, ry, c.width, row),
                      $"{Cap(other),-12}{value,4:F0} {standing.ToString().ToUpperInvariant()}", style2);
            ry += row;
        }
        style2.normal.textColor = prev;
    }

    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

}
