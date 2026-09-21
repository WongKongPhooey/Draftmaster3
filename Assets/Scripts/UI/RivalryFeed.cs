using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Sim;
using Draftmaster.Controls;

// On-screen feed for the driver-relationship system: contact toasts, standing changes ("X and Y are
// now RIVALS"), and payback declarations. Also holds a toggleable standings panel (default F4) listing
// the player's relationships. Created on demand by DriverRelationships.Ensure() calls — scene-local,
// so it dies with the race scene and never leaks into menus.
//
// Who sees which toast is RivalryFeedAudience's rule: driving, you only get the notices you are named
// in — two other drivers falling out somewhere in the field is not something a driver in the car would
// know about. Those field-wide notices are held back for the crew chief (CrewChiefController.IsCrewChief),
// whose job is the whole race, and they come off screen again the moment the player leaves the pit wall.
public class RivalryFeed : MonoBehaviour
{
    public static RivalryFeed Instance { get; private set; }

    [Tooltip("Seconds a toast stays readable before fading out.")]
    public float toastSeconds = 4.5f;
    [Tooltip("Max toasts shown at once.")]
    public int maxToasts = 5;
    [Tooltip("Contact toasts obey the audience rule (the player's own contacts, plus the rest of the field " +
             "while acting as crew chief). Turn off to show every contact in the field at all times.")]
    public bool playerContactsOnly = true;
    [Tooltip("Key toggling the relationship standings panel.")]
    public KeyCode standingsKey = KeyCode.F4;

    struct Toast
    {
        public string text;
        public Color color;
        public float bornAt;
        public bool chiefOnly;   // a notice about two other drivers, shown because the player is the chief
    }

    readonly List<Toast> _toasts = new();
    bool _showStandings;

    public bool StandingsOpen { get => _showStandings; set => _showStandings = value; }

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
        if (Input.GetKeyDown(standingsKey) || PadInput.PressedDriving(PadBindings.Rivalries))
            _showStandings = !_showStandings;
        bool chief = CrewChiefController.IsCrewChief;
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            // Stepping off the pit wall takes the field-wide notices with it, rather than leaving other
            // people's arguments fading over the windscreen for the next few seconds.
            if (Time.time - _toasts[i].bornAt > toastSeconds || (_toasts[i].chiefOnly && !chief))
                _toasts.RemoveAt(i);
        }
    }

    void OnContact(string striker, string victim, float severity)
    {
        bool playerInvolved = DriverRelationships.IsPlayerName(striker) || DriverRelationships.IsPlayerName(victim);
        if (playerContactsOnly && !Announces(playerInvolved)) return;
        string verb = severity > 0.55f ? "slams into" : "trades paint with";
        Push($"{striker} {verb} {victim}", playerInvolved ? PixelGUI.Gold : PixelGUI.TextDim,
             chiefOnly: playerContactsOnly && !playerInvolved);
    }

    void OnChanged(string a, string b, float value, float delta)
    {
        bool playerInvolved = DriverRelationships.IsPlayerName(a) || DriverRelationships.IsPlayerName(b);
        if (!Announces(playerInvolved)) return;

        // Announce only threshold crossings, not every nudge.
        float prev = value - delta;
        var was = DriverRelationships.StandingOf(prev);
        var now = DriverRelationships.StandingOf(value);
        if (was == now) return;

        switch (now)
        {
            case DriverRelationships.Standing.Furious:
                Push($"{a} is FURIOUS with {b}!", PixelGUI.Danger, chiefOnly: !playerInvolved);
                break;
            case DriverRelationships.Standing.Rival:
                if (was == DriverRelationships.Standing.Furious)
                    Push($"{a} and {b} are cooling off", PixelGUI.Info, chiefOnly: !playerInvolved);
                else
                    Push($"{a} and {b} are now RIVALS", PixelGUI.Gold, chiefOnly: !playerInvolved);
                break;
            case DriverRelationships.Standing.Ally:
                Push($"{a} and {b} are working together", PixelGUI.Confirm, chiefOnly: !playerInvolved);
                break;
            case DriverRelationships.Standing.Neutral:
                break;
        }
    }

    void OnPayback(string attacker, string target)
    {
        bool playerInvolved = DriverRelationships.IsPlayerName(attacker) || DriverRelationships.IsPlayerName(target);
        if (!Announces(playerInvolved)) return;
        Push($"{attacker} wants PAYBACK on {target}!", PixelGUI.Danger, chiefOnly: !playerInvolved);
    }

    // The audience rule, asked with the live crew-chief state.
    static bool Announces(bool playerInvolved)
        => RivalryFeedAudience.ShouldAnnounce(playerInvolved, CrewChiefController.IsCrewChief);

    void Push(string text, Color color, bool chiefOnly)
    {
        _toasts.Add(new Toast { text = text, color = color, bornAt = Time.time, chiefOnly = chiefOnly });
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
        PixelGUI.KeyTab(new Rect(x, y, w, h),
                        standingsKey == KeyCode.None ? "" : standingsKey.ToString(), PadBindings.Rivalries);
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
