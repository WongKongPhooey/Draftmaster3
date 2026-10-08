using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEngine;

// Draw each championship's safety cell - the roll cage that never folds - on top of one of its cars.
//
// Draftmaster > Art > Safety Cell Editor. Pick a series, and one of its liveries is drawn large with the cell over
// it: red is rigid (the cage), amber the band where the metal goes from rigid to foldable, clear is bodywork.
// Drag inside the box to move it, drag an edge or a corner to resize it, drag anywhere outside it to draw a new
// one. Save writes the box to SafetyCellShapes and bakes the series' mask, which VehicleDamage reads the next time
// a car's bodywork is built (enter play mode again to see it on track).
//
// The car is shown nose LEFT, as the liveries are drawn (sprite x 0 is the nose). "Mirror across the car" keeps
// the box centred door to door, which every real cage is.
public class SafetyCellEditorWindow : EditorWindow
{
    [MenuItem("Draftmaster/Art/Safety Cell Editor")]
    static void Open() => GetWindow<SafetyCellEditorWindow>("Safety Cell");

    // Car size the fractions are shown against, as VehicleCollision's default box (2 m x 4.8 m).
    const float CarLengthM = 4.8f;
    const float CarWidthM = 2f;
    const float HandlePx = 7f;
    const float MinHalf = 0.02f;

    static readonly (RacingSeries series, string prefix)[] Carsets =
    {
        (RacingSeries.Cup, "cup26"),
        (RacingSeries.National, "xfi25"),
        (RacingSeries.Trucks, "cts25"),
    };

    int _seriesIndex;
    readonly List<Sprite> _cars = new List<Sprite>();
    int _carIndex;
    SeriesSafetyCells.Cell _cell;
    SeriesSafetyCells.Cell _saved;
    bool _mirror = true;
    bool _loaded;

    Texture2D _overlay;
    bool _overlayDirty = true;

    enum Grab { None, Move, Draw, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }
    Grab _grab;
    Vector2 _grabStart;               // sprite fractions where the drag began
    SeriesSafetyCells.Cell _grabCell; // the cell when it began

    RacingSeries Series => Carsets[_seriesIndex].series;

    void OnEnable() => LoadSeries();

    void OnDisable()
    {
        if (_overlay != null) DestroyImmediate(_overlay);
    }

    void LoadSeries()
    {
        SeriesSafetyCells.ForgetCache();
        _cell = SeriesSafetyCells.ShapeOf(Series);
        _saved = _cell;
        _mirror = Mathf.Abs(_cell.centre.y - 0.5f) < 0.001f;
        _cars.Clear();
        string prefix = Carsets[_seriesIndex].prefix;
        for (int n = 0; n <= 99 && _cars.Count < 60; n++)
        {
            var spr = Resources.Load<Sprite>($"{prefix}livery{n}");
            if (spr != null) _cars.Add(spr);
        }
        _carIndex = Mathf.Clamp(_carIndex, 0, Mathf.Max(0, _cars.Count - 1));
        _overlayDirty = true;
        _loaded = true;
    }

    bool Dirty => !Same(_cell, _saved);

    static bool Same(in SeriesSafetyCells.Cell a, in SeriesSafetyCells.Cell b) =>
        a.centre == b.centre && a.half == b.half && Mathf.Approximately(a.corner, b.corner) && Mathf.Approximately(a.soft, b.soft);

    void OnGUI()
    {
        if (!_loaded) LoadSeries();

        // --- Toolbar: series, which car, save.
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var names = new string[Carsets.Length];
            for (int i = 0; i < Carsets.Length; i++) names[i] = SeriesCatalog.Nickname(Carsets[i].series);
            int pick = GUILayout.Toolbar(_seriesIndex, names, EditorStyles.toolbarButton, GUILayout.Width(300));
            if (pick != _seriesIndex)
            {
                if (!Dirty || EditorUtility.DisplayDialog("Safety Cell", $"Discard the unsaved {SeriesCatalog.Nickname(Series)} cell?", "Discard", "Keep editing"))
                {
                    _seriesIndex = pick;
                    LoadSeries();
                }
            }
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_cars.Count < 2))
            {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton)) _carIndex = (_carIndex + _cars.Count - 1) % _cars.Count;
                GUILayout.Label(_cars.Count > 0 ? $"car {_carIndex + 1} / {_cars.Count}" : "no liveries", EditorStyles.miniLabel);
                if (GUILayout.Button("▶", EditorStyles.toolbarButton)) _carIndex = (_carIndex + 1) % _cars.Count;
            }
        }

        EditorGUILayout.HelpBox("Red = roll cage (never folds). Amber = crumple band. Clear = bodywork. " +
                                "Drag inside the box to move it, drag an edge or corner to resize, drag outside it to draw a new box. " +
                                "Nose is on the LEFT.", MessageType.None);

        // --- The car, as large as the window allows at its own aspect.
        Sprite car = _cars.Count > 0 ? _cars[_carIndex] : null;
        float aspect = car != null ? car.rect.width / car.rect.height : 2f;
        Rect area = GUILayoutUtility.GetRect(10f, 10000f, 120f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        area = new RectOffset(16, 16, 12, 12).Remove(area);
        float w = Mathf.Min(area.width, area.height * aspect);
        float h = w / aspect;
        var carRect = new Rect(area.x + (area.width - w) * 0.5f, area.y + (area.height - h) * 0.5f, w, h);

        EditorGUI.DrawRect(carRect, new Color(0.18f, 0.18f, 0.18f));
        if (car != null)
        {
            var tex = car.texture;
            var uv = new Rect(car.textureRect.x / tex.width, car.textureRect.y / tex.height,
                              car.textureRect.width / tex.width, car.textureRect.height / tex.height);
            GUI.DrawTextureWithTexCoords(carRect, tex, uv, true);
        }

        if (_overlayDirty) RebuildOverlay();
        if (_overlay != null) GUI.DrawTexture(carRect, _overlay, ScaleMode.StretchToFill, true);

        Rect box = BoxRect(carRect);
        DrawOutline(box, new Color(1f, 0.25f, 0.2f), 2f);
        foreach (var hrect in HandleRects(box)) EditorGUI.DrawRect(hrect, Color.white);
        GUI.Label(new Rect(carRect.x, carRect.yMax + 2f, 60f, 16f), "◀ nose", EditorStyles.miniLabel);

        HandleMouse(carRect, box);

        // --- Numbers.
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                float x0 = _cell.centre.x - _cell.half.x, x1 = _cell.centre.x + _cell.half.x;
                float y0 = _cell.centre.y - _cell.half.y, y1 = _cell.centre.y + _cell.half.y;
                EditorGUILayout.MinMaxSlider("Along (nose → tail)", ref x0, ref x1, 0f, 1f);
                EditorGUILayout.MinMaxSlider("Across (door → door)", ref y0, ref y1, 0f, 1f);
                SetBox(x0, x1, y0, y1, keepMirror: true);
                _cell.corner = EditorGUILayout.Slider(new GUIContent("Corner squareness", "2 = ellipse, 12 = sharp rectangle"), _cell.corner, 2f, 12f);
                _cell.soft = EditorGUILayout.Slider(new GUIContent("Crumple band", "How far outside the cell (relative to its size) the metal goes from rigid to fully foldable"), _cell.soft, 0.02f, 1f);
                _mirror = EditorGUILayout.Toggle(new GUIContent("Mirror across the car", "Keep the box centred door to door"), _mirror);
                if (_mirror) _cell.centre.y = 0.5f;
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                SeriesSafetyCells.EndRoom(_cell, out float nose, out float tail);
                EditorGUILayout.LabelField("Cage", $"{_cell.half.x * 2f * CarLengthM:0.00} m long x {_cell.half.y * 2f * CarWidthM:0.00} m wide");
                EditorGUILayout.LabelField("Nose can fold", $"{nose * CarLengthM:0.00} m ({nose:P0})");
                EditorGUILayout.LabelField("Tail can fold", $"{tail * CarLengthM:0.00} m ({tail:P0})");
                EditorGUILayout.LabelField(" ", Dirty ? "unsaved changes" : "saved", EditorStyles.miniLabel);
            }
        }
        if (EditorGUI.EndChangeCheck()) _overlayDirty = true;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Built-in default")) { _cell = SeriesSafetyCells.DefaultShapeOf(Series); _overlayDirty = true; }
            using (new EditorGUI.DisabledScope(!Dirty))
            {
                if (GUILayout.Button("Revert")) { _cell = _saved; _overlayDirty = true; }
                if (GUILayout.Button($"Save {SeriesCatalog.Nickname(Series)} cell", GUILayout.Height(24))) Save();
            }
        }
        GUILayout.Space(4f);

        if (_overlayDirty) Repaint();
    }

    // ---- Mapping. Sprite fractions: x 0 at the left (nose), y 0 at the BOTTOM of the livery (texture space).

    static Vector2 ToFrac(Rect carRect, Vector2 mouse) =>
        new Vector2((mouse.x - carRect.x) / carRect.width, 1f - (mouse.y - carRect.y) / carRect.height);

    Rect BoxRect(Rect carRect)
    {
        float x0 = _cell.centre.x - _cell.half.x, x1 = _cell.centre.x + _cell.half.x;
        float y0 = _cell.centre.y - _cell.half.y, y1 = _cell.centre.y + _cell.half.y;
        return Rect.MinMaxRect(carRect.x + x0 * carRect.width, carRect.y + (1f - y1) * carRect.height,
                               carRect.x + x1 * carRect.width, carRect.y + (1f - y0) * carRect.height);
    }

    static Rect[] HandleRects(Rect b)
    {
        float s = HandlePx;
        Rect At(float x, float y) => new Rect(x - s * 0.5f, y - s * 0.5f, s, s);
        return new[]
        {
            At(b.xMin, b.yMin), At(b.xMax, b.yMin), At(b.xMin, b.yMax), At(b.xMax, b.yMax),
            At(b.center.x, b.yMin), At(b.center.x, b.yMax), At(b.xMin, b.center.y), At(b.xMax, b.center.y),
        };
    }

    static void DrawOutline(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.yMin, t, r.height), c);
    }

    Grab HitTest(Rect box, Vector2 m)
    {
        float s = HandlePx;
        bool nearL = Mathf.Abs(m.x - box.xMin) <= s, nearR = Mathf.Abs(m.x - box.xMax) <= s;
        bool nearT = Mathf.Abs(m.y - box.yMin) <= s, nearB = Mathf.Abs(m.y - box.yMax) <= s;
        bool inX = m.x > box.xMin - s && m.x < box.xMax + s, inY = m.y > box.yMin - s && m.y < box.yMax + s;
        if (nearL && nearT) return Grab.TopLeft;
        if (nearR && nearT) return Grab.TopRight;
        if (nearL && nearB) return Grab.BottomLeft;
        if (nearR && nearB) return Grab.BottomRight;
        if (nearL && inY) return Grab.Left;
        if (nearR && inY) return Grab.Right;
        if (nearT && inX) return Grab.Top;
        if (nearB && inX) return Grab.Bottom;
        if (box.Contains(m)) return Grab.Move;
        return Grab.Draw;
    }

    void HandleMouse(Rect carRect, Rect box)
    {
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Repaint)
        {
            // Cursor hints over the handles and the box.
            EditorGUIUtility.AddCursorRect(box, MouseCursor.MoveArrow);
            EditorGUIUtility.AddCursorRect(new Rect(box.xMin - HandlePx, box.yMin, HandlePx * 2f, box.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(box.xMax - HandlePx, box.yMin, HandlePx * 2f, box.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(box.xMin, box.yMin - HandlePx, box.width, HandlePx * 2f), MouseCursor.ResizeVertical);
            EditorGUIUtility.AddCursorRect(new Rect(box.xMin, box.yMax - HandlePx, box.width, HandlePx * 2f), MouseCursor.ResizeVertical);
        }

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button != 0 || !carRect.Contains(e.mousePosition)) break;
                _grab = HitTest(box, e.mousePosition);
                _grabStart = ToFrac(carRect, e.mousePosition);
                _grabCell = _cell;
                GUIUtility.hotControl = id;
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id || _grab == Grab.None) break;
                ApplyDrag(ToFrac(carRect, e.mousePosition));
                _overlayDirty = true;
                e.Use();
                Repaint();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                _grab = Grab.None;
                e.Use();
                break;
        }
    }

    void ApplyDrag(Vector2 f)
    {
        f.x = Mathf.Clamp01(f.x);
        f.y = Mathf.Clamp01(f.y);
        var c = _grabCell;
        float x0 = c.centre.x - c.half.x, x1 = c.centre.x + c.half.x;
        float y0 = c.centre.y - c.half.y, y1 = c.centre.y + c.half.y;
        Vector2 d = f - _grabStart;

        // Screen top is sprite y1 (y runs up in the texture).
        switch (_grab)
        {
            case Grab.Move:
                float dx = Mathf.Clamp(d.x, -x0, 1f - x1);
                float dy = _mirror ? 0f : Mathf.Clamp(d.y, -y0, 1f - y1);
                x0 += dx; x1 += dx; y0 += dy; y1 += dy;
                break;
            case Grab.Draw:
                x0 = Mathf.Min(_grabStart.x, f.x); x1 = Mathf.Max(_grabStart.x, f.x);
                y0 = Mathf.Min(_grabStart.y, f.y); y1 = Mathf.Max(_grabStart.y, f.y);
                if (_mirror) { float hy = Mathf.Max(Mathf.Abs(_grabStart.y - 0.5f), Mathf.Abs(f.y - 0.5f)); y0 = 0.5f - hy; y1 = 0.5f + hy; }
                break;
            default:
                if (_grab == Grab.Left || _grab == Grab.TopLeft || _grab == Grab.BottomLeft) x0 = Mathf.Min(f.x, x1 - 2f * MinHalf);
                if (_grab == Grab.Right || _grab == Grab.TopRight || _grab == Grab.BottomRight) x1 = Mathf.Max(f.x, x0 + 2f * MinHalf);
                if (_grab == Grab.Top || _grab == Grab.TopLeft || _grab == Grab.TopRight) y1 = Mathf.Max(f.y, y0 + 2f * MinHalf);
                if (_grab == Grab.Bottom || _grab == Grab.BottomLeft || _grab == Grab.BottomRight) y0 = Mathf.Min(f.y, y1 - 2f * MinHalf);
                if (_mirror && (_grab == Grab.Top || _grab == Grab.TopLeft || _grab == Grab.TopRight)) y0 = 1f - y1;
                if (_mirror && (_grab == Grab.Bottom || _grab == Grab.BottomLeft || _grab == Grab.BottomRight)) y1 = 1f - y0;
                break;
        }
        SetBox(x0, x1, y0, y1, keepMirror: false);
    }

    void SetBox(float x0, float x1, float y0, float y1, bool keepMirror)
    {
        if (x1 - x0 < 2f * MinHalf) x1 = x0 + 2f * MinHalf;
        if (y1 - y0 < 2f * MinHalf) y1 = y0 + 2f * MinHalf;
        _cell.centre = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        _cell.half = new Vector2((x1 - x0) * 0.5f, (y1 - y0) * 0.5f);
        if (_mirror)
        {
            // Centred door to door: keep the half-height, put the middle on the car's centreline.
            if (keepMirror) _cell.half.y = Mathf.Min(_cell.half.y, 0.5f);
            _cell.centre.y = 0.5f;
        }
    }

    // ---- The rigid/soft picture, from the same function the mask is baked from.
    void RebuildOverlay()
    {
        _overlayDirty = false;
        const int W = 160, H = 80;
        if (_overlay == null)
            _overlay = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float fy = (y + 0.5f) / H;
            for (int x = 0; x < W; x++)
            {
                float fx = (x + 0.5f) / W;
                float deform = SeriesSafetyCells.DeformAt(_cell, fx, fy);   // 0 rigid .. 1 bodywork
                Color c = deform <= 0.001f ? new Color(1f, 0.15f, 0.1f, 0.45f)
                        : deform >= 0.999f ? new Color(0f, 0f, 0f, 0f)
                        : new Color(1f, 0.7f, 0.1f, 0.4f * (1f - deform));
                px[y * W + x] = c;
            }
        }
        _overlay.SetPixels32(px);
        _overlay.Apply();
    }

    // ---- Save: the box into SafetyCellShapes, then the series' mask baked from it.
    void Save()
    {
        SafetyCellMaskBuilder.EnsureFolder();
        var asset = AssetDatabase.LoadAssetAtPath<SafetyCellShapes>(SafetyCellShapes.AssetPath);
        if (asset == null)
        {
            asset = CreateInstance<SafetyCellShapes>();
            AssetDatabase.CreateAsset(asset, SafetyCellShapes.AssetPath);
        }
        Undo.RecordObject(asset, "Safety cell");
        var e = asset.Find(Series);
        if (e == null) { e = new SafetyCellShapes.Entry { series = Series }; asset.entries.Add(e); }
        e.centre = _cell.centre;
        e.half = _cell.half;
        e.corner = _cell.corner;
        e.soft = _cell.soft;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        SeriesSafetyCells.ForgetCache();
        SafetyCellMaskBuilder.BuildOne(Series);
        SeriesSafetyCells.ForgetCache();
        _saved = _cell;
        Debug.Log($"Safety cell saved for {SeriesCatalog.Nickname(Series)}: centre {_cell.centre}, half {_cell.half}, " +
                  $"corner {_cell.corner:0.0}, band {_cell.soft:0.00}; mask baked to {SeriesSafetyCells.AssetPath(Series)}. " +
                  "Re-enter play mode to see it on the cars.");
    }
}
