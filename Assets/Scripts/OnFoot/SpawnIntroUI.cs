using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Spawn-in presentation for the on-foot scene open. Two jobs:
//  1. A brief centred title card: "<Track Name> - <Spawn Point Label>" (e.g. "Watkins Glen - Paddock"),
//     fading in, holding, fading out.
//  2. Objective markers for key things (currently the parked car). While the target is on screen the
//     marker floats over it; once it's off screen the marker clamps to the screen edge with an arrow
//     pointing at the target, a live distance label ("100m") and an icon of the target's sprite —
//     for the car that's its actual paint scheme.
// Created by PitLaneStart at spawn; markers are removed as their objective completes (car entered).
public class SpawnIntroUI : MonoBehaviour
{
    [Header("Title Card")]
    public float titleFadeIn = 0.4f;
    public float titleHold = 2.6f;
    public float titleFadeOut = 0.9f;
    public int titleFontSize = 40;

    [Header("Markers")]
    [Tooltip("Distance (px) the edge-clamped marker sits in from the screen border.")]
    public float edgeMargin = 64f;
    [Tooltip("Icon size (px, longest side).")]
    public float iconSize = 44f;
    [Tooltip("Don't show a marker's distance label when the target is closer than this (m).")]
    public float distanceLabelMinMetres = 15f;

    [Header("Marker Fly-In")]
    [Tooltip("A marker starts oversized in the middle of the screen and flies out to its resting spot, to pull the eye toward the objective. Seconds that move takes.")]
    public float markerIntroTime = 0.7f;
    [Tooltip("Size multiplier at the start of the fly-out.")]
    public float markerIntroScale = 3f;

    class Marker
    {
        public Transform target;
        public Sprite icon;
        public float hideWithinMetres;
        public float introTimer;
    }

    // The one on screen. The weekend's objectives hang their markers on it rather than drawing a second
    // set of arrows over the top of these.
    public static SpawnIntroUI Instance { get; private set; }

    // What the card said when the scene opened — the track, and the day and time the weekend is at. Kept
    // apart from the live title because that gets reused as an objective banner, and "where am I and when
    // is it" is worth being able to ask for later.
    public string SpawnTitle { get; private set; }
    public string SpawnSubtitle { get; private set; }

    string _title;
    string _subtitle;
    float _titleTimer;
    Transform _player;
    readonly List<Marker> _markers = new();
    GUIStyle _titleStyle, _distStyle;
    Texture2D _px, _arrow;

    public static SpawnIntroUI Create(string title, Transform player, string subtitle = "")
    {
        var go = new GameObject("SpawnIntroUI");
        var ui = go.AddComponent<SpawnIntroUI>();
        ui._title = title;
        ui._subtitle = subtitle;
        ui.SpawnTitle = title;
        ui.SpawnSubtitle = subtitle;
        ui._player = player;
        Instance = ui;
        Debug.Log($"[SpawnIntro] \"{title}\"" + (string.IsNullOrEmpty(subtitle) ? "" : $" / \"{subtitle}\""));
        return ui;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void AddMarker(Transform target, Sprite icon, float hideWithinMetres = 0f)
    {
        if (target == null) return;
        _markers.Add(new Marker { target = target, icon = icon, hideWithinMetres = hideWithinMetres });
    }

    public void RemoveMarker(Transform target)
    {
        _markers.RemoveAll(m => m.target == target);
    }

    // Replay a marker's fly-in. Called when a beat ends and its objective becomes the live one, so the
    // eye gets pulled from the middle of the screen out to the direction the player has to walk.
    public void PulseMarker(Transform target)
    {
        for (int i = 0; i < _markers.Count; i++)
            if (_markers[i].target == target) _markers[i].introTimer = 0f;
    }

    // True while the card on screen is still playing. Anything that wants the banner for its own message
    // waits for this rather than cutting the card off mid-sentence — the first thing the player reads on
    // arriving should be where they are and what day it is.
    public bool TitleBusy => !string.IsNullOrEmpty(_title) &&
                             _titleTimer < titleFadeIn + titleHold + titleFadeOut;

    // Re-use the title card as an objective banner ("HEAD TO YOUR CAR") — same fade-in/hold/fade-out,
    // restarted from the top. Called when a beat finishes and the player needs pointing at the next thing.
    public void ShowTitle(string text, string subtitle = "")
    {
        _title = text;
        _subtitle = subtitle;
        _titleTimer = 0f;
    }

    // "WatkinsGlen" -> "Watkins Glen". For scene/asset names used as a title fallback.
    public static string Nicify(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    void Update()
    {
        _titleTimer += Time.unscaledDeltaTime; // title keeps its rhythm even if the game gets paused
        for (int i = 0; i < _markers.Count; i++) _markers[i].introTimer += Time.unscaledDeltaTime;
    }

    void OnGUI()
    {
        if (RacePauseMenu.IsPaused) return;
        EnsureAssets();
        DrawMarkers();
        DrawTitle();
    }

    void DrawTitle()
    {
        if (string.IsNullOrEmpty(_title)) return;
        float total = titleFadeIn + titleHold + titleFadeOut;
        if (_titleTimer >= total) return;

        float a;
        if (_titleTimer < titleFadeIn) a = _titleTimer / Mathf.Max(0.01f, titleFadeIn);
        else if (_titleTimer < titleFadeIn + titleHold) a = 1f;
        else a = 1f - (_titleTimer - titleFadeIn - titleHold) / Mathf.Max(0.01f, titleFadeOut);

        var rect = new Rect(0f, Screen.height * 0.30f - 30f, Screen.width, 60f);
        var shadow = rect; shadow.x += 2f; shadow.y += 2f;
        var prev = _titleStyle.normal.textColor;
        _titleStyle.normal.textColor = new Color(PixelGUI.Ink.r, PixelGUI.Ink.g, PixelGUI.Ink.b, 0.9f * a);
        GUI.Label(shadow, _title, _titleStyle);
        _titleStyle.normal.textColor = new Color(PixelGUI.Text.r, PixelGUI.Text.g, PixelGUI.Text.b, a);
        GUI.Label(rect, _title, _titleStyle);
        _titleStyle.normal.textColor = prev;

        // The line under the name: the day and the time you have arrived at. A weekend is a schedule, and
        // the card is the first place the player is told where they are in it.
        if (string.IsNullOrEmpty(_subtitle)) return;

        var under = new Rect(rect.x, rect.yMax - 6f, rect.width, 24f);
        var underShadow = under; underShadow.x += 2f; underShadow.y += 2f;
        var wasSub = _distStyle.normal.textColor;
        var wasAlign = _distStyle.alignment;
        _distStyle.alignment = TextAnchor.UpperCenter;
        _distStyle.normal.textColor = new Color(PixelGUI.Ink.r, PixelGUI.Ink.g, PixelGUI.Ink.b, 0.9f * a);
        GUI.Label(underShadow, _subtitle, _distStyle);
        _distStyle.normal.textColor = new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, a);
        GUI.Label(under, _subtitle, _distStyle);
        _distStyle.normal.textColor = wasSub;
        _distStyle.alignment = wasAlign;
    }

    void DrawMarkers()
    {
        var cam = Camera.main;
        if (cam == null || _player == null) return;

        for (int i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            if (m.target == null) continue;

            float dist = Vector2.Distance(_player.position, m.target.position);
            if (m.hideWithinMetres > 0f && dist < m.hideWithinMetres) continue;

            Vector3 sp = cam.WorldToScreenPoint(m.target.position);
            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y); // GUI space runs y-down

            bool onScreen = sp.z > 0f &&
                            gui.x >= edgeMargin && gui.x <= Screen.width - edgeMargin &&
                            gui.y >= edgeMargin && gui.y <= Screen.height - edgeMargin;

            // Where the marker lives once it has settled, plus the direction it points when edge-clamped.
            Vector2 rest;
            Vector2 dir = Vector2.zero;
            if (onScreen)
            {
                rest = new Vector2(gui.x, gui.y - iconSize);
            }
            else
            {
                // Clamp to the screen edge; the arrow points from the marker toward the real position.
                rest = new Vector2(
                    Mathf.Clamp(gui.x, edgeMargin, Screen.width - edgeMargin),
                    Mathf.Clamp(gui.y, edgeMargin, Screen.height - edgeMargin));
                dir = gui - rest;
                if (dir.sqrMagnitude < 1f) dir = Vector2.right;
            }

            // Fly-in: oversized in the middle of the screen, then shrinking and sliding out to `rest`.
            // Eased so it leaves the centre fast and settles softly at the edge.
            float t = markerIntroTime > 0f ? Mathf.Clamp01(m.introTimer / markerIntroTime) : 1f;
            float ease = 1f - (1f - t) * (1f - t);
            bool settled = t >= 1f;
            float scale = Mathf.Lerp(markerIntroScale, 1f, ease);
            Vector2 pos = settled
                ? rest
                : Vector2.Lerp(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), rest, ease);

            // Arrow and distance are read-at-a-glance detail — they'd only be clutter mid-flight.
            if (settled && dir != Vector2.zero)
            {
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // GUI degrees, y-down
                Vector2 arrowPos = pos + dir.normalized * (iconSize * 0.5f + 12f);
                var mtx = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, arrowPos);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(arrowPos.x - 11f, arrowPos.y - 11f, 22f, 22f), _arrow);
                GUI.matrix = mtx;
            }

            DrawIcon(pos, m.icon, scale);

            if (settled && (!onScreen || dist >= distanceLabelMinMetres))
                DrawDistance(new Vector2(pos.x, pos.y + iconSize * 0.75f), dist);
        }
        GUI.color = Color.white;
    }

    // Icon centred on pos, aspect kept, over a soft dark backing plate. `sizeMul` drives the fly-in.
    void DrawIcon(Vector2 pos, Sprite icon, float sizeMul = 1f)
    {
        float size = iconSize * sizeMul;
        float plate = size + 10f * sizeMul;
        // Opaque plate with a cream keyline, the kit's slot treatment: a translucent black square behind a
        // pixel icon picks up whatever is under it and the icon loses its edge.
        float b = PixelGUI.Px(1f);
        var plateRect = new Rect(pos.x - plate * 0.5f, pos.y - plate * 0.5f, plate, plate);
        PixelGUI.Fill(new Rect(plateRect.x - b, plateRect.y - b, plateRect.width + b * 2f, plateRect.height + b * 2f),
                      PixelGUI.Text);
        PixelGUI.Fill(plateRect, PixelGUI.PlateDeep);
        GUI.color = Color.white;

        if (icon == null) return;
        Rect tr = icon.textureRect;
        float scale = size / Mathf.Max(tr.width, tr.height);
        float w = tr.width * scale, h = tr.height * scale;
        Rect uv = new Rect(tr.x / icon.texture.width, tr.y / icon.texture.height,
                           tr.width / icon.texture.width, tr.height / icon.texture.height);
        GUI.DrawTextureWithTexCoords(new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h), icon.texture, uv);
    }

    void DrawDistance(Vector2 pos, float metres)
    {
        string text = $"{Mathf.RoundToInt(metres)}m";
        var r = new Rect(pos.x - 40f, pos.y, 80f, 20f);
        var shadow = r; shadow.x += PixelGUI.Px(1f); shadow.y += PixelGUI.Px(1f);
        var prev = _distStyle.normal.textColor;
        _distStyle.normal.textColor = PixelGUI.Ink;
        GUI.Label(shadow, text, _distStyle);
        _distStyle.normal.textColor = prev;
        GUI.Label(r, text, _distStyle);
    }

    void EnsureAssets()
    {
        if (_titleStyle == null)
        {
            // Silkscreen for the location title, VT323 for the distance readouts. Both sized off the kit's
            // cells rather than titleFontSize's raw pixels, so they stay crisp at any display scale.
            _titleStyle = new GUIStyle(PixelGUI.Heading)
            {
                fontSize = 16 * PixelGUI.Scale,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = PixelGUI.Text;
            _distStyle = new GUIStyle(PixelGUI.Data) { alignment = TextAnchor.MiddleCenter };
        }
        if (_px == null)
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }
        if (_arrow == null) _arrow = BuildArrowTexture();
    }

    // A small right-pointing triangle, white on transparent. Rotated at draw time.
    static Texture2D BuildArrowTexture()
    {
        const int n = 24;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // Triangle: base on the left edge, tip at the right middle.
                float fx = x / (n - 1f);
                float fy = Mathf.Abs(y - (n - 1) * 0.5f) / ((n - 1) * 0.5f); // 0 centre .. 1 edge
                bool inside = fy <= 1f - fx;
                tex.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Point;   // hard edges, like everything else drawn here
        return tex;
    }
}
