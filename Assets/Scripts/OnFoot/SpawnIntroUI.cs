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

    class Marker
    {
        public Transform target;
        public Sprite icon;
        public float hideWithinMetres;
    }

    string _title;
    float _titleTimer;
    Transform _player;
    readonly List<Marker> _markers = new();
    GUIStyle _titleStyle, _distStyle;
    Texture2D _px, _arrow;

    public static SpawnIntroUI Create(string title, Transform player)
    {
        var go = new GameObject("SpawnIntroUI");
        var ui = go.AddComponent<SpawnIntroUI>();
        ui._title = title;
        ui._player = player;
        Debug.Log($"[SpawnIntro] \"{title}\"");
        return ui;
    }

    public void AddMarker(Transform target, Sprite icon, float hideWithinMetres = 0f)
    {
        if (target == null) return;
        _markers.Add(new Marker { target = target, icon = icon, hideWithinMetres = hideWithinMetres });
    }

    public void RemoveMarker(Transform target)
    {
        _markers.RemoveAll(m => m.target == target);
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
        _titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
        GUI.Label(shadow, _title, _titleStyle);
        _titleStyle.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.Label(rect, _title, _titleStyle);
        _titleStyle.normal.textColor = prev;
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

            if (onScreen)
            {
                DrawIcon(new Vector2(gui.x, gui.y - iconSize), m.icon);
                if (dist >= distanceLabelMinMetres)
                    DrawDistance(new Vector2(gui.x, gui.y - iconSize + iconSize * 0.75f), dist);
            }
            else
            {
                // Clamp to the screen edge; the arrow points from the marker toward the real position.
                Vector2 clamped = new Vector2(
                    Mathf.Clamp(gui.x, edgeMargin, Screen.width - edgeMargin),
                    Mathf.Clamp(gui.y, edgeMargin, Screen.height - edgeMargin));
                Vector2 dir = gui - clamped;
                if (dir.sqrMagnitude < 1f) dir = Vector2.right;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // GUI degrees, y-down

                // Arrow on the far side of the icon, pointing out toward the target.
                Vector2 arrowPos = clamped + dir.normalized * (iconSize * 0.5f + 12f);
                var mtx = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, arrowPos);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(arrowPos.x - 11f, arrowPos.y - 11f, 22f, 22f), _arrow);
                GUI.matrix = mtx;

                DrawIcon(clamped, m.icon);
                DrawDistance(new Vector2(clamped.x, clamped.y + iconSize * 0.75f), dist);
            }
        }
        GUI.color = Color.white;
    }

    // Icon centred on pos, aspect kept, over a soft dark backing plate.
    void DrawIcon(Vector2 pos, Sprite icon)
    {
        float plate = iconSize + 10f;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(pos.x - plate * 0.5f, pos.y - plate * 0.5f, plate, plate), _px);
        GUI.color = Color.white;

        if (icon == null) return;
        Rect tr = icon.textureRect;
        float scale = iconSize / Mathf.Max(tr.width, tr.height);
        float w = tr.width * scale, h = tr.height * scale;
        Rect uv = new Rect(tr.x / icon.texture.width, tr.y / icon.texture.height,
                           tr.width / icon.texture.width, tr.height / icon.texture.height);
        GUI.DrawTextureWithTexCoords(new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h), icon.texture, uv);
    }

    void DrawDistance(Vector2 pos, float metres)
    {
        string text = $"{Mathf.RoundToInt(metres)}m";
        var r = new Rect(pos.x - 40f, pos.y, 80f, 20f);
        var shadow = r; shadow.x += 1f; shadow.y += 1f;
        var prev = _distStyle.normal.textColor;
        _distStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(shadow, text, _distStyle);
        _distStyle.normal.textColor = prev;
        GUI.Label(r, text, _distStyle);
    }

    void EnsureAssets()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _distStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _distStyle.normal.textColor = Color.white;
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
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
